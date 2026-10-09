using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.Forms
{
    public class TransactionItemsDialog : Form
    {
        private readonly ApiService _apiService;
        private readonly int _companyId;
        private readonly int _transactionId;
        private readonly List<ProductModel> _products;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color BorderColor = Color.FromArgb(220, 210, 200);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private DataGridView _itemsGrid = null!;
        private Label _statusLabel = null!;

        private List<TransactionItemModel> _items = new();

        public TransactionItemsDialog(ApiService apiService, int companyId, int transactionId, List<ProductModel> products)
        {
            _apiService = apiService;
            _companyId = companyId;
            _transactionId = transactionId;
            _products = products;

            InitializeForm();
            InitializeControls();

            Load += TransactionItemsDialog_Load;
        }

        private void InitializeForm()
        {
            Text = $"Manage Items - Transaction #{_transactionId}";
            Width = 640;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(500, 350);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
        }

        private void InitializeControls()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            var toolbarPanel = new Panel { Dock = DockStyle.Fill };

            var noteLabel = new Label
            {
                Text = "Items are fixed once a transaction is saved.",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = LabelGray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            toolbarPanel.Controls.Add(noteLabel);

            root.Controls.Add(toolbarPanel, 0, 0);

            _itemsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
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
                RowTemplate = { Height = 34 },
                EnableHeadersVisualStyles = false
            };
            _itemsGrid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _itemsGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _itemsGrid.ColumnHeadersHeight = 38;

            root.Controls.Add(_itemsGrid, 0, 1);

            _statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick
            };
            root.Controls.Add(_statusLabel, 0, 2);

            Controls.Add(root);
        }

        private async void TransactionItemsDialog_Load(object? sender, EventArgs e)
        {
            await LoadItemsAsync();
        }

        private async Task LoadItemsAsync()
        {
            try
            {
                _statusLabel.Text = "";
                _items = await _apiService.GetTransactionItemsAsync(_companyId, _transactionId);
                BindGrid();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Failed to load items: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
        }

        private void BindGrid()
        {
            var displayRows = _items.Select(i => new
            {
                i.TransactionItemId,
                ProductName = GetProductName(i.ProductId),
                i.Quantity,
                i.UnitPrice,
                i.Subtotal
            }).ToList();

            _itemsGrid.AutoGenerateColumns = true;
            _itemsGrid.DataSource = null;
            _itemsGrid.DataSource = displayRows;

            if (_itemsGrid.Columns["TransactionItemId"] != null)
            {
                _itemsGrid.Columns["TransactionItemId"].Visible = false;
            }

            if (_itemsGrid.Columns["ProductName"] != null)
            {
                _itemsGrid.Columns["ProductName"].HeaderText = "Product";
            }
            if (_itemsGrid.Columns["Quantity"] != null)
            {
                _itemsGrid.Columns["Quantity"].HeaderText = "Qty";
            }
            if (_itemsGrid.Columns["UnitPrice"] != null)
            {
                _itemsGrid.Columns["UnitPrice"].HeaderText = "Unit Price";
            }
            if (_itemsGrid.Columns["Subtotal"] != null)
            {
                _itemsGrid.Columns["Subtotal"].HeaderText = "Subtotal";
            }
        }

        private string GetProductName(int productId)
        {
            var product = _products.FirstOrDefault(p => p.ProductId == productId);
            return product?.ProductName ?? $"Product #{productId}";
        }

    }
}