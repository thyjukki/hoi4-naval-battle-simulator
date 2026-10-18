namespace NavySimulator.GameData.Paradox;

/// <summary>
/// Parses Clausewitz script text into a <see cref="PdxBlock"/> tree. The file root is treated as an implicit block.
/// </summary>
public static class PdxParser
{
    public static PdxBlock Parse(string source, string? sourceName = null)
    {
        var tokens = PdxTokenizer.Tokenize(source, sourceName);
        var position = 0;
        var root = ParseEntries(tokens, ref position, isRoot: true, sourceName);
        return root;
    }

    public static PdxBlock ParseFile(string path)
    {
        return Parse(File.ReadAllText(path), path);
    }

    private static PdxBlock ParseEntries(List<PdxToken> tokens, ref int position, bool isRoot, string? sourceName)
    {
        var entries = new List<PdxEntry>();

        while (true)
        {
            var token = tokens[position];

            if (token.Kind == PdxTokenKind.EndOfFile)
            {
                if (!isRoot)
                {
                    throw new PdxParseException("Missing closing brace", token.Line, sourceName);
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
                var value = ParseValue(tokens, ref position, sourceName);
                entries.Add(new PdxEntry(token.Text, next.Text, value, token.Line));
                continue;
            }

            var bareValue = ParseValue(tokens, ref position, sourceName);
            entries.Add(new PdxEntry(null, null, bareValue, token.Line));
        }
    }

    private static PdxValue ParseValue(List<PdxToken> tokens, ref int position, string? sourceName)
    {
        var token = tokens[position];

        switch (token.Kind)
        {
            case PdxTokenKind.OpenBrace:
                position++;
                return PdxValue.FromBlock(ParseEntries(tokens, ref position, isRoot: false, sourceName));
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
