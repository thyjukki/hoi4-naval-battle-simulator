using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

public sealed class RulesetTests : IDisposable
{
    private readonly TestWorkspace test = new();

    public void Dispose()
    {
        test.Dispose();
    }

    private string Folder(params string[] parts)
    {
        var path = Path.Combine([test.Root, .. parts]);
        Directory.CreateDirectory(path);
        return path;
    }

    private string Mod(string folderName, string? descriptorName, string definesLine = "NDefines.NNavy.COMBAT_MIN_HIT_CHANCE = 0.33")
    {
        var root = Folder("mods", folderName);
        File.WriteAllText(Path.Combine(Folder("mods", folderName, "common", "defines"), "00_mod.lua"), definesLine + "\n");

        if (descriptorName is not null)
        {
            File.WriteAllText(Path.Combine(root, "descriptor.mod"), $"version=\"1\"\nname=\"{descriptorName} \"\nsupported_version=\"1.19.*\"\n");
        }

        return root;
    }

    [Fact]
    public void Locator_FindsTheGameWorkshopModsAndTheLaunchersLocalMods()
    {
        var library = Folder("steam");
        Folder("steam", "steamapps", "common", "Hearts of Iron IV", "common", "defines");
        var workshopMod = Folder("steam", "steamapps", "workshop", "content", "394360", "123");
        Folder("steam", "steamapps", "workshop", "content", "394360", "123", "common", "defines");
        File.WriteAllText(Path.Combine(workshopMod, "descriptor.mod"), "name=\"Workshop Mod\"\n");
        Folder("steam", "steamapps", "workshop", "content", "394360", "456", "gfx");
        var localDev = Mod("Local-Dev", "Local Dev");
        var modFolder = Folder("paradox", "mod");
        File.WriteAllText(Path.Combine(modFolder, "local.mod"), $"name=\"Local Dev (launcher)\"\npath=\"{localDev.Replace('\\', '/')}\"\n");
        File.WriteAllText(Path.Combine(modFolder, "ugc_123.mod"), $"name=\"Workshop Mod\"\npath=\"{workshopMod.Replace('\\', '/')}\"\n");
        File.WriteAllText(Path.Combine(modFolder, "missing.mod"), "name=\"Gone\"\npath=\"C:/does/not/exist\"\n");
        Folder("paradox", "mod", "In-Folder", "common", "modifiers");
        Folder("paradox", "mod", "Map-Only", "map");

        var candidates = Hoi4Locator.Find([library], modFolder);

        Assert.Equal(
            [
                ("Hearts of Iron IV", DefinesCandidateKind.Game),
                ("In-Folder", DefinesCandidateKind.LocalMod),
                ("Local Dev (launcher)", DefinesCandidateKind.LocalMod),
                ("Workshop Mod", DefinesCandidateKind.WorkshopMod)
            ],
            candidates.Select(candidate => (candidate.Name, candidate.Kind)));
        Assert.Equal("Local Dev (launcher)", Hoi4Locator.DisplayName(localDev + "\\", candidates));
    }

    [Fact]
    public void UseRuleset_SwitchesBetweenAModAndVanilla()
    {
        var mod = Mod("Their-Finest-Bruh", "Their Finest Bruh DEV");

        test.Workspace.UseRuleset(mod);
        var withMod = test.Workspace.BuildScenario();

        Assert.Equal("Their Finest Bruh DEV", test.Workspace.RulesetName);
        Assert.Equal([mod], test.Workspace.ExtraDefineSources);
        Assert.Equal(0.33, withMod.Scenario!.Defines.NNavy.COMBAT_MIN_HIT_CHANCE);

        test.Workspace.UseRuleset(null);
        var vanilla = test.Workspace.BuildScenario();

        Assert.Equal("Vanilla 1.19.4", test.Workspace.RulesetName);
        Assert.Empty(test.Workspace.ExtraDefineSources);
        Assert.NotEqual(0.33, vanilla.Scenario!.Defines.NNavy.COMBAT_MIN_HIT_CHANCE);
    }

    [Fact]
    public void Ruleset_IsRememberedBetweenSessions()
    {
        var mod = Mod("Their-Finest-Bruh", "Their Finest Bruh DEV");
        test.Workspace.UseRuleset(mod);

        var next = new Workspace(new WorkspaceOptions(test.DataDirectory, []), new GuiSettingsStore(Path.Combine(test.Root, "settings.json")));

        Assert.Equal([mod], next.SettingsDefineSources);
    }
}
