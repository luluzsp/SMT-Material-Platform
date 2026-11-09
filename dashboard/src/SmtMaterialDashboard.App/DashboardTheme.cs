namespace SmtMaterialDashboard.App;

internal static class DashboardTheme
{
    public static readonly Color Background = Color.FromArgb(6, 23, 43);
    public static readonly Color HeaderStart = Color.FromArgb(11, 43, 82);
    public static readonly Color HeaderEnd = Color.FromArgb(13, 67, 105);
    public static readonly Color Surface = Color.FromArgb(13, 41, 69);
    public static readonly Color SurfaceAlt = Color.FromArgb(18, 55, 84);
    public static readonly Color Border = Color.FromArgb(40, 84, 116);
    public static readonly Color Text = Color.White;
    public static readonly Color Muted = Color.FromArgb(169, 194, 215);
    public static readonly Color Cyan = Color.FromArgb(36, 216, 232);
    public static readonly Color Green = Color.FromArgb(52, 209, 123);
    public static readonly Color Amber = Color.FromArgb(247, 185, 40);
    public static readonly Color Red = Color.FromArgb(255, 92, 104);
    public static readonly FontFamily UiFont = ResolveFont();

    private static FontFamily ResolveFont()
    {
        var preferred = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" };
        var installed = FontFamily.Families.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in preferred)
        {
            if (installed.TryGetValue(name, out var family)) return family;
        }
        return SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
    }
}
