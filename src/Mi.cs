// MRID6 Helper — доступ к платформенному WMI-интерфейсу MECHREVO Jiaolong MRID6 (Bitland, Insyde).
// Протокол восстановлен чтением штатной программы «蛟龙游戏控制中心» на этой же машине (см. docs/PROTOCOL.md).
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace Mrid6Helper
{
    /// <summary>root\WMI : MICommonInterface.MiInterface(InData[32]) -> OutData[30]</summary>
    static class Mi
    {
        public const byte GET = 0xFA, SET = 0xFB;

        public const byte M_PERF = 8;        // 0 баланс, 1 производительность, 2 тихий
        public const byte M_GPUMODE = 9;     // 0 гибрид, 1 дискретная
        public const byte M_KBSTATUS = 10;
        public const byte M_FNLOCK = 11;
        public const byte M_TPLOCK = 12;
        public const byte M_FANRPM = 13;     // get: cpu = out[4]|out[5]<<8 ; gpu = out[6]|out[7]<<8
        public const byte M_AMBIENT = 15;
        public const byte M_KBMODE = 16;     // 0 выкл, 1 радуга, 2 фикс, 3 пользовательский
        public const byte M_KBCOLOR = 17;    // r,g,b
        public const byte M_KBBRIGHT = 18;   // 0..3
        public const byte M_ACTYPE = 19;
        public const byte M_FANFIXSW = 20;   // 0/1
        public const byte M_FANFIX = 21;     // 0..100 %
        public const byte M_CPUTEMP = 22;
        public const byte M_CPUPOWER = 23;   // set: [0]=состояние (0 выкл,1 вкл,2 SPL,3 SPPT,4 temp wall), [1]=значение

        public const byte P_CLOSE = 0, P_OPEN = 1, P_SPL = 2, P_SPPT = 3, P_WALL = 4;

        static readonly object Lock = new object();
        static ManagementObject _obj;
        public static string LastError = "";
        public static bool Available;

        static readonly string[] PathCandidates =
        {
            @"MICommonInterface.InstanceName='ACPI\PNP0C14\MIFS_0'",
            @"MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'",
        };

        static ManagementObject Obj()
        {
            if (_obj != null) return _obj;
            Exception last = null;
            foreach (string p in PathCandidates)
            {
                try
                {
                    ManagementObject o = new ManagementObject(@"root\WMI", p, null);
                    o.Get();
                    _obj = o;
                    return _obj;
                }
                catch (Exception ex) { last = ex; }
            }
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MICommonInterface"))
                    foreach (ManagementObject o in s.Get()) { _obj = o; return _obj; }
            }
            catch (Exception ex) { last = ex; }
            throw last ?? new InvalidOperationException("MICommonInterface not found");
        }

        /// <summary>Сырой вызов. Возвращает OutData или null.</summary>
        public static byte[] Call(byte type, byte method, params byte[] args)
        {
            byte[] inb = new byte[32];
            inb[1] = type;
            inb[3] = method;
            if (args != null)
                for (int i = 0; i < args.Length && 4 + i < 32; i++) inb[4 + i] = args[i];
            lock (Lock)
            {
                try
                {
                    ManagementObject o = Obj();
                    ManagementBaseObject ip = o.GetMethodParameters("MiInterface");
                    ip["InData"] = inb;
                    ManagementBaseObject r = o.InvokeMethod("MiInterface", ip, null);
                    byte[] outb = r["OutData"] as byte[];
                    Available = true;
                    LastError = "";
                    return outb;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    _obj = null;
                    Available = false;
                    return null;
                }
            }
        }

        public static int GetByte(byte method)
        {
            byte[] o = Call(GET, method);
            return (o != null && o.Length > 4) ? o[4] : -1;
        }

        public static bool GetFans(out int cpu, out int gpu)
        {
            cpu = gpu = -1;
            byte[] o = Call(GET, M_FANRPM);
            if (o == null || o.Length < 8) return false;
            cpu = o[4] | (o[5] << 8);
            gpu = o[6] | (o[7] << 8);
            return true;
        }

        public static bool GetColor(out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            byte[] o = Call(GET, M_KBCOLOR);
            if (o == null || o.Length < 7) return false;
            r = o[4]; g = o[5]; b = o[6];
            return true;
        }

        public static bool Set(byte method, params byte[] args)
        {
            return Call(SET, method, args) != null;
        }

        // ---- удобные обёртки ----
        public static bool SetPerfMode(int mode) { return Set(M_PERF, (byte)mode); }

        /// <summary>Пользовательские лимиты: включить и записать temp wall / SPL / SPPT (как делает штатная программа).</summary>
        public static bool SetCustomPower(bool on, int wall, int spl, int sppt)
        {
            if (!on) return Set(M_CPUPOWER, P_CLOSE);
            bool ok = Set(M_CPUPOWER, P_OPEN);
            ok &= Set(M_CPUPOWER, P_WALL, (byte)wall);
            ok &= Set(M_CPUPOWER, P_SPL, (byte)spl);
            ok &= Set(M_CPUPOWER, P_SPPT, (byte)sppt);
            return ok;
        }

        public static bool SetKbColor(byte r, byte g, byte b)
        {
            bool ok = Set(M_KBMODE, 2);
            System.Threading.Thread.Sleep(500);   // штатная программа ждёт 500 мс между режимом и цветом
            ok &= Set(M_KBCOLOR, r, g, b);
            return ok;
        }

        public static string Hex(byte[] b)
        {
            if (b == null) return "(null)";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < b.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(b[i].ToString("X2")); }
            return sb.ToString();
        }
    }

    /// <summary>События Fn-клавиш: HID_EVENT20.EventDetail = [тип, имя, значение(, значение lo)]</summary>
    sealed class FnEvents : IDisposable
    {
        public const int EV_AIRPLANE = 4, EV_KBBRIGHT = 5, EV_TOUCHPAD = 6, EV_FN = 7, EV_KBMODE = 8, EV_CAPS = 9,
            EV_AMBIENT = 10, EV_NUM = 13, EV_SCROLL = 14, EV_PERF = 15, EV_REFRESH = 25, EV_CPUFAN = 26, EV_GPUFAN = 32, EV_WINLOCK = 33;

        public event Action<int, int, int> Arrived; // name, value, raw type
        ManagementEventWatcher _w;
        int _lastName = -1, _lastVal = -1, _lastTick;   // плата присылает каждое событие дважды — отсекаем дубли

        public bool Start()
        {
            try
            {
                _w = new ManagementEventWatcher(@"root\WMI", "SELECT * FROM HID_EVENT20");
                _w.EventArrived += OnEvent;
                _w.Start();
                return true;
            }
            catch { return false; }
        }

        void OnEvent(object sender, EventArrivedEventArgs e)
        {
            try
            {
                byte[] d = e.NewEvent["EventDetail"] as byte[];
                if (d == null || d.Length < 3) return;
                int type = d[0], name = d[1];
                int val = (name == EV_CPUFAN || name == EV_GPUFAN) ? ((d[2] << 8) + d[3]) : d[2];
                lock (this)
                {
                    int now = Environment.TickCount;
                    if (name == _lastName && val == _lastVal && unchecked(now - _lastTick) < 400) return;
                    _lastName = name; _lastVal = val; _lastTick = now;
                }
                Action<int, int, int> h = Arrived;
                if (h != null) h(name, val, type);
            }
            catch { }
        }

        public void Dispose()
        {
            try { if (_w != null) { _w.Stop(); _w.Dispose(); } } catch { }
        }
    }

    /// <summary>NVML: температура / мощность / частота / загрузка GPU.</summary>
    static class Nvml
    {
        [StructLayout(LayoutKind.Sequential)] struct Util { public uint gpu; public uint memory; }

        [DllImport("nvml.dll")] static extern int nvmlInit_v2();
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr dev);
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetTemperature(IntPtr dev, int sensor, out uint temp);
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetPowerUsage(IntPtr dev, out uint mw);
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetClockInfo(IntPtr dev, int type, out uint mhz);
        [DllImport("nvml.dll")] static extern int nvmlDeviceGetUtilizationRates(IntPtr dev, out Util u);

        static IntPtr _dev;
        static bool _ok;

        public static bool Init()
        {
            try
            {
                if (nvmlInit_v2() != 0) return false;
                if (nvmlDeviceGetHandleByIndex_v2(0, out _dev) != 0) return false;
                _ok = true;
            }
            catch { _ok = false; }
            return _ok;
        }

        public static bool Read(out int tempC, out int powerW, out int clockMhz, out int utilPct)
        {
            tempC = powerW = clockMhz = utilPct = -1;
            if (!_ok) return false;
            try
            {
                uint t, p, c; Util u;
                if (nvmlDeviceGetTemperature(_dev, 0, out t) == 0) tempC = (int)t;
                if (nvmlDeviceGetPowerUsage(_dev, out p) == 0) powerW = (int)(p / 1000);
                if (nvmlDeviceGetClockInfo(_dev, 0, out c) == 0) clockMhz = (int)c;
                if (nvmlDeviceGetUtilizationRates(_dev, out u) == 0) utilPct = (int)u.gpu;
                return tempC >= 0;
            }
            catch { return false; }
        }
    }

    /// <summary>Температура ACPI-зоны (запасной источник температуры CPU).</summary>
    static class Acpi
    {
        public static int ThermalZoneC()
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    int best = -1;
                    foreach (ManagementObject o in s.Get())
                    {
                        int c = (int)Math.Round(Convert.ToDouble(o["CurrentTemperature"]) / 10.0 - 273.15);
                        if (c > best && c < 150) best = c;
                    }
                    return best;
                }
            }
            catch { return -1; }
        }
    }
}
