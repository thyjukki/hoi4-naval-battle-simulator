namespace NavySimulator.Engine.Results;

public enum BattleEventKind
{
    /// <summary>The ship's line activated: it starts firing next hour (convoys start escaping instead).</summary>
    Activated,

    /// <summary>The ship decided to retreat (S-RETREAT).</summary>
    RetreatStarted,

    /// <summary>The ship's retreat progress reached 1 and it left the battle.</summary>
    Escaped,

    /// <summary>The ship started retreating because the enemy only has hidden submarines left.</summary>
    Disengaged,

    /// <summary>The ship was sunk by a shot or an air strike.</summary>
    Sunk,

    /// <summary>A side fully retreated and the escaped ships were put back in (tooling option ContinueAfterRetreat).</summary>
    Reengaged
}
