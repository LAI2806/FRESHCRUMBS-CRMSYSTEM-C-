using System.Collections.Concurrent;

namespace freshcrumbs.CRM.api.Services.Sync
{
    // Desktop (Local mode): the cloud session token of each user who signed in ONLINE on this computer.
    // Kept in memory only (never written to disk) and used only for operations that must run in the cloud,
    // such as managing employee accounts. Restarting the local API or an offline sign-in leaves none.
    public class CloudSessionTokens
    {
        private readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAtUtc)> _tokens = new();

        public void Set(string userId, string token, DateTime expiresAtUtc)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return;
            }

            _tokens[userId] = (token, expiresAtUtc);
        }

        public string? Get(string? userId)
        {
            if (string.IsNullOrEmpty(userId) || !_tokens.TryGetValue(userId, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(1))
            {
                _tokens.TryRemove(userId, out _);
                return null;
            }

            return entry.Token;
        }

        public void Remove(string userId)
        {
            _tokens.TryRemove(userId, out _);
        }
    }
}
