namespace Middleware_Core.Outbox
{
    public interface IOutboxRepository
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<bool> TryAddAsync(OutboxRecord record, CancellationToken cancellationToken = default);
        Task<List<OutboxRecord>> GetDuePendingAsync(int limit, DateTime nowUtc, CancellationToken cancellationToken = default);
        Task<List<OutboxRecord>> GetByStatusAsync(string status, int limit, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);
        Task<int> RequeueAsync(string id, CancellationToken cancellationToken = default);
        Task<int> RequeueRangeAsync(DateTime fromUtc, DateTime toUtc, bool includeSent, CancellationToken cancellationToken = default);
        Task MarkSentAsync(string id, CancellationToken cancellationToken = default);
        Task MarkRetryAsync(string id, int retryCount, DateTime nextAttemptUtc, string? error, CancellationToken cancellationToken = default);
        Task MarkFailedAsync(string id, string? error, CancellationToken cancellationToken = default);
    }
}

