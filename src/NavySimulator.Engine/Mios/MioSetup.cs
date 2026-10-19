namespace NavySimulator.Engine.Mios;

/// <summary>A side's state of one MIO: the unlocked traits (the initial trait is always unlocked) and the attached policy (S-MIO).</summary>
public sealed class MioSetup(Mio mio, IReadOnlyList<MioTrait> unlockedTraits, MioPolicy? policy)
{
    public Mio Mio { get; } = mio;
    public IReadOnlyList<MioTrait> UnlockedTraits { get; } = unlockedTraits;
    public MioPolicy? Policy { get; } = policy;
}
