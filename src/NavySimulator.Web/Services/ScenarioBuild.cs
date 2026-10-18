using NavySimulator.Engine.Battles;
using NavySimulator.GameData.Defines;

namespace NavySimulator.Web.Services;

/// <summary>The outcome of validating and building the edited scenario; <see cref="Scenario"/> is null when it has errors.</summary>
public sealed record ScenarioBuild(
    BattleScenario? Scenario,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    DefinesBindingReport? DefinesReport);
