using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class BranchEditForm : Form
    {
        public BranchModel Result { get; private set; } = new();

        private readonly bool _isEditMode;
        private TextBox _nameBox = null!;
        private TextBox _addressBox = null!;
        private ComboBox _statusBox = null!;
        private ComboBox _managerBox = null!;
        private readonly List<BranchAssignmentModel> _managers;

        private sealed record ManagerOption(string? UserId, string Text)
        {
            public override string ToString() => Text;
        }
        private Label _errorLabel = null!;

        // managers: active MANAGER-role accounts that can be chosen; the manager is optional ("(None)").
        public BranchEditForm(BranchModel? existing, List<BranchAssignmentModel>? managers = null)
        {
            _isEditMode = existing != null;
            _managers = managers ?? new List<BranchAssignmentModel>();

            if (existing != null)
            {
                Result = new BranchModel
                {
                    BranchId = existing.BranchId,
                    BranchName = existing.BranchName,
                    Address = existing.Address,
                    Status = existing.Status,
                    ManagerUserId = existing.ManagerUserId,
                    ManagerName = existing.ManagerName
                };
            }

            Text = _isEditMode ? "Edit Branch" : "Add Branch";
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

            root.Controls.Add(BranchUi.CreateTitle(_isEditMode ? "Edit Branch" : "Add New Branch", 13));

            root.Controls.Add(BranchUi.CreateFieldLabel("BRANCH NAME"));
            _nameBox = new TextBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 100,
                Text = Result.BranchName,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_nameBox);

            root.Controls.Add(BranchUi.CreateFieldLabel("ADDRESS"));
            _addressBox = new TextBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 300,
                Text = Result.Address,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_addressBox);

            root.Controls.Add(BranchUi.CreateFieldLabel("MANAGER (OPTIONAL)"));
            _managerBox = new ComboBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _managerBox.Items.Add(new ManagerOption(null, "(None)"));

            foreach (var manager in _managers)
            {
                string name = string.IsNullOrWhiteSpace(manager.FullName) ? manager.UserName : manager.FullName;
                string where = manager.BranchId != null && manager.BranchId != Result.BranchId
                    ? $"  (now at {manager.BranchName})"
                    : string.Empty;

                _managerBox.Items.Add(new ManagerOption(manager.UserId, name + where));
            }

            _managerBox.SelectedItem = _managerBox.Items.OfType<ManagerOption>()
                .FirstOrDefault(o => o.UserId == Result.ManagerUserId)
                ?? _managerBox.Items[0];
            root.Controls.Add(_managerBox);

            _statusBox = new ComboBox
            {
                Width = 350,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _statusBox.Items.AddRange(new object[] { "Active", "Inactive" });
            _statusBox.SelectedItem = string.Equals(Result.Status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

            if (_isEditMode)
            {
                root.Controls.Add(BranchUi.CreateFieldLabel("STATUS"));
                root.Controls.Add(_statusBox);
            }

            _errorLabel = BranchUi.CreateMessageLabel();
            root.Controls.Add(_errorLabel);

            var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };

            var cancelButton = BranchUi.CreateButton("Cancel", false, 120);
            cancelButton.DialogResult = DialogResult.Cancel;

            var saveButton = BranchUi.CreateButton(_isEditMode ? "Save Changes" : "Add Branch", true, 150);
            saveButton.Click += SaveButton_Click;

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(saveButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            string name = _nameBox.Text.Trim();

            if (name.Length == 0)
            {
                _errorLabel.Text = "Branch name is required.";
                return;
            }

            Result.BranchName = name;
            Result.Address = _addressBox.Text.Trim();
            Result.Status = _isEditMode ? (_statusBox.SelectedItem?.ToString() ?? "Active") : "Active";
            Result.ManagerUserId = (_managerBox.SelectedItem as ManagerOption)?.UserId;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
