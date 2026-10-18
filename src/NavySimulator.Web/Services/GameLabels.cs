using NavySimulator.Engine.Battles;

namespace NavySimulator.Web.Services;

/// <summary>The game's names for task force settings, as its localisation shows them.</summary>
public static class GameLabels
{
    /// <summary>Aggressiveness (<c>SHIP_ENGAGEMENT_BUTTON_AGGRESSIVENESS_1..5</c>).</summary>
    public static string Of(Aggressiveness aggressiveness)
    {
        return aggressiveness switch
        {
            Aggressiveness.DoNotEngage => "Do Not Engage",
            Aggressiveness.EngageAtLowRisk => "Engage At Low Risk",
            Aggressiveness.EngageAtMediumRisk => "Engage At Medium Risk",
            Aggressiveness.EngageAtHighRisk => "Engage At High Risk",
            Aggressiveness.AlwaysEngage => "Always Engage",
            _ => aggressiveness.ToString()
        };
    }

    /// <summary>Repair priority (<c>NAVY_REPAIR_POLICY_0..3</c>).</summary>
    public static string Of(RepairPriority repairPriority)
    {
        return repairPriority switch
        {
            RepairPriority.NeverRepair => "Never repair",
            RepairPriority.Low => "Repair priority - Low",
            RepairPriority.Medium => "Repair priority - Medium",
            RepairPriority.High => "Repair priority - High",
            _ => repairPriority.ToString()
        };
    }
}
