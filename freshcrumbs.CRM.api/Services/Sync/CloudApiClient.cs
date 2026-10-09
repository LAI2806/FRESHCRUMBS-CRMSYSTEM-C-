using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace freshcrumbs.CRM.api.Services.Sync
{
    public class CloudResult<T>
    {
        public bool Ok { get; set; }

        // True for network problems (no internet, DNS, timeout, connection dropped...). NOT for HTTP errors.
        public bool Unreachable { get; set; }

        public HttpStatusCode? StatusCode { get; set; }

        public T? Data { get; set; }

        public string? Message { get; set; }
    }

    // The only place on the desktop that talks to the cloud API. Every call has a short timeout and
    // never throws for network problems, so a bad connection can never break normal (local) work.
    public class CloudApiClient
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;
        private readonly SyncOptions _options;

        public CloudApiClient(SyncOptions options)
        {
            _options = options;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds)) };
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.CloudApiUrl);

        public async Task<bool> PingAsync(CancellationToken ct)
        {
            var result = await SendAsync<JsonElement>(HttpMethod.Get, "api/sync/ping", null, null, ct);
            return result.Ok;
        }

        public Task<CloudResult<JsonElement>> LoginAsync(string userName, string password, CancellationToken ct)
        {
            return SendAsync<JsonElement>(HttpMethod.Post, "api/auth/login", null, new { userName, password }, ct);
        }

        public Task<CloudResult<CompanySnapshotDto>> GetSnapshotAsync(string token, int companyId, CancellationToken ct)
        {
            return SendAsync<CompanySnapshotDto>(HttpMethod.Get, $"api/tenant/{companyId}/subscription/snapshot", token, null, ct);
        }

        public Task<CloudResult<SyncTokenDto>> GetSyncTokenAsync(string token, CancellationToken ct)
        {
            return SendAsync<SyncTokenDto>(HttpMethod.Post, "api/auth/sync-token", token, null, ct);
        }

        public Task<CloudResult<PushResponse>> PushAsync(string token, int companyId, PushRequest request, CancellationToken ct)
        {
            return SendAsync<PushResponse>(HttpMethod.Post, $"api/tenant/{companyId}/sync/push", token, request, ct);
        }

        public Task<CloudResult<PullResponse>> PullAsync(
            string token, int companyId, string type, DateTime? sinceUtc, int skip, int take, CancellationToken ct)
        {
            var url = $"api/tenant/{companyId}/sync/pull?type={Uri.EscapeDataString(type)}&skip={skip}&take={take}";

            if (sinceUtc != null)
            {
                url += "&since=" + Uri.EscapeDataString(DateTime.SpecifyKind(sinceUtc.Value, DateTimeKind.Utc).ToString("O"));
            }

            return SendAsync<PullResponse>(HttpMethod.Get, url, token, null, ct);
        }

        // Cloud-only operations requested by a signed-in desktop user (the cloud endpoint must answer with a JSON body).
        public Task<CloudResult<JsonElement>> SendJsonAsync(HttpMethod method, string relativeUrl, string token, object? body, CancellationToken ct)
        {
            return SendAsync<JsonElement>(method, relativeUrl, token, body, ct);
        }

        private async Task<CloudResult<T>> SendAsync<T>(HttpMethod method, string relativeUrl, string? bearer, object? body, CancellationToken ct)
        {
            if (!IsConfigured)
            {
                return new CloudResult<T> { Unreachable = true, Message = "Sync:CloudApiUrl is not configured." };
            }

            try
            {
                var baseUrl = _options.CloudApiUrl.TrimEnd('/') + "/";
                using var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), relativeUrl));

                if (!string.IsNullOrEmpty(bearer))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                }

                if (body != null)
                {
                    request.Content = JsonContent.Create(body, options: Json);
                }

                using var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    string? message = null;

                    try
                    {
                        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);

                        if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var m))
                        {
                            message = m.GetString();
                        }
                    }
                    catch
                    {
                        // body was not JSON
                    }

                    // 5xx from a hosting provider / gateway is treated like "cloud not usable right now".
                    var serverProblem = (int)response.StatusCode >= 500;

                    return new CloudResult<T>
                    {
                        Ok = false,
                        Unreachable = serverProblem,
                        StatusCode = response.StatusCode,
                        Message = message ?? $"Cloud returned {(int)response.StatusCode}."
                    };
                }

                var data = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return new CloudResult<T> { Ok = true, StatusCode = response.StatusCode, Data = data };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or OperationCanceledException)
            {
                // Includes timeouts and connections dropped in the middle of a request.
                return new CloudResult<T> { Unreachable = true, Message = ex.Message };
            }
        }
    }
}