using System.Text.RegularExpressions;
using NavySimulator.Engine;
using NavySimulator.Engine.Battles;
using NavySimulator.GameData.Contracts;
using NavySimulator.GameData.Paradox;

namespace NavySimulator.GameData.Import;

/// <summary>
/// S-NATIONAL: reads what countries' history files (<c>history/countries</c>) and focus trees (<c>common/national_focus</c>)
/// give their navies: the national spirits (<c>common/ideas</c>, outside the spirit slots) and dynamic modifiers
/// (<c>common/dynamic_modifiers</c>) they add, the variables those modifiers read, and the effect statements that change
/// them (S-NATIONAL-EFFECT), following scripted effects (<c>common/scripted_effects</c>). Only content with effects the
/// simulator models is kept.
/// </summary>
public static partial class NationImporter
{
    /// <summary>The countries imported when none are named.</summary>
    public static readonly IReadOnlyList<string> DefaultCountries = ["USA", "JAP"];

    private static readonly HashSet<string> SpiritIdeaTypes = new(["navy_spirit", "air_spirit"], StringComparer.Ordinal);

    // Keys of a dynamic modifier that are not modifiers.
    private static readonly HashSet<string> DynamicModifierNonModifierKeys = new(
        ["icon", "enable", "remove_trigger", "attacker_modifier", "valid_relation_trigger", "custom_modifier_tooltip"], StringComparer.Ordinal);

    // Idea keys run once when the idea is added or removed; the simulator does not follow them.
    private static readonly HashSet<string> IdeaOneTimeKeys = new(["on_add", "on_remove"], StringComparer.Ordinal);

    // [ASSUMED] Words in an equipment or sub-unit bonus target that mark it as naval (the bonus can reach a ship or plane).
    private static readonly string[] NavalTargetWords =
    [
        "ship", "hull", "carrier", "submarine", "cruiser", "destroyer", "battleship", "naval", "navy", "torpedo", "screen",
        "capital", "cv_", "_cv"
    ];

    private const int MaxScriptedEffectDepth = 10;

    public static NationImport Read(GameFileSystem files, IEnumerable<string>? countries = null, string language = "english")
    {
        var import = new NationImport();
        var localisation = LocalisationTable.Load(files, language);
        var tags = (countries ?? DefaultCountries).Select(tag => tag.Trim().ToUpperInvariant()).Where(tag => tag.Length > 0).Distinct().ToList();
        var ideas = ReadIdeas(files, import);
        var dynamicModifiers = ReadTopLevel(files, "common/dynamic_modifiers", import);
        var walker = new EffectWalker(ReadTopLevel(files, "common/scripted_effects", import), tags);
        var (trees, sharedFocuses) = ReadFocusTrees(files, import);
        var collected = tags.ToDictionary(tag => tag, _ => new Collected(), StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            var history = files.GetFiles("history/countries", "*.txt")
                .FirstOrDefault(path => HistoryFileTag(Path.GetFileName(path)) == tag);

            if (history is null)
            {
                import.Warnings.Add($"{tag} has no history file in history/countries.");
            }
            else if (Parse(files, history, import) is { } root)
            {
                walker.WalkHistory(root, tag, (scope, effect) => collected[scope].Add(effect, scope == tag ? "history" : $"{tag} history", isOwn: scope == tag));
            }

            var tree = trees.Where(item => item.Weight(tag) > 0).MaxBy(item => item.Weight(tag));

            if (tree is null)
            {
                import.Warnings.Add($"{tag} has no focus tree in common/national_focus.");
                continue;
            }

            collected[tag].FocusTree = tree.ID;

            foreach (var focus in tree.Focuses(sharedFocuses))
            {
                var id = focus.GetString("id") ?? string.Empty;
                var dto = new NationalFocusDto
                {
                    ID = id,
                    Name = localisation.Get(id),
                    MutuallyExclusive = focus.GetAll("mutually_exclusive").Where(value => value.Block is not null)
                        .SelectMany(value => value.Block!.GetAll("focus").Select(item => item.Scalar))
                        .OfType<string>()
                        .Distinct(StringComparer.Ordinal)
                        .ToList()
                };

                if (focus.GetBlock("completion_reward") is { } reward)
                {
                    walker.Walk(reward, tag, (scope, effect) =>
                    {
                        if (scope == tag)
                        {
                            dto.Effects.Add(effect);
                        }

                        collected[scope].Add(effect, scope == tag ? $"focus:{id}" : $"{tag} focus:{id}", isOwn: scope == tag);
                    });
                }

                collected[tag].Focuses.Add(dto);
            }
        }

        foreach (var tag in tags)
        {
            import.Nations.Add(BuildNation(tag, tags, collected[tag], ideas, dynamicModifiers, localisation));
        }

        return import;
    }

    /// <summary>
    /// S-NATIONAL [ASSUMED]: the nation with what combat uses: national spirits its own history or focuses add, another
    /// imported country's scripts give it, whose <c>allowed</c> trigger or ID prefix names it, or with a targeted modifier
    /// against another imported country (A Day of Infamy against JAP); not one targeted against itself. Dynamic modifiers
    /// its history or focuses add; the effects on those and on the variables those modifiers read; and the focuses with
    /// such an effect, other than adding a dynamic modifier the history already adds.
    /// </summary>
    private static NationDto BuildNation(
        string tag,
        IReadOnlyList<string> tags,
        Collected collected,
        IReadOnlyDictionary<string, IdeaEntry> ideas,
        IReadOnlyDictionary<string, PdxBlock> dynamicModifiers,
        LocalisationTable localisation)
    {
        var nation = new NationDto { ID = tag, Name = localisation.Get(tag), FocusTree = collected.FocusTree };
        var spiritSources = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void AddSource(string idea, string source)
        {
            if (!spiritSources.TryGetValue(idea, out var sources))
            {
                spiritSources[idea] = sources = [];
            }

            if (!sources.Contains(source))
            {
                sources.Add(source);
            }
        }

        foreach (var (idea, source) in collected.AddedIdeas)
        {
            AddSource(idea, source);
        }

        foreach (var idea in ideas.Values)
        {
            var against = idea.Block.GetAll("targeted_modifier").Select(value => value.Block?.GetString("tag")).OfType<string>().ToList();

            if (AllowedFor(idea.Block, tag))
            {
                AddSource(idea.ID, "allowed");
            }
            else if (idea.ID.StartsWith($"{tag}_", StringComparison.Ordinal) && !against.Contains(tag))
            {
                AddSource(idea.ID, "id");
            }

            foreach (var enemy in against.Where(enemy => enemy != tag && tags.Contains(enemy)))
            {
                AddSource(idea.ID, $"against:{enemy}");
            }
        }

        foreach (var (id, sources) in spiritSources)
        {
            if (ideas.TryGetValue(id, out var idea) && ReadSpirit(idea, localisation) is { } spirit && IsRelevant(spirit) &&
                spirit.TargetedModifiers.All(targeted => targeted.Tag != tag || sources.Any(source => source.StartsWith("focus:", StringComparison.Ordinal) || source == "history")))
            {
                spirit.Sources = sources;
                nation.Spirits.Add(spirit);
            }
        }

        foreach (var id in collected.OwnEffects.Where(effect => effect.Effect == "add_dynamic_modifier").Select(effect => effect.Target).Distinct(StringComparer.Ordinal))
        {
            if (dynamicModifiers.TryGetValue(id, out var block) && ReadDynamicModifier(id, block, localisation) is { } modifier && IsRelevant(modifier))
            {
                nation.DynamicModifiers.Add(modifier);
            }
        }

        var spirits = nation.Spirits.Select(spirit => spirit.ID).ToHashSet(StringComparer.Ordinal);
        var modifiers = nation.DynamicModifiers.Select(modifier => modifier.ID).ToHashSet(StringComparer.Ordinal);
        var variables = nation.DynamicModifiers
            .SelectMany(modifier => modifier.VariableModifiers.Where(entry => IsRelevantKey(entry.Key)).Select(entry => entry.Value))
            .ToHashSet(StringComparer.Ordinal);

        bool Kept(NationalEffectDto effect) => effect.Effect switch
        {
            "add_ideas" or "remove_ideas" => spirits.Contains(effect.Target),
            "add_dynamic_modifier" or "remove_dynamic_modifier" => modifiers.Contains(effect.Target),
            _ => variables.Contains(effect.Target)
        };

        nation.History.AddRange(collected.History.Where(Kept));
        var historyModifiers = nation.History.Where(effect => effect.Effect == "add_dynamic_modifier" && effect.Condition is null).Select(effect => effect.Target).ToHashSet(StringComparer.Ordinal);

        foreach (var focus in collected.Focuses)
        {
            focus.Effects = focus.Effects.Where(Kept).ToList();

            if (focus.Effects.Any(effect => effect.Effect != "add_dynamic_modifier" || !historyModifiers.Contains(effect.Target)))
            {
                nation.Focuses.Add(focus);
            }
        }

        return nation;
    }

    private static NationalSpiritDto? ReadSpirit(IdeaEntry idea, LocalisationTable localisation)
    {
        var effects = DoctrineImporter.ReadEffects(idea.Block, DoctrineImporter.IdeaNonEffectKeys);
        effects.Scripted.RemoveAll(key => key == "targeted_modifier" || IdeaOneTimeKeys.Contains(key));
        var spirit = new NationalSpiritDto
        {
            ID = idea.ID,
            Name = PlainName(localisation.Get(idea.Block.GetString("name") ?? idea.ID) ?? localisation.Get(idea.ID)),
            Category = idea.Category,
            Effects = effects
        };

        foreach (var targeted in idea.Block.GetAll("targeted_modifier").Where(value => value.Block is not null))
        {
            var block = targeted.Block!;
            spirit.TargetedModifiers.Add(new TargetedModifierDto
            {
                Tag = block.GetString("tag") ?? string.Empty,
                Modifiers = block.KeyedEntries.Where(entry => entry.Key != "tag" && entry.Value.TryGetDouble(out _))
                    .GroupBy(entry => entry.Key!, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value.TryGetDouble(out var value) ? value : 0), StringComparer.Ordinal)
            });
        }

        return spirit;
    }

    private static DynamicModifierDto ReadDynamicModifier(string id, PdxBlock block, LocalisationTable localisation)
    {
        var modifier = new DynamicModifierDto { ID = id, Name = localisation.Get(id) };

        foreach (var entry in block.KeyedEntries.Where(entry => !DynamicModifierNonModifierKeys.Contains(entry.Key!) && entry.Value.Block is null))
        {
            if (entry.Value.TryGetDouble(out var value))
            {
                modifier.Modifiers[entry.Key!] = modifier.Modifiers.GetValueOrDefault(entry.Key!) + value;
            }
            else if (entry.Value.Scalar is { } variable)
            {
                modifier.VariableModifiers[entry.Key!] = variable;
            }
        }

        return modifier;
    }

    // S-NATIONAL [ASSUMED]: what the simulator models (BattleParticipant.IsModelledCountryModifier), a ship or plane stat
    // bonus for a naval target, or a naval targeted modifier.
    private static bool IsRelevant(NationalSpiritDto spirit)
    {
        return spirit.Effects.CountryModifiers.Keys.Any(IsRelevantKey) ||
               spirit.Effects.UnitBonuses.Any(entry => IsNavalTarget(entry.Key) && entry.Value.Keys.Any(key => IsCombatStat(key) || PlaneBonus.StatKeys.Contains(key))) ||
               spirit.Effects.EquipmentBonuses.Any(entry => IsNavalTarget(entry.Key) && entry.Value.Keys.Any(IsCombatStat)) ||
               spirit.TargetedModifiers.Any(targeted => targeted.Modifiers.Keys.Any(key => key.StartsWith("navy_", StringComparison.Ordinal) || key.StartsWith("naval_", StringComparison.Ordinal)));
    }

    private static bool IsRelevant(DynamicModifierDto modifier)
    {
        return modifier.Modifiers.Keys.Concat(modifier.VariableModifiers.Keys).Any(IsRelevantKey);
    }

    private static bool IsRelevantKey(string key)
    {
        return BattleParticipant.IsModelledCountryModifier(key);
    }

    private static bool IsCombatStat(string key)
    {
        return key != "build_cost_ic" && ShipStatKeys.ByGameKey.ContainsKey(key);
    }

    private static bool IsNavalTarget(string target)
    {
        return target.StartsWith("category_", StringComparison.Ordinal) || NavalTargetWords.Any(word => target.Contains(word, StringComparison.Ordinal));
    }

    // An allowed trigger naming the country: tag = X or original_tag = X, at any depth.
    private static bool AllowedFor(PdxBlock idea, string tag)
    {
        static bool Names(PdxBlock block, string tag) => block.KeyedEntries.Any(entry =>
            (entry.Key is "tag" or "original_tag" && entry.Value.Scalar == tag) || (entry.Value.Block is { } inner && Names(inner, tag)));
        return idea.GetBlock("allowed") is { } allowed && Names(allowed, tag);
    }

    // Scripted localisation ("Decisive Battle Against [?JAP.target.GetNameDef]") needs the game's state; it becomes "…".
    private static string? PlainName(string? name)
    {
        return name is null ? null : Regex.Replace(ScriptedLocalisation().Replace(name, "…"), " {2,}", " ").Trim();
    }

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex ScriptedLocalisation();

    // "JAP - Japan.txt" -> "JAP".
    private static string HistoryFileTag(string fileName)
    {
        var index = fileName.IndexOf('-');
        return (index < 0 ? Path.GetFileNameWithoutExtension(fileName) : fileName[..index]).Trim().ToUpperInvariant();
    }

    /// <summary>The ideas outside the spirit slots (<c>common/idea_tags</c> types navy_spirit, air_spirit); a later definition wins.</summary>
    private static Dictionary<string, IdeaEntry> ReadIdeas(GameFileSystem files, NationImport import)
    {
        var spiritCategories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/idea_tags", "*.txt"))
        {
            foreach (var entry in Parse(files, path, import)?.GetAll("idea_categories").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries) ?? [])
            {
                if (entry.Value.Block?.GetString("type") is { } type && SpiritIdeaTypes.Contains(type))
                {
                    spiritCategories.Add(entry.Key!);
                }
            }
        }

        var ideas = new Dictionary<string, IdeaEntry>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles("common/ideas", "*.txt"))
        {
            foreach (var category in Parse(files, path, import)?.GetAll("ideas").Where(value => value.Block is not null).SelectMany(value => value.Block!.KeyedEntries) ?? [])
            {
                if (spiritCategories.Contains(category.Key!) || category.Value.Block is null)
                {
                    continue;
                }

                foreach (var idea in category.Value.Block.KeyedEntries.Where(entry => entry.Value.Block is not null))
                {
                    ideas[idea.Key!] = new IdeaEntry(idea.Key!, category.Key!, idea.Value.Block!);
                }
            }
        }

        return ideas;
    }

    /// <summary>Every top-level <c>id = { ... }</c> entry of a folder's effective files; a later definition wins.</summary>
    private static Dictionary<string, PdxBlock> ReadTopLevel(GameFileSystem files, string folder, NationImport import)
    {
        var entries = new Dictionary<string, PdxBlock>(StringComparer.Ordinal);

        foreach (var path in files.GetFiles(folder, "*.txt", recursive: true))
        {
            foreach (var entry in Parse(files, path, import)?.KeyedEntries.Where(entry => entry.Value.Block is not null) ?? [])
            {
                entries[entry.Key!] = entry.Value.Block!;
            }
        }

        return entries;
    }

    private static (List<FocusTree> Trees, List<PdxBlock> SharedFocuses) ReadFocusTrees(GameFileSystem files, NationImport import)
    {
        var trees = new List<FocusTree>();
        var shared = new List<PdxBlock>();

        foreach (var path in files.GetFiles("common/national_focus", "*.txt"))
        {
            var root = Parse(files, path, import);

            foreach (var tree in root?.GetAll("focus_tree").Select(value => value.Block).OfType<PdxBlock>() ?? [])
            {
                trees.Add(new FocusTree(tree));
            }

            shared.AddRange(root?.GetAll("shared_focus").Select(value => value.Block).OfType<PdxBlock>() ?? []);
        }

        return (trees, shared);
    }

    private static PdxBlock? Parse(GameFileSystem files, string path, NationImport import)
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

    private sealed record IdeaEntry(string ID, string Category, PdxBlock Block);

    /// <summary>A focus tree: its ID, the country weights of its <c>country</c> block, its focuses and shared focus references.</summary>
    private sealed class FocusTree(PdxBlock block)
    {
        public string ID { get; } = block.GetString("id") ?? string.Empty;

        /// <summary>
        /// [PDX] The tree a country gets is the one whose <c>country</c> block weighs it highest: here the <c>add</c> of
        /// modifiers that name its <c>tag</c> or <c>original_tag</c>.
        /// </summary>
        public double Weight(string tag)
        {
            var country = block.GetBlock("country");
            return country?.GetAll("modifier").Select(value => value.Block).OfType<PdxBlock>()
                .Where(modifier => modifier.GetString("tag") == tag || modifier.GetString("original_tag") == tag)
                .Sum(modifier => modifier.GetDouble("add") ?? 0) ?? 0;
        }

        /// <summary>
        /// The tree's focuses in file order, then its shared focuses [PDX]: each one it names and every shared focus whose
        /// prerequisites lead to one of those.
        /// </summary>
        public IEnumerable<PdxBlock> Focuses(IReadOnlyList<PdxBlock> sharedFocuses)
        {
            var own = block.GetAll("focus").Select(value => value.Block).OfType<PdxBlock>().ToList();
            var included = block.GetAll("shared_focus").Select(value => value.Scalar).OfType<string>().ToHashSet(StringComparer.Ordinal);
            bool changed;

            do
            {
                changed = false;

                foreach (var focus in sharedFocuses.Where(focus => focus.GetString("id") is { } id && !included.Contains(id)))
                {
                    var needs = focus.GetAll("prerequisite").Select(value => value.Block).OfType<PdxBlock>()
                        .SelectMany(prerequisite => prerequisite.GetAll("focus").Select(value => value.Scalar));

                    if (needs.Any(id => id is not null && included.Contains(id)))
                    {
                        included.Add(focus.GetString("id")!);
                        changed = true;
                    }
                }
            }
            while (changed);

            return own.Concat(sharedFocuses.Where(focus => focus.GetString("id") is { } id && included.Contains(id)).DistinctBy(focus => focus.GetString("id")));
        }
    }

    /// <summary>The effects found for one country: its own (history, focuses) and ideas any imported country's scripts give it.</summary>
    private sealed class Collected
    {
        public string? FocusTree { get; set; }
        public List<NationalEffectDto> History { get; } = [];
        public List<NationalFocusDto> Focuses { get; } = [];
        public List<NationalEffectDto> OwnEffects { get; } = [];
        public List<(string Idea, string Source)> AddedIdeas { get; } = [];

        public void Add(NationalEffectDto effect, string source, bool isOwn)
        {
            if (isOwn)
            {
                OwnEffects.Add(effect);

                if (source == "history")
                {
                    History.Add(effect);
                }
            }

            if (effect.Effect == "add_ideas")
            {
                AddedIdeas.Add((effect.Target, source));
            }
        }
    }

    /// <summary>
    /// S-NATIONAL-EFFECT [PDX + ASSUMED]: walks effect script for the statements the simulator follows. An <c>if</c> body is
    /// taken (its effects carry the <c>limit</c> as condition) and its <c>else_if</c>/<c>else</c> branches are not;
    /// <c>hidden_effect</c> and scripted effects are followed; a block keyed by an imported country's tag switches the
    /// scope to it; other scopes, random lists and tooltips are skipped. Variables with a scope or a variable operand are
    /// skipped.
    /// </summary>
    private sealed partial class EffectWalker(IReadOnlyDictionary<string, PdxBlock> scriptedEffects, IReadOnlyList<string> tags)
    {
        private static readonly Dictionary<string, string> VariableEffects = new(StringComparer.OrdinalIgnoreCase)
        {
            ["set_variable"] = "set_variable",
            ["add_to_variable"] = "add_to_variable",
            ["subtract_from_variable"] = "subtract_from_variable",
            ["multiply_variable"] = "multiply_variable"
        };

        // S-NATIONAL-HISTORY [PDX]: dated history blocks apply when the game starts on or after their date; the 1936
        // bookmark starts on 1936.1.1.
        private static readonly DateOnly HistoryStart = new(1936, 1, 1);

        public void WalkHistory(PdxBlock root, string tag, Action<string, NationalEffectDto> emit)
        {
            foreach (var entry in root.KeyedEntries)
            {
                if (DateKey().Match(entry.Key!) is { Success: true } date)
                {
                    var day = new DateOnly(int.Parse(date.Groups[1].Value), int.Parse(date.Groups[2].Value), int.Parse(date.Groups[3].Value));

                    if (day <= HistoryStart && entry.Value.Block is { } block)
                    {
                        Walk(block, tag, emit);
                    }

                    continue;
                }

                WalkEntry(entry, tag, null, 0, emit);
            }
        }

        public void Walk(PdxBlock block, string tag, Action<string, NationalEffectDto> emit)
        {
            WalkBlock(block, tag, null, 0, emit);
        }

        private void WalkBlock(PdxBlock block, string scope, string? condition, int depth, Action<string, NationalEffectDto> emit)
        {
            foreach (var entry in block.KeyedEntries)
            {
                WalkEntry(entry, scope, condition, depth, emit);
            }
        }

        private void WalkEntry(PdxEntry entry, string scope, string? condition, int depth, Action<string, NationalEffectDto> emit)
        {
            var key = entry.Key!;
            var block = entry.Value.Block;

            void Emit(string effect, string target, double? value = null, int? days = null)
            {
                emit(scope, new NationalEffectDto { Effect = effect, Target = target, Value = value, Condition = condition, Days = days });
            }

            switch (key.ToLowerInvariant())
            {
                case "add_ideas":
                    Words(entry.Value).ForEach(idea => Emit("add_ideas", idea));
                    break;
                case "remove_ideas":
                    Words(entry.Value).ForEach(idea => Emit("remove_ideas", idea));
                    break;
                case "add_timed_idea" when block?.GetString("idea") is { } timed:
                    Emit("add_ideas", timed, days: (int?)block.GetDouble("days"));
                    break;
                case "swap_ideas" when block is not null:
                    if (block.GetString("remove_idea") is { } removed)
                    {
                        Emit("remove_ideas", removed);
                    }

                    if (block.GetString("add_idea") is { } added)
                    {
                        Emit("add_ideas", added);
                    }

                    break;
                case "add_dynamic_modifier" when block?.GetString("modifier") is { } modifier && block.Get("scope") is null:
                    Emit("add_dynamic_modifier", modifier, days: (int?)block.GetDouble("days"));
                    break;
                case "remove_dynamic_modifier" when block?.GetString("modifier") is { } modifier:
                    Emit("remove_dynamic_modifier", modifier);
                    break;
                case var name when VariableEffects.TryGetValue(name, out var effect) && block is not null:
                    foreach (var (variable, value) in Variables(block))
                    {
                        Emit(effect, variable, value);
                    }

                    break;
                case "if" when block is not null:
                    var limit = block.Get("limit") is { } trigger ? DoctrineImporter.Text(trigger) : null;
                    var inner = condition is null ? limit : limit is null ? condition : $"{condition} and {limit}";

                    foreach (var statement in block.KeyedEntries.Where(statement => statement.Key != "limit"))
                    {
                        WalkEntry(statement, scope, inner, depth, emit);
                    }

                    break;
                case "hidden_effect" when block is not null:
                    WalkBlock(block, scope, condition, depth, emit);
                    break;
                default:
                    if (block is not null && tags.Contains(key))
                    {
                        WalkBlock(block, key, condition, depth, emit);
                    }
                    else if (entry.Value.Scalar is "yes" && depth < MaxScriptedEffectDepth && scriptedEffects.TryGetValue(key, out var scripted))
                    {
                        WalkBlock(scripted, scope, condition, depth + 1, emit);
                    }

                    break;
            }
        }

        // { var = name value = 1 } or { name = 1 }; tooltips, scoped names and variable operands are skipped.
        private static IEnumerable<(string Variable, double Value)> Variables(PdxBlock block)
        {
            if (block.GetString("var") is { } variable)
            {
                if (block.Get("value") is { } operand && operand.TryGetDouble(out var value) && IsPlainName(variable))
                {
                    yield return (variable, value);
                }

                yield break;
            }

            foreach (var entry in block.KeyedEntries.Where(entry => entry.Key != "tooltip"))
            {
                if (entry.Value.TryGetDouble(out var value) && IsPlainName(entry.Key!))
                {
                    yield return (entry.Key!, value);
                }
            }
        }

        private static bool IsPlainName(string name)
        {
            return name.IndexOfAny(['.', '@', '^', ':', '?']) < 0;
        }

        private static List<string> Words(PdxValue value)
        {
            return (value.Block is not null ? value.Block.BareValues.Select(bare => bare.Scalar) : [value.Scalar])
                .OfType<string>()
                .Where(word => word.Length > 0)
                .ToList();
        }

        [GeneratedRegex(@"^(\d{1,4})\.(\d{1,2})\.(\d{1,2})$")]
        private static partial Regex DateKey();
    }
}
