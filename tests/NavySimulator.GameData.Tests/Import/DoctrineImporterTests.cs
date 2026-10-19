using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>The doctrine and spirit import from a small game folder with a mod over it, following common/doctrines/**/_documentation.md.</summary>
public sealed class DoctrineImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-doctrines-" + Guid.NewGuid().ToString("N"));
    private readonly string game;
    private readonly string mod;

    public DoctrineImporterTests()
    {
        game = Path.Combine(root, "game");
        mod = Path.Combine(root, "mod");

        Write(game, "common/doctrines/folders/doctrine_folders.txt", """
            land = { name = "land_doctrine_folder" }
            naval = { name = "naval_doctrine_folder" }
            """);
        Write(game, "common/doctrines/tracks/sea_doctrine_tracks.txt", """
            capital_ships = { name = DOCTRINE_TRACK_CAPITAL_SHIPS mastery = { multiplier = 2.0 equipment = { capital_ship } } }
            screens = { name = DOCTRINE_TRACK_SCREENS }
            """);
        Write(game, "common/doctrines/grand_doctrines/sea_grand_doctrines.txt", """
            new_fleet_in_being = {
                folder = naval
                name = GRAND_DOCTRINE_FLEET_IN_BEING
                xp_cost = 100
                tracks = { screens capital_ships }
                battleship = { max_organisation = 15 }
                navy_capital_ship_defence_factor = 0.1
                milestones = {
                    { navy_anti_air_attack_factor = 0.1 effect = { set_variable = { screens_milestone_var = 1 } } }
                    { navy_capital_ship_attack_factor = 0.1 }
                }
            }
            mobile_warfare = { folder = land tracks = { infantry } }
            """);
        Write(game, "common/doctrines/subdoctrines/sea/navy_capital_subdoctrines.txt", """
            line_of_battle = {
                track = capital_ships
                name = SUBDOCTRINE_LINE_OF_BATTLE
                available = { always = yes }
                naval_has_potf_in_combat_attack = 0.10
                rewards = {
                    flagships = { battleship = { max_organisation = 5 } }
                    ordnance_handling_training = {
                        mastery = 150
                        battleship = { hg_attack = 0.05 supply_consumption = -0.1 }
                    }
                    crisis_management = { modifier = { navy_casualty_on_hit = -0.4 } }
                    capital_screens = { equipment_bonus = { ship_hull_heavy = { armor_value = 0.15 instant = yes } } }
                }
            }
            """);
        Write(game, "common/doctrines/subdoctrines/sea/navy_screen_doctrines.txt", """
            torpedo_primacy = {
                track = { screens }
                xor = { patrol_boats }
                enable_tactic = tactic_torpedo_run
                rewards = { modern_broadsides = { destroyer = { torpedo_attack = 0.1 } } }
            }
            """);
        Write(game, "common/idea_tags/00_idea.txt", """
            idea_categories = {
                country = { type = national_spirit }
                naval_academy_spirit = { type = navy_spirit slot = naval_academy_spirit }
                naval_command_spirit = { type = navy_spirit slot = naval_command_spirit }
            }
            """);
        Write(game, "common/ideas/navy_spirits.txt", """
            ideas = {
                naval_academy_spirit = {
                    fleet_in_being_academy_spirit = {
                        available = { AND = { has_naval_academy = yes has_doctrine = new_fleet_in_being } }
                        modifier = { custom_modifier_tooltip = x_tt trait_ironside_xp_gain_factor = 0.2 }
                    }
                }
                naval_command_spirit = {
                    night_fighting_spirit = {
                        available = { OR = { has_doctrine = torpedo_primacy has_doctrine = new_base_strike } }
                        modifier = { naval_night_attack = 0.1 }
                    }
                    bureau_of_ordnance_spirit = {
                        visible = { tag = USA }
                        modifier = { naval_torpedo_hit_chance_factor = -0.1 }
                    }
                }
                country = { not_a_spirit = { modifier = { stability_factor = 0.1 } } }
            }
            """);
        Write(game, "localisation/english/doctrines_l_english.yml", """
            l_english:
             naval_doctrine_folder: "Naval Doctrine"
             DOCTRINE_TRACK_CAPITAL_SHIPS: "Capital Ships"
             GRAND_DOCTRINE_FLEET_IN_BEING: "Fleet in Being"
             SUBDOCTRINE_LINE_OF_BATTLE: "Line of Battle"
             line_of_battle_flagships: "Flagship Coordination"
             night_fighting_spirit: "Night Fighting"
             naval_command_spirit: "Spirit of Naval Command"
            """);

        // The mod halves the subdoctrine's base effect and leaves its last closing brace out, as TFB's submarine file does.
        Write(mod, "common/doctrines/subdoctrines/sea/navy_capital_subdoctrines.txt", """
            line_of_battle = {
                track = capital_ships
                naval_has_potf_in_combat_attack = 0.05
                rewards = {
                    flagships = { battleship = { max_organisation = 2.5 } }
                }
            """);
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void GrandDoctrines_ReadTheirFolderTracksEffectsAndMilestonesByTrack()
    {
        var import = DoctrineImporter.Read(new GameFileSystem(game));

        var folder = Assert.Single(import.Folders);
        Assert.Equal("naval", folder.ID);
        Assert.Equal("Naval Doctrine", folder.Name);
        Assert.Equal(["screens", "capital_ships"], folder.Tracks.Select(track => track.ID));
        Assert.Equal("Capital Ships", folder.Tracks[1].Name);

        var grand = Assert.Single(folder.GrandDoctrines);
        Assert.Equal("Fleet in Being", grand.Name);
        Assert.Equal(0.1, grand.Effects.CountryModifiers["navy_capital_ship_defence_factor"]);
        Assert.Equal(15, grand.Effects.UnitBonuses["battleship"]["max_organisation"]);
        Assert.Equal(0.1, grand.Milestones["screens"].CountryModifiers["navy_anti_air_attack_factor"]);
        Assert.Equal(["effect"], grand.Milestones["screens"].Scripted);
        Assert.Equal(0.1, grand.Milestones["capital_ships"].CountryModifiers["navy_capital_ship_attack_factor"]);
    }

    [Fact]
    public void Subdoctrines_ReadRewardsInOrder_WithMasteryUnitEquipmentAndModifierEffects()
    {
        var import = DoctrineImporter.Read(new GameFileSystem(game));
        var subdoctrine = import.Folders[0].Subdoctrines.Single(item => item.ID == "line_of_battle");

        Assert.Equal("Line of Battle", subdoctrine.Name);
        Assert.Equal(["capital_ships"], subdoctrine.Tracks);
        Assert.Equal(0.1, subdoctrine.Effects.CountryModifiers["naval_has_potf_in_combat_attack"]);
        Assert.Equal(["flagships", "ordnance_handling_training", "crisis_management", "capital_screens"], subdoctrine.Rewards.Select(reward => reward.ID));
        Assert.Equal("Flagship Coordination", subdoctrine.Rewards[0].Name);
        Assert.Null(subdoctrine.Rewards[0].Mastery);
        Assert.Equal(150, subdoctrine.Rewards[1].Mastery);
        Assert.Equal(-0.1, subdoctrine.Rewards[1].Effects.UnitBonuses["battleship"]["supply_consumption"]);
        Assert.Equal(-0.4, subdoctrine.Rewards[2].Effects.CountryModifiers["navy_casualty_on_hit"]);
        Assert.Equal(0.15, subdoctrine.Rewards[3].Effects.EquipmentBonuses["ship_hull_heavy"]["armor_value"]);
        Assert.False(subdoctrine.Rewards[3].Effects.EquipmentBonuses["ship_hull_heavy"].ContainsKey("instant"));

        var torpedoes = import.Folders[0].Subdoctrines.Single(item => item.ID == "torpedo_primacy");
        Assert.Equal(["patrol_boats"], torpedoes.Xor);
        Assert.Equal(["enable_tactic = tactic_torpedo_run"], torpedoes.Effects.Scripted);
    }

    [Fact]
    public void ModFiles_ReplaceTheGames_AndAMissingLastBraceIsClosedAtTheEnd()
    {
        var import = DoctrineImporter.Read(new GameFileSystem(game, [mod]));
        var subdoctrine = import.Folders[0].Subdoctrines.Single(item => item.ID == "line_of_battle");

        Assert.Equal(0.05, subdoctrine.Effects.CountryModifiers["naval_has_potf_in_combat_attack"]);
        Assert.Equal(2.5, Assert.Single(subdoctrine.Rewards).Effects.UnitBonuses["battleship"]["max_organisation"]);
        Assert.Contains(import.Warnings, warning => warning.StartsWith("common/doctrines/subdoctrines/sea/navy_capital_subdoctrines.txt line", StringComparison.Ordinal) &&
                                                    warning.Contains("missing closing brace", StringComparison.Ordinal));
    }

    [Fact]
    public void Spirits_ComeFromNavySpiritCategories_WithTheirHasDoctrineConditions()
    {
        var import = DoctrineImporter.Read(new GameFileSystem(game));

        Assert.Equal(["fleet_in_being_academy_spirit", "night_fighting_spirit", "bureau_of_ordnance_spirit"], import.Spirits.Select(spirit => spirit.ID));
        var academy = import.Spirits[0];
        Assert.Equal("naval_academy_spirit", academy.Category);
        Assert.Equal([["new_fleet_in_being"]], academy.RequiredDoctrines);
        Assert.Equal(["has_naval_academy = yes"], academy.OtherConditions);
        Assert.Equal(0.2, academy.Effects.CountryModifiers["trait_ironside_xp_gain_factor"]);
        Assert.False(academy.Effects.CountryModifiers.ContainsKey("custom_modifier_tooltip"));

        var nightFighting = import.Spirits[1];
        Assert.Equal("Night Fighting", nightFighting.Name);
        Assert.Equal("Spirit of Naval Command", nightFighting.CategoryName);
        Assert.Equal([["torpedo_primacy", "new_base_strike"]], nightFighting.RequiredDoctrines);
        Assert.Empty(import.Spirits[2].RequiredDoctrines);
        Assert.Equal(["tag = USA"], import.Spirits[2].OtherConditions);
    }

    [Fact]
    public void Writer_WritesFilesTheLoaderReads()
    {
        var import = DoctrineImporter.Read(new GameFileSystem(game));
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(data, "spirits"));
        File.WriteAllText(Path.Combine(data, "spirits", "old.json"), """{ "spirits": [] }""");

        var files = DoctrineWriter.Write(data, import);
        var read = new SetupLoader().ReadCatalog(DataDirectoryWith(data));

        Assert.Equal(["doctrines/naval.json", "spirits/navy-spirits.json"], files);
        Assert.False(File.Exists(Path.Combine(data, "spirits", "old.json")));
        Assert.Equal("new_fleet_in_being", Assert.Single(Assert.Single(read.DoctrineFolders).GrandDoctrines).ID);
        Assert.Equal(3, read.Spirits.Count);
        Assert.Equal(150, read.DoctrineFolders[0].Subdoctrines[0].Rewards[1].Mastery);
    }

    // The loader needs the other collections; take them from the repository data.
    private string DataDirectoryWith(string imported)
    {
        var data = Path.Combine(root, "full");
        DataDirectoryCopy.CopyDirectory(RepositoryPaths.Data, data);

        foreach (var folder in new[] { DoctrineWriter.DoctrinesFolder, DoctrineWriter.SpiritsFolder })
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
