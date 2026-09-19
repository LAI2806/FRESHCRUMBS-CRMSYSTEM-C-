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
        private readonly List<PromotionModel> _promotions;
        private readonly List<ProductModel> _products;
        private readonly List<TransactionItemModel> _items = new();

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);
        private static readonly System.Globalization.CultureInfo PesoCulture = new System.Globalization.CultureInfo("en-PH");

        private ComboBox _customerBox = null!;
        private DateTimePicker _datePicker = null!;
        private DataGridView _itemsGrid = null!;
        private Button _addItemButton = null!;
        private Button _removeItemButton = null!;
        private Label _totalAmountLabel = null!;
        private ComboBox _promotionBox = null!;
        private Label _discountLabel = null!;
        private Label _promotionMessageLabel = null!;
        private Label _finalAmountLabel = null!;
        private NumericUpDown _pointsUsedBox = null!;
        private NumericUpDown _pointsEarnedBox = null!;
        private ComboBox _paymentMethodBox = null!;
        private ComboBox _statusBox = null!;
        private Label _errorLabel = null!;
        private int? _selectedCustomerLoyaltyBalance;

        public SalesEditForm(SalesTransactionModel? existingTransaction, List<CustomerModel> customers, List<PromotionModel> promotions, List<ProductModel> products)
        {
            _isEditMode = existingTransaction != null;
            _customers = customers;
            _promotions = promotions;
            _products = products;

            InitializeForm();
            InitializeControls();

            if (existingTransaction != null)
            {
                Result = new SalesTransactionModel
                {
                    TransactionId = existingTransaction.TransactionId,
                    CustomerId = existingTransaction.CustomerId,
                    PromotionId = existingTransaction.PromotionId,
                    TransactionDate = existingTransaction.TransactionDate,
                    TotalAmount = existingTransaction.TotalAmount,
                    DiscountAmount = existingTransaction.DiscountAmount,
                    PointsUsed = existingTransaction.PointsUsed,
                    PointsEarned = existingTransaction.PointsEarned,
                    FinalAmount = existingTransaction.FinalAmount,
                    PaymentMethod = existingTransaction.PaymentMethod,
                    Status = existingTransaction.Status
                };

                _customerBox.SelectedValue = Result.CustomerId;
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

            AddSectionLabel(root, "CUSTOMER");
            _customerBox = new ComboBox
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14),
                DataSource = _customers,
                DisplayMember = "FirstName",
                ValueMember = "CustomerId"
            };
            root.Controls.Add(_customerBox);

            AddSectionLabel(root, "TRANSACTION DATE");
            _datePicker = new DateTimePicker
            {
                Width = 540,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Format = DateTimePickerFormat.Short,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_datePicker);

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

            AddSectionLabel(root, "TOTAL AMOUNT");
            _totalAmountLabel = new Label
            {
                Text = "₱0.00",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_totalAmountLabel);

            AddSectionLabel(root, "PROMOTION (OPTIONAL)");
            var promotionOptions = new List<PromotionOption> { new PromotionOption { PromotionId = 0, PromotionName = "-- None --" } };
            promotionOptions.AddRange(_promotions.Select(p => new PromotionOption { PromotionId = p.PromotionId, PromotionName = p.PromotionName }));

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
            _customerBox.SelectedIndexChanged += (s, e) =>
            {
                // Switching customers changes the available balance, so the
                // promotion has to be re-evaluated and redisplayed as well.
                UpdateSelectedCustomerBalance();
                ApplyPointsRedemptionIfNeeded();
                RecalculateDiscount(GetEffectiveTotal());
            };
            root.Controls.Add(_promotionBox);

            _promotionMessageLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.DarkOrange,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_promotionMessageLabel);

            AddSectionLabel(root, "DISCOUNT");
            _discountLabel = new Label
            {
                Text = "₱0.00",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_discountLabel);

            AddSectionLabel(root, "FINAL AMOUNT");
            _finalAmountLabel = new Label
            {
                Text = "₱0.00",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = AccentColor,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_finalAmountLabel);

            _pointsUsedBox = AddNumericField(root, "POINTS USED");

            // Points Used is always system-calculated from the selected promotion.
            // Staff can never type or spin a value here.
            _pointsUsedBox.ReadOnly = true;
            _pointsUsedBox.Enabled = false;

            _pointsEarnedBox = AddNumericField(root, "POINTS EARNED");

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
            _totalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
            _promotionMessageLabel.Text = "";

            int promotionId = (int)(_promotionBox.SelectedValue ?? 0);

            if (promotionId == 0)
            {
                _discountLabel.Text = "₱0.00";
                _finalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
                return;
            }

            var promotion = _promotions.FirstOrDefault(p => p.PromotionId == promotionId);

            if (promotion == null)
            {
                _discountLabel.Text = "₱0.00";
                _finalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
                return;
            }

            if (!string.Equals(promotion.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                _promotionMessageLabel.Text = "This promotion is not active.";
                _discountLabel.Text = "₱0.00";
                _finalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
                return;
            }

            if (_datePicker.Value < promotion.StartDate || _datePicker.Value > promotion.EndDate)
            {
                _promotionMessageLabel.Text = "This promotion is not valid for the selected date.";
                _discountLabel.Text = "₱0.00";
                _finalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
                return;
            }

            if (totalAmount < promotion.MinimumPurchase)
            {
                _promotionMessageLabel.Text = $"Minimum purchase of {promotion.MinimumPurchase.ToString("C2", PesoCulture)} required.";
                _discountLabel.Text = "₱0.00";
                _finalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
                return;
            }

            if (!HasSufficientLoyaltyPoints(promotion))
            {
                _promotionMessageLabel.Text =
                    $"Customer does not have enough loyalty points. This promotion requires {promotion.RequiredLoyaltyPoints}, customer has {GetAvailableLoyaltyPoints()}.";
                _discountLabel.Text = "₱0.00";
                _finalAmountLabel.Text = totalAmount.ToString("C2", PesoCulture);
                return;
            }

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
            _discountLabel.Text = discount.ToString("C2", PesoCulture);
            _finalAmountLabel.Text = (totalAmount - discount).ToString("C2", PesoCulture);
        }

        private void UpdateSelectedCustomerBalance()
        {
            if (_customerBox.SelectedItem is CustomerModel selectedCustomer)
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
                _customerBox.SelectedValue is int selectedCustomerId &&
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

            if (_customerBox.SelectedValue is not int customerId)
            {
                _errorLabel.Text = "Please select a customer.";
                return;
            }

            if (!_isEditMode && _items.Count == 0)
            {
                _errorLabel.Text = "Please add at least one product.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_paymentMethodBox.Text))
            {
                _errorLabel.Text = "Please select a payment method.";
                return;
            }

            decimal totalAmount = GetEffectiveTotal();
            int promotionId = (int)(_promotionBox.SelectedValue ?? 0);

            decimal discountAmount = 0;
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
            }

            Result.CustomerId = customerId;
            Result.PromotionId = promotionId == 0 ? null : promotionId;
            Result.TransactionDate = _datePicker.Value;
            Result.TotalAmount = totalAmount;
            Result.DiscountAmount = discountAmount;
            // Never read Points Used back from the control; derive it from the rule.
            Result.PointsUsed = promotion?.RequiredLoyaltyPoints > 0
                ? promotion.RequiredLoyaltyPoints
                : 0;
            Result.PointsEarned = (int)_pointsEarnedBox.Value;
            Result.FinalAmount = totalAmount - discountAmount;
            Result.PaymentMethod = _paymentMethodBox.Text;
            // New transactions are always Completed. Status is only user-editable
            // in edit mode, where it also carries Cancelled for the existing
            // cancellation workflow.
            Result.Status = _isEditMode ? _statusBox.Text : "Completed";

            ResultItems = _items;

            DialogResult = DialogResult.OK;
            Close();
        }

        private class PromotionOption
        {
            public int PromotionId { get; set; }
            public string PromotionName { get; set; } = string.Empty;
        }
    }
}