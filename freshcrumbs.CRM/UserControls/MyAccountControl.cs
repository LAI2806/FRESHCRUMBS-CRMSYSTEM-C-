using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    // My Account for every MainCRM user (ADMIN / MANAGER / STAFF). Only the signed-in user's own account:
    // the API takes the account from the session token and never accepts another user's id.
    public class MyAccountControl : UserControl
    {
        private readonly ApiService _apiService = new();
        private readonly Action? _profileChanged;

        private Label _companyValue = null!;
        private Label _roleValue = null!;
        private TextBox _firstNameBox = null!;
        private TextBox _lastNameBox = null!;
        private TextBox _emailBox = null!;
        private TextBox _contactBox = null!;
        private Button _saveButton = null!;
        private Label _profileMessage = null!;

        private TextBox _currentPasswordBox = null!;
        private TextBox _newPasswordBox = null!;
        private TextBox _confirmPasswordBox = null!;
        private Button _passwordButton = null!;
        private Label _passwordMessage = null!;

        private Label _offlineLabel = null!;

        public MyAccountControl(Action? profileChanged = null)
        {
            _profileChanged = profileChanged;

            Dock = DockStyle.Fill;
            BackColor = BranchUi.PageBg;

            InitializeLayout();
            Load += async (s, e) => await LoadAccountAsync();
        }

        private void InitializeLayout()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BranchUi.PageBg };

            var root = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 10, 0, 20),
                BackColor = BranchUi.PageBg
            };

            root.Controls.Add(new Label
            {
                Text = "My Account",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = BranchUi.TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            });

            root.Controls.Add(new Label
            {
                Text = "Your own sign-in details. Your role and company are managed by your administrator.",
                Font = new Font("Segoe UI", 10),
                ForeColor = BranchUi.LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            });

            _offlineLabel = new Label
            {
                Text = "Offline: your account is shown read-only. Changing it needs the internet.",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(_offlineLabel);

            root.Controls.Add(BuildProfileCard());
            root.Controls.Add(BuildPasswordCard());

            scroll.Controls.Add(root);
            Controls.Add(scroll);
        }

        private Panel BuildProfileCard()
        {
            var card = CreateCard(out var body, "Profile");

            body.Controls.Add(BranchUi.CreateFieldLabel("COMPANY"));
            _companyValue = CreateReadOnlyValue();
            body.Controls.Add(_companyValue);

            body.Controls.Add(BranchUi.CreateFieldLabel("ROLE"));
            _roleValue = CreateReadOnlyValue();
            body.Controls.Add(_roleValue);

            _firstNameBox = AddTextField(body, "FIRST NAME", 100, false);
            _lastNameBox = AddTextField(body, "LAST NAME", 100, false);
            _emailBox = AddTextField(body, "EMAIL (SIGN-IN NAME)", 256, false);
            _contactBox = AddTextField(body, "CONTACT NUMBER", 20, false);

            _profileMessage = BranchUi.CreateMessageLabel();
            _profileMessage.MaximumSize = new Size(420, 0);
            body.Controls.Add(_profileMessage);

            _saveButton = BranchUi.CreateButton("Save Changes", true, 160);
            _saveButton.Click += SaveButton_Click;
            body.Controls.Add(_saveButton);

            return card;
        }

        private Panel BuildPasswordCard()
        {
            var card = CreateCard(out var body, "Change Password");

            _currentPasswordBox = AddTextField(body, "CURRENT PASSWORD", 100, true);
            _newPasswordBox = AddTextField(body, "NEW PASSWORD", 100, true);
            _confirmPasswordBox = AddTextField(body, "CONFIRM NEW PASSWORD", 100, true);

            body.Controls.Add(new Label
            {
                Text = "At least 6 characters with an uppercase letter, a lowercase letter, a number and a symbol.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = BranchUi.LabelGray,
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Margin = new Padding(0, 0, 0, 6)
            });

            _passwordMessage = BranchUi.CreateMessageLabel();
            _passwordMessage.MaximumSize = new Size(420, 0);
            body.Controls.Add(_passwordMessage);

            _passwordButton = BranchUi.CreateButton("Change Password", true, 170);
            _passwordButton.Click += PasswordButton_Click;
            body.Controls.Add(_passwordButton);

            return card;
        }

        private static Panel CreateCard(out FlowLayoutPanel body, string title)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(20, 16, 20, 16),
                Margin = new Padding(0, 0, 0, 14)
            };

            body = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            body.Controls.Add(BranchUi.CreateTitle(title, 13));
            card.Controls.Add(body);
            return card;
        }

        private static Label CreateReadOnlyValue()
        {
            return new Label
            {
                Text = "-",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = BranchUi.TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
        }

        private static TextBox AddTextField(FlowLayoutPanel body, string label, int maxLength, bool password)
        {
            body.Controls.Add(BranchUi.CreateFieldLabel(label));

            var box = new TextBox
            {
                Width = 420,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = maxLength,
                UseSystemPasswordChar = password,
                Margin = new Padding(0, 0, 0, 12)
            };

            body.Controls.Add(box);
            return box;
        }

        private async Task LoadAccountAsync()
        {
            try
            {
                ShowAccount(await _apiService.GetOwnAccountAsync());
            }
            catch (Exception ex)
            {
                ShowMessage(_profileMessage, BranchUi.GetMessage(ex), true);
                SetEditable(false);
            }
        }

        private void ShowAccount(TenantAccountModel account)
        {
            _companyValue.Text = string.IsNullOrWhiteSpace(account.CompanyName) ? "-" : account.CompanyName;
            _roleValue.Text = string.IsNullOrWhiteSpace(account.Role) ? "-" : account.Role;
            _firstNameBox.Text = account.FirstName;
            _lastNameBox.Text = account.LastName;
            _emailBox.Text = account.Email;
            _contactBox.Text = account.ContactNumber;

            _offlineLabel.Visible = !account.Online;
            SetEditable(account.Online);
        }

        private void SetEditable(bool editable)
        {
            foreach (var box in new[] { _firstNameBox, _lastNameBox, _emailBox, _contactBox, _currentPasswordBox, _newPasswordBox, _confirmPasswordBox })
            {
                box.ReadOnly = !editable;
            }

            _saveButton.Enabled = editable;
            _passwordButton.Enabled = editable;
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            string firstName = _firstNameBox.Text.Trim();
            string lastName = _lastNameBox.Text.Trim();
            string email = _emailBox.Text.Trim();
            string contact = _contactBox.Text.Trim();

            if (!ValidationHelper.IsRequired(firstName) || !ValidationHelper.IsRequired(lastName))
            {
                ShowMessage(_profileMessage, "First name and last name are required.", true);
                return;
            }

            if (!ValidationHelper.IsValidEmail(email))
            {
                ShowMessage(_profileMessage, "A valid email address is required. It is your sign-in name.", true);
                return;
            }

            if (contact.Length > 0 && !ValidationHelper.IsValidPhoneNumber(contact))
            {
                ShowMessage(_profileMessage, "Contact number must be 7 to 20 characters (digits, +, -, spaces, parentheses).", true);
                return;
            }

            bool emailChanged = !string.Equals(AuthSession.Current?.Email, email, StringComparison.OrdinalIgnoreCase);
            _saveButton.Enabled = false;

            try
            {
                var account = await _apiService.UpdateOwnAccountAsync(firstName, lastName, email, contact);
                ShowAccount(account);

                if (AuthSession.Current != null)
                {
                    AuthSession.Current.FullName = account.FullName;
                    AuthSession.Current.Email = account.Email;
                    AuthSession.Current.UserName = account.UserName;
                }

                _profileChanged?.Invoke();
                ShowMessage(_profileMessage,
                    emailChanged ? $"Your account was updated. Sign in with {account.Email} from now on." : "Your account was updated.",
                    false);
            }
            catch (Exception ex)
            {
                ShowMessage(_profileMessage, BranchUi.GetMessage(ex), true);
            }
            finally
            {
                if (!IsDisposed && !_offlineLabel.Visible)
                {
                    _saveButton.Enabled = true;
                }
            }
        }

        private async void PasswordButton_Click(object? sender, EventArgs e)
        {
            string current = _currentPasswordBox.Text;
            string newPassword = _newPasswordBox.Text;

            if (current.Length == 0)
            {
                ShowMessage(_passwordMessage, "Enter your current password.", true);
                return;
            }

            if (newPassword.Length == 0)
            {
                ShowMessage(_passwordMessage, "Enter a new password.", true);
                return;
            }

            if (newPassword != _confirmPasswordBox.Text)
            {
                ShowMessage(_passwordMessage, "The new passwords do not match.", true);
                return;
            }

            _passwordButton.Enabled = false;

            try
            {
                await _apiService.ChangeOwnPasswordAsync(current, newPassword);

                _currentPasswordBox.Clear();
                _newPasswordBox.Clear();
                _confirmPasswordBox.Clear();
                ShowMessage(_passwordMessage, "Your password was changed.", false);
            }
            catch (Exception ex)
            {
                ShowMessage(_passwordMessage, BranchUi.GetMessage(ex), true);
            }
            finally
            {
                if (!IsDisposed && !_offlineLabel.Visible)
                {
                    _passwordButton.Enabled = true;
                }
            }
        }

        private static void ShowMessage(Label label, string message, bool isError)
        {
            label.ForeColor = isError ? Color.Firebrick : Color.FromArgb(46, 130, 80);
            label.Text = message;
        }
    }
}
