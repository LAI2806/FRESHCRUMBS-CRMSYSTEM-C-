using System.Drawing.Drawing2D;

namespace freshcrumbs.CRM.winforms.UserControls
{
    public class ChartSeries
    {
        public string Name { get; set; } = string.Empty;

        public Color Color { get; set; }

        public List<int> Values { get; set; } = new();
    }

    internal static class ChartMath
    {
        public static int NiceMax(int max)
        {
            int[] steps = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000 };

            foreach (int step in steps)
            {
                if (step * 4 >= max)
                {
                    return step * 4;
                }
            }

            return ((max / 4) + 1) * 4;
        }

        public static void DrawEmpty(Graphics g, Rectangle bounds, string text)
        {
            using var brush = new SolidBrush(Color.FromArgb(160, 150, 140));
            using var font = new Font("Segoe UI", 10);
            g.DrawString(text, font, brush, bounds,
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
    }

    public class LineChartPanel : Panel
    {
        private const int MarginLeft = 40;
        private const int MarginRight = 16;
        private const int MarginTop = 30;
        private const int MarginBottom = 28;

        private readonly ToolTip _tip = new();
        private List<string> _labels = new();
        private List<ChartSeries> _series = new();
        private string _emptyText = "No data yet.";
        private int _hover = -1;

        public LineChartPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Dock = DockStyle.Fill;
        }

        public void SetData(List<string> labels, List<ChartSeries> series, string emptyText)
        {
            _labels = labels;
            _series = series;
            _emptyText = emptyText;
            _hover = -1;
            Invalidate();
        }

        private bool HasData => _series.Any(s => s.Values.Any(v => v > 0));

        private RectangleF Plot => new(
            MarginLeft, MarginTop,
            Math.Max(10, Width - MarginLeft - MarginRight),
            Math.Max(10, Height - MarginTop - MarginBottom));

        private float XAt(int index, RectangleF plot) =>
            _labels.Count <= 1 ? plot.Left + plot.Width / 2 : plot.Left + plot.Width * index / (_labels.Count - 1);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!HasData || _labels.Count == 0)
            {
                return;
            }

            var plot = Plot;
            int nearest = 0;
            float best = float.MaxValue;

            for (int i = 0; i < _labels.Count; i++)
            {
                float distance = Math.Abs(XAt(i, plot) - e.X);

                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            if (nearest != _hover)
            {
                _hover = nearest;
                _tip.SetToolTip(this, _labels[nearest] + "\n" +
                    string.Join("\n", _series.Select(s => $"{s.Name}: {s.Values[nearest]}")));
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (!HasData || _labels.Count == 0)
            {
                ChartMath.DrawEmpty(g, ClientRectangle, _emptyText);
                return;
            }

            var plot = Plot;
            int yMax = ChartMath.NiceMax(_series.Max(s => s.Values.Max()));

            using var gridPen = new Pen(Color.FromArgb(225, 210, 200));
            using var axisBrush = new SolidBrush(Color.FromArgb(120, 110, 100));
            using var axisFont = new Font("Segoe UI", 8.5f);

            for (int k = 0; k <= 4; k++)
            {
                float y = plot.Bottom - plot.Height * k / 4f;
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString((yMax * k / 4).ToString(), axisFont, axisBrush,
                    new RectangleF(0, y - 9, MarginLeft - 6, 18),
                    new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
            }

            int labelStep = _labels.Count > 8 && plot.Width / _labels.Count < 46 ? 2 : 1;

            for (int i = 0; i < _labels.Count; i += labelStep)
            {
                g.DrawString(_labels[i], axisFont, axisBrush,
                    new RectangleF(XAt(i, plot) - 28, plot.Bottom + 6, 56, 18),
                    new StringFormat { Alignment = StringAlignment.Center });
            }

            if (_hover >= 0)
            {
                using var hoverPen = new Pen(Color.FromArgb(210, 200, 190)) { DashStyle = DashStyle.Dash };
                g.DrawLine(hoverPen, XAt(_hover, plot), plot.Top, XAt(_hover, plot), plot.Bottom);
            }

            foreach (var series in _series)
            {
                var points = series.Values
                    .Select((value, i) => new PointF(XAt(i, plot), plot.Bottom - plot.Height * value / yMax))
                    .ToArray();

                using var pen = new Pen(series.Color, 2.5f) { LineJoin = LineJoin.Round };

                if (points.Length > 1)
                {
                    g.DrawLines(pen, points);
                }

                using var dotBrush = new SolidBrush(series.Color);

                for (int i = 0; i < points.Length; i++)
                {
                    float radius = i == _hover ? 5f : 3f;
                    g.FillEllipse(dotBrush, points[i].X - radius, points[i].Y - radius, radius * 2, radius * 2);
                }
            }

            if (_series.Count > 1)
            {
                float x = MarginLeft;

                foreach (var series in _series)
                {
                    using var brush = new SolidBrush(series.Color);
                    g.FillRectangle(brush, x, 10, 10, 10);
                    g.DrawString(series.Name, axisFont, axisBrush, x + 14, 6);
                    x += 14 + g.MeasureString(series.Name, axisFont).Width + 16;
                }
            }
        }
    }

    public class ColumnChartPanel : Panel
    {
        private const int MarginLeft = 40;
        private const int MarginRight = 16;
        private const int MarginTop = 24;
        private const int MarginBottom = 28;

        private readonly ToolTip _tip = new();
        private List<string> _labels = new();
        private List<int> _values = new();
        private Color _color = Color.FromArgb(210, 140, 60);
        private string _emptyText = "No data yet.";
        private int _hover = -1;

        public ColumnChartPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Dock = DockStyle.Fill;
        }

        public void SetData(List<string> labels, List<int> values, Color color, string emptyText)
        {
            _labels = labels;
            _values = values;
            _color = color;
            _emptyText = emptyText;
            _hover = -1;
            Invalidate();
        }

        private RectangleF Plot => new(
            MarginLeft, MarginTop,
            Math.Max(10, Width - MarginLeft - MarginRight),
            Math.Max(10, Height - MarginTop - MarginBottom));

        private int SlotAt(float x)
        {
            var plot = Plot;

            if (_labels.Count == 0 || x < plot.Left || x > plot.Right)
            {
                return -1;
            }

            return Math.Min(_labels.Count - 1, (int)((x - plot.Left) / (plot.Width / _labels.Count)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int slot = SlotAt(e.X);

            if (slot != _hover)
            {
                _hover = slot;

                if (slot >= 0)
                {
                    _tip.SetToolTip(this, $"{_labels[slot]}: {_values[slot]}");
                }

                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_values.Count == 0 || _values.All(v => v == 0))
            {
                ChartMath.DrawEmpty(g, ClientRectangle, _emptyText);
                return;
            }

            var plot = Plot;
            int yMax = ChartMath.NiceMax(_values.Max());

            using var gridPen = new Pen(Color.FromArgb(225, 210, 200));
            using var axisBrush = new SolidBrush(Color.FromArgb(120, 110, 100));
            using var axisFont = new Font("Segoe UI", 8.5f);
            using var valueFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            for (int k = 0; k <= 4; k++)
            {
                float y = plot.Bottom - plot.Height * k / 4f;
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString((yMax * k / 4).ToString(), axisFont, axisBrush,
                    new RectangleF(0, y - 9, MarginLeft - 6, 18),
                    new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
            }

            float slotWidth = plot.Width / _labels.Count;
            float barWidth = Math.Max(6, slotWidth * 0.55f);
            int labelStep = _labels.Count > 8 && slotWidth < 46 ? 2 : 1;

            for (int i = 0; i < _labels.Count; i++)
            {
                float centerX = plot.Left + slotWidth * i + slotWidth / 2;
                float height = plot.Height * _values[i] / yMax;
                var bar = new RectangleF(centerX - barWidth / 2, plot.Bottom - height, barWidth, height);

                using var brush = new SolidBrush(i == _hover ? ControlPaint.Light(_color) : _color);

                if (height > 0)
                {
                    g.FillRectangle(brush, bar);
                    g.DrawString(_values[i].ToString(), valueFont, axisBrush,
                        new RectangleF(centerX - 20, bar.Top - 18, 40, 16),
                        new StringFormat { Alignment = StringAlignment.Center });
                }

                if (i % labelStep == 0)
                {
                    g.DrawString(_labels[i], axisFont, axisBrush,
                        new RectangleF(centerX - 28, plot.Bottom + 6, 56, 18),
                        new StringFormat { Alignment = StringAlignment.Center });
                }
            }
        }
    }

    public class DonutChartPanel : Panel
    {
        private readonly ToolTip _tip = new();
        private List<(string Label, int Value, Color Color)> _slices = new();
        private string _caption = string.Empty;
        private string _emptyText = "No data yet.";
        private bool _clickable;
        private int _hover = -1;

        public event Action<int>? SliceClicked;

        public DonutChartPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            Dock = DockStyle.Fill;
        }

        public void SetData(List<(string Label, int Value, Color Color)> slices, string caption, string emptyText, bool clickable)
        {
            _slices = slices;
            _caption = caption;
            _emptyText = emptyText;
            _clickable = clickable;
            _hover = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        private int Total => _slices.Sum(s => s.Value);

        private Rectangle RingBounds
        {
            get
            {
                int size = Math.Max(60, Math.Min(Height - 24, (int)(Width * 0.5) - 16));
                return new Rectangle(12, (Height - size) / 2, size, size);
            }
        }

        private Rectangle LegendRow(int index) => new(RingBounds.Right + 24, 24 + index * 28, Math.Max(40, Width - RingBounds.Right - 36), 26);

        private int HitTest(Point point)
        {
            if (Total == 0)
            {
                return -1;
            }

            var ring = RingBounds;
            float cx = ring.Left + ring.Width / 2f;
            float cy = ring.Top + ring.Height / 2f;
            float dx = point.X - cx;
            float dy = point.Y - cy;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance <= ring.Width / 2f && distance >= ring.Width * 0.32f)
            {
                float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI) + 90;

                if (angle < 0)
                {
                    angle += 360;
                }

                float start = 0;

                for (int i = 0; i < _slices.Count; i++)
                {
                    float sweep = 360f * _slices[i].Value / Total;

                    if (_slices[i].Value > 0 && angle >= start && angle < start + sweep)
                    {
                        return i;
                    }

                    start += sweep;
                }
            }

            for (int i = 0; i < _slices.Count; i++)
            {
                if (LegendRow(i).Contains(point))
                {
                    return i;
                }
            }

            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int hit = HitTest(e.Location);
            Cursor = _clickable && hit >= 0 ? Cursors.Hand : Cursors.Default;

            if (hit != _hover)
            {
                _hover = hit;

                if (hit >= 0)
                {
                    _tip.SetToolTip(this, $"{_slices[hit].Label}: {_slices[hit].Value} ({_slices[hit].Value * 100 / Math.Max(1, Total)}%)");
                }

                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_clickable)
            {
                int hit = HitTest(e.Location);

                if (hit >= 0)
                {
                    SliceClicked?.Invoke(hit);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (Total == 0)
            {
                ChartMath.DrawEmpty(g, ClientRectangle, _emptyText);
                return;
            }

            var ring = RingBounds;
            float start = -90;

            for (int i = 0; i < _slices.Count; i++)
            {
                if (_slices[i].Value <= 0)
                {
                    continue;
                }

                float sweep = 360f * _slices[i].Value / Total;
                using var brush = new SolidBrush(i == _hover ? ControlPaint.Light(_slices[i].Color) : _slices[i].Color);
                g.FillPie(brush, ring, start, sweep);
                start += sweep;
            }

            int hole = (int)(ring.Width * 0.64);
            using var holeBrush = new SolidBrush(BackColor);
            g.FillEllipse(holeBrush, ring.Left + (ring.Width - hole) / 2, ring.Top + (ring.Height - hole) / 2, hole, hole);

            using var totalFont = new Font("Segoe UI", 18, FontStyle.Bold);
            using var captionFont = new Font("Segoe UI", 8.5f);
            using var darkBrush = new SolidBrush(Color.FromArgb(50, 35, 25));
            using var mutedBrush = new SolidBrush(Color.FromArgb(120, 110, 100));
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.DrawString(Total.ToString(), totalFont, darkBrush,
                new RectangleF(ring.Left, ring.Top + ring.Height / 2f - 20, ring.Width, 26), center);
            g.DrawString(_caption, captionFont, mutedBrush,
                new RectangleF(ring.Left, ring.Top + ring.Height / 2f + 4, ring.Width, 18), center);

            using var legendFont = new Font("Segoe UI", 9.5f);
            using var legendBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            for (int i = 0; i < _slices.Count; i++)
            {
                var row = LegendRow(i);

                if (row.Bottom > Height)
                {
                    break;
                }

                using var swatch = new SolidBrush(_slices[i].Color);
                g.FillRectangle(swatch, row.Left, row.Top + 8, 10, 10);

                var labelFormat = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                g.DrawString(_slices[i].Label, i == _hover ? legendBold : legendFont, darkBrush,
                    new RectangleF(row.Left + 16, row.Top, row.Width - 64, row.Height), labelFormat);
                g.DrawString(_slices[i].Value.ToString(), legendBold, darkBrush,
                    new RectangleF(row.Right - 44, row.Top, 44, row.Height),
                    new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
            }
        }
    }
}