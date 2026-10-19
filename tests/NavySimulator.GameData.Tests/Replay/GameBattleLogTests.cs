using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Random;
using NavySimulator.GameData.Replay;

namespace NavySimulator.GameData.Tests.Replay;

public class GameBattleLogTests
{
    // Two ships, three logged updates: the creation update (counter 0), then counters 1 and 2. Values in fixed point.
    private const string Meta = """
        { "ships": { "11": { "country": "ROM", "design": "dd" }, "22": { "country": "BUL", "design": "cl" } } }
        """;

    private static string Member(int id, int state, long hp, long org, int mp, long xp = 0, long escape = 0) =>
        $$"""{ "id": {{id}}, "state": {{state}}, "hp": {{hp}}, "maxHp": 5000000, "org": {{org}}, "xp": {{xp}}, "mp": {{mp}}, "maxMp": 400, "charge": [0, 100000, 200000], "reveal": -1, "escape": {{escape}}, "joining": 0 }""";

    private static string Hour(int counter, string start, string end, string rolls, string shots) =>
        $$"""{ "type": "hour", "combat": "0x1", "tick": {{counter}}, "hourAtUpdate": {{counter}}, "hourAtEndHour": {{counter}}, "start": {{start}}, "sides": {{end}}, "rolls": {{rolls}}, "shots": {{shots}}, "damage": [], "ended": false }""".ReplaceLineEndings(" ");

    private static string Sides(string rom, string bul) => $$"""[{ "ships": [{{rom}}] }, { "ships": [{{bul}}] }]""";

    [Fact]
    public void Hours_SkipTheCreationUpdate_AndReadRollsShotsAndStates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "navysim-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var full = Sides(Member(11, 1, 5000000, 3500000, 400), Member(22, 1, 13200000, 4000000, 1200));
            var hit = """
                [{ "shooter": 11, "target": 22, "weapon": 2, "profile": { "critChance": 500 }, "sank": false,
                   "shots": [{ "rolls": [{ "line": 1415, "value": 1200 }, { "line": 1427, "value": 50000 }, { "line": 1439, "value": 400 }],
                               "hits": [{ "str": 1000000, "org": 0, "before": { "hp": 13200000, "org": 4000000 }, "after": { "hp": 12200000, "org": 3990000 } }] }] }]
                """;
            var rolls = """[{ "line": 891, "value": 25000, "loop": -1 }, { "line": 1415, "value": 1200, "loop": 0 }, { "line": 1038, "value": 7, "loop": 0 }]""";
            var after = Sides(Member(11, 2, 5000000, 3500000, 400), Member(22, 8, 12200000, 3990000, 1150, 0, 500));
            File.WriteAllText(Path.Combine(directory, "meta.json"), Meta);
            File.WriteAllLines(Path.Combine(directory, "raw.jsonl"),
            [
                """{ "type": "tick", "ticks": 1 }""",
                Hour(0, full, full, "[]", "[]"),
                Hour(1, full, after, rolls, hit),
                Hour(2, after, after, "[]", "[]")
            ]);

            var log = GameBattleLog.Read(directory);
            var hours = log.Hours(new Dictionary<uint, string> { [11] = "rom_dd", [22] = "bul_cl" });

            Assert.Equal(2, log.HourCount);
            Assert.Equal("ROM", log.Country(0));
            Assert.Equal([0, 1], hours.Select(hour => hour.Hour));

            var first = hours[0];
            Assert.Equal([RollKind.TargetPick, RollKind.Hit, RollKind.Sinking], first.Rolls.Select(roll => roll.Kind));
            Assert.Equal(0.25, first.Rolls[0].Value, 9);

            var shot = Assert.Single(first.Shots);
            Assert.Equal(("rom_dd", "bul_cl", WeaponType.Light, true), (shot.ShooterId, shot.TargetId, shot.Weapon, shot.Hit));
            Assert.Equal(10, shot.HpDamage, 9);
            Assert.Equal(0.1, shot.OrganizationDamage, 9);
            Assert.True(shot.Critical);

            // The end of hour 0 is the start of hour 1 (the game adds experience and daily manpower after the hour).
            var end = first.End["bul_cl"];
            Assert.Equal((ShipStatus.Retreating, 122.0, 1150, 0.005), (end.Status, end.Hp, end.Manpower, end.RetreatProgress));
            Assert.Equal([0.0, 1.0, 2.0], first.Start["rom_dd"].Charges);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
