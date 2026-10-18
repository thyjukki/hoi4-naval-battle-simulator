using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>The combat log's shot filters. Hours are inclusive; the view's hour caps them.</summary>
public sealed class ShotFilter
{
    public int? FromHour { get; set; }
    public int? ToHour { get; set; }

    /// <summary>"all", "attacker" or "defender" (the shooting side).</summary>
    public string Side { get; set; } = "all";

    /// <summary>"all" or a <c>WeaponType</c> name.</summary>
    public string Weapon { get; set; } = "all";

    /// <summary>Matched against shooter and target IDs and display names, case-insensitive.</summary>
    public string Search { get; set; } = string.Empty;

    public bool HitsOnly { get; set; }
    public bool CriticalsOnly { get; set; }
    public bool KillsOnly { get; set; }

    public List<ShotRecord> Apply(BattleView view)
    {
        var to = Math.Min(ToHour ?? int.MaxValue, view.Hour);
        var from = FromHour ?? 0;
        var search = Search.Trim();
        var result = new List<ShotRecord>();

        foreach (var shot in view.Shots)
        {
            if (shot.Hour > to)
            {
                break;
            }

            if (shot.Hour < from ||
                (Side != "all" && shot.ShooterSide != Side) ||
                (Weapon != "all" && shot.Weapon.ToString() != Weapon) ||
                (HitsOnly && !shot.Hit) ||
                (CriticalsOnly && !shot.Critical) ||
                (KillsOnly && !shot.KillingBlow))
            {
                continue;
            }

            if (search.Length > 0 && !Matches(view, shot.ShooterId, search) && !Matches(view, shot.TargetId, search))
            {
                continue;
            }

            result.Add(shot);
        }

        return result;
    }

    public void Clear()
    {
        FromHour = null;
        ToHour = null;
        Side = "all";
        Weapon = "all";
        Search = string.Empty;
        HitsOnly = false;
        CriticalsOnly = false;
        KillsOnly = false;
    }

    private static bool Matches(BattleView view, string id, string search)
    {
        return id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               view.NameOf(id).Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
