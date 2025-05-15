using AccessQueueService.Models;
using Microsoft.Extensions.Configuration;

namespace AccessQueueService.Data
{
    public interface IAccessQueueRepo
    {
        public int GetUnexpiredTicketsCount();
        public int GetActiveTicketsCount(DateTime activeCutoff);
        public int GetQueueCount();
        public AccessTicket? GetTicket(string userId);
        public void UpsertTicket(AccessTicket ticket);
        public int GetRequestsAhead(string userId);
        public void Enqueue(AccessTicket ticket);
        public int DeleteExpiredTickets();
        public bool RemoveUser(string userId);
        public bool DidDequeueUntilFull(int activeSeconds, int expirationSeconds, int capacityLimit);



    }
}
