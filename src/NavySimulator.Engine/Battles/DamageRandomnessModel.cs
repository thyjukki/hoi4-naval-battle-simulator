namespace NavySimulator.Engine.Battles;

/// <summary>
/// Interpretation of <c>COMBAT_DAMAGE_RANDOMNESS</c> (R).
/// </summary>
public enum DamageRandomnessModel
{
    /// <summary>damage * (1 + R * (U - 0.05)), U uniform in [0, 1): what hoi4.exe 1.19.3 does [RE].</summary>
    Game,

    /// <summary>No randomness; for deterministic tests.</summary>
    None
}
