using freshcrumbs.CRM.winforms.Services;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace freshcrumbs.CRM.winforms.Forms
{
    // Asks for the quantity of a branch Stock In / Stock Out / Allocate. The API validates it again.
    public class StockQuantityForm : Form
    {
        public int Quantity { get; private set; }

        private readonly NumericUpDown _quantityBox;

        public StockQuantityForm(string title, string details, int? maximum = null)
        {
            Text = title;
            Width = 400;
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

            root.Controls.Add(BranchUi.CreateTitle(title, 13));

            root.Controls.Add(new Label
            {
                Text = details,
                Font = new Font("Segoe UI", 10),
                ForeColor = BranchUi.TextDark,
                AutoSize = true,
                MaximumSize = new Size(330, 0),
                Margin = new Padding(0, 0, 0, 14)
            });

            root.Controls.Add(BranchUi.CreateFieldLabel("QUANTITY"));

            int max = Math.Max(1, Math.Min(maximum ?? 99999, 99999));

            _quantityBox = new NumericUpDown
            {
                Width = 330,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 1,
                Maximum = max,
                Value = 1,
                Margin = new Padding(0, 0, 0, 16)
            };
            root.Controls.Add(_quantityBox);

            var buttons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };

            var cancelButton = BranchUi.CreateButton("Cancel", false, 120);
            cancelButton.DialogResult = DialogResult.Cancel;

            var okButton = BranchUi.CreateButton("Confirm", true, 140);
            okButton.Click += (s, e) =>
            {
                Quantity = (int)_quantityBox.Value;
                DialogResult = DialogResult.OK;
                Close();
            };

            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(okButton);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }
    }
}
