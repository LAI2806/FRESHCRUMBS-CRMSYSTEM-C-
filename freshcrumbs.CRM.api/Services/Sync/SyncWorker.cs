namespace freshcrumbs.CRM.api.Services.Sync
{
    // Runs in Local mode only. Wakes up every few seconds, checks the connection and synchronizes.
    // "Sync now" from the WinForms status label just wakes it up early.
    public class SyncWorker : BackgroundService
    {
        private readonly SyncEngine _engine;
        private readonly LocalAccessCache _cache;
        private readonly SyncStatusService _status;
        private readonly SyncOptions _options;
        private readonly ILogger<SyncWorker> _log;
        private readonly SemaphoreSlim _wake = new(0);

        public SyncWorker(
            SyncEngine engine,
            LocalAccessCache cache,
            SyncStatusService status,
            SyncOptions options,
            ILogger<SyncWorker> log)
        {
            _engine = engine;
            _cache = cache;
            _status = status;
            _options = options;
            _log = log;
        }

        public void SyncNow()
        {
            if (_wake.CurrentCount == 0)
            {
                _wake.Release();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var companyIds = _cache.Read(d => d.SyncTokens.Keys.ToList());

                    if (companyIds.Count == 0)
                    {
                        _log.LogWarning("Sync worker: no sync token is cached, so nothing can be synchronized. Sign in through the local API while the cloud is reachable.");
                        _status.Set("NeedsSignIn", online: false, error: "Sign in once while connected to the internet.");
                    }

                    foreach (var companyId in companyIds)
                    {
                        _log.LogInformation("Sync worker: starting sync for company {CompanyId}", companyId);
                        await _engine.RunOnceAsync(companyId, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Sync worker iteration failed");
                }

                try
                {
                    await _wake.WaitAsync(TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}