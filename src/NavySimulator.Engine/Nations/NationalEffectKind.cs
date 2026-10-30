namespace NavySimulator.Engine.Nations;

/// <summary>S-NATIONAL-EFFECT: the effect statements of a history file or focus reward the simulator follows, by game effect.</summary>
public enum NationalEffectKind
{
    /// <summary><c>add_ideas</c> (also <c>add_timed_idea</c> and the added half of <c>swap_ideas</c>).</summary>
    AddIdea,

    /// <summary><c>remove_ideas</c> (also the removed half of <c>swap_ideas</c>).</summary>
    RemoveIdea,
    AddDynamicModifier,
    RemoveDynamicModifier,
    SetVariable,
    AddToVariable,
    SubtractFromVariable,
    MultiplyVariable
}
