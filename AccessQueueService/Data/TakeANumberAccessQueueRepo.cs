using AccessQueueService.Models;
using Microsoft.Extensions.Configuration;

namespace AccessQueueService.Data
{
    public class TakeANumberAccessQueueRepo : IAccessQueueRepo
    {
        private readonly Dictionary<Guid, AccessTicket> _accessTickets = [];
        private readonly Dictionary<Guid, ulong> _queueNumbers = [];
        private readonly Dictionary<ulong, AccessTicket> _accessQueue = [];

        private ulong _nowServing = 0;
        private ulong _nextUnusedTicket = 0; 

        public int GetUnexpiredTicketsCount() => _accessTickets.Count(t => t.Value.ExpiresOn > DateTime.UtcNow);
        public int GetActiveTicketsCount(DateTime activeCutoff) => _accessTickets
            .Count(t => t.Value.ExpiresOn > DateTime.UtcNow && t.Value.LastActive > activeCutoff);
        public int GetQueueCount() => (int)(_nextUnusedTicket - _nowServing);
        public int GetRequestsAhead(Guid userId)
        {
            if(_queueNumbers.TryGetValue(userId, out var queueNumber))
            {
                if(_accessQueue.TryGetValue(queueNumber, out var ticket))
                {
                    ticket.LastActive = DateTime.UtcNow;
                    return queueNumber >= _nowServing ? (int)(queueNumber - _nowServing) : -1;
                }
            }
            return -1;
            
        }

        public void Enqueue(AccessTicket ticket)
        {
            _queueNumbers[ticket.UserId] = _nextUnusedTicket;
            _accessQueue[_nextUnusedTicket] = ticket;
            _nextUnusedTicket++;
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
            if(openSpots <= 0)
            {
                return true;
            }
            int filledSpots = 0;
            while (filledSpots < openSpots && _nowServing < _nextUnusedTicket)
            {
                if (_accessQueue.TryGetValue(_nowServing, out var nextUser))
                {
                    _accessQueue.Remove(_nowServing);
                    _queueNumbers.Remove(nextUser.UserId);
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
                _nowServing++;
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
            if(_queueNumbers.TryGetValue(userId, out var queueNumber))
            {
                _accessQueue.Remove(queueNumber);
                _queueNumbers.Remove(userId);
            }
            return _accessTickets.Remove(userId);
        }
    }
}
