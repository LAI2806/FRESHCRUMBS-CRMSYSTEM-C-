namespace freshcrumbs.CRM.winforms.Services
{
    // Thrown for HTTP 401/403 so screens can show an "access denied" state.
    public class ApiAccessDeniedException : ApiValidationException
    {
        public ApiAccessDeniedException(string message) : base(message)
        {
        }
    }
}