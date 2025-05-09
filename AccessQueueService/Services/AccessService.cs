using AccessQueueService.Models;

namespace AccessQueueService.Services
{
    public class AccessService : IAccessService
    {
        private readonly Dictionary<Guid, AccessTicket> _accessTickets = new();
        private readonly Queue<AccessTicket> _accessQueue = new();
        private static SemaphoreSlim _queueLock = new(1, 1);
        private IConfiguration _configuration;
        private readonly int EXP_SECONDS;
        private readonly int ACT_SECONDS;
        private readonly int CAPACITY_LIMIT;
        private readonly bool ROLLING_EXPIRATION;
        public AccessService(IConfiguration configuration)
        {
            _configuration = configuration;
            EXP_SECONDS = _configuration.GetValue<int>("AccessQueue:ExpirationSeconds");
            ACT_SECONDS = _configuration.GetValue<int>("AccessQueue:ActivitySeconds");
            CAPACITY_LIMIT = _configuration.GetValue<int>("AccessQueue:CapacityLimit");
            ROLLING_EXPIRATION = _configuration.GetValue<bool>("AccessQueue:RollingExpiration");
        }
        public int UnexpiredTicketsCount => _accessTickets.Count(t => t.Value.ExpiresOn > DateTime.UtcNow);
        public int ActiveTicketsCount => _accessTickets.Count(t => t.Value.ExpiresOn > DateTime.UtcNow && t.Value.LastActive > DateTime.UtcNow.AddSeconds(-_configuration.GetValue<int>("AccessQueue:ActivitySeconds")));
        public int QueueCount => _accessQueue.Count;
        public async Task<AccessResponse> RequestAccess(Guid userId)
        {
            await _queueLock.WaitAsync();
            try
            {
                var hasCapacity = !DidDequeueUntilFull();
                var existingTicket = _accessTickets.GetValueOrDefault(userId);
                if (existingTicket != null && existingTicket.ExpiresOn > DateTime.UtcNow)
                {
                    var expiresOn = existingTicket.ExpiresOn;
                    if (ROLLING_EXPIRATION)
                    {
                        expiresOn = DateTime.UtcNow.AddSeconds(EXP_SECONDS);
                    }
                    _accessTickets[userId] = new AccessTicket
                    {
                        UserId = userId,
                        ExpiresOn = expiresOn,
                        LastActive = DateTime.UtcNow
                    };
                    return new AccessResponse
                    {
                        ExpiresOn = expiresOn
                    };
                }
                if (hasCapacity)
                {
                    var accessTicket = new AccessTicket
                    {
                        UserId = userId,
                        ExpiresOn = DateTime.UtcNow.AddSeconds(EXP_SECONDS),
                        LastActive = DateTime.UtcNow
                    };
                    _accessTickets[userId] = accessTicket;
                    return new AccessResponse
                    {
                        ExpiresOn = _accessTickets[userId].ExpiresOn,
                        RequestsAhead = _accessQueue.Count
                    };
                }
                else
                {
                    var indexOfTicket = IndexOfTicket(userId);
                    var requestsAhead = _accessQueue.Count - indexOfTicket - 1;
                    if (indexOfTicket == -1)
                    {
                        _accessQueue.Enqueue(new AccessTicket
                        {
                            UserId = userId,
                            LastActive = DateTime.UtcNow,
                            ExpiresOn = DateTime.MaxValue,
                        });
                    }
                    return new AccessResponse
                    {
                        ExpiresOn = null,
                        RequestsAhead = requestsAhead
                    };
                }
            }
            finally
            {
                _queueLock.Release();
            }
        }

        public async Task<bool> RevokeAccess(Guid userId)
        {
            await _queueLock.WaitAsync();
            try
            {
                return _accessTickets.Remove(userId);
            }
            finally
            {
                _queueLock.Release();
            }
        }

        public int DeleteExpiredTickets()
        {
            var expiredTickets = _accessTickets.Where(t => t.Value.ExpiresOn < DateTime.UtcNow);
            int count = 0;
            foreach (var ticket in expiredTickets)
            {
                count++;
                _accessTickets.Remove(ticket.Key);
            }
            return count;
        }

        private bool DidDequeueUntilFull()
        {
            var activeCutoff = DateTime.UtcNow.AddSeconds(-ACT_SECONDS);
            var numberOfActiveUsers = _accessTickets.Count(t => t.Value.ExpiresOn > DateTime.UtcNow && t.Value.LastActive > activeCutoff);
            var openSpots = CAPACITY_LIMIT - numberOfActiveUsers;
            int filledSpots = 0;
            while (filledSpots < openSpots)
            {
                if (_accessQueue.TryDequeue(out var nextUser))
                {
                    if (nextUser.LastActive < activeCutoff)
                    {
                        // User is inactive, throw away their ticket
                        continue;
                    }
                    _accessTickets[nextUser.UserId] = new AccessTicket
                    {
                        UserId = nextUser.UserId,
                        ExpiresOn = DateTime.UtcNow.AddSeconds(EXP_SECONDS),
                        LastActive = DateTime.UtcNow
                    };
                    filledSpots++;
                }
                else
                {
                    break;
                }
            }
            return filledSpots == openSpots;
        }

        private int IndexOfTicket(Guid userId)
        {
            var index = 0;
            foreach (var ticket in _accessQueue)
            {
                if (ticket.UserId == userId)
                {
                    return index;
                }
                index++;
            }
            return -1;
        }
    }
}
