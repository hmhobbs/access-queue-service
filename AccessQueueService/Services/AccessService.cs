using System.Threading.Tasks;
using AccessQueueService.Data;
using AccessQueueService.Models;
using Microsoft.Extensions.Logging;

namespace AccessQueueService.Services
{
    public class AccessService : IAccessService
    {
        private readonly IConfiguration _configuration;
        private readonly IAccessQueueRepo _accessQueueRepo;
        private readonly ILogger<AccessService> _logger;

        private readonly SemaphoreSlim _queueLock = new(1, 1);
        private readonly int EXP_SECONDS;
        private readonly int ACT_SECONDS;
        private readonly int CAPACITY_LIMIT;
        private readonly bool ROLLING_EXPIRATION;
        public AccessService(IConfiguration configuration, IAccessQueueRepo accessQueueRepo, ILogger<AccessService> logger)
        {
            _configuration = configuration;
            _accessQueueRepo = accessQueueRepo;
            _logger = logger;
            EXP_SECONDS = _configuration.GetValue<int>("AccessQueue:ExpirationSeconds");
            ACT_SECONDS = _configuration.GetValue<int>("AccessQueue:ActivitySeconds");
            CAPACITY_LIMIT = _configuration.GetValue<int>("AccessQueue:CapacityLimit");
            ROLLING_EXPIRATION = _configuration.GetValue<bool>("AccessQueue:RollingExpiration");
        }
        public int UnexpiredTicketsCount => _accessQueueRepo.GetUnexpiredTicketsCount();
        public int ActiveTicketsCount => _accessQueueRepo.GetActiveTicketsCount(DateTime.UtcNow.AddSeconds(-_configuration.GetValue<int>("AccessQueue:ActivitySeconds")));
        public int QueueCount => _accessQueueRepo.GetQueueCount();
        public async Task<AccessResponse> RequestAccess(string userId)
        {
            await _queueLock.WaitAsync();
            try
            {
                var hasCapacity = !_accessQueueRepo.DidDequeueUntilFull(ACT_SECONDS, EXP_SECONDS, CAPACITY_LIMIT);
                var existingTicket = _accessQueueRepo.GetTicket(userId);
                if (existingTicket != null && existingTicket.ExpiresOn > DateTime.UtcNow)
                {
                    // Already has access
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
                    _logger.LogInformation("User {UserId} already has access. Expires on {ExpiresOn}.", userId, expiresOn);
                    return new AccessResponse
                    {
                        ExpiresOn = expiresOn
                    };
                }
                if (hasCapacity)
                {
                    // Doesn't have access, but there's space available
                    var accessTicket = new AccessTicket
                    {
                        UserId = userId,
                        ExpiresOn = DateTime.UtcNow.AddSeconds(EXP_SECONDS),
                        LastActive = DateTime.UtcNow
                    };
                    _accessQueueRepo.UpsertTicket(accessTicket);
                    _logger.LogInformation("User {UserId} granted access. Expires on {ExpiresOn}.", userId, accessTicket.ExpiresOn);
                    return new AccessResponse
                    {
                        ExpiresOn = accessTicket.ExpiresOn,
                    };
                }
                else
                {
                    // No access and no space, add to queue
                    var requestsAhead = _accessQueueRepo.GetRequestsAhead(userId);
                    if (requestsAhead == -1)
                    {
                        requestsAhead = _accessQueueRepo.GetQueueCount();
                        _accessQueueRepo.Enqueue(new AccessTicket
                        {
                            UserId = userId,
                            LastActive = DateTime.UtcNow,
                            ExpiresOn = DateTime.MaxValue,
                        });
                        _logger.LogInformation("User {UserId} added to queue. Requests ahead: {RequestsAhead}.", userId, requestsAhead);
                    }
                    return new AccessResponse
                    {
                        ExpiresOn = null,
                        RequestsAhead = requestsAhead
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while processing access request for user {UserId}.", userId);
                throw;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        public async Task<bool> RevokeAccess(string userId)
        {
            await _queueLock.WaitAsync();
            try
            {
                var removed = _accessQueueRepo.RemoveUser(userId);
                if (removed)
                {
                    _logger.LogInformation("User {UserId} access revoked.", userId);
                }
                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while revoking access for user {UserId}.", userId);
                throw;
            }
            finally
            {
                _queueLock.Release();
            }
        }

        public async Task<int> DeleteExpiredTickets()
        {
            await _queueLock.WaitAsync();
            try
            {
                var removed = _accessQueueRepo.DeleteExpiredTickets();
                if (removed > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired tickets.", removed);
                }
                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during expired ticket cleanup.");
                throw;
            }
            finally
            {
                _queueLock.Release();
            }
        }
    }
}
