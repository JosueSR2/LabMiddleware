using System.Text.Json;
using Middleware_Core.Configuration;
using Middleware_Core.Models;
using Middleware_Core.Outbox;
using Middleware_Core.Parsers;
using Middleware_Core.Queue;

namespace Middleware_Core.Services
{
    public class IncomingMessageIngestionService
    {
        private readonly IncomingMessageQueue _queue;
        private readonly IOutboxRepository _outboxRepository;
        private readonly OperationalMetrics _metrics;
        private readonly MiddlewareOptions _options;

        public IncomingMessageIngestionService(
            IncomingMessageQueue queue,
            IOutboxRepository outboxRepository,
            OperationalMetrics metrics,
            MiddlewareOptions options)
        {
            _queue = queue;
            _outboxRepository = outboxRepository;
            _metrics = metrics;
            _options = options;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _queue.ReadAllAsync(cancellationToken))
            {
                var messageId = Guid.NewGuid().ToString("N");
                _metrics.IncrementIngress();

                try
                {
                    StructuredLog.Info("ingestion.received", new { messageId, analyzerId = message.ExternalId, source = message.Source });
                    var parser = ParserFactory.GetParser(string.Empty, message.RawMessage);
                    var results = parser.Parse(message.RawMessage);

                    var normalizedResults = results
                        .Select(result =>
                        {
                            var canonical = LabResultCanonicalNormalizer.Normalize(result, message.Source);
                            canonical.RawMessage = string.IsNullOrWhiteSpace(canonical.RawMessage)
                                ? message.RawMessage
                                : canonical.RawMessage;
                            canonical.AnalyzerId = string.IsNullOrWhiteSpace(canonical.AnalyzerId)
                                ? message.ExternalId ?? string.Empty
                                : canonical.AnalyzerId;
                            return canonical;
                        })
                        .ToList();

                    var useSingleRawMessageRecord = _options.LisDelivery.PayloadMode == LisPayloadMode.RawMessage &&
                                                    _options.LisDelivery.SendOneRecordPerIncomingMessage;

                    if (useSingleRawMessageRecord)
                    {
                        var envelope = normalizedResults.FirstOrDefault() ?? LabResultCanonicalNormalizer.Normalize(new LabResult(), message.Source);
                        envelope.RawMessage = message.RawMessage;
                        envelope.SourceMachine = string.IsNullOrWhiteSpace(message.Source) ? envelope.SourceMachine : message.Source;
                        envelope.AnalyzerId = string.IsNullOrWhiteSpace(envelope.AnalyzerId)
                            ? message.ExternalId ?? string.Empty
                            : envelope.AnalyzerId;

                        var fingerprint = MessageFingerprintService.BuildRawMessage(message.Source, message.ExternalId, message.RawMessage);
                        await TryInsertOutboxRecordAsync(messageId, message.ExternalId, envelope, fingerprint, cancellationToken);
                        continue;
                    }

                    foreach (var canonical in normalizedResults)
                    {
                        var fingerprint = MessageFingerprintService.Build(canonical);
                        await TryInsertOutboxRecordAsync(messageId, message.ExternalId, canonical, fingerprint, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    StructuredLog.Error("ingestion.error", new { messageId, analyzerId = message.ExternalId, error = ex.Message });
                }
            }
        }

        private async Task TryInsertOutboxRecordAsync(
            string messageId,
            string? analyzerId,
            LabResult canonical,
            string fingerprint,
            CancellationToken cancellationToken)
        {
            var payloadJson = JsonSerializer.Serialize(canonical);

            var added = await _outboxRepository.TryAddAsync(new OutboxRecord
            {
                Fingerprint = fingerprint,
                Payload = canonical,
                PayloadJson = payloadJson,
                Status = "Pending",
                RetryCount = 0,
                NextAttemptUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            }, cancellationToken);

            if (added)
            {
                _metrics.IncrementOutboxInserted();
                StructuredLog.Info("outbox.inserted", new { messageId, analyzerId, fingerprint });
            }
            else
            {
                _metrics.IncrementOutboxDuplicate();
                StructuredLog.Info("outbox.duplicate", new { messageId, analyzerId, fingerprint });
            }
        }
    }
}
