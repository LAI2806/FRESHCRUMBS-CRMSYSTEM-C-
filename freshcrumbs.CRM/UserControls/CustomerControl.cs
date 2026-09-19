using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class CustomerControl : UserControl
    {
        private readonly ApiService _apiService;
        private readonly int _companyId;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private DataGridView _customerGrid = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _deleteButton = null!;
        private Label _statusLabel = null!;

        private List<CustomerModel> _customers = new();

        public CustomerControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += CustomerControl_Load;
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
                Text = "Customer Management",
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
                PlaceholderText = "Search by name, code, or email..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);

            _statusFilterBox = new ComboBox
            {
                Location = new Point(330, 10),
                Width = 200,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[] { "All Customers", "Active Customers", "Inactive Customers" });
            _statusFilterBox.SelectedIndex = 0;
            _statusFilterBox.SelectedIndexChanged += StatusFilterBox_SelectedIndexChanged;
            toolbarPanel.Controls.Add(_statusFilterBox);

            _addButton = CreateActionButton("+  Add Customer", AccentColor, Color.White, 160);
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

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);
            PositionToolbarButtons(toolbarPanel);

            rootLayout.Controls.Add(toolbarPanel, 0, 1);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            _customerGrid = new DataGridView
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

            _customerGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _customerGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _customerGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _customerGrid.ColumnHeadersHeight = 42;
            _customerGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _customerGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _customerGrid.DefaultCellStyle.SelectionForeColor = TextDark;
            _customerGrid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            _customerGrid.SelectionChanged += CustomerGrid_SelectionChanged;

            gridContainer.Controls.Add(_customerGrid);
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
            _deleteButton.Location = new Point(_editButton.Left - _deleteButton.Width - 10, 8);
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

        private async void CustomerControl_Load(object? sender, EventArgs e)
        {
            await LoadCustomersAsync();
        }

        private async Task LoadCustomersAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _customers = await _apiService.GetCustomersAsync(_companyId, includeInactive: true);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load customers: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private void BindGrid(List<CustomerModel> customers)
        {
            _customerGrid.AutoGenerateColumns = true;
            _customerGrid.DataSource = null;
            _customerGrid.DataSource = customers;

            if (_customerGrid.Columns["CustomerId"] != null)
            {
                _customerGrid.Columns["CustomerId"].Visible = false;
            }

            SetColumnHeader("CustomerCode", "Code");
            SetColumnHeader("FirstName", "First Name");
            SetColumnHeader("LastName", "Last Name");
            SetColumnHeader("Email", "Email");
            SetColumnHeader("ContactNo", "Contact No.");
            SetColumnHeader("Address", "Address");
            SetColumnHeader("LoyaltyPoints", "Points");
            SetColumnHeader("Status", "Status");
        }

        private void SetColumnHeader(string columnName, string headerText)
        {
            if (_customerGrid.Columns[columnName] != null)
            {
                _customerGrid.Columns[columnName].HeaderText = headerText;
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

        private void ApplyFilters()
        {
            string term = _searchBox.Text.Trim().ToLowerInvariant();
            string statusFilter = _statusFilterBox.SelectedItem?.ToString() ?? "All Customers";

            IEnumerable<CustomerModel> filtered = _customers;

            if (statusFilter == "Active Customers")
            {
                filtered = filtered.Where(c => c.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));
            }
            else if (statusFilter == "Inactive Customers")
            {
                filtered = filtered.Where(c => c.Status.Equals("Inactive", StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(term))
            {
                filtered = filtered.Where(c =>
                    c.FirstName.ToLowerInvariant().Contains(term) ||
                    c.LastName.ToLowerInvariant().Contains(term) ||
                    c.CustomerCode.ToLowerInvariant().Contains(term) ||
                    c.Email.ToLowerInvariant().Contains(term));
            }

            BindGrid(filtered.ToList());
        }

        private void CustomerGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = _customerGrid.SelectedRows.Count > 0;
            _editButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
        }

        private string GenerateNextCustomerCode()
        {
            const string prefix = "CUST-";
            int maxNumber = 0;

            foreach (var customer in _customers)
            {
                if (customer.CustomerCode.StartsWith(prefix) &&
                    int.TryParse(customer.CustomerCode.Substring(prefix.Length), out int number))
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
            string suggestedCode = GenerateNextCustomerCode();
            using var form = new CustomerEditForm(null, suggestedCode);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreateCustomerAsync(_companyId, form.Result);
                await LoadCustomersAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create customer: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            if (_customerGrid.SelectedRows.Count == 0 ||
                _customerGrid.SelectedRows[0].DataBoundItem is not CustomerModel selectedCustomer)
            {
                return;
            }

            using var form = new CustomerEditForm(selectedCustomer);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateCustomerAsync(_companyId, selectedCustomer.CustomerId, form.Result);
                await LoadCustomersAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update customer: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_customerGrid.SelectedRows.Count == 0 ||
                _customerGrid.SelectedRows[0].DataBoundItem is not CustomerModel selectedCustomer)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete \"{selectedCustomer.FirstName} {selectedCustomer.LastName}\"?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeleteCustomerAsync(_companyId, selectedCustomer.CustomerId);
                await LoadCustomersAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to delete customer: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }
    }
}