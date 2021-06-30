using System.Buffers.Binary;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Ssmp.Extensions;

namespace Ssmp
{
    public class ConnectedClient : IDisposable
    {
        private readonly ILogger<ConnectedClient> _logger;
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly ISsmpHandler _handler;
        private readonly ChannelWriter<byte[]> _writer;
        private readonly ChannelReader<byte[]> _reader;
        private readonly string _clientIp;

        public static ConnectedClient Connect(ILoggerFactory loggerFactory, ISsmpHandler handler, string ip, int port, int messageQueueLimit) => 
            new(loggerFactory, handler, new TcpClient(ip, port), messageQueueLimit);

        public static ConnectedClient Adopt(ILoggerFactory loggerFactory, ISsmpHandler handler, TcpClient tcpClient, int messageQueueLimit) => 
            new(loggerFactory, handler, tcpClient, messageQueueLimit);

        private ConnectedClient(ILoggerFactory loggerFactory, ISsmpHandler handler, TcpClient tcpClient, int messageQueueLimit)
        {
            _logger = loggerFactory.CreateLogger<ConnectedClient>();
            _tcpClient = tcpClient;
            _stream = _tcpClient.GetStream();
            _handler = handler;

            var sendQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(messageQueueLimit)
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
            
            _writer = sendQueue.Writer;
            _reader = sendQueue.Reader;

            _clientIp = _tcpClient.Client.RemoteEndPoint switch
            {
                IPEndPoint ep => $"{ep.Address}:{ep.Port}",
                _ => "Unknown"
            };
        }

        public async Task<ConnectedClient> Spin()
        {
            await Task.WhenAll(SendPendingMessages(), ReceiveMessages());
            return this;
        }

        public async void SendMessage(byte[] message) => 
            await _writer.WriteAsync(message);

        private async Task SendPendingMessages()
        {
            var lengthBuffer = new byte[4];

            while (_tcpClient.Connected)
            {
                var message = await _reader.ReadAsync();
                
                BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, message.Length);
                await _stream.WriteAsync(lengthBuffer);
                await _stream.WriteAsync(message);
            }
        }

        private async Task ReceiveMessages()
        {
            var lengthBuffer = new byte[4];

            while (_tcpClient.Connected)
            {
                //read length
                var readLengthBytes = await _stream.ReadBufferLength(lengthBuffer);

                //return if error, should automatically close the connection;
                if (readLengthBytes != lengthBuffer.Length)
                {
                    _logger.LogError(
                        "Received EOL from client {clientIp} while reading the message length. The connection will be treated as closed (most often this error results from the client disconnecting due to a network error/client dying while sending a message).",
                        _clientIp
                    );
                    return;
                }
                
                var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                
                //allocate buffer & read message
                var buffer = new byte[length]; //new buffer is allocated so ownership of the buffer can be passed off of this thread
                var readBytes = await _stream.ReadBufferLength(buffer);

                //return if error, should automatically close the connection;
                if (readBytes != buffer.Length)
                {
                    _logger.LogError(
                        "Received EOL from client {clientIp} while reading message content bytes. The connection will be treated as closed (most often this error results from the client disconnecting due to a network error/client dying while sending a message).",
                        _clientIp
                    );
                    return;
                }

                //handle message
                //rationale for try/catch: error in the handler should not bring down the entire hosted service;
                //it is however better to catch it here so we don't catch everything in the hosted service
                //and end up in an invalid state that can't be recovered from causing undefined behaviour;
                try
                {
                    _logger.LogDebug("Started handling an incoming message from client: {clientIp}.", _clientIp);
                    
                    await _handler.Handle(this, buffer);
                    
                    _logger.LogDebug("Finished handling an incoming message from client: {clientIp}.", _clientIp);
                }
                catch(Exception e)
                {
                    _logger.LogError(
                        e,
                        "An error has occurred in the message handler while handling a message sent from client: {clientIp}.",
                        _clientIp
                    );
                }
            }
        }
        
        public void Dispose()
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }
    }
}