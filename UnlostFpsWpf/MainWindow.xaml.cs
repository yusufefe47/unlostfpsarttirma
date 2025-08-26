using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Shapes;
using IOPath = System.IO.Path;
using Shapes = System.Windows.Shapes;
using MahApps.Metro.IconPacks;

namespace UnlostFpsWpf;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _metricsTimer = new DispatcherTimer();
    // CPU usage via GetSystemTimes snapshot deltas
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);
    private ulong _prevIdle, _prevKernel, _prevUser;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public MainWindow()
    {
    try { File.AppendAllText(IOPath.Combine(IOPath.GetTempPath(), "UnlostFpsWpf.log"), $"MainWindow ctor enter: {DateTime.Now}\n"); } catch { }

        Application.Current.DispatcherUnhandledException += (s, exArgs) =>
        {
            try { File.AppendAllText(IOPath.Combine(IOPath.GetTempPath(), "UnlostFpsWpf.log"), $"DispatcherUnhandledException: {exArgs.Exception}\n"); } catch { }
            MessageBox.Show(exArgs.Exception.ToString(), "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
            exArgs.Handled = true;
        };
        try
        {
            InitializeComponent();
            try { File.AppendAllText(IOPath.Combine(IOPath.GetTempPath(), "UnlostFpsWpf.log"), $"InitializeComponent OK: {DateTime.Now}\n"); } catch { }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Initialization error", MessageBoxButton.OK, MessageBoxImage.Error);
            try { File.AppendAllText(IOPath.Combine(IOPath.GetTempPath(), "UnlostFpsWpf.log"), $"InitializeComponent ERROR: {ex}\n"); } catch { }
            Close();
            return;
        }
        
        // Init CPU times snapshot
        try { InitCpuSnapshot(); } catch { }

        // Metrics timer
        _metricsTimer.Interval = TimeSpan.FromSeconds(1);
        _metricsTimer.Tick += (_, __) => UpdateMetrics();
        _metricsTimer.Start();

        // Apply localization after InitializeComponent
        ApplyLocalization();
        UpdateAdminStatus();

        // Sync caption icon initially and on window state changes
        UpdateMaxRestoreIcon(this.WindowState == WindowState.Maximized);
        StateChanged += (_, __) => UpdateMaxRestoreIcon(this.WindowState == WindowState.Maximized);
        
        Loaded += (_, __) =>
        {
            try
            {
                Topmost = true;
                Activate();
                Topmost = false;
                Focus();
            }
            catch { }
        };
    }

    private void ApplyLocalization()
    {
        try
        {
            Title = Localization.Get("AppTitle");
            
            // Find and update text elements by name or content
            UpdateTextBlock("AppTitleText", Localization.Get("AppTitle"));
            UpdateTextBlock("AppSubtitleText", Localization.Get("AppSubtitle"));
            UpdateTextBlock("StatusOnlineText", Localization.Get("StatusOnline"));
            UpdateTextBlock("OptSettingsText", Localization.Get("OptimizationSettings"));
            UpdateTextBlock("OptSubtitleText", Localization.Get("OptimizationSubtitle"));
            UpdateTextBlock("SystemStatusText", Localization.Get("SystemStatus"));
            UpdateTextBlock("OperationLogText", Localization.Get("OperationLog"));
            UpdateTextBlock("CpuUsageText", Localization.Get("CpuUsage"));
            UpdateTextBlock("RamUsageText", Localization.Get("RamUsage"));
            UpdateTextBlock("StatusReadyText", Localization.Get("StatusReady"));
            UpdateTextBlock("HealthStatusText", Localization.Get("StatusStable"));

            // CS2 UI removed
            
            // Update checkboxes
            if (ChkCleanTemp != null) ChkCleanTemp.Content = Localization.Get("CleanTemp");
            if (ChkPowerPlan != null) ChkPowerPlan.Content = Localization.Get("PowerPlan");
            if (ChkUltimate != null) ChkUltimate.Content = Localization.Get("UltimatePerf");
            if (ChkVisualFX != null) ChkVisualFX.Content = Localization.Get("VisualFX");
            if (ChkTransparency != null) ChkTransparency.Content = Localization.Get("DisableTransparency");
            if (ChkGameBar != null) ChkGameBar.Content = Localization.Get("DisableGameBar");
            if (ChkCloseBackground != null) ChkCloseBackground.Content = Localization.Get("CloseBackgroundApps");
            if (ChkCleanupDesktop != null) ChkCleanupDesktop.Content = Localization.Get("CleanupDesktop");
            if (ChkBlackWallpaper != null) ChkBlackWallpaper.Content = Localization.Get("BlackWallpaper");
            if (ChkDiskCleanup != null) ChkDiskCleanup.Content = Localization.Get("DiskCleanup");
            if (ChkMemoryClean != null) ChkMemoryClean.Content = Localization.Get("MemoryClean");
            if (ChkEnableGameMode != null) ChkEnableGameMode.Content = Localization.Get("EnableGameMode");
            if (ChkSystemHealth != null) ChkSystemHealth.Content = Localization.Get("SystemHealth");
            var _chkNet = FindName("ChkNetworkOptimize") as CheckBox;
            if (_chkNet != null) _chkNet.Content = "🌐 Network optimization (low latency)";
            
            // Update system health details
            UpdateTextBlock("SystemHealthDesc", Localization.Get("SystemHealthDesc"));
            UpdateTextBlock("SystemHealthDetail1", Localization.Get("SystemHealthDetail1"));
            UpdateTextBlock("SystemHealthDetail2", Localization.Get("SystemHealthDetail2"));
            UpdateTextBlock("SystemHealthDetail3", Localization.Get("SystemHealthDetail3"));
            UpdateTextBlock("SystemHealthDetail4", Localization.Get("SystemHealthDetail4"));
            UpdateTextBlock("SystemHealthDetail5", Localization.Get("SystemHealthDetail5"));
            UpdateTextBlock("SystemHealthFooter", Localization.Get("SystemHealthFooter"));
            
            // Update buttons
            var _btnRunAsAdmin = FindName("BtnRunAsAdmin") as Button;
            if (_btnRunAsAdmin != null) _btnRunAsAdmin.Content = Localization.Get("RunAsAdmin");
            var _btnRestartExplorer = FindName("BtnRestartExplorer") as Button;
            if (_btnRestartExplorer != null) _btnRestartExplorer.Content = Localization.Get("RestartExplorer");
            if (BtnOptimize != null) BtnOptimize.Content = Localization.Get("OptimizeSystem");
            if (BtnSelectAll != null) BtnSelectAll.Content = Localization.Get("SelectAll");
            if (BtnClearAll != null) BtnClearAll.Content = Localization.Get("ClearAll");
            
            // Update log
            if (TxtLog != null) TxtLog.Text = Localization.Get("LogReady");
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(IOPath.Combine(IOPath.GetTempPath(), "UnlostFpsWpf.log"), $"ApplyLocalization ERROR: {ex}\n"); } catch { }
        }
    }
    
    private void UpdateTextBlock(string name, string text)
    {
        try
        {
            var element = FindName(name) as TextBlock;
            if (element != null) element.Text = text;
        }
        catch { }
    }

    private void UpdateAdminStatus()
    {
        var isAdmin = Optimizer.IsAdministrator();
        var _btnRunAsAdmin = FindName("BtnRunAsAdmin") as Button;
        if (_btnRunAsAdmin != null) _btnRunAsAdmin.IsEnabled = !isAdmin;
    }

    private async void BtnOptimize_Click(object sender, RoutedEventArgs e)
    {
        SetUiEnabled(false);
        TxtLog.Text = $"Started: {DateTime.Now:HH:mm:ss}\n";

        var options = new OptimizeOptions
        {
            CleanTemp = ChkCleanTemp.IsChecked == true,
            SetPowerPlan = ChkPowerPlan.IsChecked == true,
            TryUltimate = ChkUltimate.IsChecked == true,
            VisualEffectsPerformance = ChkVisualFX.IsChecked == true,
            DisableTransparency = ChkTransparency.IsChecked == true,
            DisableGameBar = ChkGameBar.IsChecked == true,
            CloseBackgroundApps = ChkCloseBackground.IsChecked == true,
            CleanupDesktop = ChkCleanupDesktop.IsChecked == true,
            SetBlackWallpaper = ChkBlackWallpaper.IsChecked == true,
            DiskCleanup = ChkDiskCleanup.IsChecked == true,
            EnableGameMode = ChkEnableGameMode.IsChecked == true
            ,MemoryClean = ChkMemoryClean.IsChecked == true
            ,SystemHealth = ChkSystemHealth.IsChecked == true
            ,NetworkOptimize = (FindName("ChkNetworkOptimize") as CheckBox)?.IsChecked == true
        };

        try
        {
            await Optimizer.RunAsync(options, msg => Log($"[{DateTime.Now:HH:mm:ss}] {msg}"));
            MessageBox.Show(this, "System optimization completed.", "Optimization Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
        }
        finally
        {
            SetUiEnabled(true);
        }
    }

    private void BtnRunAsAdmin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var message = lang == "tr"
                ? "Bu özelliklerin bazıları (DISM/SFC, Windows Update önbellek temizliği, gelişmiş bellek temizliği) yönetici ayrıcalığı gerektirir.\n\nUygulama yönetici olarak yeniden başlatılacak. Açık bir örnek varsa kapatılmalı. Devam edilsin mi?"
                : "Some features (DISM/SFC, Windows Update cache cleanup, advanced memory cleanup) require administrator privileges.\n\nThe app will restart elevated. If another instance is running, it must be closed. Continue?";

            if (MessageBox.Show(this, message, "Run as Administrator", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Optimizer.RestartAsAdministrator();
            }
        }
        catch (Exception ex) { Log(ex.Message); }
    }

    private void BtnRestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("explorer")) p.Kill();
            Process.Start("explorer.exe");
            Log("Explorer restarted");
        }
        catch (Exception ex) { Log(ex.Message); }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        SetChecks(true);
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        SetChecks(false);
    }

    // CS2 handlers removed

    private void UpdateHealthFromOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return;
        var text = output.ToLowerInvariant();
        if (text.Contains("no component store corruption") || text.Contains("no corruption") || text.Contains("did not find any integrity violations"))
        {
            HealthPill.Background = new SolidColorBrush(Color.FromRgb(16,185,129));
            HealthStatusText.Text = Localization.Get("HealthGood");
        }
        else if (text.Contains("repairable") || text.Contains("repaired") || text.Contains("successfully repaired"))
        {
            HealthPill.Background = new SolidColorBrush(Color.FromRgb(234,179,8));
            HealthStatusText.Text = Localization.Get("HealthMedium");
        }
        else if (text.Contains("cannot repair") || text.Contains("corruption") || text.Contains("failed"))
        {
            HealthPill.Background = new SolidColorBrush(Color.FromRgb(239,68,68));
            HealthStatusText.Text = Localization.Get("HealthHigh");
        }
        else
        {
            HealthPill.Background = new SolidColorBrush(Color.FromRgb(156,163,175));
            HealthStatusText.Text = Localization.Get("HealthUnknown");
        }
    }

    private void SetUiEnabled(bool enabled)
    {
        ChkCleanTemp.IsEnabled = enabled;
        ChkPowerPlan.IsEnabled = enabled;
        ChkUltimate.IsEnabled = enabled;
        ChkVisualFX.IsEnabled = enabled;
        ChkTransparency.IsEnabled = enabled;
        ChkGameBar.IsEnabled = enabled;
    ChkCloseBackground.IsEnabled = enabled;
    ChkCleanupDesktop.IsEnabled = enabled;
    ChkBlackWallpaper.IsEnabled = enabled;
    ChkDiskCleanup.IsEnabled = enabled;
    ChkEnableGameMode.IsEnabled = enabled;
    ChkMemoryClean.IsEnabled = enabled;
    ChkSystemHealth.IsEnabled = enabled;
    var _chkNet = FindName("ChkNetworkOptimize") as CheckBox; if (_chkNet != null) _chkNet.IsEnabled = enabled;
    BtnOptimize.IsEnabled = enabled;
    var _btnRunAsAdmin = FindName("BtnRunAsAdmin") as Button;
    if (_btnRunAsAdmin != null) _btnRunAsAdmin.IsEnabled = enabled && !Optimizer.IsAdministrator();
    var _btnRestartExplorer = FindName("BtnRestartExplorer") as Button;
    if (_btnRestartExplorer != null) _btnRestartExplorer.IsEnabled = enabled;
    }

    private void SetChecks(bool value)
    {
        ChkCleanTemp.IsChecked = value;
        ChkPowerPlan.IsChecked = value;
        ChkUltimate.IsChecked = value;
        ChkVisualFX.IsChecked = value;
        ChkTransparency.IsChecked = value;
        ChkGameBar.IsChecked = value;
        ChkCloseBackground.IsChecked = value;
        ChkCleanupDesktop.IsChecked = value;
        ChkBlackWallpaper.IsChecked = value;
        ChkDiskCleanup.IsChecked = value;
        ChkEnableGameMode.IsChecked = value;
        ChkMemoryClean.IsChecked = value;
        ChkSystemHealth.IsChecked = value;
        var _chkNet = FindName("ChkNetworkOptimize") as CheckBox; if (_chkNet != null) _chkNet.IsChecked = value;
    }

    private void Log(string msg) => Dispatcher.Invoke(() => TxtLog.Text += msg + "\n");

    private void UpdateMetrics()
    {
        try
        {
            int cpu = GetCpuUsagePercent();
            int usedPct = GetRamUsagePercent();

            // Update texts
            CpuPercentText.Text = $"{cpu}%";
            RamPercentText.Text = $"{usedPct}%";

            // Update progress bars (using fixed width calculation)
            CpuUsageBar.Width = Math.Max(0, 200 * cpu / 100.0);
            RamUsageBar.Width = Math.Max(0, 200 * usedPct / 100.0);

            // Update status pills based on system load
            if (cpu < 60 && usedPct < 70)
            {
                // System is healthy - green colors
                ReadyPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                HealthPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                HealthStatusText.Text = Localization.Get("HealthGood");
            }
            else if (cpu < 85 && usedPct < 85)
            {
                // System under moderate load - amber colors
                ReadyPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8));
                HealthPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(234, 179, 8));
                HealthStatusText.Text = Localization.Get("HealthMedium");
            }
            else
            {
                // System under high load - red colors
                ReadyPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                HealthPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                HealthStatusText.Text = Localization.Get("HealthHigh");
            }
        }
        catch { }
    }

    private void InitCpuSnapshot()
    {
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            _prevIdle = ToUInt64(idle);
            _prevKernel = ToUInt64(kernel);
            _prevUser = ToUInt64(user);
        }
    }

    private int GetCpuUsagePercent()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
            ulong idleTicks = ToUInt64(idle);
            ulong kernelTicks = ToUInt64(kernel);
            ulong userTicks = ToUInt64(user);

            ulong idleDelta = idleTicks - _prevIdle;
            ulong kernelDelta = kernelTicks - _prevKernel;
            ulong userDelta = userTicks - _prevUser;

            _prevIdle = idleTicks;
            _prevKernel = kernelTicks;
            _prevUser = userTicks;

            // kernelDelta includes idle time; subtract it out
            ulong total = kernelDelta + userDelta;
            if (total == 0) return 0;
            ulong busy = total - idleDelta;
            if (busy > total) busy = total;
            var pct = (int)Math.Round(busy * 100.0 / total);
            return Math.Min(100, Math.Max(0, pct));
        }
        catch { return 0; }
    }

    private static ulong ToUInt64(FILETIME ft) => ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;

    private int GetRamUsagePercent()
    {
        try
        {
            var ms = new MEMORYSTATUSEX();
            ms.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref ms))
            {
                return (int)Math.Min(100, Math.Max(0, ms.dwMemoryLoad));
            }
        }
        catch { }
        return 0;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.ClickCount == 2)
            {
                ToggleMaxRestore();
                return;
            }
            DragMove();
        }
        catch { }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        try { WindowState = WindowState.Minimized; } catch { }
    }

    private void BtnMaxRestore_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaxRestore();
    }

    private void ToggleMaxRestore()
    {
        try
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                UpdateMaxRestoreIcon(isMaximized: false);
            }
            else
            {
                WindowState = WindowState.Maximized;
                UpdateMaxRestoreIcon(isMaximized: true);
            }
        }
        catch { }
    }

    private void UpdateMaxRestoreIcon(bool isMaximized)
    {
        try
        {
            // Use MahApps IconPacks icon instead of manual shapes
            var icon = FindName("MaxRestoreIcon") as PackIconMaterial;
            if (icon != null)
            {
                icon.Kind = isMaximized ? PackIconMaterialKind.WindowRestore : PackIconMaterialKind.WindowMaximize;
            }
            // Optional: update tooltip text accordingly
            if (BtnMaxRestore != null)
            {
                BtnMaxRestore.ToolTip = isMaximized ? "Restore" : "Maximize";
            }
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        try { Close(); } catch { }
    }

    private void BtnCopyGpuGuide_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // GPU rehberi TextBlock'u bul ve içeriğini panoya kopyala (adı verildi)
            var guide = FindName("TxtGpuGuide") as TextBlock;
            if (guide != null)
            {
                Clipboard.SetText(guide.Text);
                Log("GPU rehberi panoya kopyalandı.");
            }
        }
        catch (Exception ex)
        {
            Log($"Kopyalama hatası: {ex.Message}");
        }
    }
}