namespace ZFood.Core;

/// <summary>Which member of a bidirectional field pair currently acts as the input.</summary>
public enum PairSide
{
    /// <summary>No member has a role yet (fresh start or after Reset).</summary>
    None,

    /// <summary>The first member (eaten grams, or gross scale reading) is the input.</summary>
    A,

    /// <summary>The second member (eaten calories, or net food weight) is the input.</summary>
    B,
}

/// <summary>
/// Last-edited-wins role tracking for a bidirectional pair. The field the user
/// most recently typed into is the input; its partner is computed. Merely
/// focusing a field never changes roles; only an accepted edit does.
/// </summary>
public sealed class PairRoleMachine
{
    /// <summary>The member currently acting as input, or None.</summary>
    public PairSide Input { get; private set; } = PairSide.None;

    /// <summary>Records an accepted user edit of the given member.</summary>
    public void UserEdited(PairSide side)
    {
        if (side != PairSide.None)
            Input = side;
    }

    /// <summary>Clears both roles (Reset).</summary>
    public void Reset() => Input = PairSide.None;
}
