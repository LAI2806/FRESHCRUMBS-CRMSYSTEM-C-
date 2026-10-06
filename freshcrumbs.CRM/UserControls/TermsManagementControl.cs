using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class TermsManagementControl : UserControl
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
        private Button _newButton = null!;
        private Button _viewButton = null!;
        private Button _editButton = null!;
        private Button _publishButton = null!;
        private Button _deleteButton = null!;
        private Label _statusLabel = null!;
        private TableLayoutPanel _root = null!;

        private List<TermsVersionModel> _versions = new();
        private int? _selectedId;

        public TermsManagementControl()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            InitializeLayout();

            Load += async (s, e) => await LoadAsync();
        }

        private void InitializeLayout()
        {
            _root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = PageBg
            };
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55f));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            _root.Controls.Add(new Label
            {
                Text = "Terms & Conditions",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var toolbar = new Panel { Dock = DockStyle.Fill };

            _searchBox = new TextBox
            {
                Location = new Point(0, 10),
                Width = 230,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Search by title or version..."
            };
            _searchBox.TextChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_searchBox);

            toolbar.Controls.Add(new Label
            {
                Text = "Status:",
                Location = new Point(246, 14),
                AutoSize = true,
                ForeColor = LabelGray
            });

            _statusFilterBox = new ComboBox
            {
                Location = new Point(294, 10),
                Width = 110,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[] { FilterAll, "Draft", "Published", "Archived" });
            _statusFilterBox.SelectedItem = FilterAll;
            _statusFilterBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_statusFilterBox);

            _newButton = CreateButton("+  New Draft", AccentColor, Color.White, 120, false);
            _newButton.Click += NewButton_Click;

            _viewButton = CreateButton("View", Color.White, LabelGray, 70, true);
            _viewButton.Click += (s, e) => OpenSelected(TermsFormMode.View);

            _editButton = CreateButton("Edit Draft", Color.White, LabelGray, 95, true);
            _editButton.Click += (s, e) => OpenSelected(TermsFormMode.Edit);

            _publishButton = CreateButton("Publish", Color.White, AccentColor, 85, true);
            _publishButton.Click += PublishButton_Click;

            _deleteButton = CreateButton("Delete Draft", Color.White, Color.Firebrick, 105, true);
            _deleteButton.Click += DeleteButton_Click;

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(_newButton);
            buttons.Controls.Add(_deleteButton);
            buttons.Controls.Add(_publishButton);
            buttons.Controls.Add(_editButton);
            buttons.Controls.Add(_viewButton);
            toolbar.Controls.Add(buttons);

            _root.Controls.Add(toolbar, 0, 1);

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

            AddColumn("VersionText", "Version", 8);
            AddColumn("Title", "Title", 36);
            AddColumn("Status", "Status", 12);
            AddColumn("CreatedText", "Created", 14);
            AddColumn("PublishedText", "Published", 14);
            AddColumn("AcceptanceText", "Companies Accepted", 16);

            _grid.SelectionChanged += (s, e) =>
            {
                _selectedId = _grid.SelectedRows.Count > 0
                    ? (_grid.SelectedRows[0].DataBoundItem as TermsVersionModel)?.TermsVersionId
                    : null;
                UpdateButtonStates();
            };
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    OpenSelected(TermsFormMode.View);
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

            _root.Controls.Add(_grid, 0, 2);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _root.Controls.Add(_statusLabel, 0, 3);

            Controls.Add(_root);
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
            button.FlatAppearance.BorderColor = fore == LabelGray || fore == Color.White ? BorderColor : fore;
            return button;
        }

        private async Task LoadAsync()
        {
            AdminUi.ShowInfo(_statusLabel, "Loading terms...");

            try
            {
                _versions = await _apiService.GetTermsVersionsAsync();
                ApplyFilter();
                _statusLabel.Text = "";
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

            var rows = _versions
                .Where(v => status == FilterAll || v.Status == status)
                .Where(v => search.Length == 0
                    || v.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || v.VersionText.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            int? keep = _selectedId;

            _grid.DataSource = rows;
            _grid.ClearSelection();
            _selectedId = null;

            if (keep != null)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    if ((row.DataBoundItem as TermsVersionModel)?.TermsVersionId == keep)
                    {
                        row.Selected = true;
                        _grid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            _emptyLabel.Text = _versions.Count == 0
                ? "No terms versions yet. Click \"+ New Draft\" to write the first one."
                : "No versions match your search or filter.";
            _emptyLabel.Visible = rows.Count == 0;
            _emptyLabel.Width = _grid.ClientSize.Width;

            UpdateButtonStates();
        }

        private TermsVersionModel? Selected()
        {
            return _selectedId == null ? null : _versions.FirstOrDefault(v => v.TermsVersionId == _selectedId);
        }

        private void UpdateButtonStates()
        {
            var selected = Selected();
            bool hasSelection = selected != null;
            bool isDraft = selected?.Status == "Draft";

            _viewButton.Enabled = hasSelection;
            _editButton.Enabled = isDraft;
            _publishButton.Enabled = isDraft;
            _deleteButton.Enabled = isDraft;
        }

        private async void NewButton_Click(object? sender, EventArgs e)
        {
            using var form = new TermsEditForm(TermsFormMode.Create, null);

            if (form.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAsync();
                AdminUi.ShowSuccess(_statusLabel, "Draft created.");
            }
        }

        private async void OpenSelected(TermsFormMode mode)
        {
            var selected = Selected();

            if (selected == null)
            {
                return;
            }

            using var form = new TermsEditForm(mode, selected.TermsVersionId);

            if (form.ShowDialog(FindForm()) == DialogResult.OK)
            {
                await LoadAsync();
                AdminUi.ShowSuccess(_statusLabel, "Draft saved.");
            }
        }

        private async void PublishButton_Click(object? sender, EventArgs e)
        {
            var selected = Selected();

            if (selected == null || selected.Status != "Draft")
            {
                return;
            }

            var current = _versions.FirstOrDefault(v => v.Status == "Published");

            using var dialog = new TermsPublishForm(selected, current);

            if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.PublishTermsAsync(selected.TermsVersionId, dialog.RequiresAcceptance);
                await LoadAsync();
                AdminUi.ShowSuccess(_statusLabel, $"{selected.VersionText} is now published.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(AdminUi.GetMessage(ex), "Publish Terms", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void DeleteButton_Click(object? sender, EventArgs e)
        {
            var selected = Selected();

            if (selected == null || selected.Status != "Draft")
            {
                return;
            }

            if (MessageBox.Show(
                    $"Delete draft {selected.VersionText} \"{selected.Title}\"? This cannot be undone.",
                    "Delete Draft", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.DeleteTermsDraftAsync(selected.TermsVersionId);
                _selectedId = null;
                await LoadAsync();
                AdminUi.ShowSuccess(_statusLabel, "Draft deleted.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(AdminUi.GetMessage(ex), "Delete Draft", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}