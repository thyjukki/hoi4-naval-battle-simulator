namespace NavySimulator.Engine.Random;

/// <summary>
/// The first point in an hour where the engine's draws stopped following the game's: the engine asked for
/// <see cref="Requested"/> as roll <see cref="Position"/> of the hour, the game had rolled <see cref="Logged"/> there
/// (null: the game made no further roll). <see cref="Requested"/> is null when the engine finished the hour with game
/// rolls left over.
/// </summary>
public sealed record ReplayDivergence(int Position, RollKind? Requested, ReplayRoll? Logged);
