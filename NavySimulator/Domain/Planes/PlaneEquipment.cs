namespace NavySimulator.Domain;

public class PlaneEquipment
{
    public string ID;
    public PlaneStats Stats;

    public PlaneEquipment(string id, PlaneStats stats)
    {
        ID = id;
        Stats = stats;
    }
}
