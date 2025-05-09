namespace AccessQueueService.Models
{
    public class AccessResponse
    {
        public DateTime? ExpiresOn { get; set; }
        public int RequestsAhead { get; set; } = 0;
        public bool HasAccess
        {
            get
            {
                return ExpiresOn != null && ExpiresOn > DateTime.UtcNow;
            }
        }
    }
}
