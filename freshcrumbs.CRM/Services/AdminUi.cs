namespace freshcrumbs.CRM.winforms.Services
{
    // Small shared helpers so every Super Admin screen reports loading, success and error states the same way.
    public static class AdminUi
    {
        public static readonly Color SuccessColor = Color.FromArgb(46, 130, 80);
        public static readonly Color ErrorColor = Color.Firebrick;
        public static readonly Color InfoColor = Color.FromArgb(120, 110, 100);

        public static readonly Color Accent = Color.FromArgb(210, 140, 60);
        public static readonly Color Green = Color.FromArgb(90, 150, 120);
        public static readonly Color Brown = Color.FromArgb(140, 100, 70);
        public static readonly Color Red = Color.FromArgb(200, 80, 80);
        public static readonly Color Steel = Color.FromArgb(70, 130, 180);
        public static readonly Color Purple = Color.FromArgb(125, 95, 170);
        public static readonly Color Neutral = Color.FromArgb(170, 165, 160);

        public static readonly Color[] ChartColors = { Accent, Green, Purple, Steel, Brown, Red };

        public static string GetMessage(Exception ex)
        {
            return ex is ApiValidationException ? ex.Message : ErrorMessageHelper.GetFriendlyMessage(ex);
        }

        public static bool IsAccessDenied(Exception ex) => ex is ApiAccessDeniedException;

        public static void ShowInfo(Label label, string message) => Show(label, message, InfoColor);

        public static void ShowSuccess(Label label, string message) => Show(label, message, SuccessColor);

        public static void ShowError(Label label, Exception ex) => Show(label, GetMessage(ex), ErrorColor);

        private static void Show(Label label, string message, Color color)
        {
            label.ForeColor = color;
            label.Text = message;
        }
    }
}