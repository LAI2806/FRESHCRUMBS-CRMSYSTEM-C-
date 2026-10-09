using FontAwesome.Sharp;
using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;
using freshcrumbs.CRM.winforms.UserControls;
using System.Windows.Input;

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
        private Label _profileCompanyLabel = null!;
        private Label _profileNameLabel = null!;
        private Label _profileRoleLabel = null!;

        // Set when the user chose Log Out (the login screen opens again instead of the application closing).
        public bool LogoutRequested { get; private set; }
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

            ApplyFeatureVisibility();
            SetActiveModule(GetStartModule());
        }

        // Subscription side: which plan feature a module needs. The dashboard exists on every plan
        // (Basic Dashboard), and so do Reports (Basic Reports), so neither has an entry.
        private static readonly Dictionary<string, string> ModuleFeatures = new()
        {
            ["products"] = "MainTransactions",
            ["sales"] = "MainTransactions",
            ["customers"] = "MainTransactions",
            ["feedback"] = "DataCollection",
            ["inquiries"] = "DataCollection",
            ["promotions"] = "ActionsRetention",
            ["loyalty"] = "ActionsRetention",
            ["branches"] = "Branching"
        };

        // A module is shown only when the SUBSCRIPTION provides it AND the user's ROLE allows it.
        private bool IsModuleAllowed(string key)
        {
            return IsFeatureAllowed(key) && IsRoleAllowedForModule(key);
        }

        private bool IsFeatureAllowed(string key)
        {
            return _currentCompany.EnabledFeatures == null
                || !ModuleFeatures.TryGetValue(key, out var feature)
                || _currentCompany.EnabledFeatures.Contains(feature);
        }

        // Role side. Staff get the Product List (view/search/filter) but not Promotions or Loyalty management.
        private static bool IsRoleAllowedForModule(string key)
        {
            return key switch
            {
                "dashboard" => TenantCapabilities.CanViewDashboardData,
                "products" => TenantCapabilities.CanViewProducts,
                "promotions" => TenantCapabilities.CanManagePromotions,
                "loyalty" => TenantCapabilities.CanManageLoyalty,
                "reports" => TenantCapabilities.CanGenerateReports || TenantCapabilities.CanViewOperationalReports,
                "branches" => TenantCapabilities.CanManageBranchStock,
                "users" => TenantCapabilities.CanManageUsers,
                _ => true
            };
        }

        private string GetNotAvailableMessage(string key)
        {
            return IsFeatureAllowed(key)
                ? "Your role does not have access to this module."
                : "Your current subscription plan does not include this module.";
        }

        private void ApplyFeatureVisibility()
        {
            foreach (var kvp in _navButtons)
            {
                kvp.Value.Visible = IsModuleAllowed(kvp.Key);
            }

            // Same Products screen for everyone; only the label (and the actions inside it) follow the role.
            if (_navButtons.TryGetValue("products", out var productsButton))
            {
                productsButton.Text = "   " + (TenantCapabilities.CanManageProducts ? "Product Management" : "Product List");
            }
        }

        private string GetStartModule()
        {
            return _navButtons.Keys.FirstOrDefault(IsModuleAllowed) ?? "dashboard";
        }
        private void ShowProductManagement()
        {
            _contentPanel.Controls.Clear();

            var productControl = new ProductControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(productControl);
        }

        private void ShowBranchManagement()
        {
            _contentPanel.Controls.Clear();

            var branchControl = new BranchControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(branchControl);
        }

        private void ShowUserManagement()
        {
            _contentPanel.Controls.Clear();

            var userControl = new TenantUserControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(userControl);
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
        private void ShowReportsManagement()
        {
            _contentPanel.Controls.Clear();

            // ADMIN / MANAGER: the report generator. STAFF: read-only operational reports on the dashboard layout.
            Control reportsControl = TenantCapabilities.CanGenerateReports
                ? new ReportsControl(_currentCompany.CompanyId, SetActiveModule)
                : new DashboardControl(_currentCompany.CompanyId, SetActiveModule, operationalReports: true);
            _contentPanel.Controls.Add(reportsControl);
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
                new RowStyle(SizeType.Absolute, 108f));

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
                // Scrolls when the window is too short for every module (no right padding, so no horizontal bar).
                AutoScroll = true,
                Padding = new Padding(18, 8, 0, 0),
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
                (IconChar.CashRegister, "Sales", "sales"),
                (IconChar.CodeBranch, "Branches", "branches"),
                (IconChar.ChartLine, "Reports", "reports"),
                (IconChar.UsersGear, "Users", "users")
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

            var profilePanel = CreateProfilePanel();

            _sidebarLayout.Controls.Add(
                logoPanel,
                0,
                0);

            _sidebarLayout.Controls.Add(
                _navFlowPanel,
                0,
                1);

            _sidebarLayout.Controls.Add(
                profilePanel,
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

        // Signed-in user: company, name and role, with My Account and Log Out (every role).
        private Panel CreateProfilePanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Padding = new Padding(18, 8, 14, 6),
                Margin = new Padding(0)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = SidebarBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _profileCompanyLabel = CreateProfileLabel(new Font("Segoe UI", 9, FontStyle.Bold), AccentColor);
            _profileNameLabel = CreateProfileLabel(new Font("Segoe UI", 10.5f, FontStyle.Bold), Color.FromArgb(50, 35, 25));
            _profileRoleLabel = CreateProfileLabel(new Font("Segoe UI", 8.5f), SidebarText);

            layout.Controls.Add(_profileCompanyLabel, 0, 0);
            layout.Controls.Add(_profileNameLabel, 0, 1);
            layout.Controls.Add(_profileRoleLabel, 0, 2);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = SidebarBg,
                Margin = new Padding(0),
                Padding = new Padding(0, 4, 0, 0)
            };

            var accountButton = CreateProfileButton("My Account", 104, false);
            accountButton.Click += (s, e) => SetActiveModule("account");

            var logoutButton = CreateProfileButton("Log Out", 90, true);
            logoutButton.Click += LogoutButton_Click;

            actions.Controls.Add(accountButton);
            actions.Controls.Add(logoutButton);
            layout.Controls.Add(actions, 0, 3);

            var topLine = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = SidebarBorder
            };

            panel.Controls.Add(layout);
            panel.Controls.Add(topLine);

            RefreshProfile();
            return panel;
        }

        private static Label CreateProfileLabel(Font font, Color color)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Font = font,
                ForeColor = color,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
        }

        private Button CreateProfileButton(string text, int width, bool primary)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? SidebarActiveBg : SidebarBg,
                ForeColor = primary ? Color.White : SidebarText,
                Cursor = System.Windows.Forms.Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };

            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = SidebarBorder;
            return button;
        }

        // Company from the current tenant; name and role from the signed-in session.
        private void RefreshProfile()
        {
            var session = AuthSession.Current;
            string name = session == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(session.FullName) ? session.UserName : session.FullName;

            _profileCompanyLabel.Text = _currentCompany.CompanyName.ToUpperInvariant();
            _profileNameLabel.Text = name;
            _profileRoleLabel.Text = AuthSession.Role;
        }

        private async void LogoutButton_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Log out of FreshCrumbs?", "Log Out",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            if (sender is Button button)
            {
                button.Enabled = false;
            }

            await new ApiService().LogoutAsync();

            LogoutRequested = true;
            Close();
        }

        private void ShowMyAccount()
        {
            _contentPanel.Controls.Clear();

            var accountControl = new MyAccountControl(RefreshProfile);
            _contentPanel.Controls.Add(accountControl);
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
                Height = 42,
                Font = new Font("Segoe UI", 11),
                FlatStyle = FlatStyle.Flat,
                BackColor = SidebarBg,
                ForeColor = SidebarText,
                Cursor = System.Windows.Forms.Cursors.Hand,
                Margin = new Padding(0, 0, 0, 4),
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

            // Online / Offline / Syncing indicator (hidden automatically when the API runs in cloud mode).
            _topBarPanel.Controls.Add(new SyncStatusLabel { Dock = DockStyle.Left });

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

            if (!IsModuleAllowed(key))
            {
                ShowPlaceholder("Not available", GetNotAvailableMessage(key));
                return;
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

                case "reports":
                    ShowReportsManagement();
                    break;

                case "branches":
                    ShowBranchManagement();
                    break;

                case "users":
                    ShowUserManagement();
                    break;

                case "account":
                    ShowMyAccount();
                    break;
            }
        }

        private void ShowDashboard()
        {
            _contentPanel.Controls.Clear();

            var dashboardControl = new DashboardControl(_currentCompany.CompanyId, SetActiveModule);
            _contentPanel.Controls.Add(dashboardControl);
        }
        private void ShowFeedbackManagement()
        {
            _contentPanel.Controls.Clear();

            var feedbackControl = new FeedbackControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(feedbackControl);
        }

        private void ShowPromotionManagement()
        {
            _contentPanel.Controls.Clear();

            var promotionControl = new PromotionControl(_currentCompany.CompanyId);
            _contentPanel.Controls.Add(promotionControl);
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