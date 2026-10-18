namespace NavySimulator.Web.Services;

/// <summary>A side's damage dealt so far, split three ways. Each split adds up to the same totals.</summary>
public sealed class DamageBreakdown(IReadOnlyList<DamageShare> byWeapon, IReadOnlyList<DamageShare> byRole, IReadOnlyList<DamageShare> byDesign)
{
    public IReadOnlyList<DamageShare> ByWeapon { get; } = byWeapon;
    public IReadOnlyList<DamageShare> ByRole { get; } = byRole;
    public IReadOnlyList<DamageShare> ByDesign { get; } = byDesign;

    public double HpDamage => ByWeapon.Sum(share => share.HpDamage);
    public double OrganizationDamage => ByWeapon.Sum(share => share.OrganizationDamage);
}
