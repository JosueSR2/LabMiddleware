namespace Middleware_Core.Outbox
{
    public interface IOutboxRepository
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<bool> TryAddAsync(OutboxRecord record, CancellationToken cancellationToken = default);
        Task<List<OutboxRecord>> GetDuePendingAsync(int limit, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task MarkSentAsync(string id, CancellationToken cancellationToken = default);
        Task MarkRetryAsync(string id, int retryCount, DateTime nextAttemptUtc, string? error, CancellationToken cancellationToken = default);
        Task MarkFailedAsync(string id, string? error, CancellationToken cancellationToken = default);
    }
}
