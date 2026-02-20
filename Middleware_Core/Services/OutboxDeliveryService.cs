using System.Diagnostics;
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
        private readonly OperationalMetrics _metrics;

        public OutboxDeliveryService(IOutboxRepository outboxRepository, LisSenderService lisSender, MiddlewareOptions options, OperationalMetrics metrics)
        {
            _outboxRepository = outboxRepository;
            _lisSender = lisSender;
            _options = options;
            _metrics = metrics;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var records = await _outboxRepository.GetDuePendingAsync(_options.DeliveryBatchSize, DateTime.UtcNow, cancellationToken);

                foreach (var record in records)
                {
                    var sw = Stopwatch.StartNew();
                    var correlationId = Guid.NewGuid().ToString("N");

                    try
                    {
                        var payload = JsonSerializer.Deserialize<LabResult>(record.PayloadJson);
                        if (payload == null)
                        {
                            await _outboxRepository.MarkFailedAsync(record.Id, "Invalid payload JSON", cancellationToken);
                            _metrics.IncrementFailedPermanent();
                            StructuredLog.Error("delivery.invalid_payload", new { correlationId, messageId = record.Id });
                            continue;
                        }

                        var sendResult = await _lisSender.SendAsync(payload, _options.LisUrl, cancellationToken);
                        if (sendResult.Success)
                        {
                            await _outboxRepository.MarkSentAsync(record.Id, cancellationToken);
                            _metrics.IncrementLisSuccess();
                            StructuredLog.Info("delivery.sent", new { correlationId, messageId = record.Id, sampleId = payload.SampleId });
                            continue;
                        }

                        _metrics.IncrementLisFailure();
                        await ScheduleRetryAsync(record, sendResult.Error ?? "LIS delivery failed", cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _metrics.IncrementLisFailure();
                        await ScheduleRetryAsync(record, ex.Message, cancellationToken);
                    }
                    finally
                    {
                        sw.Stop();
                        _metrics.AddDeliveryLatency(sw.ElapsedMilliseconds);
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
                _metrics.IncrementFailedPermanent();
                StructuredLog.Error("delivery.failed_permanent", new { messageId = record.Id, error });
                return;
            }

            var waitSeconds = _options.RetryScheduleSeconds[nextRetryCount - 1];
            var nextAttempt = DateTime.UtcNow.AddSeconds(waitSeconds);
            await _outboxRepository.MarkRetryAsync(record.Id, nextRetryCount, nextAttempt, error, cancellationToken);
            _metrics.IncrementRetryScheduled();
            StructuredLog.Info("delivery.retry_scheduled", new { messageId = record.Id, nextRetryCount, waitSeconds, error });
        }
    }
}
