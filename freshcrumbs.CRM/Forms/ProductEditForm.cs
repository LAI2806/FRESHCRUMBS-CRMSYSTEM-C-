using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class ProductEditForm : Form
    {
        public ProductModel Result { get; private set; } = new();

        private readonly bool _isEditMode;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color PageBg = Color.White;
        private static readonly Color InvalidFieldColor = Color.FromArgb(255, 232, 232);
        private static readonly Color ValidFieldColor = Color.White;

        private TextBox _codeBox = null!;
        private TextBox _nameBox = null!;
        private ComboBox _categoryBox = null!;
        private TextBox _descriptionBox = null!;
        private NumericUpDown _priceBox = null!;
        private NumericUpDown _quantityBox = null!;
        private ComboBox _statusBox = null!;
        private Label _errorLabel = null!;

        public ProductEditForm(ProductModel? existingProduct, string suggestedCode = "")
        {
            _isEditMode = existingProduct != null;

            InitializeForm();
            InitializeControls();

            if (existingProduct == null && !string.IsNullOrEmpty(suggestedCode))
            {
                _codeBox.Text = suggestedCode;
            }

            if (existingProduct != null)
            {
                Result = new ProductModel
                {
                    ProductId = existingProduct.ProductId,
                    ProductCode = existingProduct.ProductCode,
                    ProductName = existingProduct.ProductName,
                    Category = existingProduct.Category,
                    Description = existingProduct.Description,
                    Price = existingProduct.Price,
                    Quantity = existingProduct.Quantity,
                    Status = existingProduct.Status
                };

                _codeBox.Text = Result.ProductCode;
                _nameBox.Text = Result.ProductName;
                _categoryBox.Text = Result.Category;
                _descriptionBox.Text = Result.Description;
                _priceBox.Value = Result.Price;
                _quantityBox.Value = Result.Quantity;
                _statusBox.Text = Result.Status;
            }
        }

        private void InitializeForm()
        {
            Text = _isEditMode ? "Edit Product" : "Add Product";
            Width = 460;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = PageBg;
            Font = new Font("Segoe UI", 9.5f);
            Padding = new Padding(30, 25, 30, 25);
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
                Text = _isEditMode ? "Edit Product" : "Add New Product",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = TextDark,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 18)
            };

            root.Controls.Add(titleLabel);

            _codeBox = AddField(root, "Product Code");

            _nameBox = AddField(root, "Product Name");

            var categoryLabel = new Label
            {
                Text = "CATEGORY",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 5)
            };

            root.Controls.Add(categoryLabel);

            _categoryBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextDark,
                BackColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDown,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 10)
            };

            _categoryBox.Items.AddRange(new object[]
            {
                "Cakes",
                "Cupcakes",
                "Pastries",
                "Cookies",
                "Bread",
                "Desserts",
                "Beverages",
                "Others"
            });

            root.Controls.Add(_categoryBox);

            _descriptionBox = AddField(root, "Description", true);

            _priceBox = AddNumericField(root, "Price", 0, 999999, true);

            _quantityBox = AddNumericField(root, "Quantity", 0, 999999, false);

            _statusBox = new ComboBox
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextDark,
                BackColor = Color.White,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 0, 10)
            };

            _statusBox.Items.AddRange(new object[]
            {
                "Active",
                "Inactive"
            });

            _statusBox.SelectedIndex = 0;

            if (_isEditMode)
            {
                var statusLabel = new Label
                {
                    Text = "STATUS",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = LabelGray,
                    AutoSize = true,
                    Margin = new Padding(0, 8, 0, 5)
                };

                root.Controls.Add(statusLabel);
                root.Controls.Add(_statusBox);
            }

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Firebrick,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 5)
            };

            root.Controls.Add(_errorLabel);

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 110,
                Height = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextDark,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0),
                DialogResult = DialogResult.Cancel
            };

            cancelButton.FlatAppearance.BorderColor = BorderColor;
            cancelButton.FlatAppearance.BorderSize = 1;

            var saveButton = new Button
            {
                Text = _isEditMode ? "Save Changes" : "Add Product",
                Width = 150,
                Height = 38,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = AccentColor,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0)
            };

            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;

            var buttonPanel = new FlowLayoutPanel
            {
                Width = 380,
                Height = 45,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                AutoSize = false,
                Margin = new Padding(0, 5, 0, 0)
            };

            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);

            root.Controls.Add(buttonPanel);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            Controls.Add(root);
        }

        private TextBox AddField(
            TableLayoutPanel root,
            string labelText,
            bool multiline = false)
        {
            var label = new Label
            {
                Text = labelText.ToUpper(),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 5)
            };

            root.Controls.Add(label);

            var box = new TextBox
            {
                Width = 380,
                Height = multiline ? 70 : 34,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextDark,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = multiline,
                Margin = new Padding(0, 0, 0, 10)
            };

            if (multiline)
            {
                box.ScrollBars = ScrollBars.Vertical;
            }

            root.Controls.Add(box);

            return box;
        }

        private NumericUpDown AddNumericField(
            TableLayoutPanel root,
            string labelText,
            decimal minimum,
            decimal maximum,
            bool isPrice)
        {
            var label = new Label
            {
                Text = labelText.ToUpper(),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 5)
            };

            root.Controls.Add(label);

            var box = new NumericUpDown
            {
                Width = 380,
                Height = 34,
                Font = new Font("Segoe UI", 10f),
                ForeColor = TextDark,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = isPrice ? 2 : 0,
                ThousandsSeparator = true,
                Margin = new Padding(0, 0, 0, 10)
            };

            root.Controls.Add(box);

            return box;
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            _errorLabel.Text = "";
            ClearFieldHighlights();

            string productCode = _codeBox.Text.Trim();
            string productName = _nameBox.Text.Trim();
            string category = _categoryBox.Text.Trim();
            string description = _descriptionBox.Text.Trim();

            if (!ValidationHelper.IsRequired(productCode))
            {
                ShowFieldError(_codeBox, "Product Code is required.");
                return;
            }

            if (!ValidationHelper.IsRequired(productName))
            {
                ShowFieldError(_nameBox, "Product Name is required.");
                return;
            }

            if (!ValidationHelper.IsRequired(category))
            {
                ShowFieldError(_categoryBox, "Category is required.");
                return;
            }

            if (!ValidationHelper.IsPositive(_priceBox.Value))
            {
                ShowFieldError(_priceBox, "Price must be greater than 0.");
                return;
            }

            if (!ValidationHelper.IsPositive((int)_quantityBox.Value))
            {
                ShowFieldError(_quantityBox, "Quantity must be greater than 0.");
                return;
            }

            if (_isEditMode && !ValidationHelper.IsValidSelection(_statusBox))
            {
                ShowFieldError(_statusBox, "Please select a status.");
                return;
            }

            Result.ProductCode = productCode;
            Result.ProductName = productName;
            Result.Category = category;
            Result.Description = description;
            Result.Price = _priceBox.Value;
            Result.Quantity = (int)_quantityBox.Value;
            Result.Status = _isEditMode ? _statusBox.Text : "Active";

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ShowFieldError(Control control, string message)
        {
            _errorLabel.Text = message;
            control.BackColor = InvalidFieldColor;
            control.Focus();
        }

        private void ClearFieldHighlights()
        {
            _codeBox.BackColor = ValidFieldColor;
            _nameBox.BackColor = ValidFieldColor;
            _categoryBox.BackColor = ValidFieldColor;
            _descriptionBox.BackColor = ValidFieldColor;
            _priceBox.BackColor = ValidFieldColor;
            _quantityBox.BackColor = ValidFieldColor;
            _statusBox.BackColor = ValidFieldColor;
        }
    }
}