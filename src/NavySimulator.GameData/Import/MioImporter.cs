using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads the MIOs that can be assigned to ships (<c>common/military_industrial_organization/organizations</c>), the
/// policies they can take (<c>.../policies</c>) and the equipment groups (<c>common/equipment_groups</c>) from the game and
/// mods, following the game's <c>organizations/_documentation.md</c> and <c>policies/_documentation.md</c> [PDX] and the
/// include resolution decoded from hoi4.exe (S-MIO-IMPORT [RE], finding S-MIO-include-and-tree.md). Only problems of the
/// MIOs that are imported are reported.
/// </summary>
public static class MioImporter
{
    public const string OrganizationsFolder = "common/military_industrial_organization/organizations";
    public const string PoliciesFolder = "common/military_industrial_organization/policies";
    public const string EquipmentGroupsFolder = "common/equipment_groups";

    /// <summary>The policy key that reaches every design the MIO is assigned to.</summary>
    public const string SameAsMio = "same_as_mio";

    private static readonly HashSet<string> NavalUnitTypes = new(["capital_ship", "screen_ship", "carrier", "submarine"], StringComparer.Ordinal);

    // Keys of an including MIO that change the tree or the include itself instead of overriding a field.
    private static readonly HashSet<string> IncludeKeys = new(
        ["include", "delete_included_values", "initial_trait", "trait", "add_trait", "remove_trait", "override_trait"], StringComparer.Ordinal);

    private static readonly HashSet<string> TriggerKeys = new(["allowed", "visible", "available"], StringComparer.Ordinal);

    // Keys of a trait that MergeTrait does not take key by key.
    private static readonly HashSet<string> MergeOwnKeys = new(["token", "delete_included_values", "equipment_bonus", "production_bonus"], StringComparer.Ordinal);

    public static MioImport Read(GameFileSystem files, string language = "english")
    {
        var import = new MioImport();
        var localisation = LocalisationTable.Load(files, language);
        var groups = ReadEntries(files, EquipmentGroupsFolder, import)
            .ToDictionary(entry => entry.Key, entry => Words(entry.Value.Block.GetAll("equipment_type")), StringComparer.Ordinal);
        var navalTokens = NavalEquipment(files, import);

        bool IsNaval(IEnumerable<string> tokens) => tokens.Any(token => Expand(token, groups).Any(navalTokens.Contains));

        var scriptedTriggers = ReadEntries(files, "common/scripted_triggers", import, reportDuplicates: false)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Block, StringComparer.OrdinalIgnoreCase);
        var organizations = ReadEntries(files, OrganizationsFolder, import);
        var resolved = new Dictionary<string, RawMio?>(StringComparer.Ordinal);
        var problems = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (id, entry) in organizations)
        {
            var mio = Resolve(id, organizations, resolved, [], problems);

            if (mio is not null && IsNaval(Words(mio.Fields.Where(field => field.Key == "equipment_type").Select(field => field.Value))))
            {
                import.Warnings.AddRange(problems.GetValueOrDefault(id) ?? []);
                import.Mios.Add((ToDto(id, mio, localisation, scriptedTriggers, import), entry.File));
            }
        }

        foreach (var (id, entry) in ReadEntries(files, PoliciesFolder, import))
        {
            var policy = ReadPolicy(id, entry.Block, localisation);

            if (policy.AllowedEquipmentTypes.Count == 0 || IsNaval(policy.AllowedEquipmentTypes))
            {
                import.Policies.Add(policy);
            }
        }

        foreach (var (id, members) in groups.Where(group => Expand(group.Key, groups).Any(navalTokens.Contains)))
        {
            import.EquipmentGroups.Add(new EquipmentGroupDto { ID = id, Name = localisation.Get(id), EquipmentTypes = members });
        }

        ReadCountries(files, localisation, import);
        return import;
    }

    /// <summary>
    /// The tags of <c>common/country_tags</c> in load order, without the files marked <c>dynamic_tags = yes</c> (civil war
    /// and releasable placeholders), named by their localisation key (<c>USA</c>, else <c>USA_DEF</c>).
    /// </summary>
    private static void ReadCountries(GameFileSystem files, LocalisationTable localisation, MioImport import)
    {
        foreach (var path in files.GetFiles("common/country_tags", "*.txt"))
        {
            var root = Parse(files, path, import);

            if (root is null || root.GetBool("dynamic_tags") == true)
            {
                continue;
            }

            foreach (var entry in root.KeyedEntries.Where(entry => entry.Value.Scalar is not null && entry.Key!.Length == 3))
            {
                if (import.Countries.All(country => country.ID != entry.Key))
                {
                    import.Countries.Add(new CountryDto { ID = entry.Key!, Name = localisation.Get(entry.Key!) ?? localisation.Get($"{entry.Key}_DEF") });
                }
            }
        }
    }

    /// <summary>
    /// The equipment tokens that name ship equipment: the ship archetypes (<c>is_archetype</c> with a naval <c>type</c>),
    /// every equipment of those archetypes, and the naval equipment types themselves.
    /// </summary>
    private static HashSet<string> NavalEquipment(GameFileSystem files, MioImport import)
    {
        var tokens = new HashSet<string>(NavalUnitTypes, StringComparer.Ordinal);
        var equipment = new List<(string ID, PdxBlock Block)>();

        foreach (var path in files.GetFiles("common/units/equipment", "*.txt"))
        {
            foreach (var root in Parse(files, path, import)?.GetAll("equipments").Where(value => value.Block is not null) ?? [])
            {
                equipment.AddRange(root.Block!.KeyedEntries.Where(entry => entry.Value.Block is not null).Select(entry => (entry.Key!, entry.Value.Block!)));
            }
        }

        foreach (var (id, block) in equipment.Where(item => item.Block.GetBool("is_archetype") == true && NavalUnitTypes.Contains(item.Block.GetString("type") ?? string.Empty)))
        {
            tokens.Add(id);
        }

        foreach (var (id, _) in equipment.Where(item => tokens.Contains(item.Block.GetString("archetype") ?? string.Empty)).ToList())
        {
            tokens.Add(id);
        }

        return tokens;
    }

    /// <summary>A token with its equipment groups expanded, recursively; a token that is no group stands for itself.</summary>
    private static IEnumerable<string> Expand(string token, IReadOnlyDictionary<string, List<string>> groups, int depth = 0)
    {
        if (depth > 10 || !groups.TryGetValue(token, out var members))
        {
            return [token];
        }

        return members.SelectMany(member => Expand(member, groups, depth + 1));
    }

    private static RawMio? Resolve(
        string id,
        IReadOnlyDictionary<string, Entry> organizations,
        Dictionary<string, RawMio?> resolved,
        HashSet<string> including,
        Dictionary<string, List<string>> problems)
    {
        if (resolved.TryGetValue(id, out var done))
        {
            return done;
        }

        if (!problems.TryGetValue(id, out var warnings))
        {
            warnings = [];
            problems[id] = warnings;
        }

        if (!including.Add(id))
        {
            warnings.Add($"MIO '{id}' includes itself; it is skipped.");
            return null;
        }

        var block = organizations[id].Block;
        var includeId = block.GetString("include");
        RawMio mio;

        if (includeId is null)
        {
            mio = new RawMio(block.KeyedEntries.Where(entry => !IncludeKeys.Contains(entry.Key!)).ToList(), InitialTrait(block), OwnTraits(block));
        }
        else if (!organizations.ContainsKey(includeId) || Resolve(includeId, organizations, resolved, including, problems) is not { } included)
        {
            warnings.Add($"MIO '{id}' includes unknown MIO '{includeId}'; it is skipped.");
            resolved[id] = null;
            return null;
        }
        else
        {
            mio = Include(id, includeId, included, block, warnings);
        }

        including.Remove(id);
        resolved[id] = mio;
        return mio;
    }

    /// <summary>
    /// S-MIO-IMPORT [RE] 0xa54150: the including MIO's own block with the included MIO filled in. A field is taken from
    /// the included MIO when the own one is unset (missing or an empty block) and not in <c>delete_included_values</c>;
    /// lists are never appended. The initial trait is merged (MergeTrait) unless <c>delete_included_values</c> names it.
    /// The tree is the own <c>trait</c>/<c>add_trait</c> blocks in file order, then the included traits in their order:
    /// one in <c>remove_trait</c> is skipped (an own block with its token then stays unmerged); else an
    /// <c>override_trait</c> with its token is appended with the included trait merged in; else an own block with its
    /// token gets the included trait merged in, in place (vanilla's BEL, NOR and PHI shipyards narrow a generic trait's
    /// limit this way); else it is copied. An override that matches no included trait is left out.
    /// </summary>
    private static RawMio Include(string id, string includeId, RawMio included, PdxBlock block, List<string> warnings)
    {
        var deleted = Words(block.GetAll("delete_included_values")).ToHashSet(StringComparer.Ordinal);
        var own = block.KeyedEntries.Where(entry => !IncludeKeys.Contains(entry.Key!)).ToList();
        var fields = own.Concat(included.Fields.Where(field =>
                !deleted.Contains(field.Key!) && !own.Any(entry => entry.Key == field.Key && IsSet(entry))))
            .ToList();
        fields.Add(new PdxEntry("include", "=", PdxValue.FromScalar(includeId), 0));

        var initial = InitialTrait(block);

        if (!deleted.Contains("initial_trait") && included.InitialTrait is { } includedInitial)
        {
            initial = MergeTrait(initial ?? [], includedInitial);
        }

        var removed = Words(block.GetAll("remove_trait")).ToHashSet(StringComparer.Ordinal);
        var overrides = block.GetAll("override_trait").Where(value => value.Block is not null).Select(value => value.Block!.KeyedEntries.ToList()).ToList();
        var traits = OwnTraits(block);
        var ownCount = traits.Count;

        foreach (var trait in included.Traits)
        {
            var token = Token(trait);

            if (token is null || removed.Contains(token))
            {
                continue;
            }

            var overrideIndex = overrides.FindIndex(candidate => Token(candidate) == token);
            var ownIndex = traits.FindIndex(0, ownCount, candidate => Token(candidate) == token);

            if (overrideIndex >= 0)
            {
                traits.Add(MergeTrait(overrides[overrideIndex], trait));
                overrides.RemoveAt(overrideIndex);
            }
            else if (ownIndex >= 0)
            {
                traits[ownIndex] = MergeTrait(traits[ownIndex], trait);
            }
            else
            {
                traits.Add(MergeTrait([], trait));
            }
        }

        foreach (var unmatched in overrides)
        {
            warnings.Add($"MIO '{id}' overrides trait '{Token(unmatched)}', which '{includeId}' does not have (or removes); the override is left out.");
        }

        return new RawMio(fields, initial, traits);
    }

    /// <summary>
    /// S-MIO-IMPORT [RE] 0xa507b0: an own trait with the included trait merged in. Each key the own trait leaves unset
    /// (missing, an empty block, or <c>special_trait_background = no</c>) and does not list in its
    /// <c>delete_included_values</c> comes from the included trait; <c>parent</c>, <c>any_parent</c> and
    /// <c>all_parents</c> are one key (<c>parent</c>), so any own parent group drops all included ones. Equipment and
    /// production bonuses are merged per kind: the included equipment stats are taken only when the own trait has none
    /// (same for production).
    /// </summary>
    private static List<PdxEntry> MergeTrait(List<PdxEntry> own, List<PdxEntry> included)
    {
        var deleted = Words(own.Where(entry => entry.Key == "delete_included_values").Select(entry => entry.Value)).ToHashSet(StringComparer.Ordinal);
        var merged = included.Where(entry => entry.Key == "token").Take(1)
            .Concat(own.Where(entry => entry.Key is not ("token" or "delete_included_values")))
            .ToList();
        var ownSet = own.Where(IsSet).Select(entry => MergeKey(entry.Key!)).ToHashSet(StringComparer.Ordinal);

        merged.AddRange(included.Where(entry => !MergeOwnKeys.Contains(entry.Key!) &&
                                                !ownSet.Contains(MergeKey(entry.Key!)) &&
                                                !deleted.Contains(MergeKey(entry.Key!))));

        foreach (var kind in new[] { "equipment_bonus", "production_bonus" })
        {
            if (!deleted.Contains(kind) && Numbers(own.Where(entry => entry.Key == kind).Select(entry => entry.Value)).Count == 0)
            {
                merged.AddRange(included.Where(entry => entry.Key == kind));
            }
        }

        return merged;
    }

    // [RE] All parent groups are one list in the game's trait, keyed "parent" in the merge.
    private static string MergeKey(string key)
    {
        return key is "any_parent" or "all_parents" ? "parent" : key;
    }

    // [RE] 0xa50dc0 / 0xa54aa0: an empty block counts as unset, and special_trait_background only when yes. Any parent
    // group counts (even an empty one: the group exists).
    private static bool IsSet(PdxEntry entry)
    {
        return entry.Key switch
        {
            "special_trait_background" => entry.Value.TryGetBool(out var special) && special,
            "parent" or "any_parent" or "all_parents" => true,
            _ => entry.Value.Block is not { Entries.Count: 0 }
        };
    }

    // [RE] A second initial_trait block replaces the first.
    private static List<PdxEntry>? InitialTrait(PdxBlock block)
    {
        return block.GetAll("initial_trait").LastOrDefault(value => value.Block is not null)?.Block!.KeyedEntries.ToList();
    }

    // [RE] trait and add_trait share one reader: the own tree in file order.
    private static List<List<PdxEntry>> OwnTraits(PdxBlock block)
    {
        return block.KeyedEntries
            .Where(entry => entry.Key is "trait" or "add_trait" && entry.Value.Block is not null)
            .Select(entry => entry.Value.Block!.KeyedEntries.ToList())
            .ToList();
    }

    private static MioDto ToDto(
        string id,
        RawMio raw,
        LocalisationTable localisation,
        IReadOnlyDictionary<string, PdxBlock> scriptedTriggers,
        MioImport import)
    {
        var fields = new PdxBlock(raw.Fields);
        var countries = new List<string>();

        var excluded = new List<string>();

        foreach (var allowed in fields.GetAll("allowed").Where(value => value.Block is not null))
        {
            CollectTags(allowed.Block!, countries, excluded, negated: false, scriptedTriggers, 0);
        }

        var tag = countries.FirstOrDefault();
        var nameKey = fields.GetString("name");
        var mio = new MioDto
        {
            ID = id,
            Name = nameKey is null ? LocalisedForOwner(id, tag, localisation) : LocalisedForOwner(nameKey, tag, localisation),
            Countries = countries,
            ExcludedCountries = excluded,
            ArchetypeOnly = fields.GetAll("allowed").Any(value => value.Block?.Get("always")?.TryGetBool(out var always) == true && !always),
            DebugOnly = fields.GetAll("allowed").Any(value => value.Block?.Get("is_debug")?.TryGetBool(out var debug) == true && debug),
            Include = fields.GetString("include"),
            EquipmentTypes = Words(fields.GetAll("equipment_type")),
            Conditions = Conditions(fields)
        };

        if (raw.InitialTrait is { } initial)
        {
            mio.InitialTrait = ToTrait(id, "initial_trait", new PdxBlock(initial), tag, localisation);
            mio.InitialTrait.ID = string.Empty;
        }

        var traits = raw.Traits.Select(trait => new PdxBlock(trait)).ToList();
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

        foreach (var trait in traits)
        {
            var token = trait.GetString("token");

            if (string.IsNullOrEmpty(token))
            {
                import.Warnings.Add($"MIO '{id}' has a trait without a token; it is skipped.");
                continue;
            }

            if (mio.Traits.Any(existing => existing.ID == token))
            {
                import.Warnings.Add($"MIO '{id}' has trait '{token}' twice; the first is used.");
                continue;
            }

            var dto = ToTrait(id, token, trait, tag, localisation);
            (dto.X, dto.Y) = Position(token, traits, positions, [], id, import);
            mio.Traits.Add(dto);
        }

        foreach (var trait in mio.Traits)
        {
            foreach (var missing in trait.Parents.SelectMany(parent => parent.Traits).Concat(trait.MutuallyExclusive)
                         .Where(other => mio.Traits.All(existing => existing.ID != other)).Distinct(StringComparer.Ordinal))
            {
                import.Warnings.Add($"MIO '{id}' trait '{trait.ID}' names trait '{missing}', which the MIO does not have.");
            }
        }

        return mio;
    }

    private static MioTraitDto ToTrait(string mioId, string token, PdxBlock block, string? tag, LocalisationTable localisation)
    {
        var nameKey = block.GetString("name");
        var trait = new MioTraitDto
        {
            ID = token,
            Name = nameKey is null
                ? LocalisedForOwner($"{mioId}_{token}", tag, localisation) ?? localisation.Get(token)
                : LocalisedForOwner(nameKey, tag, localisation),
            Special = block.GetBool("special_trait_background") == true,
            MutuallyExclusive = Words(block.GetAll("mutually_exclusive")),
            LimitToEquipmentTypes = Words(block.GetAll("limit_to_equipment_type")),
            EquipmentBonus = Numbers(block.GetAll("equipment_bonus")),
            ProductionBonus = Numbers(block.GetAll("production_bonus")),
            OrganizationModifiers = Numbers(block.GetAll("organization_modifier")),
            Conditions = Conditions(block)
        };

        foreach (var parent in block.GetAll("parent").Where(value => value.Block is not null))
        {
            var traits = Words(parent.Block!.GetAll("traits"));
            AddParent(trait, traits, (int)(parent.Block!.GetDouble("num_parents_needed") ?? 1));
        }

        foreach (var parent in block.GetAll("any_parent"))
        {
            AddParent(trait, Words([parent]), 1);
        }

        foreach (var parent in block.GetAll("all_parents"))
        {
            var traits = Words([parent]);
            AddParent(trait, traits, traits.Count);
        }

        return trait;
    }

    // S-MIO-TREE [RE] 0xdba8e0: a group holds when `needed` of its traits are unlocked. One needing none (all_parents = { })
    // always holds and is left out; an empty any_parent needs 1 and never holds, so it is kept.
    private static void AddParent(MioTraitDto trait, List<string> traits, int needed)
    {
        if (needed > 0)
        {
            trait.Parents.Add(new MioTraitParentDto { Traits = traits, Needed = needed });
        }
    }

    /// <summary>
    /// S-MIO-IMPORT [RE] 0xa4e2c0: <c>position</c> (unset: −1, −1), as a delta from the position of the first trait with
    /// the <c>relative_position_id</c> token when it has one; an unknown relative trait puts it at (0, 0).
    /// </summary>
    private static (double X, double Y) Position(
        string token,
        IReadOnlyList<PdxBlock> traits,
        Dictionary<string, (double X, double Y)> positions,
        HashSet<string> visiting,
        string mioId,
        MioImport import)
    {
        if (positions.TryGetValue(token, out var known))
        {
            return known;
        }

        var block = traits.FirstOrDefault(trait => trait.GetString("token") == token);

        if (block is null || !visiting.Add(token))
        {
            return (0, 0);
        }

        var position = block.GetBlock("position");
        var x = position?.GetDouble("x") ?? -1;
        var y = position?.GetDouble("y") ?? -1;

        if (block.GetString("relative_position_id") is { } relative)
        {
            if (traits.All(trait => trait.GetString("token") != relative) || visiting.Contains(relative))
            {
                import.Warnings.Add($"MIO '{mioId}': trait '{token}' is placed relative to '{relative}', which cannot be found; it is at (0, 0).");
                (x, y) = (0, 0);
            }
            else
            {
                var origin = Position(relative, traits, positions, visiting, mioId, import);
                x += origin.X;
                y += origin.Y;
            }
        }

        visiting.Remove(token);
        positions[token] = (x, y);
        return (x, y);
    }

    private static MioPolicyDto ReadPolicy(string id, PdxBlock block, LocalisationTable localisation)
    {
        var nameKey = block.GetString("name");
        var policy = new MioPolicyDto
        {
            ID = id,
            Name = localisation.Get(nameKey ?? id),
            OrganizationModifiers = Numbers(block.GetAll("organization_modifier")),
            Conditions = Conditions(block)
        };

        foreach (var allowed in block.GetAll("allowed").Where(value => value.Block is not null))
        {
            CollectEquipmentConditions(allowed.Block!, policy.AllowedEquipmentTypes);
        }

        foreach (var (key, target) in new[] { ("equipment_bonus", policy.EquipmentBonus), ("production_bonus", policy.ProductionBonus) })
        {
            foreach (var bonus in block.GetAll(key).Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
            {
                if (bonus.Value.Block is not null)
                {
                    var stats = target.GetValueOrDefault(bonus.Key!) ?? new Dictionary<string, double>(StringComparer.Ordinal);

                    foreach (var (stat, value) in Numbers([bonus.Value]))
                    {
                        stats[stat] = stats.GetValueOrDefault(stat) + value;
                    }

                    target[bonus.Key!] = stats;
                }
            }
        }

        return policy;
    }

    /// <summary>
    /// S-MIO-COUNTRY [PDX]: the <c>tag</c> and <c>original_tag</c> conditions of <c>allowed</c> (keys are case-insensitive,
    /// TFB writes <c>original_TAG</c>), those under a <c>NOT</c> kept apart; a scripted trigger used as
    /// <c>NOR_AAT = yes</c> (<c>common/scripted_triggers</c>) is looked into, <c>= no</c> negated.
    /// </summary>
    private static void CollectTags(
        PdxBlock trigger,
        List<string> tags,
        List<string> excluded,
        bool negated,
        IReadOnlyDictionary<string, PdxBlock> scriptedTriggers,
        int depth)
    {
        foreach (var entry in trigger.KeyedEntries)
        {
            var key = entry.Key!;
            var target = negated ? excluded : tags;

            if ((key.Equals("tag", StringComparison.OrdinalIgnoreCase) || key.Equals("original_tag", StringComparison.OrdinalIgnoreCase)) &&
                entry.Value.Scalar is { } tag)
            {
                if (!target.Contains(tag.ToUpperInvariant()))
                {
                    target.Add(tag.ToUpperInvariant());
                }
            }
            else if (!key.Equals("limit", StringComparison.OrdinalIgnoreCase) && entry.Value.Block is { } inner)
            {
                CollectTags(inner, tags, excluded, negated ^ key.Equals("NOT", StringComparison.OrdinalIgnoreCase), scriptedTriggers, depth);
            }
            else if (depth < 5 && entry.Value.TryGetBool(out var holds) && scriptedTriggers.TryGetValue(key, out var scripted))
            {
                CollectTags(scripted, tags, excluded, negated ^ !holds, scriptedTriggers, depth + 1);
            }
        }
    }

    private static void CollectEquipmentConditions(PdxBlock trigger, List<string> tokens)
    {
        foreach (var entry in trigger.KeyedEntries)
        {
            if (entry.Key == "has_mio_equipment_type" && entry.Value.Scalar is { } token && !tokens.Contains(token))
            {
                tokens.Add(token);
            }
            else if (entry.Key != "NOT" && entry.Value.Block is { } inner)
            {
                CollectEquipmentConditions(inner, tokens);
            }
        }
    }

    private static List<string> Conditions(PdxBlock block)
    {
        return block.KeyedEntries
            .Where(entry => TriggerKeys.Contains(entry.Key!) && entry.Value.Block is { Entries.Count: > 0 })
            .Select(Text)
            .ToList();
    }

    /// <summary>[PDX] <c>TAG_key</c> when the owner's tag has it, else <c>key</c>.</summary>
    private static string? LocalisedForOwner(string key, string? tag, LocalisationTable localisation)
    {
        return (tag is null ? null : localisation.Get($"{tag}_{key}")) ?? localisation.Get(key);
    }

    private static string? Token(List<PdxEntry> trait)
    {
        return trait.LastOrDefault(entry => entry.Key == "token")?.Value.Scalar;
    }

    // Repeated keys add up, as the game's bonus sums do.
    private static Dictionary<string, double> Numbers(IEnumerable<PdxValue> blocks)
    {
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var entry in blocks.Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
        {
            if (entry.Value.TryGetDouble(out var value))
            {
                numbers[entry.Key!] = numbers.GetValueOrDefault(entry.Key!) + value;
            }
        }

        return numbers;
    }

    private static string Text(PdxEntry entry)
    {
        return $"{entry.Key} {entry.Operator ?? "="} {Text(entry.Value)}";
    }

    private static string Text(PdxValue value)
    {
        if (value.Block is null)
        {
            return value.ToString();
        }

        var parts = value.Block.Entries.Select(entry => entry.Key is null ? Text(entry.Value) : Text(entry));
        return $"{{ {string.Join(" ", parts)} }}";
    }

    /// <summary>The words of <c>key = word</c> and <c>key = { word word }</c> entries.</summary>
    private static List<string> Words(IEnumerable<PdxValue> values)
    {
        return values
            .SelectMany(value => value.Block is not null ? value.Block.BareValues.Select(bare => bare.Scalar) : [value.Scalar])
            .Where(word => !string.IsNullOrEmpty(word))
            .Select(word => word!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static PdxBlock? Parse(GameFileSystem files, string path, MioImport import)
    {
        try
        {
            var recovered = new List<string>();
            var block = PdxParser.ParseFile(path, recovered);
            import.Warnings.AddRange(recovered.Select(problem => $"{files.RelativePath(path)} {problem}."));
            return block;
        }
        catch (PdxParseException exception)
        {
            import.Warnings.Add($"{files.RelativePath(path)} could not be read: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Every top-level <c>id = { ... }</c> entry of a folder's effective files, by ID, in load order; a later definition
    /// wins (reported, unless <paramref name="reportDuplicates"/> is false: scripted triggers have unrelated ones).
    /// </summary>
    private static Dictionary<string, Entry> ReadEntries(GameFileSystem files, string folder, MioImport import, bool reportDuplicates = true)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles(folder, "*.txt"))
        {
            foreach (var entry in Parse(files, path, import)?.KeyedEntries.Where(entry => entry.Value.Block is not null) ?? [])
            {
                if (entries.TryGetValue(entry.Key!, out var previous) && reportDuplicates)
                {
                    import.Warnings.Add($"'{entry.Key}' is defined in {previous.File} and {files.RelativePath(path)}; the later one is used.");
                }

                entries[entry.Key!] = new Entry(entry.Value.Block!, files.RelativePath(path));
            }
        }

        return entries;
    }

    private sealed record Entry(PdxBlock Block, string File);

    /// <summary>An MIO during include resolution: its own fields, its initial trait and its traits, each as script entries.</summary>
    private sealed record RawMio(List<PdxEntry> Fields, List<PdxEntry>? InitialTrait, List<List<PdxEntry>> Traits);
}
