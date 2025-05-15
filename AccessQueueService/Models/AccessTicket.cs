namespace AccessQueueService.Models
{
    public class AccessTicket
    {
        public string UserId { get; set; }
        public DateTime ExpiresOn { get; set; }
        public DateTime LastActive { get; set; }
    }
}
