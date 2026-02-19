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

        public IncomingMessageIngestionService(IncomingMessageQueue queue, IOutboxRepository outboxRepository)
        {
            _queue = queue;
            _outboxRepository = outboxRepository;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _queue.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var parser = ParserFactory.GetParser(string.Empty, message.RawMessage);
                    var results = parser.Parse(message.RawMessage);

                    foreach (var result in results)
                    {
                        if (string.IsNullOrWhiteSpace(result.SourceMachine))
                            result.SourceMachine = message.Source;

                        var fingerprint = MessageFingerprintService.Build(result);
                        var payloadJson = JsonSerializer.Serialize(result);
                        var added = await _outboxRepository.TryAddAsync(new OutboxRecord
                        {
                            Fingerprint = fingerprint,
                            Payload = result,
                            PayloadJson = payloadJson,
                            Status = "Pending",
                            RetryCount = 0,
                            NextAttemptUtc = DateTime.UtcNow,
                            CreatedUtc = DateTime.UtcNow,
                            UpdatedUtc = DateTime.UtcNow
                        }, cancellationToken);

                        if (!added)
                        {
                            Console.WriteLine($"[OUTBOX] Duplicate ignored (fingerprint={fingerprint})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[INGESTION ERROR] {ex.Message}");
                }
            }
        }
    }
}
