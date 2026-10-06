using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using freshcrumbs.CRM.winforms.UserControls;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class SubscriberRegistrationForm : Form
    {
        private readonly ApiService _apiService = new();

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private TextBox _codeBox = null!;
        private TextBox _nameBox = null!;
        private TextBox _addressBox = null!;
        private TextBox _contactBox = null!;
        private TextBox _emailBox = null!;
        private ComboBox _planBox = null!;
        private PlanTermsPanel _termsPanel = null!;
        private DateTimePicker _startPicker = null!;
        private Button _saveButton = null!;
        private Label _errorLabel = null!;

        public SubscriberRegistrationForm()
        {
            Text = "Register Tenant";
            Width = 520;
            Height = 900;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            InitializeControls();

            Load += async (s, e) => await LoadPlansAsync();
        }

        private TextBox AddField(string label, int y, int maxLength)
        {
            Controls.Add(new Label
            {
                Text = label,
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = LabelGray
            });

            var box = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                MaxLength = maxLength,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(box);
            return box;
        }

        private void InitializeControls()
        {
            Controls.Add(new Label
            {
                Text = "Register Tenant",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(30, 20),
                AutoSize = true
            });

            int y = 70;
            _codeBox = AddField("Company Code *", y, 50);
            _codeBox.CharacterCasing = CharacterCasing.Upper;
            y += 62;
            _nameBox = AddField("Company Name *", y, 200);
            y += 62;
            _addressBox = AddField("Business Address *", y, 300);
            y += 62;
            _contactBox = AddField("Contact Number *", y, 20);
            y += 62;
            _emailBox = AddField("Email *", y, 150);

            y += 66;
            Controls.Add(new Label
            {
                Text = "Plan *  (active plans only)",
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = LabelGray
            });
            _planBox = new ComboBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _planBox.SelectedIndexChanged += (s, e) =>
                _termsPanel.ShowPlan((_planBox.SelectedItem as PlanOption)?.Plan);
            Controls.Add(_planBox);

            y += 62;
            _termsPanel = new PlanTermsPanel
            {
                Location = new Point(30, y),
                Size = new Size(440, 110)
            };
            Controls.Add(_termsPanel);

            y += 125;
            Controls.Add(new Label
            {
                Text = "Start Date",
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = LabelGray
            });
            _startPicker = new DateTimePicker
            {
                Location = new Point(30, y + 22),
                Width = 200,
                Format = DateTimePickerFormat.Short,
                MinDate = DateTime.Today,
                MaxDate = DateTime.Today.AddDays(365),
                Value = DateTime.Today
            };
            Controls.Add(_startPicker);

            y += 62;
            Controls.Add(new Label
            {
                Text = "Registering a company does not create its tenant database. " +
                       "The database is provisioned separately.",
                Location = new Point(30, y),
                Size = new Size(440, 40),
                ForeColor = LabelGray,
                Font = new Font("Segoe UI", 9)
            });

            y += 45;
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
                Text = "Register",
                Location = new Point(30, y),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;
            _saveButton.Click += SaveButton_Click;
            Controls.Add(_saveButton);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(150, y),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderColor = BorderColor;
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancelButton);
        }

        private async Task LoadPlansAsync()
        {
            try
            {
                var plans = await _apiService.GetPlansAsync("Active");

                foreach (var plan in plans)
                {
                    _planBox.Items.Add(new PlanOption(plan));
                }

                if (plans.Count == 0)
                {
                    _errorLabel.Text = "There are no active plans. Create or activate a plan first.";
                }
            }
            catch (ApiValidationException ex)
            {
                _errorLabel.Text = ex.Message;
            }
            catch (Exception ex)
            {
                _errorLabel.Text = ErrorMessageHelper.GetFriendlyMessage(ex);
            }
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            if (!ValidationHelper.IsRequired(_codeBox.Text))
            {
                _errorLabel.Text = "Company code is required.";
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(_codeBox.Text.Trim().ToUpperInvariant(), "^[A-Z0-9][A-Z0-9_-]{1,49}$"))
            {
                _errorLabel.Text = "Company code must be 2 to 50 characters: letters, numbers, hyphens and underscores only.";
                return;
            }

            if (!ValidationHelper.IsRequired(_nameBox.Text))
            {
                _errorLabel.Text = "Company name is required.";
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

            if (_planBox.SelectedItem is not PlanOption option)
            {
                _errorLabel.Text = "Please select a plan.";
                return;
            }

            _saveButton.Enabled = false;

            try
            {
                await _apiService.RegisterSubscriberAsync(
                    _codeBox.Text.Trim(),
                    _nameBox.Text.Trim(),
                    _addressBox.Text.Trim(),
                    _contactBox.Text.Trim(),
                    _emailBox.Text.Trim(),
                    option.Plan.PlanId,
                    _startPicker.Value.Date);

                MessageBox.Show(
                    "The tenant was registered with the selected plan.\n\n" +
                    "Reminder: the tenant database for this company still has to be provisioned separately.",
                    "Tenant Registered", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiValidationException ex)
            {
                _errorLabel.Text = ex.Message;
            }
            catch (Exception ex)
            {
                _errorLabel.Text = ErrorMessageHelper.GetFriendlyMessage(ex);
            }
            finally
            {
                _saveButton.Enabled = true;
            }
        }
    }
}