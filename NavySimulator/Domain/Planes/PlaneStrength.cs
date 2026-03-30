namespace NavySimulator.Domain.Planes;

public record PlaneStrength(int Fighters, int Bombers)
{

    public int Total => Fighters + Bombers;
}
