using System.Drawing.Printing;
using freshcrumbs.CRM.winforms.Models;

namespace freshcrumbs.CRM.winforms.Services
{
    /// <summary>
    /// Prints, previews or saves (via the built-in Windows "Microsoft Print to PDF" printer)
    /// a generated report. Uses only System.Drawing.Printing, no extra packages.
    /// </summary>
    public sealed class ReportPrinter
    {
        private const string PdfPrinterName = "Microsoft Print to PDF";
        private const string AppName = "FreshCrumbs CRM";
        private const float CellPadding = 4f;
        private const float MaxColumnWidth = 260f;
        private const float MinColumnWidth = 36f;

        private readonly GeneratedReportModel _report;

        private float[]? _columnWidths;
        private int _nextRow;
        private int _pageNumber;
        private bool _totalsPrinted;

        public ReportPrinter(GeneratedReportModel report)
        {
            _report = report;
        }

        public static bool IsPdfPrinterAvailable()
        {
            try
            {
                return PrinterSettings.InstalledPrinters
                    .Cast<string>()
                    .Any(name => string.Equals(name, PdfPrinterName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void ShowPreview(IWin32Window owner)
        {
            try
            {
                using var document = CreateDocument();
                using var dialog = new PrintPreviewDialog
                {
                    Document = document,
                    UseAntiAlias = true,
                    Width = 1100,
                    Height = 800,
                    StartPosition = FormStartPosition.CenterParent
                };

                dialog.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                ShowPrintError(owner, "The print preview could not be opened.", ex);
            }
        }

        public void Print(IWin32Window owner)
        {
            try
            {
                using var document = CreateDocument();
                using var dialog = new PrintDialog
                {
                    Document = document,
                    UseEXDialog = true
                };

                if (dialog.ShowDialog(owner) != DialogResult.OK)
                {
                    return;
                }

                document.Print();
            }
            catch (Exception ex)
            {
                ShowPrintError(owner, "The report could not be printed.", ex);
            }
        }

        public void SaveAsPdf(IWin32Window owner)
        {
            if (!IsPdfPrinterAvailable())
            {
                MessageBox.Show(
                    owner,
                    "Save as PDF uses the Windows \"Microsoft Print to PDF\" printer, which is not installed or not enabled on this computer." +
                    Environment.NewLine + Environment.NewLine +
                    "You can turn it on under Windows Features (\"Microsoft Print to PDF\"), or use Print and choose another PDF printer.",
                    "PDF Printer Not Available",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using var saveDialog = new SaveFileDialog
            {
                Title = "Save Report as PDF",
                Filter = "PDF file (*.pdf)|*.pdf",
                DefaultExt = "pdf",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = BuildDefaultFileName()
            };

            if (saveDialog.ShowDialog(owner) != DialogResult.OK)
            {
                return;
            }

            string path = saveDialog.FileName;

            try
            {
                using var document = CreateDocument(PdfPrinterName);
                document.PrinterSettings.PrintToFile = true;
                document.PrinterSettings.PrintFileName = path;

                if (!document.PrinterSettings.IsValid)
                {
                    throw new InvalidOperationException("The \"" + PdfPrinterName + "\" printer is not available.");
                }

                document.Print();
            }
            catch (Exception ex)
            {
                ShowPrintError(owner, "The PDF could not be created.", ex);
                return;
            }

            for (int i = 0; i < 15 && !File.Exists(path); i++)
            {
                Thread.Sleep(200);
            }

            if (File.Exists(path))
            {
                MessageBox.Show(owner, "Report saved as PDF:" + Environment.NewLine + path,
                    "PDF Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    owner,
                    "The PDF file was not found after printing. Please check that you can write to the selected folder and try again, or use Print Preview / Print instead.",
                    "PDF Not Created",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static void ShowPrintError(IWin32Window owner, string headline, Exception ex)
        {
            string detail = ex is InvalidPrinterException
                ? "No usable printer was found. Please install or enable a printer and try again."
                : ex.Message;

            MessageBox.Show(owner, headline + Environment.NewLine + Environment.NewLine + detail,
                "Print", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private string BuildDefaultFileName()
        {
            string name = _report.Title + " " + _report.Period;

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return name.Trim();
        }

        private PrintDocument CreateDocument(string? printerName = null)
        {
            var document = new PrintDocument { DocumentName = _report.Title };

            if (printerName != null)
            {
                document.PrinterSettings.PrinterName = printerName;
            }

            document.DefaultPageSettings.Landscape = _report.Columns.Count >= 7;
            document.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);

            document.BeginPrint += (s, e) =>
            {
                _columnWidths = null;
                _nextRow = 0;
                _pageNumber = 0;
                _totalsPrinted = false;
            };
            document.PrintPage += PrintPage;

            return document;
        }

        private void PrintPage(object sender, PrintPageEventArgs e)
        {
            if (e.Graphics is not Graphics g)
            {
                return;
            }

            RectangleF area = e.MarginBounds;
            _pageNumber++;

            using var brandFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var titleFont = new Font("Segoe UI", 16f, FontStyle.Bold);
            using var infoFont = new Font("Segoe UI", 9f);
            using var smallFont = new Font("Segoe UI", 8f);
            using var textFont = new Font("Segoe UI", 8.5f);
            using var boldFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var dark = new SolidBrush(Color.FromArgb(40, 40, 40));
            using var gray = new SolidBrush(Color.FromArgb(110, 110, 110));
            using var headerFill = new SolidBrush(Color.FromArgb(225, 225, 225));
            using var stripeFill = new SolidBrush(Color.FromArgb(245, 245, 245));
            using var linePen = new Pen(Color.FromArgb(170, 170, 170), 1f);

            _columnWidths ??= ComputeColumnWidths(g, area.Width, textFont, boldFont);

            float y = area.Top;

            if (_pageNumber == 1)
            {
                g.DrawString(AppName, brandFont, gray, area.Left, y);
                y += brandFont.GetHeight(g) + 2f;

                g.DrawString(_report.Title, titleFont, dark, area.Left, y);
                y += titleFont.GetHeight(g) + 4f;

                g.DrawString("Period: " + _report.Period, infoFont, dark, area.Left, y);
                y += infoFont.GetHeight(g) + 1f;

                string generated = "Generated: " +
                    _report.GeneratedAt.ToString("MMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture) +
                    "   |   Records: " + _report.Rows.Count.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                g.DrawString(generated, infoFont, gray, area.Left, y);
                y += infoFont.GetHeight(g) + 8f;
            }
            else
            {
                g.DrawString(_report.Title + " (continued)  -  " + _report.Period, boldFont, dark, area.Left, y);
                y += boldFont.GetHeight(g) + 8f;
            }

            float rowHeight = textFont.GetHeight(g) + 6f;
            float bottomLimit = area.Bottom - 24f;

            DrawRow(g, _report.Columns.Select(c => c.Header).ToList(), area.Left, y, rowHeight, boldFont, dark, headerFill);
            y += rowHeight;
            g.DrawLine(linePen, area.Left, y, area.Left + _columnWidths.Sum(), y);

            int drawnOnPage = 0;

            while (_nextRow < _report.Rows.Count)
            {
                if (y + rowHeight > bottomLimit && drawnOnPage > 0)
                {
                    DrawFooter(g, area, smallFont, gray);
                    e.HasMorePages = true;
                    return;
                }

                Brush? fill = _nextRow % 2 == 1 ? stripeFill : null;
                DrawRow(g, _report.Rows[_nextRow], area.Left, y, rowHeight, textFont, dark, fill);
                y += rowHeight;
                _nextRow++;
                drawnOnPage++;
            }

            if (_report.Rows.Count == 0)
            {
                g.DrawString("No records found for the selected report and period.", infoFont, gray, area.Left, y + 8f);
                y += infoFont.GetHeight(g) + 16f;
            }

            if (!_totalsPrinted && _report.Totals.Count == _report.Columns.Count)
            {
                if (y + rowHeight + 4f > bottomLimit && drawnOnPage > 0)
                {
                    DrawFooter(g, area, smallFont, gray);
                    e.HasMorePages = true;
                    return;
                }

                g.DrawLine(linePen, area.Left, y + 2f, area.Left + _columnWidths.Sum(), y + 2f);
                DrawRow(g, _report.Totals, area.Left, y + 3f, rowHeight, boldFont, dark, headerFill);
                _totalsPrinted = true;
            }

            DrawFooter(g, area, smallFont, gray);
            e.HasMorePages = false;
        }

        private void DrawRow(
            Graphics g,
            List<string> cells,
            float left,
            float y,
            float rowHeight,
            Font font,
            Brush textBrush,
            Brush? fill)
        {
            float x = left;

            if (fill != null)
            {
                g.FillRectangle(fill, left, y, _columnWidths!.Sum(), rowHeight);
            }

            using var format = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
                LineAlignment = StringAlignment.Center
            };

            for (int i = 0; i < _columnWidths!.Length; i++)
            {
                format.Alignment = string.Equals(_report.Columns[i].Align, "Right", StringComparison.OrdinalIgnoreCase)
                    ? StringAlignment.Far
                    : StringAlignment.Near;

                string text = i < cells.Count ? cells[i] : string.Empty;
                var cell = new RectangleF(x + CellPadding, y, _columnWidths[i] - (CellPadding * 2), rowHeight);
                g.DrawString(text, font, textBrush, cell, format);

                x += _columnWidths[i];
            }
        }

        private void DrawFooter(Graphics g, RectangleF area, Font font, Brush brush)
        {
            float y = area.Bottom - font.GetHeight(g);

            g.DrawString(AppName + " - " + _report.Title, font, brush, area.Left, y);

            using var right = new StringFormat { Alignment = StringAlignment.Far };
            g.DrawString("Page " + _pageNumber, font, brush, new RectangleF(area.Left, y, area.Width, font.GetHeight(g)), right);
        }

        private float[] ComputeColumnWidths(Graphics g, float available, Font textFont, Font boldFont)
        {
            int count = _report.Columns.Count;
            var natural = new float[count];

            for (int i = 0; i < count; i++)
            {
                float width = g.MeasureString(_report.Columns[i].Header, boldFont).Width;

                foreach (var row in _report.Rows.Take(300))
                {
                    if (i < row.Count)
                    {
                        width = Math.Max(width, g.MeasureString(row[i], textFont).Width);
                    }
                }

                if (_report.Totals.Count == count)
                {
                    width = Math.Max(width, g.MeasureString(_report.Totals[i], boldFont).Width);
                }

                natural[i] = Math.Max(MinColumnWidth, Math.Min(width + (CellPadding * 2), MaxColumnWidth));
            }

            float total = natural.Sum();

            if (total > available)
            {
                // Shrink the text columns first so numeric columns keep their full width.
                float numericWidth = 0f;
                float textWidth = 0f;

                for (int i = 0; i < count; i++)
                {
                    if (string.Equals(_report.Columns[i].Align, "Right", StringComparison.OrdinalIgnoreCase))
                    {
                        numericWidth += natural[i];
                    }
                    else
                    {
                        textWidth += natural[i];
                    }
                }

                float textScale = textWidth > 0f
                    ? Math.Max(0.35f, (available - numericWidth) / textWidth)
                    : 1f;

                for (int i = 0; i < count; i++)
                {
                    if (!string.Equals(_report.Columns[i].Align, "Right", StringComparison.OrdinalIgnoreCase))
                    {
                        natural[i] = Math.Max(MinColumnWidth, natural[i] * textScale);
                    }
                }

                total = natural.Sum();
            }

            // Fit exactly to the printable width (shrinks any remainder, or stretches narrow reports).
            float factor = available / total;

            for (int i = 0; i < count; i++)
            {
                natural[i] *= factor;
            }

            return natural;
        }
    }
}