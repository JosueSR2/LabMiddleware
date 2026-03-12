namespace Middleware_Core.Configuration
{
    public class MiddlewareOptions
    {
        public string LisUrl { get; set; } = string.Empty;
        public string LisUrlAstm { get; set; } = string.Empty;
        public string LisUrlHl7 { get; set; } = string.Empty;
        public string WatchFolder { get; set; } = "./TestingResources";
        public string OutboxDbPath { get; set; } = "./data/outbox.db";
        public int DeliveryBatchSize { get; set; } = 50;
        public List<int> RetryScheduleSeconds { get; set; } = new() { 10, 30, 120, 300 };
        public TcpOptions Tcp { get; set; } = new();
        public SerialOptions Serial { get; set; } = new();
        public List<AnalyzerProfile> Analyzers { get; set; } = new();
        public LisSecurityOptions LisSecurity { get; set; } = new();
        public ResilienceOptions Resilience { get; set; } = new();
        public LisDeliveryOptions LisDelivery { get; set; } = new();
    }

    public class LisSecurityOptions
    {
        public bool RequireTls { get; set; }
        public string BearerToken { get; set; } = string.Empty;
        public string BasicUsername { get; set; } = string.Empty;
        public string BasicPassword { get; set; } = string.Empty;
        public bool AllowInvalidServerCertificate { get; set; }
        public bool AllowServerCertificateNameMismatch { get; set; }

        // Optional client TLS material (mTLS). Supports PEM cert+key or a PKCS#12 bundle.
        public string ClientCertificatePath { get; set; } = string.Empty;
        public string ClientKeyPath { get; set; } = string.Empty;

        // Optional trust store (PKCS#12). When set, server certificate validation can be
        // performed against these custom roots in addition to standard checks.
        public string TrustStorePath { get; set; } = string.Empty;
        public string TrustStorePassword { get; set; } = string.Empty;
    }

    public class ResilienceOptions
    {
        public int HttpTimeoutSeconds { get; set; } = 10;
        public int CircuitBreakerFailureThreshold { get; set; } = 5;
        public int CircuitBreakerBreakSeconds { get; set; } = 30;
    }

    public class LisDeliveryOptions
    {
        public LisPayloadMode PayloadMode { get; set; } = LisPayloadMode.RawMessage;
        public string ContentType { get; set; } = "text/plain";
        public string Accept { get; set; } = "*/*";
        public bool SendOneRecordPerIncomingMessage { get; set; } = true;
    }

    public enum LisPayloadMode
    {
        RawMessage,
        Hl7FromResult,
        JsonLabResult
    }

    public enum TransportType
    {
        File,
        Tcp,
        Serial
    }

    public enum ProtocolType
    {
        Raw,
        Hl7Mllp,
        Astm
    }

    public class AnalyzerProfile
    {
        public string Name { get; set; } = "default";
        public string SourceMachine { get; set; } = "Unknown";
        public TransportType Transport { get; set; } = TransportType.Tcp;
        public ProtocolType Protocol { get; set; } = ProtocolType.Raw;
        public int TcpPort { get; set; } = 5001;
        public string SerialPortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public string? WatchFolder { get; set; }
        public string EncodingName { get; set; } = "utf-8";

        // Optional: informational name for LIS/OpenELIS-side analyzer mapping.
        public string LisAnalyzerName { get; set; } = string.Empty;
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
