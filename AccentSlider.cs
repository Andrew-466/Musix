using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicPlayer
{
    /// <summary>
    /// Custom slider dengan visual halus & natural, sesuai tema gelap.
    /// </summary>
    public sealed class AccentSlider : Control
    {
        private int _min = 0, _max = 100, _value = 0;
        private bool _dragging = false;
        private bool _hovering = false;

        public event EventHandler? ValueChanged;
        public event EventHandler? DragStarted;
        public event EventHandler? DragEnded;

        public Color TrackColor      { get; set; } = Color.FromArgb(55, 55, 55);
        public Color TrackHoverColor { get; set; } = Color.FromArgb(70, 70, 70);
        public Color FillColor       { get; set; } = Color.FromArgb(29, 185, 84);
        public Color FillHoverColor  { get; set; } = Color.FromArgb(35, 210, 100);
        public Color ThumbColor      { get; set; } = Color.White;

        public int TrackHeight       { get; set; } = 5;
        public int ThumbSize         { get; set; } = 12;
        public int ThumbSizeHover    { get; set; } = 14;

        public int Minimum
        {
            get => _min;
            set { _min = value; if (_value < _min) Value = _min; Invalidate(); }
        }

        public int Maximum
        {
            get => _max;
            set { _max = value; if (_value > _max) Value = _max; Invalidate(); }
        }

        public int Value
        {
            get => _value;
            set
            {
                var v = Math.Max(_min, Math.Min(_max, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public AccentSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Height = 26;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        private double CurrentRatio =>
            _max == _min ? 0 : (_value - _min) / (double)(_max - _min);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                DragStarted?.Invoke(this, EventArgs.Empty);
                SetValueFromMouse(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) SetValueFromMouse(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                DragEnded?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovering = true; Invalidate(); base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false; Invalidate(); base.OnMouseLeave(e);
        }

        private void SetValueFromMouse(int x)
        {
            int width = Width;
            if (width <= 0) return;
            int thumbW = ThumbSize;
            int trackStart = thumbW / 2;
            int trackLen = Math.Max(1, width - thumbW);
            double ratio = (x - trackStart) / (double)trackLen;
            ratio = Math.Max(0, Math.Min(1, ratio));
            Value = _min + (int)Math.Round(ratio * (_max - _min));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int cy = Height / 2;
            int thumbW = ThumbSize;
            int trackX = thumbW / 2;
            int trackW = Math.Max(1, Width - thumbW);
            int trackH = TrackHeight;

            // Background track
            var trackColor = _hovering ? TrackHoverColor : TrackColor;
            using (var b = new SolidBrush(trackColor))
            using (var path = RoundedRect(
                new RectangleF(trackX, cy - trackH / 2f, trackW, trackH), trackH / 2f))
                g.FillPath(b, path);

            // Filled part
            double ratio = CurrentRatio;
            int fillW = (int)Math.Round(trackW * ratio);
            if (fillW > 2)
            {
                var fillColor = (_hovering || _dragging) ? FillHoverColor : FillColor;
                using var b = new SolidBrush(fillColor);
                using var path = RoundedRect(
                    new RectangleF(trackX, cy - trackH / 2f, fillW, trackH), trackH / 2f);
                g.FillPath(b, path);
            }

            // Thumb — bayangan halus + lingkaran putih
            int thumbSize = (_hovering || _dragging) ? ThumbSizeHover : ThumbSize;
            float thumbCx = trackX + (float)(trackW * ratio);

            // Shadow tipis
            using (var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillEllipse(shadow,
                    thumbCx - thumbSize / 2f + 0.5f,
                    cy - thumbSize / 2f + 1f,
                    thumbSize, thumbSize);

            // Thumb utama
            using (var tb = new SolidBrush(ThumbColor))
                g.FillEllipse(tb,
                    thumbCx - thumbSize / 2f,
                    cy - thumbSize / 2f,
                    thumbSize, thumbSize);
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) radius = 0.1f;
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}