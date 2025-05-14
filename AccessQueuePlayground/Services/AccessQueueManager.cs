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
                Active = false,
            };
            return id;
        }

        public void SetUserActive(Guid userId, bool isActive)
        {
            var user = _users[userId];
            if (user != null)
            {
                user.Active = isActive;
            }
        }

        public async Task RecalculateStatus()
        {
            var userList = _users.Values.ToList();
            var newStatus = new AccessQueueStatus();
            foreach (var user in userList)
            {
                if (user.Active)
                {
                    user.LatestResponse = await _accessService.RequestAccess(user.Id);
                    if (user.LatestResponse?.HasAccess ?? false)
                    {
                        newStatus.AccessUsers.Add(user);
                    }
                    else
                    {
                        newStatus.QueuedUsers.Add(user);
                    }
                }
                else
                {
                    if(user.LatestResponse?.ExpiresOn != null && user.LatestResponse.ExpiresOn > DateTime.UtcNow)
                    {
                        newStatus.AccessUsers.Add(user);
                    }
                    else
                    {
                        newStatus.InactiveUsers.Add(user);
                    }
                }
            }
            newStatus.QueuedUsers.Sort((user1, user2) => user1.LatestResponse!.RequestsAhead - user2.LatestResponse!.RequestsAhead);
            _status = newStatus;
            NotifyStatusUpdated();
        }
    }
}
