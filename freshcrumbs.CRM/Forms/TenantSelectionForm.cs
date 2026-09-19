using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class TenantSelectionForm : Form
    {
        private readonly ApiService _apiService;
        private ComboBox _tenantComboBox = null!;
        private Button _continueButton = null!;
        private Label _statusLabel = null!;
        private List<CompanyModel> _companies = new();

        public TenantSelectionForm()
        {
            _apiService = new ApiService();

            InitializeForm();
            InitializeControls();

            Load += TenantSelectionForm_Load;
        }

        private void InitializeForm()
        {
            Text = "FreshCrumbs CRM - Select Company";
            Width = 450;
            Height = 280;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.WhiteSmoke;
        }

        private void InitializeControls()
        {
            var titleLabel = new Label
            {
                Text = "FreshCrumbs CRM",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 40, 30),
                Location = new Point(30, 25),
                AutoSize = true
            };
            Controls.Add(titleLabel);

            var subLabel = new Label
            {
                Text = "Select Company / Tenant",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.DimGray,
                Location = new Point(30, 65),
                AutoSize = true
            };
            Controls.Add(subLabel);

            _tenantComboBox = new ComboBox
            {
                Location = new Point(30, 95),
                Width = 370,
                Height = 30,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _tenantComboBox.SelectedIndexChanged += TenantComboBox_SelectedIndexChanged;
            Controls.Add(_tenantComboBox);

            _continueButton = new Button
            {
                Text = "Continue",
                Location = new Point(30, 140),
                Width = 120,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(210, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _continueButton.FlatAppearance.BorderSize = 0;
            _continueButton.Click += ContinueButton_Click;
            Controls.Add(_continueButton);

            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(30, 190),
                Width = 370,
                ForeColor = Color.Firebrick,
                Font = new Font("Segoe UI", 9)
            };
            Controls.Add(_statusLabel);
        }

        private async void TenantSelectionForm_Load(object? sender, EventArgs e)
        {
            try
            {
                _companies = await _apiService.GetCompaniesAsync();

                _tenantComboBox.DataSource = _companies;
                _tenantComboBox.DisplayMember = "CompanyName";
                _tenantComboBox.ValueMember = "CompanyId";
                _tenantComboBox.SelectedIndex = -1;

                if (_companies.Count == 0)
                {
                    _statusLabel.Text = "No companies found. Please create one first.";
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load companies: {ex.Message}";
            }
        }

        private void TenantComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _continueButton.Enabled = _tenantComboBox.SelectedItem != null;
            _statusLabel.Text = "";
        }

        private void ContinueButton_Click(object? sender, EventArgs e)
        {
            if (_tenantComboBox.SelectedItem is not CompanyModel selectedCompany)
            {
                _statusLabel.Text = "Please select a company before continuing.";
                return;
            }

            var mainForm = new MainCrmForm(selectedCompany);
            mainForm.Show();
            Hide();

            mainForm.FormClosed += (s, args) => Close();
        }
    }
}