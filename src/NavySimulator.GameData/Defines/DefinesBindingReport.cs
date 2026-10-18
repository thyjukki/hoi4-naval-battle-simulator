namespace NavySimulator.GameData.Defines;

public sealed class DefinesBindingReport
{
    public List<string> Applied { get; } = [];
    public List<string> TypeMismatches { get; } = [];

    /// <summary>Number of keys in the source files that the engine does not model. Expected to be large.</summary>
    public int IgnoredKeyCount { get; set; }
}
