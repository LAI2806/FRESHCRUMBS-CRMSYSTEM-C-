using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class FeedbackEditForm : Form
    {
        public FeedbackModel Result { get; private set; } = new();

        private readonly bool _isEditMode;
        private readonly List<CustomerModel> _customers;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;

        private ComboBox _customerBox = null!;
        private ComboBox _typeBox = null!;
        private TextBox _subjectBox = null!;
        private TextBox _descriptionBox = null!;
        private DateTimePicker _datePicker = null!;
        private ComboBox _statusBox = null!;
        private Label _errorLabel = null!;

        public FeedbackEditForm(FeedbackModel? existingFeedback, List<CustomerModel> customers)
        {
            _isEditMode = existingFeedback != null;
            _customers = customers;

            InitializeForm();
            InitializeControls();

            if (existingFeedback != null)
            {
                Result = new FeedbackModel
                {
                    FeedbackId = existingFeedback.FeedbackId,
                    CustomerId = existingFeedback.CustomerId,
                    Type = existingFeedback.Type,
                    Subject = existingFeedback.Subject,
                    Description = existingFeedback.Description,
                    DateSubmitted = existingFeedback.DateSubmitted,
                    Status = existingFeedback.Status
                };

                _customerBox.SelectedValue = Result.CustomerId;
                _typeBox.Text = Result.Type;
                _subjectBox.Text = Result.Subject;
                _descriptionBox.Text = Result.Description;
                _datePicker.Value = Result.DateSubmitted;
                _statusBox.Text = Result.Status;
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Feedback" : "Add Feedback";
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
                Text = _isEditMode ? "Edit Feedback" : "Add New Feedback",
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

            var typeLabel = new Label
            {
                Text = "TYPE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(typeLabel);

            _typeBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _typeBox.Items.AddRange(new object[] { "Complaint", "Feedback", "Suggestion" });
            _typeBox.SelectedIndex = 0;
            root.Controls.Add(_typeBox);

            _subjectBox = AddField(root, "Subject");
            _descriptionBox = AddField(root, "Description");

            var dateLabel = new Label
            {
                Text = "DATE SUBMITTED",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(dateLabel);

            _datePicker = new DateTimePicker
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Format = DateTimePickerFormat.Short,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_datePicker);

            _statusBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _statusBox.Items.AddRange(new object[] { "Reviewed", "Resolved" });
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
                Text = _isEditMode ? "Save Changes" : "Add Feedback",
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

            if (string.IsNullOrWhiteSpace(_descriptionBox.Text))
            {
                _errorLabel.Text = "Description is required.";
                return;
            }

            Result.CustomerId = customerId;
            Result.Type = _typeBox.Text;
            Result.Subject = _subjectBox.Text.Trim();
            Result.Description = _descriptionBox.Text.Trim();
            Result.DateSubmitted = _datePicker.Value;
            Result.Status = _isEditMode ? _statusBox.Text : "Pending";

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}