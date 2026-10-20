using System.Globalization;

namespace NavySimulator.GameData.Import;

/// <summary>
/// S-NIGHT [PDX]: province map positions from <c>map/unitstacks.txt</c> (<c>province;type;x;height;z;rotation;offset</c>),
/// the type 0 (standing unit) entry, x from the left and z from the bottom of the map.
/// </summary>
public static class ProvincePositions
{
    public static IReadOnlyDictionary<int, (double X, double Y)> Read(GameFileSystem files, List<string> warnings)
    {
        var positions = new Dictionary<int, (double X, double Y)>();
        var path = files.GetFiles("map", "unitstacks.txt").FirstOrDefault();

        if (path is null)
        {
            warnings.Add("map/unitstacks.txt not found: no province positions.");
            return positions;
        }

        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split(';');

            if (fields.Length < 5 || fields[1].Trim() != "0" ||
                !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                !double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                continue;
            }

            positions[id] = (x, z);
        }

        return positions;
    }

    /// <summary>The width and height of <c>map/provinces.bmp</c> from its header (vanilla and TFB: 5632 × 2048).</summary>
    public static (int Width, int Height) MapSize(GameFileSystem files, List<string> warnings)
    {
        var path = files.GetFiles("map", "provinces.bmp").FirstOrDefault();

        if (path is null)
        {
            warnings.Add("map/provinces.bmp not found: the map size is unknown, so darkness cannot be computed.");
            return (0, 0);
        }

        using var stream = File.OpenRead(path);
        var header = new byte[26];

        if (stream.Read(header, 0, header.Length) < header.Length || header[0] != 'B' || header[1] != 'M')
        {
            warnings.Add("map/provinces.bmp is not a bitmap: the map size is unknown.");
            return (0, 0);
        }

        return (BitConverter.ToInt32(header, 18), Math.Abs(BitConverter.ToInt32(header, 22)));
    }
}
