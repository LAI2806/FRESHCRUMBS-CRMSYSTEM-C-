using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace freshcrumbs.CRM.winforms.Forms
{
    // ADMIN: which branch each employee works at.
    public class BranchAssignmentsForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly int _companyId;
        private readonly List<BranchModel> _branches;

        private DataGridView _grid = null!;
        private ComboBox _branchBox = null!;
        private Label _messageLabel = null!;

        public BranchAssignmentsForm(int companyId, List<BranchModel> branches)
        {
            _companyId = companyId;
            _branches = branches;

            Text = "Employee Branch Assignments";
            Width = 820;
            Height = 560;
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(20);

            InitializeControls();
            Load += async (s, e) => await LoadAssignmentsAsync();
        }

        private void InitializeControls()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(BranchUi.CreateTitle("Employee Branch Assignments", 15), 0, 0);

            _grid = BranchUi.CreateGrid();
            root.Controls.Add(_grid, 0, 1);

            var assignPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };

            assignPanel.Controls.Add(new Label
            {
                Text = "Branch for the selected employee:",
                AutoSize = true,
                ForeColor = BranchUi.LabelGray,
                Margin = new Padding(0, 10, 10, 0)
            });

            var options = new List<BranchOption> { new BranchOption { BranchId = null, Name = "(Not assigned)" } };
            options.AddRange(_branches
                .Where(b => string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .Select(b => new BranchOption { BranchId = b.BranchId, Name = b.BranchName }));

            _branchBox = new ComboBox
            {
                Width = 240,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                DataSource = options,
                DisplayMember = "Name",
                Margin = new Padding(0, 5, 10, 0)
            };
            assignPanel.Controls.Add(_branchBox);

            var assignButton = BranchUi.CreateButton("Assign", true, 120);
            assignButton.Click += async (s, e) => await AssignAsync();
            assignPanel.Controls.Add(assignButton);

            root.Controls.Add(assignPanel, 0, 2);

            _messageLabel = BranchUi.CreateMessageLabel();
            root.Controls.Add(_messageLabel, 0, 3);

            Controls.Add(root);
        }

        private async Task LoadAssignmentsAsync()
        {
            try
            {
                var rows = await _apiService.GetBranchAssignmentsAsync(_companyId);

                foreach (var row in rows)
                {
                    row.BranchName ??= "Not assigned";
                }

                _grid.DataSource = null;
                _grid.DataSource = rows;
                BranchUi.ShowColumns(_grid,
                    ("FullName", "Name"),
                    ("UserName", "Username"),
                    ("Role", "Role"),
                    ("Status", "Status"),
                    ("BranchName", "Branch"));

                if (rows.Count == 0)
                {
                    ShowMessage("No employee accounts are available. Sync once while online, then reopen this window.", true);
                }
            }
            catch (Exception ex)
            {
                ShowMessage(BranchUi.GetMessage(ex), true);
            }
        }

        private async Task AssignAsync()
        {
            if (_grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].DataBoundItem is not BranchAssignmentModel row)
            {
                ShowMessage("Select an employee first.", true);
                return;
            }

            if (_branchBox.SelectedItem is not BranchOption option)
            {
                return;
            }

            try
            {
                await _apiService.SetBranchAssignmentAsync(_companyId, row.UserId, option.BranchId);
                await LoadAssignmentsAsync();
                ShowMessage($"{row.UserName}: {option.Name}.", false);
            }
            catch (Exception ex)
            {
                ShowMessage(BranchUi.GetMessage(ex), true);
            }
        }

        private void ShowMessage(string message, bool isError)
        {
            _messageLabel.ForeColor = isError ? Color.Firebrick : Color.FromArgb(46, 130, 80);
            _messageLabel.Text = message;
        }

        private class BranchOption
        {
            public int? BranchId { get; set; }

            public string Name { get; set; } = string.Empty;
        }
    }
}
