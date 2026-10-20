namespace NavySimulator.Engine.Battles.Replay;

/// <summary>
/// One air object's strike attempt in a logged game hour (S-REPLAY, A-*): the carrier (engine ship ID; null for
/// land-based planes), the planes of each of its wings when it began, whether it struck, and what the strike did: sortie
/// efficiency, target, planes shot down by fighters and by AA, strikers, and the HP and organization it took off.
/// Values the game did not reach (no target, no strike) are null.
/// </summary>
public sealed record ReplayAirStrike(
    string? CarrierId,
    IReadOnlyList<int> WingPlanes,
    bool Struck,
    double? Sortie,
    string? TargetId,
    int? Intercepted,
    int? ShotDown,
    int? Strikers,
    double HpDamage,
    double OrganizationDamage);
