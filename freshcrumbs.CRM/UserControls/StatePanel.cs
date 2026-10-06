namespace freshcrumbs.CRM.winforms.UserControls
{
    // Centered message used for empty, error and access-denied states.
    public class StatePanel : UserControl
    {
        public StatePanel(string title, string message, string? buttonText = null, Action? onButtonClick = null)
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(244, 244, 246);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = BackColor
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62f));

            layout.Controls.Add(new Label
            {
                Text = title,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 35, 25)
            }, 0, 1);

            layout.Controls.Add(new Label
            {
                Text = message,
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = Color.FromArgb(120, 110, 100),
                Margin = new Padding(0, 8, 0, 12)
            }, 0, 2);

            if (buttonText != null && onButtonClick != null)
            {
                var button = new Button
                {
                    Text = buttonText,
                    Width = 140,
                    Height = 36,
                    Anchor = AnchorStyles.None,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(210, 140, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                button.FlatAppearance.BorderSize = 0;
                button.Click += (s, e) => onButtonClick();
                layout.Controls.Add(button, 0, 3);
            }

            Controls.Add(layout);
        }
    }
}