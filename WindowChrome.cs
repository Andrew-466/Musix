using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicPlayer
{
    /// <summary>
    /// Helper: mendeteksi apakah kursor berada di tepi window (area resize).
    /// </summary>
    internal static class EdgeHit
    {
        public const int WM_NCHITTEST = 0x84;
        public const int HTTRANSPARENT = -1;
        public const int ResizeEdge = 6;

        public static bool IsOnFormEdge(Control c, IntPtr lParam)
        {
            var form = c.FindForm();
            if (form == null || form.WindowState != FormWindowState.Normal) return false;

            long lp = lParam.ToInt64();
            int x = (short)(lp & 0xFFFF);
            int y = (short)((lp >> 16) & 0xFFFF);
            var pt = form.PointToClient(new Point(x, y));

            return pt.X < ResizeEdge || pt.Y < ResizeEdge ||
                   pt.X >= form.ClientSize.Width - ResizeEdge ||
                   pt.Y >= form.ClientSize.Height - ResizeEdge;
        }
    }

    /// <summary>
    /// Panel biasa, tapi "tembus" di tepi window supaya form di belakangnya
    /// bisa menangani resize (window frameless tidak punya border native).
    /// </summary>
    public class EdgePanel : Panel
    {
        public EdgePanel()
        {
            DoubleBuffered = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == EdgeHit.WM_NCHITTEST && EdgeHit.IsOnFormEdge(this, m.LParam))
            {
                m.Result = (IntPtr)EdgeHit.HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }
    }

    public enum WindowButtonKind { Minimize, Maximize, Restore, Close }

    /// <summary>
    /// Tombol minimize / maximize / close ala Windows 11, digambar manual.
    /// </summary>
    public sealed class WindowButton : Control
    {
        private bool _hover, _down;
        private WindowButtonKind _kind;

        public WindowButtonKind Kind
        {
            get => _kind;
            set { _kind = value; Invalidate(); }
        }

        public Color HoverColor { get; set; } = Color.FromArgb(40, 40, 45);
        public Color PressedColor { get; set; } = Color.FromArgb(55, 55, 60);
        public Color GlyphColor { get; set; } = Color.FromArgb(225, 225, 225);

        public WindowButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Size = new Size(46, 36);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == EdgeHit.WM_NCHITTEST && EdgeHit.IsOnFormEdge(this, m.LParam))
            {
                m.Result = (IntPtr)EdgeHit.HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool isClose = _kind == WindowButtonKind.Close;

            Color bg = Parent?.BackColor ?? Color.Black;
            Color glyph = GlyphColor;
            if (_hover || _down)
            {
                if (isClose)
                {
                    bg = _down ? Color.FromArgb(190, 20, 35) : Color.FromArgb(232, 17, 35);
                    glyph = Color.White;
                }
                else
                {
                    bg = _down ? PressedColor : HoverColor;
                    glyph = Color.White;
                }
            }

            using (var b = new SolidBrush(bg))
                g.FillRectangle(b, ClientRectangle);

            float cx = Width / 2f;
            float cy = Height / 2f;

            using var pen = new Pen(glyph, 1f);

            switch (_kind)
            {
                case WindowButtonKind.Minimize:
                    g.SmoothingMode = SmoothingMode.None;
                    g.DrawLine(pen, (int)cx - 5, (int)cy, (int)cx + 5, (int)cy);
                    break;

                case WindowButtonKind.Maximize:
                    g.SmoothingMode = SmoothingMode.None;
                    g.DrawRectangle(pen, (int)cx - 5, (int)cy - 5, 9, 9);
                    break;

                case WindowButtonKind.Restore:
                    g.SmoothingMode = SmoothingMode.None;
                    // kotak belakang
                    g.DrawLines(pen, new[]
                    {
                        new Point((int)cx - 2, (int)cy - 5 + 0),
                        new Point((int)cx - 2, (int)cy - 7),
                        new Point((int)cx + 5, (int)cy - 7),
                        new Point((int)cx + 5, (int)cy),
                        new Point((int)cx + 3, (int)cy)
                    });
                    // kotak depan
                    g.DrawRectangle(pen, (int)cx - 5, (int)cy - 4, 7, 7);
                    break;

                case WindowButtonKind.Close:
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                    break;
            }
        }
    }
}
