using NavySimulator.Engine;
using NavySimulator.Engine.Doctrines;
using NavySimulator.Engine.Leaders;
using NavySimulator.Engine.Mios;
using NavySimulator.Engine.Nations;
using NavySimulator.Engine.Stats;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Import;

namespace NavySimulator.GameData;

/// <summary>
/// Engine objects for the content of a <see cref="ContentCatalog"/>, keyed by ID. Lenient: entries the engine cannot
/// represent (ship types with an unknown role) are skipped and the first of duplicate IDs wins, so editors can preview
/// designs of a catalog that does not validate yet. <see cref="SetupLoader"/> only builds it from validated catalogs.
/// </summary>
public sealed class ContentLibrary
{
    private ContentLibrary(
        Dictionary<string, Hull> hulls,
        Dictionary<string, StatModule> modules,
        Dictionary<string, ShipType> shipTypes,
        Dictionary<string, Mio> mios,
        Dictionary<string, MioPolicy> mioPolicies,
        Dictionary<string, IReadOnlySet<string>> equipmentGroups,
        Dictionary<string, Research> researches,
        Dictionary<string, Spirit> spirits,
        Dictionary<string, PlaneEquipment> planes,
        Dictionary<string, GrandDoctrine> grandDoctrines,
        Dictionary<string, Subdoctrine> subdoctrines,
        Dictionary<string, LeaderTrait> leaderTraits,
        LeaderSkills leaderSkills,
        Dictionary<string, Nation> nations)
    {
        Hulls = hulls;
        Modules = modules;
        ShipTypes = shipTypes;
        Mios = mios;
        MioPolicies = mioPolicies;
        EquipmentGroups = equipmentGroups;
        Researches = researches;
        Spirits = spirits;
        Planes = planes;
        GrandDoctrines = grandDoctrines;
        Subdoctrines = subdoctrines;
        LeaderTraits = leaderTraits;
        LeaderSkills = leaderSkills;
        Nations = nations;
    }

    public IReadOnlyDictionary<string, Hull> Hulls { get; }
    public IReadOnlyDictionary<string, StatModule> Modules { get; }
    public IReadOnlyDictionary<string, ShipType> ShipTypes { get; }
    public IReadOnlyDictionary<string, Mio> Mios { get; }
    public IReadOnlyDictionary<string, MioPolicy> MioPolicies { get; }

    /// <summary>S-MIO-MATCH: each equipment group with its members expanded (nested groups resolved).</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> EquipmentGroups { get; }
    public IReadOnlyDictionary<string, Research> Researches { get; }
    public IReadOnlyDictionary<string, Spirit> Spirits { get; }
    public IReadOnlyDictionary<string, PlaneEquipment> Planes { get; }
    public IReadOnlyDictionary<string, GrandDoctrine> GrandDoctrines { get; }
    public IReadOnlyDictionary<string, Subdoctrine> Subdoctrines { get; }

    /// <summary>S-ADMIRAL-TRAIT: the leader traits of <c>leader-traits/</c>.</summary>
    public IReadOnlyDictionary<string, LeaderTrait> LeaderTraits { get; }

    /// <summary>S-ADMIRAL-SKILL: the navy skill levels of <c>leader-skills/</c> (none when not imported).</summary>
    public LeaderSkills LeaderSkills { get; }

    /// <summary>S-NATIONAL: the nations of <c>nations/</c>, by country tag.</summary>
    public IReadOnlyDictionary<string, Nation> Nations { get; }

    public static ContentLibrary From(ContentCatalog catalog)
    {
        var hulls = new Dictionary<string, Hull>();
        var parts = new Dictionary<string, CriticalPart>();
        var modules = new Dictionary<string, StatModule>();
        var shipTypes = new Dictionary<string, ShipType>();
        var mios = new Dictionary<string, Mio>();
        var mioPolicies = new Dictionary<string, MioPolicy>();
        var researches = new Dictionary<string, Research>();
        var spirits = new Dictionary<string, Spirit>();
        var planes = new Dictionary<string, PlaneEquipment>();
        var grandDoctrines = new Dictionary<string, GrandDoctrine>();
        var subdoctrines = new Dictionary<string, Subdoctrine>();

        foreach (var hull in catalog.Hulls.Where(hull => !string.IsNullOrWhiteSpace(hull.ID)))
        {
            hulls.TryAdd(hull.ID, new Hull(
                hull.ID,
                hull.Archetype,
                hull.EquipmentType,
                hull.Manpower,
                hull.BaseStats.ToDomain(),
                (hull.Slots ?? []).Select((slot, index) => new ModuleSlot(slot.ID, slot.Required, slot.AllowedCategories ?? [], slot.GameOrder ?? index)).ToList(),
                (hull.ModuleCountLimits ?? [])
                    .Select(limit => new ModuleCountLimit(limit.Modules ?? [], limit.Categories ?? [], limit.Operator, limit.Count))
                    .ToList(),
                hull.NavalDominanceFactor,
                (hull.DefaultModules ?? []).Values.SelectMany(id => catalog.Modules.FirstOrDefault(module => module.ID == id)?.AddEquipmentTypes ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .ToList()));
        }

        foreach (var part in catalog.CriticalParts.Where(part => !string.IsNullOrWhiteSpace(part.ID)))
        {
            parts.TryAdd(part.ID, new CriticalPart(
                part.ID,
                part.Chance,
                part.StrDamageMultiplier,
                part.OrgDamageMultiplier,
                part.StrDamage,
                part.OrgDamage,
                part.BaseDamageInstance,
                part.DamageInstancePerAddedModule,
                part.MaxDamageInstance,
                part.StatPenalties.ToDomain(),
                new ModifierSet(part.Modifiers ?? [])));
        }

        // Unknown part IDs are left out (the loader warns about them).
        List<CriticalPart> Parts(IEnumerable<string>? ids) =>
            (ids ?? []).Where(parts.ContainsKey).Select(id => parts[id]).ToList();

        foreach (var module in catalog.Modules.Where(module => !string.IsNullOrWhiteSpace(module.ID)))
        {
            modules.TryAdd(module.ID, new StatModule(
                module.ID,
                module.StatModifiers.ToDomain(),
                module.StatMultipliers.ToDomain(),
                module.StatAverages.ToDomain(),
                module.Category,
                module.AddEquipmentTypes ?? [],
                module.ForbiddenCategories ?? [],
                Parts(module.CriticalParts),
                module.Manpower ?? 0));
        }

        foreach (var shipType in catalog.ShipTypes.Where(shipType => !string.IsNullOrWhiteSpace(shipType.ID)))
        {
            if (Enum.TryParse<ShipRole>(shipType.Role, true, out var role))
            {
                shipTypes.TryAdd(shipType.ID, new ShipType(
                    shipType.ID,
                    role,
                    shipType.UnitTypes ?? [],
                    shipType.Priority,
                    shipType.Hulls ?? [],
                    shipType.RequiredModules ?? [],
                    shipType.Organization,
                    Parts(shipType.CriticalParts),
                    shipType.CriticalPartDamageChanceMult));
            }
        }

        foreach (var mio in catalog.Mios.Where(mio => !string.IsNullOrWhiteSpace(mio.ID)))
        {
            mios.TryAdd(mio.ID, new Mio(
                mio.ID,
                mio.EquipmentTypes ?? [],
                mio.InitialTrait is null ? null : Trait(mio.InitialTrait),
                (mio.Traits ?? []).Where(trait => !string.IsNullOrWhiteSpace(trait.ID)).DistinctBy(trait => trait.ID).Select(Trait).ToList()));
        }

        foreach (var policy in catalog.MioPolicies.Where(policy => !string.IsNullOrWhiteSpace(policy.ID)))
        {
            var notSimulated = new List<string>();
            var bonuses = (policy.EquipmentBonus ?? [])
                .Select(bonus => (bonus.Key, EquipmentStats(bonus.Value, notSimulated)))
                .ToList();
            mioPolicies.TryAdd(policy.ID, new MioPolicy(policy.ID, policy.AllowedEquipmentTypes ?? [], bonuses, notSimulated.Distinct().ToList()));
        }

        var groupMembers = catalog.EquipmentGroups.Where(group => !string.IsNullOrWhiteSpace(group.ID))
            .DistinctBy(group => group.ID)
            .ToDictionary(group => group.ID, group => group.EquipmentTypes ?? [], StringComparer.Ordinal);
        var equipmentGroups = groupMembers.Keys.ToDictionary(
            id => id,
            id => (IReadOnlySet<string>)ExpandGroup(id, groupMembers, 0).ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.Ordinal);

        foreach (var research in catalog.Researches.Where(research => !string.IsNullOrWhiteSpace(research.ID)))
        {
            researches.TryAdd(research.ID, new Research(
                research.ID,
                research.StatModifiers.ToDomain(),
                research.StatAverages.ToDomain(),
                research.StatMultipliers.ToDomain(),
                ParseRoles(research.AppliesToRoles),
                ParseTypes(research.AppliesToTypes ?? []),
                research.EquipmentBonus,
                new ModifierSet(research.CountryModifiers ?? [])));
        }

        foreach (var spirit in catalog.Spirits.Where(spirit => !string.IsNullOrWhiteSpace(spirit.ID)))
        {
            spirits.TryAdd(spirit.ID, new Spirit(
                spirit.ID,
                spirit.Category,
                (spirit.RequiredDoctrines ?? []).Select(group => (IReadOnlyList<string>)(group ?? [])).ToList(),
                Effects(spirit.Effects, spirit.ID)));
        }

        foreach (var folder in catalog.DoctrineFolders.Where(folder => !string.IsNullOrWhiteSpace(folder.ID)))
        {
            foreach (var doctrine in (folder.GrandDoctrines ?? []).Where(doctrine => !string.IsNullOrWhiteSpace(doctrine.ID)))
            {
                grandDoctrines.TryAdd(doctrine.ID, new GrandDoctrine(
                    doctrine.ID,
                    folder.ID,
                    doctrine.Tracks ?? [],
                    Effects(doctrine.Effects, doctrine.ID),
                    (doctrine.Milestones ?? []).ToDictionary(
                        milestone => milestone.Key,
                        milestone => Effects(milestone.Value, $"{doctrine.ID}/{milestone.Key} milestone"),
                        StringComparer.Ordinal)));
            }

            foreach (var subdoctrine in (folder.Subdoctrines ?? []).Where(subdoctrine => !string.IsNullOrWhiteSpace(subdoctrine.ID)))
            {
                subdoctrines.TryAdd(subdoctrine.ID, new Subdoctrine(
                    subdoctrine.ID,
                    folder.ID,
                    subdoctrine.Tracks ?? [],
                    subdoctrine.AllowInMultipleTracks,
                    subdoctrine.Xor ?? [],
                    Effects(subdoctrine.Effects, subdoctrine.ID),
                    (subdoctrine.Rewards ?? [])
                        .Select(reward => new DoctrineReward(reward.ID, reward.Mastery, Effects(reward.Effects, $"{subdoctrine.ID}/{reward.ID}")))
                        .ToList()));
            }
        }

        foreach (var (plane, order) in catalog.Planes.Where(plane => !string.IsNullOrWhiteSpace(plane.ID)).Select((plane, order) => (plane, order)))
        {
            planes.TryAdd(plane.ID, new PlaneEquipment(plane.ID, plane.Stats.ToDomain(), plane.Types, plane.Missions, plane.CarrierCapable,
                plane.SubUnit, plane.SubUnitTypes, plane.SubUnitCategories, plane.Year ?? 0, order));
        }

        var leaderTraits = new Dictionary<string, LeaderTrait>(StringComparer.Ordinal);

        foreach (var trait in catalog.LeaderTraits.Where(trait => !string.IsNullOrWhiteSpace(trait.ID)))
        {
            leaderTraits.TryAdd(trait.ID, LeaderTraitOf(trait));
        }

        // A later file's level replaces an earlier one, as in the game's files.
        var skillLevels = new Dictionary<string, IReadOnlyDictionary<int, ModifierSet>>(StringComparer.Ordinal);

        foreach (var skill in catalog.LeaderSkills.Where(skill => !string.IsNullOrWhiteSpace(skill.ID)))
        {
            var levels = skillLevels.TryGetValue(skill.ID, out var existing) ? existing.ToDictionary() : new Dictionary<int, ModifierSet>();

            foreach (var level in skill.Levels ?? [])
            {
                levels[level.Level] = new ModifierSet(level.Modifiers ?? []);
            }

            skillLevels[skill.ID] = levels;
        }

        var nations = new Dictionary<string, Nation>(StringComparer.Ordinal);

        foreach (var nation in catalog.Nations.Where(nation => !string.IsNullOrWhiteSpace(nation.ID)))
        {
            nations.TryAdd(nation.ID, CreateNation(nation));
        }

        return new ContentLibrary(hulls, modules, shipTypes, mios, mioPolicies, equipmentGroups, researches, spirits, planes, grandDoctrines, subdoctrines,
            leaderTraits, new LeaderSkills(skillLevels), nations);
    }

    /// <summary>
    /// S-NATIONAL: the engine form of a nation. A national spirit's targeted modifiers are listed as not simulated: the
    /// <c>_against</c> attack and defence factors are read for the controller of the task force's province, which at sea
    /// is the null tag, so they never act in a naval battle [RE] (S-STATS-AGAINST); the others act outside battle.
    /// Effects with an unknown effect name are left out.
    /// </summary>
    internal static Nation CreateNation(NationDto nation)
    {
        var spirits = new Dictionary<string, NationalSpirit>(StringComparer.Ordinal);

        foreach (var spirit in (nation.Spirits ?? []).Where(spirit => !string.IsNullOrWhiteSpace(spirit.ID)))
        {
            var effects = Effects(spirit.Effects, spirit.ID);
            var targeted = (spirit.TargetedModifiers ?? [])
                .SelectMany(block => (block.Modifiers ?? []).Select(entry => FormattableString.Invariant($"{entry.Key} {entry.Value:0.###} against {block.Tag} (targeted; no effect at sea)")));
            effects = new CountryEffects(effects.CountryModifiers, effects.StatBonuses, effects.NotSimulated.Concat(targeted).ToList(), effects.PlaneBonuses);
            spirits.TryAdd(spirit.ID, new NationalSpirit(spirit.ID, spirit.Category, effects));
        }

        var modifiers = new Dictionary<string, DynamicModifier>(StringComparer.Ordinal);

        foreach (var modifier in (nation.DynamicModifiers ?? []).Where(modifier => !string.IsNullOrWhiteSpace(modifier.ID)))
        {
            modifiers.TryAdd(modifier.ID, new DynamicModifier(modifier.ID, new ModifierSet(modifier.Modifiers ?? []),
                new Dictionary<string, string>(modifier.VariableModifiers ?? [], StringComparer.Ordinal)));
        }

        var focuses = (nation.Focuses ?? []).Where(focus => !string.IsNullOrWhiteSpace(focus.ID))
            .DistinctBy(focus => focus.ID)
            .Select(focus => new NationalFocus(focus.ID, NationalEffects(focus.Effects), focus.MutuallyExclusive ?? []))
            .ToList();
        return new Nation(nation.ID, spirits, modifiers, NationalEffects(nation.History), focuses);
    }

    /// <summary>S-NATIONAL-EFFECT: the game effect names <see cref="NationalEffectDto.Effect"/> takes, by engine kind.</summary>
    public static IReadOnlyDictionary<string, NationalEffectKind> NationalEffectNames { get; } = new Dictionary<string, NationalEffectKind>(StringComparer.Ordinal)
    {
        ["add_ideas"] = NationalEffectKind.AddIdea,
        ["remove_ideas"] = NationalEffectKind.RemoveIdea,
        ["add_dynamic_modifier"] = NationalEffectKind.AddDynamicModifier,
        ["remove_dynamic_modifier"] = NationalEffectKind.RemoveDynamicModifier,
        ["set_variable"] = NationalEffectKind.SetVariable,
        ["add_to_variable"] = NationalEffectKind.AddToVariable,
        ["subtract_from_variable"] = NationalEffectKind.SubtractFromVariable,
        ["multiply_variable"] = NationalEffectKind.MultiplyVariable
    };

    private static List<NationalEffect> NationalEffects(IEnumerable<NationalEffectDto>? effects)
    {
        return (effects ?? []).Where(effect => NationalEffectNames.ContainsKey(effect.Effect))
            .Select(effect => new NationalEffect(NationalEffectNames[effect.Effect], effect.Target, effect.Value ?? 0))
            .ToList();
    }

    /// <summary>
    /// S-ADMIRAL: the engine admiral of a side's admiral. Lenient, for previews: unknown traits are left out (the scenario
    /// check reports them).
    /// </summary>
    public Admiral CreateAdmiral(AdmiralDto admiral, Engine.Defines.GameDefines? defines = null)
    {
        var traits = (admiral.Traits ?? []).Distinct(StringComparer.Ordinal).Where(LeaderTraits.ContainsKey).Select(id => LeaderTraits[id]).ToList();
        var name = string.IsNullOrWhiteSpace(admiral.Name) ? (string.IsNullOrWhiteSpace(admiral.ID) ? "Admiral" : admiral.ID) : admiral.Name;
        return new Admiral(admiral.ID ?? string.Empty, name, admiral.Skill, admiral.AttackSkill, admiral.DefenseSkill, admiral.ManeuveringSkill,
            admiral.CoordinationSkill, traits, LeaderSkills, defines);
    }

    private static LeaderTrait LeaderTraitOf(LeaderTraitDto trait)
    {
        static LeaderAdjuster Adjuster(LeaderAdjusterDto? adjuster) =>
            adjuster is null ? default : new LeaderAdjuster(adjuster.Attack, adjuster.Defence, adjuster.Movement);

        return new LeaderTrait(
            trait.ID,
            trait.TraitType,
            new ModifierSet(trait.Modifiers ?? []),
            new ModifierSet(trait.NonSharedModifiers ?? []),
            (trait.Adjusters ?? []).ToDictionary(entry => entry.Key, entry => Adjuster(entry.Value), StringComparer.Ordinal),
            (trait.SubUnitModifiers ?? []).ToDictionary(entry => entry.Key, entry => new ModifierSet(entry.Value ?? []), StringComparer.Ordinal),
            (trait.SubUnitAdjusters ?? []).ToDictionary(entry => entry.Key, entry => Adjuster(entry.Value), StringComparer.Ordinal),
            new Dictionary<string, int>(trait.Skills ?? [], StringComparer.Ordinal),
            (trait.Parents ?? []).Select(parent => new LeaderTraitParent(parent.Traits ?? [], parent.Needed)).ToList(),
            trait.MutuallyExclusive ?? [],
            trait.Scripted ?? []);
    }

    /// <summary>S-MIO-TREE: the engine form of a trait; bonus keys with no simulator stat are listed as not simulated.</summary>
    private static MioTrait Trait(MioTraitDto trait)
    {
        var notSimulated = new List<string>();
        return new MioTrait(
            trait.ID ?? string.Empty,
            (trait.Parents ?? []).Select(parent => new MioTraitParent(parent.Traits ?? [], parent.Needed)).ToList(),
            trait.MutuallyExclusive ?? [],
            trait.LimitToEquipmentTypes ?? [],
            EquipmentStats(trait.EquipmentBonus, notSimulated),
            notSimulated);
    }

    private static ShipStats EquipmentStats(Dictionary<string, double>? values, List<string> notSimulated)
    {
        var stats = new ShipStatsDto();

        foreach (var (key, value) in values ?? [])
        {
            if (!ShipStatKeys.TryAdd(stats, key, value))
            {
                notSimulated.Add(key);
            }
        }

        return stats.ToDomain();
    }

    private static IEnumerable<string> ExpandGroup(string token, IReadOnlyDictionary<string, List<string>> groups, int depth)
    {
        if (depth > 10 || !groups.TryGetValue(token, out var members))
        {
            return depth == 0 ? [] : [token];
        }

        return members.SelectMany(member => ExpandGroup(member, groups, depth + 1));
    }

    /// <summary>
    /// S-DOCTRINE-EFFECT: the engine form of a doctrine part's or spirit's effects. Each sub-unit block becomes a
    /// technology-like bonus for that sub-unit (S-STATS-SHIP), each <c>equipment_bonus</c> block an equipment bonus for that
    /// equipment type (S-STATS-BONUS); plane stat keys of a sub-unit block become a plane bonus for the plane sub-unit or
    /// category it names (A-STATS); other stat keys and scripted effects are listed as not simulated.
    /// </summary>
    internal static CountryEffects Effects(CountryEffectsDto? effects, string source)
    {
        if (effects is null)
        {
            return CountryEffects.Empty;
        }

        var bonuses = new List<Research>();
        var planeBonuses = new List<PlaneBonus>();
        var notSimulated = new List<string>();

        void AddBonuses(Dictionary<string, Dictionary<string, double>>? blocks, bool isEquipmentBonus)
        {
            foreach (var (target, values) in blocks ?? [])
            {
                var stats = new ShipStatsDto();
                var planeStats = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var (key, value) in values ?? [])
                {
                    // Ships and planes share maximum_speed and reliability; a block for a ship sub-unit matches no plane
                    // and one for a plane category no ship.
                    var planeKey = !isEquipmentBonus && PlaneBonus.StatKeys.Contains(key);

                    if (planeKey)
                    {
                        planeStats[key] = value;
                    }

                    if (!ShipStatKeys.TryAdd(stats, key, value) && !planeKey)
                    {
                        notSimulated.Add($"{key} ({target})");
                    }
                }

                if (planeStats.Count > 0)
                {
                    planeBonuses.Add(new PlaneBonus($"{source} ({target})", target, new ModifierSet(planeStats)));
                }

                var domain = stats.ToDomain();

                if (domain != new ShipStats())
                {
                    bonuses.Add(new Research($"{source} ({target})", domain, new ShipStats(), new ShipStats(), [], [target], isEquipmentBonus));
                }
            }
        }

        AddBonuses(effects.UnitBonuses, isEquipmentBonus: false);
        AddBonuses(effects.EquipmentBonuses, isEquipmentBonus: true);
        notSimulated.AddRange(effects.Scripted ?? []);
        return new CountryEffects(new ModifierSet(effects.CountryModifiers ?? []), bonuses, notSimulated, planeBonuses);
    }

    /// <summary>
    /// The design as an engine object, or null when its hull is unknown or no ship type needs the hull. Unknown module
    /// and MIO IDs are left out and reported in <paramref name="problems"/>; so are modules in slots the hull does not
    /// have. Module restrictions do not stop a design: see <see cref="CheckDesign"/>. The design belongs to the
    /// equipment groups holding its equipment types (S-MIO-MATCH).
    /// </summary>
    public ShipDesign? TryCreateDesign(ShipDesignDto design, List<string>? problems = null)
    {
        if (!TryResolveModules(design, problems, out var hull, out var modulesBySlot))
        {
            return null;
        }

        foreach (var slotId in modulesBySlot.Keys.Where(slotId => hull.Slots.All(slot => slot.ID != slotId)))
        {
            problems?.Add($"hull '{hull.ID}' has no slot '{slotId}'");
        }

        // In the game's slot order: stats do not depend on it, the critical part pick does (S-CRIT-PART).
        var modules = hull.Slots.Where(slot => modulesBySlot.ContainsKey(slot.ID)).OrderBy(slot => slot.GameOrder).Select(slot => modulesBySlot[slot.ID]).ToList();
        var (shipType, _) = DesignRules.ResolveShipType(hull, modules, ShipTypes.Values);

        if (shipType is null)
        {
            problems?.Add($"no ship type needs hull '{hull.ID}' (archetype '{hull.Archetype}')");
            return null;
        }

        Mio? mio = null;

        if (!string.IsNullOrWhiteSpace(design.MioID) && !Mios.TryGetValue(design.MioID, out mio))
        {
            problems?.Add($"unknown MIO '{design.MioID}' (import MIOs with 'NavySimulator import-mios')");
        }

        return new ShipDesign(design.ID, hull, shipType, modules, mio, EquipmentGroups);
    }

    /// <summary>The designer rules for the design (S-DESIGN), or null when its hull is unknown. Unknown modules are skipped.</summary>
    public DesignCheck? CheckDesign(ShipDesignDto design)
    {
        return TryResolveModules(design, null, out var hull, out var modulesBySlot)
            ? DesignRules.Check(hull, modulesBySlot, ShipTypes.Values)
            : null;
    }

    private bool TryResolveModules(
        ShipDesignDto design,
        List<string>? problems,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Hull? hull,
        out Dictionary<string, StatModule> modulesBySlot)
    {
        modulesBySlot = new Dictionary<string, StatModule>(StringComparer.Ordinal);

        if (!Hulls.TryGetValue(design.HullID, out hull))
        {
            problems?.Add($"unknown hull '{design.HullID}'");
            return false;
        }

        foreach (var (slotId, moduleId) in design.Modules ?? [])
        {
            if (Modules.TryGetValue(moduleId, out var module))
            {
                modulesBySlot[slotId] = module;
            }
            else
            {
                problems?.Add($"unknown module '{moduleId}' in slot '{slotId}'");
            }
        }

        return true;
    }

    internal static List<ShipRole> ParseRoles(List<string> roleNames)
    {
        var roles = new List<ShipRole>();

        foreach (var roleName in roleNames)
        {
            if (Enum.TryParse<ShipRole>(roleName, true, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    internal static List<string> ParseTypes(List<string> typeNames)
    {
        return typeNames
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
