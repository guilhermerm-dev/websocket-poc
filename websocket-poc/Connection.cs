using System.Net.WebSockets;

namespace websocket_poc
{
    public class Connection
    {
        public Connection(string connectionId, WebSocket webSocket)
        {
            ConnectionId = connectionId;
            WebSocket = webSocket;
        }

        public string ConnectionId { get; private set; }

        public WebSocket WebSocket { get; private set; }

    }
}
