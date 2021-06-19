using System.Threading.Tasks;

namespace Ssmp
{
    public interface ISsmpHandler
    {
        ValueTask Handle(ConnectedClient client, byte[] message);
    }
}