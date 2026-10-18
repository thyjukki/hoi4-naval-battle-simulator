using NavySimulator.Engine.Results;

namespace NavySimulator.Web.Services;

/// <summary>Collects one ship's combat log from a <see cref="BattleView"/>, up to the view's hour.</summary>
public static class ShipLog
{
    public static IReadOnlyList<ShipLogEntry> Build(BattleView view, string shipId)
    {
        // Within an hour: state changes before firing (activation, retreat decision), then fire in the order it
        // happened, then the outcomes (sinking, escape).
        var entries = new List<(int Hour, int Phase, int Order, ShipLogEntry Entry)>();
        var order = 0;

        foreach (var shot in view.Shots)
        {
            if (shot.Hour > view.Hour)
            {
                break;
            }

            if (shot.ShooterId == shipId)
            {
                entries.Add((shot.Hour, 1, order++, FromShot(shot, ShipLogKind.Fired, shot.TargetId)));
            }
            else if (shot.TargetId == shipId)
            {
                entries.Add((shot.Hour, 1, order++, FromShot(shot, ShipLogKind.Received, shot.ShooterId)));
            }
        }

        foreach (var strike in view.AirStrikes.Where(strike => strike.Hour <= view.Hour))
        {
            if (strike.Source == shipId || strike.TargetId == shipId)
            {
                var outgoing = strike.Source == shipId;
                entries.Add((strike.Hour, 1, order++, new ShipLogEntry(
                    strike.Hour,
                    outgoing ? ShipLogKind.AirStrike : ShipLogKind.AirAttacked,
                    "Air",
                    outgoing ? strike.TargetId : strike.Source,
                    null,
                    strike.Hits > 0,
                    strike.HpDamage,
                    strike.HpDamage,
                    strike.OrganizationDamage,
                    strike.Critical,
                    null,
                    strike.KillingBlow,
                    $"{strike.Flying} flying, {strike.Intercepted} intercepted, {strike.ShotDownByAntiAir} shot down by AA, {strike.Hits} hits")));
            }
        }

        foreach (var battleEvent in view.Events.Where(battleEvent => battleEvent.Hour <= view.Hour && battleEvent.ShipId == shipId))
        {
            var phase = battleEvent.Kind is BattleEventKind.Activated or BattleEventKind.RetreatStarted ? 0 : 2;
            entries.Add((battleEvent.Hour, phase, order++, new ShipLogEntry(
                battleEvent.Hour,
                ShipLogKind.Event,
                string.Empty,
                battleEvent.OtherShipId,
                null,
                false,
                0,
                0,
                0,
                false,
                null,
                battleEvent.Kind == BattleEventKind.Sunk,
                battleEvent.Message,
                battleEvent.Kind)));
        }

        return entries
            .OrderBy(entry => entry.Hour)
            .ThenBy(entry => entry.Phase)
            .ThenBy(entry => entry.Order)
            .Select(entry => entry.Entry)
            .ToList();
    }

    private static ShipLogEntry FromShot(ShotRecord shot, ShipLogKind kind, string otherId)
    {
        return new ShipLogEntry(
            shot.Hour,
            kind,
            shot.Weapon.ToString(),
            otherId,
            shot.HitChance,
            shot.Hit,
            shot.Damage,
            shot.HpDamage,
            shot.OrganizationDamage,
            shot.Critical,
            shot.Hit ? shot.PiercingTier : null,
            shot.KillingBlow,
            string.Empty);
    }
}
