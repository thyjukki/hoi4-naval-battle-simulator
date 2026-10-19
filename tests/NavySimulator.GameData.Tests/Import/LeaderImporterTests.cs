using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>The admiral import (skills, traits, characters) from a small game folder with a mod over it.</summary>
public sealed class LeaderImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-leaders-" + Guid.NewGuid().ToString("N"));
    private readonly string game;
    private readonly string mod;

    public LeaderImporterTests()
    {
        game = Path.Combine(root, "game");
        mod = Path.Combine(root, "mod");

        Write(game, "common/unit_leader/00_attack_skills.txt", """
            leader_attack_skills = {
                1 = { cost = 100 type = navy modifier = { naval_damage_factor = 0.05 } building_module_modifier = { spotting_chance = 0.02 } }
                2 = { cost = 200 type = navy modifier = { naval_damage_factor = 0.10 } }
                1 = { cost = 100 type = corps_commander modifier = { army_attack_factor = 0.05 } }
            }
            """);
        Write(game, "common/unit_leader/00_maneuvering_skills.txt", """
            leader_maneuvering_skills = {
                1 = { type = navy modifier = { positioning = 0.025 naval_retreat_speed = 0.01 } }
            }
            """);
        Write(game, "common/unit_leader/00_traits.txt", """
            leader_traits = {
                blue_water_expert = {
                    type = navy
                    trait_type = basic_terrain_trait
                    modifier = { water_deep_ocean = { attack = 0.1 movement = 0.1 defence = 0.1 } }
                }
                destroyer_leader = {
                    type = navy
                    trait_type = assignable_trait
                    sub_unit_modifiers = {
                        destroyer = { naval_speed_factor = 0.05 units = { attack = 0.1 } }
                    }
                    any_parent = { fleet_protector superior_tactician }
                    mutually_exclusive = lancer
                    on_add = { add_skill = 1 }
                }
                demoted = {
                    type = all
                    trait_type = status_trait
                    attack_skill = -1
                    non_shared_modifier = { skill_bonus_factor = -0.5 }
                }
                infantry_officer = { type = corps_commander modifier = { army_attack_factor = 0.1 } }
            }
            """);
        Write(game, "common/country_tags/00_countries.txt", """
            USA = "countries/USA.txt"
            BUL = "countries/Bulgaria.txt"
            """);
        Write(game, "common/characters/USA.txt", """
            characters = {
                USA_chester_w_nimitz = {
                    name = USA_chester_w_nimitz
                    navy_leader = {
                        traits = { blue_water_expert infantry_officer }
                        skill = 4
                        attack_skill = 2
                        maneuvering_skill = 4
                        coordination_skill = 5
                    }
                }
                USA_george_s_patton = { name = USA_george_s_patton corps_commander = { skill = 4 } }
                debug_ernest_king = { name = "Ernest King" navy_leader = { skill = 3 } }
            }
            """);
        Write(game, "history/countries/USA - USA.txt", """
            capital = 1
            1936.1.1 = { recruit_character = USA_chester_w_nimitz }
            """);
        Write(game, "history/countries/BUL - Bulgaria.txt", """
            if = { limit = { always = yes } recruit_character = debug_ernest_king }
            """);
        Write(game, "localisation/english/leaders_l_english.yml", """
            l_english:
             USA_chester_w_nimitz: "Chester W. Nimitz"
             blue_water_expert: "Blue Water Expert"
            """);

        // The mod halves the attack skill, as TFB does.
        Write(mod, "common/unit_leader/00_attack_skills.txt", """
            leader_attack_skills = {
                1 = { type = navy modifier = { naval_damage_factor = 0.025 } }
            }
            """);
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void Skills_KeepTheNavyLevels_AndAModFileReplacesTheGamesFile()
    {
        var import = LeaderImporter.Read(new GameFileSystem(game, [mod]));

        var attack = import.Skills.Single(skill => skill.ID == "attack_skill");
        Assert.Equal([1], attack.Levels.Select(level => level.Level));
        Assert.Equal(0.025, attack.Levels[0].Modifiers["naval_damage_factor"]);
        var maneuvering = import.Skills.Single(skill => skill.ID == "maneuvering_skill");
        Assert.Equal(0.01, maneuvering.Levels[0].Modifiers["naval_retreat_speed"]);

        var vanilla = LeaderImporter.Read(new GameFileSystem(game)).Skills.Single(skill => skill.ID == "attack_skill");
        Assert.Equal([1, 2], vanilla.Levels.Select(level => level.Level));
        Assert.Equal(0.02, vanilla.Levels[0].BuildingModuleModifiers["spotting_chance"]);
    }

    [Fact]
    public void Traits_OfTypeNavyOrAll_KeepTheirModifiersAdjustersAndTree()
    {
        var import = LeaderImporter.Read(new GameFileSystem(game, [mod]));
        var traits = import.Traits.ToDictionary(trait => trait.ID);

        Assert.Equal(["blue_water_expert", "destroyer_leader", "demoted"], import.Traits.Select(trait => trait.ID));
        Assert.Equal("Blue Water Expert", traits["blue_water_expert"].Name);
        Assert.Equal(0.1, traits["blue_water_expert"].Adjusters["water_deep_ocean"].Attack);
        Assert.Empty(traits["blue_water_expert"].Modifiers);

        var leader = traits["destroyer_leader"];
        Assert.Equal(0.05, leader.SubUnitModifiers["destroyer"]["naval_speed_factor"]);
        Assert.Equal(0.1, leader.SubUnitAdjusters["destroyer"].Attack);
        Assert.Equal(["fleet_protector", "superior_tactician"], leader.Parents.Single().Traits);
        Assert.Equal(1, leader.Parents.Single().Needed);
        Assert.Equal(["lancer"], leader.MutuallyExclusive);
        Assert.Equal(["on_add"], leader.Scripted);

        Assert.Equal(-1, traits["demoted"].Skills["attack_skill"]);
        Assert.Equal(-0.5, traits["demoted"].NonSharedModifiers["skill_bonus_factor"]);
    }

    [Fact]
    public void Admirals_AreTheNavyLeaders_WithTheirRecruitingCountry()
    {
        var import = LeaderImporter.Read(new GameFileSystem(game, [mod]));

        Assert.Equal(["USA_chester_w_nimitz", "debug_ernest_king"], import.Admirals.Select(admiral => admiral.ID));
        var nimitz = import.Admirals[0];
        Assert.Equal("Chester W. Nimitz", nimitz.Name);
        Assert.Equal("USA", nimitz.Country);
        Assert.Equal((4, 2, 1, 4, 5), (nimitz.Skill, nimitz.AttackSkill, nimitz.DefenseSkill, nimitz.ManeuveringSkill, nimitz.CoordinationSkill));
        Assert.Equal(["blue_water_expert"], nimitz.Traits);
        Assert.Contains("admiral 'USA_chester_w_nimitz' has trait 'infantry_officer', which is not a navy trait; it is left out.", import.Warnings);

        // Recruited by Bulgaria's history file, not by the file it is defined in.
        var king = import.Admirals[1];
        Assert.Equal(("Ernest King", "BUL"), (king.Name, king.Country));
    }

    [Fact]
    public void Writer_ReplacesTheThreeFolders()
    {
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(data, "admirals"));
        File.WriteAllText(Path.Combine(data, "admirals", "old.json"), "{}");

        var files = LeaderWriter.Write(data, LeaderImporter.Read(new GameFileSystem(game, [mod])));

        Assert.Equal(["admirals/admirals.json", "leader-skills/navy-skills.json", "leader-traits/navy-traits.json"], files);
        Assert.False(File.Exists(Path.Combine(data, "admirals", "old.json")));
        var catalog = new SetupLoader().ReadCatalog(data);
        Assert.Equal(2, catalog.Admirals.Count);
        Assert.Equal(3, catalog.LeaderTraits.Count);
        Assert.Equal(["attack_skill", "maneuvering_skill"], catalog.LeaderSkills.Select(skill => skill.ID));
        Assert.Equal(new AdmiralDto().Traits, catalog.Admirals[1].Traits);
    }

    private static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
