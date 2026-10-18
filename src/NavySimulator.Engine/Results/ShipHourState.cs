namespace NavySimulator.Engine.Results;

/// <summary>A ship's state at the end of a battle hour (recorded when a session keeps ship history).</summary>
public readonly record struct ShipHourState(double Hp, double Organization, ShipStatus Status, double Experience)
{
    public bool Sunk => Hp <= 0;
}
