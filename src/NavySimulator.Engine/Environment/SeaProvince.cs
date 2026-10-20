namespace NavySimulator.Engine.Environment;

/// <summary>S-NIGHT: a sea province and its map position (whole pixels, y from the bottom row), which its darkness follows.</summary>
public sealed record SeaProvince(int ID, double X, double Y);
