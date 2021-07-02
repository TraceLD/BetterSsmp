using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ssmp.Tests
{
    public class ConnectedClientTest
    {
        private readonly ILoggerFactory _loggerFactory;

        public ConnectedClientTest() =>
            _loggerFactory = new LoggerFactory();
        
        private class EmptyHandler : ISsmpHandler
        {
            public ValueTask Handle(ConnectedClient client, byte[] message)
            {
                return ValueTask.CompletedTask;
            }
        }

        private class TestSingleMessageInternalClientHandler : ISsmpHandler
        {
            public ValueTask Handle(ConnectedClient client, byte[] message)
            {
                if (!message.SequenceEqual(new byte[] {10, 20, 30}))
                {
                    throw new Exception("Message contents don't match.");
                }

                return ValueTask.CompletedTask;
            }
        }

        private class TestManyMessagesInternalClientHandler : ISsmpHandler
        {
            public ValueTask Handle(ConnectedClient client, byte[] message)
            {
                BinaryPrimitives.ReadInt32LittleEndian(message);
                return ValueTask.CompletedTask;
            }
        }

        [Fact]
        public async Task TestSingleMessage()
        {
            var exception = await Record.ExceptionAsync(async () =>
            {
                //setup server
                var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 16384);
                listener.Start();

                //send a message
                var externalClient = ConnectedClient.Connect(_loggerFactory, new EmptyHandler(), "localhost", 16384, 10);
                externalClient.SendMessage(new byte[] {10, 20, 30});

                //setup receiving
                var client = await listener.AcceptTcpClientAsync();
                var internalClient = ConnectedClient.Adopt(_loggerFactory, new TestSingleMessageInternalClientHandler(), client, 10);

                //let the clients do their thing for an absurdly long period of time
                await Task.WhenAny(internalClient.Spin(), externalClient.Spin(), Task.Delay(10));
            });
            
            Assert.Null(exception);
        }

        [Fact]
        public async Task TestManyMessages()
        {
            const int messageCount = 16384;

            var exception = await Record.ExceptionAsync(async () =>
            {
                //setup server
                var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 16385);
                listener.Start();

                //send a message
                var externalClient =
                    ConnectedClient.Connect(_loggerFactory, new EmptyHandler(), "localhost", 16385, messageCount * 2);

                for (var i = 0; i < messageCount; ++i)
                {
                    var message = new byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(message, i);
                    externalClient.SendMessage(message);
                }

                //setup receiving
                var client = await listener.AcceptTcpClientAsync();
                var internalClient =
                    ConnectedClient.Adopt(_loggerFactory, new TestManyMessagesInternalClientHandler(), client, messageCount * 2);

                //let the clients do their thing for an absurdly long period of time
                await Task.WhenAny(internalClient.Spin(), externalClient.Spin(), Task.Delay(1000));
            });

            Assert.Null(exception);
        }
    }
}