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

        public async Task UpdateInquiryStatusAsync(int companyId, int inquiryId, string status)
        {
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/inquiries/{inquiryId}/status", status);
            response.EnsureSuccessStatusCode();
        }

        public async Task RespondToInquiryAsync(int companyId, int inquiryId, string responseText, string respondedBy)
        {
            var payload = new { response = responseText, respondedBy };
            var response = await _httpClient.PutAsJsonAsync($"tenant/{companyId}/inquiries/{inquiryId}/respond", payload);
            response.EnsureSuccessStatusCode();
        }
    }
}