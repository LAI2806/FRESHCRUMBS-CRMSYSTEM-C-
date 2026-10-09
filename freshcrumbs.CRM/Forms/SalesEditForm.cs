using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class SalesEditForm : Form
    {
        public SalesTransactionModel Result { get; private set; } = new();
        public List<TransactionItemModel> ResultItems { get; private set; } = new();

        private readonly bool _isEditMode;
        private readonly List<CustomerModel> _customers;
        private readonly int _companyId;
        private bool _lookingUpCustomer;
        private readonly List<PromotionModel> _promotions;
        private readonly List<ProductModel> _products;
        private readonly List<TransactionItemModel> _items = new();
        private readonly List<BranchModel> _branches;
        private readonly BranchModel? _myBranch;
        private readonly int? _editBranchId;
        private readonly int? _editPromotionId;
        private ComboBox? _branchBox;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);
        private static readonly System.Globalization.CultureInfo PesoCulture = new System.Globalization.CultureInfo("en-PH");

        // Loyalty earning rule (must match the API): PHP 10 of Final Amount = 1 point,
        // rounded down, max 100 points per sale. The API recalculates it on save.
        private const decimal LoyaltyAmountPerPoint = 10m;
        private const int MaxPointsEarnedPerSale = 100;

        private TextBox _customerCodeBox = null!;
        private TextBox _customerNameBox = null!;
        private Label _customerLookupLabel = null!;
        private CustomerModel? _selectedCustomer;
        private DateTimePicker _datePicker = null!;
        private DataGridView _itemsGrid = null!;
        private Button _addItemButton = null!;
        private Button _removeItemButton = null!;
        private Label _totalAmountLabel = null!;
        private ComboBox _promotionBox = null!;
        private Label _discountLabel = null!;
        private Label _promotionMessageLabel = null!;
        private Label _finalAmountLabel = null!;
        private Label _customerDiscountLabel = null!;
        private Label _totalDiscountLabel = null!;
        private Label _customerDiscountMessageLabel = null!;
        private NumericUpDown _pointsUsedBox = null!;
        private NumericUpDown _pointsEarnedBox = null!;
        private ComboBox _paymentMethodBox = null!;
        private ComboBox _statusBox = null!;
        private Label _errorLabel = null!;
        private int? _selectedCustomerLoyaltyBalance;

        public SalesEditForm(
            SalesTransactionModel? existingTransaction,
            List<CustomerModel> customers,
            List<PromotionModel> promotions,
            List<ProductModel> products,
            List<BranchModel>? branches = null,
            BranchModel? myBranch = null,
            int companyId = 0)
        {
            _isEditMode = existingTransaction != null;
            _customers = customers;
            _companyId = companyId;
            _promotions = promotions;
            _products = products;
            _branches = branches ?? new List<BranchModel>();
            _myBranch = myBranch;
            _editBranchId = existingTransaction?.BranchId;
            _editPromotionId = existingTransaction?.PromotionId;

            InitializeForm();
            InitializeControls();

            if (existingTransaction != null)
            {
                Result = new SalesTransactionModel
                {
                    TransactionId = existingTransaction.TransactionId,
                    CustomerId = existingTransaction.CustomerId,
                    PromotionId = existingTransaction.PromotionId,
                    BranchId = existingTransaction.BranchId,
                    TransactionDate = existingTransaction.TransactionDate,
                    TotalAmount = existingTransaction.TotalAmount,
                    DiscountAmount = existingTransaction.DiscountAmount,
                    CustomerDiscountAmount = existingTransaction.CustomerDiscountAmount,
                    PointsUsed = existingTransaction.PointsUsed,
                    PointsEarned = existingTransaction.PointsEarned,
                    FinalAmount = existingTransaction.FinalAmount,
                    PaymentMethod = existingTransaction.PaymentMethod,
                    Status = existingTransaction.Status
                };

                // The customer is locked in edit mode: the update endpoint never changes a
                // transaction's CustomerId, so the existing customer is shown read-only.
                var existingCustomer = _customers.FirstOrDefault(c => c.CustomerId == Result.CustomerId);
                _customerCodeBox.Text = existingCustomer?.CustomerCode ?? string.Empty;
                _customerCodeBox.ReadOnly = true;
                _customerCodeBox.TabStop = false;
                _customerCodeBox.BackColor = HeaderRowColor;
                _datePicker.Value = Result.TransactionDate;
                _promotionBox.SelectedValue = Result.PromotionId ?? 0;
                _pointsUsedBox.Value = Result.PointsUsed;
                _pointsEarnedBox.Value = Result.PointsEarned;
                _paymentMethodBox.Text = Result.PaymentMethod;
                _statusBox.Text = Result.Status;

                // Points Used is derived from the promotion, never from the stored
                // value alone, so an existing transaction is re-checked on open.
                UpdateSelectedCustomerBalance();
                ApplyPointsRedemptionIfNeeded();
                RecalculateDiscount(GetEffectiveTotal());
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Sales Transaction" : "Add Sales Transaction";
            Width = 620;
            Height = 700;
            MinimumSize = new Size(620, 450);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
        }

        private void InitializeControls()
        {
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PageBg
            };

            var root = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(30, 25, 30, 20)
            };

            var titleLabel = new Label
            {
                Text = _isEditMode ? "Edit Sales Transaction" : "Add New Sales Transaction",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            root.Controls.Add(titleLabel);

            AddSectionLabel(root, "CUSTOMER CODE");
            _customerCodeBox = new TextBox
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Enter Customer Code (e.g. CUST-001)",
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_customerCodeBox);

            AddSectionLabel(root, "CUSTOMER NAME");
            _customerNameBox = new TextBox
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true,
                TabStop = false,
                BackColor = HeaderRowColor,
                ForeColor = TextDark,
                PlaceholderText = "Filled in automatically from the Customer Code",
                Margin = new Padding(0, 0, 0, 2)
            };
            root.Controls.Add(_customerNameBox);

            _customerLookupLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = false,
                Width = 540,
                Height = 20,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_customerLookupLabel);

            AddSectionLabel(root, "TRANSACTION DATE");
            _datePicker = new DateTimePicker
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Format = DateTimePickerFormat.Short,
                Margin = new Padding(0, 0, 0, 14)
            };
            // The date decides whether a promotion is valid, which changes Final Amount
            // and therefore Points Earned.
            _datePicker.ValueChanged += (s, e) => RecalculateDiscount(GetEffectiveTotal());
            root.Controls.Add(_datePicker);

            // PREMIUM (Branching): ADMIN picks the branch (defaults to their own); MANAGER / STAFF always sell
            // from their assigned branch. The API applies the same rule.
            if (!_isEditMode && TenantCapabilities.HasBranching)
            {
                AddSectionLabel(root, "BRANCH");

                if (AuthSession.IsAdmin)
                {
                    var branchOptions = new List<BranchOption>();

                    if (_myBranch == null)
                    {
                        branchOptions.Add(new BranchOption { BranchId = null, Name = "-- Select a branch --" });
                    }

                    // The admin's own branch (if any) is listed first, so it is the default selection.
                    branchOptions.AddRange(_branches
                        .Where(b => string.Equals(b.Status, "Active", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(b => b.BranchId == _myBranch?.BranchId ? 0 : 1)
                        .ThenBy(b => b.BranchName)
                        .Select(b => new BranchOption { BranchId = b.BranchId, Name = b.BranchName }));

                    _branchBox = new ComboBox
                    {
                        Width = 540,
                        Height = 34,
                        Font = new Font("Segoe UI", 10.5f),
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        FlatStyle = FlatStyle.Flat,
                        Margin = new Padding(0, 0, 0, 14),
                        DataSource = branchOptions,
                        DisplayMember = "Name"
                    };
                    root.Controls.Add(_branchBox);
                }
                else
                {
                    root.Controls.Add(new TextBox
                    {
                        Width = 540,
                        Height = 34,
                        Font = new Font("Segoe UI", 10.5f),
                        BorderStyle = BorderStyle.FixedSingle,
                        ReadOnly = true,
                        TabStop = false,
                        BackColor = HeaderRowColor,
                        ForeColor = _myBranch == null ? Color.Firebrick : TextDark,
                        Text = _myBranch?.BranchName ?? "Not assigned to a branch - ask your administrator.",
                        Margin = new Padding(0, 0, 0, 14)
                    });
                }
            }

            if (!_isEditMode)
            {
                AddSectionLabel(root, "ITEMS");

                var itemsToolbar = new Panel { Width = 540, Height = 40, Margin = new Padding(0, 0, 0, 6) };

                _addItemButton = new Button
                {
                    Text = "+  Add Product",
                    Width = 140,
                    Height = 34,
                    Location = new Point(0, 3),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    BackColor = AccentColor,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                _addItemButton.FlatAppearance.BorderSize = 0;
                _addItemButton.Click += AddItemButton_Click;
                itemsToolbar.Controls.Add(_addItemButton);

                _removeItemButton = new Button
                {
                    Text = "Remove",
                    Width = 100,
                    Height = 34,
                    Location = new Point(150, 3),
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                    BackColor = Color.White,
                    ForeColor = Color.Firebrick,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Enabled = false
                };
                _removeItemButton.FlatAppearance.BorderSize = 1;
                _removeItemButton.FlatAppearance.BorderColor = Color.Firebrick;
                _removeItemButton.Click += RemoveItemButton_Click;
                itemsToolbar.Controls.Add(_removeItemButton);

                root.Controls.Add(itemsToolbar);

                _itemsGrid = new DataGridView
                {
                    Width = 540,
                    Height = 150,
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    GridColor = Color.FromArgb(235, 230, 225),
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    RowHeadersVisible = false,
                    Font = new Font("Segoe UI", 9.5f),
                    RowTemplate = { Height = 32 },
                    EnableHeadersVisualStyles = false,
                    Margin = new Padding(0, 0, 0, 14)
                };
                _itemsGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
                _itemsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                _itemsGrid.ColumnHeadersHeight = 34;
                _itemsGrid.SelectionChanged += ItemsGrid_SelectionChanged;

                root.Controls.Add(_itemsGrid);
            }

            if (TenantCapabilities.CanUsePromotions)
            {
                AddSectionLabel(root, "PROMOTION (OPTIONAL)");
            }

            var promotionOptions = BuildPromotionOptions();

            _promotionBox = new ComboBox
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 10),
                DataSource = promotionOptions,
                DisplayMember = "PromotionName",
                ValueMember = "PromotionId"
            };
            _promotionBox.SelectedIndexChanged += (s, e) =>
            {
                ApplyPointsRedemptionIfNeeded();
                RecalculateDiscount(GetEffectiveTotal());
            };

            if (_branchBox != null)
            {
                _branchBox.SelectedIndexChanged += (s, e) => RefreshPromotionOptions();
            }
            // A customer registered at another branch is not in this branch's list: Enter (or leaving the box)
            // looks the exact code up company-wide, so the existing customer is used instead of a duplicate.
            _customerCodeBox.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LookupCustomerByCodeAsync();
                }
            };
            _customerCodeBox.Leave += async (s, e) => await LookupCustomerByCodeAsync();

            _customerCodeBox.TextChanged += (s, e) =>
            {
                var previousCustomer = _selectedCustomer;
                ResolveCustomerFromCode();

                // Only a change in the resolved customer (the old SelectedIndexChanged
                // equivalent) needs the balance and promotion re-evaluated.
                if (ReferenceEquals(previousCustomer, _selectedCustomer))
                {
                    return;
                }

                // Switching customers changes the available balance, so the
                // promotion has to be re-evaluated and redisplayed as well.
                UpdateSelectedCustomerBalance();
                ApplyPointsRedemptionIfNeeded();
                RecalculateDiscount(GetEffectiveTotal());
            };
            if (TenantCapabilities.CanUsePromotions)
            {
                root.Controls.Add(_promotionBox);
            }

            _promotionMessageLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.DarkOrange,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            if (TenantCapabilities.CanUsePromotions)
            {
                root.Controls.Add(_promotionMessageLabel);
            }

            _customerDiscountMessageLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.DarkOrange,
                AutoSize = true,
                MaximumSize = new Size(540, 0),
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_customerDiscountMessageLabel);

            AddSectionLabel(root, "DISCOUNT BREAKDOWN");
            var breakdownTable = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 5,
                Width = 540,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = HeaderRowColor,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(0, 0, 0, 14)
            };
            breakdownTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            breakdownTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _totalAmountLabel = CreateBreakdownValueLabel(12, TextDark);
            _customerDiscountLabel = CreateBreakdownValueLabel(12, Color.Firebrick);
            _discountLabel = CreateBreakdownValueLabel(12, Color.Firebrick);
            _totalDiscountLabel = CreateBreakdownValueLabel(12, Color.Firebrick);
            _finalAmountLabel = CreateBreakdownValueLabel(16, AccentColor);

            int breakdownRow = 0;
            AddBreakdownRow(breakdownTable, breakdownRow++, "Subtotal", _totalAmountLabel);
            AddBreakdownRow(breakdownTable, breakdownRow++, "Customer Discount", _customerDiscountLabel);

            if (TenantCapabilities.CanUsePromotions)
            {
                AddBreakdownRow(breakdownTable, breakdownRow++, "Promotion Discount", _discountLabel);
                AddBreakdownRow(breakdownTable, breakdownRow++, "Total Discount", _totalDiscountLabel);
            }

            AddBreakdownRow(breakdownTable, breakdownRow++, "Final Amount", _finalAmountLabel);
            breakdownTable.RowCount = breakdownRow;
            root.Controls.Add(breakdownTable);

            _pointsUsedBox = TenantCapabilities.CanUseLoyalty
                ? AddNumericField(root, "POINTS USED")
                : new NumericUpDown { Minimum = 0, Maximum = 999999 };

            // Points Used is always system-calculated from the selected promotion.
            // Staff can never type or spin a value here.
            _pointsUsedBox.ReadOnly = true;
            _pointsUsedBox.Enabled = false;

            _pointsEarnedBox = TenantCapabilities.CanUseLoyalty
                ? AddNumericField(root, "POINTS EARNED")
                : new NumericUpDown { Minimum = 0, Maximum = 999999 };

            // Points Earned is always system-calculated from the Final Amount.
            // Staff can never type or spin a value here.
            _pointsEarnedBox.ReadOnly = true;
            _pointsEarnedBox.Enabled = false;

            AddSectionLabel(root, "PAYMENT METHOD");
            _paymentMethodBox = new ComboBox
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _paymentMethodBox.Items.AddRange(new object[] { "Cash", "GCash", "Card", "Bank Transfer" });
            _paymentMethodBox.SelectedIndex = 0;
            root.Controls.Add(_paymentMethodBox);

            _statusBox = new ComboBox
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14)
            };
            _statusBox.Items.AddRange(new object[] { "Completed", "Cancelled" });
            _statusBox.SelectedIndex = 0;

            // Status is only shown/editable in edit mode, where it drives the
            // existing cancellation workflow. New transactions are always
            // Completed and staff never choose the status for them.
            if (_isEditMode)
            {
                AddSectionLabel(root, "STATUS");
                root.Controls.Add(_statusBox);
            }

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_errorLabel);

            scrollPanel.Controls.Add(root);

            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = Color.White
            };

            var footerBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = BorderColor
            };
            footerPanel.Controls.Add(footerBorder);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 120,
                Height = 42,
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.BorderColor = BorderColor;
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            footerPanel.Controls.Add(cancelButton);

            var saveButton = new Button
            {
                Text = _isEditMode ? "Save Changes" : "Add Transaction",
                Width = 165,
                Height = 42,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;
            footerPanel.Controls.Add(saveButton);

            void PositionFooterButtons()
            {
                int groupWidth = cancelButton.Width + 10 + saveButton.Width;
                int startX = (footerPanel.Width - groupWidth) / 2;
                int y = (footerPanel.Height - saveButton.Height) / 2 + 5;

                cancelButton.Location = new Point(startX, y);
                saveButton.Location = new Point(startX + cancelButton.Width + 10, y);
            }

            footerPanel.Resize += (s, e) => PositionFooterButtons();

            AcceptButton = null;
            CancelButton = cancelButton;

            Controls.Add(scrollPanel);
            Controls.Add(footerPanel);

            PositionFooterButtons();

            UpdateSelectedCustomerBalance();

            if (!_isEditMode)
            {
                RefreshItemsGrid();
            }
        }

        private void AddSectionLabel(TableLayoutPanel root, string text)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(label);
        }

        private Label CreateBreakdownValueLabel(float fontSize, Color color)
        {
            return new Label
            {
                Text = "₱0.00",
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                ForeColor = color,
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 4, 0, 4)
            };
        }

        private void AddBreakdownRow(TableLayoutPanel table, int row, string caption, Label valueLabel)
        {
            var captionLabel = new Label
            {
                Text = caption,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 0, 4)
            };
            table.Controls.Add(captionLabel, 0, row);
            table.Controls.Add(valueLabel, 1, row);
        }

        private NumericUpDown AddNumericField(TableLayoutPanel root, string labelText)
        {
            AddSectionLabel(root, labelText);

            var numericBox = new NumericUpDown
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(numericBox);

            return numericBox;
        }

        private void RefreshItemsGrid()
        {
            var displayRows = _items.Select(i => new
            {
                Product = GetProductName(i.ProductId),
                i.Quantity,
                i.UnitPrice,
                i.Subtotal
            }).ToList();

            _itemsGrid.AutoGenerateColumns = true;
            _itemsGrid.DataSource = null;
            _itemsGrid.DataSource = displayRows;

            RecalculateDiscount(GetCurrentTotal());
        }

        private string GetProductName(int productId)
        {
            var product = _products.FirstOrDefault(p => p.ProductId == productId);
            return product?.ProductName ?? $"Product #{productId}";
        }

        private decimal GetCurrentTotal()
        {
            return _items.Sum(i => i.Subtotal);
        }

        private void RecalculateDiscount(decimal totalAmount)
        {
            _promotionMessageLabel.Text = "";

            PromotionModel? appliedPromotion = null;
            decimal promotionDiscount = 0m;

            int promotionId = (int)(_promotionBox.SelectedValue ?? 0);
            var promotion = promotionId == 0 ? null : _promotions.FirstOrDefault(p => p.PromotionId == promotionId);

            if (promotion != null)
            {
                if (!string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    _promotionMessageLabel.Text = "This promotion is not active.";
                }
                else if (_datePicker.Value < promotion.StartDate || _datePicker.Value > promotion.EndDate)
                {
                    _promotionMessageLabel.Text = "This promotion is not valid for the selected date.";
                }
                else if (totalAmount < promotion.MinimumPurchase)
                {
                    _promotionMessageLabel.Text = $"Minimum purchase of {promotion.MinimumPurchase.ToString("C2", PesoCulture)} required.";
                }
                else if (!HasSufficientLoyaltyPoints(promotion))
                {
                    _promotionMessageLabel.Text =
                        $"Customer does not have enough loyalty points. This promotion requires {promotion.RequiredLoyaltyPoints}, customer has {GetAvailableLoyaltyPoints()}.";
                }
                else
                {
                    decimal discount = string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                        ? totalAmount * (promotion.DiscountValue / 100m)
                        : promotion.DiscountValue;

                    if (discount > totalAmount)
                    {
                        discount = totalAmount;
                    }

                    _promotionMessageLabel.Text = promotion.RequiredLoyaltyPoints > 0
                        ? $"Promotion applied. {promotion.RequiredLoyaltyPoints} loyalty points will be used."
                        : "Promotion applied.";

                    appliedPromotion = promotion;
                    promotionDiscount = discount;
                }
            }

            decimal customerDiscount = CalculateCustomerDiscount(
                totalAmount - promotionDiscount,
                appliedPromotion,
                out string customerDiscountMessage);

            decimal totalDiscount = promotionDiscount + customerDiscount;

            _totalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
            _customerDiscountLabel.Text = customerDiscount.ToString("C2", PesoCulture);
            _discountLabel.Text = promotionDiscount.ToString("C2", PesoCulture);
            _totalDiscountLabel.Text = totalDiscount.ToString("C2", PesoCulture);
            _finalAmountLabel.Text = (totalAmount - totalDiscount).ToString("C2", PesoCulture);
            _customerDiscountMessageLabel.Text = customerDiscountMessage;

            _pointsEarnedBox.Value = CalculatePointsEarned(totalAmount - totalDiscount);
        }

        private static int CalculatePointsEarned(decimal finalAmount)
        {
            if (finalAmount <= 0m)
            {
                return 0;
            }

            decimal points = Math.Floor(finalAmount / LoyaltyAmountPerPoint);
            return (int)Math.Min(points, MaxPointsEarnedPerSale);
        }

        private decimal CalculateCustomerDiscount(decimal amountAfterPromotion, PromotionModel? appliedPromotion, out string message)
        {
            message = "";

            if (_isEditMode && (int)(_promotionBox.SelectedValue ?? 0) == (Result.PromotionId ?? 0))
            {
                message = Result.CustomerDiscountAmount > 0m
                    ? "Customer discount from the original transaction is retained."
                    : "";
                return Math.Min(Result.CustomerDiscountAmount, Math.Max(amountAfterPromotion, 0m));
            }

            var customer = _isEditMode
                ? _customers.FirstOrDefault(c => c.CustomerId == Result.CustomerId)
                : _selectedCustomer;

            if (customer == null)
            {
                return 0m;
            }

            var bestEligibility = (customer.DiscountEligibilities ?? new List<CustomerDiscountEligibilityModel>())
                .Where(e => string.Equals(e.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase))
                .Select(e => new { e.Category, Rate = GetCustomerDiscountRate(e.Category) })
                .Where(x => x.Rate > 0m)
                .OrderByDescending(x => x.Rate)
                .FirstOrDefault();

            if (bestEligibility == null)
            {
                return 0m;
            }

            if (appliedPromotion != null && !string.IsNullOrWhiteSpace(appliedPromotion.EligibilityCategory))
            {
                message = $"{bestEligibility.Category} discount not applied: the selected promotion is already an eligibility discount.";
                return 0m;
            }

            if (amountAfterPromotion <= 0m)
            {
                return 0m;
            }

            message = $"{bestEligibility.Category} (verified): {bestEligibility.Rate * 100m:0.##}% customer discount applied.";
            return Math.Round(amountAfterPromotion * bestEligibility.Rate, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal GetCustomerDiscountRate(string category)
        {
            return category switch
            {
                "Senior Citizen" => 0.20m,
                "PWD" => 0.20m,
                _ => 0m
            };
        }

        private void ResolveCustomerFromCode()
        {
            string code = _customerCodeBox.Text.Trim();

            _selectedCustomer = null;
            _customerNameBox.Text = string.Empty;
            _customerLookupLabel.Text = string.Empty;

            if (code.Length == 0)
            {
                return;
            }

            // Lookup is by the existing CustomerCode only: trimmed and case-insensitive.
            var customer = _customers.FirstOrDefault(c =>
                string.Equals(c.CustomerCode.Trim(), code, StringComparison.OrdinalIgnoreCase));

            if (customer == null)
            {
                _customerLookupLabel.Text = _isEditMode || _companyId <= 0
                    ? "Customer code not found."
                    : "Customer code not found here. Press Enter to look it up.";
                return;
            }

            // New transactions can only be created for active customers (the API enforces
            // this too). In edit mode the customer is locked, so an inactive customer is
            // still displayed.
            if (!_isEditMode && !string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                _customerLookupLabel.Text = "This customer is inactive and cannot be selected.";
                return;
            }

            _selectedCustomer = customer;
            _customerNameBox.Text = $"{customer.FirstName} {customer.LastName}";
        }

        private async Task LookupCustomerByCodeAsync()
        {
            string code = _customerCodeBox.Text.Trim();

            if (_isEditMode || _companyId <= 0 || _lookingUpCustomer || code.Length == 0 || _selectedCustomer != null)
            {
                return;
            }

            _lookingUpCustomer = true;

            try
            {
                var found = await new ApiService().LookupCustomerByCodeAsync(_companyId, code);

                if (IsDisposed)
                {
                    return;
                }

                if (found == null)
                {
                    _customerLookupLabel.Text = "Customer code not found.";
                    return;
                }

                if (!_customers.Any(c => c.CustomerId == found.CustomerId))
                {
                    _customers.Add(found);
                }

                // Re-run the normal selection (balance, promotion and discount re-evaluated as usual).
                _customerCodeBox.Text = string.Empty;
                _customerCodeBox.Text = found.CustomerCode;
            }
            catch (Exception ex)
            {
                _customerLookupLabel.Text = BranchUi.GetMessage(ex);
            }
            finally
            {
                _lookingUpCustomer = false;
            }
        }

        private void UpdateSelectedCustomerBalance()
        {
            if (_selectedCustomer is CustomerModel selectedCustomer)
            {
                _selectedCustomerLoyaltyBalance = selectedCustomer.LoyaltyPoints;
            }
            else
            {
                _selectedCustomerLoyaltyBalance = null;
            }
        }

        private void ApplyPointsRedemptionIfNeeded()
        {
            // Points Used stays disabled in every branch. The validation message
            // itself is written by RecalculateDiscount, which runs after this and
            // clears _promotionMessageLabel on entry.
            _pointsUsedBox.ReadOnly = true;
            _pointsUsedBox.Enabled = false;

            var promotion = GetSelectedPromotion();

            if (promotion == null || promotion.RequiredLoyaltyPoints <= 0)
            {
                _pointsUsedBox.Value = 0;
                return;
            }

            _pointsUsedBox.Value = HasSufficientLoyaltyPoints(promotion)
                ? promotion.RequiredLoyaltyPoints
                : 0;
        }

        private PromotionModel? GetSelectedPromotion()
        {
            int promotionId = (int)(_promotionBox.SelectedValue ?? 0);
            return promotionId == 0 ? null : _promotions.FirstOrDefault(p => p.PromotionId == promotionId);
        }

        private int GetAvailableLoyaltyPoints()
        {
            int available = _selectedCustomerLoyaltyBalance ?? 0;

            // When editing, the points this same transaction already consumed are
            // still spendable by it. Without this, reopening and re-saving an
            // unchanged transaction would fail its own validation.
            if (_isEditMode &&
                _selectedCustomer?.CustomerId is int selectedCustomerId &&
                selectedCustomerId == Result.CustomerId)
            {
                available += Result.PointsUsed;
            }

            return available;
        }

        private bool HasSufficientLoyaltyPoints(PromotionModel promotion)
        {
            return ValidationHelper.HasSufficientLoyaltyPoints(
                GetAvailableLoyaltyPoints(),
                promotion.RequiredLoyaltyPoints);
        }

        // The sale's branch as the form knows it (the API sets the real one): edit = the sale's branch,
        // ADMIN = the selected branch, MANAGER / STAFF = their assigned branch, no branches = null.
        private int? GetSaleBranchId()
        {
            if (_isEditMode)
            {
                return _editBranchId;
            }

            if (_branchBox != null)
            {
                return (_branchBox.SelectedItem as BranchOption)?.BranchId;
            }

            return _myBranch?.BranchId;
        }

        // Company-wide promotions plus those of the sale's branch (the API rejects any other).
        private List<PromotionOption> BuildPromotionOptions()
        {
            int? branchId = GetSaleBranchId();

            var options = new List<PromotionOption> { new PromotionOption { PromotionId = 0, PromotionName = "-- None --" } };
            options.AddRange(_promotions
                .Where(p => p.BranchId == null
                            || (branchId != null && p.BranchId == branchId)
                            || (_isEditMode && p.PromotionId == _editPromotionId))
                .Select(p => new PromotionOption { PromotionId = p.PromotionId, PromotionName = p.PromotionName }));

            return options;
        }

        private void RefreshPromotionOptions()
        {
            int selected = (int)(_promotionBox.SelectedValue ?? 0);
            var options = BuildPromotionOptions();

            _promotionBox.DataSource = options;
            _promotionBox.SelectedValue = options.Any(o => o.PromotionId == selected) ? selected : 0;
        }

        private decimal GetEffectiveTotal()
        {
            return _isEditMode ? Result.TotalAmount : GetCurrentTotal();
        }

        private void ItemsGrid_SelectionChanged(object? sender, EventArgs e)
        {
            _removeItemButton.Enabled = _itemsGrid.SelectedRows.Count > 0;
        }

        private void AddItemButton_Click(object? sender, EventArgs e)
        {
            using var form = new TransactionItemEditForm(_products);
            if (form.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            form.Result.Subtotal = form.Result.Quantity * form.Result.UnitPrice;
            _items.Add(form.Result);
            RefreshItemsGrid();
        }

        private void RemoveItemButton_Click(object? sender, EventArgs e)
        {
            if (_itemsGrid.SelectedRows.Count == 0)
            {
                return;
            }

            int index = _itemsGrid.SelectedRows[0].Index;
            _items.RemoveAt(index);
            RefreshItemsGrid();
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            // In edit mode the customer is locked, so the existing CustomerId is kept.
            int customerId = Result.CustomerId;

            if (!_isEditMode)
            {
                if (_selectedCustomer == null)
                {
                    _errorLabel.Text = _lookingUpCustomer
                        ? "Looking up the customer code. Please try again in a moment."
                        : "Please enter a valid Customer Code.";
                    return;
                }

                customerId = _selectedCustomer.CustomerId;
            }

            if (!_isEditMode && _items.Count == 0)
            {
                _errorLabel.Text = "Please add at least one product.";
                return;
            }

            int? branchId = Result.BranchId;

            if (!_isEditMode && TenantCapabilities.HasBranching)
            {
                if (AuthSession.IsAdmin)
                {
                    branchId = (_branchBox?.SelectedItem as BranchOption)?.BranchId;

                    if (branchId == null)
                    {
                        _errorLabel.Text = "Please select the branch for this sale.";
                        return;
                    }
                }
                else
                {
                    if (_myBranch == null)
                    {
                        _errorLabel.Text = "You are not assigned to a branch. Ask your administrator to assign you before recording sales.";
                        return;
                    }

                    branchId = _myBranch.BranchId;
                }
            }

            if (string.IsNullOrWhiteSpace(_paymentMethodBox.Text))
            {
                _errorLabel.Text = "Please select a payment method.";
                return;
            }

            if (_datePicker.Value.Date > DateTime.Today)
            {
                _errorLabel.Text = "The sale date cannot be in the future.";
                return;
            }

            decimal totalAmount = GetEffectiveTotal();
            int promotionId = (int)(_promotionBox.SelectedValue ?? 0);

            decimal discountAmount = 0;
            PromotionModel? appliedPromotion = null;
            var promotion = promotionId == 0 ? null : _promotions.FirstOrDefault(p => p.PromotionId == promotionId);

            // A points-based promotion cannot be saved onto a customer who cannot
            // pay for it. Returning here leaves DialogResult unset, so SalesControl
            // never reaches CreateSalesTransactionAsync / UpdateSalesTransactionAsync.
            if (promotion != null && !HasSufficientLoyaltyPoints(promotion))
            {
                _errorLabel.Text =
                    $"Customer does not have enough loyalty points for \"{promotion.PromotionName}\". " +
                    $"Required: {promotion.RequiredLoyaltyPoints}. Available: {GetAvailableLoyaltyPoints()}.";
                return;
            }

            if (promotion != null &&
                string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                _datePicker.Value >= promotion.StartDate &&
                _datePicker.Value <= promotion.EndDate &&
                totalAmount >= promotion.MinimumPurchase)
            {
                discountAmount = string.Equals(promotion.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                    ? totalAmount * (promotion.DiscountValue / 100m)
                    : promotion.DiscountValue;

                if (discountAmount > totalAmount)
                {
                    discountAmount = totalAmount;
                }

                appliedPromotion = promotion;
            }

            decimal customerDiscountAmount = CalculateCustomerDiscount(
                totalAmount - discountAmount,
                appliedPromotion,
                out _);

            int? originalPromotionId = Result.PromotionId;
            int originalPointsUsed = Result.PointsUsed;
            int originalPointsEarned = Result.PointsEarned;

            Result.CustomerId = customerId;
            Result.BranchId = branchId;
            Result.PromotionId = promotionId == 0 ? null : promotionId;
            Result.TransactionDate = _datePicker.Value;
            Result.TotalAmount = totalAmount;
            Result.DiscountAmount = discountAmount;
            Result.CustomerDiscountAmount = customerDiscountAmount;
            // Never read Points Used back from the control; derive it from the rule.
            Result.PointsUsed = promotion?.RequiredLoyaltyPoints > 0
                ? promotion.RequiredLoyaltyPoints
                : 0;
            Result.FinalAmount = totalAmount - discountAmount - customerDiscountAmount;
            // Never read Points Earned back from the control; derive it from the rule.
            Result.PointsEarned = CalculatePointsEarned(Result.FinalAmount);

            if (!TenantCapabilities.CanUsePromotions)
            {
                Result.PromotionId = _isEditMode ? originalPromotionId : null;
            }

            if (!TenantCapabilities.CanUseLoyalty)
            {
                Result.PointsUsed = _isEditMode ? originalPointsUsed : 0;
                Result.PointsEarned = _isEditMode ? originalPointsEarned : 0;
            }

            Result.PaymentMethod = _paymentMethodBox.Text;
            // New transactions are always Completed. Status is only user-editable
            // in edit mode, where it also carries Cancelled for the existing
            // cancellation workflow.
            Result.Status = _isEditMode ? _statusBox.Text : "Completed";

            ResultItems = _items;

            DialogResult = DialogResult.OK;
            Close();
        }

        private class BranchOption
        {
            public int? BranchId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class PromotionOption
        {
            public int PromotionId { get; set; }
            public string PromotionName { get; set; } = string.Empty;
        }
    }
}