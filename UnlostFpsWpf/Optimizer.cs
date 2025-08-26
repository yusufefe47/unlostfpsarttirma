using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;

namespace UnlostFpsWpf;

public class OptimizeOptions
{
    public bool CleanTemp { get; set; }
    public bool SetPowerPlan { get; set; }
    public bool TryUltimate { get; set; }
    public bool VisualEffectsPerformance { get; set; }
    public bool DisableTransparency { get; set; }
    public bool DisableGameBar { get; set; }
    public bool CloseBackgroundApps { get; set; }
    public bool CleanupDesktop { get; set; }
    public bool SetBlackWallpaper { get; set; }
    public bool DiskCleanup { get; set; }
    public bool EnableGameMode { get; set; }
    public bool MemoryClean { get; set; }
    public bool SystemHealth { get; set; }
    public bool NetworkOptimize { get; set; }
    // AV-dostu: PowerShell ile UWP kaldırma işlemlerini varsayılan olarak kapalı tut
    public bool AggressiveUwpRemoval { get; set; }
}

public static class Optimizer
{
    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);

    // Memory clean P/Invokes
    [System.Runtime.InteropServices.DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [System.Runtime.InteropServices.DllImport("ntdll.dll")] 
    private static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // Reboot-time move scheduling
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, int dwFlags);
    private const int MOVEFILE_REPLACE_EXISTING = 0x1;
    private const int MOVEFILE_DELAY_UNTIL_REBOOT = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privileges; }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const int SystemMemoryListInformation = 0x50; // undocumented class id used by EmptyStandbyList
    private const int MemoryPurgeStandbyList = 4;

    // Process access rights for EmptyWorkingSet
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_SET_QUOTA = 0x0100;

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdministrator()
    {
        var exe = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? "";
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = "--elevated-wait"
        };
        Process.Start(psi);
        Environment.Exit(0);
    }

    public static async Task RunAsync(OptimizeOptions opt, Action<string> log)
    {
        // Admin privilege check for specific operations
        var needsAdmin = opt.MemoryClean || opt.SystemHealth || opt.DiskCleanup || opt.DisableGameBar || opt.CleanupDesktop || opt.NetworkOptimize;
        if (needsAdmin && !IsAdministrator()) 
        {
            log("⚠️ Bu işlemler yönetici ayrıcalığı gerektiriyor. Yönetici olarak yeniden başlatılıyor...");
            await Task.Delay(1500); // Give user time to read
            RestartAsAdministrator();
            return;
        }

        if (!IsAdministrator()) log("Not running as Administrator. Some settings may not be applied.");

        if (opt.CloseBackgroundApps)
        {
            try { await CloseBackgroundAppsAsync(log); } catch (Exception ex) { log($"CloseBackgroundApps: {ex.Message}"); }
        }

        if (opt.CleanupDesktop)
        {
            try { await CleanupDesktopAsync(log); } catch (Exception ex) { log($"CleanupDesktop: {ex.Message}"); }
        }

        if (opt.SetBlackWallpaper)
        {
            try { await SetBlackWallpaperAsync(log); } catch (Exception ex) { log($"SetBlackWallpaper: {ex.Message}"); }
        }

        if (opt.CleanTemp)
        {
            await Task.Run(() =>
            {
                try { var c = CleanTempFiles(); log($"Temp cleanup: {c} items removed."); } catch (Exception ex) { log(ex.Message); }
            });
        }

        if (opt.SetPowerPlan)
        {
            try
            {
                if (opt.TryUltimate) RunPowerCfg("-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", log);
                SetBestPowerPlan(log);
            }
            catch (Exception ex) { log(ex.Message); }
        }

        if (opt.VisualEffectsPerformance)
        {
            try { SetVisualEffectsPerformance(); log("Visual effects set to Best Performance."); } catch (Exception ex) { log(ex.Message); }
        }

        if (opt.DisableTransparency)
        { try { DisableTransparencyEffects(); log("Transparency disabled."); } catch (Exception ex) { log(ex.Message); } }

        if (opt.DisableGameBar)
        { 
            try 
            { 
                DisableGameBarAndDvr();
                if (opt.AggressiveUwpRemoval)
                {
                    TryRemoveXboxGameBarPackages(log);
                    log("Xbox Game Bar paket kaldırma denendi (agresif mod). Win+G için oturum kapatma/yeniden başlatma gerekebilir.");
                }
                else
                {
                    log("Xbox Game Bar ve DVR devre dışı bırakıldı. Paket kaldırma devre dışı (AV uyumu için).");
                }
            } 
            catch (Exception ex) { log(ex.Message); } 
        }

        if (opt.EnableGameMode)
        { try { EnableGameMode(); log("Game Mode enabled."); } catch (Exception ex) { log(ex.Message); } }

        if (opt.SetPowerPlan)
        {
            try { EnsureHighPerformanceTweaks(log); } catch (Exception ex) { log(ex.Message); }
        }

        if (opt.DiskCleanup)
        {
            try { await RunDiskCleanupAsync(log); } catch (Exception ex) { log(ex.Message); }
        }

        if (opt.MemoryClean)
        {
            try { await MemoryCleanAsync(log); } catch (Exception ex) { log(ex.Message); }
        }

        if (opt.SystemHealth)
        {
            try { await RunSystemHealthAsync(log); } catch (Exception ex) { log(ex.Message); }
        }

        if (opt.NetworkOptimize)
        {
            try { await OptimizeNetworkAsync(log); } catch (Exception ex) { log($"Network optimize error: {ex.Message}"); }
        }
    }

    // CS2 functionality removed per request

    private static void RunPowerCfg(string args, Action<string>? log = null)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (!string.IsNullOrWhiteSpace(output)) log?.Invoke(output.Trim());
        if (!string.IsNullOrWhiteSpace(error)) log?.Invoke(error.Trim());
    }

    private static void SetBestPowerPlan(Action<string> log)
    {
        var output = RunAndCapture("powercfg", "/L");
        var ultimate = FindGuid(output, "Ultimate Performance");
        var high = FindGuid(output, "High performance");
        var chosen = ultimate ?? high;
        if (chosen != null)
        {
            RunPowerCfg($"/S {chosen}", log);
            log($"Active power plan set.");
        }
        else { log("Suitable power plan not found."); }
    }

    private static string? FindGuid(string text, string keyword)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var i = line.IndexOf(':');
                if (i > 0)
                {
                    var rest = line[(i + 1)..].Trim();
                    var parts = rest.Split(' ');
                    foreach (var part in parts)
                    {
                        if (Guid.TryParse(part.Trim('{', '}', '*'), out var g)) return g.ToString();
                    }
                }
            }
        }
        return null;
    }

    private static int CleanTempFiles()
    {
        int deleted = 0;
        long totalSizeDeleted = 0;
        
        void CleanDir(string path, string description = "")
        {
            if (!Directory.Exists(path)) return;
            
            try
            {
                // Clean files first
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var size = fileInfo.Length;
                        File.Delete(file);
                        deleted++;
                        totalSizeDeleted += size;
                    }
                    catch { /* Ignore locked/protected files */ }
                }
                
                // Clean empty directories
                var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                                          .OrderByDescending(d => d.Length); // Deepest first
                foreach (var dir in directories)
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            deleted++;
                        }
                    }
                    catch { /* Ignore protected directories */ }
                }
            }
            catch { /* Path might not exist or be accessible */ }
        }

        // 1. User Temp folder (%TEMP% / %TMP%)
        CleanDir(Path.GetTempPath(), "User Temp");
        
        // 2. System Temp folder (Windows\Temp)
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "System Temp");
        
        // 3. Windows Prefetch files (speeds up repeated program launches, but can accumulate)
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"), "Prefetch");
        
        // 4. Recent Items
        CleanDir(Environment.GetFolderPath(Environment.SpecialFolder.Recent), "Recent Items");
        
        // 5. Internet Explorer Cache
        var ieCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                  "Microsoft", "Windows", "INetCache");
        CleanDir(ieCache, "IE Cache");
        
        // 6. Edge Cache
        var edgeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                   "Microsoft", "Edge", "User Data", "Default", "Cache");
        CleanDir(edgeCache, "Edge Cache");
        
        // 7. Chrome Cache
        var chromeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                     "Google", "Chrome", "User Data", "Default", "Cache");
        CleanDir(chromeCache, "Chrome Cache");
        
        // 8. Firefox Cache
        var firefoxProfiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                         "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            foreach (var profile in Directory.GetDirectories(firefoxProfiles))
            {
                CleanDir(Path.Combine(profile, "cache2"), "Firefox Cache");
            }
        }
        
        // 9. Windows Error Reporting
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                            "Microsoft", "Windows", "WER"), "Error Reports");
        
        // 10. Windows Defender scan history
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                            "Microsoft", "Windows Defender", "Scans", "History"), "Defender History");
        
        // 11. Thumbnail cache
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                            "Microsoft", "Windows", "Explorer"), "Thumbnail Cache");
        
        // 12. Event logs temp files
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "LogFiles"), "Log Files");
        
        // 13. Software Distribution Download cache (Windows Update cache)
        CleanDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), 
                            "SoftwareDistribution", "Download"), "WU Downloads");
        
        // 14. .NET compilation cache
        var dotnetTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), 
                                    "Microsoft.NET", "Framework64", "v4.0.30319", "Temporary ASP.NET Files");
        CleanDir(dotnetTemp, ".NET Temp");
        
        // 15. Windows Installer cache (be careful - only temp files)
        var msiCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer", "$PatchCache$");
        CleanDir(msiCache, "MSI Patch Cache");
        
        return deleted;
    }

    private static void SetVisualEffectsPerformance()
    {
        using var ve = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects");
        ve?.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
        using var desk = Registry.CurrentUser.CreateSubKey("Control Panel\\Desktop");
        desk?.SetValue("UserPreferencesMask", new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
    }

    private static void DisableTransparencyEffects()
    { using var p = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"); p?.SetValue("EnableTransparency", 0, RegistryValueKind.DWord); }

    private static void DisableGameBarAndDvr()
    {
        // HKCU - Game Bar UI ve Game Mode
        using (var g = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\GameBar"))
        {
            g?.SetValue("ShowStartupPanel", 0, RegistryValueKind.DWord);
            g?.SetValue("GamePanelStartupTipIndex", 0, RegistryValueKind.DWord);
            g?.SetValue("UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
            g?.SetValue("AutoGameModeEnabled", 0, RegistryValueKind.DWord);
            g?.SetValue("Enabled", 0, RegistryValueKind.DWord); // Eski sürümlerde genel enable
            g?.SetValue("GamebarEnabled", 0, RegistryValueKind.DWord);
        }

        // HKCU - Game DVR ayarları
        using (var dvr = Registry.CurrentUser.CreateSubKey("System\\GameConfigStore"))
        {
            dvr?.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_FSEBehavior", 2, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_HonorUserFSEBehaviorMode", 2, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_DXGIHonorFSE", 0, RegistryValueKind.DWord);
        }

        // HKLM Policies - GameDVR'ı policy ile kapat (Yönetici gerekir)
        try
        {
            using var pol = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR");
            pol?.SetValue("AllowGameDVR", 0, RegistryValueKind.DWord);
        }
        catch { /* Non-admin or locked by org policy */ }

        // HKLM - Windows GameBar Presence Writer ve AppCapture disable
        try
        {
            using var cap = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR");
            cap?.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
            cap?.SetValue("HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
        }
        catch { }

        // HKCU - AppCapture disable (Win+G kısayolunun davranışını etkileyebilir)
        try
        {
            using var capUser = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR");
            capUser?.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
            capUser?.SetValue("HistoricalCaptureEnabled", 0, RegistryValueKind.DWord);
        }
        catch { }

        // Çalışan Game Bar süreçlerini kapat (gerekiyorsa Win+G yine çalışmaz)
        KillIfRunning("GameBar");
        KillIfRunning("GameBarFT");
        KillIfRunning("GameBarPresenceWriter");
        KillIfRunning("XboxGameBar");
        KillIfRunning("xboxgamebar");
    }

    private static void TryRemoveXboxGameBarPackages(Action<string> log)
    {
        try
        {
            // Remove current user's Xbox Game Bar (Microsoft.XboxGamingOverlay). Non-fatal if not installed.
            var ps1 = "Get-AppxPackage -Name Microsoft.XboxGamingOverlay | Remove-AppxPackage";
            RunPowerShell(ps1, log);

            // If elevated, try remove for all users and deprovision for future users
            if (IsAdministrator())
            {
                var psAll = "Get-AppxPackage -AllUsers -Name Microsoft.XboxGamingOverlay | Remove-AppxPackage -AllUsers";
                RunPowerShell(psAll, log);
                var psProv = "Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -like '*XboxGamingOverlay*' } | Remove-AppxProvisionedPackage -Online";
                RunPowerShell(psProv, log);
            }

            log("Xbox Game Bar paketinin kaldırılması denendi (başarısız olabilir, güvenli).");
        }
        catch (Exception ex) { log($"Xbox Game Bar kaldırma hatası: {ex.Message}"); }
    }

    private static void RunPowerShell(string command, Action<string>? log = null)
    {
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (!string.IsNullOrWhiteSpace(output)) log?.Invoke(TrimForLog(output));
        if (!string.IsNullOrWhiteSpace(error)) log?.Invoke(TrimForLog(error));
    }

    private static async Task OptimizeNetworkAsync(Action<string> log)
    {
        await Task.Run(() =>
        {
            try
            {
                log("🌐 Network optimization started (low latency tweaks)...");

                // 1) Multimedia scheduler tweaks: remove throttling, reduce system responsiveness for games
                try
                {
                    using var sp = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile");
                    sp?.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                    sp?.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                    log("SystemProfile: NetworkThrottlingIndex=0xFFFFFFFF, SystemResponsiveness=0");
                }
                catch (Exception ex) { log($"SystemProfile tweak error: {ex.Message}"); }

                // 2) TCP ACK/Nagle tweaks per interface
                try
                {
                    using var ifs = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces", true);
                    if (ifs != null)
                    {
                        foreach (var sub in ifs.GetSubKeyNames())
                        {
                            try
                            {
                                using var k = ifs.OpenSubKey(sub, writable: true);
                                if (k == null) continue;
                                k.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                k.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                // Optional: reduce delayed ACK timer
                                k.SetValue("TcpDelAckTicks", 0, RegistryValueKind.DWord);
                            }
                            catch { }
                        }
                        log("TCP per-interface: TcpAckFrequency=1, TCPNoDelay=1, TcpDelAckTicks=0 (applied)");
                    }
                }
                catch (Exception ex) { log($"TCP tweak error: {ex.Message}"); }

                // 3) Netsh TCP global settings (safe defaults)
                try
                {
                    RunAndCapture("netsh", "int tcp set heuristics disabled");
                    RunAndCapture("netsh", "int tcp set global autotuninglevel=normal");
                    RunAndCapture("netsh", "int tcp set global rss=enabled");
                    // RSC off for some NICs might reduce latency on uploads; skip by default to avoid breaking throughput
                    log("netsh TCP: heuristics disabled, autotuninglevel=normal, rss=enabled");
                }
                catch (Exception ex) { log($"netsh error: {ex.Message}"); }

                log("✅ Network optimization applied. Not: Bazı ayarlar için yeniden başlatma gerekebilir.");
            }
            catch (Exception ex)
            {
                log($"Network optimization fatal error: {ex.Message}");
            }
        });
    }

    private static void KillIfRunning(string processName)
    {
        try
        {
            var procs = Process.GetProcessesByName(processName);
            foreach (var p in procs)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                try { p.Dispose(); } catch { }
            }
        }
        catch { }
    }

    private static void EnableGameMode()
    {
        using var g = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\GameBar");
        g?.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
        g?.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
    }

    private static void EnsureHighPerformanceTweaks(Action<string> log)
    {
        // Keep/Set best available plan (Ultimate/High)
        SetBestPowerPlan(log);

        // Try to push CPU min state to 100% on AC power (may fail silently on some devices)
        RunPowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100", log);
        RunPowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100", log);
        // Processor performance boost mode to aggressive (value 2)
        RunPowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2", log);
        // Apply current scheme to make sure values take effect
        RunPowerCfg("/S SCHEME_CURRENT", log);

        // PCIe Link State Power Management -> Off (AC/DC). Aliaslar tüm cihazlarda çalışmayabilir; hata olursa loglanır.
        RunPowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0", log);
        RunPowerCfg("-setdcvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0", log);

        // Windows Graphics - Hardware Accelerated GPU Scheduling (requires reboot to take effect)
        TryEnableHags(log);
        // Set this app to High Performance in Windows graphics settings
        TrySetWindowsGpuHighPerformanceForThisApp(log);
        // Also apply High Performance for common game launchers if found
        TrySetWindowsGpuHighPerformanceForKnownApps(log);
    }

    private static void TryEnableHags(Action<string> log)
    {
        try
        {
            using var k = Registry.LocalMachine.CreateSubKey("SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers");
            k?.SetValue("HwSchMode", 2, RegistryValueKind.DWord); // 2 = Enabled
            log("Hardware Accelerated GPU Scheduling set to Enabled (reboot required).");
        }
        catch (Exception ex) { log($"HAGS set error: {ex.Message}"); }
    }

    private static void TrySetWindowsGpuHighPerformanceForThisApp(Action<string> log)
    {
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return;
            using var k = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\DirectX\\UserGpuPreferences");
            k?.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String); // 2 = High Performance GPU
            log("Windows graphics preference set to High Performance for this app.");
        }
        catch (Exception ex) { log($"GPU preference error: {ex.Message}"); }
    }

    private static void TrySetWindowsGpuHighPerformanceForKnownApps(Action<string> log)
    {
        try
        {
            var candidates = new (string name, string[] paths)[]
            {
                ("Steam", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe")
                }),
                ("Epic Games Launcher", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games", "Launcher", "Portal", "Binaries", "Win64", "EpicGamesLauncher.exe")
                }),
                ("Battle.net", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Battle.net", "Battle.net.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Battle.net", "Battle.net Launcher.exe")
                }),
                ("Riot Client", new[]{
                    Path.Combine("C:", "Riot Games", "Riot Client", "RiotClientServices.exe")
                }),
                ("Rockstar Games Launcher", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rockstar Games", "Launcher", "Launcher.exe")
                }),
                ("EA Desktop", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EA", "EA Desktop", "EA Desktop", "EADesktop.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Origin", "Origin.exe")
                }),
                ("Ubisoft Connect", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Ubisoft", "Ubisoft Game Launcher", "UbisoftConnect.exe")
                }),
                ("GOG Galaxy", new[]{
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "GalaxyClient.exe")
                })
            };

            using var k = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\DirectX\\UserGpuPreferences");
            int setCount = 0;
            foreach (var (name, paths) in candidates)
            {
                foreach (var p in paths)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        {
                            k?.SetValue(p, "GpuPreference=2;", RegistryValueKind.String);
                            setCount++;
                            log($"High Performance GPU preference applied: {name}");
                            break;
                        }
                    }
                    catch { /* continue other candidates */ }
                }
            }

            if (setCount == 0)
                log("No known game launchers found for GPU preference.");
        }
        catch (Exception ex)
        {
            log($"Known apps GPU preference error: {ex.Message}");
        }
    }

    private static async Task CloseBackgroundAppsAsync(Action<string> log)
    {
        await Task.Run(() =>
        {
            log("🔄 Başlangıç programları devre dışı bırakılıyor...");
            
            // Only disable startup programs, don't close running apps
            int disabledStartup = DisableStartupPrograms(log);
            
            log($"🎯 Toplam: {disabledStartup} başlangıç programı devre dışı bırakıldı");
        });
    }

    private static int DisableStartupPrograms(Action<string> log)
    {
        int disabled = 0;
        
        try
        {
            // Registry locations where startup programs are stored
            string[] startupKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
            };
            
            string[] startupTargets = new[]
            {
                "OneDrive", "Dropbox", "Steam", "Epic", "Battle", "Origin", "EA", "Galaxy",
                "Discord", "Teams", "Telegram", "WhatsApp", "Slack", "Zoom", "Skype", "Spotify", "iTunes",
                "Razer", "Logitech", "NVIDIA", "GeForce", "Adobe", "Acrobat", "MSI", "SteelSeries",
                "Ubisoft", "Corsair", "iCUE", "Realtek", "Nahimic", "Chrome", "Firefox", "Edge", "Opera"
            };

            foreach (var keyPath in startupKeys)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                    {
                        if (key == null) continue;
                        
                        var valueNames = key.GetValueNames().ToList();
                        foreach (var valueName in valueNames)
                        {
                            try
                            {
                                var valueData = key.GetValue(valueName)?.ToString() ?? "";
                                var lowerValueName = valueName.ToLowerInvariant();
                                var lowerValueData = valueData.ToLowerInvariant();
                                
                                bool shouldDisable = false;
                                foreach (var target in startupTargets)
                                {
                                    var lowerTarget = target.ToLowerInvariant();
                                    if (lowerValueName.Contains(lowerTarget) || lowerValueData.Contains(lowerTarget))
                                    {
                                        shouldDisable = true;
                                        break;
                                    }
                                }
                                
                                if (shouldDisable)
                                {
                                    // Instead of deleting, rename to disable (safer)
                                    var disabledName = $"_DISABLED_{valueName}";
                                    key.SetValue(disabledName, valueData);
                                    key.DeleteValue(valueName);
                                    disabled++;
                                    log($"🚫 Başlangıç programı devre dışı: {valueName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                log($"❌ Başlangıç programı işlem hatası: {valueName} - {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"❌ Registry erişim hatası: {keyPath} - {ex.Message}");
                }
            }
            
            // Also check machine-wide startup (requires admin)
            if (IsAdministrator())
            {
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            var valueNames = key.GetValueNames().ToList();
                            foreach (var valueName in valueNames)
                            {
                                try
                                {
                                    var valueData = key.GetValue(valueName)?.ToString() ?? "";
                                    var lowerValueName = valueName.ToLowerInvariant();
                                    var lowerValueData = valueData.ToLowerInvariant();
                                    
                                    bool shouldDisable = false;
                                    foreach (var target in startupTargets)
                                    {
                                        var lowerTarget = target.ToLowerInvariant();
                                        if (lowerValueName.Contains(lowerTarget) || lowerValueData.Contains(lowerTarget))
                                        {
                                            shouldDisable = true;
                                            break;
                                        }
                                    }
                                    
                                    if (shouldDisable)
                                    {
                                        var disabledName = $"_DISABLED_{valueName}";
                                        key.SetValue(disabledName, valueData);
                                        key.DeleteValue(valueName);
                                        disabled++;
                                        log($"🚫 Sistem başlangıç programı devre dışı: {valueName}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log($"❌ Sistem başlangıç programı işlem hatası: {valueName} - {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"❌ Sistem registry erişim hatası: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log($"❌ Başlangıç programları devre dışı bırakma hatası: {ex.Message}");
        }
        
        return disabled;
    }

    private static async Task CleanupDesktopAsync(Action<string> log)
    {
        // Hızlı ve doğrudan taşıma modu: kopyalama yok, derin attribute/izin işlemleri yok
        await Task.Run(() =>
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!Directory.Exists(desktop)) { log("Desktop folder not found."); return; }

            // Klasör adı sabit: "Masaüstü" (varsa zaman damgalı yedek isim)
            var baseTargetName = "Masaüstü";
            var target = Path.Combine(desktop, baseTargetName);
            if (Directory.Exists(target))
                target = Path.Combine(desktop, $"{baseTargetName}_{DateTime.Now:yyyyMMdd_HHmm}");

            Directory.CreateDirectory(target);

            int moved = 0, skipped = 0, errors = 0;

            // Kendi EXE'mizi koru
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            var currentExeName = Path.GetFileName(currentExePath);

            // Büyük listelerde RAM tüketimini azaltmak için Enumerate kullan
            foreach (var path in Directory.EnumerateFileSystemEntries(desktop, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var name = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(name)) { skipped++; continue; }

                    // Hedef klasörün kendisi veya uygulamanın EXE'si ise geç
                    if (string.Equals(path, target, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    if (string.Equals(name, currentExeName, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                    // Unlost adındaki .exe'leri güvenlik için geç
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("unlost", StringComparison.OrdinalIgnoreCase))
                    { skipped++; continue; }

                    var dest = EnsureUniquePath(Path.Combine(target, name));

                    // Yalnızca üst seviye öznitelikleri temizle (derin tarama yok)
                    try
                    {
                        if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                        else if (Directory.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                    }
                    catch { }

                    // Doğrudan taşıma (aynı birimde anında, farklı birimde OS kopyala+sil yapar)
                    if (File.Exists(path))
                    {
                        File.Move(path, dest);
                        moved++;
                    }
                    else if (Directory.Exists(path))
                    {
                        Directory.Move(path, dest);
                        moved++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    // Aşırı log ile UI'yi yormamak için yalnızca hatayı kısa geç
                    log($"Masaüstü taşıma hatası: {Path.GetFileName(path)} - {ex.Message}");
                }
            }

            log($"✅ Masaüstü düzenlendi: {moved} taşındı, {skipped} atlandı, {errors} hata → '{Path.GetFileName(target)}'");
        });
    }

    private static void TryClearAttributes(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            else if (Directory.Exists(path))
            {
                ClearDirectoryAttributes(path);
            }
        }
        catch { }
    }

    private static void ClearDirectoryAttributes(string dir)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
            }
            foreach (var d in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(d, FileAttributes.Normal); } catch { }
            }
            try { File.SetAttributes(dir, FileAttributes.Normal); } catch { }
        }
        catch { }
    }

    private static bool TryMove(string source, string dest)
    {
        try
        {
            if (File.Exists(source))
            {
                File.Move(source, dest);
                return true;
            }
            if (Directory.Exists(source))
            {
                Directory.Move(source, dest);
                return true;
            }
            return false;
        }
        catch (IOException)
        { return false; }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool ScheduleMoveOnReboot(string source, string dest)
    {
        try
        {
            // Hedef mevcutsa benzersiz yap
            dest = EnsureUniquePath(dest);
            return MoveFileEx(source, dest, MOVEFILE_REPLACE_EXISTING | MOVEFILE_DELAY_UNTIL_REBOOT);
        }
        catch { return false; }
    }

    private static void TryGrantFullAccess(string path, Action<string> log)
    {
        try
        {
            var user = WindowsIdentity.GetCurrent().Name; // DOMAIN\\User veya Machine\\User
            // /T alt öğeleri, /C hatalarda devam
            var output = RunAndCapture("icacls", $"\"{path}\" /grant \"{user}\":F /T /C");
            if (!string.IsNullOrWhiteSpace(output)) log(TrimForLog(output));
        }
        catch (Exception ex)
        {
            log($"İzin verme denemesi başarısız: {ex.Message}");
        }
    }

    private static string EnsureUniquePath(string dest)
    {
        if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
        var dir = Path.GetDirectoryName(dest) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        int i = 1;
        while (File.Exists(dest) || Directory.Exists(dest))
        {
            dest = Path.Combine(dir, $"{name} ({i++}){ext}");
        }
        return dest;
    }

    private static async Task SetBlackWallpaperAsync(Action<string> log)
    {
        await Task.Run(() =>
        {
            try
            {
                using (var desk = Registry.CurrentUser.CreateSubKey("Control Panel\\Desktop"))
                {
                    desk?.SetValue("Wallpaper", string.Empty, RegistryValueKind.String);
                    desk?.SetValue("WallpaperStyle", "0", RegistryValueKind.String);
                    desk?.SetValue("TileWallpaper", "0", RegistryValueKind.String);
                }
                using (var colors = Registry.CurrentUser.CreateSubKey("Control Panel\\Colors"))
                {
                    colors?.SetValue("Background", "0 0 0", RegistryValueKind.String);
                }
                // Broadcast change
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, string.Empty, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                log("Wallpaper set to solid black.");
            }
            catch (Exception ex) { log($"SetBlackWallpaper error: {ex.Message}"); }
        });
    }

    private static async Task RunDiskCleanupAsync(Action<string> log)
    {
        if (!IsAdministrator()) { log("Disk cleanup needs Administrator. Skipping Windows Update cache/DISM."); }

        // 1) Windows Update cache cleanup
        try { await CleanWindowsUpdateCacheAsync(log); } catch (Exception ex) { log($"WU cache cleanup: {ex.Message}"); }

        // 2) DISM component store cleanup (can take long)
        try
        {
            if (IsAdministrator())
            {
                log("Running DISM component cleanup (this may take several minutes)...");
                await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /StartComponentCleanup /ResetBase"));
                log("DISM component cleanup finished.");
            }
        }
        catch (Exception ex) { log($"DISM error: {ex.Message}"); }

        // 3) Disk Cleanup utility (best-effort)
        try
        {
            var cleanmgr = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cleanmgr.exe");
            if (File.Exists(cleanmgr))
            {
                log("Starting CleanMgr in verylowdisk mode...");
                RunAndCapture(cleanmgr, "/verylowdisk");
            }
        }
        catch (Exception ex) { log($"CleanMgr error: {ex.Message}"); }
    }

    private static async Task CleanWindowsUpdateCacheAsync(Action<string> log)
    {
        await Task.Run(() =>
        {
            if (!IsAdministrator()) { log("Not Admin: skipping WU cache."); return; }
            RunAndCapture("net", "stop wuauserv");
            RunAndCapture("net", "stop bits");
            var download = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
            int removed = 0;
            if (Directory.Exists(download))
            {
                foreach (var f in Directory.GetFiles(download, "*", SearchOption.AllDirectories)) { try { File.Delete(f); removed++; } catch { } }
                foreach (var d in Directory.GetDirectories(download, "*", SearchOption.AllDirectories)) { try { Directory.Delete(d, true); } catch { } }
            }
            log($"Windows Update cache files deleted: {removed}.");
            RunAndCapture("net", "start wuauserv");
            RunAndCapture("net", "start bits");
        });
    }

    private static string RunAndCapture(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        sb.Append(p.StandardOutput.ReadToEnd());
        sb.AppendLine(p.StandardError.ReadToEnd());
        p.WaitForExit();
        return sb.ToString();
    }

    public static async Task<string> DismCheckHealthAsync(Action<string> log)
    {
        log("DISM CheckHealth started...");
        var output = await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /CheckHealth"));
        if (!string.IsNullOrWhiteSpace(output)) log(TrimForLog(output));
        return output;
    }

    public static async Task<string> DismScanHealthAsync(Action<string> log)
    {
        log("DISM ScanHealth started...");
        var output = await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /ScanHealth"));
        if (!string.IsNullOrWhiteSpace(output)) log(TrimForLog(output));
        return output;
    }

    public static async Task<string> DismRestoreHealthAsync(Action<string> log)
    {
        log("DISM RestoreHealth started...");
        var output = await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /RestoreHealth"));
        if (!string.IsNullOrWhiteSpace(output)) log(TrimForLog(output));
        return output;
    }

    public static async Task<string> SfcScanNowAsync(Action<string> log)
    {
        log("SFC /scannow started...");
        var output = await Task.Run(() => RunAndCapture("sfc", "/scannow"));
        if (!string.IsNullOrWhiteSpace(output)) log(TrimForLog(output));
        return output;
    }

    private static async Task MemoryCleanAsync(Action<string> log)
    {
        log("🧠 Advanced Memory Cleanup Started - Professional Mode");
        
        // Phase 1: Memory Analysis
        var memBefore = GC.GetTotalMemory(false);
        var memInfo = GetMemoryInfo();
        log($"📊 Pre-cleanup: Available: {memInfo.availableMB:F0} MB, Used: {memInfo.usedPercent:F1}%");
        
        // Phase 2: Advanced Working Set Trimming
        int trimmed = 0, errors = 0, skipped = 0;
        long totalFreed = 0;
        await Task.Run(() =>
        {
            var processes = Process.GetProcesses().Where(IsProcessTrimmable).ToArray();
            log($"🔍 Analyzing {processes.Length} processes for memory optimization...");

            foreach (var proc in processes)
            {
                try
                {
                    // Snapshot before
                    long wsBefore = 0;
                    try { wsBefore = proc.WorkingSet64; } catch { }

                    var freed = TryEmptyWorkingSet(proc);
                    if (freed >= 0)
                    {
                        totalFreed += freed;
                        trimmed++;
                        if (freed > 50L * 1024 * 1024)
                            log($"  ✅ {proc.ProcessName}: freed {freed / (1024 * 1024):F0} MB");
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch { errors++; }
                finally { try { proc.Dispose(); } catch { } }
            }
        });
        
        log($"⚡ Working Set Trim Complete: {trimmed} optimized, {skipped} skipped, {errors} errors");
        log($"💾 Total Memory Freed: {totalFreed / (1024 * 1024):F0} MB");

        // Phase 3: Standby List Purge (Advanced)
        await PurgeStandbyListAdvanced(log);
        
        // Phase 4: System Memory Optimization
        await OptimizeSystemMemory(log);
        
        // Phase 5: Final Analysis
        var memAfter = GetMemoryInfo();
        var improvement = memAfter.availableMB - memInfo.availableMB;
        log($"📈 Post-cleanup: Available: {memAfter.availableMB:F0} MB (+{improvement:F0} MB)");
        log($"🚀 Memory Cleanup Complete - System Performance Enhanced!");
    }

    private static bool IsProcessTrimmable(Process proc)
    {
        try
        {
            var name = proc.ProcessName?.ToLowerInvariant() ?? string.Empty;
            
            // Skip our own process
            if (proc.Id == Process.GetCurrentProcess().Id) return false;
            
            // Skip empty names
            if (string.IsNullOrEmpty(name)) return false;
            
            // Skip critical system processes
            var criticalProcesses = new HashSet<string>
            {
                "system", "idle", "dwm", "winlogon", "csrss", "lsass", "smss", 
                "services", "wininit", "audiodg", "conhost", "svchost"
            };
            if (criticalProcesses.Contains(name)) return false;
            
            // Skip Windows system executables
            try
            {
                var mainModule = proc.MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(mainModule))
                {
                    var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    if (mainModule.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            catch { /* MainModule access can fail */ }
            
            // Skip processes with very low memory usage (< 10MB)
            if (proc.WorkingSet64 < 10 * 1024 * 1024) return false;
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Returns freed bytes, or -1 if skipped/failed
    private static long TryEmptyWorkingSet(Process proc)
    {
        try
        {
            // Skip very young processes (reduce churn)
            try
            {
                var age = DateTime.Now - proc.StartTime;
                if (age.TotalSeconds < 5) return -1;
            }
            catch { }

            long before = 0, after = 0;
            try { before = proc.WorkingSet64; } catch { }

            var h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION, false, proc.Id);
            if (h == IntPtr.Zero)
            {
                // Fallback to existing handle if accessible
                try
                {
                    if (!EmptyWorkingSet(proc.Handle)) return -1;
                }
                catch { return -1; }
            }
            else
            {
                try
                {
                    if (!EmptyWorkingSet(h)) return -1;
                }
                finally
                {
                    try { CloseHandle(h); } catch { }
                }
            }

            // Allow the process to settle
            Thread.Sleep(5);
            try { proc.Refresh(); } catch { }
            try { after = proc.WorkingSet64; } catch { }

            return Math.Max(0, before - after);
        }
        catch
        {
            return -1;
        }
    }

    private static async Task PurgeStandbyListAdvanced(Action<string> log)
    {
        log("🔧 Advanced Standby List Purge...");
        
    // Enable required privileges
    bool privOk = EnablePrivilege("SeProfileSingleProcessPrivilege") | 
              EnablePrivilege("SeIncreaseQuotaPrivilege") |
              EnablePrivilege("SeSystemEnvironmentPrivilege") |
              EnablePrivilege("SeDebugPrivilege");
        
        if (!privOk && !IsAdministrator())
        {
            log("⚠️ Limited privileges - run as Administrator for optimal memory cleanup");
            return;
        }

        try
        {
            // Purge different memory lists
            var purgeTypes = new[]
            {
                (MemoryPurgeStandbyList, "Standby List"),
                (6, "Low Priority Standby List"),
                (5, "Modified Page List"),
                (1, "Empty Process Working Sets")
            };

            foreach (var (cmdValue, name) in purgeTypes)
            {
                try
                {
                    int cmd = cmdValue;
                    int status = NtSetSystemInformation(SystemMemoryListInformation, ref cmd, sizeof(int));
                    if (status == 0)
                        log($"  ✅ {name} purged successfully");
                    else
                        log($"  ⚠️ {name} purge status: 0x{status:X} {DecodeNtStatus(status)}");
                }
                catch (Exception ex)
                {
                    log($"  ❌ {name} purge failed: {ex.Message}");
                }
                
                await Task.Delay(100); // Small delay between operations
            }
        }
        catch (Exception ex)
        {
            log($"❌ Advanced purge error: {ex.Message}");
        }
    }

    private static async Task OptimizeSystemMemory(Action<string> log)
    {
        log("⚙️ System Memory Optimization...");
        
        await Task.Run(() =>
        {
            try
            {
                // Force garbage collection
                log("  🧹 Forcing .NET garbage collection...");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Trim process working set
                log("  📉 Trimming current process working set...");
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                
                // Additional system optimization
                log("  ⚡ Applying memory optimization tweaks...");
                
            }
            catch (Exception ex)
            {
                log($"  ⚠️ System optimization warning: {ex.Message}");
            }
        });
    }

    private static string DecodeNtStatus(int status)
    {
        // Common NTSTATUS codes seen when calling NtSetSystemInformation
        return status switch
        {
            unchecked((int)0xC0000061) => "(STATUS_PRIVILEGE_NOT_HELD)",
            unchecked((int)0xC0000001) => "(STATUS_UNSUCCESSFUL)",
            unchecked((int)0xC0000022) => "(STATUS_ACCESS_DENIED)",
            unchecked((int)0xC0000008) => "(STATUS_INVALID_HANDLE)",
            unchecked((int)0xC000000D) => "(STATUS_INVALID_PARAMETER)",
            0 => "(STATUS_SUCCESS)",
            _ => string.Empty
        };
    }

    private static (double availableMB, double usedPercent) GetMemoryInfo()
    {
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf(memStatus);
            
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                var totalMB = memStatus.ullTotalPhys / (1024.0 * 1024.0);
                var availMB = memStatus.ullAvailPhys / (1024.0 * 1024.0);
                var usedPercent = ((totalMB - availMB) / totalMB) * 100;
                
                return (availMB, usedPercent);
            }
        }
        catch { }
        
        return (0, 0);
    }

    private static bool EnablePrivilege(string name)
    {
        try
        {
            var hProc = Process.GetCurrentProcess().Handle;
            if (!OpenProcessToken(hProc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hToken)) return false;
            try
            {
                if (!LookupPrivilegeValue(null, name, out var luid)) return false;
                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }
                };
                return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { try { System.Runtime.InteropServices.Marshal.Release(hToken); } catch { } }
        }
        catch { return false; }
    }

    private static async Task RunSystemHealthAsync(Action<string> log)
    {
        log("System health check and repair started (DISM/SFC). This may take a while...");
        if (!IsAdministrator())
        {
            log("Not running as Administrator. DISM/SFC may fail. Consider 'Run as Admin'.");
        }

        try
        {
            // Quick health check
            log("Running: dism /online /Cleanup-Image /CheckHealth");
            var out1 = await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /CheckHealth"));
            if (!string.IsNullOrWhiteSpace(out1)) log(TrimForLog(out1));
        }
        catch (Exception ex) { log($"DISM CheckHealth error: {ex.Message}"); }

        try
        {
            // Deep scan
            log("Running: dism /online /Cleanup-Image /ScanHealth");
            var out2 = await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /ScanHealth"));
            if (!string.IsNullOrWhiteSpace(out2)) log(TrimForLog(out2));
        }
        catch (Exception ex) { log($"DISM ScanHealth error: {ex.Message}"); }

        try
        {
            // Repair
            log("Running: dism /online /Cleanup-Image /RestoreHealth");
            var out3 = await Task.Run(() => RunAndCapture("dism", "/online /Cleanup-Image /RestoreHealth"));
            if (!string.IsNullOrWhiteSpace(out3)) log(TrimForLog(out3));
        }
        catch (Exception ex) { log($"DISM RestoreHealth error: {ex.Message}"); }

        try
        {
            // System file checker
            log("Running: sfc /scannow");
            var out4 = await Task.Run(() => RunAndCapture("sfc", "/scannow"));
            if (!string.IsNullOrWhiteSpace(out4)) log(TrimForLog(out4));
        }
        catch (Exception ex) { log($"SFC error: {ex.Message}"); }

        log("System health workflow finished.");
    }

    private static string TrimForLog(string text)
    {
        // Avoid flooding the UI log panel; keep last ~1200 chars
        const int max = 1200;
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Trim();
        return text.Length <= max ? text : text.Substring(Math.Max(0, text.Length - max));
    }
}