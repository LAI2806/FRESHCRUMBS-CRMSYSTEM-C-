using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://localhost:7230/api/";

        public ApiService()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl)
            };

            if (!string.IsNullOrEmpty(AuthSession.Token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthSession.Token);
            }
        }

        public async Task<LoginResultModel> LoginAsync(string userName, string password)
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", new { userName, password });
            await EnsureSuccessWithMessageAsync(response);

            var result = await response.Content.ReadFromJsonAsync<LoginResultModel>();
            return result ?? throw new ApiValidationException("The server returned an empty login response.");
        }

        public async Task<List<PlanModel>> GetPlansAsync(string? status = null)
        {
            var url = string.IsNullOrEmpty(status) ? "platform/plans" : $"platform/plans?status={status}";
            var response = await _httpClient.GetAsync(url);
            await EnsureSuccessWithMessageAsync(response);

            var plans = await response.Content.ReadFromJsonAsync<List<PlanModel>>();
            return plans ?? new List<PlanModel>();
        }

        public async Task<PlanModel?> CreatePlanAsync(PlanModel plan)
        {
            var response = await _httpClient.PostAsJsonAsync("platform/plans", plan);
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<PlanModel>();
        }

        public async Task<PlanModel?> UpdatePlanAsync(int planId, PlanModel plan)
        {
            var response = await _httpClient.PutAsJsonAsync($"platform/plans/{planId}", plan);
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<PlanModel>();
        }

        public async Task SetPlanActiveAsync(int planId, bool isActive)
        {
            var action = isActive ? "activate" : "deactivate";
            var response = await _httpClient.PutAsync($"platform/plans/{planId}/{action}", null);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<TenantSubscriptionModel> GetTenantSubscriptionAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/subscription");
            await EnsureSuccessWithMessageAsync(response);

            var result = await response.Content.ReadFromJsonAsync<TenantSubscriptionModel>();
            return result ?? new TenantSubscriptionModel { Message = "The server returned an empty subscription response." };
        }

        public async Task<List<SubscriberListItemModel>> GetSubscribersAsync()
        {
            var response = await _httpClient.GetAsync("platform/subscribers");
            await EnsureSuccessWithMessageAsync(response);

            var items = await response.Content.ReadFromJsonAsync<List<SubscriberListItemModel>>();
            return items ?? new List<SubscriberListItemModel>();
        }

        public async Task<SubscriberDetailModel> GetSubscriberAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"platform/subscribers/{companyId}");
            await EnsureSuccessWithMessageAsync(response);

            var detail = await response.Content.ReadFromJsonAsync<SubscriberDetailModel>();
            return detail ?? throw new ApiValidationException("The server returned an empty subscriber response.");
        }

        public async Task<SubscriberDetailModel?> RegisterSubscriberAsync(
            string companyCode, string companyName, string businessAddress,
            string contactNo, string email, int planId, DateTime startDate)
        {
            var response = await _httpClient.PostAsJsonAsync("platform/subscribers", new
            {
                companyCode,
                companyName,
                businessAddress,
                contactNo,
                email,
                planId,
                startDate
            });
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<SubscriberDetailModel>();
        }

        public async Task ChangeSubscriberPlanAsync(int companyId, int planId, DateTime effectiveDate, string reason)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"platform/subscribers/{companyId}/change-plan", new { planId, effectiveDate, reason });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task RenewSubscriptionAsync(int companyId, string reason)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"platform/subscribers/{companyId}/renew", new { reason });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task CancelSubscriptionAsync(int companyId, DateTime effectiveDate, string reason)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"platform/subscribers/{companyId}/cancel", new { effectiveDate, reason });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task SetSubscriptionSuspendedAsync(int companyId, bool suspend)
        {
            var action = suspend ? "suspend" : "reactivate";
            var response = await _httpClient.PostAsync($"platform/subscribers/{companyId}/{action}", null);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<List<TermsVersionModel>> GetTermsVersionsAsync()
        {
            var response = await _httpClient.GetAsync("platform/terms");
            await EnsureSuccessWithMessageAsync(response);

            var items = await response.Content.ReadFromJsonAsync<List<TermsVersionModel>>();
            return items ?? new List<TermsVersionModel>();
        }

        public async Task<TermsVersionModel> GetTermsVersionAsync(int termsVersionId)
        {
            var response = await _httpClient.GetAsync($"platform/terms/{termsVersionId}");
            await EnsureSuccessWithMessageAsync(response);

            var item = await response.Content.ReadFromJsonAsync<TermsVersionModel>();
            return item ?? throw new ApiValidationException("The server returned an empty terms response.");
        }

        public async Task CreateTermsDraftAsync(string title, string content)
        {
            var response = await _httpClient.PostAsJsonAsync("platform/terms", new { title, content });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task UpdateTermsDraftAsync(int termsVersionId, string title, string content)
        {
            var response = await _httpClient.PutAsJsonAsync($"platform/terms/{termsVersionId}", new { title, content });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteTermsDraftAsync(int termsVersionId)
        {
            var response = await _httpClient.DeleteAsync($"platform/terms/{termsVersionId}");
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task PublishTermsAsync(int termsVersionId, bool requiresAcceptance)
        {
            var response = await _httpClient.PostAsJsonAsync($"platform/terms/{termsVersionId}/publish", new { requiresAcceptance });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<List<TermsAcceptanceModel>> GetTermsAcceptancesAsync(int termsVersionId)
        {
            var response = await _httpClient.GetAsync($"platform/terms/{termsVersionId}/acceptances");
            await EnsureSuccessWithMessageAsync(response);

            var items = await response.Content.ReadFromJsonAsync<List<TermsAcceptanceModel>>();
            return items ?? new List<TermsAcceptanceModel>();
        }

        public async Task<PlatformBiModel> GetPlatformBiAsync(int days)
        {
            var response = await _httpClient.GetAsync($"platform/bi/overview?days={days}");
            await EnsureSuccessWithMessageAsync(response);

            var bi = await response.Content.ReadFromJsonAsync<PlatformBiModel>();
            return bi ?? new PlatformBiModel();
        }

        public async Task<TenantTermsModel> GetTenantTermsAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/terms");
            await EnsureSuccessWithMessageAsync(response);

            var terms = await response.Content.ReadFromJsonAsync<TenantTermsModel>();
            return terms ?? new TenantTermsModel();
        }

        public async Task AcceptTenantTermsAsync(int companyId, int termsVersionId)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/terms/accept", new { termsVersionId });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<List<UserDirectoryItemModel>> GetUserDirectoryAsync()
        {
            var response = await _httpClient.GetAsync("platform/users");
            await EnsureSuccessWithMessageAsync(response);

            var items = await response.Content.ReadFromJsonAsync<List<UserDirectoryItemModel>>();
            return items ?? new List<UserDirectoryItemModel>();
        }

        public async Task UpdateTenantAsync(int companyId, string companyName, string businessAddress, string contactNo, string email)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"platform/tenants/{companyId}", new { companyName, businessAddress, contactNo, email });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<MyAccountModel> GetMyAccountAsync()
        {
            var response = await _httpClient.GetAsync("platform/account");
            await EnsureSuccessWithMessageAsync(response);

            var account = await response.Content.ReadFromJsonAsync<MyAccountModel>();
            return account ?? throw new ApiValidationException("The server returned an empty account response.");
        }

        public async Task<MyAccountModel> UpdateMyAccountAsync(string firstName, string lastName, string email, string contactNumber)
        {
            var response = await _httpClient.PutAsJsonAsync(
                "platform/account", new { firstName, lastName, email, contactNumber });
            await EnsureSuccessWithMessageAsync(response);

            var account = await response.Content.ReadFromJsonAsync<MyAccountModel>();
            return account ?? throw new ApiValidationException("The server returned an empty account response.");
        }

        public async Task ChangeMyPasswordAsync(string currentPassword, string newPassword)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "platform/account/change-password", new { currentPassword, newPassword });
            await EnsureSuccessWithMessageAsync(response);
        }

        private static async Task EnsureSuccessWithMessageAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string? message = null;

            try
            {
                var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

                if (body.ValueKind == System.Text.Json.JsonValueKind.Object
                    && body.TryGetProperty("message", out var messageElement))
                {
                    message = messageElement.GetString();
                }
            }
            catch
            {
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && AuthSession.Current != null)
            {
                throw new ApiAccessDeniedException("Your session has expired. Please log out and log in again.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && AuthSession.Current != null)
            {
                throw new ApiAccessDeniedException(
                    !string.IsNullOrWhiteSpace(message) ? message
                    : AuthSession.IsSuperAdmin ? "Access denied. This area requires Super Admin permissions."
                    : "You do not have permission to perform this action.");
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                throw new ApiValidationException(message);
            }

            response.EnsureSuccessStatusCode();
        }

        public async Task<List<CompanyModel>> GetCompaniesAsync()
        {
            var response = await _httpClient.GetAsync("companies");
            response.EnsureSuccessStatusCode();

            var companies = await response.Content.ReadFromJsonAsync<List<CompanyModel>>();
            return companies ?? new List<CompanyModel>();
        }

        public async Task<List<ProductModel>> GetProductsAsync(int companyId, bool includeInactive = false)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/products?includeInactive={includeInactive}");
            response.EnsureSuccessStatusCode();

            var products = await response.Content.ReadFromJsonAsync<List<ProductModel>>();
            return products ?? new List<ProductModel>();
        }

        public async Task ReactivateProductAsync(int companyId, int productId)
        {
            var response = await _httpClient.PutAsync($"tenant/{companyId}/products/{productId}/reactivate", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ProductModel?> CreateProductAsync(int companyId, ProductModel product)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/products", product);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ProductModel>();
        }

        public async Task UpdateProductAsync(int companyId, int productId, ProductModel product)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/products/{productId}", product);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteProductAsync(int companyId, int productId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/products/{productId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<CustomerModel>> GetCustomersAsync(int companyId, bool includeInactive = false)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/customers?includeInactive={includeInactive}");
            response.EnsureSuccessStatusCode();

            var customers = await response.Content.ReadFromJsonAsync<List<CustomerModel>>();
            return customers ?? new List<CustomerModel>();
        }

        public async Task ReactivateCustomerAsync(int companyId, int customerId)
        {
            var response = await _httpClient.PutAsync($"tenant/{companyId}/customers/{customerId}/reactivate", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task<CustomerModel?> CreateCustomerAsync(int companyId, CustomerModel customer)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/customers", customer);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<CustomerModel>();
        }

        public async Task UpdateCustomerAsync(int companyId, int customerId, CustomerModel customer)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/customers/{customerId}", customer);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteCustomerAsync(int companyId, int customerId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/customers/{customerId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<PromotionModel>> GetPromotionsAsync(int companyId, bool includeInactive = false)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/promotions?includeInactive={includeInactive}");
            response.EnsureSuccessStatusCode();

            var promotions = await response.Content.ReadFromJsonAsync<List<PromotionModel>>();
            return promotions ?? new List<PromotionModel>();
        }

        public async Task ReactivatePromotionAsync(int companyId, int promotionId)
        {
            var response = await _httpClient.PutAsync($"tenant/{companyId}/promotions/{promotionId}/reactivate", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task<PromotionModel?> CreatePromotionAsync(int companyId, PromotionModel promotion)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/promotions", promotion);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PromotionModel>();
        }

        public async Task UpdatePromotionAsync(int companyId, int promotionId, PromotionModel promotion)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/promotions/{promotionId}", promotion);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeletePromotionAsync(int companyId, int promotionId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/promotions/{promotionId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<LoyaltyModel>> GetLoyaltyTransactionsAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/loyalty");
            response.EnsureSuccessStatusCode();

            var transactions = await response.Content.ReadFromJsonAsync<List<LoyaltyModel>>();
            return transactions ?? new List<LoyaltyModel>();
        }

        public async Task<LoyaltyModel?> CreateLoyaltyTransactionAsync(int companyId, LoyaltyModel transaction)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/loyalty", transaction);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<LoyaltyModel>();
        }

        public async Task UpdateLoyaltyTransactionAsync(int companyId, int loyaltyTransactionId, LoyaltyModel transaction)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/loyalty/{loyaltyTransactionId}", transaction);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteLoyaltyTransactionAsync(int companyId, int loyaltyTransactionId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/loyalty/{loyaltyTransactionId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<FeedbackModel>> GetFeedbackAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/feedback");
            response.EnsureSuccessStatusCode();

            var feedback = await response.Content.ReadFromJsonAsync<List<FeedbackModel>>();
            return feedback ?? new List<FeedbackModel>();
        }

        public async Task<FeedbackModel?> CreateFeedbackAsync(int companyId, FeedbackModel feedback)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/feedback", feedback);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<FeedbackModel>();
        }

        public async Task UpdateFeedbackAsync(int companyId, int feedbackId, FeedbackModel feedback)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/feedback/{feedbackId}", feedback);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteFeedbackAsync(int companyId, int feedbackId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/feedback/{feedbackId}");
            response.EnsureSuccessStatusCode();
        }
        public async Task<List<SalesTransactionModel>> GetSalesTransactionsAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/sales");
            response.EnsureSuccessStatusCode();

            var transactions = await response.Content.ReadFromJsonAsync<List<SalesTransactionModel>>();
            return transactions ?? new List<SalesTransactionModel>();
        }

        public async Task<SalesTransactionModel?> CreateSalesTransactionAsync(int companyId, SalesTransactionModel transaction)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/sales", transaction);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<SalesTransactionModel>();
        }

        public async Task UpdateSalesTransactionAsync(int companyId, int transactionId, SalesTransactionModel transaction)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/sales/{transactionId}", transaction);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteSalesTransactionAsync(int companyId, int transactionId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/sales/{transactionId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<TransactionItemModel>> GetTransactionItemsAsync(int companyId, int transactionId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/sales/{transactionId}/items");
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<TransactionItemModel>>();
            return items ?? new List<TransactionItemModel>();
        }

        public async Task<TransactionItemModel?> CreateTransactionItemAsync(int companyId, int transactionId, TransactionItemModel item)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/sales/{transactionId}/items", item);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TransactionItemModel>();
        }

        public async Task UpdateTransactionItemAsync(int companyId, int transactionId, int itemId, TransactionItemModel item)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/sales/{transactionId}/items/{itemId}", item);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteTransactionItemAsync(int companyId, int transactionId, int itemId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/sales/{transactionId}/items/{itemId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task UpdateFeedbackStatusAsync(int companyId, int feedbackId, string status)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/feedback/{feedbackId}/status", status);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<InquiryModel>> GetInquiriesAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/inquiries");
            response.EnsureSuccessStatusCode();
            var inquiries = await response.Content.ReadFromJsonAsync<List<InquiryModel>>();
            return inquiries ?? new List<InquiryModel>();
        }

        public async Task<InquiryModel?> CreateInquiryAsync(int companyId, InquiryModel inquiry)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/inquiries", inquiry);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<InquiryModel>();
        }

        public async Task UpdateInquiryAsync(int companyId, int inquiryId, InquiryModel inquiry)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/inquiries/{inquiryId}", inquiry);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteInquiryAsync(int companyId, int inquiryId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/inquiries/{inquiryId}");
            response.EnsureSuccessStatusCode();
        }

        private static string BuildMonthQuery(int? year, int? month, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (startDate != null && endDate != null)
            {
                string fromDate = startDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string toDate = endDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return $"?startDate={fromDate}&endDate={toDate}";
            }

            if (year == null || month == null)
            {
                return string.Empty;
            }

            return $"?year={year.Value}&month={month.Value}";
        }

        public async Task<ReportsOverviewModel> GetReportsOverviewAsync(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/overview{BuildMonthQuery(year, month, startDate, endDate)}");
            response.EnsureSuccessStatusCode();
            var overview = await response.Content.ReadFromJsonAsync<ReportsOverviewModel>();
            return overview ?? new ReportsOverviewModel();
        }

        public async Task<List<ReportInsightModel>> GetReportInsightsAsync(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/insights{BuildMonthQuery(year, month, startDate, endDate)}");
            response.EnsureSuccessStatusCode();
            var insights = await response.Content.ReadFromJsonAsync<List<ReportInsightModel>>();
            return insights ?? new List<ReportInsightModel>();
        }

        public async Task<ReportChartsModel> GetReportChartsAsync(int companyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/charts{BuildMonthQuery(year, month, startDate, endDate)}");
            response.EnsureSuccessStatusCode();
            var charts = await response.Content.ReadFromJsonAsync<ReportChartsModel>();
            return charts ?? new ReportChartsModel();
        }

        public async Task<DashboardModel> GetDashboardAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/dashboard");
            response.EnsureSuccessStatusCode();
            var dashboard = await response.Content.ReadFromJsonAsync<DashboardModel>();
            return dashboard ?? new DashboardModel();
        }

        public async Task<GeneratedReportModel> GenerateReportAsync(int companyId, string reportType, DateTime? startDate = null, DateTime? endDate = null)
        {
            string query = $"?reportType={Uri.EscapeDataString(reportType)}";

            if (startDate != null && endDate != null)
            {
                string fromDate = startDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string toDate = endDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                query += $"&startDate={fromDate}&endDate={toDate}";
            }

            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/generate{query}");
            response.EnsureSuccessStatusCode();
            var report = await response.Content.ReadFromJsonAsync<GeneratedReportModel>();
            return report ?? new GeneratedReportModel();
        }

    }
}