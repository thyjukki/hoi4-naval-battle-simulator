namespace NavySimulator.GameData.Contracts;

/// <summary>
/// An MIO policy of <c>common/military_industrial_organization/policies</c> that a naval MIO can have attached, written
/// by <c>NavySimulator import-mios</c> to <c>mio-policies/</c> (S-MIO-POLICY).
/// </summary>
public class MioPolicyDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Stable Platforms".</summary>
    public string? Name { get; set; }

    /// <summary>
    /// The <c>has_mio_equipment_type</c> conditions of its <c>allowed</c> trigger: an MIO with one of these equipment types
    /// can take the policy. Empty: any MIO.
    /// </summary>
    public List<string> AllowedEquipmentTypes { get; set; } = [];

    /// <summary>
    /// <c>equipment_bonus</c> by equipment type, archetype or group, then by stat key; <c>same_as_mio</c> reaches every
    /// design the MIO is assigned to.
    /// </summary>
    public Dictionary<string, Dictionary<string, double>> EquipmentBonus { get; set; } = [];

    /// <summary><c>production_bonus</c>, keyed like <see cref="EquipmentBonus"/>; no effect in battle.</summary>
    public Dictionary<string, Dictionary<string, double>> ProductionBonus { get; set; } = [];

    /// <summary><c>organization_modifier</c>; no effect in battle.</summary>
    public Dictionary<string, double> OrganizationModifiers { get; set; } = [];

    /// <summary>The <c>allowed</c>, <c>visible</c> and <c>available</c> triggers as script text, e.g. <c>has_mio_size > 5</c>.</summary>
    public List<string> Conditions { get; set; } = [];
}
