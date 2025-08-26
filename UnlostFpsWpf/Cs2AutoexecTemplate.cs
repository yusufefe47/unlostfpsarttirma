namespace UnlostFpsWpf;

/// <summary>
/// CS2 autoexec.cfg içerik şablonu (koda gömülü). Bu metni düzenleyerek herkes için ortak
/// ayarları dağıtabilirsiniz. Boş bırakılırsa diğer kaynaklara (yan dosya/Assets) veya güvenli
/// fallback şablona düşer.
/// </summary>
public static class Cs2AutoexecTemplate
{
    // Bu metni dilediğiniz gibi düzenleyin. Örn. hassasiyet/bind vs. ekleyebilirsiniz.
    // Uyarı: Çok agresif grafik ayarlarından kaçının; güvenli ve evrensel değerleri tercih edin.
    public static string Content = @"// UNLOST - CS2 Performance Autoexec (editable in code)
// Buradaki içerik doğrudan kod içinde tutulur. İsterseniz EXE yanına cs2_autoexec.cfg koyarak
// bunu override edebilirsiniz. Boş bırakırsanız Assets/Cs2/autoexec.cfg veya güvenli fallback kullanılır.

fps_max 0
rate 786432
cl_interp 0
cl_interp_ratio 1
cl_updaterate 128
cl_cmdrate 128
engine_low_latency_sleep_after_client_tick 1
r_player_visibility_mode 1
cq_netgraph 1

// Buradan sonrasını özgürce düzenleyebilirsiniz.
// bind ""F1"" ""+showscores""

echo ""[UNLOST] CS2 autoexec (code-embedded) loaded""
// End of autoexec
";
}
