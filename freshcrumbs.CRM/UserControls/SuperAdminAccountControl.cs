using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class SuperAdminAccountControl : UserControl
    {
        private readonly ApiService _apiService = new();

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);

        private TextBox _userNameBox = null!;
        private TextBox _firstNameBox = null!;
        private TextBox _lastNameBox = null!;
        private TextBox _emailBox = null!;
        private TextBox _contactBox = null!;
        private TextBox _currentPasswordBox = null!;
        private TextBox _newPasswordBox = null!;
        private TextBox _confirmPasswordBox = null!;
        private Button _saveButton = null!;
        private Button _passwordButton = null!;
        private Label _profileStatus = null!;
        private Label _passwordStatus = null!;

        public event Action<string>? ProfileUpdated;

        public SuperAdminAccountControl()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;
            AutoScroll = true;

            Controls.Add(new Label
            {
                Text = "My Account",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(0, 0),
                AutoSize = true
            });

            var profileCard = CreateCard(0, 60, 560, 430, "Profile");
            _userNameBox = AddField(profileCard, "Username (cannot be changed)", 52, 50, true);
            _firstNameBox = AddField(profileCard, "First Name *", 112, 100, false);
            _lastNameBox = AddField(profileCard, "Last Name *", 172, 100, false);
            _emailBox = AddField(profileCard, "Email *", 232, 256, false);
            _contactBox = AddField(profileCard, "Contact Number", 292, 20, false);

            _saveButton = CreateButton("Save Changes", 20, 358, 140, AccentColor, Color.White);
            _saveButton.Enabled = false;
            _saveButton.Click += SaveButton_Click;
            profileCard.Controls.Add(_saveButton);

            _profileStatus = new Label
            {
                Location = new Point(20, 400),
                Size = new Size(520, 22),
                Font = new Font("Segoe UI", 9.5f)
            };
            profileCard.Controls.Add(_profileStatus);

            var passwordCard = CreateCard(0, 510, 560, 360, "Change Password");
            _currentPasswordBox = AddField(passwordCard, "Current Password *", 52, 128, false, true);
            _newPasswordBox = AddField(passwordCard, "New Password *", 112, 128, false, true);
            _confirmPasswordBox = AddField(passwordCard, "Confirm New Password *", 172, 128, false, true);

            _passwordButton = CreateButton("Update Password", 20, 240, 160, Color.White, LabelGray);
            _passwordButton.Click += PasswordButton_Click;
            passwordCard.Controls.Add(_passwordButton);

            _passwordStatus = new Label
            {
                Location = new Point(20, 286),
                Size = new Size(520, 50),
                Font = new Font("Segoe UI", 9.5f)
            };
            passwordCard.Controls.Add(_passwordStatus);

            Load += async (s, e) => await LoadAsync();
        }

        private Panel CreateCard(int x, int y, int width, int height, string title)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            card.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(20, 14),
                AutoSize = true
            });

            Controls.Add(card);
            return card;
        }

        private static TextBox AddField(Panel card, string label, int y, int maxLength, bool readOnly, bool password = false)
        {
            card.Controls.Add(new Label
            {
                Text = label,
                Location = new Point(20, y),
                AutoSize = true,
                ForeColor = LabelGray
            });

            var box = new TextBox
            {
                Location = new Point(20, y + 22),
                Width = 520,
                MaxLength = maxLength,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = readOnly,
                UseSystemPasswordChar = password
            };
            card.Controls.Add(box);
            return box;
        }

        private static Button CreateButton(string text, int x, int y, int width, Color back, Color fore)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = width,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = back == AccentColor ? 0 : 1;
            button.FlatAppearance.BorderColor = BorderColor;
            return button;
        }

        private async Task LoadAsync()
        {
            AdminUi.ShowInfo(_profileStatus, "Loading your account...");

            try
            {
                Fill(await _apiService.GetMyAccountAsync());
                _profileStatus.Text = string.Empty;
                _saveButton.Enabled = true;
            }
            catch (Exception ex) when (AdminUi.IsAccessDenied(ex))
            {
                Controls.Clear();
                Controls.Add(new StatePanel("Access denied", ex.Message));
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_profileStatus, ex);
            }
        }

        private void Fill(MyAccountModel account)
        {
            _userNameBox.Text = account.UserName;
            _firstNameBox.Text = account.FirstName;
            _lastNameBox.Text = account.LastName;
            _emailBox.Text = account.Email;
            _contactBox.Text = account.ContactNumber;
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            if (!ValidationHelper.IsRequired(_firstNameBox.Text))
            {
                AdminUi.ShowError(_profileStatus, new ApiValidationException("First name is required."));
                return;
            }

            if (!ValidationHelper.IsRequired(_lastNameBox.Text))
            {
                AdminUi.ShowError(_profileStatus, new ApiValidationException("Last name is required."));
                return;
            }

            if (!ValidationHelper.IsValidEmail(_emailBox.Text.Trim()))
            {
                AdminUi.ShowError(_profileStatus, new ApiValidationException("Enter a valid email address."));
                return;
            }

            if (_contactBox.Text.Trim().Length > 0 && !ValidationHelper.IsValidPhoneNumber(_contactBox.Text.Trim()))
            {
                AdminUi.ShowError(_profileStatus, new ApiValidationException("Enter a valid contact number (7 to 20 digits or symbols)."));
                return;
            }

            _saveButton.Enabled = false;
            AdminUi.ShowInfo(_profileStatus, "Saving...");

            try
            {
                await _apiService.UpdateMyAccountAsync(
                    _firstNameBox.Text.Trim(),
                    _lastNameBox.Text.Trim(),
                    _emailBox.Text.Trim(),
                    _contactBox.Text.Trim());

                var account = await _apiService.GetMyAccountAsync();
                Fill(account);

                if (AuthSession.Current != null)
                {
                    AuthSession.Current.FullName = account.FullName;
                    AuthSession.Current.Email = account.Email;
                }

                ProfileUpdated?.Invoke(account.FullName);
                AdminUi.ShowSuccess(_profileStatus, "Your account was updated.");
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_profileStatus, ex);
            }
            finally
            {
                _saveButton.Enabled = true;
            }
        }

        private async void PasswordButton_Click(object? sender, EventArgs e)
        {
            if (_currentPasswordBox.Text.Length == 0 || _newPasswordBox.Text.Length == 0)
            {
                AdminUi.ShowError(_passwordStatus, new ApiValidationException("Enter your current password and a new password."));
                return;
            }

            if (_newPasswordBox.Text != _confirmPasswordBox.Text)
            {
                AdminUi.ShowError(_passwordStatus, new ApiValidationException("The new password and its confirmation do not match."));
                return;
            }

            if (MessageBox.Show("Change your password?", "Change Password",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _passwordButton.Enabled = false;
            AdminUi.ShowInfo(_passwordStatus, "Updating password...");

            try
            {
                await _apiService.ChangeMyPasswordAsync(_currentPasswordBox.Text, _newPasswordBox.Text);

                _currentPasswordBox.Clear();
                _newPasswordBox.Clear();
                _confirmPasswordBox.Clear();
                AdminUi.ShowSuccess(_passwordStatus, "Your password was changed.");
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_passwordStatus, ex);
            }
            finally
            {
                _passwordButton.Enabled = true;
            }
        }
    }
}