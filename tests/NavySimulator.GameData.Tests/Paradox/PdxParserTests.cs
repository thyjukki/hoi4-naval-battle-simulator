using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Tests.Paradox;

public class PdxParserTests
{
    private const string ModuleSample = """
        # Excerpt in the shape of common/units/equipment/modules/00_ship_modules.txt
        equipment_modules = {
            limit = { ship_light_battery_1 = 4 }

            ship_light_battery_1 = {
                abbreviation = "ship_light_battery_1"
                category = ship_light_battery
                gfx = gfx_ship_light_battery_1
                add_stats = {
                    lg_attack = 5
                    lg_armor_piercing = 5.5
                    build_cost_ic = 125
                }
                multiply_stats = { naval_speed = -0.01 }
                add_average_stats = { lg_armor_piercing = 3 }
                critical_parts = { damaged_light_guns }
                can_convert_from = { module_category = ship_light_battery }
                can_convert_from = { module_category = ship_anti_air }
            }
        }
        """;

    [Fact]
    public void Parse_ReadsNestedBlocksAndNumbers()
    {
        var root = PdxParser.Parse(ModuleSample);
        var module = root.GetBlock("equipment_modules")!.GetBlock("ship_light_battery_1")!;

        Assert.Equal("ship_light_battery", module.GetString("category"));
        Assert.Equal(5.5, module.GetBlock("add_stats")!.GetDouble("lg_armor_piercing"));
        Assert.Equal(-0.01, module.GetBlock("multiply_stats")!.GetDouble("naval_speed"));
        Assert.Equal(new Dictionary<string, double> { ["lg_attack"] = 5, ["lg_armor_piercing"] = 5.5, ["build_cost_ic"] = 125 },
            module.GetBlock("add_stats")!.GetNumericEntries());
    }

    [Fact]
    public void Parse_KeepsRepeatedKeysAndBareValues()
    {
        var module = PdxParser.Parse(ModuleSample).GetBlock("equipment_modules")!.GetBlock("ship_light_battery_1")!;

        Assert.Equal(2, module.GetAll("can_convert_from").Count());
        Assert.Equal(["damaged_light_guns"], module.GetBlock("critical_parts")!.BareValues.Select(value => value.Scalar));
    }

    [Fact]
    public void Parse_HandlesQuotedStringsComparisonOperatorsAndComments()
    {
        var root = PdxParser.Parse("""
            name = "Iowa # not a comment"   # a comment
            trigger = { year >= 1940 num_of_factories < 5 has_dlc != "Man the Guns" }
            """);

        Assert.Equal("Iowa # not a comment", root.GetString("name"));
        var trigger = root.GetBlock("trigger")!;
        Assert.Equal([">=", "<", "!="], trigger.Entries.Select(entry => entry.Operator));
        Assert.True(trigger.Get("has_dlc")!.IsQuoted);
    }

    [Fact]
    public void Parse_ReportsLineOfUnbalancedBraces()
    {
        var exception = Assert.Throws<PdxParseException>(() => PdxParser.Parse("a = {\n b = 1\n", "sample.txt"));

        Assert.Contains("sample.txt", exception.Message);
        Assert.Contains("Missing closing brace", exception.Message);
    }
}
