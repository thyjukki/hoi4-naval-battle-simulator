namespace NavySimulator.Domain.Battles;

public class PlaneStrength
{
    public int Fighters;
    public int Bombers;

    public PlaneStrength(int fighters, int bombers)
    {
        Fighters = fighters;
        Bombers = bombers;
    }

    public int Total => Fighters + Bombers;
}
