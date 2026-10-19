using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Mios;

/// <summary>An MIO policy (S-MIO-POLICY): equipment bonuses for the designs of the MIO it is attached to.</summary>
public sealed class MioPolicy(
    string id,
    IReadOnlyList<string> allowedEquipmentTypes,
    IReadOnlyList<(string Target, ShipStats Bonus)> equipmentBonuses,
    IReadOnlyList<string> notSimulated)
{
    public string ID { get; } = id;

    /// <summary>The <c>has_mio_equipment_type</c> conditions of <c>allowed</c>: one must name an MIO equipment type. Empty: any MIO.</summary>
    public IReadOnlyList<string> AllowedEquipmentTypes { get; } = allowedEquipmentTypes;

    /// <summary>
    /// <c>equipment_bonus</c> blocks: each reaches the designs of its equipment type, archetype or group, or every design
    /// of the MIO for <see cref="SameAsMio"/>.
    /// </summary>
    public IReadOnlyList<(string Target, ShipStats Bonus)> EquipmentBonuses { get; } = equipmentBonuses;

    /// <summary>Bonus keys with no simulator stat.</summary>
    public IReadOnlyList<string> NotSimulated { get; } = notSimulated;

    public const string SameAsMio = "same_as_mio";
}
