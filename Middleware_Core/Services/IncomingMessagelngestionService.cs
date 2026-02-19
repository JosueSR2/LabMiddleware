using System.Text.Json;
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

        public IncomingMessageIngestionService(IncomingMessageQueue queue, IOutboxRepository outboxRepository, OperationalMetrics metrics)
        {
            _queue = queue;
            _outboxRepository = outboxRepository;
            _metrics = metrics;
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

                    foreach (var result in results)
                    {
                        var canonical = LabResultCanonicalNormalizer.Normalize(result, message.Source);
                        var fingerprint = MessageFingerprintService.Build(canonical);
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
                            StructuredLog.Info("outbox.inserted", new { messageId, analyzerId = message.ExternalId, fingerprint });
                        }
                        else
                        {
                            _metrics.IncrementOutboxDuplicate();
                            StructuredLog.Info("outbox.duplicate", new { messageId, analyzerId = message.ExternalId, fingerprint });
                        }
                    }
                }
                catch (Exception ex)
                {
                    StructuredLog.Error("ingestion.error", new { messageId, analyzerId = message.ExternalId, error = ex.Message });
                }
            }
        }
    }
}