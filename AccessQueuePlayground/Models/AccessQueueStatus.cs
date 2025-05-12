using AccessQueueService.Models;

namespace AccessQueuePlayground.Models
{
    public class AccessQueueStatus
    {
        public List<User> AccessUsers { get; set; } = [];
        public List<User> QueuedUsers { get; set; } = [];
        public List<User> InactiveUsers { get; set; } = [];
    }
}
