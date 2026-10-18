namespace NavySimulator.GameData.Paradox;

/// <summary>
/// One statement inside a block. Key and operator are null for bare values inside arrays (e.g. <c>{ 1 2 3 }</c>).
/// </summary>
public sealed record PdxEntry(string? Key, string? Operator, PdxValue Value, int Line);
