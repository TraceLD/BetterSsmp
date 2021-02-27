using System.Net.Sockets;
using System.Threading.Tasks;

namespace Ssmp
{
    public static class NetworkStreamExtension
    {

        public static async Task ReadNBytes(this NetworkStream ns, int n, byte[] buffer)
        {
            var index = 0;
            while(index < n)
            {
                index += await ns.ReadAsync(buffer, index, n-index);
            }
        }

    }

}