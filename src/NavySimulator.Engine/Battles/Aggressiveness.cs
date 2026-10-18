namespace NavySimulator.Engine.Battles;

/// <summary>
/// Task force aggressiveness, named as in the game's "Select aggressiveness" button: its engagement rule
/// (<c>SHIP_ENGAGEMENT_BUTTON_AGGRESSIVENESS_1..5</c>; save key <c>navy_engagement_rule</c>; game field
/// taskforce+0x394, values 0..4). It selects the in-combat retreat threshold (S-RETREAT) and which lines submarines may
/// attack (S-TARGET-SUB). The task force's repair priority is a separate setting that combat does not read.
/// </summary>
public enum Aggressiveness
{
    DoNotEngage = 0,
    EngageAtLowRisk = 1,
    EngageAtMediumRisk = 2,
    EngageAtHighRisk = 3,
    AlwaysEngage = 4
}
