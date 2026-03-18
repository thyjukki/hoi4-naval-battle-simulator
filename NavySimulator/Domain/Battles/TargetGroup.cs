namespace NavySimulator.Domain.Battles;

internal sealed record TargetGroup(GroupType GroupType, List<Ship> Ships);