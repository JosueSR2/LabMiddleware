using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Middleware.Core.Receivers
{
    public class TcpReceiver
    {
        private readonly int _port;
        private readonly Action<string> _onMessageReceived;

        public TcpReceiver(int port, Action<string> onMessageReceived)
        {
            _port = port;
            _onMessageReceived = onMessageReceived;
        }

        public void Start()
        {
            Task.Run(() =>
            {
                var listener = new TcpListener(IPAddress.Any, _port);
                listener.Start();
                Console.WriteLine($"[TCP] Listening on port {_port}...");

                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    using var stream = client.GetStream();

                    byte[] buffer = new byte[8192];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine("[TCP] Message received");
                        _onMessageReceived(message);
                    }
                }
            });
        }
    }
}
