namespace NavySimulator.Domain;

public class Ship
{
    public string ID;
    public ShipDesign Design;
    public double CurrentOrganization;
    public double CurrentHP;
    public ShipStatus CurrentStatus;
    public double RetreatProgress;

    public Ship(string id, ShipDesign design)
    {
        ID = id;
        Design = design;
        var effectiveStats = design.GetFinalStats();
        CurrentOrganization = effectiveStats.Organization;
        CurrentHP = effectiveStats.HP;
    }

    public Ship(ShipDesign design)
        : this(Guid.NewGuid().ToString("N"), design)
    {
    }

    public bool IsSunk => CurrentHP <= 0;

    public void ApplyDamage(double damage)
    {
        // Damage is applied against both the target's HP (by 60%[33]) and organization (by 100%[34]).
        // Additionally ships start out immune against organization damage, scaling up to the nominal value as the ship's HP level goes down.
        // As an example, assume a ship with 100/50 HP/Org receives multiple hits of 50 damage each.
        // The consecutive hits will do 30/0, 30/15, 30/30, and 30/45 HP/Org damage, with the fourth hit sinking the ship.
        
        var hpDamage = damage * Hoi4Defines.COMBAT_DAMAGE_TO_STR_FACTOR;
        CurrentHP = Math.Max(0, CurrentHP - hpDamage);
        
        var orgDamage = damage * Hoi4Defines.COMBAT_DAMAGE_TO_ORG_FACTOR;
        orgDamage *= 1.0 - (CurrentHP / Design.GetFinalStats().HP);
        
        CurrentOrganization = Math.Max(0, CurrentOrganization - orgDamage);
    }
}