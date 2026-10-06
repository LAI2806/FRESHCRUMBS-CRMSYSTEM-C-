using freshcrumbs.CRM.winforms.Services;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class HorizontalBarPanel : Panel
    {
        private const int RowHeight = 32;

        private List<(string Label, int Value)> _items = new();
        private string _emptyText = "No data to show.";
        private Func<int, string> _format = value => value.ToString();
        private bool _clickable;

        public event Action<int>? ItemClicked;

        public HorizontalBarPanel()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = Color.White;
            Dock = DockStyle.Fill;
        }

        public void SetData(
            IEnumerable<(string Label, int Value)> items,
            string emptyText,
            Func<int, string>? valueFormatter = null,
            bool clickable = false)
        {
            _items = items.ToList();
            _emptyText = emptyText;
            _format = valueFormatter ?? (value => value.ToString());
            _clickable = clickable;
            AutoScrollMinSize = new Size(0, _items.Count * RowHeight);
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        private int RowAt(Point point)
        {
            int index = (point.Y - AutoScrollPosition.Y) / RowHeight;
            return index >= 0 && index < _items.Count ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = _clickable && RowAt(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_clickable)
            {
                int index = RowAt(e.Location);

                if (index >= 0)
                {
                    ItemClicked?.Invoke(index);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var textBrush = new SolidBrush(Color.FromArgb(50, 35, 25));
            using var mutedBrush = new SolidBrush(Color.FromArgb(160, 150, 140));
            using var font = new Font("Segoe UI", 9.5f);
            using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            if (_items.Count == 0)
            {
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_emptyText, font, mutedBrush, ClientRectangle, format);
                return;
            }

            g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            int labelWidth = Math.Min(190, Math.Max(100, ClientSize.Width / 3));
            int valueWidth = 52;
            int barLeft = labelWidth + 8;
            int barMax = Math.Max(20, ClientSize.Width - barLeft - valueWidth - 20);
            int max = Math.Max(1, _items.Max(i => i.Value));

            for (int i = 0; i < _items.Count; i++)
            {
                int y = i * RowHeight;
                var item = _items[i];

                var labelFormat = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(item.Label, font, textBrush, new RectangleF(0, y, labelWidth, RowHeight), labelFormat);

                int barWidth = item.Value <= 0 ? 0 : Math.Max(4, (int)(barMax * (item.Value / (double)max)));

                using var trackBrush = new SolidBrush(Color.FromArgb(244, 241, 238));
                g.FillRectangle(trackBrush, barLeft, y + 8, barMax, 16);

                if (barWidth > 0)
                {
                    using var barBrush = new SolidBrush(AdminUi.ChartColors[i % AdminUi.ChartColors.Length]);
                    g.FillRectangle(barBrush, barLeft, y + 8, barWidth, 16);
                }

                g.DrawString(_format(item.Value), boldFont, textBrush,
                    new RectangleF(barLeft + barMax + 6, y, valueWidth, RowHeight),
                    new StringFormat { LineAlignment = StringAlignment.Center });
            }
        }
    }
}