using System.Net.Sockets;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ssmp
{
    public interface ICentralServerService
    {
    }
    
    public class CentralServerService : ICentralServerService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly int _messageQueueLimit;
        private readonly List<Task> _tasks = new();
        private readonly TcpListener _listener;
        private readonly ISsmpHandler _handler;

        private volatile ImmutableList<ConnectedClient> _connectedClients = ImmutableList<ConnectedClient>.Empty;

        public ImmutableList<ConnectedClient> ConnectedClients => _connectedClients;

        public CentralServerService(
            ILoggerFactory loggerFactory,
            IOptions<SsmpOptions> options,
            ISsmpHandler handler
        )
        {
            var ssmpOptions = options.Value;

            _loggerFactory = loggerFactory;
            _handler = handler;
            _messageQueueLimit = ssmpOptions.Port;
            _listener = new TcpListener(IPAddress.Parse(ssmpOptions.IpAddress), ssmpOptions.Port);

            _listener.Start();
        }

        internal async Task SpinOnce()
        {
            _tasks.Add(_listener.AcceptTcpClientAsync());

            var completedTask = await Task.WhenAny(_tasks);

            _tasks.Remove(completedTask);

            if (completedTask is Task<TcpClient> newConnection)
            {
                var client = ConnectedClient.Adopt(_loggerFactory, _handler, await newConnection, _messageQueueLimit);
                
                _connectedClients = _connectedClients.Add(client);
                _tasks.Add(client.Spin());
            }
            else if (completedTask is Task<ConnectedClient> endedClient)
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