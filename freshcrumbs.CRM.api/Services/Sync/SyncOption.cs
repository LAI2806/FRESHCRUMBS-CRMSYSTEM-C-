namespace freshcrumbs.CRM.api.Services.Sync
{
    // Bound from the "Sync" section of appsettings.
    //   Mode = "Cloud" (default): the API behaves exactly as before and talks to the MonsterASP databases.
    //   Mode = "Local": the API runs on the desktop, uses SQL Server Express and syncs with CloudApiUrl.
    public class SyncOptions
    {
        public string Mode { get; set; } = "Cloud";

        // Base URL of the API deployed on MonsterASP, e.g. https://yourapp.monsterasp.net
        public string CloudApiUrl { get; set; } = string.Empty;

        // How long a desktop may work without a successful cloud validation (subscription/user/login).
        public int OfflineGraceDays { get; set; } = 7;

        public int IntervalSeconds { get; set; } = 30;

        // Maximum number of change groups (e.g. sales) sent in one push request.
        public int PushBatchGroups { get; set; } = 50;

        public int PullPageSize { get; set; } = 300;

        public int RequestTimeoutSeconds { get; set; } = 20;

        // A group that keeps failing (not a network error) is parked as Rejected after this many attempts.
        public int MaxAttempts { get; set; } = 5;

        public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
    }
}