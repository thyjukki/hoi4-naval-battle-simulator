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
                small_plane_airframe_1 = { archetype = small_plane_airframe }
                guided_missile_equipment = { is_archetype = yes type = missile interface_category = interface_category_air }
                guided_missile_equipment_1 = { archetype = guided_missile_equipment }
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
    public void Write_ReplacesThePlanesFolder()
    {
        var data = Path.Combine(root, "data");
        Write(data, "planes/old.json", "{}");

        var files = PlaneWriter.Write(data, PlaneImporter.Read(new GameFileSystem(game, [mod])));

        Assert.Equal(["planes/planes.json"], files);
        Assert.False(File.Exists(Path.Combine(data, "planes", "old.json")));
        Assert.Contains("\"cv_nav_bomber_equipment_2\"", File.ReadAllText(Path.Combine(data, "planes", "planes.json")));
    }

    private static void Write(string folder, string relativePath, string text)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
