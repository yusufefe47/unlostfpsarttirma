using System.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace UnlostFpsWpf;

public partial class App : Application
{
	private static Mutex? _singleInstanceMutex;

	protected override void OnStartup(StartupEventArgs e)
	{
		// Single-instance guard with optional wait for elevated restart handoff
		var mutexName = "Global/UnlostFpsWpf_SingleInstance";
		_singleInstanceMutex = new Mutex(false, mutexName);

		// If launched with --elevated-wait, give previous instance a short time to exit
		var hasElevatedWaitArg = false;
		foreach (var arg in e.Args)
		{
			if (string.Equals(arg, "--elevated-wait", StringComparison.OrdinalIgnoreCase)) { hasElevatedWaitArg = true; break; }
		}

		bool acquired = false;
		try
		{
			// Try to acquire immediately
			acquired = _singleInstanceMutex.WaitOne(0, false);
			if (!acquired && hasElevatedWaitArg)
			{
				// Wait up to 4 seconds for the previous instance to release
				acquired = _singleInstanceMutex.WaitOne(4000, false);
			}
		}
		catch { acquired = false; }

		if (!acquired)
		{
			// Friendly bilingual message explaining admin requirement scenario
			var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
			string msg = lang == "tr"
				? "Uygulama zaten çalışıyor.\n\nYönetici olarak yeniden başlatmaya çalışıyorsanız, mevcut örnek kapatılmadan yükseltme yapılamaz. Lütfen açık örneği kapatın veya uygulama içindeki 'Yönetici Olarak Çalıştır' düğmesini kullanın.\n\nNot: Bazı özellikler (DISM/SFC, WU temizliği, gelişmiş bellek temizliği) yönetici ayrıcalığı gerektirir."
				: "The app is already running.\n\nIf you tried to restart as Administrator, elevation can't proceed while a non-admin instance is open. Please close the running instance or use the 'Run as Administrator' button from within the app.\n\nNote: Some features (DISM/SFC, Windows Update cleanup, advanced memory cleanup) require administrator privileges.";
			MessageBox.Show(msg, "UNLOST FPS Booster", MessageBoxButton.OK, MessageBoxImage.Information);
			Shutdown(0);
			return;
		}

		this.DispatcherUnhandledException += (s, exArgs) =>
		{
			MessageBox.Show($"Beklenmeyen hata: {exArgs.Exception}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
			exArgs.Handled = true;
		};
		AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
		{
			var ex = exArgs.ExceptionObject as Exception;
			MessageBox.Show($"Kritik hata: {ex}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
		};
		TaskScheduler.UnobservedTaskException += (s, exArgs) =>
		{
			MessageBox.Show($"Görev hatası: {exArgs.Exception}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
			exArgs.SetObserved();
		};
		base.OnStartup(e);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
		_singleInstanceMutex?.Dispose();
		base.OnExit(e);
	}
}