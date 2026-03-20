using NavySimulator.Domain.Stats;

namespace NavySimulator.Domain;

public class Hull
{
    public string ID;
    public ShipRole Role;
    public List<string> Types;
    public ShipStats BaseStats;

    public Hull(string id, ShipRole role, List<string> types, ShipStats baseStats)
    {
        ID = id;
        Role = role;
        Types = types;
        BaseStats = baseStats;
    }
}


