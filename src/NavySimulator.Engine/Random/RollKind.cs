namespace NavySimulator.Engine.Random;

/// <summary>
/// What a random draw decides. The game tags every <c>random_fixed</c> call with its source line; a replay of a logged
/// battle (<see cref="ReplayRandomSource"/>) checks that the engine asks for the same kind of roll at the same point.
/// </summary>
public enum RollKind
{
    Unspecified,

    /// <summary>S-SUB: hourly reveal roll of a hidden submarine, or the reveal roll after it fires torpedoes.</summary>
    SubmarineReveal,

    /// <summary>S-RETREAT: retreat decision of a fighting ship below its HP threshold (game line 0xa83).</summary>
    RetreatDecision,

    /// <summary>S-TARGET-TORP: the one roll that decides how far a torpedo reaches (game line 0x3d9).</summary>
    TorpedoLine,

    /// <summary>S-TARGET: the weighted target pick (game line 0x37b).</summary>
    TargetPick,

    /// <summary>S-HIT: hit roll (game line 0x587).</summary>
    Hit,

    /// <summary>S-DMG-RANDOM: damage randomness (game line 0x593).</summary>
    DamageRandomness,

    /// <summary>S-CRIT: critical roll (game line 0x59f).</summary>
    Critical,

    /// <summary>S-CRIT-PART: whether a critical hit damages a part (game ship.cpp line 0x2f4).</summary>
    PartDamage,

    /// <summary>S-CRIT-PART: which part is damaged (game ship.cpp line 0x78f); the engine has no part data and does not roll it.</summary>
    PartPick,

    /// <summary>A-*: any roll of the air phase.</summary>
    Air,

    /// <summary>Rolls the game makes when a ship sinks (game line 0x40e, 6–9 per sinking); no combat effect, the engine makes none.</summary>
    Sinking
}
