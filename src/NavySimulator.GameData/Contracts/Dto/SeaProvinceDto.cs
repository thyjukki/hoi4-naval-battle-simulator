namespace NavySimulator.GameData.Contracts;

/// <summary>S-REGION: a sea province of a <see cref="SeaRegionDto"/>, with the map position its darkness is computed from (S-NIGHT).</summary>
public class SeaProvinceDto
{
    public int ID { get; set; }

    /// <summary>The province's map x (pixels from the left edge of <c>provinces.bmp</c>; the truncated mean pixel, 0x140a3c0).</summary>
    public double X { get; set; }

    /// <summary>The province's map y (pixels from the bottom edge, as the game's map coordinates run).</summary>
    public double Y { get; set; }
}
