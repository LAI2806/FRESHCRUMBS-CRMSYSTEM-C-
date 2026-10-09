using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace freshcrumbs.CRM.api.Services.Sync
{
    public class CachedUser
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? Role { get; set; }
        public List<string> Roles { get; set; } = new();

        // ASP.NET Identity PBKDF2 hash created on THIS machine from the password typed at the last
        // successful online login. The password itself is never stored.
        public string PasswordHash { get; set; } = string.Empty;

        // The account still uses the one-time temporary password its ADMIN received.
        public bool MustChangePassword { get; set; }

        public DateTime LastOnlineLoginUtc { get; set; }
        public int FailedOfflineAttempts { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
    }

    public class CachedCompany
    {
        public CompanySnapshotDto Snapshot { get; set; } = new();

        // Last time the cloud confirmed the user / company / subscription. Drives the offline grace period.
        public DateTime LastCloudValidatedUtc { get; set; }
    }

    public class CachedSyncToken
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class AccessCacheDoc
    {
        public Dictionary<string, CachedUser> Users { get; set; } = new();
        public Dictionary<int, CachedCompany> Companies { get; set; } = new();
        public Dictionary<int, CachedSyncToken> SyncTokens { get; set; } = new();

        // Highest clock value ever seen; a clock set back beyond this is treated as tampering.
        public DateTime LastSeenUtc { get; set; }
    }

    // Everything the desktop needs to enforce users and subscription rules while offline.
    // Stored encrypted with ASP.NET Data Protection (DPAPI on Windows) in the user's profile folder.
    public class LocalAccessCache
    {
        private const string Purpose = "FreshCrumbs.LocalAccessCache.v1";
        private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(10);

        private readonly IDataProtector _protector;
        private readonly SyncOptions _options;
        private readonly string _path;
        private readonly object _gate = new();
        private AccessCacheDoc? _doc;

        public LocalAccessCache(IDataProtectionProvider provider, SyncOptions options)
        {
            _protector = provider.CreateProtector(Purpose);
            _options = options;

            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FreshCrumbs");
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, "access-cache.bin");
        }

        // Runs a read-only action against a copy-safe view of the cache.
        public T Read<T>(Func<AccessCacheDoc, T> reader)
        {
            lock (_gate)
            {
                return reader(Load());
            }
        }

        public void Update(Action<AccessCacheDoc> mutate)
        {
            lock (_gate)
            {
                var doc = Load();
                mutate(doc);
                Save(doc);
            }
        }

        // Records "now" as seen (throttled to once a minute so normal use does not rewrite the file).
        public void Touch()
        {
            var now = DateTime.UtcNow;

            lock (_gate)
            {
                var doc = Load();

                if (now > doc.LastSeenUtc.AddMinutes(1))
                {
                    doc.LastSeenUtc = now;
                    Save(doc);
                }
            }
        }

        public bool ClockLooksTampered()
        {
            return Read(d => DateTime.UtcNow < d.LastSeenUtc - ClockTolerance);
        }

        // An account the last cloud snapshot lists as not Active is refused on this computer too.
        public bool IsUserInactive(int companyId, string userId)
        {
            return Read(d => d.Companies.TryGetValue(companyId, out var company)
                && company.Snapshot.Users.Any(u => u.Id == userId
                    && !string.Equals(u.Status, "Active", StringComparison.OrdinalIgnoreCase)));
        }

        public record GraceState(bool Valid, string Code, string Message, double DaysLeft);

        // Offline use is only allowed for a limited time after the last successful cloud validation.
        public GraceState GetGrace(int companyId)
        {
            return Read(doc =>
            {
                var now = DateTime.UtcNow;

                if (now < doc.LastSeenUtc - ClockTolerance)
                {
                    return new GraceState(false, "ClockInvalid",
                        "The computer's clock was set back. Connect to the internet and sign in again.", 0);
                }

                if (!doc.Companies.TryGetValue(companyId, out var company))
                {
                    return new GraceState(false, "OfflineGraceExpired",
                        "This company has not been validated online yet. Connect to the internet and sign in.", 0);
                }

                var deadline = company.LastCloudValidatedUtc.AddDays(_options.OfflineGraceDays);
                var left = (deadline - now).TotalDays;

                return left > 0
                    ? new GraceState(true, "Allowed", string.Empty, left)
                    : new GraceState(false, "OfflineGraceExpired",
                        $"The {_options.OfflineGraceDays}-day offline period has ended. Connect to the internet and sign in again.", 0);
            });
        }

        private AccessCacheDoc Load()
        {
            if (_doc != null)
            {
                return _doc;
            }

            try
            {
                if (File.Exists(_path))
                {
                    var json = _protector.Unprotect(File.ReadAllText(_path));
                    _doc = JsonSerializer.Deserialize<AccessCacheDoc>(json) ?? new AccessCacheDoc();
                    return _doc;
                }
            }
            catch (Exception ex)
            {
                // Unreadable or tampered file: start empty. The user must sign in online again.
                Console.Error.WriteLine("LocalAccessCache: could not read access-cache.bin, starting empty: " + ex.Message);
            }

            _doc = new AccessCacheDoc();
            return _doc;
        }

        private void Save(AccessCacheDoc doc)
        {
            _doc = doc;
            var protectedText = _protector.Protect(JsonSerializer.Serialize(doc));
            var temp = _path + ".tmp";
            File.WriteAllText(temp, protectedText);
            File.Move(temp, _path, overwrite: true);
        }
    }
}