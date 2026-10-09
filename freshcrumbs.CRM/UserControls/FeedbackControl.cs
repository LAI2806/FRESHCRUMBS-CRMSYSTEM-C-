using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class FeedbackControl : UserControl
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

        private DataGridView _feedbackGrid = null!;
        private TextBox _searchBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _deleteButton = null!;
        private Label _statusLabel = null!;
        private Panel _pagerPanel = null!;
        private Label _pageInfoLabel = null!;
        private Button _prevPageButton = null!;
        private Button _nextPageButton = null!;

        private List<FeedbackModel> _feedbackList = new();
        private List<CustomerModel> _customers = new();
        private List<FeedbackDisplayRow> _filteredRows = new();
        private int _currentPage = 1;

        private class FeedbackDisplayRow
        {
            public int FeedbackId { get; set; }
            public string CustomerCode { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Comment { get; set; } = string.Empty;
            public DateTime DateSubmitted { get; set; }
            public string Status { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
        }

        public FeedbackControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += FeedbackControl_Load;
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
                Text = "Feedback Management",
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
                PlaceholderText = "Search code, customer, category, or type..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);
            AddBranchFilter(toolbarPanel);

            _addButton = CreateActionButton("+  Add Feedback", AccentColor, Color.White, 160);
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

            // STAFF may record feedback and complaints; managing existing records is a management action.
            if (!TenantCapabilities.CanManageFeedback)
            {
                _editButton.Visible = false;
                _deleteButton.Visible = false;
            }

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);

            toolbarPanel.Resize += (s, e) => PositionToolbarButtons(toolbarPanel);
            PositionToolbarButtons(toolbarPanel);

            rootLayout.Controls.Add(toolbarPanel, 0, 1);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 10, 0, 0)
            };

            _feedbackGrid = new DataGridView
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

            _feedbackGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _feedbackGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _feedbackGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _feedbackGrid.ColumnHeadersHeight = 42;
            _feedbackGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _feedbackGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _feedbackGrid.DefaultCellStyle.SelectionForeColor = TextDark;
            _feedbackGrid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            _feedbackGrid.SelectionChanged += FeedbackGrid_SelectionChanged;

            gridContainer.Controls.Add(_feedbackGrid);
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

            _feedbackGrid.AutoGenerateColumns = true;
            _feedbackGrid.DataSource = null;
            _feedbackGrid.DataSource = pageItems;

            if (_feedbackGrid.Columns["BranchName"] != null)
            {
                _feedbackGrid.Columns["BranchName"].Visible = TenantCapabilities.HasBranching;
                _feedbackGrid.Columns["BranchName"].HeaderText = "Branch";
            }

            if (_feedbackGrid.Columns["FeedbackId"] != null)
            {
                _feedbackGrid.Columns["FeedbackId"].Visible = false;
            }

            SetColumnHeader("CustomerCode", "Customer Code");
            SetColumnHeader("CustomerName", "Customer Name");
            SetColumnHeader("Type", "Type");
            SetColumnHeader("Category", "Category");
            SetColumnHeader("Comment", "Comment");
            SetColumnHeader("DateSubmitted", "Date Submitted");
            SetColumnHeader("Status", "Status");

            if (_feedbackGrid.Columns["DateSubmitted"] != null)
            {
                _feedbackGrid.Columns["DateSubmitted"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }

            _pageInfoLabel.Text = totalRecords == 0
                ? "No records"
                : $"Page {_currentPage} of {totalPages} ({totalRecords} {(totalRecords == 1 ? "record" : "records")})";

            _prevPageButton.Enabled = _currentPage > 1;
            _nextPageButton.Enabled = _currentPage < totalPages;
        }

        private async void FeedbackControl_Load(object? sender, EventArgs e)
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
                _feedbackList = await _apiService.GetFeedbackAsync(_companyId);
                await PopulateBranchFilterAsync();
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load feedback: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
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

            _filteredRows = _feedbackList
                .Where(x => MatchesBranchFilter(x.BranchId))
                .Select(f => new FeedbackDisplayRow
                {
                    FeedbackId = f.FeedbackId,
                    CustomerCode = GetCustomerCode(f.CustomerId),
                    CustomerName = GetCustomerName(f.CustomerId),
                    Type = f.Type,
                    Category = f.Category,
                    Comment = f.Comment,
                    DateSubmitted = f.DateSubmitted,
                    Status = f.Status,
                    BranchName = f.BranchName ?? string.Empty
                })
                .Where(row =>
                    string.IsNullOrEmpty(term) ||
                    row.CustomerCode.ToLowerInvariant().Contains(term) ||
                    row.CustomerName.ToLowerInvariant().Contains(term) ||
                    row.Category.ToLowerInvariant().Contains(term) ||
                    row.Type.ToLowerInvariant().Contains(term))
                .ToList();

            if (resetPage)
            {
                _currentPage = 1;
            }

            RenderCurrentPage();
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

        private void SetColumnHeader(string columnName, string headerText)
        {
            if (_feedbackGrid.Columns[columnName] != null)
            {
                _feedbackGrid.Columns[columnName].HeaderText = headerText;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            BindGrid();
        }

        private void FeedbackGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = _feedbackGrid.SelectedRows.Count > 0;
            _editButton.Enabled = hasSelection;
            _deleteButton.Enabled = hasSelection;
        }

        private FeedbackModel? GetSelectedFeedback()
        {
            if (_feedbackGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            dynamic row = _feedbackGrid.SelectedRows[0].DataBoundItem;
            int id = row.FeedbackId;

            return _feedbackList.FirstOrDefault(f => f.FeedbackId == id);
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new FeedbackEditForm(null, _customers);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreateFeedbackAsync(_companyId, form.Result);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create feedback: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            var selectedFeedback = GetSelectedFeedback();
            if (selectedFeedback == null)
            {
                return;
            }

            using var form = new FeedbackEditForm(selectedFeedback, _customers);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdateFeedbackAsync(_companyId, selectedFeedback.FeedbackId, form.Result);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update feedback: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            var selectedFeedback = GetSelectedFeedback();
            if (selectedFeedback == null)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete this feedback: \"{selectedFeedback.Category}\"?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeleteFeedbackAsync(_companyId, selectedFeedback.FeedbackId);
                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to delete feedback: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }
    }
}