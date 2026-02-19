namespace Middleware_Core.Services
{
    public class OperationalMetrics
    {
        private long _ingressCount;
        private long _outboxInserted;
        private long _outboxDuplicate;
        private long _lisSuccess;
        private long _lisFailure;
        private long _retryScheduled;
        private long _failedPermanent;
        private long _deliveryLatencyMsTotal;

        public void IncrementIngress() => Interlocked.Increment(ref _ingressCount);
        public void IncrementOutboxInserted() => Interlocked.Increment(ref _outboxInserted);
        public void IncrementOutboxDuplicate() => Interlocked.Increment(ref _outboxDuplicate);
        public void IncrementLisSuccess() => Interlocked.Increment(ref _lisSuccess);
        public void IncrementLisFailure() => Interlocked.Increment(ref _lisFailure);
        public void IncrementRetryScheduled() => Interlocked.Increment(ref _retryScheduled);
        public void IncrementFailedPermanent() => Interlocked.Increment(ref _failedPermanent);
        public void AddDeliveryLatency(long elapsedMs) => Interlocked.Add(ref _deliveryLatencyMsTotal, elapsedMs);

        public object Snapshot() => new
        {
            ingressCount = Interlocked.Read(ref _ingressCount),
            outboxInserted = Interlocked.Read(ref _outboxInserted),
            outboxDuplicate = Interlocked.Read(ref _outboxDuplicate),
            lisSuccess = Interlocked.Read(ref _lisSuccess),
            lisFailure = Interlocked.Read(ref _lisFailure),
            retryScheduled = Interlocked.Read(ref _retryScheduled),
            failedPermanent = Interlocked.Read(ref _failedPermanent),
            deliveryLatencyMsTotal = Interlocked.Read(ref _deliveryLatencyMsTotal)
        };
    }
}
