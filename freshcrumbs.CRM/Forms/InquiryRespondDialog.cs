namespace freshcrumbs.CRM.winforms.Forms
{
    public class InquiryRespondDialog : Form
    {
        public string ResponseText { get; private set; } = string.Empty;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private TextBox _responseBox = null!;
        private Label _errorLabel = null!;

        public InquiryRespondDialog(string subject, string message)
        {
            Text = "Respond to Inquiry";
            Width = 460;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(30, 25, 30, 20);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var subjectLabel = new Label
            {
                Text = subject,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(subjectLabel);

            var messageLabel = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = LabelGray,
                MaximumSize = new Size(380, 0),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            root.Controls.Add(messageLabel);

            var responseLabel = new Label
            {
                Text = "YOUR RESPONSE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(responseLabel);

            _responseBox = new TextBox
            {
                Width = 380,
                Height = 90,
                Multiline = true,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_responseBox);

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
                Text = "Send Response",
                Width = 150,
                Height = 42,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_responseBox.Text))
                {
                    _errorLabel.Text = "Response cannot be empty.";
                    return;
                }

                ResponseText = _responseBox.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };

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
    }
}