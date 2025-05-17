namespace AccessQueueService.Services
{
    public class AccessCleanupBackgroundService : BackgroundService
    {
        private readonly IAccessService _accessService;
        private readonly IConfiguration _config;
        private readonly ILogger<AccessCleanupBackgroundService> _logger;

        public AccessCleanupBackgroundService(IAccessService accessService, IConfiguration config, ILogger<AccessCleanupBackgroundService> logger)
        {
            _accessService = accessService;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cleanupIntervalMillis = _config.GetValue<int>("AccessQueue:CleanupIntervalSeconds") * 1000;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var removed = await _accessService.DeleteExpiredTickets();
                    if (removed > 0)
                    {
                        _logger.LogInformation("Background cleanup removed {Count} expired tickets.", removed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred during background cleanup.");
                }
                await Task.Delay(cleanupIntervalMillis, stoppingToken);
            }
        }
    }
}
