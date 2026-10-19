namespace NavySimulator.Engine.Leaders;

/// <summary>A parent requirement of a leader trait: at least <see cref="Needed"/> of <see cref="Traits"/> had first (S-ADMIRAL-TRAIT).</summary>
public sealed record LeaderTraitParent(IReadOnlyList<string> Traits, int Needed);
