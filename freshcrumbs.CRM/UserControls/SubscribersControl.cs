using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class SubscribersControl : UserControl
    {
        private readonly ApiService _apiService;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private const string FilterAll = "All";

        private DataGridView _grid = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterBox = null!;
        private Button _addButton = null!;
        private Button _viewButton = null!;
        private Label _statusLabel = null!;
        private Label _emptyLabel = null!;

        private List<SubscriberListItemModel> _subscribers = new();

        public SubscribersControl(string? initialStatus = null, string? initialSearch = null)
        {
            _apiService = new ApiService();

            InitializeLayout();

            if (!string.IsNullOrEmpty(initialStatus) && _statusFilterBox.Items.Contains(initialStatus))
            {
                _statusFilterBox.SelectedItem = initialStatus;
            }

            if (!string.IsNullOrEmpty(initialSearch))
            {
                _searchBox.Text = initialSearch;
            }

            Load += async (s, e) => await LoadSubscribersAsync();
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = PageBg
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            rootLayout.Controls.Add(new Label
            {
                Text = "Subscribers",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var toolbar = new Panel { Dock = DockStyle.Fill };

            _searchBox = new TextBox
            {
                Location = new Point(0, 10),
                Width = 280,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by code, name, email or plan..."
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_searchBox);

            toolbar.Controls.Add(new Label
            {
                Text = "Status:",
                Location = new Point(296, 14),
                AutoSize = true,
                ForeColor = LabelGray,
                Font = new Font("Segoe UI", 9.5f)
            });

            _statusFilterBox = new ComboBox
            {
                Location = new Point(344, 10),
                Width = 150,
                Font = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[]
            {
                FilterAll, "Active", "Expired", "Cancelled", "Expired / Cancelled", "Suspended", "Scheduled", "No Subscription"
            });
            _statusFilterBox.SelectedItem = FilterAll;
            _statusFilterBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_statusFilterBox);

            _addButton = CreateActionButton("+  Register Tenant", AccentColor, Color.White, 190, false);
            _addButton.Click += AddButton_Click;

            _viewButton = CreateActionButton("View Details", Color.White, LabelGray, 120, true);
            _viewButton.Click += ViewButton_Click;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(_addButton);
            buttons.Controls.Add(_viewButton);
            toolbar.Controls.Add(buttons);

            rootLayout.Controls.Add(toolbar, 0, 1);

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

            AddColumn("CompanyCode", "Code", 11);
            AddColumn("CompanyName", "Company", 20);
            AddColumn("PlanText", "Plan", 20);
            AddColumn("Status", "Status", 12);
            AddColumn("ExpiryText", "Expires (UTC)", 12);
            AddColumn("UsersText", "Users", 9);
            AddColumn("Email", "Email", 20);

            _grid.SelectionChanged += (s, e) => UpdateButtonStates();
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    ViewButton_Click(s, EventArgs.Empty);
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

            rootLayout.Controls.Add(_grid, 0, 2);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.Firebrick,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            rootLayout.Controls.Add(_statusLabel, 0, 3);

            Controls.Add(rootLayout);
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

        private Button CreateActionButton(string text, Color back, Color fore, int width, bool bordered)
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

        private async Task LoadSubscribersAsync()
        {
            AdminUi.ShowInfo(_statusLabel, "Loading subscribers...");

            try
            {
                _subscribers = await _apiService.GetSubscribersAsync();
                ApplyFilter();
                _statusLabel.Text = "";
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

            var rows = _subscribers
                .Where(x => status == FilterAll
                    || (status == "Expired / Cancelled" ? x.Status == "Expired" || x.Status == "Cancelled" : x.Status == status))
                .Where(x => search.Length == 0
                    || x.CompanyCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.PlanText ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || x.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _grid.DataSource = rows;
            _grid.ClearSelection();

            _emptyLabel.Text = _subscribers.Count == 0
                ? "No subscribers yet. Click \"+ Register Tenant\" to add the first company."
                : "No subscribers match your search or filter.";
            _emptyLabel.Visible = rows.Count == 0;
            _emptyLabel.Width = _grid.ClientSize.Width;

            UpdateButtonStates();
        }

        private SubscriberListItemModel? SelectedSubscriber()
        {
            if (_grid.CurrentRow?.DataBoundItem is SubscriberListItemModel item && _grid.SelectedRows.Count > 0)
            {
                return item;
            }

            return null;
        }

        private void UpdateButtonStates()
        {
            _viewButton.Enabled = SelectedSubscriber() != null;
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new SubscriberRegistrationForm();

            if (form.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadSubscribersAsync();
                AdminUi.ShowSuccess(_statusLabel, "Tenant registered.");
            }
        }

        private async void ViewButton_Click(object? sender, EventArgs e)
        {
            var subscriber = SelectedSubscriber();

            if (subscriber == null)
            {
                return;
            }

            using var form = new SubscriberDetailsForm(subscriber.CompanyId);
            form.ShowDialog(FindForm());

            await LoadSubscribersAsync();
        }
    }
}