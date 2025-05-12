using AccessQueueService.Models;

namespace AccessQueuePlayground.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public bool Active { get; set; }
        public AccessResponse? LatestResponse { get; set; }
    }
}
