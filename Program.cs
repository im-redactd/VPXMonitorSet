using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
class Program
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

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

    static void Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("This application only runs on Windows.");
            return;
        }

        isDebug = args.Contains("--debug");

        int targetResolution = 3840;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--resolution" or "-r")
            {
                if (int.TryParse(args[i + 1], out int resolution))
                {
                    targetResolution = resolution;
                }
                break;
            }
        }

        Log($"Starting monitor detection (Target Resolution: {targetResolution})...");

        var displays = EnumerateDisplays();
        int targetIndex = -1;

        for (int i = 0; i < displays.Count; i++)
        {
            var (deviceName, width, height) = displays[i];
            Log($"Monitor {i}: {deviceName} - {width}x{height}");

            if (width == targetResolution)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex != -1)
        {
            Log($"Found display with resolution {targetResolution} at index {targetIndex}");
            UpdateRegistry(targetIndex);
            UpdateIniFile(targetIndex);
        }
        else
        {
            Log($"No display with resolution {targetResolution} found!");
        }

        if (isDebug) Thread.Sleep(3000);
    }

    static List<(string deviceName, uint width, uint height)> EnumerateDisplays()
    {
        var displays = new List<(string, uint, uint)>();
        var device = new DISPLAY_DEVICE();
        var devMode = new DEVMODE();
        device.cb = (uint)Marshal.SizeOf(device);
        devMode.dmSize = (short)Marshal.SizeOf(devMode);

        try
        {
            for (uint id = 0; EnumDisplayDevices(null, id, ref device, 0); id++)
            {
                if ((device.StateFlags & 0x1) != 0) // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
                {
                    if (EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        displays.Add((device.DeviceName, devMode.dmPelsWidth, devMode.dmPelsHeight));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error enumerating displays: {ex.Message}");
            throw;
        }

        if (displays.Count == 0)
        {
            Log("Warning: No displays detected!");
        }

        return displays;
    }

    static void UpdateRegistry(int index)
    {
        if (index < 0)
        {
            throw new ArgumentException("Display index cannot be negative", nameof(index));
        }

        Log($"Updating VPinballX registry with display index: {index}");

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Visual Pinball\VP10\Player");
            key.SetValue("Display", index, RegistryValueKind.DWord);
            Log("Registry update successful");
        }
        catch (Exception ex)
        {
            Log($"Registry update failed: {ex.Message}");
            throw;
        }
    }

    static void UpdateIniFile(int index)
    {
        if (index < 0)
        {
            throw new ArgumentException("Display index cannot be negative", nameof(index));
        }

        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VPinballX",
            "VPinballX.ini"
        );

        Log($"Updating VPinballX config at: {path}");

        try
        {
            var lines = File.Exists(path)
                ? File.ReadAllLines(path).ToList()
                : new List<string>();

            bool foundSection = false;
            bool foundSetting = false;
            int sectionIndex = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim() == "[Player]")
                {
                    foundSection = true;
                    sectionIndex = i;
                    break;
                }
            }

            if (!foundSection)
            {
                lines.Add("[Player]");
                sectionIndex = lines.Count - 1;
            }

            for (int i = sectionIndex + 1; i < lines.Count; i++)
            {
                if (lines[i].Trim().StartsWith("[")) break;
                if (lines[i].StartsWith("Display = "))
                {
                    lines[i] = $"Display = {index}";
                    foundSetting = true;
                    break;
                }
            }

            if (!foundSetting)
            {
                lines.Insert(sectionIndex + 1, $"Display = {index}");
            }

            File.WriteAllLines(path, lines);
            Log("INI file updated successfully");
        }
        catch (Exception ex)
        {
            Log($"INI update failed: {ex.Message}");
            throw;
        }
    }

    static void Log(string message)
    {
        if (isDebug)
            Console.WriteLine(message);
    }
}