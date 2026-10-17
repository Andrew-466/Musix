using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using System.Runtime.InteropServices;
using Timer = System.Windows.Forms.Timer;

namespace MusicPlayer
{
    // ================= THEME =================
    public static class Theme
    {
        public static readonly Color Background  = Color.FromArgb(14, 14, 16);
        public static readonly Color Panel       = Color.FromArgb(24, 24, 28);
        public static readonly Color PanelHover  = Color.FromArgb(42, 42, 48);
        public static readonly Color PanelRaised = Color.FromArgb(34, 34, 39);
        public static readonly Color Accent      = Color.FromArgb(29, 185, 84);
        public static readonly Color AccentHover = Color.FromArgb(38, 214, 104);
        public static readonly Color AccentPress = Color.FromArgb(22, 150, 70);
        public static readonly Color Text        = Color.FromArgb(238, 238, 242);
        public static readonly Color TextBright  = Color.White;
        public static readonly Color TextDim     = Color.FromArgb(150, 150, 160);
        public static readonly Color TextFaint   = Color.FromArgb(95, 95, 105);
        public static readonly Color Border      = Color.FromArgb(40, 40, 46);
        public static readonly Color Warning     = Color.FromArgb(230, 180, 80);
        public static readonly Color Track       = Color.FromArgb(62, 62, 70);
    }

    public enum RepeatMode { None = 0, All = 1, One = 2 }

    // ================= TRACK =================
    public sealed class Track
    {
        public string FilePath { get; set; } = "";
        public string Title    { get; set; } = "";
        public string Artist   { get; set; } = "";
        public string Album    { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public override string ToString() =>
            string.IsNullOrEmpty(Artist) ? Title : $"{Artist} - {Title}";
    }

    // ================= SETTINGS =================
    internal sealed class AppSettings
    {
        public int Volume { get; set; } = 50;
        public bool Shuffle { get; set; }
        public int RepeatMode { get; set; }
        public int LastIndex { get; set; } = -1;
        public double LastPosition { get; set; } = 0;
        public List<string> PlaylistPaths { get; set; } = new();
    }

    // ================= FORM =================
    public sealed class Form1 : Form
    {
        private WaveOutEvent?    output;
        private AudioFileReader? audioFile;
        private bool isPlaying = false;
        private bool isDraggingSeek = false;
        private bool switchingTrack = false;
        private volatile bool stopRequested = false;

        private readonly List<Track> playlist = new();
        private int currentIndex = -1;

        private bool shuffle = false;
        private RepeatMode repeatMode = RepeatMode.None;
        private readonly Random rnd = new();
        private readonly Stack<int> shuffleHistory = new();

        private int volumeBeforeMute = 50;
        private double _pendingResumePosition = 0;

        // UI
        private ListBox      lstPlaylist = null!;
        private Panel        pnlEmptyState = null!;
        private RoundButton  btnFolder = null!;
        private RoundButton  btnPrev = null!, btnPlayPause = null!, btnStop = null!, btnNext = null!;
        private RoundButton  btnShuffle = null!, btnRepeat = null!;
        private AccentSlider trkVolume = null!, trkSeek = null!;
        private Label        lblTitle = null!, lblStatus = null!, lblTime = null!, lblTotal = null!, lblVolume = null!;
        private PictureBox   picVolume = null!;
        private Timer        uiTimer = null!;

        // Frameless window chrome
        private EdgePanel    pnlTitleBar = null!;
        private WindowButton btnMin = null!, btnMax = null!, btnClose = null!;
        private Label        lblQueue = null!;
        private Icon?        appIcon;
        private Bitmap?      appIconBmp;
        private readonly Font fontAppTitle = new("Segoe UI Semibold", 9.5F);

        private readonly Font fontListTitle = new("Segoe UI Semibold", 10.5F);
        private readonly Font fontListSub   = new("Segoe UI", 8.5F);
        private readonly Font fontListNum   = new("Segoe UI", 9F);

        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicPlayer");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

        private static readonly string[] AudioExts =
            { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac", ".ogg", ".aiff" };

        // ================= CTOR =================
        public Form1()
        {
            LoadAppIcon();
            BuildUI();
            WireEvents();
            uiTimer.Start();
            lblStatus.Text = "Belum ada lagu dimuat";

            Load += async (_, _) => await LoadSettingsAsync();
            Load += (_, _) => ApplyDarkScrollbar(lstPlaylist);
        }

        // ==================================================
        // BUILD UI
        // ==================================================
        private void BuildUI()
        {
            Text             = "MusiX";
            FormBorderStyle  = FormBorderStyle.None;      // frameless
            ClientSize       = new Size(980, 720);
            MinimumSize      = new Size(860, 620);
            StartPosition    = FormStartPosition.CenterScreen;
            BackColor        = Theme.Border;              // Padding 1px => garis tepi window
            Padding          = new Padding(1);
            ForeColor        = Theme.Text;
            Font             = new Font("Segoe UI", 9F);
            KeyPreview       = true;
            DoubleBuffered   = true;
            AllowDrop        = true;
            ControlBox       = false;   // jangan biarkan Windows menggambar tombol/caption sendiri
            if (appIcon != null) Icon = appIcon;

            // ==================================================
            // TITLE BAR (custom) — ikon + nama app kiri, min/max/close kanan
            // ==================================================
            pnlTitleBar = new EdgePanel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Theme.Background
            };
            pnlTitleBar.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                int x = 14;
                if (appIconBmp != null)
                {
                    g.DrawImage(appIconBmp, x, (pnlTitleBar.Height - 18) / 2, 18, 18);
                    x += 18 + 10;
                }
                TextRenderer.DrawText(g, "MusiX", fontAppTitle,
                    new Rectangle(x, 0, 200, pnlTitleBar.Height), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };

            btnMin   = new WindowButton { Dock = DockStyle.Right, Width = 46, Kind = WindowButtonKind.Minimize };
            btnMax   = new WindowButton { Dock = DockStyle.Right, Width = 46, Kind = WindowButtonKind.Maximize };
            btnClose = new WindowButton { Dock = DockStyle.Right, Width = 46, Kind = WindowButtonKind.Close };
            // urutan Add: yang terakhir ditambahkan menempel paling kanan
            pnlTitleBar.Controls.Add(btnMin);
            pnlTitleBar.Controls.Add(btnMax);
            pnlTitleBar.Controls.Add(btnClose);

            // ==================================================
            // HEADER — judul lagu kiri, tombol Buka Folder kanan
            // ==================================================
            var pnlHeader = new EdgePanel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Theme.Background,
                Padding = new Padding(26, 8, 26, 10)
            };

            var pnlTexts = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextDim,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = ""
            };

            lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Semibold", 17F),
                ForeColor = Theme.TextBright,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = "Tidak ada lagu diputar"
            };

            pnlTexts.Controls.Add(lblTitle);
            pnlTexts.Controls.Add(lblStatus);

            // Host tombol: padding atas/bawah supaya tombol selalu penuh & rata tengah
            var pnlBtnHost = new Panel
            {
                Dock = DockStyle.Right,
                Width = 176,
                BackColor = Theme.Background,
                Padding = new Padding(16, 11, 0, 11)
            };

            btnFolder = new RoundButton
            {
                Text = "Buka Folder",
                Dock = DockStyle.Fill,
                CornerRadius = 22,
                NormalColor = Theme.Accent,
                HoverColor = Theme.AccentHover,
                PressedColor = Theme.AccentPress,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10F),
                Margin = Padding.Empty
            };
            try { btnFolder.Image = BxIcon.FolderOpen(18, Color.White); } catch { }
            pnlBtnHost.Controls.Add(btnFolder);

            pnlHeader.Controls.Add(pnlTexts);
            pnlHeader.Controls.Add(pnlBtnHost);

            // ==================================================
            // PLAYLIST + EMPTY STATE
            // ==================================================
            lstPlaylist = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 54,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 10F),
                SelectionMode = SelectionMode.One,
                AllowDrop = true
            };
            ApplyDarkScrollbar(lstPlaylist);
            pnlEmptyState = BuildEmptyState();

            var pnlList = new EdgePanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Background,
                Padding = new Padding(20, 4, 20, 14)
            };
            pnlList.Controls.Add(lstPlaylist);
            pnlList.Controls.Add(pnlEmptyState);
            pnlEmptyState.BringToFront();

            // ==================================================
            // CONTROL BAR (bawah) — seek di atas, tombol + volume di bawah
            // ==================================================
            var pnlControls = new EdgePanel
            {
                Dock = DockStyle.Bottom,
                Height = 128,
                BackColor = Theme.Background,
                Padding = new Padding(26, 10, 26, 12)
            };
            pnlControls.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, pnlControls.Width, 0);
            };

            var tblControls = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblControls.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            tblControls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ---- BARIS 1: Seek ----
            var pnlSeek = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            pnlSeek.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            pnlSeek.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlSeek.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            pnlSeek.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            lblTime = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5F),
                ForeColor = Theme.TextDim,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "00:00"
            };
            lblTotal = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.5F),
                ForeColor = Theme.TextDim,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "00:00"
            };
            trkSeek = new AccentSlider
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                TrackColor = Theme.Track,
                FillColor = Theme.Accent,
                ThumbColor = Color.White,
                ThumbSize = 12,
                ThumbSizeHover = 14,
                Margin = new Padding(0, 4, 0, 4)
            };

            pnlSeek.Controls.Add(lblTime, 0, 0);
            pnlSeek.Controls.Add(trkSeek, 1, 0);
            pnlSeek.Controls.Add(lblTotal, 2, 0);

            // ---- BARIS 2: [ antrian ] [ tombol kontrol ] [ volume ] ----
            var tblButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // kiri: nomor antrian
            lblQueue = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextDim,
                BackColor = Theme.Background,
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty,
                Text = ""
            };

            // tengah: tombol
            var flowBtns = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.None
            };

            btnShuffle = MakeIconButton(44, 44);
            btnPrev    = MakeIconButton(44, 44);
            btnStop    = MakeIconButton(44, 44);
            btnNext    = MakeIconButton(44, 44);

            // Play — besar, hijau, bulat. Margin vertikal 0 → total tinggi 60
            btnPlayPause = new RoundButton
            {
                Width = 60,
                Height = 60,
                CornerRadius = 30,
                NormalColor = Theme.Accent,
                HoverColor = Theme.AccentHover,
                PressedColor = Theme.AccentPress,
                ForeColor = Color.White,
                Margin = new Padding(12, 0, 12, 0)
            };
            try { btnPlayPause.Image = BxIcon.Play(28, Color.White); } catch { }

            // Repeat — pill. Margin vertikal (10) + tinggi 40 = 60 → sejajar dengan Play
            btnRepeat = new RoundButton
            {
                Text = "OFF",
                Width = 76,
                Height = 40,
                CornerRadius = 20,
                NormalColor = Theme.Panel,
                HoverColor = Theme.PanelHover,
                PressedColor = Theme.PanelHover,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI Semibold", 8.5F),
                Margin = new Padding(8, 10, 4, 10)
            };
            try { btnRepeat.Image = BxIcon.Repeat(16, Theme.Text); } catch { }

            try
            {
                btnShuffle.Image = BxIcon.Shuffle(20, Theme.Text);
                btnPrev.Image    = BxIcon.SkipPrev(20, Theme.Text);
                btnStop.Image    = BxIcon.Stop(20, Theme.Text);
                btnNext.Image    = BxIcon.SkipNext(20, Theme.Text);
            }
            catch { }

            flowBtns.Controls.AddRange(new Control[]
                { btnShuffle, btnPrev, btnPlayPause, btnNext, btnStop, btnRepeat });

            // kanan: volume
            var flowVol = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Theme.Background,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.Right
            };

            picVolume = new PictureBox
            {
                Width = 22, Height = 22,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Theme.Background,
                Margin = new Padding(0, 8, 6, 8)
            };
            try { picVolume.Image = BxIcon.VolumeFull(18, Theme.TextDim); } catch { }

            trkVolume = new AccentSlider
            {
                Width = 110,
                Height = 26,
                Minimum = 0, Maximum = 100, Value = 50,
                TrackColor = Theme.Track,
                FillColor = Theme.Accent,
                ThumbColor = Color.White,
                ThumbSize = 12,
                ThumbSizeHover = 14,
                Margin = new Padding(0, 6, 8, 6)
            };

            lblVolume = new Label
            {
                Width = 42, Height = 26,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextDim,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "50%",
                Margin = new Padding(0, 6, 0, 6)
            };

            flowVol.Controls.AddRange(new Control[] { picVolume, trkVolume, lblVolume });

            tblButtons.Controls.Add(lblQueue, 0, 0);
            tblButtons.Controls.Add(flowBtns, 1, 0);
            tblButtons.Controls.Add(flowVol, 2, 0);

            tblControls.Controls.Add(pnlSeek, 0, 0);
            tblControls.Controls.Add(tblButtons, 0, 1);
            pnlControls.Controls.Add(tblControls);

            // ---------- ADD TO FORM (yang terakhir ditambahkan di-dock paling luar) ----------
            Controls.Add(pnlList);
            Controls.Add(pnlControls);
            Controls.Add(pnlHeader);
            Controls.Add(pnlTitleBar);

            uiTimer = new Timer { Interval = 250 };
            UpdateEmptyState();
            UpdateWindowChrome();
        }

        // ==================================================
        // FRAMELESS WINDOW
        // ==================================================
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCLIENT = 1, HTCAPTION = 2;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                          HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void LoadAppIcon()
        {
            try
            {
                using var stream = typeof(Form1).Assembly.GetManifestResourceStream("musix.ico");
                if (stream == null) return;
                appIcon = new Icon(stream);
                using var small = new Icon(appIcon, new Size(32, 32));
                appIconBmp = small.ToBitmap();
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // Style native ini yang membuat Aero Snap (drag ke tepi layar, Win+Arrow)
                // dan minimize dari taskbar bekerja di window tanpa border.
                cp.Style      |= 0x00040000    // WS_THICKFRAME
                               | 0x00010000    // WS_MAXIMIZEBOX
                               | 0x00020000;   // WS_MINIMIZEBOX
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW → bayangan window
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2;   // DWMWCP_ROUND (Windows 11)
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
            }
            catch { }
        }

        private const int WM_NCCALCSIZE   = 0x0083;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_NCPAINT       = 0x0085;
        private const int WM_NCACTIVATE    = 0x0086;

        protected override void WndProc(ref Message m)
        {
            // Hilangkan frame native (WS_THICKFRAME) supaya seluruh window = client area
            if (m.Msg == WM_NCCALCSIZE)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Cegah Windows menggambar caption/frame native di atas title bar kita
            if (m.Msg == WM_NCPAINT)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WM_NCACTIVATE)
            {
                m.LParam = (IntPtr)(-1);   // proses aktivasi, tapi tanpa repaint frame
                base.WndProc(ref m);
                return;
            }

            // Batas maximize mengikuti work area monitor tempat window berada
            // (berlaku juga untuk Win+Up / snap, bukan hanya tombol maximize)
            if (m.Msg == WM_GETMINMAXINFO)
                UpdateMaximizedBounds();

            // Resize dari tepi/sudut (window tanpa border native)
            if (m.Msg == EdgeHit.WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    long lp = m.LParam.ToInt64();
                    int x = (short)(lp & 0xFFFF);
                    int y = (short)((lp >> 16) & 0xFFFF);
                    var pt = PointToClient(new Point(x, y));

                    const int e = EdgeHit.ResizeEdge;   // tebal tepi
                    const int c = 16;                   // area sudut
                    int w = ClientSize.Width, h = ClientSize.Height;

                    bool left = pt.X < e, right = pt.X >= w - e;
                    bool top = pt.Y < e, bottom = pt.Y >= h - e;
                    bool nearL = pt.X < c, nearR = pt.X >= w - c;
                    bool nearT = pt.Y < c, nearB = pt.Y >= h - c;

                    int hit = HTCLIENT;
                    if ((left && nearT) || (top && nearL))          hit = HTTOPLEFT;
                    else if ((right && nearT) || (top && nearR))    hit = HTTOPRIGHT;
                    else if ((left && nearB) || (bottom && nearL))  hit = HTBOTTOMLEFT;
                    else if ((right && nearB) || (bottom && nearR)) hit = HTBOTTOMRIGHT;
                    else if (left)   hit = HTLEFT;
                    else if (right)  hit = HTRIGHT;
                    else if (top)    hit = HTTOP;
                    else if (bottom) hit = HTBOTTOM;

                    if (hit != HTCLIENT) m.Result = (IntPtr)hit;
                }
                return;
            }
            base.WndProc(ref m);
        }

        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (e.Clicks == 2)
            {
                ToggleMaximize();
                return;
            }
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private void UpdateMaximizedBounds()
        {
            try
            {
                var scr = Screen.FromHandle(Handle);
                var wa = scr.WorkingArea;
                MaximizedBounds = new Rectangle(wa.X - scr.Bounds.X, wa.Y - scr.Bounds.Y, wa.Width, wa.Height);
            }
            catch { }
        }

        private void UpdateWindowChrome()
        {
            if (btnMax == null) return;
            bool max = WindowState == FormWindowState.Maximized;
            Padding = max ? Padding.Empty : new Padding(1);
            btnMax.Kind = max ? WindowButtonKind.Restore : WindowButtonKind.Maximize;
        }

        // ================= DARK SCROLLBAR (Win32) =================
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

        private const int GWL_STYLE = -16;
        private const int WS_VSCROLL = 0x00200000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// Aktifkan dark mode scrollbar pada ListBox.
        /// </summary>
        private static void ApplyDarkScrollbar(ListBox list)
        {
            if (list.IsHandleCreated)
            {
                SetWindowTheme(list.Handle, "DarkMode_Explorer", null);
            }
            else
            {
                list.HandleCreated += (_, _) =>
                    SetWindowTheme(list.Handle, "DarkMode_Explorer", null);
            }
        }

        // ==================================================
        // EMPTY STATE
        // ==================================================
        private Panel BuildEmptyState()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Panel,
                Visible = true
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Theme.Panel
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

            var pic = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Theme.Panel
            };
            try { pic.Image = BxIcon.Get("bx-music", 96, Theme.TextDim); } catch { }

            var lblTitleEmpty = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Belum ada lagu",
                Font = new Font("Segoe UI Semibold", 14F),
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.Panel
            };

            var lblHint = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Klik  \"Buka Folder\"  atau seret file musik ke sini",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Theme.TextDim,
                TextAlign = ContentAlignment.TopCenter,
                BackColor = Theme.Panel
            };

            layout.Controls.Add(pic, 0, 0);
            layout.Controls.Add(lblTitleEmpty, 0, 1);
            layout.Controls.Add(lblHint, 0, 2);
            pnl.Controls.Add(layout);
            return pnl;
        }

        // ==================================================
        // BUTTON FACTORY
        // ==================================================
        private static RoundButton MakeIconButton(int w, int h)
        {
            return new RoundButton
            {
                Width = w,
                Height = h,
                CornerRadius = 22,
                NormalColor = Theme.Panel,
                HoverColor = Theme.PanelHover,
                PressedColor = Theme.Accent,
                ForeColor = Theme.Text,
                Margin = new Padding(4, 8, 4, 8)   // total tinggi 60 → sejajar dengan tombol Play
            };
        }

        // ==================================================
        // WIRE EVENTS
        // ==================================================
        private void WireEvents()
        {
            btnFolder.Click += async (_, _) => await OpenFolderAsync();

            btnPlayPause.Click += (_, _) => TogglePlayPause();
            btnStop.Click      += (_, _) => StopPlayback();
            btnNext.Click      += (_, _) => PlayNext();
            btnPrev.Click      += (_, _) => PlayPrev();

            btnShuffle.Click += (_, _) => { shuffle = !shuffle; UpdateModeButtons(); };
            btnRepeat.Click  += (_, _) =>
            {
                repeatMode = (RepeatMode)(((int)repeatMode + 1) % 3);
                UpdateModeButtons();
            };

            trkVolume.ValueChanged += (_, _) =>
            {
                if (audioFile != null) audioFile.Volume = trkVolume.Value / 100f;
                UpdateVolumeUI();
            };

            trkSeek.DragStarted  += (_, _) => isDraggingSeek = true;
            trkSeek.DragEnded    += (_, _) => isDraggingSeek = false;
            trkSeek.ValueChanged += (_, _) =>
            {
                if (!isDraggingSeek) return;
                if (audioFile == null || audioFile.TotalTime.TotalSeconds <= 0) return;
                audioFile.CurrentTime = TimeSpan.FromSeconds(
                    (trkSeek.Value / 100.0) * audioFile.TotalTime.TotalSeconds);
            };

            lstPlaylist.DrawItem    += LstPlaylist_DrawItem;
            lstPlaylist.DoubleClick += (_, _) =>
            {
                if (lstPlaylist.SelectedIndex >= 0)
                {
                    currentIndex = lstPlaylist.SelectedIndex;
                    PlayCurrent();
                }
            };
            lstPlaylist.KeyDown += LstPlaylist_KeyDown;

            // window chrome
            btnMin.Click   += (_, _) => WindowState = FormWindowState.Minimized;
            btnMax.Click   += (_, _) => ToggleMaximize();
            btnClose.Click += (_, _) => Close();
            pnlTitleBar.MouseDown += TitleBar_MouseDown;
            Resize += (_, _) => UpdateWindowChrome();

            uiTimer.Tick += UiTimer_Tick;
            KeyDown      += Form_KeyDown;
            FormClosing  += (_, _) => { SaveSettings(); CleanupAudio(); };

            DragEnter += Form_DragEnter;
            DragDrop  += Form_DragDrop;
            lstPlaylist.DragEnter += Form_DragEnter;
            lstPlaylist.DragDrop  += Form_DragDrop;
        }

        // ==================================================
        // VOLUME UI
        // ==================================================
        private void UpdateVolumeUI()
        {
            bool muted = trkVolume.Value == 0;
            lblVolume.Text = $"{trkVolume.Value}%";
            try
            {
                picVolume.Image = muted
                    ? BxIcon.VolumeMute(18, Theme.TextDim)
                    : BxIcon.VolumeFull(18, Theme.TextDim);
            }
            catch { }
        }

        // ==================================================
        // DUPLICATE CHECK
        // ==================================================
        private bool ContainsPath(string path)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch { return false; }
            foreach (var t in playlist)
                if (string.Equals(t.FilePath, full, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private int AddTracksFiltered(IEnumerable<Track> tracks)
        {
            int added = 0;
            foreach (var t in tracks)
            {
                if (ContainsPath(t.FilePath)) continue;
                playlist.Add(t);
                lstPlaylist.Items.Add(t);
                added++;
            }
            return added;
        }

        // ==================================================
        // OPEN FOLDER
        // ==================================================
        private async Task OpenFolderAsync()
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "Pilih folder berisi file audio",
                UseDescriptionForTitle = true
            };
            if (fbd.ShowDialog(this) != DialogResult.OK) return;

            SetBusy(true, "Membaca folder...");
            try
            {
                var files = await Task.Run(() => EnumerateAudioFiles(fbd.SelectedPath).ToList());
                if (files.Count == 0)
                {
                    MessageBox.Show(this, "Tidak ada file audio di folder tersebut.",
                        "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SetStatusSafe($"Memuat metadata 0/{files.Count}...");
                var tracks = await Task.Run(() => files.Select(LoadTrackMetadata).ToList());

                int added = AddTracksFiltered(tracks);
                int skipped = tracks.Count - added;

                UpdateStatusCount();
                if (skipped > 0)
                    SetStatusSafe($"Ditambahkan {added} lagu ({skipped} duplikat dilewati)");

                if (currentIndex == -1 && playlist.Count > 0)
                {
                    currentIndex = 0;
                    PlayCurrent();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Gagal membaca folder: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false, null); }
        }

        // ==================================================
        // DRAG & DROP
        // ==================================================
        private void Form_DragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private async void Form_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths) return;

            SetBusy(true, "Memuat file yang di-drop...");
            try
            {
                var files = await Task.Run(() =>
                {
                    var list = new List<string>();
                    foreach (var p in paths)
                    {
                        if (Directory.Exists(p)) list.AddRange(EnumerateAudioFiles(p));
                        else if (File.Exists(p) && IsAudioFile(p)) list.Add(p);
                    }
                    return list.Distinct().ToList();
                });

                if (files.Count == 0) return;

                var tracks = await Task.Run(() => files.Select(LoadTrackMetadata).ToList());

                int added = AddTracksFiltered(tracks);
                int skipped = tracks.Count - added;

                UpdateStatusCount();
                if (added > 0)
                    SetStatusSafe($"Ditambahkan {added} lagu" +
                                  (skipped > 0 ? $" ({skipped} duplikat dilewati)" : ""));

                if (currentIndex == -1 && playlist.Count > 0)
                {
                    currentIndex = 0;
                    PlayCurrent();
                }
            }
            finally { SetBusy(false, null); }
        }

        // ==================================================
        // REMOVE
        // ==================================================
        private void RemoveAt(int index)
        {
            if (index < 0 || index >= playlist.Count) return;

            bool wasCurrent = index == currentIndex;
            playlist.RemoveAt(index);
            lstPlaylist.Items.RemoveAt(index);

            if (playlist.Count == 0)
            {
                CleanupAudio();
                currentIndex = -1;
                lblTitle.Text = "Tidak ada lagu diputar";
                lblTime.Text = lblTotal.Text = "00:00";
                trkSeek.Value = 0;
            }
            else if (wasCurrent)
            {
                if (currentIndex >= playlist.Count) currentIndex = playlist.Count - 1;
                PlayCurrent();
            }
            else if (index < currentIndex) currentIndex--;

            UpdateStatusCount();
        }

        private void UpdateStatusCount()
        {
            int n = playlist.Count;
            lblStatus.Text = n == 0 ? "Belum ada lagu dimuat" : $"{n} lagu dalam playlist";
            UpdateQueueLabel();
            UpdateEmptyState();
        }

        private void UpdateQueueLabel()
        {
            if (lblQueue == null) return;
            lblQueue.Text = (playlist.Count > 0 && currentIndex >= 0)
                ? $"{currentIndex + 1} / {playlist.Count}"
                : "";
        }

        private void UpdateEmptyState()
        {
            pnlEmptyState.Visible = playlist.Count == 0;
            if (pnlEmptyState.Visible) pnlEmptyState.BringToFront();
        }

        // ==================================================
        // PLAYBACK
        // ==================================================
        private void PlayCurrent()
        {
            if (currentIndex < 0 || currentIndex >= playlist.Count) return;
            if (TryPlayIndex(currentIndex)) return;
            PlayNext();
        }

        private bool TryPlayIndex(int index)
        {
            if (index < 0 || index >= playlist.Count) return false;

            var track = playlist[index];
            if (!File.Exists(track.FilePath))
            {
                SetStatusSafe($"File tidak ditemukan: {track.Title}");
                return false;
            }

            try
            {
                CleanupAudio();
                stopRequested = false;

                audioFile = new AudioFileReader(track.FilePath)
                {
                    Volume = trkVolume.Value / 100f
                };
                output = new WaveOutEvent { DesiredLatency = 200 };
                output.Init(audioFile);
                output.PlaybackStopped += Output_PlaybackStopped;
                output.Play();

                isPlaying = true;
                currentIndex = index;

                lblTitle.Text = track.ToString();
                lblTotal.Text = FormatTime(audioFile.TotalTime);
                lblTime.Text  = "00:00";
                trkSeek.Value = 0;

                if (currentIndex < lstPlaylist.Items.Count)
                    lstPlaylist.SelectedIndex = currentIndex;

                UpdatePlayButton();
                UpdateStatusCount();
                return true;
            }
            catch (Exception ex)
            {
                SetStatusSafe($"Gagal memutar: {ex.Message}");
                return false;
            }
        }

        private void TogglePlayPause()
        {
            if (output == null || audioFile == null)
            {
                if (playlist.Count > 0)
                {
                    if (currentIndex < 0) currentIndex = 0;
                    PlayCurrent();
                }
                return;
            }

            switch (output.PlaybackState)
            {
                case PlaybackState.Playing: output.Pause(); isPlaying = false; break;
                case PlaybackState.Paused:  output.Play();  isPlaying = true;  break;
                default:                    PlayCurrent(); return;
            }
            UpdatePlayButton();
        }

        private void StopPlayback()
        {
            if (output == null) return;
            stopRequested = true;
            output.Stop();
            isPlaying = false;
            if (audioFile != null) audioFile.CurrentTime = TimeSpan.Zero;
            trkSeek.Value = 0;
            lblTime.Text  = "00:00";
            UpdatePlayButton();
        }

        private void PlayNext(bool fromNaturalEnd = false)
        {
            if (playlist.Count == 0) return;

            if (fromNaturalEnd && repeatMode == RepeatMode.One)
            {
                PlayCurrent();
                return;
            }

            int total = playlist.Count;
            int idx = currentIndex;
            int tried = 0;

            while (tried < total)
            {
                if (shuffle && total > 1)
                {
                    shuffleHistory.Push(idx);
                    int old = idx;
                    do { idx = rnd.Next(total); } while (idx == old);
                }
                else
                {
                    idx++;
                    if (idx >= total)
                    {
                        if (repeatMode == RepeatMode.All) idx = 0;
                        else { StopPlayback(); return; }
                    }
                }

                if (TryPlayIndex(idx)) return;
                tried++;
            }

            StopPlayback();
            MessageBox.Show(this,
                "Tidak ada lagu yang bisa diputar (semua file hilang atau rusak).",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void PlayPrev()
        {
            if (playlist.Count == 0) return;

            if (audioFile != null && audioFile.CurrentTime.TotalSeconds > 3)
            {
                audioFile.CurrentTime = TimeSpan.Zero;
                return;
            }

            if (shuffle && shuffleHistory.Count > 0)
            {
                while (shuffleHistory.Count > 0)
                {
                    int prev = shuffleHistory.Pop();
                    if (TryPlayIndex(prev)) return;
                }
            }

            int idx = currentIndex - 1;
            if (idx < 0) idx = playlist.Count - 1;
            if (TryPlayIndex(idx)) return;

            for (int i = 0; i < playlist.Count; i++)
            {
                idx = idx - 1;
                if (idx < 0) idx = playlist.Count - 1;
                if (TryPlayIndex(idx)) return;
            }

            StopPlayback();
            MessageBox.Show(this,
                "Tidak ada lagu sebelumnya yang bisa diputar.",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (stopRequested) { stopRequested = false; return; }
            if (switchingTrack) return;

            if (e.Exception != null)
            {
                var msg = e.Exception.Message;
                BeginInvoke(new Action(() =>
                {
                    SetStatusSafe($"Playback error: {msg} — lanjut ke lagu berikutnya");
                    PlayNext(fromNaturalEnd: true);
                }));
                return;
            }

            BeginInvoke(new Action(() => PlayNext(fromNaturalEnd: true)));
        }

        private void CleanupAudio()
        {
            switchingTrack = true;
            if (output != null)
            {
                output.PlaybackStopped -= Output_PlaybackStopped;
                try { output.Stop(); } catch { }
                try { output.Dispose(); } catch { }
                output = null;
            }
            audioFile?.Dispose();
            audioFile = null;
            isPlaying = false;
            switchingTrack = false;
            UpdatePlayButton();
        }

        // ==================================================
        // UI UPDATE
        // ==================================================
        private void UpdatePlayButton()
        {
            try
            {
                btnPlayPause.Image = isPlaying
                    ? BxIcon.Pause(28, Color.White)
                    : BxIcon.Play(28, Color.White);
            }
            catch { }
        }

        private void UpdateModeButtons()
        {
            // Shuffle
            btnShuffle.NormalColor  = shuffle ? Theme.Accent : Theme.Panel;
            btnShuffle.HoverColor   = shuffle ? Theme.AccentHover : Theme.PanelHover;
            btnShuffle.PressedColor = shuffle ? Theme.AccentPress : Theme.PanelHover;
            try
            {
                btnShuffle.Image = shuffle
                    ? BxIcon.Shuffle(20, Color.White)
                    : BxIcon.Shuffle(20, Theme.Text);
            }
            catch { }
            btnShuffle.Invalidate();

            // Repeat
            btnRepeat.Text = repeatMode switch
            {
                RepeatMode.None => "OFF",
                RepeatMode.All  => "ALL",
                RepeatMode.One  => "1",
                _ => "OFF"
            };
            bool active = repeatMode != RepeatMode.None;
            btnRepeat.NormalColor  = active ? Theme.Accent : Theme.Panel;
            btnRepeat.HoverColor   = active ? Theme.AccentHover : Theme.PanelHover;
            btnRepeat.PressedColor = active ? Theme.AccentPress : Theme.PanelHover;
            btnRepeat.ForeColor    = active ? Color.White : Theme.Text;
            try
            {
                btnRepeat.Image = active
                    ? BxIcon.Repeat(16, Color.White)
                    : BxIcon.Repeat(16, Theme.Text);
            }
            catch { }
            btnRepeat.Invalidate();
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (audioFile == null) return;

            double total = audioFile.TotalTime.TotalSeconds;
            double cur   = audioFile.CurrentTime.TotalSeconds;

            if (!isDraggingSeek && total > 0)
            {
                int pct = (int)Math.Round((cur / total) * 100);
                pct = Math.Max(0, Math.Min(100, pct));
                if (trkSeek.Value != pct) trkSeek.Value = pct;
            }

            lblTime.Text = FormatTime(audioFile.CurrentTime);
        }

        private static string FormatTime(TimeSpan t)
        {
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
            return $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        private void SetStatusSafe(string text)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => lblStatus.Text = text)); return; }
            lblStatus.Text = text;
        }

        private void SetBusy(bool busy, string? status)
        {
            btnFolder.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (status != null) lblStatus.Text = status;
            else UpdateStatusCount();
        }

        // ==================================================
        // OWNER-DRAW PLAYLIST
        // ==================================================
        private void LstPlaylist_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= playlist.Count) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool playing = e.Index == currentIndex;
            var track = playlist[e.Index];

            using (var b = new SolidBrush(Theme.Panel))
                g.FillRectangle(b, e.Bounds);

            // Kartu baris (pill) untuk item terpilih
            var card = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 2, e.Bounds.Width - 16, e.Bounds.Height - 4);
            if (selected)
            {
                using var path = RoundRectPath(card, 8);
                using var b = new SolidBrush(Theme.PanelHover);
                g.FillPath(b, path);
            }

            // Kolom nomor / indikator sedang diputar
            var numRect = new Rectangle(card.X + 6, card.Y, 40, card.Height);
            if (playing)
            {
                int[] heights = { 8, 15, 11 };
                int barW = 3, gap = 2;
                int totalW = barW * 3 + gap * 2;
                int x0 = numRect.X + (numRect.Width - totalW) / 2;
                int baseY = numRect.Y + (numRect.Height + 15) / 2;
                using var b = new SolidBrush(Theme.Accent);
                for (int i = 0; i < 3; i++)
                    g.FillRectangle(b, x0 + i * (barW + gap), baseY - heights[i], barW, heights[i]);
            }
            else
            {
                TextRenderer.DrawText(g, (e.Index + 1).ToString(), fontListNum, numRect,
                    selected ? Theme.TextDim : Theme.TextFaint,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }

            // Teks
            const int durW = 64;
            int textX = card.X + 52;
            int textW = Math.Max(10, card.Right - 10 - durW - textX);
            var titleRect = new Rectangle(textX, card.Y + 5, textW, 22);
            var subRect   = new Rectangle(textX, card.Y + 27, textW, 18);
            var durRect   = new Rectangle(card.Right - 10 - durW, card.Y, durW, card.Height);

            const TextFormatFlags left =
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            bool missing = !File.Exists(track.FilePath);
            Color titleColor = missing ? Theme.Warning : (playing ? Theme.Accent : Theme.Text);

            TextRenderer.DrawText(g, track.Title, fontListTitle, titleRect, titleColor, left);

            string sub = string.IsNullOrEmpty(track.Artist) ? "Unknown Artist" : track.Artist;
            if (missing) sub += "  ·  (file hilang)";
            TextRenderer.DrawText(g, sub, fontListSub, subRect, Theme.TextDim, left);

            if (track.Duration.TotalSeconds > 0)
                TextRenderer.DrawText(g, FormatTime(track.Duration), fontListSub, durRect, Theme.TextDim,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath RoundRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ==================================================
        // KEYBOARD
        // ==================================================
        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Space: TogglePlayPause(); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Right:
                    if (audioFile != null)
                        audioFile.CurrentTime = audioFile.CurrentTime.Add(TimeSpan.FromSeconds(5));
                    e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Left:
                    if (audioFile != null)
                    {
                        var t = audioFile.CurrentTime - TimeSpan.FromSeconds(5);
                        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
                        audioFile.CurrentTime = t;
                    }
                    e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Up:   trkVolume.Value = Math.Min(100, trkVolume.Value + 5); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Down: trkVolume.Value = Math.Max(0, trkVolume.Value - 5); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.M:
                    if (trkVolume.Value > 0) { volumeBeforeMute = trkVolume.Value; trkVolume.Value = 0; }
                    else { trkVolume.Value = volumeBeforeMute > 0 ? volumeBeforeMute : 50; }
                    e.Handled = e.SuppressKeyPress = true; break;
                case Keys.N: PlayNext(); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.P: PlayPrev(); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.S:
                    if (!e.Control) { StopPlayback(); e.Handled = e.SuppressKeyPress = true; }
                    break;
            }
        }

        private void LstPlaylist_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && lstPlaylist.SelectedIndex >= 0)
            {
                RemoveAt(lstPlaylist.SelectedIndex);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && lstPlaylist.SelectedIndex >= 0)
            {
                currentIndex = lstPlaylist.SelectedIndex;
                PlayCurrent();
                e.Handled = true;
            }
        }

        // ==================================================
        // HELPERS
        // ==================================================
        private static Track LoadTrackMetadata(string path)
        {
            var t = new Track
            {
                FilePath = path,
                Title = Path.GetFileNameWithoutExtension(path)
            };
            try
            {
                using var tag = TagLib.File.Create(path);
                if (!string.IsNullOrWhiteSpace(tag.Tag.Title)) t.Title = tag.Tag.Title!;
                if (tag.Tag.Performers is { Length: > 0 })     t.Artist = tag.Tag.Performers[0];
                if (!string.IsNullOrWhiteSpace(tag.Tag.Album))  t.Album  = tag.Tag.Album!;
                t.Duration = tag.Properties.Duration;
            }
            catch { }
            return t;
        }

        private static IEnumerable<string> EnumerateAudioFiles(string folder)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
            }
            catch { yield break; }

            foreach (var f in files)
            {
                bool ok;
                try { ok = AudioExts.Contains(Path.GetExtension(f).ToLowerInvariant()); }
                catch { ok = false; }
                if (ok) yield return f;
            }
        }

        private static bool IsAudioFile(string path) =>
            AudioExts.Contains(Path.GetExtension(path).ToLowerInvariant());

        // ==================================================
        // SETTINGS
        // ==================================================
        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);

                double lastPos = 0;
                if (audioFile != null) { try { lastPos = audioFile.CurrentTime.TotalSeconds; } catch { } }

                var s = new AppSettings
                {
                    Volume = trkVolume.Value,
                    Shuffle = shuffle,
                    RepeatMode = (int)repeatMode,
                    LastIndex = currentIndex,
                    LastPosition = lastPos,
                    PlaylistPaths = playlist.Select(t => t.FilePath).ToList()
                };

                var json = JsonSerializer.Serialize(s,
                    new JsonSerializerOptions { WriteIndented = true });

                var task = Task.Run(() =>
                {
                    try { File.WriteAllText(SettingsFile, json); } catch { }
                });
                task.Wait(500);
            }
            catch { }
        }

        private async Task LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var json = await File.ReadAllTextAsync(SettingsFile);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s == null) return;

                trkVolume.Value = Math.Max(0, Math.Min(100, s.Volume));
                UpdateVolumeUI();
                volumeBeforeMute = trkVolume.Value > 0 ? trkVolume.Value : 50;

                shuffle    = s.Shuffle;
                repeatMode = (RepeatMode)Math.Max(0, Math.Min(2, s.RepeatMode));
                UpdateModeButtons();

                var validPaths = s.PlaylistPaths.Where(File.Exists).Distinct().ToList();
                if (validPaths.Count > 0)
                {
                    SetBusy(true, $"Memuat playlist sebelumnya ({validPaths.Count} lagu)...");
                    var tracks = await Task.Run(() => validPaths.Select(LoadTrackMetadata).ToList());

                    int added = AddTracksFiltered(tracks);
                    UpdateStatusCount();

                    if (s.LastIndex >= 0 && s.LastIndex < playlist.Count)
                    {
                        currentIndex = s.LastIndex;
                        lstPlaylist.SelectedIndex = currentIndex;
                        _pendingResumePosition = Math.Max(0, s.LastPosition);
                        UpdateQueueLabel();
                    }
                    SetStatusSafe($"Playlist dimuat kembali: {added} lagu");
                }
            }
            catch { }
            finally { SetBusy(false, null); }
        }

        // ==================================================
        // CLEANUP
        // ==================================================
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CleanupAudio();
            uiTimer?.Dispose();
            fontListTitle.Dispose();
            fontListSub.Dispose();
            fontListNum.Dispose();
            fontAppTitle.Dispose();
            appIconBmp?.Dispose();
            appIcon?.Dispose();
            BxIcon.DisposeAll();
            base.OnFormClosed(e);
        }
    }
}