using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZFood.Core;

namespace ZFood.App.ViewModels;

/// <summary>One cookware item in the editor, editing its model in place.</summary>
public partial class CookwareItemViewModel : ObservableObject
{
    private readonly CookwareEditorViewModel _owner;

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string gramsText;

    /// <summary>True while the weight text does not parse (the stored tare keeps its last valid value).</summary>
    [ObservableProperty]
    private bool gramsInvalid;

    [ObservableProperty]
    private bool pinned;

    public CookwareItemViewModel(CookwareEditorViewModel owner, Cookware model)
    {
        _owner = owner;
        Model = model;
        name = model.Name;
        gramsText = Numeric.FormatEditable(model.Grams);
        pinned = model.Pinned;
    }

    public Cookware Model { get; }

    partial void OnNameChanged(string value) => Model.Name = value;

    partial void OnGramsTextChanged(string value)
    {
        // Same validation rule as the main window's numeric fields: invalid
        // text never reaches the model, and the field is visibly flagged so
        // the mismatch with the stored tare is never silent.
        if (Numeric.ParseNonNegative(value) is double grams)
        {
            Model.Grams = grams;
            GramsInvalid = false;
        }
        else
        {
            GramsInvalid = true;
        }
    }

    partial void OnPinnedChanged(bool value) => _owner.OnItemPinChanged(this, value);
}

/// <summary>
/// The cookware CRUD behind the gear icon: add, rename, re-weigh, pin, order,
/// and delete cookware items. Edits apply to the settings object; Save
/// persists them through the injected saver, and the caller re-syncs the
/// scale panel after the dialog closes.
/// </summary>
public partial class CookwareEditorViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Func<bool> _save;
    private bool _revertingPin;

    [ObservableProperty]
    private CookwareItemViewModel? selected;

    [ObservableProperty]
    private string note = "";

    /// <summary>Name entered in the add row, pending an Add.</summary>
    [ObservableProperty]
    private string newName = "";

    /// <summary>Weight text entered in the add row, pending an Add.</summary>
    [ObservableProperty]
    private string newGramsText = "";

    /// <summary>True while the add row's weight text does not parse.</summary>
    [ObservableProperty]
    private bool newGramsInvalid;

    /// <summary>True after a successful save (shows the inline confirmation).</summary>
    [ObservableProperty]
    private bool saved;

    public CookwareEditorViewModel(Settings settings, Func<bool>? save = null)
    {
        _settings = settings;
        _save = save ?? (() => true);
        foreach (var item in settings.Cookware)
            Items.Add(new CookwareItemViewModel(this, item));
        Selected = Items.FirstOrDefault();
    }

    /// <summary>
    /// Persists the settings. On success the inline confirmation appears and
    /// the dialog may close; on failure it stays open with the warning note.
    /// </summary>
    public bool Save()
    {
        if (_save())
        {
            Note = "";
            Saved = true;
            return true;
        }

        Saved = false;
        Note = "couldn't save settings; changes may not survive a restart";
        return false;
    }

    public ObservableCollection<CookwareItemViewModel> Items { get; } = new();

    partial void OnNewGramsTextChanged(string value)
        => NewGramsInvalid = !string.IsNullOrWhiteSpace(value) && Numeric.ParseNonNegative(value) is null;

    /// <summary>
    /// Appends the add row's entry to the list and clears the row for the next
    /// one. Nothing else opens: the appended list row is the completion signal.
    /// Returns false (with the reason in <see cref="Note"/> or the invalid
    /// flag) when the entry is not usable yet.
    /// </summary>
    public bool AddNew()
    {
        var name = NewName.Trim();
        if (name.Length == 0)
        {
            Note = "enter a name for the new cookware";
            return false;
        }

        double grams = 0;
        if (!string.IsNullOrWhiteSpace(NewGramsText))
        {
            if (Numeric.ParseNonNegative(NewGramsText) is not double parsed)
            {
                NewGramsInvalid = true;
                Note = "the weight is not a number";
                return false;
            }

            grams = parsed;
        }

        var model = new Cookware { Name = name, Grams = grams };
        _settings.Cookware.Add(model);
        Items.Add(new CookwareItemViewModel(this, model));
        ReassignOrders();
        NewName = "";
        NewGramsText = "";
        NewGramsInvalid = false;
        Note = "";
        return true;
    }

    public void DeleteSelected()
    {
        if (Selected is not CookwareItemViewModel item)
            return;
        var index = Items.IndexOf(item);
        _settings.Cookware.Remove(item.Model);
        Items.Remove(item);
        Selected = Items.Count > 0 ? Items[Math.Min(index, Items.Count - 1)] : null;
        ReassignOrders();
    }

    public void MoveSelected(int direction)
    {
        if (Selected is not CookwareItemViewModel item)
            return;
        var index = Items.IndexOf(item);
        var target = index + direction;
        if (target < 0 || target >= Items.Count)
            return;
        Items.Move(index, target);
        _settings.Cookware.Remove(item.Model);
        _settings.Cookware.Insert(target, item.Model);
        ReassignOrders();
    }

    internal void OnItemPinChanged(CookwareItemViewModel item, bool nowPinned)
    {
        if (_revertingPin)
            return;

        if (nowPinned && Items.Count(i => i.Pinned) > ScalePanelModel.PinnedCap)
        {
            _revertingPin = true;
            item.Pinned = false;
            _revertingPin = false;
            Note = $"pin limit is {ScalePanelModel.PinnedCap}; unpin another cookware item first";
            return;
        }

        item.Model.Pinned = nowPinned;
        Note = "";
        ReassignOrders();
    }

    private void ReassignOrders()
    {
        for (var i = 0; i < Items.Count; i++)
            Items[i].Model.Order = i;
    }
}
