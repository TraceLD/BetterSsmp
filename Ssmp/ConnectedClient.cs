using System.Buffers.Binary;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace Ssmp
{
    public class ConnectedClient : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly ISsmpHandler _handler;
        private readonly ChannelWriter<byte[]> _writer;
        private readonly ChannelReader<byte[]> _reader;

        public static ConnectedClient Connect(ISsmpHandler handler, string ip, int port, int messageQueueLimit) => 
            new(handler, new TcpClient(ip, port), messageQueueLimit);

        public static ConnectedClient Adopt(ISsmpHandler handler, TcpClient tcpClient, int messageQueueLimit) => 
            new(handler, tcpClient, messageQueueLimit);

        private ConnectedClient(ISsmpHandler handler, TcpClient tcpClient, int messageQueueLimit)
        {
            _tcpClient = tcpClient;
            _handler = handler;
            _stream = _tcpClient.GetStream();
            
            var sendQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(messageQueueLimit)
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
            
            _writer = sendQueue.Writer;
            _reader = sendQueue.Reader;
        }

        public async Task<ConnectedClient> Spin()
        {
            await Task.WhenAll(SendPendingMessages(), ReceiveMessages());
            return this;
        }

        public async void SendMessage(byte[] message) => 
            await _writer.WriteAsync(message);

        public void Dispose()
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }

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
                await _stream.ReadNBytes(4, lengthBuffer);
                var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

                //allocate buffer & read message
                var buffer = new byte[length]; //new buffer is allocated so ownership of the buffer can be passed off of this thread
                await _stream.ReadNBytes(length, buffer);

                //handle message
                await _handler.Handle(this, buffer);
            }
        }
    }
}