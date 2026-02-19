using System.Net;
using System.Net.Sockets;
using System.Text;
using Middleware_Core.Configuration;

namespace Middleware_Core.Protocols
{
    public class TcpProtocolServer
    {
        private readonly AnalyzerProfile _profile;
        private readonly Action<string, AnalyzerProfile> _onMessage;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;

        public TcpProtocolServer(AnalyzerProfile profile, Action<string, AnalyzerProfile> onMessage)
        {
            _profile = profile;
            _onMessage = onMessage;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _profile.TcpPort);
            _listener.Start();

            _ = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
            Console.WriteLine($"[TCP:{_profile.Name}] Listening on {_profile.TcpPort} ({_profile.Protocol})");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener != null)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var _ = client;
            using var stream = client.GetStream();

            var encoding = Encoding.GetEncoding(_profile.EncodingName);
            var receiveBuffer = new byte[8192];
            var textBuffer = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(receiveBuffer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (bytesRead <= 0)
                    break;

                textBuffer.Append(encoding.GetString(receiveBuffer, 0, bytesRead));

                var extracted = ProtocolFrameExtractor.ExtractMessages(textBuffer, _profile.Protocol).ToList();
                foreach (var message in extracted)
                {
                    _onMessage(message, _profile);

                    var ack = ProtocolFrameExtractor.BuildAck(_profile.Protocol);
                    if (ack.Length > 0)
                        await stream.WriteAsync(ack, cancellationToken);
                }
            }
        }
    }
}