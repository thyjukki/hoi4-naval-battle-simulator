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

    /// <summary>A-TARGET: the air target pick, an integer draw (naval_fire_exchange_air.cpp line 0x43f) used as U = n / 2^31.</summary>
    AirTargetPick,

    /// <summary>
    /// A-MIXED-REMOVE (1.19.4): which plane a wing loses, an integer draw n (random.h line 0x95, at RemovePlanes 0xfe7af4)
    /// used as U = n / 2^31; the plane is the one at n mod the wing's planes.
    /// </summary>
    AirPlaneLoss,

    /// <summary>A-INTERCEPT: the roll that rounds the planes carrier fighters shoot down (line 0x27d).</summary>
    AirInterception,

    /// <summary>A-AA-PREEMPT: whether the target's AA fires before the strike (line 0x212).</summary>
    AirAntiAirChance,

    /// <summary>A-AA-PREEMPT: the roll that rounds the planes the target's AA shoots down (line 0x21c).</summary>
    AirAntiAirKills,

    /// <summary>A-HIT: the uniform factor of the hit count (line 0x45d).</summary>
    AirHits,

    /// <summary>A-CRIT: critical roll of an air strike (line 0x487).</summary>
    AirCritical,

    /// <summary>
    /// S-WEATHER-SIM: a draw of the simulated weather pattern (U1, U2 of the region's roll, the start phase). The game's
    /// weather has its own hash outside the combat update; the engine draws from a separate stream, never the battle's.
    /// </summary>
    Weather,

    /// <summary>
    /// A-REGION: the roll that rounds the planes one wing shoots down of another in the region's air war (airmission.cpp
    /// line 0xacf). The game makes it outside the naval combat update, so it is not in a battle's roll log.
    /// </summary>
    RegionAirLoss,

    /// <summary>
    /// A-ACCIDENT: a draw of the hourly air accident pass (the gate, the start pool, each wing's roll, the effect). The game
    /// draws them from a local generator seeded once an hour (strategicair.cpp 0x1be8), outside the logged rolls; a replay
    /// serves them from its fallback.
    /// </summary>
    AirAccident,

    /// <summary>A-ACE: whether a strike wing earns an ace after a strike (airmission.cpp line 0xb68).</summary>
    AirAceEarn,

    /// <summary>A-ACE: whether a wing's ace dies after a strike that lost planes (line 0xb52).</summary>
    AirAceDeath,

    /// <summary>
    /// A-ACE: which template a new ace gets. The game draws it from its own hash generator (seeded by the wing and the
    /// date), outside the logged rolls; a replay serves it from its fallback.
    /// </summary>
    AirAceTemplate,

    /// <summary>
    /// Rolls the game makes around the ace rolls with no combat effect: an ace killed by an enemy ace (line 0xb54, only
    /// after air-to-air combat), the promotion event seed (integer draw, line 0xb72) and once per strike (integer draw,
    /// line 0x1c9); the engine makes none.
    /// </summary>
    AirBookkeeping,

    /// <summary>Rolls the game makes when a ship sinks (game line 0x40e, 6–9 per sinking); no combat effect, the engine makes none.</summary>
    Sinking,

    /// <summary>
    /// S-RECOVERY: the ship recovery roll at a sinking (taskforce.cpp line 0xb31, an integer draw mixed into 0..99999),
    /// made only when the chance before the factor is above 0.
    /// </summary>
    ShipRecovery
}
