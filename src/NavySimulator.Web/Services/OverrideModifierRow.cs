namespace NavySimulator.Web.Services;

/// <summary>A static modifier key an override set changes: block, modifier, value before the set and the set's (null: removed).</summary>
public sealed record OverrideModifierRow(string Block, string Modifier, double? Base, double? Value, bool BlockUsed);
