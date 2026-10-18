using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>Splits a side's damage dealt up to an hour by weapon, by the shooter's role and by the shooter's design.</summary>
public static class DamageBreakdownBuilder
{
    public const string LandBasedPlanes = "Land-based planes";

    private static readonly WeaponType[] WeaponOrder = [WeaponType.Light, WeaponType.Heavy, WeaponType.Torpedo, WeaponType.DepthCharge];

    public static DamageBreakdown Build(
        string side,
        IEnumerable<ShotRecord> shots,
        IEnumerable<AirStrikeRecord> airStrikes,
        IReadOnlyDictionary<string, ShipRow> shipsById,
        int hour)
    {
        var byWeapon = new Totals();
        var byRole = new Totals();
        var byDesign = new Totals();

        foreach (var shot in shots)
        {
            if (shot.Hour > hour)
            {
                break;
            }

            if (shot.ShooterSide != side)
            {
                continue;
            }

            var shooter = shipsById.GetValueOrDefault(shot.ShooterId);
            byWeapon.AddShot(WeaponName(shot.Weapon), (int)shot.Weapon, shot);
            byRole.AddShot(shooter is null ? "?" : RoleName(shooter.Role), shooter is null ? 99 : (int)shooter.Role, shot);
            byDesign.AddShot(shooter?.DesignId ?? shot.ShooterId, 0, shot);
        }

        foreach (var strike in airStrikes.Where(strike => strike.Side == side && strike.Hour <= hour))
        {
            var carrier = shipsById.GetValueOrDefault(strike.Source);
            byWeapon.AddStrike("Air", WeaponOrder.Length, strike);
            byRole.AddStrike(carrier is null ? LandBasedPlanes : RoleName(carrier.Role), carrier is null ? 100 : (int)carrier.Role, strike);
            byDesign.AddStrike(carrier?.DesignId ?? LandBasedPlanes, 0, strike);
        }

        return new DamageBreakdown(byWeapon.InOrder(), byRole.InOrder(), byDesign.ByDamage());
    }

    public static string WeaponName(WeaponType weapon)
    {
        return weapon switch
        {
            WeaponType.Light => "Light guns",
            WeaponType.Heavy => "Heavy guns",
            WeaponType.Torpedo => "Torpedoes",
            _ => "Depth charges"
        };
    }

    public static string RoleName(ShipRole role)
    {
        return role switch
        {
            ShipRole.Screen => "Screens",
            ShipRole.Capital => "Capitals",
            ShipRole.Carrier => "Carriers",
            ShipRole.Submarine => "Submarines",
            _ => "Convoys"
        };
    }

    private sealed class Totals
    {
        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

        public void AddShot(string label, int order, ShotRecord shot)
        {
            var entry = Of(label, order);
            entry.Shots++;
            entry.Hits += shot.Hit ? 1 : 0;
            entry.Hp += shot.HpDamage;
            entry.Organization += shot.OrganizationDamage;
        }

        public void AddStrike(string label, int order, AirStrikeRecord strike)
        {
            var entry = Of(label, order);
            entry.Strikes++;
            entry.Hp += strike.HpDamage;
            entry.Organization += strike.OrganizationDamage;
        }

        public IReadOnlyList<DamageShare> InOrder()
        {
            return entries.OrderBy(entry => entry.Value.Order).Select(entry => entry.Value.Share(entry.Key)).ToList();
        }

        public IReadOnlyList<DamageShare> ByDamage()
        {
            return entries
                .OrderByDescending(entry => entry.Value.Hp)
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Value.Share(entry.Key))
                .ToList();
        }

        private Entry Of(string label, int order)
        {
            if (!entries.TryGetValue(label, out var entry))
            {
                entry = new Entry { Order = order };
                entries[label] = entry;
            }

            return entry;
        }
    }

    private sealed class Entry
    {
        public int Order;
        public int Shots;
        public int Hits;
        public int Strikes;
        public double Hp;
        public double Organization;

        public DamageShare Share(string label)
        {
            return new DamageShare(label, Hp, Organization, Shots, Hits, Strikes);
        }
    }
}
