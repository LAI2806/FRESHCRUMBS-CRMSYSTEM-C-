using System.Text.Json;
using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.api.Services.Sync
{
    public class PushOperationDto
    {
        public Guid OperationId { get; set; }
        public Guid GroupId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public Guid EntityRowGuid { get; set; }
        public string Operation { get; set; } = SyncOperationType.Upsert;
        public DateTime ChangedAtUtc { get; set; }
        public JsonElement Payload { get; set; }
    }

    public class PushRequest
    {
        public List<PushOperationDto> Operations { get; set; } = new();
    }

    public static class PushResult
    {
        public const string Applied = "Applied";
        public const string Duplicate = "Duplicate";   // already processed earlier (safe retry)
        public const string Skipped = "Skipped";       // a newer cloud version won (conflict)
        public const string Rejected = "Rejected";     // permanent (e.g. plan does not include the feature)
        public const string Failed = "Failed";         // retry later
    }

    public class PushOperationResult
    {
        public Guid OperationId { get; set; }
        public string Result { get; set; } = PushResult.Applied;
        public string? Detail { get; set; }
    }

    public class PushResponse
    {
        public List<PushOperationResult> Results { get; set; } = new();
        public DateTime ServerTimeUtc { get; set; }
    }

    public class PullItemDto
    {
        public string EntityType { get; set; } = string.Empty;
        public Guid RowGuid { get; set; }
        public DateTime UpdatedAt { get; set; }
        public JsonElement Payload { get; set; }
    }

    public class PullResponse
    {
        public List<PullItemDto> Items { get; set; } = new();
        public bool HasMore { get; set; }
        public DateTime ServerTimeUtc { get; set; }
    }

    // Plain copy of a Subscription row (cached on the desktop so the plan rules keep working offline).
    public class SubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int CompanyId { get; set; }
        public int PlanId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public BillingCycle BillingCycle { get; set; }
        public string Features { get; set; } = string.Empty;
        public int MaxUsers { get; set; }
        public bool BranchingEnabled { get; set; }
        public int? MaxBranches { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SubscriptionStatus Status { get; set; }

        public static SubscriptionDto From(Subscription s) => new()
        {
            SubscriptionId = s.SubscriptionId,
            CompanyId = s.CompanyId,
            PlanId = s.PlanId,
            PlanCode = s.PlanCode,
            PlanName = s.PlanName,
            Price = s.Price,
            BillingCycle = s.BillingCycle,
            Features = s.Features,
            MaxUsers = s.MaxUsers,
            BranchingEnabled = s.BranchingEnabled,
            MaxBranches = s.MaxBranches,
            StartDate = DateTime.SpecifyKind(s.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(s.EndDate, DateTimeKind.Utc),
            Status = s.Status
        };

        public Subscription ToEntity() => new()
        {
            SubscriptionId = SubscriptionId,
            CompanyId = CompanyId,
            PlanId = PlanId,
            PlanCode = PlanCode,
            PlanName = PlanName,
            Price = Price,
            BillingCycle = BillingCycle,
            Features = Features,
            MaxUsers = MaxUsers,
            BranchingEnabled = BranchingEnabled,
            MaxBranches = MaxBranches,
            StartDate = StartDate,
            EndDate = EndDate,
            Status = Status
        };
    }

    // What the cloud tells a desktop about a company so the access rules can be enforced offline.
    public class CompanySnapshotDto
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<SubscriptionDto> Subscriptions { get; set; } = new();
        public int ActiveUsers { get; set; }
        // Version number of Terms & Conditions this company still has to accept (null = none).
        public int? PendingTermsVersion { get; set; }

        // The company's accounts, so an ADMIN can assign employees to branches while offline.
        public List<CompanyUserDto> Users { get; set; } = new();
    }

    public class CompanyUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SyncTokenDto
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public class SyncStatusDto
    {
        public string Mode { get; set; } = "Cloud";
        // Online / Offline / Syncing / NeedsSignIn / Error / Cloud
        public string State { get; set; } = "Cloud";
        public bool IsOnline { get; set; }
        public int Pending { get; set; }
        public int Rejected { get; set; }
        public DateTime? LastSyncUtc { get; set; }
        public string? LastError { get; set; }
        public double? GraceDaysLeft { get; set; }
    }
}