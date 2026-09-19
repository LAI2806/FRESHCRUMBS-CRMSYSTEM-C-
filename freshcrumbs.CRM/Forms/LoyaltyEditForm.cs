using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class LoyaltyEditForm : Form
    {
        public LoyaltyModel Result { get; private set; } = new();

        private readonly bool _isEditMode;
        private readonly List<CustomerModel> _customers;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;

        private ComboBox _customerBox = null!;
        private NumericUpDown _pointsEarnedBox = null!;
        private NumericUpDown _pointsUsedBox = null!;
        private ComboBox _transactionTypeBox = null!;
        private DateTimePicker _datePicker = null!;
        private Label _errorLabel = null!;

        public LoyaltyEditForm(LoyaltyModel? existingTransaction, List<CustomerModel> customers)
        {
            _isEditMode = existingTransaction != null;
            _customers = customers;

            InitializeForm();
            InitializeControls();

            if (existingTransaction != null)
            {
                Result = new LoyaltyModel
                {
                    LoyaltyTransactionId = existingTransaction.LoyaltyTransactionId,
                    CustomerId = existingTransaction.CustomerId,
                    PointsEarned = existingTransaction.PointsEarned,
                    PointsUsed = existingTransaction.PointsUsed,
                    TransactionType = existingTransaction.TransactionType,
                    Date = existingTransaction.Date
                };

                _customerBox.SelectedValue = Result.CustomerId;
                _pointsEarnedBox.Value = Result.PointsEarned;
                _pointsUsedBox.Value = Result.PointsUsed;
                _transactionTypeBox.Text = Result.TransactionType;
                _datePicker.Value = Result.Date;
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Loyalty Transaction" : "Add Loyalty Transaction";
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
                Text = _isEditMode ? "Edit Loyalty Transaction" : "Add Loyalty Transaction",
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

            var pointsEarnedLabel = new Label
            {
                Text = "POINTS EARNED",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(pointsEarnedLabel);

            _pointsEarnedBox = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_pointsEarnedBox);

            var pointsUsedLabel = new Label
            {
                Text = "POINTS USED",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(pointsUsedLabel);

            _pointsUsedBox = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_pointsUsedBox);

            var typeLabel = new Label
            {
                Text = "TRANSACTION TYPE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(typeLabel);

            _transactionTypeBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _transactionTypeBox.Items.AddRange(new object[] { "Earned", "Used" });
            _transactionTypeBox.SelectedIndex = 0;
            root.Controls.Add(_transactionTypeBox);

            var dateLabel = new Label
            {
                Text = "DATE",
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
                Text = _isEditMode ? "Save Changes" : "Add Transaction",
                Width = 165,
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

            Result.CustomerId = customerId;
            Result.PointsEarned = (int)_pointsEarnedBox.Value;
            Result.PointsUsed = (int)_pointsUsedBox.Value;
            Result.TransactionType = _transactionTypeBox.Text;
            Result.Date = _datePicker.Value;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}