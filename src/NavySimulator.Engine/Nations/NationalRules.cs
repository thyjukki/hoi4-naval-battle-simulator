using NavySimulator.Engine.Doctrines;

namespace NavySimulator.Engine.Nations;

/// <summary>
/// S-NATIONAL: turns a side's national setup into its active national spirits, dynamic modifiers and variables, and those
/// into country effects. Spec: docs/mechanics/surface.md.
/// </summary>
public static class NationalRules
{
    /// <summary>
    /// S-NATIONAL [PDX + ASSUMED]: the history's effects (when the setup starts from history), then each completed focus's
    /// in the tree's order, run as the game runs the statements; then the spirits added and removed by hand and the
    /// variables set by hand. Adding an active idea or dynamic modifier again changes nothing; an unset variable is 0.
    /// </summary>
    public static NationalState Resolve(NationalSetup? setup)
    {
        if (setup is null)
        {
            return NationalState.Empty;
        }

        var spirits = new List<string>();
        var modifiers = new List<string>();
        var variables = new Dictionary<string, double>(StringComparer.Ordinal);
        var effects = (setup.FromHistory ? setup.Nation.History : [])
            .Concat(setup.Nation.Focuses.Where(focus => setup.Focuses.Any(done => done.ID == focus.ID)).SelectMany(focus => focus.Effects));

        foreach (var effect in effects)
        {
            Apply(effect, spirits, modifiers, variables);
        }

        foreach (var id in setup.AddedSpirits)
        {
            Apply(new NationalEffect(NationalEffectKind.AddIdea, id), spirits, modifiers, variables);
        }

        spirits.RemoveAll(setup.RemovedSpirits.Contains);

        foreach (var (name, value) in setup.Variables)
        {
            variables[name] = value;
        }

        return new NationalState(spirits, modifiers, variables);
    }

    /// <summary>
    /// S-NATIONAL [PDX]: the effects of the active national spirits and dynamic modifiers, each with its ID as source, as
    /// <see cref="BattleParticipant.ActiveEffects"/> lists them. IDs the nation does not have are left out.
    /// </summary>
    public static IReadOnlyList<(string Source, CountryEffects Effects)> ActiveEffects(NationalSetup? setup)
    {
        if (setup is null)
        {
            return [];
        }

        var state = Resolve(setup);
        var nation = setup.Nation;
        return state.Spirits.Where(nation.Spirits.ContainsKey).Select(id => (id, nation.Spirits[id].Effects))
            .Concat(state.DynamicModifiers.Where(nation.DynamicModifiers.ContainsKey).Select(id => (id, nation.DynamicModifiers[id].Effects(state.Variables))))
            .ToList();
    }

    /// <summary>S-NATIONAL-FOCUS [PDX]: pairs of completed focuses that exclude each other (each pair once).</summary>
    public static IReadOnlyList<(string First, string Second)> ExclusiveFocusPairs(IEnumerable<NationalFocus> focuses)
    {
        var done = focuses.ToList();
        return done.SelectMany(focus => focus.MutuallyExclusive
                .Where(id => id != focus.ID && done.Any(other => other.ID == id))
                .Select(id => string.CompareOrdinal(focus.ID, id) < 0 ? (First: focus.ID, Second: id) : (First: id, Second: focus.ID)))
            .Distinct()
            .ToList();
    }

    private static void Apply(NationalEffect effect, List<string> spirits, List<string> modifiers, Dictionary<string, double> variables)
    {
        switch (effect.Kind)
        {
            case NationalEffectKind.AddIdea when !spirits.Contains(effect.Target):
                spirits.Add(effect.Target);
                break;
            case NationalEffectKind.RemoveIdea:
                spirits.Remove(effect.Target);
                break;
            case NationalEffectKind.AddDynamicModifier when !modifiers.Contains(effect.Target):
                modifiers.Add(effect.Target);
                break;
            case NationalEffectKind.RemoveDynamicModifier:
                modifiers.Remove(effect.Target);
                break;
            case NationalEffectKind.SetVariable:
                variables[effect.Target] = effect.Value;
                break;
            case NationalEffectKind.AddToVariable:
                variables[effect.Target] = variables.GetValueOrDefault(effect.Target) + effect.Value;
                break;
            case NationalEffectKind.SubtractFromVariable:
                variables[effect.Target] = variables.GetValueOrDefault(effect.Target) - effect.Value;
                break;
            case NationalEffectKind.MultiplyVariable:
                variables[effect.Target] = variables.GetValueOrDefault(effect.Target) * effect.Value;
                break;
        }
    }
}
