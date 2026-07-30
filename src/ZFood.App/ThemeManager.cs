using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ZFood.App;

/// <summary>
/// Loads and swaps the theme resource dictionaries at runtime. Every style in
/// the app references semantic tokens through DynamicResource, so replacing
/// the merged dictionary restyles the whole UI live, no restart needed.
/// </summary>
public static class ThemeManager
{
    public const string Porcelain = "porcelain";
    public const string Aurora = "aurora";
    public const string JuiceBar = "juicebar";
    public const string Glossy = "glossy";
    public const string Limonata = "limonata";
    public const string Matcha = "matcha";
    public const string BerryMilk = "berrymilk";

    /// <summary>Applied when settings carry no theme or an unknown one.</summary>
    public const string DefaultTheme = JuiceBar;

    /// <summary>All theme ids, in picker order: light themes first, dark last.</summary>
    public static readonly string[] Themes =
        { JuiceBar, Limonata, Matcha, BerryMilk, Porcelain, Aurora, Glossy };

    private static readonly Dictionary<string, string> Files = new()
    {
        [Porcelain] = "Porcelain",
        [Aurora] = "Aurora",
        [JuiceBar] = "JuiceBar",
        [Glossy] = "Glossy",
        [Limonata] = "Limonata",
        [Matcha] = "MatchaGarden",
        [BerryMilk] = "BerryMilk",
    };

    private static ResourceDictionary? _current;

    /// <summary>The theme currently applied to the application.</summary>
    public static string CurrentTheme { get; private set; } = DefaultTheme;

    /// <summary>Raised after a theme is applied (layout presets re-evaluate).</summary>
    public static event Action? ThemeChanged;

    /// <summary>Maps any stored value onto a known theme id, defaulting on unknowns.</summary>
    public static string Normalize(string? theme)
    {
        var id = theme?.Trim().ToLowerInvariant();
        return id is not null && Themes.Contains(id) ? id : DefaultTheme;
    }

    /// <summary>Applies a theme to the running application, live.</summary>
    public static void Apply(Application app, string? theme)
    {
        var id = Normalize(theme);
        if (_current is not null && id == CurrentTheme
            && app.Resources.MergedDictionaries.Contains(_current))
        {
            return;
        }

        ResourceDictionary dictionary;
        try
        {
            var uri = new Uri($"avares://ZFood/Themes/{Files[id]}.axaml");
            dictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
        }
        catch (Exception) when (id != DefaultTheme)
        {
            // A theme that fails to load must never leave the app unstyled.
            Apply(app, DefaultTheme);
            return;
        }

        if (_current is not null)
            app.Resources.MergedDictionaries.Remove(_current);
        app.Resources.MergedDictionaries.Add(dictionary);
        _current = dictionary;
        CurrentTheme = id;
        ThemeChanged?.Invoke();
    }
}
