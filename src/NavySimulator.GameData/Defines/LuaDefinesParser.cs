using System.Globalization;
using System.Text;

namespace NavySimulator.GameData.Defines;

/// <summary>
/// Minimal Lua reader for HOI4 define files. Supports the vanilla layout
/// (<c>NDefines = { NNavy = { KEY = value, ... } }</c>) and mod override statements
/// (<c>NDefines.NNavy.KEY = value</c>). Only the subset of Lua used by define files is handled.
/// </summary>
public static class LuaDefinesParser
{
    private const string RootPrefix = "NDefines";

    public static DefinesTable ParseFile(string path)
    {
        return Parse(File.ReadAllText(path), path);
    }

    public static DefinesTable Parse(string source, string? sourceName = null)
    {
        var reader = new Reader(source, sourceName);
        var table = new DefinesTable();

        while (true)
        {
            reader.SkipTrivia();

            if (reader.AtEnd)
            {
                return table;
            }

            var path = reader.ReadDottedName();
            reader.Expect('=');
            var value = reader.ReadValue();

            if (path.Count == 0 || !path[0].StartsWith(RootPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            ApplyStatement(table, path, value);
        }
    }

    private static void ApplyStatement(DefinesTable table, List<string> path, DefineValue value)
    {
        switch (path.Count)
        {
            case 1 when value.Fields is not null:
                foreach (var category in value.Fields)
                {
                    ApplyCategory(table, category.Key, category.Value);
                }

                break;
            case 2:
                ApplyCategory(table, path[1], value);
                break;
            case >= 3:
                table.Set(path[1], string.Join('.', path.Skip(2)), value);
                break;
        }
    }

    private static void ApplyCategory(DefinesTable table, string category, DefineValue value)
    {
        if (value.Fields is null)
        {
            return;
        }

        foreach (var entry in value.Fields)
        {
            table.Set(category, entry.Key, entry.Value);
        }
    }

    private sealed class Reader(string source, string? sourceName)
    {
        private int index;
        private int line = 1;

        public bool AtEnd => index >= source.Length;

        public void SkipTrivia()
        {
            while (index < source.Length)
            {
                var character = source[index];

                if (character == '\n')
                {
                    line++;
                    index++;
                }
                else if (char.IsWhiteSpace(character) || character == '﻿' || character == ';')
                {
                    index++;
                }
                else if (character == '-' && Peek(1) == '-')
                {
                    SkipComment();
                }
                else
                {
                    return;
                }
            }
        }

        public List<string> ReadDottedName()
        {
            var parts = new List<string> { ReadIdentifier() };

            while (true)
            {
                SkipTrivia();

                if (Peek(0) != '.')
                {
                    return parts;
                }

                index++;
                parts.Add(ReadIdentifier());
            }
        }

        public void Expect(char expected)
        {
            SkipTrivia();

            if (Peek(0) != expected)
            {
                throw Error($"Expected '{expected}' but found '{(AtEnd ? "<eof>" : Peek(0).ToString())}'");
            }

            index++;
        }

        public DefineValue ReadValue()
        {
            SkipTrivia();
            var character = Peek(0);

            if (character == '{')
            {
                return ReadTable();
            }

            if (character is '"' or '\'')
            {
                return new DefineValue(Text: ReadString());
            }

            if (char.IsDigit(character) || character is '-' or '+' or '.' or '(')
            {
                return new DefineValue(Number: ReadExpression());
            }

            var word = ReadIdentifier();
            return word switch
            {
                "true" => new DefineValue(Boolean: true),
                "false" => new DefineValue(Boolean: false),
                "nil" => new DefineValue(),
                _ => new DefineValue(Text: word)
            };
        }

        private DefineValue ReadTable()
        {
            Expect('{');
            var items = new List<DefineValue>();
            var fields = new Dictionary<string, DefineValue>(StringComparer.Ordinal);

            while (true)
            {
                SkipTrivia();

                if (Peek(0) == '}')
                {
                    index++;
                    break;
                }

                if (IsIdentifierStart(Peek(0)) && NextNonTriviaAfterIdentifierIsAssignment())
                {
                    var key = ReadIdentifier();
                    Expect('=');
                    fields[key] = ReadValue();
                }
                else if (Peek(0) == '[')
                {
                    index++;
                    var keyValue = ReadValue();
                    Expect(']');
                    Expect('=');
                    fields[keyValue.Text ?? keyValue.ToString()] = ReadValue();
                }
                else
                {
                    items.Add(ReadValue());
                }

                SkipTrivia();

                if (Peek(0) is ',' or ';')
                {
                    index++;
                }
            }

            return new DefineValue(
                Items: items.Count > 0 || fields.Count == 0 ? items : null,
                Fields: fields.Count > 0 ? fields : null);
        }

        private bool NextNonTriviaAfterIdentifierIsAssignment()
        {
            var savedIndex = index;
            var savedLine = line;
            ReadIdentifier();
            SkipTrivia();
            var isAssignment = Peek(0) == '=' && Peek(1) != '=';
            index = savedIndex;
            line = savedLine;
            return isAssignment;
        }

        private string ReadIdentifier()
        {
            SkipTrivia();
            var start = index;

            while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_'))
            {
                index++;
            }

            if (start == index)
            {
                throw Error($"Expected identifier but found '{(AtEnd ? "<eof>" : Peek(0).ToString())}'");
            }

            return source[start..index];
        }

        private string ReadString()
        {
            var quote = source[index];
            index++;
            var builder = new StringBuilder();

            while (index < source.Length && source[index] != quote)
            {
                if (source[index] == '\\' && index + 1 < source.Length)
                {
                    index++;
                }

                builder.Append(source[index]);
                index++;
            }

            if (AtEnd)
            {
                throw Error("Unterminated string");
            }

            index++;
            return builder.ToString();
        }

        // Define files occasionally use arithmetic such as "7 * 24", so numbers are read as + - * / expressions.
        private double ReadExpression()
        {
            var value = ReadTerm();

            while (true)
            {
                SkipTrivia();
                var character = Peek(0);

                if (character == '+')
                {
                    index++;
                    value += ReadTerm();
                }
                else if (character == '-' && Peek(1) != '-')
                {
                    index++;
                    value -= ReadTerm();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ReadTerm()
        {
            var value = ReadFactor();

            while (true)
            {
                SkipTrivia();
                var character = Peek(0);

                if (character == '*')
                {
                    index++;
                    value *= ReadFactor();
                }
                else if (character == '/')
                {
                    index++;
                    value /= ReadFactor();
                }
                else
                {
                    return value;
                }
            }
        }

        private double ReadFactor()
        {
            SkipTrivia();
            var character = Peek(0);

            switch (character)
            {
                case '-':
                    index++;
                    return -ReadFactor();
                case '+':
                    index++;
                    return ReadFactor();
                case '(':
                {
                    index++;
                    var value = ReadExpression();
                    Expect(')');
                    return value;
                }
                default:
                    return ReadNumberLiteral();
            }
        }

        private double ReadNumberLiteral()
        {
            var start = index;

            while (index < source.Length &&
                   (char.IsLetterOrDigit(source[index]) || source[index] == '.' ||
                    (source[index] is '-' or '+' && index > start && source[index - 1] is 'e' or 'E' && !IsHexLiteral(start))))
            {
                index++;
            }

            var text = source[start..index];

            if (IsHexLiteral(start))
            {
                return long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                throw Error($"Invalid number '{text}'");
            }

            return value;
        }

        private bool IsHexLiteral(int start)
        {
            return start + 1 < source.Length && source[start] == '0' && source[start + 1] is 'x' or 'X';
        }

        private void SkipComment()
        {
            index += 2;

            if (Peek(0) == '[')
            {
                var levelStart = index + 1;
                var level = 0;

                while (Peek(1 + level) == '=')
                {
                    level++;
                }

                if (Peek(1 + level) == '[')
                {
                    var terminator = "]" + new string('=', level) + "]";
                    var end = source.IndexOf(terminator, levelStart, StringComparison.Ordinal);
                    var stop = end < 0 ? source.Length : end + terminator.Length;
                    line += source.AsSpan(index, stop - index).Count('\n');
                    index = stop;
                    return;
                }
            }

            while (index < source.Length && source[index] != '\n')
            {
                index++;
            }
        }

        private static bool IsIdentifierStart(char character)
        {
            return char.IsLetter(character) || character == '_';
        }

        private char Peek(int offset)
        {
            var position = index + offset;
            return position < source.Length ? source[position] : '\0';
        }

        private FormatException Error(string message)
        {
            return new FormatException($"{sourceName ?? "<defines>"}:{line}: {message}");
        }
    }
}
