namespace ZFood.Core;

/// <summary>
/// The dish-binding latch. Exactly one pot row (or none) is the dish, the row
/// feeding the water section. While the recipe field is empty the binding
/// follows the last row the user typed in, clicked, or jumped to; once the
/// recipe field holds text the binding freezes and only an explicit act (Enter
/// in a row, a click, an accelerator) moves it. Special case for commutativity:
/// a recipe typed first parks, and the first row typed afterwards still becomes
/// the dish even though the latch is frozen, because there was nothing bound to
/// protect.
/// </summary>
public sealed class DishLatch
{
    /// <summary>Row id of the current dish, or null when the water section is dormant.</summary>
    public string? DishRowId { get; private set; }

    /// <summary>True while the recipe field holds text and the binding is frozen.</summary>
    public bool Frozen { get; private set; }

    /// <summary>Tracks whether the recipe field currently holds any text.</summary>
    public void RecipeChanged(bool recipeHasText) => Frozen = recipeHasText;

    /// <summary>
    /// The user typed in a row. Moves the binding only when it is free, or when
    /// nothing is bound yet. Returns true when the binding moved.
    /// </summary>
    public bool RowTyped(string rowId)
    {
        if (Frozen && DishRowId is not null)
            return false;
        return Move(rowId);
    }

    /// <summary>
    /// An explicit act (Enter in a row, click, accelerator) always moves the
    /// binding. Returns true when it actually changed. The caller is responsible
    /// for making a frozen-state move visually loud.
    /// </summary>
    public bool ExplicitBind(string rowId) => Move(rowId);

    /// <summary>The bound row disappeared; the water section goes dormant.</summary>
    public void RowRemoved(string rowId)
    {
        if (DishRowId == rowId)
            DishRowId = null;
    }

    /// <summary>Reset: dormant and free.</summary>
    public void Reset()
    {
        DishRowId = null;
        Frozen = false;
    }

    private bool Move(string rowId)
    {
        if (DishRowId == rowId)
            return false;
        DishRowId = rowId;
        return true;
    }
}
