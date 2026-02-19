namespace Middleware_Core.Configuration
{
    public class MiddlewareOptions
    {
        public string LisUrl { get; set; } = string.Empty;
        public string WatchFolder { get; set; } = "./TestingResources";
        public string OutboxDbPath { get; set; } = "./data/outbox.db";
        public int DeliveryBatchSize { get; set; } = 50;
        public List<int> RetryScheduleSeconds { get; set; } = new() { 10, 30, 120, 300 };
        public TcpOptions Tcp { get; set; } = new();
        public SerialOptions Serial { get; set; } = new();
    }

    public class TcpOptions
    {
        public bool Enabled { get; set; } = true;
        public int Port { get; set; } = 5001;
    }

    public class SerialOptions
    {
        public bool Enabled { get; set; }
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
    }
}