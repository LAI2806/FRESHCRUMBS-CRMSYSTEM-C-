using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class SubscriberDetailsForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly int _companyId;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private Label _titleLabel = null!;
        private Label _companyInfoLabel = null!;
        private Label _subscriptionInfoLabel = null!;
        private DataGridView _historyGrid = null!;
        private Button _changePlanButton = null!;
        private Button _renewButton = null!;
        private Button _cancelButton = null!;
        private Button _suspendButton = null!;
        private Label _statusLabel = null!;

        private SubscriberDetailModel? _detail;

        public SubscriberDetailsForm(int companyId)
        {
            _companyId = companyId;

            Text = "Subscriber Details";
            Width = 960;
            Height = 800;
            MinimumSize = new Size(900, 700);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            InitializeControls();

            Load += async (s, e) => await LoadDetailAsync();
        }

        private void InitializeControls()
        {
            _titleLabel = new Label
            {
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(25, 15),
                AutoSize = true,
                Text = "Subscriber"
            };
            Controls.Add(_titleLabel);

            _companyInfoLabel = new Label
            {
                Location = new Point(25, 55),
                Size = new Size(890, 90),
                ForeColor = TextDark,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_companyInfoLabel);

            Controls.Add(new Label
            {
                Text = "Current Subscription",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(25, 150),
                AutoSize = true
            });

            _subscriptionInfoLabel = new Label
            {
                Location = new Point(25, 178),
                Size = new Size(890, 150),
                ForeColor = TextDark,
                BackColor = HeaderRowColor,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_subscriptionInfoLabel);

            _changePlanButton = CreateButton("Change Plan", 25, 340, 130, AccentColor, Color.White);
            _changePlanButton.Click += (s, e) => RunAction(SubscriptionAction.ChangePlan);

            _renewButton = CreateButton("Renew", 165, 340, 100, Color.White, LabelGray);
            _renewButton.Click += (s, e) => RunAction(SubscriptionAction.Renew);

            _cancelButton = CreateButton("Cancel Subscription", 275, 340, 160, Color.White, Color.Firebrick);
            _cancelButton.Click += (s, e) => RunAction(SubscriptionAction.Cancel);

            _suspendButton = CreateButton("Suspend", 445, 340, 110, Color.White, LabelGray);
            _suspendButton.Click += SuspendButton_Click;

            Controls.Add(new Label
            {
                Text = "Subscription History",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(25, 395),
                AutoSize = true
            });

            _historyGrid = new DataGridView
            {
                Location = new Point(25, 425),
                Size = new Size(890, 270),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 36,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _historyGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _historyGrid.ColumnHeadersDefaultCellStyle.ForeColor = LabelGray;
            _historyGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = HeaderRowColor;
            _historyGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(248, 240, 232);
            _historyGrid.DefaultCellStyle.SelectionForeColor = TextDark;

            AddColumn("StartText", "Start (UTC)", 11);
            AddColumn("EndText", "End (UTC)", 11);
            AddColumn("PlanName", "Plan", 14);
            AddColumn("PriceText", "Price", 14);
            AddColumn("MaxUsers", "Users", 6);
            AddColumn("ChangeType", "Change", 10);
            AddColumn("Status", "Status", 10);
            AddColumn("ChangedBy", "Changed By", 10);
            AddColumn("Reason", "Reason", 18);
            Controls.Add(_historyGrid);

            _statusLabel = new Label
            {
                Location = new Point(25, 705),
                Size = new Size(700, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.Firebrick
            };
            Controls.Add(_statusLabel);

            var closeButton = CreateButton("Close", 805, 702, 110, Color.White, LabelGray);
            closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            closeButton.Click += (s, e) => Close();
        }

        private Button CreateButton(string text, int x, int y, int width, Color back, Color fore)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Width = width,
                Height = 36,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderSize = back == AccentColor ? 0 : 1;
            button.FlatAppearance.BorderColor = fore == Color.Firebrick ? Color.Firebrick : BorderColor;
            Controls.Add(button);
            return button;
        }

        private void AddColumn(string property, string header, float weight)
        {
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                HeaderText = header,
                FillWeight = weight
            });
        }

        private async Task LoadDetailAsync()
        {
            AdminUi.ShowInfo(_statusLabel, "Loading subscriber...");

            try
            {
                _detail = await _apiService.GetSubscriberAsync(_companyId);
                Render();
                _statusLabel.Text = "";
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_statusLabel, ex);
            }
        }

        private void Render()
        {
            if (_detail == null)
            {
                return;
            }

            var company = _detail.Company;
            var sub = _detail.CurrentSubscription;

            _titleLabel.Text = $"{company.CompanyName}  ({company.CompanyCode})";

            _companyInfoLabel.Text =
                $"Email: {company.Email}      Contact: {company.ContactNo}\n" +
                $"Address: {company.BusinessAddress}\n" +
                $"Company status: {(company.IsActive ? "Active" : "Inactive")}\n" +
                $"Tenant database: {(_detail.TenantDatabaseProvisioned ? "Provisioned" : "Not provisioned yet (set up manually)")}";

            if (sub == null)
            {
                _subscriptionInfoLabel.Text = "This company has no subscription. Use Assign Plan to subscribe it to a plan.";
            }
            else
            {
                string usersText = $"{_detail.ActiveUsers} / {sub.MaxUsers} Users";

                _subscriptionInfoLabel.Text =
                    $"Plan: {sub.PlanName} ({sub.PlanCode})      Status: {sub.Status}\n" +
                    $"Period: {sub.StartText} to {sub.EndText} (UTC)      Price: {sub.PriceText}\n" +
                    $"Usage: {usersText}\n" +
                    $"Branching: {sub.BranchingText}\n" +
                    $"Included features: {sub.FeaturesText}";
            }

            string status = sub?.Status ?? "None";
            bool inForce = status == "Active" || status == "Suspended";

            _changePlanButton.Text = inForce ? "Change Plan" : "Assign Plan";
            _renewButton.Enabled = sub != null && status != "Cancelled" && status != "Scheduled";
            _cancelButton.Enabled = inForce;
            _suspendButton.Enabled = inForce;
            _suspendButton.Text = status == "Suspended" ? "Reactivate" : "Suspend";

            _historyGrid.DataSource = _detail.History;
            _historyGrid.ClearSelection();
        }

        private async void RunAction(SubscriptionAction action)
        {
            if (_detail == null)
            {
                return;
            }

            var sub = _detail.CurrentSubscription;
            string status = sub?.Status ?? "None";
            bool inForce = status == "Active" || status == "Suspended";

            using var form = new SubscriptionActionForm(
                action,
                _companyId,
                _detail.Company.CompanyName,
                inForce ? sub?.PlanCode : null,
                inForce);

            if (form.ShowDialog(this) == DialogResult.OK)
            {
                await LoadDetailAsync();
                AdminUi.ShowSuccess(_statusLabel, action switch
                {
                    SubscriptionAction.ChangePlan => "The plan was changed. The previous subscription is kept in the history.",
                    SubscriptionAction.Renew => "The subscription was renewed.",
                    _ => "The subscription was cancelled. Business data was not deleted."
                });
            }
        }

        private async void SuspendButton_Click(object? sender, EventArgs e)
        {
            if (_detail?.CurrentSubscription == null)
            {
                return;
            }

            bool suspend = _detail.CurrentSubscription.Status != "Suspended";
            string message = suspend
                ? "Suspend this subscription? The company loses access to subscription-protected features until it is reactivated."
                : "Reactivate this subscription?";

            if (MessageBox.Show(message, suspend ? "Suspend" : "Reactivate",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                await _apiService.SetSubscriptionSuspendedAsync(_companyId, suspend);
                await LoadDetailAsync();
                AdminUi.ShowSuccess(_statusLabel, suspend ? "The subscription was suspended." : "The subscription was reactivated.");
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_statusLabel, ex);
            }
        }
    }
}