namespace NavySimulator.Engine;

/// <summary>
/// S-DESIGN-LIMIT [PDX]: a hull's <c>module_count_limit</c>. A slot counts when its module (or its emptiness) matches
/// any of the conditions: a module ID, a category, <c>any</c> module, or an <c>empty</c> slot.
/// <see cref="Operator"/> is <c>&lt;</c>, <c>&gt;</c>, <c>=</c> or <c>any</c> (no limit, used to cancel an inherited one).
/// </summary>
public sealed record ModuleCountLimit(IReadOnlyList<string> Modules, IReadOnlyList<string> Categories, string Operator, int Count)
{
    public const string AnyKeyword = "any";
    public const string EmptyKeyword = "empty";

    public bool Matches(StatModule? module)
    {
        if (module is null)
        {
            return Modules.Contains(EmptyKeyword);
        }

        return Modules.Contains(AnyKeyword) || Categories.Contains(AnyKeyword) ||
               Modules.Contains(module.ID) || Categories.Contains(module.Category);
    }

    public bool Allows(int count)
    {
        return Operator switch
        {
            "<" => count < Count,
            ">" => count > Count,
            "=" => count == Count,
            _ => true
        };
    }

    /// <summary>The match conditions, e.g. <c>category ship_radar</c>; an inherited limit is replaced by one with the same conditions.</summary>
    public string Conditions => string.Join(", ", Modules.Select(module => $"module {module}").Concat(Categories.Select(category => $"category {category}")));

    public override string ToString()
    {
        return Operator == AnyKeyword ? $"{Conditions}: any count" : $"{Conditions}: count {Operator} {Count}";
    }
}
