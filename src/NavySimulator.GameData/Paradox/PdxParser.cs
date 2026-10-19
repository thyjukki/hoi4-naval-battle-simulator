namespace NavySimulator.GameData.Paradox;

/// <summary>
/// Parses Clausewitz script text into a <see cref="PdxBlock"/> tree. The file root is treated as an implicit block.
/// </summary>
public static class PdxParser
{
    /// <summary>
    /// Parses <paramref name="source"/>. With <paramref name="recovered"/>, blocks still open at the end of the file are
    /// closed there and the problem is added to it, as the game reads such files [PDX: TFB's submarine doctrines lack
    /// their last closing brace and work in the game]; without it, that is an error.
    /// </summary>
    public static PdxBlock Parse(string source, string? sourceName = null, List<string>? recovered = null)
    {
        var tokens = PdxTokenizer.Tokenize(source, sourceName);
        var position = 0;
        var root = ParseEntries(tokens, ref position, isRoot: true, sourceName, recovered);
        return root;
    }

    public static PdxBlock ParseFile(string path, List<string>? recovered = null)
    {
        return Parse(File.ReadAllText(path), path, recovered);
    }

    private static PdxBlock ParseEntries(List<PdxToken> tokens, ref int position, bool isRoot, string? sourceName, List<string>? recovered)
    {
        var entries = new List<PdxEntry>();

        while (true)
        {
            var token = tokens[position];

            if (token.Kind == PdxTokenKind.EndOfFile)
            {
                if (!isRoot)
                {
                    var message = $"line {token.Line}: missing closing brace; the open blocks are closed at the end of the file";

                    if (recovered is null)
                    {
                        throw new PdxParseException("Missing closing brace", token.Line, sourceName);
                    }

                    if (!recovered.Contains(message))
                    {
                        recovered.Add(message);
                    }
                }

                return new PdxBlock(entries);
            }

            if (token.Kind == PdxTokenKind.CloseBrace)
            {
                if (isRoot)
                {
                    throw new PdxParseException("Unexpected closing brace", token.Line, sourceName);
                }

                position++;
                return new PdxBlock(entries);
            }

            if (token.Kind == PdxTokenKind.Operator)
            {
                throw new PdxParseException($"Unexpected operator '{token.Text}'", token.Line, sourceName);
            }

            var next = tokens[position + 1];

            if (token.Kind != PdxTokenKind.OpenBrace && next.Kind == PdxTokenKind.Operator)
            {
                position += 2;
                var value = ParseValue(tokens, ref position, sourceName, recovered);
                entries.Add(new PdxEntry(token.Text, next.Text, value, token.Line));
                continue;
            }

            var bareValue = ParseValue(tokens, ref position, sourceName, recovered);
            entries.Add(new PdxEntry(null, null, bareValue, token.Line));
        }
    }

    private static PdxValue ParseValue(List<PdxToken> tokens, ref int position, string? sourceName, List<string>? recovered)
    {
        var token = tokens[position];

        switch (token.Kind)
        {
            case PdxTokenKind.OpenBrace:
                position++;
                return PdxValue.FromBlock(ParseEntries(tokens, ref position, isRoot: false, sourceName, recovered));
            case PdxTokenKind.Word:
                position++;
                return PdxValue.FromScalar(token.Text);
            case PdxTokenKind.QuotedString:
                position++;
                return PdxValue.FromScalar(token.Text, isQuoted: true);
            default:
                throw new PdxParseException($"Expected a value but found '{token.Text}'", token.Line, sourceName);
        }
    }
}
