using System.Threading.Channels;

namespace Middleware_Core.Queue
{
    public class IncomingMessageQueue
    {
        private readonly Channel<IncomingMessage> _channel = Channel.CreateUnbounded<IncomingMessage>();

        public ValueTask EnqueueAsync(IncomingMessage message, CancellationToken cancellationToken = default)
            => _channel.Writer.WriteAsync(message, cancellationToken);

        public IAsyncEnumerable<IncomingMessage> ReadAllAsync(CancellationToken cancellationToken = default)
            => _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

