namespace NavySimulator.Domain;

public class StatModule : IModule
{
    public string ID { get; }
    public ShipStats StatModifiers { get; }

    public StatModule(string id, ShipStats statModifiers)
    {
        ID = id;
        StatModifiers = statModifiers;
    }
}


