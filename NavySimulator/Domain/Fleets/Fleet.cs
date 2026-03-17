
namespace NavySimulator.Domain;

public class Fleet
{
    public string ID;
    public List<Ship> Ships;

    public Fleet(string id, List<Ship> ships)
    {
        ID = id;
        Ships = ships;
    }
}


