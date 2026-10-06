namespace freshcrumbs.CRM.winforms.UserControls
{
    public class PlaceholderControl : UserControl
    {
        public PlaceholderControl(string title, string message)
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(244, 244, 246);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            layout.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 35, 25),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(120, 110, 100),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 10, 0, 0)
            }, 0, 1);

            Controls.Add(layout);
        }
    }
}