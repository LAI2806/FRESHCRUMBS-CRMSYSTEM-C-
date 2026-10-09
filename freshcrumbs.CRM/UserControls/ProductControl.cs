using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class ProductControl : UserControl
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

        private DataGridView _productGrid = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _deleteButton = null!;
        private Button _reactivateButton = null!;
        private Button _branchStockButton = null!;
        private Label _statusLabel = null!;
        private Panel _pagerPanel = null!;
        private Label _pageInfoLabel = null!;
        private Button _prevPageButton = null!;
        private Button _nextPageButton = null!;

        private List<ProductModel> _products = new();
        private List<ProductModel> _filteredProducts = new();
        private int _currentPage = 1;

        public ProductControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += ProductControl_Load;
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
                Text = TenantCapabilities.CanManageProducts ? "Product Management" : "Product List",
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
                PlaceholderText = "Search by name, code, or category..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);

            _addButton = CreateActionButton("+  Add Product", AccentColor, Color.White, 150);
            _addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _addButton.Click += AddButton_Click;
            toolbarPanel.Controls.Add(_addButton);

            _editButton = CreateActionButton("Edit", Color.White, LabelGray, 100);
            _editButton.FlatAppearance.BorderSize = 1;
            _editButton.FlatAppearance.BorderColor = BorderColor;
            _editButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _editButton.Enabled = false;
            _editButton.Click += EditButton_Click;
            toolbarPanel.Controls.Add(_editButton);

            _deleteButton = CreateActionButton("Delete", Color.White, Color.Firebrick, 100);
            _deleteButton.FlatAppearance.BorderSize = 1;
            _deleteButton.FlatAppearance.BorderColor = Color.Firebrick;
            _deleteButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _deleteButton.Enabled = false;
            _deleteButton.Click += DeleteButton_Click;
            toolbarPanel.Controls.Add(_deleteButton);

            _reactivateButton = CreateActionButton("Reactivate", Color.White, AccentColor, 110);
            _reactivateButton.FlatAppearance.BorderSize = 1;
            _reactivateButton.FlatAppearance.BorderColor = AccentColor;
            _reactivateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _reactivateButton.Enabled = false;
            _reactivateButton.Visible = false;
            _reactivateButton.Click += ReactivateButton_Click;
            toolbarPanel.Controls.Add(_reactivateButton);

            // PREMIUM (Branching) ADMIN: stock per branch for the selected product. Shares the Reactivate slot,
            // which is only shown for inactive products.
            _branchStockButton = CreateActionButton("Branch Stock", Color.White, AccentColor, 110);
            _branchStockButton.FlatAppearance.BorderSize = 1;
            _branchStockButton.FlatAppearance.BorderColor = AccentColor;
            _branchStockButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _branchStockButton.Enabled = false;
            _branchStockButton.Visible = CanUseBranchStock;
            _branchStockButton.Click += BranchStockButton_Click;
            toolbarPanel.Controls.Add(_branchStockButton);

            _statusFilterBox = new ComboBox
            {
                Location = new Point(330, 10),
                Width = 200,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[] { "All Products", "Active Products", "Inactive Products" });
            _statusFilterBox.SelectedIndex = 0;
            _statusFilterBox.SelectedIndexChanged += StatusFilterBox_SelectedIndexChanged;
            toolbarPanel.Controls.Add(_statusFilterBox);

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);
            PositionToolbarButtons(toolbarPanel);

            // Everyone with product access can view, search and filter. Only product managers
            // (ADMIN / MANAGER) get Add / Edit / Delete / Reactivate; the API enforces this too.
            if (!TenantCapabilities.CanManageProducts)
            {
                _addButton.Visible = false;
                _editButton.Visible = false;
                _deleteButton.Visible = false;
            }

            rootLayout.Controls.Add(toolbarPanel, 0, 1);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            _productGrid = new DataGridView
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

            _productGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _productGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _productGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _productGrid.ColumnHeadersHeight = 42;
            _productGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _productGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _productGrid.DefaultCellStyle.SelectionForeColor = TextDark;
            _productGrid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            _productGrid.SelectionChanged += ProductGrid_SelectionChanged;

            gridContainer.Controls.Add(_productGrid);
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
            _reactivateButton.Location = new Point(_editButton.Left - _reactivateButton.Width - 10, 8);
            _branchStockButton.Location = _reactivateButton.Location;
            _deleteButton.Location = new Point(_reactivateButton.Left - _deleteButton.Width - 10, 8);
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
            int totalRecords = _filteredProducts.Count;
            int totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)PageSize);

            if (_currentPage > totalPages)
            {
                _currentPage = totalPages;
            }
            if (_currentPage < 1)
            {
                _currentPage = 1;
            }

            var pageItems = _filteredProducts
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

        private async void ProductControl_Load(object? sender, EventArgs e)
        {
            await LoadProductsAsync();
        }

        private async Task LoadProductsAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _products = await _apiService.GetProductsAsync(_companyId, includeInactive: true);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load products: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private void BindGrid(List<ProductModel> products)
        {
            _productGrid.AutoGenerateColumns = true;
            _productGrid.DataSource = null;
            _productGrid.DataSource = products;

            if (_productGrid.Columns["ProductId"] != null)
            {
                _productGrid.Columns["ProductId"].Visible = false;
            }

            SetColumnHeader("ProductCode", "Code");
            SetColumnHeader("ProductName", "Product Name");
            SetColumnHeader("Category", "Category");
            SetColumnHeader("Description", "Description");
            SetColumnHeader("Price", "Price");
            SetColumnHeader("Quantity", TenantCapabilities.HasBranching ? "Total Stock" : "Stock");
            SetColumnHeader("BranchQuantity", "My Branch Stock");

            if (_productGrid.Columns["BranchQuantity"] != null)
            {
                _productGrid.Columns["BranchQuantity"].Visible =
                    TenantCapabilities.HasBranching && _products.Any(p => p.BranchQuantity != null);
            }
            SetColumnHeader("Sold", "Sold");
            SetColumnHeader("ReorderLevel", "Reorder");
            SetColumnHeader("StockLevel", "Stock Level");
            SetColumnHeader("Status", "Status");
        }

        private void SetColumnHeader(string columnName, string headerText)
        {
            if (_productGrid.Columns[columnName] != null)
            {
                _productGrid.Columns[columnName].HeaderText = headerText;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void StatusFilterBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters(bool resetPage = true)
        {
            string term = _searchBox.Text.Trim().ToLowerInvariant();
            string statusFilter = _statusFilterBox.SelectedItem?.ToString() ?? "All Products";

            IEnumerable<ProductModel> filtered = _products;

            if (statusFilter == "Active Products")
            {
                filtered = filtered.Where(p => p.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));
            }
            else if (statusFilter == "Inactive Products")
            {
                filtered = filtered.Where(p => p.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(term))
            {
                filtered = filtered.Where(p =>
                    p.ProductName.ToLowerInvariant().Contains(term) ||
                    p.ProductCode.ToLowerInvariant().Contains(term) ||
                    p.Category.ToLowerInvariant().Contains(term));
            }

            _filteredProducts = filtered.ToList();

            if (resetPage)
            {
                _currentPage = 1;
            }

            RenderCurrentPage();
        }

        private void ProductGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = _productGrid.SelectedRows.Count > 0;
            bool isInactive = hasSelection && GetSelectedStatus() == "Inactive";

            _editButton.Enabled = hasSelection && !isInactive;
            _deleteButton.Enabled = hasSelection && !isInactive;
            bool canReactivate = isInactive && TenantCapabilities.CanManageProducts;

            _reactivateButton.Visible = canReactivate;
            _reactivateButton.Enabled = canReactivate;

            _branchStockButton.Visible = CanUseBranchStock && !canReactivate;
            _branchStockButton.Enabled = hasSelection && !isInactive;
        }

        private static bool CanUseBranchStock => TenantCapabilities.HasBranching && TenantCapabilities.CanManageBranches;

        private async void BranchStockButton_Click(object? sender, EventArgs e)
        {
            if (_productGrid.SelectedRows.Count == 0 ||
                _productGrid.SelectedRows[0].DataBoundItem is not ProductModel selectedProduct)
            {
                return;
            }

            using var form = new ProductBranchStockForm(_companyId, selectedProduct);
            form.ShowDialog(this);

            if (form.StockChanged)
            {
                await LoadProductsAsync();
            }
        }

        private string? GetSelectedStatus()
        {
            if (_productGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            dynamic row = _productGrid.SelectedRows[0].DataBoundItem;
            return row.Status;
        }

        private string GenerateNextProductCode(List<ProductModel> products)
        {
            const string prefix = "PROD-";
            int maxNumber = 0;

            foreach (var product in products)
            {
                if (product.ProductCode.StartsWith(prefix) &&
                    int.TryParse(product.ProductCode.Substring(prefix.Length), out int number))
                {
                    if (number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }

            int nextNumber = maxNumber + 1;
            return $"{prefix}{nextNumber:D3}";
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            string suggestedCode = GenerateNextProductCode(_products);
            using var form = new ProductEditForm(null, suggestedCode);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreateProductAsync(_companyId, form.Result);
                await LoadProductsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create product: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            if (_productGrid.SelectedRows.Count == 0 ||
                _productGrid.SelectedRows[0].DataBoundItem is not ProductModel selectedProduct)
            {
                return;
            }

            using var form = new ProductEditForm(selectedProduct);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateProductAsync(_companyId, selectedProduct.ProductId, form.Result);
                await LoadProductsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update product: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_productGrid.SelectedRows.Count == 0 ||
                _productGrid.SelectedRows[0].DataBoundItem is not ProductModel selectedProduct)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to deactivate \"{selectedProduct.ProductName}\"? It will be hidden from the active list but its records will be kept.",
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeleteProductAsync(_companyId, selectedProduct.ProductId);
                await LoadProductsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to deactivate product: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void ReactivateButton_Click(object? sender, EventArgs e)
        {
            if (_productGrid.SelectedRows.Count == 0 ||
                _productGrid.SelectedRows[0].DataBoundItem is not ProductModel selectedProduct)
            {
                return;
            }

            try
            {
                await _apiService.ReactivateProductAsync(_companyId, selectedProduct.ProductId);
                await LoadProductsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to reactivate product: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }
    }
}