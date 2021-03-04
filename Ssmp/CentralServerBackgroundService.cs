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
        private readonly ILogger<CentralServerBackgroundService> _logger;
        private Handler _handler;
        private CentralServerService _centralServerService;
        private int _messageQueueLimit;
        private readonly IPAddress _ipAddress;
        private readonly int _port;

        public CentralServerBackgroundService(CentralServerService centralServerService, ILogger<CentralServerBackgroundService> logger, Handler handler, int messageQueueLimit, IPAddress ipAddress, int port)
        {
            _centralServerService = centralServerService;
            _logger = logger;
            _handler = handler;
            _messageQueueLimit = messageQueueLimit;
            _ipAddress = ipAddress;
            _port = port;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) //TODO: Don't ignore the stopping token
        {
            var tasks = new List<Task>();

            var listener = new TcpListener(_ipAddress, _port);
            
            while(!stoppingToken.IsCancellationRequested)
            {
                tasks.Add(listener.AcceptTcpClientAsync());
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                if(completedTask is Task<TcpClient> newConnection)
                {
                    var client = ConnectedClient.Adopt(_handler, await newConnection, _messageQueueLimit);
                    _centralServerService._connectedClients = _centralServerService._connectedClients.Add(client);
                    tasks.Add(client.Spin());
                }
                else if(completedTask is Task<ConnectedClient> endedClient)
                {
                    _centralServerService._connectedClients = _centralServerService._connectedClients.Remove(await endedClient);
                }
                else
                {
                    await completedTask;
                }
            }
        }

    }
}