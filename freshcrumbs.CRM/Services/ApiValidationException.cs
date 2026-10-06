namespace freshcrumbs.CRM.winforms.Services
{
    public class ApiValidationException : Exception
    {
        public ApiValidationException(string message) : base(message)
        {
        }
    }
}