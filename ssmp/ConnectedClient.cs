using System.Buffers.Binary;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Threading;

namespace Ssmp
{

    public class ConnectedClient : IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly IMessageHandler _handler;
        private readonly Channel<byte[]> _sendQueue;
        private readonly ChannelWriter<byte[]> _writer;
        private readonly ChannelReader<byte[]> _reader;

        public static ConnectedClient Connect(IMessageHandler handler, string ip, Int32 port, int messageQueueLimit)
        {
            return new ConnectedClient(handler, new TcpClient(ip, port), messageQueueLimit);
        }

        internal ConnectedClient(IMessageHandler handler, TcpClient tcpClient, int messageQueueLimit)
        {
            _handler = handler;
            _tcpClient = tcpClient;
            _stream = _tcpClient.GetStream();
            _sendQueue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(messageQueueLimit)
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = true
            });
            _writer = _sendQueue.Writer;
            _reader = _sendQueue.Reader;
        }

        public async Task Spin()
        {
            await Task.WhenAll(SendPendingMessages(), ReceiveMessages());
        }
    
        public async void SendMessage(byte[] message)
        {
            await _writer.WriteAsync(message);
        }

        public void Dispose(){
            _stream?.Dispose();
            _tcpClient?.Dispose();
        }

        private async Task SendPendingMessages()
        {
            var lengthBuffer = new byte[4];

            while(_tcpClient.Connected)
            {
                var message = await _reader.ReadAsync();
                BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, message.Length);
                await _stream.WriteAsync(lengthBuffer, 0, lengthBuffer.Length);
                await _stream.WriteAsync(message, 0, message.Length);
            }
        }

        private async Task ReceiveMessages()
        {
            var lengthBuffer = new byte[4];

            while(_tcpClient.Connected)
            {
                //read length
                await _stream.ReadNBytes(4, lengthBuffer);
                var length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);

                //allocate buffer & read message
                var buffer = new byte[length]; //new buffer is allocated so ownership of the buffer can be passed off of this thread
                await _stream.ReadNBytes(length, buffer);

                //handle message
                Task.Run(() => _handler.Handle(this, buffer)); //fire & forget the handling so that the handler can't mess up our Glorious Threading
            }
        }

    }

    interface IMessageHandler
    {
        Task Handle(ConnectedClient client, byte[] data);
    }

}