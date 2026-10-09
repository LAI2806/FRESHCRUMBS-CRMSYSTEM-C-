using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    // Add / edit a STAFF or MANAGER account. The branch list is passed only on Branching (Premium) plans.
    public class TenantUserEditForm : Form
    {
        private const string Unassigned = "(Unassigned)";

        public TenantUserModel Result { get; private set; } = new();

        public bool BranchChanged { get; private set; }

        private readonly bool _isEditMode;
        private readonly List<BranchModel>? _branches;
        private readonly int? _originalBranchId;

        private TextBox _firstNameBox = null!;
        private TextBox _lastNameBox = null!;
        private TextBox _emailBox = null!;
        private TextBox _contactBox = null!;
        private ComboBox _roleBox = null!;
        private ComboBox? _branchBox;
        private Label _errorLabel = null!;

        private sealed record BranchOption(int? BranchId, string Text)
        {
            public override string ToString() => Text;
        }

        // adminAccount: SuperAdmin creating a company's first ADMIN (no role choice, no branch: ADMIN is company-wide).
        private readonly bool _adminAccount;

        public TenantUserEditForm(TenantUserModel? existing, List<BranchModel>? branches, bool adminAccount = false)
        {
            _isEditMode = existing != null;
            _adminAccount = adminAccount && existing == null;
            _branches = _adminAccount ? null : branches;

            if (existing != null)
            {
                Result = new TenantUserModel
                {
                    Id = existing.Id,
                    FirstName = existing.FirstName,
                    LastName = existing.LastName,
                    Email = existing.Email,
                    ContactNumber = existing.ContactNumber,
                    Role = existing.Role,
                    Status = existing.Status,
                    BranchId = existing.BranchId,
                    BranchName = existing.BranchName
                };
                _originalBranchId = existing.BranchId;
            }

            Text = _isEditMode ? "Edit Employee" : _adminAccount ? "Create Tenant Admin" : "Add Employee";
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

            root.Controls.Add(BranchUi.CreateTitle(_isEditMode ? "Edit Employee" : _adminAccount ? "Create Tenant Admin" : "Add New Employee", 13));

            _firstNameBox = AddTextField(root, "FIRST NAME", Result.FirstName, 100);
            _lastNameBox = AddTextField(root, "LAST NAME", Result.LastName, 100);
            _emailBox = AddTextField(root, "EMAIL (SIGN-IN NAME)", Result.Email, 256);
            _contactBox = AddTextField(root, "CONTACT NUMBER", Result.ContactNumber, 20);

            _roleBox = CreateComboBox();
            _roleBox.Items.AddRange(new object[] { TenantRole.Staff, TenantRole.Manager });
            _roleBox.SelectedItem = string.Equals(Result.Role, TenantRole.Manager, StringComparison.OrdinalIgnoreCase)
                ? TenantRole.Manager
                : TenantRole.Staff;

            if (!_adminAccount)
            {
                root.Controls.Add(BranchUi.CreateFieldLabel("ROLE"));
                root.Controls.Add(_roleBox);
            }

            if (_branches != null)
            {
                root.Controls.Add(BranchUi.CreateFieldLabel("ASSIGNED BRANCH"));
                _branchBox = CreateComboBox();
                _branchBox.Items.Add(new BranchOption(null, Unassigned));

                foreach (var branch in _branches.Where(b => string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase)))
                {
                    _branchBox.Items.Add(new BranchOption(branch.BranchId, branch.BranchName));
                }

                // Keep a current assignment to a branch that has since been deactivated, so saving does not drop it.
                if (_originalBranchId != null
                    && !_branchBox.Items.OfType<BranchOption>().Any(o => o.BranchId == _originalBranchId))
                {
                    _branchBox.Items.Add(new BranchOption(_originalBranchId, $"{Result.BranchName} (inactive)"));
                }

                _branchBox.SelectedItem = _branchBox.Items.OfType<BranchOption>()
                    .First(o => o.BranchId == _originalBranchId);
                root.Controls.Add(_branchBox);
            }

            if (!_isEditMode)
            {
                root.Controls.Add(new Label
                {
                    Text = "A one-time temporary password is created and shown once. The employee must set a new password at the first sign-in.",
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = BranchUi.LabelGray,
                    AutoSize = true,
                    MaximumSize = new Size(350, 0),
                    Margin = new Padding(0, 0, 0, 6)
                });
            }

            _errorLabel = BranchUi.CreateMessageLabel();
            _errorLabel.MaximumSize = new Size(350, 0);
            root.Controls.Add(_errorLabel);

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };

            var cancelButton = BranchUi.CreateButton("Cancel", false, 120);
            cancelButton.DialogResult = DialogResult.Cancel;

            var saveButton = BranchUi.CreateButton(_isEditMode ? "Save Changes" : "Add Employee", true, 150);
            saveButton.Click += SaveButton_Click;

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(saveButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private static TextBox AddTextField(TableLayoutPanel root, string label, string value, int maxLength)
        {
            root.Controls.Add(BranchUi.CreateFieldLabel(label));

            var box = new TextBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = maxLength,
                Text = value,
                Margin = new Padding(0, 0, 0, 14)
            };

            root.Controls.Add(box);
            return box;
        }

        private static ComboBox CreateComboBox()
        {
            return new ComboBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            string firstName = _firstNameBox.Text.Trim();
            string lastName = _lastNameBox.Text.Trim();
            string email = _emailBox.Text.Trim();
            string contact = _contactBox.Text.Trim();

            if (!ValidationHelper.IsRequired(firstName) || !ValidationHelper.IsRequired(lastName))
            {
                _errorLabel.Text = "First name and last name are required.";
                return;
            }

            if (!ValidationHelper.IsValidEmail(email))
            {
                _errorLabel.Text = "A valid email address is required. It is the employee's sign-in name.";
                return;
            }

            if (contact.Length > 0 && !ValidationHelper.IsValidPhoneNumber(contact))
            {
                _errorLabel.Text = "Contact number must be 7 to 20 characters (digits, +, -, spaces, parentheses).";
                return;
            }

            Result.FirstName = firstName;
            Result.LastName = lastName;
            Result.Email = email;
            Result.ContactNumber = contact;
            Result.Role = _adminAccount ? TenantRole.Admin : _roleBox.SelectedItem?.ToString() ?? TenantRole.Staff;

            if (_branchBox?.SelectedItem is BranchOption option)
            {
                Result.BranchId = option.BranchId;
                BranchChanged = option.BranchId != _originalBranchId;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
