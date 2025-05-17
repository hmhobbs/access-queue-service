using AccessQueueService.Models;

namespace AccessQueueService.Services
{
    public interface IAccessService
    {
        public Task<AccessResponse> RequestAccess(string userId);
        public Task<bool> RevokeAccess(string userId);
        public Task<int> DeleteExpiredTickets();
    }
}
