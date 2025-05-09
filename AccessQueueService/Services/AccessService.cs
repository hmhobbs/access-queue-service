using AccessQueueService.Data;
using AccessQueueService.Models;

namespace AccessQueueService.Services
{
    public class AccessService : IAccessService
    {
        private readonly IConfiguration _configuration;
        private readonly IAccessQueueRepo _accessQueueRepo;

        //private readonly Dictionary<Guid, AccessTicket> _accessTickets = new();
        //private readonly Queue<AccessTicket> _accessQueue = new();
        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private readonly int EXP_SECONDS;
        private readonly int ACT_SECONDS;
        private readonly int CAPACITY_LIMIT;
        private readonly bool ROLLING_EXPIRATION;
        public AccessService(IConfiguration configuration, IAccessQueueRepo accessQueueRepo)
        {
            _configuration = configuration;
            _accessQueueRepo = accessQueueRepo;
            EXP_SECONDS = _configuration.GetValue<int>("AccessQueue:ExpirationSeconds");
            ACT_SECONDS = _configuration.GetValue<int>("AccessQueue:ActivitySeconds");
            CAPACITY_LIMIT = _configuration.GetValue<int>("AccessQueue:CapacityLimit");
            ROLLING_EXPIRATION = _configuration.GetValue<bool>("AccessQueue:RollingExpiration");
        }
        public int UnexpiredTicketsCount => _accessQueueRepo.GetUnexpiredTicketsCount();
        public int ActiveTicketsCount => _accessQueueRepo.GetActiveTicketsCount(DateTime.UtcNow.AddSeconds(-_configuration.GetValue<int>("AccessQueue:ActivitySeconds")));
        public int QueueCount => _accessQueueRepo.GetQueueCount();
        public async Task<AccessResponse> RequestAccess(Guid userId)
        {
            await _queueLock.WaitAsync();
            try
            {
                var hasCapacity = !_accessQueueRepo.DidDequeueUntilFull(ACT_SECONDS, EXP_SECONDS, CAPACITY_LIMIT);
                var existingTicket = _accessQueueRepo.GetTicket(userId);
                if (existingTicket != null && existingTicket.ExpiresOn > DateTime.UtcNow)
                {
                    var expiresOn = existingTicket.ExpiresOn;
                    if (ROLLING_EXPIRATION)
                    {
                        expiresOn = DateTime.UtcNow.AddSeconds(EXP_SECONDS);
                    }
                    _accessQueueRepo.UpsertTicket(new AccessTicket
                    {
                        UserId = userId,
                        ExpiresOn = expiresOn,
                        LastActive = DateTime.UtcNow
                    });
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
                    _accessQueueRepo.UpsertTicket(accessTicket);
                    return new AccessResponse
                    {
                        ExpiresOn = accessTicket.ExpiresOn,
                    };
                }
                else
                {
                    var indexOfTicket = _accessQueueRepo.IndexOfTicket(userId);
                    var requestsAhead = _accessQueueRepo.GetQueueCount() - indexOfTicket - 1;
                    if (indexOfTicket == -1)
                    {
                        _accessQueueRepo.Enqueue(new AccessTicket
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
                return _accessQueueRepo.RemoveUser(userId);
            }
            finally
            {
                _queueLock.Release();
            }
        }

        public int DeleteExpiredTickets() => _accessQueueRepo.DeleteExpiredTickets();
    }
}
