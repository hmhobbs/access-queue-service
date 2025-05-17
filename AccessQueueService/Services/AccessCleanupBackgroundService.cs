
namespace AccessQueueService.Services
{
    public class AccessCleanupBackgroundService : BackgroundService
    {
        private readonly IAccessService _accessService;
        private readonly IConfiguration _config;

        public AccessCleanupBackgroundService(IAccessService accessService, IConfiguration config)
        {
            _accessService = accessService;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cleanupIntervalMillis = _config.GetValue<int>("AccessQueue:CleanupIntervalSeconds") * 1000;
            while (!stoppingToken.IsCancellationRequested)
            {
                await _accessService.DeleteExpiredTickets();
                await Task.Delay(cleanupIntervalMillis, stoppingToken);
            }
        }
    }
}
