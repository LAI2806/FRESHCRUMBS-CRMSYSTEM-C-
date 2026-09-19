using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class SalesControl : UserControl
    {
        private readonly ApiService _apiService;
        private readonly int _companyId;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private DataGridView _salesGrid = null!;
        private TextBox _searchBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _itemsButton = null!;
        private Label _statusLabel = null!;

        private List<SalesTransactionModel> _transactions = new();
        private List<CustomerModel> _customers = new();
        private List<PromotionModel> _promotions = new();
        private List<ProductModel> _products = new();

        public SalesControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += SalesControl_Load;
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
                Text = "Sales Management",
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
                Width = 280,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by customer or payment method..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);

            _addButton = CreateActionButton("+  Add Sale", AccentColor, Color.White, 130);
            _addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _addButton.Click += AddButton_Click;
            toolbarPanel.Controls.Add(_addButton);

            _editButton = CreateActionButton("Edit", Color.White, LabelGray, 90);
            _editButton.FlatAppearance.BorderSize = 1;
            _editButton.FlatAppearance.BorderColor = BorderColor;
            _editButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _editButton.Enabled = false;
            _editButton.Click += EditButton_Click;
            toolbarPanel.Controls.Add(_editButton);

            _itemsButton = CreateActionButton("Manage Items", Color.White, LabelGray, 140);
            _itemsButton.FlatAppearance.BorderSize = 1;
            _itemsButton.FlatAppearance.BorderColor = BorderColor;
            _itemsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _itemsButton.Enabled = false;
            _itemsButton.Click += ItemsButton_Click;
            toolbarPanel.Controls.Add(_itemsButton);


            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);
            PositionToolbarButtons(toolbarPanel);

            rootLayout.Controls.Add(toolbarPanel, 0, 1);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            _salesGrid = new DataGridView
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
                RowTemplate = { Height = 38 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };

            _salesGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _salesGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _salesGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _salesGrid.ColumnHeadersHeight = 42;
            _salesGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _salesGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _salesGrid.DefaultCellStyle.SelectionForeColor = TextDark;
            _salesGrid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            _salesGrid.SelectionChanged += SalesGrid_SelectionChanged;

            gridContainer.Controls.Add(_salesGrid);
            rootLayout.Controls.Add(gridContainer, 0, 2);

            _statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                TextAlign = ContentAlignment.MiddleLeft
            };
            gridContainer.Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            Controls.Add(rootLayout);
        }
        private void PositionToolbarButtons(Panel toolbarPanel)
        {
            _addButton.Location = new Point(toolbarPanel.Width - _addButton.Width, 8);
            _editButton.Location = new Point(_addButton.Left - _editButton.Width - 10, 8);
            _itemsButton.Location = new Point(_editButton.Left - _itemsButton.Width - 10, 8);
        }

        private Button CreateActionButton(string text, Color backColor, Color foreColor, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private async void SalesControl_Load(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _customers = await _apiService.GetCustomersAsync(_companyId);
                _promotions = await _apiService.GetPromotionsAsync(_companyId);
                _products = await _apiService.GetProductsAsync(_companyId);
                _transactions = await _apiService.GetSalesTransactionsAsync(_companyId);
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load sales data: {ex.Message}";
            }
        }

        private void BindGrid()
        {
            string term = _searchBox?.Text.Trim().ToLowerInvariant() ?? "";

            var displayRows = _transactions
                .Select(t => new
                {
                    t.TransactionId,
                    CustomerName = GetCustomerName(t.CustomerId),
                    PromotionName = t.PromotionId.HasValue ? GetPromotionName(t.PromotionId.Value) : "None",
                    t.TransactionDate,
                    t.TotalAmount,
                    t.DiscountAmount,
                    t.FinalAmount,
                    t.PaymentMethod,
                    t.Status
                })
                .Where(row =>
                    string.IsNullOrEmpty(term) ||
                    row.CustomerName.ToLowerInvariant().Contains(term) ||
                    row.PaymentMethod.ToLowerInvariant().Contains(term))
                .ToList();

            _salesGrid.AutoGenerateColumns = true;
            _salesGrid.DataSource = null;
            _salesGrid.DataSource = displayRows;

            if (_salesGrid.Columns["TransactionId"] != null)
            {
                _salesGrid.Columns["TransactionId"].Visible = false;
            }

            SetColumnHeader("CustomerName", "Customer");
            SetColumnHeader("PromotionName", "Promotion");
            SetColumnHeader("TransactionDate", "Date");
            SetColumnHeader("TotalAmount", "Total");
            SetColumnHeader("DiscountAmount", "Discount");
            SetColumnHeader("FinalAmount", "Final Amount");
            SetColumnHeader("PaymentMethod", "Payment");
            SetColumnHeader("Status", "Status");

            if (_salesGrid.Columns["TransactionDate"] != null)
            {
                _salesGrid.Columns["TransactionDate"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }
        }

        private string GetCustomerName(int customerId)
        {
            var customer = _customers.FirstOrDefault(c => c.CustomerId == customerId);
            return customer != null ? $"{customer.FirstName} {customer.LastName}" : $"Customer #{customerId}";
        }

        private string GetPromotionName(int promotionId)
        {
            var promotion = _promotions.FirstOrDefault(p => p.PromotionId == promotionId);
            return promotion?.PromotionName ?? $"Promotion #{promotionId}";
        }

        private void SetColumnHeader(string columnName, string headerText)
        {
            if (_salesGrid.Columns[columnName] != null)
            {
                _salesGrid.Columns[columnName].HeaderText = headerText;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            BindGrid();
        }

        private void SalesGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = _salesGrid.SelectedRows.Count > 0;
            _editButton.Enabled = hasSelection;
            _itemsButton.Enabled = hasSelection;
        }

        private SalesTransactionModel? GetSelectedTransaction()
        {
            if (_salesGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            dynamic row = _salesGrid.SelectedRows[0].DataBoundItem;
            int id = row.TransactionId;

            return _transactions.FirstOrDefault(t => t.TransactionId == id);
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new SalesEditForm(null, _customers, _promotions, _products);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                var created = await _apiService.CreateSalesTransactionAsync(_companyId, form.Result);

                if (created != null)
                {
                    foreach (var item in form.ResultItems)
                    {
                        await _apiService.CreateTransactionItemAsync(_companyId, created.TransactionId, item);
                    }
                }

                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create transaction: {ex.Message}";
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            var selectedTransaction = GetSelectedTransaction();
            if (selectedTransaction == null)
            {
                return;
            }

            using var form = new SalesEditForm(selectedTransaction, _customers, _promotions, _products);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateSalesTransactionAsync(_companyId, selectedTransaction.TransactionId, form.Result);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update transaction: {ex.Message}";
            }
        }

        private void ItemsButton_Click(object? sender, EventArgs e)
        {
            var selectedTransaction = GetSelectedTransaction();
            if (selectedTransaction == null)
            {
                return;
            }

            using var dialog = new TransactionItemsDialog(_apiService, _companyId, selectedTransaction.TransactionId, _products);
            dialog.ShowDialog(this);
        }

        }
    }