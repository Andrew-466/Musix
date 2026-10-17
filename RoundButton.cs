using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicPlayer
{
    public sealed class RoundButton : Button
    {
        public int CornerRadius { get; set; } = 8;
        public Color NormalColor { get; set; } = Color.FromArgb(28, 28, 28);
        public Color HoverColor { get; set; } = Color.FromArgb(45, 45, 45);
        public Color PressedColor { get; set; } = Color.FromArgb(45, 45, 45);
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderWidth { get; set; } = 0;

        private bool _hovering;
        private bool _pressed;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.FromArgb(28, 28, 28);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 9.5F);
            ForeColor = Color.White;
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // ⬇️ FIX: paint parent background dulu supaya tidak ada sisa warna
            Color parentBg = Parent?.BackColor ?? BackColor;
            if (parentBg == Color.Transparent || parentBg == Color.Empty)
                parentBg = Color.FromArgb(28, 28, 28);
            using (var bg = new SolidBrush(parentBg))
                g.FillRectangle(bg, ClientRectangle);

            // Gambar tombol rounded
            var bgColor = _pressed ? PressedColor : (_hovering ? HoverColor : NormalColor);
            using (var path = RoundedRect(
                new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), CornerRadius))
            {
                using (var b = new SolidBrush(bgColor))
                    g.FillPath(b, path);

                if (BorderWidth > 0 && BorderColor != Color.Transparent)
                {
                    using var pen = new Pen(BorderColor, BorderWidth);
                    g.DrawPath(pen, path);
                }
            }

            // Layout image + text (horizontal center)
            var img = Image;
            int imgW = img?.Width ?? 0;
            int imgH = img?.Height ?? 0;
            var txtSize = string.IsNullOrEmpty(Text)
                ? Size.Empty
                : TextRenderer.MeasureText(Text, Font);
            int gap = (img != null && txtSize.Width > 0) ? 8 : 0;
            int totalW = imgW + gap + txtSize.Width;
            int startX = (Width - totalW) / 2;

            if (img != null)
                g.DrawImage(img, startX, (Height - imgH) / 2, imgW, imgH);

            if (!string.IsNullOrEmpty(Text))
            {
                int txtX = startX + imgW + gap;
                int txtY = (Height - txtSize.Height) / 2;
                TextRenderer.DrawText(g, Text, Font,
                    new Point(txtX, txtY), ForeColor, TextFormatFlags.NoPadding);
            }
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