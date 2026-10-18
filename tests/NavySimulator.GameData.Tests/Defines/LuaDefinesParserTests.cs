using NavySimulator.GameData.Defines;

namespace NavySimulator.GameData.Tests.Defines;

public class LuaDefinesParserTests
{
    private const string VanillaShape = """
        NDefines = {

        NGame = {
            START_DATE = "1936.1.1.12",	-- comment, with = signs
        },

        NNavy = {
            COMBAT_BASE_HIT_CHANCE = 0.1,		-- base chance for hit
            AMPHIBIOUS_LANDING_PENALTY = -0.5,
            MIN_HOURS_TO_SHUFFLE_NEWLY_ASSIGNED_PATROLS = 7 * 24,
            GUN_HIT_PROFILES = { -- hit profiles for guns
                80.0,	-- big guns
                100.0,	-- torpedoes
                45.0,	-- small guns
            },
            CONVOY_BLOCKED_BY_ENEMY_CONTROLLED_REGION = true,
            --[[ a block comment
            DISABLED = 1,
            ]]
        },

        }
        """;

    [Fact]
    public void Parse_ReadsVanillaTableLayout()
    {
        var table = LuaDefinesParser.Parse(VanillaShape);

        Assert.Equal("1936.1.1.12", table.Get("NGame", "START_DATE")!.Text);
        Assert.Equal(0.1, table.Get("NNavy", "COMBAT_BASE_HIT_CHANCE")!.Number);
        Assert.Equal(-0.5, table.Get("NNavy", "AMPHIBIOUS_LANDING_PENALTY")!.Number);
        Assert.Equal(168, table.Get("NNavy", "MIN_HOURS_TO_SHUFFLE_NEWLY_ASSIGNED_PATROLS")!.Number);
        Assert.Equal([80.0, 100.0, 45.0], table.Get("NNavy", "GUN_HIT_PROFILES")!.ToNumberArray()!);
        Assert.True(table.Get("NNavy", "CONVOY_BLOCKED_BY_ENEMY_CONTROLLED_REGION")!.Boolean);
        Assert.Null(table.Get("NNavy", "DISABLED"));
    }

    [Fact]
    public void Parse_ReadsModOverrideStatements()
    {
        var table = LuaDefinesParser.Parse("""
            NDefines.NNavy.COMBAT_BASE_HIT_CHANCE = 0.12
            NDefines.NNavy.BASE_GUN_COOLDOWNS = { 1, 3, (2 - 1) / 2 }
            NDefines.NAir.NAVAL_STRIKE_CARRIER_MULTIPLIER = -(-2.5e1)
            """);

        Assert.Equal(0.12, table.Get("NNavy", "COMBAT_BASE_HIT_CHANCE")!.Number);
        Assert.Equal([1.0, 3.0, 0.5], table.Get("NNavy", "BASE_GUN_COOLDOWNS")!.ToNumberArray()!);
        Assert.Equal(25, table.Get("NAir", "NAVAL_STRIKE_CARRIER_MULTIPLIER")!.Number);
    }

    [Fact]
    public void Merge_LetsLaterFilesOverrideEarlierOnes()
    {
        var table = LuaDefinesParser.Parse(VanillaShape);
        table.Merge(LuaDefinesParser.Parse("NDefines.NNavy.COMBAT_BASE_HIT_CHANCE = 0.2"));

        Assert.Equal(0.2, table.Get("NNavy", "COMBAT_BASE_HIT_CHANCE")!.Number);
        Assert.Equal(-0.5, table.Get("NNavy", "AMPHIBIOUS_LANDING_PENALTY")!.Number);
    }

    [Fact]
    public void Parse_SkipsStatementsItCannotEvaluate_WithWarnings()
    {
        // Shapes found in real files: 00_graphics.lua ends with a for loop over NDefines_Graphics.
        var table = LuaDefinesParser.Parse("""
            NDefines_Graphics = { NGraphics = { X = 1 } }
            NDefines.NNavy.X = 1
            NDefines.NNavy.Y 2
            local factor = 3
            NDefines.NNavy.Z = NDefines.NNavy.X * 2
            for k,v in pairs( NDefines_Graphics ) do NDefines[k] = v end
            NDefines.NNavy.W = 4
            """, "bad.lua");

        Assert.Equal(1, table.Get("NNavy", "X")!.Number);
        Assert.Equal(4, table.Get("NNavy", "W")!.Number);
        Assert.Null(table.Get("NNavy", "Z")!.Number);
        Assert.Null(table.Get("NGraphics", "X"));
        Assert.Contains(table.Warnings, warning => warning.Contains("bad.lua:3"));
        Assert.Contains(table.Warnings, warning => warning.Contains("NDefines.NNavy.X * 2"));
    }

    [Fact]
    public void Parse_AppliesIndexedAssignmentsToEarlierArrays()
    {
        var table = LuaDefinesParser.Parse("""
            NDefines.NNavy.BASE_GUN_COOLDOWNS = { 1, 4, 1 }
            NDefines.NNavy.BASE_GUN_COOLDOWNS[2] = 3
            """);

        Assert.Equal([1.0, 3.0, 1.0], table.Get("NNavy", "BASE_GUN_COOLDOWNS")!.ToNumberArray()!);
    }
}
