using System.Collections.Immutable;
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
    public class CentralServerService : BackgroundService
    {
        private readonly ILogger<CentralServerService> _logger;
        private Handler _handler;
        private volatile ImmutableList<ConnectedClient> _connectedClients;
        private int _messageQueueLimit;
        private readonly IPAddress _ipAddress;
        private readonly int _port;

        public CentralServerService(ILogger<CentralServerService> logger, Handler handler, int messageQueueLimit, IPAddress ipAddress, int port)
        {
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
                    _connectedClients = _connectedClients.Add(client);
                    tasks.Add(client.Spin());
                }
                else if(completedTask is Task<ConnectedClient> endedClient)
                {
                    _connectedClients = _connectedClients.Remove(await endedClient);
                }
                else
                {
                    await completedTask;
                }
            }
        }

        public ImmutableList<ConnectedClient> GetConnectedClients(){
            return _connectedClients;
        }

    }
}