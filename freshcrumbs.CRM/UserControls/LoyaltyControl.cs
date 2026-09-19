using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class LoyaltyControl : UserControl
    {
        private readonly ApiService _apiService;
        private readonly int _companyId;

        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);
        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);

        private DataGridView _summaryGrid = null!;
        private TextBox _searchBox = null!;
        private Button _breakdownButton = null!;
        private Label _statusLabel = null!;

        private List<LoyaltyModel> _transactions = new();
        private List<CustomerModel> _customers = new();

        public LoyaltyControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += LoyaltyControl_Load;
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = PageBg
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var header = new Label
            {
                Text = "Loyalty Management",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            rootLayout.Controls.Add(header, 0, 0);

            var toolbarPanel = new Panel { Dock = DockStyle.Fill };

            _searchBox = new TextBox
            {
                Location = new Point(0, 10),
                Width = 320,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by customer name..."
            };
            _searchBox.TextChanged += (s, e) => BindGrid();
            toolbarPanel.Controls.Add(_searchBox);

            _breakdownButton = new Button
            {
                Text = "View Breakdown",
                Width = 150,
                Height = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false
            };
            _breakdownButton.FlatAppearance.BorderSize = 0;
            _breakdownButton.Click += BreakdownButton_Click;
            toolbarPanel.Controls.Add(_breakdownButton);

            toolbarPanel.Resize += (s, e) =>
            {
                _breakdownButton.Location = new Point(toolbarPanel.Width - _breakdownButton.Width, 8);
            };
            _breakdownButton.Location = new Point(toolbarPanel.Width - _breakdownButton.Width, 8);

            rootLayout.Controls.Add(toolbarPanel, 0, 1);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            _summaryGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(235, 230, 225),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9.5f),
                RowTemplate = { Height = 38 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _summaryGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _summaryGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _summaryGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _summaryGrid.ColumnHeadersHeight = 42;
            _summaryGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _summaryGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _summaryGrid.DefaultCellStyle.SelectionForeColor = TextDark;

            _summaryGrid.SelectionChanged += (s, e) =>
            {
                _breakdownButton.Enabled = _summaryGrid.SelectedRows.Count > 0;
            };

            gridContainer.Controls.Add(_summaryGrid);
            rootLayout.Controls.Add(gridContainer, 0, 2);

            _statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick
            };
            gridContainer.Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            Controls.Add(rootLayout);
        }

        private async void LoyaltyControl_Load(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _customers = await _apiService.GetCustomersAsync(_companyId, true);
                _transactions = await _apiService.GetLoyaltyTransactionsAsync(_companyId);
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load loyalty data: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private class LoyaltySummaryRow
        {
            public int CustomerId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public int TotalEarned { get; set; }
            public int TotalUsed { get; set; }
            public int CurrentPoints { get; set; }
        }

        private void BindGrid()
        {
            string term = _searchBox?.Text.Trim().ToLowerInvariant() ?? "";

            var summary = _customers
                .Select(c => new LoyaltySummaryRow
                {
                    CustomerId = c.CustomerId,
                    CustomerName = $"{c.FirstName} {c.LastName}",
                    TotalEarned = _transactions.Where(t => t.CustomerId == c.CustomerId).Sum(t => t.PointsEarned),
                    TotalUsed = _transactions.Where(t => t.CustomerId == c.CustomerId).Sum(t => t.PointsUsed),
                    CurrentPoints = c.LoyaltyPoints
                })
                .Where(row =>
                    (row.TotalEarned > 0 || row.TotalUsed > 0) &&
                    (string.IsNullOrEmpty(term) || row.CustomerName.ToLowerInvariant().Contains(term)))
                .ToList();

            _summaryGrid.DataSource = null;
            _summaryGrid.Columns.Clear();
            _summaryGrid.AutoGenerateColumns = false;

            _summaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CustomerId",
                Visible = false
            });
            _summaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CustomerName",
                HeaderText = "Customer",
                Name = "CustomerName"
            });
            _summaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TotalEarned",
                HeaderText = "Total Points Earned",
                Name = "TotalEarned"
            });
            _summaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TotalUsed",
                HeaderText = "Total Points Used",
                Name = "TotalUsed"
            });
            _summaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CurrentPoints",
                HeaderText = "Current Points",
                Name = "CurrentPoints"
            });

            _summaryGrid.DataSource = summary;
        }

        private void BreakdownButton_Click(object? sender, EventArgs e)
        {
            if (_summaryGrid.SelectedRows.Count == 0 ||
                _summaryGrid.SelectedRows[0].DataBoundItem is not LoyaltySummaryRow selectedRow)
            {
                return;
            }

            var customerTransactions = _transactions.Where(t => t.CustomerId == selectedRow.CustomerId).ToList();

            using var dialog = new LoyaltyBreakdownDialog(selectedRow.CustomerName, customerTransactions);
            dialog.ShowDialog(this);
        }
    }
}