using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using freshcrumbs.CRM.api.Authorization;
using freshcrumbs.CRM.domain.entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace freshcrumbs.CRM.api.Services.Sync
{

    public class LocalLoginService
    {
        private const int MaxOfflineAttempts = 5;
        private static readonly TimeSpan OfflineLockout = TimeSpan.FromMinutes(5);

        private readonly IConfiguration _configuration;
        private readonly LocalAccessCache _cache;
        private readonly CloudApiClient _cloud;
        private readonly SyncOptions _options;
        private readonly SyncStatusService _status;
        private readonly CloudSessionTokens _sessions;
        private readonly PasswordHasher<ApplicationUser> _hasher = new();
        private readonly ILogger<LocalLoginService> _log;

        public LocalLoginService(
            IConfiguration configuration,
            LocalAccessCache cache,
            CloudApiClient cloud,
            SyncOptions options,
            SyncStatusService status,
            CloudSessionTokens sessions,
            ILogger<LocalLoginService> log)
        {
            _sessions = sessions;
            _log = log;
            _configuration = configuration;
            _cache = cache;
            _cloud = cloud;
            _options = options;
            _status = status;
        }

        public async Task<IActionResult> LoginAsync(string userName, string password, CancellationToken ct)
        {
            userName = userName.Trim();

            var cloudLogin = await _cloud.LoginAsync(userName, password, ct);

            if (cloudLogin.Ok && cloudLogin.Data.ValueKind == JsonValueKind.Object)
            {
                return await OnlineLoginAsync(cloudLogin.Data, password, ct);
            }

            if (!cloudLogin.Unreachable)
            {
                // The cloud answered and said no (wrong password, locked, inactive...): that decision is final.
                if (cloudLogin.StatusCode == HttpStatusCode.Unauthorized || cloudLogin.StatusCode == HttpStatusCode.Forbidden)
                {
                    RemoveCachedUser(userName);
                }

                return Denied((int)(cloudLogin.StatusCode ?? HttpStatusCode.Unauthorized),
                    cloudLogin.Message ?? "Invalid username or password.");
            }

            _log.LogWarning("Cloud login unreachable ({Message}); using the offline cached login.", cloudLogin.Message);
            return OfflineLogin(userName, password);
        }

        private async Task<IActionResult> OnlineLoginAsync(JsonElement session, string password, CancellationToken ct)
        {
            if (session.TryGetProperty("isSuperAdmin", out var superAdmin) && superAdmin.GetBoolean())
            {
                return Denied(403, "Platform administrator accounts must use the cloud service, not the offline desktop app.");
            }

            if (!session.TryGetProperty("tenantId", out var tenantProp) || tenantProp.ValueKind != JsonValueKind.Number)
            {
                return Denied(403, "This account is not assigned to a company.");
            }

            var tenantId = tenantProp.GetInt32();
            var cloudToken = Str(session, "token");

            var snapshot = await _cloud.GetSnapshotAsync(cloudToken, tenantId, ct);

            if (!snapshot.Ok || snapshot.Data == null)
            {
                return Denied(503, "Signed in, but the cloud could not provide the subscription details. Please try again.");
            }

            var syncToken = await _cloud.GetSyncTokenAsync(cloudToken, ct);

            if (!syncToken.Ok || syncToken.Data == null || string.IsNullOrEmpty(syncToken.Data.Token))
            {
                _log.LogWarning("Cloud sync-token request failed ({Status} {Message}); background sync will not start until a later sign-in succeeds.", syncToken.StatusCode, syncToken.Message);
            }

            var user = new CachedUser
            {
                Id = Str(session, "userId"),
                UserName = Str(session, "userName"),
                Email = Str(session, "email"),
                FullName = Str(session, "fullName"),
                TenantId = tenantId,
                CompanyCode = Str(session, "companyCode"),
                CompanyName = Str(session, "companyName"),
                Role = session.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null,
                Roles = session.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array
                    ? roles.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                    : new List<string>(),
                MustChangePassword = session.TryGetProperty("mustChangePassword", out var mustChange)
                    && mustChange.ValueKind == JsonValueKind.True,
                LastOnlineLoginUtc = DateTime.UtcNow
            };

            if (session.TryGetProperty("expiresAt", out var cloudExpires)
                && cloudExpires.ValueKind == JsonValueKind.String
                && cloudExpires.TryGetDateTime(out var cloudExpiresAt))
            {
                _sessions.Set(user.Id, cloudToken, cloudExpiresAt.ToUniversalTime());
            }

            user.PasswordHash = _hasher.HashPassword(new ApplicationUser { UserName = user.UserName }, password);

            _cache.Update(doc =>
            {
                doc.Users[user.Id] = user;
                doc.Companies[tenantId] = new CachedCompany
                {
                    Snapshot = snapshot.Data,
                    LastCloudValidatedUtc = DateTime.UtcNow
                };

                if (syncToken.Ok && syncToken.Data != null)
                {
                    doc.SyncTokens[tenantId] = new CachedSyncToken
                    {
                        Token = syncToken.Data.Token,
                        ExpiresAtUtc = DateTime.SpecifyKind(syncToken.Data.ExpiresAt, DateTimeKind.Utc)
                    };
                }

                doc.LastSeenUtc = DateTime.UtcNow;
            });

            _status.Set("Online", online: true, clearError: true);

            return Ok(user, DateTime.UtcNow.AddMinutes(_configuration.GetValue("Jwt:ExpiryMinutes", 480)));
        }

        private IActionResult OfflineLogin(string userName, string password)
        {
            var key = userName.ToUpperInvariant();

            var cached = _cache.Read(d => d.Users.Values.FirstOrDefault(u =>
                u.UserName.ToUpperInvariant() == key || u.Email.ToUpperInvariant() == key));

            if (cached == null)
            {
                return Denied(503,
                    "You are offline and this account has not signed in on this computer before. Connect to the internet to sign in.");
            }

            var now = DateTime.UtcNow;

            if (cached.LockedUntilUtc != null && cached.LockedUntilUtc > now)
            {
                return Denied(403, "This account is temporarily locked. Please try again later.");
            }

            var verify = _hasher.VerifyHashedPassword(
                new ApplicationUser { UserName = cached.UserName }, cached.PasswordHash, password);

            if (verify == PasswordVerificationResult.Failed)
            {
                _cache.Update(doc =>
                {
                    if (doc.Users.TryGetValue(cached.Id, out var u))
                    {
                        u.FailedOfflineAttempts++;

                        if (u.FailedOfflineAttempts >= MaxOfflineAttempts)
                        {
                            u.LockedUntilUtc = DateTime.UtcNow.Add(OfflineLockout);
                            u.FailedOfflineAttempts = 0;
                        }
                    }
                });

                return Denied(401, "Invalid username or password.");
            }

            var grace = _cache.GetGrace(cached.TenantId);

            if (!grace.Valid)
            {
                return Denied(403, grace.Message);
            }

            if (!TenantRoles.IsValid(cached.Role))
            {
                return Denied(403, "This account does not have a valid role (ADMIN, MANAGER or STAFF). Please contact your administrator.");
            }

            if (_cache.IsUserInactive(cached.TenantId, cached.Id))
            {
                return Denied(403, "This account is inactive. Please contact your administrator.");
            }

            _cache.Update(doc =>
            {
                if (doc.Users.TryGetValue(cached.Id, out var u))
                {
                    u.FailedOfflineAttempts = 0;
                    u.LockedUntilUtc = null;
                }

                doc.LastSeenUtc = DateTime.UtcNow;
            });

            // A local session never outlives the offline grace period.
            var expires = now.AddMinutes(_configuration.GetValue("Jwt:ExpiryMinutes", 480));
            var deadline = now.AddDays(grace.DaysLeft);

            if (deadline < expires)
            {
                expires = deadline;
            }

            _status.Set("Offline", online: false);

            return Ok(cached, expires);
        }

        private void RemoveCachedUser(string userName)
        {
            var key = userName.ToUpperInvariant();

            _cache.Update(doc =>
            {
                var ids = doc.Users.Values
                    .Where(u => u.UserName.ToUpperInvariant() == key || u.Email.ToUpperInvariant() == key)
                    .Select(u => u.Id)
                    .ToList();

                foreach (var id in ids)
                {
                    doc.Users.Remove(id);
                }
            });
        }

        private IActionResult Ok(CachedUser user, DateTime expiresAt)
        {
            var token = CreateToken(user, expiresAt);

            return new OkObjectResult(new
            {
                token,
                expiresAt,
                userId = user.Id,
                userName = user.UserName,
                fullName = user.FullName,
                email = user.Email,
                roles = user.Roles,
                isSuperAdmin = false,
                role = TenantRoles.Normalize(user.Role),
                tenantId = (int?)user.TenantId,
                companyCode = user.CompanyCode,
                companyName = user.CompanyName,
                mustChangePassword = user.MustChangePassword
            });
        }

        // My Account: the cloud copy when online; otherwise a read-only copy of this computer's sign-in and company snapshot.
        public async Task<IActionResult> GetAccountAsync(string? userId, CancellationToken ct)
        {
            var cached = _cache.Read(d => userId != null && d.Users.TryGetValue(userId, out var u) ? u : null);

            if (userId == null || cached == null)
            {
                return Denied(401, "Please log in to continue.");
            }

            var cloudToken = _sessions.Get(userId);

            if (cloudToken != null)
            {
                var result = await _cloud.SendJsonAsync(HttpMethod.Get, "api/auth/me/account", cloudToken, null, ct);

                if (result.Ok)
                {
                    return new OkObjectResult(result.Data);
                }

                if (result.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _sessions.Remove(userId);
                }
                else if (!result.Unreachable)
                {
                    return Denied((int)(result.StatusCode ?? HttpStatusCode.BadRequest), result.Message ?? "Your account could not be loaded.");
                }
            }

            if (_cache.IsUserInactive(cached.TenantId, cached.Id))
            {
                return Denied(403, "This account is inactive. Please contact your administrator.");
            }

            var listed = _cache.Read(d => d.Companies.TryGetValue(cached.TenantId, out var company)
                ? company.Snapshot.Users.FirstOrDefault(u => u.Id == cached.Id)
                : null);

            return new OkObjectResult(new
            {
                firstName = listed?.FirstName ?? string.Empty,
                lastName = listed?.LastName ?? string.Empty,
                fullName = cached.FullName,
                userName = cached.UserName,
                email = cached.Email,
                contactNumber = string.Empty,
                role = TenantRoles.Normalize(cached.Role),
                companyName = cached.CompanyName,
                online = false
            });
        }

        // Account changes happen in the cloud (accounts live there). Needs an online sign-in on this computer.
        public async Task<IActionResult> UpdateAccountAsync(string? userId, object account, CancellationToken ct)
        {
            var cloudToken = _sessions.Get(userId);

            if (userId == null || cloudToken == null)
            {
                return Denied(503, "Updating your account needs the internet. Sign out, sign in again while online, then try again.");
            }

            var result = await _cloud.SendJsonAsync(HttpMethod.Put, "api/auth/me/account", cloudToken, account, ct);

            if (result.Unreachable)
            {
                return Denied(503, "You are offline. Updating your account needs the internet.");
            }

            if (!result.Ok)
            {
                if (result.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _sessions.Remove(userId);
                    return Denied(503, "Your online session has expired. Sign out and sign in again.");
                }

                return Denied((int)(result.StatusCode ?? HttpStatusCode.BadRequest), result.Message ?? "Your account could not be updated.");
            }

            // Offline sign-in on this computer looks the account up by user name / email, so keep the copy in step.
            var data = result.Data;
            var userName = Str(data, "userName");
            var email = Str(data, "email");
            var fullName = Str(data, "fullName");

            int? tenantId = null;

            _cache.Update(doc =>
            {
                if (doc.Users.TryGetValue(userId, out var cached))
                {
                    cached.UserName = userName.Length > 0 ? userName : cached.UserName;
                    cached.Email = email.Length > 0 ? email : cached.Email;
                    cached.FullName = fullName.Length > 0 ? fullName : cached.FullName;
                    tenantId = cached.TenantId;
                }
            });

            // The company snapshot lists the accounts too (offline My Account, branch assignment), so refresh it now.
            if (tenantId != null)
            {
                await RefreshCompanySnapshotAsync(cloudToken, tenantId.Value, ct);
            }

            return new OkObjectResult(data);
        }

        // Replaces this computer's copy of the company snapshot (plan, accounts, pending Terms) with the cloud's.
        // Returns false when the cloud could not provide it; the old copy is then kept.
        public async Task<bool> RefreshCompanySnapshotAsync(string cloudToken, int tenantId, CancellationToken ct)
        {
            var snapshot = await _cloud.GetSnapshotAsync(cloudToken, tenantId, ct);

            if (!snapshot.Ok || snapshot.Data == null)
            {
                return false;
            }

            _cache.Update(doc =>
            {
                doc.Companies[tenantId] = new CachedCompany
                {
                    Snapshot = snapshot.Data,
                    LastCloudValidatedUtc = DateTime.UtcNow
                };
            });

            return true;
        }

        // Password changes happen in the cloud (accounts live there). Needs an online sign-in on this computer.
        public async Task<IActionResult> ChangePasswordAsync(string? userId, string currentPassword, string newPassword, CancellationToken ct)
        {
            var cloudToken = _sessions.Get(userId);

            if (userId == null || cloudToken == null)
            {
                return Denied(503, "Changing your password needs the internet. Sign out, sign in again while online, then try again.");
            }

            var result = await _cloud.SendJsonAsync(HttpMethod.Post, "api/auth/change-password", cloudToken,
                new { currentPassword, newPassword }, ct);

            if (result.Unreachable)
            {
                return Denied(503, "You are offline. Changing your password needs the internet.");
            }

            if (!result.Ok)
            {
                return Denied(result.StatusCode == HttpStatusCode.Unauthorized ? 503 : (int)(result.StatusCode ?? HttpStatusCode.BadRequest),
                    result.StatusCode == HttpStatusCode.Unauthorized
                        ? "Your online session has expired. Sign out and sign in again."
                        : result.Message ?? "The password could not be changed.");
            }

            // Offline sign-in on this computer must accept the NEW password from now on.
            _cache.Update(doc =>
            {
                if (doc.Users.TryGetValue(userId, out var cached))
                {
                    cached.PasswordHash = _hasher.HashPassword(new ApplicationUser { UserName = cached.UserName }, newPassword);
                    cached.MustChangePassword = false;
                }
            });

            return new OkObjectResult(new { message = "Password changed." });
        }

        // Same claim layout as the cloud token, signed with the LOCAL key (Jwt:Key of the local API).
        private string CreateToken(CachedUser user, DateTime expiresAt)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName),
                new(TenantAccessRequirement.TenantIdClaim, user.TenantId.ToString())
            };

            claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

            if (user.MustChangePassword)
            {
                claims.Add(new Claim(TenantAccessFilter.PasswordChangeClaim, "true"));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        private static string Str(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? string.Empty
                : string.Empty;
        }

        private static IActionResult Denied(int status, string message)
        {
            return new ObjectResult(new { message }) { StatusCode = status };
        }
    }
}