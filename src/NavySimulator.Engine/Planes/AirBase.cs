namespace NavySimulator.Engine;

/// <summary>
/// A-COVERAGE: a state's air base and the point land wings' coverage is measured from, the centre of the box of the
/// state's provinces (0xabce20), in map pixels (x from the left, y from the bottom).
/// </summary>
public sealed record AirBase(int State, string Name, int X, int Y);
