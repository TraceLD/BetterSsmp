using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Ssmp
{
    public class CentralServerBackgroundService : BackgroundService
    {
        private CentralServerService _centralServerService;
        public CentralServerBackgroundService(CentralServerService centralServerService)
        {
            _centralServerService = centralServerService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) //TODO: Don't ignore the stopping token
        {
            
            while(!stoppingToken.IsCancellationRequested)
            {
               await _centralServerService.SpinOnce();
            }
        }

        public async Task LaunchForUnitTesting(CancellationToken token){
            await ExecuteAsync(token);
        }

    }
}