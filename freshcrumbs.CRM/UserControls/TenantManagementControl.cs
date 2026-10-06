using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class TenantManagementControl : UserControl
    {
        private readonly ApiService _apiService = new();

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private const string FilterAll = "All";

        private DataGridView _grid = null!;
        private Label _emptyLabel = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterBox = null!;
        private Button _editButton = null!;
        private Button _subscriptionButton = null!;
        private Label _statusLabel = null!;

        private List<SubscriberListItemModel> _tenants = new();
        private int? _selectedId;

        public event Action<string, string?, string?>? NavigateRequested;

        public TenantManagementControl()
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
                Text = "Tenant Management",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var toolbar = new Panel { Dock = DockStyle.Fill };

            _searchBox = new TextBox
            {
                Location = new Point(0, 10),
                Width = 260,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by code, name, email or database..."
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_searchBox);

            toolbar.Controls.Add(new Label
            {
                Text = "Status:",
                Location = new Point(276, 14),
                AutoSize = true,
                ForeColor = LabelGray
            });

            _statusFilterBox = new ComboBox
            {
                Location = new Point(324, 10),
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[] { FilterAll, "Active", "Inactive" });
            _statusFilterBox.SelectedItem = FilterAll;
            _statusFilterBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_statusFilterBox);

            _editButton = CreateButton("Edit Tenant", AccentColor, Color.White, 120, false);
            _editButton.Click += (s, e) => EditSelected();

            _subscriptionButton = CreateButton("View Subscription", Color.White, LabelGray, 160, true);
            _subscriptionButton.Click += (s, e) =>
            {
                var selected = Selected();

                if (selected != null)
                {
                    NavigateRequested?.Invoke("subscribers", null, selected.CompanyCode);
                }
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(_editButton);
            buttons.Controls.Add(_subscriptionButton);
            toolbar.Controls.Add(buttons);

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

            AddColumn("CompanyCode", "Code", 8);
            AddColumn("CompanyName", "Business", 17);
            AddColumn("Email", "Email", 15);
            AddColumn("TenantStatus", "Status", 7);
            AddColumn("ServerText", "Database Server", 16);
            AddColumn("DatabaseText", "Database", 11);
            AddColumn("DatabaseStatus", "DB Status", 9);
            AddColumn("PlanText", "Plan (read-only)", 15);
            AddColumn("Status", "Subscription", 10);

            _grid.SelectionChanged += (s, e) =>
            {
                _selectedId = _grid.SelectedRows.Count > 0
                    ? (_grid.SelectedRows[0].DataBoundItem as SubscriberListItemModel)?.CompanyId
                    : null;
                UpdateButtonStates();
            };
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    EditSelected();
                }
            };

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
            UpdateButtonStates();
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

        private Button CreateButton(string text, Color back, Color fore, int width, bool bordered)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 0, 0, 0)
            };
            button.FlatAppearance.BorderSize = bordered ? 1 : 0;
            button.FlatAppearance.BorderColor = BorderColor;
            return button;
        }

        private async Task LoadAsync()
        {
            AdminUi.ShowInfo(_statusLabel, "Loading tenants...");

            try
            {
                _tenants = await _apiService.GetSubscribersAsync();
                ApplyFilter();
                AdminUi.ShowInfo(_statusLabel, "Plans and subscription status are managed under Subscribers.");
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
        }

        private void ApplyFilter()
        {
            string search = _searchBox.Text.Trim();
            string status = _statusFilterBox.SelectedItem?.ToString() ?? FilterAll;

            var rows = _tenants
                .Where(t => status == FilterAll || t.TenantStatus == status)
                .Where(t => search.Length == 0
                    || t.CompanyCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || t.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || t.DatabaseText.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || t.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int? keep = _selectedId;

            _grid.DataSource = rows;
            _grid.ClearSelection();
            _selectedId = null;

            if (keep != null)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if ((row.DataBoundItem as SubscriberListItemModel)?.CompanyId == keep)
                    {
                        row.Selected = true;
                        _grid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            _emptyLabel.Text = _tenants.Count == 0
                ? "No tenants yet. Register the first tenant from Subscribers."
                : "No tenants match your search or filter.";
            _emptyLabel.Visible = rows.Count == 0;
            _emptyLabel.Width = _grid.ClientSize.Width;

            UpdateButtonStates();
        }

        private SubscriberListItemModel? Selected()
        {
            return _selectedId == null ? null : _tenants.FirstOrDefault(t => t.CompanyId == _selectedId);
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = Selected() != null;
            _editButton.Enabled = hasSelection;
            _subscriptionButton.Enabled = hasSelection;
        }

        private async void EditSelected()
        {
            var selected = Selected();

            if (selected == null)
            {
                return;
            }

            using var form = new TenantEditForm(selected.CompanyId);

            if (form.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAsync();
                AdminUi.ShowSuccess(_statusLabel, "Tenant information was updated.");
            }
        }
    }
}