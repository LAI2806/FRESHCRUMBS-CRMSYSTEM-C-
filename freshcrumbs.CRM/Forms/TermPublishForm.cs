using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class TermsPublishForm : Form
    {
        private CheckBox _requireBox = null!;

        public bool RequiresAcceptance => _requireBox.Checked;

        public TermsPublishForm(TermsVersionModel draft, TermsVersionModel? currentlyPublished)
        {
            Text = "Publish Terms";
            Width = 520;
            Height = 340;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(new Label
            {
                Text = $"Publish {draft.VersionText}?",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 35, 25),
                Location = new Point(25, 18),
                AutoSize = true
            });

            string archiveNote = currentlyPublished == null
                ? "There is no published version at the moment."
                : $"The currently published version ({currentlyPublished.VersionText}) will be archived and kept as history.";

            Controls.Add(new Label
            {
                Text = $"\"{draft.Title}\"\n\nOnce published, this version can never be edited or deleted. " +
                       "To change it later you create a new version.\n\n" + archiveNote,
                Location = new Point(25, 62),
                Size = new Size(460, 130),
                ForeColor = Color.FromArgb(90, 80, 70)
            });

            _requireBox = new CheckBox
            {
                Text = "Require every company to accept this version before using the CRM",
                Location = new Point(25, 200),
                Size = new Size(460, 24),
                Checked = true
            };
            Controls.Add(_requireBox);

            var publish = new Button
            {
                Text = "Publish",
                Location = new Point(25, 245),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(210, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            publish.FlatAppearance.BorderSize = 0;
            Controls.Add(publish);

            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(145, 245),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(120, 110, 100),
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(220, 210, 200);
            Controls.Add(cancel);

            AcceptButton = publish;
            CancelButton = cancel;
        }
    }
}