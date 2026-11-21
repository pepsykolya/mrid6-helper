using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Mrid6Helper
{
    /// <summary>Настройки приложения (простой key=value в %APPDATA%\Mrid6Helper\settings.ini)</summary>
    sealed class Cfg
    {
        public bool CustomPower;
        public int Spl = 101, Sppt = 105, Wall = 99;           // пределы вендора: SPL 95..105, SPPT 105..120, стенка 89..100
        public bool FanFixed;
        public int FanPct = 60;
        public bool Osd;
        public bool CloseToTray = true;
        public bool NoSave;                    // самотест: не писать настройки
        public bool HasFan;                    // в файле настроек есть сохранённый выбор по вентиляторам

        static string PathFile()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mrid6Helper");
            Directory.CreateDirectory(d);
            return Path.Combine(d, "settings.ini");
        }

        public static Cfg Load()
        {
            Cfg c = new Cfg();
            try
            {
                foreach (string line in File.ReadAllLines(PathFile()))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    int n; bool b = v == "1" || v.ToLowerInvariant() == "true";
                    switch (k)
                    {
                        case "CustomPower": c.CustomPower = b; break;
                        case "Spl": if (int.TryParse(v, out n)) c.Spl = n; break;
                        case "Sppt": if (int.TryParse(v, out n)) c.Sppt = n; break;
                        case "Wall": if (int.TryParse(v, out n)) c.Wall = n; break;
                        case "FanFixed": c.FanFixed = b; c.HasFan = true; break;
                        case "FanPct": if (int.TryParse(v, out n)) c.FanPct = n; break;
                        case "Osd": c.Osd = b; break;
                        case "CloseToTray": c.CloseToTray = b; break;
                    }
                }
            }
            catch { }
            c.Spl = Clamp(c.Spl, 95, 105); c.Sppt = Clamp(c.Sppt, 105, 120); c.Wall = Clamp(c.Wall, 89, 100);
            c.FanPct = Clamp(c.FanPct, 30, 100);
            return c;
        }

        public void Save()
        {
            if (NoSave) return;
            try
            {
                List<string> lines = new List<string> { "CustomPower=" + (CustomPower ? 1 : 0), "Spl=" + Spl, "Sppt=" + Sppt, "Wall=" + Wall,
                    "Osd=" + (Osd ? 1 : 0), "CloseToTray=" + (CloseToTray ? 1 : 0) };
                if (HasFan) { lines.Add("FanFixed=" + (FanFixed ? 1 : 0)); lines.Add("FanPct=" + FanPct); }   // только явный выбор пользователя
                File.WriteAllText(PathFile(), string.Join(Environment.NewLine, lines.ToArray()));
            }
            catch { }
        }

        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }

    static class Log
    {
        static readonly object L = new object();
        public static void Write(string m)
        {
            try
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mrid6Helper");
                Directory.CreateDirectory(d);
                string f = Path.Combine(d, "app.log");
                lock (L)
                {
                    try { if (File.Exists(f) && new FileInfo(f).Length > 256 * 1024) File.Delete(f); } catch { }
                    File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + m + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    sealed class Snap
    {
        public int CpuEc = -1, CpuAcpi = -1, GpuT = -1, GpuW = -1, GpuClk = -1, GpuUtil = -1, FanC = -1, FanG = -1, CpuLoad = -1;
        public int Mode = -1, FnLock = -1, Tp = -1, Amb = -1, KbMode = -1, KbBright = -1;   // состояния из платы (синхронизация с Fn-клавишами)
        public bool HasStates;
        public int CpuT { get { return CpuEc > 0 ? CpuEc : CpuAcpi; } }
    }

    /// <summary>Переключатель со своей отрисовкой: галочка хорошо видна на тёмной теме.</summary>
    sealed class ToggleBox : CheckBox
    {
        static readonly Color Back = Color.FromArgb(22, 25, 31), Off = Color.FromArgb(32, 36, 45), Edge = Color.FromArgb(100, 108, 124), On = Color.FromArgb(0, 168, 232);
        bool _hover;
        public ToggleBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }
        public override Size GetPreferredSize(Size proposedSize)
        {
            Size s = TextRenderer.MeasureText(Text ?? "", Font);
            return new Size(s.Width + 32, Math.Max(s.Height + 8, 26));
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Back);
            int size = 18, y = (Height - size) / 2;
            Rectangle r = new Rectangle(2, y, size, size);
            using (GraphicsPath gp = Round(r, 4))
            {
                using (SolidBrush b = new SolidBrush(Checked ? On : Off)) g.FillPath(b, gp);
                using (Pen p = new Pen(Checked ? On : (_hover ? Color.FromArgb(0, 168, 232) : Edge), 1.6f)) g.DrawPath(p, gp);
            }
            if (Checked)
                using (Pen p = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(p, new PointF[] { new PointF(r.X + 4.5f, r.Y + 9.5f), new PointF(r.X + 7.7f, r.Y + 12.7f), new PointF(r.X + 13.5f, r.Y + 5.5f) });
            if (Focused && ShowFocusCues)
                using (Pen p = new Pen(Color.FromArgb(120, 0, 168, 232)) { DashStyle = DashStyle.Dot }) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(g, Text ?? "", Font, new Rectangle(28, 0, Width - 28, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath(); int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure();
            return p;
        }
    }

    /// <summary>Лёгкий OSD в нижней части экрана (по желанию).</summary>
    sealed class Osd : Form
    {
        readonly Label _l = new Label();
        readonly System.Windows.Forms.Timer _t = new System.Windows.Forms.Timer();
        public Osd()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(24, 28, 36); Size = new Size(340, 64); Opacity = 0.92;
            _l.Dock = DockStyle.Fill; _l.ForeColor = Color.White; _l.Font = new Font("Segoe UI Semibold", 15f); _l.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(_l);
            _t.Interval = 1400; _t.Tick += delegate { _t.Stop(); Hide(); };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { CreateParams p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
        public void Flash(string text)
        {
            _l.Text = text;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - 90);
            if (!Visible) Show();
            _t.Stop(); _t.Start();
        }
    }

    sealed class MainForm : Form
    {
        static readonly Color Bg = Color.FromArgb(22, 25, 31), Panel2 = Color.FromArgb(32, 36, 45), Fg = Color.FromArgb(230, 233, 238),
            Dim = Color.FromArgb(140, 148, 160), Accent = Color.FromArgb(0, 168, 232);

        readonly Cfg _cfg = Cfg.Load();
        readonly NotifyIcon _tray = new NotifyIcon();
        readonly Osd _osd = new Osd();
        readonly FnEvents _fn = new FnEvents();
        readonly PerformanceCounter _cpuLoad;
        Thread _poll;
        volatile bool _quit;
        bool _loading = true;
        bool _boosted;                       // защита: временно 100% оборотов
        bool _powerOk;
        int _quiet;                          // до этого момента (TickCount) опрос не трогает элементы: пользователь только что действовал
        void Touch() { _quiet = Environment.TickCount + 2500; }
        bool _selfTest;                      // идёт самотест: опрос не трогает элементы
        int _mode = -1;                      // текущий режим из платы (0 баланс, 1 производит., 2 тихий)
        int _hotTicks;
        Icon _lastIcon;

        // датчики
        readonly Label lCpu = new Label(), lGpu = new Label(), lFanC = new Label(), lFanG = new Label(), lGpuW = new Label(), lGpuClk = new Label(), lCpuLoad = new Label(), lGpuUtil = new Label();
        readonly Panel chart = new DoubleBufferedPanel();
        readonly List<int> hCpu = new List<int>(), hGpu = new List<int>();
        // режимы
        readonly RadioButton rQuiet = new RadioButton(), rBal = new RadioButton(), rPerf = new RadioButton();
        // питание
        readonly CheckBox cPower = new ToggleBox();
        readonly TrackBar tSpl = new TrackBar(), tSppt = new TrackBar(), tWall = new TrackBar();
        readonly Label vSpl = new Label(), vSppt = new Label(), vWall = new Label();
        // вентиляторы
        readonly CheckBox cFan = new ToggleBox();
        readonly TrackBar tFan = new TrackBar();
        readonly Label vFan = new Label();
        // клавиатура
        readonly ComboBox cbKb = new ComboBox();
        readonly TrackBar tBright = new TrackBar();
        readonly Button bColor = new Button();
        readonly CheckBox cFnLock = new ToggleBox(), cTp = new ToggleBox(), cAmb = new ToggleBox();
        // система
        readonly ComboBox cbGpu = new ComboBox();
        readonly CheckBox cOsd = new ToggleBox(), cTrayClose = new ToggleBox(), cAuto = new ToggleBox();
        readonly Label status = new Label();
        Color _kbColor = Color.White;

        sealed class DoubleBufferedPanel : Panel { public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; } }

        public MainForm()
        {
            Text = "MRID6 Helper " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + " — MECHREVO Jiaolong";
            Icon = SystemIcons.Application;
            BackColor = Bg; ForeColor = Fg; Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 760);
            MinimumSize = new Size(460, 500);
            StartPosition = FormStartPosition.CenterScreen;

            try { _cpuLoad = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuLoad.NextValue(); } catch { }

            BuildUi();
            BuildTray();
            Nvml.Init();

            HandleCreated += delegate { SyncFromHardware(); ApplyStartup(); };
            FormClosing += OnClosing;
            SystemEvents.PowerModeChanged += OnPower;

            _fn.Arrived += OnFn;
            _fnStarted = _fn.Start();
            Log.Write("fn watcher: " + (_fnStarted ? "started" : "FAILED"));

            _poll = new Thread(PollLoop) { IsBackground = true, Name = "poll" };
            _poll.Start();
        }

        // ------------------------------------------------------------------ UI
        const int LabelW = 150;

        void BuildUi()
        {
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10, 8, 10, 8) };
            status.Dock = DockStyle.Bottom; status.Height = 26; status.ForeColor = Dim; status.Padding = new Padding(10, 5, 0, 0);
            Controls.Add(scroll); Controls.Add(status);

            FlowLayoutPanel flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Top, BackColor = Bg };
            scroll.Controls.Add(flow);

            // --- Датчики ---
            TableLayoutPanel b1; GroupBox g1 = Group("Датчики", out b1);
            TableLayoutPanel t1 = Grid(4, 128, 88, 128, 0);
            KV(t1, "CPU", lCpu, "GPU", lGpu);
            KV(t1, "Кулер CPU, об/мин", lFanC, "Кулер GPU, об/мин", lFanG);
            KV(t1, "Загрузка CPU", lCpuLoad, "Загрузка GPU", lGpuUtil);
            KV(t1, "GPU мощность", lGpuW, "GPU частота", lGpuClk);
            Row(b1, t1);
            chart.Height = 96; chart.MinimumSize = new Size(0, 96); chart.BackColor = Panel2; chart.Paint += PaintChart;
            Row(b1, chart);
            Fit(g1, b1, 22); flow.Controls.Add(g1);

            // --- Режим ---
            TableLayoutPanel b2; GroupBox g2 = Group("Режим производительности  (Fn+F10)", out b2);
            TableLayoutPanel t2 = Grid(3, 0, 0, 0);
            StyleMode(rQuiet, "Тихий"); StyleMode(rBal, "Баланс"); StyleMode(rPerf, "Производит.");
            t2.RowCount = 1; t2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t2.Controls.Add(rQuiet, 0, 0); t2.Controls.Add(rBal, 1, 0); t2.Controls.Add(rPerf, 2, 0);
            rQuiet.CheckedChanged += delegate { if (rQuiet.Checked) SetMode(2); };
            rBal.CheckedChanged += delegate { if (rBal.Checked) SetMode(0); };
            rPerf.CheckedChanged += delegate { if (rPerf.Checked) SetMode(1); };
            Row(b2, t2);
            Fit(g2, b2, 4); flow.Controls.Add(g2);

            // --- Лимиты питания ---
            TableLayoutPanel b3; GroupBox g3 = Group("Лимиты питания CPU", out b3);
            Flat(cPower); cPower.Text = "Использовать свои лимиты";
            Row(b3, cPower);
            TableLayoutPanel t3 = Grid(3, LabelW, 0, 52);
            Slider(t3, "SPL, Вт", tSpl, vSpl, 95, 105);
            Slider(t3, "SPPT, Вт", tSppt, vSppt, 105, 120);
            Slider(t3, "Температурная стенка, °C", tWall, vWall, 89, 100);
            Row(b3, t3);
            Button bApply = new Button { Text = "Применить", Height = 32 }; StyleBtn(bApply);
            bApply.Click += delegate { ApplyPower(true); };
            cPower.CheckedChanged += delegate { if (!_loading) ApplyPower(true); };
            Row(b3, bApply);
            Row(b3, Hint("Диапазоны — как в штатной программе вендора. Лимиты сбрасываются при смене режима и сне — приложение применяет их заново автоматически."));
            Fit(g3, b3, 28); flow.Controls.Add(g3);

            // --- Вентиляторы ---
            TableLayoutPanel b4; GroupBox g4 = Group("Вентиляторы", out b4);
            Flat(cFan); cFan.Text = "Фиксированные обороты (вместо автоматики)";
            Row(b4, cFan);
            TableLayoutPanel t4 = Grid(3, LabelW, 0, 52);
            Slider(t4, "Обороты, %", tFan, vFan, 30, 100);
            Row(b4, t4);
            Row(b4, Hint("100% ≈ 5000 об/мин, выше ~60% обороты почти не растут. Защита: при CPU ≥ 90 °C или GPU ≥ 88 °C обороты автоматически поднимутся до 100% и вернутся после остывания."));
            cFan.CheckedChanged += delegate { if (!_loading) ApplyFan(); };
            tFan.MouseUp += delegate { if (!_loading) ApplyFan(); };
            tFan.KeyUp += delegate { if (!_loading) ApplyFan(); };
            Fit(g4, b4, 40); flow.Controls.Add(g4);

            // --- Клавиатура ---
            TableLayoutPanel b5; GroupBox g5 = Group("Клавиатура и светодиоды  (Fn+F9)", out b5);
            TableLayoutPanel t5 = Grid(2, LabelW, 0);
            cbKb.DropDownStyle = ComboBoxStyle.DropDownList; cbKb.FlatStyle = FlatStyle.Flat; cbKb.BackColor = Panel2; cbKb.ForeColor = Fg;
            cbKb.Items.AddRange(new object[] { "Выкл (яркость 0)", "Радуга", "Один цвет" });
            cbKb.SelectedIndexChanged += delegate { if (!_loading && cbKb.SelectedIndex >= 0) SetKbMode(cbKb.SelectedIndex); };
            Pair(t5, "Режим подсветки", cbKb);
            tBright.Minimum = 0; tBright.Maximum = 3; tBright.TickFrequency = 1; tBright.AutoSize = false; tBright.Height = 34; tBright.BackColor = Bg;
            tBright.ValueChanged += delegate
            {
                if (_loading) return;
                Touch(); Mi.Set(Mi.M_KBBRIGHT, (byte)tBright.Value);
                bool l = _loading; _loading = true; cbKb.SelectedIndex = KbIndex(Mi.GetByte(Mi.M_KBMODE), tBright.Value); _loading = l;
            };
            Pair(t5, "Яркость (0–3)", tBright);
            bColor.Text = "Выбрать цвет…"; StyleBtn(bColor); bColor.Height = 30;
            bColor.Click += delegate { PickColor(); };
            Pair(t5, "Цвет", bColor);
            Row(b5, t5);
            Flat(cFnLock); cFnLock.Text = "Fn-lock (мультимедиа-ряд без Fn)";
            Flat(cTp); cTp.Text = "Блокировка тачпада";
            Flat(cAmb); cAmb.Text = "Подсветка по периметру (Ambient)";
            cFnLock.CheckedChanged += delegate { if (!_loading) { Touch(); Mi.Set(Mi.M_FNLOCK, (byte)(cFnLock.Checked ? 1 : 0)); } };
            cTp.CheckedChanged += delegate { if (!_loading) { Touch(); Mi.Set(Mi.M_TPLOCK, (byte)(cTp.Checked ? 1 : 0)); } };
            cAmb.CheckedChanged += delegate { if (!_loading) { Touch(); Mi.Set(Mi.M_AMBIENT, (byte)(cAmb.Checked ? 1 : 0)); } };
            Row(b5, cFnLock); Row(b5, cTp); Row(b5, cAmb);
            Fit(g5, b5, 6); flow.Controls.Add(g5);

            // --- Система ---
            TableLayoutPanel b6; GroupBox g6 = Group("Система", out b6);
            TableLayoutPanel t6 = Grid(3, 110, 0, 120);
            cbGpu.DropDownStyle = ComboBoxStyle.DropDownList; cbGpu.FlatStyle = FlatStyle.Flat; cbGpu.BackColor = Panel2; cbGpu.ForeColor = Fg;
            cbGpu.Items.AddRange(new object[] { "Гибридный", "Только дискретная" });
            Button bGpu = new Button { Text = "Применить", Height = 28 }; StyleBtn(bGpu); bGpu.Click += delegate { ApplyGpuMode(); };
            t6.RowCount = 1; t6.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t6.Controls.Add(Lbl("Режим GPU"), 0, 0);
            cbGpu.Dock = DockStyle.Fill; cbGpu.Margin = new Padding(3, 4, 3, 4); bGpu.Dock = DockStyle.Fill;
            t6.Controls.Add(cbGpu, 1, 0); t6.Controls.Add(bGpu, 2, 0);
            Row(b6, t6);
            Row(b6, Hint("Переключение режима GPU вступает в силу после перезагрузки."));
            Flat(cOsd); cOsd.Text = "Всплывающая подсказка при Fn-клавишах";
            Flat(cTrayClose); cTrayClose.Text = "Закрытие окна сворачивает в трей";
            Flat(cAuto); cAuto.Text = "Запускать при входе в Windows (с правами администратора)";
            cOsd.CheckedChanged += delegate { if (!_loading) { _cfg.Osd = cOsd.Checked; _cfg.Save(); } };
            cTrayClose.CheckedChanged += delegate { if (!_loading) { _cfg.CloseToTray = cTrayClose.Checked; _cfg.Save(); } };
            cAuto.CheckedChanged += delegate { if (!_loading) SetAutostart(cAuto.Checked); };
            Row(b6, cOsd); Row(b6, cTrayClose); Row(b6, cAuto);
            Button bDiag = new Button { Text = "Диагностика (сохранить отчёт на рабочий стол)", Height = 30 }; StyleBtn(bDiag);
            bDiag.Click += delegate { RunDiag(); };
            Row(b6, bDiag);
            Fit(g6, b6, 6); flow.Controls.Add(g6);
        }

        // ---- помощники раскладки (авторазмер: ничего не режется при любом DPI)
        const int GroupW = 456;

        GroupBox Group(string title, out TableLayoutPanel body)
        {
            GroupBox g = new GroupBox { Text = title, Width = GroupW, ForeColor = Accent, BackColor = Bg, AutoSize = false, Padding = new Padding(8, 6, 8, 8), Margin = new Padding(0, 0, 0, 10) };
            body = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = Color.Transparent, ForeColor = Fg, Margin = new Padding(0) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            g.Controls.Add(body);
            return g;
        }
        readonly List<GroupBox> _fitG = new List<GroupBox>();
        readonly List<TableLayoutPanel> _fitB = new List<TableLayoutPanel>();
        void FitAll()
        {
            for (int i = 0; i < _fitG.Count; i++)
            {
                _fitB[i].PerformLayout();
                _fitG[i].Height = _fitB[i].Height + _fitG[i].Padding.Vertical + 26;
            }
        }
        void Fit(GroupBox g, TableLayoutPanel body, int slack)
        {
            _fitG.Add(g); _fitB.Add(body);
            int w = GroupW - g.Padding.Horizontal - 2;
            body.Width = w;
            Size p = body.GetPreferredSize(new Size(w, 0));
            g.Height = p.Height + g.Padding.Vertical + 24 + slack;
        }
        static void Row(TableLayoutPanel t, Control c)
        {
            c.Dock = DockStyle.Fill; c.Margin = new Padding(0, 3, 0, 3);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowCount = t.RowStyles.Count;
            t.Controls.Add(c, 0, t.RowCount - 1);
        }
        static TableLayoutPanel Grid(int cols, params int[] widths)
        {
            TableLayoutPanel t = new TableLayoutPanel { ColumnCount = cols, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, ForeColor = Fg, Margin = new Padding(0) };
            for (int i = 0; i < cols; i++) t.ColumnStyles.Add(widths[i] > 0 ? new ColumnStyle(SizeType.Absolute, widths[i]) : new ColumnStyle(SizeType.Percent, 100));
            return t;
        }
        static Label Lbl(string text)
        {
            return new Label { Text = text, ForeColor = Dim, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 6, 6), MaximumSize = new Size(LabelW + 10, 0) };
        }
        static void KV(TableLayoutPanel t, string n1, Label v1, string n2, Label v2)
        {
            int row = t.RowStyles.Count; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowCount = t.RowStyles.Count;
            Label a = Lbl(n1), b = Lbl(n2); a.MaximumSize = b.MaximumSize = new Size(0, 0);
            foreach (Label v in new Label[] { v1, v2 }) { v.AutoSize = true; v.Anchor = AnchorStyles.Left; v.Text = "—"; v.Font = new Font("Segoe UI Semibold", 10f); v.ForeColor = Fg; v.Margin = new Padding(0, 3, 0, 3); }
            t.Controls.Add(a, 0, row); t.Controls.Add(v1, 1, row); t.Controls.Add(b, 2, row); t.Controls.Add(v2, 3, row);
        }
        static void Pair(TableLayoutPanel t, string name, Control c)
        {
            int row = t.RowStyles.Count; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowCount = t.RowStyles.Count;
            c.Dock = DockStyle.Fill; c.Margin = new Padding(3, 3, 3, 3);
            t.Controls.Add(Lbl(name), 0, row); t.Controls.Add(c, 1, row);
        }
        static void Slider(TableLayoutPanel t, string name, TrackBar tb, Label val, int min, int max)
        {
            int row = t.RowStyles.Count; t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); t.RowCount = t.RowStyles.Count;
            tb.Minimum = min; tb.Maximum = max; tb.TickFrequency = Math.Max(1, (max - min) / 10); tb.SmallChange = 1; tb.LargeChange = 5; tb.Value = min;
            tb.AutoSize = false; tb.Height = 34; tb.Dock = DockStyle.Fill; tb.Margin = new Padding(0); tb.BackColor = Bg;
            val.AutoSize = false; val.Dock = DockStyle.Fill; val.TextAlign = ContentAlignment.MiddleRight; val.ForeColor = Fg; val.Text = min.ToString(); val.Margin = new Padding(0, 0, 4, 0);
            tb.ValueChanged += delegate { val.Text = tb.Value.ToString(); };
            t.Controls.Add(Lbl(name), 0, row); t.Controls.Add(tb, 1, row); t.Controls.Add(val, 2, row);
        }
        static void Flat(CheckBox c) { c.ForeColor = Fg; c.FlatStyle = FlatStyle.Flat; c.AutoSize = true; c.Padding = new Padding(2, 2, 2, 2); }
        static void StyleBtn(Button b) { b.FlatStyle = FlatStyle.Flat; b.BackColor = Panel2; b.ForeColor = Fg; b.FlatAppearance.BorderColor = Color.FromArgb(60, 66, 80); b.Cursor = Cursors.Hand; }
        static void StyleMode(RadioButton r, string text)
        {
            r.Text = text; r.Appearance = Appearance.Button; r.FlatStyle = FlatStyle.Flat; r.Dock = DockStyle.Fill; r.TextAlign = ContentAlignment.MiddleCenter; r.Height = 38; r.AutoSize = false;
            r.BackColor = Panel2; r.ForeColor = Fg; r.FlatAppearance.BorderColor = Color.FromArgb(60, 66, 80); r.FlatAppearance.CheckedBackColor = Color.FromArgb(0, 110, 160); r.Cursor = Cursors.Hand; r.Margin = new Padding(0, 0, 6, 0);
        }
        static Label Hint(string text) { return new Label { Text = text, ForeColor = Dim, AutoSize = true, MaximumSize = new Size(GroupW - 40, 0), Font = new Font("Segoe UI", 8f) }; }

        // ------------------------------------------------------------------ трей
        void BuildTray()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Открыть", null, delegate { ShowMain(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Тихий", null, delegate { SetMode(2); SyncMode(2); });
            m.Items.Add("Баланс", null, delegate { SetMode(0); SyncMode(0); });
            m.Items.Add("Производительность", null, delegate { SetMode(1); SyncMode(1); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Выход", null, delegate { _quit = true; Close(); });
            _tray.ContextMenuStrip = m;
            _tray.Visible = true;
            _tray.Text = "MRID6 Helper";
            _tray.Icon = MakeIcon(-1, Color.Gray);
            _tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMain(); };
        }


        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitAll();
            if (SelfTestPath != null) { _selfTest = true; _cfg.NoSave = true; new Thread(RunSelfTest) { IsBackground = true, Name = "selftest" }.Start(); return; }
            if (ShotPath == null) return;
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 4000 };
            t.Tick += delegate
            {
                t.Stop();
                try
                {
                    ClientSize = new Size(ClientSize.Width, 1560);
                    Application.DoEvents();
                    using (Bitmap bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(ShotPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex) { Log.Write("shot: " + ex.Message); }
                _quit = true; Close();
            };
            t.Start();
        }

        public void ShowMain()
        {
            Show(); WindowState = FormWindowState.Normal; Activate(); BringToFront();
        }

        public bool StartHidden;
        public string SelfTestPath;             // служебное: самотест (--selftest файл)
        bool _fnStarted;
        public string ShotPath;                 // служебное: сохранить снимок окна и выйти (--shot файл)
        bool _firstShow = true;
        protected override void SetVisibleCore(bool value)
        {
            if (_firstShow)
            {
                _firstShow = false;
                if (StartHidden) { if (!IsHandleCreated) CreateHandle(); value = false; }
            }
            base.SetVisibleCore(value);
        }

        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        /// <summary>Цвет иконки в трее по режиму: тихий — синий, баланс — зелёный, производительность — оранжевый.</summary>
        public static Color ModeColor(int mode)
        {
            switch (mode)
            {
                case 2: return Color.FromArgb(0, 112, 192);
                case 0: return Color.FromArgb(0, 128, 84);
                case 1: return Color.FromArgb(240, 130, 0);
                default: return Color.FromArgb(110, 116, 128);
            }
        }

        Icon MakeIcon(int temp, Color c) { return MakeIcon(temp, c, false); }
        Icon MakeIcon(int temp, Color c, bool hot)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, 0, 0, 32, 32);
                if (hot) using (Pen p = new Pen(Color.Red, 5f)) g.DrawRectangle(p, 2, 2, 28, 28);   // перегрев — красная рамка, цвет режима остаётся
                string s = temp > 0 ? temp.ToString() : "--";
                using (Font f = new Font("Segoe UI", s.Length > 2 ? 15f : 19f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    SizeF sz = g.MeasureString(s, f);
                    g.DrawString(s, f, Brushes.White, (32 - sz.Width) / 2f, (32 - sz.Height) / 2f);
                }
                IntPtr h = bmp.GetHicon();
                Icon tmp = Icon.FromHandle(h);
                Icon ic = (Icon)tmp.Clone();
                DestroyIcon(h);
                return ic;
            }
        }

        // ------------------------------------------------------------------ синхронизация с железом
        void SyncFromHardware()
        {
            _loading = true;
            try
            {
                int perf = Mi.GetByte(Mi.M_PERF);
                if (perf < 0) { status.Text = "WMI-интерфейс недоступен: " + Mi.LastError + "  (запустите приложение от имени администратора)"; status.ForeColor = Color.Tomato; }
                else { status.Text = "Интерфейс платы подключён (MICommonInterface)"; status.ForeColor = Color.MediumSeaGreen; }
                SyncMode(perf);
                int v;
                int kbm = Mi.GetByte(Mi.M_KBMODE), kbb = Mi.GetByte(Mi.M_KBBRIGHT);
                if (kbb >= 0) tBright.Value = Cfg.Clamp(kbb, 0, 3);
                if (kbm >= 0 && kbb >= 0) cbKb.SelectedIndex = KbIndex(kbm, kbb);
                byte r, g, b; if (Mi.GetColor(out r, out g, out b)) { _kbColor = Color.FromArgb(r, g, b); bColor.BackColor = _kbColor; bColor.ForeColor = (r + g + b) > 380 ? Color.Black : Color.White; }
                v = Mi.GetByte(Mi.M_FNLOCK); if (v >= 0) cFnLock.Checked = v == 1;
                v = Mi.GetByte(Mi.M_TPLOCK); if (v >= 0) cTp.Checked = v == 1;
                v = Mi.GetByte(Mi.M_AMBIENT); if (v >= 0) cAmb.Checked = v == 1;
                v = Mi.GetByte(Mi.M_GPUMODE); if (v >= 0 && v < cbGpu.Items.Count) cbGpu.SelectedIndex = v;

                cPower.Checked = _cfg.CustomPower; tSpl.Value = _cfg.Spl; tSppt.Value = _cfg.Sppt; tWall.Value = _cfg.Wall;
                // вентиляторы: если пользователь не сохранял выбор — берём реальное состояние железа
                if (!_cfg.HasFan)
                {
                    int sw = Mi.GetByte(Mi.M_FANFIXSW), pct = Mi.GetByte(Mi.M_FANFIX);
                    _cfg.FanFixed = sw == 1; if (pct > 0) _cfg.FanPct = Cfg.Clamp(pct, 30, 100);
                    Log.Write("fan hw state: fixed=" + sw + " pct=" + pct);
                }
                cFan.Checked = _cfg.FanFixed; tFan.Value = _cfg.FanPct;
                cOsd.Checked = _cfg.Osd; cTrayClose.Checked = _cfg.CloseToTray; cAuto.Checked = AutostartExists();
            }
            finally { _loading = false; }
        }

        void SyncMode(int perf)
        {
            bool l = _loading; _loading = true;
            if (perf >= 0) _mode = perf;
            rQuiet.Checked = perf == 2; rBal.Checked = perf == 0; rPerf.Checked = perf == 1;
            _loading = l;
        }

        void ApplyStartup()
        {
            if (_cfg.CustomPower) ApplyPower(false);
            if (_cfg.HasFan && _cfg.FanFixed) ApplyFan();
        }

        // ------------------------------------------------------------------ действия
        void SetMode(int mode)
        {
            if (_loading) return;
            Touch();
            _mode = mode;
            Mi.SetPerfMode(mode);
            if (_cfg.CustomPower) DelayedReapply();
        }

        System.Windows.Forms.Timer _reapplyT;
        void DelayedReapply()
        {
            if (_reapplyT == null) { _reapplyT = new System.Windows.Forms.Timer { Interval = 1500 }; _reapplyT.Tick += delegate { _reapplyT.Stop(); ApplyPower(false); }; }
            _reapplyT.Stop(); _reapplyT.Start();
        }

        void ApplyPower(bool save)
        {
            _cfg.CustomPower = cPower.Checked; _cfg.Spl = tSpl.Value; _cfg.Sppt = tSppt.Value; _cfg.Wall = tWall.Value;
            if (save) _cfg.Save();
            bool ok = Mi.SetCustomPower(_cfg.CustomPower, _cfg.Wall, _cfg.Spl, _cfg.Sppt);
            _powerOk = ok;
            status.ForeColor = ok ? Color.MediumSeaGreen : Color.Tomato;
            status.Text = ok ? (_cfg.CustomPower ? string.Format("Лимиты применены: SPL {0} Вт, SPPT {1} Вт, стенка {2} °C", _cfg.Spl, _cfg.Sppt, _cfg.Wall) : "Свои лимиты выключены") : "Не удалось применить лимиты: " + Mi.LastError;
        }

        void ApplyFan()
        {
            Touch();
            _cfg.FanFixed = cFan.Checked; _cfg.FanPct = tFan.Value; _cfg.HasFan = true; _cfg.Save();
            if (_cfg.FanFixed)
            {
                Mi.Set(Mi.M_FANFIX, (byte)(_boosted ? 100 : _cfg.FanPct));
                Mi.Set(Mi.M_FANFIXSW, 1);
            }
            else { _boosted = false; Mi.Set(Mi.M_FANFIXSW, 0); }
        }

        /// <summary>Индекс списка подсветки: 0 «выкл» (яркость 0), 1 радуга, 2 один цвет.</summary>
        static int KbIndex(int mode, int bright) { return bright == 0 ? 0 : (mode == 1 ? 1 : 2); }

        /// <summary>Плата принимает только режимы 1 (радуга) и 2 (один цвет); «выкл» — это яркость 0.
        /// При яркости 0 любой режим невидим — поднимаем яркость до максимума.</summary>
        void SetKbMode(int index)
        {
            Touch();
            string note = ""; bool ok;
            if (index == 0)
            {
                ok = Mi.Set(Mi.M_KBBRIGHT, 0);
                bool l = _loading; _loading = true; tBright.Value = 0; _loading = l;
            }
            else
            {
                ok = Mi.Set(Mi.M_KBMODE, (byte)index);
                if (ok && Mi.GetByte(Mi.M_KBMODE) != index) { ok = false; Mi.LastError = "плата не приняла режим " + index; }
                if (ok && Mi.GetByte(Mi.M_KBBRIGHT) == 0)
                {
                    Mi.Set(Mi.M_KBBRIGHT, 3);
                    bool l = _loading; _loading = true; tBright.Value = 3; _loading = l;
                    note = " (яркость была 0 — поднята до 3)";
                }
            }
            status.ForeColor = ok ? Color.MediumSeaGreen : Color.Tomato;
            status.Text = ok ? "Подсветка: " + cbKb.Items[index] + note : "Не удалось сменить режим подсветки: " + Mi.LastError;
        }

        void PickColor()
        {
            using (ColorDialog d = new ColorDialog { Color = _kbColor, FullOpen = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                _kbColor = d.Color; bColor.BackColor = _kbColor; bColor.ForeColor = (_kbColor.R + _kbColor.G + _kbColor.B) > 380 ? Color.Black : Color.White;
                Color cc = _kbColor;
                ThreadPool.QueueUserWorkItem(delegate { Mi.SetKbColor(cc.R, cc.G, cc.B); });   // пауза 500 мс между режимом и цветом — как у вендора
                bool l = _loading; _loading = true; cbKb.SelectedIndex = 2; _loading = l;
                if (Mi.GetByte(Mi.M_KBBRIGHT) == 0) { Mi.Set(Mi.M_KBBRIGHT, 3); tBright.Value = 3; }
            }
        }

        void ApplyGpuMode()
        {
            if (cbGpu.SelectedIndex < 0) return;
            if (MessageBox.Show(this, "Переключение режима GPU вступит в силу после перезагрузки. Продолжить?", "Режим GPU", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            bool ok = Mi.Set(Mi.M_GPUMODE, (byte)cbGpu.SelectedIndex);
            status.ForeColor = ok ? Color.MediumSeaGreen : Color.Tomato;
            status.Text = ok ? "Режим GPU записан — перезагрузите ноутбук" : "Не удалось записать режим GPU: " + Mi.LastError;
        }

        // ------------------------------------------------------------------ автозапуск
        const string TaskName = "Mrid6Helper";
        static bool AutostartExists()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Query /TN " + TaskName) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process p = Process.Start(psi)) { p.WaitForExit(4000); return p.ExitCode == 0; }
            }
            catch { return false; }
        }
        void SetAutostart(bool on)
        {
            try
            {
                string exe = Application.ExecutablePath;
                string args = on ? "/Create /TN " + TaskName + " /TR \"\\\"" + exe + "\\\" --tray\" /SC ONLOGON /RL HIGHEST /F" : "/Delete /TN " + TaskName + " /F";
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process p = Process.Start(psi)) { p.WaitForExit(8000); if (p.ExitCode != 0) status.Text = "Автозапуск: ошибка " + p.ExitCode; }
            }
            catch (Exception ex) { status.Text = "Автозапуск: " + ex.Message; }
        }

        void RunDiag()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "mrid6-diag.txt");
                File.WriteAllText(path, Probe.Report(), Encoding.UTF8);
                Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { status.Text = "Диагностика: " + ex.Message; }
        }

        // ------------------------------------------------------------------ события Fn
        void OnFn(int name, int val, int type)
        {
            Log.Write(string.Format("fn event: type={0} name={1} value={2}", type, name, val));
            try
            {
                BeginInvoke((Action)delegate
                {
                    string msg = null;
                    switch (name)
                    {
                        case FnEvents.EV_PERF:
                            SyncMode(val);
                            msg = "Режим: " + (val == 2 ? "Тихий" : val == 1 ? "Производительность" : "Баланс");
                            if (_cfg.CustomPower) DelayedReapply();
                            break;
                        case FnEvents.EV_KBBRIGHT:
                            { bool l = _loading; _loading = true; tBright.Value = Cfg.Clamp(val, 0, 3); cbKb.SelectedIndex = KbIndex(Mi.GetByte(Mi.M_KBMODE), val); _loading = l; }
                            msg = "Яркость подсветки: " + val; break;
                        case FnEvents.EV_KBMODE:
                            { bool l = _loading; _loading = true; cbKb.SelectedIndex = KbIndex(val, tBright.Value); _loading = l; }
                            msg = "Подсветка: " + (val == 1 ? "радуга" : val == 2 ? "один цвет" : "режим " + val); break;
                        case FnEvents.EV_TOUCHPAD:
                            msg = "Тачпад: " + (val == 1 ? "включён" : "выключен"); break;
                        case FnEvents.EV_FN: msg = "Fn-lock: " + (val == 1 ? "вкл" : "выкл"); break;
                        case FnEvents.EV_AIRPLANE: msg = "Режим «в самолёте»"; break;
                        case FnEvents.EV_AMBIENT: msg = "Периметр: " + (val == 1 ? "вкл" : "выкл"); break;
                    }
                    if (msg != null && _cfg.Osd) _osd.Flash(msg);
                });
            }
            catch { }
        }

        void OnPower(object s, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                try { BeginInvoke((Action)delegate { DelayedReapply(); if (_cfg.HasFan && _cfg.FanFixed) ApplyFan(); }); } catch { }
            }
        }


        // ------------------------------------------------------------------ самотест
        readonly StringBuilder _tr = new StringBuilder();
        int _pass, _fail;
        void Chk(string name, bool ok, string detail)
        {
            _tr.AppendLine((ok ? "PASS  " : "FAIL  ") + name + (string.IsNullOrEmpty(detail) ? "" : "   [" + detail + "]"));
            if (ok) _pass++; else _fail++;
            try { File.WriteAllText(SelfTestPath, _tr.ToString(), Encoding.UTF8); } catch { }
        }
        void Stage(string name) { Info("режим после этапа «" + name + "»: " + Mi.GetByte(Mi.M_PERF)); }
        void Info(string t) { _tr.AppendLine("INFO  " + t); try { File.WriteAllText(SelfTestPath, _tr.ToString(), Encoding.UTF8); } catch { } }
        void UI(Action a) { Invoke(a); }

        void RunSelfTest()
        {
            try
            {
                Thread.Sleep(3000);
                int oMode = Mi.GetByte(Mi.M_PERF), oFn = Mi.GetByte(Mi.M_FNLOCK), oTp = Mi.GetByte(Mi.M_TPLOCK), oAmb = Mi.GetByte(Mi.M_AMBIENT);
                int oKb = Mi.GetByte(Mi.M_KBMODE), oBr = Mi.GetByte(Mi.M_KBBRIGHT), oSw = Mi.GetByte(Mi.M_FANFIXSW), oPct = Mi.GetByte(Mi.M_FANFIX), oGpu = Mi.GetByte(Mi.M_GPUMODE);
                byte r0, g0, b0; Mi.GetColor(out r0, out g0, out b0);
                bool oAuto = AutostartExists();
                Info(string.Format("исходное: mode={0} fn={1} tp={2} amb={3} kb={4} bright={5} fanSw={6} fanPct={7} gpu={8} rgb={9},{10},{11}", oMode, oFn, oTp, oAmb, oKb, oBr, oSw, oPct, oGpu, r0, g0, b0));

                // --- интерфейс и датчики
                Chk("WMI-интерфейс платы отвечает", oMode >= 0, "mode=" + oMode);
                Chk("слежение за Fn-событиями запущено (HID_EVENT20)", _fnStarted, "");
                string t0 = null, t1 = null, t2 = null, t3 = null, t4 = null;
                UI(delegate { t0 = lCpu.Text; t1 = lGpu.Text; t2 = lFanC.Text; t3 = lFanG.Text; t4 = lCpuLoad.Text; });
                Chk("датчик CPU", t0 != "—", t0); Chk("датчик GPU", t1 != "—", t1);
                Chk("обороты кулера CPU", t2 != "—" && t2 != "0", t2); Chk("обороты кулера GPU", t3 != "—" && t3 != "0", t3); Chk("загрузка CPU", t4 != "—", t4);
                int hc = 0; UI(delegate { hc = hCpu.Count; }); Chk("график накапливает точки", hc > 1, "n=" + hc);

                // --- режимы производительности (через радиокнопки окна)
                RadioButton[] rb = { rBal, rPerf, rQuiet };   // индексы 0,1,2 = значения платы
                for (int m = 2; m >= 0; m--)
                {
                    int mm = m;
                    UI(delegate { rb[mm].Checked = true; });
                    Thread.Sleep(700);
                    int hw = Mi.GetByte(Mi.M_PERF);
                    Chk("режим " + (m == 2 ? "Тихий" : m == 1 ? "Производительность" : "Баланс") + " → плата", hw == m, "hw=" + hw);
                    Icon ic = null; UI(delegate { ic = MakeIcon(50, ModeColor(_mode), false); });
                    Color px = ic.ToBitmap().GetPixel(1, 1), want = ModeColor(m);
                    Chk("  цвет иконки по режиму", Math.Abs(px.R - want.R) < 8 && Math.Abs(px.G - want.G) < 8 && Math.Abs(px.B - want.B) < 8, string.Format("пиксель {0},{1},{2}", px.R, px.G, px.B));
                }
                Thread.Sleep(3500);
                Chk("режим устойчив после опроса", Mi.GetByte(Mi.M_PERF) == 0, "");
                UI(delegate { rb[oMode >= 0 ? oMode : 0].Checked = true; }); Thread.Sleep(500);

                Stage("режимы");
                // --- переключатели
                ToggleCase("Fn-lock", cFnLock, Mi.M_FNLOCK, oFn);
                ToggleCase("Блокировка тачпада", cTp, Mi.M_TPLOCK, oTp);
                ToggleCase("Ambient (периметр)", cAmb, Mi.M_AMBIENT, oAmb);

                Stage("переключатели");
                // --- подсветка
                for (int b = 3; b >= 0; b--) { int bb = b; UI(delegate { tBright.Value = bb; }); Thread.Sleep(350); Chk("яркость подсветки " + b, Mi.GetByte(Mi.M_KBBRIGHT) == b, "hw=" + Mi.GetByte(Mi.M_KBBRIGHT)); }
                UI(delegate { tBright.Value = 0; }); Thread.Sleep(350);
                int kbi = -1; UI(delegate { kbi = cbKb.SelectedIndex; }); Chk("список подсветки показывает «Выкл» при яркости 0", kbi == 0, "index=" + kbi);
                UI(delegate { cbKb.SelectedIndex = 1; }); Thread.Sleep(900);
                Chk("режим подсветки: Радуга → плата", Mi.GetByte(Mi.M_KBMODE) == 1, "hw=" + Mi.GetByte(Mi.M_KBMODE));
                Chk("  при яркости 0 яркость поднялась до 3", Mi.GetByte(Mi.M_KBBRIGHT) == 3, "bright=" + Mi.GetByte(Mi.M_KBBRIGHT));
                Thread.Sleep(2500);
                Chk("  Радуга не слетает после опроса", Mi.GetByte(Mi.M_KBMODE) == 1, "");
                UI(delegate { cbKb.SelectedIndex = 2; }); Thread.Sleep(800);
                Chk("режим подсветки: Один цвет → плата", Mi.GetByte(Mi.M_KBMODE) == 2, "hw=" + Mi.GetByte(Mi.M_KBMODE));
                UI(delegate { cbKb.SelectedIndex = 0; }); Thread.Sleep(800);
                Chk("режим подсветки: Выкл → яркость 0", Mi.GetByte(Mi.M_KBBRIGHT) == 0, "bright=" + Mi.GetByte(Mi.M_KBBRIGHT));
                UI(delegate { cbKb.SelectedIndex = 2; }); Thread.Sleep(800);
                Chk("  включение из «Выкл» поднимает яркость", Mi.GetByte(Mi.M_KBBRIGHT) == 3, "bright=" + Mi.GetByte(Mi.M_KBBRIGHT));
                Mi.SetKbColor(255, 0, 128);
                byte r1, g1, b1; Mi.GetColor(out r1, out g1, out b1);
                Chk("цвет подсветки RGB(255,0,128)", r1 == 255 && g1 == 0 && b1 == 128, string.Format("hw={0},{1},{2}", r1, g1, b1));
                Chk("  режим после записи цвета = «Один цвет»", Mi.GetByte(Mi.M_KBMODE) == 2, "");

                Stage("подсветка");
                // --- вентиляторы (только повышение / насыщенная область >= 60 %)
                UI(delegate { tFan.Value = 80; cFan.Checked = true; ApplyFan(); }); Thread.Sleep(500);
                int sw = Mi.GetByte(Mi.M_FANFIXSW), pc = Mi.GetByte(Mi.M_FANFIX);
                Chk("фиксированные обороты: переключатель", sw == 1, "hw=" + sw);
                Chk("фиксированные обороты: 80 %", pc == 80 || pc == 100, "hw=" + pc + (pc == 100 ? " (защита уже подняла)" : ""));
                Thread.Sleep(7000);
                int cpuT = 0, gpuT = 0; UI(delegate { cpuT = int.Parse(("0" + lCpu.Text.Replace(" °C", "")).Replace("—", "0")); gpuT = int.Parse(("0" + lGpu.Text.Replace(" °C", "")).Replace("—", "0")); });
                int pc2 = Mi.GetByte(Mi.M_FANFIX);
                bool stillWarm = cpuT >= 82 || gpuT >= 80;   // защита отпускает только после остывания (гистерезис)
                Chk("защита от перегрева (100 % пока горячо, гистерезис)", stillWarm ? pc2 == 100 : pc2 == 80, string.Format("cpu={0} gpu={1} fan={2}%", cpuT, gpuT, pc2));
                UI(delegate { cFan.Checked = false; }); Thread.Sleep(500);
                Chk("возврат к автоматике вентиляторов", Mi.GetByte(Mi.M_FANFIXSW) == 0, "");
                UI(delegate { _boosted = false; tFan.Value = Cfg.Clamp(oPct, 30, 100); cFan.Checked = oSw == 1; ApplyFan(); if (oSw != 1) Mi.Set(Mi.M_FANFIXSW, 0); }); Thread.Sleep(400);

                Stage("вентиляторы");
                // --- лимиты питания (запись принимается; обратно не читается)
                UI(delegate { cPower.Checked = true; }); Thread.Sleep(600);
                Chk("свои лимиты: включение (плата приняла 4 записи)", _powerOk && _cfg.CustomPower, "ok=" + _powerOk);
                UI(delegate { cPower.Checked = false; }); Thread.Sleep(600);
                Chk("свои лимиты: выключение", _powerOk && !_cfg.CustomPower, "ok=" + _powerOk);

                Stage("лимиты питания");
                // --- режим GPU (записать и сразу вернуть)
                int tgt = oGpu == 0 ? 1 : 0;
                Mi.Set(Mi.M_GPUMODE, (byte)tgt); Thread.Sleep(300);
                Chk("режим GPU: запись/чтение", Mi.GetByte(Mi.M_GPUMODE) == tgt, "hw=" + Mi.GetByte(Mi.M_GPUMODE));
                Mi.Set(Mi.M_GPUMODE, (byte)oGpu); Thread.Sleep(300);
                Chk("режим GPU: возвращён", Mi.GetByte(Mi.M_GPUMODE) == oGpu, "hw=" + Mi.GetByte(Mi.M_GPUMODE));

                Stage("режим GPU");
                // --- автозапуск, OSD, трей
                UI(delegate { cAuto.Checked = true; }); Thread.Sleep(1500);
                Chk("автозапуск: создание задачи", AutostartExists(), "");
                UI(delegate { cAuto.Checked = false; }); Thread.Sleep(1500);
                Chk("автозапуск: удаление задачи", !AutostartExists(), "");
                if (oAuto) { UI(delegate { cAuto.Checked = true; }); Thread.Sleep(1200); }
                bool osdOk = true; try { UI(delegate { _osd.Flash("Самотест: OSD"); }); } catch { osdOk = false; }
                Chk("всплывающая подсказка (OSD)", osdOk, "");
                Chk("иконка в трее создана", _tray.Visible, "");

                Stage("автозапуск/OSD");
                // --- возврат исходного состояния
                UI(delegate { cbKb.SelectedIndex = oKb >= 0 ? oKb : 2; }); Thread.Sleep(700);
                Mi.SetKbColor(r0, g0, b0);
                Mi.Set(Mi.M_KBMODE, (byte)(oKb >= 0 ? oKb : 2));
                UI(delegate { tBright.Value = Cfg.Clamp(oBr, 0, 3); });
                Thread.Sleep(400);
                Info(string.Format("возвращено: kb={0} bright={1} rgb={2} mode={3} fanSw={4} fanPct={5}", Mi.GetByte(Mi.M_KBMODE), Mi.GetByte(Mi.M_KBBRIGHT), Mi.Hex(Cut3()), Mi.GetByte(Mi.M_PERF), Mi.GetByte(Mi.M_FANFIXSW), Mi.GetByte(Mi.M_FANFIX)));
            }
            catch (Exception ex) { Chk("самотест не упал", false, ex.GetType().Name + ": " + ex.Message); }
            Info("ИТОГ: пройдено " + _pass + ", не пройдено " + _fail);
            _quit = true;
            try { BeginInvoke((Action)delegate { Close(); }); } catch { }
        }

        static byte[] Cut3() { byte r, g, b; Mi.GetColor(out r, out g, out b); return new byte[] { r, g, b }; }

        void ToggleCase(string name, CheckBox cb, byte method, int orig)
        {
            int want = orig == 1 ? 0 : 1;
            UI(delegate { cb.Checked = want == 1; });
            Thread.Sleep(600);
            Chk(name + ": включить/выключить через окно → плата", Mi.GetByte(method) == want, "hw=" + Mi.GetByte(method) + " want=" + want);
            Thread.Sleep(3500);   // опрос не должен откатывать
            Chk(name + ": состояние держится после опроса", Mi.GetByte(method) == want, "");
            UI(delegate { cb.Checked = orig == 1; });
            Thread.Sleep(600);
            Chk(name + ": возврат", Mi.GetByte(method) == orig, "hw=" + Mi.GetByte(method));
        }

        // ------------------------------------------------------------------ опрос датчиков
        void PollLoop()
        {
            int tick = 0;
            while (!_quit)
            {
                Snap s = new Snap();
                try
                {
                    int c, g;
                    if (Mi.GetFans(out c, out g)) { s.FanC = c; s.FanG = g; }
                    int t = Mi.GetByte(Mi.M_CPUTEMP); if (t > 0 && t < 130) s.CpuEc = t;
                    if (s.CpuEc <= 0 && tick % 3 == 0) s.CpuAcpi = Acpi.ThermalZoneC();
                    int tc, pw, ck, ut;
                    if (Nvml.Read(out tc, out pw, out ck, out ut)) { s.GpuT = tc; s.GpuW = pw; s.GpuClk = ck; s.GpuUtil = ut; }
                    if (_cpuLoad != null) { try { s.CpuLoad = (int)Math.Round(_cpuLoad.NextValue()); } catch { } }
                    s.Mode = Mi.GetByte(Mi.M_PERF);                       // режим — каждый цикл (Fn+F10)
                    if (tick % 2 == 0)                                    // остальное — раз в ~3 с
                    {
                        s.FnLock = Mi.GetByte(Mi.M_FNLOCK); s.Tp = Mi.GetByte(Mi.M_TPLOCK); s.Amb = Mi.GetByte(Mi.M_AMBIENT);
                        s.KbMode = Mi.GetByte(Mi.M_KBMODE); s.KbBright = Mi.GetByte(Mi.M_KBBRIGHT);
                        s.HasStates = true;
                    }
                }
                catch { }
                tick++;
                try { if (!_quit && IsHandleCreated) BeginInvoke((Action<Snap>)ShowSnap, s); } catch { }
                for (int i = 0; i < 15 && !_quit; i++) Thread.Sleep(100);
            }
        }

        /// <summary>Привести элементы окна в соответствие с железом (Fn-клавиши меняют состояние помимо окна).</summary>
        void SyncStates(Snap s)
        {
            if (unchecked(Environment.TickCount - _quiet) < 0) return;
            bool l = _loading; _loading = true;
            try
            {
                if (s.Mode >= 0 && s.Mode != _mode) { Log.Write("sync: mode " + _mode + " -> " + s.Mode); SyncMode(s.Mode); }
                if (!s.HasStates) return;
                if (s.FnLock >= 0 && cFnLock.Checked != (s.FnLock == 1)) cFnLock.Checked = s.FnLock == 1;
                if (s.Tp >= 0 && cTp.Checked != (s.Tp == 1)) cTp.Checked = s.Tp == 1;
                if (s.Amb >= 0 && cAmb.Checked != (s.Amb == 1)) cAmb.Checked = s.Amb == 1;
                if (s.KbBright >= 0 && s.KbBright <= 3 && tBright.Value != s.KbBright && !tBright.Capture) tBright.Value = s.KbBright;
                if (s.KbMode >= 0 && s.KbBright >= 0 && !cbKb.DroppedDown) { int ki = KbIndex(s.KbMode, s.KbBright); if (cbKb.SelectedIndex != ki) cbKb.SelectedIndex = ki; }
            }
            finally { _loading = l; }
        }

        void ShowSnap(Snap s)
        {
            if (!_selfTest) SyncStates(s);
            lCpu.Text = s.CpuT > 0 ? s.CpuT + " °C" : "—"; lCpu.ForeColor = TempColor(s.CpuT, 85, 95);
            lGpu.Text = s.GpuT > 0 ? s.GpuT + " °C" : "—"; lGpu.ForeColor = TempColor(s.GpuT, 80, 87);
            lFanC.Text = s.FanC >= 0 ? s.FanC.ToString() : "—";
            lFanG.Text = s.FanG >= 0 ? s.FanG.ToString() : "—";
            lCpuLoad.Text = s.CpuLoad >= 0 ? s.CpuLoad + " %" : "—";
            lGpuUtil.Text = s.GpuUtil >= 0 ? s.GpuUtil + " %" : "—";
            lGpuW.Text = s.GpuW >= 0 ? s.GpuW + " Вт" : "—";
            lGpuClk.Text = s.GpuClk >= 0 ? s.GpuClk + " МГц" : "—";

            hCpu.Add(s.CpuT); hGpu.Add(s.GpuT);
            if (hCpu.Count > 120) { hCpu.RemoveAt(0); hGpu.RemoveAt(0); }
            chart.Invalidate();

            int mx = Math.Max(s.CpuT, s.GpuT);
            Icon ni = MakeIcon(mx, ModeColor(_mode), mx >= 90);
            _tray.Icon = ni; if (_lastIcon != null) _lastIcon.Dispose(); _lastIcon = ni;
            _tray.Text = Trim63(string.Format("CPU {0}°  GPU {1}°\nКулеры {2}/{3}", s.CpuT, s.GpuT, s.FanC, s.FanG));

            Guard(s);
        }

        static string Trim63(string t) { return t.Length > 63 ? t.Substring(0, 63) : t; }
        static Color TempColor(int t, int warn, int hot) { return t >= hot ? Color.Tomato : t >= warn ? Color.Orange : Color.FromArgb(230, 233, 238); }

        // Защита фиксированных оборотов
        void Guard(Snap s)
        {
            if (!cFan.Checked) return;
            bool hot = s.CpuT >= 90 || s.GpuT >= 88;
            bool cool = s.CpuT < 82 && s.GpuT < 80;
            if (hot) _hotTicks++; else _hotTicks = 0;
            if (!_boosted && hot && _hotTicks >= 2 && tFan.Value < 100)
            {
                _boosted = true; Mi.Set(Mi.M_FANFIX, 100); Log.Write("guard: boost cpu=" + s.CpuT + " gpu=" + s.GpuT); status.ForeColor = Color.Orange; status.Text = "Защита: перегрев — обороты временно 100%";
            }
            else if (_boosted && cool)
            {
                _boosted = false; Mi.Set(Mi.M_FANFIX, (byte)tFan.Value); status.ForeColor = Color.MediumSeaGreen; status.Text = "Температура в норме — обороты " + tFan.Value + "%";
            }
        }

        void PaintChart(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = chart.ClientRectangle; r.Inflate(-6, -6);
            using (Pen grid = new Pen(Color.FromArgb(55, 60, 72)))
            {
                for (int t = 40; t <= 100; t += 20)
                {
                    float y = r.Bottom - (t - 30) / 75f * r.Height;
                    g.DrawLine(grid, r.Left, y, r.Right, y);
                    g.DrawString(t.ToString(), Font, Brushes.Gray, r.Left, y - 12);
                }
            }
            Draw(g, r, hCpu, Color.FromArgb(0, 168, 232)); Draw(g, r, hGpu, Color.FromArgb(120, 220, 90));
            g.DrawString("CPU", Font, Brushes.DeepSkyBlue, r.Right - 70, r.Top - 2);
            g.DrawString("GPU", Font, Brushes.LightGreen, r.Right - 36, r.Top - 2);
        }
        static void Draw(Graphics g, Rectangle r, List<int> h, Color c)
        {
            if (h.Count < 2) return;
            List<PointF> pts = new List<PointF>();
            for (int i = 0; i < h.Count; i++)
            {
                if (h[i] <= 0) continue;
                float x = r.Left + (float)i / 119f * r.Width;
                float y = r.Bottom - (Math.Min(105, Math.Max(30, h[i])) - 30) / 75f * r.Height;
                pts.Add(new PointF(x, y));
            }
            if (pts.Count > 1) using (Pen p = new Pen(c, 2f)) g.DrawLines(p, pts.ToArray());
        }

        // ------------------------------------------------------------------ закрытие
        void OnClosing(object s, FormClosingEventArgs e)
        {
            if (!_quit && _cfg.CloseToTray && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            _quit = true;
            try { if (_boosted && cFan.Checked) Mi.Set(Mi.M_FANFIX, (byte)tFan.Value); } catch { }   // снять защитный разгон при выходе
            try { SystemEvents.PowerModeChanged -= OnPower; } catch { }
            try { _fn.Dispose(); } catch { }
            try { _tray.Visible = false; _tray.Dispose(); } catch { }
            _cfg.Save();
        }
    }
}
