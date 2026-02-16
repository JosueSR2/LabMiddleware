using System.IO.Ports;
using System.Text;

namespace Middleware_Core.Receivers
{
    public class SerialReceiver
    {
        private readonly SerialPort _port;
        private readonly Action<string> _onMessageReceived;

        public SerialReceiver(string portName, int baudRate, Action<string> onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;

            _port = new SerialPort(portName, baudRate)
            {
                Encoding = Encoding.ASCII
            };

            _port.DataReceived += Port_DataReceived;
        }

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            string data = _port.ReadExisting();
            Console.WriteLine("[SERIAL] Data received");
            _onMessageReceived(data);
        }

        public void Start()
        {
            _port.Open();
            Console.WriteLine("[SERIAL] Port opened");
        }
    }
}
