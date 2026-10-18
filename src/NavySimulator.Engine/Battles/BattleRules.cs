namespace NavySimulator.Engine.Battles;

/// <summary>
/// Options for the parts of the model that are not settled by reverse-engineering, plus tooling switches.
/// Mechanics confirmed from hoi4.exe 1.19.3 are not configurable here (see docs/mechanics/surface.md).
/// </summary>
public sealed record BattleRules
{
    /// <summary>S-DMG-RANDOM: <see cref="DamageRandomnessModel.Game"/> is confirmed; <c>None</c> exists for tests.</summary>
    public DamageRandomnessModel DamageRandomness { get; init; } = DamageRandomnessModel.Game;

    /// <summary>S-NIGHT: apply the <c>night</c> static modifier while it is dark. The darkness model is [ASSUMED].</summary>
    public bool NightEnabled { get; init; } = true;

    /// <summary>Hour of day (0-23) at which the battle starts (battle hour 0).</summary>
    public int StartHourOfDay { get; init; } = 12;

    /// <summary>S-RETREAT: when false, ships never decide to retreat (tooling).</summary>
    public bool RetreatEnabled { get; init; } = true;

    /// <summary>
    /// When a side has no ships left in the battle only because they escaped, put the escaped ships back in and keep
    /// fighting. Tooling option, not a game mechanic.
    /// </summary>
    public bool ContinueAfterRetreat { get; init; }

    /// <summary>Safety stop in hours. The game has no maximum battle duration.</summary>
    public int MaxHours { get; init; } = 24 * 30;
}
