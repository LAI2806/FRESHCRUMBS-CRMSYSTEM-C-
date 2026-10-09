using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class LoginForm : Form
    {
        private readonly ApiService _apiService;
        private TextBox _userNameBox = null!;
        private TextBox _passwordBox = null!;
        private Button _loginButton = null!;
        private Label _statusLabel = null!;

        public LoginForm()
        {
            _apiService = new ApiService();

            InitializeForm();
            InitializeControls();
        }

        private void InitializeForm()
        {
            Text = "FreshCrumbs - Log In";
            Width = 450;
            Height = 330;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.WhiteSmoke;
        }

        private void InitializeControls()
        {
            Controls.Add(new Label
            {
                Text = "FreshCrumbs",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 40, 30),
                Location = new Point(30, 25),
                AutoSize = true
            });

            Controls.Add(new Label
            {
                Text = "Log in to continue",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.DimGray,
                Location = new Point(30, 65),
                AutoSize = true
            });

            _userNameBox = new TextBox
            {
                Location = new Point(30, 100),
                Width = 370,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Username or email"
            };
            _userNameBox.KeyDown += InputBox_KeyDown;
            Controls.Add(_userNameBox);

            _passwordBox = new TextBox
            {
                Location = new Point(30, 140),
                Width = 370,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Password",
                UseSystemPasswordChar = true
            };
            _passwordBox.KeyDown += InputBox_KeyDown;
            Controls.Add(_passwordBox);

            _loginButton = new Button
            {
                Text = "Log In",
                Location = new Point(30, 185),
                Width = 120,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(210, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _loginButton.FlatAppearance.BorderSize = 0;
            _loginButton.Click += LoginButton_Click;
            Controls.Add(_loginButton);

            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(30, 235),
                Size = new Size(370, 40),
                ForeColor = Color.Firebrick,
                Font = new Font("Segoe UI", 9)
            };
            Controls.Add(_statusLabel);

            AcceptButton = _loginButton;
        }

        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            _statusLabel.Text = "";
        }

        private async void LoginButton_Click(object? sender, EventArgs e)
        {
            if (!ValidationHelper.IsRequired(_userNameBox.Text))
            {
                _statusLabel.Text = "Username is required.";
                _userNameBox.Focus();
                return;
            }

            if (!ValidationHelper.IsRequired(_passwordBox.Text))
            {
                _statusLabel.Text = "Password is required.";
                _passwordBox.Focus();
                return;
            }

            _loginButton.Enabled = false;
            _statusLabel.Text = "";

            try
            {
                var login = await _apiService.LoginAsync(_userNameBox.Text.Trim(), _passwordBox.Text);

                if (!login.IsSuperAdmin && login.TenantId == null)
                {
                    _statusLabel.Text = "This account is not assigned to a company.";
                    return;
                }

                AuthSession.Start(login);

                // First sign-in with the temporary password the ADMIN received: a new password is required.
                if (login.MustChangePassword)
                {
                    using var passwordForm = new ChangePasswordForm(_passwordBox.Text);

                    if (passwordForm.ShowDialog(this) != DialogResult.OK)
                    {
                        AuthSession.Clear();
                        _statusLabel.Text = "You must set your own password before using FreshCrumbs.";
                        return;
                    }

                    // The first session was limited to changing the password; sign in again with the new one.
                    login = await _apiService.LoginAsync(_userNameBox.Text.Trim(), passwordForm.NewPassword);

                    if (login.MustChangePassword || login.TenantId == null)
                    {
                        AuthSession.Clear();
                        _statusLabel.Text = "Your password was changed. Please sign in again with the new password.";
                        return;
                    }

                    AuthSession.Start(login);
                }

                List<string>? features = null;

                if (!login.IsSuperAdmin)
                {
                    // A new ApiService so the request carries the token that was just issued.
                    var subscription = await new ApiService().GetTenantSubscriptionAsync(login.TenantId!.Value);

                    if (!subscription.HasAccess)
                    {
                        AuthSession.Clear();
                        _statusLabel.Text = subscription.Message;
                        return;
                    }

                    features = subscription.Features;
                    AuthSession.SetAccess(subscription.Features, subscription.Permissions);

                    // The latest Terms & Conditions must be accepted before the company can use the CRM.
                    var terms = await new ApiService().GetTenantTermsAsync(login.TenantId!.Value);

                    if (terms.PendingAcceptance)
                    {
                        using var termsForm = new TermsAcceptanceForm(login.TenantId!.Value, terms);

                        if (termsForm.ShowDialog(this) != DialogResult.OK)
                        {
                            AuthSession.Clear();
                            _statusLabel.Text = "You must accept the Terms & Conditions to use FreshCrumbs.";
                            return;
                        }
                    }
                }

                OpenWorkspace(login, features);
            }
            catch (ApiValidationException ex)
            {
                AuthSession.Clear();
                _statusLabel.Text = ex.Message;
            }
            catch (Exception ex)
            {
                AuthSession.Clear();
                _statusLabel.Text = ErrorMessageHelper.GetFriendlyMessage(ex);
            }
            finally
            {
                _loginButton.Enabled = true;
            }
        }

        private void OpenWorkspace(LoginResultModel login, List<string>? features)
        {
            _passwordBox.Clear();

            if (login.IsSuperAdmin)
            {
                var shell = new SuperAdminShellForm();

                shell.FormClosed += (s, args) =>
                {
                    if (shell.LogoutRequested)
                    {
                        AuthSession.Clear();
                        _userNameBox.Clear();
                        _statusLabel.Text = "";
                        Show();
                        _userNameBox.Focus();
                    }
                    else
                    {
                        Close();
                    }
                };

                shell.Show();
                Hide();
                return;
            }

            var company = new CompanyModel
            {
                CompanyId = login.TenantId!.Value,
                CompanyCode = login.CompanyCode ?? string.Empty,
                CompanyName = login.CompanyName ?? string.Empty,
                EnabledFeatures = features
            };

            var mainForm = new MainCrmForm(company);

            // Same as the Super Admin console: Log Out returns to this screen; closing the window exits.
            mainForm.FormClosed += (s, args) =>
            {
                if (mainForm.LogoutRequested)
                {
                    AuthSession.Clear();
                    _userNameBox.Clear();
                    _statusLabel.Text = "";
                    Show();
                    _userNameBox.Focus();
                }
                else
                {
                    Close();
                }
            };

            mainForm.Show();
            Hide();
        }
    }
}