using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Tests.Ships;

/// <summary>S-DESIGN: slots, forbidden categories, count limits and the ship type, on a cruiser hull shaped like TFB's and vanilla's.</summary>
public sealed class DesignRulesTests
{
    private static readonly StatModule LightBattery = Module("ship_light_battery_3", "ship_light_battery");
    private static readonly StatModule LightMediumBattery = Module("ship_light_medium_battery_3", "ship_light_medium_battery", forbidden: ["ship_medium_battery"]);
    private static readonly StatModule MediumBattery = Module("ship_medium_battery_3", "ship_medium_battery", ["capital_ship"], ["ship_light_battery"]);
    private static readonly StatModule Radar = Module("ship_radar_2", "ship_radar");
    private static readonly StatModule Engine = Module("cruiser_ship_engine_3", "cruiser_ship_engine");
    private static readonly StatModule LightArmor = Module("ship_armor_cruiser_3", "ship_cruiser_armor", forbidden: ["ship_medium_battery"]);
    private static readonly StatModule HeavyArmor = Module("ship_armor_cruiser_heavy_3", "ship_heavy_cruiser_armor", ["capital_ship"]);

    // TFB: the armor decides; vanilla: the medium battery's add_equipment_type decides.
    private static readonly ShipType TfbLightCruiser = new("light_cruiser", ShipRole.Screen, ["screen_ship"], 5, ["ship_hull_cruiser"], [LightArmor.ID], 40);
    private static readonly ShipType TfbHeavyCruiser = new("heavy_cruiser", ShipRole.Capital, ["capital_ship"], 6, ["ship_hull_cruiser"], [HeavyArmor.ID], 40);
    private static readonly ShipType VanillaLightCruiser = new("light_cruiser", ShipRole.Screen, ["screen_ship"], 5, ["ship_hull_cruiser"], [], 40);
    private static readonly ShipType VanillaHeavyCruiser = new("heavy_cruiser", ShipRole.Capital, ["capital_ship"], 6, ["ship_hull_cruiser"], [], 40);
    private static readonly ShipType Destroyer = new("destroyer", ShipRole.Screen, ["screen_ship"], 3, ["ship_hull_light"], [], 35);

    private static readonly Hull Cruiser = new(
        "ship_hull_cruiser_3",
        "ship_hull_cruiser",
        "screen_ship",
        1200,
        new ShipStats(Hp: 110, Speed: 30),
        [
            new ModuleSlot("fixed_ship_battery_slot", true, ["ship_light_battery", "ship_light_medium_battery", "ship_medium_battery"]),
            new ModuleSlot("fixed_ship_radar_slot", false, ["ship_radar"]),
            new ModuleSlot("fixed_ship_engine_slot", true, ["cruiser_ship_engine"]),
            new ModuleSlot("fixed_ship_armor_slot", true, ["ship_cruiser_armor", "ship_heavy_cruiser_armor"]),
            new ModuleSlot("mid_1_custom_slot", false, ["ship_light_battery", "ship_radar"]),
            new ModuleSlot("rear_1_custom_slot", false, ["ship_light_battery", "ship_radar"])
        ],
        [new ModuleCountLimit([], ["ship_radar"], "<", 2)]);

    [Fact]
    public void LegalLightCruiser_HasNoIssues()
    {
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", LightMediumBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", LightArmor), ("mid_1_custom_slot", LightBattery));

        Assert.Empty(check.Issues);
        Assert.Same(TfbLightCruiser, check.ShipType);
        Assert.True(check.ShipTypeMatches);
    }

    [Fact]
    public void ModuleOutsideTheSlotsCategories_IsReported()
    {
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", LightBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", LightArmor), ("mid_1_custom_slot", Engine));

        var issue = Assert.Single(check.Issues);
        Assert.Equal(DesignIssueKind.CategoryNotAllowed, issue.Kind);
        Assert.Equal("mid_1_custom_slot", issue.SlotID);
        Assert.True(issue.IsRestriction);
    }

    [Fact]
    public void EmptyRequiredSlots_AreReported()
    {
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", LightBattery), ("fixed_ship_armor_slot", LightArmor));

        var issue = Assert.Single(check.Issues);
        Assert.Equal(DesignIssueKind.RequiredSlotEmpty, issue.Kind);
        Assert.Equal("fixed_ship_engine_slot", issue.SlotID);
    }

    [Fact]
    public void LightCruiserArmor_ForbidsAMediumBattery()
    {
        // TFB: ship_armor_cruiser_X has forbid_module_categories = { ship_medium_battery }.
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", MediumBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", LightArmor));

        var issue = Assert.Single(check.Issues);
        Assert.Equal(DesignIssueKind.ForbiddenCategory, issue.Kind);
        Assert.Equal("fixed_ship_battery_slot", issue.SlotID);
        Assert.Equal(MediumBattery.ID, issue.ModuleID);
        Assert.Equal(LightArmor.ID, issue.Other);
    }

    [Fact]
    public void ForbiddenCategories_ApplyToEveryOtherSlot()
    {
        // Vanilla: a medium battery forbids light batteries anywhere on the design.
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", MediumBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", HeavyArmor), ("mid_1_custom_slot", LightBattery), ("rear_1_custom_slot", LightBattery));

        Assert.Equal(["mid_1_custom_slot", "rear_1_custom_slot"],
            check.Issues.Where(issue => issue.Kind == DesignIssueKind.ForbiddenCategory).Select(issue => issue.SlotID));
    }

    [Fact]
    public void CountLimits_CountMatchingSlots()
    {
        var oneRadar = Check(TfbTypes, ("fixed_ship_battery_slot", LightBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", LightArmor), ("fixed_ship_radar_slot", Radar));
        var twoRadars = Check(TfbTypes, ("fixed_ship_battery_slot", LightBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", LightArmor), ("fixed_ship_radar_slot", Radar), ("rear_1_custom_slot", Radar));

        Assert.Empty(oneRadar.Issues);
        Assert.Equal(DesignIssueKind.CountLimit, Assert.Single(twoRadars.Issues).Kind);
    }

    [Fact]
    public void CountLimits_MatchEmptySlotsAndAnyModule()
    {
        var atMostOneEmpty = new ModuleCountLimit([ModuleCountLimit.EmptyKeyword], [], "<", 2);
        var exactlyTwoModules = new ModuleCountLimit([ModuleCountLimit.AnyKeyword], [], "=", 2);

        Assert.True(atMostOneEmpty.Matches(null));
        Assert.False(atMostOneEmpty.Matches(LightBattery));
        Assert.True(exactlyTwoModules.Matches(LightBattery));
        Assert.False(exactlyTwoModules.Matches(null));
        Assert.True(exactlyTwoModules.Allows(2));
        Assert.False(exactlyTwoModules.Allows(3));
        Assert.True(new ModuleCountLimit([], ["ship_radar"], ModuleCountLimit.AnyKeyword, 0).Allows(99));
    }

    [Fact]
    public void Tfb_TheArmorPicksLightOrHeavyCruiser()
    {
        Assert.Same(TfbLightCruiser, DesignRules.ResolveShipType(Cruiser, [LightMediumBattery, LightArmor], TfbTypes).Type);
        Assert.Same(TfbHeavyCruiser, DesignRules.ResolveShipType(Cruiser, [MediumBattery, HeavyArmor], TfbTypes).Type);

        // A medium battery does not make a heavy cruiser in TFB: the light armor keeps it a light cruiser.
        Assert.Same(TfbLightCruiser, DesignRules.ResolveShipType(Cruiser, [MediumBattery, LightArmor], TfbTypes).Type);
    }

    [Fact]
    public void Vanilla_AddEquipmentTypeAndPriorityPickHeavyCruiser()
    {
        Assert.Same(VanillaLightCruiser, DesignRules.ResolveShipType(Cruiser, [LightBattery, LightArmor], VanillaTypes).Type);
        Assert.Same(VanillaHeavyCruiser, DesignRules.ResolveShipType(Cruiser, [MediumBattery, LightArmor], VanillaTypes).Type);
    }

    [Fact]
    public void NoMatchingType_FallsBackAndIsReported()
    {
        // TFB cruiser without armor: no type accepts it; the fallback keeps the hull's unit type (screen).
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", LightBattery), ("fixed_ship_engine_slot", Engine));

        Assert.Same(TfbLightCruiser, check.ShipType);
        Assert.False(check.ShipTypeMatches);
        Assert.Contains(check.Issues, issue => issue.Kind == DesignIssueKind.NoMatchingShipType && issue.Other == "light_cruiser");
        Assert.Null(DesignRules.ResolveShipType(Cruiser, [], [Destroyer]).Type);
    }

    [Fact]
    public void ModuleInASlotTheHullLacks_IsNotARestriction()
    {
        var check = Check(TfbTypes, ("fixed_ship_battery_slot", LightBattery), ("fixed_ship_engine_slot", Engine),
            ("fixed_ship_armor_slot", LightArmor), ("front_9_custom_slot", LightBattery));

        var issue = Assert.Single(check.Issues);
        Assert.Equal(DesignIssueKind.UnknownSlot, issue.Kind);
        Assert.False(issue.IsRestriction);
    }

    [Fact]
    public void ShipType_GivesRoleTypesAndUnscaledOrganization()
    {
        var hull = new Hull("ship_hull_cruiser_3", "ship_hull_cruiser", "screen_ship", 1200, new ShipStats(Hp: 100));
        var design = new ShipDesign("ca", hull, TfbHeavyCruiser, [Module("hp_plus", "x", multipliers: new ShipStats(Organization: 0.5, Hp: 0.5))]);

        var stats = design.GetFinalStats();

        Assert.Equal(ShipRole.Capital, design.Role);
        Assert.Equal(["heavy_cruiser", "ship_hull_cruiser_3", "ship_hull_cruiser"], design.Types);
        Assert.Equal(150, stats.Hp, 9);
        Assert.Equal(40, stats.Organization, 9);
    }

    private static IReadOnlyList<ShipType> TfbTypes => [TfbLightCruiser, TfbHeavyCruiser, Destroyer];

    private static IReadOnlyList<ShipType> VanillaTypes => [VanillaLightCruiser, VanillaHeavyCruiser, Destroyer];

    private static DesignCheck Check(IReadOnlyList<ShipType> types, params (string Slot, StatModule Module)[] modules)
    {
        return DesignRules.Check(Cruiser, modules.ToDictionary(entry => entry.Slot, entry => entry.Module), types);
    }

    private static StatModule Module(
        string id,
        string category,
        IReadOnlyList<string>? addedTypes = null,
        IReadOnlyList<string>? forbidden = null,
        ShipStats? multipliers = null)
    {
        return new StatModule(id, new ShipStats(), multipliers ?? new ShipStats(), null, category, addedTypes, forbidden);
    }
}
