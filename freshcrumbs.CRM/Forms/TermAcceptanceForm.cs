using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    // Shown to a tenant user at login when the current Terms & Conditions still need to be accepted.
    public class TermsAcceptanceForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly int _companyId;
        private readonly TenantTermsModel _terms;

        private CheckBox _agreeBox = null!;
        private Button _acceptButton = null!;
        private Label _errorLabel = null!;

        public TermsAcceptanceForm(int companyId, TenantTermsModel terms)
        {
            _companyId = companyId;
            _terms = terms;

            Text = "Terms & Conditions";
            Width = 760;
            Height = 700;
            MinimumSize = new Size(640, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            ControlBox = false;

            Controls.Add(new Label
            {
                Text = terms.Title,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 35, 25),
                Location = new Point(25, 18),
                Size = new Size(690, 32),
                AutoEllipsis = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });

            Controls.Add(new Label
            {
                Text = $"Version {terms.VersionNumber}" +
                       (terms.PublishedAt != null ? $"  |  Published {terms.PublishedAt.Value:yyyy-MM-dd}" : "") +
                       "  |  Please read and accept to continue.",
                Location = new Point(25, 54),
                Size = new Size(690, 22),
                ForeColor = Color.FromArgb(120, 110, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });

            Controls.Add(new TextBox
            {
                Location = new Point(25, 85),
                Size = new Size(690, 450),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = terms.Content.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            });

            _agreeBox = new CheckBox
            {
                Text = "I have read and accept these Terms & Conditions on behalf of my company",
                Location = new Point(25, 548),
                Size = new Size(690, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _agreeBox.CheckedChanged += (s, e) => _acceptButton.Enabled = _agreeBox.Checked;
            Controls.Add(_agreeBox);

            _errorLabel = new Label
            {
                Location = new Point(25, 578),
                Size = new Size(690, 22),
                ForeColor = Color.Firebrick,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_errorLabel);

            _acceptButton = new Button
            {
                Text = "Accept",
                Location = new Point(25, 610),
                Width = 120,
                Height = 38,
                Enabled = false,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(210, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            _acceptButton.FlatAppearance.BorderSize = 0;
            _acceptButton.Click += AcceptButton_Click;
            Controls.Add(_acceptButton);

            var decline = new Button
            {
                Text = "Decline",
                Location = new Point(155, 610),
                Width = 120,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(120, 110, 100),
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                DialogResult = DialogResult.Cancel
            };
            decline.FlatAppearance.BorderColor = Color.FromArgb(220, 210, 200);
            Controls.Add(decline);

            CancelButton = decline;
        }

        private async void AcceptButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";
            _acceptButton.Enabled = false;

            try
            {
                await _apiService.AcceptTenantTermsAsync(_companyId, _terms.TermsVersionId);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = AdminUi.GetMessage(ex);
                _acceptButton.Enabled = _agreeBox.Checked;
            }
        }
    }
}