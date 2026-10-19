using System.Globalization;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads the doctrine folders the simulator uses (<c>naval</c>, <c>air</c>) from <c>common/doctrines</c> and the navy and
/// air spirits from <c>common/ideas</c> (the idea categories <c>common/idea_tags</c> gives <c>type = navy_spirit</c> or
/// <c>air_spirit</c>), with their localised names, following the game's <c>common/doctrines/**/_documentation.md</c> [PDX].
/// </summary>
public static class DoctrineImporter
{
    public static readonly IReadOnlyList<string> FolderIds = ["naval", "air"];

    private static readonly HashSet<string> SpiritIdeaTypes = new(["navy_spirit", "air_spirit"], StringComparer.Ordinal);

    // Keys of a grand doctrine, subdoctrine or reward that are not activation effects.
    private static readonly HashSet<string> DoctrineNonEffectKeys = new(
    [
        "folder", "name", "description", "icon", "available", "visible", "xp_cost", "xp_type", "ai_will_do", "tracks",
        "milestones", "rewards", "track", "allow_in_multiple_tracks", "reward_gfx", "xor", "mastery", "max_track_rows",
        "max_track_columns"
    ], StringComparer.Ordinal);

    // Keys of an idea that are neither effects nor conditions.
    private static readonly HashSet<string> IdeaNonEffectKeys = new(
    [
        "name", "ledger", "ai_will_do", "cost", "removal_cost", "picture", "cancel", "allowed_civil_war", "allowed_to_remove",
        "level", "traits", "default", "available", "visible", "allowed"
    ], StringComparer.Ordinal);

    public static DoctrineImport Read(GameFileSystem files, string language = "english")
    {
        var import = new DoctrineImport();
        var localisation = LocalisationTable.Load(files, language);
        var folders = ReadEntries(files, "common/doctrines/folders", import);
        var tracks = ReadEntries(files, "common/doctrines/tracks", import);
        var grandDoctrines = ReadEntries(files, "common/doctrines/grand_doctrines", import);
        var subdoctrines = ReadEntries(files, "common/doctrines/subdoctrines", import, recursive: true);

        foreach (var folderId in FolderIds)
        {
            var nameKey = folders.GetValueOrDefault(folderId)?.Block.GetString("name");
            var folder = new DoctrineFolderDto { ID = folderId, Name = nameKey is null ? null : localisation.Get(nameKey) };

            foreach (var item in grandDoctrines.Values.Where(item => item.Block.GetString("folder") == folderId))
            {
                folder.GrandDoctrines.Add(ReadGrandDoctrine(item, localisation, import));
            }

            var trackIds = folder.GrandDoctrines.SelectMany(doctrine => doctrine.Tracks).Distinct(StringComparer.Ordinal).ToList();

            foreach (var trackId in trackIds)
            {
                var track = tracks.GetValueOrDefault(trackId);

                if (track is null)
                {
                    import.Warnings.Add($"track '{trackId}' is used by a {folderId} grand doctrine but not defined.");
                }

                var trackName = track?.Block.GetString("name");
                folder.Tracks.Add(new DoctrineTrackDto { ID = trackId, Name = trackName is null ? null : localisation.Get(trackName) });
            }

            foreach (var item in subdoctrines.Values.Where(item => Words(item.Block.GetAll("track")).Any(trackIds.Contains)))
            {
                folder.Subdoctrines.Add(ReadSubdoctrine(item, localisation, import));
            }

            if (folder.GrandDoctrines.Count > 0)
            {
                import.Folders.Add(folder);
            }
        }

        ReadSpirits(files, localisation, import);
        return import;
    }

    private static GrandDoctrineDto ReadGrandDoctrine(Entry item, LocalisationTable localisation, DoctrineImport import)
    {
        var doctrine = new GrandDoctrineDto
        {
            ID = item.ID,
            Name = LocalisedName(item, localisation),
            Tracks = Words(item.Block.GetAll("tracks")),
            Effects = ReadEffects(item.Block, DoctrineNonEffectKeys)
        };

        // [PDX] "milestones are in the same order as the tracks".
        var milestones = item.Block.GetAll("milestones").Where(value => value.Block is not null)
            .SelectMany(value => value.Block!.Entries.Where(entry => entry.Value.Block is not null))
            .ToList();

        for (var i = 0; i < milestones.Count; i++)
        {
            if (i >= doctrine.Tracks.Count)
            {
                import.Warnings.Add($"grand doctrine '{item.ID}' has more milestones than tracks; milestone {i + 1} is skipped.");
                continue;
            }

            doctrine.Milestones[doctrine.Tracks[i]] = ReadEffects(milestones[i].Value.Block!, DoctrineNonEffectKeys);
        }

        return doctrine;
    }

    private static SubdoctrineDto ReadSubdoctrine(Entry item, LocalisationTable localisation, DoctrineImport import)
    {
        var subdoctrine = new SubdoctrineDto
        {
            ID = item.ID,
            Name = LocalisedName(item, localisation),
            Tracks = Words(item.Block.GetAll("track")),
            AllowInMultipleTracks = item.Block.GetBool("allow_in_multiple_tracks") == true,
            Xor = Words(item.Block.GetAll("xor")),
            Effects = ReadEffects(item.Block, DoctrineNonEffectKeys)
        };

        var rewards = item.Block.GetAll("rewards").Where(value => value.Block is not null)
            .SelectMany(value => value.Block!.Entries.Where(entry => entry.Value.Block is not null))
            .ToList();

        for (var i = 0; i < rewards.Count; i++)
        {
            // [PDX] Reward localisation keys are <subdoctrine>_<reward>; the documentation's example has unnamed rewards.
            var id = rewards[i].Key ?? (i + 1).ToString(CultureInfo.InvariantCulture);
            var block = rewards[i].Value.Block!;
            subdoctrine.Rewards.Add(new DoctrineRewardDto
            {
                ID = id,
                Name = rewards[i].Key is null ? null : localisation.Get($"{item.ID}_{id}"),
                Mastery = block.GetDouble("mastery"),
                Effects = ReadEffects(block, DoctrineNonEffectKeys)
            });
        }

        if (subdoctrine.Rewards.Select(reward => reward.ID).Distinct(StringComparer.Ordinal).Count() != subdoctrine.Rewards.Count)
        {
            import.Warnings.Add($"subdoctrine '{item.ID}' has two rewards with the same name.");
        }

        return subdoctrine;
    }

    private static void ReadSpirits(GameFileSystem files, LocalisationTable localisation, DoctrineImport import)
    {
        var categories = new List<string>();

        foreach (var path in files.GetFiles("common/idea_tags", "*.txt"))
        {
            var root = Parse(files, path, import);

            foreach (var entry in root?.GetAll("idea_categories").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries) ?? [])
            {
                if (entry.Value.Block?.GetString("type") is { } type && SpiritIdeaTypes.Contains(type) && !categories.Contains(entry.Key!))
                {
                    categories.Add(entry.Key!);
                }
            }
        }

        var spirits = new Dictionary<string, SpiritDto>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/ideas", "*.txt"))
        {
            var root = Parse(files, path, import);

            foreach (var category in root?.GetAll("ideas").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries) ?? [])
            {
                if (!categories.Contains(category.Key!) || category.Value.Block is null)
                {
                    continue;
                }

                foreach (var idea in category.Value.Block.KeyedEntries.Where(entry => entry.Value.Block is not null))
                {
                    if (spirits.ContainsKey(idea.Key!))
                    {
                        import.Warnings.Add($"spirit '{idea.Key}' is defined twice; the later one ({files.RelativePath(path)}) is used.");
                    }

                    spirits[idea.Key!] = ReadSpirit(idea.Key!, category.Key!, idea.Value.Block!, localisation);
                }
            }
        }

        // Grouped by category, in the game's category order.
        import.Spirits.AddRange(spirits.Values.OrderBy(spirit => categories.IndexOf(spirit.Category)));
    }

    private static SpiritDto ReadSpirit(string id, string category, PdxBlock block, LocalisationTable localisation)
    {
        var spirit = new SpiritDto
        {
            ID = id,
            Name = localisation.Get(id),
            Category = category,
            CategoryName = localisation.Get(category),
            Effects = ReadEffects(block, IdeaNonEffectKeys)
        };

        foreach (var key in new[] { "available", "visible", "allowed" })
        {
            foreach (var trigger in block.GetAll(key).Where(value => value.Block is not null))
            {
                ReadConditions(trigger.Block!, spirit);
            }
        }

        return spirit;
    }

    /// <summary>
    /// S-SPIRIT [PDX]: splits a trigger into <c>has_doctrine</c> groups (a top-level or <c>AND</c> condition is a group of
    /// one; an <c>OR</c> of only <c>has_doctrine</c> conditions is one group) and the other conditions, kept as text.
    /// </summary>
    private static void ReadConditions(PdxBlock trigger, SpiritDto spirit)
    {
        foreach (var entry in trigger.KeyedEntries)
        {
            switch (entry.Key)
            {
                case "has_doctrine" when entry.Value.Scalar is not null:
                    spirit.RequiredDoctrines.Add([entry.Value.Scalar]);
                    break;
                case "AND" when entry.Value.Block is not null:
                    ReadConditions(entry.Value.Block, spirit);
                    break;
                case "OR" when entry.Value.Block is { } options && options.Entries.All(option => option.Key == "has_doctrine" && option.Value.Scalar is not null):
                    spirit.RequiredDoctrines.Add(options.Entries.Select(option => option.Value.Scalar!).Distinct(StringComparer.Ordinal).ToList());
                    break;
                default:
                    spirit.OtherConditions.Add(Text(entry));
                    break;
            }
        }
    }

    /// <summary>
    /// S-DOCTRINE-EFFECT [RE] 0x1529a80: the activation effects of a block. Numbers are country modifiers, a
    /// <c>modifier</c> block too; <c>equipment_bonus</c> holds equipment bonuses; any other block is a sub-unit (or
    /// sub-unit category) bonus; <c>effect</c>, <c>enable_tactic</c> and the like are scripted. The documented plural
    /// <c>modifiers</c> block has no case in the game's reader, so it is listed as scripted, not applied.
    /// </summary>
    private static CountryEffectsDto ReadEffects(PdxBlock block, HashSet<string> nonEffectKeys)
    {
        var effects = new CountryEffectsDto();

        foreach (var entry in block.KeyedEntries.Where(entry => !nonEffectKeys.Contains(entry.Key!)))
        {
            var key = entry.Key!;

            if (entry.Value.TryGetDouble(out var number))
            {
                effects.CountryModifiers[key] = effects.CountryModifiers.GetValueOrDefault(key) + number;
            }
            else if (key == "modifier" && entry.Value.Block is { } modifiers)
            {
                foreach (var (modifier, value) in Numbers(modifiers))
                {
                    effects.CountryModifiers[modifier] = effects.CountryModifiers.GetValueOrDefault(modifier) + value;
                }
            }
            else if (key == "equipment_bonus" && entry.Value.Block is { } bonuses)
            {
                foreach (var bonus in bonuses.KeyedEntries.Where(bonus => bonus.Value.Block is not null))
                {
                    AddStats(effects.EquipmentBonuses, bonus.Key!, Numbers(bonus.Value.Block!).Where(stat => stat.Key != "instant"));
                }
            }
            else if (key is "effect" or "modifiers" or "research_bonus" or "on_add" or "on_remove" or "do_effect" or "targeted_modifier" or "rule")
            {
                effects.Scripted.Add(key);
            }
            else if (entry.Value.Block is { } unit)
            {
                AddStats(effects.UnitBonuses, key, Numbers(unit));
            }
            else
            {
                effects.Scripted.Add(Text(entry));
            }
        }

        return effects;
    }

    private static void AddStats(Dictionary<string, Dictionary<string, double>> target, string key, IEnumerable<KeyValuePair<string, double>> stats)
    {
        if (!target.TryGetValue(key, out var values))
        {
            values = new Dictionary<string, double>(StringComparer.Ordinal);
            target[key] = values;
        }

        foreach (var (stat, value) in stats)
        {
            values[stat] = values.GetValueOrDefault(stat) + value;
        }
    }

    // Repeated keys add up, as the game's modifier and stat sums do.
    private static IEnumerable<KeyValuePair<string, double>> Numbers(PdxBlock block)
    {
        foreach (var entry in block.KeyedEntries)
        {
            if (entry.Value.TryGetDouble(out var value))
            {
                yield return new KeyValuePair<string, double>(entry.Key!, value);
            }
        }
    }

    private static string? LocalisedName(Entry item, LocalisationTable localisation)
    {
        var key = item.Block.GetString("name");
        return key is null ? localisation.Get(item.ID) : localisation.Get(key);
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

    private static PdxBlock? Parse(GameFileSystem files, string path, DoctrineImport import)
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

    /// <summary>Every top-level <c>id = { ... }</c> entry of a folder's effective files, by ID; a later definition wins.</summary>
    private static Dictionary<string, Entry> ReadEntries(GameFileSystem files, string folder, DoctrineImport import, bool recursive = false)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles(folder, "*.txt", recursive))
        {
            foreach (var entry in Parse(files, path, import)?.KeyedEntries.Where(entry => entry.Value.Block is not null) ?? [])
            {
                if (entries.TryGetValue(entry.Key!, out var previous))
                {
                    import.Warnings.Add($"'{entry.Key}' is defined in {previous.File} and {files.RelativePath(path)}; the later one is used.");
                }

                entries[entry.Key!] = new Entry(entry.Key!, entry.Value.Block!, files.RelativePath(path));
            }
        }

        return entries;
    }

    private sealed record Entry(string ID, PdxBlock Block, string File);
}
