using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Text;

[SupportedOSPlatform("windows")]
class Program
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("dxva2.dll", EntryPoint = "GetNumberOfPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitors")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    static bool isDebug;
    static bool isLogging;
    static string logPath = "";

    record DisplayInfo(
        string DeviceName,
        string DeviceString,
        string DeviceID,
        int Index,
        uint Width,
        uint Height,
        string MonitorFriendlyName,
        string MonitorDescription
    );

    private static List<DisplayInfo> monitors = new();
    static void Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("This application only runs on Windows.");
            return;
        }

        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(@"VPXMonitorSet - Configure VPinballX and Future Pinball monitor settings

    Usage: VPXMonitorSet.exe [options]

    Options:
      -h, --help                    Show this help message
      -r, --resolution <value>      Set target resolution to find playfield monitor (default: 3840)
      -pn, --playfield-name <name>  Match playfield monitor by name
  
    Future Pinball Specific Options:
      -b, --backbox <value>         Set Future Pinball backbox monitor resolution
      -bn, --backbox-name <name>    Match Future Pinball backbox monitor by name

    Debug Options:
      --debug                       Enable debug output
      --log                         Enable logging to file

    Examples:
      VPXMonitorSet.exe                          Use default 3840 resolution to find playfield
      VPXMonitorSet.exe -r 1920                  Find playfield monitor with 1920 resolution
      VPXMonitorSet.exe -b 1080 -bn ""INSIGNIA""   Set FP backbox by resolution and name
      VPXMonitorSet.exe -pn ""LG"" -bn ""INSIGNIA""  Find playfield by name, FP backbox by name

    Notes:
      - Resolution and name are used to FIND the correct monitor
      - VPinballX is configured with the found monitor's index number
      - Future Pinball stores both monitor ID and resolution settings
      - If both name and resolution specified, both must match
      - Default resolution (3840) used if not specified
");
            return;
        }

        // Initialize debug and logging flags.
        isDebug = args.Contains("--debug");
        isLogging = args.Contains("--log");

        if (isLogging)
        {
            logPath = Path.Combine(
                Path.GetDirectoryName(AppContext.BaseDirectory) ?? "",
                $"VPXMonitorSet_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            );
        }

        // Default configuration values.
        int playfieldResolution = 3840;
        int? backboxResolution = null;
        string? playfieldNameMatch = null;
        string? backboxNameMatch = null;
        bool hasBackboxConfig = false;
        bool changesMade = false;

        // Parse command line arguments.
        // (Note: We use a simple index-based loop and advance the counter when consuming a value.)
        for (int i = 0; i < args.Length; i++)
        {
            string currentArg = args[i].ToLowerInvariant();
            string? nextArg = (i + 1 < args.Length) ? args[i + 1] : null;

            switch (currentArg)
            {
                case "--debug":
                    isDebug = true;
                    break;

                case "--log":
                    isLogging = true;
                    break;

                case "--resolution":
                case "-r":
                    if (nextArg != null && int.TryParse(nextArg, out int res))
                    {
                        playfieldResolution = res;
                        i++;
                    }
                    else
                    {
                        Log("Error: Invalid or missing value for --resolution");
                    }
                    break;

                case "--backbox":
                case "-b":
                    if (nextArg != null && int.TryParse(nextArg, out int bbRes))
                    {
                        hasBackboxConfig = true;
                        backboxResolution = bbRes;
                        i++;
                    }
                    else
                    {
                        Log("Error: Invalid or missing value for --backbox");
                    }
                    break;

                case "--playfield-name":
                case "-pn":
                    if (nextArg != null)
                    {
                        playfieldNameMatch = nextArg;
                        i++;
                    }
                    else
                    {
                        Log("Error: Missing value for --playfield-name");
                    }
                    break;

                case "--backbox-name":
                case "--bn":
                case "-bn":
                    if (nextArg != null)
                    {
                        hasBackboxConfig = true;
                        backboxNameMatch = nextArg;
                        Log($"Parsed Backbox Name: {backboxNameMatch}");
                        i++;
                    }
                    else
                    {
                        Log("Error: Missing value for --backbox-name");
                    }
                    break;

                default:
                    if (currentArg.StartsWith("-"))
                    {
                        Log($"Error: Unrecognized or unsupported argument '{currentArg}'");
                        return;
                    }
                    break;
            }
        }

        // Log the configuration summary.
        Log("Configuration Summary:");
        Log($"  Debug Mode: {(isDebug ? "ON" : "OFF")}");
        Log($"  Logging: {(isLogging ? "ON" : "OFF")}");
        Log($"  Playfield Resolution: {playfieldResolution}");
        Log($"  Backbox Name: {(backboxNameMatch ?? "None")}");
        Log("");

        // If debug mode is enabled, print extra information.
        if (isDebug)
        {
            Log("Debug: Starting monitor detection...");
            Log($"Debug: Command-line arguments: {string.Join(" ", args)}");
        }

        Log($"Starting monitor detection");
        Log("");

        Log("Configuration Request:");
        Log("  Playfield:");
        Log($"    - Resolution: {playfieldResolution}");
        if (!string.IsNullOrEmpty(playfieldNameMatch))
        {
            Log($"    - Name Match: {playfieldNameMatch}");
        }
        Log("");

        if (hasBackboxConfig)
        {
            Log("  Backbox:");
            if (backboxResolution.HasValue)
            {
                Log($"    - Resolution: {backboxResolution}");
            }
            if (!string.IsNullOrEmpty(backboxNameMatch))
            {
                Log($"    - Name Match: {backboxNameMatch}");
            }
            Log("");
        }

        // (Assuming these helper methods are implemented elsewhere.)
        LogCurrentSettings();
        Log("");

        var displays = EnumerateDisplays();
        Log("");

        // Always find playfield monitor first.
        var playfieldMonitor = FindMonitor(displays, playfieldResolution,
            nameMatch: playfieldNameMatch,
            ignoreResolution: !string.IsNullOrEmpty(playfieldNameMatch) && playfieldResolution == 3840);

        // Then find backbox monitor if configured.
        DisplayInfo? backboxMonitor = null;
        if (hasBackboxConfig)
        {
            backboxMonitor = FindMonitor(displays,
                backboxResolution ?? 0,
                excludeDeviceName: playfieldMonitor?.DeviceName,
                nameMatch: backboxNameMatch,
                ignoreResolution: !backboxResolution.HasValue);
        }

        if (playfieldMonitor == null)
        {
            var criteria = new List<string>();
            if (!string.IsNullOrEmpty(playfieldNameMatch))
                criteria.Add($"name containing '{playfieldNameMatch}'");
            if (playfieldResolution > 0 && (string.IsNullOrEmpty(playfieldNameMatch) || playfieldResolution != 3840))
                criteria.Add($"resolution {playfieldResolution}");

            Log($"Error: No display matching {string.Join(" and ", criteria)} found for playfield");
            return;
        }

        Log("Applying Configurations:");
        Log("  VPinballX Configuration:");
        Log($"    Playfield Monitor Selected:");
        Log($"      - Resolution: {playfieldMonitor.Width}x{playfieldMonitor.Height}");
        Log($"      - Index: {playfieldMonitor.Index}");
        Log($"      - Name: {playfieldMonitor.MonitorFriendlyName}");
        Log($"      - Device: {playfieldMonitor.DeviceName}");

        try
        {
            bool vpxChanged = UpdateRegistry(playfieldMonitor.Index);
            bool iniChanged = UpdateIniFile(playfieldMonitor.Index);

            if (vpxChanged || iniChanged)
            {
                changesMade = true;
            }
        }
        catch (Exception ex)
        {
            Log($"Error updating VPinballX settings: {ex.Message}");
            return;
        }

        if (hasBackboxConfig)
        {
            Log("");
            Log("  Future Pinball Configuration:");
            if (backboxMonitor != null)
            {
                string currentBackboxDevice = GetFuturePinballBackboxDevice() ?? "Not Set";
                Log($"    Current Backbox: {currentBackboxDevice}");
                Log($"    Selected Backbox Monitor:");
                Log($"      - Resolution: {backboxMonitor.Width}x{backboxMonitor.Height}");
                Log($"      - Index: {backboxMonitor.Index}");
                Log($"      - Name: {backboxMonitor.MonitorFriendlyName}");
                Log($"      - Device: {backboxMonitor.DeviceName}");

                try
                {
                    bool fpChanged = UpdateFuturePinballRegistry(playfieldMonitor.DeviceName, backboxMonitor.DeviceName);
                    if (fpChanged)
                    {
                        changesMade = true;
                        Log("    Future Pinball configuration updated successfully");
                    }
                }
                catch (Exception ex)
                {
                    Log($"    Error updating Future Pinball settings: {ex.Message}");
                }
            }
            else
            {
                Log($"    Error: No suitable display found for backbox matching name: {backboxNameMatch}");
            }
        }

        Log("");
        Log($"Process completed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Log(changesMade ? "Changes were applied successfully" : "No changes were necessary");

        // If debugging, pause before exiting so you can see the output.
        if (isDebug)
        {
            Log("Debug: Pausing for 3 seconds...");
            Thread.Sleep(3000);
        }
    }

    static string? GetFuturePinballBackboxDevice()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Future Pinball");
            return key?.GetValue("BackboxMonitorID") as string;
        }
        catch
        {
            return null;
        }
    }

        static DisplayInfo? FindMonitor(List<DisplayInfo> displays, int targetResolution,
        string? excludeDeviceName = null, string? nameMatch = null, bool ignoreResolution = false)
    {
        return displays.FirstOrDefault(d =>
            (ignoreResolution || d.Width == targetResolution) &&
            d.DeviceName != excludeDeviceName &&
            (string.IsNullOrEmpty(nameMatch) ||
             (d.MonitorFriendlyName?.Contains(nameMatch, StringComparison.OrdinalIgnoreCase) ?? false)));
    }

    static List<DisplayInfo> EnumerateDisplays()
    {
        monitors.Clear();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorCallback, IntPtr.Zero);

        foreach (var monitor in monitors)
        {
            Log($"Found Monitor: {monitor.MonitorFriendlyName}");  // This should now show the PowerShell-retrieved name
            Log($"  Description: {monitor.MonitorDescription}");
            Log($"  Device Name: {monitor.DeviceName}");
            Log($"  Device ID: {monitor.DeviceID}");
            Log($"  Resolution: {monitor.Width}x{monitor.Height}");
            Log($"  Index: {monitor.Index}");
            Log($"  Bus Description: {monitor.MonitorFriendlyName}");  // Add this line
            Log("  ---------------------");
        }

        if (monitors.Count == 0)
        {
            Log("Warning: No displays detected!");
        }

        return monitors;
    }

    private static bool MonitorCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
    {
        // Get monitor info
        var monitorInfo = new MONITORINFOEX();
        monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFOEX));
        GetMonitorInfo(hMonitor, ref monitorInfo);

        // Get display device info
        var device = new DISPLAY_DEVICE();
        var devMode = new DEVMODE();
        device.cb = (uint)Marshal.SizeOf(device);
        devMode.dmSize = (short)Marshal.SizeOf(devMode);

        EnumDisplayDevices(null, (uint)monitors.Count, ref device, 0);
        EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode);

        // Get monitor-specific device info (this gets us the monitor's Device ID)
        var monitorDevice = new DISPLAY_DEVICE();
        monitorDevice.cb = (uint)Marshal.SizeOf(monitorDevice);
        EnumDisplayDevices(device.DeviceName, 0, ref monitorDevice, 0);

        string busReportedDescription = MonitorInfo.GetBusReportedDescription(monitorDevice.DeviceID);

        monitors.Add(new DisplayInfo(
            device.DeviceName,
            device.DeviceString,
            monitorDevice.DeviceID,
            monitors.Count,
            devMode.dmPelsWidth,
            devMode.dmPelsHeight,
            busReportedDescription,  // Use the PowerShell-retrieved name
            monitorDevice.DeviceString
        ));

        return true;
    }
    private static string GetMonitorFriendlyName(string deviceID)
    {
        try
        {
            // Convert device ID path to registry path
            string? cleanDeviceID = deviceID.Split('\\')
                .Skip(1)  // Skip PCI
                .Take(3)  // Take VEN, DEV, SUBSYS
                .Aggregate((a, b) => $"{a}\\{b}");

            if (string.IsNullOrEmpty(cleanDeviceID))
                return "Unknown Monitor";

            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{cleanDeviceID}");

            if (key == null)
                return "Unknown Monitor";

            // Get the first child key (usually something like "0")
            string? firstSubKey = key.GetSubKeyNames().FirstOrDefault();
            if (firstSubKey == null)
                return "Unknown Monitor";

            using var subKey = key.OpenSubKey(firstSubKey);
            if (subKey == null)
                return "Unknown Monitor";

            // Get friendly name
            return (subKey.GetValue("DeviceDesc") as string) ?? "Unknown Monitor";
        }
        catch (Exception ex)
        {
            Log($"Error getting monitor friendly name: {ex.Message}");
            return "Unknown Monitor";
        }
    }


static void LogCurrentSettings()
{
    try
    {
        // Choose the correct registry view.
        // Use RegistryView.Registry32 if the keys are in the 32-bit registry,
        // or RegistryView.Registry64 if they are in the 64-bit registry.
        RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);

        // VPinballX Registry
        using (var key = baseKey.OpenSubKey(@"SOFTWARE\Visual Pinball\VP10\Player"))
        {
            if (key == null)
            {
                Log("Error: Could not open the registry key for VPinballX.");
            }
            else
            {
                var display = key.GetValue("Display", null);  // Don't provide default
                Log($"Current VPinballX Display setting: {display}");
            }
        }

        // VPinballX.ini
        string iniPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VPinballX",
            "VPinballX.ini"
        );
        if (File.Exists(iniPath))
        {
            var lines = File.ReadAllLines(iniPath);
            var displayLine = lines.FirstOrDefault(l => l.StartsWith("Display = "));
            Log($"Current VPinballX.ini Display setting: {displayLine}");
        }

        // Future Pinball Settings (adjust registry view if needed)
        using (var fpKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32)
                                        .OpenSubKey(@"SOFTWARE\Future Pinball\GamePlayer"))
        {
            if (fpKey == null)
            {
                Log("Error: Could not open the registry key for Future Pinball.");
            }
            else
            {
                Log($"Current Future Pinball PlayfieldMonitorID: {fpKey.GetValue("PlayfieldMonitorID")}");
                Log($"Current Future Pinball BackboxMonitorID: {fpKey.GetValue("BackboxMonitorID")}");
                Log($"Current Future Pinball Width: {fpKey.GetValue("Width")}");
                Log($"Current Future Pinball Height: {fpKey.GetValue("Height")}");
                Log($"Current Future Pinball SecondMonitorWidth: {fpKey.GetValue("SecondMonitorWidth")}");
                Log($"Current Future Pinball SecondMonitorHeight: {fpKey.GetValue("SecondMonitorHeight")}");
            }
        }
    }
    catch (Exception ex)
    {
        Log($"Error reading settings: {ex.Message}");
    }
}


static bool UpdateRegistry(int index)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Visual Pinball\VP10\Player");
            var currentValue = key.GetValue("Display");

            Log($"  VPinballX Registry:");
            Log($"    Current Value: {currentValue ?? "Not Set"}");
            Log($"    Target Value: {index}");

            if (currentValue is int current && current == index)
            {
                Log("    No change needed");
                return false;
            }

            key.SetValue("Display", index, RegistryValueKind.DWord);
            Log("    Updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Log($"    Failed: {ex.Message}");
            throw;
        }
    }

    static bool UpdateIniFile(int index)
    {
        try
        {
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VPinballX",
                "VPinballX.ini"
            );

            Log($"  VPinballX INI File:");
            Log($"    Path: {configPath}");

            if (!File.Exists(configPath))
            {
                Log("    File not found");
                return false;
            }

            var lines = File.ReadAllLines(configPath);
            bool found = false;
            bool changed = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Display = "))
                {
                    found = true;
                    var currentValue = lines[i].Substring(9).Trim();
                    Log($"    Current Value: {currentValue}");
                    Log($"    Target Value: {index}");

                    if (currentValue != index.ToString())
                    {
                        lines[i] = $"Display = {index}";
                        changed = true;
                        Log("    Updated successfully");
                    }
                    else
                    {
                        Log("    No change needed");
                    }
                    break;
                }
            }

            if (!found)
            {
                Array.Resize(ref lines, lines.Length + 1);
                lines[lines.Length - 1] = $"Display = {index}";
                changed = true;
                Log("    Added new setting");
            }

            if (changed)
            {
                File.WriteAllLines(configPath, lines);
            }

            return changed;
        }
        catch (Exception ex)
        {
            Log($"    Failed: {ex.Message}");
            throw;
        }
    }

    static bool UpdateFuturePinballRegistry(string playfieldDevice, string backboxDevice)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Future Pinball\GamePlayer");
            bool changed = false;

            // Always update playfield
            var currentPlayfield = key.GetValue("PlayfieldMonitorID") as string;
            if (currentPlayfield != playfieldDevice)
            {
                key.SetValue("PlayfieldMonitorID", playfieldDevice);
                changed = true;
                Log($"    Updated PlayfieldMonitorID: {playfieldDevice}");
            }

            // Update backbox if provided
            var currentBackbox = key.GetValue("BackboxMonitorID") as string;
            if (currentBackbox != backboxDevice)
            {
                key.SetValue("BackboxMonitorID", backboxDevice);
                changed = true;
                Log($"    Updated BackboxMonitorID: {backboxDevice}");
            }

            return changed;
        }
        catch (Exception ex)
        {
            Log($"    Failed: {ex.Message}");
            throw;
        }
    }

    static void Log(string message)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        if (isDebug)
            Console.WriteLine(logMessage);

        if (isLogging && !string.IsNullOrEmpty(logPath))
        {
            try
            {
                File.AppendAllText(logPath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }



    public class MonitorInfo
    {

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            IntPtr Enumerator,  // Changed to IntPtr
            IntPtr hwndParent,
            uint Flags
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet,
            uint MemberIndex,
            ref SP_DEVINFO_DATA DeviceInfoData
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDevicePropertyW(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            ref DEVPROPKEY propertyKey,
            out uint propertyType,
            byte[] propertyBuffer,
            uint propertyBufferSize,
            out uint requiredSize,
            uint flags
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId,
            uint DeviceInstanceIdSize,
            out uint RequiredSize
        );

        [DllImport("setupapi.dll")]
        private static extern int SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        public static string GetBusReportedDescription(string deviceID)
        {
            try
            {
                // Extract just the monitor ID part (e.g., "DELA107" from "MONITOR\DELA107\{4d36e96e-e325-11ce-bfc1-08002be10318}\0005")
                string monitorId = deviceID.Split('\\')[1];
                Log($"Searching for monitor ID: {monitorId}");

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"(Get-WMIObject Win32_PnPEntity | where {{$_.DeviceID -like '*{monitorId}*' -and $_.Name -match 'monitor'}}).GetDeviceProperties('DEVPKEY_Device_BusReportedDeviceDesc').DeviceProperties.Data\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Log($"Running PowerShell command with DeviceID filter: *{monitorId}*");

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Log("Failed to start PowerShell process");
                    return "Unknown Monitor";
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Log($"PowerShell error: {error}");
                }

                if (!string.IsNullOrWhiteSpace(output))
                {
                    Log($"Found monitor description: {output.Trim()}");
                    return output.Trim();
                }
                else
                {
                    Log("No output from PowerShell command");
                }
            }
            catch (Exception ex)
            {
                Log($"Error executing PowerShell command: {ex.Message}");
            }

            return "Unknown Monitor";
        }
    }
    }