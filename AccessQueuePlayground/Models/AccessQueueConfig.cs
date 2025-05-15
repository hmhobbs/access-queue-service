namespace AccessQueuePlayground.Models
{
    public class AccessQueueConfig
    {
        public int ActivitySeconds { get; set; }
        public int ExpirationSeconds { get; set; }
        public int CapacityLimit { get; set; }

    }
}
