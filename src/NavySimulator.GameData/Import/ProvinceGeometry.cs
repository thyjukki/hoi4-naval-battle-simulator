using System.Globalization;

namespace NavySimulator.GameData.Import;

/// <summary>
/// S-NIGHT, A-COVERAGE [RE] (0x140a3c0, 0x140a610): each province's map position and box from <c>map/provinces.bmp</c> and
/// <c>map/definition.csv</c>: the position is the truncated mean of its pixels, the box its pixels' bounds snapped outward
/// to even pixels (x0 = min &amp; ~1, x1 = max | 1). Coordinates are pixels, x from the left, y from the bottom row.
/// </summary>
public sealed class ProvinceGeometry
{
    private ProvinceGeometry(int width, int height, Dictionary<int, Shape> shapes)
    {
        Width = width;
        Height = height;
        Shapes = shapes;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>The provinces found in the bitmap, by ID.</summary>
    public IReadOnlyDictionary<int, Shape> Shapes { get; }

    /// <summary>One province: its pixel count, position (truncated mean pixel) and snapped box (x, y, width, height).</summary>
    public readonly record struct Shape(int Pixels, int X, int Y, int BoxX, int BoxY, int BoxWidth, int BoxHeight);

    /// <summary>
    /// The union of the provinces' boxes (state: 0xabce20, region: 0x15a43c0) and its centre box.x + w / 2, box.y + h / 2
    /// (integer). The game's seam case (a state or region across the map's left and right edge) is not handled.
    /// </summary>
    public (int X, int Y, int BoxX, int BoxY, int BoxWidth, int BoxHeight)? Union(IEnumerable<int> provinces)
    {
        var boxes = provinces.Where(Shapes.ContainsKey).Select(id => Shapes[id]).ToList();

        if (boxes.Count == 0)
        {
            return null;
        }

        var x0 = boxes.Min(box => box.BoxX);
        var y0 = boxes.Min(box => box.BoxY);
        var x1 = boxes.Max(box => box.BoxX + box.BoxWidth);
        var y1 = boxes.Max(box => box.BoxY + box.BoxHeight);
        return (x0 + (x1 - x0) / 2, y0 + (y1 - y0) / 2, x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Reads the bitmap (24-bit) and the colour table; null with a warning when either is missing.</summary>
    public static ProvinceGeometry? Read(GameFileSystem files, List<string> warnings)
    {
        var bitmapPath = files.GetFiles("map", "provinces.bmp").FirstOrDefault();
        var definitionPath = files.GetFiles("map", "definition.csv").FirstOrDefault();

        if (bitmapPath is null || definitionPath is null)
        {
            warnings.Add("map/provinces.bmp or map/definition.csv not found: no province geometry.");
            return null;
        }

        var colours = new Dictionary<int, int>();

        foreach (var line in File.ReadLines(definitionPath, System.Text.Encoding.Latin1))
        {
            var fields = line.Split(';');

            if (fields.Length > 3 &&
                int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
                int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) &&
                int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) &&
                int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
            {
                colours.TryAdd((r << 16) | (g << 8) | b, id);
            }
        }

        var bytes = File.ReadAllBytes(bitmapPath);

        if (bytes.Length < 54 || bytes[0] != 'B' || bytes[1] != 'M' || BitConverter.ToInt16(bytes, 28) != 24)
        {
            warnings.Add("map/provinces.bmp is not a 24-bit bitmap: no province geometry.");
            return null;
        }

        var offset = BitConverter.ToInt32(bytes, 10);
        var width = BitConverter.ToInt32(bytes, 18);
        var rawHeight = BitConverter.ToInt32(bytes, 22);
        var height = Math.Abs(rawHeight);
        var stride = (width * 3 + 3) & ~3;

        if (width <= 0 || (long)offset + (long)stride * height > bytes.Length)
        {
            warnings.Add("map/provinces.bmp is shorter than its header says: no province geometry.");
            return null;
        }
        var size = colours.Count == 0 ? 0 : colours.Values.Max() + 1;
        var count = new long[size];
        var sumX = new long[size];
        var sumY = new long[size];
        var minX = new int[size];
        var maxX = new int[size];
        var minY = new int[size];
        var maxY = new int[size];
        var lastColour = -1;
        var lastId = -1;

        for (var row = 0; row < height; row++)
        {
            // Bottom-up rows (positive height) are already counted from the bottom.
            var y = rawHeight > 0 ? row : height - 1 - row;
            var start = offset + row * stride;

            for (var x = 0; x < width; x++)
            {
                var i = start + x * 3;
                var colour = (bytes[i + 2] << 16) | (bytes[i + 1] << 8) | bytes[i];

                if (colour != lastColour)
                {
                    lastColour = colour;
                    lastId = colours.TryGetValue(colour, out var found) ? found : -1;
                }

                if (lastId < 0)
                {
                    continue;
                }

                if (count[lastId]++ == 0)
                {
                    minX[lastId] = maxX[lastId] = x;
                    minY[lastId] = maxY[lastId] = y;
                }
                else
                {
                    minX[lastId] = Math.Min(minX[lastId], x);
                    maxX[lastId] = Math.Max(maxX[lastId], x);
                    minY[lastId] = Math.Min(minY[lastId], y);
                    maxY[lastId] = Math.Max(maxY[lastId], y);
                }

                sumX[lastId] += x;
                sumY[lastId] += y;
            }
        }

        var shapes = new Dictionary<int, Shape>();

        for (var id = 0; id < size; id++)
        {
            if (count[id] == 0)
            {
                continue;
            }

            var x0 = minX[id] & ~1;
            var y0 = minY[id] & ~1;
            var x1 = maxX[id] | 1;
            var y1 = maxY[id] | 1;
            shapes[id] = new Shape((int)count[id], (int)(sumX[id] / count[id]), (int)(sumY[id] / count[id]), x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        return new ProvinceGeometry(width, height, shapes);
    }
}
