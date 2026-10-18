using NavySimulator.Web.Services;

namespace NavySimulator.Web.Tests;

/// <summary>A <see cref="Workspace"/> over a temporary copy of the repository's data directory.</summary>
internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "navysim-web-" + Guid.NewGuid().ToString("N"));
        DataDirectory = Path.Combine(Root, "data");
        CopyDirectory(Path.Combine(RepositoryRoot(), "data"), DataDirectory);
        Workspace = new Workspace(new WorkspaceOptions(DataDirectory, []), new GuiSettingsStore(Path.Combine(Root, "settings.json")));
    }

    public string Root { get; }
    public string DataDirectory { get; }
    public Workspace Workspace { get; }

    public void Dispose()
    {
        Directory.Delete(Root, true);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NavySimulator.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
