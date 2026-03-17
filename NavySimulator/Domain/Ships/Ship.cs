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
}
