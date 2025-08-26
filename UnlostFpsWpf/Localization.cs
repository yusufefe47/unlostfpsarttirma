using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnlostFpsWpf;

public static class Localization
{
    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["AppTitle"] = "⚡ UNLOST FPS BOOSTER",
            ["AppSubtitle"] = "Professional Performance Optimizer",
            ["StatusOnline"] = "● ONLINE",
            ["OptimizationSettings"] = "🎯 Optimization Settings",
            ["OptimizationSubtitle"] = "Configure advanced performance tweaks",
            ["SystemStatus"] = "📊 System Status",
            ["SystemSubtitle"] = "Real-time monitoring dashboard",
            ["OperationLog"] = "📝 Operation Log",
            ["LogSubtitle"] = "Live system updates and diagnostics",
            ["CleanTemp"] = "🧹 Advanced system & cache cleanup (15 locations)",
            ["PowerPlan"] = "⚡ Enable High Performance power plan",
            ["UltimatePerf"] = "🚀 Activate Ultimate Performance mode",
            ["VisualFX"] = "✨ Optimize visual effects for performance",
            ["DisableTransparency"] = "🪟 Disable transparency effects",
            ["DisableGameBar"] = "🎮 Disable Xbox Game Bar and DVR",
            ["CloseBackgroundApps"] = "🔻 Close background applications",
            ["CleanupDesktop"] = "🗂️ Organize desktop into folder",
            ["BlackWallpaper"] = "🖼️ Set solid black wallpaper",
            ["DiskCleanup"] = "🧽 Run disk and Windows Update cleanup",
            ["EnableGameMode"] = "🕹️ Enable Windows Game Mode",
            ["MemoryClean"] = "💾 Advanced memory optimization",
            ["SystemHealth"] = "🩺 Comprehensive System Health Analysis",
            ["SystemHealthDesc"] = "Performs deep system diagnostics including:",
            ["SystemHealthDetail1"] = "• DISM component store integrity verification",
            ["SystemHealthDetail2"] = "• SFC system file corruption scan",
            ["SystemHealthDetail3"] = "• Windows image health analysis",
            ["SystemHealthDetail4"] = "• Automatic corruption repair",
            ["SystemHealthDetail5"] = "• Performance optimization recommendations",
            ["SystemHealthFooter"] = "Essential for maintaining optimal system stability and performance. Detects and repairs Windows corruption issues automatically.",
            ["CpuUsage"] = "CPU Usage",
            ["RamUsage"] = "RAM Usage",
            ["StatusReady"] = "READY",
            ["StatusStable"] = "HEALTHY",
            ["RunAsAdmin"] = "🛡️ Run as Administrator",
            ["RestartExplorer"] = "🔄 Restart Explorer",
            ["OptimizeSystem"] = "🚀 OPTIMIZE SYSTEM NOW",
            ["LogReady"] = "🚀 System ready for optimization...\n💡 All components loaded successfully\n✅ Performance engine initialized",
            ["SelectAll"] = "✅ Select All",
            ["ClearAll"] = "❌ Clear All",
            ["HealthUnknown"] = "UNKNOWN",
            ["HealthGood"] = "HEALTHY",
            ["HealthMedium"] = "WARNING",
            ["HealthHigh"] = "CRITICAL"
        },
        ["tr"] = new()
        {
            ["AppTitle"] = "⚡ UNLOST FPS ARTIRICI",
            ["AppSubtitle"] = "Profesyonel Performans Optimize Edici",
            ["StatusOnline"] = "● ÇEVRİMİÇİ",
            ["OptimizationSettings"] = "🎯 Optimizasyon Ayarları",
            ["OptimizationSubtitle"] = "Gelişmiş performans iyileştirmelerini yapılandır",
            ["SystemStatus"] = "📊 Sistem Durumu",
            ["SystemSubtitle"] = "Gerçek zamanlı izleme panosu",
            ["OperationLog"] = "📝 İşlem Günlüğü",
            ["LogSubtitle"] = "Canlı sistem güncellemeleri ve tanıları",
            ["CleanTemp"] = "🧹 Gelişmiş sistem ve önbellek temizliği (15 konum)",
            ["PowerPlan"] = "⚡ Yüksek Performans güç planını etkinleştir",
            ["UltimatePerf"] = "🚀 Üstün Performans modunu aktive et",
            ["VisualFX"] = "✨ Görsel efektleri performans için optimize et",
            ["DisableTransparency"] = "🪟 Şeffaflık efektlerini devre dışı bırak",
            ["DisableGameBar"] = "🎮 Xbox Game Bar ve DVR'ı devre dışı bırak",
            ["CloseBackgroundApps"] = "🔻 Arka plan uygulamalarını kapat",
            ["CleanupDesktop"] = "🗂️ Masaüstünü klasöre düzenle",
            ["BlackWallpaper"] = "🖼️ Siyah duvar kağıdı ayarla",
            ["DiskCleanup"] = "🧽 Disk ve Windows Update temizliği yap",
            ["EnableGameMode"] = "🕹️ Windows Oyun Modunu etkinleştir",
            ["MemoryClean"] = "💾 Gelişmiş bellek optimizasyonu",
            ["SystemHealth"] = "🩺 Kapsamlı Sistem Sağlığı Analizi",
            ["SystemHealthDesc"] = "Derin sistem tanısı gerçekleştirir:",
            ["SystemHealthDetail1"] = "• DISM bileşen deposu bütünlük doğrulaması",
            ["SystemHealthDetail2"] = "• SFC sistem dosyası bozulma taraması",
            ["SystemHealthDetail3"] = "• Windows görüntü sağlığı analizi",
            ["SystemHealthDetail4"] = "• Otomatik bozulma onarımı",
            ["SystemHealthDetail5"] = "• Performans optimizasyonu önerileri",
            ["SystemHealthFooter"] = "Optimal sistem kararlılığı ve performansı için gereklidir. Windows bozulma sorunlarını otomatik olarak tespit eder ve onarır.",
            ["CpuUsage"] = "İşlemci Kullanımı",
            ["RamUsage"] = "Bellek Kullanımı",
            ["StatusReady"] = "HAZIR",
            ["StatusStable"] = "SAĞLIKLI",
            ["RunAsAdmin"] = "🛡️ Yönetici Olarak Çalıştır",
            ["RestartExplorer"] = "🔄 Explorer'ı Yeniden Başlat",
            ["OptimizeSystem"] = "🚀 SİSTEMİ ŞİMDİ OPTİMİZE ET",
            ["LogReady"] = "🚀 Sistem optimizasyon için hazır...\n💡 Tüm bileşenler başarıyla yüklendi\n✅ Performans motoru başlatıldı",
            ["SelectAll"] = "✅ Tümünü Seç",
            ["ClearAll"] = "❌ Tümünü Temizle",
            ["HealthUnknown"] = "BİLİNMİYOR",
            ["HealthGood"] = "SAĞLIKLI",
            ["HealthMedium"] = "UYARI",
            ["HealthHigh"] = "KRİTİK"
        }
    };

    private static string _currentLanguage = GetSystemLanguage();

    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set => _currentLanguage = Translations.ContainsKey(value) ? value : "en";
    }

    private static string GetSystemLanguage()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Translations.ContainsKey(culture) ? culture : "en";
    }

    public static string Get(string key)
    {
        if (Translations.TryGetValue(CurrentLanguage, out var langDict) &&
            langDict.TryGetValue(key, out var translation))
        {
            return translation;
        }
        
        // Fallback to English
        if (Translations.TryGetValue("en", out var enDict) &&
            enDict.TryGetValue(key, out var enTranslation))
        {
            return enTranslation;
        }

        return key; // Return key if no translation found
    }
}