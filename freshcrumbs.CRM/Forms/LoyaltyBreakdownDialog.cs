using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class LoyaltyBreakdownDialog : Form
    {
        private class BreakdownRow
        {
            public string Date { get; set; } = string.Empty;
            public int PointsEarned { get; set; }
            public int PointsUsed { get; set; }
            public string TransactionType { get; set; } = string.Empty;
        }

        public LoyaltyBreakdownDialog(string customerName, List<LoyaltyModel> transactions)
        {
            Text = $"{customerName} - Loyalty Breakdown";
            Width = 560;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            var header = new Label
            {
                Text = $"{customerName} - Loyalty Breakdown",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 35, 25),
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(20, 15, 20, 0)
            };
            Controls.Add(header);

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9.5f),
                RowTemplate = { Height = 36 },
                EnableHeadersVisualStyles = false,
                AutoGenerateColumns = false
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 246, 242);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Date",
                HeaderText = "Date",
                Name = "Date"
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PointsEarned",
                HeaderText = "Points Earned",
                Name = "PointsEarned"
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PointsUsed",
                HeaderText = "Points Used",
                Name = "PointsUsed"
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TransactionType",
                HeaderText = "Type",
                Name = "TransactionType"
            });

            var rows = transactions
                .OrderByDescending(t => t.Date)
                .Select(t => new BreakdownRow
                {
                    Date = t.Date.ToString("MM/dd/yyyy"),
                    PointsEarned = t.PointsEarned,
                    PointsUsed = t.PointsUsed,
                    TransactionType = t.TransactionType
                })
                .ToList();

            grid.DataSource = rows;

            Controls.Add(grid);
            grid.BringToFront();
        }
    }
}