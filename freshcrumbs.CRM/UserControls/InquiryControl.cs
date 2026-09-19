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

        private DataGridView _inquiryGrid = null!;
        private TextBox _searchBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _actionButton = null!;
        private Label _statusLabel = null!;

        private List<InquiryModel> _inquiries = new();
        private List<CustomerModel> _customers = new();

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
                PlaceholderText = "Search by subject or customer..."
            };
            _searchBox.TextChanged += (s, e) => BindGrid();
            toolbarPanel.Controls.Add(_searchBox);

            _addButton = CreateActionButton("+  New Inquiry", AccentColor, Color.White, 150);
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

            _actionButton = CreateActionButton("Start", Color.White, AccentColor, 100);
            _actionButton.FlatAppearance.BorderSize = 1;
            _actionButton.FlatAppearance.BorderColor = AccentColor;
            _actionButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _actionButton.Enabled = false;
            _actionButton.Click += ActionButton_Click;
            toolbarPanel.Controls.Add(_actionButton);

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
            _actionButton.Location = new Point(_editButton.Left - _actionButton.Width - 10, 8);
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

        private async void InquiryControl_Load(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _customers = await _apiService.GetCustomersAsync(_companyId);
                _inquiries = await _apiService.GetInquiriesAsync(_companyId);
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load inquiries: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private void BindGrid()
        {
            string term = _searchBox?.Text.Trim().ToLowerInvariant() ?? "";

            var displayRows = _inquiries
                .Select(i => new
                {
                    i.InquiryId,
                    CustomerName = GetCustomerName(i.CustomerId),
                    i.Subject,
                    i.Message,
                    i.DateSubmitted,
                    i.Status,
                    i.Response
                })
                .Where(row =>
                    string.IsNullOrEmpty(term) ||
                    row.Subject.ToLowerInvariant().Contains(term) ||
                    row.CustomerName.ToLowerInvariant().Contains(term))
                .ToList();

            _inquiryGrid.AutoGenerateColumns = true;
            _inquiryGrid.DataSource = null;
            _inquiryGrid.DataSource = displayRows;

            if (_inquiryGrid.Columns["InquiryId"] != null)
            {
                _inquiryGrid.Columns["InquiryId"].Visible = false;
            }

            SetHeader("CustomerName", "Customer");
            SetHeader("Subject", "Subject");
            SetHeader("Message", "Message");
            SetHeader("DateSubmitted", "Date");
            SetHeader("Status", "Status");
            SetHeader("Response", "Response");

            if (_inquiryGrid.Columns["DateSubmitted"] != null)
            {
                _inquiryGrid.Columns["DateSubmitted"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }
        }

        private void SetHeader(string column, string text)
        {
            if (_inquiryGrid.Columns[column] != null)
            {
                _inquiryGrid.Columns[column].HeaderText = text;
            }
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
            var selected = GetSelectedInquiry();
            _editButton.Enabled = selected != null;

            if (selected == null)
            {
                _actionButton.Enabled = false;
                return;
            }

            switch (selected.Status)
            {
                case "Pending":
                    _actionButton.Text = "Start";
                    _actionButton.Enabled = true;
                    break;
                case "In Progress":
                    _actionButton.Text = "Respond";
                    _actionButton.Enabled = true;
                    break;
                case "Answered":
                    _actionButton.Text = "Close";
                    _actionButton.Enabled = true;
                    break;
                default:
                    _actionButton.Text = "Closed";
                    _actionButton.Enabled = false;
                    break;
            }
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

        private async void ActionButton_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedInquiry();
            if (selected == null)
            {
                return;
            }

            try
            {
                if (selected.Status == "Pending")
                {
                    await _apiService.UpdateInquiryStatusAsync(_companyId, selected.InquiryId, "In Progress");
                }
                else if (selected.Status == "In Progress")
                {
                    using var dialog = new InquiryRespondDialog(selected.Subject, selected.Message);
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    await _apiService.RespondToInquiryAsync(_companyId, selected.InquiryId, dialog.ResponseText, "Staff");
                }
                else if (selected.Status == "Answered")
                {
                    await _apiService.UpdateInquiryStatusAsync(_companyId, selected.InquiryId, "Closed");
                }

                await LoadDataAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update inquiry: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }
    }
}