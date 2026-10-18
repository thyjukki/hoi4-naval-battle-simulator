namespace NavySimulator.GameData.Tests;

internal static class RepositoryPaths
{
    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NavySimulator.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }

    public static string Data => Path.Combine(Root, "data");
}
