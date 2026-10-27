using NavySimulator.GameData.Import;

namespace NavySimulator.GameData.Tests.Import;

/// <summary>The plane import (A-PLANES) from a small game folder with a mod over it.</summary>
public sealed class PlaneImporterTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "navysim-planes-" + Guid.NewGuid().ToString("N"));
    private readonly string game;
    private readonly string mod;

    public PlaneImporterTests()
    {
        game = Path.Combine(root, "game");
        mod = Path.Combine(root, "mod");

        Write(game, "common/units/equipment/single_engine_airframe.txt", """
            equipments = {
                cv_nav_bomber_equipment = {
                    is_archetype = yes
                    type = naval_bomber
                    carrier_capable = yes
                    interface_category = interface_category_air
                    allow_mission_type = training
                    allow_mission_type = { naval_bomber port_strike }
                    reliability = 0.8
                    naval_strike_attack = 10
                    naval_strike_targetting = 10
                }
                cv_nav_bomber_equipment_1 = { year = 1936 archetype = cv_nav_bomber_equipment air_agility = 30 }
            }
            """);
        Write(game, "common/units/air.txt", """
            sub_units = {
                cv_nav_bomber = { type = { naval_bomber } need = { cv_nav_bomber_equipment = 1 } carrier_air_wing_size = 10 land_air_wing_size = 100 }
                cv_cas = { type = { cas strategic_bomber } need = { cv_CAS_equipment = 1 } }
            }
            """);
        Write(game, "common/aces/00_aces.txt", """
            modifiers = {
                support_good = { type = { cas tactical_bomber naval_bomber } chance = 0.9 effect = { naval_strike_targetting_factor = 0.05 air_agility_factor = 0.05 } }
                fighter_good = { type = { fighter interceptor } chance = 0.9 effect = { air_attack_factor = 0.05 } }
            }
            """);
        Write(game, "localisation/english/equipment_l_english.yml", """
            l_english:
             cv_nav_bomber_equipment_2: "Carrier Naval Bomber II"
            """);

        // The mod replaces the file, as TFB does: its archetype and variants win.
        Write(mod, "common/units/equipment/single_engine_airframe.txt", """
            equipments = {
                cv_nav_bomber_equipment = {
                    is_archetype = yes
                    type = naval_bomber
                    carrier_capable = yes
                    interface_category = interface_category_air
                    allow_mission_type = { naval_bomber port_strike naval_patrol }
                    reliability = 0.8
                    naval_strike_attack = 10
                    naval_strike_targetting = 10
                    build_cost_ic = 112
                }
                cv_nav_bomber_equipment_2 = {
                    year = 1939
                    archetype = cv_nav_bomber_equipment
                    parent = cv_nav_bomber_equipment_1
                    maximum_speed = 270
                    air_agility = 35
                    naval_strike_attack = 14
                }
                fighter_equipment = {
                    is_archetype = yes
                    type = { fighter interceptor }
                    interface_category = interface_category_air
                    allow_mission_type = { air_superiority interception }
                }
                fighter_equipment_1 = { archetype = fighter_equipment air_attack = 24 }
                small_plane_airframe = {
                    is_archetype = yes
                    type = fighter
                    interface_category = interface_category_air
                    module_slots = { }
                }
                small_plane_airframe_1 = { archetype = small_plane_airframe module_slots = { } }
                guided_missile_equipment = { is_archetype = yes type = missile interface_category = interface_category_air }
                guided_missile_equipment_1 = { archetype = guided_missile_equipment }
            }
            """);
    }

    // TFB since its air rewrite (BBA's layout): fixed planes on airframe archetypes with module slots and on
    // duplicate_archetypes, missions only on each line's first variant.
    private void WriteBbaLayout()
    {
        Write(mod, "common/units/equipment/single_engine_airframe.txt", "equipments = { }");
        Write(mod, "common/units/equipment/plane_airframes.txt", """
            equipments = {
                small_plane_airframe = {
                    is_archetype = yes
                    type = fighter
                    interface_category = interface_category_air
                    allow_mission_type = training
                    air_superiority = 1
                    module_slots = { fixed_main_weapon_slot = { required = yes } }
                }
                small_plane_airframe_1 = { archetype = small_plane_airframe derived_variant_name = fighter_equipment_1 module_slots = { } }
            }
            """);
        Write(mod, "common/units/equipment/x_plane_airframes.txt", """
            duplicate_archetypes = {
                small_plane_naval_bomber_airframe = {
                    archetype = small_plane_airframe
                    type = naval_bomber
                    variant_name = { small_plane_naval_bomber_airframe_1 = nav_bomber_equipment_1 }
                }
            }
            equipments = {
                fighter_equipment_0 = { archetype = small_plane_airframe allow_mission_type = { air_superiority interception } air_attack = 20 }
                nav_bomber_equipment_1 = {
                    archetype = small_plane_naval_bomber_airframe
                    allow_mission_type = { naval_bomber port_strike }
                    naval_strike_attack = 12
                }
                nav_bomber_equipment_2 = { archetype = small_plane_naval_bomber_airframe parent = nav_bomber_equipment_1 naval_strike_attack = 15 }
            }
            """);
        Write(mod, "common/units/air.txt", """
            sub_units = {
                fighter = { type = { fighter } need = { small_plane_airframe = 1 } }
                nav_bomber = { type = { naval_bomber } need = { small_plane_naval_bomber_airframe = 1 } land_air_wing_size = 100 }
            }
            """);
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    [Fact]
    public void Read_TakesFixedPlanes_WithTheArchetypesStatsTypesAndMissions()
    {
        var import = PlaneImporter.Read(new GameFileSystem(game, [mod]));

        Assert.Equal(["cv_nav_bomber_equipment_2", "fighter_equipment_1"], import.Planes.Select(plane => plane.ID));
        var bomber = import.Planes[0];
        Assert.Equal("Carrier Naval Bomber II", bomber.Name);
        Assert.Equal("cv_nav_bomber_equipment", bomber.Archetype);
        Assert.Equal(1939, bomber.Year);
        Assert.True(bomber.CarrierCapable);
        Assert.Equal(["naval_bomber"], bomber.Types);
        Assert.Equal(["naval_bomber", "port_strike", "naval_patrol"], bomber.Missions);
        Assert.Equal(14, bomber.Stats.NavalAttack);
        Assert.Equal(10, bomber.Stats.NavalTargeting);
        Assert.Equal(35, bomber.Stats.Agility);
        Assert.Equal(270, bomber.Stats.Speed);
        Assert.Equal(0.8, bomber.Stats.Reliability);
        Assert.Equal(112, bomber.Stats.ProductionCost);
        Assert.Equal("cv_nav_bomber", bomber.SubUnit);
        Assert.Equal(["naval_bomber"], bomber.SubUnitTypes);
        Assert.Equal(10, bomber.CarrierWingSize);
        Assert.Equal(100, bomber.LandWingSize);

        var fighter = import.Planes[1];
        Assert.False(fighter.CarrierCapable);
        Assert.Equal(["fighter", "interceptor"], fighter.Types);
        Assert.Equal(24, fighter.Stats.AirAttack);
        Assert.Null(fighter.SubUnit);
        Assert.Contains("plane 'fighter_equipment_1': no sub-unit needs 'fighter_equipment'.", import.Warnings);
    }

    [Fact]
    public void Read_TakesFixedPlanesOfAirframesAndDuplicateArchetypes_LeavingDesignerAirframesOut()
    {
        WriteBbaLayout();

        var import = PlaneImporter.Read(new GameFileSystem(game, [mod]));

        Assert.DoesNotContain(import.Planes, plane => plane.ID == "small_plane_airframe_1");
        var fighter = Assert.Single(import.Planes, plane => plane.ID == "fighter_equipment_0");
        Assert.Equal("small_plane_airframe", fighter.Archetype);
        Assert.Equal(["fighter"], fighter.Types);
        Assert.Equal(["training", "air_superiority", "interception"], fighter.Missions);
        Assert.Equal(1, fighter.Stats.AirSuperiority);
        Assert.Equal("fighter", fighter.SubUnit);

        var bomber = Assert.Single(import.Planes, plane => plane.ID == "nav_bomber_equipment_2");
        Assert.Equal("small_plane_naval_bomber_airframe", bomber.Archetype);
        Assert.Equal(["naval_bomber"], bomber.Types);
        Assert.Equal(1, bomber.Stats.AirSuperiority);
        Assert.Equal(15, bomber.Stats.NavalAttack);
        Assert.Equal("nav_bomber", bomber.SubUnit);
        Assert.Equal(100, bomber.LandWingSize);
    }

    [Fact]
    public void Read_TakesMissionsFromTheParentChain_WhenAVariantListsNone()
    {
        WriteBbaLayout();

        var import = PlaneImporter.Read(new GameFileSystem(game, [mod]));

        Assert.Equal(["training", "naval_bomber", "port_strike"], import.Planes.Single(plane => plane.ID == "nav_bomber_equipment_1").Missions);
        Assert.Equal(["training", "naval_bomber", "port_strike"], import.Planes.Single(plane => plane.ID == "nav_bomber_equipment_2").Missions);
    }

    [Fact]
    public void Write_ReplacesThePlanesFolder()
    {
        var data = Path.Combine(root, "data");
        Write(data, "planes/old.json", "{}");

        var files = PlaneWriter.Write(data, PlaneImporter.Read(new GameFileSystem(game, [mod])));

        Assert.Equal(["planes/planes.json", "aces/aces.json"], files);
        Assert.False(File.Exists(Path.Combine(data, "planes", "old.json")));
        Assert.Contains("\"cv_nav_bomber_equipment_2\"", File.ReadAllText(Path.Combine(data, "planes", "planes.json")));
    }

    [Fact]
    public void Read_TakesTheAceTemplates()
    {
        var import = PlaneImporter.Read(new GameFileSystem(game, [mod]));

        var support = Assert.Single(import.Aces, ace => ace.ID == "support_good");
        Assert.Equal(["cas", "tactical_bomber", "naval_bomber"], support.Types);
        Assert.Equal(0.9, support.Chance);
        Assert.Equal(0.05, support.Effects["naval_strike_targetting_factor"]);
        Assert.Equal(2, import.Aces.Count);
    }

    private static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
