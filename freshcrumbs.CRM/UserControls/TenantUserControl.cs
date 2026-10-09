using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    // Tenant User Management (ADMIN only). Employees of the signed-in company; the API enforces the same rules.
    public class TenantUserControl : UserControl
    {
        private const string AllOption = "All";
        private const string UnassignedOption = "Unassigned";

        private readonly ApiService _apiService = new();
        private readonly int _companyId;
        private readonly bool _showBranches = TenantCapabilities.HasBranching && TenantCapabilities.CanManageBranches;

        private TextBox _searchBox = null!;
        private ComboBox _roleFilter = null!;
        private ComboBox? _branchFilter;
        private ComboBox _statusFilter = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _statusButton = null!;
        private DataGridView _grid = null!;
        private Label _offlineLabel = null!;
        private Label _messageLabel = null!;

        private List<TenantUserModel> _users = new();
        private List<BranchModel>? _branches;
        private bool _online;
        private int _loadVersion;

        public TenantUserControl(int companyId)
        {
            _companyId = companyId;

            Dock = DockStyle.Fill;
            BackColor = BranchUi.PageBg;

            InitializeLayout();
            Load += async (s, e) => await LoadUsersAsync();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = BranchUi.PageBg };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                Text = "User Management",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = BranchUi.TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };

            _addButton = BranchUi.CreateButton("+  Add Employee", true, 160);
            _addButton.Click += AddButton_Click;

            _editButton = BranchUi.CreateButton("Edit", false, 100);
            _editButton.Click += EditButton_Click;

            _statusButton = BranchUi.CreateButton("Deactivate", false, 120);
            _statusButton.Click += StatusButton_Click;

            var refreshButton = BranchUi.CreateButton("Refresh", false, 100);
            refreshButton.Click += async (s, e) => await LoadUsersAsync();

            toolbar.Controls.Add(_addButton);
            toolbar.Controls.Add(_editButton);
            toolbar.Controls.Add(_statusButton);
            toolbar.Controls.Add(refreshButton);
            root.Controls.Add(toolbar, 0, 1);

            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };

            filters.Controls.Add(CreateFilterLabel("Search:"));
            _searchBox = new TextBox
            {
                Width = 220,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Name, email or contact number",
                Margin = new Padding(0, 2, 16, 0)
            };
            _searchBox.TextChanged += (s, e) => ApplyFilters();
            filters.Controls.Add(_searchBox);

            filters.Controls.Add(CreateFilterLabel("Role:"));
            _roleFilter = CreateFilterBox(AllOption, TenantRole.Admin, TenantRole.Manager, TenantRole.Staff);
            filters.Controls.Add(_roleFilter);

            if (_showBranches)
            {
                filters.Controls.Add(CreateFilterLabel("Branch:"));
                _branchFilter = CreateFilterBox(AllOption, UnassignedOption);
                filters.Controls.Add(_branchFilter);
            }

            filters.Controls.Add(CreateFilterLabel("Status:"));
            _statusFilter = CreateFilterBox(AllOption, "Active", "Inactive");
            filters.Controls.Add(_statusFilter);

            root.Controls.Add(filters, 0, 2);

            _offlineLabel = new Label
            {
                Text = "Offline: showing the list from the last sync. Adding, editing and activating employees needs the internet.",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            root.Controls.Add(_offlineLabel, 0, 3);

            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            _grid = BranchUi.CreateGrid();
            _grid.SelectionChanged += (s, e) => UpdateButtons();
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    EditButton_Click(s, e);
                }
            };
            gridContainer.Controls.Add(_grid);
            root.Controls.Add(gridContainer, 0, 4);

            _messageLabel = BranchUi.CreateMessageLabel();
            root.Controls.Add(_messageLabel, 0, 5);

            Controls.Add(root);
        }

        private static Label CreateFilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = BranchUi.TextDark,
                AutoSize = true,
                Margin = new Padding(0, 6, 6, 0)
            };
        }

        private ComboBox CreateFilterBox(params string[] options)
        {
            var box = new ComboBox
            {
                Width = 140,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 16, 0)
            };

            box.Items.AddRange(options);
            box.SelectedIndex = 0;
            box.SelectedIndexChanged += (s, e) => ApplyFilters();
            return box;
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                ShowMessage("", false);
                int version = ++_loadVersion;

                var list = await _apiService.GetTenantUsersAsync(_companyId);
                var branches = _showBranches ? await _apiService.GetBranchesAsync(_companyId) : null;

                // A newer load (Refresh pressed again) wins.
                if (version != _loadVersion || IsDisposed)
                {
                    return;
                }

                _users = list.Users;
                _online = list.Online;

                if (branches != null)
                {
                    _branches = branches;
                    RefreshBranchFilter();
                }

                _offlineLabel.Visible = !_online;
                _addButton.Enabled = _online;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                ShowMessage(BranchUi.GetMessage(ex), true);
            }
        }

        private void RefreshBranchFilter()
        {
            if (_branchFilter == null || _branches == null)
            {
                return;
            }

            var selected = _branchFilter.SelectedItem?.ToString();

            _branchFilter.Items.Clear();
            _branchFilter.Items.Add(AllOption);
            _branchFilter.Items.Add(UnassignedOption);

            foreach (var name in _branches.Select(b => b.BranchName).OrderBy(n => n))
            {
                _branchFilter.Items.Add(name);
            }

            _branchFilter.SelectedItem = selected != null && _branchFilter.Items.Contains(selected) ? selected : AllOption;
        }

        private void ApplyFilters()
        {
            if (_grid == null)
            {
                return;
            }

            string search = _searchBox.Text.Trim();
            string role = _roleFilter.SelectedItem?.ToString() ?? AllOption;
            string branch = _branchFilter?.SelectedItem?.ToString() ?? AllOption;
            string status = _statusFilter.SelectedItem?.ToString() ?? AllOption;

            var rows = _users.Where(u =>
                    (search.Length == 0
                        || u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || u.ContactNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
                    && (role == AllOption || string.Equals(u.Role, role, StringComparison.OrdinalIgnoreCase))
                    && (branch == AllOption
                        || (branch == UnassignedOption && u.BranchId == null)
                        || string.Equals(u.BranchName, branch, StringComparison.OrdinalIgnoreCase))
                    && (status == AllOption || string.Equals(u.Status, status, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _grid.DataSource = null;
            _grid.DataSource = rows;

            var columns = new List<(string Name, string Header)>
            {
                ("FullName", "Employee Name"),
                ("Email", "Email"),
                ("Role", "Role")
            };

            if (_showBranches)
            {
                columns.Add(("BranchDisplay", "Assigned Branch"));
            }

            columns.Add(("ContactNumber", "Contact Number"));
            columns.Add(("Status", "Status"));

            BranchUi.ShowColumns(_grid, columns.ToArray());
            UpdateButtons();
        }

        private TenantUserModel? SelectedUser()
        {
            return _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].DataBoundItem as TenantUserModel : null;
        }

        private void UpdateButtons()
        {
            var user = SelectedUser();
            bool editable = _online && user != null && user.CanEdit;

            _editButton.Enabled = editable;
            _statusButton.Enabled = editable;
            _statusButton.Text = user != null && !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
                ? "Activate"
                : "Deactivate";
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new TenantUserEditForm(null, _showBranches ? _branches : null);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            CreatedTenantUserModel created;

            try
            {
                created = await _apiService.CreateTenantUserAsync(_companyId, form.Result);
            }
            catch (Exception ex)
            {
                ShowMessage(BranchUi.GetMessage(ex), true);
                return;
            }

            string? branchError = await AssignBranchAsync(created.User.Id, form.Result.BranchId, form.BranchChanged);

            using (var passwordForm = new TemporaryPasswordForm(created.User.FullName, created.User.Email, created.TemporaryPassword))
            {
                passwordForm.ShowDialog(this);
            }

            await LoadUsersAsync();

            if (branchError != null)
            {
                ShowMessage($"The account was created, but the branch could not be assigned: {branchError}", true);
            }
            else
            {
                ShowMessage($"{created.User.FullName} was added.", false);
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            var user = SelectedUser();

            if (user == null || !_online || !user.CanEdit)
            {
                return;
            }

            using var form = new TenantUserEditForm(user, _showBranches ? _branches : null);

            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateTenantUserAsync(_companyId, user.Id, form.Result);
            }
            catch (Exception ex)
            {
                ShowMessage(BranchUi.GetMessage(ex), true);
                return;
            }

            // Promoted to MANAGER while already at a branch: re-apply the assignment so that branch keeps one manager.
            bool becameManager = !string.Equals(user.Role, TenantRole.Manager, StringComparison.OrdinalIgnoreCase)
                && string.Equals(form.Result.Role, TenantRole.Manager, StringComparison.OrdinalIgnoreCase)
                && form.Result.BranchId != null;

            string? branchError = await AssignBranchAsync(user.Id, form.Result.BranchId, form.BranchChanged || becameManager);

            await LoadUsersAsync();

            if (branchError != null)
            {
                ShowMessage($"The details were saved, but the branch could not be changed: {branchError}", true);
            }
            else
            {
                ShowMessage($"{form.Result.FirstName} {form.Result.LastName} was updated.", false);
            }
        }

        private async void StatusButton_Click(object? sender, EventArgs e)
        {
            var user = SelectedUser();

            if (user == null || !_online || !user.CanEdit)
            {
                return;
            }

            bool activate = !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase);
            string question = activate
                ? $"Activate {user.FullName}? They will be able to sign in again."
                : $"Deactivate {user.FullName}? They will no longer be able to sign in. Their records are kept.";

            if (MessageBox.Show(this, question, activate ? "Activate Employee" : "Deactivate Employee",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.SetTenantUserStatusAsync(_companyId, user.Id, activate ? "Active" : "Inactive");
                await LoadUsersAsync();
                ShowMessage($"{user.FullName} is now {(activate ? "active" : "inactive")}.", false);
            }
            catch (Exception ex)
            {
                ShowMessage(BranchUi.GetMessage(ex), true);
            }
        }

        private async Task<string?> AssignBranchAsync(string userId, int? branchId, bool changed)
        {
            if (!_showBranches || !changed)
            {
                return null;
            }

            try
            {
                await _apiService.SetBranchAssignmentAsync(_companyId, userId, branchId);
                return null;
            }
            catch (Exception ex)
            {
                return BranchUi.GetMessage(ex);
            }
        }

        private void ShowMessage(string message, bool isError)
        {
            _messageLabel.ForeColor = isError ? Color.Firebrick : Color.FromArgb(46, 130, 80);
            _messageLabel.Text = message;
        }
    }
}
