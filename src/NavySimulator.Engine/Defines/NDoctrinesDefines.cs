namespace NavySimulator.Engine.Defines;

/// <summary>
/// <c>NDefines.NDoctrines</c> values the doctrine setup reads (S-DOCTRINE-MASTERY). Property names match the game keys
/// exactly so they can be bound from <c>common/defines/*.lua</c>. Defaults are vanilla 1.19.3.
/// </summary>
public sealed class NDoctrinesDefines
{
    /// <summary>How much mastery is required for unlocking a doctrine reward, if no override is set</summary>
    public double DEFAULT_REWARD_MASTERY { get; set; } = 100.0;
}
