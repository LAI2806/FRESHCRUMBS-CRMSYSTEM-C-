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

        private DataGridView _feedbackGrid = null!;
        private TextBox _searchBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _deleteButton = null!;
        private Label _statusLabel = null!;

        private List<FeedbackModel> _feedbackList = new();
        private List<CustomerModel> _customers = new();

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
                PlaceholderText = "Search by customer, subject, or type..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);

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

        private async void FeedbackControl_Load(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _customers = await _apiService.GetCustomersAsync(_companyId);
                _feedbackList = await _apiService.GetFeedbackAsync(_companyId);
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load feedback: {ex.Message}";
            }
        }

        private void BindGrid()
        {
            string term = _searchBox?.Text.Trim().ToLowerInvariant() ?? "";

            var displayRows = _feedbackList
                .Select(f => new
                {
                    f.FeedbackId,
                    CustomerName = GetCustomerName(f.CustomerId),
                    f.Type,
                    f.Subject,
                    f.Description,
                    f.DateSubmitted,
                    f.Status
                })
                .Where(row =>
                    string.IsNullOrEmpty(term) ||
                    row.CustomerName.ToLowerInvariant().Contains(term) ||
                    row.Subject.ToLowerInvariant().Contains(term) ||
                    row.Type.ToLowerInvariant().Contains(term))
                .ToList();

            _feedbackGrid.AutoGenerateColumns = true;
            _feedbackGrid.DataSource = null;
            _feedbackGrid.DataSource = displayRows;

            if (_feedbackGrid.Columns["FeedbackId"] != null)
            {
                _feedbackGrid.Columns["FeedbackId"].Visible = false;
            }

            SetColumnHeader("CustomerName", "Customer");
            SetColumnHeader("Type", "Type");
            SetColumnHeader("Subject", "Subject");
            SetColumnHeader("Description", "Description");
            SetColumnHeader("DateSubmitted", "Date Submitted");
            SetColumnHeader("Status", "Status");

            if (_feedbackGrid.Columns["DateSubmitted"] != null)
            {
                _feedbackGrid.Columns["DateSubmitted"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }
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
            _editButton.Enabled = _feedbackGrid.SelectedRows.Count > 0;
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
                _statusLabel.Text = $"Failed to create feedback: {ex.Message}";
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
                _statusLabel.Text = $"Failed to update feedback: {ex.Message}";
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
                $"Are you sure you want to delete this feedback: \"{selectedFeedback.Subject}\"?",
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
                _statusLabel.Text = $"Failed to delete feedback: {ex.Message}";
            }
        }
    }
}