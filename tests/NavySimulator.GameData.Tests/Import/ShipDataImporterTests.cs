using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>The import from a small game folder with a mod over it, following common/units/equipment/_documentation.md.</summary>
public sealed class ShipDataImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-import-" + Guid.NewGuid().ToString("N"));
    private readonly string game;
    private readonly string mod;

    public ShipDataImporterTests()
    {
        game = Path.Combine(root, "game");
        mod = Path.Combine(root, "mod");

        Write(game, "common/units/equipment/ship_hull_cruiser.txt", """
            equipments = {
                ship_hull_cruiser = {
                    is_archetype = yes
                    is_buildable = no
                    type = screen_ship
                    module_slots = {
                        fixed_ship_battery_slot = { required = yes allowed_module_categories = { ship_light_battery ship_medium_battery } }
                        fixed_ship_armor_slot = { required = no allowed_module_categories = { ship_cruiser_armor } }
                        mid_1_custom_slot = { required = no allowed_module_categories = { ship_light_battery ship_radar } }
                        mid_2_custom_slot = mid_1_custom_slot
                    }
                    module_count_limit = { category = ship_radar count < 2 }
                    default_modules = { fixed_ship_battery_slot = ship_light_battery_1 mid_1_custom_slot = empty }
                    max_strength = 100
                    naval_speed = 27
                    naval_range = 2500
                    manpower = 600
                }
                ship_hull_cruiser_1 = {
                    year = 1922
                    archetype = ship_hull_cruiser
                    module_slots = inherit
                }
                ship_hull_cruiser_2 = {
                    year = 1936
                    archetype = ship_hull_cruiser
                    parent = ship_hull_cruiser_1
                    max_strength = 110
                    module_slots = {
                        fixed_ship_battery_slot = inherit
                        front_1_custom_slot = { allowed_module_categories = { ship_light_battery } }
                        mid_1_custom_slot = inherit
                    }
                    module_count_limit = { category = ship_radar count = any }
                    default_modules = { front_1_custom_slot = ship_light_battery_1 }
                }
                light_cruiser_1 = {
                    archetype = ship_hull_cruiser
                    module_slots = inherit
                    type = { screen_ship anti_air }
                }
            }
            """);
        Write(game, "common/units/equipment/modules/00_ship_modules.txt", """
            equipment_modules = {
                limit = { has_dlc = "Man the Guns" }
                ship_light_battery_1 = {
                    category = ship_light_battery
                    add_stats = { lg_attack = 1 build_cost_ic = 90 }
                    critical_parts = { damaged_light_guns }
                }
                ship_medium_battery_1 = {
                    category = ship_medium_battery
                    add_equipment_type = capital_ship
                    add_stats = { hg_attack = 6 }
                }
                ship_armor_cruiser_1 = {
                    category = ship_cruiser_armor
                    multiply_stats = { naval_speed = -0.05 }
                    multiply_stats = { max_strength = 0.1 }
                    add_stats = { fuel_consumption = 1 }
                    forbid_module_categories = { ship_medium_battery }
                }
                tank_gun_1 = { category = tank_gun }
            }
            """);
        Write(game, "common/units/light_cruiser.txt", """
            sub_units = {
                light_cruiser = {
                    priority = 5
                    type = { screen_ship }
                    need = { ship_hull_cruiser = 1 }
                    need_equipment_modules = { any = { ship_armor_cruiser_1 = 1 } }
                    max_organisation = 40
                    critical_parts = { on_fire }
                    critical_part_damage_chance_mult = 0.5
                }
            }
            """);
        Write(game, "common/units/critical_parts/00_critical_parts.txt", """
            critical_parts = {
                damaged_light_guns = { stat_penalties = { lg_attack = -0.5 } damage_instance_per_added_module = 1 }
                on_fire = { org_damage_multiplier = 2 org_damage = 5 modifier = { navy_org_factor = -0.5 } }
                unused_part = { chance = 0.3 }
            }
            """);
        Write(game, "common/units/infantry.txt", "sub_units = { infantry = { type = { infantry } need = { infantry_equipment = 100 } } }");
        Write(game, "localisation/english/equip_l_english.yml", """
            l_english:
             ship_hull_cruiser:0 "Cruiser"
             ship_hull_cruiser_2:0 "Basic Cruiser"
             ship_light_battery_1:0 "85 mm Gun"
             ship_light_battery_1_short:0 "Light Battery I"
             EQ_MOD_CAT_ship_light_battery_TITLE:0 "§YRapid Fire Guns§!"
             EQ_MOD_SLOT_fixed_ship_battery_slot_TITLE:0 "Battery"
             light_cruiser:0 "Light $CRUISER$"
             CRUISER:0 "Cruiser"
            """);

        // The mod replaces the module file and renames a module in a replace folder.
        Write(mod, "descriptor.mod", "name=\"Test mod\"\nreplace_path=\"common/units/equipment/modules\"");
        Write(mod, "common/units/equipment/modules/mod_modules.txt", """
            equipment_modules = {
                ship_light_battery_1 = { category = ship_light_battery add_stats = { lg_attack = 2 } }
                ship_armor_cruiser_1 = { category = ship_cruiser_armor forbid_module_categories = { ship_medium_battery } }
            }
            """);
        Write(mod, "localisation/english/replace/mod_l_english.yml", "l_english:\n ship_light_battery_1_short:0 \"Light Battery I (mod)\"\n");
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void Hulls_InheritSlotsFromTheParentOrArchetype()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var first = import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_1");
        var second = import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_2");

        Assert.Equal(["fixed_ship_battery_slot", "fixed_ship_armor_slot", "mid_1_custom_slot", "mid_2_custom_slot"], first.Slots.Select(slot => slot.ID));
        Assert.Equal(first.Slots[2].AllowedCategories, first.Slots[3].AllowedCategories);
        Assert.True(first.Slots[0].Required);
        Assert.Equal(["fixed_ship_battery_slot", "front_1_custom_slot", "mid_1_custom_slot"], second.Slots.Select(slot => slot.ID));
        Assert.Equal(["ship_light_battery", "ship_radar"], second.Slots[2].AllowedCategories);
    }

    [Fact]
    public void Hulls_TakeTheArchetypesStatsOverriddenByTheirOwn()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var second = import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_2");

        Assert.Equal(110, second.BaseStats.HP);
        Assert.Equal(27, second.BaseStats.Speed);
        Assert.Equal(600, second.Manpower);
        Assert.Equal(1936, second.Year);
        Assert.Equal("screen_ship", second.EquipmentType);
        Assert.Contains("naval_range", import.IgnoredKeys.Keys);
    }

    [Fact]
    public void CountLimitsAndDefaultModules_AreInherited_AndCountAnyCancelsALimit()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var first = import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_1");
        var second = import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_2");

        var limit = Assert.Single(first.ModuleCountLimits);
        Assert.Equal(["ship_radar"], limit.Categories);
        Assert.Equal("<", limit.Operator);
        Assert.Equal(2, limit.Count);
        Assert.Empty(second.ModuleCountLimits);
        Assert.Equal(new Dictionary<string, string> { ["fixed_ship_battery_slot"] = "ship_light_battery_1" }, first.DefaultModules);
        Assert.Equal("ship_light_battery_1", second.DefaultModules["front_1_custom_slot"]);
    }

    [Fact]
    public void LegacyShipsOfTheArchetype_AreSkipped()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));

        Assert.Equal(["ship_hull_cruiser_1", "ship_hull_cruiser_2"], import.Hulls.Select(hull => hull.ID));
        Assert.Equal(["light_cruiser_1"], import.SkippedEquipment);
    }

    [Fact]
    public void Modules_ReadCategoriesRulesAndMergedStatBlocks()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var armor = import.Modules.Single(module => module.ID == "ship_armor_cruiser_1");
        var medium = import.Modules.Single(module => module.ID == "ship_medium_battery_1");

        Assert.DoesNotContain(import.Modules, module => module.ID == "tank_gun_1");
        Assert.Equal(["ship_medium_battery"], armor.ForbiddenCategories);
        Assert.Equal(-0.05, armor.StatMultipliers.Speed);
        Assert.Equal(0.1, armor.StatMultipliers.HP);
        Assert.Equal(["capital_ship"], medium.AddEquipmentTypes);
        Assert.Contains("fuel_consumption", import.IgnoredKeys.Keys);
        Assert.Empty(import.Warnings);
    }

    [Fact]
    public void ShipTypes_ComeFromNavalSubUnits()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));

        var shipType = Assert.Single(import.ShipTypes);
        Assert.Equal("light_cruiser", shipType.ID);
        Assert.Equal("Light Cruiser", shipType.Name);
        Assert.Equal("Screen", shipType.Role);
        Assert.Equal(["ship_hull_cruiser"], shipType.Hulls);
        Assert.Equal(["ship_armor_cruiser_1"], shipType.RequiredModules);
        Assert.Equal(40, shipType.Organization);
        Assert.Equal(5, shipType.Priority);
    }

    [Fact]
    public void CriticalParts_ComeFromTheirFolder_OnlyThoseInUse_WithTheGamesDefaults()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var guns = import.CriticalParts.Single(part => part.ID == "damaged_light_guns");
        var fire = import.CriticalParts.Single(part => part.ID == "on_fire");
        var shipType = Assert.Single(import.ShipTypes);

        Assert.Equal(["damaged_light_guns", "on_fire"], import.CriticalParts.Select(part => part.ID).Order(StringComparer.Ordinal));
        Assert.Equal(-0.5, guns.StatPenalties.LightAttack);
        Assert.Equal(1, guns.DamageInstancePerAddedModule);
        Assert.Equal((1, 1, -1), (guns.Chance, guns.BaseDamageInstance, guns.MaxDamageInstance));
        Assert.Equal((2, 5), (fire.OrgDamageMultiplier, fire.OrgDamage));
        Assert.Equal(-0.5, fire.Modifiers["navy_org_factor"]);
        Assert.Equal(["damaged_light_guns"], import.Modules.Single(module => module.ID == "ship_light_battery_1").CriticalParts);
        Assert.Equal(["on_fire"], shipType.CriticalParts);
        Assert.Equal(0.5, shipType.CriticalPartDamageChanceMult);
    }

    [Fact]
    public void Slots_GetTheGamesOrder_TheFirstAppearanceOfTheirNameInTheEquipmentFiles()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var expected = new Dictionary<string, int?>
        {
            ["fixed_ship_battery_slot"] = 0,
            ["fixed_ship_armor_slot"] = 1,
            ["mid_1_custom_slot"] = 2,
            ["mid_2_custom_slot"] = 3,
            ["front_1_custom_slot"] = 4
        };

        Assert.All(import.Hulls.SelectMany(hull => hull.Slots), slot => Assert.Equal(expected[slot.ID], slot.GameOrder));
    }

    [Fact]
    public void Names_ComeFromTheLocalisation()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game));
        var battery = import.Modules.Single(module => module.ID == "ship_light_battery_1");
        var second = import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_2");

        Assert.Equal("85 mm Gun", battery.Name);
        Assert.Equal("Light Battery I", battery.ShortName);
        Assert.Equal("Rapid Fire Guns", battery.CategoryName);
        Assert.Equal("Basic Cruiser", second.Name);
        Assert.Equal("Cruiser", second.ArchetypeName);
        Assert.Equal("Battery", second.Slots[0].Name);
        Assert.Null(import.Hulls.Single(hull => hull.ID == "ship_hull_cruiser_1").Name);
    }

    [Fact]
    public void AMod_ReplacesFilesFoldersAndNames()
    {
        var import = ShipDataImporter.Read(new GameFileSystem(game, [mod]));
        var battery = import.Modules.Single(module => module.ID == "ship_light_battery_1");

        // replace_path hides the game's module file, so the medium battery is gone and the mod's values are used.
        Assert.Equal(["ship_light_battery_1", "ship_armor_cruiser_1"], import.Modules.Select(module => module.ID));
        Assert.Equal(2, battery.StatModifiers.LightAttack);
        Assert.Equal(0, battery.StatModifiers.ProductionCost);
        Assert.Equal("Light Battery I (mod)", battery.ShortName);
        Assert.Equal("85 mm Gun", battery.Name);
    }

    [Fact]
    public void WrittenFiles_LoadBackIntoTheCatalog()
    {
        var data = Path.Combine(root, "data");
        var import = ShipDataImporter.Read(new GameFileSystem(game));

        var files = ShipDataWriter.Write(data, import);
        var catalog = new SetupLoader().ReadCatalog(data);

        Assert.Equal(["critical-parts/critical-parts.json", "hulls/cruiser.json", "modules/armor.json", "modules/light-guns.json", "modules/medium-guns.json", "ship-types/ship-types.json"], files);
        Assert.Equal(["ship_hull_cruiser_1", "ship_hull_cruiser_2"], catalog.Hulls.Select(hull => hull.ID));
        Assert.Equal(4, catalog.Hulls[0].Slots.Count);
        Assert.Equal(-0.05, catalog.Modules.Single(module => module.ID == "ship_armor_cruiser_1").StatMultipliers.Speed);
        Assert.Equal("light_cruiser", Assert.Single(catalog.ShipTypes).ID);
        Assert.Equal(2, catalog.CriticalParts.Count);
        Assert.Equal(4, catalog.Hulls[1].Slots.Single(slot => slot.ID == "front_1_custom_slot").GameOrder);
        Assert.DoesNotContain("\"required\": false", File.ReadAllText(Path.Combine(data, "hulls", "cruiser.json")));
    }

    private static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
