using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ZFood.App.Views;

/// <summary>
/// The one motion effect in the app: bind <see cref="TickProperty"/> to a
/// counter that increments on recompute and the control briefly gains the
/// "pulse" style class (~150 ms), tinting the computed value.
/// </summary>
public static class Pulse
{
    public static readonly AttachedProperty<int> TickProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("Tick", typeof(Pulse));

    static Pulse()
    {
        TickProperty.Changed.AddClassHandler<Control>((control, _) => Trigger(control));
    }

    public static int GetTick(Control control) => control.GetValue(TickProperty);

    public static void SetTick(Control control, int value) => control.SetValue(TickProperty, value);

    /// <summary>Fires the pulse imperatively (used for loud dish re-targets).</summary>
    public static void Trigger(Control control)
    {
        control.Classes.Add("pulse");
        DispatcherTimer.RunOnce(() => control.Classes.Remove("pulse"), TimeSpan.FromMilliseconds(150));
    }
}
