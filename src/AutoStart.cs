// Автозапуск и слежение: задача планировщика «при входе + каждую минуту» с политикой «не запускать второй экземпляр».
// Если приложение упало или его убили — через минуту его поднимет задача. Умышленный выход («Выход» в меню трея)
// запоминается на текущую загрузку Windows, и наблюдатель его не воскрешает.
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security;
using System.Text;

namespace Mrid6Helper
{
    static class AutoStart
    {
        public const string TaskName = "Mrid6Helper";

        static string Run(string args, out int code)
        {
            code = -1;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    code = p.ExitCode;
                    return o;
                }
            }
            catch (Exception ex) { return ex.Message; }
        }

        public static bool Exists() { int c; Run("/Query /TN " + TaskName, out c); return c == 0; }

        /// <summary>Описание задачи (XML) — для проверки настроек в самотесте.</summary>
        public static string Xml() { int c; string o = Run("/Query /TN " + TaskName + " /XML", out c); return c == 0 ? o : ""; }

        static string BuildXml(string exe)
        {
            string user = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            return
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
"  <RegistrationInfo><Description>MRID6 Helper: запуск при входе и контроль (раз в минуту поднимает приложение, если оно не запущено)</Description></RegistrationInfo>\r\n" +
"  <Triggers>\r\n" +
"    <LogonTrigger>\r\n" +
"      <Enabled>true</Enabled>\r\n" +
"      <UserId>" + SecurityElement.Escape(user) + "</UserId>\r\n" +
"    </LogonTrigger>\r\n" +
"    <TimeTrigger>\r\n" +
"      <Repetition><Interval>PT1M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition>\r\n" +
"      <StartBoundary>" + DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss") + "</StartBoundary>\r\n" +
"      <Enabled>true</Enabled>\r\n" +
"    </TimeTrigger>\r\n" +
"  </Triggers>\r\n" +
"  <Principals><Principal id=\"Author\"><UserId>" + SecurityElement.Escape(user) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
"  <Settings>\r\n" +
"    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\r\n" +
"    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\r\n" +
"    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\r\n" +
"    <AllowHardTerminate>true</AllowHardTerminate>\r\n" +
"    <StartWhenAvailable>true</StartWhenAvailable>\r\n" +
"    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\r\n" +
"    <AllowStartOnDemand>true</AllowStartOnDemand>\r\n" +
"    <Enabled>true</Enabled>\r\n" +
"    <Hidden>false</Hidden>\r\n" +
"    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\r\n" +
"    <Priority>6</Priority>\r\n" +
"    <RestartOnFailure><Interval>PT1M</Interval><Count>999</Count></RestartOnFailure>\r\n" +
"  </Settings>\r\n" +
"  <Actions Context=\"Author\"><Exec><Command>" + SecurityElement.Escape(exe) + "</Command><Arguments>--tray --watchdog</Arguments></Exec></Actions>\r\n" +
"</Task>\r\n";
        }

        /// <summary>Создать/обновить задачу. Возвращает текст ошибки или null.</summary>
        public static string Install(string exe)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "mrid6-task-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(tmp, BuildXml(exe), Encoding.Unicode);
                int c; string o = Run("/Create /TN " + TaskName + " /XML \"" + tmp + "\" /F", out c);
                return c == 0 ? null : "код " + c + ": " + o.Trim();
            }
            catch (Exception ex) { return ex.Message; }
            finally { try { File.Delete(tmp); } catch { } }
        }

        public static string Remove()
        {
            int c; string o = Run("/Delete /TN " + TaskName + " /F", out c);
            return c == 0 || !Exists() ? null : "код " + c + ": " + o.Trim();
        }

        // ---- умышленный выход: не воскрешать до следующей загрузки Windows
        static string MarkerPath()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mrid6Helper");
            Directory.CreateDirectory(d);
            return Path.Combine(d, "exit-requested.txt");
        }

        public static string BootId()
        {
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                    foreach (ManagementObject o in s.Get()) return Convert.ToString(o["LastBootUpTime"]);
            }
            catch { }
            return "unknown";
        }

        public static void MarkExit() { try { File.WriteAllText(MarkerPath(), BootId()); } catch { } }
        public static void ClearExitMark() { try { File.Delete(MarkerPath()); } catch { } }
        public static bool ExitMarkedThisBoot()
        {
            try { return File.Exists(MarkerPath()) && File.ReadAllText(MarkerPath()).Trim() == BootId(); } catch { return false; }
        }
    }
}
