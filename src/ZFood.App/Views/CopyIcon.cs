using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using ZFood.Core;

namespace ZFood.App.Views;

/// <summary>
/// Behavior for the in-field copy buttons: while attached, the button is
/// enabled exactly when its host text box displays a copyable number (not
/// empty, not a placeholder dash), so every field gets the same affordance
/// without per-field wiring.
/// </summary>
public static class CopyIcon
{
    public static readonly AttachedProperty<bool> AutoEnableProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("AutoEnable", typeof(CopyIcon));

    private static readonly AttachedProperty<IDisposable?> SubscriptionProperty =
        AvaloniaProperty.RegisterAttached<Control, IDisposable?>("Subscription", typeof(CopyIcon));

    static CopyIcon()
    {
        AutoEnableProperty.Changed.AddClassHandler<Button>((button, e) =>
        {
            if (e.NewValue is true)
            {
                button.AttachedToVisualTree += OnAttached;
                button.DetachedFromVisualTree += OnDetached;
                if (button.GetVisualRoot() is not null)
                    Subscribe(button);
            }
            else
            {
                button.AttachedToVisualTree -= OnAttached;
                button.DetachedFromVisualTree -= OnDetached;
                Unsubscribe(button);
            }
        });
    }

    public static bool GetAutoEnable(Control control) => control.GetValue(AutoEnableProperty);

    public static void SetAutoEnable(Control control, bool value) => control.SetValue(AutoEnableProperty, value);

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => Subscribe((Button)sender!);

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => Unsubscribe((Button)sender!);

    private static void Subscribe(Button button)
    {
        Unsubscribe(button);
        if (button.FindAncestorOfType<TextBox>() is not TextBox host)
            return;
        button.SetValue(SubscriptionProperty, host.GetObservable(TextBox.TextProperty)
            .Subscribe(new AnonymousObserver<string?>(
                text => button.IsEnabled = Numeric.ParseDisplayed(text) is not null)));
    }

    private static void Unsubscribe(Button button)
    {
        button.GetValue(SubscriptionProperty)?.Dispose();
        button.SetValue(SubscriptionProperty, null);
    }
}
