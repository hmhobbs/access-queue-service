using AccessQueueService.Models;

namespace AccessQueueService.Services
{
    public interface IAccessService
    {
        public Task<AccessResponse> RequestAccess(Guid userId);
        public Task<bool> RevokeAccess(Guid userId);
        public int DeleteExpiredTickets();
    }
}
