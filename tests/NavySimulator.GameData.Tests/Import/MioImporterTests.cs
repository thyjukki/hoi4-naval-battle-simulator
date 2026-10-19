using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>
/// The MIO import from a small game folder with a mod over it, following the game's
/// common/military_industrial_organization/organizations/_documentation.md and policies/_documentation.md (S-MIO).
/// </summary>
public sealed class MioImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-mios-" + Guid.NewGuid().ToString("N"));
    private readonly string game;
    private readonly string mod;

    public MioImporterTests()
    {
        game = Path.Combine(root, "game");
        mod = Path.Combine(root, "mod");

        Write(game, "common/units/equipment/ship_hull_heavy.txt", """
            equipments = {
                ship_hull_heavy = { is_archetype = yes type = capital_ship }
                ship_hull_heavy_1 = { archetype = ship_hull_heavy }
            }
            """);
        Write(game, "common/units/equipment/infantry.txt", """
            equipments = { infantry_equipment = { is_archetype = yes type = infantry } }
            """);
        Write(game, "common/equipment_groups/mio_equipment_groups.txt", """
            mio_cat_eq_all_battleship = { equipment_type = { ship_hull_heavy ship_hull_super_heavy } }
            mio_cat_eq_all_infantry = { equipment_type = { infantry_equipment } }
            """);
        Write(game, "common/military_industrial_organization/organizations/00_generic_organization.txt", """
            generic_battle_line_ship_organization = {
                allowed = { always = no }
                equipment_type = { mio_cat_eq_all_battleship }
                initial_trait = {
                    name = generic_mio_initial_trait_battle_line_ship_builders
                    production_bonus = { production_capacity_factor = 0.05 }
                }
                trait = {
                    token = generic_mio_trait_external_armor_belt
                    name = generic_mio_trait_external_armor_belt
                    position = { x=0 y=0 }
                    mutually_exclusive = { generic_mio_trait_internal_armor_belt }
                    equipment_bonus = { armor_value = 0.05 }
                }
                trait = {
                    token = generic_mio_trait_internal_armor_belt
                    position = { x=2 y=0 }
                    relative_position_id = generic_mio_trait_external_armor_belt
                    mutually_exclusive = { generic_mio_trait_external_armor_belt }
                    equipment_bonus = { armor_value = 0.15 build_cost_ic = 0.05 }
                }
                trait = {
                    token = generic_mio_trait_hardened_critical_components
                    position = { x=1 y=2 }
                    relative_position_id = generic_mio_trait_external_armor_belt
                    any_parent = { generic_mio_trait_internal_armor_belt generic_mio_trait_external_armor_belt }
                    limit_to_equipment_type = { mio_cat_eq_all_battleship }
                    equipment_bonus = { naval_torpedo_enemy_critical_chance_factor = -0.05 }
                }
                trait = {
                    token = generic_mio_trait_combat_information_center
                    special_trait_background = yes
                    position = { x=0 y=2 }
                    relative_position_id = generic_mio_trait_hardened_critical_components
                    all_parents = { generic_mio_trait_hardened_critical_components }
                    parent = { traits = { generic_mio_trait_external_armor_belt generic_mio_trait_internal_armor_belt } num_parents_needed = 1 }
                    equipment_bonus = { max_strength = 0.1 }
                }
            }
            generic_infantry_organization = {
                allowed = { always = yes }
                equipment_type = { mio_cat_eq_all_infantry }
            }
            """);
        Write(game, "common/military_industrial_organization/organizations/USA_organization.txt", """
            USA_brooklyn_naval_yard_organization = {
                include = generic_battle_line_ship_organization
                allowed = { OR = { tag = USA original_tag = PHI } NOT = { tag = GER } }
                initial_trait = { equipment_bonus = { reliability = 0.05 } }
                remove_trait = { generic_mio_trait_combat_information_center }
                override_trait = {
                    token = generic_mio_trait_internal_armor_belt
                    equipment_bonus = { armor_value = 0.2 }
                    delete_included_values = { mutually_exclusive }
                }
                trait = {
                    token = generic_mio_trait_hardened_critical_components
                    limit_to_equipment_type = { capital_ship }
                }
                add_trait = {
                    token = USA_mio_trait_drydock_expansion
                    position = { x=0 y=1 }
                    relative_position_id = generic_mio_trait_hardened_critical_components
                    all_parents = { }
                    available = { has_tech = centimetric_radar }
                    equipment_bonus = { naval_speed = 0.05 fuel_consumption = -0.1 }
                }
            }
            USA_springfield_armory_organization = { include = generic_infantry_organization }
            PHI_cavite_naval_base_organization = {
                include = generic_battle_line_ship_organization
                allowed = { tag = PHI }
                equipment_type = { }
                remove_trait = { generic_mio_trait_external_armor_belt }
                trait = { token = generic_mio_trait_external_armor_belt equipment_bonus = { reliability = 0.1 } }
                override_trait = {
                    token = generic_mio_trait_combat_information_center
                    any_parent = { generic_mio_trait_internal_armor_belt }
                    special_trait_background = no
                }
                add_trait = { token = PHI_mio_trait_unplaced any_parent = { } }
            }
            """);
        Write(game, "common/military_industrial_organization/policies/_navy_policies.txt", """
            mio_policy_navy_stable_platforms = {
                allowed = { OR = { has_mio_equipment_type = capital_ship has_mio_equipment_type = mio_cat_eq_all_battleship } }
                available = { has_mio_size > 5 }
                equipment_bonus = {
                    same_as_mio = { naval_light_gun_hit_chance_factor = 0.1 }
                    ship_hull_heavy = { armor_value = 0.05 }
                }
            }
            mio_policy_army_light_infantry = {
                allowed = { has_mio_equipment_type = infantry_equipment }
                equipment_bonus = { same_as_mio = { soft_attack = 0.1 } }
            }
            """);
        Write(game, "common/country_tags/00_countries.txt", """
            USA = "countries/USA.txt"
            PHI = "countries/Philippines.txt"
            """);
        Write(game, "common/country_tags/zz_dynamic_countries.txt", """
            dynamic_tags = yes
            D01 = "countries/D01.txt"
            """);
        Write(game, "localisation/english/countries_l_english.yml", """
            l_english:
             USA: "America"
             PHI_DEF: "the Philippines"
            """);
        Write(game, "localisation/english/mio_l_english.yml", """
            l_english:
             generic_battle_line_ship_organization: "Battle Line Ship Manufacturer"
             USA_brooklyn_naval_yard_organization: "Brooklyn Naval Yard"
             generic_mio_initial_trait_battle_line_ship_builders: "Battle Line Shipbuilders"
             generic_mio_trait_external_armor_belt: "Vertical Armor Belt"
             USA_generic_mio_trait_external_armor_belt: "American Armor Belt"
             mio_policy_navy_stable_platforms: "Stable Gunnery Platforms"
             mio_cat_eq_all_battleship: "All Battleship"
            """);

        // Country conditions as the game's and TFB's files write them: a scripted trigger and an upper-case key.
        Write(mod, "common/scripted_triggers/NOR_scripted_triggers.txt", """
            NOR_AAT = { original_tag = NOR has_dlc = "Arms Against Tyranny" }
            """);
        Write(mod, "common/military_industrial_organization/organizations/NOR_organization.txt", """
            NOR_akers_organization = { include = generic_battle_line_ship_organization allowed = { NOR_AAT = yes } }
            AST_cockatoo_organization = { include = generic_battle_line_ship_organization allowed = { original_TAG = AST } }
            """);

        // The mod gives the battle line archetype a new initial trait bonus.
        Write(mod, "common/military_industrial_organization/organizations/00_generic_organization.txt", """
            generic_battle_line_ship_organization = {
                allowed = { always = no }
                equipment_type = { mio_cat_eq_all_battleship }
                initial_trait = { name = generic_mio_initial_trait_battle_line_ship_builders equipment_bonus = { max_strength = 0.05 } }
                trait = { token = generic_mio_trait_external_armor_belt equipment_bonus = { armor_value = 0.1 } }
            }
            """);
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void OnlyMiosWithShipEquipmentAreImported_ArchetypesIncluded()
    {
        var import = MioImporter.Read(new GameFileSystem(game));

        Assert.Equal(["PHI_cavite_naval_base_organization", "USA_brooklyn_naval_yard_organization", "generic_battle_line_ship_organization"],
            import.Mios.Select(item => item.Mio.ID).Order(StringComparer.Ordinal));
        var archetype = import.Mios[0].Mio;
        Assert.True(archetype.ArchetypeOnly);
        Assert.Equal("Battle Line Ship Manufacturer", archetype.Name);
        Assert.Equal("common/military_industrial_organization/organizations/00_generic_organization.txt", import.Mios[0].File);
        Assert.Equal("Battle Line Shipbuilders", archetype.InitialTrait!.Name);
        Assert.Equal(0.05, archetype.InitialTrait.ProductionBonus["production_capacity_factor"]);
    }

    [Fact]
    public void Traits_ReadTheirTreeBonusesAndPositions()
    {
        var import = MioImporter.Read(new GameFileSystem(game));
        var traits = import.Mios[0].Mio.Traits;

        Assert.Equal("Vertical Armor Belt", traits[0].Name);
        Assert.Equal(["generic_mio_trait_internal_armor_belt"], traits[0].MutuallyExclusive);
        Assert.Equal((2, 0), (traits[1].X, traits[1].Y));
        Assert.Equal(0.05, traits[1].EquipmentBonus["build_cost_ic"]);

        var hardened = traits[2];
        Assert.Equal((1, 2), (hardened.X, hardened.Y));
        Assert.Equal(["mio_cat_eq_all_battleship"], hardened.LimitToEquipmentTypes);
        var anyParent = Assert.Single(hardened.Parents);
        Assert.Equal(["generic_mio_trait_internal_armor_belt", "generic_mio_trait_external_armor_belt"], anyParent.Traits);
        Assert.Equal(1, anyParent.Needed);

        // Relative to a relative position: (0, 0) + (1, 2) + (0, 2).
        var center = traits[3];
        Assert.Equal((1, 4), (center.X, center.Y));
        Assert.Equal([(2, 1), (1, 1)], center.Parents.Select(parent => (parent.Traits.Count, parent.Needed)));
        Assert.Equal(0.1, center.EquipmentBonus["max_strength"]);
    }

    [Fact]
    public void Include_CopiesTheTree_ThenRemovesOverridesMergesAndAddsTraits()
    {
        var import = MioImporter.Read(new GameFileSystem(game));
        var brooklyn = import.Mios.Single(item => item.Mio.ID == "USA_brooklyn_naval_yard_organization").Mio;

        Assert.Equal("Brooklyn Naval Yard", brooklyn.Name);
        Assert.Equal("generic_battle_line_ship_organization", brooklyn.Include);
        Assert.Equal(["USA", "PHI"], brooklyn.Countries);
        Assert.Equal(["GER"], brooklyn.ExcludedCountries);
        Assert.False(brooklyn.ArchetypeOnly);
        Assert.Equal(["mio_cat_eq_all_battleship"], brooklyn.EquipmentTypes);

        // The initial trait merges: the name stays, the bonuses are the including MIO's.
        Assert.Equal("Battle Line Shipbuilders", brooklyn.InitialTrait!.Name);
        Assert.Equal(0.05, brooklyn.InitialTrait.EquipmentBonus["reliability"]);
        Assert.Equal(0.05, brooklyn.InitialTrait.ProductionBonus["production_capacity_factor"]);

        // [RE] Own blocks first (merged in place), then the included traits in their order: copied, or overridden.
        Assert.Equal(
        [
            "generic_mio_trait_hardened_critical_components", "USA_mio_trait_drydock_expansion",
            "generic_mio_trait_external_armor_belt", "generic_mio_trait_internal_armor_belt"
        ], brooklyn.Traits.Select(trait => trait.ID));
        Assert.Equal("American Armor Belt", brooklyn.Traits[2].Name);

        var internalBelt = brooklyn.Traits[3];
        Assert.Equal(0.2, internalBelt.EquipmentBonus["armor_value"]);
        Assert.False(internalBelt.EquipmentBonus.ContainsKey("build_cost_ic"));
        Assert.Empty(internalBelt.MutuallyExclusive);
        Assert.Equal((2, 0), (internalBelt.X, internalBelt.Y));

        // [RE] A plain trait block with a known token is merged: new limit, same bonus, parents and position.
        var hardened = brooklyn.Traits[0];
        Assert.Equal(["capital_ship"], hardened.LimitToEquipmentTypes);
        Assert.Equal(-0.05, hardened.EquipmentBonus["naval_torpedo_enemy_critical_chance_factor"]);
        Assert.Single(hardened.Parents);
        Assert.Equal((1, 2), (hardened.X, hardened.Y));

        var drydock = brooklyn.Traits[1];
        Assert.Equal((1, 3), (drydock.X, drydock.Y));
        Assert.Empty(drydock.Parents);
        Assert.Equal(-0.1, drydock.EquipmentBonus["fuel_consumption"]);
        Assert.Equal(["available = { has_tech = centimetric_radar }"], drydock.Conditions);
    }

    [Fact]
    public void Include_FollowsTheGamesMergeRules()
    {
        var import = MioImporter.Read(new GameFileSystem(game));
        var cavite = import.Mios.Single(item => item.Mio.ID == "PHI_cavite_naval_base_organization").Mio;

        // An empty own block counts as unset, so the included equipment types fill it.
        Assert.Equal(["mio_cat_eq_all_battleship"], cavite.EquipmentTypes);
        Assert.Equal(
        [
            "generic_mio_trait_external_armor_belt", "PHI_mio_trait_unplaced", "generic_mio_trait_internal_armor_belt",
            "generic_mio_trait_hardened_critical_components", "generic_mio_trait_combat_information_center"
        ], cavite.Traits.Select(trait => trait.ID));

        // remove_trait wins: the own block with that token stays as written, without position (unset is −1, −1).
        var external = cavite.Traits[0];
        Assert.Equal(["reliability"], external.EquipmentBonus.Keys);
        Assert.Empty(external.MutuallyExclusive);
        Assert.Equal((-1, -1), (external.X, external.Y));

        // An own any_parent replaces every included parent group; an own special_trait_background = no is unset.
        var center = cavite.Traits[4];
        var parent = Assert.Single(center.Parents);
        Assert.Equal(["generic_mio_trait_internal_armor_belt"], parent.Traits);
        Assert.Equal(1, parent.Needed);
        Assert.True(center.Special);
        Assert.Equal(0.1, center.EquipmentBonus["max_strength"]);

        // Relative to the first trait with the token in this MIO's tree: the unplaced own block (−1, −1) + (1, 2) + (0, 2).
        Assert.Equal((0, 3), (center.X, center.Y));

        // An empty any_parent needs one of nothing and never holds.
        var unplaced = Assert.Single(cavite.Traits[1].Parents);
        Assert.Equal((0, 1), (unplaced.Traits.Count, unplaced.Needed));
    }

    [Fact]
    public void ModFiles_ReplaceTheGames_AndIncludesSeeTheModdedArchetype()
    {
        var import = MioImporter.Read(new GameFileSystem(game, [mod]));
        var brooklyn = import.Mios.Single(item => item.Mio.ID == "USA_brooklyn_naval_yard_organization").Mio;

        // The including MIO's equipment_bonus replaces the modded archetype's block as a whole.
        Assert.Equal(["reliability"], brooklyn.InitialTrait!.EquipmentBonus.Keys);
        Assert.Equal(0.1, brooklyn.Traits.Single(trait => trait.ID == "generic_mio_trait_external_armor_belt").EquipmentBonus["armor_value"]);
        Assert.Contains(import.Warnings, warning => warning.Contains("overrides trait 'generic_mio_trait_internal_armor_belt'", StringComparison.Ordinal));
    }

    [Fact]
    public void Policies_AndEquipmentGroups_AreTheOnesForShips()
    {
        var import = MioImporter.Read(new GameFileSystem(game));

        var policy = Assert.Single(import.Policies);
        Assert.Equal("Stable Gunnery Platforms", policy.Name);
        Assert.Equal(["capital_ship", "mio_cat_eq_all_battleship"], policy.AllowedEquipmentTypes);
        Assert.Equal(0.1, policy.EquipmentBonus["same_as_mio"]["naval_light_gun_hit_chance_factor"]);
        Assert.Equal(0.05, policy.EquipmentBonus["ship_hull_heavy"]["armor_value"]);
        Assert.Contains("available = { has_mio_size > 5 }", policy.Conditions);

        var group = Assert.Single(import.EquipmentGroups);
        Assert.Equal(("mio_cat_eq_all_battleship", "All Battleship"), (group.ID, group.Name));
        Assert.Equal(["ship_hull_heavy", "ship_hull_super_heavy"], group.EquipmentTypes);
    }

    [Fact]
    public void CountryConditions_LookIntoScriptedTriggers_AndIgnoreKeyCase()
    {
        var import = MioImporter.Read(new GameFileSystem(game, [mod]));

        Assert.Equal(["NOR"], import.Mios.Single(item => item.Mio.ID == "NOR_akers_organization").Mio.Countries);
        Assert.Equal(["AST"], import.Mios.Single(item => item.Mio.ID == "AST_cockatoo_organization").Mio.Countries);
    }

    [Fact]
    public void Countries_AreTheTagsWithTheirNames_WithoutDynamicTags()
    {
        var import = MioImporter.Read(new GameFileSystem(game));

        Assert.Equal([("USA", "America"), ("PHI", "the Philippines")], import.Countries.Select(country => (country.ID, country.Name)));
    }

    [Fact]
    public void Writer_WritesFilesTheLoaderReads()
    {
        var import = MioImporter.Read(new GameFileSystem(game));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(data, MioWriter.MiosFolder));
        File.WriteAllText(Path.Combine(data, MioWriter.MiosFolder, "old.json"), """{ "mios": [] }""");

        var files = MioWriter.Write(data, import);
        var read = new SetupLoader().ReadCatalog(DataDirectoryWith(data));

        Assert.Equal(["countries/countries.json", "equipment-groups/naval-groups.json", "mio-policies/naval-policies.json", "mios/00_generic_organization.json", "mios/USA_organization.json"], files);
        Assert.False(File.Exists(Path.Combine(data, MioWriter.MiosFolder, "old.json")));
        Assert.Equal(3, read.Mios.Count);
        var brooklyn = read.Mios.Single(mio => mio.ID == "USA_brooklyn_naval_yard_organization");
        Assert.Equal(-0.1, brooklyn.Traits[1].EquipmentBonus["fuel_consumption"]);
        Assert.Equal(1, brooklyn.Traits[0].Parents[0].Needed);
        Assert.Empty(read.Mios.Single(mio => mio.ID == "PHI_cavite_naval_base_organization").Traits[1].Parents[0].Traits);
        Assert.Equal("mio_policy_navy_stable_platforms", Assert.Single(read.MioPolicies).ID);
        Assert.Equal("mio_cat_eq_all_battleship", Assert.Single(read.EquipmentGroups).ID);
        Assert.Equal(["USA", "PHI"], read.Countries.Select(country => country.ID));
    }

    // The loader needs the other collections; take them from the repository data.
    private string DataDirectoryWith(string imported)
    {
        var data = Path.Combine(root, "full");
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);

        foreach (var folder in new[] { MioWriter.MiosFolder, MioWriter.PoliciesFolder, MioWriter.EquipmentGroupsFolder, MioWriter.CountriesFolder })
        {
            Directory.Delete(Path.Combine(data, folder), true);
            DataDirectoryCopy.CopyDirectory(Path.Combine(imported, folder), Path.Combine(data, folder));
        }

        return data;
    }

    private static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
