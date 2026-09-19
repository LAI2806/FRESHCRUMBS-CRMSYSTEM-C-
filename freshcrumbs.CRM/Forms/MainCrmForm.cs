using FontAwesome.Sharp;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.UserControls;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class MainCrmForm : Form
    {
        private readonly CompanyModel _currentCompany;

        private TableLayoutPanel _rootLayout = null!;
        private TableLayoutPanel _sidebarLayout = null!;
        private TableLayoutPanel _mainLayout = null!;
        private Panel _sidebarPanel = null!;
        private Panel _mainContentContainer = null!;
        private Panel _topBarPanel = null!;
        private Panel _contentPanel = null!;
        private Label _companyLabel = null!;
        private FlowLayoutPanel _navFlowPanel = null!;
        private readonly Dictionary<string, IconChar> _navIcons = new();

        private readonly Dictionary<string, Button> _navButtons = new();
        private string _activeKey = "dashboard";

        private static readonly Color SidebarBg = Color.White;
        private static readonly Color SidebarBorder = Color.FromArgb(225, 210, 200);
        private static readonly Color SidebarActiveBg = Color.FromArgb(60, 40, 30);
        private static readonly Color SidebarText = Color.FromArgb(90, 70, 60);
        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color HeaderBg = Color.White;
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);

        public MainCrmForm(CompanyModel currentCompany)
        {
            _currentCompany = currentCompany;

            InitializeForm();
            InitializeRootLayout();
            InitializeSidebar();
            InitializeMainContentContainer();

            SetActiveModule("dashboard");
        }
        private void ShowProductManagement()
        {
            _contentPanel.Controls.Clear();

            var productControl = new ProductControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(productControl);
        }

        private void ShowSalesManagement()
        {
            _contentPanel.Controls.Clear();

            var salesControl = new SalesControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(salesControl);
        }

        private void ShowCustomerManagement()
        {
            _contentPanel.Controls.Clear();

            var customerControl = new CustomerControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(customerControl);
        }
        private void ShowLoyaltyManagement()
        {
            _contentPanel.Controls.Clear();

            var loyaltyControl = new LoyaltyControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(loyaltyControl);
        }
        private void InitializeForm()
        {
            Text = "FreshCrumbs CRM";
            Width = 1200;
            Height = 750;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
        }

        private void InitializeRootLayout()
        {
            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = PageBg
            };

            _rootLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 260f));

            _rootLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            _rootLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100f));

            Controls.Add(_rootLayout);
        }

        private void InitializeSidebar()
        {
            _sidebarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _sidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = SidebarBg
            };

            _sidebarLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            _sidebarLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 80f));

            _sidebarLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100f));

            _sidebarLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 70f));

            _sidebarPanel.Controls.Add(_sidebarLayout);

            var logoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var logoIcon = new Label
            {
                Text = "🥐",
                Font = new Font("Segoe UI Emoji", 12),
                Location = new Point(20, 22),
                AutoSize = true
            };

            logoPanel.Controls.Add(logoIcon);

            var logoText = new Label
            {
                Text = "FreshCrumbs",
                Font = new Font(
                    "Segoe UI",
                    14,
                    FontStyle.Bold),
                ForeColor = AccentColor,
                Location = new Point(56, 27),
                AutoSize = true
            };

            logoPanel.Controls.Add(logoText);

            _navFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(18, 15, 18, 0),
                Margin = new Padding(0),
                BackColor = SidebarBg
            };

            var navItems = new (IconChar Icon, string Text, string Key)[]
            {
                (IconChar.Gauge, "Dashboard", "dashboard"),
                (IconChar.Box, "Products", "products"),
                (IconChar.Users, "Customers", "customers"),
                (IconChar.Percent, "Promotions", "promotions"),
                (IconChar.Star, "Loyalty", "loyalty"),
                (IconChar.Comment, "Feedback", "feedback"),
                (IconChar.QuestionCircle, "Inquiries", "inquiries"),
                (IconChar.CashRegister, "Sales", "sales")
                        };

            foreach (var item in navItems)
            {
                var navButton = CreateNavButton(
                    item.Icon,
                    item.Text,
                    item.Key);

                _navButtons[item.Key] = navButton;
                _navIcons[item.Key] = item.Icon;
                _navFlowPanel.Controls.Add(navButton);
            }

            var settingsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Padding = new Padding(15, 15, 15, 15),
                Margin = new Padding(0)
            };

            var settingsButton = CreateNavButton(
                IconChar.Gear,
                "Settings",
                "settings");

            settingsButton.Dock = DockStyle.Fill;
            settingsButton.Margin = new Padding(0);

            _navButtons["settings"] = settingsButton;
            _navIcons["settings"] = IconChar.Gear;

            settingsPanel.Controls.Add(settingsButton);

            _sidebarLayout.Controls.Add(
                logoPanel,
                0,
                0);

            _sidebarLayout.Controls.Add(
                _navFlowPanel,
                0,
                1);

            _sidebarLayout.Controls.Add(
                settingsPanel,
                0,
                2);

            var borderLine = new Panel
            {
                Dock = DockStyle.Right,
                Width = 1,
                BackColor = SidebarBorder
            };

            _sidebarPanel.Controls.Add(borderLine);

            _rootLayout.Controls.Add(
                _sidebarPanel,
                0,
                0);
        }

        private Button CreateNavButton(
            IconChar icon,
            string text,
            string key)
        {
            var button = new Button
            {
                Text = "   " + text,
                Tag = key,
                Width = 220,
                Height = 52,
                Font = new Font("Segoe UI", 11),
                FlatStyle = FlatStyle.Flat,
                BackColor = SidebarBg,
                ForeColor = SidebarText,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 6),
                Padding = new Padding(0),
                TextAlign = ContentAlignment.MiddleLeft,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Image = icon.ToBitmap(SidebarText, 20)
            };

            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = SidebarBorder;
            button.FlatAppearance.MouseOverBackColor =
                Color.FromArgb(248, 240, 232);

            button.Click += NavButton_Click;

            return button;
        }
        private void InitializeMainContentContainer()
        {
            _mainContentContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = PageBg
            };

            _mainLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            _mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 64f));

            _mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100f));

            _mainContentContainer.Controls.Add(_mainLayout);

            InitializeTopBar();
            InitializeContentArea();

            _rootLayout.Controls.Add(
                _mainContentContainer,
                1,
                0);
        }

        private void InitializeTopBar()
        {
            _topBarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = HeaderBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var borderLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = SidebarBorder
            };

            _topBarPanel.Controls.Add(borderLine);

            _companyLabel = new Label
            {
                Text = _currentCompany.CompanyName,
                Font = new Font(
                    "Segoe UI",
                    11,
                    FontStyle.Bold),
                ForeColor =
                    Color.FromArgb(60, 40, 30),
                AutoSize = true
            };

            _topBarPanel.Controls.Add(_companyLabel);

            RepositionCompanyLabel();

            _topBarPanel.Resize += (s, e) =>
                RepositionCompanyLabel();

            _mainLayout.Controls.Add(
                _topBarPanel,
                0,
                0);
        }

        private void RepositionCompanyLabel()
        {
            if (_companyLabel == null ||
                _topBarPanel == null)
            {
                return;
            }

            _companyLabel.Location = new Point(
                _topBarPanel.Width -
                _companyLabel.PreferredWidth -
                30,
                (_topBarPanel.Height -
                 _companyLabel.PreferredHeight) / 2);
        }

        private void InitializeContentArea()
        {
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(30),
                Margin = new Padding(0)
            };

            _mainLayout.Controls.Add(
                _contentPanel,
                0,
                1);
        }

        private void NavButton_Click(
            object? sender,
            EventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not string key)
            {
                return;
            }

            SetActiveModule(key);
        }

        private void ShowInquiries()
        {
            _contentPanel.Controls.Clear();
            var inquiryControl = new InquiryControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(inquiryControl);
        }

        private void SetActiveModule(string key)
        {
            _activeKey = key;

            foreach (var kvp in _navButtons)
            {
                bool isActive = kvp.Key == key;

                kvp.Value.BackColor = isActive
                    ? SidebarActiveBg
                    : SidebarBg;

                kvp.Value.ForeColor = isActive
                    ? Color.White
                    : SidebarText;

                kvp.Value.Font = new Font(
                    "Segoe UI",
                    10,
                    isActive
                        ? FontStyle.Bold
                        : FontStyle.Regular);

                if (_navIcons.TryGetValue(kvp.Key, out var iconChar))
                {
                    kvp.Value.Image = iconChar.ToBitmap(
                        isActive ? Color.White : SidebarText,
                        20);
                }
            }


            switch (key)
            {
                case "dashboard":
                    ShowDashboard();
                    break;

                case "products":
                    ShowProductManagement();
                    break;

                case "customers":
                    ShowCustomerManagement();
                    break;

                case "promotions":
                    ShowPromotionManagement();
                    break;

                case "loyalty":
                    ShowLoyaltyManagement();
                    break;

                case "feedback":
                    ShowFeedbackManagement();
                    break;

                case "sales":
                    ShowSalesManagement();
                    break;

                case "inquiries":
                    ShowInquiries();
                    break;

                case "settings":
                    ShowPlaceholder(
                        "Settings",
                        "Not built yet.");
                    break;
            }
        }

        private void ShowDashboard()
        {
            _contentPanel.Controls.Clear();

            var dashboardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = PageBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            dashboardLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            dashboardLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 55f));

            dashboardLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 120f));

            dashboardLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100f));

            var header = new Label
            {
                Text = "Dashboard",
                Font = new Font(
                    "Segoe UI",
                    20,
                    FontStyle.Bold),
                ForeColor =
                    Color.FromArgb(50, 35, 25),
                Dock = DockStyle.Fill,
                TextAlign =
                    ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };

            dashboardLayout.Controls.Add(
                header,
                0,
                0);

            var kpiTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = PageBg,
                Margin = new Padding(0)
            };

            for (int i = 0; i < 4; i++)
            {
                kpiTable.ColumnStyles.Add(
                    new ColumnStyle(
                        SizeType.Percent,
                        25f));
            }

            kpiTable.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100f));

            var kpiData =
                new (string Title, string Value)[]
                {
                    ("Total Products", "--"),
                    ("Total Customers", "--"),
                    ("Active Promotions", "--"),
                    ("Sales Summary", "--")
                };

            for (int i = 0; i < kpiData.Length; i++)
            {
                var card = CreateStatCard(
                    kpiData[i].Title,
                    kpiData[i].Value);

                card.Dock = DockStyle.Fill;

                card.Margin = new Padding(
                    i == 0 ? 0 : 8,
                    0,
                    i == kpiData.Length - 1 ? 0 : 8,
                    0);

                kpiTable.Controls.Add(
                    card,
                    i,
                    0);
            }

            dashboardLayout.Controls.Add(
                kpiTable,
                0,
                1);

            var sectionsTable = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 2,
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Margin = new Padding(0, 10, 0, 0)
            };

            sectionsTable.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 50f));

            sectionsTable.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 50f));

            sectionsTable.RowStyles.Add(
                new RowStyle(SizeType.Percent, 50f));

            sectionsTable.RowStyles.Add(
                new RowStyle(SizeType.Percent, 50f));

            var salesSection = CreateSectionCard(
                "Sales Summary",
                "No sales data yet.");

            salesSection.Dock = DockStyle.Fill;
            salesSection.Margin =
                new Padding(0, 0, 8, 8);

            sectionsTable.Controls.Add(
                salesSection,
                0,
                0);

            var customerSection = CreateSectionCard(
                "Customer Activity",
                "No recent activity yet.");

            customerSection.Dock = DockStyle.Fill;
            customerSection.Margin =
                new Padding(8, 0, 0, 8);

            sectionsTable.Controls.Add(
                customerSection,
                1,
                0);

            var transactionsSection = CreateSectionCard(
                "Recent Transactions",
                "No transactions yet.");

            transactionsSection.Dock = DockStyle.Fill;
            transactionsSection.Margin =
                new Padding(0, 8, 8, 0);

            sectionsTable.Controls.Add(
                transactionsSection,
                0,
                1);

            var feedbackSection = CreateSectionCard(
                "Active Promotions & Feedback",
                "Nothing to show yet.");

            feedbackSection.Dock = DockStyle.Fill;
            feedbackSection.Margin =
                new Padding(8, 8, 0, 0);

            sectionsTable.Controls.Add(
                feedbackSection,
                1,
                1);

            dashboardLayout.Controls.Add(
                sectionsTable,
                0,
                2);

            _contentPanel.Controls.Add(
                dashboardLayout);
        }
        private void ShowFeedbackManagement()
        {
            _contentPanel.Controls.Clear();

            var feedbackControl = new FeedbackControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(feedbackControl);
        }

        private Panel CreateStatCard(
            string title,
            string value)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(18)
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(
                    "Segoe UI",
                    9.5f),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(18, 18)
            };

            card.Controls.Add(titleLabel);

            var valueLabel = new Label
            {
                Text = value,
                Font = new Font(
                    "Segoe UI",
                    24,
                    FontStyle.Bold),
                ForeColor = AccentColor,
                AutoSize = true,
                Location = new Point(18, 48)
            };

            card.Controls.Add(valueLabel);

            return card;
        }

        private void ShowPromotionManagement()
        {
            _contentPanel.Controls.Clear();

            var promotionControl = new PromotionControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(promotionControl);
        }

        private Panel CreateSectionCard(
            string title,
            string emptyMessage)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(20)
            };

            var titleLabel = new Label
            {
                Text = title,
                Font = new Font(
                    "Segoe UI",
                    12,
                    FontStyle.Bold),
                ForeColor =
                    Color.FromArgb(50, 35, 25),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            card.Controls.Add(titleLabel);

            var messageLabel = new Label
            {
                Text = emptyMessage,
                Font = new Font(
                    "Segoe UI",
                    9.5f,
                    FontStyle.Italic),
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(20, 50)
            };

            card.Controls.Add(messageLabel);

            return card;
        }

        private void ShowPlaceholder(
            string title,
            string message)
        {
            _contentPanel.Controls.Clear();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = PageBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            layout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100f));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 55f));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100f));

            var header = new Label
            {
                Text = title,
                Font = new Font(
                    "Segoe UI",
                    20,
                    FontStyle.Bold),
                ForeColor =
                    Color.FromArgb(50, 35, 25),
                Dock = DockStyle.Fill,
                TextAlign =
                    ContentAlignment.MiddleLeft
            };

            layout.Controls.Add(
                header,
                0,
                0);

            var messageLabel = new Label
            {
                Text = message,
                Font = new Font(
                    "Segoe UI",
                    10),
                ForeColor = Color.Gray,
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(0, 10, 0, 0)
            };

            layout.Controls.Add(
                messageLabel,
                0,
                1);

            _contentPanel.Controls.Add(layout);
        }
    }
}