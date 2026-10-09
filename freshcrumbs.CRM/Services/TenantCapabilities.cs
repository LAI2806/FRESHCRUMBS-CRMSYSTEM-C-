namespace freshcrumbs.CRM.winforms.Services
{
    // One place the WinForms screens ask "may this user see/do this?". It only drives what is shown;
    // the API enforces every action (ROLE allows it AND the SUBSCRIPTION provides the feature).
    // Permissions come from the server (role AND feature already combined), so the rules live in one place.
    public static class TenantCapabilities
    {
        public static bool IsFeatureAvailable(string featureKey)
        {
            return AuthSession.EnabledFeatures.Contains(featureKey, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsRoleAllowed(params string[] roles)
        {
            return AuthSession.IsInRole(roles);
        }

        public static bool Has(string permission)
        {
            return AuthSession.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }

        public static bool CanViewProducts => Has("ViewProducts");

        // Add / edit / deactivate products and change stock.
        public static bool CanManageProducts => Has("ManageProducts");

        public static bool CanManageCustomers => Has("ManageCustomers");

        public static bool CanManageFeedback => Has("ManageFeedback");

        public static bool CanManageInquiries => Has("ManageInquiries");

        public static bool CanManagePromotions => Has("ManagePromotions");

        public static bool CanManageLoyalty => Has("ManageLoyalty");

        public static bool CanProcessSeniorPwdDiscount => Has("ProcessSeniorPwdDiscount");

        public static bool CanViewDashboardData => Has("ViewDashboardData");

        public static bool CanGenerateReports => Has("GenerateReports");

        // Read-only operational reports (every role); STAFF use these instead of the management report generator.
        public static bool CanViewOperationalReports => Has("ViewOperationalReports");

        public static bool CanUsePromotions => Has("UsePromotions");

        public static bool CanUseLoyalty => Has("UseLoyalty");

        public static bool CanViewManagementDashboard => Has("ViewManagementDashboard");

        public static bool CanViewRetentionDashboard => Has("ViewRetentionDashboard");

        public static bool CanViewCompanyDashboard => Has("ViewCompanyDashboard");

        public static bool CanGenerateReport(string reportType) => Has("Report." + reportType);

        // PREMIUM (Branching) plans.
        public static bool HasBranching => IsFeatureAvailable("Branching");

        public static bool CanManageBranches => Has("ManageBranches");

        public static bool CanManageBranchStock => Has("ManageBranchStock");

        public static bool CanViewBranchStock => Has("ViewBranchStock");

        public static bool CanViewBranchBI => Has("ViewBranchBI");

        // Edit / cancel / delete sales and correct their items (ADMIN, MANAGER). STAFF records sales only.
        public static bool CanManageSales => Has("ManageSales");

        // PREMIUM company-wide view (ADMIN): may filter Sales, Feedback and Inquiries by branch.
        public static bool CanFilterByBranch => HasBranching && AuthSession.IsAdmin;

        // PREMIUM, every role: the shared customer list can be narrowed by branch (a filter, not an access rule).
        public static bool CanFilterCustomersByBranch => HasBranching;

        // Tenant User Management (ADMIN only, every plan).
        public static bool CanManageUsers => Has("ManageUsers");
    }
}
