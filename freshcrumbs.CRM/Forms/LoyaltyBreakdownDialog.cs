using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class LoyaltyBreakdownDialog : Form
    {
        private class BreakdownRow
        {
            public int LoyaltyTransactionId { get; set; }
            public string Date { get; set; } = string.Empty;
            public int PointsEarned { get; set; }
            public int PointsUsed { get; set; }
            public string TransactionType { get; set; } = string.Empty;
        }

        private readonly ApiService _apiService;
        private readonly int _companyId;
        private readonly List<LoyaltyModel> _transactions;

        private DataGridView _grid = null!;
        private Button _deleteButton = null!;
        private Label _statusLabel = null!;

        public LoyaltyBreakdownDialog(string customerName, List<LoyaltyModel> transactions, int companyId)
        {
            _apiService = new ApiService();
            _companyId = companyId;
            _transactions = transactions;

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

            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56
            };

            _statusLabel = new Label
            {
                Text = "",
                AutoSize = false,
                Location = new Point(20, 18),
                Width = 320,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick
            };
            footerPanel.Controls.Add(_statusLabel);

            _deleteButton = new Button
            {
                Text = "Delete Transaction",
                Width = 160,
                Height = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(180, 60, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _deleteButton.FlatAppearance.BorderSize = 0;
            _deleteButton.Click += DeleteButton_Click;
            footerPanel.Controls.Add(_deleteButton);

            footerPanel.Resize += (s, e) =>
            {
                _deleteButton.Location = new Point(footerPanel.Width - _deleteButton.Width - 20, 9);
            };
            _deleteButton.Location = new Point(footerPanel.Width - _deleteButton.Width - 20, 9);

            Controls.Add(footerPanel);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9.5f),
                RowTemplate = { Height = 36 },
                EnableHeadersVisualStyles = false,
                AutoGenerateColumns = false
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 246, 242);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _grid.ColumnHeadersHeight = 40;

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LoyaltyTransactionId",
                Visible = false
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Date",
                HeaderText = "Date",
                Name = "Date"
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PointsEarned",
                HeaderText = "Points Earned",
                Name = "PointsEarned"
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "PointsUsed",
                HeaderText = "Points Used",
                Name = "PointsUsed"
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TransactionType",
                HeaderText = "Type",
                Name = "TransactionType"
            });

            _grid.SelectionChanged += (s, e) =>
            {
                _deleteButton.Enabled = _grid.SelectedRows.Count > 0;
            };

            BindGrid();

            Controls.Add(_grid);
            _grid.BringToFront();
        }

        private void BindGrid()
        {
            var rows = _transactions
                .OrderByDescending(t => t.Date)
                .Select(t => new BreakdownRow
                {
                    LoyaltyTransactionId = t.LoyaltyTransactionId,
                    Date = t.Date.ToString("MM/dd/yyyy"),
                    PointsEarned = t.PointsEarned,
                    PointsUsed = t.PointsUsed,
                    TransactionType = t.TransactionType
                })
                .ToList();

            _grid.DataSource = rows;
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_grid.SelectedRows.Count == 0 ||
                _grid.SelectedRows[0].DataBoundItem is not BreakdownRow selectedRow)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "Delete this loyalty transaction? This will reverse its points.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _statusLabel.Text = "";
                _deleteButton.Enabled = false;

                await _apiService.DeleteLoyaltyTransactionAsync(_companyId, selectedRow.LoyaltyTransactionId);

                _transactions.RemoveAll(t => t.LoyaltyTransactionId == selectedRow.LoyaltyTransactionId);
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to delete: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
                _deleteButton.Enabled = true;
            }
        }
    }
}