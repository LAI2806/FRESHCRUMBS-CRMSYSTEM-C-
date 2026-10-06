using FontAwesome.Sharp;
using freshcrumbs.CRM.winforms.Services;
using freshcrumbs.CRM.winforms.UserControls;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class SuperAdminShellForm : Form
    {
        public bool LogoutRequested { get; private set; }

        private static readonly Color SidebarBg = Color.White;
        private static readonly Color SidebarBorder = Color.FromArgb(225, 210, 200);
        private static readonly Color SidebarActiveBg = Color.FromArgb(60, 40, 30);
        private static readonly Color SidebarText = Color.FromArgb(90, 70, 60);
        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color HeaderBg = Color.White;
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color GroupTitleColor = Color.FromArgb(160, 148, 138);

        private readonly Dictionary<string, Button> _navButtons = new();
        private readonly Dictionary<string, IconChar> _navIcons = new();
        private readonly Dictionary<string, string> _pageTitles = new()
        {
            ["dashboard"] = "Dashboard / Business Intelligence",
            ["plans"] = "Subscription / Plan Management",
            ["subscribers"] = "Subscription / Subscribers",
            ["tenants"] = "Tenant & Users / Tenant Management",
            ["users"] = "Tenant & Users / User Directory",
            ["terms"] = "Legal / Terms & Conditions",
            ["account"] = "My Account"
        };

        private Panel _contentPanel = null!;
        private Panel _topBarPanel = null!;
        private Label _pageTitleLabel = null!;
        private Button _accountButton = null!;
        private ContextMenuStrip _accountMenu = null!;
        private ToolStripMenuItem _accountNameItem = null!;

        public SuperAdminShellForm()
        {
            Text = "FreshCrumbs Platform - Super Admin";
            Width = 1200;
            Height = 750;
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);

            BuildLayout();
            SetActiveModule("dashboard");
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = PageBg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            root.Controls.Add(BuildSidebar(), 0, 0);
            root.Controls.Add(BuildMainArea(), 1, 0);
        }

        private Control BuildSidebar()
        {
            var sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = SidebarBg
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            sidebar.Controls.Add(layout);

            var logoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            logoPanel.Controls.Add(new Label
            {
                Text = "🥐",
                Font = new Font("Segoe UI Emoji", 12),
                Location = new Point(20, 22),
                AutoSize = true
            });

            logoPanel.Controls.Add(new Label
            {
                Text = "FreshCrumbs",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = AccentColor,
                Location = new Point(56, 20),
                AutoSize = true
            });

            logoPanel.Controls.Add(new Label
            {
                Text = "Platform Admin",
                Font = new Font("Segoe UI", 9),
                ForeColor = SidebarText,
                Location = new Point(58, 50),
                AutoSize = true
            });

            layout.Controls.Add(logoPanel, 0, 0);

            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(18, 15, 18, 0),
                Margin = new Padding(0),
                BackColor = SidebarBg
            };

            AddGroupTitle(nav, "Dashboard");
            AddNavButton(nav, IconChar.ChartPie, "Business Intelligence", "dashboard");

            AddGroupTitle(nav, "Subscription");
            AddNavButton(nav, IconChar.ClipboardList, "Plan Management", "plans");
            AddNavButton(nav, IconChar.Users, "Subscribers", "subscribers");

            AddGroupTitle(nav, "Tenant & Users");
            AddNavButton(nav, IconChar.Building, "Tenant Management", "tenants");
            AddNavButton(nav, IconChar.AddressBook, "User Directory", "users");

            AddGroupTitle(nav, "Legal");
            AddNavButton(nav, IconChar.FileContract, "Terms & Conditions", "terms");

            layout.Controls.Add(nav, 0, 1);

            sidebar.Controls.Add(new Panel
            {
                Dock = DockStyle.Right,
                Width = 1,
                BackColor = SidebarBorder
            });

            return sidebar;
        }

        private void AddGroupTitle(FlowLayoutPanel nav, string title)
        {
            nav.Controls.Add(new Label
            {
                Text = title.ToUpperInvariant(),
                Width = 220,
                Height = 24,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = GroupTitleColor,
                TextAlign = ContentAlignment.BottomLeft,
                Padding = new Padding(4, 0, 0, 2),
                Margin = new Padding(0, 10, 0, 4),
                TabStop = false
            });
        }

        private void AddNavButton(FlowLayoutPanel nav, IconChar icon, string text, string key)
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
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 240, 232);
            button.Click += NavButton_Click;

            _navButtons[key] = button;
            _navIcons[key] = icon;
            nav.Controls.Add(button);
        }

        private Control BuildMainArea()
        {
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = PageBg
            };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 70f));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _topBarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = HeaderBg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _topBarPanel.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = SidebarBorder
            });

            _pageTitleLabel = new Label
            {
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 40, 30),
                AutoSize = true,
                Location = new Point(30, 25)
            };
            _topBarPanel.Controls.Add(_pageTitleLabel);

            string name = !string.IsNullOrWhiteSpace(AuthSession.Current?.FullName)
                ? AuthSession.Current!.FullName
                : AuthSession.Current?.UserName ?? "Account";

            _accountNameItem = new ToolStripMenuItem(name) { Enabled = false };

            _accountMenu = new ContextMenuStrip();
            _accountMenu.Items.Add(_accountNameItem);
            _accountMenu.Items.Add(new ToolStripMenuItem("Super Admin") { Enabled = false });
            _accountMenu.Items.Add(new ToolStripSeparator());
            _accountMenu.Items.Add("My Account", null, (s, e) => SetActiveModule("account"));
            _accountMenu.Items.Add("Logout", null, LogoutMenu_Click);

            _accountButton = new Button
            {
                Text = name + "  ▾",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Height = 36,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = HeaderBg,
                ForeColor = Color.FromArgb(60, 40, 30),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Padding = new Padding(10, 0, 10, 0)
            };
            _accountButton.FlatAppearance.BorderColor = SidebarBorder;
            _accountButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 240, 232);
            _accountButton.Click += (s, e) => _accountMenu.Show(_accountButton, new Point(0, _accountButton.Height));
            _topBarPanel.Controls.Add(_accountButton);

            void PlaceAccountButton() => _accountButton.Location = new Point(_topBarPanel.Width - _accountButton.Width - 30, (_topBarPanel.Height - _accountButton.Height) / 2);
            _topBarPanel.Resize += (s, e) => PlaceAccountButton();
            _accountButton.SizeChanged += (s, e) => PlaceAccountButton();

            main.Controls.Add(_topBarPanel, 0, 0);

            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PageBg,
                Padding = new Padding(30),
                Margin = new Padding(0)
            };
            main.Controls.Add(_contentPanel, 0, 1);

            return main;
        }

        private void NavButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button { Tag: string key })
            {
                SetActiveModule(key);
            }
        }

        private void LogoutMenu_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Log out of the platform console?", "Logout",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            LogoutRequested = true;
            Close();
        }

        private void SetActiveModule(string key, string? statusFilter = null, string? search = null)
        {
            foreach (var pair in _navButtons)
            {
                bool isActive = pair.Key == key;

                pair.Value.BackColor = isActive ? SidebarActiveBg : SidebarBg;
                pair.Value.ForeColor = isActive ? Color.White : SidebarText;
                pair.Value.Font = new Font("Segoe UI", 10, isActive ? FontStyle.Bold : FontStyle.Regular);
                pair.Value.Image = _navIcons[pair.Key].ToBitmap(isActive ? Color.White : SidebarText, 20);
            }

            _pageTitleLabel.Text = _pageTitles[key];

            foreach (Control old in _contentPanel.Controls.Cast<Control>().ToList())
            {
                old.Dispose();
            }

            _contentPanel.Controls.Clear();

            Control content;

            if (!AuthSession.IsSuperAdmin)
            {
                content = new StatePanel("Access denied", "This console is only available to Super Admin accounts.");
            }
            else
            {
                content = key switch
                {
                    "dashboard" => CreateDashboard(),
                    "plans" => new PlanManagementControl(),
                    "subscribers" => new SubscribersControl(statusFilter, search),
                    "tenants" => CreateTenantManagement(),
                    "users" => new UserDirectoryControl(),
                    "terms" => new TermsManagementControl(),
                    _ => CreateAccount()
                };
            }

            _contentPanel.Controls.Add(content);
        }

        private Control CreateDashboard()
        {
            var dashboard = new SuperAdminDashboardControl();
            dashboard.NavigateRequested += (key, status, search) => SetActiveModule(key, status, search);
            return dashboard;
        }

        private Control CreateTenantManagement()
        {
            var tenants = new TenantManagementControl();
            tenants.NavigateRequested += (key, status, search) => SetActiveModule(key, status, search);
            return tenants;
        }

        private Control CreateAccount()
        {
            var account = new SuperAdminAccountControl();

            account.ProfileUpdated += fullName =>
            {
                _accountButton.Text = fullName + "  ▾";
                _accountNameItem.Text = fullName;
            };

            return account;
        }
    }
}