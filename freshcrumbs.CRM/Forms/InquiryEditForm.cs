using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class InquiryEditForm : Form
    {
        public InquiryModel Result { get; private set; } = new();

        private readonly bool _isEditMode;
        private readonly List<CustomerModel> _customers;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private ComboBox _customerBox = null!;
        private TextBox _subjectBox = null!;
        private TextBox _messageBox = null!;
        private Label _errorLabel = null!;

        public InquiryEditForm(InquiryModel? existingInquiry, List<CustomerModel> customers)
        {
            _isEditMode = existingInquiry != null;
            _customers = customers;

            InitializeForm();
            InitializeControls();

            if (existingInquiry != null)
            {
                Result = new InquiryModel
                {
                    InquiryId = existingInquiry.InquiryId,
                    CustomerId = existingInquiry.CustomerId,
                    Subject = existingInquiry.Subject,
                    Message = existingInquiry.Message,
                    Status = existingInquiry.Status
                };

                _customerBox.SelectedValue = Result.CustomerId;
                _customerBox.Enabled = false;
                _subjectBox.Text = Result.Subject;
                _messageBox.Text = Result.Message;
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Inquiry" : "New Inquiry";
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
                Text = _isEditMode ? "Edit Inquiry" : "New Inquiry",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 18)
            };
            root.Controls.Add(titleLabel);

            var customerLabel = new Label
            {
                Text = "CUSTOMER",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(customerLabel);

            _customerBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14),
                DataSource = _customers,
                DisplayMember = "FirstName",
                ValueMember = "CustomerId"
            };
            root.Controls.Add(_customerBox);

            var subjectLabel = new Label
            {
                Text = "SUBJECT",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(subjectLabel);

            _subjectBox = new TextBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_subjectBox);

            var messageLabel = new Label
            {
                Text = "MESSAGE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(messageLabel);

            _messageBox = new TextBox
            {
                Width = 380,
                Height = 80,
                Multiline = true,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_messageBox);

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
                Text = _isEditMode ? "Save Changes" : "Submit Inquiry",
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

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            if (_customerBox.SelectedValue is not int customerId)
            {
                _errorLabel.Text = "Please select a customer.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_subjectBox.Text))
            {
                _errorLabel.Text = "Subject is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_messageBox.Text))
            {
                _errorLabel.Text = "Message is required.";
                return;
            }

            Result.CustomerId = customerId;
            Result.Subject = _subjectBox.Text.Trim();
            Result.Message = _messageBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}