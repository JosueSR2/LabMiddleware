using System.Text.Json;
using Middleware_Core.Configuration;
using Middleware_Core.Models;
using Middleware_Core.Outbox;

namespace Middleware_Core.Services
{
    public class OutboxDeliveryService
    {
        private readonly IOutboxRepository _outboxRepository;
        private readonly LisSenderService _lisSender;
        private readonly MiddlewareOptions _options;

        public OutboxDeliveryService(IOutboxRepository outboxRepository, LisSenderService lisSender, MiddlewareOptions options)
        {
            _outboxRepository = outboxRepository;
            _lisSender = lisSender;
            _options = options;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var records = await _outboxRepository.GetDuePendingAsync(_options.DeliveryBatchSize, DateTime.UtcNow, cancellationToken);

                foreach (var record in records)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<LabResult>(record.PayloadJson);
                        if (payload == null)
                        {
                            await _outboxRepository.MarkFailedAsync(record.Id, "Invalid payload JSON", cancellationToken);
                            continue;
                        }

                        var sent = await _lisSender.SendAsync(payload, _options.LisUrl);
                        if (sent)
                        {
                            await _outboxRepository.MarkSentAsync(record.Id, cancellationToken);
                            continue;
                        }

                        await ScheduleRetryAsync(record, "LIS returned non-success status", cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await ScheduleRetryAsync(record, ex.Message, cancellationToken);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        private async Task ScheduleRetryAsync(OutboxRecord record, string? error, CancellationToken cancellationToken)
        {
            var nextRetryCount = record.RetryCount + 1;
            if (nextRetryCount > _options.RetryScheduleSeconds.Count)
            {
                await _outboxRepository.MarkFailedAsync(record.Id, error, cancellationToken);
                return;
            }

            var waitSeconds = _options.RetryScheduleSeconds[nextRetryCount - 1];
            var nextAttempt = DateTime.UtcNow.AddSeconds(waitSeconds);
            await _outboxRepository.MarkRetryAsync(record.Id, nextRetryCount, nextAttempt, error, cancellationToken);
        }
    }
}
