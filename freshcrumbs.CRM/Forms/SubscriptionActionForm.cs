using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using freshcrumbs.CRM.winforms.UserControls;

namespace freshcrumbs.CRM.winforms.Forms
{
    public enum SubscriptionAction
    {
        ChangePlan,
        Renew,
        Cancel
    }

    public class SubscriptionActionForm : Form
    {
        private readonly ApiService _apiService = new();
        private readonly SubscriptionAction _action;
        private readonly int _companyId;
        private readonly string? _currentPlanCode;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private ComboBox _planBox = null!;
        private PlanTermsPanel _termsPanel = null!;
        private DateTimePicker _datePicker = null!;
        private TextBox _reasonBox = null!;
        private Button _confirmButton = null!;
        private Label _errorLabel = null!;

        public SubscriptionActionForm(SubscriptionAction action, int companyId, string companyName, string? currentPlanCode, bool hasCurrent)
        {
            _action = action;
            _companyId = companyId;
            _currentPlanCode = currentPlanCode;

            string title = action switch
            {
                SubscriptionAction.ChangePlan => hasCurrent ? "Change Plan" : "Assign Plan",
                SubscriptionAction.Renew => "Renew Subscription",
                _ => "Cancel Subscription"
            };

            Text = title;
            Width = 520;
            Height = action == SubscriptionAction.ChangePlan ? 640 : 380;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            InitializeControls(title, companyName);

            if (action == SubscriptionAction.ChangePlan)
            {
                Load += async (s, e) => await LoadPlansAsync();
            }
        }

        private void InitializeControls(string title, string companyName)
        {
            Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(30, 20),
                AutoSize = true
            });

            Controls.Add(new Label
            {
                Text = companyName,
                ForeColor = LabelGray,
                Location = new Point(30, 58),
                AutoSize = true
            });

            int y = 90;

            if (_action == SubscriptionAction.ChangePlan)
            {
                Controls.Add(new Label { Text = "New Plan *  (active plans only)", Location = new Point(30, y), AutoSize = true, ForeColor = LabelGray });
                _planBox = new ComboBox
                {
                    Location = new Point(30, y + 22),
                    Width = 440,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                _planBox.SelectedIndexChanged += (s, e) =>
                    _termsPanel.ShowPlan((_planBox.SelectedItem as PlanOption)?.Plan);
                Controls.Add(_planBox);

                y += 62;
                _termsPanel = new PlanTermsPanel { Location = new Point(30, y), Size = new Size(440, 110) };
                Controls.Add(_termsPanel);
                y += 125;
            }

            if (_action != SubscriptionAction.Renew)
            {
                Controls.Add(new Label { Text = "Effective Date", Location = new Point(30, y), AutoSize = true, ForeColor = LabelGray });
                _datePicker = new DateTimePicker
                {
                    Location = new Point(30, y + 22),
                    Width = 200,
                    Format = DateTimePickerFormat.Short,
                    MinDate = DateTime.Today,
                    MaxDate = DateTime.Today.AddDays(365),
                    Value = DateTime.Today
                };
                Controls.Add(_datePicker);
                y += 62;
            }
            else
            {
                Controls.Add(new Label
                {
                    Text = "The subscription is renewed with its current terms, starting when it expires (or now if it already expired).",
                    Location = new Point(30, y),
                    Size = new Size(440, 40),
                    ForeColor = LabelGray
                });
                y += 50;
            }

            Controls.Add(new Label { Text = "Reason (optional)", Location = new Point(30, y), AutoSize = true, ForeColor = LabelGray });
            _reasonBox = new TextBox
            {
                Location = new Point(30, y + 22),
                Width = 440,
                Height = 60,
                Multiline = true,
                MaxLength = 500,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_reasonBox);

            y += 95;
            _errorLabel = new Label
            {
                Location = new Point(30, y),
                Size = new Size(440, 40),
                ForeColor = Color.Firebrick
            };
            Controls.Add(_errorLabel);

            y += 45;
            _confirmButton = new Button
            {
                Text = _action == SubscriptionAction.Cancel ? "Cancel Subscription" : "Confirm",
                Location = new Point(30, y),
                Width = _action == SubscriptionAction.Cancel ? 160 : 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = _action == SubscriptionAction.Cancel ? Color.Firebrick : AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _confirmButton.FlatAppearance.BorderSize = 0;
            _confirmButton.Click += ConfirmButton_Click;
            Controls.Add(_confirmButton);

            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(_confirmButton.Right + 10, y),
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat
            };
            closeButton.FlatAppearance.BorderColor = BorderColor;
            closeButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(closeButton);
        }

        private async Task LoadPlansAsync()
        {
            try
            {
                var plans = await _apiService.GetPlansAsync("Active");

                foreach (var plan in plans.Where(p => p.PlanCode != _currentPlanCode))
                {
                    _planBox.Items.Add(new PlanOption(plan));
                }

                if (_planBox.Items.Count == 0)
                {
                    _errorLabel.Text = "There are no other active plans to choose from.";
                }
            }
            catch (ApiValidationException ex)
            {
                _errorLabel.Text = ex.Message;
            }
            catch (Exception ex)
            {
                _errorLabel.Text = ErrorMessageHelper.GetFriendlyMessage(ex);
            }
        }

        private async void ConfirmButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";
            string reason = _reasonBox.Text.Trim();

            if (_action == SubscriptionAction.ChangePlan && _planBox.SelectedItem is not PlanOption)
            {
                _errorLabel.Text = "Please select the new plan.";
                return;
            }

            if (_action == SubscriptionAction.Cancel &&
                MessageBox.Show(
                    "Cancel this subscription? The company loses access to subscription-protected features from the effective date. " +
                    "Business data is not deleted.",
                    "Cancel Subscription", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            _confirmButton.Enabled = false;

            try
            {
                switch (_action)
                {
                    case SubscriptionAction.ChangePlan:
                        var option = (PlanOption)_planBox.SelectedItem!;
                        await _apiService.ChangeSubscriberPlanAsync(_companyId, option.Plan.PlanId, _datePicker.Value.Date, reason);
                        break;

                    case SubscriptionAction.Renew:
                        await _apiService.RenewSubscriptionAsync(_companyId, reason);
                        break;

                    default:
                        await _apiService.CancelSubscriptionAsync(_companyId, _datePicker.Value.Date, reason);
                        break;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiValidationException ex)
            {
                _errorLabel.Text = ex.Message;
            }
            catch (Exception ex)
            {
                _errorLabel.Text = ErrorMessageHelper.GetFriendlyMessage(ex);
            }
            finally
            {
                _confirmButton.Enabled = true;
            }
        }
    }
}