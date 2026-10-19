namespace NavySimulator.GameData.Contracts;

public class ModuleCountLimitDto
{
    /// <summary>Module IDs, or <c>any</c> / <c>empty</c>.</summary>
    public List<string> Modules { get; set; } = [];

    /// <summary>Module categories, or <c>any</c>.</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary><c>&lt;</c>, <c>&gt;</c>, <c>=</c> or <c>any</c>.</summary>
    public string Operator { get; set; } = "<";

    public int Count { get; set; }
}
