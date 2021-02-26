using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ssmp
{
    public class CentralServerService : BackgroundService
    {
        private readonly ILogger<CentralServerService> _logger;
        private readonly IPAddress _ipAddress;
        private readonly int _port;

        public CentralServerService(ILogger<CentralServerService> logger, int port)
        {
            _logger = logger;
            _ipAddress = IPAddress.Loopback;
            _port = port;
        }

        public CentralServerService(ILogger<CentralServerService> logger, IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            _logger = logger;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(_ipAddress, _port);
            listener.Start();
            var tasks = new List<Task>();
            
            while (!stoppingToken.IsCancellationRequested)
            {
                stoppingToken.ThrowIfCancellationRequested();

                tasks.Add(listener.AcceptTcpClientAsync());

                var completedTask = await Task.WhenAny(tasks);
                
                if (completedTask is Task<TcpClient> tcpClientTask)
                {
                    var client = await tcpClientTask;
                    tasks.Add(ProcessAsync(client));
                }
                else
                {
                    await completedTask;
                }

                tasks.Remove(completedTask);
            }
        }

        private async Task ProcessAsync(TcpClient tcpClient)
        {
            var clientEndPoint = tcpClient.Client.RemoteEndPoint;
            _logger.LogInformation($"Received connection request from {clientEndPoint}");

            try
            {
                await using var networkStream = tcpClient.GetStream();

                while (true)
                {
                    var buffer = new byte[1024];
                    var requestLength = await networkStream.ReadAsync(buffer);
                    var requestBytes = buffer.ToArray();

                    if (requestBytes.Any())
                    {
                        // do something with the byte array;
                        
                        _logger.LogInformation($"Received a message from {clientEndPoint}");
                        
                        // send response;
                        var lengthArrayBuffer = new byte[4];
                        var msgBytes = new byte[1024]; // this would be proto message bytes;

                        BinaryPrimitives.WriteInt32BigEndian(lengthArrayBuffer.AsSpan(), msgBytes.Length);
                        await networkStream.WriteAsync(lengthArrayBuffer);
                        await networkStream.WriteAsync(msgBytes);
                    }
                    else
                    {
                        _logger.LogInformation($"Closed connection with {clientEndPoint}");
                    }
                    
                    _logger.LogInformation($"Closed connection with {clientEndPoint}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error in the central server.", e);
                if (tcpClient.Connected)
                {
                    tcpClient.Close();
                }
            }
        }
    }
}