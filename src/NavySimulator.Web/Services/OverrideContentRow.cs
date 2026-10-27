using System.Text.Json.Nodes;
using NavySimulator.GameData.Contracts;

namespace NavySimulator.Web.Services;

/// <summary>A content change of an override set, with the item it changes and the value in the data directory.</summary>
public sealed record OverrideContentRow(ContentOverrideDto Change, string CollectionLabel, string? ItemName, bool ItemFound, JsonNode? Base);
