using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    // Shows a new employee's one-time temporary password. It is not stored anywhere and cannot be shown again.
    public class TemporaryPasswordForm : Form
    {
        public TemporaryPasswordForm(string fullName, string email, string temporaryPassword)
        {
            Text = "Employee Account Created";
            Width = 440;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(25, 20, 25, 20);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            root.Controls.Add(BranchUi.CreateTitle("Account Created", 13));

            root.Controls.Add(new Label
            {
                Text = $"{fullName} can now sign in with {email} and the temporary password below.",
                Font = new Font("Segoe UI", 10),
                ForeColor = BranchUi.TextDark,
                AutoSize = true,
                MaximumSize = new Size(370, 0),
                Margin = new Padding(0, 0, 0, 12)
            });

            root.Controls.Add(BranchUi.CreateFieldLabel("TEMPORARY PASSWORD"));

            var passwordBox = new TextBox
            {
                Text = temporaryPassword,
                ReadOnly = true,
                Width = 370,
                Font = new Font("Consolas", 13f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(passwordBox);

            root.Controls.Add(new Label
            {
                Text = "This password is shown only once. Give it to the employee privately. They must set their own password at the first sign-in.",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                MaximumSize = new Size(370, 0),
                Margin = new Padding(0, 0, 0, 14)
            });

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };

            var copyButton = BranchUi.CreateButton("Copy Password", false, 150);
            copyButton.Click += (s, e) =>
            {
                Clipboard.SetText(temporaryPassword);
                copyButton.Text = "Copied";
            };

            var doneButton = BranchUi.CreateButton("Done", true, 120);
            doneButton.DialogResult = DialogResult.OK;

            buttons.Controls.Add(copyButton);
            buttons.Controls.Add(doneButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = doneButton;
            CancelButton = doneButton;
        }
    }
}
