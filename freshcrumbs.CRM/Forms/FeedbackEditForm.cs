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
        private static readonly Color ReadOnlyFieldColor = Color.FromArgb(250, 246, 242);

        private static readonly string[] CategoryOptions =
        {
            "Customer Service",
            "Product Quality",
            "Product Availability",
            "Orders",
            "Pricing and Payments",
            "Packaging"
        };

        private TextBox _customerCodeBox = null!;
        private TextBox _customerNameBox = null!;
        private Label _customerLookupLabel = null!;
        private CustomerModel? _selectedCustomer;
        private ComboBox _typeBox = null!;
        private ComboBox _categoryBox = null!;
        private TextBox _commentBox = null!;
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
                    Category = existingFeedback.Category,
                    Comment = existingFeedback.Comment,
                    DateSubmitted = existingFeedback.DateSubmitted,
                    Status = existingFeedback.Status
                };

                // The customer is locked in edit mode: the update endpoint never changes
                // CustomerId, so the existing customer is shown read-only.
                var existingCustomer = _customers.FirstOrDefault(c => c.CustomerId == Result.CustomerId);
                _customerCodeBox.Text = existingCustomer?.CustomerCode ?? string.Empty;
                _customerCodeBox.ReadOnly = true;
                _customerCodeBox.TabStop = false;
                _customerCodeBox.BackColor = ReadOnlyFieldColor;
                _typeBox.Text = Result.Type;
                _categoryBox.Text = Result.Category;
                _commentBox.Text = Result.Comment;
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

            var customerCodeLabel = new Label
            {
                Text = "CUSTOMER CODE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(customerCodeLabel);

            _customerCodeBox = new TextBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Enter Customer Code (e.g. CUST-001)",
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_customerCodeBox);

            var customerNameLabel = new Label
            {
                Text = "CUSTOMER NAME",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(customerNameLabel);

            _customerNameBox = new TextBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                TabStop = false,
                BackColor = ReadOnlyFieldColor,
                ForeColor = TextDark,
                PlaceholderText = "Filled in automatically from the Customer Code",
                Margin = new Padding(0, 0, 0, 2)
            };
            root.Controls.Add(_customerNameBox);

            // Fixed height so the dialog does not jump when a lookup message appears.
            _customerLookupLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = false,
                Width = 380,
                Height = 20,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_customerLookupLabel);

            _customerCodeBox.TextChanged += (s, e) => ResolveCustomerFromCode();

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

            var categoryLabel = new Label
            {
                Text = "CATEGORY",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(categoryLabel);

            _categoryBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _categoryBox.Items.AddRange(CategoryOptions);
            root.Controls.Add(_categoryBox);

            var commentLabel = new Label
            {
                Text = "COMMENT",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(commentLabel);

            _commentBox = new TextBox
            {
                Width = 380,
                Height = 80,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_commentBox);

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

        private void ResolveCustomerFromCode()
        {
            string code = _customerCodeBox.Text.Trim();

            _selectedCustomer = null;
            _customerNameBox.Text = string.Empty;
            _customerLookupLabel.Text = string.Empty;

            if (code.Length == 0)
            {
                return;
            }

            // Lookup is by the existing CustomerCode only: trimmed and case-insensitive.
            var customer = _customers.FirstOrDefault(c =>
                string.Equals(c.CustomerCode.Trim(), code, StringComparison.OrdinalIgnoreCase));

            if (customer == null)
            {
                _customerLookupLabel.Text = "Customer code not found.";
                return;
            }

            // New records can only be created for active customers. In edit mode the
            // customer is locked, so an inactive customer is still displayed.
            if (!_isEditMode && !string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                _customerLookupLabel.Text = "This customer is inactive and cannot be selected.";
                return;
            }

            _selectedCustomer = customer;
            _customerNameBox.Text = $"{customer.FirstName} {customer.LastName}";
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            // In edit mode the customer is locked, so the existing CustomerId is kept.
            int customerId = Result.CustomerId;

            if (!_isEditMode)
            {
                if (_selectedCustomer == null)
                {
                    _errorLabel.Text = "Please enter a valid Customer Code.";
                    return;
                }

                customerId = _selectedCustomer.CustomerId;
            }

            if (_categoryBox.SelectedIndex < 0)
            {
                _errorLabel.Text = "Please select a category.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_commentBox.Text))
            {
                _errorLabel.Text = "Comment is required.";
                return;
            }

            Result.CustomerId = customerId;
            Result.Type = _typeBox.Text;
            Result.Category = _categoryBox.Text;
            Result.Comment = _commentBox.Text.Trim();
            Result.DateSubmitted = _datePicker.Value;
            Result.Status = _isEditMode ? _statusBox.Text : "Pending";

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}