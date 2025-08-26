using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace UnlostFpsWinUI;

public class OptimizeOptions
{
    public bool CleanTemp { get; set; }
    public bool SetPowerPlan { get; set; }
    public bool TryUltimate { get; set; }
    public bool VisualEffectsPerformance { get; set; }
    public bool DisableTransparency { get; set; }
    public bool DisableGameBar { get; set; }
}

public static class Optimizer
{
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
            Verb = "runas"
        };
        Process.Start(psi);
        Environment.Exit(0);
    }

    public static async Task RunAsync(OptimizeOptions opt, Action<string> log)
    {
        if (!IsAdministrator())
        {
            log("⚠️ Not running as Administrator. Some settings may not be applied.");
        }

        if (opt.CleanTemp)
        {
            await Task.Run(() =>
            {
                try
                {
                    var count = CleanTempFiles();
                    log($"✅ Temp cleanup completed: {count} items removed.");
                }
                catch (Exception ex)
                {
                    log($"❌ Temp cleanup error: {ex.Message}");
                }
            });
        }

        if (opt.SetPowerPlan)
        {
            try
            {
                if (opt.TryUltimate)
                {
                    // Try to create Ultimate Performance plan
                    RunPowerCfg("-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", log);
                }
                SetBestPowerPlan(log);
            }
            catch (Exception ex)
            {
                log($"❌ Power plan setting failed: {ex.Message}");
            }
        }

        if (opt.VisualEffectsPerformance)
        {
            try
            {
                SetVisualEffectsPerformance();
                log("✅ Visual effects set to: Best Performance");
            }
            catch (Exception ex)
            {
                log($"❌ Visual effects setting failed: {ex.Message}");
            }
        }

        if (opt.DisableTransparency)
        {
            try
            {
                DisableTransparencyEffects();
                log("✅ Transparency effects disabled");
            }
            catch (Exception ex)
            {
                log($"❌ Transparency setting failed: {ex.Message}");
            }
        }

        if (opt.DisableGameBar)
        {
            try
            {
                DisableGameBarAndDvr();
                log("✅ Xbox Game Bar and Game DVR disabled");
            }
            catch (Exception ex)
            {
                log($"❌ Game Bar setting failed: {ex.Message}");
            }
        }

        log("🎯 Optimization process completed!");
    }

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
        if (!string.IsNullOrWhiteSpace(output)) log?.Invoke($"📋 {output.Trim()}");
        if (!string.IsNullOrWhiteSpace(error)) log?.Invoke($"⚠️ {error.Trim()}");
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
            var planName = ultimate != null ? "Ultimate Performance" : "High Performance";
            log($"🔋 Active power plan set to: {planName}");
        }
        else
        {
            log("⚠️ Could not find suitable power plan. Please check manually.");
        }
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
                        if (Guid.TryParse(part.Trim('{', '}', '*'), out var g))
                            return g.ToString();
                    }
                }
            }
        }
        return null;
    }

    private static int CleanTempFiles()
    {
        int deleted = 0;
        void CleanDir(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var f in Directory.GetFiles(path))
            {
                try { File.Delete(f); deleted++; } catch { }
            }
            foreach (var d in Directory.GetDirectories(path))
            {
                try { Directory.Delete(d, true); deleted++; } catch { }
            }
        }
        
        var temp = Path.GetTempPath();
        CleanDir(temp);
        var windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        CleanDir(windowsTemp);
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
    {
        using var p = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
        p?.SetValue("EnableTransparency", 0, RegistryValueKind.DWord);
    }

    private static void DisableGameBarAndDvr()
    {
        using (var g = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\GameBar"))
        {
            g?.SetValue("ShowStartupPanel", 0, RegistryValueKind.DWord);
            g?.SetValue("GamePanelStartupTipIndex", 0, RegistryValueKind.DWord);
            g?.SetValue("UseNexusForGameBarEnabled", 0, RegistryValueKind.DWord);
            g?.SetValue("AutoGameModeEnabled", 0, RegistryValueKind.DWord);
        }
        
        using (var dvr = Registry.CurrentUser.CreateSubKey("System\\GameConfigStore"))
        {
            dvr?.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_FSEBehavior", 2, RegistryValueKind.DWord);
            dvr?.SetValue("GameDVR_HonorUserFSEBehaviorMode", 2, RegistryValueKind.DWord);
        }
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
}