namespace freshcrumbs.CRM.domain.entities
{
    // Small key/value table for sync bookkeeping (last pull time, last push time, last error...).
    public class SyncState
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}