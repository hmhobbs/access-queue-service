using AccessQueuePlayground.Models;

namespace AccessQueuePlayground.Services
{
    public interface IAccessQueueManager
    {
        public event Action? StatusUpdated;
        public Task RecalculateStatus();
        public AccessQueueStatus GetStatus();
        public Guid AddUser();
        public void SetUserActive(Guid userId, bool isActive);
        public void RevokeAccess(Guid userId);
        public void RevokeAllAccess();
        public void Reset();

    }
}
