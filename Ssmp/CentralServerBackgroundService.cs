using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Ssmp.ConnectedClient;

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

    }
}