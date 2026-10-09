namespace freshcrumbs.CRM.api.Services.Sync
{
    // In-memory state of the background synchronization, shown in the WinForms status label.
    public class SyncStatusService
    {
        private readonly object _gate = new();
        private string _state = "Offline";
        private bool _online;
        private DateTime? _lastSyncUtc;
        private string? _lastError;

        public void Set(string state, bool? online = null, string? error = null, bool clearError = false)
        {
            lock (_gate)
            {
                _state = state;

                if (online != null)
                {
                    _online = online.Value;
                }

                if (error != null)
                {
                    _lastError = error;
                }
                else if (clearError)
                {
                    _lastError = null;
                }
            }
        }

        public void MarkSynced()
        {
            lock (_gate)
            {
                _lastSyncUtc = DateTime.UtcNow;
                _lastError = null;
                _online = true;
                _state = "Online";
            }
        }

        public (string State, bool Online, DateTime? LastSyncUtc, string? LastError) Snapshot()
        {
            lock (_gate)
            {
                return (_state, _online, _lastSyncUtc, _lastError);
            }
        }
    }
}