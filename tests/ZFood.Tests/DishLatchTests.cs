using ZFood.Core;

namespace ZFood.Tests;

public class DishLatchTests
{
    [Fact]
    public void Starts_dormant_and_free()
    {
        var latch = new DishLatch();
        Assert.Null(latch.DishRowId);
        Assert.False(latch.Frozen);
    }

    [Fact]
    public void Follows_typing_while_the_recipe_is_empty()
    {
        var latch = new DishLatch();
        Assert.True(latch.RowTyped("a"));
        Assert.Equal("a", latch.DishRowId);
        Assert.True(latch.RowTyped("b"));
        Assert.Equal("b", latch.DishRowId);
    }

    [Fact]
    public void Typing_in_the_bound_row_does_not_count_as_a_move()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        Assert.False(latch.RowTyped("a"));
        Assert.Equal("a", latch.DishRowId);
    }

    [Fact]
    public void Freezes_once_the_recipe_holds_text()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        latch.RecipeChanged(recipeHasText: true);

        Assert.False(latch.RowTyped("b"));
        Assert.Equal("a", latch.DishRowId);
    }

    [Fact]
    public void Explicit_bind_moves_even_while_frozen()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        latch.RecipeChanged(recipeHasText: true);

        Assert.True(latch.ExplicitBind("b"));
        Assert.Equal("b", latch.DishRowId);
    }

    [Fact]
    public void Recipe_first_commutativity_lets_the_first_typed_row_bind()
    {
        var latch = new DishLatch();
        latch.RecipeChanged(recipeHasText: true); // recipe typed first, parks

        Assert.True(latch.RowTyped("a"));
        Assert.Equal("a", latch.DishRowId);

        // But the second row typed no longer moves it.
        Assert.False(latch.RowTyped("b"));
        Assert.Equal("a", latch.DishRowId);
    }

    [Fact]
    public void Clearing_the_recipe_frees_the_latch_again()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        latch.RecipeChanged(recipeHasText: true);
        latch.RecipeChanged(recipeHasText: false);

        Assert.True(latch.RowTyped("b"));
        Assert.Equal("b", latch.DishRowId);
    }

    [Fact]
    public void Removing_the_bound_row_drops_to_dormant()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        latch.RowRemoved("a");
        Assert.Null(latch.DishRowId);
    }

    [Fact]
    public void Removing_an_unbound_row_changes_nothing()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        latch.RowRemoved("b");
        Assert.Equal("a", latch.DishRowId);
    }

    [Fact]
    public void Reset_returns_to_dormant_and_free()
    {
        var latch = new DishLatch();
        latch.RowTyped("a");
        latch.RecipeChanged(recipeHasText: true);
        latch.Reset();

        Assert.Null(latch.DishRowId);
        Assert.False(latch.Frozen);
    }
}
