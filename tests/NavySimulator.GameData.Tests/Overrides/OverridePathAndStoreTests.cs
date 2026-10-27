using System.Text.Json.Nodes;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Overrides;

namespace NavySimulator.GameData.Tests.Overrides;

public class OverridePathAndStoreTests
{
    [Fact]
    public void Parse_ReadsNamesAndListEntries()
    {
        var segments = ContentPath.Parse("subdoctrines[line_of_battle].rewards[flagships].effects.countryModifiers.naval_hit_chance");

        Assert.Equal(
            [new ContentPathSegment("subdoctrines", "line_of_battle"), new ContentPathSegment("rewards", "flagships"), new ContentPathSegment("effects"),
             new ContentPathSegment("countryModifiers"), new ContentPathSegment("naval_hit_chance")],
            segments);
        Assert.Equal("subdoctrines[line_of_battle].rewards[flagships].effects.countryModifiers.naval_hit_chance", ContentPath.Join(segments));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a..b")]
    [InlineData("a[x")]
    [InlineData("a[]")]
    [InlineData("a.")]
    [InlineData("a[x]b")]
    public void Parse_RejectsMalformedPaths(string path)
    {
        Assert.Throws<FormatException>(() => ContentPath.Parse(path));
    }

    [Fact]
    public void Write_CreatesMissingObjects_MatchesPropertiesIgnoringCase_AndRemovesWithNull()
    {
        var root = (JsonObject)JsonNode.Parse("""{ "statModifiers": { "lightAttack": 1 }, "items": [ { "id": "a", "value": 1 } ] }""")!;

        ContentPath.Write(root, "StatModifiers.LightAttack", 5);
        ContentPath.Write(root, "effects.countryModifiers.naval_hit_chance", 0.1);
        ContentPath.Write(root, "items[a].value", 7);
        ContentPath.Write(root, "statModifiers.lightAttack", null);

        Assert.False(ContentPath.Exists(root, "statModifiers.lightAttack"));
        Assert.Equal(0.1, ContentPath.Read(root, "effects.countryModifiers.naval_hit_chance")!.GetValue<double>());
        Assert.Equal(7, ContentPath.Read(root, "items[a].value")!.GetValue<int>());
        Assert.Throws<InvalidOperationException>(() => ContentPath.Write(root, "items[b].value", 1));
    }

    [Theory]
    [InlineData("0.25", "0.25")]
    [InlineData("{ 3, 6, 2 }", "{ 3, 6, 2 }")]
    [InlineData("[3, 6, 2]", "{ 3, 6, 2 }")]
    [InlineData("true", "true")]
    [InlineData("some text", "some text")]
    [InlineData("\"quoted\"", "quoted")]
    public void Values_ParseAndFormatAsTyped(string text, string formatted)
    {
        Assert.Equal(formatted, OverrideValues.Format(OverrideValues.Parse(text)));
    }

    [Fact]
    public void Values_CompareNumbersByValue()
    {
        Assert.True(OverrideValues.Equal(JsonValue.Create(5), JsonNode.Parse("5.0")));
        Assert.False(OverrideValues.Equal(JsonValue.Create(5), JsonValue.Create("5")));
        Assert.Null(OverrideValues.Parse("  "));
    }

    [Fact]
    public void Store_WritesAndReadsASet_KeepingRemovedStaticModifiers()
    {
        var directory = Path.Combine(Path.GetTempPath(), "navysim-overrides-" + Guid.NewGuid().ToString("N"));

        try
        {
            var set = new OverrideSetDto { ID = "cl buff", Name = "CL buff", Description = "Lighter guns hit harder." };
            set.Defines["NNavy.BASE_GUN_COOLDOWNS"] = new JsonArray(3, 5, 2);
            set.StaticModifiers["night"] = new Dictionary<string, double?> { ["naval_hit_chance"] = -0.1, ["carrier_traffic"] = null };
            set.Content.Add(new ContentOverrideDto { Collection = "modules", ID = "ship_light_battery_3", Path = "statModifiers.lightAttack", Value = 12 });
            var file = OverrideSetStore.FileFor(set.ID);

            OverrideSetStore.Write(directory, file, set);
            var read = OverrideSetStore.Read(Path.Combine(directory, file));

            Assert.Equal("overrides/cl buff.json", file);
            Assert.Equal([file], OverrideSetStore.ListFiles(directory));
            Assert.Equal(OverrideSetStore.Serialize(set), OverrideSetStore.Serialize(read));
            Assert.True(read.StaticModifiers["night"].ContainsKey("carrier_traffic"));
            Assert.Null(read.StaticModifiers["night"]["carrier_traffic"]);
            Assert.Equal("cl buff", OverrideSetStore.Resolve("cl buff", directory).ID);
            Assert.Throws<ArgumentException>(() => OverrideSetStore.Write(directory, "hulls/x.json", set));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void Fields_ListLeavesDictionariesAndListEntries_WithoutTheId()
    {
        var catalog = new SetupLoader().ReadCatalog(RepositoryPaths.Data);
        var naval = catalog.DoctrineFolders.Single(folder => folder.ID == "naval");

        var fields = ContentFields.Describe(naval);

        Assert.Contains(fields, field => field.Path == "subdoctrines[line_of_battle].rewards[flagships].mastery" && field.Kind == ContentFieldKind.Number);
        Assert.Contains(fields, field => field.Path == "grandDoctrines[new_fleet_in_being].effects.countryModifiers" && field.Kind == ContentFieldKind.Dictionary);
        Assert.Contains(fields, field => field.Path == "name" && field.Kind == ContentFieldKind.Text);
        Assert.DoesNotContain(fields, field => field.Path == "id" || field.Path.EndsWith(".id", StringComparison.Ordinal));

        var module = ContentFields.Describe(catalog.Modules.Single(candidate => candidate.ID == "ship_light_battery_3"));
        Assert.Contains(module, field => field.Path == "statModifiers.lightAttack" && field.Kind == ContentFieldKind.Number);
        Assert.Contains(module, field => field.Path == "forbiddenCategories" && field.Kind == ContentFieldKind.List);
    }
}
