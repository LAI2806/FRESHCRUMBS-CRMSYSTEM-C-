namespace freshcrumbs.CRM.winforms.Services
{
    public static class AdminTheme
    {
        public static readonly Color SidebarBg = Color.FromArgb(22, 30, 46);
        public static readonly Color SidebarBorder = Color.FromArgb(40, 52, 75);
        public static readonly Color SidebarHover = Color.FromArgb(33, 45, 68);
        public static readonly Color SidebarActiveBg = Color.FromArgb(37, 99, 235);
        public static readonly Color SidebarText = Color.FromArgb(203, 213, 225);
        public static readonly Color SidebarMuted = Color.FromArgb(112, 128, 154);

        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        public static readonly Color TextDark = Color.FromArgb(15, 23, 42);
        public static readonly Color LabelGray = Color.FromArgb(100, 116, 139);
        public static readonly Color Border = Color.FromArgb(226, 232, 240);
        public static readonly Color HeaderRow = Color.FromArgb(248, 250, 252);
        public static readonly Color RowSelected = Color.FromArgb(232, 240, 254);

        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger = Color.FromArgb(220, 38, 38);
        public static readonly Color Neutral = Color.FromArgb(100, 116, 139);
        public static readonly Color Teal = Color.FromArgb(13, 148, 136);
        public static readonly Color Indigo = Color.FromArgb(99, 102, 241);

        public static Color StatusColor(string status) => status switch
        {
            "Active" => Success,
            "Expired" => Warning,
            "Cancelled" => Danger,
            "Suspended" => Neutral,
            "Scheduled" => Indigo,
            _ => Neutral
        };
    }
}