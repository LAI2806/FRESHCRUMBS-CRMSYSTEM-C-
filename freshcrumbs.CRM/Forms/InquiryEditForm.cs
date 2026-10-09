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
        private static readonly Color ReadOnlyFieldColor = Color.FromArgb(250, 246, 242);

        private TextBox _customerCodeBox = null!;
        private TextBox _customerNameBox = null!;
        private Label _customerLookupLabel = null!;
        private CustomerModel? _selectedCustomer;
        private ComboBox _typeBox = null!;
        private ComboBox _sourceBox = null!;
        private TextBox _concernBox = null!;
        private Label _dateReceivedValueLabel = null!;
        private ComboBox _statusBox = null!;
        private TextBox _responseBox = null!;
        private Label _respondedByValueLabel = null!;
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
                    Type = existingInquiry.Type,
                    Source = existingInquiry.Source,
                    Subject = existingInquiry.Subject,
                    Message = existingInquiry.Message,
                    DateSubmitted = existingInquiry.DateSubmitted,
                    Status = existingInquiry.Status,
                    Response = existingInquiry.Response,
                    RespondedBy = existingInquiry.RespondedBy,
                    RespondedAt = existingInquiry.RespondedAt
                };

                // The customer is locked in edit mode: the update endpoint never changes
                // CustomerId, so the existing customer is shown read-only.
                var existingCustomer = _customers.FirstOrDefault(c => c.CustomerId == Result.CustomerId);
                _customerCodeBox.Text = existingCustomer?.CustomerCode ?? string.Empty;
                _customerCodeBox.ReadOnly = true;
                _customerCodeBox.TabStop = false;
                _customerCodeBox.BackColor = ReadOnlyFieldColor;
                _typeBox.Text = Result.Type;
                _sourceBox.Text = Result.Source;
                _concernBox.Text = Result.Message;
                _dateReceivedValueLabel.Text = Result.DateSubmitted.ToString("MM/dd/yyyy h:mm tt");
                _statusBox.Text = Result.Status;
                _responseBox.Text = Result.Response;
                _respondedByValueLabel.Text = string.IsNullOrWhiteSpace(Result.RespondedBy)
                    ? "Not yet responded"
                    : $"{Result.RespondedBy} on {Result.RespondedAt:MM/dd/yyyy h:mm tt}";
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Review / Process Inquiry" : "New Inquiry";
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
                Text = _isEditMode ? "Review / Process Inquiry" : "New Inquiry",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 18)
            };
            root.Controls.Add(titleLabel);

            AddSectionLabel(root, "CUSTOMER CODE");

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

            AddSectionLabel(root, "CUSTOMER NAME");

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

            AddSectionLabel(root, "TYPE");

            _typeBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _typeBox.Items.AddRange(new object[] { "Product", "Order", "Payment", "Promotion", "Other" });
            _typeBox.SelectedIndex = 0;
            root.Controls.Add(_typeBox);

            AddSectionLabel(root, "SOURCE");

            _sourceBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _sourceBox.Items.AddRange(new object[] { "Phone Call", "Email", "Facebook", "Walk-in", "Other" });
            _sourceBox.SelectedIndex = 0;
            root.Controls.Add(_sourceBox);

            AddSectionLabel(root, "CONCERN");

            _concernBox = new TextBox
            {
                MaxLength = 1000,
                Width = 380,
                Height = 80,
                Multiline = true,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_concernBox);

            // Date Received, Status, Response, and Responded By only apply once an
            // inquiry already exists. Date Received is system-recorded and never
            // staff-entered, so it is display-only here.
            _dateReceivedValueLabel = new Label
            {
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };

            _statusBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _statusBox.Items.AddRange(new object[] { "Pending", "In Progress", "Completed" });
            _statusBox.SelectedIndex = 0;

            _responseBox = new TextBox
            {
                MaxLength = 1000,
                Width = 380,
                Height = 90,
                Multiline = true,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 14)
            };

            // Responded By/At are system-recorded when the response is saved and
            // are never staff-entered, so they are display-only here.
            _respondedByValueLabel = new Label
            {
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };

            if (_isEditMode)
            {
                AddSectionLabel(root, "DATE RECEIVED");
                root.Controls.Add(_dateReceivedValueLabel);

                AddSectionLabel(root, "STATUS");
                root.Controls.Add(_statusBox);

                AddSectionLabel(root, "RESPONSE");
                root.Controls.Add(_responseBox);

                AddSectionLabel(root, "RESPONDED BY");
                root.Controls.Add(_respondedByValueLabel);
            }

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                MaximumSize = new Size(380, 0),
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

        private void AddSectionLabel(TableLayoutPanel root, string text)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(label);
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

            if (!(_typeBox.SelectedIndex >= 0 && _typeBox.SelectedItem != null))
            {
                _errorLabel.Text = "Please select a type.";
                return;
            }

            if (!(_sourceBox.SelectedIndex >= 0 && _sourceBox.SelectedItem != null))
            {
                _errorLabel.Text = "Please select a source.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_concernBox.Text))
            {
                _errorLabel.Text = "Concern is required.";
                return;
            }

            // A response must already exist before staff can mark an inquiry
            // Completed. Checked here so a bad submission never reaches the API.
            if (_isEditMode &&
                _statusBox.Text == "Completed" &&
                string.IsNullOrWhiteSpace(_responseBox.Text))
            {
                _errorLabel.Text = "A response is required before marking this inquiry as Completed.";
                return;
            }

            Result.CustomerId = customerId;
            Result.Type = _typeBox.Text;
            Result.Source = _sourceBox.Text;
            Result.Message = _concernBox.Text.Trim();

            if (_isEditMode)
            {
                Result.Status = _statusBox.Text;
                Result.Response = _responseBox.Text.Trim();
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}