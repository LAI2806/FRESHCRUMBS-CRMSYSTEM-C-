using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    internal class KpiCard : Panel
    {
        private static readonly Color NormalBack = Color.White;
        private static readonly Color HoverBack = Color.FromArgb(248, 240, 232);
        private static readonly Color BorderColor = Color.FromArgb(225, 210, 200);

        private readonly Color _accent;
        private readonly bool _clickable;
        private readonly Label _value;
        private readonly Label _caption;

        public event EventHandler? CardClicked;

        public KpiCard(string title, Color accent, bool clickable)
        {
            _accent = accent;
            _clickable = clickable;

            DoubleBuffered = true;
            Height = 104;
            Margin = new Padding(0, 0, 12, 12);
            BackColor = NormalBack;

            Controls.Add(new Label
            {
                Text = title,
                AutoSize = false,
                Location = new Point(18, 10),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(120, 110, 100),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            });

            _value = new Label
            {
                Text = "-",
                AutoSize = false,
                Location = new Point(18, 32),
                Size = new Size(200, 40),
                Font = new Font("Segoe UI", 21, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 35, 25),
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_value);

            _caption = new Label
            {
                Text = string.Empty,
                AutoSize = false,
                Location = new Point(18, 74),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 130, 120),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            Controls.Add(_caption);

            Resize += (s, e) =>
            {
                foreach (Control child in Controls)
                {
                    child.Width = Math.Max(40, Width - 30);
                }
            };

            if (clickable)
            {
                Cursor = Cursors.Hand;
                Hook(this);
                Click += (s, e) => CardClicked?.Invoke(this, EventArgs.Empty);
                MouseEnter += (s, e) => BackColor = HoverBack;
                MouseLeave += (s, e) =>
                {
                    if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    {
                        BackColor = NormalBack;
                    }
                };
            }
        }

        private void Hook(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                child.Cursor = Cursors.Hand;
                child.Click += (s, e) => CardClicked?.Invoke(this, EventArgs.Empty);
                child.MouseEnter += (s, e) => BackColor = HoverBack;
                Hook(child);
            }
        }

        public void SetValue(string value, string caption)
        {
            _value.Text = value;
            _caption.Text = caption;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using var borderPen = new Pen(BorderColor);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            using var accentBrush = new SolidBrush(_accent);
            e.Graphics.FillRectangle(accentBrush, 0, 0, 4, Height);
        }
    }

    public class SuperAdminDashboardControl : UserControl
    {
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color BorderColor = Color.FromArgb(225, 210, 200);

        private readonly ApiService _apiService = new();
        private readonly Dictionary<string, KpiCard> _kpis = new();
        private readonly List<Control> _fullWidth = new();

        private Panel _scroll = null!;
        private FlowLayoutPanel _flow = null!;
        private FlowLayoutPanel _kpiFlow = null!;
        private Label _statusLabel = null!;
        private Button _refreshButton = null!;

        private LineChartPanel _growthChart = null!;
        private ColumnChartPanel _registrationChart = null!;
        private DonutChartPanel _statusDonut = null!;
        private DonutChartPanel _planDonut = null!;
        private LineChartPanel _trendChart = null!;
        private HorizontalBarPanel _popularityBars = null!;
        private HorizontalBarPanel _usageBars = null!;

        private List<PlanDistributionItem> _planSlices = new();
        private List<string> _statusKeys = new();
        private List<UserUsageItem> _usageRows = new();
        private bool _loading;

        public event Action<string, string?, string?>? NavigateRequested;

        public SuperAdminDashboardControl()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            InitializeLayout();

            Load += async (s, e) => await LoadAsync();
        }

        private void InitializeLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PageBg
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var header = new Panel { Dock = DockStyle.Fill };

            header.Controls.Add(new Label
            {
                Text = "Business Intelligence",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Location = new Point(0, 8),
                AutoSize = true
            });

            _refreshButton = new Button
            {
                Text = "Refresh",
                Width = 96,
                Height = 32,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(700, 14),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat
            };
            _refreshButton.FlatAppearance.BorderColor = BorderColor;
            _refreshButton.Click += async (s, e) => await LoadAsync();
            header.Controls.Add(_refreshButton);
            header.Resize += (s, e) => _refreshButton.Left = header.Width - _refreshButton.Width;

            _statusLabel = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(420, 22),
                Font = new Font("Segoe UI", 9.5f)
            };
            header.Controls.Add(_statusLabel);
            header.Resize += (s, e) => _statusLabel.Left = Math.Max(300, _refreshButton.Left - _statusLabel.Width - 12);

            root.Controls.Add(header, 0, 0);

            _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = PageBg };

            _flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = PageBg,
                Margin = new Padding(0)
            };

            BuildKpis();
            BuildCharts();

            _scroll.Controls.Add(_flow);
            _scroll.Resize += (s, e) => ResizeContent();
            root.Controls.Add(_scroll, 0, 1);

            Controls.Add(root);
            ResizeContent();
        }

        private void AddKpi(string key, string title, Color accent, string? navigateKey, string? status = null)
        {
            var card = new KpiCard(title, accent, navigateKey != null);

            if (navigateKey != null)
            {
                card.CardClicked += (s, e) => NavigateRequested?.Invoke(navigateKey, status, null);
            }

            _kpis[key] = card;
            _kpiFlow.Controls.Add(card);
        }

        private void BuildKpis()
        {
            _kpiFlow = new FlowLayoutPanel
            {
                Height = 232,
                WrapContents = true,
                BackColor = PageBg,
                Margin = new Padding(0)
            };

            AddKpi("tenants", "Total Tenants", AdminUi.Accent, "tenants");
            AddKpi("subscribers", "Total Subscribers", AdminUi.Steel, "subscribers");
            AddKpi("active", "Active Subscriptions", AdminUi.Green, "subscribers", "Active");
            AddKpi("ended", "Expired / Cancelled", AdminUi.Red, "subscribers", "Expired / Cancelled");
            AddKpi("popular", "Most Popular Plan", AdminUi.Brown, "plans");
            AddKpi("new", "New Subscribers", AdminUi.Accent, null);
            AddKpi("value", "Monthly Recurring Value", AdminUi.Green, null);
            AddKpi("expiring", "Expiring Soon", AdminUi.Brown, null);

            _fullWidth.Add(_kpiFlow);
            _flow.Controls.Add(_kpiFlow);
        }

        private Panel CreateChartCard(string title, string subtitle, Control chart)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(14, 10, 14, 12),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 12, 12)
            };

            chart.Dock = DockStyle.Fill;

            card.Controls.Add(chart);
            card.Controls.Add(new Label
            {
                Text = subtitle,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 130, 120)
            });
            card.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = TextDark
            });

            return card;
        }

        private void BuildCharts()
        {
            var grid = new TableLayoutPanel
            {
                Height = 1260,
                ColumnCount = 2,
                RowCount = 4,
                BackColor = PageBg,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));

            _growthChart = new LineChartPanel();
            grid.Controls.Add(CreateChartCard("Subscriber Growth Over Time", "Total tenants registered, last 12 months", _growthChart), 0, 0);

            _registrationChart = new ColumnChartPanel();
            grid.Controls.Add(CreateChartCard("New Tenant Registrations", "Tenants registered per month", _registrationChart), 1, 0);

            _statusDonut = new DonutChartPanel();
            _statusDonut.SliceClicked += index =>
            {
                if (index >= 0 && index < _statusKeys.Count)
                {
                    NavigateRequested?.Invoke("subscribers", _statusKeys[index], null);
                }
            };
            grid.Controls.Add(CreateChartCard("Active vs Expired / Cancelled", "Current subscription status. Click a status to filter Subscribers", _statusDonut), 0, 1);

            _planDonut = new DonutChartPanel();
            _planDonut.SliceClicked += index =>
            {
                if (index >= 0 && index < _planSlices.Count)
                {
                    NavigateRequested?.Invoke("subscribers", null, _planSlices[index].PlanName);
                }
            };
            grid.Controls.Add(CreateChartCard("Subscription Distribution by Plan", "Active tenants per plan. Click a plan to see its subscribers", _planDonut), 1, 1);

            _trendChart = new LineChartPanel();
            grid.Controls.Add(CreateChartCard("Subscription Status Trends", "Tenants with a subscription in force vs ended, by month", _trendChart), 0, 2);

            _popularityBars = new HorizontalBarPanel();
            _popularityBars.ItemClicked += index => NavigateRequested?.Invoke("plans", null, null);
            grid.Controls.Add(CreateChartCard("Plan Popularity", "Times each plan was chosen (new, upgrade or downgrade)", _popularityBars), 1, 2);

            _usageBars = new HorizontalBarPanel();
            _usageBars.ItemClicked += index =>
            {
                if (index >= 0 && index < _usageRows.Count)
                {
                    NavigateRequested?.Invoke("subscribers", null, _usageRows[index].CompanyName);
                }
            };
            var usageCard = CreateChartCard("User Usage vs Plan Limits", "Share of each plan's user limit in use. Top 8 tenants. Click a tenant to open it in Subscribers", _usageBars);
            usageCard.Margin = new Padding(0, 0, 0, 12);
            grid.Controls.Add(usageCard, 0, 3);
            grid.SetColumnSpan(usageCard, 2);

            _fullWidth.Add(grid);
            _flow.Controls.Add(grid);
        }

        private void ResizeContent()
        {
            int width = Math.Max(720, _scroll.ClientSize.Width - 6);

            foreach (var control in _fullWidth)
            {
                control.Width = width;
            }

            int cardWidth = (width - 3 * 12) / 4;

            foreach (var card in _kpis.Values)
            {
                card.Width = cardWidth;
            }
        }

        private async Task LoadAsync()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            _refreshButton.Enabled = false;
            AdminUi.ShowInfo(_statusLabel, "Loading...");

            try
            {
                var bi = await _apiService.GetPlatformBiAsync(30);
                Render(bi);
                AdminUi.ShowInfo(_statusLabel, $"Updated {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex) when (AdminUi.IsAccessDenied(ex))
            {
                Controls.Clear();
                Controls.Add(new StatePanel("Access denied", ex.Message));
            }
            catch (Exception ex)
            {
                AdminUi.ShowError(_statusLabel, ex);
            }
            finally
            {
                _loading = false;
                _refreshButton.Enabled = true;
            }
        }

        private void Render(PlatformBiModel bi)
        {
            var status = bi.SubscriptionStatus;
            var topPlan = bi.PlanDistribution.FirstOrDefault();

            _kpis["tenants"].SetValue(bi.TotalCompanies.ToString(), $"{bi.ActiveCompanies} active");
            _kpis["subscribers"].SetValue(bi.TotalSubscribers.ToString(), "with a subscription");
            _kpis["active"].SetValue(status.Active.ToString(), status.Suspended > 0 ? $"{status.Suspended} suspended" : "in force");
            _kpis["ended"].SetValue((status.Expired + status.Cancelled).ToString(), $"Expired {status.Expired} · Cancelled {status.Cancelled}");
            _kpis["popular"].SetValue(topPlan?.PlanName ?? "-", topPlan == null ? "no active subscriptions" : $"{topPlan.Count} active tenant(s)");
            _kpis["new"].SetValue(bi.NewSubscribers.ToString(), "last 30 days");
            _kpis["value"].SetValue("₱" + bi.MonthlyRecurringValue.ToString("N0"), "from active plan prices, per month");
            _kpis["expiring"].SetValue(bi.ExpiringSoon.Count.ToString(), "within 30 days");

            var labels = bi.Months.Select(m => m.ShortMonth).ToList();

            _growthChart.SetData(labels, new List<ChartSeries>
            {
                new() { Name = "Tenants", Color = AdminUi.Accent, Values = bi.Months.Select(m => m.TotalTenants).ToList() }
            }, "No tenants registered yet.");

            _registrationChart.SetData(labels, bi.Months.Select(m => m.NewTenants).ToList(), AdminUi.Green, "No registrations in the last 12 months.");

            var statusRows = new List<(string Key, int Value, Color Color)>
            {
                ("Active", status.Active, AdminUi.Green),
                ("Suspended", status.Suspended, AdminUi.Neutral),
                ("Expired", status.Expired, AdminUi.Brown),
                ("Cancelled", status.Cancelled, AdminUi.Red)
            };
            _statusKeys = statusRows.Select(r => r.Key).ToList();
            _statusDonut.SetData(
                statusRows.Select(r => (r.Key, r.Value, r.Color)).ToList(),
                "tenants", "No subscriptions yet.", true);

            _planSlices = bi.PlanDistribution;
            _planDonut.SetData(
                bi.PlanDistribution
                    .Select((p, i) => (p.PlanName, p.Count, AdminUi.ChartColors[i % AdminUi.ChartColors.Length]))
                    .ToList(),
                "active", "No active subscriptions yet.", true);

            _trendChart.SetData(labels, new List<ChartSeries>
            {
                new() { Name = "Active", Color = AdminUi.Green, Values = bi.Months.Select(m => m.ActiveSubscriptions).ToList() },
                new() { Name = "Expired / Cancelled", Color = AdminUi.Red, Values = bi.Months.Select(m => m.InactiveSubscriptions).ToList() }
            }, "No subscription history yet.");

            _popularityBars.SetData(
                bi.PlanPopularity.Select(p => (p.PlanName, p.Count)),
                "No plan selections yet.", null, true);

            _usageRows = bi.UserUsage.Take(8).ToList();
            _usageBars.SetData(
                _usageRows.Select(u => ($"{u.CompanyName} ({u.ActiveUsers}/{u.MaxUsers})", u.PercentUsed)),
                "No tenant has a subscription in force.", value => value + "%", true);
        }
    }
}