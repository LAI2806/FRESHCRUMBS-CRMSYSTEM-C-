namespace freshcrumbs.CRM.domain.entities
{
    // Implemented by every tenant entity that takes part in offline/cloud synchronization.
    // The integer primary keys stay database-local (identity); RowGuid is the global identity
    // that is the same on every device and in the cloud.
    public interface ISyncEntity
    {
        Guid RowGuid { get; set; }

        DateTime UpdatedAt { get; set; }
    }
}