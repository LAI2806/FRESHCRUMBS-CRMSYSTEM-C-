using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class PlanEditForm : Form
    {
        public PlanModel Result { get; private set; } = new();

        private readonly bool _isEditMode;
        private readonly bool _isViewMode;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private TextBox _codeBox = null!;
        private TextBox _nameBox = null!;
        private TextBox _descriptionBox = null!;
        private NumericUpDown _priceBox = null!;
        private ComboBox _billingCycleBox = null!;
        private NumericUpDown _maxUsersBox = null!;
        private NumericUpDown _maxBranchesBox = null!;
        private readonly Dictionary<string, CheckBox> _featureBoxes = new();
        private Label _statusValueLabel = null!;
        private Label _errorLabel = null!;

        public PlanEditForm(PlanModel? existingPlan, bool viewOnly = false)
        {
            _isEditMode = existingPlan != null;
            _isViewMode = viewOnly && existingPlan != null;

            InitializeForm();
            InitializeControls();

            if (existingPlan != null)
            {
                Result = new PlanModel
                {
                    PlanId = existingPlan.PlanId,
                    PlanCode = existingPlan.PlanCode,
                    DisplayName = existingPlan.DisplayName,
                    Description = existingPlan.Description,
                    Price = existingPlan.Price,
                    BillingCycle = existingPlan.BillingCycle,
                    Features = new List<string>(existingPlan.Features),
                    MaxUsers = existingPlan.MaxUsers,
                    BranchingEnabled = existingPlan.BranchingEnabled,
                    MaxBranches = existingPlan.MaxBranches,
                    IsActive = existingPlan.IsActive,
                    CreatedAt = existingPlan.CreatedAt,
                    UpdatedAt = existingPlan.UpdatedAt
                };

                _codeBox.Text = Result.PlanCode;
                _nameBox.Text = Result.DisplayName;
                _descriptionBox.Text = Result.Description;
                _priceBox.Value = Result.Price;
                _billingCycleBox.SelectedItem = Result.BillingCycle;
                _maxUsersBox.Value = Result.MaxUsers;

                foreach (var pair in _featureBoxes)
                {
                    pair.Value.Checked = pair.Key == "Branching"
                        ? Result.BranchingEnabled
                        : Result.Features.Contains(pair.Key);
                }

                if (Result.BranchingEnabled && Result.MaxBranches != null)
                {
                    _maxBranchesBox.Value = Result.MaxBranches.Value;
                }

                _statusValueLabel.Text = Result.StatusText;
            }

            UpdateBranchLimitState();
        }

        private void InitializeForm()
        {
            Text = _isViewMode ? "Plan Details" : _isEditMode ? "Customize Plan" : "Create Plan";
            Width = 520;
            Height = 780;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
        }

        private Label AddLabel(string text, int y)
        {
            var label = new Label
            {
                Text = text,
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = LabelGray,
                Font = new Font("Segoe UI", 9.5f)
            };
            Controls.Add(label);
            return label;
        }

        private void InitializeControls()
        {
            Controls.Add(new Label
            {
                Text = Text,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(30, 20),
                AutoSize = true
            });

            int y = 70;

            AddLabel(_isEditMode ? "Plan Code" : "Plan Code (generated automatically)", y);
            _codeBox = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                MaxLength = 30,
                ReadOnly = true,
                TabStop = false,
                BorderStyle = BorderStyle.FixedSingle
            };

            if (!_isEditMode)
            {
                _codeBox.Text = "Generated when the plan is saved";
                _codeBox.ForeColor = LabelGray;
            }

            Controls.Add(_codeBox);

            y += 62;
            AddLabel("Display Name *", y);
            _nameBox = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                MaxLength = 100,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_nameBox);

            y += 62;
            AddLabel("Description", y);
            _descriptionBox = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                Height = 60,
                MaxLength = 500,
                Multiline = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_descriptionBox);

            y += 100;
            AddLabel("Price *", y);
            _priceBox = new NumericUpDown
            {
                Location = new Point(30, y + 22),
                Width = 200,
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 99999999.99m,
                ThousandsSeparator = true
            };
            Controls.Add(_priceBox);

            AddLabel("Billing Cycle *", y).Left = 270;
            _billingCycleBox = new ComboBox
            {
                Location = new Point(270, y + 22),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _billingCycleBox.Items.AddRange(new object[] { "Weekly", "Monthly", "Yearly" });
            _billingCycleBox.SelectedItem = "Monthly";
            Controls.Add(_billingCycleBox);

            y += 62;
            AddLabel("Included Features *", y);
            var featurePanel = new Panel
            {
                Location = new Point(30, y + 22),
                Size = new Size(440, 130),
                BorderStyle = BorderStyle.FixedSingle
            };

            int featureY = 8;
            foreach (var feature in PlanFeatureCatalog.All)
            {
                var checkBox = new CheckBox
                {
                    Text = feature.DisplayName,
                    Location = new Point(12, featureY),
                    AutoSize = true
                };

                if (feature.Key == "Branching")
                {
                    checkBox.CheckedChanged += (s, e) => UpdateBranchLimitState();
                }

                _featureBoxes[feature.Key] = checkBox;
                featurePanel.Controls.Add(checkBox);
                featureY += 23;
            }
            Controls.Add(featurePanel);

            y += 164;
            AddLabel("Maximum Users *", y);
            _maxUsersBox = new NumericUpDown
            {
                Location = new Point(30, y + 22),
                Width = 200,
                Minimum = 1,
                Maximum = 100000,
                Value = 1
            };
            Controls.Add(_maxUsersBox);

            AddLabel("Maximum Branches", y).Left = 270;
            _maxBranchesBox = new NumericUpDown
            {
                Location = new Point(270, y + 22),
                Width = 200,
                Minimum = 1,
                Maximum = 1000,
                Value = 1
            };
            Controls.Add(_maxBranchesBox);

            y += 62;
            AddLabel("Status", y);
            _statusValueLabel = new Label
            {
                Text = "Active",
                Location = new Point(30, y + 22),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TextDark
            };
            Controls.Add(_statusValueLabel);

            y += 55;
            _errorLabel = new Label
            {
                Location = new Point(30, y),
                Size = new Size(440, 40),
                ForeColor = Color.Firebrick
            };
            Controls.Add(_errorLabel);

            y += 45;

            if (_isViewMode)
            {
                _codeBox.ReadOnly = true;
                _nameBox.ReadOnly = true;
                _descriptionBox.ReadOnly = true;
                _priceBox.Enabled = false;
                _billingCycleBox.Enabled = false;
                _maxUsersBox.Enabled = false;

                foreach (var box in _featureBoxes.Values)
                {
                    box.Enabled = false;
                }

                AddButton("Close", Color.White, LabelGray, 30, y, true).Click += (s, e) => Close();
                return;
            }

            var saveButton = AddButton("Save", AccentColor, Color.White, 30, y, false);
            saveButton.Click += SaveButton_Click;

            var cancelButton = AddButton("Cancel", Color.White, LabelGray, 150, y, true);
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
        }

        private Button AddButton(string text, Color back, Color fore, int x, int y, bool bordered)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = bordered ? 1 : 0;
            button.FlatAppearance.BorderColor = BorderColor;
            Controls.Add(button);
            return button;
        }

        private void UpdateBranchLimitState()
        {
            bool branching = _featureBoxes.TryGetValue("Branching", out var box) && box.Checked;
            _maxBranchesBox.Enabled = branching && !_isViewMode;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            if (!ValidationHelper.IsRequired(_nameBox.Text))
            {
                _errorLabel.Text = "Display name is required.";
                return;
            }

            if (!ValidationHelper.IsValidSelection(_billingCycleBox))
            {
                _errorLabel.Text = "Please select a billing cycle.";
                return;
            }

            if (!_featureBoxes.Values.Any(x => x.Checked))
            {
                _errorLabel.Text = "Select at least one included feature.";
                return;
            }

            if (!ValidationHelper.IsPositive((int)_maxUsersBox.Value))
            {
                _errorLabel.Text = "Maximum users must be at least 1.";
                return;
            }

            bool branching = _featureBoxes["Branching"].Checked;

            if (branching && !ValidationHelper.IsPositive((int)_maxBranchesBox.Value))
            {
                _errorLabel.Text = "Maximum branches must be at least 1 when branching is enabled.";
                return;
            }

            Result.DisplayName = _nameBox.Text.Trim();
            Result.Description = _descriptionBox.Text.Trim();
            Result.Price = _priceBox.Value;
            Result.BillingCycle = _billingCycleBox.SelectedItem!.ToString()!;
            Result.MaxUsers = (int)_maxUsersBox.Value;
            Result.BranchingEnabled = branching;
            Result.MaxBranches = branching ? (int)_maxBranchesBox.Value : null;
            Result.Features = _featureBoxes
                .Where(x => x.Value.Checked)
                .Select(x => x.Key)
                .ToList();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}