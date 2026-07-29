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
        if (Numeric.ParseNonNegative(value) is double grams)
            Model.Grams = grams;
    }

    partial void OnPinnedChanged(bool value) => _owner.OnItemPinChanged(this, value);
}

/// <summary>
/// The cookware CRUD behind the gear icon: add, rename, re-weigh, pin, order,
/// and delete pots. Edits apply to the settings object; the caller saves and
/// re-syncs the scale panel after the dialog closes.
/// </summary>
public partial class CookwareEditorViewModel : ObservableObject
{
    private readonly Settings _settings;
    private bool _revertingPin;

    [ObservableProperty]
    private CookwareItemViewModel? selected;

    [ObservableProperty]
    private string note = "";

    public CookwareEditorViewModel(Settings settings)
    {
        _settings = settings;
        foreach (var pot in settings.Cookware)
            Items.Add(new CookwareItemViewModel(this, pot));
        Selected = Items.FirstOrDefault();
    }

    public ObservableCollection<CookwareItemViewModel> Items { get; } = new();

    public void Add()
    {
        var pot = new Cookware { Name = "New pot" };
        _settings.Cookware.Add(pot);
        var item = new CookwareItemViewModel(this, pot);
        Items.Add(item);
        Selected = item;
        Note = "";
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
            Note = $"pin limit is {ScalePanelModel.PinnedCap}; unpin another pot first";
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
