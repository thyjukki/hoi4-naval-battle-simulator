namespace NavySimulator.GameData.Overrides;

public enum ContentFieldKind
{
    Number,
    Boolean,
    Text,

    /// <summary>A list of values (or of lists), edited as one JSON value.</summary>
    List,

    /// <summary>A dictionary: its keys are fields, and new keys can be added.</summary>
    Dictionary
}
