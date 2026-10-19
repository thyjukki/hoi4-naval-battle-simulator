namespace NavySimulator.Engine.Mios;

/// <summary>A parent requirement of an MIO trait: at least <see cref="Needed"/> of <see cref="Traits"/> unlocked (S-MIO-TREE).</summary>
public sealed record MioTraitParent(IReadOnlyList<string> Traits, int Needed);
