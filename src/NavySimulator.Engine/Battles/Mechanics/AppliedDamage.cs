namespace NavySimulator.Engine.Battles.Mechanics;

public readonly record struct AppliedDamage(double HpDamage, double OrganizationDamage, int ManpowerLost, bool Sunk);
