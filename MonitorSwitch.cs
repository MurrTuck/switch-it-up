// MonitorSwitch - toggles a monitor between two inputs using DDC/CI (VCP code 0x60).
// Build with build.cmd (uses the C# compiler that ships with Windows).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MonitorSwitch
{
    class MonitorInfo
    {
        public string Key;          // stable-ish identifier used to remember the choice
        public string DeviceName;   // \\.\DISPLAY1
        public int Index;           // physical monitor index on that display
        public string Name;         // friendly model name
        public Rectangle Bounds;
        public bool Primary;

        public override string ToString()
        {
            string num = DeviceName.Replace(@"\\.\DISPLAY", "");
            return "Display " + num + ": " + Name + " (" + Bounds.Width + "x" + Bounds.Height +
                   (Primary ? ", main" : "") + ")";
        }
    }

    static class Ddc
    {
        const byte VcpInputSource = 0x60;

        delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, IntPtr rect, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFOEX mi);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplayDevices(string device, uint devNum, ref DISPLAY_DEVICE dd, uint flags);
        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMon, out uint count);
        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMon, uint count, [Out] PHYSICAL_MONITOR[] arr);
        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool DestroyPhysicalMonitors(uint count, [In] PHYSICAL_MONITOR[] arr);
        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMon, byte code, IntPtr type, out uint current, out uint max);
        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool SetVCPFeature(IntPtr hMon, byte code, uint value);

        class Entry
        {
            public MonitorInfo Info;
            public IntPtr Handle;
        }

        // Enumerates every physical monitor, hands the list to 'use', then releases the handles.
        static T WithMonitors<T>(Func<List<Entry>, T> use)
        {
            List<Entry> entries = new List<Entry>();
            List<PHYSICAL_MONITOR[]> toFree = new List<PHYSICAL_MONITOR[]>();
            Dictionary<string, string> friendly = null;

            MonitorEnumProc cb = delegate(IntPtr hMon, IntPtr hdc, IntPtr rect, IntPtr data)
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                if (!GetMonitorInfo(hMon, ref mi)) return true;

                uint count;
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) || count == 0) return true;
                PHYSICAL_MONITOR[] arr = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMon, count, arr)) return true;
                toFree.Add(arr);

                for (int i = 0; i < arr.Length; i++)
                {
                    MonitorInfo info = new MonitorInfo();
                    info.DeviceName = mi.szDevice;
                    info.Index = i;
                    info.Bounds = Rectangle.FromLTRB(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                    info.Primary = (mi.dwFlags & 1) != 0;
                    info.Name = arr[i].szDescription;
                    info.Key = mi.szDevice + "#" + i;

                    DISPLAY_DEVICE dd = new DISPLAY_DEVICE();
                    dd.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
                    // flag 1 = EDD_GET_DEVICE_INTERFACE_NAME
                    if (EnumDisplayDevices(mi.szDevice, (uint)i, ref dd, 1) && !string.IsNullOrEmpty(dd.DeviceID))
                    {
                        info.Key = dd.DeviceID;
                        if (friendly == null) friendly = LoadFriendlyNames();
                        string inst = InterfaceToInstance(dd.DeviceID);
                        foreach (KeyValuePair<string, string> kv in friendly)
                        {
                            if (kv.Key.StartsWith(inst, StringComparison.OrdinalIgnoreCase))
                            {
                                info.Name = kv.Value;
                                break;
                            }
                        }
                    }

                    Entry e = new Entry();
                    e.Info = info;
                    e.Handle = arr[i].hPhysicalMonitor;
                    entries.Add(e);
                }
                return true;
            };

            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, cb, IntPtr.Zero);
                GC.KeepAlive(cb);
                return use(entries);
            }
            finally
            {
                foreach (PHYSICAL_MONITOR[] arr in toFree) DestroyPhysicalMonitors((uint)arr.Length, arr);
            }
        }

        // "\\?\DISPLAY#DELA0EC#5&abc&0&UID4352#{guid}" -> "DISPLAY\DELA0EC\5&abc&0&UID4352"
        static string InterfaceToInstance(string id)
        {
            string s = id;
            if (s.StartsWith(@"\\?\")) s = s.Substring(4);
            int brace = s.IndexOf("#{");
            if (brace >= 0) s = s.Substring(0, brace);
            return s.Replace('#', '\\');
        }

        // Model names as reported by the monitors themselves (EDID), keyed by PnP instance name.
        static Dictionary<string, string> LoadFriendlyNames()
        {
            Dictionary<string, string> map = new Dictionary<string, string>();
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID"))
                {
                    foreach (ManagementObject mo in s.Get())
                    {
                        string inst = mo["InstanceName"] as string;
                        ushort[] chars = mo["UserFriendlyName"] as ushort[];
                        if (inst == null || chars == null) continue;
                        string name = "";
                        foreach (ushort c in chars) { if (c == 0) break; name += (char)c; }
                        name = name.Trim();
                        if (name.Length > 0) map[inst] = name;
                    }
                }
            }
            catch { }
            return map;
        }

        public static List<MonitorInfo> List()
        {
            return WithMonitors(delegate(List<Entry> entries)
            {
                List<MonitorInfo> list = new List<MonitorInfo>();
                foreach (Entry e in entries) list.Add(e.Info);
                return list;
            });
        }

        static Entry Find(List<Entry> entries, string key)
        {
            foreach (Entry e in entries)
                if (string.Equals(e.Info.Key, key, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        // Returns the current input value, -1 if the monitor did not answer, -2 if it was not found.
        public static int GetInput(string key)
        {
            return WithMonitors(delegate(List<Entry> entries)
            {
                Entry e = Find(entries, key);
                if (e == null) return -2;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    uint cur, max;
                    if (GetVCPFeatureAndVCPFeatureReply(e.Handle, VcpInputSource, IntPtr.Zero, out cur, out max))
                        return (int)(cur & 0xFF);
                    Thread.Sleep(80);
                }
                return -1;
            });
        }

        // Returns 0 on success, -1 if the monitor rejected the command, -2 if it was not found.
        public static int SetInput(string key, int value)
        {
            return WithMonitors(delegate(List<Entry> entries)
            {
                Entry e = Find(entries, key);
                if (e == null) return -2;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (SetVCPFeature(e.Handle, VcpInputSource, (uint)value)) return 0;
                    Thread.Sleep(80);
                }
                return -1;
            });
        }
    }

    static class Inputs
    {
        // Standard MCCS input source values. Some monitors use their own numbers,
        // which can be typed straight into the input boxes.
        public static readonly int[] Values = { 15, 16, 17, 18, 27, 3, 4, 1 };
        public static readonly string[] Names = { "DisplayPort 1", "DisplayPort 2", "HDMI 1", "HDMI 2", "USB-C", "DVI 1", "DVI 2", "VGA" };

        public static string Label(int value)
        {
            int i = Array.IndexOf(Values, value);
            return i >= 0 ? Names[i] + " (" + value + ")" : "Input " + value;
        }

        public static string ShortName(int value)
        {
            int i = Array.IndexOf(Values, value);
            return i >= 0 ? Names[i] : "input " + value;
        }

        // Accepts "HDMI 1 (17)", "17" or "0x11".
        public static bool TryParse(string text, out int value)
        {
            value = 0;
            if (text == null) return false;
            string t = text.Trim();
            int open = t.LastIndexOf('(');
            int close = t.LastIndexOf(')');
            if (open >= 0 && close > open) t = t.Substring(open + 1, close - open - 1).Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(t.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value) && value > 0 && value < 256;
            return int.TryParse(t, out value) && value > 0 && value < 256;
        }
    }

    class Settings
    {
        public string MonitorKey = "";
        public int InputA = 15;   // DisplayPort 1
        public int InputB = 17;   // HDMI 1
        public int LastSet = 0;
        public bool TopMost = false;
        public int HotkeyMods = 3;      // MOD_ALT | MOD_CONTROL
        public int HotkeyKey = 0x53;    // S  (0 = no shortcut)

        static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "MonitorSwitch", "settings.txt");
            }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                    int n;
                    if (k == "monitor") s.MonitorKey = v;
                    else if (k == "inputA" && int.TryParse(v, out n)) s.InputA = n;
                    else if (k == "inputB" && int.TryParse(v, out n)) s.InputB = n;
                    else if (k == "lastSet" && int.TryParse(v, out n)) s.LastSet = n;
                    else if (k == "topMost") s.TopMost = v == "1";
                    else if (k == "hotkeyMods" && int.TryParse(v, out n)) s.HotkeyMods = n;
                    else if (k == "hotkeyKey" && int.TryParse(v, out n)) s.HotkeyKey = n;
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new string[] {
                    "monitor=" + MonitorKey,
                    "inputA=" + InputA,
                    "inputB=" + InputB,
                    "lastSet=" + LastSet,
                    "topMost=" + (TopMost ? "1" : "0"),
                    "hotkeyMods=" + HotkeyMods,
                    "hotkeyKey=" + HotkeyKey
                });
            }
            catch { }
        }
    }

    static class Switcher
    {
        // Flips the chosen monitor to the other input. Returns a message for the user.
        public static string Toggle(Settings s, out bool ok)
        {
            ok = false;
            if (string.IsNullOrEmpty(s.MonitorKey)) return "Pick a monitor first.";

            int current = Ddc.GetInput(s.MonitorKey);
            if (current == -2) return "That monitor isn't visible to Windows right now.";

            int target;
            if (current == s.InputA) target = s.InputB;
            else if (current == s.InputB) target = s.InputA;
            else if (current == -1) target = (s.LastSet == s.InputB) ? s.InputA : s.InputB; // monitor won't report; go by memory
            else target = s.InputA;

            int result = Ddc.SetInput(s.MonitorKey, target);
            if (result == -2) return "That monitor isn't visible to Windows right now.";
            if (result != 0) return "The monitor didn't accept the command. Check that DDC/CI is turned on in its menu.";

            s.LastSet = target;
            s.Save();
            ok = true;
            return "Switched to " + Inputs.ShortName(target) + ".";
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            // "MonitorSwitch.exe --toggle" switches immediately without showing the window.
            if (args.Length > 0 && args[0] == "--toggle")
            {
                bool ok;
                Switcher.Toggle(Settings.Load(), out ok);
                return ok ? 0 : 1;
            }
            // "--list" prints what the app can see (only visible in a console build).
            if (args.Length > 0 && args[0] == "--list")
            {
                foreach (MonitorInfo m in Ddc.List())
                {
                    int cur = Ddc.GetInput(m.Key);
                    Console.WriteLine(m + " | input=" + cur + " | key=" + m.Key);
                }
                return 0;
            }

            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
