namespace NavySimulator.GameData.Overrides;

/// <summary>One step of a <see cref="ContentPath"/>: a JSON property, and with <see cref="Id"/> the list entry of that ID.</summary>
public sealed record ContentPathSegment(string Name, string? Id = null)
{
    public override string ToString()
    {
        return Id is null ? Name : $"{Name}[{Id}]";
    }
}
