namespace NavySimulator.Engine.Battles;

/// <summary>
/// A-REGION [LOG, unsettled]: what an escaped carrier's wings do in the region's air war. The scramble list (0xc66480)
/// takes carriers whose task force is in combat, and on escape the carrier's air object leaves the naval combat side
/// (0x1c6cc50); which task force the escaped ship is in afterwards is not traced, and the logged battles disagree.
/// </summary>
public enum EscapedCarrierWings
{
    /// <summary>Its wings stay in the region's air war as before the escape (best fit of the free-running populations).</summary>
    Stay,

    /// <summary>Its wings leave the region's air war (fits the `air_cv_land_mix` replays, not `air_cv_bombers_vs_fighters`).</summary>
    Leave,

    /// <summary>Its wings leave the scramble list but keep a region mission they had (the task force out of combat).</summary>
    RegionMissionOnly
}
