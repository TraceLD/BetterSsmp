using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using Xunit;
using Ssmp;

namespace Ssmp.Tests
{
    public class ConnectedClientTest
    {
        [Fact]
        public async Task TestSingleMessage()
        {
            //setup server
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 16384);
            listener.Start();

            //send a message
            var externalClient = ConnectedClient.Connect((a, b) => {}, "localhost", 16384, 10);
            externalClient.SendMessage(new byte[]{10, 20, 30});

            //setup recieving
            var isReceived = false;
            var client = listener.AcceptTcpClient();
            var internalClient = ConnectedClient.Adopt((client, message) =>
            {
                if(Enumerable.SequenceEqual(message, new byte[]{10, 20, 30})){
                    isReceived = true;
                }else{
                    throw new Exception("Message contents don't match.");
                }
            },
            client, 10);
            
            //let the clients do their thing for an absurdly long period of time
            await Task.WhenAny(internalClient.Spin(), externalClient.Spin(), Task.Delay(10));

            Assert.True(isReceived, "Failed to recieve message.");
        }

        [Fact]
        public async Task TestManyMessages()
        {
            const int MESSAGE_COUNT = 16384;

            //setup server
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 16385);
            listener.Start();

            //send a message
            var externalClient = ConnectedClient.Connect((a, b) => {}, "localhost", 16385, MESSAGE_COUNT*2);
            for(var i=0;i<MESSAGE_COUNT;++i){
                var message = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(message, i);
                externalClient.SendMessage(message);
            }

            //setup recieving
            var wasRecieved = new bool[MESSAGE_COUNT]; //we need to just do a bool since the messages are allowed to be re-ordered
            var client = listener.AcceptTcpClient();
            var internalClient = ConnectedClient.Adopt((client, message) =>
            {
                var id = BinaryPrimitives.ReadInt32LittleEndian(message);
                wasRecieved[id] = true;
            },
            client, MESSAGE_COUNT*2);
            
            //let the clients do their thing for an absurdly long period of time
            await Task.WhenAny(internalClient.Spin(), externalClient.Spin(), Task.Delay(1000));

            for(var i=0;i<MESSAGE_COUNT;++i){
                Assert.True(wasRecieved[i]);
            }
        }

    }
}
