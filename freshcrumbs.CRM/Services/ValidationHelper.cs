using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace freshcrumbs.CRM.winforms.Services
{
    public static class ValidationHelper
    {
        public static bool IsValidPhoneNumber(string value)
        {
            return Regex.IsMatch(value, @"^[0-9+\-\s()]{7,20}$");
        }

        public static bool IsValidPhilippineMobileNumber(string value)
        {
            return Regex.IsMatch(value, @"^09\d{9}$");
        }

        public static bool IsValidEmail(string value)
        {
            return Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        public static bool IsRequired(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        public static bool IsValidSelection(ComboBox comboBox)
        {
            return comboBox.SelectedIndex >= 0 && comboBox.SelectedItem != null;
        }

        public static bool IsNonNegative(decimal value)
        {
            return value >= 0;
        }

        public static bool IsNonNegative(int value)
        {
            return value >= 0;
        }

        public static bool IsPositive(decimal value)
        {
            return value > 0;
        }

        public static bool IsPositive(int value)
        {
            return value > 0;
        }

        public static bool IsValidDateRange(DateTime startDate, DateTime endDate)
        {
            return endDate >= startDate;
        }

        /// <summary>
        /// A promotion with RequiredLoyaltyPoints of 0 is not points-based and is
        /// always allowed. Otherwise the customer must hold at least that many points.
        /// </summary>
        public static bool HasSufficientLoyaltyPoints(int availablePoints, int requiredPoints)
        {
            return requiredPoints <= 0 || availablePoints >= requiredPoints;
        }
    }
}