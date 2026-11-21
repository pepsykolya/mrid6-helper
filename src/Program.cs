using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Mrid6Helper
{
    /// <summary>Диагностика: ТОЛЬКО чтение. Снимает ответы платы на все известные Get-команды.</summary>
    static class Probe
    {
        static readonly string[] Names =
        {
            "08 SystemPerMode (0 баланс/1 произв./2 тихий)", "09 GPUMode (0 гибрид/1 дискр.)", "0A RGBKeyboardStatus", "0B FnLock", "0C TPLock",
            "0D CPU/GPU fan RPM", "0F Ambientlight", "10 RGB mode", "11 RGB color", "12 RGB brightness", "13 AC type (1 TypeC/2 круглый)",
            "14 MaxFanSpeedSwitch", "15 MaxFanSpeed %", "16 CPU thermometer", "17 CPU power state"
        };
        static readonly byte[] Ids = { 8, 9, 10, 11, 12, 13, 15, 16, 17, 18, 19, 20, 21, 22, 23 };

        public static string Report()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("MRID6 Helper — диагностика " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Администратор: " + new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator));
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem"))
                    foreach (ManagementObject o in s.Get()) sb.AppendLine("Машина: " + o["Manufacturer"] + " " + o["Model"]);
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
                    foreach (ManagementObject o in s.Get()) sb.AppendLine("BIOS: " + o["SMBIOSBIOSVersion"]);
            }
            catch (Exception ex) { sb.AppendLine("Win32: " + ex.Message); }
            sb.AppendLine();
            sb.AppendLine("== MICommonInterface.MiInterface (Get 0xFA) ==");
            for (int i = 0; i < Ids.Length; i++)
            {
                byte[] o = Mi.Call(Mi.GET, Ids[i]);
                sb.AppendLine(string.Format("{0,-44} out[0..11] = {1}", Names[i], o == null ? "(нет ответа: " + Mi.LastError + ")" : Mi.Hex(Slice(o, 12))));
            }
            int c, g;
            if (Mi.GetFans(out c, out g)) sb.AppendLine(string.Format("RPM: CPU {0}, GPU {1}", c, g));
            sb.AppendLine("CPU thermometer (EC): " + Mi.GetByte(Mi.M_CPUTEMP));
            sb.AppendLine("ACPI thermal zone: " + Acpi.ThermalZoneC());
            int t, p, ck, ut;
            if (Nvml.Init() && Nvml.Read(out t, out p, out ck, out ut)) sb.AppendLine(string.Format("NVML: {0} °C, {1} Вт, {2} МГц, {3}%", t, p, ck, ut));
            else sb.AppendLine("NVML: недоступен");
            return sb.ToString();
        }

        static byte[] Slice(byte[] b, int n) { byte[] r = new byte[Math.Min(n, b.Length)]; Array.Copy(b, r, r.Length); return r; }
    }

    /// <summary>Лабораторный режим: `--script in.txt out.txt`. Строки: G m | S m b0 b1.. | W ms | R (RPM+температуры).</summary>
    static class Lab
    {
        static volatile bool burnStop;
        public static void Run(string inFile, string outFile)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string raw in File.ReadAllLines(inFile))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    switch (p[0].ToUpperInvariant())
                    {
                        case "G":
                            { byte m = Convert.ToByte(p[1], 16); sb.AppendLine("G " + p[1] + " -> " + Mi.Hex(Cut(Mi.Call(Mi.GET, m), 12))); break; }
                        case "S":
                            {
                                byte m = Convert.ToByte(p[1], 16);
                                byte[] a = new byte[p.Length - 2];
                                for (int i = 2; i < p.Length; i++) a[i - 2] = Convert.ToByte(p[i], 16);
                                byte[] o = Mi.Call(Mi.SET, m, a);
                                sb.AppendLine("S " + line.Substring(2) + " -> " + (o == null ? "FAIL " + Mi.LastError : "ok " + Mi.Hex(Cut(o, 8)))); break;
                            }
                        case "B":
                            {   // нагрузка на все потоки N секунд + средняя частота CPU и максимум температуры
                                int secs = int.Parse(p[1]); burnStop = false;
                                for (int ti = 0; ti < Environment.ProcessorCount; ti++)
                                    new Thread(delegate () { double x = 1; while (!burnStop) { for (int k = 0; k < 100000; k++) x = Math.Sqrt(x + k) + 1; } }) { IsBackground = true }.Start();
                                System.Diagnostics.PerformanceCounter fq = null, pf = null;
                                try { fq = new System.Diagnostics.PerformanceCounter("Processor Information", "Processor Frequency", "_Total"); pf = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Performance", "_Total"); fq.NextValue(); pf.NextValue(); } catch { }
                                List<float> fl = new List<float>(), pl = new List<float>(); int tmax = 0;
                                for (int sec = 0; sec < secs; sec++)
                                {
                                    Thread.Sleep(1000);
                                    if (fq != null) { try { fl.Add(fq.NextValue()); pl.Add(pf.NextValue()); } catch { } }
                                    int ct = Mi.GetByte(Mi.M_CPUTEMP); if (ct > tmax) tmax = ct;
                                }
                                burnStop = true;
                                int skip = Math.Min(4, fl.Count / 2); double sf = 0, sp = 0; int n = 0;
                                for (int q = skip; q < fl.Count; q++) { sf += fl[q]; sp += pl[q]; n++; }
                                sb.AppendLine(string.Format("B {0:HH:mm:ss} нагрузка {1} с: частота {2:0} МГц, производительность {3:0} %, cpuTmax={4}", DateTime.Now, secs, n > 0 ? sf / n : -1, n > 0 ? sp / n : -1, tmax));
                                break;
                            }
                        case "W": Thread.Sleep(int.Parse(p[1])); break;
                        case "R":
                            {
                                int c, g, t, pw, ck, ut; Mi.GetFans(out c, out g);
                                bool ok = Nvml.Init() && Nvml.Read(out t, out pw, out ck, out ut);
                                t = pw = ck = ut = -1; Nvml.Read(out t, out pw, out ck, out ut);
                                sb.AppendLine(string.Format("R {0:HH:mm:ss} rpm cpu={1} gpu={2} | cpuT={3} gpuT={4} gpuW={5}W", DateTime.Now, c, g, Mi.GetByte(Mi.M_CPUTEMP), t, pw)); break;
                            }
                        default: sb.AppendLine("? " + line); break;
                    }
                }
                catch (Exception ex) { sb.AppendLine("ERR " + line + " : " + ex.Message); }
                File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
            }
            File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        }
        static byte[] Cut(byte[] b, int n) { if (b == null) return null; byte[] r = new byte[Math.Min(n, b.Length)]; Array.Copy(b, r, r.Length); return r; }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--autostart" && i + 1 < args.Length)
                {
                    string e = args[i + 1] == "on" ? AutoStart.Install(Application.ExecutablePath) : AutoStart.Remove();
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "mrid6-autostart.txt"), e ?? "ok", Encoding.UTF8);
                    return e == null ? 0 : 1;
                }
                if (args[i] == "--install-open-task") { string e2 = AutoStart.InstallOpenTask(Application.ExecutablePath); File.WriteAllText(Path.Combine(Path.GetTempPath(), "mrid6-opentask.txt"), e2 ?? "ok", Encoding.UTF8); return e2 == null ? 0 : 1; }
                if (args[i] == "--mark-exit") { AutoStart.MarkExit(); return 0; }
                if (args[i] == "--clear-exit") { AutoStart.ClearExitMark(); return 0; }
                if (args[i] == "--script" && i + 2 < args.Length) { Lab.Run(args[i + 1], args[i + 2]); return 0; }
                if (args[i] == "--probe")
                {
                    string path = i + 1 < args.Length ? args[i + 1] : Path.Combine(Path.GetTempPath(), "mrid6-probe.txt");
                    File.WriteAllText(path, Probe.Report(), Encoding.UTF8);
                    return 0;
                }
            }

            bool watchdog = Array.IndexOf(args, "--watchdog") >= 0;
            if (watchdog && AutoStart.ExitMarkedThisBoot()) return 0;   // пользователь закрыл программу умышленно — не воскрешаем до перезагрузки
            if (!watchdog) AutoStart.ClearExitMark();                    // ручной запуск снимает запрет

            bool created;
            using (Mutex m = new Mutex(true, "Local\\Mrid6Helper.Single", out created))
            {
                EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Mrid6Helper.Show");
                if (!created) { show.Set(); return 0; }

                Application.ThreadException += delegate (object o, ThreadExceptionEventArgs e) { Log.Write("UI exception: " + e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate (object o, UnhandledExceptionEventArgs e) { Log.Write("FATAL: " + e.ExceptionObject); };
                Log.Write("start " + string.Join(" ", args));
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                MainForm f = new MainForm();
                f.StartHidden = Array.IndexOf(args, "--tray") >= 0;
                if (Array.IndexOf(args, "--selftest") >= 0 || Array.IndexOf(args, "--shot") >= 0) { f.WindowState = FormWindowState.Minimized; f.ShowInTaskbar = false; }   // служебные прогоны не выскакивают на экран
                int si = Array.IndexOf(args, "--shot"); if (si >= 0 && si + 1 < args.Length) f.ShotPath = args[si + 1];
                int ti = Array.IndexOf(args, "--selftest"); if (ti >= 0 && ti + 1 < args.Length) f.SelfTestPath = args[ti + 1];

                Thread w = new Thread(delegate ()
                {
                    while (show.WaitOne())
                    {
                        try { f.BeginInvoke((Action)f.ShowMain); } catch { }
                    }
                }) { IsBackground = true };
                w.Start();

                Application.Run(f);
            }
            return 0;
        }
    }
}
