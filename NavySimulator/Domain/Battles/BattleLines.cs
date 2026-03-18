namespace NavySimulator.Domain.Battles;

internal sealed record BattleLines(
    List<Ship> Screens,
    List<Ship> Capitals,
    List<Ship> Carriers,
    List<Ship> Submarines,
    List<Ship> Convoys)
{

    public List<Ship> AllAliveShips =>
    [
        .. Screens,
        .. Capitals,
        .. Carriers,
        .. Submarines,
        .. Convoys
    ];
}