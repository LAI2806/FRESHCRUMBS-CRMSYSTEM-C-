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

        public async Task<RegisteredSubscriberModel> RegisterSubscriberAsync(
            string companyCode, string companyName, string businessAddress,
            string contactNo, string email, int planId, DateTime startDate,
            TenantUserModel admin, string databaseKey)
        {
            var response = await _httpClient.PostAsJsonAsync("platform/subscribers", new
            {
                companyCode,
                companyName,
                businessAddress,
                contactNo,
                email,
                planId,
                startDate,
                adminFirstName = admin.FirstName,
                adminLastName = admin.LastName,
                adminEmail = admin.Email,
                adminContactNumber = admin.ContactNumber,
                databaseKey
            });
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<RegisteredSubscriberModel>()
                ?? throw new ApiValidationException("The server returned an empty registration response.");
        }

        // SuperAdmin: the first ADMIN of a company that has no active Admin (one-time temporary password).
        public async Task<CreatedTenantAdminModel> CreateTenantAdminAsync(int companyId, TenantUserModel admin)
        {
            var response = await _httpClient.PostAsJsonAsync($"platform/subscribers/{companyId}/admin", new
            {
                admin.FirstName,
                admin.LastName,
                admin.Email,
                admin.ContactNumber
            });
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<CreatedTenantAdminModel>()
                ?? throw new ApiValidationException("The server returned an empty account response.");
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

        // Tenant employees: set a new password (required after signing in with a temporary password).
        public async Task ChangeOwnPasswordAsync(string currentPassword, string newPassword)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "auth/change-password", new { currentPassword, newPassword });
            await EnsureSuccessWithMessageAsync(response);
        }

        // My Account (MainCRM): always the signed-in user's own account; the server takes it from the token.
        public async Task<TenantAccountModel> GetOwnAccountAsync()
        {
            var response = await _httpClient.GetAsync("auth/me/account");
            await EnsureSuccessWithMessageAsync(response);

            var account = await response.Content.ReadFromJsonAsync<TenantAccountModel>();
            return account ?? throw new ApiValidationException("The server returned an empty account response.");
        }

        public async Task<TenantAccountModel> UpdateOwnAccountAsync(string firstName, string lastName, string email, string contactNumber)
        {
            var response = await _httpClient.PutAsJsonAsync(
                "auth/me/account", new { firstName, lastName, email, contactNumber });
            await EnsureSuccessWithMessageAsync(response);

            var account = await response.Content.ReadFromJsonAsync<TenantAccountModel>();
            return account ?? throw new ApiValidationException("The server returned an empty account response.");
        }

        // Best effort: the local API forgets this user's in-memory cloud session. Logging out never fails because of it.
        public async Task LogoutAsync()
        {
            try
            {
                using var response = await _httpClient.PostAsync("auth/logout", null);
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }

        // The API answers with { message }, a validation problem ({ title, errors }), a JSON string or plain text.
        private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
        {
            try
            {
                string text = (await response.Content.ReadAsStringAsync()).Trim();

                if (text.Length == 0)
                {
                    return null;
                }

                if (text[0] == '<')
                {
                    return null;
                }

                if (text[0] != '{' && text[0] != '"')
                {
                    return text.Length <= 300 ? text : null;
                }

                using var document = System.Text.Json.JsonDocument.Parse(text);
                var body = document.RootElement;

                if (body.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return body.GetString();
                }

                if (body.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    return null;
                }

                if (body.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return messageElement.GetString();
                }

                if (body.TryGetProperty("errors", out var errors) && errors.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind == System.Text.Json.JsonValueKind.Array && field.Value.GetArrayLength() > 0)
                        {
                            return "Please check the entered values: " + field.Value[0].GetString();
                        }
                    }
                }

                return body.TryGetProperty("title", out var title) && title.ValueKind == System.Text.Json.JsonValueKind.String
                    ? title.GetString()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task EnsureSuccessWithMessageAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string? message = null;

            // Only request errors (4xx) and "cloud required" (503) carry a message meant for the user; a server
            // error (500) is never shown as text, so no internal detail can reach the screen.
            int status = (int)response.StatusCode;

            if (status < 500 || status == 503)
            {
                message = await ReadErrorMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<ProductModel?> CreateProductAsync(int companyId, ProductModel product)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/products", product);
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<ProductModel>();
        }

        public async Task UpdateProductAsync(int companyId, int productId, ProductModel product)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/products/{productId}", product);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteProductAsync(int companyId, int productId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/products/{productId}");
            await EnsureSuccessWithMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<CustomerModel?> CreateCustomerAsync(int companyId, CustomerModel customer)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/customers", customer);
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<CustomerModel>();
        }

        public async Task UpdateCustomerAsync(int companyId, int customerId, CustomerModel customer)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/customers/{customerId}", customer);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteCustomerAsync(int companyId, int customerId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/customers/{customerId}");
            await EnsureSuccessWithMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<PromotionModel?> CreatePromotionAsync(int companyId, PromotionModel promotion)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/promotions", promotion);
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<PromotionModel>();
        }

        public async Task UpdatePromotionAsync(int companyId, int promotionId, PromotionModel promotion)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/promotions/{promotionId}", promotion);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeletePromotionAsync(int companyId, int promotionId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/promotions/{promotionId}");
            await EnsureSuccessWithMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<LoyaltyModel>();
        }

        public async Task UpdateLoyaltyTransactionAsync(int companyId, int loyaltyTransactionId, LoyaltyModel transaction)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/loyalty/{loyaltyTransactionId}", transaction);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteLoyaltyTransactionAsync(int companyId, int loyaltyTransactionId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/loyalty/{loyaltyTransactionId}");
            await EnsureSuccessWithMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<FeedbackModel>();
        }

        public async Task UpdateFeedbackAsync(int companyId, int feedbackId, FeedbackModel feedback)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/feedback/{feedbackId}", feedback);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteFeedbackAsync(int companyId, int feedbackId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/feedback/{feedbackId}");
            await EnsureSuccessWithMessageAsync(response);
        }
        public async Task<List<SalesTransactionModel>> GetSalesTransactionsAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/sales");
            response.EnsureSuccessStatusCode();

            var transactions = await response.Content.ReadFromJsonAsync<List<SalesTransactionModel>>();
            return transactions ?? new List<SalesTransactionModel>();
        }

        // One request: the sale and all its items are saved together. The server takes prices from the products
        // and calculates the totals; for MANAGER / STAFF on Branching plans it also decides the branch.
        public async Task<SalesTransactionModel?> CreateSalesTransactionAsync(
            int companyId,
            SalesTransactionModel transaction,
            List<TransactionItemModel>? items = null)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/sales", new
            {
                transaction.CustomerId,
                transaction.PromotionId,
                transaction.BranchId,
                transaction.TransactionDate,
                transaction.TotalAmount,
                transaction.DiscountAmount,
                transaction.CustomerDiscountAmount,
                transaction.PointsUsed,
                transaction.PointsEarned,
                transaction.FinalAmount,
                transaction.PaymentMethod,
                transaction.Status,
                TransactionItems = (items ?? new List<TransactionItemModel>())
                    .Select(i => new { i.ProductId, i.Quantity })
                    .ToList()
            });
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<SalesTransactionModel>();
        }

        // Next free customer code across the whole company (the visible list may be limited to one branch).
        public async Task<string?> GetNextCustomerCodeAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/customers/next-code");
            await EnsureSuccessWithMessageAsync(response);

            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            return body.ValueKind == System.Text.Json.JsonValueKind.Object && body.TryGetProperty("customerCode", out var code)
                ? code.GetString()
                : null;
        }

        // Exact customer code, company-wide, minimum data (used to serve a customer registered at another branch).
        public async Task<CustomerModel?> LookupCustomerByCodeAsync(int companyId, string code)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/customers/lookup?code={Uri.EscapeDataString(code)}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureSuccessWithMessageAsync(response);
            return await response.Content.ReadFromJsonAsync<CustomerModel>();
        }

        // Senior/PWD eligibility only (STAFF may do this; the rest of the customer record is unchanged).
        public async Task UpdateCustomerEligibilitiesAsync(int companyId, int customerId, List<CustomerDiscountEligibilityModel> eligibilities)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/customers/{customerId}/eligibilities", eligibilities);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task UpdateSalesTransactionAsync(int companyId, int transactionId, SalesTransactionModel transaction)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/sales/{transactionId}", transaction);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteSalesTransactionAsync(int companyId, int transactionId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/sales/{transactionId}");
            await EnsureSuccessWithMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);

            return await response.Content.ReadFromJsonAsync<TransactionItemModel>();
        }

        public async Task UpdateTransactionItemAsync(int companyId, int transactionId, int itemId, TransactionItemModel item)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/sales/{transactionId}/items/{itemId}", item);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteTransactionItemAsync(int companyId, int transactionId, int itemId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/sales/{transactionId}/items/{itemId}");
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<List<BranchModel>> GetBranchesAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/branches");
            await EnsureSuccessWithMessageAsync(response);

            var branches = await response.Content.ReadFromJsonAsync<List<BranchModel>>();
            return branches ?? new List<BranchModel>();
        }

        // The signed-in employee's active branch, or null when not assigned.
        public async Task<BranchModel?> GetMyBranchAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/branches/me");
            await EnsureSuccessWithMessageAsync(response);

            var mine = await response.Content.ReadFromJsonAsync<MyBranchResponse>();
            return mine?.BranchId == null
                ? null
                : new BranchModel { BranchId = mine.BranchId.Value, BranchName = mine.BranchName ?? string.Empty };
        }

        public async Task CreateBranchAsync(int companyId, BranchModel branch)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/branches", branch);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task UpdateBranchAsync(int companyId, int branchId, BranchModel branch)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/branches/{branchId}", branch);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<List<BranchInventoryModel>> GetBranchInventoryAsync(int companyId, int branchId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/branches/{branchId}/inventory");
            await EnsureSuccessWithMessageAsync(response);

            var rows = await response.Content.ReadFromJsonAsync<List<BranchInventoryModel>>();
            return rows ?? new List<BranchInventoryModel>();
        }

        // action: "In", "Out" or "Allocate".
        public async Task ChangeBranchStockAsync(int companyId, int branchId, int productId, int quantity, string action)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"tenant/{companyId}/branches/{branchId}/stock",
                new { productId, quantity, action });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<ProductBranchStockModel> GetProductBranchStockAsync(int companyId, int productId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/branches/products/{productId}");
            await EnsureSuccessWithMessageAsync(response);

            var stock = await response.Content.ReadFromJsonAsync<ProductBranchStockModel>();
            return stock ?? throw new ApiValidationException("The server returned an empty stock response.");
        }

        public async Task<List<BranchAssignmentModel>> GetBranchAssignmentsAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/branches/assignments");
            await EnsureSuccessWithMessageAsync(response);

            var rows = await response.Content.ReadFromJsonAsync<List<BranchAssignmentModel>>();
            return rows ?? new List<BranchAssignmentModel>();
        }

        public async Task SetBranchAssignmentAsync(int companyId, string userId, int? branchId)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"tenant/{companyId}/branches/assignments/{Uri.EscapeDataString(userId)}",
                new { branchId });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task<TenantUserListModel> GetTenantUsersAsync(int companyId)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/users");
            await EnsureSuccessWithMessageAsync(response);

            var list = await response.Content.ReadFromJsonAsync<TenantUserListModel>();
            return list ?? new TenantUserListModel();
        }

        public async Task<CreatedTenantUserModel> CreateTenantUserAsync(int companyId, TenantUserModel user)
        {
            var response = await _httpClient.PostAsJsonAsync($"tenant/{companyId}/users", new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.ContactNumber,
                user.Role
            });
            await EnsureSuccessWithMessageAsync(response);

            var created = await response.Content.ReadFromJsonAsync<CreatedTenantUserModel>();
            return created ?? throw new ApiValidationException("The server returned an empty response.");
        }

        public async Task UpdateTenantUserAsync(int companyId, string userId, TenantUserModel user)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/users/{Uri.EscapeDataString(userId)}", new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.ContactNumber,
                user.Role
            });
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task SetTenantUserStatusAsync(int companyId, string userId, string status)
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"tenant/{companyId}/users/{Uri.EscapeDataString(userId)}/status", new { status });
            await EnsureSuccessWithMessageAsync(response);
        }

        private class MyBranchResponse
        {
            public int? BranchId { get; set; }

            public string? BranchName { get; set; }
        }

        public async Task UpdateFeedbackStatusAsync(int companyId, int feedbackId, string status)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/feedback/{feedbackId}/status", status);
            await EnsureSuccessWithMessageAsync(response);
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
            await EnsureSuccessWithMessageAsync(response);
            return await response.Content.ReadFromJsonAsync<InquiryModel>();
        }

        public async Task UpdateInquiryAsync(int companyId, int inquiryId, InquiryModel inquiry)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/inquiries/{inquiryId}", inquiry);
            await EnsureSuccessWithMessageAsync(response);
        }

        public async Task DeleteInquiryAsync(int companyId, int inquiryId)
        {
            var response = await _httpClient.DeleteAsync($"tenant/{companyId}/inquiries/{inquiryId}");
            await EnsureSuccessWithMessageAsync(response);
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

        public async Task<BranchDashboardModel> GetBranchDashboardAsync(int companyId, DateTime startDate, DateTime endDate)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/branches{BuildMonthQuery(null, null, startDate, endDate)}");
            await EnsureSuccessWithMessageAsync(response);
            var branches = await response.Content.ReadFromJsonAsync<BranchDashboardModel>();
            return branches ?? new BranchDashboardModel();
        }

        public async Task<OperationalReportModel> GetOperationalReportAsync(int companyId, DateTime startDate, DateTime endDate)
        {
            var response = await _httpClient.GetAsync($"tenant/{companyId}/reports/operations{BuildMonthQuery(null, null, startDate, endDate)}");
            await EnsureSuccessWithMessageAsync(response);
            var report = await response.Content.ReadFromJsonAsync<OperationalReportModel>();
            return report ?? new OperationalReportModel();
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