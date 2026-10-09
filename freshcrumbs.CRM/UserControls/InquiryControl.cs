using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class InquiryControl : UserControl
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

        private DataGridView _inquiryGrid = null!;
        private TextBox _searchBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;

        private Button _deleteButton = null!;
        private Label _statusLabel = null!;
        private Panel _pagerPanel = null!;
        private Label _pageInfoLabel = null!;
        private Button _prevPageButton = null!;
        private Button _nextPageButton = null!;

        private List<InquiryModel> _inquiries = new();
        private List<CustomerModel> _customers = new();
        private List<InquiryDisplayRow> _filteredRows = new();
        private int _currentPage = 1;

        private class InquiryDisplayRow
        {
            public int InquiryId { get; set; }
            public string CustomerCode { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public DateTime DateSubmitted { get; set; }
            public string Source { get; set; } = string.Empty;
            public string Concern { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string RespondedBy { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
        }

        public InquiryControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += InquiryControl_Load;
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
                Text = "Inquiries",
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
                Width = 300,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by subject, code, or customer..."
            };
            _searchBox.TextChanged += (s, e) => BindGrid();
            toolbarPanel.Controls.Add(_searchBox);
            AddBranchFilter(toolbarPanel);

            _addButton = CreateActionButton("+  New Inquiry", AccentColor, Color.White, 150);
            _addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _addButton.Click += AddButton_Click;
            toolbarPanel.Controls.Add(_addButton);

            _editButton = CreateActionButton("Review / Process", Color.White, LabelGray, 150);
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

            // STAFF may record inquiries; reviewing/processing and deleting is a management action.
            if (!TenantCapabilities.CanManageInquiries)
            {
                _editButton.Visible = false;
                _deleteButton.Visible = false;
            }

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);
            PositionToolbarButtons(toolbarPanel);

            rootLayout.Controls.Add(toolbarPanel, 0, 1);

            var gridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };

            _inquiryGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(235, 230, 225),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9.5f),
                RowTemplate = { Height = 38 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
            };
            _inquiryGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _inquiryGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _inquiryGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _inquiryGrid.ColumnHeadersHeight = 42;
            _inquiryGrid.SelectionChanged += InquiryGrid_SelectionChanged;

            gridContainer.Controls.Add(_inquiryGrid);
            rootLayout.Controls.Add(gridContainer, 0, 2);

            _pagerPanel = CreatePagerPanel();
            gridContainer.Controls.Add(_pagerPanel);

            _statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick
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
            int totalRecords = _filteredRows.Count;
            int totalPages = totalRecords == 0 ? 1 : (int)Math.Ceiling(totalRecords / (double)PageSize);

            if (_currentPage > totalPages)
            {
                _currentPage = totalPages;
            }
            if (_currentPage < 1)
            {
                _currentPage = 1;
            }

            var pageItems = _filteredRows
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _inquiryGrid.AutoGenerateColumns = true;
            _inquiryGrid.DataSource = null;
            _inquiryGrid.DataSource = pageItems;

            if (_inquiryGrid.Columns["BranchName"] != null)
            {
                _inquiryGrid.Columns["BranchName"].Visible = TenantCapabilities.HasBranching;
                _inquiryGrid.Columns["BranchName"].HeaderText = "Branch";
            }

            if (_inquiryGrid.Columns["InquiryId"] != null)
            {
                _inquiryGrid.Columns["InquiryId"].Visible = false;
            }

            SetHeader("CustomerCode", "Customer Code");
            SetHeader("CustomerName", "Customer Name");
            SetHeader("Type", "Type");
            SetHeader("DateSubmitted", "Date Received");
            SetHeader("Source", "Source");
            SetHeader("Concern", "Concern");
            SetHeader("Status", "Status");
            SetHeader("RespondedBy", "Responded By");

            if (_inquiryGrid.Columns["DateSubmitted"] != null)
            {
                _inquiryGrid.Columns["DateSubmitted"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }

            _pageInfoLabel.Text = totalRecords == 0
                ? "No records"
                : $"Page {_currentPage} of {totalPages} ({totalRecords} {(totalRecords == 1 ? "record" : "records")})";

            _prevPageButton.Enabled = _currentPage > 1;
            _nextPageButton.Enabled = _currentPage < totalPages;
        }

        private async void InquiryControl_Load(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _statusLabel.Text = "";
                // Include inactive customers so older records still show their Customer Code and name.
                _customers = await _apiService.GetCustomersAsync(_companyId, includeInactive: true);
                _inquiries = await _apiService.GetInquiriesAsync(_companyId);
                await PopulateBranchFilterAsync();
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load inquiries: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private const int AllBranches = -1;
        private const int NoBranch = 0;
        private ComboBox? _branchFilter;

        // PREMIUM ADMIN only: filter by branch. MANAGER / STAFF already receive only their branch from the API.
        private void AddBranchFilter(Panel toolbarPanel)
        {
            if (!TenantCapabilities.CanFilterByBranch)
            {
                return;
            }

            _branchFilter = new ComboBox
            {
                Location = new Point(330, 10),
                Width = 200,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _branchFilter.SelectedIndexChanged += (s, e) => BindGrid();
            toolbarPanel.Controls.Add(_branchFilter);
        }

        private async Task PopulateBranchFilterAsync()
        {
            if (_branchFilter == null || _branchFilter.Items.Count > 0)
            {
                return;
            }

            var branches = await _apiService.GetBranchesAsync(_companyId);

            _branchFilter.Items.Add(new BranchModel { BranchId = AllBranches, BranchName = "All Branches" });

            foreach (var branch in branches.Where(b => string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase)))
            {
                _branchFilter.Items.Add(branch);
            }

            _branchFilter.Items.Add(new BranchModel { BranchId = NoBranch, BranchName = "No branch (earlier records)" });
            _branchFilter.SelectedIndex = 0;
        }

        private bool MatchesBranchFilter(int? branchId)
        {
            int selected = (_branchFilter?.SelectedItem as BranchModel)?.BranchId ?? AllBranches;

            return selected == AllBranches
                || (selected == NoBranch ? branchId == null : branchId == selected);
        }

        private void BindGrid(bool resetPage = true)
        {
            string term = _searchBox?.Text.Trim().ToLowerInvariant() ?? "";

            _filteredRows = _inquiries
                .Where(x => MatchesBranchFilter(x.BranchId))
                .Select(i => new InquiryDisplayRow
                {
                    InquiryId = i.InquiryId,
                    CustomerCode = GetCustomerCode(i.CustomerId),
                    CustomerName = GetCustomerName(i.CustomerId),
                    Type = i.Type,
                    DateSubmitted = i.DateSubmitted,
                    Source = i.Source,
                    Concern = i.Message,
                    Status = i.Status,
                    RespondedBy = i.RespondedBy,
                    BranchName = i.BranchName ?? string.Empty
                })
                .Where(row =>
                    string.IsNullOrEmpty(term) ||
                    row.Concern.ToLowerInvariant().Contains(term) ||
                    row.CustomerCode.ToLowerInvariant().Contains(term) ||
                    row.CustomerName.ToLowerInvariant().Contains(term))
                .ToList();

            if (resetPage)
            {
                _currentPage = 1;
            }

            RenderCurrentPage();
        }

        private void SetHeader(string column, string text)
        {
            if (_inquiryGrid.Columns[column] != null)
            {
                _inquiryGrid.Columns[column].HeaderText = text;
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

        private InquiryModel? GetSelectedInquiry()
        {
            if (_inquiryGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            dynamic row = _inquiryGrid.SelectedRows[0].DataBoundItem;
            int id = row.InquiryId;
            return _inquiries.FirstOrDefault(i => i.InquiryId == id);
        }

        private void InquiryGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = GetSelectedInquiry() != null;
            _editButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new InquiryEditForm(null, _customers);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreateInquiryAsync(_companyId, form.Result);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create inquiry: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedInquiry();
            if (selected == null)
            {
                return;
            }

            using var form = new InquiryEditForm(selected, _customers);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateInquiryAsync(_companyId, selected.InquiryId, form.Result);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update inquiry: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedInquiry();
            if (selected == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "Are you sure you want to delete this inquiry? It will be hidden from the list but the record will be kept.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeleteInquiryAsync(_companyId, selected.InquiryId);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to delete inquiry: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

    }
}