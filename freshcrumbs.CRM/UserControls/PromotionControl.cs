using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class PromotionControl : UserControl
    {
        private readonly ApiService _apiService;
        private readonly int _companyId;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private const string FilterAll = "All";
        private const string FilterActive = "Active";
        private const string FilterInactive = "Inactive";

        private DataGridView _promotionGrid = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterBox = null!;
        private Button _addButton = null!;
        private Button _editButton = null!;
        private Button _deleteButton = null!;
        private Button _reactivateButton = null!;
        private Label _statusLabel = null!;

        private List<PromotionModel> _promotions = new();

        public PromotionControl(int companyId)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();

            Load += PromotionControl_Load;
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
                Text = "Promotion Management",
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
                Width = 280,
                Height = 34,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by promotion name..."
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            toolbarPanel.Controls.Add(_searchBox);

            var statusFilterLabel = new Label
            {
                Text = "Status:",
                Location = new Point(296, 14),
                AutoSize = true,
                ForeColor = LabelGray,
                Font = new Font("Segoe UI", 9.5f)
            };
            toolbarPanel.Controls.Add(statusFilterLabel);

            _statusFilterBox = new ComboBox
            {
                Location = new Point(344, 10),
                Width = 120,
                Font = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[] { FilterAll, FilterActive, FilterInactive });
            _statusFilterBox.SelectedItem = FilterActive;
            _statusFilterBox.SelectedIndexChanged += async (s, e) => await LoadPromotionsAsync();
            toolbarPanel.Controls.Add(_statusFilterBox);

            _addButton = CreateActionButton("+  Add Promotion", AccentColor, Color.White, 165);
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

            _reactivateButton = CreateActionButton("Reactivate", Color.White, AccentColor, 110);
            _reactivateButton.FlatAppearance.BorderSize = 1;
            _reactivateButton.FlatAppearance.BorderColor = AccentColor;
            _reactivateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _reactivateButton.Enabled = false;
            _reactivateButton.Visible = false;
            _reactivateButton.Click += ReactivateButton_Click;
            toolbarPanel.Controls.Add(_reactivateButton);

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

            _promotionGrid = new DataGridView
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

            _promotionGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _promotionGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _promotionGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _promotionGrid.ColumnHeadersHeight = 42;
            _promotionGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _promotionGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _promotionGrid.DefaultCellStyle.SelectionForeColor = TextDark;
            _promotionGrid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            _promotionGrid.SelectionChanged += PromotionGrid_SelectionChanged;

            gridContainer.Controls.Add(_promotionGrid);
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
            _reactivateButton.Location = new Point(_editButton.Left - _reactivateButton.Width - 10, 8);
            _deleteButton.Location = new Point(_reactivateButton.Left - _deleteButton.Width - 10, 8);
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

        private async void PromotionControl_Load(object? sender, EventArgs e)
        {
            await LoadPromotionsAsync();
        }

        private async Task LoadPromotionsAsync()
        {
            try
            {
                _statusLabel.Text = "";

                // "Active" only needs the active rows; "All" and "Inactive" need
                // the inactive (soft-deleted) rows too. Nothing is ever removed
                // from the database, so they can always be listed again.
                bool includeInactive = GetSelectedStatusFilter() != FilterActive;

                _promotions = await _apiService.GetPromotionsAsync(_companyId, includeInactive);
                ApplyFilters();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load promotions: {ex.Message}";
            }
        }

        private string GetSelectedStatusFilter()
        {
            return _statusFilterBox.SelectedItem?.ToString() ?? FilterActive;
        }

        private void ApplyFilters()
        {
            IEnumerable<PromotionModel> filtered = _promotions;

            string statusFilter = GetSelectedStatusFilter();
            if (statusFilter != FilterAll)
            {
                filtered = filtered.Where(p =>
                    string.Equals(p.Status, statusFilter, StringComparison.OrdinalIgnoreCase));
            }

            string term = _searchBox.Text.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(term))
            {
                filtered = filtered.Where(p => p.PromotionName.ToLowerInvariant().Contains(term));
            }

            BindGrid(filtered.ToList());
        }

        private void BindGrid(List<PromotionModel> promotions)
        {
            _promotionGrid.AutoGenerateColumns = true;
            _promotionGrid.DataSource = null;
            _promotionGrid.DataSource = promotions;

            if (_promotionGrid.Columns["PromotionId"] != null)
            {
                _promotionGrid.Columns["PromotionId"].Visible = false;
            }

            SetColumnHeader("PromotionName", "Promotion Name");
            SetColumnHeader("Description", "Description");
            SetColumnHeader("DiscountType", "Discount Type");
            SetColumnHeader("DiscountValue", "Value");
            SetColumnHeader("MinimumPurchase", "Min. Purchase");
            SetColumnHeader("RequiredLoyaltyPoints", "Points Required");
            SetColumnHeader("StartDate", "Start Date");
            SetColumnHeader("EndDate", "End Date");
            SetColumnHeader("Status", "Status");

            if (_promotionGrid.Columns["StartDate"] != null)
            {
                _promotionGrid.Columns["StartDate"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }
            if (_promotionGrid.Columns["EndDate"] != null)
            {
                _promotionGrid.Columns["EndDate"].DefaultCellStyle.Format = "MM/dd/yyyy";
            }
        }

        private void SetColumnHeader(string columnName, string headerText)
        {
            if (_promotionGrid.Columns[columnName] != null)
            {
                _promotionGrid.Columns[columnName].HeaderText = headerText;
            }
        }

        private void SearchBox_TextChanged(object? sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void PromotionGrid_SelectionChanged(object? sender, EventArgs e)
        {
            bool hasSelection = _promotionGrid.SelectedRows.Count > 0;
            bool isInactive = hasSelection && GetSelectedStatus() == "Inactive";

            _editButton.Enabled = hasSelection && !isInactive;
            _deleteButton.Enabled = hasSelection && !isInactive;
            _reactivateButton.Visible = isInactive;
            _reactivateButton.Enabled = isInactive;
        }

        private string? GetSelectedStatus()
        {
            if (_promotionGrid.SelectedRows.Count == 0)
            {
                return null;
            }

            dynamic row = _promotionGrid.SelectedRows[0].DataBoundItem;
            return row.Status;
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new PromotionEditForm(null);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreatePromotionAsync(_companyId, form.Result);
                await LoadPromotionsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to create promotion: {ex.Message}";
            }
        }

        private async void EditButton_Click(object? sender, EventArgs e)
        {
            if (_promotionGrid.SelectedRows.Count == 0 ||
                _promotionGrid.SelectedRows[0].DataBoundItem is not PromotionModel selectedPromotion)
            {
                return;
            }

            using var form = new PromotionEditForm(selectedPromotion);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdatePromotionAsync(_companyId, selectedPromotion.PromotionId, form.Result);
                await LoadPromotionsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to update promotion: {ex.Message}";
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (_promotionGrid.SelectedRows.Count == 0 ||
                _promotionGrid.SelectedRows[0].DataBoundItem is not PromotionModel selectedPromotion)
            {
                return;
            }

            var confirm = MessageBox.Show(
                $"Are you sure you want to deactivate \"{selectedPromotion.PromotionName}\"? Past sales that used it will be unaffected.",
                "Confirm Deactivate",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeletePromotionAsync(_companyId, selectedPromotion.PromotionId);
                await LoadPromotionsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to deactivate promotion: {ex.Message}";
            }
        }

        private async void ReactivateButton_Click(object? sender, EventArgs e)
        {
            if (_promotionGrid.SelectedRows.Count == 0 ||
                _promotionGrid.SelectedRows[0].DataBoundItem is not PromotionModel selectedPromotion)
            {
                return;
            }

            try
            {
                await _apiService.ReactivatePromotionAsync(_companyId, selectedPromotion.PromotionId);
                await LoadPromotionsAsync();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to reactivate promotion: {ex.Message}";
            }
        }
    }
}