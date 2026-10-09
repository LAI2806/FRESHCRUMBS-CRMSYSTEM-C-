using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    // First sign-in with a temporary password: the employee must choose their own password before continuing.
    public class ChangePasswordForm : Form
    {
        private readonly string _currentPassword;

        public string NewPassword { get; private set; } = string.Empty;

        private TextBox _newPasswordBox = null!;
        private TextBox _confirmBox = null!;
        private Button _saveButton = null!;
        private Label _errorLabel = null!;

        public ChangePasswordForm(string currentPassword)
        {
            _currentPassword = currentPassword;

            Text = "Set Your Password";
            Width = 420;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(25, 20, 25, 20);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            InitializeControls();
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

            root.Controls.Add(BranchUi.CreateTitle("Set Your Password", 13));

            root.Controls.Add(new Label
            {
                Text = "You signed in with a temporary password. Choose your own password to continue. "
                     + "Use at least 6 characters with an uppercase letter, a lowercase letter, a number and a symbol.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = BranchUi.TextDark,
                AutoSize = true,
                MaximumSize = new Size(350, 0),
                Margin = new Padding(0, 0, 0, 12)
            });

            root.Controls.Add(BranchUi.CreateFieldLabel("NEW PASSWORD"));
            _newPasswordBox = CreatePasswordBox();
            root.Controls.Add(_newPasswordBox);

            root.Controls.Add(BranchUi.CreateFieldLabel("CONFIRM NEW PASSWORD"));
            _confirmBox = CreatePasswordBox();
            root.Controls.Add(_confirmBox);

            _errorLabel = BranchUi.CreateMessageLabel();
            _errorLabel.MaximumSize = new Size(350, 0);
            root.Controls.Add(_errorLabel);

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };

            var cancelButton = BranchUi.CreateButton("Cancel", false, 120);
            cancelButton.DialogResult = DialogResult.Cancel;

            _saveButton = BranchUi.CreateButton("Set Password", true, 150);
            _saveButton.Click += SaveButton_Click;

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(_saveButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = _saveButton;
            CancelButton = cancelButton;
        }

        private static TextBox CreatePasswordBox()
        {
            return new TextBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true,
                MaxLength = 100,
                Margin = new Padding(0, 0, 0, 14)
            };
        }

        private async void SaveButton_Click(object? sender, EventArgs e)
        {
            string newPassword = _newPasswordBox.Text;

            if (newPassword.Length == 0)
            {
                _errorLabel.Text = "Enter a new password.";
                return;
            }

            if (newPassword != _confirmBox.Text)
            {
                _errorLabel.Text = "The passwords do not match.";
                return;
            }

            if (newPassword == _currentPassword)
            {
                _errorLabel.Text = "The new password must be different from the temporary password.";
                return;
            }

            _saveButton.Enabled = false;
            _errorLabel.Text = "";

            try
            {
                await new ApiService().ChangeOwnPasswordAsync(_currentPassword, newPassword);

                NewPassword = newPassword;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = BranchUi.GetMessage(ex);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _saveButton.Enabled = true;
                }
            }
        }
    }
}
