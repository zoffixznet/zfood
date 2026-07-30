using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ZFood.App;
using ZFood.App.Views;
using ZFood.Core;

namespace ZFood.Tests;

public class ThemeTests
{
    [Theory]
    [InlineData("porcelain", "porcelain")]
    [InlineData("aurora", "aurora")]
    [InlineData("juicebar", "juicebar")]
    [InlineData("glossy", "glossy")]
    [InlineData("limonata", "limonata")]
    [InlineData("matcha", "matcha")]
    [InlineData("berrymilk", "berrymilk")]
    [InlineData("  Aurora  ", "aurora")]
    [InlineData("GLOSSY", "glossy")]
    [InlineData("BerryMilk", "berrymilk")]
    [InlineData("solarized", ThemeManager.DefaultTheme)]
    [InlineData("", ThemeManager.DefaultTheme)]
    [InlineData(null, ThemeManager.DefaultTheme)]
    public void Unknown_and_missing_theme_values_normalize_to_the_default(string? stored, string expected)
        => Assert.Equal(expected, ThemeManager.Normalize(stored));

    [Fact]
    public void The_theme_choice_persists_through_the_settings_store()
    {
        using var dir = new TempDir();
        var paths = new AppPaths(dir.Path);
        var store = new SettingsStore(paths, DiagnosticsLog.Null);

        Assert.True(store.Save(new Settings { Theme = "aurora" }));
        Assert.Equal("aurora", store.Load().Theme);

        // The stored field is free-form; whatever is on disk survives the
        // round trip and normalization happens only when it is applied.
        Assert.True(store.Save(new Settings { Theme = "no-such-theme" }));
        Assert.Equal("no-such-theme", store.Load().Theme);
        Assert.Equal(ThemeManager.DefaultTheme, ThemeManager.Normalize(store.Load().Theme));

        Assert.True(store.Save(new Settings()));
        Assert.Null(store.Load().Theme);
    }

    [AvaloniaFact]
    public void Every_theme_supplies_the_complete_token_set()
    {
        var reference = LoadTheme("Porcelain");
        var referenceKeys = reference.Keys.Select(k => k.ToString()!).OrderBy(k => k).ToList();
        Assert.NotEmpty(referenceKeys);

        foreach (var file in new[] { "Aurora", "JuiceBar", "Glossy", "Limonata", "MatchaGarden", "BerryMilk" })
        {
            var keys = LoadTheme(file).Keys.Select(k => k.ToString()!).OrderBy(k => k).ToList();
            Assert.Equal(referenceKeys, keys);
        }
    }

    [AvaloniaFact]
    public void Applying_a_theme_swaps_the_token_values_live()
    {
        var app = Application.Current!;
        try
        {
            ThemeManager.Apply(app, "porcelain");
            var porcelainCard = GetResource(app, "CardBrush");

            ThemeManager.Apply(app, "aurora");
            Assert.Equal("aurora", ThemeManager.CurrentTheme);
            var auroraCard = GetResource(app, "CardBrush");
            Assert.NotEqual(porcelainCard, auroraCard);

            // An unknown value falls back to the default theme.
            ThemeManager.Apply(app, "definitely-not-a-theme");
            Assert.Equal(ThemeManager.DefaultTheme, ThemeManager.CurrentTheme);
        }
        finally
        {
            ThemeManager.Apply(app, ThemeManager.DefaultTheme);
        }
    }

    [AvaloniaFact]
    public void The_picker_reflects_the_saved_theme_and_applies_a_new_one_instantly()
    {
        var app = Application.Current!;
        try
        {
            var settings = new Settings { Theme = "juicebar" };
            var window = new CookwareWindow(settings);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // The saved theme's radio is preselected.
            Assert.True(window.FindControl<RadioButton>("ThemeJuiceBar")!.IsChecked);
            Assert.False(window.FindControl<RadioButton>("ThemePorcelain")!.IsChecked);

            // Checking another radio applies live and updates the settings
            // object that Save later persists.
            window.FindControl<RadioButton>("ThemeGlossy")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("glossy", settings.Theme);
            Assert.Equal("glossy", ThemeManager.CurrentTheme);

            // The new light themes work the same way.
            window.FindControl<RadioButton>("ThemeLimonata")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("limonata", settings.Theme);
            Assert.Equal("limonata", ThemeManager.CurrentTheme);

            window.FindControl<RadioButton>("ThemeBerryMilk")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("berrymilk", settings.Theme);
            Assert.Equal("berrymilk", ThemeManager.CurrentTheme);

            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            ThemeManager.Apply(app, ThemeManager.DefaultTheme);
        }
    }

    [AvaloniaFact]
    public void An_unknown_saved_theme_preselects_the_default_in_the_picker()
    {
        var settings = new Settings { Theme = "from-a-newer-version" };
        var window = new CookwareWindow(settings);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.FindControl<RadioButton>("ThemeJuiceBar")!.IsChecked);
        Assert.False(window.FindControl<RadioButton>("ThemePorcelain")!.IsChecked);

        window.Close();
        Dispatcher.UIThread.RunJobs();
        ThemeManager.Apply(Application.Current!, ThemeManager.DefaultTheme);
    }

    private static ResourceDictionary LoadTheme(string file)
        => (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri($"avares://ZFood/Themes/{file}.axaml"));

    private static object? GetResource(Application app, string key)
    {
        Assert.True(app.TryGetResource(key, null, out var value), $"missing token {key}");
        return value;
    }
}
