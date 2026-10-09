namespace freshcrumbs.CRM.winforms.Services
{
    // Shared look for the PREMIUM branch screens (same colors and controls as Product Management).
    public static class BranchUi
    {
        public static readonly Color Accent = Color.FromArgb(210, 140, 60);
        public static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        public static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        public static readonly Color Border = Color.FromArgb(220, 210, 200);
        public static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        public static readonly Color HeaderRow = Color.FromArgb(250, 246, 242);

        public static string GetMessage(Exception ex)
        {
            return ex is ApiValidationException or ApiAccessDeniedException
                ? ex.Message
                : ErrorMessageHelper.GetFriendlyMessage(ex);
        }

        public static Button CreateButton(string text, bool primary, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : LabelGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;
            return button;
        }

        public static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(235, 230, 225),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9.5f),
                RowTemplate = { Height = 36 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                AutoGenerateColumns = true
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRow;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            grid.DefaultCellStyle.SelectionForeColor = TextDark;
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            return grid;
        }

        // Applies headers; columns not listed are hidden.
        public static void ShowColumns(DataGridView grid, params (string Name, string Header)[] columns)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.Visible = false;
            }

            int index = 0;
            foreach (var (name, header) in columns)
            {
                var column = grid.Columns[name];
                if (column == null)
                {
                    continue;
                }

                column.Visible = true;
                column.HeaderText = header;
                column.DisplayIndex = index++;
            }
        }

        public static Label CreateTitle(string text, float size)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
        }

        public static Label CreateFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        public static Label CreateMessageLabel()
        {
            return new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                MaximumSize = new Size(700, 0),
                Margin = new Padding(0, 6, 0, 6)
            };
        }
    }
}
