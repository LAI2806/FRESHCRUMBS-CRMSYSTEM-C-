using freshcrumbs.CRM.winforms.Forms;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class PlanManagementControl : UserControl
    {
        private readonly ApiService _apiService;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);

        private const string FilterAll = "All";
        private const string FilterActive = "Active";
        private const string FilterInactive = "Inactive";

        private const int CardColumns = 3;
        private const int CardRowHeight = 540;

        private Panel _scrollPanel = null!;
        private TableLayoutPanel _cardTable = null!;
        private Label _emptyLabel = null!;
        private TextBox _searchBox = null!;
        private ComboBox _statusFilterBox = null!;
        private Button _addButton = null!;
        private Button _viewButton = null!;
        private Button _toggleButton = null!;
        private Label _statusLabel = null!;
        private readonly ToolTip _hint = new();

        private List<PlanModel> _plans = new();
        private List<PlanModel> _filteredPlans = new();
        private int? _selectedPlanId;

        public PlanManagementControl()
        {
            _apiService = new ApiService();

            InitializeLayout();

            Load += async (s, e) => await LoadPlansAsync();
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
                Text = "Plan Management",
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
                PlaceholderText = "Search by plan code or name..."
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
                Width = 120,
                Font = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            _statusFilterBox.Items.AddRange(new object[] { FilterAll, FilterActive, FilterInactive });
            _statusFilterBox.SelectedItem = FilterAll;
            _statusFilterBox.SelectedIndexChanged += (s, e) => ApplyFilter();
            toolbar.Controls.Add(_statusFilterBox);

            _addButton = CreateActionButton("+  Create Plan", AccentColor, Color.White, 150, false);
            _addButton.Click += AddButton_Click;

            _viewButton = CreateActionButton("View", Color.White, LabelGray, 90, true);
            _viewButton.Click += ViewButton_Click;

            _toggleButton = CreateActionButton("Deactivate", Color.White, Color.Firebrick, 120, true);
            _toggleButton.Click += ToggleButton_Click;

            _hint.SetToolTip(_viewButton, "Click a plan card to select it first.");
            _hint.SetToolTip(_toggleButton, "Click a plan card to select it first.");

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };
            buttons.Controls.Add(_addButton);
            buttons.Controls.Add(_toggleButton);
            buttons.Controls.Add(_viewButton);
            toolbar.Controls.Add(buttons);

            rootLayout.Controls.Add(toolbar, 0, 1);

            _scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PageBg
            };

            _cardTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = CardColumns,
                RowCount = 1,
                MinimumSize = new Size(870, 0),
                BackColor = PageBg,
                Margin = new Padding(0)
            };
            for (int i = 0; i < CardColumns; i++)
            {
                _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / CardColumns));
            }

            _emptyLabel = new Label
            {
                Text = "No plans found.",
                Dock = DockStyle.Top,
                Height = 60,
                Font = new Font("Segoe UI", 11),
                ForeColor = LabelGray,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            _scrollPanel.Controls.Add(_cardTable);
            _scrollPanel.Controls.Add(_emptyLabel);

            rootLayout.Controls.Add(_scrollPanel, 0, 2);

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
            button.FlatAppearance.BorderColor = fore == Color.White ? BorderColor : fore;
            if (bordered && fore == LabelGray)
            {
                button.FlatAppearance.BorderColor = BorderColor;
            }
            return button;
        }

        private async Task LoadPlansAsync()
        {
            AdminUi.ShowInfo(_statusLabel, "Loading plans...");

            try
            {
                _plans = await _apiService.GetPlansAsync();
                ApplyFilter();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_statusLabel, ex);
            }
        }

        private static decimal MonthlyPrice(PlanModel plan) => plan.BillingCycle switch
        {
            "Weekly" => plan.Price * 52m / 12m,
            "Yearly" => plan.Price / 12m,
            _ => plan.Price
        };

        private void ApplyFilter()
        {
            string search = _searchBox.Text.Trim();
            string status = _statusFilterBox.SelectedItem?.ToString() ?? FilterAll;

            _filteredPlans = _plans
                .Where(p => status == FilterAll || p.StatusText == status)
                .Where(p => search.Length == 0
                    || p.PlanCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || p.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(MonthlyPrice)
                .ThenBy(p => p.DisplayName)
                .ToList();

            if (_selectedPlanId != null && !_filteredPlans.Any(p => p.PlanId == _selectedPlanId))
            {
                _selectedPlanId = null;
            }

            RenderCards();
            UpdateButtonStates();
        }

        private void RenderCards()
        {
            // Tier colours follow the price rank across ALL plans so they stay stable while filtering.
            var tierRank = _plans
                .OrderBy(MonthlyPrice)
                .ThenBy(p => p.PlanCode)
                .Select((plan, index) => new { plan.PlanId, index })
                .ToDictionary(x => x.PlanId, x => x.index);

            _cardTable.SuspendLayout();

            foreach (Control old in _cardTable.Controls.Cast<Control>().ToList())
            {
                old.Dispose();
            }

            _cardTable.Controls.Clear();
            _cardTable.RowStyles.Clear();

            int rowCount = (int)Math.Ceiling(_filteredPlans.Count / (double)CardColumns);
            _cardTable.RowCount = Math.Max(rowCount, 1);

            for (int r = 0; r < rowCount; r++)
            {
                _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, CardRowHeight));
            }

            _cardTable.Height = rowCount * CardRowHeight;

            for (int i = 0; i < _filteredPlans.Count; i++)
            {
                var plan = _filteredPlans[i];
                var tierColor = PlanCardControl.TierColors[tierRank[plan.PlanId] % PlanCardControl.TierColors.Length];

                var card = new PlanCardControl(plan, tierColor);
                card.IsSelected = plan.PlanId == _selectedPlanId;
                card.CardClicked += (s, e) => SelectPlan(plan.PlanId);
                card.CardDoubleClicked += (s, e) =>
                {
                    SelectPlan(plan.PlanId);
                    ViewButton_Click(s, EventArgs.Empty);
                };
                card.CustomizeClicked += (s, e) =>
                {
                    SelectPlan(plan.PlanId);
                    CustomizePlan(plan);
                };

                _cardTable.Controls.Add(card, i % CardColumns, i / CardColumns);
            }

            _cardTable.ResumeLayout();
            _emptyLabel.Text = _plans.Count == 0
                ? "No plans yet. Click \"+ Create Plan\" to add your first plan."
                : "No plans match your search or filter.";
            _emptyLabel.Visible = _filteredPlans.Count == 0;
        }

        private void SelectPlan(int planId)
        {
            _selectedPlanId = planId;

            foreach (var card in _cardTable.Controls.OfType<PlanCardControl>())
            {
                card.IsSelected = card.Plan.PlanId == planId;
            }

            UpdateButtonStates();
        }

        private PlanModel? SelectedPlan()
        {
            return _selectedPlanId == null
                ? null
                : _plans.FirstOrDefault(p => p.PlanId == _selectedPlanId);
        }

        private void UpdateButtonStates()
        {
            var plan = SelectedPlan();
            bool hasSelection = plan != null;

            _viewButton.Enabled = hasSelection;
            _toggleButton.Enabled = hasSelection;

            if (plan != null && !plan.IsActive)
            {
                _toggleButton.Text = "Activate";
                _toggleButton.ForeColor = AccentColor;
                _toggleButton.FlatAppearance.BorderColor = AccentColor;
            }
            else
            {
                _toggleButton.Text = "Deactivate";
                _toggleButton.ForeColor = Color.Firebrick;
                _toggleButton.FlatAppearance.BorderColor = Color.Firebrick;
            }
        }

        private async void AddButton_Click(object? sender, EventArgs e)
        {
            using var form = new PlanEditForm(null);

            if (form.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.CreatePlanAsync(form.Result);
                await LoadPlansAsync();
                AdminUi.ShowSuccess(_statusLabel, $"Plan '{form.Result.DisplayName}' was created.");
            }
            catch (ApiValidationException ex)
            {
                MessageBox.Show(ex.Message, "Create Plan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(AdminUi.GetMessage(ex), "Create Plan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ViewButton_Click(object? sender, EventArgs e)
        {
            var plan = SelectedPlan();

            if (plan == null)
            {
                return;
            }

            using var form = new PlanEditForm(plan, viewOnly: true);
            form.ShowDialog(FindForm());
        }

        private async void CustomizePlan(PlanModel plan)
        {
            using var form = new PlanEditForm(plan);

            if (form.ShowDialog(FindForm()) != DialogResult.OK)
            {
                return;
            }

            try
            {
                await _apiService.UpdatePlanAsync(plan.PlanId, form.Result);
                await LoadPlansAsync();
                AdminUi.ShowSuccess(_statusLabel, $"Plan '{form.Result.DisplayName}' was updated. Existing subscriptions keep their original terms.");
            }
            catch (ApiValidationException ex)
            {
                MessageBox.Show(ex.Message, "Customize Plan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(AdminUi.GetMessage(ex), "Customize Plan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ToggleButton_Click(object? sender, EventArgs e)
        {
            var plan = SelectedPlan();

            if (plan == null)
            {
                return;
            }

            bool activate = !plan.IsActive;
            string message = activate
                ? $"Activate plan '{plan.DisplayName}'? It can then be assigned to new subscribers."
                : $"Deactivate plan '{plan.DisplayName}'? It can no longer be assigned to new subscribers. Existing subscriptions are not affected.";

            if (MessageBox.Show(message, activate ? "Activate Plan" : "Deactivate Plan",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.SetPlanActiveAsync(plan.PlanId, activate);
                await LoadPlansAsync();
                AdminUi.ShowSuccess(_statusLabel, $"Plan '{plan.DisplayName}' was {(activate ? "activated" : "deactivated")}.");
            }
            catch (ApiValidationException ex)
            {
                MessageBox.Show(ex.Message, "Plan Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(AdminUi.GetMessage(ex), "Plan Status", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}