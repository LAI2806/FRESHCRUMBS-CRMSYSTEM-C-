using System.Text.RegularExpressions;

namespace freshcrumbs.CRM.api.Services
{
    // Small shared checks for tenant request fields, matching the database column limits and the
    // rules the desktop forms already apply. Text is trimmed but otherwise free: names, product names,
    // addresses and messages may contain spaces, hyphens, apostrophes, periods, quotes and any Unicode letters.
    public static class InputRules
    {
        public const decimal MaxMoney = 999_999.99m;
        public const int MaxQuantity = 99_999;
        public const int MaxStock = 999_999;
        public const int MaxPoints = 999_999;

        // Same patterns as the desktop ValidationHelper.
        private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        private static readonly Regex PhilippineMobilePattern = new(@"^09\d{9}$", RegexOptions.Compiled);

        // Trims the value; returns an error message (null = valid). Optional empty text becomes "".
        public static string? Text(string? value, string label, int maxLength, bool required, out string cleaned)
        {
            cleaned = (value ?? string.Empty).Trim();

            if (required && cleaned.Length == 0)
            {
                return $"{label} is required.";
            }

            if (cleaned.Length > maxLength)
            {
                return $"{label} cannot be longer than {maxLength} characters.";
            }

            return null;
        }

        // The allowed value in its standard spelling (case-insensitive match), or null when not allowed.
        public static string? OneOf(string? value, params string[] allowed)
        {
            var trimmed = (value ?? string.Empty).Trim();
            return allowed.FirstOrDefault(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsValidEmail(string value) => EmailPattern.IsMatch(value);

        public static bool IsValidPhilippineMobile(string value) => PhilippineMobilePattern.IsMatch(value);

        // A peso amount that fits decimal(18,2) as entered (no hidden rounding).
        public static bool IsMoney(decimal value, decimal min = 0m) =>
            value >= min && value <= MaxMoney && decimal.Round(value, 2) == value;

        // A record date: not empty and not in the future (one day of tolerance for time zones).
        public static bool IsRecordDate(DateTime value) =>
            value.Year >= 2000 && value.Date <= DateTime.UtcNow.Date.AddDays(1);

        public static object Message(string message) => new { message };
    }
}
