using freshcrumbs.CRM.domain.entities;

namespace freshcrumbs.CRM.api.Authorization
{
    // Single table of tenant actions. An action is allowed only when
    //   ROLE allows it  AND  the subscription provides the feature it belongs to.
    // A role never overrides a missing subscription feature. Enforced by TenantAccessFilter
    // (via [RequireTenantPermission]) and also used to tell the WinForms client what it may show.
    public static class TenantPermissions
    {
        public const string ViewProducts = "ViewProducts";
        public const string ManageProducts = "ManageProducts";
        public const string ManageCustomers = "ManageCustomers";
        public const string ManageFeedback = "ManageFeedback";
        public const string ManageInquiries = "ManageInquiries";
        public const string ManagePromotions = "ManagePromotions";
        public const string ManageLoyalty = "ManageLoyalty";
        public const string ProcessSeniorPwdDiscount = "ProcessSeniorPwdDiscount";
        public const string ViewDashboardData = "ViewDashboardData";
        public const string GenerateReports = "GenerateReports";
        public const string UsePromotions = "UsePromotions";
        public const string UseLoyalty = "UseLoyalty";
        public const string ViewManagementDashboard = "ViewManagementDashboard";
        public const string ViewRetentionDashboard = "ViewRetentionDashboard";
        public const string ViewCompanyDashboard = "ViewCompanyDashboard";
        public const string ManageBranches = "ManageBranches";
        public const string ManageBranchStock = "ManageBranchStock";
        public const string ViewBranchStock = "ViewBranchStock";
        public const string ViewBranchBI = "ViewBranchBI";
        public const string ManageUsers = "ManageUsers";
        public const string ManageSales = "ManageSales";
        public const string ViewOperationalReports = "ViewOperationalReports";

        public const string ReportPrefix = "Report.";

        public static string ReportPermission(string reportType) => ReportPrefix + reportType;

        private static readonly string[] Everyone = TenantRoles.All.ToArray();
        private static readonly string[] ManagementRoles = { TenantRoles.Admin, TenantRoles.Manager };
        private static readonly string[] AdminOnly = { TenantRoles.Admin };

        // Feature = null means no extra plan feature is needed beyond an active subscription.
        private sealed record Rule(string[] Roles, string? Feature);

        private static readonly Dictionary<string, Rule> Rules = new(StringComparer.OrdinalIgnoreCase)
        {
            // Product viewing/search/filter is a role permission, not a subscription tier feature.
            [ViewProducts] = new(Everyone, PlanFeatureKeys.MainTransactions),
            // Product master data and stock (stock is edited through the same product record).
            [ManageProducts] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            // STAFF records and views customers; editing/deactivating is a management action.
            [ManageCustomers] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            // STAFF records feedback/complaints/inquiries; reviewing, editing and deleting is management.
            [ManageFeedback] = new(ManagementRoles, PlanFeatureKeys.DataCollection),
            [ManageInquiries] = new(ManagementRoles, PlanFeatureKeys.DataCollection),
            // Standard/Premium only (ActionsRetention), and only for management roles.
            [ManagePromotions] = new(ManagementRoles, PlanFeatureKeys.ActionsRetention),
            [ManageLoyalty] = new(ManagementRoles, PlanFeatureKeys.ActionsRetention),
            // STAFF records sales; editing, cancelling, deleting and item corrections are management actions.
            [ManageSales] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            // Senior/PWD discounts are part of Basic sales, so they follow the sales feature.
            [ProcessSeniorPwdDiscount] = new(Everyone, PlanFeatureKeys.MainTransactions),
            // The dashboard exists on every plan; Part 2 decides how much data each role/plan sees.
            [ViewDashboardData] = new(Everyone, null),
            // The Reports module is a management tool on every plan (Basic Reports): ADMIN and MANAGER only.
            // Which report types a plan gets is decided by the Report.* rules below.
            [GenerateReports] = new(ManagementRoles, null),
            // Read-only operational reports (sales, customers, products, feedback, inquiries, and promotion / loyalty
            // activity when the plan has them) for every role; no management analytics. Branch-scoped on PREMIUM.
            [ViewOperationalReports] = new(Everyone, PlanFeatureKeys.MainTransactions),
            [UsePromotions] = new(Everyone, PlanFeatureKeys.ActionsRetention),
            [UseLoyalty] = new(Everyone, PlanFeatureKeys.ActionsRetention),
            [ViewManagementDashboard] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            [ViewRetentionDashboard] = new(ManagementRoles, PlanFeatureKeys.ActionsRetention),
            [ViewCompanyDashboard] = new(AdminOnly, PlanFeatureKeys.MainTransactions),
            // PREMIUM (Branching): ADMIN manages branches, employee assignments and stock allocation;
            // MANAGER changes stock of the assigned branch only (enforced in BranchesController).
            [ManageBranches] = new(AdminOnly, PlanFeatureKeys.Branching),
            [ManageBranchStock] = new(ManagementRoles, PlanFeatureKeys.Branching),
            [ViewBranchStock] = new(Everyone, PlanFeatureKeys.Branching),
            // ADMIN: every active branch; MANAGER: the assigned branch only (scoped in TenantReportsController).
            [ViewBranchBI] = new(ManagementRoles, PlanFeatureKeys.Branching),
            // Employee accounts of the ADMIN's own company, on every plan.
            [ManageUsers] = new(AdminOnly, null),
            [ReportPermission("sales")] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            [ReportPermission("product-sales")] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            [ReportPermission("inventory")] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            [ReportPermission("customers")] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            [ReportPermission("discounts")] = new(ManagementRoles, PlanFeatureKeys.MainTransactions),
            [ReportPermission("feedback")] = new(ManagementRoles, PlanFeatureKeys.DataCollection),
            [ReportPermission("inquiries")] = new(ManagementRoles, PlanFeatureKeys.DataCollection),
            [ReportPermission("promotions")] = new(ManagementRoles, PlanFeatureKeys.ActionsRetention),
            [ReportPermission("loyalty")] = new(ManagementRoles, PlanFeatureKeys.ActionsRetention),
            // One generated report summarising the business (ADMIN company-wide, PREMIUM MANAGER their branch).
            [ReportPermission("business-summary")] = new(ManagementRoles, PlanFeatureKeys.BusinessIntelligence)
        };

        public static bool IsKnown(string permission) => Rules.ContainsKey(permission);

        public static bool IsRoleAllowed(string permission, string? role)
        {
            var normalized = TenantRoles.Normalize(role);

            return normalized != null
                && Rules.TryGetValue(permission, out var rule)
                && rule.Roles.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        public static string? RequiredFeature(string permission)
        {
            return Rules.TryGetValue(permission, out var rule) ? rule.Feature : null;
        }

        public static bool IsFeatureAvailable(string permission, IEnumerable<string> enabledFeatures)
        {
            var feature = RequiredFeature(permission);

            return feature == null || enabledFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase);
        }

        // Permissions this role actually holds under the given plan features (role AND feature).
        public static List<string> Effective(string? role, IEnumerable<string> enabledFeatures)
        {
            var features = enabledFeatures.ToList();

            return Rules.Keys
                .Where(p => IsRoleAllowed(p, role) && IsFeatureAvailable(p, features))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }
    }
}
