using Middleware.Core.Core.Models;

namespace Middleware.Core.Core.Repositories
{
    public interface IResultRepository
    {
        Task<List<LabResult>> GetPendingAsync();
        Task MarkAsSent(Guid id);
        Task IncrementRetry(Guid id);
    }
}
