using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class PromotionEditForm : Form
    {
        public PromotionModel Result { get; private set; } = new();

        private readonly bool _isEditMode;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;

        private TextBox _nameBox = null!;
        private TextBox _descriptionBox = null!;
        private ComboBox _discountTypeBox = null!;
        private NumericUpDown _discountValueBox = null!;
        private NumericUpDown _minimumPurchaseBox = null!;
        private NumericUpDown _requiredPointsBox = null!;
        private DateTimePicker _startDatePicker = null!;
        private DateTimePicker _endDatePicker = null!;
        private ComboBox _statusBox = null!;
        private Label _errorLabel = null!;

        public PromotionEditForm(PromotionModel? existingPromotion)
        {
            _isEditMode = existingPromotion != null;

            InitializeForm();
            InitializeControls();

            if (existingPromotion != null)
            {
                Result = new PromotionModel
                {
                    PromotionId = existingPromotion.PromotionId,
                    PromotionName = existingPromotion.PromotionName,
                    Description = existingPromotion.Description,
                    DiscountType = existingPromotion.DiscountType,
                    DiscountValue = existingPromotion.DiscountValue,
                    MinimumPurchase = existingPromotion.MinimumPurchase,
                    RequiredLoyaltyPoints = existingPromotion.RequiredLoyaltyPoints,
                    StartDate = existingPromotion.StartDate,
                    EndDate = existingPromotion.EndDate,
                    Status = existingPromotion.Status
                };

                _nameBox.Text = Result.PromotionName;
                _descriptionBox.Text = Result.Description;
                _discountTypeBox.Text = Result.DiscountType;
                _discountValueBox.Value = Result.DiscountValue;
                _minimumPurchaseBox.Value = Result.MinimumPurchase;
                _requiredPointsBox.Value = Result.RequiredLoyaltyPoints;
                _startDatePicker.Value = Result.StartDate;
                _endDatePicker.Value = Result.EndDate;
                _statusBox.Text = Result.Status;
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Promotion" : "Add Promotion";
            Width = 460;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(30, 25, 30, 25);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            KeyPreview = true;
        }

        private void InitializeControls()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var titleLabel = new Label
            {
                Text = _isEditMode ? "Edit Promotion" : "Add New Promotion",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 18)
            };
            root.Controls.Add(titleLabel);

            _nameBox = AddField(root, "Promotion Name");
            _descriptionBox = AddField(root, "Description");

            var discountTypeLabel = new Label
            {
                Text = "DISCOUNT TYPE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(discountTypeLabel);

            _discountTypeBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _discountTypeBox.Items.AddRange(new object[] { "Percentage", "Fixed Amount" });
            _discountTypeBox.SelectedIndex = 0;
            root.Controls.Add(_discountTypeBox);

            var discountValueLabel = new Label
            {
                Text = "DISCOUNT VALUE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(discountValueLabel);

            _discountValueBox = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_discountValueBox);

            var minimumPurchaseLabel = new Label
            {
                Text = "MINIMUM PURCHASE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(minimumPurchaseLabel);

            _minimumPurchaseBox = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_minimumPurchaseBox);

            var requiredPointsLabel = new Label
            {
                Text = "REQUIRED LOYALTY POINTS (0 = NOT POINTS-BASED)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(requiredPointsLabel);

            _requiredPointsBox = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_requiredPointsBox);

            var startDateLabel = new Label
            {
                Text = "START DATE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(startDateLabel);

            _startDatePicker = new DateTimePicker
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Format = DateTimePickerFormat.Short,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_startDatePicker);

            var endDateLabel = new Label
            {
                Text = "END DATE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(endDateLabel);

            _endDatePicker = new DateTimePicker
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Format = DateTimePickerFormat.Short,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_endDatePicker);

            var statusLabel = new Label
            {
                Text = "STATUS",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            _statusBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _statusBox.Items.AddRange(new object[] { "Active", "Inactive" });
            _statusBox.SelectedIndex = 0;

            // Status is only editable when editing an existing promotion.
            // New promotions are always created as Active.
            if (_isEditMode)
            {
                root.Controls.Add(statusLabel);
                root.Controls.Add(_statusBox);
            }

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                MaximumSize = new Size(380, 0),
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_errorLabel);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 120,
                Height = 42,
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.BorderColor = BorderColor;
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var saveButton = new Button
            {
                Text = _isEditMode ? "Save Changes" : "Add Promotion",
                Width = 160,
                Height = 42,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;

            int groupWidth = cancelButton.Width + 10 + saveButton.Width;
            int leftMargin = (380 - groupWidth) / 2;

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(leftMargin, 6, 0, 0)
            };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(saveButton);

            root.Controls.Add(buttonPanel);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            Controls.Add(root);
        }

        private TextBox AddField(TableLayoutPanel root, string labelText)
        {
            var label = new Label
            {
                Text = labelText.ToUpperInvariant(),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(label);

            var textBox = new TextBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(textBox);

            return textBox;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            // Full input validation happens here. If anything fails we stop:
            // the dialog stays open, DialogResult is never set to OK, so the
            // caller never reaches the API and nothing is written to the database.
            if (!ValidateInput())
            {
                return;
            }

            Result.PromotionName = _nameBox.Text.Trim();
            Result.Description = _descriptionBox.Text.Trim();
            Result.DiscountType = _discountTypeBox.Text;
            Result.DiscountValue = _discountValueBox.Value;
            Result.MinimumPurchase = _minimumPurchaseBox.Value;
            Result.RequiredLoyaltyPoints = (int)_requiredPointsBox.Value;
            Result.StartDate = _startDatePicker.Value.Date;
            Result.EndDate = _endDatePicker.Value.Date;

            // New promotions are always Active. Status is only user-editable
            // in edit mode, where it drives soft-delete / reactivation.
            Result.Status = _isEditMode ? _statusBox.Text : "Active";

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ValidateInput()
        {
            if (!ValidationHelper.IsRequired(_nameBox.Text))
            {
                return Fail("Promotion Name is required.", _nameBox);
            }

            if (!ValidationHelper.IsValidSelection(_discountTypeBox) ||
                !ValidationHelper.IsRequired(_discountTypeBox.Text))
            {
                return Fail("Discount Type is required.", _discountTypeBox);
            }

            if (!ValidationHelper.IsPositive(_discountValueBox.Value))
            {
                return Fail("Discount Value must be greater than 0.", _discountValueBox);
            }

            if (_discountTypeBox.Text == "Percentage" && _discountValueBox.Value > 100)
            {
                return Fail("Percentage discount must be greater than 0 and cannot exceed 100.", _discountValueBox);
            }

            if (!ValidationHelper.IsNonNegative(_minimumPurchaseBox.Value))
            {
                return Fail("Minimum Purchase must be 0 or greater.", _minimumPurchaseBox);
            }

            if (!ValidationHelper.IsNonNegative((int)_requiredPointsBox.Value))
            {
                return Fail("Required Loyalty Points must be 0 or greater. Use 0 if the promotion is not points-based.", _requiredPointsBox);
            }

            if (_startDatePicker.Value == default)
            {
                return Fail("Start Date is required.", _startDatePicker);
            }

            if (_endDatePicker.Value == default)
            {
                return Fail("End Date is required.", _endDatePicker);
            }

            if (!ValidationHelper.IsValidDateRange(_startDatePicker.Value.Date, _endDatePicker.Value.Date))
            {
                return Fail("End Date cannot be earlier than Start Date.", _endDatePicker);
            }

            if (_isEditMode && !ValidationHelper.IsValidSelection(_statusBox))
            {
                return Fail("Status is required.", _statusBox);
            }

            return true;
        }

        private bool Fail(string message, Control controlToFocus)
        {
            _errorLabel.Text = message;
            controlToFocus.Focus();
            return false;
        }
    }
}