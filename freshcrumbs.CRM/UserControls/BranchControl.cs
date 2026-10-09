using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    // PREMIUM (Branching) module. ADMIN: all branches, add/edit, employee assignments, inventory.
    // MANAGER: only the assigned branch and its inventory. The API enforces the same rules.
    public class BranchControl : UserControl
    {
        private readonly ApiService _apiService = new();
        private readonly int _companyId;

        private DataGridView _grid = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _employeesButton = null!;
        private Button _inventoryButton = null!;
        private Label _messageLabel = null!;
        private List<BranchModel> _branches = new();

        public BranchControl(int companyId)
        {
            _companyId = companyId;

            Dock = DockStyle.Fill;
            BackColor = BranchUi.PageBg;

            InitializeLayout();
            Load += async (s, e) => await LoadBranchesAsync();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BranchUi.PageBg };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                Text = TenantCapabilities.CanManageBranches ? "Branch Management" : "My Branch",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = BranchUi.TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };

            _addButton = BranchUi.CreateButton("+  Add Branch", true, 140);
            _addButton.Click += AddButton_Click;

            _editButton = BranchUi.CreateButton("Edit", false, 100);
            _editButton.Click += EditButton_Click;

            _employeesButton = BranchUi.CreateButton("Employees", false, 120);
            _employeesButton.Click += EmployeesButton_Click;

            _inventoryButton = BranchUi.CreateButton("Inventory", false, 120);
            _inventoryButton.Click += InventoryButton_Click;

            bool isAdmin = TenantCapabilities.CanManageBranches;
            _addButton.Visible = isAdmin;
            _editButton.Visible = isAdmin;
            _employeesButton.Visible = isAdmin;

            toolbar.Controls.Add(_addButton);
            toolbar.Controls.Add(_editButton);
            toolbar.Controls.Add(_employeesButton);
            toolbar.Controls.Add(_inventoryButton);
            root.Controls.Add(toolbar, 0, 1);

            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            _grid = BranchUi.CreateGrid();
            _grid.CellDoubleClick += (s, e) => InventoryButton_Click(s, e);
            gridContainer.Controls.Add(_grid);
            root.Controls.Add(gridContainer, 0, 2);

            _messageLabel = BranchUi.CreateMessageLabel();
            root.Controls.Add(_messageLabel, 0, 3);

            Controls.Add(root);
        }

        private async Task LoadBranchesAsync()
        {
            try
            {
                _messageLabel.Text = "";

                if (TenantCapabilities.CanManageBranches)
                {
                    _branches = await _apiService.GetBranchesAsync(_companyId);

                    if (_branches.Count == 0)
                    {
                        _messageLabel.ForeColor = BranchUi.LabelGray;
                        _messageLabel.Text = "No branches yet. Use \"+ Add Branch\" to create the first one.";
                    }
                }
                else
                {
                    var mine = await _apiService.GetMyBranchAsync(_companyId);
                    _branches = new List<BranchModel>();

                    if (mine == null)
                    {
                        _messageLabel.ForeColor = Color.Firebrick;
                        _messageLabel.Text = "You are not assigned to a branch. Ask your administrator to assign you.";
                    }
                    else
                    {
                        var all = await _apiService.GetBranchesAsync(_companyId);
                        _branches = all.Where(b => b.BranchId == mine.BranchId).ToList();
                    }
                }

                _grid.DataSource = null;
                _grid.DataSource = _branches;
                BranchUi.ShowColumns(_grid,
                    ("BranchName", "Branch"),
                    ("Address", "Address"),
                    ("ManagerDisplay", "Manager"),
                    ("Status", "Status"));
            }
            catch (Exception ex)
            {
                _messageLabel.ForeColor = Color.Firebrick;
                _messageLabel.Text = BranchUi.GetMessage(ex);
            }
        }

        private BranchModel? SelectedBranch()
        {
            return _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].DataBoundItem as BranchModel : null;
        }

        // Active MANAGER-role accounts for the branch Manager field (from the existing assignment list).
        private async Task<List<BranchAssignmentModel>> LoadManagerChoicesAsync()
        {
            var accounts = await _apiService.GetBranchAssignmentsAsync(_companyId);

            return accounts
                .Where(a => string.Equals(a.Role, TenantRole.Manager, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => string.IsNullOrWhiteSpace(a.FullName) ? a.UserName : a.FullName)
                .ToList();
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            List<BranchAssignmentModel> managers;

            try
            {
                managers = await LoadManagerChoicesAsync();
            }
            catch (Exception ex)
            {
                _messageLabel.ForeColor = Color.Firebrick;
                _messageLabel.Text = BranchUi.GetMessage(ex);
                return;
            }

            using var form = new BranchEditForm(null, managers);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreateBranchAsync(_companyId, form.Result);
                await LoadBranchesAsync();
            }
            catch (Exception ex)
            {
                _messageLabel.ForeColor = Color.Firebrick;
                _messageLabel.Text = BranchUi.GetMessage(ex);
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            var branch = SelectedBranch();
            if (branch == null)
            {
                return;
            }

            List<BranchAssignmentModel> managers;

            try
            {
                managers = await LoadManagerChoicesAsync();
            }
            catch (Exception ex)
            {
                _messageLabel.ForeColor = Color.Firebrick;
                _messageLabel.Text = BranchUi.GetMessage(ex);
                return;
            }

            using var form = new BranchEditForm(branch, managers);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateBranchAsync(_companyId, branch.BranchId, form.Result);
                await LoadBranchesAsync();
            }
            catch (Exception ex)
            {
                _messageLabel.ForeColor = Color.Firebrick;
                _messageLabel.Text = BranchUi.GetMessage(ex);
            }
        }

        private void EmployeesButton_Click(object? sender, EventArgs e)
        {
            using var form = new BranchAssignmentsForm(_companyId, _branches);
            form.ShowDialog(this);
        }

        private void InventoryButton_Click(object? sender, EventArgs e)
        {
            var branch = SelectedBranch();
            if (branch == null)
            {
                return;
            }

            using var form = new BranchInventoryForm(_companyId, branch);
            form.ShowDialog(this);
        }
    }
}
