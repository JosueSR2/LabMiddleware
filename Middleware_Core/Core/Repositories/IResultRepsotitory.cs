using Middleware_Core.Models;

namespace Middleware_Core.Core.Repositories
{
    public interface IResultRepository
    {
        Task<List<LabResult>> GetPendingAsync();
        Task MarkAsSent(Guid id);
        Task IncrementRetry(Guid id);
    }
}
