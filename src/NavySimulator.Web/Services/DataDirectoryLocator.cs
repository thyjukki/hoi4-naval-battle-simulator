namespace NavySimulator.Web.Services;

/// <summary>Finds the data directory to edit: the nearest <c>data</c> folder with content, above the working directory or the executable.</summary>
public static class DataDirectoryLocator
{
    public static string? Find()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                foreach (var name in new[] { "data", "Data" })
                {
                    var candidate = Path.Combine(directory.FullName, name);

                    if (IsDataDirectory(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    public static bool IsDataDirectory(string path)
    {
        return Directory.Exists(Path.Combine(path, "hulls")) && Directory.Exists(Path.Combine(path, "ship-designs"));
    }
}
