using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace AccessQueuePlayground.Services
{
    public class AccessQueueBackgroundService : BackgroundService
    {
        private readonly IAccessQueueManager _accessQueueManager;

        public AccessQueueBackgroundService(IAccessQueueManager accessQueueManager)
        {
            _accessQueueManager = accessQueueManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _accessQueueManager.RecalculateStatus();
                await Task.Delay(1000, stoppingToken); // Run every second
            }
        }
    }
}
