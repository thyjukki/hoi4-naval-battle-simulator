using System.Globalization;

namespace NavySimulator.GameData.Paradox;

/// <summary>
/// A script value: either a scalar (word or quoted string) or a nested block.
/// </summary>
public sealed class PdxValue
{
    private PdxValue(string? scalar, bool isQuoted, PdxBlock? block)
    {
        Scalar = scalar;
        IsQuoted = isQuoted;
        Block = block;
    }

    public string? Scalar { get; }
    public bool IsQuoted { get; }
    public PdxBlock? Block { get; }
    public bool IsBlock => Block is not null;

    public static PdxValue FromScalar(string text, bool isQuoted = false)
    {
        return new PdxValue(text, isQuoted, null);
    }

    public static PdxValue FromBlock(PdxBlock block)
    {
        return new PdxValue(null, false, block);
    }

    public bool TryGetDouble(out double value)
    {
        value = 0;
        return Scalar is not null &&
               double.TryParse(Scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public bool TryGetBool(out bool value)
    {
        value = Scalar is "yes";
        return Scalar is "yes" or "no";
    }

    public override string ToString()
    {
        return IsBlock ? "{...}" : IsQuoted ? $"\"{Scalar}\"" : Scalar ?? string.Empty;
    }
}
