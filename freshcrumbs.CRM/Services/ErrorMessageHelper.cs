using System.Net.Http;

namespace freshcrumbs.CRM.winforms.Services
{
    public static class ErrorMessageHelper
    {
        public static string GetFriendlyMessage(Exception ex)
        {
            // Messages written by the API for the user (validation, access, branch rules) are shown as they are.
            if (ex is ApiValidationException or ApiAccessDeniedException)
            {
                return ex.Message;
            }

            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return "The requested record could not be found. It may have been removed.";
                }

                if (httpEx.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    return "The information provided is invalid. Please check the fields and try again.";
                }

                if (httpEx.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    return "This action could not be completed because it conflicts with existing data.";
                }

                if (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return "Your session has expired. Please log out and log in again.";
                }

                if (httpEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return "Access was denied. Your role may not allow this action, your subscription may be inactive or may not include this feature, or new Terms & Conditions may need to be accepted. Please log out and log in again, or contact your administrator.";
                }

                if (httpEx.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    return "The server encountered a problem processing this request. Please try again.";
                }

                return "Could not connect to the server. Please check your connection and try again.";
            }

            return "An unexpected error occurred. Please try again.";
        }
    }
}