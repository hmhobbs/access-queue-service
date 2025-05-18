using AccessQueuePlayground.Models;

namespace AccessQueuePlayground.Services
{
    public interface IAccessQueueManager
    {
        public event Action? StatusUpdated;
        public AccessQueueConfig GetConfig();
        public Task RecalculateStatus();
        public AccessQueueStatus GetStatus();
        public Guid AddUser(bool isActive);
        public void SetUserActive(Guid userId, bool isActive);
        public void RevokeAccess(Guid userId);
        public void RevokeAllAccess();
        public void Reset();

    }
}
