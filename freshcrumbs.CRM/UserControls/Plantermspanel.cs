using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class PlanOption
    {
        public PlanModel Plan { get; }

        public PlanOption(PlanModel plan)
        {
            Plan = plan;
        }

        public override string ToString() => $"{Plan.DisplayName} ({Plan.PlanCode})";
    }

    // Read-only preview of the terms a plan provides, so they never have to be retyped.
    public class PlanTermsPanel : Panel
    {
        private readonly Label _body;

        public PlanTermsPanel()
        {
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Color.FromArgb(250, 246, 242);
            Padding = new Padding(10);

            _body = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(50, 35, 25)
            };
            Controls.Add(_body);

            ShowPlan(null);
        }

        public void ShowPlan(PlanModel? plan)
        {
            if (plan == null)
            {
                _body.Text = "Select a plan to see its terms.";
                return;
            }

            _body.Text =
                $"Price:  {plan.Price:N2} / {plan.BillingCycle}\n" +
                $"Maximum users:  {plan.MaxUsers}\n" +
                $"Branching:  {(plan.BranchingEnabled ? $"Enabled (up to {plan.MaxBranches} branches)" : "Not included")}\n" +
                $"Features:  {plan.FeaturesText}";
        }
    }
}