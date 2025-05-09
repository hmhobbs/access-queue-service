namespace AccessQueueService.Models
{
    public class AccessTicket
    {
        public Guid UserId { get; set; }
        public DateTime ExpiresOn { get; set; }
        public DateTime LastActive { get; set; }
    }
}
