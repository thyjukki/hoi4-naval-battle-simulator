namespace NavySimulator.Domain;

public class MioBonus
{
    public string ID;
    public ShipStats PercentBonus;

    public MioBonus(string id, ShipStats percentBonus)
    {
        ID = id;
        PercentBonus = percentBonus;
    }
}


