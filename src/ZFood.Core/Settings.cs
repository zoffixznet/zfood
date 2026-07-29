namespace ZFood.Core;

/// <summary>A cookware item: freeform name plus empty (tare) weight in grams.</summary>
public sealed class Cookware
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public double Grams { get; set; }

    /// <summary>Pinned items render as always-visible rows on the main window.</summary>
    public bool Pinned { get; set; }

    /// <summary>Display order among pinned items (ascending).</summary>
    public int Order { get; set; }
}

/// <summary>Saved main-window geometry.</summary>
public sealed class WindowGeometry
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Maximized { get; set; }
}

/// <summary>
/// Everything persisted in settings.json. Note there is no selected-cookware
/// field: the dish binding is session state and is never persisted.
/// </summary>
public sealed class Settings
{
    public int Version { get; set; } = 1;

    public WindowGeometry? Window { get; set; }

    public List<Cookware> Cookware { get; set; } = new();

    /// <summary>Repairs null or out-of-range values after deserialization.</summary>
    public Settings Sanitized()
    {
        Cookware ??= new List<Cookware>();
        Cookware.RemoveAll(c => c is null);
        foreach (var c in Cookware)
        {
            c.Name ??= "";
            c.Id = string.IsNullOrWhiteSpace(c.Id) ? Guid.NewGuid().ToString("N") : c.Id;
            if (double.IsNaN(c.Grams) || double.IsInfinity(c.Grams) || c.Grams < 0)
                c.Grams = 0;
        }

        return this;
    }
}
