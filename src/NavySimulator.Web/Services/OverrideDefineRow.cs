using System.Text.Json.Nodes;

namespace NavySimulator.Web.Services;

/// <summary>A define an override set changes: its key, the value before the set, and the set's value.</summary>
public sealed record OverrideDefineRow(string Key, JsonNode? Base, JsonNode? Value, bool Known);
