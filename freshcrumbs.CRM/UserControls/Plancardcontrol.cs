using System.Drawing.Drawing2D;
using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.UserControls
{
    // One reusable card; every plan on the Plan Management page is an instance of this control.
    public class PlanCardControl : UserControl
    {
        public static readonly Color[] TierColors =
        {
            Color.FromArgb(90, 150, 120),
            Color.FromArgb(210, 140, 60),
            Color.FromArgb(125, 95, 170),
            Color.FromArgb(70, 130, 180)
        };

        private static readonly Color InactiveAccent = Color.FromArgb(170, 165, 160);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color EnabledColor = Color.FromArgb(46, 160, 90);
        private static readonly Color DisabledColor = Color.FromArgb(200, 80, 80);
        private static readonly Color DisabledText = Color.FromArgb(170, 165, 160);

        private const int CornerRadius = 12;

        private readonly Color _accent;
        private readonly Panel _surface;
        private bool _isSelected;

        public PlanModel Plan { get; }

        public bool IsRecommended { get; }

        public event EventHandler? CustomizeClicked;

        public event EventHandler? CardClicked;

        public event EventHandler? CardDoubleClicked;

        public PlanCardControl(PlanModel plan, Color tierColor, bool isRecommended = false)
        {
            Plan = plan;
            IsRecommended = isRecommended;
            _accent = plan.IsActive ? tierColor : InactiveAccent;

            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            Margin = new Padding(12);
            BackColor = BorderColor;
            Padding = new Padding(1);

            _surface = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Controls.Add(_surface);

            BuildContent();

            _surface.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 6, BackColor = _accent });

            HookClicks(_surface);
            Click += (s, e) => CardClicked?.Invoke(this, EventArgs.Empty);
            DoubleClick += (s, e) => CardDoubleClicked?.Invoke(this, EventArgs.Empty);
            _surface.Click += (s, e) => CardClicked?.Invoke(this, EventArgs.Empty);
            _surface.DoubleClick += (s, e) => CardDoubleClicked?.Invoke(this, EventArgs.Empty);

            SizeChanged += (s, e) => Region = CreateRoundedRegion(Size, CornerRadius);
            _surface.SizeChanged += (s, e) => _surface.Region = CreateRoundedRegion(_surface.Size, CornerRadius - 1);
        }

        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                BackColor = value ? _accent : BorderColor;
                Padding = new Padding(value ? 2 : 1);
            }
        }

        private void BuildContent()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(22, 14, 22, 18),
                BackColor = Color.White
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

            layout.Controls.Add(BuildHeader(), 0, 0);
            layout.Controls.Add(BuildPrice(), 0, 1);
            layout.Controls.Add(BuildDescription(), 0, 2);

            layout.Controls.Add(new Label
            {
                Text = "Included Features",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 3);

            layout.Controls.Add(BuildFeatureList(), 0, 4);
            layout.Controls.Add(BuildFooter(), 0, 5);

            _surface.Controls.Add(layout);
        }

        private Control BuildHeader()
        {
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                BackColor = Color.White
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            header.Controls.Add(new Label
            {
                Text = Plan.DisplayName,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var badges = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0),
                BackColor = Color.White
            };

            if (IsRecommended)
            {
                badges.Controls.Add(CreateBadge("Recommended", _accent, Color.White));
            }

            if (!Plan.IsActive)
            {
                badges.Controls.Add(CreateBadge("Inactive", Color.FromArgb(235, 232, 228), LabelGray));
            }

            header.Controls.Add(badges, 1, 0);
            return header;
        }

        private static Label CreateBadge(string text, Color back, Color fore)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                BackColor = back,
                ForeColor = fore,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Padding = new Padding(8, 3, 8, 3),
                Margin = new Padding(4, 0, 0, 0)
            };
        }

        private Control BuildPrice()
        {
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                BackColor = Color.White
            };

            string amount = Plan.Price % 1 == 0 ? Plan.Price.ToString("N0") : Plan.Price.ToString("N2");

            flow.Controls.Add(new Label
            {
                Text = "₱" + amount,
                AutoSize = true,
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = TextDark,
                Margin = new Padding(0, 4, 0, 0)
            });

            flow.Controls.Add(new Label
            {
                Text = "/ " + BillingPeriodText(Plan.BillingCycle),
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = LabelGray,
                Margin = new Padding(4, 24, 0, 0)
            });

            return flow;
        }

        private static string BillingPeriodText(string billingCycle) => billingCycle switch
        {
            "Weekly" => "week",
            "Yearly" => "year",
            _ => "month"
        };

        private Control BuildDescription()
        {
            return new Label
            {
                Text = string.IsNullOrWhiteSpace(Plan.Description) ? "No description provided." : Plan.Description,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = LabelGray,
                AutoEllipsis = true
            };
        }

        private Control BuildFeatureList()
        {
            var rows = PlanFeatureCatalog.ModuleRows;

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rows.Length,
                Margin = new Padding(0),
                BackColor = Color.White
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 26f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            for (int i = 0; i < rows.Length; i++)
            {
                bool enabled = Plan.Features.Contains(rows[i].FeatureKey);

                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));

                table.Controls.Add(new Label
                {
                    Text = enabled ? "✓" : "✗",
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI Symbol", 11, FontStyle.Bold),
                    ForeColor = enabled ? EnabledColor : DisabledColor,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, i);

                table.Controls.Add(new Label
                {
                    Text = rows[i].Label,
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = enabled ? TextDark : DisabledText,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 1, i);
            }

            return table;
        }

        private Control BuildFooter()
        {
            var button = new Button
            {
                Text = "Customize Plan",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = _accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) => CustomizeClicked?.Invoke(this, EventArgs.Empty);
            return button;
        }

        private void HookClicks(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Button)
                {
                    continue;
                }

                child.Click += (s, e) => CardClicked?.Invoke(this, EventArgs.Empty);
                child.DoubleClick += (s, e) => CardDoubleClicked?.Invoke(this, EventArgs.Empty);
                HookClicks(child);
            }
        }

        private static Region CreateRoundedRegion(Size size, int radius)
        {
            int d = radius * 2;

            if (size.Width <= d || size.Height <= d)
            {
                return new Region(new Rectangle(Point.Empty, size));
            }

            var path = new GraphicsPath();
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(size.Width - d, 0, d, d, 270, 90);
            path.AddArc(size.Width - d, size.Height - d, d, d, 0, 90);
            path.AddArc(0, size.Height - d, d, d, 90, 90);
            path.CloseFigure();

            return new Region(path);
        }
    }
}