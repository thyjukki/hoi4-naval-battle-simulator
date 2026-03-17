namespace NavySimulator.Domain;

public interface IModule
{
    public string ID { get; }
    public ShipStats StatModifiers { get; }
}
