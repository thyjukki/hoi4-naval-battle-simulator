using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Results;
using NavySimulator.Engine.Stats;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class DamageBreakdownTests : IDisposable
{
    private readonly TestWorkspace test = new();

    public void Dispose()
    {
        test.Dispose();
    }

    private static ShipRow Row(string id, string side, string design, ShipRole role)
    {
        return new ShipRow(0, side, id, "#1", design, role, new ShipStats(Hp: 100), 100, 30, 0, ShipStatus.Active, null, null, 0, 0, 0, 0, 0, 0);
    }

    private static ShotRecord Shot(int hour, string side, string shooter, WeaponType weapon, bool hit, double hp, double org = 1)
    {
        return new ShotRecord(hour, side, shooter, weapon, "target", GroupType.Screen, 0.5, 1, 1, hit, hit ? hp : 0, 1, false, false, hit ? hp : 0, hit ? org : 0, false);
    }

    private static AirStrikeRecord Strike(int hour, string side, string source, double hp)
    {
        return new AirStrikeRecord(hour, side, source, "target", 10, 0, 1, 2, 0.1, false, hp, 2, false);
    }

    [Fact]
    public void SplitsByWeaponRoleAndDesign_UpToTheHour()
    {
        var ships = new Dictionary<string, ShipRow>
        {
            ["dd"] = Row("dd", "attacker", "dd_design", ShipRole.Screen),
            ["bb"] = Row("bb", "attacker", "bb_design", ShipRole.Capital),
            ["cv"] = Row("cv", "attacker", "cv_design", ShipRole.Carrier)
        };
        ShotRecord[] shots =
        [
            Shot(1, "attacker", "dd", WeaponType.Light, true, 5),
            Shot(1, "attacker", "dd", WeaponType.Torpedo, false, 0),
            Shot(1, "defender", "enemy", WeaponType.Heavy, true, 50),
            Shot(2, "attacker", "bb", WeaponType.Heavy, true, 20),
            Shot(2, "attacker", "bb", WeaponType.Light, true, 3),
            Shot(9, "attacker", "bb", WeaponType.Heavy, true, 99)
        ];
        AirStrikeRecord[] strikes = [Strike(2, "attacker", "cv", 7), Strike(2, "attacker", "land-based", 11), Strike(9, "attacker", "cv", 100)];

        var breakdown = DamageBreakdownBuilder.Build("attacker", shots, strikes, ships, 2);

        Assert.Equal(["Light guns", "Heavy guns", "Torpedoes", "Air"], breakdown.ByWeapon.Select(share => share.Label));
        Assert.Equal(8, breakdown.ByWeapon[0].HpDamage);
        Assert.Equal(1, breakdown.ByWeapon[2].ShotsFired);
        Assert.Equal(0, breakdown.ByWeapon[2].Hits);
        Assert.Equal(18, breakdown.ByWeapon[3].HpDamage);
        Assert.Equal(2, breakdown.ByWeapon[3].AirStrikes);

        Assert.Equal(["Screens", "Capitals", "Carriers", DamageBreakdownBuilder.LandBasedPlanes], breakdown.ByRole.Select(share => share.Label));
        Assert.Equal(["bb_design", DamageBreakdownBuilder.LandBasedPlanes, "cv_design", "dd_design"], breakdown.ByDesign.Select(share => share.Label));
        Assert.Equal(46, breakdown.HpDamage);
        Assert.Equal(breakdown.HpDamage, breakdown.ByRole.Sum(share => share.HpDamage));
        Assert.Equal(breakdown.HpDamage, breakdown.ByDesign.Sum(share => share.HpDamage));
    }

    [Fact]
    public async Task InABattle_EverySplitAddsUpToTheSidesDamage()
    {
        test.Workspace.Scenario.Attacker.FleetID = "4cv_10bb_29_cl";
        test.Workspace.Scenario.Defender.FleetID = "2cv_25torpca_42_cl";
        var runner = new BattleRunner(test.Workspace);
        Assert.True(runner.Start(11));
        await runner.StepAsync(30);
        var view = runner.View()!;

        foreach (var side in view.Sides)
        {
            var damage = side.DamageDealt;
            var gunShots = view.Shots.Where(shot => shot.ShooterSide == side.Label).ToList();
            var air = view.AirStrikes.Where(strike => strike.Side == side.Label).ToList();
            var expected = gunShots.Sum(shot => shot.HpDamage) + air.Sum(strike => strike.HpDamage);

            Assert.True(expected > 0, $"{side.Label} dealt no damage");
            Assert.Equal(expected, damage.HpDamage, 6);
            Assert.Equal(expected, damage.ByRole.Sum(share => share.HpDamage), 6);
            Assert.Equal(expected, damage.ByDesign.Sum(share => share.HpDamage), 6);
            Assert.Equal(gunShots.Count, damage.ByWeapon.Sum(share => share.ShotsFired));
            Assert.Equal(air.Count, damage.ByRole.Sum(share => share.AirStrikes));
            Assert.Equal(expected, side.HpDamageDealt, 6);
            Assert.True(damage.ByRole.Count >= 2);
        }
    }

    [Fact]
    public async Task RewoundView_SplitsOnlyTheDamageUpToThatHour()
    {
        var runner = new BattleRunner(test.Workspace);
        runner.Start(4);
        await runner.StepAsync(20);

        var rewound = runner.View(8)!;

        Assert.Equal(rewound.Shots.Where(shot => shot.ShooterSide == "attacker" && shot.Hour <= 8).Sum(shot => shot.HpDamage),
            rewound.Attacker.DamageDealt.HpDamage, 6);
    }
}
