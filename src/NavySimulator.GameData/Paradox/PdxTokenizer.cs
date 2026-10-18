using System.Text;

namespace NavySimulator.GameData.Paradox;

/// <summary>
/// Tokenizer for the Clausewitz script format used by HOI4 files under common/ (units, equipment, modules, technologies, MIOs).
/// </summary>
public static class PdxTokenizer
{
    public static List<PdxToken> Tokenize(string source, string? sourceName = null)
    {
        var tokens = new List<PdxToken>();
        var line = 1;
        var index = 0;

        while (index < source.Length)
        {
            var character = source[index];

            if (character == '\n')
            {
                line++;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(character) || character == '﻿')
            {
                index++;
                continue;
            }

            if (character == '#')
            {
                while (index < source.Length && source[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            switch (character)
            {
                case '{':
                    tokens.Add(new PdxToken(PdxTokenKind.OpenBrace, "{", line));
                    index++;
                    continue;
                case '}':
                    tokens.Add(new PdxToken(PdxTokenKind.CloseBrace, "}", line));
                    index++;
                    continue;
                case '"':
                    tokens.Add(ReadQuotedString(source, ref index, ref line, sourceName));
                    continue;
            }

            if (IsOperatorStart(character))
            {
                tokens.Add(ReadOperator(source, ref index, line));
                continue;
            }

            tokens.Add(ReadWord(source, ref index, line));
        }

        tokens.Add(new PdxToken(PdxTokenKind.EndOfFile, string.Empty, line));
        return tokens;
    }

    private static bool IsOperatorStart(char character)
    {
        return character is '=' or '<' or '>' or '!' or '?';
    }

    private static PdxToken ReadOperator(string source, ref int index, int line)
    {
        var start = index;
        index++;

        if (index < source.Length && source[index] == '=')
        {
            index++;
        }

        return new PdxToken(PdxTokenKind.Operator, source[start..index], line);
    }

    private static PdxToken ReadQuotedString(string source, ref int index, ref int line, string? sourceName)
    {
        var startLine = line;
        var builder = new StringBuilder();
        index++;

        while (index < source.Length)
        {
            var character = source[index];

            if (character == '\\' && index + 1 < source.Length)
            {
                builder.Append(source[index + 1]);
                index += 2;
                continue;
            }

            if (character == '"')
            {
                index++;
                return new PdxToken(PdxTokenKind.QuotedString, builder.ToString(), startLine);
            }

            if (character == '\n')
            {
                line++;
            }

            builder.Append(character);
            index++;
        }

        throw new PdxParseException("Unterminated quoted string", startLine, sourceName);
    }

    private static PdxToken ReadWord(string source, ref int index, int line)
    {
        var start = index;

        while (index < source.Length)
        {
            var character = source[index];

            if (char.IsWhiteSpace(character) || character is '{' or '}' or '"' or '#' || IsOperatorStart(character))
            {
                break;
            }

            index++;
        }

        return new PdxToken(PdxTokenKind.Word, source[start..index], line);
    }
}
