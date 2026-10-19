using NavySimulator.Engine;
using NavySimulator.GameData.Contracts;
using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

/// <summary>The Designs page's slot editor over the repository's (TFB) hulls and modules.</summary>
public sealed class DesignSlotsTests : IDisposable
{
    private readonly TestWorkspace test = new();

    private Workspace Workspace => test.Workspace;

    public void Dispose()
    {
        test.Dispose();
    }

    [Fact]
    public void Rows_ListTheHullsSlots_FixedFirst_WithTheirModules()
    {
        var design = Workspace.FindDesign("cl3_armor4_full_la")!;

        var rows = DesignSlots.Rows(Workspace.Catalog, Workspace.Library, design);

        Assert.Equal(Workspace.FindHull(design.HullID)!.Slots.Count, rows.Count);
        Assert.True(rows.TakeWhile(row => DesignSlots.IsFixed(row.Slot)).Count() == rows.Count(row => DesignSlots.IsFixed(row.Slot)));
        Assert.Equal("ship_armor_cruiser_3", rows.Single(row => row.Slot.ID == "fixed_ship_armor_slot").ModuleID);
        Assert.All(rows, row => Assert.Empty(row.Issues));
    }

    [Fact]
    public void Options_OfferOnlyTheSlotsCategories_UnlessRestrictionsAreOverridden()
    {
        var armorSlot = Workspace.FindHull("ship_hull_torpedo_cruiser")!.Slots.Single(slot => slot.ID == "fixed_ship_armor_slot");

        var allowed = DesignSlots.Options(Workspace.Catalog, armorSlot, allowAnyModule: false);
        var all = DesignSlots.Options(Workspace.Catalog, armorSlot, allowAnyModule: true);

        Assert.All(allowed, module => Assert.Equal("ship_heavy_cruiser_armor", module.Category));
        Assert.Equal(Workspace.Catalog.Modules.Count, all.Count);
        Assert.Equal(allowed, all.Take(allowed.Count));
    }

    [Fact]
    public void ChangeHull_KeepsTheModulesTheNewHullTakes()
    {
        var design = Workspace.Clone(Workspace.FindDesign("cl3_armor4_full_la")!);

        var removed = DesignSlots.ChangeHull(Workspace.Catalog, design, "ship_hull_torpedo_cruiser");

        // The torpedo cruiser takes only medium batteries, heavy cruiser armor and torpedoes, AA or catapults in custom slots.
        Assert.Equal("ship_hull_torpedo_cruiser", design.HullID);
        Assert.False(design.Modules.ContainsKey("fixed_ship_battery_slot"));
        Assert.False(design.Modules.ContainsKey("fixed_ship_armor_slot"));
        Assert.Equal("cruiser_ship_engine_3", design.Modules["fixed_ship_engine_slot"]);
        Assert.Contains(removed, module => module.StartsWith("Cruiser Armor III", StringComparison.Ordinal));
    }

    [Fact]
    public void ChangeHull_WithRestrictionsOverridden_KeepsEveryModuleWithASlot()
    {
        var design = Workspace.Clone(Workspace.FindDesign("cl3_armor4_full_la")!);
        design.AllowAnyModule = true;

        DesignSlots.ChangeHull(Workspace.Catalog, design, "ship_hull_torpedo_cruiser");

        Assert.Equal("ship_armor_cruiser_3", design.Modules["fixed_ship_armor_slot"]);
    }

    [Fact]
    public void UseDefaults_FitsTheHullsDefaultVariant_InSlotOrder()
    {
        var design = new ShipDesignDto { ID = "x", HullID = "ship_hull_torpedo_cruiser" };

        DesignSlots.UseDefaults(Workspace.Catalog, design);

        Assert.Equal("ship_armor_cruiser_heavy_1", design.Modules["fixed_ship_armor_slot"]);
        Assert.Equal("ship_torpedo_1", design.Modules["mid_1_custom_slot"]);
        var order = Workspace.FindHull("ship_hull_torpedo_cruiser")!.Slots.Select(slot => slot.ID).ToList();
        Assert.Equal(design.Modules.Keys.OrderBy(order.IndexOf), design.Modules.Keys);
    }

    [Fact]
    public void SettingTheArmor_ChangesTheShipType()
    {
        var design = Workspace.Clone(Workspace.FindDesign("torpedo_cruiser_torpattack")!);

        Assert.Equal("heavy_cruiser", Workspace.ShipTypeOf(design).Type!.ID);
        Assert.Empty(Workspace.Library.CheckDesign(design)!.Issues);

        DesignSlots.SetModule(Workspace.Catalog, design, "fixed_ship_armor_slot", "ship_armor_cruiser_4");

        Assert.Equal("light_cruiser", Workspace.ShipTypeOf(design).Type!.ID);
        Assert.Equal(ShipRole.Screen, Workspace.RoleOf(design));
    }

    [Fact]
    public void Issues_AreDescribedWithTheGamesNames()
    {
        // TFB's torpedo cruiser hull takes only heavy cruiser armor, and light cruiser armor forbids its medium battery.
        var design = Workspace.Clone(Workspace.FindDesign("torpedo_cruiser_torpattack")!);
        DesignSlots.SetModule(Workspace.Catalog, design, "fixed_ship_armor_slot", "ship_armor_cruiser_4");
        var issues = Workspace.Library.CheckDesign(design)!.Issues;

        var texts = issues.Select(issue => DesignSlots.Describe(Workspace.Catalog, issue)).ToList();

        Assert.Equal(2, texts.Count);
        Assert.Contains("This slot does not take Light Cruiser Armor Scheme modules.", texts);
        Assert.Contains(texts, text => text.StartsWith("Cruiser Armor IV", StringComparison.Ordinal) && text.EndsWith("forbids Medium Battery modules on the design.", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveDesign_WritesTheModulesInSlotOrder()
    {
        var design = Workspace.Clone(Workspace.FindDesign("dd_3_la")!);
        var file = Workspace.Catalog.SourceFileOf(Workspace.FindDesign("dd_3_la")!)!;
        design.Modules = design.Modules.Reverse().ToDictionary();

        Workspace.SaveDesign(design, file, "dd_3_la");

        var order = Workspace.FindHull(design.HullID)!.Slots.Select(slot => slot.ID).ToList();
        var saved = Workspace.FindDesign("dd_3_la")!;
        Assert.Equal(saved.Modules.Keys.OrderBy(order.IndexOf), saved.Modules.Keys);
    }
}
