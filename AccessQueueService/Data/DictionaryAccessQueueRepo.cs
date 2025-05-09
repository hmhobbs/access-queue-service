using AccessQueueService.Models;
using Microsoft.Extensions.Configuration;

namespace AccessQueueService.Data
{
    public class DictionaryAccessQueueRepo : IAccessQueueRepo
    {
        private readonly Dictionary<Guid, AccessTicket> _accessTickets = new();
        private readonly Queue<AccessTicket> _accessQueue = new();

        public int GetUnexpiredTicketsCount() => _accessTickets.Count(t => t.Value.ExpiresOn > DateTime.UtcNow);
        public int GetActiveTicketsCount(DateTime activeCutoff) => _accessTickets
            .Count(t => t.Value.ExpiresOn > DateTime.UtcNow && t.Value.LastActive >activeCutoff);
        public int GetQueueCount() => _accessQueue.Count;
        public int IndexOfTicket(Guid userId)
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

        public void Enqueue(AccessTicket ticket)
        {
            _accessQueue.Enqueue(ticket);
        }
        
        public int DeleteExpiredTickets()
        {
            var cutoff = DateTime.UtcNow;
            var expiredTickets = _accessTickets.Where(t => t.Value.ExpiresOn < cutoff);
            int count = 0;
            foreach (var ticket in expiredTickets)
            {
                count++;
                _accessTickets.Remove(ticket.Key);
            }
            return count;
        }

        public void RemoveUser(Guid userId)
        {
            _accessTickets.Remove(userId);
        }

        public bool DidDequeueUntilFull(int activeSeconds, int expirationSeconds, int capacityLimit)
        {
            var now = DateTime.UtcNow;
            var activeCutoff = now.AddSeconds(-activeSeconds);
            var numberOfActiveUsers = _accessTickets.Count(t => t.Value.ExpiresOn > now && t.Value.LastActive > activeCutoff);
            var openSpots = capacityLimit - numberOfActiveUsers;
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
                        ExpiresOn = now.AddSeconds(expirationSeconds),
                        LastActive = now
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

        public AccessTicket? GetTicket(Guid userId)
        {
            return _accessTickets.TryGetValue(userId, out var ticket) ? ticket : null;
        }

        public void UpsertTicket(AccessTicket ticket)
        {
            _accessTickets[ticket.UserId] = ticket;
        }

        bool IAccessQueueRepo.RemoveUser(Guid userId)
        {
            return _accessTickets.Remove(userId);
        }
    }
}
