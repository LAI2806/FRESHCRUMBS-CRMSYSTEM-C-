using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Services
{
    public static class AuthSession
    {
        public static LoginResultModel? Current { get; private set; }

        public static string? Token => Current?.Token;

        public static bool IsSuperAdmin => Current?.IsSuperAdmin == true;

        public static void Start(LoginResultModel login)
        {
            Current = login;
        }

        public static void Clear()
        {
            Current = null;
        }
    }
}