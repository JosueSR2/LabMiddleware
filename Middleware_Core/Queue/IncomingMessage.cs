namespace Middleware_Core.Queue
{
    public record IncomingMessage(string Source, string RawMessage, string? ExternalId = null, DateTime? ReceivedAtUtc = null);
}
