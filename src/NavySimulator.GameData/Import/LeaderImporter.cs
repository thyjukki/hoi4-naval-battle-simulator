using System.Globalization;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// Reads what admirals are made of from the game's files [PDX]: the navy levels of the leader skills
/// (<c>common/unit_leader/00_*skills.txt</c>), the traits of <c>type = navy</c> or <c>all</c> (<c>leader_traits</c>, documented
/// at the top of <c>00_traits.txt</c>), and every character of <c>common/characters</c> with a <c>navy_leader</c> block,
/// with the country that recruits it.
/// </summary>
public static class LeaderImporter
{
    /// <summary>The skill files' root keys and the character key each skill is read with.</summary>
    public static readonly IReadOnlyDictionary<string, string> SkillRoots = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["leader_skills"] = "skill",
        ["leader_attack_skills"] = "attack_skill",
        ["leader_defense_skills"] = "defense_skill",
        ["leader_maneuvering_skills"] = "maneuvering_skill",
        ["leader_coordination_skills"] = "coordination_skill"
    };

    private static readonly string[] SkillKeys =
        ["attack_skill", "defense_skill", "maneuvering_skill", "coordination_skill", "logistics_skill", "planning_skill"];

    // Trait keys with effects the simulator does not run.
    private static readonly HashSet<string> ScriptedTraitKeys = new(["on_add", "on_remove", "daily_effect", "enable_ability"], StringComparer.Ordinal);

    public static LeaderImport Read(GameFileSystem files, string language = "english")
    {
        var import = new LeaderImport();
        var localisation = LocalisationTable.Load(files, language);
        var roots = files.GetFiles("common/unit_leader", "*.txt").Select(path => (Path: path, Root: Parse(files, path, import))).ToList();

        ReadSkills(roots, import);
        ReadTraits(files, roots, localisation, import);
        ReadAdmirals(files, localisation, import);
        return import;
    }

    private static void ReadSkills(List<(string Path, PdxBlock? Root)> roots, LeaderImport import)
    {
        var levels = new Dictionary<string, SortedDictionary<int, LeaderSkillLevelDto>>(StringComparer.Ordinal);

        foreach (var (_, root) in roots)
        {
            foreach (var entry in root?.KeyedEntries.Where(entry => SkillRoots.ContainsKey(entry.Key!) && entry.Value.Block is not null) ?? [])
            {
                var skill = SkillRoots[entry.Key!];

                foreach (var level in entry.Value.Block!.KeyedEntries.Where(level => level.Value.Block is not null))
                {
                    if (!int.TryParse(level.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ||
                        !Words(level.Value.Block!.GetAll("type")).Contains("navy"))
                    {
                        continue;
                    }

                    if (!levels.TryGetValue(skill, out var byLevel))
                    {
                        byLevel = [];
                        levels[skill] = byLevel;
                    }

                    // A later file's level replaces an earlier one.
                    byLevel[number] = new LeaderSkillLevelDto
                    {
                        Level = number,
                        Modifiers = Numbers(level.Value.Block!.GetBlock("modifier")),
                        BuildingModuleModifiers = Numbers(level.Value.Block!.GetBlock("building_module_modifier"))
                    };
                }
            }
        }

        foreach (var skill in SkillRoots.Values.Where(levels.ContainsKey))
        {
            import.Skills.Add(new LeaderSkillDto { ID = skill, Levels = levels[skill].Values.ToList() });
        }
    }

    private static void ReadTraits(GameFileSystem files, List<(string Path, PdxBlock? Root)> roots, LocalisationTable localisation, LeaderImport import)
    {
        var traits = new Dictionary<string, LeaderTraitDto>(StringComparer.Ordinal);

        foreach (var (path, root) in roots)
        {
            foreach (var entry in root?.GetAll("leader_traits").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries) ?? [])
            {
                if (entry.Value.Block is not { } block || !Words(block.GetAll("type")).Any(type => type is "navy" or "all"))
                {
                    continue;
                }

                if (traits.ContainsKey(entry.Key!))
                {
                    import.Warnings.Add($"leader trait '{entry.Key}' is defined twice; the later one ({files.RelativePath(path)}) is used.");
                }

                traits[entry.Key!] = ReadTrait(entry.Key!, block, localisation);
            }
        }

        import.Traits.AddRange(traits.Values);
    }

    private static LeaderTraitDto ReadTrait(string id, PdxBlock block, LocalisationTable localisation)
    {
        var trait = new LeaderTraitDto
        {
            ID = id,
            Name = localisation.Get(id),
            TraitType = block.GetString("trait_type"),
            NonSharedModifiers = Numbers(block.GetBlock("non_shared_modifier"))
        };

        foreach (var modifier in block.GetAll("modifier").Where(value => value.Block is not null))
        {
            foreach (var entry in modifier.Block!.KeyedEntries)
            {
                if (entry.Value.TryGetDouble(out var value))
                {
                    trait.Modifiers[entry.Key!] = trait.Modifiers.GetValueOrDefault(entry.Key!) + value;
                }
                else if (entry.Value.Block is { } adjuster)
                {
                    // [PDX] Terrain (water_deep_ocean = { attack defence movement }) and night adjusters.
                    trait.Adjusters[entry.Key!] = Adjuster(adjuster);
                }
            }
        }

        foreach (var subUnit in block.GetAll("sub_unit_modifiers").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries))
        {
            if (subUnit.Value.Block is not { } subUnitBlock)
            {
                continue;
            }

            var numbers = Numbers(subUnitBlock);

            if (numbers.Count > 0)
            {
                trait.SubUnitModifiers[subUnit.Key!] = numbers;
            }

            if (subUnitBlock.GetBlock("units") is { } units)
            {
                trait.SubUnitAdjusters[subUnit.Key!] = Adjuster(units);
            }
        }

        foreach (var key in SkillKeys)
        {
            if (block.GetDouble(key) is { } skill && skill != 0)
            {
                trait.Skills[key] = (int)skill;
            }
        }

        foreach (var parent in block.GetAll("parent").Where(value => value.Block is not null))
        {
            var traits = Words(parent.Block!.GetAll("traits"));
            trait.Parents.Add(new LeaderTraitParentDto { Traits = traits, Needed = (int)(parent.Block!.GetDouble("num_parents_needed") ?? 1) });
        }

        foreach (var parent in block.GetAll("any_parent"))
        {
            trait.Parents.Add(new LeaderTraitParentDto { Traits = Words([parent]), Needed = 1 });
        }

        foreach (var parent in block.GetAll("all_parents"))
        {
            var traits = Words([parent]);
            trait.Parents.Add(new LeaderTraitParentDto { Traits = traits, Needed = traits.Count });
        }

        trait.MutuallyExclusive = Words(block.GetAll("mutually_exclusive"));
        trait.Scripted = block.KeyedEntries.Select(entry => entry.Key!).Where(ScriptedTraitKeys.Contains).Distinct(StringComparer.Ordinal).ToList();
        return trait;
    }

    private static void ReadAdmirals(GameFileSystem files, LocalisationTable localisation, LeaderImport import)
    {
        var tags = CountryTags(files, import);
        var recruiters = Recruiters(files, import);
        var traitIds = import.Traits.Select(trait => trait.ID).ToHashSet(StringComparer.Ordinal);
        var admirals = new Dictionary<string, AdmiralDto>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/characters", "*.txt"))
        {
            var fileTag = Path.GetFileNameWithoutExtension(path);

            foreach (var character in Parse(files, path, import)?.GetAll("characters").Where(value => value.Block is not null)
                         .SelectMany(value => value.Block!.KeyedEntries) ?? [])
            {
                if (character.Value.Block?.GetBlock("navy_leader") is not { } leader)
                {
                    continue;
                }

                var id = character.Key!;
                var nameKey = character.Value.Block.GetString("name");
                var prefix = id.Split('_')[0];
                var admiral = new AdmiralDto
                {
                    ID = id,
                    Name = nameKey is null ? localisation.Get(id) ?? id : localisation.Get(nameKey) ?? nameKey,
                    Country = recruiters.GetValueOrDefault(id) ?? (tags.Contains(prefix) ? prefix : tags.Contains(fileTag) ? fileTag : null),
                    Skill = Skill(leader, "skill"),
                    AttackSkill = Skill(leader, "attack_skill"),
                    DefenseSkill = Skill(leader, "defense_skill"),
                    ManeuveringSkill = Skill(leader, "maneuvering_skill"),
                    CoordinationSkill = Skill(leader, "coordination_skill")
                };

                foreach (var trait in Words(leader.GetAll("traits")))
                {
                    if (traitIds.Contains(trait))
                    {
                        admiral.Traits.Add(trait);
                    }
                    else
                    {
                        import.Warnings.Add($"admiral '{id}' has trait '{trait}', which is not a navy trait; it is left out.");
                    }
                }

                if (admirals.ContainsKey(id))
                {
                    import.Warnings.Add($"character '{id}' is defined twice; the later one ({files.RelativePath(path)}) is used.");
                }

                admirals[id] = admiral;
            }
        }

        import.Admirals.AddRange(admirals.Values);
    }

    // [PDX] A navy_leader block without a skill key has level 1.
    private static int Skill(PdxBlock leader, string key)
    {
        return (int)(leader.GetDouble(key) ?? 1);
    }

    /// <summary>
    /// [PDX] The country whose history file (<c>history/countries/TAG - Name.txt</c>) recruits each character
    /// (<c>recruit_character</c>, at any depth, dated blocks included); the first one wins.
    /// </summary>
    private static Dictionary<string, string> Recruiters(GameFileSystem files, LeaderImport import)
    {
        var recruiters = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("history/countries", "*.txt"))
        {
            var tag = Path.GetFileName(path).Split(' ', '-')[0].Trim();

            foreach (var character in Recruited(Parse(files, path, import)))
            {
                recruiters.TryAdd(character, tag);
            }
        }

        return recruiters;
    }

    private static IEnumerable<string> Recruited(PdxBlock? block)
    {
        foreach (var entry in block?.Entries ?? [])
        {
            if (entry.Key == "recruit_character" && entry.Value.Scalar is { } character)
            {
                yield return character;
            }

            foreach (var nested in Recruited(entry.Value.Block))
            {
                yield return nested;
            }
        }
    }

    /// <summary>The country tags of <c>common/country_tags</c> (not the dynamic ones).</summary>
    private static HashSet<string> CountryTags(GameFileSystem files, LeaderImport import)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/country_tags", "*.txt"))
        {
            var root = Parse(files, path, import);

            if (root is not null && root.GetBool("dynamic_tags") != true)
            {
                tags.UnionWith(root.KeyedEntries.Where(entry => entry.Value.Scalar is not null && entry.Key!.Length == 3).Select(entry => entry.Key!));
            }
        }

        return tags;
    }

    private static LeaderAdjusterDto Adjuster(PdxBlock block)
    {
        return new LeaderAdjusterDto
        {
            Attack = block.GetDouble("attack") ?? 0,
            Defence = block.GetDouble("defence") ?? 0,
            Movement = block.GetDouble("movement") ?? 0
        };
    }

    // Repeated keys add up, as the game's modifier sums do.
    private static Dictionary<string, double> Numbers(PdxBlock? block)
    {
        var numbers = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var entry in block?.KeyedEntries ?? [])
        {
            if (entry.Value.TryGetDouble(out var value))
            {
                numbers[entry.Key!] = numbers.GetValueOrDefault(entry.Key!) + value;
            }
        }

        return numbers;
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

    private static PdxBlock? Parse(GameFileSystem files, string path, LeaderImport import)
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
}
