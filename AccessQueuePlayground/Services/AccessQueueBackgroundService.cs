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
                try
                {
                    await _accessQueueManager.RecalculateStatus();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                await Task.Delay(100, stoppingToken); // Run every second
            }
            Console.WriteLine("Stopping now because who tf knows why");
        }
    }
}
