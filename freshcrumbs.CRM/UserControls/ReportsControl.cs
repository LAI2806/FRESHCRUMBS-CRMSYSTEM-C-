using freshcrumbs.CRM.winforms.Models;
using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class ReportsControl : UserControl
    {
        private const int MaxRangeDays = 3660;

        private static readonly Color AccentColor = Color.FromArgb(210, 140, 60);
        private static readonly Color TextDark = Color.FromArgb(50, 35, 25);
        private static readonly Color LabelGray = Color.FromArgb(120, 110, 100);
        private static readonly Color PageBg = Color.FromArgb(244, 244, 246);
        private static readonly Color HeaderRowColor = Color.FromArgb(250, 246, 242);

        private readonly ApiService _apiService;
        private readonly int _companyId;

        private ComboBox _reportTypeCombo = null!;
        private DateTimePicker _startPicker = null!;
        private DateTimePicker _endPicker = null!;
        private Button _generateButton = null!;
        private Button _previewButton = null!;
        private Button _printButton = null!;
        private Button _pdfButton = null!;
        private Label _loadingLabel = null!;
        private Label _statusLabel = null!;
        private Label _resultTitleLabel = null!;
        private Label _resultInfoLabel = null!;
        private Label _placeholderLabel = null!;
        private DataGridView _grid = null!;

        private GeneratedReportModel? _currentReport;
        private bool _isGenerating;

        // The navigation callback is accepted so the existing MainCrmForm wiring stays unchanged.
        public ReportsControl(int companyId, Action<string>? navigateToModule = null)
        {
            _companyId = companyId;
            _apiService = new ApiService();

            InitializeLayout();
        }

        private sealed class ReportOption
        {
            public ReportOption(string label, string key, bool usesDateRange)
            {
                Label = label;
                Key = key;
                UsesDateRange = usesDateRange;
            }

            public string Label { get; }

            public string Key { get; }

            public bool UsesDateRange { get; }

            public override string ToString()
            {
                return Label;
            }
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            BackColor = PageBg;

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = PageBg,
                Padding = new Padding(0)
            };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var header = new Label
            {
                Text = "Reports",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            rootLayout.Controls.Add(header, 0, 0);

            var subHeader = new Label
            {
                Text = "Generate detailed reports from your CRM data, then print or save them as PDF.",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = LabelGray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            rootLayout.Controls.Add(subHeader, 0, 1);

            var filterRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = PageBg,
                Padding = new Padding(0, 6, 0, 0)
            };

            filterRow.Controls.Add(CreateFilterLabel("Report Type:"));

            _reportTypeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                Width = 200,
                Margin = new Padding(0, 2, 16, 0)
            };
            PopulateReportTypes();
            _reportTypeCombo.SelectedIndexChanged += ReportTypeCombo_SelectedIndexChanged;
            filterRow.Controls.Add(_reportTypeCombo);

            filterRow.Controls.Add(CreateFilterLabel("Start Date:"));
            _startPicker = CreateDatePicker();
            filterRow.Controls.Add(_startPicker);

            filterRow.Controls.Add(CreateFilterLabel("End Date:"));
            _endPicker = CreateDatePicker();
            filterRow.Controls.Add(_endPicker);

            var currentMonthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _startPicker.Value = currentMonthStart;
            _endPicker.Value = currentMonthStart.AddMonths(1).AddDays(-1);

            _generateButton = CreateActionButton("Generate Report", AccentColor, Color.White, 160);
            _generateButton.Margin = new Padding(8, 0, 12, 0);
            _generateButton.Click += GenerateButton_Click;
            filterRow.Controls.Add(_generateButton);

            _loadingLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = LabelGray,
                AutoSize = true,
                Margin = new Padding(0, 9, 0, 0)
            };
            filterRow.Controls.Add(_loadingLabel);

            rootLayout.Controls.Add(filterRow, 0, 2);

            rootLayout.Controls.Add(CreateResultPanel(), 0, 3);

            _statusLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 24,
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.Firebrick
            };

            Controls.Add(rootLayout);
            Controls.Add(_statusLabel);
            _statusLabel.BringToFront();

            UpdateDatePickersForSelection();
        }

        private Panel CreateResultPanel()
        {
            var resultPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(12)
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(235, 230, 225),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                Font = new Font("Segoe UI", 9.5f),
                RowTemplate = { Height = 32 },
                EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                Visible = false
            };

            _grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderRowColor;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _grid.ColumnHeadersHeight = 38;
            _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(250, 236, 220);
            _grid.DefaultCellStyle.SelectionForeColor = TextDark;
            _grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);

            resultPanel.Controls.Add(_grid);

            _placeholderLabel = new Label
            {
                Text = "Select a report type, set the date range, then click Generate Report.",
                Font = new Font("Segoe UI", 11, FontStyle.Italic),
                ForeColor = LabelGray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            resultPanel.Controls.Add(_placeholderLabel);

            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = Color.White
            };

            _resultInfoLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = LabelGray,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            topBar.Controls.Add(_resultInfoLabel);

            _resultTitleLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = TextDark,
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft
            };
            topBar.Controls.Add(_resultTitleLabel);

            var buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 420,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.White
            };

            _pdfButton = CreateActionButton("Save as PDF", AccentColor, Color.White, 130);
            _pdfButton.Click += PdfButton_Click;
            _printButton = CreateActionButton("Print", Color.White, LabelGray, 90);
            _printButton.FlatAppearance.BorderSize = 1;
            _printButton.FlatAppearance.BorderColor = Color.FromArgb(225, 210, 200);
            _printButton.Click += PrintButton_Click;
            _previewButton = CreateActionButton("Print Preview", Color.White, LabelGray, 130);
            _previewButton.FlatAppearance.BorderSize = 1;
            _previewButton.FlatAppearance.BorderColor = Color.FromArgb(225, 210, 200);
            _previewButton.Click += PreviewButton_Click;

            buttonFlow.Controls.Add(_pdfButton);
            buttonFlow.Controls.Add(_printButton);
            buttonFlow.Controls.Add(_previewButton);
            topBar.Controls.Add(buttonFlow);

            SetOutputButtonsEnabled(false);

            resultPanel.Controls.Add(topBar);

            return resultPanel;
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

        private static Button CreateActionButton(string text, Color backColor, Color foreColor, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void PopulateReportTypes()
        {
            var options = new[]
            {
                new ReportOption("Sales Report", "sales", true),
                new ReportOption("Customer Report", "customers", true),
                new ReportOption("Inventory Report", "inventory", false),
                new ReportOption("Product Sales Report", "product-sales", true),
                new ReportOption("Loyalty Report", "loyalty", true),
                new ReportOption("Promotion Report", "promotions", true),
                new ReportOption("Discount Report", "discounts", true),
                new ReportOption("Feedback Report", "feedback", true),
                new ReportOption("Inquiry Report", "inquiries", true),
                new ReportOption("Business Summary Report", "business-summary", true)
            };

            foreach (var option in options)
            {
                if (TenantCapabilities.CanGenerateReport(option.Key))
                {
                    _reportTypeCombo.Items.Add(option);
                }
            }

            if (_reportTypeCombo.Items.Count > 0)
            {
                _reportTypeCombo.SelectedIndex = 0;
            }
        }

        private ReportOption? SelectedReport
        {
            get { return _reportTypeCombo.SelectedItem as ReportOption; }
        }

        private void ReportTypeCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateDatePickersForSelection();
        }

        private void UpdateDatePickersForSelection()
        {
            // Inventory is a current-stock snapshot, so date filtering is disabled for it.
            bool usesDateRange = SelectedReport?.UsesDateRange ?? true;
            _startPicker.Enabled = usesDateRange;
            _endPicker.Enabled = usesDateRange;
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

            // The API counts the end day too, so the same span is compared here.
            if ((end.Date - start.Date).TotalDays >= MaxRangeDays)
            {
                _statusLabel.Text = "The selected date range cannot exceed 10 years.";
                return false;
            }

            return true;
        }

        private async void GenerateButton_Click(object? sender, EventArgs e)
        {
            if (_isGenerating)
            {
                return;
            }

            var option = SelectedReport;

            if (option == null)
            {
                _statusLabel.Text = "Please select a report type.";
                return;
            }

            _statusLabel.Text = "";

            DateTime? startDate = null;
            DateTime? endDate = null;

            if (option.UsesDateRange)
            {
                if (!TryGetDateRange(out var start, out var end))
                {
                    return;
                }

                startDate = start;
                endDate = end;
            }

            _isGenerating = true;
            _generateButton.Enabled = false;
            _loadingLabel.Text = "Generating report...";

            try
            {
                var report = await _apiService.GenerateReportAsync(_companyId, option.Key, startDate, endDate);

                if (IsDisposed)
                {
                    return;
                }

                BindReport(report);
                _loadingLabel.Text = "";
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                {
                    return;
                }

                _loadingLabel.Text = "";
                _statusLabel.Text = $"Failed to generate report: {ErrorMessageHelper.GetFriendlyMessage(ex)}";
            }
            finally
            {
                _isGenerating = false;

                if (!IsDisposed)
                {
                    _generateButton.Enabled = true;
                }
            }
        }

        private void BindReport(GeneratedReportModel report)
        {
            _currentReport = report;

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            _grid.Columns.Clear();

            foreach (var column in report.Columns)
            {
                bool right = string.Equals(column.Align, "Right", StringComparison.OrdinalIgnoreCase);

                var gridColumn = new DataGridViewTextBoxColumn
                {
                    HeaderText = column.Header,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    MinimumWidth = 60
                };

                if (right)
                {
                    gridColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    gridColumn.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                }

                _grid.Columns.Add(gridColumn);
            }

            foreach (var row in report.Rows)
            {
                _grid.Rows.Add(ToCells(row, report.Columns.Count));
            }

            if (report.Totals.Count == report.Columns.Count)
            {
                int totalsIndex = _grid.Rows.Add(ToCells(report.Totals, report.Columns.Count));
                var totalsRow = _grid.Rows[totalsIndex];
                totalsRow.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                totalsRow.DefaultCellStyle.BackColor = HeaderRowColor;
            }

            _grid.ResumeLayout();

            _resultTitleLabel.Text = report.Title;

            string records = report.Rows.Count == 1 ? "1 record" : $"{report.Rows.Count:N0} records";
            string generated = report.GeneratedAt.ToString("MMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            _resultInfoLabel.Text = $"Period: {report.Period}   |   {records}   |   Generated: {generated}";

            _placeholderLabel.Visible = false;
            _grid.Visible = true;

            SetOutputButtonsEnabled(true);
        }

        private static object[] ToCells(List<string> row, int columnCount)
        {
            var cells = new object[columnCount];

            for (int i = 0; i < columnCount; i++)
            {
                cells[i] = i < row.Count ? row[i] : string.Empty;
            }

            return cells;
        }

        private void SetOutputButtonsEnabled(bool enabled)
        {
            _previewButton.Enabled = enabled;
            _printButton.Enabled = enabled;
            _pdfButton.Enabled = enabled;
        }

        private void PreviewButton_Click(object? sender, EventArgs e)
        {
            if (_currentReport != null)
            {
                new ReportPrinter(_currentReport).ShowPreview(this);
            }
        }

        private void PrintButton_Click(object? sender, EventArgs e)
        {
            if (_currentReport != null)
            {
                new ReportPrinter(_currentReport).Print(this);
            }
        }

        private void PdfButton_Click(object? sender, EventArgs e)
        {
            if (_currentReport != null)
            {
                new ReportPrinter(_currentReport).SaveAsPdf(this);
            }
        }
    }
}