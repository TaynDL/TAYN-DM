using System.Linq;
using System.Windows;

namespace TaynDM;

/// <summary>
/// Manages dark/light theme switching by swapping ResourceDictionaries
/// and persisting the user preference.
/// </summary>
public static class ThemeManager
{
    private const string DarkPack = "pack://application:,,,/Themes/Dark.xaml";
    private const string LightPack = "pack://application:,,,/Themes/Light.xaml";

    /// <summary>True when the active theme is dark.</summary>
    public static bool IsDark { get; private set; } = true;

    /// <summary>Toggle between dark and light themes.</summary>
    public static void Toggle()
    {
        IsDark = !IsDark;
        Apply();

        // Persist preference
        var settings = AppSettings.Load();
        settings.DarkTheme = IsDark;
        settings.Save();
    }

    /// <summary>Apply the current theme to the application.</summary>
    public static void Apply()
    {
        var app = Application.Current;
        if (app == null) return;

        var merged = app.Resources.MergedDictionaries;

        // Remove any existing theme dictionary
        var old = merged
            .Where(d => d.Source != null &&
                (d.Source.OriginalString.Contains("Dark.xaml") ||
                 d.Source.OriginalString.Contains("Light.xaml")))
            .ToList();

        foreach (var dict in old)
            merged.Remove(dict);

        // Add the new one
        var source = IsDark ? DarkPack : LightPack;
        merged.Add(new ResourceDictionary { Source = new Uri(source) });
    }

    /// <summary>Load saved theme preference from settings.</summary>
    public static void LoadFromSettings(AppSettings settings)
    {
        IsDark = settings.DarkTheme;
    }
}
