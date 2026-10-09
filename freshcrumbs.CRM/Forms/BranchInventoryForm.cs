using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace freshcrumbs.CRM.winforms.Forms
{
    // Stock of every active product at one branch. ADMIN: any branch; MANAGER: own branch only (API-enforced).
    public class BranchInventoryForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly int _companyId;
        private readonly BranchModel _branch;

        private DataGridView _grid = null!;
        private Label _messageLabel = null!;
        private List<BranchInventoryModel> _rows = new();

        public BranchInventoryForm(int companyId, BranchModel branch)
        {
            _companyId = companyId;
            _branch = branch;

            Text = $"Branch Inventory - {branch.BranchName}";
            Width = 820;
            Height = 560;
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(20);

            InitializeControls();
            Load += async (s, e) => await LoadInventoryAsync();
        }

        private void InitializeControls()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BranchUi.CreateTitle($"{_branch.BranchName} - Inventory", 15), 0, 0);

            var toolbar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };

            if (TenantCapabilities.CanManageBranchStock)
            {
                var stockInButton = BranchUi.CreateButton("+  Stock In", true, 130);
                stockInButton.Click += async (s, e) => await RunActionAsync("In");
                toolbar.Controls.Add(stockInButton);

                var stockOutButton = BranchUi.CreateButton("-  Stock Out", false, 130);
                stockOutButton.Click += async (s, e) => await RunActionAsync("Out");
                toolbar.Controls.Add(stockOutButton);
            }

            if (TenantCapabilities.CanManageBranches)
            {
                var allocateButton = BranchUi.CreateButton("Allocate Unallocated", false, 180);
                allocateButton.Click += async (s, e) => await RunActionAsync("Allocate");
                toolbar.Controls.Add(allocateButton);
            }

            var refreshButton = BranchUi.CreateButton("Refresh", false, 100);
            refreshButton.Click += async (s, e) => await LoadInventoryAsync();
            toolbar.Controls.Add(refreshButton);

            root.Controls.Add(toolbar, 0, 1);

            _grid = BranchUi.CreateGrid();
            root.Controls.Add(_grid, 0, 2);

            _messageLabel = BranchUi.CreateMessageLabel();
            root.Controls.Add(_messageLabel, 0, 3);

            Controls.Add(root);
        }

        private async Task LoadInventoryAsync()
        {
            try
            {
                _rows = await _apiService.GetBranchInventoryAsync(_companyId, _branch.BranchId);
                _grid.DataSource = null;
                _grid.DataSource = _rows;
                BranchUi.ShowColumns(_grid,
                    ("ProductCode", "Code"),
                    ("ProductName", "Product Name"),
                    ("Category", "Category"),
                    ("Quantity", "Branch Stock"),
                    ("ReorderLevel", "Reorder"),
                    ("StockLevel", "Stock Level"));
            }
            catch (Exception ex)
            {
                ShowError(BranchUi.GetMessage(ex));
            }
        }

        private async Task RunActionAsync(string action)
        {
            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].DataBoundItem is not BranchInventoryModel row)
            {
                ShowError("Select a product first.");
                return;
            }

            try
            {
                int? maximum = null;
                string details = $"{row.ProductName} at {_branch.BranchName}. Current branch stock: {row.Quantity}.";

                if (action == "Out")
                {
                    if (row.Quantity <= 0)
                    {
                        ShowError($"{row.ProductName} has no stock at {_branch.BranchName}.");
                        return;
                    }

                    maximum = row.Quantity;
                }
                else if (action == "Allocate")
                {
                    var stock = await _apiService.GetProductBranchStockAsync(_companyId, row.ProductId);

                    if (stock.UnallocatedQuantity <= 0)
                    {
                        ShowError($"{row.ProductName} has no unallocated stock.");
                        return;
                    }

                    maximum = stock.UnallocatedQuantity;
                    details += $" Unallocated company stock: {stock.UnallocatedQuantity}.";
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

                await _apiService.ChangeBranchStockAsync(_companyId, _branch.BranchId, row.ProductId, form.Quantity, action);
                await LoadInventoryAsync();
                ShowSuccess($"{title}: {form.Quantity} x {row.ProductName} saved.");
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

        private void ShowSuccess(string message)
        {
            _messageLabel.ForeColor = Color.FromArgb(46, 130, 80);
            _messageLabel.Text = message;
        }
    }
}
