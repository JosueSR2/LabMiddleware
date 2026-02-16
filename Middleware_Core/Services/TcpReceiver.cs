using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Middleware_Core.Services
{
    public class TcpReceiver
    {
        private readonly int _port;
        private readonly Action<string> _onMessageReceived;
        private TcpListener? _listener;
        private bool _isRunning;

        public TcpReceiver(int port, Action<string> onMessageReceived)
        {
            _port = port;
            _onMessageReceived = onMessageReceived;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"TCP Receiver listening on port {_port}...");

            new Thread(ListenLoop).Start();
        }

        private void ListenLoop()
        {
            while (_isRunning && _listener != null)
            {
                var client = _listener.AcceptTcpClient();
                var stream = client.GetStream();

                byte[] buffer = new byte[8192];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                _onMessageReceived?.Invoke(message);

                client.Close();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }
    }
}
