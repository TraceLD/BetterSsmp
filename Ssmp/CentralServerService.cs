using System.Net.Sockets;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Ssmp.ConnectedClient;

namespace Ssmp{

    public class CentralServerService
    {
        private readonly ILogger<CentralServerBackgroundService> _logger;
        private Handler _handler;
        internal volatile ImmutableList<ConnectedClient> _connectedClients = ImmutableList<ConnectedClient>.Empty;
        private int _messageQueueLimit;
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private List<Task> _tasks = new List<Task>();
        private TcpListener _listener;

        public CentralServerService(ILogger<CentralServerBackgroundService> logger, Handler handler, int messageQueueLimit, IPAddress ipAddress, int port)
        {
            _logger = logger;
            _handler = handler;
            _messageQueueLimit = messageQueueLimit;
            _ipAddress = ipAddress;
            _port = port;
            _listener = new TcpListener(_ipAddress, _port);
        }

        public ImmutableList<ConnectedClient> GetConnectedClients()
        {
            return _connectedClients;
        }

        internal async Task SpinOnce(){
            _tasks.Add(_listener.AcceptTcpClientAsync());
                var completedTask = await Task.WhenAny(_tasks);
                _tasks.Remove(completedTask);
                if(completedTask is Task<TcpClient> newConnection)
                {
                    var client = ConnectedClient.Adopt(_handler, await newConnection, _messageQueueLimit);
                    _connectedClients = _connectedClients.Add(client);
                    _tasks.Add(client.Spin());
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

}