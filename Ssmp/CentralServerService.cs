using System.Collections.Immutable;

namespace Ssmp{

    public class CentralServerService
    {
        internal volatile ImmutableList<ConnectedClient> _connectedClients = ImmutableList<ConnectedClient>.Empty;
        public ImmutableList<ConnectedClient> GetConnectedClients()
        {
            return _connectedClients;
        }

    }

}