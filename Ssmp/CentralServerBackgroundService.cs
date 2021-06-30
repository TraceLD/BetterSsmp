using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ssmp
{
    public class CentralServerBackgroundService : BackgroundService
    {
        private readonly ILogger<CentralServerBackgroundService> _logger;
        private readonly CentralServerService _centralServerService;

        public CentralServerBackgroundService(
            ILogger<CentralServerBackgroundService> logger,
            CentralServerService centralServerService
        )
        {
            _logger = logger;
            _centralServerService = centralServerService;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.LogInformation("Started Ssmp.");
            
            while (!ct.IsCancellationRequested)
            {
                await _centralServerService.SpinOnce();
            }
            
            _logger.LogInformation("Stopped Ssmp.");
        }

        public async Task LaunchForUnitTesting(CancellationToken token)
        {
            await ExecuteAsync(token);
        }
    }
}