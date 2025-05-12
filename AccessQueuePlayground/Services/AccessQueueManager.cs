using System.Collections.Concurrent;
using AccessQueuePlayground.Models;
using AccessQueueService.Models;
using AccessQueueService.Services;

namespace AccessQueuePlayground.Services
{
    public class AccessQueueManager : IAccessQueueManager
    {
        private readonly IAccessService _accessService;
        private readonly ConcurrentDictionary<Guid, User> _users;
        private AccessQueueStatus _status;
        public event Action? StatusUpdated;

        private void NotifyStatusUpdated()
        {
            StatusUpdated?.Invoke();
        }

        public AccessQueueManager(IAccessService accessService)
        {
            _accessService = accessService;
            _users = new ConcurrentDictionary<Guid, User>();
            _status = new AccessQueueStatus();
        }

        public AccessQueueStatus GetStatus() => _status;

        public Guid AddUser()
        {
            var id = Guid.NewGuid();
            _users[id] = new User
            {
                Id = id,
                Active = true,
            };
            return id;
        }

        public void ToggleUserActivity(Guid userId)
        {
            var user = _users[userId];
            if (user != null)
            {
                user.Active = !user.Active;
            }
        }

        public async Task RecalculateStatus()
        {
            var userList = _users.Values.ToList();
            var newStatus = new AccessQueueStatus();
            foreach (var user in userList)
            {
                AccessResponse? response = user.LatestResponse;
                if (user.Active)
                {
                    response = await _accessService.RequestAccess(user.Id);
                    user.LatestResponse = response;
                }
                newStatus.Users.Add(user);
            }
            _status = newStatus;
            NotifyStatusUpdated();
        }
    }
}
