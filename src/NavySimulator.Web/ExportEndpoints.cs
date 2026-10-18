using System.Text;
using NavySimulator.GameData;
using NavySimulator.Web.Services;

namespace NavySimulator.Web;

/// <summary>CSV downloads of the current battle and the last batch, in the CLI's report formats.</summary>
public static class ExportEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/export/battle/{file}", (string file, BattleRunner runner) =>
        {
            var result = runner.Result();

            if (result is null)
            {
                return Results.NotFound("No battle has been started.");
            }

            var content = file switch
            {
                "shots.csv" => ReportWriter.BuildShotsCsv(result),
                "ships.csv" => ReportWriter.BuildShipsCsv(result),
                "air.csv" => ReportWriter.BuildAirCsv(result),
                "hourly.log" => string.Join(Environment.NewLine, result.HourlyLog),
                "summary.txt" => string.Join(Environment.NewLine, ReportWriter.BuildRunSummary(result)),
                _ => null
            };

            return content is null
                ? Results.NotFound()
                : Results.File(Encoding.UTF8.GetBytes(content), file.EndsWith(".csv") ? "text/csv" : "text/plain", $"{result.ScenarioId}_seed{result.Seed}_{file}");
        });

        app.MapGet("/export/batch/runs.csv", (BatchRunner batch) =>
        {
            var last = batch.Last;
            return last is null
                ? Results.NotFound("No batch has been run.")
                : Results.File(Encoding.UTF8.GetBytes(BatchRunner.RunsCsv(last.Runs)), "text/csv", $"{last.Scenario.ID}_batch_runs.csv");
        });
    }
}
