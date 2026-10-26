using NavySimulator.Engine.Battles;
using NavySimulator.Engine.Battles.Air;
using NavySimulator.Engine.Battles.Mechanics;
using NavySimulator.Engine.Defines;
using NavySimulator.Engine.Environment;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Random;
using NavySimulator.Engine.Stats;
using NavySimulator.Engine.Tests.TestSupport;

namespace NavySimulator.Engine.Tests.Mechanics;

/// <summary>S-WEATHER, S-WEATHER-SIM, S-TERRAIN [RE]: the region's weather and naval terrain in the combat terms.</summary>
public class WeatherRulesTests
{
    private readonly GameDefines defines = GameDefines.Vanilla();

    // TFB's Black Sea in May: 50 % none, 25 % light rain, 10 % heavy rain, 12 to 30 degrees.
    private static readonly WeatherPeriod BlackSeaMay = new(0, 4, 30, 4, 12, 30, 0.5, 0.25, 0.1, 0, 0, 0, 0);

    [Fact]
    public void Roll_PicksByTheWeights_AndTheDurationFromTheSameDraw()
    {
        // [RE + LOG] The logged battles' first roll: U1 0.17168 is light rain for 24 × (5 + 0.17168 × 15) = 181.8 hours.
        var (weather, hours) = WeatherRules.Roll(BlackSeaMay, false, 0.17168, 0.9, defines.Weather);

        Assert.Equal(new RegionWeather(Weather.RainLight), weather);
        Assert.Equal(24 * (5 + 0.17168 * 15), hours, 9);
        Assert.Equal(Weather.RainHeavy, WeatherRules.Roll(BlackSeaMay, false, 0.4, 0.9, defines.Weather).Weather.Phenomenon);
        Assert.Equal(Weather.None, WeatherRules.Roll(BlackSeaMay, false, 0.5, 0.9, defines.Weather).Weather.Phenomenon);
    }

    [Fact]
    public void Roll_SnowsOnlyWhenCold()
    {
        var winter = new WeatherPeriod(0, 0, 30, 0, -20, -10, 0.5, 0.25, 0.1, 0.3, 0.1, 0, 0);
        var mild = winter with { MinTemperature = 2, MaxTemperature = 10 };

        // At -15 degrees no rain and full snow: x = 0.2 × (0.5 + 0.3 + 0.1) = 0.18 is snow.
        Assert.Equal(Weather.Snow, WeatherRules.Roll(winter, false, 0.2, 0.9, defines.Weather).Weather.Phenomenon);
        Assert.Equal(Weather.RainLight, WeatherRules.Roll(mild, false, 0.2, 0.9, defines.Weather).Weather.Phenomenon);
    }

    [Fact]
    public void Pattern_ChangesOnlyAtVisitsAfterTheSpell()
    {
        var region = new SeaRegion(30, "Black Sea", null, [new SeaProvince(7906, 3260, 1382)], [BlackSeaMay], 5632, 2048, 315);
        // Draws: phase 0.5 (hour 22 of 45), the start roll (light rain, 181.8 h) entered at 0.5 of the spell, then a clear roll.
        var random = new ScriptedRandomSource(0.5, 0.17168, 0.9, 0.5, 0.9, 0.9);
        var pattern = new WeatherPattern(region, new GameDate(1936, 5, 6, 13), defines, random);

        Assert.Equal(45, pattern.VisitHours);
        Assert.Equal(Weather.RainLight, pattern.At(0).Phenomenon);
        // The spell ends after 90.9 h; the next visits are hours 22, 67, 112.
        Assert.Equal(Weather.RainLight, pattern.At(111).Phenomenon);
        Assert.Equal(Weather.None, pattern.At(112).Phenomenon);
    }

    [Fact]
    public void RegionWeather_IsTheStaticBlocksWhole()
    {
        var rain = new RegionWeather(Weather.RainLight, ArcticWater: true).Modifiers(defines.StaticModifiers);

        Assert.Equal(-0.05, rain.Get(StaticModifiers.NavalHitChance), 9);
        Assert.Equal(0.05 - 0.05, rain.Get(StaticModifiers.NavalRetreatSpeed), 9);
        Assert.Equal(-0.2, rain.Get(StaticModifiers.Positioning), 9);
        Assert.Equal(2, new RegionWeather(Weather.RainLight).CombatType);
        Assert.Equal(0, new RegionWeather(Weather.Sandstorm).CombatType);
    }

    [Fact]
    public void ShipModifier_TakesTheWeatherTimesTheShipsWeatherFactor()
    {
        var environment = Rain();
        var normal = Screen("dd", weatherFactor: 1);
        var superHeavy = Screen("shbb", weatherFactor: 0);
        var trained = Screen("dd2", weatherFactor: 1, new ModifierSet([new(StaticModifiers.NavyWeatherPenalty, -0.4)]));

        Assert.Equal(-0.05, MemberModifierRules.ShipModifier(normal, StaticModifiers.NavalHitChance, environment, defines), 9);
        Assert.Equal(0, MemberModifierRules.ShipModifier(superHeavy, StaticModifiers.NavalHitChance, environment, defines), 9);
        Assert.Equal(-0.05 * 0.6, MemberModifierRules.ShipModifier(trained, StaticModifiers.NavalHitChance, environment, defines), 9);
    }

    [Fact]
    public void Rain_SlowsTheTarget_SoItsHitProfileGrows()
    {
        // [RE + LOG] The +4 % of the logged rain battles: naval_speed_factor -0.05 makes the target easier to hit.
        var target = Screen("cl", weatherFactor: 1);
        var dry = CombatStatRules.CombatSpeed(target, defines);

        Assert.Equal(dry * 0.95, CombatStatRules.CombatSpeed(target, defines, Rain()), 9);
        Assert.True(HitChanceRules.ShipHitProfile(target, defines, Rain()) > HitChanceRules.ShipHitProfile(target, defines));
    }

    [Fact]
    public void Air_TakesNavalStrikeAndCarrierTrafficWhole()
    {
        var heavy = new RegionWeather(Weather.RainHeavy);
        var environment = new BattleEnvironment(0, null, heavy, heavy.Modifiers(defines.StaticModifiers));

        Assert.Equal(1 - 0.2, AirRules.TargetingMultiplier(Rain(), defines), 9);
        Assert.Equal(1 - 0.4, AirRules.TargetingMultiplier(environment, defines), 9);
        Assert.Equal(1, AirRules.TargetingMultiplier(BattleEnvironment.None, defines), 9);
    }

    [Fact]
    public void Rain_LowersSurfaceDetection_AndSoTheDetectionPositioningBonus()
    {
        // S-POS-DETECT [RE + CAL] 0xc3aae0: detection × (1 + naval_detection); light rain −0.1 on a ship taking the weather
        // whole, none on one without the weather factor (TFB's super-heavies).
        var exposed = Screen("dd", 1);
        var immune = Screen("shbb", 0);
        var own = new BattleLines(Enumerable.Range(0, 10).Select(i => Screen($"own{i}", 1)));
        var enemy = new BattleLines(Enumerable.Range(0, 5).Select(i => Screen($"enemy{i}", 1)));

        Assert.Equal(18, CombatStatRules.SurfaceDetection(exposed, defines, Rain()), 9);
        Assert.Equal(20, CombatStatRules.SurfaceDetection(immune, defines, Rain()), 9);
        Assert.Equal(0.01, PositioningRules.DetectionBonus(own, enemy, defines), 9);
        Assert.Equal(0.009, PositioningRules.DetectionBonus(own, enemy, defines, Rain()), 9);
    }

    [Fact]
    public void Terrain_AdjustsAttackArmorAndSpeedBySubUnit_AndAddsItsKeys()
    {
        var terrain = new NavalTerrain("water_deep_ocean", new ModifierSet([new(StaticModifiers.Positioning, -0.05)]),
            new Dictionary<string, ModifierSet> { ["destroyer"] = new([new(StaticModifiers.NavyVisibility, -0.2)]) },
            new Dictionary<string, LeaderAdjuster> { ["destroyer"] = new(-0.2, -0.1, -0.25) });
        var environment = new BattleEnvironment(0, terrain);
        var destroyer = Screen("dd", weatherFactor: 1, subUnit: "destroyer");
        var cruiser = Screen("cl", weatherFactor: 1, subUnit: "light_cruiser");

        Assert.Equal(CombatStatRules.CombatValue(destroyer, 40, false, BattleEnvironment.None, defines) * 0.8 / 1.0,
            CombatStatRules.CombatValue(destroyer, 40, false, environment, defines), 9);
        Assert.Equal(CombatStatRules.CombatValue(destroyer, 40, true, BattleEnvironment.None, defines) * 0.9,
            CombatStatRules.CombatValue(destroyer, 40, true, environment, defines), 9);
        Assert.Equal(CombatStatRules.CombatSpeed(destroyer, defines) * 0.75, CombatStatRules.CombatSpeed(destroyer, defines, environment), 9);
        Assert.Equal(CombatStatRules.Visibility(destroyer, false, defines) * 0.8, CombatStatRules.Visibility(destroyer, false, defines, environment), 9);
        Assert.Equal(CombatStatRules.CombatSpeed(cruiser, defines), CombatStatRules.CombatSpeed(cruiser, defines, environment), 9);
        // The terrain's own keys reach every ship, and side-level reads whole.
        Assert.Equal(-0.05, MemberModifierRules.ShipModifier(cruiser, StaticModifiers.Positioning, environment, defines), 9);
        Assert.Equal(-0.05, environment.LocationModifier(StaticModifiers.Positioning), 9);
    }

    private BattleEnvironment Rain()
    {
        var rain = new RegionWeather(Weather.RainLight);
        return new BattleEnvironment(0, null, rain, rain.Modifiers(defines.StaticModifiers));
    }

    private static Ship Screen(string id, double weatherFactor, ModifierSet? country = null, string subUnit = "destroyer")
    {
        var stats = TestEntityFactory.ScreenStats() with { NavalWeatherPenaltyFactor = weatherFactor };
        var hull = new Hull($"{id}_hull", $"{id}_archetype", "screen_ship", 400, stats);
        var shipType = new ShipType(subUnit, ShipRole.Screen, ["screen_ship"], 0, [hull.Archetype], [], 40);
        // Experience level 1, the base level: no experience bonus or malus.
        var ship = new Ship(id, new ShipDesign($"{id}_design", hull, shipType, []), 0.2 * GameDefines.Vanilla().NNavy.NAVY_MAX_XP);
        ship.CountryModifiers = country ?? ModifierSet.Empty;
        return ship;
    }
}
