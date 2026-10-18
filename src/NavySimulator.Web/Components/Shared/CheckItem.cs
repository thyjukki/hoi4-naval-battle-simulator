namespace NavySimulator.Web.Components.Shared;

/// <summary>One option of a <see cref="CheckList"/>.</summary>
public sealed record CheckItem(string Id, string Description = "", string Group = "");
