namespace PocketRoulette
{
    // A dirty flag, not a heartbeat. Idle raids never start a request.
    public sealed class RaidSyncSchedule
    {
        public bool Dirty { get; private set; }
        public int Failures { get; private set; }
        private double _next;
        public void Request(double now, double delay = 0.15)
        {
            _next = Dirty ? System.Math.Min(_next, now + delay) : now + delay;
            Dirty = true;
            Failures = 0;
        }
        public bool TryStart(double now, bool pendingAcknowledgement, bool force = false)
        {
            if (!force && (Failures >= 3 || !Dirty && !pendingAcknowledgement || now < _next)) return false;
            Dirty = false;
            _next = now + 10;
            return true;
        }
        public void Succeeded() => Failures = 0;
        public void Failed(double now)
        {
            Failures++;
            Dirty = true;
            _next = now + 10 * Failures;
        }
    }
}
