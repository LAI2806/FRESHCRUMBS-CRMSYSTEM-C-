using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class TransactionItemEditForm : Form
    {
        public TransactionItemModel Result { get; private set; } = new();

        private readonly List<ProductModel> _products;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);

        private ComboBox _productBox = null!;
        private NumericUpDown _quantityBox = null!;
        private NumericUpDown _unitPriceBox = null!;
        private Label _errorLabel = null!;

        public TransactionItemEditForm(List<ProductModel> products)
        {
            _products = products;

            InitializeForm();
            InitializeControls();
        }

        private void InitializeForm()
        {
            Text = "Add Item";
            Width = 400;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(25, 20, 25, 20);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            KeyPreview = true;
        }

        private void InitializeControls()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var titleLabel = new Label
            {
                Text = "Add Product to Transaction",
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            root.Controls.Add(titleLabel);

            var productLabel = new Label
            {
                Text = "PRODUCT",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(productLabel);

            _productBox = new ComboBox
            {
                Width = 330,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 14),
                DataSource = _products,
                DisplayMember = "ProductName",
                ValueMember = "ProductId"
            };
            _productBox.SelectedIndexChanged += ProductBox_SelectedIndexChanged;
            root.Controls.Add(_productBox);

            var qtyLabel = new Label
            {
                Text = "QUANTITY",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(qtyLabel);

            _quantityBox = new NumericUpDown
            {
                Width = 330,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                Minimum = 1,
                Maximum = 99999,
                Value = 1,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_quantityBox);

            var priceLabel = new Label
            {
                Text = "UNIT PRICE",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            root.Controls.Add(priceLabel);

            _unitPriceBox = new NumericUpDown
            {
                Width = 330,
                Height = 34,
                Font = new Font("Segoe UI", 10.5f),
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 999999,
                Margin = new Padding(0, 0, 0, 14)
            };
            root.Controls.Add(_unitPriceBox);

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(_errorLabel);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 110,
                Height = 40,
                BackColor = Color.White,
                ForeColor = LabelGray,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            cancelButton.FlatAppearance.BorderSize = 1;
            cancelButton.FlatAppearance.BorderColor = BorderColor;
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            var saveButton = new Button
            {
                Text = "Add Item",
                Width = 140,
                Height = 40,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;

            int groupWidth = cancelButton.Width + 10 + saveButton.Width;
            int leftMargin = (330 - groupWidth) / 2;

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Margin = new Padding(leftMargin, 6, 0, 0)
            };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(saveButton);

            root.Controls.Add(buttonPanel);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            Controls.Add(root);

            if (_products.Count > 0)
            {
                _unitPriceBox.Value = _products[0].Price;
            }
        }

        private void ProductBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_productBox.SelectedItem is ProductModel selectedProduct)
            {
                _unitPriceBox.Value = selectedProduct.Price;
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";

            if (_productBox.SelectedValue is not int productId)
            {
                _errorLabel.Text = "Please select a product.";
                return;
            }

            if (_quantityBox.Value <= 0)
            {
                _errorLabel.Text = "Quantity must be greater than 0.";
                return;
            }
            Result.ProductId = productId;
            Result.Quantity = (int)_quantityBox.Value;
            Result.UnitPrice = _unitPriceBox.Value;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}