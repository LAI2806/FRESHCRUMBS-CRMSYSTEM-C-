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

        private const int PageSize = 50;

        private DataGridView _salesGrid = null!;
        private TextBox _searchBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _itemsButton = null!;

        private Button _deleteButton = null!;
        private ComboBox? _branchFilter;
        private Label _statusLabel = null!;
        private Panel _pagerPanel = null!;
        private Label _pageInfoLabel = null!;
        private Button _prevPageButton = null!;
        private Button _nextPageButton = null!;

        private List<SalesTransactionModel> _transactions = new();
        private List<CustomerModel> _customers = new();
        private List<PromotionModel> _promotions = new();
        private List<ProductModel> _products = new();
        private List<BranchModel> _branches = new();
        private BranchModel? _myBranch;
        private List<SalesDisplayRow> _filteredTransactions = new();
        private int _currentPage = 1;

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
                PlaceholderText = "Search code, customer, or payment..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);

            // PREMIUM ADMIN only: filter by branch. MANAGER / STAFF already receive only their branch from the API.
            if (TenantCapabilities.CanFilterByBranch)
            {
                _branchFilter = new ComboBox
                {
                    Location = new Point(290, 10),
                    Width = 200,
                    Font = new Font("Segoe UI", 10),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat
                };
                _branchFilter.SelectedIndexChanged += (s, e) => ApplyFilters();
                toolbarPanel.Controls.Add(_branchFilter);
            }

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

            _deleteButton = CreateActionButton("Delete", Color.White, Color.Firebrick, 90);
            _deleteButton.FlatAppearance.BorderSize = 1;
            _deleteButton.FlatAppearance.BorderColor = Color.Firebrick;
            _deleteButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _deleteButton.Enabled = false;
            _deleteButton.Click += DeleteButton_Click;
            toolbarPanel.Controls.Add(_deleteButton);

            // STAFF records sales only; editing, item corrections, cancelling and deleting are management actions.
            bool manage = TenantCapabilities.CanManageSales;
            _editButton.Visible = manage;
            _itemsButton.Visible = manage;
            _deleteButton.Visible = manage;

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);


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

            _pagerPanel = CreatePagerPanel();
            gridContainer.Controls.Add(_pagerPanel);

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
            _deleteButton.Location = new Point(_itemsButton.Left - _deleteButton.Width - 10, 8);
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

        private Panel CreatePagerPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40
            };

            _pageInfoLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = LabelGray,
                Location = new Point(0, 11),
                Text = "Page 1 of 1"
            };
            panel.Controls.Add(_pageInfoLabel);

            _nextPageButton = new Button
            {
                Text = "Next \u203A",
                Width = 90,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _nextPageButton.FlatAppearance.BorderSize = 1;
            _nextPageButton.FlatAppearance.BorderColor = BorderColor;
            _nextPageButton.Click += (s, e) => ChangePage(1);
            panel.Controls.Add(_nextPageButton);

            _prevPageButton = new Button
            {
                Text = "\u2039 Previous",
                Width = 90,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _prevPageButton.FlatAppearance.BorderSize = 1;
            _prevPageButton.FlatAppearance.BorderColor = BorderColor;
            _prevPageButton.Click += (s, e) => ChangePage(-1);
            panel.Controls.Add(_prevPageButton);

            panel.Resize += (s, e) => PositionPagerButtons(panel);
            PositionPagerButtons(panel);

            return panel;
        }

        private void PositionPagerButtons(Panel pagerPanel)
        {
            _nextPageButton.Location = new Point(pagerPanel.Width - _nextPageButton.Width, 5);
            _prevPageButton.Location = new Point(_nextPageButton.Left - _prevPageButton.Width - 10, 5);
        }

        private void ChangePage(int delta)
        {
            _currentPage += delta;
            RenderCurrentPage();
        }

        private void RenderCurrentPage()
        {
            int totalRecords = _filteredTransactions.Count;
            int totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)PageSize);

            if (_currentPage > totalPages)
            {
                _currentPage = totalPages;
            }
            if (_currentPage < 1)
            {
                _currentPage = 1;
            }

            var pageItems = _filteredTransactions
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            BindGrid(pageItems);

            _pageInfoLabel.Text = totalRecords == 0
                ? "No records"
                : $"Page {_currentPage} of {totalPages} ({totalRecords} {(totalRecords == 1 ? "record" : "records")})";

            _prevPageButton.Enabled = _currentPage > 1;
            _nextPageButton.Enabled = _currentPage < totalPages;
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
                // Include inactive customers so older transactions still show their Customer Code and name.
                _customers = await _apiService.GetCustomersAsync(_companyId, includeInactive: true);
                _promotions = new List<PromotionModel>();
                if (TenantCapabilities.CanUsePromotions)
                {
                    try
                    {
                        _promotions = await _apiService.GetPromotionsAsync(_companyId);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _promotions = new List<PromotionModel>();
                    }
                }
                _products = await _apiService.GetProductsAsync(_companyId);

                if (TenantCapabilities.HasBranching)
                {
                    _branches = await _apiService.GetBranchesAsync(_companyId);
                    _myBranch = await _apiService.GetMyBranchAsync(_companyId);
                    PopulateBranchFilter();
                }

                _transactions = await _apiService.GetSalesTransactionsAsync(_companyId);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load sales data: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private class SalesDisplayRow
        {
            public int TransactionId { get; set; }
            public string CustomerCode { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string PromotionName { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public DateTime TransactionDate { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal DiscountAmount { get; set; }
            public decimal CustomerDiscountAmount { get; set; }
            public decimal FinalAmount { get; set; }
            public string PaymentMethod { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        private void ApplyFilters(bool resetPage = true)
        {
            string term = _searchBox?.Text.Trim().ToLowerInvariant() ?? "";

            _filteredTransactions = _transactions
                .Where(t => MatchesBranchFilter(t.BranchId))
                .Select(t => new SalesDisplayRow
                {
                    TransactionId = t.TransactionId,
                    CustomerCode = GetCustomerCode(t.CustomerId),
                    CustomerName = GetCustomerName(t.CustomerId),
                    PromotionName = t.PromotionId.HasValue ? GetPromotionName(t.PromotionId.Value) : "None",
                    BranchName = t.BranchId.HasValue
                        ? _branches.FirstOrDefault(b => b.BranchId == t.BranchId.Value)?.BranchName ?? string.Empty
                        : string.Empty,
                    TransactionDate = t.TransactionDate,
                    TotalAmount = t.TotalAmount,
                    DiscountAmount = t.DiscountAmount,
                    CustomerDiscountAmount = t.CustomerDiscountAmount,
                    FinalAmount = t.FinalAmount,
                    PaymentMethod = t.PaymentMethod,
                    Status = t.Status
                })
                .Where(row =>
                    string.IsNullOrEmpty(term) ||
                    row.CustomerCode.ToLowerInvariant().Contains(term) ||
                    row.CustomerName.ToLowerInvariant().Contains(term) ||
                    row.PaymentMethod.ToLowerInvariant().Contains(term))
                .ToList();

            if (resetPage)
            {
                _currentPage = 1;
            }

            RenderCurrentPage();
        }

        private const int AllBranches = -1;
        private const int NoBranch = 0;

        private void PopulateBranchFilter()
        {
            if (_branchFilter == null)
            {
                return;
            }

            int selected = (_branchFilter.SelectedItem as BranchModel)?.BranchId ?? AllBranches;

            _branchFilter.Items.Clear();
            _branchFilter.Items.Add(new BranchModel { BranchId = AllBranches, BranchName = "All Branches" });

            foreach (var branch in _branches.Where(b => string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase)))
            {
                _branchFilter.Items.Add(branch);
            }

            _branchFilter.Items.Add(new BranchModel { BranchId = NoBranch, BranchName = "No branch (before branching)" });

            _branchFilter.SelectedItem = _branchFilter.Items.OfType<BranchModel>().FirstOrDefault(b => b.BranchId == selected)
                ?? _branchFilter.Items[0];
        }

        private bool MatchesBranchFilter(int? branchId)
        {
            int selected = (_branchFilter?.SelectedItem as BranchModel)?.BranchId ?? AllBranches;

            return selected == AllBranches
                || (selected == NoBranch ? branchId == null : branchId == selected);
        }

        private void BindGrid(List<SalesDisplayRow> displayRows)
        {
            _salesGrid.AutoGenerateColumns = true;
            _salesGrid.DataSource = null;
            _salesGrid.DataSource = displayRows;

            if (_salesGrid.Columns["TransactionId"] != null)
            {
                _salesGrid.Columns["TransactionId"].Visible = false;
            }

            if (!TenantCapabilities.CanUsePromotions)
            {
                foreach (var columnName in new[] { "PromotionName", "DiscountAmount" })
                {
                    if (_salesGrid.Columns[columnName] != null)
                    {
                        _salesGrid.Columns[columnName].Visible = false;
                    }
                }
            }

            SetColumnHeader("CustomerCode", "Customer Code");
            SetColumnHeader("CustomerName", "Customer Name");
            SetColumnHeader("PromotionName", "Promotion");
            SetColumnHeader("TransactionDate", "Date");
            SetColumnHeader("TotalAmount", "Total");
            SetColumnHeader("DiscountAmount", "Promotion Discount");
            SetColumnHeader("CustomerDiscountAmount", "Customer Discount");
            SetColumnHeader("FinalAmount", "Final Amount");
            SetColumnHeader("PaymentMethod", "Payment");
            SetColumnHeader("Status", "Status");
            SetColumnHeader("BranchName", "Branch");

            if (_salesGrid.Columns["BranchName"] != null)
            {
                _salesGrid.Columns["BranchName"].Visible = TenantCapabilities.HasBranching;
            }

            if (_salesGrid.Columns["TransactionDate"] != null)
            {
                _salesGrid.Columns["TransactionDate"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }
        }

        private string GetCustomerCode(int customerId)
        {
            var customer = _customers.FirstOrDefault(c => c.CustomerId == customerId);
            return customer != null ? customer.CustomerCode : string.Empty;
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
            ApplyFilters();
        }

        private void SalesGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = _salesGrid.SelectedRows.Count > 0;
            _editButton.Enabled = hasSelection;
            _itemsButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
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
            using var form = new SalesEditForm(null, _customers, _promotions, _products, _branches, _myBranch, _companyId);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                // The sale and its items in one request (no half-saved sale if an item fails).
                await _apiService.CreateSalesTransactionAsync(_companyId, form.Result, form.ResultItems);

                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create transaction: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
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
                _statusLabel.Text = $"Failed to update transaction: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
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

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            var selectedTransaction = GetSelectedTransaction();
            if (selectedTransaction == null)
            {
                return;
            }

            if (string.Equals(selectedTransaction.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "A completed transaction cannot be deleted. Cancel it first so its stock and loyalty points are restored.",
                    "Delete Not Allowed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                "Are you sure you want to delete this transaction? It will be hidden from the list but the record will be kept.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeleteSalesTransactionAsync(_companyId, selectedTransaction.TransactionId);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to delete transaction: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

    }
}