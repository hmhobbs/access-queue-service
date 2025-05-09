using AccessQueueService.Models;
using Microsoft.Extensions.Configuration;

namespace AccessQueueService.Data
{
    public interface IAccessQueueRepo
    {
        public int GetUnexpiredTicketsCount();
        public int GetActiveTicketsCount(DateTime activeCutoff);
        public int GetQueueCount();
        public AccessTicket? GetTicket(Guid userId);
        public void UpsertTicket(AccessTicket ticket);
        public int IndexOfTicket(Guid userId);
        public void Enqueue(AccessTicket ticket);
        public int DeleteExpiredTickets();
        public bool RemoveUser(Guid userId);
        public bool DidDequeueUntilFull(int activeSeconds, int expirationSeconds, int capacityLimit);



    }
}
