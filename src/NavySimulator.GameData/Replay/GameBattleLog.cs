using System.Text.Json;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Replay;
using NavySimulator.Engine.Random;

namespace NavySimulator.GameData.Replay;

/// <summary>
/// A battle logged in the running game by <c>tools/ingame/run_battles.py</c> (a run folder with <c>meta.json</c> and
/// <c>raw.jsonl</c>), read for a replay (S-REPLAY). Per combat hour the logger records every member's state at the
/// start and the end of the hour, every roll of the combat update in call order with its source line, and every shot.
/// Ships are matched to the scenario by design: <c>make_save.py</c> names the game's variants after the design IDs.
/// </summary>
public sealed class GameBattleLog
{
    /// <summary>Source lines of the <c>random_fixed</c> calls in the naval combat update, hoi4.exe 1.19.3 ([LOG]).</summary>
    public static IReadOnlyDictionary<int, RollKind> RollLines { get; } = new Dictionary<int, RollKind>
    {
        [0x37b] = RollKind.TargetPick,
        [0x3d9] = RollKind.TorpedoLine,
        [0x587] = RollKind.Hit,
        [0x593] = RollKind.DamageRandomness,
        [0x59f] = RollKind.Critical,
        [0x2f4] = RollKind.PartDamage,
        [0x78f] = RollKind.PartPick,
        [0xa83] = RollKind.RetreatDecision,
        [0x40e] = RollKind.Sinking,
        [0x27d] = RollKind.AirInterception,
        [0x212] = RollKind.AirAntiAirChance,
        [0x21c] = RollKind.AirAntiAirKills,
        [0x45d] = RollKind.AirHits,
        [0x487] = RollKind.AirCritical,
        [0xb52] = RollKind.AirAceDeath,
        [0xb54] = RollKind.AirBookkeeping,
        [0xb68] = RollKind.AirAceEarn
    };

    /// <summary>
    /// Source lines of the integer draws (random int31, the same generator) in the combat update ([LOG], logged since
    /// 2026-10-20): 0x43f the air target pick (naval_fire_exchange_air.cpp), 0x96 with the sinking rolls, 0x1c9 once per
    /// air strike and 0xb72 after an ace is earned (the promotion event); only the target pick decides anything in the battle.
    /// </summary>
    public static IReadOnlyDictionary<int, RollKind> IntegerRollLines { get; } = new Dictionary<int, RollKind>
    {
        [0x43f] = RollKind.AirTargetPick,
        [0x96] = RollKind.Sinking,
        [0x1c9] = RollKind.AirBookkeeping,
        [0xb72] = RollKind.AirBookkeeping,
        [0x369] = RollKind.AirBookkeeping
    };

    // Game fixed point: values are raw / 100000.
    private const double Fx = 100000.0;

    private readonly List<JsonElement> hours;
    private readonly Dictionary<uint, (string Country, string Design)> ships;

    private GameBattleLog(List<JsonElement> hours, Dictionary<uint, (string Country, string Design)> ships)
    {
        this.hours = hours;
        this.ships = ships;
    }

    /// <summary>
    /// Hours logged for the main combat (the one with the most hours) that have a start-of-hour state. A battle the game
    /// creates during play runs one update with hour counter 0 first (no rolls, no shots); it is left out, so counter
    /// <c>c</c> is the engine's battle hour <c>c - 1</c> (S-HOUR).
    /// </summary>
    public int HourCount => hours.Count;

    public static GameBattleLog Read(string runDirectory)
    {
        using var meta = JsonDocument.Parse(File.ReadAllText(Path.Combine(runDirectory, "meta.json")));
        var ships = new Dictionary<uint, (string Country, string Design)>();

        foreach (var ship in meta.RootElement.GetProperty("ships").EnumerateObject())
        {
            ships[uint.Parse(ship.Name)] = (ship.Value.GetProperty("country").GetString() ?? string.Empty,
                ship.Value.GetProperty("design").GetString() ?? string.Empty);
        }

        var records = new List<JsonElement>();

        foreach (var line in File.ReadLines(Path.Combine(runDirectory, "raw.jsonl")))
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (root.TryGetProperty("type", out var type) && type.GetString() == "hour" &&
                root.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.Array &&
                root.GetProperty("hourAtUpdate").ValueKind == JsonValueKind.Number &&
                root.GetProperty("hourAtUpdate").GetInt32() >= 1)
            {
                records.Add(root.Clone());
            }
        }

        if (records.Count == 0)
        {
            throw new InvalidDataException($"{runDirectory} has no combat hours with start states (logged before rolls were recorded?).");
        }

        var main = records.GroupBy(record => record.GetProperty("combat").GetString()).MaxBy(group => group.Count())!.Key;
        return new GameBattleLog(records.Where(record => record.GetProperty("combat").GetString() == main).ToList(), ships);
    }

    /// <summary>Country of game side <paramref name="side"/> (0 attacker, 1 defender).</summary>
    public string Country(int side)
    {
        return Members(side).Select(member => ships[member].Country).FirstOrDefault() ?? string.Empty;
    }

    /// <summary>True when the scenario's attacker and defender have the designs of the game's attacker and defender.</summary>
    public bool SidesMatch(BattleScenario scenario)
    {
        return SameDesigns(0, scenario.Attacker) && SameDesigns(1, scenario.Defender);
    }

    /// <summary>
    /// Orders each scenario side's fleet like the game's members (firing order and target candidate order depend on it)
    /// and maps game ship IDs to engine ship IDs. The scenario must be built for this replay only.
    /// </summary>
    public IReadOnlyDictionary<uint, string> MapOnto(BattleScenario scenario)
    {
        if (!SidesMatch(scenario))
        {
            throw new InvalidOperationException(
                $"The game's attacker ({Country(0)}) and defender ({Country(1)}) do not have the designs of the scenario's attacker and defender.");
        }

        var map = new Dictionary<uint, string>();

        foreach (var (side, participant) in new[] { (0, scenario.Attacker), (1, scenario.Defender) })
        {
            var unused = participant.Fleet.Ships.ToList();
            var ordered = new List<Ship>();

            foreach (var member in Members(side))
            {
                var ship = unused.First(candidate => candidate.Design.ID == ships[member].Design);
                unused.Remove(ship);
                ordered.Add(ship);
                map[member] = ship.ID;
            }

            participant.Fleet.Ships.Clear();
            participant.Fleet.Ships.AddRange(ordered);
        }

        return map;
    }

    /// <summary>
    /// The logged hours with engine ship IDs (<paramref name="map"/> from <see cref="MapOnto"/>). An hour's end state is
    /// the next hour's start state when that was logged: the game adds the hour's experience gain and the daily
    /// manpower after the combat hour ends, where the engine's hour includes both.
    /// </summary>
    public IReadOnlyList<ReplayHour> Hours(IReadOnlyDictionary<uint, string> map)
    {
        var counted = CountedPlanes();

        return hours.Select((record, i) => new ReplayHour(
                record.GetProperty("hourAtUpdate").GetInt32() - 1,
                States(record.GetProperty("start"), map),
                Rolls(record.GetProperty("rolls")),
                Shots(record.GetProperty("shots"), map),
                i + 1 < hours.Count && hours[i + 1].GetProperty("hourAtUpdate").GetInt32() == record.GetProperty("hourAtUpdate").GetInt32() + 1
                    ? States(hours[i + 1].GetProperty("start"), map)
                    : States(record.GetProperty("sides"), map),
                Air(record, map),
                SideStates(record.GetProperty("sides")),
                SideStates(record.GetProperty("start")),
                i > 0 ? counted[i - 1] : null))
            .ToList();
    }

    /// <summary>
    /// airfx+0x5c of every air object at the end of each hour (raw.jsonl <c>airObjects</c>, logged since 2026-10-20), by
    /// the air object's pointer. Logs without the pointer there are matched by carrier and pool position to the
    /// pointers of the strike records seen so far.
    /// </summary>
    private List<Dictionary<string, int>?> CountedPlanes()
    {
        var pointers = new Dictionary<(uint Carrier, int Index), string>();
        var result = new List<Dictionary<string, int>?>();

        foreach (var record in hours)
        {
            if (record.TryGetProperty("air", out var air) && air.ValueKind == JsonValueKind.Array)
            {
                foreach (var group in air.EnumerateArray().Where(entry => entry.GetProperty("carrier").ValueKind == JsonValueKind.Number)
                             .GroupBy(entry => entry.GetProperty("carrier").GetUInt32()))
                {
                    var k = 0;

                    foreach (var entry in group)
                    {
                        pointers[(group.Key, k++)] = entry.GetProperty("fx").GetString()!;
                    }
                }
            }

            if (!record.TryGetProperty("airObjects", out var sides) || sides.ValueKind != JsonValueKind.Array)
            {
                result.Add(null);
                continue;
            }

            var planes = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var side in sides.EnumerateArray())
            {
                foreach (var group in side.EnumerateArray().Where(entry => entry.TryGetProperty("carrier", out var c) && c.ValueKind == JsonValueKind.Number)
                             .GroupBy(entry => entry.GetProperty("carrier").GetUInt32()))
                {
                    var k = 0;

                    foreach (var entry in group)
                    {
                        var pointer = entry.TryGetProperty("fx", out var fx) ? fx.GetString() : pointers.GetValueOrDefault((group.Key, k));
                        k++;

                        if (pointer is not null && entry.TryGetProperty("planes", out var count))
                        {
                            planes[pointer] = count.GetInt32();
                        }
                    }
                }
            }

            result.Add(planes);
        }

        return result;
    }

    // Null for logs without the side values (written before 2026-10-19).
    // A-ACE: each wing's ace; null when the log predates ace logging (2026-10-20 03:40).
    private static List<ReplayAce?>? Aces(JsonElement wings)
    {
        var list = wings.EnumerateArray().ToList();

        if (list.Count == 0 || !list.Any(wing => wing.TryGetProperty("disruption", out _)) || list.Any(wing => !wing.TryGetProperty("planes", out _)))
        {
            return null;
        }

        return list.Select(wing => wing.TryGetProperty("ace", out var ace)
                ? new ReplayAce(ace.TryGetProperty("alive", out var alive) && alive.GetInt32() != 0,
                    ace.TryGetProperty("chance", out var chance) ? chance.GetInt64() / Fx : null)
                : null)
            .ToList();
    }

    private static List<ReplaySideState>? SideStates(JsonElement sides)
    {
        if (sides.ValueKind != JsonValueKind.Array || sides.EnumerateArray().Any(side => !side.TryGetProperty("fleetAa", out _)))
        {
            return null;
        }

        return sides.EnumerateArray().Select(side => new ReplaySideState(
            side.GetProperty("positioning").GetInt64() / Fx, side.GetProperty("screening").GetInt64() / Fx,
            side.GetProperty("carrierScreening").GetInt64() / Fx, side.GetProperty("fleetAa").GetInt64() / Fx)).ToList();
    }

    /// <summary>
    /// The hour's air objects (raw.jsonl <c>air</c>, A-WINGS) with what their strikes did. The damage applied outside the
    /// shot loops (<c>damage</c>) belongs to the strikes that hit, in order (each applies its damage to its target once).
    /// </summary>
    private static List<ReplayAirStrike> Air(JsonElement record, IReadOnlyDictionary<uint, string> map)
    {
        var strikes = new List<ReplayAirStrike>();

        if (!record.TryGetProperty("air", out var air) || air.ValueKind != JsonValueKind.Array)
        {
            return strikes;
        }

        var damage = record.TryGetProperty("damage", out var hits) ? hits.EnumerateArray().ToList() : [];
        var next = 0;

        foreach (var entry in air.EnumerateArray())
        {
            string? Id(string key) => entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number &&
                                      map.TryGetValue(value.GetUInt32(), out var id) ? id : null;
            int? Int(string key) => entry.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

            var wings = entry.GetProperty("wings").EnumerateArray()
                .Select(wing => wing.TryGetProperty("planes", out var planes) ? planes.GetInt32() : 0)
                .ToList();
            var struck = entry.TryGetProperty("struck", out var flag) && flag.GetBoolean();
            var hp = 0.0;
            var org = 0.0;

            // Logs since 2026-10-20 03:00 attach the strike's damage to it; older ones list it per hour, in strike order, and
            // give the damage truncated to an integer ("hits" or "damageInt"; 0 can still be a hit below 1 HP).
            JsonElement? applied = entry.TryGetProperty("damage", out var own) && own.ValueKind == JsonValueKind.Object ? own : null;

            if (applied is null && struck && next < damage.Count && entry.TryGetProperty("target", out var target) &&
                target.ValueKind == JsonValueKind.Number && damage[next].GetProperty("target").GetUInt32() == target.GetUInt32())
            {
                var whole = Int("damageInt") ?? Int("hits");
                var loss = (damage[next].GetProperty("before").GetProperty("hp").GetInt64() - damage[next].GetProperty("after").GetProperty("hp").GetInt64()) / Fx;

                if (whole is null || whole > 0 || loss < 1)
                {
                    applied = damage[next];
                    next++;
                }
            }

            if (applied is { } hit)
            {
                var before = hit.GetProperty("before");
                var after = hit.GetProperty("after");
                hp = (before.GetProperty("hp").GetInt64() - after.GetProperty("hp").GetInt64()) / Fx;
                org = (before.GetProperty("org").GetInt64() - after.GetProperty("org").GetInt64()) / Fx;
            }

            strikes.Add(new ReplayAirStrike(Id("carrier"), wings, struck,
                entry.TryGetProperty("sortie", out var sortie) ? sortie.GetInt64() / Fx : null,
                Id("target"), Int("intercepted"), Int("shotDown"), Int("strikers"), hp, org,
                entry.TryGetProperty("external", out var external) && external.GetInt32() != 0,
                entry.TryGetProperty("efficiency", out var factor) ? factor.GetInt64() / Fx : null,
                entry.GetProperty("wings").EnumerateArray().Select(wing => wing.GetProperty("handle")[1].GetUInt32()).ToList(),
                entry.TryGetProperty("fx", out var fx) ? fx.GetString() : null,
                Aces(entry.GetProperty("wings")),
                entry.GetProperty("wings").EnumerateArray().All(wing => wing.TryGetProperty("disruption", out _))
                    ? entry.GetProperty("wings").EnumerateArray().Select(wing => wing.GetProperty("disruption").GetInt64() / Fx).ToList()
                    : null));
        }

        return strikes;
    }

    private bool SameDesigns(int side, BattleParticipant participant)
    {
        var game = Members(side).Select(member => ships[member].Design).Order(StringComparer.Ordinal);
        var engine = participant.Fleet.Ships.Select(ship => ship.Design.ID).Order(StringComparer.Ordinal);
        return game.SequenceEqual(engine, StringComparer.Ordinal);
    }

    private IEnumerable<uint> Members(int side)
    {
        return hours[0].GetProperty("start")[side].GetProperty("ships").EnumerateArray().Select(member => member.GetProperty("id").GetUInt32());
    }

    private static Dictionary<string, ReplayShipState> States(JsonElement sides, IReadOnlyDictionary<uint, string> map)
    {
        var states = new Dictionary<string, ReplayShipState>(StringComparer.Ordinal);

        foreach (var side in sides.EnumerateArray())
        {
            foreach (var member in side.GetProperty("ships").EnumerateArray())
            {
                if (map.TryGetValue(member.GetProperty("id").GetUInt32(), out var id))
                {
                    states[id] = State(member);
                }
            }
        }

        return states;
    }

    // Member state bits: 1 waiting, 2 fighting, 4 joining, 8 retreating, 16 escaped, 32 sunk.
    private static ReplayShipState State(JsonElement member)
    {
        var state = member.GetProperty("state").GetInt32();
        var gone = member.TryGetProperty("gone", out var goneValue) && goneValue.GetBoolean();
        var status = state switch
        {
            2 => ShipStatus.Active,
            8 => ShipStatus.Retreating,
            16 => ShipStatus.Retreated,
            32 => ShipStatus.Active,
            _ => ShipStatus.Waiting
        };

        if (gone)
        {
            // A member whose ship is gone was sunk unless it had escaped.
            return new ReplayShipState(status, state != 16, 0, 0, 0, 0, 0, [0, 0, 0]);
        }

        var charges = member.GetProperty("charge").EnumerateArray().Select(charge => charge.GetInt64() / Fx).ToArray();
        return new ReplayShipState(
            status,
            state == 32,
            member.GetProperty("hp").GetInt64() / Fx,
            member.GetProperty("org").GetInt64() / Fx,
            member.GetProperty("mp").GetInt32(),
            member.GetProperty("xp").GetInt64() / Fx,
            member.GetProperty("escape").GetInt64() / Fx,
            charges);
    }

    private static List<ReplayRoll> Rolls(JsonElement rolls)
    {
        return rolls.EnumerateArray()
            .Select(roll =>
            {
                var line = roll.GetProperty("line").GetInt32();

                // An integer draw n is used as U = n / 2^31 (0x24f04f0).
                if (roll.TryGetProperty("int", out var integer) && integer.GetBoolean())
                {
                    return new ReplayRoll(IntegerRollLines.GetValueOrDefault(line, RollKind.Unspecified), roll.GetProperty("value").GetInt64() / 2147483648.0, line);
                }

                return new ReplayRoll(RollLines.GetValueOrDefault(line, RollKind.Unspecified), roll.GetProperty("value").GetInt64() / Fx, line);
            })
            .ToList();
    }

    private static List<ReplayShot> Shots(JsonElement loops, IReadOnlyDictionary<uint, string> map)
    {
        var shots = new List<ReplayShot>();

        foreach (var loop in loops.EnumerateArray())
        {
            if (loop.GetProperty("shooter").ValueKind != JsonValueKind.Number ||
                !map.TryGetValue(loop.GetProperty("shooter").GetUInt32(), out var shooter) ||
                !map.TryGetValue(loop.GetProperty("target").GetUInt32(), out var target))
            {
                continue;
            }

            var weapon = loop.GetProperty("weapon").GetInt32() switch
            {
                0 => WeaponType.Heavy,
                1 => WeaponType.Torpedo,
                _ => WeaponType.Light
            };

            var profile = loop.GetProperty("profile");
            var critChance = profile.ValueKind == JsonValueKind.Object ? profile.GetProperty("critChance").GetInt64() : 0;
            // The profile stores the hit chance × 100.
            var hitChance = profile.ValueKind == JsonValueKind.Object && profile.TryGetProperty("hitChance", out var logged)
                ? logged.GetInt64() / Fx / 100
                : double.NaN;

            foreach (var shot in loop.GetProperty("shots").EnumerateArray())
            {
                var hits = shot.GetProperty("hits");
                var lines = shot.GetProperty("rolls").EnumerateArray()
                    .Select(roll => (Line: roll.GetProperty("line").GetInt32(), Value: roll.GetProperty("value").GetInt64()))
                    .ToList();
                // S-CRIT [RE]: a crit when the roll (0..99999) is at most the chance (fx5).
                var critical = lines.Any(roll => RollLines.GetValueOrDefault(roll.Line) == RollKind.Critical && roll.Value <= critChance);
                var partRolled = lines.Any(roll => RollLines.GetValueOrDefault(roll.Line) == RollKind.PartDamage);
                var partDamaged = lines.Any(roll => RollLines.GetValueOrDefault(roll.Line) == RollKind.PartPick);

                if (hits.GetArrayLength() == 0)
                {
                    shots.Add(new ReplayShot(shooter, target, weapon, false, 0, 0, HitChance: hitChance));
                    continue;
                }

                var before = hits[0].GetProperty("before");
                var after = hits[0].GetProperty("after");
                shots.Add(new ReplayShot(shooter, target, weapon, true,
                    (before.GetProperty("hp").GetInt64() - after.GetProperty("hp").GetInt64()) / Fx,
                    (before.GetProperty("org").GetInt64() - after.GetProperty("org").GetInt64()) / Fx,
                    critical, partRolled, partDamaged, hitChance));
            }
        }

        return shots;
    }
}
