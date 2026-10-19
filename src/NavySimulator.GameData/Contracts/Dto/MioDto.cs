namespace NavySimulator.GameData.Contracts;

/// <summary>
/// A military industrial organization (MIO) of <c>common/military_industrial_organization/organizations</c> that can be
/// assigned to ships, with its <c>include</c> resolved, written by <c>NavySimulator import-mios</c> to <c>mios/</c>
/// (S-MIO).
/// </summary>
public class MioDto
{
    public string ID { get; set; } = string.Empty;

    /// <summary>The game's name (localisation), e.g. "Brooklyn Naval Yard".</summary>
    public string? Name { get; set; }

    /// <summary>The country tags named in its <c>allowed</c> trigger (<c>tag</c>/<c>original_tag</c>), outside any <c>NOT</c>.</summary>
    public List<string> Countries { get; set; } = [];

    /// <summary>The country tags its <c>allowed</c> trigger names inside a <c>NOT</c>: an MIO for every other country.</summary>
    public List<string> ExcludedCountries { get; set; } = [];

    /// <summary><c>allowed = { always = no }</c>: an archetype other MIOs include; no country gets it in the game.</summary>
    public bool ArchetypeOnly { get; set; }

    /// <summary><c>allowed = { is_debug = yes }</c>: countries get it only when the game runs in debug mode.</summary>
    public bool DebugOnly { get; set; }

    /// <summary>The MIO this one was built from (<c>include</c>), if any.</summary>
    public string? Include { get; set; }

    /// <summary>
    /// <c>equipment_type</c>: the equipment types, archetypes and equipment groups whose designs the MIO can be assigned
    /// to (S-MIO-MATCH).
    /// </summary>
    public List<string> EquipmentTypes { get; set; } = [];

    /// <summary><c>initial_trait</c>: unlocked from the start.</summary>
    public MioTraitDto? InitialTrait { get; set; }

    /// <summary>The trait tree, in script order.</summary>
    public List<MioTraitDto> Traits { get; set; } = [];

    /// <summary>The <c>allowed</c>, <c>visible</c> and <c>available</c> triggers as script text; the simulator does not check them.</summary>
    public List<string> Conditions { get; set; } = [];
}
