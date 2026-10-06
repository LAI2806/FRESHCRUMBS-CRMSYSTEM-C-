using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class TenantEditForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly int _companyId;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private TextBox _codeBox = null!;
        private TextBox _nameBox = null!;
        private TextBox _addressBox = null!;
        private TextBox _contactBox = null!;
        private TextBox _emailBox = null!;
        private Label _subscriptionLabel = null!;
        private Label _databaseLabel = null!;
        private Button _saveButton = null!;
        private Label _errorLabel = null!;

        public TenantEditForm(int companyId)
        {
            _companyId = companyId;

            Text = "Edit Tenant";
            Width = 520;
            Height = 760;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            InitializeControls();

            Load += async (s, e) => await LoadAsync();
        }

        private TextBox AddField(string label, int y, int maxLength, bool readOnly = false)
        {
            Controls.Add(new Label { Text = label, Location = new Point(30, y), AutoSize = true, ForeColor = LabelGray });

            var box = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                MaxLength = maxLength,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = readOnly,
                Enabled = false
            };
            Controls.Add(box);
            return box;
        }

        private void InitializeControls()
        {
            Controls.Add(new Label
            {
                Text = "Edit Tenant",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(30, 20),
                AutoSize = true
            });

            int y = 70;
            _codeBox = AddField("Tenant Code (cannot be changed)", y, 50, true);
            y += 62;
            _nameBox = AddField("Business Name *", y, 200);
            y += 62;
            _addressBox = AddField("Business Address *", y, 300);
            y += 62;
            _contactBox = AddField("Contact Number *", y, 20);
            y += 62;
            _emailBox = AddField("Email *", y, 150);

            y += 70;
            Controls.Add(new Label
            {
                Text = "Subscription (read-only)",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(30, y),
                AutoSize = true
            });

            _subscriptionLabel = new Label
            {
                Location = new Point(30, y + 26),
                Size = new Size(440, 60),
                ForeColor = Color.FromArgb(90, 80, 70),
                Text = "Loading..."
            };
            Controls.Add(_subscriptionLabel);

            y += 76;
            Controls.Add(new Label
            {
                Text = "Tenant Database (read-only)",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(30, y),
                AutoSize = true
            });

            _databaseLabel = new Label
            {
                Location = new Point(30, y + 26),
                Size = new Size(440, 76),
                ForeColor = Color.FromArgb(90, 80, 70),
                Text = "Loading..."
            };
            Controls.Add(_databaseLabel);

            y += 110;
            _errorLabel = new Label
            {
                Location = new Point(30, y),
                Size = new Size(440, 40),
                ForeColor = Color.Firebrick
            };
            Controls.Add(_errorLabel);

            y += 45;
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(30, y),
                Width = 110,
                Height = 38,
                Enabled = false,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(150, y),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            cancel.FlatAppearance.BorderColor = BorderColor;
            Controls.Add(cancel);

            CancelButton = cancel;
        }

        private async Task LoadAsync()
        {
            try
            {
                var detail = await _apiService.GetSubscriberAsync(_companyId);
                var company = detail.Company;

                _codeBox.Text = company.CompanyCode;
                _nameBox.Text = company.CompanyName;
                _addressBox.Text = company.BusinessAddress;
                _contactBox.Text = company.ContactNo;
                _emailBox.Text = company.Email;

                var sub = detail.CurrentSubscription;

                _subscriptionLabel.Text = sub == null
                    ? "No subscription. To assign a plan, use Subscribers."
                    : $"{sub.PlanName} ({sub.PlanCode}) | {sub.Status} | expires {sub.EndText} (UTC)\n" +
                      "To change the plan or subscription status, use Subscribers.";

                var database = detail.TenantDatabase;

                _databaseLabel.Text = database == null
                    ? "No tenant database is registered for this tenant yet (provisioned manually)."
                    : $"Server: {database.ServerName}\nDatabase: {database.DatabaseName}\n" +
                      $"Credential key: {database.CredentialKey}\nStatus: {(database.IsActive ? "Active" : "Inactive")}";

                foreach (var box in new[] { _nameBox, _addressBox, _contactBox, _emailBox })
                {
                    box.Enabled = true;
                }

                _codeBox.Enabled = true;
                _saveButton.Enabled = true;
            }
            catch (Exception ex)
            {
                _subscriptionLabel.Text = string.Empty;
                _databaseLabel.Text = string.Empty;
                _errorLabel.Text = AdminUi.GetMessage(ex);
            }
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            if (!ValidationHelper.IsRequired(_nameBox.Text))
            {
                _errorLabel.Text = "Business name is required.";
                return;
            }

            if (!ValidationHelper.IsRequired(_addressBox.Text))
            {
                _errorLabel.Text = "Business address is required.";
                return;
            }

            if (!ValidationHelper.IsValidPhoneNumber(_contactBox.Text.Trim()))
            {
                _errorLabel.Text = "Enter a valid contact number (7 to 20 digits or symbols).";
                return;
            }

            if (!ValidationHelper.IsValidEmail(_emailBox.Text.Trim()))
            {
                _errorLabel.Text = "Enter a valid email address.";
                return;
            }

            _saveButton.Enabled = false;

            try
            {
                await _apiService.UpdateTenantAsync(
                    _companyId,
                    _nameBox.Text.Trim(),
                    _addressBox.Text.Trim(),
                    _contactBox.Text.Trim(),
                    _emailBox.Text.Trim());

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = AdminUi.GetMessage(ex);
                _saveButton.Enabled = true;
            }
        }
    }
}