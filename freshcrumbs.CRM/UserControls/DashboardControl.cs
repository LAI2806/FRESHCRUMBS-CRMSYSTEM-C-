using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class DashboardControl : UserControl
    {
        private const int Gap = 14;
        private const int SummaryCardHeight = 132;
        private const int ChartHeight = 400;
        private const int MaxRangeDays = 3660;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color DarkBrown = Color.FromArgb(60, 40, 30);
        private static readonly Color GridColor = Color.FromArgb(230, 230, 230);
        private static readonly Color AxisColor = Color.FromArgb(190, 180, 170);
        private static readonly Color CardBg = Color.White;
        private static readonly Color LowStockRed = Color.FromArgb(190, 70, 55);
        private static readonly Color PositiveGreen = Color.FromArgb(50, 130, 70);

        private static readonly Color[] SlicePalette =
        {
            Color.FromArgb(210, 140, 60),
            Color.FromArgb(160, 110, 70),
            Color.FromArgb(230, 180, 120),
            Color.FromArgb(120, 90, 60),
            Color.FromArgb(200, 160, 130),
            Color.FromArgb(90, 65, 45),
            Color.FromArgb(185, 120, 50),
            Color.FromArgb(240, 210, 170)
        };

        private static readonly string[] TabNames =
        {
            "Overview",
            "Sales",
            "Customers",
            "Products and Inventory",
            "Promotions and Discounts",
            "Loyalty",
            "Feedback and Inquiries"
        };

        private readonly ApiService _apiService;
        private readonly int _companyId;
        private readonly Action<string>? _navigateToModule;

        private readonly List<Panel> _tabScrolls = new();
        private readonly List<TableLayoutPanel> _tabRoots = new();
        private readonly ToolTip _toolTip = new();

        private TabControl _tabs = null!;
        private DateTimePicker _startPicker = null!;
        private DateTimePicker _endPicker = null!;
        private Button _refreshButton = null!;
        private Label _loadingLabel = null!;
        private Label _statusLabel = null!;

        private bool _suppressFilterEvents;
        private int _loadVersion;

        public DashboardControl(int companyId, Action<string>? navigateToModule = null)
        {
            _companyId = companyId;
            _navigateToModule = navigateToModule;
            _apiService = new ApiService();

            InitializeLayout();

            Load += DashboardControl_Load;
        }

        private sealed record Kpi(
            string Title,
            string Value,
            string ModuleKey,
            bool Warn = false,
            string? Note = null,
            Color? NoteColor = null);

        private sealed record CardItem(Panel Card, int Span);

        private async void DashboardControl_Load(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = PageBg,
                Padding = new Padding(20, 16, 20, 0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            layout.Controls.Add(CreateHeaderPanel(), 0, 0);
            layout.Controls.Add(CreateFilterRow(), 0, 1);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                Multiline = true,
                Padding = new Point(14, 6)
            };

            foreach (string name in TabNames)
            {
                _tabs.TabPages.Add(CreateTabPage(name));
            }

            layout.Controls.Add(_tabs, 0, 2);

            _statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick
            };

            Controls.Add(layout);
            Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            _startPicker.ValueChanged += DatePicker_ValueChanged;
            _endPicker.ValueChanged += DatePicker_ValueChanged;
        }

        private static Panel CreateHeaderPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg
            };

            panel.Controls.Add(new Label
            {
                Text = "Dashboard",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Location = new Point(0, 0)
            });

            panel.Controls.Add(new Label
            {
                Text = "Business analytics and key performance at a glance.",
                Font = new Font("Segoe UI", 10),
                ForeColor = LabelGray,
                AutoSize = true,
                Location = new Point(0, 40)
            });

            return panel;
        }

        private FlowLayoutPanel CreateFilterRow()
        {
            var filterRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = PageBg,
                Padding = new Padding(0, 6, 0, 0)
            };

            filterRow.Controls.Add(CreateFilterLabel("Start Date:"));
            _startPicker = CreateDatePicker();
            filterRow.Controls.Add(_startPicker);

            filterRow.Controls.Add(CreateFilterLabel("End Date:"));
            _endPicker = CreateDatePicker();
            filterRow.Controls.Add(_endPicker);

            var currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            SetDateRange(currentMonthStart, currentMonthStart.AddMonths(1).AddDays(-1));

            _refreshButton = new Button
            {
                Text = "Refresh",
                Width = 100,
                Height = 30,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = DarkBrown,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 12, 0)
            };
            _refreshButton.FlatAppearance.BorderSize = 0;
            _refreshButton.Click += RefreshButton_Click;
            filterRow.Controls.Add(_refreshButton);

            _loadingLabel = new Label
            {
                Text = "Loading dashboard...",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 9, 0, 0)
            };
            filterRow.Controls.Add(_loadingLabel);

            return filterRow;
        }

        private static Label CreateFilterLabel(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 6, 6, 0)
            };
        }

        private static DateTimePicker CreateDatePicker()
        {
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM dd, yyyy",
                Font = new Font("Segoe UI", 10),
                Width = 120,
                MinDate = new DateTime(2000, 1, 1),
                MaxDate = new DateTime(2100, 12, 31),
                Margin = new Padding(0, 2, 16, 0)
            };
        }

        private TabPage CreateTabPage(string name)
        {
            var page = new TabPage(name)
            {
                BackColor = PageBg,
                UseVisualStyleBackColor = false
            };

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                AutoScrollMinSize = new Size(900, 0),
                BackColor = PageBg,
                Padding = new Padding(0, 14, 4, 0)
            };

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = PageBg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            scroll.Controls.Add(root);
            page.Controls.Add(scroll);

            _tabScrolls.Add(scroll);
            _tabRoots.Add(root);

            return page;
        }

        private void SetDateRange(DateTime start, DateTime end)
        {
            _suppressFilterEvents = true;

            try
            {
                _startPicker.Value = start.Date;
                _endPicker.Value = end.Date;
            }
            finally
            {
                _suppressFilterEvents = false;
            }
        }

        private bool TryGetDateRange(out DateTime start, out DateTime end)
        {
            start = _startPicker.Value.Date;
            end = _endPicker.Value.Date;

            if (start > end)
            {
                _statusLabel.Text = "Start date cannot be later than the end date.";
                return false;
            }

            if ((end - start).TotalDays > MaxRangeDays)
            {
                _statusLabel.Text = "The selected date range cannot exceed 10 years.";
                return false;
            }

            return true;
        }

        private async void DatePicker_ValueChanged(object? sender, EventArgs e)
        {
            if (_suppressFilterEvents)
            {
                return;
            }

            await LoadDataAsync();
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            _statusLabel.Text = "";

            if (!TryGetDateRange(out var startDate, out var endDate))
            {
                return;
            }

            int version = ++_loadVersion;
            _loadingLabel.Text = "Loading dashboard...";

            try
            {
                var dashboardTask = _apiService.GetDashboardAsync(_companyId);
                var overviewTask = _apiService.GetReportsOverviewAsync(_companyId, null, null, startDate, endDate);
                var chartsTask = _apiService.GetReportChartsAsync(_companyId, null, null, startDate, endDate);

                await Task.WhenAll(dashboardTask, overviewTask, chartsTask);

                if (version != _loadVersion || IsDisposed)
                {
                    return;
                }

                BuildDashboard(await dashboardTask, await overviewTask, await chartsTask);

                _loadingLabel.Text = "";
            }
            catch (Exception ex)
            {
                if (version != _loadVersion || IsDisposed)
                {
                    return;
                }

                _loadingLabel.Text = "";
                _statusLabel.Text = $"Unable to load dashboard data: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private void BuildDashboard(DashboardModel dashboard, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            SuspendLayout();

            for (int i = 0; i < _tabRoots.Count; i++)
            {
                ClearRoot(_tabRoots[i]);
            }

            BuildOverviewTab(_tabRoots[0], dashboard, overview, charts);
            BuildSalesTab(_tabRoots[1], overview, charts);
            BuildCustomersTab(_tabRoots[2], dashboard, overview, charts);
            BuildProductsTab(_tabRoots[3], dashboard, overview, charts);
            BuildPromotionsTab(_tabRoots[4], overview, charts);
            BuildLoyaltyTab(_tabRoots[5], dashboard, overview, charts);
            BuildFeedbackTab(_tabRoots[6], overview, charts);

            for (int i = 0; i < _tabRoots.Count; i++)
            {
                _tabRoots[i].ResumeLayout(true);
                HookWheelFocus(_tabRoots[i], _tabScrolls[i]);
            }

            ResumeLayout(true);
        }

        private static void ClearRoot(TableLayoutPanel root)
        {
            root.SuspendLayout();

            var old = root.Controls.Cast<Control>().ToList();
            root.Controls.Clear();

            foreach (var control in old)
            {
                control.Dispose();
            }

            root.RowStyles.Clear();
            root.RowCount = 0;
        }

        // ---------------------------------------------------------------- tabs

        private void BuildOverviewTab(TableLayoutPanel root, DashboardModel dashboard, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddSectionLabel(root, "TODAY");
            AddKpiRow(root,
                new Kpi("TOTAL CUSTOMERS", FormatCount(dashboard.ActiveCustomers), "customers"),
                new Kpi("ACTIVE PRODUCTS", FormatCount(dashboard.ActiveProducts), "products"),
                new Kpi("TODAY'S SALES", FormatPeso(dashboard.TodaysSales), "sales"),
                new Kpi("LOW STOCK", FormatCount(dashboard.LowStockCount), "products", dashboard.LowStockCount > 0));

            AddSectionLabel(root, "SALES (SELECTED PERIOD)");
            AddKpiRow(root,
                BuildTotalSalesKpi(overview),
                new Kpi("TRANSACTIONS", FormatCount(overview.TransactionCount), "sales"),
                new Kpi("AVERAGE TRANSACTION", FormatPeso(overview.AverageTransaction), "sales"),
                new Kpi("TOTAL PRODUCTS SOLD", FormatCount(overview.TotalProductsSold), "sales"));

            AddSectionLabel(root, "CUSTOMERS AND LOYALTY");
            AddKpiRow(root,
                new Kpi("NEW CUSTOMERS (LAST 30 DAYS)", FormatCount(overview.NewCustomersLast30Days), "customers"),
                new Kpi("REPEAT CUSTOMER RATE", FormatPercent(overview.RepeatCustomerRatePercent), "customers"),
                new Kpi("TOTAL EARNED POINTS", FormatCount(overview.TotalEarnedPoints), "loyalty"),
                new Kpi("TOTAL USED POINTS", FormatCount(overview.TotalUsedPoints), "loyalty"));

            AddSectionLabel(root, "PROMOTIONS AND SERVICE");
            AddKpiRow(root,
                new Kpi("ACTIVE PROMOTIONS", FormatCount(overview.TotalActivePromotions), "promotions"),
                new Kpi("RESOLUTION RATE", FormatPercent(overview.ResolutionRatePercent), "feedback"),
                new Kpi("RESPONSE RATE", FormatPercent(overview.ResponseRatePercent), "inquiries"),
                new Kpi("RESOLVED COMPLAINTS", FormatCount(overview.TotalResolvedComplaints), "feedback"));

            AddSectionLabel(root, "ANALYTICS");
            AddCardGrid(root,
                ChartItem("Sales Trend (Selected Period)",
                    BuildLineChart("Date", "Final Amount", true, true, ("Sales", charts.SalesTrend, AccentColor)),
                    "sales", 2),
                new CardItem(BuildSalesOverviewCard(dashboard), 1),
                new CardItem(BuildRecentSalesCard(dashboard), 1),
                ChartItem("Top-Selling Products",
                    BuildBarChart("Product", "Quantity Sold", charts.ProductPerformance, false, true), "products"),
                ChartItem("Sales by Product Category",
                    BuildDonutChart(charts.SalesByCategory, true), "products"),
                ChartItem("Customer Growth Trend",
                    BuildLineChart("Date", "Customer Count", false, false, ("Customers", charts.CustomerGrowth, AccentColor)),
                    "customers"),
                ChartItem("New vs. Returning Customers",
                    BuildDonutChart(charts.NewVsReturningCustomers, false), "customers"),
                ChartItem("Sales by Promotion",
                    BuildDonutChart(charts.SalesByPromotion, true), "sales"),
                ChartItem("Discount Amount by Promotion",
                    BuildBarChart("Promotion", "Total Discount Amount", charts.DiscountByPromotion, true, true), "promotions"),
                ChartItem("Top Customers by Loyalty Points",
                    BuildBarChart("Customer", "Current Points", charts.TopLoyalCustomers, false, true), "loyalty"),
                new CardItem(BuildLowStockCard(dashboard), 1),
                ChartItem("Feedback & Inquiry Submissions Over Time",
                    BuildLineChart("Date", "Number of Submissions", false, true,
                        ("Feedback", charts.FeedbackTrend, AccentColor),
                        ("Inquiries", charts.InquiryTrend, Color.FromArgb(90, 65, 45))),
                    "feedback", 2));
        }

        private void BuildSalesTab(TableLayoutPanel root, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddKpiRow(root,
                BuildTotalSalesKpi(overview),
                new Kpi("TRANSACTIONS", FormatCount(overview.TransactionCount), "sales"),
                new Kpi("AVERAGE TRANSACTION", FormatPeso(overview.AverageTransaction), "sales"),
                new Kpi("TOTAL PRODUCTS SOLD", FormatCount(overview.TotalProductsSold), "sales"));

            AddCardGrid(root,
                ChartItem("Sales Trend",
                    BuildLineChart("Date", "Final Amount", true, true, ("Sales", charts.SalesTrend, AccentColor)),
                    "sales", 2),
                ChartItem("Sales by Promotion",
                    BuildDonutChart(charts.SalesByPromotion, true), "sales"));
        }

        private void BuildCustomersTab(TableLayoutPanel root, DashboardModel dashboard, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddKpiRow(root,
                new Kpi("TOTAL CUSTOMERS", FormatCount(overview.TotalCustomers), "customers"),
                new Kpi("NEW CUSTOMERS (LAST 30 DAYS)", FormatCount(overview.NewCustomersLast30Days), "customers"),
                new Kpi("REPEAT CUSTOMER RATE", FormatPercent(overview.RepeatCustomerRatePercent), "customers"));

            AddSectionLabel(root, "CUSTOMER ACTIVITY THIS MONTH");
            AddKpiRow(root,
                new Kpi("NEW CUSTOMERS", FormatCount(dashboard.NewCustomersThisMonth), "customers"),
                new Kpi("ACTIVE CUSTOMERS", FormatCount(dashboard.ActiveCustomersThisMonth), "customers"));

            AddCardGrid(root,
                ChartItem("Customer Growth Trend",
                    BuildLineChart("Date", "Customer Count", false, false, ("Customers", charts.CustomerGrowth, AccentColor)),
                    "customers", 2),
                ChartItem("New vs. Returning Customers",
                    BuildDonutChart(charts.NewVsReturningCustomers, false), "customers"));
        }

        private void BuildProductsTab(TableLayoutPanel root, DashboardModel dashboard, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddKpiRow(root,
                new Kpi("TOTAL PRODUCTS SOLD", FormatCount(overview.TotalProductsSold), "products"),
                new Kpi("ACTIVE PRODUCTS", FormatCount(dashboard.ActiveProducts), "products"),
                new Kpi("LOW STOCK", FormatCount(dashboard.LowStockCount), "products", dashboard.LowStockCount > 0));

            AddCardGrid(root,
                ChartItem("Top-Selling Products",
                    BuildBarChart("Product", "Quantity Sold", charts.ProductPerformance, false, true), "products"),
                ChartItem("Sales by Product Category",
                    BuildDonutChart(charts.SalesByCategory, true), "products"),
                ChartItem("Product Stock",
                    BuildBarChart("Product", "Current Stock", charts.ProductStock, false, true), "products"),
                new CardItem(BuildLowStockCard(dashboard), 1));
        }

        private void BuildPromotionsTab(TableLayoutPanel root, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddKpiRow(root,
                new Kpi("ACTIVE PROMOTIONS", FormatCount(overview.TotalActivePromotions), "promotions"));

            AddCardGrid(root,
                ChartItem("Promotion Usage",
                    BuildBarChart("Promotion", "Completed Transactions", charts.PromotionUsage, false, true), "promotions"),
                ChartItem("Promotion Status",
                    BuildDonutChart(charts.PromotionStatus, false), "promotions"),
                ChartItem("Discount Amount by Promotion",
                    BuildBarChart("Promotion", "Total Discount Amount", charts.DiscountByPromotion, true, true), "promotions"));
        }

        private void BuildLoyaltyTab(TableLayoutPanel root, DashboardModel dashboard, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddKpiRow(root,
                new Kpi("TOTAL EARNED POINTS", FormatCount(overview.TotalEarnedPoints), "loyalty"),
                new Kpi("TOTAL USED POINTS", FormatCount(overview.TotalUsedPoints), "loyalty"),
                new Kpi("LOYALTY ACTIVITY (THIS MONTH)", FormatCount(dashboard.LoyaltyActivityThisMonth), "loyalty"));

            AddCardGrid(root,
                ChartItem("Top Customers by Loyalty Points",
                    BuildBarChart("Customer", "Current Points", charts.TopLoyalCustomers, false, true), "loyalty"));
        }

        private void BuildFeedbackTab(TableLayoutPanel root, ReportsOverviewModel overview, ReportChartsModel charts)
        {
            AddKpiRow(root,
                new Kpi("RESOLVED FEEDBACK", FormatCount(overview.TotalResolvedFeedback), "feedback"),
                new Kpi("RESOLVED COMPLAINTS", FormatCount(overview.TotalResolvedComplaints), "feedback"),
                new Kpi("RESPONDED INQUIRIES", FormatCount(overview.TotalRespondedInquiries), "inquiries"),
                new Kpi("RESOLUTION RATE", FormatPercent(overview.ResolutionRatePercent), "feedback"),
                new Kpi("RESPONSE RATE", FormatPercent(overview.ResponseRatePercent), "inquiries"));

            AddCardGrid(root,
                ChartItem("Complaints by Category",
                    BuildBarChart("Category", "Number of Complaints", charts.ComplaintsByCategory, false, false), "feedback"),
                ChartItem("Feedback by Category",
                    BuildBarChart("Category", "Number of Feedback", charts.FeedbackByCategory, false, false), "feedback"),
                ChartItem("Feedback & Inquiry Submissions Over Time",
                    BuildLineChart("Date", "Number of Submissions", false, true,
                        ("Feedback", charts.FeedbackTrend, AccentColor),
                        ("Inquiries", charts.InquiryTrend, Color.FromArgb(90, 65, 45))),
                    "feedback", 2),
                ChartItem("Inquiries by Type",
                    BuildBarChart("Inquiry Type", "Number of Inquiries", charts.InquiryTypes, false, false), "inquiries"));
        }

        private static Kpi BuildTotalSalesKpi(ReportsOverviewModel overview)
        {
            var (note, noteColor) = BuildSalesComparison(overview.TotalSales, overview.PreviousPeriodSales);
            return new Kpi("TOTAL SALES", FormatPeso(overview.TotalSales), "sales", false, note, noteColor);
        }

        private static (string Note, Color NoteColor) BuildSalesComparison(decimal current, decimal previous)
        {
            if (previous <= 0m)
            {
                return ("No completed sales in the previous period", LabelGray);
            }

            double change = Math.Round((double)((current - previous) / previous * 100m), 1);
            string previousText = FormatPeso(previous);

            if (change > 0)
            {
                return ($"▲ {change.ToString("0.0", CultureInfo.InvariantCulture)}% vs previous period ({previousText})", PositiveGreen);
            }

            if (change < 0)
            {
                return ($"▼ {Math.Abs(change).ToString("0.0", CultureInfo.InvariantCulture)}% vs previous period ({previousText})", LowStockRed);
            }

            return ($"No change vs previous period ({previousText})", LabelGray);
        }

        // ------------------------------------------------------------- layout

        private void AddRow(TableLayoutPanel root, Control control, float height = 0)
        {
            int rowIndex = root.RowCount;
            root.RowCount++;
            // The row must also hold the control's bottom margin, or the bottom of the control is clipped.
            root.RowStyles.Add(height > 0
                ? new RowStyle(SizeType.Absolute, height + Gap)
                : new RowStyle(SizeType.AutoSize));

            control.Margin = new Padding(0, 0, 0, Gap);
            control.Dock = DockStyle.Top;
            root.Controls.Add(control, 0, rowIndex);
        }

        private void AddSectionLabel(TableLayoutPanel root, string text)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                Height = 24,
                TextAlign = ContentAlignment.BottomLeft,
                UseMnemonic = false
            };

            AddRow(root, label, 24);
        }

        private void AddKpiRow(TableLayoutPanel root, params Kpi[] kpis)
        {
            // Keep every KPI card the same width across tabs (4 columns, or 5 when needed).
            int columns = Math.Max(4, kpis.Length);

            var table = new TableLayoutPanel
            {
                ColumnCount = columns,
                RowCount = 1,
                Height = SummaryCardHeight,
                Dock = DockStyle.Top,
                BackColor = PageBg
            };

            for (int i = 0; i < columns; i++)
            {
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }

            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            for (int i = 0; i < kpis.Length; i++)
            {
                var kpi = kpis[i];
                var card = CreateSummaryCard(kpi.Title, kpi.Value, kpi.Warn, kpi.Note, kpi.NoteColor);
                card.Dock = DockStyle.Fill;
                card.Margin = new Padding(i == 0 ? 0 : Gap / 2, 0, i == columns - 1 ? 0 : Gap / 2, 0);
                MakeClickable(card, kpi.ModuleKey);
                table.Controls.Add(card, i, 0);
            }

            AddRow(root, table, SummaryCardHeight);
        }

        private void AddCardGrid(TableLayoutPanel root, params CardItem[] items)
        {
            int index = 0;

            while (index < items.Length)
            {
                var table = new TableLayoutPanel
                {
                    ColumnCount = 2,
                    RowCount = 1,
                    Height = ChartHeight,
                    Dock = DockStyle.Top,
                    BackColor = PageBg
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                var first = items[index];
                index++;

                first.Card.Dock = DockStyle.Fill;

                if (first.Span >= 2)
                {
                    first.Card.Margin = new Padding(0);
                    table.Controls.Add(first.Card, 0, 0);
                    table.SetColumnSpan(first.Card, 2);
                }
                else
                {
                    first.Card.Margin = new Padding(0, 0, Gap / 2, 0);
                    table.Controls.Add(first.Card, 0, 0);

                    if (index < items.Length && items[index].Span < 2)
                    {
                        var second = items[index];
                        index++;

                        second.Card.Dock = DockStyle.Fill;
                        second.Card.Margin = new Padding(Gap / 2, 0, 0, 0);
                        table.Controls.Add(second.Card, 1, 0);
                    }
                }

                AddRow(root, table, ChartHeight);
            }
        }

        private CardItem ChartItem(string title, Chart? chart, string moduleKey, int span = 1)
        {
            var card = CreateChartCard(title, chart);

            if (chart != null)
            {
                MakeClickable(card, moduleKey);
            }

            return new CardItem(card, span);
        }

        private void MakeClickable(Control control, string moduleKey)
        {
            if (_navigateToModule == null)
            {
                return;
            }

            control.Cursor = Cursors.Hand;
            control.Click += (s, e) => _navigateToModule(moduleKey);

            foreach (Control child in control.Controls)
            {
                MakeClickable(child, moduleKey);
            }
        }

        private static void HookWheelFocus(Control control, Control scrollTarget)
        {
            control.MouseEnter += (s, e) => scrollTarget.Focus();

            foreach (Control child in control.Controls)
            {
                HookWheelFocus(child, scrollTarget);
            }
        }

        // -------------------------------------------------------------- cards

        private Panel CreateSummaryCard(string title, string value, bool warn, string? note = null, Color? noteColor = null)
        {
            var card = new Panel
            {
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(16, 10, 16, 8)
            };

            // Added bottom-first: the last control added is docked first (top of the card).
            if (!string.IsNullOrEmpty(note))
            {
                var noteLabel = new Label
                {
                    Text = note,
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = noteColor ?? LabelGray,
                    Dock = DockStyle.Bottom,
                    Height = 34,
                    TextAlign = ContentAlignment.TopLeft,
                    UseMnemonic = false
                };
                _toolTip.SetToolTip(noteLabel, note);
                card.Controls.Add(noteLabel);
            }

            var valueLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = warn ? LowStockRed : AccentColor,
                Dock = DockStyle.Top,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false
            };
            // Shrink the number's font when the card is narrow so it is never cut off.
            valueLabel.Resize += (s, e) => FitValueFont(valueLabel);
            _toolTip.SetToolTip(valueLabel, value);
            card.Controls.Add(valueLabel);

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                Dock = DockStyle.Top,
                Height = 34,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            _toolTip.SetToolTip(titleLabel, title);
            card.Controls.Add(titleLabel);

            return card;
        }

        private static void FitValueFont(Label label)
        {
            const float maxSize = 22f;
            const float minSize = 11f;

            int available = label.ClientSize.Width - 2;

            if (available <= 0 || string.IsNullOrEmpty(label.Text))
            {
                return;
            }

            float size = maxSize;

            while (size > minSize)
            {
                using var candidate = new Font("Segoe UI", size, FontStyle.Bold);
                int width = TextRenderer.MeasureText(
                    label.Text,
                    candidate,
                    new Size(int.MaxValue, label.Height),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;

                if (width <= available)
                {
                    break;
                }

                size -= 1f;
            }

            if (Math.Abs(label.Font.Size - size) > 0.1f)
            {
                var previous = label.Font;
                label.Font = new Font("Segoe UI", size, FontStyle.Bold);
                previous.Dispose();
            }
        }

        private Panel CreateCardShell(string title, out Panel body, string? buttonLabel = null, string? buttonTarget = null)
        {
            var card = new Panel
            {
                BackColor = CardBg,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(18)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                UseMnemonic = false
            };
            layout.Controls.Add(titleLabel, 0, 0);

            body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CardBg
            };
            layout.Controls.Add(body, 0, 1);

            if (!string.IsNullOrWhiteSpace(buttonLabel) && !string.IsNullOrWhiteSpace(buttonTarget) && _navigateToModule != null)
            {
                var button = new Button
                {
                    Text = buttonLabel + "  →",
                    Tag = buttonTarget,
                    Width = 150,
                    Height = 32,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = DarkBrown,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 10, 0, 0)
                };
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 65, 45);
                button.Click += NavButton_Click;
                layout.Controls.Add(button, 0, 2);
            }

            card.Controls.Add(layout);
            return card;
        }

        private void NavButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button { Tag: string target } && !string.IsNullOrWhiteSpace(target))
            {
                _navigateToModule?.Invoke(target);
            }
        }

        private Panel BuildRecentSalesCard(DashboardModel dashboard)
        {
            var card = CreateCardShell("Recent Sales", out var body, "View Sales", "sales");

            if (dashboard.RecentSales.Count == 0)
            {
                body.Controls.Add(CreateEmptyState("No recent sales."));
                return card;
            }

            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = false,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None
            };

            list.Columns.Add("Customer", 160);
            list.Columns.Add("Amount", 100, HorizontalAlignment.Right);
            list.Columns.Add("Payment", 110);
            list.Columns.Add("Status", 100);
            list.ShowItemToolTips = true;
            list.Resize += (s, e) => FitListColumns(list, 0.36f, 0.24f, 0.22f, 0.18f);

            foreach (var sale in dashboard.RecentSales)
            {
                var item = new ListViewItem(sale.Customer);
                item.SubItems.Add("₱" + sale.FinalAmount.ToString("N2", CultureInfo.InvariantCulture));
                item.SubItems.Add(sale.PaymentMethod);
                item.SubItems.Add(sale.Status);
                list.Items.Add(item);
            }

            body.Controls.Add(list);
            return card;
        }

        private Panel BuildLowStockCard(DashboardModel dashboard)
        {
            var card = CreateCardShell("Low-Stock Products", out var body, "View Products", "products");

            if (dashboard.LowStockProducts.Count == 0)
            {
                body.Controls.Add(CreateEmptyState("All products are sufficiently stocked."));
                return card;
            }

            var list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = false,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.None,
                MultiSelect = false,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None
            };

            list.Columns.Add("Product", 220);
            list.Columns.Add("Stock", 100);
            list.ShowItemToolTips = true;
            list.Resize += (s, e) => FitListColumns(list, 0.7f, 0.3f);

            foreach (var product in dashboard.LowStockProducts)
            {
                var item = new ListViewItem(product.ProductName);
                item.SubItems.Add($"{product.Quantity} left");

                if (product.Quantity <= 0)
                {
                    item.ForeColor = LowStockRed;
                }

                list.Items.Add(item);
            }

            body.Controls.Add(list);
            return card;
        }

        private static void FitListColumns(ListView list, params float[] weights)
        {
            // Keep room for the vertical scroll bar so the last column is never cut off.
            int available = list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;

            if (available <= 0 || list.Columns.Count != weights.Length)
            {
                return;
            }

            float total = weights.Sum();

            for (int i = 0; i < weights.Length; i++)
            {
                list.Columns[i].Width = (int)(available * weights[i] / total);
            }
        }

        private Panel BuildSalesOverviewCard(DashboardModel dashboard)
        {
            var card = CreateCardShell("Sales Overview (Last 7 Days)", out var body, "View Sales", "sales");

            var chart = BuildLineChart("Date", "Sales", true, true, ("Sales", dashboard.SalesOverview, AccentColor));

            if (chart == null)
            {
                body.Controls.Add(CreateEmptyState("No sales recorded in the last 7 days."));
            }
            else
            {
                chart.Dock = DockStyle.Fill;
                body.Controls.Add(chart);
            }

            return card;
        }

        private static Label CreateEmptyState(string message)
        {
            return new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 20, 0, 0)
            };
        }

        // ------------------------------------------------- formatting / charts

        private static string FormatPeso(decimal amount)
        {
            return "₱" + amount.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static string FormatCount(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max - 1).TrimEnd() + "…";
        }

        private static bool HasData(List<ChartPointModel>? points)
        {
            return points != null && points.Any(p => p.Value != 0);
        }

        private static Panel CreateChartCard(string title, Chart? chart)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(14)
            };

            if (chart != null)
            {
                chart.Dock = DockStyle.Fill;
                card.Controls.Add(chart);
            }
            else
            {
                var emptyLabel = new Label
                {
                    Text = "No data available.",
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                    ForeColor = LabelGray,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                card.Controls.Add(emptyLabel);
            }

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 26,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            card.Controls.Add(titleLabel);

            return card;
        }

        private static Chart CreateChart()
        {
            return new Chart
            {
                BackColor = Color.White,
                AntiAliasing = AntiAliasingStyles.All,
                TextAntiAliasingQuality = TextAntiAliasingQuality.High
            };
        }

        private static void StyleAxis(Axis axis, string title, bool showGrid)
        {
            axis.Title = title;
            axis.TitleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            axis.TitleForeColor = LabelGray;
            axis.LabelStyle.Font = new Font("Segoe UI", 8f);
            axis.LabelStyle.ForeColor = TextDark;
            axis.LineColor = AxisColor;
            axis.MajorTickMark.LineColor = AxisColor;
            axis.MajorGrid.Enabled = showGrid;
            axis.MajorGrid.LineColor = GridColor;
        }

        private static Chart? BuildLineChart(
            string xTitle,
            string yTitle,
            bool isCurrency,
            bool yFromZero,
            params (string Name, List<ChartPointModel>? Points, Color Color)[] seriesList)
        {
            if (!seriesList.Any(s => HasData(s.Points)))
            {
                return null;
            }

            var chart = CreateChart();
            var area = new ChartArea("Main") { BackColor = Color.White };

            StyleAxis(area.AxisX, xTitle, false);
            StyleAxis(area.AxisY, yTitle, true);
            area.AxisX.IsMarginVisible = false;
            area.AxisY.LabelStyle.Format = isCurrency ? "₱#,##0" : "#,##0";

            double max = seriesList
                .SelectMany(s => s.Points ?? new List<ChartPointModel>())
                .Select(p => p.Value)
                .DefaultIfEmpty(0)
                .Max();
            int pointCount = seriesList.Max(s => s.Points?.Count ?? 0);

            // Daily time-series points carry their real date, so plot them on a true date axis
            // (instead of positions 1, 2, 3... with the date text attached as a label).
            bool useDateAxis = seriesList.All(s => s.Points == null || s.Points.All(p => p.Date.HasValue));

            if (useDateAxis)
            {
                var dates = seriesList
                    .SelectMany(s => s.Points ?? new List<ChartPointModel>())
                    .Select(p => p.Date!.Value.Date)
                    .ToList();

                DateTime firstDate = dates.Min();
                DateTime lastDate = dates.Max();
                var (stepDays, dateFormat) = GetDateAxisSettings(firstDate, lastDate);

                area.AxisX.IntervalType = DateTimeIntervalType.Days;
                area.AxisX.Interval = stepDays;
                area.AxisX.LabelStyle.Format = dateFormat;

                // The axis spans exactly the plotted timeline, so there is no empty space after the last point.
                if (firstDate < lastDate)
                {
                    area.AxisX.Minimum = firstDate.ToOADate();
                    area.AxisX.Maximum = lastDate.ToOADate();
                }
                else
                {
                    area.AxisX.Minimum = firstDate.AddDays(-1).ToOADate();
                    area.AxisX.Maximum = firstDate.AddDays(1).ToOADate();
                }
            }
            else
            {
                area.AxisX.Interval = Math.Max(1, Math.Ceiling(pointCount / 7.0));
            }

            if (yFromZero)
            {
                area.AxisY.Minimum = 0;
            }

            if (!isCurrency && max <= 10)
            {
                area.AxisY.Interval = 1;
            }

            chart.ChartAreas.Add(area);

            bool multiSeries = seriesList.Length > 1;

            if (multiSeries)
            {
                chart.Legends.Add(new Legend("Main")
                {
                    Docking = Docking.Bottom,
                    Font = new Font("Segoe UI", 8.5f),
                    BackColor = Color.White
                });
            }

            foreach (var item in seriesList)
            {
                var series = new Series(item.Name)
                {
                    ChartType = SeriesChartType.Line,
                    ChartArea = "Main",
                    Color = item.Color,
                    BorderWidth = 3,
                    MarkerStyle = MarkerStyle.Circle,
                    MarkerSize = 6,
                    MarkerColor = item.Color,
                    ToolTip = (useDateAxis ? "#VALX{MMM d, yyyy}" : "#VALX") + ": " + (isCurrency ? "₱#VAL{N2}" : "#VAL{N0}")
                };

                if (useDateAxis)
                {
                    series.XValueType = ChartValueType.DateTime;
                }
                else
                {
                    series.IsXValueIndexed = true;
                }

                if (multiSeries)
                {
                    series.Legend = "Main";
                }
                else
                {
                    series.IsVisibleInLegend = false;
                }

                foreach (var point in item.Points ?? new List<ChartPointModel>())
                {
                    if (useDateAxis)
                    {
                        series.Points.AddXY(point.Date!.Value.Date, point.Value);
                    }
                    else
                    {
                        series.Points.AddXY(point.Label, point.Value);
                    }
                }

                chart.Series.Add(series);
            }

            return chart;
        }

        private static readonly int[] DateStepDays = { 1, 2, 3, 5, 7, 10, 14, 15, 30, 60, 90, 180, 365, 730 };

        private static (int StepDays, string Format) GetDateAxisSettings(DateTime first, DateTime last)
        {
            int spanDays = (int)(last.Date - first.Date).TotalDays + 1;
            int target = (int)Math.Ceiling(spanDays / 8.0);

            int step = DateStepDays.FirstOrDefault(d => d >= target);

            if (step == 0)
            {
                step = target;
            }

            // Show the year only when the period crosses a year boundary.
            string format = first.Year == last.Year ? "MMM d" : "MMM d, yyyy";

            return (step, format);
        }

        private static Chart? BuildBarChart(
            string categoryTitle,
            string valueTitle,
            List<ChartPointModel>? points,
            bool isCurrency,
            bool horizontal)
        {
            if (points == null || !HasData(points))
            {
                return null;
            }

            var chart = CreateChart();
            var area = new ChartArea("Main") { BackColor = Color.White };

            StyleAxis(area.AxisX, categoryTitle, false);
            StyleAxis(area.AxisY, valueTitle, true);
            area.AxisX.Interval = 1;
            area.AxisY.Minimum = 0;
            area.AxisY.LabelStyle.Format = isCurrency ? "₱#,##0" : "#,##0";

            double max = points.Max(p => p.Value);
            area.AxisY.Maximum = Math.Ceiling(max * (horizontal ? 1.3 : 1.18));

            if (!isCurrency && max <= 10)
            {
                area.AxisY.Interval = 1;
            }

            if (!horizontal)
            {
                area.AxisX.LabelStyle.Angle = -35;
            }

            chart.ChartAreas.Add(area);

            var series = new Series("Values")
            {
                ChartType = horizontal ? SeriesChartType.Bar : SeriesChartType.Column,
                ChartArea = "Main",
                Color = AccentColor,
                IsValueShownAsLabel = true,
                LabelFormat = isCurrency ? "₱#,##0" : "#,##0",
                LabelForeColor = TextDark,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                IsVisibleInLegend = false,
                IsXValueIndexed = true
            };

            series["PointWidth"] = "0.65";

            IEnumerable<ChartPointModel> ordered = horizontal ? Enumerable.Reverse(points) : points;

            foreach (var point in ordered)
            {
                int index = series.Points.AddXY(Shorten(point.Label, horizontal ? 24 : 22), point.Value);

                series.Points[index].ToolTip = isCurrency
                    ? $"{point.Label}: ₱{point.Value.ToString("N2", CultureInfo.InvariantCulture)}"
                    : $"{point.Label}: {point.Value.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            chart.Series.Add(series);

            return chart;
        }

        private static Chart? BuildDonutChart(List<ChartPointModel>? points, bool isCurrency)
        {
            if (points == null)
            {
                return null;
            }

            var data = points.Where(p => p.Value > 0).ToList();

            if (data.Count == 0)
            {
                return null;
            }

            data = LimitSlices(data, 7);

            var chart = CreateChart();
            chart.ChartAreas.Add(new ChartArea("Main") { BackColor = Color.White });
            // Legend on the right so long slice names never squeeze the doughnut vertically.
            chart.Legends.Add(new Legend("Main")
            {
                Docking = Docking.Right,
                Alignment = StringAlignment.Center,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = Color.White
            });

            var series = new Series("Values")
            {
                ChartType = SeriesChartType.Doughnut,
                ChartArea = "Main",
                Legend = "Main",
                Label = "#PERCENT{P0}",
                LabelForeColor = TextDark,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ToolTip = isCurrency ? "#VALX: ₱#VAL{N2} (#PERCENT{P0})" : "#VALX: #VAL{N0} (#PERCENT{P0})"
            };
            series["DoughnutRadius"] = "55";
            series["PieLabelStyle"] = "Outside";
            series["PieLineColor"] = "Gray";

            for (int i = 0; i < data.Count; i++)
            {
                int index = series.Points.AddXY(data[i].Label, data[i].Value);
                series.Points[index].Color = SlicePalette[i % SlicePalette.Length];

                string valueText = isCurrency
                    ? "₱" + data[i].Value.ToString("N0", CultureInfo.InvariantCulture)
                    : data[i].Value.ToString("N0", CultureInfo.InvariantCulture);
                series.Points[index].LegendText = $"{Shorten(data[i].Label, 22)} ({valueText})";
            }

            chart.Series.Add(series);

            return chart;
        }

        private static List<ChartPointModel> LimitSlices(List<ChartPointModel> points, int max)
        {
            if (points.Count <= max)
            {
                return points;
            }

            var ordered = points.OrderByDescending(p => p.Value).ToList();
            var top = ordered.Take(max - 1).ToList();

            top.Add(new ChartPointModel
            {
                Label = "Others",
                Value = ordered.Skip(max - 1).Sum(p => p.Value)
            });

            return top;
        }
    }
}