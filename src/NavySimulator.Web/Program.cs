using System.Diagnostics;
using System.Globalization;
using NavySimulator.Web;
using NavySimulator.Web.Components;
using NavySimulator.Web.Services;

WebOptions options;

try
{
    options = WebOptions.Parse(args);
}
catch (Exception exception) when (exception is ArgumentException or FormatException)
{
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine(WebOptions.Usage);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(WebOptions.Usage);
    return 0;
}

var dataDirectory = options.DataDirectory ?? DataDirectoryLocator.Find();

if (dataDirectory is null || !DataDirectoryLocator.IsDataDirectory(dataDirectory))
{
    Console.Error.WriteLine($"No data directory found{(dataDirectory is null ? string.Empty : $" at {dataDirectory}")}. Pass --data <dir>.");
    return 1;
}

// Numbers in the GUI use '.' decimals, like the game and the number inputs, and "12.5%" percentages.
var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
culture.NumberFormat.PercentPositivePattern = 1;
culture.NumberFormat.PercentNegativePattern = 1;
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseStaticWebAssets();
builder.WebHost.UseUrls($"http://localhost:{options.Port}");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddRazorComponents().AddInteractiveServerComponents(circuit => circuit.DetailedErrors = true);
builder.Services.AddSingleton(new WorkspaceOptions(dataDirectory, options.DefineSources));
builder.Services.AddSingleton(new GuiSettingsStore(options.SettingsFile));
builder.Services.AddSingleton<Workspace>();
builder.Services.AddSingleton<BattleRunner>();
builder.Services.AddSingleton<BatchRunner>();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
ExportEndpoints.Map(app);

var url = $"http://localhost:{options.Port}";
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"NavySimulator GUI running at {url} (data: {dataDirectory}). Press Ctrl+C to stop.");

    if (!options.OpenBrowser)
    {
        return;
    }

    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
    {
        Console.WriteLine($"Open {url} in your browser.");
    }
});

app.Run();
return 0;
