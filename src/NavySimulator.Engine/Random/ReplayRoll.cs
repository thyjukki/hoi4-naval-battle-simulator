namespace NavySimulator.Engine.Random;

/// <summary>One logged game roll: what it decided (from its source line), its value in [0, 1) and the game's line tag.</summary>
public readonly record struct ReplayRoll(RollKind Kind, double Value, int Line);
