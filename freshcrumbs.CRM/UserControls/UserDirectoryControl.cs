using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class UserDirectoryControl : UserControl
    {
        private readonly ApiService _apiService = new();

        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private const string AllTenants = "All tenants";
        private const string AllRoles = "All roles";
        private const string AllStatuses = "All statuses";

        private DataGridView _grid = null!;
        private Label _emptyLabel = null!;
        private TextBox _searchBox = null!;
        private ComboBox _tenantBox = null!;
        private ComboBox _roleBox = null!;
        private ComboBox _statusBox = null!;
        private Button _refreshButton = null!;
        private Label _statusLabel = null!;

        private List<UserDirectoryItemModel> _users = new();
        private bool _binding;

        public UserDirectoryControl()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            InitializeLayout();

            Load += async (s, e) => await LoadAsync();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = PageBg
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            root.Controls.Add(new Label
            {
                Text = "User Directory",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var toolbar = new Panel { Dock = DockStyle.Fill };

            _searchBox = new TextBox
            {
                Location = new Point(0, 10),
                Width = 240,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search user, email or tenant..."
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_searchBox);

            _tenantBox = CreateCombo(250, 170, AllTenants);
            _roleBox = CreateCombo(430, 120, AllRoles);
            _statusBox = CreateCombo(560, 120, AllStatuses);
            toolbar.Controls.Add(_tenantBox);
            toolbar.Controls.Add(_roleBox);
            toolbar.Controls.Add(_statusBox);

            _refreshButton = new Button
            {
                Text = "Refresh",
                Dock = DockStyle.Right,
                Width = 96,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat
            };
            _refreshButton.FlatAppearance.BorderColor = BorderColor;
            _refreshButton.Click += async (s, e) => await LoadAsync();
            toolbar.Controls.Add(_refreshButton);

            root.Controls.Add(toolbar, 0, 1);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 38 },
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = LabelGray;
            _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderRowColor;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 240, 232);
            _grid.DefaultCellStyle.SelectionForeColor = TextDark;

            AddColumn("UserText", "User", 28);
            AddColumn("TenantName", "Tenant", 26);
            AddColumn("Role", "Role", 14);
            AddColumn("Status", "Status", 10);
            AddColumn("Email", "Email", 22);

            _emptyLabel = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(0, 80),
                Height = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = LabelGray,
                Font = new Font("Segoe UI", 10.5f),
                Visible = false
            };
            _grid.Controls.Add(_emptyLabel);
            _grid.Resize += (s, e) => _emptyLabel.Width = _grid.ClientSize.Width;

            root.Controls.Add(_grid, 0, 2);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_statusLabel, 0, 3);

            Controls.Add(root);
        }

        private ComboBox CreateCombo(int left, int width, string allText)
        {
            var box = new ComboBox
            {
                Location = new Point(left, 10),
                Width = width,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            box.Items.Add(allText);
            box.SelectedIndex = 0;
            box.SelectedIndexChanged += (s, e) =>
            {
                if (!_binding)
                {
                    ApplyFilter();
                }
            };
            return box;
        }

        private void AddColumn(string property, string header, float weight)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = header,
                FillWeight = weight
            });
        }

        private async Task LoadAsync()
        {
            _refreshButton.Enabled = false;
            AdminUi.ShowInfo(_statusLabel, "Loading users...");

            try
            {
                _users = await _apiService.GetUserDirectoryAsync();
                FillFilterLists();
                ApplyFilter();
                AdminUi.ShowInfo(_statusLabel, "View-only directory. Each tenant manages its own users.");
            }
            catch (Exception ex) when (AdminUi.IsAccessDenied(ex))
            {
                Controls.Clear();
                Controls.Add(new StatePanel("Access denied", ex.Message));
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_statusLabel, ex);
            }
            finally
            {
                _refreshButton.Enabled = true;
            }
        }

        private void FillFilterLists()
        {
            _binding = true;

            Refill(_tenantBox, AllTenants, _users.Select(u => u.TenantName));
            Refill(_roleBox, AllRoles, _users.Select(u => u.Role));
            Refill(_statusBox, AllStatuses, _users.Select(u => u.Status));

            _binding = false;
        }

        private static void Refill(ComboBox box, string allText, IEnumerable<string> values)
        {
            string? previous = box.SelectedItem?.ToString();

            box.Items.Clear();
            box.Items.Add(allText);

            foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v))
            {
                box.Items.Add(value);
            }

            box.SelectedItem = previous != null && box.Items.Contains(previous) ? previous : allText;
        }

        private void ApplyFilter()
        {
            string search = _searchBox.Text.Trim();
            string tenant = _tenantBox.SelectedItem?.ToString() ?? AllTenants;
            string role = _roleBox.SelectedItem?.ToString() ?? AllRoles;
            string status = _statusBox.SelectedItem?.ToString() ?? AllStatuses;

            var rows = _users
                .Where(u => tenant == AllTenants || u.TenantName == tenant)
                .Where(u => role == AllRoles || u.Role == role)
                .Where(u => status == AllStatuses || u.Status == status)
                .Where(u => search.Length == 0
                    || u.UserText.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || u.TenantName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _grid.DataSource = rows;
            _grid.ClearSelection();

            _emptyLabel.Text = _users.Count == 0
                ? "No users yet."
                : "No users match your search or filters.";
            _emptyLabel.Visible = rows.Count == 0;
            _emptyLabel.Width = _grid.ClientSize.Width;
        }
    }
}