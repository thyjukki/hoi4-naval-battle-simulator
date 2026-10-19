using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>
/// A trait of an MIO tree in a side's setup (S-MIO-TREE): unlocked, its parent groups holding, the unlocked traits its own
/// <c>mutually_exclusive</c> list names, and those of them that also list it (<see cref="Conflicts"/>).
/// </summary>
public sealed record MioTraitState(MioTraitDto Trait, bool Unlocked, bool ParentsMet, IReadOnlyList<string> ExcludedBy, IReadOnlyList<string> Conflicts)
{
    /// <summary>The game would let the side unlock it now.</summary>
    public bool Available => !Unlocked && ParentsMet && ExcludedBy.Count == 0;

    /// <summary>Unlocked although the game would not allow it (parents missing, or a trait that excludes it both ways unlocked).</summary>
    public bool Broken => Unlocked && (!ParentsMet || Conflicts.Count > 0);
}
