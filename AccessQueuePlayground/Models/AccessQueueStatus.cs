using AccessQueueService.Models;

namespace AccessQueuePlayground.Models
{
    public class AccessQueueStatus
    {
        public List<User> Users { get; set; } = [];
        public int QueueSize { get; set; }
        public int ActiveTickets { get; set; }
        public int UnexpiredTickets { get; set; }
    }
}
