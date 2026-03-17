namespace NavySimulator.Domain;

public class Hull
{
    public string ID;
    public ShipStats BaseStats;

    public Hull(string id, ShipStats baseStats)
    {
        ID = id;
        BaseStats = baseStats;
    }
}


