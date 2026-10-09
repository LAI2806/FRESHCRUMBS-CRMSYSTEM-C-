using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace freshcrumbs.CRM.winforms.Forms
{
    // ADMIN: one product's stock per branch, the company total and the unallocated part.
    public class ProductBranchStockForm : Form
    {
        public bool StockChanged { get; private set; }

        private readonly ApiService _apiService = new();
        private readonly int _companyId;
        private readonly ProductModel _product;

        private Label _summaryLabel = null!;
        private DataGridView _grid = null!;
        private Label _messageLabel = null!;
        private ProductBranchStockModel? _stock;

        public ProductBranchStockForm(int companyId, ProductModel product)
        {
            _companyId = companyId;
            _product = product;

            Text = $"Stock by Branch - {product.ProductName}";
            Width = 720;
            Height = 520;
            MinimumSize = new Size(600, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(20);

            InitializeControls();
            Load += async (s, e) => await LoadStockAsync();
        }

        private void InitializeControls()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BranchUi.CreateTitle($"{_product.ProductCode}  {_product.ProductName}", 15), 0, 0);

            _summaryLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = BranchUi.TextDark,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_summaryLabel, 0, 1);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };

            var stockInButton = BranchUi.CreateButton("+  Stock In", true, 130);
            stockInButton.Click += async (s, e) => await RunActionAsync("In");
            toolbar.Controls.Add(stockInButton);

            var stockOutButton = BranchUi.CreateButton("-  Stock Out", false, 130);
            stockOutButton.Click += async (s, e) => await RunActionAsync("Out");
            toolbar.Controls.Add(stockOutButton);

            var allocateButton = BranchUi.CreateButton("Allocate Unallocated", false, 180);
            allocateButton.Click += async (s, e) => await RunActionAsync("Allocate");
            toolbar.Controls.Add(allocateButton);

            root.Controls.Add(toolbar, 0, 2);

            _grid = BranchUi.CreateGrid();
            root.Controls.Add(_grid, 0, 3);

            _messageLabel = BranchUi.CreateMessageLabel();
            root.Controls.Add(_messageLabel, 0, 4);

            Controls.Add(root);
        }

        private async Task LoadStockAsync()
        {
            try
            {
                _stock = await _apiService.GetProductBranchStockAsync(_companyId, _product.ProductId);

                _summaryLabel.Text =
                    $"Combined stock: {_stock.TotalQuantity}     In branches: {_stock.AllocatedQuantity}     Unallocated: {_stock.UnallocatedQuantity}";

                _grid.DataSource = null;
                _grid.DataSource = _stock.Branches;
                BranchUi.ShowColumns(_grid,
                    ("BranchName", "Branch"),
                    ("Status", "Status"),
                    ("Quantity", "Stock"));
            }
            catch (Exception ex)
            {
                ShowError(BranchUi.GetMessage(ex));
            }
        }

        private async Task RunActionAsync(string action)
        {
            if (_stock == null)
            {
                return;
            }

            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].DataBoundItem is not ProductBranchStockLineModel line)
            {
                ShowError("Select a branch first.");
                return;
            }

            if (!string.Equals(line.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                ShowError($"{line.BranchName} is inactive.");
                return;
            }

            int? maximum = null;
            string details = $"{_product.ProductName} at {line.BranchName}. Current branch stock: {line.Quantity}.";

            if (action == "Out")
            {
                if (line.Quantity <= 0)
                {
                    ShowError($"{line.BranchName} has no stock of {_product.ProductName}.");
                    return;
                }

                maximum = line.Quantity;
            }
            else if (action == "Allocate")
            {
                if (_stock.UnallocatedQuantity <= 0)
                {
                    ShowError($"{_product.ProductName} has no unallocated stock.");
                    return;
                }

                maximum = _stock.UnallocatedQuantity;
                details += $" Unallocated company stock: {_stock.UnallocatedQuantity}.";
            }

            string title = action switch
            {
                "In" => "Stock In",
                "Out" => "Stock Out",
                _ => "Allocate Stock"
            };

            using var form = new StockQuantityForm(title, details, maximum);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.ChangeBranchStockAsync(_companyId, line.BranchId, _product.ProductId, form.Quantity, action);
                StockChanged = true;
                await LoadStockAsync();
                _messageLabel.ForeColor = Color.FromArgb(46, 130, 80);
                _messageLabel.Text = $"{title}: {form.Quantity} saved for {line.BranchName}.";
            }
            catch (Exception ex)
            {
                ShowError(BranchUi.GetMessage(ex));
            }
        }

        private void ShowError(string message)
        {
            _messageLabel.ForeColor = Color.Firebrick;
            _messageLabel.Text = message;
        }
    }
}
