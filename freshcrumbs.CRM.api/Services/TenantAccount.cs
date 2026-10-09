using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using freshcrumbs.CRM.domain.entities;
using freshcrumbs.CRM.infrastructure.data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace freshcrumbs.CRM.api.Services
{
    // Tenant employee accounts created by a company ADMIN.
    public static class TenantAccounts
    {
        // Identity user claim (AspNetUserClaims) on accounts that still use their one-time temporary password.
        public const string MustChangePasswordClaim = "must_change_password";

        public const string Active = "Active";
        public const string Inactive = "Inactive";

        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhonePattern = new(@"^[0-9+\-\s()]{7,20}$", RegexOptions.Compiled);

        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnopqrstuvwxyz";
        private const string Digits = "23456789";
        private const string Symbols = "!@#$%*?";

        // Random, satisfies the default Identity password rules (upper, lower, digit, symbol, length).
        // Look-alike characters (0/O, 1/l/I) are left out so it can be read out or typed without mistakes.
        public static string GenerateTemporaryPassword(int length = 12)
        {
            var all = Upper + Lower + Digits + Symbols;

            var chars = new List<char>
            {
                Pick(Upper),
                Pick(Lower),
                Pick(Digits),
                Pick(Symbols)
            };

            while (chars.Count < length)
            {
                chars.Add(Pick(all));
            }

            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }

        // Profile rules shared by Tenant User Management and My Account. Returns an error message or null.
        public static string? ValidateProfile(string firstName, string lastName, string email, string contactNumber)
        {
            if (firstName.Length == 0 || firstName.Length > 100)
            {
                return "First name is required (up to 100 characters).";
            }

            if (lastName.Length == 0 || lastName.Length > 100)
            {
                return "Last name is required (up to 100 characters).";
            }

            if (email.Length > 256 || !EmailPattern.IsMatch(email))
            {
                return "A valid email address is required.";
            }

            if (contactNumber.Length > 0 && !PhonePattern.IsMatch(contactNumber))
            {
                return "Contact number must be 7 to 20 characters (digits, +, -, spaces, parentheses).";
            }

            return null;
        }

        // The email is also the sign-in name, so it may not match another account's email or user name.
        public static async Task<bool> IsEmailInUseAsync(
            UserManager<ApplicationUser> userManager,
            MasterCrmDbContext masterDb,
            string email,
            string? exceptUserId)
        {
            var normalizedEmail = userManager.NormalizeEmail(email);
            var normalizedName = userManager.NormalizeName(email);

            return await masterDb.Users.AsNoTracking().AnyAsync(u =>
                u.Id != exceptUserId
                && (u.NormalizedEmail == normalizedEmail || u.NormalizedUserName == normalizedName));
        }

        // Creates a tenant account with a one-time temporary password and the "must change password" flag.
        // Shared by Tenant User Management (STAFF / MANAGER) and SuperAdmin onboarding (the company's first ADMIN).
        // The caller validates the profile, the email and the seat limit, and wraps this in its transaction.
        public static async Task<(string? TemporaryPassword, string? Error)> CreateWithTemporaryPasswordAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user)
        {
            var temporaryPassword = GenerateTemporaryPassword();

            var created = await userManager.CreateAsync(user, temporaryPassword);

            if (!created.Succeeded)
            {
                return (null, string.Join(" ", created.Errors.Select(e => e.Description)));
            }

            var flagged = await userManager.AddClaimAsync(user, new Claim(MustChangePasswordClaim, "true"));

            if (!flagged.Succeeded)
            {
                return (null, string.Join(" ", flagged.Errors.Select(e => e.Description)));
            }

            return (temporaryPassword, null);
        }

        // The email is the sign-in name, so both change together (saved by the caller's UpdateAsync).
        public static async Task SetEmailAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, string email)
        {
            if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            user.Email = email;
            user.UserName = email;
            await userManager.UpdateNormalizedEmailAsync(user);
            await userManager.UpdateNormalizedUserNameAsync(user);
        }

        private static char Pick(string source)
        {
            return source[RandomNumberGenerator.GetInt32(source.Length)];
        }
    }
}
