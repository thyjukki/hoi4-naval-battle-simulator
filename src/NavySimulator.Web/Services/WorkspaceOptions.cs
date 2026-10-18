namespace NavySimulator.Web.Services;

/// <summary>Start-up inputs of the <see cref="Workspace"/>: the data directory and the command line's <c>--defines</c>.</summary>
public sealed record WorkspaceOptions(string DataDirectory, IReadOnlyList<string> CommandLineDefineSources);
