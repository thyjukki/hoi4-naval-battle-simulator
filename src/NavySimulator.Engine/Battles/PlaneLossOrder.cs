namespace NavySimulator.Engine.Battles;

/// <summary>A-MIXED-REMOVE: which of a wing's planes a loss takes (RemovePlanes), by game version.</summary>
public enum PlaneLossOrder
{
    /// <summary>1.19.4 [RE + LOG] 0xfe7a10: each lost plane is a random pick weighted by the variants' plane counts.</summary>
    RandomPlane,

    /// <summary>1.19.3 [RE + LOG] 0xf63580: the oldest type's planes first, no draws.</summary>
    OldestFirst,
}
