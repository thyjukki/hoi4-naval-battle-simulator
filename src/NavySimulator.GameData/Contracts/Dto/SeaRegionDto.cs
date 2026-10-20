namespace NavySimulator.GameData.Contracts;

/// <summary>
/// S-REGION: a strategic region with sea provinces (<c>map/strategicregions</c>), written by <c>NavySimulator
/// import-regions</c> to <c>sea-regions/</c>.
/// </summary>
public class SeaRegionDto
{
    /// <summary>The region's <c>id</c>; also its air phase (A-CADENCE) and weather index.</summary>
    public int ID { get; set; }

    /// <summary>The game's name (localisation of <c>STRATEGICREGION_&lt;id&gt;</c>), e.g. "Black Sea".</summary>
    public string? Name { get; set; }

    /// <summary>The region's <c>naval_terrain</c> (S-TERRAIN), e.g. <c>water_deep_ocean</c>; null when the script sets none.</summary>
    public string? NavalTerrain { get; set; }

    /// <summary>S-NIGHT: the size of the map the province positions are on (<c>map/provinces.bmp</c>), in pixels.</summary>
    public int MapWidth { get; set; }

    public int MapHeight { get; set; }

    /// <summary>
    /// S-WEATHER-SIM: the game's region weather states, the highest strategic region id (land regions included) + 1; with
    /// <c>regions_per_update</c> it sets how often the region's weather can change.
    /// </summary>
    public int WeatherStates { get; set; }

    /// <summary>The region's sea provinces (<c>map/definition.csv</c> type <c>sea</c>).</summary>
    public List<SeaProvinceDto> Provinces { get; set; } = [];

    /// <summary>
    /// A-COVERAGE [RE] 0x15a43c0: the union of the region's provinces' boxes (even-snapped), pixels from the left and the
    /// bottom; land wings' coverage prechecks against its centre and half diagonal. 0 width: not imported.
    /// </summary>
    public int BoxX { get; set; }

    public int BoxY { get; set; }
    public int BoxWidth { get; set; }
    public int BoxHeight { get; set; }

    /// <summary>The weather periods of the region's <c>weather</c> block, in script order.</summary>
    public List<WeatherPeriodDto> Weather { get; set; } = [];
}
