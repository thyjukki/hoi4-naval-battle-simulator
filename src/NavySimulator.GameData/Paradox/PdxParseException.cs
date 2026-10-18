namespace NavySimulator.GameData.Paradox;

public class PdxParseException : Exception
{
    public PdxParseException(string message, int line, string? sourceName = null)
        : base($"{sourceName ?? "<input>"}:{line}: {message}")
    {
        Line = line;
        SourceName = sourceName;
    }

    public int Line { get; }
    public string? SourceName { get; }
}
