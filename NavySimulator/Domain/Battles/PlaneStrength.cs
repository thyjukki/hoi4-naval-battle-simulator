namespace NavySimulator.Domain.Battles;

public record PlaneStrength(int Fighters, int Bombers)
{

    public int Total => Fighters + Bombers;
}
