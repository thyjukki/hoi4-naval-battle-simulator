namespace NavySimulator.Web.Services;

/// <summary>
/// The damage one group (a weapon, a ship role or a design) dealt. Shots and hits count gun, torpedo and depth charge
/// fire; air strikes are counted separately.
/// </summary>
public sealed record DamageShare(string Label, double HpDamage, double OrganizationDamage, int ShotsFired, int Hits, int AirStrikes)
{
    public double HitRate => ShotsFired == 0 ? 0 : Hits / (double)ShotsFired;
}
