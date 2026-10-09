using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace freshcrumbs.CRM.winforms.Services
{
    public class SyncStatusModel
    {
        // "Cloud" when the API runs in cloud mode (no local sync); otherwise Local.
        public string Mode { get; set; } = "Cloud";

        // Online / Offline / Syncing / NeedsSignIn / Error
        public string State { get; set; } = "Offline";

        public bool IsOnline { get; set; }

        public int Pending { get; set; }

        public int Rejected { get; set; }

        public DateTime? LastSyncUtc { get; set; }

        public string? LastError { get; set; }

        public double? GraceDaysLeft { get; set; }
    }

    // Talks to the LOCAL API only (same address as ApiService). It never talks to the cloud itself.
    public class SyncStatusClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private const string BaseUrl = "https://localhost:7230/api/";

        private readonly HttpClient _http;

        public SyncStatusClient()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public async Task<SyncStatusModel?> GetStatusAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "sync/status");
            Authorize(request);

            using var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SyncStatusModel>(Json);
        }

        public async Task SyncNowAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "sync/now");
            Authorize(request);

            using var response = await _http.SendAsync(request);
        }

        private static void Authorize(HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(AuthSession.Token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthSession.Token);
            }
        }
    }
}