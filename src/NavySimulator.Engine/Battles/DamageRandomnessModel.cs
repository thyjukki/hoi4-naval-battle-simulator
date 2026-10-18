namespace NavySimulator.Engine.Battles;

/// <summary>
/// Interpretation of <c>COMBAT_DAMAGE_RANDOMNESS</c> (R). The sources disagree (docs/mechanics/surface.md, S-DMG-RANDOM).
/// </summary>
public enum DamageRandomnessModel
{
    /// <summary>damage * (1 + R * U(-0.05, 0.95)): the wiki's decoded 1.7.1 ProcessShootingAtTarget.</summary>
    WikiDecoded,

    /// <summary>damage * (1 - R * U(0, 1)): the define file comment ("max 10, randomness 30% gives 7-10").</summary>
    DefineComment,

    /// <summary>No randomness; useful for deterministic tests.</summary>
    None
}
