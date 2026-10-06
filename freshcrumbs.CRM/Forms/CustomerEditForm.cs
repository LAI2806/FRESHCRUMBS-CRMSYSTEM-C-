using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using System.Collections.Generic;
using System.Linq;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class CustomerEditForm : Form
    {
        public CustomerModel Result { get; private set; } = new();

        private readonly bool _isEditMode;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;
        private static readonly Color InvalidFieldColor = Color.FromArgb(255, 232, 232);
        private static readonly Color ValidFieldColor = Color.White;

        private TextBox _codeBox = null!;
        private TextBox _firstNameBox = null!;
        private TextBox _lastNameBox = null!;
        private TextBox _emailBox = null!;
        private TextBox _contactBox = null!;
        private TextBox _addressBox = null!;
        private NumericUpDown _loyaltyPointsBox = null!;
        private ComboBox _statusBox = null!;
        private Label _errorLabel = null!;

        private static readonly string[] EligibilityCategories =
            { "Senior Citizen", "PWD", "Other Eligible Category" };
        private static readonly string[] VerificationStatusOptions =
            { "Pending Verification", "Verified", "Rejected" };

        private sealed class EligibilityRow
        {
            public string Category { get; init; } = string.Empty;
            public CheckBox Check { get; init; } = null!;
            public TextBox IdBox { get; init; } = null!;
            public ComboBox StatusBox { get; init; } = null!;
        }

        private readonly List<EligibilityRow> _eligibilityRows = new();

        public CustomerEditForm(CustomerModel? existingCustomer, string suggestedCode = "")
        {
            _isEditMode = existingCustomer != null;

            InitializeForm();
            InitializeControls();

            if (existingCustomer == null && !string.IsNullOrEmpty(suggestedCode))
            {
                _codeBox.Text = suggestedCode;
            }

            if (existingCustomer != null)
            {
                Result = new CustomerModel
                {
                    CustomerId = existingCustomer.CustomerId,
                    CustomerCode = existingCustomer.CustomerCode,
                    FirstName = existingCustomer.FirstName,
                    LastName = existingCustomer.LastName,
                    Email = existingCustomer.Email,
                    ContactNo = existingCustomer.ContactNo,
                    Address = existingCustomer.Address,
                    LoyaltyPoints = existingCustomer.LoyaltyPoints,
                    Status = existingCustomer.Status,
                    DiscountEligibilities = existingCustomer.DiscountEligibilities
                        .Select(e => new CustomerDiscountEligibilityModel
                        {
                            EligibilityId = e.EligibilityId,
                            CustomerId = e.CustomerId,
                            Category = e.Category,
                            IdNumber = e.IdNumber,
                            VerificationStatus = e.VerificationStatus
                        })
                        .ToList()
                };

                _codeBox.Text = Result.CustomerCode;
                _firstNameBox.Text = Result.FirstName;
                _lastNameBox.Text = Result.LastName;
                _emailBox.Text = Result.Email;
                _contactBox.Text = Result.ContactNo;
                _addressBox.Text = Result.Address;
                _loyaltyPointsBox.Value = Result.LoyaltyPoints;
                _statusBox.Text = Result.Status;

                foreach (var row in _eligibilityRows)
                {
                    var record = Result.DiscountEligibilities.FirstOrDefault(e =>
                        string.Equals(e.Category, row.Category, StringComparison.OrdinalIgnoreCase));

                    if (record == null)
                    {
                        continue;
                    }

                    row.Check.Checked = true;
                    row.IdBox.Text = record.IdNumber;

                    int statusIndex = row.StatusBox.FindStringExact(record.VerificationStatus);
                    row.StatusBox.SelectedIndex = statusIndex >= 0 ? statusIndex : 0;
                }
            }

            UpdateEligibilityRowStates();
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Customer" : "Add Customer";
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
                Text = _isEditMode ? "Edit Customer" : "Add New Customer",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 18)
            };
            root.Controls.Add(titleLabel);

            _codeBox = AddField(root, "Customer Code");
            _firstNameBox = AddField(root, "First Name");
            _lastNameBox = AddField(root, "Last Name");
            _emailBox = AddField(root, "Email");
            _contactBox = AddField(root, "Contact No.");
            _addressBox = AddField(root, "Address");

            var discountLabel = new Label
            {
                Text = "DISCOUNT ELIGIBILITY",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(discountLabel);

            foreach (var category in EligibilityCategories)
            {
                root.Controls.Add(BuildEligibilityRow(category));
            }

            _loyaltyPointsBox = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 0,
                Maximum = 999999,
                Enabled = false
            };

            if (_isEditMode)
            {
                var loyaltyLabel = new Label
                {
                    Text = "LOYALTY POINTS (READ-ONLY)",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = LabelGray,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 4)
                };
                root.Controls.Add(loyaltyLabel);
                _loyaltyPointsBox.Margin = new Padding(0, 0, 0, 14);
                root.Controls.Add(_loyaltyPointsBox);
            }

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

            if (_isEditMode)
            {
                var statusLabel = new Label
                {
                    Text = "STATUS",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = LabelGray,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 4)
                };
                root.Controls.Add(statusLabel);
                root.Controls.Add(_statusBox);
            }

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
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
                Text = _isEditMode ? "Save Changes" : "Add Customer",
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

        private TableLayoutPanel BuildEligibilityRow(string category)
        {
            var check = new CheckBox
            {
                Text = category,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = TextDark,
                Anchor = AnchorStyles.Left
            };

            var idBox = new TextBox
            {
                Width = 150,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "ID Number",
                Enabled = false
            };

            var statusBox = new ComboBox
            {
                Width = 170,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            statusBox.Items.AddRange(VerificationStatusOptions);
            statusBox.SelectedIndex = 0;

            check.CheckedChanged += (s, e) =>
            {
                idBox.Enabled = check.Checked;
                statusBox.Enabled = check.Checked;
                if (!check.Checked)
                {
                    idBox.Text = "";
                    statusBox.SelectedIndex = 0;
                    idBox.BackColor = ValidFieldColor;
                }
            };

            _eligibilityRows.Add(new EligibilityRow
            {
                Category = category,
                Check = check,
                IdBox = idBox,
                StatusBox = statusBox
            });

            var row = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            row.Controls.Add(check, 0, 0);
            row.Controls.Add(idBox, 1, 0);
            row.Controls.Add(statusBox, 2, 0);
            return row;
        }

        private void UpdateEligibilityRowStates()
        {
            foreach (var row in _eligibilityRows)
            {
                row.IdBox.Enabled = row.Check.Checked;
                row.StatusBox.Enabled = row.Check.Checked;
            }
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
            ClearFieldHighlights();

            string code = _codeBox.Text.Trim();
            string firstName = _firstNameBox.Text.Trim();
            string lastName = _lastNameBox.Text.Trim();
            string email = _emailBox.Text.Trim();
            string contact = _contactBox.Text.Trim();
            string address = _addressBox.Text.Trim();

            if (!ValidationHelper.IsRequired(code))
            {
                ShowFieldError(_codeBox, "Customer Code is required.");
                return;
            }

            if (!ValidationHelper.IsRequired(firstName))
            {
                ShowFieldError(_firstNameBox, "First Name is required.");
                return;
            }

            if (!ValidationHelper.IsRequired(lastName))
            {
                ShowFieldError(_lastNameBox, "Last Name is required.");
                return;
            }

            if (!string.IsNullOrEmpty(contact) && !ValidationHelper.IsValidPhilippineMobileNumber(contact))
            {
                ShowFieldError(_contactBox, "Contact number must be exactly 11 digits (e.g. 09171234567).");
                return;
            }

            if (!string.IsNullOrEmpty(email) && !ValidationHelper.IsValidEmail(email))
            {
                ShowFieldError(_emailBox, "Please enter a valid email address.");
                return;
            }

            if (_isEditMode && !ValidationHelper.IsValidSelection(_statusBox))
            {
                ShowFieldError(_statusBox, "Please select a status.");
                return;
            }

            var eligibilities = new List<CustomerDiscountEligibilityModel>();

            foreach (var row in _eligibilityRows)
            {
                if (!row.Check.Checked)
                {
                    continue;
                }

                string idNumber = row.IdBox.Text.Trim();

                if (!ValidationHelper.IsRequired(idNumber))
                {
                    ShowFieldError(row.IdBox, $"ID Number is required for {row.Category}.");
                    return;
                }

                var existing = Result.DiscountEligibilities.FirstOrDefault(e =>
                    string.Equals(e.Category, row.Category, StringComparison.OrdinalIgnoreCase));

                eligibilities.Add(new CustomerDiscountEligibilityModel
                {
                    EligibilityId = existing?.EligibilityId ?? 0,
                    CustomerId = existing?.CustomerId ?? 0,
                    Category = row.Category,
                    IdNumber = idNumber,
                    VerificationStatus = row.StatusBox.Text
                });
            }

            Result.CustomerCode = code;
            Result.FirstName = firstName;
            Result.LastName = lastName;
            Result.Email = email;
            Result.ContactNo = contact;
            Result.Address = address;
            Result.LoyaltyPoints = _isEditMode ? (int)_loyaltyPointsBox.Value : 0;
            Result.Status = _isEditMode ? _statusBox.Text : "Active";
            Result.DiscountEligibilities = eligibilities;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowFieldError(Control control, string message)
        {
            _errorLabel.Text = message;
            control.BackColor = InvalidFieldColor;
            control.Focus();
        }

        private void ClearFieldHighlights()
        {
            _codeBox.BackColor = ValidFieldColor;
            _firstNameBox.BackColor = ValidFieldColor;
            _lastNameBox.BackColor = ValidFieldColor;
            _emailBox.BackColor = ValidFieldColor;
            _contactBox.BackColor = ValidFieldColor;
            _addressBox.BackColor = ValidFieldColor;
            _statusBox.BackColor = ValidFieldColor;

            foreach (var row in _eligibilityRows)
            {
                row.IdBox.BackColor = ValidFieldColor;
            }
        }
    }
}