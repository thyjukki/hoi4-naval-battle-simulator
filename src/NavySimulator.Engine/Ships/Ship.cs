using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine;

/// <summary>
/// One ship in a battle: its design, stats after external modifiers, and mutable combat state.
/// Combat rules live in <c>Battles/Mechanics</c>; this type only holds data.
/// </summary>
public class Ship
{
    public string ID;
    public ShipDesign Design;
    public double InitialExperience;
    private ShipStats stats;

    public Ship(string id, ShipDesign design, double experience = 0)
    {
        ID = id;
        Design = design;
        InitialExperience = experience;
        MaxManpower = Math.Max(0, design.Hull.Manpower);
        stats = design.GetFinalStats();
        ResetCombatState();
    }

    public ShipRole Role => Design.Hull.Role;
    public int MaxManpower { get; }
    public int CurrentManpower { get; set; }
    public double Experience { get; set; }
    public double CurrentOrganization { get; set; }
    public double CurrentHP { get; set; }
    public ShipStatus CurrentStatus { get; set; }
    public double RetreatProgress { get; set; }
    public bool AttemptedRetreat { get; set; }

    /// <summary>Per weapon slot (<c>WeaponSlot</c> index) charge in cooldowns; a slot fires while charge >= cooldown.</summary>
    public double[] SlotCharge { get; } = new double[3];

    /// <summary>S-SUB: hours a submarine stays revealed; 0 = hidden. Always -1 for surface ships.</summary>
    public int RevealTimer { get; set; }

    public bool IsSunk => CurrentHP <= 0;
    public bool IsInBattle => !IsSunk && CurrentStatus != ShipStatus.Retreated;
    public bool IsVisible => RevealTimer != 0;

    /// <summary>Stats from hull, modules, MIO, researches and spirits. Experience and battle modifiers are applied by the combat rules.</summary>
    public ShipStats GetFinalStats()
    {
        return stats;
    }

    public void ApplyExternalModifiers(ShipStats statModifiers, ShipStats statAverages, ShipStats statMultipliers)
    {
        var updated = Design.GetFinalStats();
        updated = updated.Add(statModifiers);
        updated = updated.Add(statAverages);
        updated = updated.Scale(statMultipliers);
        stats = updated;
        ResetCombatState();
    }

    /// <summary>Restores full HP, organization, manpower and starting experience so a scenario can be re-run.</summary>
    public void ResetCombatState()
    {
        CurrentHP = stats.Hp;
        CurrentOrganization = stats.Organization;
        CurrentManpower = MaxManpower;
        Experience = InitialExperience;
        CurrentStatus = ShipStatus.Waiting;
        RetreatProgress = 0;
        AttemptedRetreat = false;
        Array.Clear(SlotCharge);
        RevealTimer = Role == ShipRole.Submarine ? 0 : -1;
    }
}
