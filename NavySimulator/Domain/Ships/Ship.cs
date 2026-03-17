namespace NavySimulator.Domain;

public class Ship
{
    public string ID;
    public ShipDesign Design;
    public double CurrentOrganization;
    public double CurrentHP;

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
        var appliedDamage = Math.Max(0, damage);
        CurrentHP = Math.Max(0, CurrentHP - appliedDamage);
        CurrentOrganization = Math.Max(0, CurrentOrganization - appliedDamage * 0.5);
    }
}
