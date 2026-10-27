using System.Text.Json.Nodes;

namespace NavySimulator.GameData.Overrides;

/// <summary>A field of an item that an override can change: its path, kind and value.</summary>
public sealed record ContentField(string Path, ContentFieldKind Kind, JsonNode? Value);
