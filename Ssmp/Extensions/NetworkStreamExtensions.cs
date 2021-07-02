using System.Net.Sockets;
using System.Threading.Tasks;

namespace Ssmp.Extensions
{
    public static class NetworkStreamExtensions
    {
        public static async Task<int> ReadBufferLength(this NetworkStream ns, byte[] buffer)
        {
            var index = 0;

            while(index < buffer.Length)
            {
                var bytes = await ns.ReadAsync(buffer, index, buffer.Length - index);

                if (bytes <= 0)
                {
                    break;
                }

                index += bytes;
            }

            return index;
        }
    }
}