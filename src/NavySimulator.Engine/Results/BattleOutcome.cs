namespace NavySimulator.Engine.Results;

public enum BattleOutcome
{
    AttackerVictory,
    DefenderVictory,
    BothSidesGone,
    Timeout,

    /// <summary>The battle is still being fought (a result taken from a <c>BattleSession</c> before it ended).</summary>
    InProgress
}
