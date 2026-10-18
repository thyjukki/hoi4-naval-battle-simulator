namespace NavySimulator.GameData.Paradox;

public readonly record struct PdxToken(PdxTokenKind Kind, string Text, int Line);
