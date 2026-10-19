using NavySimulator.Engine.Stats;

namespace NavySimulator.Engine.Mios;

/// <summary>
/// S-MIO: what an MIO gives a design, and the trait tree's rules. Spec: docs/mechanics/surface.md.
/// </summary>
public static class MioRules
{
    /// <summary>
    /// S-MIO-MATCH [RE] 0xa0a600: a token list matches a design when a token is exactly its hull, the hull's archetype,
    /// one of the hull's equipment type keywords or an equipment group holding one of them
    /// (<see cref="ShipDesign.EquipmentBonusTypes"/>). Other tokens (sub-unit names, unknown equipment) match nothing.
    /// </summary>
    public static bool Matches(IEnumerable<string> tokens, IReadOnlyCollection<string> designTypes)
    {
        return tokens.Any(token => designTypes.Contains(token, StringComparer.Ordinal));
    }

    /// <summary>
    /// S-MIO-MATCH [RE] 0xdb6fd0: the MIO's <c>equipment_type</c> covers the design, so the design can be assigned to it.
    /// (The game also allows it when the attached policy has a key that matches; designs here are checked without a side.)
    /// </summary>
    public static bool CanAssign(Mio mio, ShipDesign design)
    {
        return Matches(mio.EquipmentTypes, design.EquipmentBonusTypes);
    }

    /// <summary>
    /// S-MIO-MATCH [RE] 0xa4fe60: a trait's bonus reaches the design: no <c>limit_to_equipment_type</c> written, or one that
    /// matches. The MIO's own <c>equipment_type</c> is not tested again; the initial trait follows its limit too.
    /// </summary>
    public static bool Reaches(MioTrait trait, ShipDesign design)
    {
        return trait.LimitToEquipmentTypes.Count == 0 || Matches(trait.LimitToEquipmentTypes, design.EquipmentBonusTypes);
    }

    /// <summary>
    /// S-STATS-MIO [RE] 0xdb7800, 0xa4b260: the bonus of <paramref name="mio"/> (the MIO the design is assigned to) for the
    /// design, one sum per stat: the initial trait, the side's unlocked traits that reach the design, and the attached
    /// policy's blocks whose key matches it (<c>same_as_mio</c>: the MIO's <c>equipment_type</c> matches it). With no
    /// setup (the side has not touched the MIO) only the initial trait counts. No MIO, or a setup of another MIO, gives
    /// nothing. [RE] The game takes the sum when the variant is created; the simulator applies the side's current
    /// traits, as for a design made after them.
    /// </summary>
    public static ShipStats EquipmentBonus(ShipDesign design, Mio? mio, MioSetup? setup)
    {
        if (mio is null || (setup is not null && setup.Mio.ID != mio.ID))
        {
            return new ShipStats();
        }

        var traits = (mio.InitialTrait is null ? [] : new[] { mio.InitialTrait }).Concat(setup?.UnlockedTraits ?? []);
        var bonus = traits.Where(trait => Reaches(trait, design))
            .Aggregate(new ShipStats(), (current, trait) => current.Add(trait.EquipmentBonus));

        foreach (var (target, values) in setup?.Policy?.EquipmentBonuses ?? [])
        {
            if (target == MioPolicy.SameAsMio ? CanAssign(mio, design) : Matches([target], design.EquipmentBonusTypes))
            {
                bonus = bonus.Add(values);
            }
        }

        return bonus;
    }

    /// <summary>
    /// S-MIO-POLICY [PDX + RE]: the MIO can take the policy: it has no <c>has_mio_equipment_type</c> condition, or one
    /// names an entry of the MIO's <c>equipment_type</c> (the trigger looks for the name in the list, it does not match).
    /// </summary>
    public static bool PolicyAllowed(Mio mio, MioPolicy policy)
    {
        return policy.AllowedEquipmentTypes.Count == 0 || policy.AllowedEquipmentTypes.Any(token => mio.EquipmentTypes.Contains(token, StringComparer.Ordinal));
    }

    /// <summary>
    /// S-MIO-TREE [RE] 0x13af360, 0xdba8e0: unlocked traits the game's tree would not allow: a parent group with fewer of
    /// its traits unlocked than it needs (all groups must hold; an empty <c>any_parent</c> never does), and two traits
    /// that exclude each other both unlocked. The game checks only the candidate's own <c>mutually_exclusive</c> list,
    /// so a one-sided pair can both be unlocked (the listing trait first); a reciprocal pair cannot (each pair once).
    /// </summary>
    public static IReadOnlyList<string> TreeProblems(MioSetup setup)
    {
        var unlocked = setup.UnlockedTraits.ToDictionary(trait => trait.ID, StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var trait in setup.UnlockedTraits)
        {
            foreach (var parent in trait.Parents.Where(parent => parent.Traits.Count(unlocked.ContainsKey) < parent.Needed))
            {
                if (parent.Traits.Count < parent.Needed)
                {
                    problems.Add($"trait '{trait.ID}' can never be unlocked (a parent group needs {parent.Needed} of {parent.Traits.Count} traits)");
                    continue;
                }

                var which = parent.Needed == parent.Traits.Count && parent.Needed > 1 ? "all of" : parent.Needed == 1 ? "one of" : $"{parent.Needed} of";
                problems.Add($"trait '{trait.ID}' needs {which} {string.Join(", ", parent.Traits)} unlocked first");
            }
        }

        var pairs = setup.UnlockedTraits.SelectMany(trait => trait.MutuallyExclusive
                .Where(other => other != trait.ID && unlocked.TryGetValue(other, out var excluded) && excluded.MutuallyExclusive.Contains(trait.ID))
                .Select(other => string.CompareOrdinal(trait.ID, other) < 0 ? (First: trait.ID, Second: other) : (First: other, Second: trait.ID)))
            .Distinct();

        problems.AddRange(pairs.Select(pair => $"traits '{pair.First}' and '{pair.Second}' are mutually exclusive"));
        return problems;
    }
}
