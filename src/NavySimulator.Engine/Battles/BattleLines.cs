namespace NavySimulator.Engine.Battles;

/// <summary>
/// A side's ships still in the battle, split into the four battle-line groups. Convoys are part of the carrier group
/// for targeting but are counted separately for screening.
/// </summary>
public sealed class BattleLines
{
    public BattleLines(IEnumerable<Ship> ships)
    {
        foreach (var ship in ships.Where(ship => ship.IsInBattle))
        {
            switch (ship.Role)
            {
                case ShipRole.Screen:
                    Screens.Add(ship);
                    break;
                case ShipRole.Capital:
                    Capitals.Add(ship);
                    break;
                case ShipRole.Carrier:
                    Carriers.Add(ship);
                    break;
                case ShipRole.Submarine:
                    Submarines.Add(ship);
                    break;
                case ShipRole.Convoy:
                    Convoys.Add(ship);
                    break;
            }
        }
    }

    public List<Ship> Screens { get; } = [];
    public List<Ship> Capitals { get; } = [];
    public List<Ship> Carriers { get; } = [];
    public List<Ship> Submarines { get; } = [];
    public List<Ship> Convoys { get; } = [];

    public int Count => Screens.Count + Capitals.Count + Carriers.Count + Submarines.Count + Convoys.Count;

    public IEnumerable<Ship> All => Screens.Concat(Capitals).Concat(Carriers).Concat(Convoys).Concat(Submarines);

    public static GroupType GetGroup(ShipRole role)
    {
        return role switch
        {
            ShipRole.Screen => GroupType.Screen,
            ShipRole.Capital => GroupType.Capital,
            ShipRole.Submarine => GroupType.Submarine,
            _ => GroupType.Carrier
        };
    }
}
