namespace NavySimulator.GameData.Contracts;

/// <summary>One parent requirement of an MIO trait: at least <see cref="Needed"/> of <see cref="Traits"/> unlocked (S-MIO-TREE).</summary>
public class MioTraitParentDto
{
    public List<string> Traits { get; set; } = [];

    /// <summary><c>num_parents_needed</c>; 1 for <c>any_parent</c>, all of them for <c>all_parents</c>.</summary>
    public int Needed { get; set; } = 1;
}
