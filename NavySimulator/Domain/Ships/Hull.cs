using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class Hull
{
    public string ID;
    public ShipRole Role;
    public ShipStats BaseStats;

    public Hull(string id, ShipRole role, ShipStats baseStats)
    {
        ID = id;
        Role = role;
        BaseStats = baseStats;
    }
}


