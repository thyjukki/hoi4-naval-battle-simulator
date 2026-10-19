namespace NavySimulator.Engine;

/// <summary>S-CRIT-PART: a part damaged on a ship and how many times (game ship +0x918).</summary>
public sealed class DamagedPart(CriticalPart part)
{
    public CriticalPart Part { get; } = part;
    public int Count { get; set; } = 1;
}
