using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ZFood.App.Services;
using ZFood.App.ViewModels;
using ZFood.Core;

namespace ZFood.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly AppServices _services;

    /// <summary>
    /// Runtime-loader and designer entry point. Builds a self-contained window
    /// over a throwaway data directory so no user data is touched; the app
    /// itself always uses the service-taking constructor.
    /// </summary>
    public MainWindow()
        : this(AppServices.Create(new AppPaths(ScratchDataDir())))
    {
    }

    private MainWindow(AppServices services)
        : this(new MainViewModel(services), services)
    {
    }

    public MainWindow(MainViewModel vm, AppServices services)
    {
        _vm = vm;
        _services = services;
        InitializeComponent();
        DataContext = vm;

        vm.CopyToClipboard = async text =>
        {
            if (Clipboard is { } clipboard)
                await clipboard.SetTextAsync(text);
        };

        vm.Portion.ComputedChanged += OnPortionComputedChanged;
        vm.Scale.DishRebound += OnDishRebound;

        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnAnyPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);

        Deactivated += (_, _) => _vm.OnWindowDeactivated();
        Closing += OnClosingWindow;
        Opened += (_, _) => FocusAndSelect(ServingGramsBox);
        SizeChanged += (_, e) => UpdateResponsiveClasses(e.NewSize);

        RestoreGeometry();
        UpdateResponsiveClasses(new Size(Width, Height));
    }

    // Two responsive presets, both style-driven: "narrow" drops the density
    // hero under the portion fields so nothing collides at small widths, and
    // "roomy" relaxes the vertical rhythm when a tall window opens the scale
    // card's flex band.
    private void UpdateResponsiveClasses(Size size)
    {
        SetClass("narrow", size.Width < 860);
        SetClass("roomy", size.Height >= 980);
    }

    private void SetClass(string name, bool on)
    {
        if (on)
        {
            if (!Classes.Contains(name))
                Classes.Add(name);
        }
        else
        {
            Classes.Remove(name);
        }
    }

    private static string ScratchDataDir()
        => Path.Combine(Path.GetTempPath(), "zfood-design-" + Guid.NewGuid().ToString("N"));

    private static GeoRect ToGeo(PixelRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    // -- Window geometry ---------------------------------------------------

    private void RestoreGeometry()
    {
        var saved = _services.Settings.Window;
        var screens = Screens.All.Select(s => ToGeo(s.WorkingArea)).ToList();
        if (saved is null || screens.Count == 0)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var primary = ToGeo((Screens.Primary ?? Screens.All[0]).WorkingArea);
        var rect = WindowGeometryMath.EnsureVisible(saved, screens, primary,
            (int)Width, (int)Height, (int)MinWidth, (int)MinHeight);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(rect.X, rect.Y);
        Width = rect.Width;
        Height = rect.Height;
        if (saved.Maximized)
            WindowState = WindowState.Maximized;
    }

    private void OnClosingWindow(object? sender, WindowClosingEventArgs e)
    {
        _vm.CommitAllChanged();

        var geometry = _services.Settings.Window ?? new WindowGeometry();
        if (WindowState == WindowState.Normal)
        {
            geometry.X = Position.X;
            geometry.Y = Position.Y;
            geometry.Width = (int)Width;
            geometry.Height = (int)Height;
        }

        geometry.Maximized = WindowState == WindowState.Maximized;
        _services.Settings.Window = geometry;
        _services.SaveSettings();
    }

    // -- Focus, selection, and unit tracking -------------------------------

    private TextBox? _pendingPointerSelectAll;

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (e.Source is TextBox box)
        {
            // Select-all on focus, so typing over a stale value costs nothing.
            // For pointer focus the click would immediately reset the caret, so
            // the selection is applied when the click completes instead.
            if (e.NavigationMethod == NavigationMethod.Pointer)
                _pendingPointerSelectAll = box;
            else
                Dispatcher.UIThread.Post(() => SelectAllAndCopy(box));
        }

        _vm.NotifyFocusedUnit(UnitFromSource(e.Source));
    }

    private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pendingPointerSelectAll is TextBox box)
        {
            _pendingPointerSelectAll = null;
            Dispatcher.UIThread.Post(() => SelectAllAndCopy(box));
        }
    }

    // Focus-select also copies: the freshly selected content, when it is a
    // valid number, lands on the clipboard with the quiet status-bar echo.
    private void SelectAllAndCopy(TextBox box)
    {
        box.SelectAll();
        _ = _vm.CopyBareNumberAsync(box.Text);
    }

    private string? UnitFromSource(object? source)
    {
        if (source is not Visual visual)
            return null;
        if (RowFromSource(visual) is CookwareRowModel row)
            return MainViewModel.UnitOfRow(row);
        if (IsWithin(visual, PortionPanel))
            return LogEntryFactory.PortionUnit;
        if ((ReferenceEquals(visual, RecipeBox) || ReferenceEquals(visual, DeltaBox)) && _vm.Scale.DishRow is CookwareRowModel dish)
            return MainViewModel.UnitOfRow(dish);
        return null;
    }

    private static bool IsWithin(Visual visual, Visual ancestor)
        => visual.GetVisualAncestors().Prepend(visual).Any(v => ReferenceEquals(v, ancestor));

    private CookwareRowModel? RowFromSource(object? source)
    {
        for (var v = source as Visual; v is not null; v = v.GetVisualParent())
        {
            if (ReferenceEquals(v, CookwareRowsControl))
                return null;
            if (v is StyledElement styled && styled.DataContext is CookwareRowModel row)
                return row;
        }

        return null;
    }

    private void FocusAndSelect(TextBox? box)
    {
        if (box is null)
            return;
        box.Focus();
        box.SelectAll();
    }

    private TextBox? RowInputBox(CookwareRowModel row)
    {
        var container = CookwareRowsControl.ContainerFromItem(row);
        if (container is null)
            return null;
        var wantNet = row.Input == PairSide.B && !row.IsNoCookware;
        var className = wantNet ? "rowNet" : "rowGross";
        return container.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(t => t.Classes.Contains(className));
    }

    // -- Keyboard ----------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandleShortcut(e))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private bool HandleShortcut(KeyEventArgs e)
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = _vm.CopyDeltaAsync();
            return true;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
            return HandleEnter(focused);

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (focused is TextBox { IsReadOnly: false } box)
            {
                // Esc clears just the focused field, and only while that field
                // holds the input role (or is a plain input field). On the
                // computed member of a pair it is a deliberate no-op: clearing
                // it would count as a typed edit, flip the roles, and wipe the
                // partner's user-entered value.
                if (!box.Classes.Contains("computed"))
                    box.Clear();
                return true;
            }

            return false;
        }

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            if (e.Key == Key.R)
            {
                _vm.Reset();
                FocusAndSelect(ServingGramsBox);
                return true;
            }

            if (e.Key == Key.L)
            {
                _vm.LogDrawerOpen = !_vm.LogDrawerOpen;
                return true;
            }

            if (e.Key >= Key.D1 && e.Key <= Key.D9)
                return FocusRowByIndex(e.Key - Key.D1);
            if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
                return FocusRowByIndex(e.Key - Key.NumPad1);
            if (e.Key is Key.D0 or Key.NumPad0)
                return FocusRow(_vm.Scale.NoCookwareRow);
        }

        if (e.KeyModifiers == KeyModifiers.Alt)
        {
            switch (e.Key)
            {
                case Key.P:
                    FocusAndSelect(ServingGramsBox);
                    return true;
                case Key.R:
                    FocusAndSelect(RecipeBox);
                    return true;
                case Key.M:
                    return ToggleMoreCookware();
            }
        }

        return false;
    }

    private bool HandleEnter(Visual? focused)
    {
        if (focused is null)
            return false;

        // The log lists and the More-cookware list handle Enter themselves.
        if (IsWithin(focused, RecentLogList) || IsWithin(focused, DrawerLogList) || IsWithin(focused, MoreCookwareList))
            return false;

        if (RowFromSource(focused) is CookwareRowModel row)
        {
            _vm.Scale.BindDish(row);
            _vm.CommitUnit(MainViewModel.UnitOfRow(row));
            FocusAndSelect(RecipeBox);
            return true;
        }

        if (ReferenceEquals(focused, RecipeBox) || ReferenceEquals(focused, DeltaBox))
        {
            if (_vm.Scale.DishRow is not null)
                _ = _vm.CopyDeltaAsync();
            return true;
        }

        if (IsWithin(focused, PortionPanel))
        {
            _vm.CommitUnit(LogEntryFactory.PortionUnit);
            return true;
        }

        return false;
    }

    private bool FocusRowByIndex(int index)
    {
        var rows = _vm.Scale.Rows.Where(r => !r.IsNoCookware).ToList();
        return index < rows.Count && FocusRow(rows[index]);
    }

    private bool FocusRow(CookwareRowModel row)
    {
        _vm.Scale.BindDish(row);
        FocusAndSelect(RowInputBox(row));
        return true;
    }

    private void OnMoreCookwareToggleClick(object? sender, RoutedEventArgs e) => ToggleMoreCookware();

    private bool ToggleMoreCookware()
    {
        if (!_vm.MoreCookwareVisible)
            return false;
        _vm.MoreCookwareOpen = !_vm.MoreCookwareOpen;
        if (_vm.MoreCookwareOpen)
        {
            // The expander content needs a layout pass before it can take focus.
            Dispatcher.UIThread.Post(() =>
            {
                if (MoreCookwareList.ItemCount == 0)
                    return;
                if (MoreCookwareList.SelectedIndex < 0)
                    MoreCookwareList.SelectedIndex = 0;
                var container = MoreCookwareList.ContainerFromIndex(MoreCookwareList.SelectedIndex);
                if (container is not null)
                    container.Focus();
                else
                    MoreCookwareList.Focus();
            }, DispatcherPriority.Loaded);
        }

        return true;
    }

    private void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        // Nothing the user enters is physically negative; the minus key is
        // rejected in every editable field.
        if (e.Text?.Contains('-') == true)
            e.Handled = true;
    }

    // Up/Down walk focus vertically through the fields (portion, cookware rows,
    // recipe, delta), so the whole app is operable with single keypresses.
    // Implemented as a tunnel handler so the text boxes never swallow the keys;
    // lists (log, More cookware) keep their own arrow behavior.
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down) || e.KeyModifiers != KeyModifiers.None)
            return;
        if (FocusManager?.GetFocusedElement() is not TextBox box)
            return;
        if (MoveVertically(box, e.Key == Key.Down ? 1 : -1))
            e.Handled = true;
    }

    private bool MoveVertically(TextBox from, int direction)
    {
        var grid = BuildFieldGrid();
        for (var row = 0; row < grid.Count; row++)
        {
            var column = Array.IndexOf(grid[row], from);
            if (column < 0)
                continue;

            var target = row + direction;
            if (target < 0 || target >= grid.Count)
                return true; // consume at the edges; focus stays put
            var fields = grid[target];
            FocusAndSelect(fields[Math.Min(column, fields.Length - 1)]);
            return true;
        }

        return false;
    }

    private List<TextBox[]> BuildFieldGrid()
    {
        var grid = new List<TextBox[]>
        {
            new[] { ServingGramsBox, ServingCaloriesBox },
            new[] { EatenGramsBox, EatenCaloriesBox },
        };

        foreach (var row in _vm.Scale.Rows)
        {
            var container = CookwareRowsControl.ContainerFromItem(row);
            if (container is null)
                continue;
            var boxes = container.GetVisualDescendants().OfType<TextBox>().ToList();
            var gross = boxes.FirstOrDefault(t => t.Classes.Contains("rowGross"));
            var net = boxes.FirstOrDefault(t => t.Classes.Contains("rowNet"));
            if (gross is null)
                continue;
            grid.Add(row.IsNoCookware || net is null ? new[] { gross } : new[] { gross, net });
        }

        grid.Add(new[] { RecipeBox });
        grid.Add(new[] { DeltaBox });
        return grid;
    }

    // -- Cookware rows -----------------------------------------------------

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A single click anywhere in a cookware row's band (except its pin star)
        // binds it as the dish and focuses its input field with the text
        // selected, so click-then-type always suffices.
        if (e.Source is not Visual visual
            || visual.GetVisualAncestors().Prepend(visual).OfType<Button>().Any()
            || RowFromSource(visual) is not CookwareRowModel row)
        {
            return;
        }

        _vm.Scale.BindDish(row);
        var onField = visual.GetVisualAncestors().Prepend(visual).OfType<TextBox>().Any();
        if (!onField)
            FocusAndSelect(RowInputBox(row));
    }

    private void OnStarClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual visual && RowFromSource(visual) is CookwareRowModel row)
            _vm.Scale.TogglePin(row);
    }

    private void OnMoreCookwareTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual visual
            && FindDataContext<Cookware>(visual) is Cookware item)
        {
            PromoteCookware(item);
            e.Handled = true;
        }
    }

    private void OnMoreCookwareKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && MoreCookwareList.SelectedItem is Cookware item)
        {
            PromoteCookware(item);
            e.Handled = true;
        }
    }

    private void PromoteCookware(Cookware item)
    {
        if (_vm.PromoteCookware(item) is not CookwareRowModel row)
            return;
        _vm.MoreCookwareOpen = false;
        // The fresh row needs a layout pass before its container exists.
        Dispatcher.UIThread.Post(() => FocusAndSelect(RowInputBox(row)), DispatcherPriority.Loaded);
    }

    private static T? FindDataContext<T>(Visual start)
        where T : class
    {
        for (var v = (Visual?)start; v is not null; v = v.GetVisualParent())
        {
            if (v is StyledElement { DataContext: T match })
                return match;
        }

        return null;
    }

    // -- Header buttons ----------------------------------------------------

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _vm.Reset();
        FocusAndSelect(ServingGramsBox);
    }

    private void OnLogClick(object? sender, RoutedEventArgs e) => _vm.LogDrawerOpen = !_vm.LogDrawerOpen;

    private async void OnGearClick(object? sender, RoutedEventArgs e)
    {
        var editor = new CookwareWindow(_services.Settings, () => _services.SaveSettings());
        await editor.ShowDialog(this);
        // Closing via the window decoration must still persist: saving through
        // the view model surfaces the status-bar warning when the write fails,
        // so edits never appear to succeed silently. After an in-dialog Save
        // this write is a harmless no-op re-save.
        _vm.SaveSettings();
        _vm.Scale.SyncFromSettings();
    }

    private void OnUseDishNetClick(object? sender, RoutedEventArgs e)
    {
        _vm.UseDishNet();
        FocusAndSelect(EatenGramsBox);
    }

    // -- In-field copy icons -----------------------------------------------

    private void OnCopyIconClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Visual visual
            && visual.GetVisualAncestors().OfType<TextBox>().FirstOrDefault() is TextBox box)
        {
            _ = _vm.CopyBareNumberAsync(box.Text);
        }
    }

    private void OnDeltaCopyIconClick(object? sender, RoutedEventArgs e) => _ = _vm.CopyDeltaAsync();

    // -- Log lists ---------------------------------------------------------

    private void OnLogListTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual visual && FindDataContext<LogRow>(visual) is LogRow row)
            _ = _vm.CopyLogRowAsync(row);
    }

    private void OnLogListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is ListBox { SelectedItem: LogRow row })
        {
            _ = _vm.CopyLogRowAsync(row);
            e.Handled = true;
        }
    }

    // -- Liveness ----------------------------------------------------------

    private void OnPortionComputedChanged(string field)
    {
        Control? target = field switch
        {
            nameof(PortionViewModel.EatenCaloriesText) => EatenCaloriesBox,
            nameof(PortionViewModel.EatenGramsText) => EatenGramsBox,
            nameof(PortionViewModel.DensityCalPerG) => DensityHero,
            _ => null,
        };
        if (target is not null)
            Pulse.Trigger(target);
    }

    private void OnDishRebound(CookwareRowModel? row, bool loud)
    {
        if (!loud)
            return;
        // A re-target while the recipe holds a value is never silent: the delta
        // and the caption flash together.
        Pulse.Trigger(DeltaBox);
        Pulse.Trigger(DishCaptionText);
    }
}
