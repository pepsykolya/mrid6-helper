// Кривая вентиляторов: модель, сглаживающий контроллер и редактор мышью.
// Железо умеет только «фиксированный процент» (команда 0x15), поэтому кривую исполняет приложение.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Mrid6Helper
{
    /// <summary>Шесть точек «температура → обороты, %» с линейной интерполяцией и защитным полом.</summary>
    sealed class FanCurve
    {
        public const int N = 6;
        public const double MinT = 30, MaxT = 100, MinPct = 20, MaxPct = 100;
        public double[] T = new double[N];
        public double[] P = new double[N];

        // Защитный пол: ниже этой линии кривую опустить нельзя (при зависании приложения плата не должна остаться на малых оборотах в жару).
        static readonly double[] FloorT = { 50, 65, 75, 82, 88, 92 };
        static readonly double[] FloorP = { 20, 26, 40, 60, 85, 100 };

        public static readonly string[] PresetNames = { "Баланс", "Тихая ночь", "Прохладнее" };

        public static FanCurve Preset(int i)
        {
            FanCurve c = new FanCurve();
            double[] t, p;
            switch (i)
            {
                case 1:  t = new double[] { 45, 58, 68, 76, 84, 91 }; p = new double[] { 22, 25, 30, 42, 62, 100 }; break;   // тихая ночь
                case 2:  t = new double[] { 40, 50, 60, 70, 80, 88 }; p = new double[] { 30, 38, 48, 62, 85, 100 }; break;   // прохладнее
                default: t = new double[] { 40, 55, 65, 75, 84, 91 }; p = new double[] { 25, 30, 38, 48, 70, 100 }; break;   // баланс
            }
            Array.Copy(t, c.T, N); Array.Copy(p, c.P, N);
            c.Normalize();
            return c;
        }

        public FanCurve Clone() { FanCurve c = new FanCurve(); Array.Copy(T, c.T, N); Array.Copy(P, c.P, N); return c; }

        /// <summary>Обороты по кривой (без защитного пола), линейная интерполяция; вне диапазона — крайние значения.</summary>
        public double Eval(double t)
        {
            if (t <= T[0]) return P[0];
            if (t >= T[N - 1]) return P[N - 1];
            for (int i = 1; i < N; i++)
                if (t <= T[i])
                {
                    double k = (t - T[i - 1]) / Math.Max(0.001, T[i] - T[i - 1]);
                    return P[i - 1] + k * (P[i] - P[i - 1]);
                }
            return P[N - 1];
        }

        /// <summary>Минимально допустимые обороты при данной температуре.</summary>
        public static double Floor(double t)
        {
            if (t <= FloorT[0]) return FloorP[0];
            if (t >= FloorT[FloorT.Length - 1]) return FloorP[FloorP.Length - 1];
            for (int i = 1; i < FloorT.Length; i++)
                if (t <= FloorT[i])
                {
                    double k = (t - FloorT[i - 1]) / (FloorT[i] - FloorT[i - 1]);
                    return FloorP[i - 1] + k * (FloorP[i] - FloorP[i - 1]);
                }
            return 100;
        }

        /// <summary>Привести точки к допустимым: порядок по температуре, зазор ≥ 2 °C, обороты не ниже защитного пола.</summary>
        public void Normalize()
        {
            for (int i = 0; i < N; i++)
            {
                T[i] = Math.Round(Math.Max(MinT, Math.Min(MaxT, T[i])));
                if (i > 0 && T[i] < T[i - 1] + 2) T[i] = T[i - 1] + 2;
            }
            if (T[N - 1] > MaxT) { T[N - 1] = MaxT; for (int i = N - 2; i >= 0 && T[i] > T[i + 1] - 2; i--) T[i] = T[i + 1] - 2; }
            for (int i = 0; i < N; i++)
                P[i] = Math.Max(Math.Ceiling(Math.Max(MinPct, Floor(T[i]))), Math.Min(MaxPct, Math.Round(P[i])));   // пол округляем вверх
        }

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < N; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(T[i].ToString("0", CultureInfo.InvariantCulture)).Append(':').Append(P[i].ToString("0", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        public static FanCurve Parse(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return null;
                string[] parts = s.Split(',');
                if (parts.Length != N) return null;
                FanCurve c = new FanCurve();
                for (int i = 0; i < N; i++)
                {
                    string[] kv = parts[i].Split(':');
                    c.T[i] = double.Parse(kv[0], CultureInfo.InvariantCulture);
                    c.P[i] = double.Parse(kv[1], CultureInfo.InvariantCulture);
                }
                c.Normalize();
                return c;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// Исполнитель кривой. Причина «пульсаций» штатной автоматики — ступенчатая реакция на температуру. Здесь:
    /// температура сглаживается (растёт быстро, спадает медленно), обороты меняются с ограничением скорости
    /// (вверх быстро, вниз очень медленно), а защитный пол и аварийный подъём действуют мгновенно.
    /// </summary>
    sealed class FanController
    {
        public FanCurve Curve = FanCurve.Preset(0);
        public int Smooth = 5;                          // 1 (отзывчиво) … 10 (очень плавно)
        public double SmoothT = double.NaN;             // сглаженная температура
        public double Out;                              // текущие обороты, %
        public double Target;                           // целевые обороты по кривой

        public void Reset() { SmoothT = double.NaN; }

        public int Step(double raw, double dt)
        {
            if (dt < 0.2) dt = 0.2; if (dt > 5) dt = 5;
            if (double.IsNaN(SmoothT)) { SmoothT = raw; Out = Math.Max(Curve.Eval(raw), FanCurve.Floor(raw)); }
            double tau = raw > SmoothT ? 1.0 : 3.0 + 3.0 * Smooth;      // вверх — быстро, вниз — медленно
            SmoothT += (raw - SmoothT) * dt / (tau + dt);

            Target = Math.Max(Curve.Eval(SmoothT), FanCurve.Floor(raw));
            if (raw >= 92) Target = 100;

            double up = 4.0 * dt;                                       // %/с вверх
            double down = Math.Max(0.1, 1.6 - 0.14 * Smooth) * dt;      // %/с вниз (Smooth 5 → 0,9; 10 → 0,2)
            if (Target > Out) Out = Math.Min(Target, Out + up); else Out = Math.Max(Target, Out - down);

            double fl = FanCurve.Floor(raw);                            // защитный пол — мгновенно
            if (Out < fl) Out = fl;
            if (Out > 100) Out = 100;
            return (int)Math.Round(Out);
        }
    }

    /// <summary>Редактор кривой: тянем точки мышью. Красная зона — ниже защитного пола, туда опустить нельзя.</summary>
    sealed class CurveEditor : Control
    {
        static readonly Color Bg2 = Color.FromArgb(32, 36, 45), GridC = Color.FromArgb(55, 60, 72), Accent = Color.FromArgb(0, 168, 232);

        public FanCurve Curve = FanCurve.Preset(0);
        public double CurT = double.NaN, CurP = double.NaN;     // рабочая точка (сглаженная температура и текущие обороты)
        public event EventHandler Changed;                       // кривая изменена мышью (после отпускания)
        int _drag = -1, _hover = -1;

        public CurveEditor()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 200; Cursor = Cursors.Default; BackColor = Bg2;
        }

        Rectangle Plot() { return new Rectangle(36, 10, Math.Max(10, Width - 36 - 12), Math.Max(10, Height - 10 - 26)); }
        PointF Px(double t, double p)
        {
            Rectangle r = Plot();
            return new PointF((float)(r.Left + (t - FanCurve.MinT) / (FanCurve.MaxT - FanCurve.MinT) * r.Width), (float)(r.Bottom - p / 100.0 * r.Height));
        }
        void FromPx(int x, int y, out double t, out double p)
        {
            Rectangle r = Plot();
            t = FanCurve.MinT + (x - r.Left) / (double)r.Width * (FanCurve.MaxT - FanCurve.MinT);
            p = (r.Bottom - y) / (double)r.Height * 100.0;
        }

        /// <summary>Переместить точку с учётом соседей и защитного пола (используется и мышью, и тестом).</summary>
        public void SetPoint(int i, double t, double p)
        {
            double lo = i == 0 ? FanCurve.MinT : Curve.T[i - 1] + 2, hi = i == FanCurve.N - 1 ? FanCurve.MaxT : Curve.T[i + 1] - 2;
            t = Math.Round(Math.Max(lo, Math.Min(hi, t)));
            p = Math.Max(Math.Ceiling(Math.Max(FanCurve.MinPct, FanCurve.Floor(t))), Math.Min(FanCurve.MaxPct, Math.Round(p)));   // пол округляем вверх
            Curve.T[i] = t; Curve.P[i] = p;
            Invalidate();
        }

        int Hit(int x, int y)
        {
            int best = -1; double bd = 13 * 13;
            for (int i = 0; i < FanCurve.N; i++)
            {
                PointF q = Px(Curve.T[i], Curve.P[i]);
                double d = (q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _drag = Hit(e.X, e.Y); Capture = _drag >= 0; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag >= 0) { double t, p; FromPx(e.X, e.Y, out t, out p); SetPoint(_drag, t, p); }
            else { int h = Hit(e.X, e.Y); if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.SizeAll : Cursors.Default; Invalidate(); } }
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_drag >= 0) { _drag = -1; Capture = false; Invalidate(); EventHandler h = Changed; if (h != null) h(this, EventArgs.Empty); }
            base.OnMouseUp(e);
        }
        protected override void OnMouseLeave(EventArgs e) { if (_hover != -1) { _hover = -1; Cursor = Cursors.Default; Invalidate(); } base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Bg2);
            Rectangle r = Plot();
            using (Pen gp = new Pen(GridC))
            using (Font f = new Font("Segoe UI", 7.5f))
            {
                for (int t = 30; t <= 100; t += 10)
                {
                    float x = Px(t, 0).X; g.DrawLine(gp, x, r.Top, x, r.Bottom);
                    g.DrawString(t.ToString(), f, Brushes.Gray, Math.Min(x - 8, Width - 24), r.Bottom + 3);
                }
                for (int p = 0; p <= 100; p += 20)
                {
                    float y = Px(30, p).Y; g.DrawLine(gp, r.Left, y, r.Right, y);
                    g.DrawString(p + "%", f, Brushes.Gray, 2, y - 7);
                }
            }
            // красная зона — ниже защитного пола
            List<PointF> zone = new List<PointF>();
            for (double t = FanCurve.MinT; t <= FanCurve.MaxT; t += 1) zone.Add(Px(t, FanCurve.Floor(t)));
            zone.Add(Px(FanCurve.MaxT, 0)); zone.Add(Px(FanCurve.MinT, 0));
            using (SolidBrush zb = new SolidBrush(Color.FromArgb(46, 220, 60, 60))) g.FillPolygon(zb, zone.ToArray());

            // кривая
            PointF[] pts = new PointF[FanCurve.N];
            for (int i = 0; i < FanCurve.N; i++) pts[i] = Px(Curve.T[i], Curve.P[i]);
            List<PointF> line = new List<PointF>();
            line.Add(Px(FanCurve.MinT, Curve.P[0])); line.AddRange(pts); line.Add(Px(FanCurve.MaxT, Curve.P[FanCurve.N - 1]));
            using (Pen cp = new Pen(Accent, 2.2f)) g.DrawLines(cp, line.ToArray());
            for (int i = 0; i < FanCurve.N; i++)
            {
                bool act = i == _drag || i == _hover;
                float rad = act ? 7f : 5.5f;
                using (SolidBrush b = new SolidBrush(act ? Color.White : Accent)) g.FillEllipse(b, pts[i].X - rad, pts[i].Y - rad, rad * 2, rad * 2);
                using (Pen ep = new Pen(Color.FromArgb(22, 25, 31), 1.5f)) g.DrawEllipse(ep, pts[i].X - rad, pts[i].Y - rad, rad * 2, rad * 2);
            }
            // рабочая точка
            if (!double.IsNaN(CurT) && !double.IsNaN(CurP))
            {
                PointF q = Px(Math.Max(FanCurve.MinT, Math.Min(FanCurve.MaxT, CurT)), CurP);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 220, 90))) g.FillEllipse(b, q.X - 5, q.Y - 5, 10, 10);
                using (Pen p = new Pen(Color.FromArgb(22, 25, 31), 1.5f)) g.DrawEllipse(p, q.X - 5, q.Y - 5, 10, 10);
            }
            // подпись точки при наведении/перетаскивании
            int hi = _drag >= 0 ? _drag : _hover;
            if (hi >= 0)
            {
                string s = Curve.T[hi].ToString("0") + " °C → " + Curve.P[hi].ToString("0") + " %";
                using (Font f = new Font("Segoe UI Semibold", 8.5f))
                {
                    SizeF sz = g.MeasureString(s, f);
                    float x = Math.Min(Width - sz.Width - 4, Math.Max(r.Left, pts[hi].X - sz.Width / 2)), y = Math.Max(2, pts[hi].Y - 26);
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(220, 22, 25, 31))) g.FillRectangle(bb, x - 3, y, sz.Width + 6, sz.Height + 2);
                    g.DrawString(s, f, Brushes.White, x, y + 1);
                }
            }
        }
    }
}
