using System.IO.Ports;
using System.Text;
using Middleware_Core.Configuration;

namespace Middleware_Core.Protocols
{
    public class SerialProtocolReceiver
    {
        private readonly AnalyzerProfile _profile;
        private readonly Action<string, AnalyzerProfile> _onMessage;
        private readonly SerialPort _serialPort;
        private readonly StringBuilder _buffer = new();

        public SerialProtocolReceiver(AnalyzerProfile profile, Action<string, AnalyzerProfile> onMessage)
        {
            _profile = profile;
            _onMessage = onMessage;

            _serialPort = new SerialPort(profile.SerialPortName, profile.BaudRate)
            {
                Encoding = Encoding.GetEncoding(profile.EncodingName)
            };

            _serialPort.DataReceived += OnDataReceived;
        }

        public void Start()
        {
            _serialPort.Open();
            Console.WriteLine($"[SERIAL:{_profile.Name}] Port {_profile.SerialPortName} open ({_profile.Protocol})");
        }

        public void Stop()
        {
            if (_serialPort.IsOpen)
                _serialPort.Close();
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = _serialPort.ReadExisting();
            if (string.IsNullOrEmpty(data))
                return;

            lock (_buffer)
            {
                _buffer.Append(data);
                var messages = ProtocolFrameExtractor.ExtractMessages(_buffer, _profile.Protocol).ToList();
                foreach (var message in messages)
                    _onMessage(message, _profile);
            }
        }
    }
}

