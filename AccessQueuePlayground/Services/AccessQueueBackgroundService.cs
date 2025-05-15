using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace AccessQueuePlayground.Services
{
    public class AccessQueueBackgroundService : BackgroundService
    {
        private readonly IAccessQueueManager _accessQueueManager;
        private readonly IConfiguration _config;

        public AccessQueueBackgroundService(IAccessQueueManager accessQueueManager, IConfiguration config)
        {
            _accessQueueManager = accessQueueManager;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int refreshRate = _config.GetValue<int>("AccessQueuePlayground:RefreshRateMilliseconds");
            while (!stoppingToken.IsCancellationRequested)
            {
                await _accessQueueManager.RecalculateStatus();
                await Task.Delay(refreshRate, stoppingToken);
            }
        }
    }
}
