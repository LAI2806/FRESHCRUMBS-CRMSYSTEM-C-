namespace freshcrumbs.CRM.winforms.Models
{
    public class SubscriberListItemModel
    {
        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string ContactNo { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? PlanCode { get; set; }

        public string? PlanName { get; set; }

        public string Status { get; set; } = "No Subscription";

        public DateTime? EndDate { get; set; }

        public int ActiveUsers { get; set; }

        public int? MaxUsers { get; set; }

        public string PlanText => string.IsNullOrEmpty(PlanName) ? "-" : $"{PlanName} ({PlanCode})";

        public string TenantStatus => IsActive ? "Active" : "Inactive";

        public string? DatabaseServer { get; set; }

        public string? DatabaseName { get; set; }

        public string? DatabaseCredentialKey { get; set; }

        public string DatabaseStatus { get; set; } = "Not provisioned";

        public string ServerText => string.IsNullOrEmpty(DatabaseServer) ? "-" : DatabaseServer;

        public string DatabaseText => string.IsNullOrEmpty(DatabaseName) ? "-" : DatabaseName;

        public string ExpiryText => EndDate == null ? "-" : EndDate.Value.ToString("yyyy-MM-dd");

        public string UsersText => MaxUsers == null ? $"{ActiveUsers} / -" : $"{ActiveUsers} / {MaxUsers}";
    }

    public class SubscriptionModel
    {
        public int SubscriptionId { get; set; }

        public int PlanId { get; set; }

        public string PlanCode { get; set; } = string.Empty;

        public string PlanName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string BillingCycle { get; set; } = string.Empty;

        public List<string> Features { get; set; } = new();

        public int MaxUsers { get; set; }

        public bool BranchingEnabled { get; set; }

        public int? MaxBranches { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string Status { get; set; } = string.Empty;

        public string ChangeType { get; set; } = string.Empty;

        public string? Reason { get; set; }

        public string ChangedBy { get; set; } = string.Empty;

        public bool IsCurrent { get; set; }

        public string FeaturesText => string.Join(", ", Features.Select(PlanFeatureCatalog.DisplayName));

        public string PriceText => $"{Price:N2} / {BillingCycle}";

        public string StartText => StartDate.ToString("yyyy-MM-dd");

        public string EndText => EndDate.ToString("yyyy-MM-dd");

        public string BranchingText => BranchingEnabled ? $"Enabled (up to {MaxBranches})" : "Not included";
    }

    public class SubscriberCompanyModel
    {
        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string BusinessAddress { get; set; } = string.Empty;

        public string ContactNo { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    public class TenantDatabaseModel
    {
        public string ServerName { get; set; } = string.Empty;

        public string DatabaseName { get; set; } = string.Empty;

        public string CredentialKey { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    // SuperAdmin onboarding: the company's first ADMIN and its one-time temporary password (shown once).
    public class CreatedTenantAdminModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class RegisteredSubscriberModel
    {
        public SubscriberDetailModel? Subscriber { get; set; }
        public CreatedTenantAdminModel Admin { get; set; } = new();
    }

    public class SubscriberDetailModel
    {
        public SubscriberCompanyModel Company { get; set; } = new();

        public bool TenantDatabaseProvisioned { get; set; }

        public TenantDatabaseModel? TenantDatabase { get; set; }

        public int ActiveUsers { get; set; }

        public int? MaxUsers { get; set; }

        public SubscriptionModel? CurrentSubscription { get; set; }

        public List<SubscriptionModel> History { get; set; } = new();
    }

    public class TenantSubscriptionModel
    {
        public bool HasAccess { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? PlanName { get; set; }

        public List<string> Features { get; set; } = new();

        // Tenant role as stored on the server, and what that role may do under this plan (role AND feature).
        public string? Role { get; set; }

        public List<string> Permissions { get; set; } = new();
    }
}