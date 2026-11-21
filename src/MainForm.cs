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
        public bool CustomPower = true;        // «свои лимиты» внутри пользовательского режима (по умолчанию включены)
        public bool KbRainbow;                 // последний выбор подсветки — программная радуга
        public int Spl = 82, Sppt = 92, Wall = 90;             // сниженные лимиты профиля «Свой» (плата принимает и значения ниже диапазона вендора 95..105: проверено)
        public int FanPct = 60;                // значение для «фиксированных оборотов»
        public bool Osd;
        public bool CloseToTray = true;
        public bool NoSave;                    // самотест: не писать настройки
        public bool CustomActive;              // пользовательский режим выбран (восстанавливается при запуске)
        public int CustomBase = 2;             // базовый режим платы: индекс списка 0 баланс, 1 тихий, 2 производительность (по умолчанию «Производительность»)
        public int KbVb = -1;                  // яркость подсветки, % (плавная); -1 — не задана
        public string KbColor = "";            // выбранный цвет подсветки RRGGBB (полная яркость)
        public int SettingsVer;                // 2 — профиль «Свой» по умолчанию: Производительность + сниженные лимиты
        public int FanKind;                    // 0 кривая, 1 автоматика платы, 2 фиксированные обороты
        public int CurveSmooth = 5;            // плавность кривой 1..10
        public string Curve = "";              // точки кривой «t:p,t:p,...»

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
                        case "KbRainbow": c.KbRainbow = b; break;
                        case "SettingsVer": if (int.TryParse(v, out n)) c.SettingsVer = n; break;
                        case "KbVb": if (int.TryParse(v, out n)) c.KbVb = n; break;
                        case "KbColor": c.KbColor = v; break;
                        case "Spl": if (int.TryParse(v, out n)) c.Spl = n; break;
                        case "Sppt": if (int.TryParse(v, out n)) c.Sppt = n; break;
                        case "Wall": if (int.TryParse(v, out n)) c.Wall = n; break;
                        case "CustomLimits": c.CustomPower = b; break;
                        case "CustomActive": c.CustomActive = b; break;
                        case "CustomBase": if (int.TryParse(v, out n)) c.CustomBase = n; break;
                        case "FanKind": if (int.TryParse(v, out n)) c.FanKind = n; break;
                        case "CurveSmooth": if (int.TryParse(v, out n)) c.CurveSmooth = n; break;
                        case "Curve": c.Curve = v; break;
                        case "FanPct": if (int.TryParse(v, out n)) c.FanPct = n; break;
                        case "Osd": c.Osd = b; break;
                        case "CloseToTray": c.CloseToTray = b; break;
                    }
                }
            }
            catch { }
            if (c.SettingsVer < 2) { c.Spl = 82; c.Sppt = 92; c.Wall = 90; c.CustomBase = 2; c.CustomPower = true; c.SettingsVer = 2; }   // миграция: новые умолчания профиля «Свой»
            c.Spl = Clamp(c.Spl, 50, 105); c.Sppt = Clamp(c.Sppt, 60, 120); c.Wall = Clamp(c.Wall, 80, 100);
            c.FanPct = Clamp(c.FanPct, 30, 100); c.CustomBase = Clamp(c.CustomBase, 0, 2); c.FanKind = Clamp(c.FanKind, 0, 2); c.CurveSmooth = Clamp(c.CurveSmooth, 1, 10);
            return c;
        }

        public void Save()
        {
            if (NoSave) return;
            try
            {
                List<string> lines = new List<string> { "SettingsVer=2", "KbRainbow=" + (KbRainbow ? 1 : 0), "Spl=" + Spl, "Sppt=" + Sppt, "Wall=" + Wall,
                    "Osd=" + (Osd ? 1 : 0), "CloseToTray=" + (CloseToTray ? 1 : 0), "FanPct=" + FanPct, "CustomLimits=" + (CustomPower ? 1 : 0),
                    "CustomActive=" + (CustomActive ? 1 : 0), "CustomBase=" + CustomBase, "FanKind=" + FanKind, "CurveSmooth=" + CurveSmooth };
                if (!string.IsNullOrEmpty(Curve)) lines.Add("Curve=" + Curve);
                if (KbVb >= 0) lines.Add("KbVb=" + KbVb);
                if (!string.IsNullOrEmpty(KbColor)) lines.Add("KbColor=" + KbColor);
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
        // пользовательский режим и вентиляторы
        readonly RadioButton rCustom = new RadioButton();
        readonly ComboBox cbBase = new ComboBox(), cbFan = new ComboBox(), cbPreset = new ComboBox();
        readonly TrackBar tFan = new TrackBar(), tSmooth = new TrackBar();
        readonly Label vFan = new Label(), vSmooth = new Label(), lFanInfo = new Label();
        readonly CurveEditor editor = new CurveEditor();
        readonly FanController _fc = new FanController();
        bool _custom;                        // включён пользовательский режим
        int _fanOut = -1, _lastFanMs, _fanChk;
        bool _fanSw, _fanClock;
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
        // плавная яркость: плата умеет 4 ступени (0–3), промежуточное значение = ступень + масштаб цвета в регистре
        double _vb = 100;                    // яркость, % (0…100)
        double _kbScale = 1;                 // множитель цвета внутри ступени
        int _hwLvl = -1, _kbHwMode = 2;      // ступень и режим платы, как мы их знаем
        int _ownLevel = -1, _ownTick;        // последняя наша запись ступени (чтобы отличить своё событие от клавиши)

        FlowLayoutPanel _flow;
        sealed class DoubleBufferedPanel : Panel { public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; } }

        public MainForm()
        {
            Text = "MRID6 Helper " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + " — MECHREVO Jiaolong";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Application; }   // дракон из ресурсов exe
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
            scroll.Controls.Add(flow); _flow = flow;

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
            TableLayoutPanel t2 = Grid(4, 0, 0, 0, 0);
            StyleMode(rQuiet, "Тихий"); StyleMode(rBal, "Баланс"); StyleMode(rPerf, "Игровой"); StyleMode(rCustom, "Свой");
            t2.RowCount = 1; t2.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t2.Controls.Add(rQuiet, 0, 0); t2.Controls.Add(rBal, 1, 0); t2.Controls.Add(rPerf, 2, 0); t2.Controls.Add(rCustom, 3, 0);
            rQuiet.CheckedChanged += delegate { if (rQuiet.Checked) SetMode(2); };
            rBal.CheckedChanged += delegate { if (rBal.Checked) SetMode(0); };
            rPerf.CheckedChanged += delegate { if (rPerf.Checked) SetMode(1); };
            rCustom.CheckedChanged += delegate { if (rCustom.Checked) SetCustom(); };
            Row(b2, t2);
            Row(b2, Hint("«Игровой» — режим платы «Производительность». «Свой» — пользовательский: плавная кривая вентиляторов и, по желанию, свои лимиты питания (настройки ниже)."));
            Fit(g2, b2, 8); flow.Controls.Add(g2);

            // --- Пользовательский режим ---
            TableLayoutPanel b3; GroupBox g3 = Group("Пользовательский режим", out b3);
            TableLayoutPanel tcb = Grid(2, LabelW, 0);
            StyleCombo(cbBase); cbBase.Items.AddRange(new object[] { "Баланс", "Тихий", "Производительность" });
            cbBase.SelectedIndexChanged += delegate { if (_loading) return; _cfg.CustomBase = cbBase.SelectedIndex; _cfg.Save(); if (_custom) ReapplyCustom(); };
            Pair(tcb, "Базовый режим", cbBase);
            StyleCombo(cbFan); cbFan.Items.AddRange(new object[] { "Кривая (плавная)", "Автоматика платы", "Фиксированные обороты" });
            cbFan.SelectedIndexChanged += delegate { if (_loading) return; _cfg.FanKind = cbFan.SelectedIndex; _cfg.FanPct = tFan.Value; _cfg.Save(); if (_custom) RestartFan(); };
            Pair(tcb, "Вентиляторы", cbFan);
            Row(b3, tcb);
            editor.Height = 190; Row(b3, editor);
            editor.Changed += delegate
            {
                _fc.Curve = editor.Curve.Clone(); _cfg.Curve = editor.Curve.Serialize(); _cfg.Save();
                bool l = _loading; _loading = true; cbPreset.SelectedIndex = -1; _loading = l;
            };
            TableLayoutPanel tpr = Grid(2, LabelW, 0);
            StyleCombo(cbPreset); cbPreset.Items.AddRange(FanCurve.PresetNames);
            cbPreset.SelectedIndexChanged += delegate
            {
                if (_loading || cbPreset.SelectedIndex < 0) return;
                editor.Curve = FanCurve.Preset(cbPreset.SelectedIndex); editor.Invalidate();
                _fc.Curve = editor.Curve.Clone(); _cfg.Curve = editor.Curve.Serialize(); _cfg.Save();
            };
            Pair(tpr, "Шаблон кривой", cbPreset);
            Row(b3, tpr);
            TableLayoutPanel tsm = Grid(3, LabelW, 0, 52);
            Slider(tsm, "Плавность, 1–10", tSmooth, vSmooth, 1, 10);
            tSmooth.ValueChanged += delegate { _fc.Smooth = tSmooth.Value; if (!_loading) { _cfg.CurveSmooth = tSmooth.Value; _cfg.Save(); } };
            Row(b3, tsm);
            TableLayoutPanel tfx = Grid(3, LabelW, 0, 52);
            Slider(tfx, "Фикс. обороты, %", tFan, vFan, 30, 100);
            tFan.MouseUp += delegate { if (_loading) return; _cfg.FanPct = tFan.Value; _cfg.Save(); if (_custom && cbFan.SelectedIndex == 2) RestartFan(); };
            tFan.KeyUp += delegate { if (_loading) return; _cfg.FanPct = tFan.Value; _cfg.Save(); if (_custom && cbFan.SelectedIndex == 2) RestartFan(); };
            Row(b3, tfx);
            lFanInfo.ForeColor = Dim; lFanInfo.AutoSize = true; lFanInfo.MaximumSize = new Size(GroupW - 40, 0); lFanInfo.Text = "Кривая работает в пользовательском режиме (кнопка «Свой»).";
            Row(b3, lFanInfo);
            Flat(cPower); cPower.Text = "Свои лимиты питания";
            Row(b3, cPower);
            TableLayoutPanel tpw = Grid(3, LabelW, 0, 52);
            Slider(tpw, "SPL, Вт", tSpl, vSpl, 50, 105);
            Slider(tpw, "SPPT, Вт", tSppt, vSppt, 60, 120);
            Slider(tpw, "Температурная стенка, °C", tWall, vWall, 80, 100);
            tSpl.ValueChanged += delegate { if (tSppt.Value < tSpl.Value + 5) tSppt.Value = Math.Min(tSppt.Maximum, tSpl.Value + 5); };   // SPPT не ниже SPL
            Row(b3, tpw);
            Button bApply = new Button { Text = "Применить лимиты", Height = 32 }; StyleBtn(bApply);
            bApply.Click += delegate { if (!_custom) rCustom.Checked = true; else ReapplyCustom(); };
            cPower.CheckedChanged += delegate
            {
                if (_loading) return;
                _cfg.CustomPower = cPower.Checked; _cfg.Save();
                if (_custom) ReapplyCustom(); else if (cPower.Checked) rCustom.Checked = true;
            };
            Row(b3, bApply);
            Row(b3, Hint("Свои лимиты включают режим «Производительность». Красная зона на графике — защитный пол: ниже него кривую опустить нельзя. Потолок оборотов зависит от базового режима: «Тихий» ≈ 3600, «Баланс» ≈ 5000, «Производительность» ≈ 5800 об/мин — на жаре выбирайте не «Тихий». Лимиты ниже штатных 95–105 Вт плата принимает (при SPL 60 / SPPT 70 нагрузка на CPU падает почти вдвое): чем ниже, тем тише и холоднее."));
            Fit(g3, b3, 40); flow.Controls.Add(g3);

            // --- Клавиатура ---
            TableLayoutPanel b5; GroupBox g5 = Group("Клавиатура и светодиоды  (Fn+F9)", out b5);
            TableLayoutPanel t5 = Grid(2, LabelW, 0);
            cbKb.DropDownStyle = ComboBoxStyle.DropDownList; cbKb.FlatStyle = FlatStyle.Flat; cbKb.BackColor = Panel2; cbKb.ForeColor = Fg;
            cbKb.Items.AddRange(new object[] { "Выкл (яркость 0)", "Радуга (плавная)", "Один цвет", "Авто-режим платы" });
            cbKb.SelectedIndexChanged += delegate { if (!_loading && cbKb.SelectedIndex >= 0) SetKbMode(cbKb.SelectedIndex); };
            Pair(t5, "Режим подсветки", cbKb);
            tBright.Minimum = 0; tBright.Maximum = 100; tBright.TickFrequency = 10; tBright.SmallChange = 2; tBright.LargeChange = 10; tBright.AutoSize = false; tBright.Height = 34; tBright.BackColor = Bg;
            tBright.ValueChanged += delegate
            {
                if (_loading) return;
                ApplyBrightness(tBright.Value, false);
                bool l = _loading; _loading = true; cbKb.SelectedIndex = KbIndex(_kbHwMode, _hwLvl); _loading = l;
            };
            tBright.MouseUp += delegate { if (!_loading) { _cfg.KbVb = (int)Math.Round(_vb); _cfg.Save(); } };
            tBright.KeyUp += delegate { if (!_loading) { _cfg.KbVb = (int)Math.Round(_vb); _cfg.Save(); } };
            Pair(t5, "Яркость, % (плавно)", tBright);
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
            Flat(cAuto); cAuto.Text = "Автозапуск и слежение за работой";
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
        static void StyleCombo(ComboBox c) { c.DropDownStyle = ComboBoxStyle.DropDownList; c.FlatStyle = FlatStyle.Flat; c.BackColor = Panel2; c.ForeColor = Fg; }
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
            m.Items.Add("Пользовательский", null, delegate { rCustom.Checked = true; });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Выход (не запускать до перезагрузки)", null, delegate { AutoStart.MarkExit(); _quit = true; Close(); });
            _tray.ContextMenuStrip = m;
            _tray.Visible = true;
            _tray.Text = "MRID6 Helper";
            _tray.Icon = MakeIcon(-1, Color.Gray);
            _tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMain(); };
        }


        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (ShotPath == null && SelfTestPath == null)
            {
                PlaceFullHeight();
                System.Windows.Forms.Timer again = new System.Windows.Forms.Timer { Interval = 500 };      // повтор: после смены масштаба/монитора размер мог пересчитаться
                again.Tick += delegate { again.Stop(); again.Dispose(); PlaceFullHeight(); };
                again.Start();
            }
            FitAll();
            if (SelfTestPath != null) { _selfTest = true; _cfg.NoSave = true; new Thread(RunSelfTest) { IsBackground = true, Name = "selftest" }.Start(); return; }
            if (ShotPath == null) return;
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = Math.Max(1, ShotDelaySec) * 1000 };
            t.Tick += delegate
            {
                t.Stop();
                try
                {
                    Application.DoEvents();
                    // панель групп имеет полную высоту (окно ограничено экраном) — рисуем её целиком; границы групп пишем рядом (для нарезки)
                    using (Bitmap bmp = new Bitmap(_flow.Width + 20, _flow.Height + 20))
                    {
                        using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Bg);
                        _flow.DrawToBitmap(bmp, new Rectangle(10, 10, _flow.Width, _flow.Height));
                        bmp.Save(ShotPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    StringBuilder gb = new StringBuilder();
                    foreach (GroupBox gx in _fitG) gb.AppendLine(gx.Text + "|" + (gx.Top + 10) + "|" + gx.Height);
                    File.WriteAllText(ShotPath + ".txt", gb.ToString(), Encoding.UTF8);
                }
                catch (Exception ex) { Log.Write("shot: " + ex.Message); }
                _quit = true; Close();
            };
            t.Start();
        }

        /// <summary>По умолчанию окно растянуто на всю высоту рабочей области экрана (того, где курсор), по центру по горизонтали.</summary>
        void PlaceFullHeight()
        {
            try
            {
                Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
                Bounds = new Rectangle(wa.Left + (wa.Width - Width) / 2, wa.Top, Width, wa.Height);
            }
            catch { }
        }

        public void ShowMain()
        {
            Show(); WindowState = FormWindowState.Normal; Activate(); BringToFront();
        }

        public bool StartHidden;
        public string SelfTestPath;             // служебное: самотест (--selftest файл)
        bool _fnStarted;
        public string ShotPath;
        public int ShotDelaySec = 4;            // через сколько секунд после показа снимать (чтобы график успел наполниться)                 // служебное: сохранить снимок окна и выйти (--shot файл)
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
                case 3: return Color.FromArgb(150, 90, 220);     // пользовательский
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
                _kbHwMode = kbm >= 0 ? kbm : 2; _hwLvl = kbb;
                if (kbb >= 0)
                {
                    _vb = (_cfg.KbVb >= 0 && LevelFor(_cfg.KbVb) == kbb) ? _cfg.KbVb : VbForLevel(kbb);
                    tBright.Value = Cfg.Clamp((int)Math.Round(_vb), 0, 100);
                    _kbScale = kbb == 0 ? 1.0 : Math.Min(1.0, LumFor(_vb) / LvlLum[kbb]);
                }
                if (kbm >= 0 && kbb >= 0) cbKb.SelectedIndex = KbIndex(kbm, kbb);
                byte r, g, b;
                if (!string.IsNullOrEmpty(_cfg.KbColor) && _cfg.KbColor.Length == 6)
                { try { _kbColor = Color.FromArgb(Convert.ToInt32(_cfg.KbColor.Substring(0, 2), 16), Convert.ToInt32(_cfg.KbColor.Substring(2, 2), 16), Convert.ToInt32(_cfg.KbColor.Substring(4, 2), 16)); } catch { } }
                else if (Mi.GetColor(out r, out g, out b))
                {   // в регистре может лежать уже приглушённый цвет — приводим к полной яркости
                    int mx = Math.Max(r, Math.Max(g, b)); double up = mx > 0 ? 255.0 / mx : 1.0;
                    _kbColor = Color.FromArgb((int)Math.Min(255, Math.Round(r * up)), (int)Math.Min(255, Math.Round(g * up)), (int)Math.Min(255, Math.Round(b * up)));
                }
                bColor.BackColor = _kbColor; bColor.ForeColor = (_kbColor.R + _kbColor.G + _kbColor.B) > 380 ? Color.Black : Color.White;
                v = Mi.GetByte(Mi.M_FNLOCK); if (v >= 0) cFnLock.Checked = v == 1;
                v = Mi.GetByte(Mi.M_TPLOCK); if (v >= 0) cTp.Checked = v == 1;
                v = Mi.GetByte(Mi.M_AMBIENT); if (v >= 0) cAmb.Checked = v == 1;
                v = Mi.GetByte(Mi.M_GPUMODE); if (v >= 0 && v < cbGpu.Items.Count) cbGpu.SelectedIndex = v;

                tSpl.Value = _cfg.Spl; tSppt.Value = _cfg.Sppt; tWall.Value = _cfg.Wall; cPower.Checked = _cfg.CustomPower;
                cbBase.SelectedIndex = _cfg.CustomBase; cbFan.SelectedIndex = _cfg.FanKind; tFan.Value = _cfg.FanPct; tSmooth.Value = _cfg.CurveSmooth;
                FanCurve fc0 = FanCurve.Parse(_cfg.Curve) ?? FanCurve.Preset(0);
                editor.Curve = fc0; _fc.Curve = fc0.Clone(); _fc.Smooth = _cfg.CurveSmooth;
                { int pi = -1; for (int q = 0; q < FanCurve.PresetNames.Length; q++) if (FanCurve.Preset(q).Serialize() == fc0.Serialize()) pi = q; cbPreset.SelectedIndex = pi; }
                cOsd.Checked = _cfg.Osd; cTrayClose.Checked = _cfg.CloseToTray; cAuto.Checked = AutostartExists();
            }
            finally { _loading = false; }
        }

        static readonly int[] BaseModes = { 0, 2, 1 };      // индекс списка «Баланс / Тихий / Производительность» → режим платы
        int BaseMode { get { int i = cbBase.SelectedIndex; return BaseModes[i < 0 ? 0 : i]; } }

        void SyncMode(int perf)
        {
            bool l = _loading; _loading = true;
            if (perf >= 0) _mode = perf;
            if (_custom) rCustom.Checked = true;
            else { rQuiet.Checked = perf == 2; rBal.Checked = perf == 0; rPerf.Checked = perf == 1; }
            _loading = l;
        }

        void ApplyStartup()
        {
            ThreadPool.QueueUserWorkItem(delegate { AutoStart.EnsureOpenTask(Application.ExecutablePath); });   // задача для ярлыка без щита UAC
            Mi.Set(Mi.M_CPUPOWER, Mi.P_CLOSE);          // по умолчанию — авто: свои лимиты закрыты (проверено: на нативный режим не влияет)
            Mi.Set(Mi.M_FANFIXSW, 0);                   // вентиляторы — автоматика платы, пока не выбран пользовательский режим
            if (_cfg.KbRainbow) { bool l = _loading; _loading = true; cbKb.SelectedIndex = 1; _loading = l; StartRainbow(); }
            if (_cfg.CustomActive) rCustom.Checked = true;      // → SetCustom()
        }

        // ------------------------------------------------------------------ действия
        void SetMode(int mode)
        {
            if (_loading) return;
            Touch();
            if (_custom) LeaveCustom();                 // сначала выйти из пользовательского режима (снимет лимиты, вернёт автоматику вентиляторов)
            _mode = mode;
            Mi.SetPerfMode(mode);
        }

        void SetCustom()
        {
            if (_loading) return;
            Touch();
            _custom = true; _cfg.CustomActive = true; _cfg.Save();
            ReapplyCustom();
        }

        /// <summary>Применить пользовательский режим на плате: режим (или свои лимиты) и перезапуск управления вентиляторами.</summary>
        void ReapplyCustom()
        {
            Touch();
            if (cPower.Checked) ApplyPower(true);
            else
            {
                Mi.Set(Mi.M_CPUPOWER, Mi.P_CLOSE);      // сначала закрыть лимиты (плата вернёт прежний режим), затем писать базовый
                Mi.SetPerfMode(BaseMode); _mode = BaseMode;
            }
            RestartFan();
            status.ForeColor = Color.MediumSeaGreen;
            status.Text = "Пользовательский режим включён" + (cPower.Checked ? " (свои лимиты)" : "");
        }

        void LeaveCustom()
        {
            _custom = false; _cfg.CustomActive = false; _cfg.Save();
            Mi.Set(Mi.M_FANFIXSW, 0);                   // вернуть вентиляторы автоматике платы
            _fanSw = false; _fanOut = -1; _boosted = false;
            if (cPower.Checked) Mi.SetCustomPower(false, 0, 0, 0);   // закрыть лимиты (галочку не трогаем — это настройка пользовательского режима)
        }

        void ApplyPower(bool save)
        {
            Touch();
            _cfg.CustomPower = cPower.Checked; _cfg.Spl = tSpl.Value; _cfg.Sppt = tSppt.Value; _cfg.Wall = tWall.Value;
            if (save) _cfg.Save();
            bool ok = Mi.SetCustomPower(_cfg.CustomPower, _cfg.Wall, _cfg.Spl, _cfg.Sppt);
            _powerOk = ok;
            if (ok) { Thread.Sleep(250); SyncMode(Mi.GetByte(Mi.M_PERF)); }   // плата сама меняет режим: включение → «Производительность», выключение → прежний
            status.ForeColor = ok ? Color.MediumSeaGreen : Color.Tomato;
            status.Text = ok ? (_cfg.CustomPower ? string.Format("Лимиты применены: SPL {0} Вт, SPPT {1} Вт, стенка {2} °C", _cfg.Spl, _cfg.Sppt, _cfg.Wall) : "Свои лимиты выключены") : "Не удалось применить лимиты: " + Mi.LastError;
        }

        void RestartFan()
        {
            _fanOut = -1; _fanSw = false; _fanClock = false; _fc.Reset(); _hotTicks = 0; _boosted = false;
            if (cbFan.SelectedIndex == 1) Mi.Set(Mi.M_FANFIXSW, 0);     // автоматика платы
        }

        /// <summary>Исполнение выбранного способа управления вентиляторами (вызывается из опроса, ≈ раз в 1,5 с).</summary>
        void FanTick(Snap s)
        {
            if (!_custom) return;
            int kind = cbFan.SelectedIndex < 0 ? 0 : cbFan.SelectedIndex;
            int cpu = s.CpuT, gpu = s.GpuT;
            if (kind == 1) { lFanInfo.Text = "Вентиляторами управляет автоматика платы."; return; }
            if (cpu <= 0 && gpu <= 0) return;                            // нет данных — не трогаем
            double raw = Math.Max(cpu, gpu);
            int nowMs = Environment.TickCount;
            double dt = _fanClock ? unchecked(nowMs - _lastFanMs) / 1000.0 : 1.5;
            _lastFanMs = nowMs; _fanClock = true;
            int want = kind == 2 ? FixedWithGuard(cpu, gpu) : _fc.Step(raw, dt);
            want = Cfg.Clamp(want, 20, 100);
            if (!_fanSw) { Mi.Set(Mi.M_FANFIX, (byte)want); Mi.Set(Mi.M_FANFIXSW, 1); _fanSw = true; _fanOut = want; _fanChk = 0; }
            else
            {
                if (want != _fanOut) { Mi.Set(Mi.M_FANFIX, (byte)want); _fanOut = want; }
                if (++_fanChk >= 8)
                {   // раз в ~12 с проверяем, что плата не сбросила режим фиксированных оборотов
                    _fanChk = 0;
                    if (Mi.GetByte(Mi.M_FANFIXSW) != 1) { Mi.Set(Mi.M_FANFIX, (byte)want); Mi.Set(Mi.M_FANFIXSW, 1); }
                }
            }
            if (kind == 0)
            {
                editor.CurT = _fc.SmoothT; editor.CurP = _fanOut; editor.Invalidate();
                lFanInfo.Text = string.Format("Сейчас: {0:0.#} °C (сглаж.) → обороты {1} %, по кривой {2:0} %", _fc.SmoothT, _fanOut, _fc.Target);
            }
            else lFanInfo.Text = string.Format("Фиксированные обороты {0} %{1}", _fanOut, _boosted ? " — защита от перегрева" : "");
        }

        /// <summary>Фиксированные обороты с защитой: при CPU ≥ 90 / GPU ≥ 88 временно 100 %, возврат после остывания.</summary>
        int FixedWithGuard(int cpu, int gpu)
        {
            bool hot = cpu >= 90 || gpu >= 88, cool = cpu < 82 && gpu < 80;
            if (hot) _hotTicks++; else _hotTicks = 0;
            if (!_boosted && hot && _hotTicks >= 2 && tFan.Value < 100)
            {
                _boosted = true; Log.Write("guard: boost cpu=" + cpu + " gpu=" + gpu);
                status.ForeColor = Color.Orange; status.Text = "Защита: перегрев — обороты временно 100%";
            }
            else if (_boosted && cool)
            {
                _boosted = false; status.ForeColor = Color.MediumSeaGreen; status.Text = "Температура в норме — обороты " + tFan.Value + "%";
            }
            return _boosted ? 100 : tFan.Value;
        }

        /// <summary>Индекс списка: 0 «выкл» (ступень 0), 1 плавная радуга (программная), 2 один цвет, 3 собственный авто-режим платы.</summary>
        int KbIndex(int mode, int level) { return level == 0 ? 0 : (_rainbow ? 1 : (mode == 1 ? 3 : 2)); }

        // ---------------------------------------------------------------- яркость подсветки
        // Плата умеет 4 ступени (0 / 40 / 70 / 100 % по данным вендора). Ползунок 0–100 % — это ВОСПРИНИМАЕМАЯ яркость (гамма 2):
        // световой поток = (ползунок/100)^2; берётся наименьшая ступень платы, которой хватает, остальное — масштаб цвета в регистре 0x11.
        static readonly double[] LvlLum = { 0.0, 0.40, 0.70, 1.0 };
        const double Gamma = 2.0;
        static double LumFor(double vb) { return Math.Pow(Math.Max(0, Math.Min(100, vb)) / 100.0, Gamma); }
        static int LevelFor(double vb)
        {
            if (vb <= 0.5) return 0;
            double l = LumFor(vb);
            for (int k = 1; k <= 3; k++) if (l <= LvlLum[k] + 1e-6) return k;
            return 3;
        }
        static double VbForLevel(int level) { return 100.0 * Math.Pow(LvlLum[Cfg.Clamp(level, 0, 3)], 1.0 / Gamma); }   // положение ползунка, соответствующее ступени платы
        static Color ScaleColor(Color c, double k) { return Color.FromArgb((int)Math.Round(c.R * k), (int)Math.Round(c.G * k), (int)Math.Round(c.B * k)); }
        void WriteScaledColor() { Color c = ScaleColor(_kbColor, _kbScale); Mi.Set(Mi.M_KBCOLOR, c.R, c.G, c.B); }

        /// <summary>Задать яркость 0…100 %. «Авто-режим платы» цвет из регистра не берёт — там только 4 ступени (ближайшая).</summary>
        void ApplyBrightness(double vb, bool updateUi)
        {
            Touch();
            _vb = Math.Max(0, Math.Min(100, vb));
            bool scalable = !(_kbHwMode == 1 && !_rainbow);
            int lvl;
            if (scalable) { lvl = LevelFor(_vb); _kbScale = lvl == 0 ? 1.0 : Math.Min(1.0, LumFor(_vb) / LvlLum[lvl]); }
            else
            {
                double l = LumFor(_vb); lvl = 0; double best = double.MaxValue;
                for (int k = 0; k <= 3; k++) { double d = Math.Abs(LvlLum[k] - l); if (d < best) { best = d; lvl = k; } }
                _kbScale = 1.0;
            }
            if (lvl != _hwLvl) { _ownLevel = lvl; _ownTick = Environment.TickCount; Mi.Set(Mi.M_KBBRIGHT, (byte)lvl); _hwLvl = lvl; }
            if (lvl > 0 && scalable && !_rainbow) WriteScaledColor();      // при радуге цвет пишет её поток (с тем же множителем)
            if (updateUi) { bool l = _loading; _loading = true; tBright.Value = Cfg.Clamp((int)Math.Round(_vb), 0, 100); _loading = l; }
        }

        /// <summary>Событие яркости от платы (клавиши Fn): плата сама шагает по ступеням — просто отражаем её ступень на ползунке.</summary>
        void HandleFnBrightness(int val)
        {
            if (val == _ownLevel && unchecked(Environment.TickCount - _ownTick) < 250) return;      // это отражение нашей же записи
            Touch();
            _hwLvl = val;
            ApplyBrightness(VbForLevel(val), true);
            _cfg.KbVb = (int)Math.Round(_vb); _cfg.Save();
        }

        // --- плавная радуга: плата сама ничего не анимирует, поэтому перебираем оттенок через цветовой регистр
        Thread _rb;
        volatile bool _rbOn;
        bool _rainbow;

        static void Hsv(double h, out byte r, out byte g, out byte b)
        {
            double c = 1.0, x = c * (1 - Math.Abs((h / 60.0) % 2 - 1)); double r1 = 0, g1 = 0, b1 = 0;
            if (h < 60) { r1 = c; g1 = x; } else if (h < 120) { r1 = x; g1 = c; } else if (h < 180) { g1 = c; b1 = x; }
            else if (h < 240) { g1 = x; b1 = c; } else if (h < 300) { r1 = x; b1 = c; } else { r1 = c; b1 = x; }
            r = (byte)Math.Round(r1 * 255); g = (byte)Math.Round(g1 * 255); b = (byte)Math.Round(b1 * 255);
        }

        void RainbowLoop()
        {
            double h = 0;
            while (_rbOn && !_quit)
            {
                byte r, g, b; Hsv(h, out r, out g, out b);
                double k = _kbScale;
                if (!Mi.Set(Mi.M_KBCOLOR, (byte)Math.Round(r * k), (byte)Math.Round(g * k), (byte)Math.Round(b * k))) Thread.Sleep(400);
                h += 2.4; if (h >= 360) h -= 360;               // полный круг ≈ 14 с
                Thread.Sleep(80);
            }
        }

        void StartRainbow()
        {
            StopRainbow();
            Touch();
            Mi.Set(Mi.M_KBMODE, 2); _kbHwMode = 2;
            _rainbow = true;
            if (_hwLvl <= 0 || _vb <= 0.5) ApplyBrightness(100, true); else ApplyBrightness(_vb, false);
            _rbOn = true;
            _rb = new Thread(delegate () { Thread.Sleep(500); RainbowLoop(); }) { IsBackground = true, Name = "rainbow" };   // 500 мс после режима — как у вендора
            _rb.Start();
        }

        void StopRainbow()
        {
            _rbOn = false; _rainbow = false;
            Thread t = _rb; _rb = null;
            if (t != null && t.IsAlive) t.Join(600);
        }

        /// <summary>«Выкл» = яркость 0 (плата игнорирует режим 0). Режимы платы — 1 (авто) и 2 (один цвет).
        /// При яркости 0 любой режим невидим — поднимаем яркость до максимума.</summary>
        void SetKbMode(int index)
        {
            Touch();
            string note = ""; bool ok = true;
            _cfg.KbRainbow = index == 1; _cfg.Save();
            if (index == 1) { StartRainbow(); status.ForeColor = Color.MediumSeaGreen; status.Text = "Подсветка: плавная радуга (работает, пока запущено приложение)"; return; }
            StopRainbow();
            if (index == 0)
            {
                ApplyBrightness(0, true); ok = true;
                _cfg.KbVb = 0; _cfg.Save();
            }
            else
            {
                int hwMode = index == 3 ? 1 : 2;
                ok = Mi.Set(Mi.M_KBMODE, (byte)hwMode);
                for (int attempt = 0; attempt < 3 && ok && Mi.GetByte(Mi.M_KBMODE) != hwMode; attempt++)
                {   // сразу после записи цвета плата может проигнорировать смену режима — повторяем с паузой
                    Thread.Sleep(600); ok = Mi.Set(Mi.M_KBMODE, (byte)hwMode);
                }
                if (ok && Mi.GetByte(Mi.M_KBMODE) != hwMode) { ok = false; Mi.LastError = "плата не приняла режим " + hwMode; }
                if (ok) _kbHwMode = hwMode;
                if (ok && (_hwLvl <= 0 || _vb <= 0.5)) { ApplyBrightness(100, true); note = " (яркость была 0 — поднята до 100%)"; }
                else if (ok) ApplyBrightness(_vb, false);     // вернуть выбранный цвет (с учётом яркости) после радуги
            }
            status.ForeColor = ok ? Color.MediumSeaGreen : Color.Tomato;
            status.Text = ok ? "Подсветка: " + cbKb.Items[index] + note : "Не удалось сменить режим подсветки: " + Mi.LastError;
        }

        void PickColor()
        {
            using (ColorDialog d = new ColorDialog { Color = _kbColor, FullOpen = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                StopRainbow(); _cfg.KbRainbow = false; _cfg.Save();
                _kbColor = d.Color; bColor.BackColor = _kbColor; bColor.ForeColor = (_kbColor.R + _kbColor.G + _kbColor.B) > 380 ? Color.Black : Color.White;
                _kbHwMode = 2; _cfg.KbColor = string.Format("{0:X2}{1:X2}{2:X2}", _kbColor.R, _kbColor.G, _kbColor.B); _cfg.Save();
                if (_hwLvl <= 0 || _vb <= 0.5) ApplyBrightness(100, true);
                Color cc = ScaleColor(_kbColor, _kbScale);
                ThreadPool.QueueUserWorkItem(delegate { Mi.SetKbColor(cc.R, cc.G, cc.B); });   // пауза 500 мс между режимом и цветом — как у вендора
                bool l = _loading; _loading = true; cbKb.SelectedIndex = 2; _loading = l;
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
        static bool AutostartExists() { return AutoStart.Exists(); }
        void SetAutostart(bool on)
        {
            string err = on ? AutoStart.Install(Application.ExecutablePath) : AutoStart.Remove();
            if (err != null) { status.ForeColor = Color.Tomato; status.Text = "Автозапуск: " + err; }
            else { status.ForeColor = Color.MediumSeaGreen; status.Text = on ? "Автозапуск включён: запуск при входе и контроль раз в минуту" : "Автозапуск выключен"; }
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
            Log.Write(string.Format("fn event: type={0} name={1} value={2} t={3}", type, name, val, Environment.TickCount));
            try
            {
                BeginInvoke((Action)delegate
                {
                    string msg = null;
                    switch (name)
                    {
                        case FnEvents.EV_PERF:
                            if (_custom && unchecked(Environment.TickCount - _quiet) >= 0)
                            {   // нажали Fn+F10 в пользовательском режиме: выходим из него и утверждаем выбранный клавишей режим
                                LeaveCustom(); Mi.SetPerfMode(val); Touch();
                            }
                            SyncMode(val);
                            msg = "Режим: " + (val == 2 ? "Тихий" : val == 1 ? "Производительность" : "Баланс");
                            break;
                        case FnEvents.EV_KBBRIGHT:
                            HandleFnBrightness(val);
                            msg = "Яркость подсветки: " + (int)Math.Round(_vb) + " %"; break;
                        case FnEvents.EV_KBMODE:
                            if (_rainbow && unchecked(Environment.TickCount - _quiet) >= 0) { StopRainbow(); _cfg.KbRainbow = false; }
                            _kbHwMode = val;
                            { bool l = _loading; _loading = true; cbKb.SelectedIndex = KbIndex(val, _hwLvl); _loading = l; }
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
                try { BeginInvoke((Action)delegate { if (_custom) ReapplyCustom(); }); } catch { }
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
                bool cu0 = true; UI(delegate { cu0 = _custom; });
                Chk("запуск: вне пользовательского режима плата в авто (свои лимиты не навязаны)", cu0 || Mi.GetByte(Mi.M_FANFIXSW) == 0, "custom=" + cu0);
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
                int[] vbs = { 100, 66, 33, 0 }, lvls = { 3, 2, 1, 0 };
                for (int bi = 0; bi < 4; bi++) { int vv = vbs[bi]; UI(delegate { tBright.Value = vv; }); Thread.Sleep(350); Chk("яркость " + vv + " % → ступень платы " + lvls[bi], Mi.GetByte(Mi.M_KBBRIGHT) == lvls[bi], "hw=" + Mi.GetByte(Mi.M_KBBRIGHT)); }
                UI(delegate { tBright.Value = 0; }); Thread.Sleep(350);
                int kbi = -1; UI(delegate { kbi = cbKb.SelectedIndex; }); Chk("список подсветки показывает «Выкл» при яркости 0", kbi == 0, "index=" + kbi);
                UI(delegate { cbKb.SelectedIndex = 3; }); Thread.Sleep(900);
                Chk("режим подсветки: авто-режим платы → плата", Mi.GetByte(Mi.M_KBMODE) == 1, "hw=" + Mi.GetByte(Mi.M_KBMODE));
                Chk("  при яркости 0 яркость поднялась до 3", Mi.GetByte(Mi.M_KBBRIGHT) == 3, "bright=" + Mi.GetByte(Mi.M_KBBRIGHT));
                Thread.Sleep(2500);
                Chk("  авто-режим платы не слетает после опроса", Mi.GetByte(Mi.M_KBMODE) == 1, "");
                // плавная радуга: цветовой регистр должен непрерывно меняться
                UI(delegate { cbKb.SelectedIndex = 1; }); Thread.Sleep(1500);
                List<string> cols = new List<string>();
                for (int q = 0; q < 6; q++) { cols.Add(Mi.Hex(Cut3())); Thread.Sleep(450); }
                int distinct = new HashSet<string>(cols).Count;
                Chk("плавная радуга: цвет непрерывно меняется", distinct >= 5, "разных значений за 2,7 с: " + distinct + " из 6 (" + cols[0] + " → " + cols[5] + ")");
                Chk("  радуга идёт в режиме «один цвет» платы", Mi.GetByte(Mi.M_KBMODE) == 2, "hw=" + Mi.GetByte(Mi.M_KBMODE));
                Thread.Sleep(2500);
                Chk("  радуга не слетает после опроса", cols.Count > 0 && new HashSet<string>(new string[] { Mi.Hex(Cut3()), cols[5] }).Count == 2, "");
                UI(delegate { cbKb.SelectedIndex = 2; }); Thread.Sleep(900);
                string c1 = Mi.Hex(Cut3()); Thread.Sleep(1300); string c2 = Mi.Hex(Cut3());
                Chk("после выбора «Один цвет» радуга остановлена", c1 == c2, c1 + " / " + c2);
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

                // --- плавная яркость: промежуточные значения = ступень платы + масштаб цвета
                UI(delegate { _kbColor = Color.FromArgb(200, 100, 50); cbKb.SelectedIndex = 2; tBright.Value = 100; }); Thread.Sleep(900);
                UI(delegate { tBright.Value = 50; }); Thread.Sleep(900);
                byte sr, sg, sb2; Mi.GetColor(out sr, out sg, out sb2);
                // 50 % воспринимаемой яркости = световой поток 0,25 → ступень платы 1 (0,40), цвет ×0,625
                Chk("плавная яркость: 50 % → ступень платы 1", Mi.GetByte(Mi.M_KBBRIGHT) == 1, "hw=" + Mi.GetByte(Mi.M_KBBRIGHT));
                Chk("  цвет в регистре масштабирован на 62,5 % (≈125,63,31)", Math.Abs(sr - 125) <= 2 && Math.Abs(sg - 63) <= 2 && Math.Abs(sb2 - 31) <= 2, sr + "," + sg + "," + sb2);
                UI(delegate { tBright.Value = 75; }); Thread.Sleep(800);
                byte tr, tg, tb3; Mi.GetColor(out tr, out tg, out tb3);
                Chk("  75 % ползунка — ещё НЕ максимум (световой поток 0,56: ступень 2, цвет ×0,80)", Mi.GetByte(Mi.M_KBBRIGHT) == 2 && Math.Abs(tr - 160) <= 3, "hw=" + Mi.GetByte(Mi.M_KBBRIGHT) + " R=" + tr);
                UI(delegate { tBright.Value = 100; }); Thread.Sleep(800);
                byte mr, mg, mb; Mi.GetColor(out mr, out mg, out mb);
                Chk("  100 % — максимум: ступень 3, цвет без ослабления", Mi.GetByte(Mi.M_KBBRIGHT) == 3 && mr == 200 && mg == 100 && mb == 50, "hw=" + Mi.GetByte(Mi.M_KBBRIGHT) + " " + mr + "," + mg + "," + mb);
                UI(delegate { tBright.Value = 40; }); Thread.Sleep(700);
                byte ur, ug, ub; Mi.GetColor(out ur, out ug, out ub);
                Chk("  40 % — темнее при той же ступени платы", Mi.GetByte(Mi.M_KBBRIGHT) == 1 && ur < sr, "R " + sr + " → " + ur);
                // клавиши Fn: плата шагает по ступеням, ползунок отражает ступень (плавного хода по клавишам нет)
                UI(delegate { tBright.Value = 20; }); Thread.Sleep(900);
                UI(delegate { HandleFnBrightness(2); }); Thread.Sleep(400);
                double vTap = 0; UI(delegate { vTap = _vb; });
                Chk("Fn: ступень платы 2 → ползунок ≈ " + Math.Round(VbForLevel(2)) + " %", Math.Abs(vTap - VbForLevel(2)) < 1, "vb=" + Math.Round(vTap, 1));
                UI(delegate { HandleFnBrightness(3); }); Thread.Sleep(300);
                UI(delegate { vTap = _vb; });
                Chk("Fn: серия нажатий — просто ступени (ступень 3 → 100 %, без плавного хода)", Math.Abs(vTap - 100) < 1, "vb=" + Math.Round(vTap, 1));
                Mi.Set(Mi.M_KBBRIGHT, 3);      // имитированные события железо не меняют — выровнять ступень платы с тем, что «известно» приложению

                Stage("подсветка");
                // --- кривая вентиляторов: расчёты без железа
                FanCurve cv = FanCurve.Preset(0);
                Chk("кривая: значения в точках", Math.Abs(cv.Eval(cv.T[0]) - cv.P[0]) < 0.01 && Math.Abs(cv.Eval(cv.T[3]) - cv.P[3]) < 0.01, "");
                Chk("кривая: линейная интерполяция", Math.Abs(cv.Eval((cv.T[1] + cv.T[2]) / 2) - (cv.P[1] + cv.P[2]) / 2) < 0.01, "");
                Chk("кривая: защитный пол (40 °C → 20 %, 95 °C → 100 %)", Math.Abs(FanCurve.Floor(40) - 20) < 0.01 && Math.Abs(FanCurve.Floor(95) - 100) < 0.01, "");
                FanCurve bad = FanCurve.Preset(1); bad.T[5] = 91; bad.P[5] = 25; bad.Normalize();
                Chk("кривая: нельзя опустить ниже защитного пола", bad.P[5] >= FanCurve.Floor(bad.T[5]) - 0.01, "точка " + bad.T[5] + "→" + bad.P[5]);
                Chk("кривая: сохранение и чтение", FanCurve.Parse(cv.Serialize()) != null && FanCurve.Parse(cv.Serialize()).Serialize() == cv.Serialize(), cv.Serialize());
                FanController fk = new FanController { Curve = FanCurve.Preset(0), Smooth = 5 };
                int lo = 999, hi = 0;
                for (int i = 0; i < 160; i++) { int o = fk.Step(70 + 4 * Math.Sin(i * Math.PI / 3), 1.5); if (i >= 60) { lo = Math.Min(lo, o); hi = Math.Max(hi, o); } }
                Chk("контроллер: шум ±4 °C не вызывает пульсаций оборотов", hi - lo <= 3, "диапазон " + lo + "…" + hi + " %");
                fk.Reset(); int o50 = fk.Step(50, 1.5); int oUp = o50; for (int i = 0; i < 8; i++) oUp = fk.Step(88, 1.5);
                Chk("контроллер: резкий рост температуры — обороты быстро растут", oUp >= o50 + 25, o50 + " → " + oUp + " %");
                int oDn = oUp; for (int i = 0; i < 10; i++) oDn = fk.Step(50, 1.5);
                Chk("контроллер: после спада обороты снижаются плавно (≤ ~1 %/с)", oUp - oDn <= 15 && oDn <= oUp, oUp + " → " + oDn + " % за 15 с");
                fk.Reset(); fk.Step(60, 1.5);
                Chk("контроллер: ≥ 92 °C — сразу 100 %", fk.Step(93, 1.5) == 100, "");
                int ei = FanCurve.N - 1; double eT = 0, eP = 0;
                UI(delegate { editor.SetPoint(ei, 91, 25); eT = editor.Curve.T[ei]; eP = editor.Curve.P[ei]; });
                Chk("редактор: точку нельзя опустить ниже защитного пола", eP >= FanCurve.Floor(eT) - 0.01, eT + "→" + eP);
                UI(delegate { editor.Curve = FanCurve.Preset(0); _fc.Curve = editor.Curve.Clone(); });

                // --- пользовательский режим на железе
                UI(delegate { cPower.Checked = false; cbBase.SelectedIndex = 0; cbFan.SelectedIndex = 0; });
                UI(delegate { rCustom.Checked = true; }); Thread.Sleep(4500);
                bool isCu = false; UI(delegate { isCu = _custom; });
                Chk("кнопка «Свой»: пользовательский режим включился", isCu, "");
                Chk("  базовый режим платы = «Баланс»", Mi.GetByte(Mi.M_PERF) == 0, "hw=" + Mi.GetByte(Mi.M_PERF));
                int fsw = Mi.GetByte(Mi.M_FANFIXSW), fpc = Mi.GetByte(Mi.M_FANFIX);
                Chk("  кривая управляет вентиляторами (плата в режиме процента)", fsw == 1, "sw=" + fsw);
                Chk("  обороты по кривой в допустимых пределах 20–100 %", fpc >= 20 && fpc <= 100, "hw=" + fpc);
                int rpm1 = 0; { int cc, gg; Mi.GetFans(out cc, out gg); rpm1 = cc; }
                Chk("  кулер вращается", rpm1 > 500, "rpm=" + rpm1);
                UI(delegate { cbBase.SelectedIndex = 1; }); Thread.Sleep(1800);
                Chk("  базовый режим «Тихий» → плата", Mi.GetByte(Mi.M_PERF) == 2, "hw=" + Mi.GetByte(Mi.M_PERF));
                UI(delegate { cbBase.SelectedIndex = 0; }); Thread.Sleep(1800);
                Chk("  базовый режим «Баланс» → плата", Mi.GetByte(Mi.M_PERF) == 0, "hw=" + Mi.GetByte(Mi.M_PERF));
                UI(delegate { tFan.Value = 80; cbFan.SelectedIndex = 2; }); Thread.Sleep(4000);
                int s2 = Mi.GetByte(Mi.M_FANFIXSW), p2 = Mi.GetByte(Mi.M_FANFIX);
                Chk("  фиксированные обороты: 80 % (или 100 % при защите)", s2 == 1 && (p2 == 80 || p2 == 100), "sw=" + s2 + " pct=" + p2);
                UI(delegate { cbFan.SelectedIndex = 1; }); Thread.Sleep(3000);
                Chk("  автоматика платы: фиксированный режим выключен", Mi.GetByte(Mi.M_FANFIXSW) == 0, "");
                UI(delegate { cbFan.SelectedIndex = 0; }); Thread.Sleep(4000);
                Chk("  возврат к кривой: управление включено снова", Mi.GetByte(Mi.M_FANFIXSW) == 1, "");
                UI(delegate { cPower.Checked = true; }); Thread.Sleep(1800);
                Chk("  свои лимиты в пользовательском режиме → плата в «Производительность»", _powerOk && Mi.GetByte(Mi.M_PERF) == 1, "ok=" + _powerOk + " hw=" + Mi.GetByte(Mi.M_PERF));
                Thread.Sleep(3500);
                Chk("  режим держится (нет прыжков)", Mi.GetByte(Mi.M_PERF) == 1, "");
                UI(delegate { cPower.Checked = false; }); Thread.Sleep(1800);
                Chk("  выключение лимитов возвращает базовый режим", Mi.GetByte(Mi.M_PERF) == 0, "hw=" + Mi.GetByte(Mi.M_PERF));
                UI(delegate { rBal.Checked = true; }); Thread.Sleep(3500);
                bool stillCu = true; UI(delegate { stillCu = _custom; });
                Chk("выход из пользовательского режима: вентиляторы — автоматика", Mi.GetByte(Mi.M_FANFIXSW) == 0 && !stillCu, "sw=" + Mi.GetByte(Mi.M_FANFIXSW));
                Chk("  режим «Баланс» держится", Mi.GetByte(Mi.M_PERF) == 0, "hw=" + Mi.GetByte(Mi.M_PERF));
                UI(delegate { rb[oMode >= 0 ? oMode : 0].Checked = true; }); Thread.Sleep(700);
                Mi.Set(Mi.M_FANFIX, (byte)(oPct > 0 ? oPct : 100)); Mi.Set(Mi.M_FANFIXSW, (byte)(oSw == 1 ? 1 : 0));

                Stage("вентиляторы и пользовательский режим");
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
                string tx = AutoStart.Xml();
                Chk("  задача: работает от батареи и не гасится по таймеру", tx.Contains("<DisallowStartIfOnBatteries>false") && tx.Contains("<StopIfGoingOnBatteries>false") && tx.Contains("<ExecutionTimeLimit>PT0S"), "");
                Chk("  задача: слежение (повтор раз в минуту, без второго экземпляра, перезапуск при сбое)", tx.Contains("<Interval>PT1M") && tx.Contains("IgnoreNew") && tx.Contains("RestartOnFailure"), "");
                Chk("  задача: запуск с повышенными правами", tx.Contains("HighestAvailable"), "");
                UI(delegate { cAuto.Checked = false; }); Thread.Sleep(1500);
                Chk("автозапуск: удаление задачи", !AutostartExists(), "");
                if (oAuto) { UI(delegate { cAuto.Checked = true; }); Thread.Sleep(1200); }
                bool osdOk = true; try { UI(delegate { _osd.Flash("Самотест: OSD"); }); } catch { osdOk = false; }
                Chk("всплывающая подсказка (OSD)", osdOk, "");
                Chk("иконка в трее создана", _tray.Visible, "");

                Stage("автозапуск/OSD");
                // --- возврат исходного состояния
                UI(delegate { StopRainbow(); _kbColor = Color.FromArgb(r0, g0, b0); cbKb.SelectedIndex = (oKb == 1 ? 3 : 2); }); Thread.Sleep(700);
                Mi.SetKbColor(r0, g0, b0);
                for (int at = 0; at < 4 && Mi.GetByte(Mi.M_KBMODE) != (oKb >= 0 ? oKb : 2); at++) { Thread.Sleep(700); Mi.Set(Mi.M_KBMODE, (byte)(oKb >= 0 ? oKb : 2)); }
                Mi.Set(Mi.M_FANFIX, (byte)(oPct > 0 ? oPct : 100));
                UI(delegate { tBright.Value = Cfg.Clamp((int)Math.Round(oBr * 100.0 / 3.0), 0, 100); });
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
                if (_rainbow && s.HasStates && s.KbMode != 2) { StopRainbow(); _cfg.KbRainbow = false; Log.Write("rainbow: stopped, mode changed by hardware"); }
                if (_custom)
                {
                    int exp = cPower.Checked ? 1 : BaseMode;
                    if (s.Mode >= 0 && s.Mode != exp) { Log.Write("sync: custom left, hardware mode " + s.Mode + " (expected " + exp + ")"); LeaveCustom(); SyncMode(s.Mode); }
                }
                else if (s.Mode >= 0 && s.Mode != _mode) { Log.Write("sync: mode " + _mode + " -> " + s.Mode); SyncMode(s.Mode); }
                if (!s.HasStates) return;
                if (s.FnLock >= 0 && cFnLock.Checked != (s.FnLock == 1)) cFnLock.Checked = s.FnLock == 1;
                if (s.Tp >= 0 && cTp.Checked != (s.Tp == 1)) cTp.Checked = s.Tp == 1;
                if (s.Amb >= 0 && cAmb.Checked != (s.Amb == 1)) cAmb.Checked = s.Amb == 1;
                if (!tBright.Capture && s.KbBright >= 0 && s.KbBright <= 3 && s.KbBright != _hwLvl)
                { _hwLvl = s.KbBright; _vb = VbForLevel(s.KbBright); _kbScale = 1.0; tBright.Value = (int)Math.Round(_vb); }
                if (s.KbMode >= 0) _kbHwMode = s.KbMode;
                if (s.KbMode >= 0 && s.KbBright >= 0 && !cbKb.DroppedDown) { int ki = KbIndex(s.KbMode, _hwLvl); if (cbKb.SelectedIndex != ki) cbKb.SelectedIndex = ki; }
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
            Icon ni = MakeIcon(mx, ModeColor(_custom ? 3 : _mode), mx >= 90);
            _tray.Icon = ni; if (_lastIcon != null) _lastIcon.Dispose(); _lastIcon = ni;
            _tray.Text = Trim63(string.Format("CPU {0}°  GPU {1}°\nКулеры {2}/{3}", s.CpuT, s.GpuT, s.FanC, s.FanG));

            FanTick(s);
        }

        static string Trim63(string t) { return t.Length > 63 ? t.Substring(0, 63) : t; }
        static Color TempColor(int t, int warn, int hot) { return t >= hot ? Color.Tomato : t >= warn ? Color.Orange : Color.FromArgb(230, 233, 238); }

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
            _quit = true; _rbOn = false;
            try { if (_custom) { Mi.Set(Mi.M_FANFIXSW, 0); if (cPower.Checked) Mi.SetCustomPower(false, 0, 0, 0); } } catch { }   // при выходе вернуть вентиляторы автоматике платы и закрыть свои лимиты
            try { SystemEvents.PowerModeChanged -= OnPower; } catch { }
            try { _fn.Dispose(); } catch { }
            try { _tray.Visible = false; _tray.Dispose(); } catch { }
            _cfg.Save();
        }
    }
}
