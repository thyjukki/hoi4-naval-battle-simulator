using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>S-NATIONAL: the national spirit, dynamic modifier and focus import from a small game folder with a mod over it.</summary>
public sealed class NationImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-nations-" + Guid.NewGuid().ToString("N"));
    private readonly NationImport import;

    public NationImporterTests()
    {
        var game = Path.Combine(root, "game");
        var mod = Path.Combine(root, "mod");

        Write(game, "common/idea_tags/00_idea.txt", """
            idea_categories = {
                country = { type = national_spirit }
                naval_command_spirit = { type = navy_spirit slot = naval_command_spirit }
            }
            """);
        Write(game, "common/ideas/japan.txt", """
            ideas = {
                country = {
                    JAP_militarism = { allowed = { original_tag = JAP } modifier = { navy_org_factor = 0.05 stability_factor = 0.1 } }
                    JAP_battlefleet = { allowed = { always = no } on_add = { add_stability = 0.1 } modifier = { navy_capital_ship_defence_factor = 0.1 } }
                    JAP_zaibatsus = { modifier = { production_speed_buildings_factor = 0.1 } }
                    JAP_tanks = { equipment_bonus = { armor = { armor_value = 1 } } }
                    JAP_cruisers = { name = JAP_cruiser_idea equipment_bonus = { ship_hull_cruiser = { lg_attack = 0.1 build_cost_ic = -0.1 instant = yes } } }
                    JAP_day_of_infamy = {
                        modifier = { navy_anti_air_attack_factor = -0.1 }
                        targeted_modifier = { tag = JAP navy_capital_ship_attack_factor_against = -0.1 }
                    }
                }
            }
            """);
        Write(game, "common/ideas/navy_spirits.txt", """
            ideas = { naval_command_spirit = { JAP_command_spirit = { allowed = { original_tag = JAP } positioning = 0.1 } } }
            """);
        Write(game, "common/dynamic_modifiers/sea.txt", """
            JAP_imperial_navy_dm = {
                icon = GFX_idea
                enable = { always = yes }
                custom_modifier_tooltip = x_tt
                naval_torpedo_hit_chance_factor = JAP_navy_torpedo
                naval_invasion_prep_speed = JAP_navy_invasion
                naval_detection = -0.1
            }
            JAP_industry_dm = { production_speed_buildings_factor = JAP_industry_speed }
            """);
        Write(game, "common/scripted_effects/jap.txt", """
            JAP_add_or_modify_imperial_navy = {
                IF = {
                    limit = { NOT = { has_dynamic_modifier = { modifier = JAP_imperial_navy_dm } } }
                    hidden_effect = { add_dynamic_modifier = { modifier = JAP_imperial_navy_dm } }
                }
                ELSE = { add_dynamic_modifier = { modifier = JAP_industry_dm } }
            }
            """);
        Write(game, "history/countries/JAP - Japan.txt", """
            add_ideas = { JAP_militarism JAP_zaibatsus limited_exports }
            add_dynamic_modifier = { modifier = JAP_imperial_navy_dm }
            set_variable = { JAP_navy_torpedo = 0.15 }
            set_variable = { JAP_navy_invasion = 0.1 }
            1936.1.1 = { set_variable = { var = JAP_navy_torpedo value = 0.2 } }
            1939.1.1 = { add_ideas = JAP_battlefleet }
            """);
        Write(game, "history/countries/USA - USA.txt", """
            add_ideas = { great_depression }
            """);
        Write(game, "common/national_focus/japan.txt", """
            focus_tree = {
                id = japan_focus
                country = { factor = 0 modifier = { add = 10 tag = JAP } }
                focus = {
                    id = JAP_mine_warfare
                    completion_reward = {
                        JAP_add_or_modify_imperial_navy = yes
                        add_to_variable = { JAP_navy_torpedo = 0.05 tooltip = x_tt }
                        add_to_variable = { JAP_navy_invasion = 0.1 }
                    }
                }
                focus = { id = JAP_only_readds completion_reward = { JAP_add_or_modify_imperial_navy = yes } }
                focus = {
                    id = JAP_supremacy_of_the_battlefleet
                    mutually_exclusive = { focus = JAP_carriers }
                    completion_reward = { swap_ideas = { remove_idea = JAP_militarism add_idea = JAP_battlefleet } }
                }
                focus = {
                    id = JAP_carriers
                    mutually_exclusive = { focus = JAP_supremacy_of_the_battlefleet }
                    completion_reward = {
                        if = { limit = { has_dlc = "Man the Guns" } add_timed_idea = { idea = JAP_cruisers days = 365 } }
                        else = { add_ideas = JAP_battlefleet }
                    }
                }
                focus = { id = JAP_pearl_harbor completion_reward = { USA = { add_ideas = JAP_day_of_infamy } add_political_power = 100 } }
                focus = { id = JAP_economy completion_reward = { add_ideas = JAP_zaibatsus } }
            }
            focus_tree = { id = generic_focus country = { factor = 1 } focus = { id = generic_x } }
            """);
        Write(game, "common/national_focus/usa.txt", """
            focus_tree = {
                id = usa_focus
                country = { factor = 0 modifier = { add = 10 tag = USA } }
                shared_focus = ABDA_start
            }
            shared_focus = { id = ABDA_start completion_reward = { add_ideas = USA_subs } }
            shared_focus = { id = ABDA_next prerequisite = { focus = ABDA_start } completion_reward = { add_ideas = USA_subs } }
            shared_focus = { id = ABDA_other completion_reward = { add_ideas = USA_subs } }
            """);
        Write(game, "localisation/english/nations_l_english.yml", """
            l_english:
             JAP: "Japan"
             JAP_mine_warfare: "Establish the Mine Warfare Force"
             JAP_militarism: "Militarism"
             JAP_cruiser_idea: "Cruiser Program"
            """);

        // The mod adds an idea file.
        Write(mod, "common/ideas/usa.txt", """
            ideas = { country = { USA_subs = { equipment_bonus = { submarine = { sub_visibility = -0.1 } } } } }
            """);

        import = NationImporter.Read(new GameFileSystem(game, [mod]), ["JAP", "usa"]);
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void Spirits_KeepThoseWithModelledEffects_FromHistoryFocusesAndTriggers()
    {
        var japan = Nation("JAP");

        Assert.Equal("Japan", japan.Name);
        Assert.Equal("japan_focus", japan.FocusTree);

        // Not kept: the economy idea, the tank bonus, a spirit-slot idea and the idea targeted against Japan itself.
        Assert.Equal(["JAP_militarism", "JAP_battlefleet", "JAP_cruisers"], japan.Spirits.Select(spirit => spirit.ID));
        Assert.Equal(["history", "allowed"], Spirit(japan, "JAP_militarism").Sources);
        Assert.Equal(["focus:JAP_supremacy_of_the_battlefleet", "id"], Spirit(japan, "JAP_battlefleet").Sources);
        Assert.Equal("Cruiser Program", Spirit(japan, "JAP_cruisers").Name);
        Assert.Equal(0.1, Spirit(japan, "JAP_cruisers").Effects.EquipmentBonuses["ship_hull_cruiser"]["lg_attack"]);

        // on_add runs once; the simulator does not list it.
        Assert.Empty(Spirit(japan, "JAP_battlefleet").Effects.Scripted);
    }

    [Fact]
    public void AnIdeaAnotherCountryGives_OrTargetedAgainstIt_GoesToTheOtherCountry()
    {
        var usa = Nation("USA");
        var infamy = Spirit(usa, "JAP_day_of_infamy");

        Assert.Equal(["JAP focus:JAP_pearl_harbor", "against:JAP"], infamy.Sources);
        Assert.Equal(-0.1, Assert.Single(infamy.TargetedModifiers).Modifiers["navy_capital_ship_attack_factor_against"]);
        Assert.Empty(infamy.Effects.Scripted);
    }

    [Fact]
    public void DynamicModifiers_ReadNumbersAndVariables()
    {
        var modifier = Assert.Single(Nation("JAP").DynamicModifiers);

        Assert.Equal("JAP_imperial_navy_dm", modifier.ID);
        Assert.Equal(-0.1, modifier.Modifiers["naval_detection"]);
        Assert.Equal("JAP_navy_torpedo", modifier.VariableModifiers["naval_torpedo_hit_chance_factor"]);
        Assert.DoesNotContain("custom_modifier_tooltip", modifier.VariableModifiers.Keys);
    }

    [Fact]
    public void History_KeepsEffectsOnKeptContent_UpToThe1936Start()
    {
        var history = Nation("JAP").History;

        // The 1939 block is after the start; the invasion variable's key is not modelled.
        Assert.Equal(
            [("add_ideas", "JAP_militarism", (double?)null), ("add_dynamic_modifier", "JAP_imperial_navy_dm", null), ("set_variable", "JAP_navy_torpedo", 0.15),
             ("set_variable", "JAP_navy_torpedo", 0.2)],
            history.Select(effect => (effect.Effect, effect.Target, effect.Value)));
    }

    [Fact]
    public void Focuses_FollowScriptedEffectsIfsAndSwaps_AndDropThoseWithoutNewEffects()
    {
        var japan = Nation("JAP");

        // JAP_only_readds only adds the history's modifier again; JAP_economy only touches an idea that is not kept.
        Assert.Equal(["JAP_mine_warfare", "JAP_supremacy_of_the_battlefleet", "JAP_carriers"], japan.Focuses.Select(focus => focus.ID));

        var mine = japan.Focuses[0];
        Assert.Equal("Establish the Mine Warfare Force", mine.Name);
        Assert.Equal([("add_dynamic_modifier", "JAP_imperial_navy_dm"), ("add_to_variable", "JAP_navy_torpedo")], mine.Effects.Select(effect => (effect.Effect, effect.Target)));
        Assert.StartsWith("{ NOT = ", mine.Effects[0].Condition);
        Assert.Null(mine.Effects[1].Condition);
        Assert.Equal(0.05, mine.Effects[1].Value);

        Assert.Equal([("remove_ideas", "JAP_militarism"), ("add_ideas", "JAP_battlefleet")],
            japan.Focuses[1].Effects.Select(effect => (effect.Effect, effect.Target)));
        Assert.Equal(["JAP_carriers"], japan.Focuses[1].MutuallyExclusive);

        // The if branch is taken, the else branch is not.
        var timed = Assert.Single(japan.Focuses[2].Effects);
        Assert.Equal(("add_ideas", "JAP_cruisers", (int?)365), (timed.Effect, timed.Target, timed.Days));
        Assert.Contains("has_dlc", timed.Condition);
    }

    [Fact]
    public void SharedFocuses_ComeWithTheTreeThatNamesThem_AndTheirFollowers()
    {
        var usa = Nation("USA");

        Assert.Equal(["ABDA_start", "ABDA_next"], usa.Focuses.Select(focus => focus.ID));
        Assert.Equal(["focus:ABDA_start", "focus:ABDA_next", "id"], Spirit(usa, "USA_subs").Sources);
        Assert.Empty(usa.History);
    }

    [Fact]
    public void Writer_WritesOneFilePerCountry_AndKeepsOthers()
    {
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(data, "nations"));
        File.WriteAllText(Path.Combine(data, "nations", "GER.json"), """{ "nations": [{ "id": "GER" }] }""");

        Assert.Equal(["nations/JAP.json", "nations/USA.json"], NationWriter.Write(data, import));
        Assert.True(File.Exists(Path.Combine(data, "nations", "GER.json")));

        var catalog = new SetupLoader().ReadCatalog(data);
        Assert.Equal(["GER", "JAP", "USA"], catalog.Nations.Select(nation => nation.ID).Order());
    }

    private NationDto Nation(string tag)
    {
        return import.Nations.Single(nation => nation.ID == tag);
    }

    private static NationalSpiritDto Spirit(NationDto nation, string id)
    {
        return nation.Spirits.Single(spirit => spirit.ID == id);
    }

    private static void Write(string directory, string relativePath, string text)
    {
        var path = Path.Combine(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
