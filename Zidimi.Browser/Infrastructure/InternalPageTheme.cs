namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Theme bridge for HTML rendered by Zidimi itself (error/welcome/new-tab fallback pages).
/// These pages live inside Chromium, so WPF DynamicResource cannot style them directly.
/// Keep the palette semantic and derived from the same active application theme.
/// </summary>
internal static class InternalPageTheme
{
    public static string CssVariables
    {
        get
        {
            var palette = ThemeManager.EffectiveCurrent switch
            {
                ThemeManager.AppTheme.Dark => new Palette(
                    Background: "#0F0E1A",
                    Surface: "#222135",
                    SurfaceHover: "#363552",
                    Border: "#34324D",
                    Text: "#F4F2FF",
                    TextSecondary: "#C8C4E8",
                    TextMuted: "#9A95C0",
                    Accent: "#8B6FFF",
                    AccentHover: "#9B82FF",
                    OnAccent: "#FFFFFF",
                    Danger: "#FF6B76",
                    DangerSurface: "#3A1A22",
                    CodeSurface: "#2C2B40",
                    Shadow: "rgba(0,0,0,.34)"),

                ThemeManager.AppTheme.Light => new Palette(
                    Background: "#F1F1F4",
                    Surface: "#FFFFFF",
                    SurfaceHover: "#ECECF1",
                    Border: "#DEDEE5",
                    Text: "#181727",
                    TextSecondary: "#5C5982",
                    TextMuted: "#8582AC",
                    Accent: "#6D4AFF",
                    AccentHover: "#7C5CFF",
                    OnAccent: "#FFFFFF",
                    Danger: "#D92D3D",
                    DangerSurface: "#FDEDEF",
                    CodeSurface: "#F6F6F8",
                    Shadow: "rgba(24,23,39,.10)"),

                _ => new Palette(
                    Background: "#F4F5F7",
                    Surface: "#FFFFFF",
                    SurfaceHover: "#EAECEF",
                    Border: "#E5E7EB",
                    Text: "#121212",
                    TextSecondary: "#595959",
                    TextMuted: "#7A7A7A",
                    Accent: "#E02020",
                    AccentHover: "#F23838",
                    OnAccent: "#FFFFFF",
                    Danger: "#C91C1C",
                    DangerSurface: "#FFF1F1",
                    CodeSurface: "#F4F5F7",
                    Shadow: "rgba(18,18,18,.10)"),
            };

            return palette.ToCssVariables();
        }
    }

    private readonly record struct Palette(
        string Background,
        string Surface,
        string SurfaceHover,
        string Border,
        string Text,
        string TextSecondary,
        string TextMuted,
        string Accent,
        string AccentHover,
        string OnAccent,
        string Danger,
        string DangerSurface,
        string CodeSurface,
        string Shadow)
    {
        public string ToCssVariables() =>
            ":root{" +
            $"--bg:{Background};" +
            $"--surface:{Surface};" +
            $"--surface-hover:{SurfaceHover};" +
            $"--border:{Border};" +
            $"--text:{Text};" +
            $"--text-secondary:{TextSecondary};" +
            $"--text-muted:{TextMuted};" +
            $"--accent:{Accent};" +
            $"--accent-hover:{AccentHover};" +
            $"--on-accent:{OnAccent};" +
            $"--danger:{Danger};" +
            $"--danger-surface:{DangerSurface};" +
            $"--code-surface:{CodeSurface};" +
            $"--shadow:{Shadow};" +
            "color-scheme:light dark}";
    }
}
