namespace NavySimulator.Engine.Mios;

/// <summary>
/// A military industrial organization a design can be assigned to (S-MIO): the equipment it covers, its initial trait
/// and its trait tree. Which traits are unlocked is the country's (<see cref="MioSetup"/>).
/// </summary>
public sealed class Mio(string id, IReadOnlyList<string> equipmentTypes, MioTrait? initialTrait, IReadOnlyList<MioTrait> traits)
{
    public string ID { get; } = id;

    /// <summary><c>equipment_type</c>: equipment types, archetypes and groups of the designs it can be assigned to (S-MIO-MATCH).</summary>
    public IReadOnlyList<string> EquipmentTypes { get; } = equipmentTypes;

    /// <summary><c>initial_trait</c>: always unlocked.</summary>
    public MioTrait? InitialTrait { get; } = initialTrait;

    public IReadOnlyList<MioTrait> Traits { get; } = traits;

    public MioTrait? Trait(string id)
    {
        return Traits.FirstOrDefault(trait => trait.ID == id);
    }
}
