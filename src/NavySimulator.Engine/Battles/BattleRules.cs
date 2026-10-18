namespace NavySimulator.Engine.Battles;

/// <summary>
/// Switches for mechanics whose in-game behavior is not yet confirmed. Each option names the spec item it belongs to
/// (docs/mechanics/*.md), so calibration runs can compare variants against recorded in-game battles.
/// Defaults are the best current reading of the sources.
/// </summary>
public sealed record BattleRules
{
    /// <summary>S-ORDER: how shots from both sides are sequenced inside an hour.</summary>
    public FiringOrder FiringOrder { get; init; } = FiringOrder.Interleaved;

    /// <summary>S-DMG-RANDOM: interpretation of COMBAT_DAMAGE_RANDOMNESS.</summary>
    public DamageRandomnessModel DamageRandomness { get; init; } = DamageRandomnessModel.WikiDecoded;

    /// <summary>
    /// S-ACTIVATE: when true, capital/screen activation delays only apply if the battle contains carriers (capitals) or
    /// carriers/capitals (screens), as the previous simulator did. The wiki states the delays unconditionally.
    /// </summary>
    public bool ActivationDependsOnComposition { get; init; }

    /// <summary>S-COOLDOWN: when true, a weapon waits one full cooldown after its line activates before the first shot.</summary>
    public bool FirstShotWaitsForCooldown { get; init; }

    /// <summary>S-NIGHT: apply the night hit chance penalty during night hours.</summary>
    public bool NightEnabled { get; init; } = true;

    /// <summary>Hour of day (0-23) when the battle starts. Battle hour 0 happens at this time of day.</summary>
    public int StartHourOfDay { get; init; } = 12;

    /// <summary>S-RETREAT: when false, ships never try to leave the battle.</summary>
    public bool RetreatEnabled { get; init; } = true;

    /// <summary>
    /// When a side has no ships left in the battle only because they retreated, put the retreated ships back in and keep fighting.
    /// Tooling option from the previous simulator, not a game mechanic.
    /// </summary>
    public bool ContinueAfterRetreat { get; init; }

    /// <summary>S-MANPOWER: apply DAILY_MANPOWER_GAIN_RATIO every 24 battle hours. The previous simulator did; in-game reinforcement during combat is unconfirmed.</summary>
    public bool DailyManpowerRecoveryInBattle { get; init; }

    /// <summary>Hard stop for the simulation, in hours. Not a game mechanic.</summary>
    public int MaxHours { get; init; } = 24 * 30;
}
