# hoi4.exe lookup index

| | |
|---|---|
| Build | 1.19.3.0 "Operation Postern" |
| Executable SHA-256 | `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5` |
| Addresses | RVAs, image base 0x140000000. Valid for this build only. |

Built only from the [findings](findings/README.md). Nothing here is new evidence: follow the link for pseudocode, evidence addresses and confidence. "?" means the source finding is unsure; the confidence word is the finding's own.

**How to add to this index.** Check this index before decoding anything in hoi4.exe. Every decoding session adds each function, struct field, modifier id and define read site it decoded here (one row each, sorted, with a link), and puts the pseudocode, evidence addresses and confidence in a findings file (`findings/<SPEC-ID>-<name>.md`, from [finding-template.md](finding-template.md)). If a new reading contradicts a row, keep both meanings, flag the row "**disagrees**", and settle it in [findings/README.md][README/disagree]. After a game patch, re-run `tools/re/` and re-verify before trusting any row.

File keys: HOUR = [S-HOUR][HOUR], ACT = [S-ACTIVATE][ACT], TGT = [S-TARGET][TGT], HIT = [S-HIT][HIT], DMG = [S-DMG][DMG], POS = [S-POS][POS], XP = [S-XP][XP], AIR = [A-air-combat][AIR], WTH = [S-WEATHER][WTH], LOG = [in-game logging][LOG], DOC = [S-DOCTRINE][DOC], STATS = [S-STATS][STATS], STC = [S-STATS-COUNTRY][STC], MIOI = [S-MIO-INCLUDE][MIOI], MIO = [S-MIO-MATCH][MIO], ADM = [S-ADMIRAL][ADM], POTF = [S-POTF][POTF], WINGS = [A-WINGS][WINGS], MSAVE = [A-MISSION-SAVE][MSAVE], ACE = [A-ACE][ACE], REG = [A-REGION][REG], NIGHT = [S-NIGHT darkness][NIGHT], TER = [S-TERRAIN][TER], WTT = [S-WEATHER terms][WTT], WSIM = [S-WEATHER-REGION][WSIM].

## 1. Functions

Sorted by RVA. "chunk" = a non-contiguous part of the function above it, as the finding names it.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x36f0 | console command registration (`weather`, `enable_weather`, `disable_weather`, `reloadweather`, `set_weather`, …) | S-WEATHER | [WTH/cmd] |
| 0x120540 | CNavyLeader vt+0x78: returns 0 (leaf) | S-ADMIRAL | [ADM/map] |
| 0x1772c0 | date → 0-based day of the month (365-day years, month lengths at 0x3085210) | S-NIGHT | [NIGHT/sun] |
| 0x1a5630 | "Resetting game": clears HasGameStarted (gamestate+0xa39) via 0x1edff0 | S-MANPOWER | [XP/mp] |
| 0x1aeb50 | feature (DLC) flag by index (jump table): 0xf = bit 4 of 0x332f248 gates pride of the fleet (eligibility, history flag, AI); 0x3a gates the captain XP of 0xc31840 | S-POTF | [POTF/assign] |
| 0x1d4810 | `gamestate.daily`: runs `country.daily` 0x6e76a0 for every country (0x1d59eb) | S-MANPOWER | [XP/mp] |
| 0x1dd370 | gamestate tick: date += 1 h, hourly update 0x1df400, then `gamestate.daily` 0x1d4810 only if the day changed (0x1de3f5) | S-MANPOWER, S-HOUR | [XP/mp] |
| 0x1df400 | gamestate hourly update (`DoCountryHourlyUpdates`); calls the combat manager hourly update 0xbb8ae0 (0x1e0103) | S-HOUR | [XP/mp] |
| 0x1e0610 | gamestate init (`InitGameState`): clears gamestate+0xa39 (0x1e063a) | S-MANPOWER | [XP/mp] |
| 0x1edff0 | `SetGameStarted(gamestate, value)`: writes `value` to gamestate+0xa39; 1 only from "on_startup", 0 from reset, savegame load and the frontend | S-MANPOWER | [XP/mp] |
| 0x22e040 | console `weather` / `enable_weather` / `disable_weather` handler (mode 0 toggle, 1 on, else off) | S-WEATHER | [WTH/cmd] |
| 0x243be0 | console `combat_analyzer`: loads two OOBs, runs hourly updates until the combat ends; walks armies only (land-only) | — | [LOG] |
| 0x25d2e0 | console `gamespeed <0-5>` handler (0 pauses) | — | [LOG] |
| 0x27e220 | console `run <file>`: feeds each line through the console input | — | [LOG] |
| 0x2874b0 | console `set_weather` (clear, rain, storm, snow, blizzard, sandstorm) for the selected province's region: setters 0xf20b60 / 0xf20b90 / 0xf20c20 / 0xf20bf0 / 0xf20b30 / 0xf20bc0 (flags +0x108 rain, +0x110 storm, +0x118 snow, +0x120 blizzard, +0x128 sandstorm) | S-WEATHER | [WSIM/roll] |
| 0x533c60 | date: move to day-of-month `n` (0-based) of the same month | S-NIGHT | [NIGHT/sun] |
| 0x535c40 | resolves member+0x10 convoy reference | S-TARGET | [TGT/bb] |
| 0x5570f0 | add a modifier container into another (copy into a cleared one) | — | [STC/leader] |
| 0x5574b0 | static modifier struct accessor (global 0x3330098, after 0x558710); same struct as 0x55b160 | S-POTF | [POTF/sink] |
| 0x5574d0 | modifier registration call `(token, id, name, flags, a5, a6, a7, a8, a9)`: flags → `def+0x60`, a5 → +0x74, a6 → +0x64, strings → +0x00/+0x20/+0x40, a9 → +0x68, id → +0x6c, token → +0x70 | S-ADMIRAL-MOD | [HIT/map], [DMG/conv], [ADM/scale] |
| 0x557840 | add a modifier holder × scale into a container | — | [STC/leader] |
| 0x557aa0 | add modifier block scaled by `s` | S-XP-EFFECT | [XP/effect] |
| 0x5583a0 | clear a modifier container | — | [STC/leader] |
| 0x55b160 | static modifier struct accessor (global 0x3330098; leaf). XP's "global block in one branch" is `pride_of_the_fleet` (+0xb0), read for the PotF ship at 0xc3b347 / 0xc3b36d | S-XP-GAIN | [XP/gain], [POTF/xp] |
| 0x55e360 | modifier container `Get(container, out, id)` | all | [STC/names] |
| 0x55f7f0 | modifier-definition registration (`MODIFIER_*` → id) | — | [HIT/map], [DMG/conv] |
| 0x60d830 | static modifier loader: block name → offset (`night` +0x58, `pride_of_the_fleet` +0xb0 (0x60ea80), `pride_of_the_fleet_country` +0xb8 (0x60eada), `pride_of_the_fleet_sunk_temporary` +0xc0 (0x60eb34), `screening_bonus` +0x100, …) | S-HIT-MODS, S-STATS-COMBAT | [STC/sum], [POTF/static] |
| 0x60fbd0 | scale a container's entries by a factor `(container, factor, depth)`; factor 1.0 = no-op. ~~bit 1 = scale, bit 2 = keep negatives~~ settled ([README/disagree]): flag bit 1 (positive is good) → every nonzero entry × factor; else bit 2 (neutral) → unchanged; else (positive is bad) → negative entries only; depth > 1 recurses into child containers `+0x18` | S-STATS-COUNTRY, S-ADMIRAL-MOD | [STC/leader], [ADM/scale] |
| 0x6dade0 | `CCountry::UpdateModifiers` ("country.calc_modifier", see the doctrine table) into `country+0x5b8`: adds static `pride_of_the_fleet_country` (+0xb8) while `country+0x250` resolves (0x6dba8d); `pride_of_the_fleet_sunk_temporary` (+0xc0) while the date lost (`+0x260`) is set and `days(now) − days(lost) < PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION`, then `country+0x270 = 1` (0x6dbb48..0x6dbd48) | S-POTF, S-STATS-COUNTRY | [POTF/sink] |
| 0x6e76a0 | `country.daily`: `army.daily` (slot 19 on armies), `fleets.daily` (0xd54840 per fleet, country +0x278/+0x284), `railway_guns.daily` (slot 19) | S-MANPOWER | [XP/mp] |
| 0x6f70f0 | country → `&country+0x250`, the pride-of-the-fleet ship handle (leaf); all 30 callers classified | S-STATS-COMBAT | [STATS/getter], [POTF/readers] |
| 0x6ff065 | game-start init: task-force manpower refill with force = true | S-MANPOWER | [XP/mp] |
| 0x705ce0 | CCountry ReadMember (vt+0x20; jump table at 0x709cf2 from token 0x3a9a): `pride_of_the_fleet` → handle reader 0x1b2240 into `country+0x250` (0x709d36); `pride_of_the_fleet_date_lost` → the date's persistent base `country+0x268` (0x709d4a) | S-POTF | [POTF/save] |
| 0x70ba00 | add the leader-bound entries of `country+0x15b8` to a leader's source holders | S-STATS-COUNTRY | [STC/leader] |
| 0x70c760 | pride of the fleet lost `(country)`: `country+0x260` = now (gamestate+0x468), then 0x6dade0; from the sunk-ship removal 0xd77ff0 and the scuttle 0xd76bd0 | S-POTF | [POTF/sink] |
| 0x712700 | SetPrideOfTheFleet `(country, ship or null)`: `country+0x250` = handle (null clears), then 0x6dade0; callers: ship dtor 0xc30380 (clear), ship init 0xc34280 (history flag), command Execute 0x1356850 | S-POTF | [POTF/readers] |
| 0x7191b0 | CCountry::Write (vt+0x10): `pride_of_the_fleet={ id type }` only when `country+0x250` is set (0x7192d4), then `pride_of_the_fleet_date_lost` (0x7192f7) | S-POTF | [POTF/save] |
| 0x93c1d0 | define loader (only other reference to COMBAT_MIN_DURATION) | S-END | [HOUR/end] |
| 0xa3b460 | ship reliability getter `[ship+0x328]` | S-CRIT | [HIT/crit] |
| 0xae2e50 | skill list Get(i): `list[i]` if `1 <= i < count`, else `list[0]` (null object) | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xae50f0 | `common/unit_leader` skill reader: token → list (`leader_skills` db+0xe8, attack +0x160, defense +0x1d8, planning +0x250, logistics +0x2c8, maneuvering +0x340, coordination +0x3b8) → 0xaed2c0 | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xae7d60 / 0xae7fb0 / 0xae80b0 / 0xae7d80 | skill lookup `(db, level, type)` in the attack / defense / maneuvering / coordination list → 0xae2e50 | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xae94a0 | trait → its `night` adjuster (token 0x2ea8; leaf) | S-STATS-COMBAT | [STC/leader] |
| 0xae94f0 / 0xae9510 / 0xae9530 / 0xae9500 | entry count (null object included) of the attack / defense / maneuvering / coordination list (leaf) | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xae9da0 | skill-level database lookup (level, leader type) → `leader+0xe60`; the overall-skill list db+0xe8, via 0xae2e50 | S-STATS-COUNTRY, S-ADMIRAL-SKILL | [STC/leader], [ADM/skill] |
| 0xaea5f0 | trait → its adjuster for one terrain | S-STATS-COMBAT | [STC/leader] |
| 0xaebc20 | unit leader trait parser: `modifier` +0x360, `non_shared_modifier` +0x420, `corps_commander_modifier` +0x4e0, `field_marshal_modifier` +0x5a0; inside `modifier` a terrain or `night` key becomes an adjuster (`+0x768`, 0xaeca3f..), not a modifier; `sub_unit_modifiers` → `+0x780` (0xaed09b) | S-STATS-COUNTRY, S-ADMIRAL-TRAIT | [STC/leader], [ADM/adjust] |
| 0xaed2c0 | skill block reader: `N = { … }` (integer N ≥ 1), entry +0x1b8 = N, +0x1c0 = type; `list[type]` grown to N + 1 with the null object, `list[type][N] = entry` | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xbb48f0 | country tag handle → country | — | [STC/sum] |
| 0xbb52f0 | same original tag `(tag*, tag)`: `gamestate+0x340` table entries equal | S-POTF | [POTF/assign] |
| 0xbb5490 | country tag → country index | — | [STATS/getter] |
| 0xbb8ae0 | combat manager hourly update: validity, Update, then EndHour on every combat | S-HOUR | [HOUR/chain] |
| 0xbba1b0 | remove combat | S-HOUR | [HOUR/chain] |
| 0xbd5d60 | variant dominance `naval_dominance_factor × (1 + (speed − S)/2S + (range − R)/2R + hg_attack × H + carrier_size × C)` | S-DOMINANCE | [DOM] |
| 0xbf9b90 | level = count of `UNIT_EXP_LEVELS[i] <= xp/maxXP` | S-XP-LEVELS | [XP/levels] |
| 0xbf9d90 | progress within level (UI only) | S-XP-LEVELS | [XP/levels] |
| 0xbfbff0 | unit → its combats list `unit+0x1a8` (count +0x1b4; leaf) | S-POTF | [POTF/combat] |
| 0xc00000 | task force in combat (`tf+0x1b4 > 0`) | S-XP-GAIN | [XP/gain] |
| 0xc03e00 | CUnit::RemoveCombat `(unit, combat)`: removes the combat from `unit+0x1a8`; callers: naval combat teardown 0x15c2980, leave/join 0x13e3570 / 0x13e3990 (no escape path) | S-POTF | [POTF/combat] |
| 0xc0dc90 | scoped pointer deref (asserts non-null, `pdx_scopedptr.h`); used on `ship+0x8f8` | S-XP-GAIN | [POTF/xp] |
| 0xc0f200 | captain XP gain (`gain × LEADER_EXPERIENCE_SCALE`) | S-XP-GAIN | [XP/gain] |
| 0xc0fb70 | leader: sum trait adjusters (`night` → +0x260, terrains → +0x248, sub-units → +0x128) | S-STATS-COMBAT | [STC/leader] |
| 0xc0fdb0 | cooldown days `days + int(days × B.reassignment_duration_factor (0x181))` (B only, no country term) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc10a10 | CUnitLeader vt+0x90: group-cap factor (cap vt+0x68 = `leader+0xe74`, n = vt+0x78); 1 when cap ≤ 0 or n ≤ cap | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc10ab0 | CUnitLeader vt+0x88 `(leader, out, useCorpsCap)`: unit-cap factor (cap `CORPS_COMMANDER_DIVISIONS_CAP` or vt+0x60 = `leader+0xe70`, n = vt+0x70); 1 when cap ≤ 0 or n ≤ cap, else `max(0, 1 − min(1, (n − cap)/cap))` | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc11540 | CNavyLeader vt+0xf0: add the four skill levels' `modifier` (`leader+0xf70/+0xf80/+0xf90/+0xfa0`) | S-STATS-COUNTRY | [STC/leader] |
| 0xc16d10 | CNavyLeader vt+0x70: Σ over the fleet's task forces of `0xd6eeb0(tf)+0xc` (ship count, medium) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc186d0 / 0xc18790 | CUnitLeader vt+0x68 / vt+0x60: `leader+0xe74` / `leader+0xe70` (leaf) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc18780 | `ADMIRAL_TASKFORCE_CAP` getter (leaf, ignores its argument) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc199f0 | overall skill level `[leader+0xe60]+0x1b8` (leaf) | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xc19ba0 | admiral per-sub-unit adjuster record (`leader+0x128` → adjuster list +0x130, via 0x101afc0) | S-STATS-COMBAT, S-ADMIRAL-ADJUST | [STATS/getter], [ADM/subunit] |
| 0xc19bf0 | admiral per-sub-unit modifier holder (`leader+0x128` → modifier-block list +0x148, via 0x101b5e0); only caller 0xc38760 | S-ADMIRAL-MOD | [ADM/subunit] |
| 0xc19c40 | admiral `_TerrainAdjusters[terrain+0x88]` (`leader+0x248`, 40-byte records) | S-STATS-COMBAT | [STATS/getter] |
| 0xc19fa0 | leader size efficiency vt+0x88(false) × vt+0x90 × (navy: task-force cap factor 0xd53880); both vt factors are 1 for a navy leader | S-STATS-COUNTRY, S-ADMIRAL-MOD | [STC/leader], [ADM/eff] |
| 0xc1aa20 | leader XP (BASE_LEADER_TRAIT_GAIN_XP, FIELD_MARSHAL_XP_RATIO) | S-HOUR | [HOUR/hour] |
| 0xc1afc0 | navy leader: rebuild (0xc21fc0, vt+0xe0) when `0xd53880(fleet) != leader+0xfc0` | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc1ce70 | common unit leader save writer (`name` … `skill`, `experience`, `script_id`, …, `traits`, `in_progress`, …, `legacy_id`); no fleet assignment | S-ADMIRAL-SAVE | [ADM/save] |
| 0xc1e650 / 0xc1e7c0 | CNavyLeader vt+0x100 / vt+0x108: add / remove a trait's `attack_skill`, `defense_skill`, `maneuvering_skill`, `coordination_skill` (set level = level + deficit ± add) | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xc1fd60 | start the modifier cooldown `(leader, reason, days, scaleByLeader)`: `+0xe84 = reason`, `+0xe90` start, `+0xea8` end hour (only extends) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc21280 | leader: rebuild trait adjusters (clears +0x270..+0x280, terrain and sub-unit records; → 0xc0fb70); nothing summed during a cooldown (`+0xe84`); scaled by the size efficiency 0xc19fa0 via 0x1016900 | S-STATS-COMBAT, S-ADMIRAL-ADJUST | [STC/leader], [ADM/adjust] |
| 0xc21570 | leader: combine containers (A +0x358 scaled → +0x418, B +0x298); no country term. `eff = 0xc19fa0 + B.skill_bonus_factor` (≥ 0; 0 in a cooldown), scales A and the sub-unit modifier blocks (depth 2); navy second factor 1.0 | S-STATS-COUNTRY, S-POS, S-ADMIRAL-MOD | [STC/leader], [ADM/eff] |
| 0xc21d30 | leader caps: `+0xe70 = 0`, `+0xe74 = −1`; corps commander / field marshal divisions and armies caps (+ B 0x15f / 0x160); navy keeps 0 / −1 | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc21fc0 | leader: rebuild source holders from traits, skill levels and leader-bound entries; merges the traits' sub-unit modifier blocks (0x101cc20) | S-STATS-COUNTRY | [STC/leader], [ADM/subunit] |
| 0xc283f0 | navy leader save writer: 0xc1ce70, then `attack_skill`, `defense_skill`, `maneuvering_skill`, `coordination_skill`, the four `_temp_deficit`s, `naval_headquarter`, `penalty` | S-ADMIRAL-SAVE | [ADM/save] |
| 0xc24740 / 0xc25580 / 0xc26c50 / 0xc260d0 | CArmyLeader set attack / defense / planning / logistics skill | — | [ADM/map] |
| 0xc24870 / 0xc256b0 / 0xc26200 / 0xc253b0 | CNavyLeader set attack / defense / maneuvering / coordination skill `(leader, x, rebuild)`: deficit `min(0, x − 1)`, level `max(1, x)`, definition = list[min(level, count − 1)] | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xc24c70 | leader: set owning character (`+0xf48`) | — | [STC/leader] |
| 0xc25b30 | leader → fleet `(leader, fleet, startCooldown)`: leaves the old fleet; reason-1 cooldown `UNIT_LEADER_MODIFIER_COOLDOWN_ON_GROUP_CHANGE` if it had another fleet | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc25db0 | leader's fleet set (fleet replaces its admiral): reason-1 cooldown if it had another fleet | S-ADMIRAL-MOD | [ADM/eff] |
| 0xc271b0 | SetSkillLevel (overall) `(leader, N)`: `+0xe60 = 0xae9da0(N)` if valid, else unchanged (no clamp) | S-ADMIRAL-SKILL | [ADM/skill] |
| 0xc2ffe8 | ship constructor site: XP cap = NAVY_MAX_XP | S-XP-LEVELS | [XP/levels] |
| 0xc30380 | CShip destructor body: if the ship is its owner's PotF and gamestate+0xa3a is 0, `0x712700(owner, null)` (0xc304f8) | S-POTF | [POTF/sink] |
| 0xc30cc0 | CShip scalar deleting destructor (vt+0 of 0x2957f28) → 0xc30380 | S-POTF | [POTF/sink] |
| 0xc30d00 | ship → `&tf+0x1d8` (owner tag; leaf) | S-POTF | [POTF/readers] |
| 0xc31030 | merge critical-part candidate into (part, max instances): append with base_damage_instance, or add damage_instance_per_added_module capped at max_damage_instance | S-CRIT-PART | [DMG/part] |
| 0xc31840 | add ship XP `(ship, gain fx5, mode)` (mode 0 combat, 1 mission, 2 training) → 0xc3b240; `xp = min(xp + d, NAVY_MAX_XP)`, dirty; if `d > 0` and mode ≠ 2 the captain record `ship+0x848` gets `d × CAPTAIN_EXPERIENCE_ON_SHIP_MULT × (1 + medals × CAPTAIN_EXPERIENCE_ON_SHIP_PER_MEDAL_MULT)` (feature 0x3a) | S-XP-GAIN | [XP/gain], [POTF/xp] |
| 0xc327e0 | add `holder+0x10 .Get(id)` into out (+ tooltip line) | — | [STC/sum] |
| 0xc33240 | ship dominance: variant 0x100ff30(`ship+0x40`) → 0xbd5d60 | S-DOMINANCE | [DOM] |
| 0xc333e0 | ship stat part by index (0x18 = carrier surface detection) | S-POS-DETECT | [POS/detect] |
| 0xc338a0 | ship combat XP gain per hour | S-XP-GAIN | [XP/gain], [HOUR/hour] |
| 0xc339d0 | level progress (UI only) | S-XP-LEVELS | [XP/levels] |
| 0xc33a50 | CanBecomePrideOfTheFleet: feature 0xf and (sub-unit `capital_ship` or the carrier test of 0xc3af20 inlined) and the government-in-exile tag `ship+0x804` unset, equal to the owner or of the same original tag (0xbb52f0); used by the UI button 0x1bed5f0 and the AI 0x1a68d40 | S-POTF | [POTF/assign] |
| 0xc34280 | ship init (assigns the handle id; from InitPostRead 0xc3ad50 and the deployment 0xd0fc86): consumes the history flag `ship+0x814`; if feature 0xf, logs "Setting a ship that can't become potf as potf." when the 0xc33a50 tests fail, then sets it anyway via 0x712700 | S-POTF | [POTF/assign] |
| 0xc34500 | critical part damage `(ship, &str, &org)`; true = part hit, crit mult skipped | S-CRIT-PART | [DMG/part], [HIT/crit] |
| 0xc34a50 | attack/armor getter `max(0, base × (1 + Σ))`, fully decoded: type-gated factors, night × darkness, naval terrain, admiral adjusters, pride of the fleet, fuel | S-DMG, S-STATS-COMBAT | [DMG/dmg], [STATS/getter], [POTF/combat] |
| 0xc36390 | ship experience level | S-XP-LEVELS | [XP/levels] |
| 0xc37000 (body 0xc37006) | ship hit profile | S-HIT-SHIP | [HIT/ship] |
| 0xc37180 | hit profile tooltip (UI only) | S-HIT-SHIP | [HIT/map] |
| 0xc37ae0 | manpower ratio `[+0x808]/[+0x80c]`, clamped [0,1] | S-HIT | [HIT/hit] |
| 0xc37e70 | max org | S-DMG-APPLY | [DMG/apply] |
| 0xc38760 | ship modifier sum `(out, ship, id, admiral)`: country, PotF static, experience, ship+0x8f8 object, task force, naval terrain × sub-unit, weather × factor, province, admiral +0x418 (or fleet admiral), the admiral's sub-unit block 0xc19bf0 (key `ship+0x80+0x58c`), parts × (1 + a separate full 0xc38760 call for 0x1bb with the resolved admiral, 0xc38b58), max building-module term | S-HIT-MODS, S-DMG, S-CRIT-PART, S-STATS-COUNTRY | [HIT/mods], [DMG/dmg], [DMG/part], [STC/sum], [ADM/part], [POTF/static] |
| 0xc39530 | org ratio `[ship+0x700] / maxOrg` | S-HIT, A-SORTIE | [HIT/hit], [AIR/sortie] |
| 0xc395a0 | hourly organization regain: members not fighting (state ≠ 2) gain `(SHIP_MORALE_TO_ORG_REGAIN_BASE + naval_morale) × max(0, 1 + ship+0x120 + dominance + naval_morale_factor)` up to max organization; dominance = NAVAL_DOMINANCE_ORG_RECOVERY when 0x1003f50 returns 1 for `tf+0x1dc` (else `+0x1d8`) in region `[tf+0x1f0]+0xc8` (0xc398f0..) | S-ORG-REGAIN, S-DOMINANCE | [LOG], [DOM] |
| 0xc3a470 | ship AA `(anti_air_attack + navy_anti_air_attack) × (1 + navy_anti_air_attack_factor)`; third argument is an admiral (AIR read "battle context"; settled), 0 = own fleet admiral | S-POS, A-TARGET, A-AA-REDUCE | [POS/aa], [AIR/target], [STATS/getter] |
| 0xc3a520 | armor | S-DMG-PIERCE | [HIT/pierce] |
| 0xc3a570 | heavy piercing | S-DMG-PIERCE | [HIT/pierce] |
| 0xc3a580 | heavy attack. **disagrees**: HOUR says light attack (settled: heavy) | S-COOLDOWN, S-DMG | [ACT/cooldown], [TGT/bb], [HIT/map], [HOUR/hour] |
| 0xc3a5c0 | light piercing | S-DMG-PIERCE | [HIT/pierce] |
| 0xc3a5d0 | light attack. **disagrees**: HOUR says torpedo attack (settled: light) | S-COOLDOWN, S-DMG | [TGT/bb], [HIT/map], [HOUR/hour] |
| 0xc3a740 | current max speed incl. modifiers `(ship, out, admiral)`: `speed × max(0.1, 1 + naval_speed_factor (sum with the admiral) + terrain sub-unit movement + fuel)`; no admiral adjuster | S-HIT-SHIP, S-RETREAT-SPEED, S-ADMIRAL | [HIT/ship], [ACT/speed], [ADM/speed] |
| 0xc3a890 | depth-charge attack (HOUR: "an anti-sub stat", unverified) | S-COOLDOWN, S-TARGET-DC | [ACT/cooldown], [TGT/bb], [HOUR/hour] |
| 0xc3a950 | member sub detection | S-SUB | [HOUR/hour] |
| 0xc3aa20 | sub visibility (STAT_NAVY_SUB_VISIBILITY) | S-SUB | [ACT/sub], [POS/sub] |
| 0xc3aae0 | effective surface detection | S-POS-DETECT | [POS/detect] |
| 0xc3ab80 | surface visibility `ship+0x1c8 × (1 + navy_visibility)` | S-HIT-SHIP | [STATS/getter] |
| 0xc3ac00 | torpedo attack. **disagrees**: HOUR says heavy attack (settled: torpedo) | S-COOLDOWN, S-DMG | [ACT/cooldown], [TGT/bb], [HOUR/hour] |
| 0xc3ac60 | HP ratio `[+0x6f8] / max([+0x308], 0.001)` | S-RETREAT, S-TARGET-WEIGHT, S-DMG-APPLY | [ACT/retreat], [TGT/weight], [DMG/apply] |
| 0xc3acc0 | ship's country: `tf+0x1dc` if > 0, else `tf+0x1d8` (leaf) | S-HIT-MODS, S-CRIT | [STATS/getter] |
| 0xc3ad50 | CShip InitPostRead (vt+0x40) → 0xc34280 first | S-POTF | [POTF/assign] |
| 0xc3af10 | is capital (`ship+0x628` bit 6) | S-SCREEN, A-STACK | [TGT/bb], [AIR/stack] |
| 0xc3af20 | is carrier or missile launcher (type bits 22 `carrier` / 37 `missile_launcher` + set lookup) | S-SCREEN, S-XP, A-* | [TGT/bb], [XP/effect], [AIR/stack], [STATS/getter], [POTF/assign] |
| 0xc3b0e0 | ship.IsPrideOfTheFleet: `{ship+8, +0xc}` == `country(tf+0x1d8)+0x250` (owner tag) | S-POTF | [POTF/readers] |
| 0xc3b130 | "no parts / convoy-like"? test; part damage skipped (DMG ties it to the +0x738 link) | S-CRIT-PART | [HIT/crit], [DMG/apply] |
| 0xc3b210 | is screen (bit 8) | S-SCREEN | [TGT/bb] |
| 0xc3b220 | is submarine (bit 7) | S-TARGET | [TGT/bb] |
| 0xc3b230 | type bit 39 = sub-unit type `support_ship` | S-ACTIVATE, A-STACK | [TGT/bb], [ACT/activate], [STATS/getter] |
| 0xc3b240 | XP add `(ship, out, &g15, mode, tooltip)`: `out = (g15 + fx15(flat)) × fx15(1 + factor) >> 15`; flat 0x35 / factor 0x34 from the operating country `+0x5b8` and the ship's medal holder (`[ship+0x8f8]+0x1b0`), plus static `pride_of_the_fleet` for the owner's PotF; factor + country `[ship+0x66c / +0x668 / +0x664]` by mode; no admiral, no 0xc38760 | S-XP-GAIN | [XP/gain], [POTF/xp] |
| 0xc3bde0 | per-ship step of the task-force update (XP gain, then 0xc3ce50) | S-XP-GAIN | [XP/gain] |
| 0xc3c060 | naval accident roll (from the mission update 0xfbcb90, `navalmission.cpp`): chance × ACCIDENTS_CHANCE_BALANCE_FACTOR, × NAVAL_ACCIDENT_CHANCE_REDUCTION_ON_POTF for the PotF; accident crit × NAVAL_ACCIDENT_CRITICAL_HIT_CHANCE_REDUCTION_POTF (not decoded further; not battle) | S-POTF | [POTF/readers] |
| 0xc3c5a0 | CShip ReadMember (vt+0x20): `pride_of_the_fleet` → `ship+0x814 = 1` (0xc3c8f2, value not read), `government_in_exile_tag` → `ship+0x804` (0xc3c8de) | S-POTF | [POTF/assign] |
| 0xc3ce50 | recompute ship experience modifiers when `ship+0x800` dirty | S-XP-EFFECT | [XP/effect] |
| 0xc3d090 | recompute ship stats `(ship, 1)`; calls 0x6f70f0 at 0xc3d309 and discards the result | S-CRIT-PART | [DMG/part], [POTF/readers] |
| 0xc3e0e0 | apply damage `(ship, str, org, flag)`: XP and manpower loss, HP, org | S-DMG-APPLY, S-XP-LOSS, A-DMG | [DMG/apply], [DMG/xp], [XP/loss], [HOUR/order] |
| 0xc3e4e0 | ship manpower update `(ship, refill)` | S-MANPOWER | [XP/mp] |
| 0xc3e680 | sunk check (HP ≤ 0, unless +0x738 link) | S-DMG-APPLY | [DMG/apply], [HOUR/order] |
| 0xc4d910 | planes on a carrier (sum of all wings) | S-POS, A-SORTIE | [POS/carrier], [AIR/sortie] |
| 0xc53fd0 | reads cached carrier crowd factor | A-SORTIE | [AIR/sortie] |
| 0xc54980 | port-strike external plane cap | A-EXTERNAL | [AIR/ext] |
| 0xc579f0 | carrier overcrowding factor chain | A-SORTIE | [AIR/sortie] |
| 0xc5c5c0 | fuel-related non-flying planes | A-LOSSES | [AIR/losses] |
| 0xc959b0 | equipment type `naval_dominance_factor` (`type+0x3f0`; leaf) | S-DOMINANCE | [DOM] |
| 0xc97b90 | equipment type keyword check along the archetype chain (dominance gate) | S-DOMINANCE | [DOM] |
| 0xcfe7e0 | draw manpower from national pool | S-MANPOWER | [XP/mp] |
| 0xd1b800 | reads 0x3334d70 (not COMBAT_DAMAGE_RANDOMNESS; who.py false positive) | S-DMG-RANDOM | [DMG/random] |
| 0xd23780 | targeted-modifier entry → holder +0x130 (leaf) | S-HIT-MODS, S-STATS-COMBAT | [STATS/getter] |
| 0xd53880 | admiral task-force cap factor `(fleet)`: 1 without a fleet admiral or with cap 0, else `n = 0xd55ed0`, `1 − clamp((n − cap)/cap, 0, 1)` | S-STATS-COUNTRY, S-ADMIRAL-MOD | [STC/leader], [ADM/eff] |
| 0xd54840 | fleet daily update: vtable +0x98 (DailyUpdate) on each task force (fleet +0xb8, count +0xc4) | S-MANPOWER | [XP/mp] |
| 0xd55ed0 | fleet's task forces with a non-empty ship list and `tf+0x4b8 == 0` (count) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xd582b0 | fleet member reader: `leader` (0x28b7) → `fleet+0xb0` as written, `home_base`, `automated_homebase`, task forces, … | S-ADMIRAL-SAVE | [ADM/save] |
| 0xd5a170 | fleet SetAdmiral: old admiral → 0xc25db0(old, 0); `fleet+0xb0 = new`; new → 0xc25b30(new, fleet, 1) | S-ADMIRAL-MOD | [ADM/eff] |
| 0xd5ebc0 | fleet save writer: id, `leader={ id type=4713 }` when set (`fleet+0xb0`), `name`, `color`, `icon`, `task_force`s, …, `home_base`, `automated_homebase` | S-ADMIRAL-SAVE | [ADM/save] |
| 0xd64510 | task force's fleet admiral (`tf+0x340` → 0xd50ee0; leaf) | S-STATS-COMBAT | [STC/sum] |
| 0xd67310 | weather factor `clamp(1 + country navy_weather_penalty, 0, 1)`; 0xc38760 multiplies the region block by `ship stat 0x2a (naval_weather_penalty_factor, hull base 1) − 1 + this` | S-WEATHER | [STC/sum], [WTT/sum] |
| 0xd69090 | CTaskForce::DailyUpdate (vtable slot 19): first calls 0xd7a7e0(tf, false), before and regardless of the in-combat test | S-MANPOWER | [XP/mp] |
| 0xd6b1b0 / 0xd6b590 | task-force naval-terrain attack / defence fallback when the sub-unit index is < 0: mean of 0x143e4a0 over the task force's ships (`tf+0x348`, count `+0x354`), never used for a ship | S-STATS-COMBAT, S-TERRAIN | [STATS/getter], [TER/terrain] |
| 0xd6c6f0 | task-force fuel ratio `tf+0x4f8 / tf+0x500`, ≥ 0 (leaf) | S-STATS-COMBAT | [STATS/getter] |
| 0xd6d0c0 | task force's current naval combat (first combat of type 2) | S-STATS-COMBAT | [STATS/getter] |
| 0xd6eeb0 | unit ship list | S-POS | [POS/joined] |
| 0xd6eee0 | task-force naval-terrain movement fallback (as 0xd6b1b0, for speed) | S-TERRAIN | [TER/terrain] |
| 0xd6fa50 | task-force modifiers | S-DMG | [DMG/dmg] |
| 0xd70270 | task force holds its owner's PotF: `resolve(country(tf+0x1d8)+0x250)+0x728 == tf` (UI and AI only) | S-POTF | [POTF/readers] |
| 0xd705f0 | CTaskForce update (vtable slot 18): per-ship XP | S-XP-GAIN | [XP/gain], [HOUR/hour] |
| 0xd76bd0 | scuttle a ship through its task force (from 0xd69090); the PotF counts as lost (0x70c760 at 0xd76c91) | S-POTF | [POTF/sink] |
| 0xd77ff0 | task force: remove a sunk ship; returns manpower to the pool, then refills the task force `0xd7a7e0(tf, false)` (0xd78b71). PotF (0xd78408): 0x70c760(owner), on_action `on_pride_of_the_fleet_sunk` (ROOT owner, FROM sinker, sinker tag > 0), career stat 0xcd6800; the ship object is deleted at 0xd78a64 (dtor clears `country+0x250`) | S-XP, S-MANPOWER | [DMG/xp], [LOG], [POTF/sink] |
| 0xd7a7e0 | refill task-force manpower `(tf, force)`: every ship if HasGameStarted() or force | S-MANPOWER | [XP/mp] |
| 0xdd70ae | CInGameIdler::InitData | S-MANPOWER | [XP/mp] |
| 0xdd3a50 | "LOADING_MAIN_MENU" / "EXITING_TO_FRONTEND": clears HasGameStarted via 0x1edff0 | S-MANPOWER | [XP/mp] |
| 0xdd9b20 | "on_startup" (`ingameidler.cpp`): sets HasGameStarted = 1 via 0x1edff0, before the on_action fires | S-MANPOWER | [XP/mp] |
| 0xdda5d0 | "LOADING_SAVEGAME": clears HasGameStarted via 0x1edff0 | S-MANPOWER | [XP/mp] |
| 0xe22660 | strategic region: daily dominance update thunk (`region+0xe8` → 0x1003c50); from 0x1d4810 ("daily.strategic_region_update") and 0x1beae0 | S-DOMINANCE-DAILY | [DOM] |
| 0xe23bb0 | strategic region init: `region+0x140 = region+0x60 mod (2 × HOURS_DELAY_AFTER_EACH_COMBAT)` | A-CADENCE | [AIR/cadence] |
| 0xe24530 | air clock: is this an air hour for the region | A-CADENCE | [AIR/cadence] |
| 0xf11390 | weather cell (per province) hourly update: period temperature mid + random walk (`temperature_variation`, clamp `max_temperature_change`), snow depth from the region's snow / blizzard flag | S-WEATHER | [WSIM/hourly] |
| 0xf14fe0 | province darkness set-up: per province `tz` (+0x248), polar terms (+0x250, +0x258) and 24 hourly unit vectors (+0x00..+0x23f) from its map position, GMT_OFFSET and the pole offsets (caller 0xf13500) | S-NIGHT | [NIGHT/vec] |
| 0xf15b40 | weather weight multiplier `1 + Σ terrain share × weather.txt terrain_modifiers[k]` (leaf; 1 at sea) | S-WEATHER | [WSIM/roll] |
| 0xf15c50 | weather duration in hours `24 × (min + min(U1, U2) × (max − min))`, settings.duration of the current weather (wm+0x290..+0x2e8) | S-WEATHER | [WSIM/roll] |
| 0xf15eb0 | night direction for the date: `(0, sin θ, cos θ)` at wm+0x350..+0x360, θ = ±5° between 22 June and 22 December; season fraction wm+0x368 | S-NIGHT | [NIGHT/sun] |
| 0xf185a0 | weather manager reset: cells, region states, seed, +0x348 = 1, +0x34a = 0 | S-WEATHER | [WTH/cmd] |
| 0xf18800 | hourly darkness update (from HourlyUpdate 0xf1c6e0): 0xf15eb0, feather constants, 0xf20c50 for every province (threaded when many) | S-NIGHT | [NIGHT/dark] |
| 0xf18ad0 | DoUpdatePasses: per pass the next 584 cells (cursor wm+0x58) and 7 region states (cursor wm+0x5c, 0xf20ef0); all of them in the 14 init passes | S-WEATHER | [WSIM/hourly] |
| 0xf191f0 | region weather period for a date: first period (region+0x30 → +0xb0, 0xe0 bytes) holding the day of year; "No weather period found" | S-WEATHER | [WSIM/roll] |
| 0xf19660 | province darkness: read of the weather manager's province record +0x240 (leaf; computed by 0xf20c50) | S-NIGHT | [HOUR/night], [NIGHT/dark] |
| 0xf19690 | darkness of a province at a given hour `(wm, out, province, hour)` without storing (caller 0xe21593) | S-NIGHT | [NIGHT/dark] |
| 0xf197b0 | province local hour (record+0x260; leaf) | S-NIGHT | [NIGHT/dark] |
| 0xf197d0 | province modifier holder by province id: record+0x270, holding static `night` × darkness (written by 0xf20c50) | S-HIT-MODS, S-NIGHT | [STC/sum], [NIGHT/dark] |
| 0xf19f00 | weather hash `U1 = H(wm+0x308 + x)` mod 100000 (leaf; same U1 as the roll and the duration) | S-WEATHER | [WSIM/roll] |
| 0xf19fd0 | region weather modifier holder by region index | S-WEATHER | [STC/sum], [STC/mod] |
| 0xf1a000 | region weather state `wm+0x40 + i × 0x160` (null out of range; leaf) | S-WEATHER | [WTH/combat] |
| 0xf1c6e0 | CWeatherManager::HourlyUpdate; skipped while +0x349 or +0x34a; `advanceSeed` (1 from the hourly tick 0x1dfcfb): seed += random_int 0x2233fa0; cells 0xf11390, passes 0xf18ad0 | S-WEATHER | [WTH/cmd], [WSIM/hourly] |
| 0xf1cb90 | rebuild weather cells (+0x10) and region states (+0x40) | S-WEATHER | [WTH/cmd], [WTH/open] |
| 0xf1f710 | weather on: log seed, run HourlyUpdate | S-WEATHER | [WTH/cmd] |
| 0xf20c50 | province darkness `clamp((n·v[hour] + F/2) / F, 0, 1)` → record+0x240, local hour +0x260; on change rebuilds the province holder (+0x270) with static `night` × darkness | S-NIGHT, S-HIT-MODS | [NIGHT/dark] |
| 0xf20ef0 | region weather roll: period weights, temperature rule, one roll for rain light / heavy / snow / blizzard, sandstorm, arctic water, next change `+0x150`; returns at once before `+0x150`; random `H(seed + region + pass)` | S-WEATHER | [WTT/fill], [WSIM/roll] |
| 0xf218a0 | mark every weather cell dirty (+0x3a8) for the map | S-WEATHER | [WTH/cmd] |
| 0xf220d0 | region weather holder refill: clear `state+0x58`, add each active block whole, then the region's triggered `static_modifiers` | S-WEATHER | [WTT/fill] |
| 0xf60760 | plane speed with modifiers | A-INTERCEPT | [AIR/conv] |
| 0xf60970 | raw plane stat `(wing, out, idx, missionFlags)` | A-* | [AIR/conv] |
| 0xf60bc0 | naval strike agility per plane | A-AA-PREEMPT | [AIR/conv] |
| 0xf60f10 | naval attack per plane (`× 0.1 × (1 + Σ)`) | A-DMG | [AIR/conv] |
| 0xf61280 | naval targeting per plane (`× 0.1 × (1 + Σ)`) | A-HIT | [AIR/conv] |
| 0xf63100 | fighter-type test (flags & 0x100000c00 = fighter, interceptor, heavy_fighter). **disagrees**: AIR says equipment flags; WINGS reads the pool's sub-unit `type` `[[wing+0x38]+0x20]+0x5a8` | A-INTERCEPT, A-WINGS | [AIR/intercept], [WINGS/types] |
| 0xf77630 | better-agility damage reduction | A-INTERCEPT | [AIR/intercept] |
| 0xf78160 | wing efficiency (fuel?). **disagrees**: WINGS decodes it as the disruption efficiency `clamp(1 − sqrt(disruption / (planes × (1+def·DDD)(1+atk·DDA)(1+spd·DDS)))/3.162, 0.001, 1)`, 1 without disruption; the source of `airfx+0xc0` | A-DMG, A-LOSSES, A-EXTERNAL | [AIR/dmg], [WINGS/ext] |
| 0xf7ad60 | better-speed damage increase (top-speed term ?) | A-INTERCEPT | [AIR/intercept] |
| 0xf7be70 | generic air combat (reads DISRUPTION_FACTOR_CARRIER) | A-INTERCEPT | [AIR/intercept] |
| 0xf80f60 | per-wing losses and damage (experience; not decoded). **disagrees**: ACE decodes it as the ace death roll (live ace, losses > 0, line 0xb52 [0xb54]) or the ace earn roll (damage > 0 and losses < damage, line 0xb68, then create 0x618860 and int roll 0xb72); no experience | A-LOSSES, A-ACE | [AIR/losses], [ACE/rolls] |
| 0xf862a0 | generic air combat (reads COMBAT_DAMAGE_SCALE_CARRIER) | A-INTERCEPT | [AIR/intercept] |
| 0xf88330 | sub-unit `type` keyword → bit: 0 convoy, 6 capital_ship, 7 submarine, 8 screen_ship, 22 carrier, 37 missile_launcher, 39 support_ship (leaf; also in the S-DOCTRINE table); full map incl. 10 fighter, 11 interceptor, 15 naval_bomber, 32 heavy_fighter in [WINGS/types] | S-STATS-COMBAT, S-TARGET, A-WINGS | [STATS/getter], [DOC/effects], [WINGS/types] |
| 0xf8b070 | `type` keyword → bit, error "incorrect type category" | — | [STATS/getter] |
| 0xf9f9c0 | character → unit-leader role (`character+0xa8`; leaf) | — | [STC/leader] |
| 0xfa50b0 | character parser (`unit_medals` → `character+0xd0`) | — | [STC/leader] |
| 0xfafda0 | a mission's dominance in a region: Σ ship dominance × efficiency × MISSION_DOMINANCE_RATIOS[type] | S-DOMINANCE | [DOM] |
| 0xfb3220 | a mission's dominance for the daily target: Σ ship dominance of the task force × efficiency (patrol in its region 1.0), no mission ratio | S-DOMINANCE-DAILY | [DOM] |
| 0x1002d60 | daily dominance: loss set (countries with an entry minus those with a target) | S-DOMINANCE-DAILY | [DOM] |
| 0x1002fe0 | daily dominance: every country's target (0x10033e0); target > 0 → entry, 0x19de3c0, gain set | S-DOMINANCE-DAILY | [DOM] |
| 0x10033e0 | a country's target in a region: Σ kind-1 mission dominance (0xfb3220) × (multiplier 0x19dd350 + strike-force terms 0x1003760) | S-DOMINANCE-DAILY | [DOM] |
| 0x1003760 | strike-force dominance term (STRIKE_FORCE_FRACTION / _MULTIREGION_DECAY; not decoded) | S-DOMINANCE-DAILY | [DOM] |
| 0x1003c50 | a region's daily dominance update: targets; `current = clamp(current + trunc(target × GAIN), 0, target)` for countries with a target, the loss step for the rest; individual ratio; snapshot 0x1006280 | S-DOMINANCE-DAILY | [DOM] |
| 0x1005280 (0x1005296) | daily loss: `current −= trunc(decline_from × LOSS)` (0x24ef6f0 negates), target 0, entry removed at 0 | S-DOMINANCE-DAILY | [DOM] |
| 0x1006280 | control snapshot: value table → array `+0x40` (stride 0x50) that 0x1003f50 reads | S-DOMINANCE-DAILY | [DOM] |
| 0x1003f50 | region control for a country: 0 none, 1 own side (ties included), 2 enemies; needs `own × t/(1 − t)` | S-DOMINANCE, S-ORG-REGAIN | [DOM] |
| 0x10055a0 | region dominance state | S-POS | [POS/convoy] |
| 0x10109b0 | equipment list has a type flag `(list, mask)`: an entry `{equipment*, amount}` (list+0x20, count +0x2c) with `equipment+0x408 & mask` and amount > 0 | S-POTF | [POTF/assign] |
| 0x1016900 | scale an adjuster by f: attack/defence/movement `v > 0 → v × f`, `v < 0 → v × (2 − f)` | S-ADMIRAL-ADJUST | [ADM/adjust] |
| 0x10169f0 | adjuster reader: `attack` +0x10, `defence` +0x18, `movement` +0x20 | S-ADMIRAL-ADJUST | [ADM/adjust] |
| 0x1018080 | CSubUnitDefinition constructor: modifier ids `experience_gain_<name>_training_factor` / `_mission_factor` / `_combat_factor` → def+0x5e4 / +0x5e8 / +0x5ec (= ship+0x664 / +0x668 / +0x66c) via 0x1483b40 | S-XP-GAIN | [POTF/xp] |
| 0x1019140 | CSubUnitModifiers constructor (vtable 0x2955af0; `trait+0x780`, `leader+0x128`) | S-ADMIRAL-TRAIT | [ADM/subunit] |
| 0x1019270 | sub-unit key of a sub-unit object `[obj+0x58c]` (leaf) | S-ADMIRAL-MOD | [ADM/subunit] |
| 0x1019290 | critical part constructor: defaults +0x148/+0x150 multipliers 1, +0x158/+0x160 adds 0, +0x168 chance 1, +0x170 base_damage_instance 1, +0x174 per added module 0, +0x178 max −1 | S-CRIT-PART | [DMG/part] |
| 0x101a0b0 / 0x101a0f0 | CSubUnitModifiers: clear the adjuster list / the modifier-block list | S-ADMIRAL-MOD | [ADM/subunit] |
| 0x101afc0 / 0x101b5e0 | CSubUnitModifiers: find by sub-unit key in the adjuster list (0x38 records) / modifier-block list (0xd0 records); miss → default +0x38 / +0x60 | S-ADMIRAL-MOD | [ADM/subunit] |
| 0x101cbf0 / 0x101cc20 | CSubUnitModifiers: merge another one's adjuster list / modifier-block list | S-ADMIRAL-MOD | [ADM/subunit] |
| 0x101cc90 | CSubUnitModifiers reader (vt+0x18): `<sub-unit> = { units = { … } <modifier> = … }`, key = sub-unit definition +0x58c ("Invalid sub unit definition", "already specified") | S-ADMIRAL-TRAIT | [ADM/subunit] |
| 0x101e130 | sub-unit definition parser: each `type = { }` keyword OR-ed into sub-unit+0x5a8 via 0xf8b070 | S-STATS-COMBAT, S-TARGET | [STATS/getter] |
| 0x12a6740 | national navy XP, capped by FIELD_EXPERIENCE_MAX_PER_DAY | S-XP-GAIN | [XP/gain] |
| 0x1310de0 | AI (CAIInteriorMinister vt+0x70 → 0x1306560): per-ship weight 1.0 for the PotF, 0.5 in its task force (purpose not decoded) | — | [POTF/readers] |
| 0x134a550 | `set_pride_of_the_fleet_command` constructor (cmd+0x28 ship handle, +0x30 country tag) | S-POTF | [POTF/assign] |
| 0x1356850 | `set_pride_of_the_fleet_command` Execute: pay PRIDE_OF_THE_FLEET_UNASSIGN_COST if a PotF exists (0xba7380), then 0x712700 | S-POTF | [POTF/assign] |
| 0x1359420 | `set_pride_of_the_fleet_command` IsValid: `country+0x484 > 0`; political power ≥ cost if a PotF exists; new ≠ current; null valid; ship's owner == command country or same original tag. No type check | S-POTF | [POTF/assign] |
| 0x1365d60 | delete-ship command: a PotF is first unassigned (command with null ship) | S-POTF | [POTF/assign] |
| 0x13e05f0 | evidence for combatant+0xd8 (side at combat+0x28) | A-SORTIE | [AIR/conv] |
| 0x13e1df0 | side.GetOpponent: `side+0xd8 ? combat+0x30 : combat+0x28` (leaf) | S-POS | [POS/pos], [ADM/pos] |
| 0x13e28c0 | CCombat::Update: the eight side virtual calls | S-HOUR, S-ORDER | [HOUR/hour], [POS/recompute] |
| 0x13e2ba0 | combat validity: checks both sides' country lists | S-HOUR | [HOUR/chain] |
| 0x143c720 | minimum dominance to control a region: `own value × t / (1 − t)` (t ≥ 1 as 0.99); also the convoy-escort bonus | S-DOMINANCE, S-POS | [DOM], [POS/convoy] |
| 0x143d340 | naval terrain: per-sub-unit modifier holder (block +0x28 by sub-unit index; miss → the terrain's own holder +0xa8; leaf), added in 0xc38760 | S-STATS-COMBAT, S-TERRAIN | [STC/sum], [TER/terrain] |
| 0x143e4a0 | naval terrain: per-sub-unit `units` adjuster {+0x10 attack, +0x18 defence, +0x20 movement} (block +0xe8 via map terrain+0x1b8; miss → the terrain's own +0x168; leaf) | S-STATS-COMBAT, S-TERRAIN | [STATS/getter], [TER/terrain] |
| 0x143e980 | terrain post-load (vtable): adds the terrain's own `units` (+0x168) and modifiers (+0xa8) into every sub-unit block | S-TERRAIN | [TER/terrain] |
| 0x143eb40 | terrain post-load: resolves each sub-unit block's name (0xac4e60; "sub unit not found for terrain "), fills the map +0x1b0 | S-TERRAIN | [TER/terrain] |
| 0x1445e00 | CUnitHistory constructor (`ship+0x8d8`): `+0x20` = medal object from 0xf9e530 | S-XP-GAIN | [POTF/xp] |
| 0x14463a0 | unit-medals object constructor (0x268 bytes; character+0xd0): holders +0x20 / +0xe0 / +0x1a0, medal list +8; for a ship it is behind `ship+0x8f8`, holder +0x1a0 read by 0xc38760 and 0xc3b240 | — | [STC/leader], [POTF/xp] |
| 0x1446450 | CUnitHistory destructor (RTTI `.?AVCUnitHistory@@`, vtable 0x29c1868) | — | [POTF/xp] |
| 0x15a5560 | NavalTerrain(region template `region+0x30`): `template+0x130` if a valid naval terrain (+0x10), else the terrain `ocean` (cached) | S-POS, S-STATS-COMBAT, S-TERRAIN | [POS/pos], [STC/mod], [TER/terrain] |
| 0x15a6a10 | strategic region template post-load: `naval_terrain` name (+0x110) → terrain (+0x130); "invalid naval terrain type for " | S-TERRAIN | [TER/terrain] |
| 0x15c28e0 | CNavalCombat::CanRetreat (vt+0x60): `hours >= COMBAT_MIN_DURATION` | S-RETREAT | [ACT/retreat], [README/disagree] |
| 0x15c28f0 | CNavalCombat constructor area (sets combatant+0xd8; sites 0x15c2913/0x15c2941) | — | [AIR/conv], [POS/top] |
| 0x15c2980 | CNavalCombat vt+0x38 teardown: 0xc03e00 for every unit of both sides | S-END | [POTF/combat] |
| 0x15c3010 | side of a combat that holds a country (combat+0x98, else +0xa0) | S-STATS-COMBAT | [STATS/getter] |
| 0x15c3080 | second modifier holder of the combat (night/darkness) | A-SORTIE, A-HIT | [AIR/conv] |
| 0x15c31b0 | other side `(combat, side)`: `side == combat+0x98 ? combat+0xa0 : combat+0x98` (leaf) | S-RETREAT | [ADM/pos] |
| 0x15c3320 | XP gain formula part | S-XP-GAIN | [XP/gain] |
| 0x15c3600 | CNavalCombat::Update (vt+0x78) | S-HOUR | [HOUR/hour], [POS/recompute] |
| 0x15c3690 | CNavalCombat::EndHour (vt+0x80): hours++, distance, end check. **disagrees**: ACT says hours++ first, then both combatants update | S-HOUR, S-END | [HOUR/end], [ACT/bg] |
| 0x15c44e0 | CNavalCombat validity (vt+0x48) → 0x13e2ba0 | S-HOUR | [HOUR/chain] |
| 0x15c4ba0 | CNavalCombat::SetProvince: combat+0x38 = province, combat+0x48 = NavalTerrain(province+0xc8) | S-TERRAIN | [TER/terrain] |
| 0x15c52b0 | merge unpaired groups (HOUR); "per-side joins" (POS) | S-HOUR | [HOUR/hour], [POS/recompute] |
| 0x15c55d0 | pair fire-exchange groups | FEX | [HOUR/fex] |
| 0x15c5820 | refresh environment: weather type (+0xa8) and region weather holder (+0xb0) from 0xf1a000, darkness (+0xb8) | S-HOUR, S-NIGHT, S-WEATHER | [HOUR/hour], [WTH/combat] |
| 0x15c6250 | per-attacker damage record (kill/assist credit) | S-DMG-APPLY, A-DMG | [DMG/apply], [AIR/dmg] |
| 0x15c70b0 | `Mod(id; country, location, admiral)` = country+0x5b8 + region weather holder + naval terrain +0xb8 + admiral+0x418; a null location or admiral is skipped | S-POS, S-SCREEN | [POS/pos], [HOUR/end], [STC/mod], [ADM/pos] |
| 0x15c7580 | fleet exempt from side-wide retreat | S-RETREAT, S-END | [ACT/retreat], [HOUR/end] |
| 0x161a4a0 | naval combatant constructor (positioning = 1.0) | S-POS | [POS/top] |
| 0x161ab40 | add fleet/ships to a side: state 4, joined penalty, dominance bonus | S-HOUR, S-POS | [HOUR/hour], [POS/joined], [POS/convoy] |
| 0x161b5f0 | external plane join cap | A-EXTERNAL | [AIR/ext] |
| 0x161b960 | ComputePositioning `(side, out, tooltip)`; `positioning` with the own commanding admiral, 0x5a and the size factor with the enemy's (0x161b9b1) | S-POS | [POS/pos], [ADM/pos] |
| 0x161ccb0 | screening and carrier screening; `screening_efficiency` with the own commanding admiral, `screening_without_screens` country only (no location, no admiral) | S-SCREEN, S-POS-SCREEN | [TGT/screen], [POS/screen], [ADM/pos] |
| 0x161e0d0 / 0x161e0fb | side surface-detection sum | S-POS-DETECT | [POS/detect] |
| 0x161e2c0 | activation count helper → 0x1c6a370 | S-ACTIVATE | [ACT/activate] |
| 0x161e400 | side vt+0xb0: positioning, screening, fleet AA | S-HOUR, S-POS, A-AA-REDUCE | [HOUR/hour], [POS/recompute], [AIR/reduce] |
| 0x161e5c0 | commanding admiral: the leader of `side+0x118` (count +0x124) with the highest overall skill 0xc199f0, strict (first wins ties); side vt+0xe8, called once from 0x1620940 | S-POS, S-ADMIRAL | [POS/top], [ADM/reads] |
| 0x161e6d0 | join group (COMBAT_MAX_GROUPS, BEST_CAPITALS_TO_SCREENS_RATIO) | FEX | [HOUR/fex], [XP/gain] |
| 0x161e9a0 | air stacking factor F | A-STACK | [AIR/stack] |
| 0x161ebd0 | present-ship count (`state & 0x34 == 0`) | S-POS-SIZE | [POS/size] |
| 0x161ec40 | own side has no screens | S-SCREEN | [TGT/screen] |
| 0x161ece0 | combined `1-(1-[side+0x148])(1-[side+0x150])` (settled by recheck) | S-SCREEN, A-SORTIE | [TGT/bonus], [AIR/sortie] |
| 0x161ed30 | positioning damage penalty `D × (1 − pos)` | S-POS-DMG | [POS/dmg], [DMG/dmg] |
| 0x161ed70 | sub reveal positioning term `SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING × (1 − pos)` | S-SUB | [POS/sub], [ACT/sub] |
| 0x161eeb0 | carrier-ratio helper `(side, &A, &B)` | S-POS | [POS/carrier] |
| 0x161f790 | side vt+0x70: leader XP (HOUR). **disagrees**: POS "medium: fire phase" | S-HOUR | [HOUR/hour], [POS/recompute] |
| 0x161f810 | side vt+0x98: each group update (firing); refreshes enemy count snapshot | S-ORDER, S-TARGET | [HOUR/order], [TGT/bb] |
| 0x161fb10 | side-wide retreat order (+0x140) | S-END, S-RETREAT | [HOUR/end] |
| 0x161fbc0 | merge first group into last if > COMBAT_MAX_GROUPS | FEX | [HOUR/fex] |
| 0x161fcf0 | IsDone(side) | S-END | [HOUR/end] |
| 0x16205c0 | external planes join `(side, wing, &budget, eff, sub)`; side vt+0xf0, called from 0xf82ca0 with sub = 0 (settled by WINGS) | A-EXTERNAL | [AIR/ext], [WINGS/ext] |
| 0x1620940 | side HourPrep: decay joined penalty, cache admiral (only while `side+0x130` is null: `+0x130 = +0x138 = vt+0xe8`), → 0x1622360 | S-HOUR, S-POS | [HOUR/hour], [POS/joined], [ADM/reads] |
| 0x1620fa0 | damage-dealt statistics | S-DMG-APPLY | [DMG/apply] |
| 0x1621900 | merge a group into another | FEX | [HOUR/hour] |
| 0x1621a70 | side vt+0x100: per-country ship-type tallies + modifier table (purpose ?) | S-HOUR | [HOUR/hour] |
| 0x16222e0 | end-of-battle air cleanup, both sides (HOUR). **disagrees**: ACT calls it the combatant update → 0x1c6b560 | S-END | [HOUR/end], [ACT/bg] |
| 0x1622360 | rebuild groups (called from 0x1620940) | S-POS | [POS/recompute], [HOUR/hour] |
| 0x1622d40 | warship filter (ships, not convoys) | A-EXTERNAL, A-AA-REDUCE | [AIR/ext], [AIR/reduce] |
| 0x1622d60 | building-module term: for a navy leader, Σ of four skill objects' +0xe0 containers; max over `tf+0x728` in 0xc38760 | S-STATS-COUNTRY | [STC/sum] |
| 0x17e4f70 | piercing tooltip (UI only) | S-DMG-PIERCE | [HIT/map] |
| 0x196f293 / 0x196f2c3 / 0x196f47b / 0x196f6f8 | member constructor sites (charge = 0, +0x12c = −1) | S-COOLDOWN, S-SUB | [ACT/cooldown], [TGT/bb] |
| 0x196f500 | CFEXMember constructor `(member, ship, state)`: `SCachedInfo` at member+0x18 (+0x28 = 0xc3b0e0 IsPotF at 0x196f607), state +0xe0 | S-POTF | [POTF/readers] |
| 0x196fa40 | record damage on a surviving target | S-DMG-APPLY, A-DMG | [DMG/apply], [AIR/dmg] |
| 0x196fca0 | fleet AA damage reduction | A-AA-REDUCE | [AIR/reduce] |
| 0x196fd60 | piercing tier lookup `(armor, piercing, table)` | S-DMG-PIERCE | [HIT/pierce], [DMG/dmg] |
| 0x196fe80 | escape speed per day | S-RETREAT-SPEED | [ACT/speed], [HOUR/hour] |
| 0x1970a90 (chunks 0x1970b48, 0x197142d) | BuildShot / shot profile: damage, crit, hit chance, piercing, cooldown | S-HIT, S-DMG, S-CRIT | [HIT/map], [DMG/pipe] |
| 0x1971440 | target weight | S-TARGET-WEIGHT | [TGT/weight] |
| 0x1971610 (chunk 0x1971652) | activation check | S-ACTIVATE | [ACT/activate], [HOUR/hour] |
| 0x1971760 | side-wide retreat order check (member) | S-RETREAT | [ACT/bg], [ACT/retreat] |
| 0x19717f0 | retreat decision | S-RETREAT | [ACT/retreat], [HOUR/hour] |
| 0x1971816 | retreat HP threshold by aggressiveness ("Aggressiveness level") | S-RETREAT | [HOUR/hour], [TGT/sub] |
| 0x1971cc0 | PickTarget | S-TARGET | [TGT/target], [HOUR/order] |
| 0x1971db4 | SubMinBox (chunk in PickTarget, ..0x1971e94) | S-TARGET-SUB | [TGT/sub] |
| 0x19723b0 | ECombatBox of a member. **disagrees** on 4/5: HIT 4 revealed / 5 hidden; ACT, TGT 4 hidden / 5 revealed | S-TARGET, S-ACTIVATE | [TGT/bb], [ACT/bg], [HIT/map] |
| 0x1972570 | member modifier getter + screening bonus (`screeningValue`); ship: 0xc38760 with `[side+0x130]`, convoy: 0x15c70b0 | S-HIT-MODS, S-RETREAT-SPEED, A-SORTIE | [HIT/map], [TGT/bonus], [AIR/sortie], [STC/member] |
| 0x1972900 | a member's attack for a weapon slot (light / torpedo / heavy getter) with the side's commanding admiral (0x197292d) | S-ADMIRAL | [ADM/reads] |
| 0x1972a70 / 0x1972b60 | weapon cooldown (variant); 0x1972a70 reads the commanding admiral at 0x1972a9d | S-COOLDOWN | [ACT/cooldown], [ADM/reads] |
| 0x1972c00 | MaxBox (light 0x1972c92, torpedo 0x1972cb7, heavy 0x1972dd4) | S-TARGET-* | [TGT/light], [TGT/heavy], [TGT/torp] |
| 0x1973470 | member HourUpdate: sub timer, state machine, retreat, fire, escape | S-HOUR, S-ACTIVATE | [HOUR/hour], [ACT/bg] |
| 0x1973521 | member hourly update (TGT), caller of 0x19740c0 (HIT); a chunk of 0x1973470 (pdata parent, [ADM/reads]); reads the commanding admiral at 0x1973545 for the speed 0xc39140 and sub attack 0xc3a890 | S-HOUR | [TGT/bb], [HIT/map], [POS/sub], [ADM/reads] |
| 0x1973d30 | is submarine (air target weight) | A-TARGET | [AIR/target] |
| 0x1973e20 | Fire one weapon slot (timer, PickTarget, shot loop) | S-ORDER, S-COOLDOWN | [HOUR/order], [ACT/cooldown] |
| 0x1973eea | fire chunk: last-shot record, torpedo-firing reveal | S-XP-GAIN, S-SUB | [XP/gain], [POS/sub] |
| 0x19740c0 | hit/shot loop: hit, randomness, crit, part, apply; `(shooterMember, targetMember, weapon, shooterShip, …)`, returns 1 when the target sank | S-HIT, S-DMG, S-CRIT | [HIT/map], [DMG/pipe], [HOUR/order], [LOG] |
| 0x1974d80 | `CFEXMember::SCachedInfo` ReadMember (`pride_of_the_fleet` → +0x28, `sunk_by` +0x90, `equipment_variant` +0x50, `highest_eq_variant` +0xb0) | S-POTF | [POTF/readers] |
| 0x1975150 | air part damage (CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR) | A-CRIT | [AIR/crit] |
| 0x1975280 | retreat roll P `= CHANCE × (1 + M(0x57)) × (hours > INITIAL ? 1 + M(0x58) : 1) × (1 + enemyAdmiral.B(0x1b9))`, clamp [0, 1], retreat if `U <= P`; a convoy only gets the admiral factor | S-RETREAT | [ACT/retreat], [HOUR/end], [ADM/pos] |
| 0x1975400 | sub reveal chance | S-SUB | [ACT/sub] |
| 0x1975480 | kill/assist credit | S-DMG-APPLY | [HOUR/order], [DMG/apply] |
| 0x1976430 | `CFEXMember::SCachedInfo` Write: `pride_of_the_fleet=yes` when +0x28 (0x1976579) | S-POTF | [POTF/save] |
| 0x1976630 | new external AirFX | A-EXTERNAL | [AIR/ext] |
| 0x1976ae0 | air entry owner | S-XP-GAIN | [XP/gain] |
| 0x1976ca0 | recompute AirFX planes (+0x5c) | A-* | [AIR/graph] |
| 0x1976db0 | carrier sortie efficiency (no org) | A-SORTIE | [AIR/sortie] |
| 0x1977000 | sortie efficiency darkness read (+0xb8 → mod 0x23) | S-NIGHT, A-SORTIE | [HOUR/night] |
| 0x19786c0 | CarrierSortieEfficiency: 0x1976db0 × org ratio | A-SORTIE, S-POS, S-XP-GAIN | [AIR/sortie], [POS/carrier], [XP/gain] |
| 0x1978e30 | average naval attack, targeting, agility over first nWings; carrier air factors 0xc9–0xcb from the country container only (0x1979478..0x19794f9) | A-DMG | [AIR/dmg], [ADM/subunit] |
| 0x1979770 | new external AirFX | A-EXTERNAL | [AIR/ext] |
| 0x197a1b0 | AirFX cooldown ≤ 0 (feeds a dead list) | A-CADENCE | [AIR/cadence] |
| 0x197a1c0 | remove plane losses from wings (type 1 intercept, 2 AA, 6 kamikaze) | A-LOSSES | [AIR/losses] |
| 0x197a6a0 | find external AirFX for an air base | A-EXTERNAL | [AIR/ext] |
| 0x197a700 | air target pick | A-TARGET | [AIR/target] |
| 0x197ac80 | air strike: hits, damage, crit | A-HIT, A-DMG, A-CRIT | [AIR/hit], [AIR/dmg], [AIR/crit], [POTF/air] |
| 0x197b380 | interception resolve | A-INTERCEPT | [AIR/intercept] |
| 0x197b890 | main air strike `(airfx, enemyGroup, combat, F)` | A-* | [AIR/graph] |
| 0x197c8b0 | preemptive AA | A-AA-PREEMPT | [AIR/preempt] |
| 0x197d4a0 | plane-weighted equipment averages (+0xc8..+0xe8) | A-HIT | [AIR/graph] |
| 0x197d770 | set carrier cooldown (no effect) | A-CADENCE | [AIR/cadence] |
| 0x197d990 | AirFX save code | A-CADENCE | [AIR/cadence] |
| 0x19895d0 | fire-exchange group stat accumulation (cooldowns 0x1972b60 with the side's commanding admiral, attacks × 1/cooldown); called from 0x16223af | FEX | [ADM/reads] |
| 0x1989c60 | aggressiveness-check strength score (not decoded) | S-RETREAT | [ACT/retreat] |
| 0x1989fe0 | aggressiveness-check ratio `min(own/max(enemy, 0.1), 10)` | S-RETREAT | [ACT/retreat] |
| 0x198a080 | update fleet distance (+0x118) | S-HOUR | [HOUR/hour] |
| 0x19dd200 | dominance value step: previous = current; current += delta (positive capped at target), floored at 0 | S-DOMINANCE-DAILY | [DOM] |
| 0x19dd350 | dominance multiplier (1 + air base)(1 + naval base)(1 + radar) via 0x19dd240 / 0x19dd440 / 0x19dd550 × the *_DOMINANCE_FACTOR defines | S-DOMINANCE-DAILY | [DOM] |
| 0x19de3c0 | dominance set target (V, target, base); decline_from = target if > 0 | S-DOMINANCE-DAILY | [DOM] |
| 0x1a68d40 | AI: a country without a PotF (feature 0xf) scores every eligible ship (0xc33a50) and assigns one by command | S-POTF | [POTF/assign] |
| 0x1aa8f60..0x1ab5598 | tooltips/script triggers reading ship level | S-XP-EFFECT | [XP/effect] |
| 0x1ad7f00 | convoy hit profile | S-HIT-SHIP | [HIT/ship] |
| 0x1ad7f30 | convoy base air target weight | A-TARGET | [AIR/target] |
| 0x1ad8060 | convoy AA | A-AA-PREEMPT | [AIR/preempt] |
| 0x1ad8180 | convoy reliability | S-CRIT, A-CRIT | [HIT/crit], [AIR/crit] |
| 0x1ad81e0 | convoy HP ratio (TGT); non-ship damage path (DMG) | S-TARGET-WEIGHT, S-DMG-APPLY | [TGT/weight], [DMG/apply] |
| 0x1ad8610 | convoy damage apply (not decoded) | S-DMG-APPLY, A-DMG | [DMG/apply], [AIR/dmg] |
| 0x1ad8680 | convoy damage path (not decoded) | S-DMG-APPLY | [DMG/apply] |
| 0x1becfc0 | UI: PotF button click; blocked when a PotF exists and political power < PRIDE_OF_THE_FLEET_UNASSIGN_COST, else the set / replace / unset popup 0x1f083d0 | S-POTF | [POTF/assign] |
| 0x1bed5f0 | UI: PotF button shown only if 0xc33a50 (frame 3 for the current PotF, 2 otherwise; disabled when another PotF exists and political power < cost) | S-POTF | [POTF/assign] |
| 0x1c69af0 | fire-exchange group constructor | FEX | [HOUR/fex] |
| 0x1c69dc0 | carrier's member in a group | A-DMG | [AIR/graph] |
| 0x1c69e40 | group air sub-detection part | S-SUB | [HOUR/hour] |
| 0x1c6a370 | member counts by type (capital [0], screen [4], carrier [8], convoy [0x14]); also the convoy-pairing value | S-SCREEN, S-ACTIVATE, FEX | [TGT/screen], [ACT/activate], [HOUR/fex] |
| 0x1c6a540 | group plane silhouette | A-STACK | [AIR/stack] |
| 0x1c6a7c0 | member joins group (sub reveal timer init) | S-SUB | [ACT/sub] |
| 0x1c6b4a0 | group has a live member | S-END | [HOUR/end] |
| 0x1c6b560 | group hourly update: counts, sub detection, member loop, removal, air | S-HOUR, S-ORDER, S-SUB | [HOUR/hour], [TGT/bb], [AIR/graph] |
| 0x1c6b5d0 / 0x1c6b7b0 | group counts +0xc0 / +0xbc | S-RETREAT | [ACT/retreat] |
| 0x1c6bfc0 | every member of the group retreating | S-END | [HOUR/end] |
| 0x1c6c040 | group pairing score ("distance or score") | FEX | [HOUR/fex] |
| 0x1c6c580 | group air step | A-CADENCE | [AIR/graph], [HOUR/hour] |
| 0x2014abf | script trigger reading ship level | S-XP-EFFECT | [XP/effect] |
| 0x2072200 | define reader storing fx15 (27 defines) | S-XP-GAIN | [XP/top] |
| 0x221f310 | resolve a `{type, id}` handle to its object (null if deleted) | — | [LOG] |
| 0x2233db0 | `random_fixed(int64* out, file, line)` | all | see §4, [LOG] |
| 0x1975480 | combat sink handler: sets the member's state to sunk (0x1975bea), calls `0xd77ff0` for the ship's task force (0x1975879) | S-MANPOWER | [LOG] |
| 0x2233fa0 | random int31 | FEX, A-* | [HOUR/fex], [AIR/conv] |
| 0x24ed3f0 | fx5 → fx15 | — | [XP/top] |
| 0x24ed580 | fx15 → fx5 | — | [XP/top] |
| 0x24ed730 | fixed → int, round half away from zero | — | see §4 |
| 0x24ef6f0 | store `-x` (negate) | — | [DMG/conv], [XP/effect] |
| 0x24ef700 | fx5 sin on [0, π/2]: Taylor `x − x³/6 + x⁵/120` (leaf) | — | [NIGHT/fx] |
| 0x24ef840 | fx5 cos: sin(x + π/2) with range reduction (2π = 6.28319) | — | [NIGHT/fx] |
| 0x24efd30 | fx5 sin with range reduction | — | [NIGHT/fx] |
| 0x24f08a0 | weighted random draw | A-TARGET | [AIR/target] |
| 0x24fc2f0 | fx5 3-vector normalize (integer Newton sqrt of the raw squares) | — | [NIGHT/fx] |

### Vtables and RTTI

| RVA | Meaning | Finding |
|---|---|---|
| 0x2719610 | CEquipmentGroup vtable (named `common/equipment_groups` group; +0x48 Matches = 0xa0a950) | [MIO/match] |
| 0x2719668 | CAnonymousEquipmentGroup vtable (`equipment_type`, `limit_to_equipment_type`; +0x48 Matches = 0xa0a600; second vtable 0x27196c0 at +0x68, slot +0x10 = 0xa0ae40) | [MIO/match] |
| 0x2719790 | CEquipmentGroupDatabase vtable | [MIO/match] |
| 0x2745dc0 | NIndustrialOrganisation::CUnlockTraitEffect vtable (`complete_mio_trait`; +0x68 Execute = 0x2fbd80) | [MIO/snap] |
| 0x27aa858 | NIndustrialOrganisation::CHasEquipmentTypeTrigger vtable (`has_mio_equipment_type`; +0xb0 Evaluate = 0x456890) | [MIO/assign] |
| 0x2955af0 | CSubUnitModifiers vtable (`sub_unit_modifiers`; +0x18 reader 0x101cc90) | [ADM/subunit] |
| 0x2955b90 | CUnitLeader vtable (RTTI `.?AVCUnitLeader@@`; slot 0xf0 pure) | [STC/leader] |
| 0x2955ca8 | CArmyLeader vtable (slot 0xf0 = 0xc11470) | [STC/leader] |
| 0x2955dc0 | CNavyLeader vtable (slot 0x60 = 0xc18790, 0x68 = 0xc186d0, 0x70 = 0xc16d10, 0x78 = 0x120540, 0x88 = 0xc10ab0, 0x90 = 0xc10a10, 0xe0 = 0xc21280, 0xf0 = 0xc11540, 0x100 = 0xc1e650, 0x108 = 0xc1e7c0; second vtable 0x2955ed8) | [STC/leader], [ADM/map] |
| 0x29530d8 | CUnit vtable (RTTI `.?AVCUnit@@`) | [STC/leader] |
| 0x2962a18 | CFleet vtable (RTTI `.?AVCFleet@@`; ctor 0xd503d0) | [STC/sum] |
| 0x2962f70 | CTaskForce vtable (RTTI `.?AVCTaskForce@@`, base CUnit); CArmy 0x295a2b0, CRailwayGun 0x2972228 share slots 18/19 | [XP/mp] |
| 0x2963000 | CTaskForce vtable slot 18 (= 0xd705f0) | [XP/gain] |
| 0x2963008 | CTaskForce vtable slot 19, DailyUpdate (= 0xd69090) | [XP/mp] |
| 0x29ddb08 | CNavalCombat vtable (+0x48 valid 0x15c44e0, +0x60 CanRetreat 0x15c28e0, +0x78 Update 0x15c3600, +0x80 EndHour 0x15c3690) | [HOUR/chain], [ACT/retreat] |
| 0x29e3e98 | naval combatant vtable | [HOUR/chain], [POS/top] |
| 0x29e3f08 / 0x29e3f30 / 0x29e3f48 / 0x29e3f98 | combatant slots +0x70 (0x161f790), +0x98 (0x161f810), +0xb0 (0x161e400), +0x100 (0x1621a70) | [HOUR/hour] |
| 0x2d218e8 | CNavalCombat RTTI | [HOUR/chain] |

### Design stats (S-STATS)

From [S-STATS-design-stats.md](findings/S-STATS-design-stats.md) (equipment variant, ship refresh and combat stat getters).

| RVA / field | Role |
|---|---|
| 0xbe0260 | CEquipmentVariant::RecalculateStats |
| 0xbdfc80 | variant final stats with breakdown → `variant+0x100` |
| 0xbdfa70 | variant final stats, no breakdown |
| 0x193cdf0 | stat_helper: size assert, calls 0x193b080 |
| 0x193b080 | gather stat components from all variant sources |
| 0x193ade0 / 0x193cc10 | single-stat gather |
| 0x193d170 | add stat container × weight (add/avg/mult) |
| 0x193ca70 | single-stat version of 0x193d170 |
| 0x6f3920 | variant idea bonuses (`+0xd0`) |
| 0xbd5b40 | variant named equipment bonuses (handles `+0xe8`) |
| 0xbd3b80 | refresh variant MIO bonus (`+0x498`) |
| 0xdb7800 / 0xdb7a10 / 0xdb7a30 | MIO trait/initial/policy bonus sum (equipment / production) |
| 0xa4fe60 | MIO trait limit_to_equipment_type check |
| 0xe5f600 / 0xe5f740 | country add idea equipment bonus (instant → existing variants) |
| 0xe65f30 | country remove idea equipment bonuses |
| 0x631e70 | equipment bonus applies to type (tail-calls the key match 0xc97e00, see [DOC/effects]) |
| 0x13e81b0 | token id → EEquipmentStats |
| 0x831c0 | static token registration (name → id) |
| 0xc3d090 | CShip::RefreshStats |
| 0xc3d340 | part stat penalties in the refresh: `stat × max(0, 1 + count × penalty)` per damaged part, every stat (in-game: attacks, surface detection, hit chance factors) |
| 0xc3d4a3 | max organization `max(0.001, (max_organisation + navy_org) × (1 + navy_org_factor))`, current organization clamped to it |
| 0x1019e50 / 0x1429cb0 / 0x142b5f0 | add variant stats to ship × (1 + sub-unit stat) |
| 0x1019ea0 / 0x1429d30 | add sub-unit stat set (direct or base path) + clamps |
| 0x1429580 | sub-unit additive stat list |
| 0x1019fb0 | ship stat block finalize (not decoded) |
| 0xd11ae0 | country per-sub-unit stats lookup (`country+0xf70`) |
| 0xd0e200 | create country sub-unit copy, apply techs |
| 0x101b1b0 / 0x193d370 / 0x193d2a0 | apply technology sub-unit bonuses (type and categories, × level); then the doctrine bonus sources `[country+0xf70]+0x08` × 1.0 ([DOC/effects]) |
| 0xee3e70 | technology researched handler (enables sub-units) |
| 0xc34a50 | combat attack/armor getter (kind 0 light, 1 heavy, 2 sub, 3 torpedo, 4 armor) |
| 0xc3a5d0 / 0xc3a580 / 0xc3a890 / 0xc3ac00 / 0xc3a520 | light / heavy / sub / torpedo attack, armor |
| 0xc3a740 | combat speed getter |
| 0x152d4f0 | module stat description (container `+0x128`) |
| 0x6317f0 | stat container tooltip (EQUIPMENT_MODULE_STAT_AVERAGED) |
| type `+0x148+8s` | hull base stats |
| variant `+0x3f0` | equipment type |
| variant `+0x68`/`+0x70`/`+0x7c` | upgrades {upgrade*, level} |
| variant `+0xb8`/`+0xc4` | modules, one per filled slot |
| variant `+0xd0`/`+0xdc` | idea bonuses |
| variant `+0xe8`/`+0xf4` | named bonus handles |
| variant `+0x100+8s` | final stats |
| variant `+0x414` | Σ upgrade levels |
| variant `+0x488` | MIO handle |
| variant `+0x498`/`+0x4a4` | MIO bonus list |
| module `+0x128` (`+0x138` add, `+0x158` avg, `+0x178` mult) | module stat container |
| upgrade `+0x40` | upgrade stat container |
| bonus `+0x40` (idea) / `+0x48` (named) | bonus stat lists |
| bonus `+0x70` | instant flag |
| country `+0x160` | idea equipment bonuses. **disagrees**: DOC shows the list is `[country+0xf68]+0x160` (equipment manager; 0xbafd23 → 0xe5f600); settled in [README/disagree] |
| country `+0xa0` | country variants. **disagrees**: DOC reads them at `[country+0xf68]+0xa0` (0xe60110); settled in [README/disagree] |
| country `+0xf70` | per-country sub-units |
| ship `+0x120+8s` | ship stat block |
| ship `+0x6f8` / `+0x700` | current HP / current org |
| ship `+0x918`/`+0x924` | damaged critical parts {part*, count} |
| sub-unit stats `+0x60+8s` | sub-unit stat array |

### Doctrines (S-DOCTRINE)

From [S-DOCTRINE-doctrines.md][DOC] (tracks, mastery, rewards, milestones, effect application, doctrine triggers). Sorted by RVA. Struct fields are in §2, define read sites in §3.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x75540 / 0x117b280 | console command `mastery` registration / handler → 0x13cd100 | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x49b830 | `add_equipment_bonus` effect execute → 0xe60110 | S-STATS-BONUS | [DOC/effects] |
| 0x5297d0 + 0x529000 | `add_mastery` effect execute: tracks matching the filter (0x13cda80), then vtable +0xd0 = `AddMastery(track, amount)` on each (complete and empty tracks too, no bank) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x52ba80 | `has_completed_track` evaluate: any track of the type, with a subdoctrine and `IsCompleted` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x52bc50 | `has_completed_subdoctrine` evaluate (0xfc8250 over folders) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x52bce0 | `has_doctrine` evaluate: active grand doctrine of any folder or subdoctrine slotted in any track; no mastery check | S-DOCTRINE-TRIGGERS | [DOC/trig] |
| 0x52be70 | `has_mastery_level` evaluate: any track with the subdoctrine and `unlocked >= amount` (int) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x52bf90 | `has_subdoctrine_in_track` evaluate | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x52c060 | `has_mastery` evaluate: any track of the type with `mastery >= amount` (fx5, cumulative) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x52c240 / 0x52d6d0 / 0x52d900 | readers of `has_doctrine` / `has_mastery_level` / `has_mastery` | S-DOCTRINE-* | [DOC/map] |
| 0x633010 / 0x632d90 | named equipment bonus entry writer (`name`, `prefix`, bonus, `id`) / bonus block writer (`<key>={ instant … }`) | S-DOCTRINE-SAVE | [DOC/load] |
| 0x6431d0 | sub-unit (0xac4fa0) or category (0xac4c70) stat block reader, the technology block type | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0x6dade0 (call 0x6dba79) | `CCountry::UpdateModifiers`: clears country+0x5b8, adds the doctrine modifiers via 0x13cdc50(cds, country+0x5a8) | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0x9cda20 | grand doctrine milestone i: `grand+0x300 + i·0x190` (bounded by the track count) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x9e4da0 | reward cost: `reward+0x1d0`, or DEFAULT_REWARD_MASTERY when 0 (leaf) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x9e5240 | reward reader: activation effects, then `mastery` → `+0x1d0` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x9e5a50 | cumulative reward cost `Σ_{j≤i}` (UI "Unlocked at X Mastery") | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x9e6770 | subdoctrine reader (`rewards`, `track`, `xor`, `mastery`) | S-DOCTRINE-MASTERY | [DOC/map] |
| 0x9e6b80 | track `active` trigger evaluate (empty = true; medium) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xbe1550 | set variant `+0xd0` idea bonuses / `+0xe8` named handles from lists, filtered by 0x631e70 | S-STATS-BONUS | [DOC/open] |
| 0xbe3c80 | variant writer (writes its `named_equipment_bonuses` handle list) | S-DOCTRINE-SAVE | [DOC/load] |
| 0xc3ad50 | ship InitPostRead: XP modifiers (0xc3ce50), then 0xc3ba50 | S-DOCTRINE-SAVE | [DOC/load] |
| 0xc3ba50 | ship refresh after load: `RefreshStats(ship, 1)` (organisation clamped to max), HP / organisation set to max when 0 | S-DOCTRINE-SAVE | [DOC/load] |
| 0xc97e00 | equipment bonus key match: equipment name = type / archetype (`+0x4d8`) / archetype `+0x4e8`; or type keyword bit vs `type+0x540 \| +0x548`; or 0x22f740 lookup. `+0x4e8` = `family`, 0x22f740 = equipment group database ([MIO/match]) | S-DOCTRINE-EFFECTS, S-STATS-BONUS, S-MIO-POLICY | [DOC/effects], [MIO/match] |
| 0xd07660 | append a 0x70-byte sub-unit bonus source `{block, source, id, index}` | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0xd0e490 | add a doctrine sub-unit block to `[country+0xf70]` and rebuild the named sub-units | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0xd11bd0 | deployment (`[country+0xf70]`) InitPostRead: create a copy per unlocked sub-unit (0xd0e200), then rebuild every copy from technologies + `unit_modifiers` (0x101b1b0) | S-DOCTRINE-SAVE | [DOC/load] |
| 0xd12330 / 0xd14650 | deployment reader / writer (`unit_modifiers`, `unlocked_subunits`) | S-DOCTRINE-SAVE | [DOC/load] |
| 0xd12d20 / 0xd12b20 | rebuild the country sub-unit copies named in a block (types, categories) → 0x101b1b0 | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0xe60110 | add named equipment bonuses (`[country+0xf68]+0x178`, new handle each); `instant` → every matching variant (0xbd2f30) | S-DOCTRINE-EFFECTS, S-STATS-BONUS | [DOC/effects] |
| 0xe71cb0 | equipment manager writer: `named_equipment_bonuses`, `available_equipments`, `last_named_equipment_bonus` | S-DOCTRINE-SAVE | [DOC/load] |
| 0xee0400 | `CTechnologyStatus::InitPostRead`: re-runs the researched handler 0xee3e70 for every completed technology (creates sub-unit copies) | S-DOCTRINE-SAVE | [DOC/load] |
| 0xf88330 | equipment `type` keyword → bit (convoy 0x1, capital_ship 0x40, submarine 0x80, screen_ship 0x100, carrier 0x400000); full map in [WINGS/types] | S-DOCTRINE-EFFECTS | [DOC/effects], [WINGS/types] |
| 0xfc69e0 | folder: track status by index | S-DOCTRINE-* | [DOC/map] |
| 0xfc6a20 | `FolderStatus::SetGrandDoctrine`: reset and unassign every track, resize, swap grand doctrine effects (source 3) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc6d90 | `FolderStatus::SetSubdoctrine(folder, i, sub)` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc6eb0 | `FolderStatus::GetDailyMastery`: gain, MIN_MASTERY_GAIN_PER_DAY floor, × MASTERY_BANK_CONVERSION_RATE on complete or empty tracks | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc7d90 | compute the daily gain of every active track → `track+0x48` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc7eb0 | apply the daily gain: AddToBank (complete or empty) or AddMastery | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc8120 / 0xfc8170 / 0xfc81c0 | track i: unlocked count / subdoctrine / track template | S-DOCTRINE-* | [DOC/map] |
| 0xfc8250 | subdoctrine in the folder's completed list | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc8530 / 0xfc8590 | folder InitPostRead (drops tracks without grand doctrine) / set folder and track back pointers | S-DOCTRINE-SAVE | [DOC/load] |
| 0xfc85d0 / 0xfc8620 | track i has a subdoctrine and is complete / has a subdoctrine | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc8670 | track filter match `{track, folder, grand doctrine, subdoctrine, index}` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc8730 | track index valid | S-DOCTRINE-* | [DOC/map] |
| 0xfc87b0 | `FolderStatus::CompleteTrack`: add to `completed_subdoctrines`, apply the milestone of that track index (source 6) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc8ce0 | `FolderStatus::RemoveMilestone` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0xfc9020 | folder status reader (`folder`, `tracks`, `grand_doctrine`, `completed_subdoctrines`) | S-DOCTRINE-SAVE | [DOC/load] |
| 0xfc99f0 | folder status save writer | S-DOCTRINE-SAVE | [DOC/save] |
| 0x13c60b0 / 0x13c6600 | country (handle / tag) → country doctrine status | S-DOCTRINE-* | [DOC/map] |
| 0x13ccee0 / 0x13ccf90 | per country: compute (0xfc7d90) / apply (0xfc7eb0) daily mastery; from the doctrine system update 0xd7ed90 | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x13cd100 | console `mastery` worker: complete or empty track → AddToBank, else AddMastery | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x13cd240 | `ApplyDoctrineEffects(cds, effects, source, name, id, index)`: effect block, modifiers dirty, tactics, sub-unit block (0xd0e490), equipment bonuses (0xe60110); source 3 grand, 4 subdoctrine, 5 reward, 6 milestone | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0x13cda80 | collect track statuses matching a filter | S-DOCTRINE-* | [DOC/mastery] |
| 0x13cdc50 | `AddDoctrineModifiers`: grand doctrine, slotted subdoctrines, unlocked rewards, milestones of complete tracks, weight 1.0 | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0x13cde50 | country doctrine status InitPostRead: compaction and missing folders only, no effect application | S-DOCTRINE-SAVE | [DOC/load] |
| 0x13cee00 | `RemoveDoctrineEffects(cds, effects, source, id, index)` (0xd13280 sub-unit block, 0xe6f170 named bonuses) | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0x13cf486 | country doctrine status save writer | S-DOCTRINE-SAVE | [DOC/save] |
| 0x147bfd0 | `TrackStatus::AddToBank`: `bank = clamp(bank + x, 0, MASTERY_BANK_MAX)` (leaf) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147c000 | `TrackStatus::AddMastery`: cumulative mastery, greedy reward unlock in order, CompleteTrack when all unlocked | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147c1d0 | mastery needed for the next reward (UI) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147c2a0 | `TrackStatus::IsCompleted`: sub set and `unlocked >= rewardCount` (leaf) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147c2c0 | track status save reader | S-DOCTRINE-SAVE | [DOC/save] |
| 0x147c600 | unassign subdoctrine: Reset + remove subdoctrine effects | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147c650 | `TrackStatus::Reset`: remove reward effects, zero unlocked / mastery / bank, remove milestone | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147c8a0 | `TrackStatus::SetSubdoctrine`: Reset, swap subdoctrine effects, `AddMastery(old bank)` | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147cb00 | `TrackStatus::UnlockReward(track, reward, i)` (source 5) | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x147cd00 | track status save writer | S-DOCTRINE-SAVE | [DOC/save] |
| 0x1492ac0 | variant builder: a new variant copies its parent's idea bonuses and named-bonus handles (0xbe1550) | S-STATS-BONUS | [DOC/open] |
| 0x1529a80 | activation effects reader: `effect`, `modifier`, `equipment_bonus`, `enable_tactic`, sub-unit/category blocks, other keys as modifiers | S-DOCTRINE-EFFECTS | [DOC/effects] |
| 0x1529f40 / 0x1529a30 | doctrine equipment bonus record writer / reader (`id`, `index`, `equipment_bonus`) | S-DOCTRINE-SAVE | [DOC/load] |
| 0x197f890 / 0x197fa10 | sub-unit bonus source reader / writer (`subunit_stat_bonus_persistent.cpp`): block, `type` (project 1, effect 2, grand_doctrine 3, sub_doctrine 4, rewards 5, milestones 6), `number`, `id`, `localization_key` | S-DOCTRINE-SAVE | [DOC/load] |
| 0x1a32f40 / 0x1a33390 / 0x1a33830; 0x1a33240 / 0x1a33ac0 / 0x1a33210 | daily mastery terms called by 0xfc6eb0 (faction sharing?, attaché, theater commander; gain factor modifiers), not decoded | S-DOCTRINE-MASTERY | [DOC/mastery] |
| 0x24be690 | generic persistent block reader (vtable +0x18): reads the members, then InitPostRead (vtable +0x40) at 0x24beb7b if the parser has no error (0x24c0050) | S-DOCTRINE-SAVE | [DOC/load] |

### MIO include and trait tree (S-MIO-INCLUDE)

From [S-MIO-include-and-tree.md][MIOI] (`industrial_org_template.cpp`, `industrial_org_trait.cpp`, `industrial_organisation.cpp`): the MIO database reader, `include` resolution, the trait merge, positions and the unlock check. Key token ids are in [MIOI]. Sorted by RVA. The bonus sum and equipment matching (0xa4fe60, 0xdb7800) are in S-MIO-equipment-matching.md.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x14b7a0 | MIO trait move constructor (0x3d8 bytes) | S-MIO-INCLUDE | [MIOI/map] |
| 0x63f3c0 | stat list: add the entries of one kind (1 equipment, 2 production) of another list, summing equal stats | S-MIO-INCLUDE | [MIOI/merge] |
| 0x640ad0 | stat list: remove the entries of one kind | S-MIO-INCLUDE | [MIOI/merge] |
| 0x641040 / 0x641090 | stat list has an equipment / production stat | S-MIO-INCLUDE | [MIOI/merge] |
| 0xa4c3d0 | trait: append a parent group `{traits, needed = 1}` | S-MIO-TREE | [MIOI/read] |
| 0xa4c640 | trait `position` reader (x, y) | S-MIO-TREE | [MIOI/read] |
| 0xa4ce80 | `parent` block reader (`traits`, `num_parents_needed`) | S-MIO-TREE | [MIOI/read] |
| 0xa4d310 | equipment filter copy (`equipment_type`, `limit_to_equipment_type`) | S-MIO-INCLUDE | [MIOI/merge] |
| 0xa4d500 / 0xa4d510 | token == / token < (leaf) | — | [MIOI/map] |
| 0xa4e2c0 | trait position: `position`, or relative trait's position (first match in the owner MIO, recursive) + `position`; missing → (0, 0) | S-MIO-TREE | [MIOI/pos] |
| 0xa4e480 / 0xa4fd50 | initial trait: warn for keys initial traits ignore | S-MIO-INCLUDE | [MIOI/map] |
| 0xa4f4c0 / 0xa4f710 | trait: `mutually_exclusive` tokens / parent groups (copies) | S-MIO-TREE | [MIOI/unlock] |
| 0xa4fab0 | trait post-read checks (`num_parents_needed` > count, non-MIO organization modifiers), log only | S-MIO-TREE | [MIOI/read] |
| 0xa4fcf0 / 0xa4fd20 | trait `available` / `visible` evaluate: own block, else the included trait's (fallback `+0x350` / `+0x358`) | S-MIO-TREE | [MIOI/unlock] |
| 0xa50000 | trait `ReadMember` (token switch; `any_parent` / `all_parents` / `parent` all append parent groups) | S-MIO-INCLUDE | [MIOI/read] |
| 0xa507b0 | `MergeTrait(dst, mio, src)`: every key dst leaves empty (and does not list in its `delete_included_values`) is taken from src; lists and parent groups replaced whole; bonuses per kind | S-MIO-INCLUDE | [MIOI/merge] |
| 0xa50dc0 | trait: take the included value? (own empty and key not in the trait's `delete_included_values`; both → warning, own kept) | S-MIO-INCLUDE | [MIOI/merge] |
| 0xa51300 / 0xa51450 | trait list: append an empty trait / move a range (erase) | S-MIO-INCLUDE | [MIOI/map] |
| 0xa519f0 | MIO trait constructor (position −1/−1) | S-MIO-TREE | [MIOI/map] |
| 0xa51d60 | MIO checks after include: unknown include, empty `allowed`, unmatched `override_trait` (log only); calls the five checks below | S-MIO-INCLUDE | [MIOI/include] |
| 0xa52080 / 0xa525c0 / 0xa52880 / 0xa52d10 / 0xa52f50 | MIO checks: positions / duplicate tokens / mutual exclusion exists and is reciprocal / parents exist / `remove_trait` (log only) | S-MIO-TREE | [MIOI/map] |
| 0xa53450 (chunk 0xa53523) | MIO database post-load: for each MIO in list order resolve its include (0xa54150), check (0xa51d60), register trait tokens | S-MIO-INCLUDE | [MIOI/include] |
| 0xa53be0 | MIO `ReadMember` (token switch; `trait` and `add_trait` share one handler, `override_trait` goes to its own list) | S-MIO-INCLUDE | [MIOI/read] |
| 0xa54150 | `ResolveInclude(mio, included)`: top-level fields, initial trait, then per included trait: remove_trait → skip, override_trait → move in and merge, own trait with that token → merge into it, else copy | S-MIO-INCLUDE | [MIOI/include] |
| 0xa54aa0 | MIO: take the included value? (own empty and key not in the MIO's `delete_included_values`) | S-MIO-INCLUDE | [MIOI/include] |
| 0xdb6020 | country MIO enabled: template `visible` and `available`, `+0x134 > 0` | S-MIO-TREE | [MIOI/unlock] |
| 0xdba8e0 | trait parents hold: every group needs `needed` of its traits unlocked (AND over groups; no group → true) | S-MIO-TREE | [MIOI/unlock] |
| 0xdbaac0 | unlocked-trait set contains token | S-MIO-TREE | [MIOI/unlock] |
| 0xdbb090 | trait excluded: one of its own `mutually_exclusive` tokens is unlocked (one-directional) | S-MIO-TREE | [MIOI/unlock] |
| 0x13af360 | `CanUnlockTrait`: not unlocked, visible, parents, not excluded, available (+ MIO enabled when asked); tooltips | S-MIO-TREE | [MIOI/unlock] |

### MIO equipment matching and bonus sum (S-MIO-MATCH)

From [S-MIO-equipment-matching.md][MIO]: the equipment-type match shared by `limit_to_equipment_type`, the MIO's `equipment_type`, policy keys and equipment bonus keys; the type set of a variant; the bonus sum; the assignment check; when the bonus is computed. Sorted by RVA. 0xbd3b80, 0xdb7800 and 0xa4fe60 are under Design stats above; the readers 0xa50000 / 0xa53be0 and the merge 0xa507b0 under S-MIO-INCLUDE. 0x63f3c0's mode 3 adds every stat kind (0x640d20).

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x10bae50 | AI design: copy a variant, recompute its MIO bonus (0xbd3b80) | S-MIO-MATCH | [MIO/snap] |
| 0x13ae4b0 | trigger evaluate `(trigger, scope, tooltip)`; empty trigger (`+0x14 == 0`) → true | S-MIO-MATCH | [MIO/sum] |
| 0x13e81b0 | token → EEquipmentStats: unknown → sink 0x4e; `naval_weather_penalty_factor` (0x3fcd) → 0x2a | S-STATS-MIO | [MIO/sum] |
| 0x1491ec0 / 0x14925d0 / 0x1492970 / 0x1492d50 | equipment designer (variant at `+0x1370`): design team change → 0xbd3b80; back to the original MIO restores its saved snapshot (`+0x2218`, count `+0x2238`) unless `+0x2208` | S-MIO-MATCH | [MIO/snap] |
| 0x152f4a0 | module reader: `add_equipment_type` (0x3b91) → module `+0x78` | S-MIO-MATCH | [MIO/type] |
| 0x1e1150 | create-variant command: design team (0xbe1c40), then the command's snapshot (0xbe1d00) or 0xbd3b80 | S-MIO-MATCH | [MIO/snap] |
| 0x1f72870 (chunk 0x1f7291f) | designer opens a variant: copies its bonus and trait count, assigns the MIO, recomputes | S-MIO-MATCH | [MIO/snap] |
| 0x22f740 | equipment group database lookup by token (robin-hood `+0x48`, slots 0x38, item `+0x10`); the 0xc97e00 third lookup | S-MIO-MATCH, S-STATS-BONUS | [MIO/match] |
| 0x2fbd80 | `complete_mio_trait` effect execute (CUnlockTraitEffect vtable +0x68) → 0xdbc860 | S-MIO-MATCH | [MIO/snap] |
| 0x4562a0 | string list contains (hash `+0x20`, then case-sensitive compare) | S-MIO-MATCH | [MIO/match] |
| 0x456890 | `has_mio_equipment_type` evaluate (CHasEquipmentTypeTrigger vtable +0xb0): literal name in the MIO's `equipment_type` expanded with group names | S-MIO-POLICY | [MIO/assign] |
| 0x631f10 | equipment group database instance (data 0x332eed0) | S-MIO-MATCH | [MIO/match] |
| 0x640d20 | stat kind filter: 1 equipment (0x6412d0), 2 production (0x641590), 3 any | S-STATS-MIO | [MIO/sum] |
| 0x9f8460 | module lookup by token (equipment database `+0xe0`, count `+0xec`); miss → element 0 | S-MIO-MATCH | [MIO/type] |
| 0x9f86f0 / 0x9f8700 → 0x9e93e0 | equipment lookup by name; miss → element 0, a reserved null entry | S-MIO-MATCH | [MIO/match] |
| 0xa08fe0 / 0xa09290 | equipment group expansion with / without group names (0xa0b1b0) | S-MIO-MATCH | [MIO/match] |
| 0xa09e80 | build an equipment group's token cache `+0x70` (sets `+0x88`) | S-MIO-MATCH | [MIO/match] |
| 0xa0a430 | type vs token list: name of the type / archetype / archetype's family listed, or a listed keyword's bit in `0xc95730(type)` | S-MIO-MATCH | [MIO/match] |
| 0xa0a550 | the same against a string list (0x4562a0, 0xf882f0) | S-MIO-MATCH | [MIO/match] |
| 0xa0a600 | `CAnonymousEquipmentGroup::Matches` (vtable +0x48): token cache, else expand + 0xa0a550 | S-MIO-MATCH | [MIO/match] |
| 0xa0a950 | `CEquipmentGroup::Matches` (named group, vtable +0x48): expand + 0xa0a550 | S-MIO-MATCH | [MIO/match] |
| 0xa0ada0 | equipment group empty: `+0x5c == 0` (written entries; leaf) | S-MIO-MATCH | [MIO/match] |
| 0xa0ae40 | equipment group cache rebuild on database reload (listener at `+0x68`, slot +0x10) | S-MIO-MATCH | [MIO/match] |
| 0xa0b1b0 | expand an equipment group's entries: names of `common/equipment_groups` groups replaced recursively (flag: keep the group name), others added once | S-MIO-MATCH | [MIO/match] |
| 0xa0c0c0 | fill an equipment group's entry list `+0x50` with the script words as read (no filtering) | S-MIO-MATCH | [MIO/match] |
| 0xa4a7f0 | policy key match: 0xc97e00, or `same_as_mio` and 0xdb6e90 | S-MIO-POLICY | [MIO/sum] |
| 0xa4b260 | policy bonus: every matching entry of the policy's one bonus map (equipment + production) added with mode 3 | S-MIO-POLICY | [MIO/sum] |
| 0xa4bac0 | any policy bonus key matches the type (assignment check) | S-MIO-POLICY | [MIO/assign] |
| 0xa4be60 / 0xa48e70 | policy reader: `equipment_bonus` and `production_bonus` into one key → stat-list map | S-MIO-POLICY | [MIO/sum] |
| 0xa51690 | constructor of a trait's `+0x60` group (vtable 0x2719668) | S-MIO-MATCH | [MIO/map] |
| 0xa53260 | MIO `equipment_type` (template `+0xd0`) Matches(type) | S-MIO-MATCH | [MIO/assign] |
| 0xa53660 | trait by token in template `+0xb8` (via `mio+0x110` = template + 0x88) | S-MIO-MATCH | [MIO/sum] |
| 0xa536f0 / 0xa53850 | country MIO `available` / `visible` trigger (via `mio+0x118` = template + 0x218, with the include fallbacks) | S-MIO-MATCH | [MIO/sum] |
| 0xbd3a50 / 0xbe1960 | variant role mask `+0x408` (installed modules' `add_equipment_type` included; not read by the MIO match; partly decoded), then 0xbe2450 | S-MIO-MATCH, S-DESIGN-TYPE | [MIO/type] |
| 0xbe1c40 / 0xbe1d00 | set a variant's design team / set its bonus snapshot and trait count from a list | S-MIO-MATCH | [MIO/snap] |
| 0xbe2450 | validate a variant's design team (0xdb6fd0); on failure clear handle, bonus, `+0x4b0` | S-MIO-MATCH | [MIO/assign] |
| 0xbe3c80 | variant writer: `design_team`, `design_team_bonus`, `number_of_design_team_traits` | S-MIO-MATCH | [MIO/snap] |
| 0xa50690 / 0xa51170 | MIO trait reference reader / writer: one key, `trait` (0x2e8e), a string; saved as `unlocked={ trait="…" }` | S-MIO-SAVE | [MIO/save] |
| 0xc922a0 / 0xc924b0 | equipment type constructors: `+0x10 = 1`, parent and family = self | S-MIO-MATCH | [MIO/type] |
| 0xc949d0 | equipment type post-load: `+0x548` = OR of the own `default_modules`' `add_equipment_type` | S-MIO-MATCH | [MIO/type] |
| 0xc95730 | equipment type keyword mask `(+0x540 or archetype's) \| +0x548`, normalized | S-MIO-MATCH | [MIO/type] |
| 0xc99410 | equipment type reader: `parent` +0x4e0, `archetype` +0x4d8, `is_archetype` +0x555, `family` +0x4e8, `type` +0x540, `default_modules` +0x88 | S-MIO-MATCH | [MIO/type] |
| 0xdb3c80 | country MIO init: `+0x110` = template + 0x88, `+0x118` = template + 0x218, `+0x120` owner | S-MIO-MATCH | [MIO/map] |
| 0xdbabe0 | country MIO post-read: "A trait in save file for: X does not exist in database." per unlocked token missing from the trait database | S-MIO-SAVE | [MIO/save] |
| 0xdbb350 | country MIO member reader: `unlocked` (set `+0x140`, 0xdb38b0), `queued_trait` (`+0x150`), `policy` (token → `+0x188`), `name`, `icon`, `size`, `funds`, `history`, … | S-MIO-SAVE | [MIO/save] |
| 0xdb6e90 | `same_as_mio`: the MIO's `equipment_type` matches the type | S-MIO-POLICY | [MIO/sum] |
| 0xdb6fd0 → 0xdb6f40 | can the MIO be on this equipment type: `equipment_type` matches, or a policy key does | S-MIO-MATCH | [MIO/assign] |
| 0xdbc860 | country MIO UnlockTrait: set insert, trait points `+0x134` − 1 (asserts `_TraitPoints > 0`), `on_complete`, modifiers, history; no variant refresh | S-MIO-MATCH, S-MIO-TREE | [MIO/snap] |
| 0xdbccc0 | country MIO save writer (key order in [MIO/save]); `policy` only when not 0x4c17 | S-MIO-SAVE | [MIO/save] |
| 0xe59400 | country `industrial_organisations` reader: MIO looked up by key (unknown: "MIO X is in save but not in DB", skipped), created with 0xdb3c80 and read; `allowed` not checked | S-MIO-SAVE | [MIO/save] |
| 0xe5d0d0 | policy by id in `[country+0xf68]+0x148` (count `+0x154`) | S-MIO-POLICY | [MIO/sum] |
| 0xf882f0 / 0xf8b070 | string → type keyword bit / reader variant ("incorrect type category") | S-MIO-MATCH | [MIO/match] |
| 0xf8b020 | type mask normalization: capital_ship clears convoy/submarine/screen_ship; else submarine clears convoy/screen_ship; else screen_ship clears convoy (only with a bit of 0x80004003c1) | S-MIO-MATCH, S-DOMINANCE | [MIO/type] |

### Air wings, joins and reinforcement (A-WINGS)

From [A-WINGS-carrier-wings-and-reinforcement.md][WINGS]: the type keyword bits, which wings strike, land-based joins and the daily air reinforcement. Sorted by RVA. Rows here can repeat an RVA of the main table with the A-WINGS reading.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x6cad9c | country ctor: `country+0xf78` = air CReinforcementStatus (0x15097f0) | A-REINFORCE | [WINGS/reinforce] |
| 0x6cf8c0 | country → its CStrategicAir (`gamestate+0x690 → +0x30[countryIdx]`) | A-REINFORCE | [WINGS/objects] |
| 0x6e76a0 (call 0x6e784c) | `country.daily` → `deployment.daily` 0xd0ffc0(country+0xf70, +0xf78, +0xf80, +0xfd0) | A-REINFORCE | [WINGS/reinforce] |
| 0xbd3a50 | variant type mask `variant+0x408`: (own `type` or archetype's) ∪ modules' `add_equipment_type`, ∩ archetype `allowed_types`, minus module +0x88; a `type_override` slot's module replaces it; writers 0xbdf910, 0xbe1960 | A-WINGS | [WINGS/types] |
| 0xbdf456 (write 0xbdf58f) | variant allowed missions `variant+0x410` (type + modules `allow_mission_type`, minus mine bits without the stats and DLC-gated 0x4000 / 0x200) | A-WINGS | [WINGS/strike] |
| 0xc3afb0 | ship has a module with flag `+0x2d0` (mega-carrier wing size); cached at ship+0x801 / +0x802 | A-REINFORCE | [WINGS/reinforce] |
| 0xc3fcd0 (comparator 0xc40380) | stable sort of wings by reinforcement score, descending; ties lower wing id first | A-REINFORCE | [WINGS/reinforce] |
| 0xc4f860 | take manpower for planes from the air delivery budget (`+0x188`, or the exile entry); returns what fits | A-REINFORCE | [WINGS/reinforce] |
| 0xc505f0 | create a wing at a base from an equipment pool; merge flag → existing pool (0xc514f0), else new pool (0xf5a480) | A-WINGS | [WINGS/objects] |
| 0xc514f0 | find the country's pool at a base for a variant: sub-unit type ∩ variant+0x408 ≠ 0 and the sub-unit needs the variant's type | A-WINGS | [WINGS/objects] |
| 0xc536b0 | base → the country's pool list (`base+0x30[idx]+0xb8`) | A-WINGS | [WINGS/strike] |
| 0xc57400 | base has an entry for the country | A-WINGS | [WINGS/strike] |
| 0xc57d30 | hourly strategic-air update (from 0x1dfda5): for naval combats in an air hour resets both sides' `+0x3e8` (0xc584e4); runs each mission with `executing_mission & 0x1f7fa` through 0xf82110 (0xc58582) | A-EXTERNAL | [WINGS/ext] |
| 0xc5c0a0 | air Deliver(variant, amount): wings of matching pools, reinforce list (0xf5cc60) then swap list (0xf5d0a0), each sorted by score | A-REINFORCE | [WINGS/reinforce] |
| 0xc63d90 | end of the daily distribution: return swapped-out planes (0xf66dd0) | A-REINFORCE | [WINGS/reinforce] |
| 0xc95710 | equipment type's own `type` mask, or the archetype's (`+0x540`; no default-module bits) | A-WINGS | [WINGS/types] |
| 0xd0ffc0 | `deployment.daily` → 0xd10310 | A-REINFORCE | [WINGS/reinforce] |
| 0xd10310 | daily equipment distribution: per stockpile variant (`[country+0xf68]+0x200`, sorted 0xbd2420) collect distributables (0xd0d980), sort (0xd0bdd0 / 0xd0c650), offer the whole amount (`vt+0x10`), remove the used part (0xe6eb90) | A-REINFORCE | [WINGS/reinforce] |
| 0xf59760 / 0xf59c00 / 0xf59f70 / 0xf5a1f0 | CAirWing ctors; reinforce timer `+0xa20` = REINFORCEMENT_DISABLING_DURATION_IN_LAND_CARRIER_TRANSFER; priority `+0x28` = REINFORCEMENT_AIRBASE_PRIORITY_DEFAULT; preference `+0x9cc` = 1 | A-REINFORCE | [WINGS/reinforce] |
| 0xf5a480 | CAirWingPool ctor (0x48 bytes; vtable 0x297ae38) | A-WINGS | [WINGS/objects] |
| 0xf5aad0 | wing owner tag: `wing+0x9c4` if set (exile), else `[pool+0x40]+0x90` | A-WINGS | [WINGS/objects] |
| 0xf5add0 | AddAirplanes(wing, variant, n, xp): equipment pool += n, `wing+0x7c` = pool count ("AddAirplanes - nAdd") | A-REINFORCE | [WINGS/reinforce] |
| 0xf5cc60 | CanReinforce(wing, variant): below max size, ready, not transferring, timer expired, same allowed missions, `+0xa28 == variant+0x42c`, **not on a carrier whose task force is in combat** (0xf5cda2), preference | A-REINFORCE | [WINGS/reinforce] |
| 0xf5d0a0 | CanSwap(wing, variant): same missions, needed type, preference, holds a variant that 0xbd7dc0 accepts; no combat check | A-REINFORCE | [WINGS/reinforce] |
| 0xf5f000 | wing max size: land `land_air_wing_size`, carrier `carrier_air_wing_size` / mega / submarine (0x101b080); sub-unit type bit 33 → base capacity `base+0x78` | A-REINFORCE | [WINGS/reinforce] |
| 0xf61f70 | reinforcement score `priority × 1000 + (mission type ≠ 0 ? 500 : 0) + missing planes` | A-REINFORCE | [WINGS/reinforce] |
| 0xf62750 | wing hourly update: ready timer `+0x64 → +0x60`, not-ready wings into `strategicAir+0x70`. **disagrees** (MSAVE): its only caller path found is the air manager daily 0xc50040 → 0xc46030 → 0xf62a50 | A-REINFORCE | [WINGS/reinforce], [MSAVE/eff] |
| 0xf63100 | fighter-type test on the pool's sub-unit `type` (`& 0x100000c00`) | A-INTERCEPT, A-WINGS | [WINGS/types] |
| 0xf632c0 | reinforcement preference `wing+0x9cc`: 0 obsolete only, 1 any, 2 only current, 3 only same ("unhandled reinforcement setting") | A-REINFORCE | [WINGS/reinforce] |
| 0xf63fa0 | Reinforce(wing, variant, amount): `n = min(amount, max − planes)`, manpower-limited, AddAirplanes; returns the rest | A-REINFORCE | [WINGS/reinforce] |
| 0xf64190 | Swap(wing, variant, amount): add `min(amount, planes)` new planes, `wing+0x88 +=` the same | A-REINFORCE | [WINGS/reinforce] |
| 0xf64af0 | experience for the planes used on a mission | A-EXTERNAL | [WINGS/ext] |
| 0xf667b0 | available missions `wing+0x30` = equipment missions `+0x2c`, + naval kamikaze for fighter-type sub-units of a kamikaze-capable country, + mines (DLC 0x13), − training, − air supply below MIN_PLANE_COUNT_AIR_SUPPLY | A-WINGS | [WINGS/strike] |
| 0xf66dd0 | return `wing+0x88` planes to the stockpile (0xe6f910) and recount | A-REINFORCE | [WINGS/reinforce] |
| 0xf78160 | external efficiency = disruption factor (see the main table) | A-EXTERNAL | [WINGS/ext] |
| 0xf78360 | mission plane budget: `round(planes × effectiveness)`; carrier × CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[stance]; missile / kind-2 bases | A-EXTERNAL | [WINGS/ext] |
| 0xf82110 | mission execution in an air hour: dispatch on `executing_mission` (0x10 / 0x80 naval, 0x8000 patrol, 0x100 port, …); joins targets while budget remains, not for 0x8 / 0x10000 | A-EXTERNAL | [WINGS/ext] |
| 0xf82ca0 | mission joins one combat target: disruption eff (0xf78160), side `vt+0xf0` = 0x16205c0 with sub = 0 | A-EXTERNAL | [WINGS/ext] |
| 0xf84d40 / 0xf87820 | air mission reader / writer: `type` +0x10, `executing_mission` +0x14, `period` +0x18, `active` +0x20, `effectiveness` +0xd8, `effective_planes_count` +0xe0 | A-WINGS | [WINGS/objects] |
| 0xf852a0 | mission enable / disable a mission bit (`type`); clears `executing_mission` when its bit goes | A-WINGS | [WINGS/strike] |
| 0xf856b0 | mission SetType (`type` = bits) | A-WINGS | [WINGS/strike] |
| 0xf88330 | type keyword → bit, full map | A-WINGS | [WINGS/types] |
| 0x101b080 | sub-unit wing size by airbase kind: 0 `land_air_wing_size` +0x600, 1 `carrier_air_wing_size` +0x604, 2 mega +0x608, 3 `submarine_carrier_air_wing_size` +0x60c | A-REINFORCE | [WINGS/reinforce] |
| 0x15097f0 | air CReinforcementStatus ctor (vtable 0x29d1e48, base CEquipmentDistributable 0x15a7410); `+0x38` country | A-REINFORCE | [WINGS/reinforce] |
| 0x150d9f0 | air CReinforcementStatus vt+0x08: planes of a variant the wings need (0xc4e8a0) | A-REINFORCE | [WINGS/reinforce] |
| 0x150e150 | air CReinforcementStatus vt+0x10 Deliver → 0xc5c0a0 | A-REINFORCE | [WINGS/reinforce] |
| 0x161ff10 | side: add a split wing to the external AirFX of the original wing | A-EXTERNAL | [WINGS/ext] |
| 0x16205c0 | external join `(side, wing, &budget, eff, sub)` | A-EXTERNAL | [WINGS/ext] |
| 0x1978e30 (0x1978ec3..0x1978fb5) | strike averages use only strike wings with `executing_mission & (port strike ? 0x100 : 0x90)` | A-DMG | [WINGS/strike] |
| 0x1979770 | AirFX AddWing(airfx, wing, base, addOffered): all-wing list; strike list if `wing+0x30 & 0x190`; `+0x58 += planes` if addOffered | A-WINGS | [WINGS/strike] |
| 0x1979aa0 (chunk 0x1979ae6) | AirFX AddPool(airfx, pool): each wing as 0x1979770, `+0x58 +=` planes | A-WINGS | [WINGS/strike] |
| 0x197a1dc | loss allocation: strike wings with `executing_mission & 0x190`, per wing ≤ min(planes, `airfx+0x58`) | A-LOSSES | [WINGS/strike] |
| 0x197a6a0 | external AirFX update: `+0x5c` recount, `+0x58 = min(add, +0x5c)`, `+0x70` date, `+0xc0` eff, `+0x80 = 0`. **disagrees**: AIR calls it "find external AirFX for an air base"; the find is the loop in 0x16205c0 over side+0x100 | A-EXTERNAL | [AIR/ext], [WINGS/ext] |
| 0x1c6a7c0 | group AddMember(group, ship, flag): one AirFX per non-empty wing pool of the ship's base (0xc536b0), unless a submarine; wings get the combat only in normal combats (+0x10c / +0x10d clear) | A-WINGS | [WINGS/strike] |
| 0x1c6c120 | add a wing to the AirFX holding another wing (split) | A-WINGS | [WINGS/strike] |
| 0x1c6c1e0 | wing changed base in a group: removed from its AirFX (0x197a5a0), new AirFX if the new base is a carrier member of the group | A-WINGS | [WINGS/strike] |

### Air mission save and execution (A-MISSION-SAVE)

From [A-MISSION-SAVE-air-mission-save-format.md][MSAVE]: the mission's save keys, what a load rebuilds, where region missions are kept and who sets `executing_mission`. Sorted by RVA. Rows here can repeat an RVA of the A-WINGS table with the MSAVE reading.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x1d4810 | `gamestate.daily` (calls the air manager daily 0xc50040 at 0x1d497e) | A-WINGS | [MSAVE/eff] |
| 0x700520 | country `+0xf88` → 0xd3fd60(…, 0); used as "may be active" by EnableMission (probably at war) | A-WINGS | [MSAVE/exec] |
| 0x714830 | reserve command power `(country, reservation, flag)`: fails above NCountry.BASE_MAX_COMMAND_POWER + modifiers − used | A-WINGS | [MSAVE/read] |
| 0x7191b0 (0x71a8a3) | country writer: country `+0x180` list written as `air_theatre` | A-WINGS | [MSAVE/lists] |
| 0xb0f470 | wing busy: `+0x79`, ready timer `+0x64 < +0x60`, or a transfer target `+0x48` | A-WINGS | [MSAVE/exec] |
| 0xc46030 / 0xc46440 | air manager daily per-country job: … each pool's wings 0xf62a50 (parallel task vtable 0x2959ea0) | A-WINGS | [MSAVE/eff] |
| 0xc49e80 | parallel body of 0xc58660: per region, land missions `strategicAir+0x98[r]`: CanExecute (0xf79a00) → `executing_mission` = 0xf7a420, else 0 on land bases; carrier list `+0x140[r]`: 0xf79c10 → 0xf7acc0, else 0; region record `+0xe0[r]+0x9a` = executing & 0xf7fa | A-WINGS, A-EXTERNAL | [MSAVE/exec] |
| 0xc50040 | air manager daily ("airmanager.daily", from 0x1d4810) | A-WINGS | [MSAVE/eff] |
| 0xc58660 | `executing_mission` refresh, called by the hourly air update 0xc57d30 at 0xc58068 before the mission loop (0xc58582): per-region mission holders, region night coverage (0xe21640), parallel 0xc49e80 (task vtable 0x2959ec0) | A-WINGS, A-EXTERNAL | [MSAVE/exec] |
| 0xc5daa0 | register a mission: `_ActiveMissions[region id]` (`strategicAir+0x98`), `_AllActiveMissions` (`+0xb0`), `_ActiveMissionRegions` (`+0xc8`, sorted ids) | A-WINGS | [MSAVE/lists] |
| 0xc5dcb0 | unregister a mission (same lists) | A-WINGS | [MSAVE/lists] |
| 0xc66c60 | strategic air writer tail: `air_theatre_index` / `air_group_index` (id counters) | A-WINGS | [MSAVE/lists] |
| 0x1012f80 / 0x10132d0 | region provinces within the wing's range of its base (land base province or the carrier's fleet location) → list (16-byte entries {province, distance}) | A-WINGS | [MSAVE/read] |
| 0x1013de0 / 0x1014010 | mission keyword → bit / bit → keyword (`air_superiority` … `sam_mission`; leaf switches) | A-WINGS | [MSAVE/token] |
| 0x14ec490 / 0x14ec5a0 | air theatre writer (`air_theatre`, regions `+0x70`, groups) / air group writer (`country`, `name`, `air_group` members `+0x40`) | A-WINGS | [MSAVE/lists] |
| 0xf5e5c0 | wing range (fixed; whole km after ÷1e5) | A-WINGS | [MSAVE/read] |
| 0xf62a50 | pool: for each wing, 0xf68040 if ready and not transferring, then 0xf62750 | A-WINGS | [MSAVE/eff] |
| 0xf62bc0 | wing InitPostRead (vtable 0x297ade0 + 0x40, `this = wing+0x10`): ace check, 0xf661e0, coverage 0xf87480(force), command power 0xf87290 | A-WINGS | [MSAVE/read] |
| 0xf651b0 | wing ReadMember (vtable 0x297ade0 + 0x20, `this = wing+0x10`): `allow_mission_type` → `wing+0x2c`, `mission` → `wing+0x90`, `region_to_assign` / `mission_to_assign` → `wing+0x70` / `+0x74`, `other_combats`, `timed_disabling`, … | A-WINGS | [MSAVE/read] |
| 0xf65c70 | pool ReadMember (vtable 0x297ae38 + 0x20): new wing (0xf5a1f0), read, available missions 0xf667b0, append | A-WINGS | [MSAVE/read] |
| 0xf78500 | mission effectiveness `(mission, &out, isCarrier, a4, a5)`: coverage term (`1 − +0x50`), × `(1 − region_change_penalty)` unless a4 or `type & 0x60` | A-EXTERNAL | [MSAVE/eff] |
| 0xf796e0 | land chooser after AS / interception: CAS, strategic, barrage, naval bomber / kamikaze (0xf7d590 target), port strike, logistics, air supply, mines, recon, naval patrol, paradrop; first match with the bit in `type` and in `wing+0x30` | A-WINGS, A-EXTERNAL | [MSAVE/exec] |
| 0xf79a00 | CanExecute(mission, night coverage): type, region, active, coverage `+0x50`, available missions, wing not busy, planes, `period` vs DAY_NIGHT_COVERAGE_FACTOR, AGGRESSION_THRESHOLD, carrier-in-combat rule; sets `effectiveness` (0xf78500) and `effective_planes_count` (0xf78360) | A-WINGS, A-EXTERNAL | [MSAVE/exec] |
| 0xf79c10 | carrier CanExecute (not decoded) | A-WINGS | [MSAVE/exec] |
| 0xf79e40 | mission command power cost (NAir.MISSION_COMMAND_POWER_COSTS × planes) | A-WINGS | [MSAVE/read] |
| 0xf7a420 | land chooser: `type & 1` and `wing+0x30 & 1` → 0x1; `& 4` → 0x4; else 0xf796e0 | A-WINGS | [MSAVE/exec] |
| 0xf7a760 | SetRegion helper: list `mission+0x58` from the base position and the region (meaning ?) | A-WINGS | [MSAVE/read] |
| 0xf7acc0 | carrier chooser from `wing+0x30` only: fighter-type → AS, interception, kamikaze, naval bomber; others → kamikaze, naval bomber, AS, interception. **Settles** the A-WINGS carrier `executing_mission` setter | A-WINGS | [MSAVE/exec] |
| 0xf7d590 (body 0xf7d5e4) | naval targets of a mission: needs `+0x44 > 0` and `region+0xa0`; walks the in-range province list `+0x38`: naval combats (`province+0x170`, `vt+0x58 == 2`, 0x13e27f0 / 0x161f620), then spotted fleets (`province+0xf8`, NAVAL_STRIKE / NAVAL_RECON_DETECTION_BALANCE_FACTOR) | A-EXTERNAL | [MSAVE/exec] |
| 0xf80490 | region change penalty decay: `+0xc0 −= gain / 24` (0xf7ec90, EFFICIENCY_REGION_CHANGE_DAILY_GAIN_*), clamped [0, 1], only with `type != 0` and not training | A-EXTERNAL | [MSAVE/eff] |
| 0xf80930 | aggressiveness test: `planes / maxSize >= NAir.AGGRESSION_THRESHOLD[+0x1c]` | A-WINGS | [MSAVE/exec] |
| 0xf84d40 | air mission ReadMember: fields directly; `executing_mission` keyword → bit; `strategic_region` id → SetRegion(…, false) when `1 <= id < gamestate+0x2ec` | A-WINGS | [MSAVE/read] |
| 0xf852a0 | EnableMission: `type` (0x30085 bits replace the mask, others are added); `active` = base has the country, no training, and 0xf61600 > 0 / recon / paradrop / 0x700520; else `active` and `executing_mission` 0; then 0xf87290 | A-WINGS | [MSAVE/exec] |
| 0xf859d0 | SetRegion(mission, region, recompute): if changed: `executing_mission = 0`, `region_change_penalty = EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR`, unregister / register (0xc5dcb0 / 0xc5daa0), `+0x30`; recompute → range and coverage; `+0x58` list (0xf7a760) | A-WINGS | [MSAVE/read] |
| 0xf87290 | reserve command power for `type` (0xf79e40, 0x714830); failure → `type = 0` | A-WINGS | [MSAVE/read] |
| 0xf87480 | UpdateCoverage(mission, force): range `+0x78`, in-range province list `+0x38` / `+0x44`, coverage `+0x50 = min(1, count / region+0x24)` | A-WINGS, A-EXTERNAL | [MSAVE/read] |
| 0xf87820 | air mission writer: `type`, `period`, `active`, [`executing_mission` keyword], [`effectiveness`], [`effective_planes_count`], [`effective_air_superiority`], [`strategic_region` = region+0x58, `region_change_penalty`], `missions_done`, [`aggressiveness`], `priority`*, [`stop_training_at_max_xp`] | A-WINGS | [MSAVE/write] |

### Air aces in naval strikes (A-ACE)

From [A-ACE-aces-and-wing-experience-in-naval-strikes.md][ACE]: ace death and earn rolls after a naval strike, the ace template pick, and how aces and wing experience enter the plane stat getters. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x1e1120 | `gamestate+0x720 = random_int(gamestate.cpp, 0x980)` (caller 0xdd6a30); a term of the ace seed (meaning ?) | A-ACE | [ACE/rolls] |
| 0x221ef50 | hash of a `{type, id}` handle: `x = (w0 << 32) + w1; m = x × 0x49631a45; (m >> 32) ^ (uint32)m` (leaf) | A-ACE | [ACE/rolls] |
| 0x2234110 | `LightRandomLog(value, file, line, …)`: logs "random_state: %s:%d = %d", draws nothing | A-ACE | [ACE/rolls] |
| 0x534fe0 | script scope ctor with a random seed (light random mixA / mixB of the seed) | A-ACE | [ACE/rolls] |
| 0x6176c0 | CAce ctor (0x178 bytes, vtable 0x27de2a0): `+0xe0` country, `+0x158` alive = 1 | A-ACE | [ACE/objects] |
| 0x6182d0 | ace wing-size factor: `min(ACE_WING_SIZE / wing planes, ACE_WING_SIZE_MAX_BONUS)`; 1 without a wing or with 0 planes | A-ACE | [ACE/stats] |
| 0x618860 | create an ace `(aceDb, seed, country, &country.aces, wing)`: own hash generator from `seed` (no game RNG); template by `chance` among those whose `type` overlaps the wing's sub-unit type; names; attach (0xf5b320) only if the wing has no ace handle | A-ACE | [ACE/create] |
| 0x619b50 | ace → its template's effect holder (`[ace+0x170]+0x38`; leaf) | A-ACE | [ACE/stats] |
| 0x61a5c0 | ace → its country's tag (`[ace+0xe0]+8`; leaf) | A-ACE | [ACE/rolls] |
| 0x61a930 | ace modifier value `(ace, out, id, −1)`: `effect[id] × 0x6182d0 × (1 + country(ace+0xe0).ace_effectiveness_factor 0x25a)` | A-ACE | [ACE/stats] |
| 0x61bba0 | ace dies `(ace, kill type, killer handle, &killer country)`: `+0x158 = 0`, `+0x15c`, `+0x160`, `+0x168` | A-ACE | [ACE/death] |
| 0x61bc70 | set the ace's wing (`ace+0xe8`) | A-ACE | [ACE/objects] |
| 0x61bd30 | CAce writer (vt+0x10): id, `modifier`, `name`, `surname`, [`callsign`], [`is_female`], `portrait`, [`alive = no`], [`handled`], [`kill_type`], [`killer_name`], [`killer_country`] | A-ACE | [ACE/save] |
| 0x7191b0 (0x719ab0) | CCountry::Write: one `ace = { … }` (token 0x31e2) per `country+0x12d8` entry | A-ACE | [ACE/save] |
| 0xc61f80 | from the country reader 0x705ce0: re-attach loaded aces to their wings (0x61bc70) | A-ACE | [ACE/save] |
| 0xf5aab0 | wing → its ace (`wing+0x2e8` resolved; null if none; leaf + tail jump) | A-ACE | [ACE/objects] |
| 0xf5b320 | wing SetAce: detach the old ace, set `wing+0x2e8`, 0x61bc70(new, wing) | A-ACE | [ACE/objects] |
| 0xf5bc80 | wing night factor `clamp(1 + air_night_penalty (country, xp holder +0x238, ace) + stat 0x4c + strat-bomber night (region, type & 8) − carrier night reduction, 0, 2)` | A-ACE | [ACE/stats] |
| 0xf5c9d0 | wing weather factor `clamp(1 + air_weather_penalty (country, xp holder +0x238, ace), 0, 2)` | A-ACE | [ACE/stats] |
| 0xf5d3b0 | generic air combat: ace and holders' `air_attack/defence/agility_factor` (+ `air_ace_bonuses_factor` per ace entry); not used by naval combat | A-ACE | [ACE/stats] |
| 0xf5df48 (chunk of 0xf5ddd5) | accident: kill planes, ace dies if the roll ≤ ACE_DEATH_CHANCE_BASE (kill type 2); from strategic air 0xc60334 | A-ACE | [ACE/death] |
| 0xf5dfa0 | wing XP gain (FIELD_EXPERIENCE_FACTOR, AIR_WING_XP_MAX, air_mission_xp_factor); callers 0xf81a20, 0xf82110, 0xf83ed0 only (not naval combat) | A-ACE | [ACE/stats] |
| 0xf60760 | interception speed `stat(+0x580) × (1 + ace air_maximum_speed_factor + [ace has it] air_ace_bonuses_factor)` | A-ACE, A-INTERCEPT | [ACE/stats] |
| 0xf60bc0 / 0xf60f10 / 0xf61280 | naval agility / attack / targeting per plane: `base × (1 + ace(0x18/0x16/0x17) + country + [region: mission efficiency (not targeting) + region weather × weatherF + night × nightF])` | A-ACE, A-HIT, A-DMG, A-AA-PREEMPT | [ACE/stats] |
| 0xf65f20 | rebuild the wing experience holder `+0x238` from experience (static struct +0xc8 / +0xd0, `air_untrained_pilots_penalty_factor`) | A-ACE | [ACE/stats] |
| 0xf661e0 | wing stat cache `+0x390` from the equipment (no ace) | A-ACE | [ACE/stats] |
| 0xf7ed40 | mission efficiency by mission `type`: &4 intercept (0xd), &1 superiority (0xb), &2 CAS (0xf), &0x110 naval (0x10) | A-ACE | [ACE/stats] |
| 0xf80f60 | after a naval strike, per strike wing `(mission, losses, damage)`: live ace → death roll 0xb52 [0xb54]; else earn roll 0xb68 → 0x618860 → int roll 0xb72 + `on_ace_promoted` | A-ACE, A-LOSSES | [ACE/rolls] |
| 0xf87550 | apply pending air-to-air losses: ace death roll (lines 0xa50 / 0xa53); clears mission `+0x98 / +0x9c / +0xa4`; strategic air only | A-ACE | [ACE/death] |
| 0x197c830..0x197c882 | strike tail: 0xf80f60 for every strike wing of the AirFX with the AirFX's losses and integer HP damage | A-ACE | [ACE/rolls] |

### Region air war (A-REGION)

From [A-REGION-carrier-wings-in-the-region-air-war.md][REG]: how carrier wings of a task force in a naval combat fight in the region air war (air hours only, before the naval hour), the loss and disruption formulas, and how disruption becomes the naval strike efficiency. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x1962420 | air-war context: `flag[r] = region+0x24 > 0 and 0xe24530(region)` (air hour) and the flagged region id list (`+0x30`); 0x1961fe0 / 0x1962300 allocate it and copy the active countries | A-CADENCE, A-REGION | [REG/order] |
| 0x15c4500 | naval combat: add region-war plane losses of a carrier wing to its side (attacker side `+0x98`, then `+0xa0`) → 0x161fe00 | A-LOSSES | [REG/losses] |
| 0x161fe00 | side: find the AirFX holding a wing (externals `+0x100`, then the groups' AirFX via 0x1c6c070) → 0x1976b00 | A-LOSSES | [REG/losses] |
| 0x1976b00 | AirFX `+0x60 += losses` (loss tally; leaf) | A-LOSSES | [REG/losses] |
| 0xc43580 / 0xc43b80 | sort the air-war attackers by `S18 × S28` (0xf5e890 without province) descending (insertion ≤ 32 / merge) | A-REGION | [REG/war] |
| 0xc46030 | hourly per-country job (vtable 0x2959f80, built in 0xc57d30 at 0xc57f93): region records reset, 0xc64ee0, 0xc66480, 0xf62a50 per pool. **disagrees**: MSAVE lists it under the daily air manager | A-REGION, A-WINGS | [REG/order] |
| 0xc46ac0 | job 0x2959fa0 body: escort value 0xf77360 for the missions on `+0x98[r]` / `+0x140[r]` of flagged regions | A-REGION | [REG/disrupt] |
| 0xc46ea0 / 0xc4a2f0 | job 0x2959f00 (from 0xc58660): mission `+0xa8 = +0xb0 = 0` for `+0x98[r]` / `+0x140[r]`; interception re-chosen (0xf796e0 / 0xf7ac80); region superiority records `+0x78 / +0x80 / +0x98` | A-REGION | [REG/order] |
| 0xc47130 / 0xc4c570 | job 0x2959fc0: per flagged region `f = 1 − min(1, Σ+0xb8 / Σ+0xb0)` over `+0x110` (self and allies?), then land-list strike missions `+0xa8 = f × +0xb0` ("TotalDisruptionTaken", "TotalDisruptionReduction") | A-REGION, A-DMG | [REG/disrupt] |
| 0xc57d30 | hourly strategic-air update: full step order (jobs, accidents 0xc5efc0, air war 0xc5f4c0 per country, escorts, disruption, 0xf87550, join reset, 0xf82110) | A-REGION | [REG/order] |
| 0xc591c0 | `hostile(selfTag, otherTag)`: 0x700570 (at war), else a fallback list (0x6f7a80; not decoded) | A-REGION | [REG/war] |
| 0xc5cb60 | air loss statistics `(airMgr, wing, n, killer tag, entry, flag, kind)` → 0xc5c8c0; removes no planes | A-REGION | [REG/combat] |
| 0xc5efc0 | every hour: random_int(strategicair.cpp, 0x1be8) seeds a local hash generator; per base entry 0xc60290 accident | A-REGION | [REG/accident] |
| 0xc5f4c0 | region air war with one country as attacker: per flagged region, defenders (hostile missions with the region on `+0x58`, hostile `+0x140[r]`), attackers (own `+0x98[r]` executing ≠ 0, own `+0x140[r]` AS / interception), random_int 0x12c9 province pick, 0xf81a20 | A-REGION | [REG/war] |
| 0xc60290 | accident per base: 90 % of hours none (`u ≥ 10000`); else first wing with `0xf61d70 × ACCIDENT_CHANCE_BALANCE_MULT > 0.1 × u2` → 0xf5dd80 | A-REGION | [REG/accident] |
| 0xc647b0 | carrier region sync `(base, oldRegion, newRegion)`: wings whose mission region == old get SetRegion(new) (from the task force code 0xd70032) | A-REGION | [REG/lists] |
| 0xc64ee0 | per country: `+0x110` = self + list A, `+0x128` = list B of 0xc53270 (allies / enemies, medium) | A-REGION | [REG/order] |
| 0xc66480 | rebuild scrambled carrier lists `+0x140[r]`: wings on carriers whose task force is in combat, `r` = the fleet's region (flagged), unless the mission has a region and `type ≠ 0` | A-REGION, A-WINGS | [REG/lists] |
| 0xf5e890 | generic air-combat wing stats `(wing, out, prov, missionBits)`: +0 attack, +8 defence, +0x10 agility (× (M + 1 + Σ air_*_factor + region weather / night + home defence / power projection)), +0x18 / +0x20 / +0x28 the same ÷ AIR_WING_MAX_STATS_* | A-REGION, A-WINGS-EXT | [REG/combat] |
| 0xf60860 | speed ÷ AIR_WING_MAX_STATS_SPEED (0xf60760) | A-REGION | [REG/combat] |
| 0xf63580 | RemovePlanes(wing, n, killer tag): from the wing's equipment entries in order (also SAM use, accidents) | A-REGION | [REG/losses] |
| 0xf75690 | targeted modifier slot 0xd of the attacker's country against a tag (cached; 0 by default): disruption × (1 + it) | A-REGION | [REG/combat] |
| 0xf75820 | defender plane factor `k`: `cv ? clamp(1 + T1, 0, 1) : clamp(det + T1, 0, 1)` (targeted modifier slot 1) | A-REGION | [REG/war] |
| 0xf77360 | escort value `+0xb8` of an AS mission: `planes × (1+spd·ESF)(1+S28·EAgF)(1+S18·EAtF)(1+det·DDF) × ESCORT_FACTOR × (1 + air_escort_efficiency)`, else 0 | A-REGION | [REG/disrupt] |
| 0xf775d0 | `effective_air_superiority (+0xe8) = effective_planes_count × stat 0x33` unless executing is only 0x904 bits | A-REGION | [REG/order] |
| 0xf78160 | naval strike `e_i = clamp(1 − sqrt(+0xa8 / den) × 0.316228, 0.001, 1)`, `den = planes(1+S20·DDD)(1+S18·DDA)(1+spd·DDS)` (stats of 0xf5e890 with the battle province); 1 when `+0xa8 == 0` | A-DMG, A-REGION | [REG/disrupt] |
| 0xf78360 | effective planes `round(planes × eff)`, carrier `× CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO[tf+0x4ec]` (also the per-hour loss budget of 0xf862a0) | A-REGION | [REG/lists] |
| 0xf79c10 | carrier CanExecute: available missions, not busy, planes, no training, `period` vs night, AGGRESSION_THRESHOLD; `+0xd8 = 0xf78500(isCarrier = 1)`, `+0xe0 = 0xf78360` | A-REGION, A-WINGS | [REG/lists] |
| 0xf7ad60 | speed damage bonus `(out, vDef, vAtk, dmg)`: `vAtk > vDef > 0 ? (min(ratio, BIGGEST_SPEED_FACTOR_DIFF) − 1) × dmg × COMBAT_BETTER_SPEED_DAMAGE_INCREASE + trunc(vAtk/100) × TOP_SPEED_DAMAGE_BONUS_FACTOR × dmg : 0` | A-REGION, A-INTERCEPT | [REG/combat] |
| 0xf7be70 | one attacker vs the defenders: engaged `min(Σleft × MULTIPLANE_CAP, n)`, per defender 0xf862a0 with shares, disruption `+0xb0`, ace earn rolls 0x82b / 0x833 / 0x834 / int 0x83f | A-REGION | [REG/combat] |
| 0xf7ee40 | mission region: `+0x30`, else the carrier location's region | A-REGION | [REG/lists] |
| 0xf80540 | defender weights `n`, `left`: `max(1, round(trunc(n × k) × (1 + stat37) × w))` (**corrected** by A-DETECT: `n × k` in fixed point; stat37 = air_visibility_factor), w CAS 2 / port 100 / strategic 1 + mod 4 / else 1; AS defenders `n` when a strike mission is present | A-REGION | [REG/war] |
| 0xf81a20 | region air combat `(region, prov, tag, attackers, defenders)`: planes = `effective_planes_count`, detection, 0xf80540, attacker XP, 0xf7be70 per attacker | A-REGION | [REG/war] |
| 0xf81ce0 | per defending country sums → war support / statistics (0xd026f0); no combat effect | A-REGION | [REG/war] |
| 0xf862a0 | defender losses: `dmg = pot × COMBAT_DAMAGE_STATS_MULTILPIER + speed bonus − agility reduction`; `x = max(0.001, scale × dmg × 0.01 / defence)`, scale `COMBAT_DAMAGE_SCALE_CARRIER` if both carrier, random rounding U 0xacf below the cap; `+0x98 += losses`; killer rolls 0xade / 0xae5 | A-REGION | [REG/combat] |
| 0xf87550 | apply the hour's air-war losses: ace death 0xa50 / 0xa53, 0xf63580, carrier in combat → 0x15c4500 (naval combat tally) | A-REGION, A-LOSSES | [REG/losses] |

### Region air detection (A-DETECT)

From [A-DETECT-region-air-detection.md][DET]: a country's air detection `det` in a strategic region (the region air war's defender weights, disruption and escorts), its radar, territory, aircraft, night and weather terms, and the debug lock. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x10a3f20 | radar record coverage: level (+0x58), range (+0x50) from NBuildings.RADAR_RANGE_*, covered provinces, per-region counts (+0x18 / +0x30) | A-DETECT | [DET/rad] |
| 0x10a83f0 | country radar rebuild: one 0x70-byte radar record per state with radar buildings → country+0x1120 | A-DETECT | [DET/rad] |
| 0x268ad0 | console command `lock_air_det` handler (registered in 0x36f0), fills 0x332f5f0 | A-DETECT | [DET/lock] |
| 0x702780 | friendly test country × country (relation set +0x290, subject relation 0x30d1, relation +0x210); also "not enemy" in 0xf860e0 | A-DETECT | [DET/rad] |
| 0x9d4190 | share of a state's provinces controlled by a tag (or the same original tag) | A-DETECT | [DET/rad] |
| 0xc546e0 | strategicAir `+0x98[region id]` (region mission list) | A-DETECT | [DET/tot] |
| 0xe21640 | mean province darkness (0xf19660) over a region's provinces | A-DETECT | [DET/night] |
| 0xe21800 | share of the region's land provinces (terrain +0xd2 bit 0) controlled by tag / same original tag; 0 without land | A-DETECT | [DET/base] |
| 0xf5e520 | wing base region (air base province's region, or the carrier task force's region), optional int | A-DETECT | [DET/tot] |
| 0xf75690 | (update) targeted modifier 0xd = `air_intercept_efficiency` against the target | A-DETECT, A-REGION | [DET/use] |
| 0xf75820 | (update) cached `clamp(base + targeted modifier 0x1 air_detection vs target, 0, 1)`; bases 1 (carrier) / `det` | A-DETECT, A-REGION | [DET/use] |
| 0xf77730 | region air detection `det(country, region)` = `clamp((AIR + RAD + BASE) × (1 + NIGHT + WEA), 0.01, 1)`; debug lock table 0x332f5f0; tooltip AIR_DETECTION_IN_REGION_DETAILS | A-DETECT | [DET/tot] |
| 0xf7a460 | radar coverage of a region for a country and its friends (0x702780): Σ 0xf861d0 / region provinces, clamp 0..1 (1 if none) | A-DETECT | [DET/rad] |
| 0xf7be70 | (update) the disruption share uses the defender's `left` read before 0xf862a0's decrement (0xf7c282 → 0xf7c5a2) | A-DETECT, A-REGION | [DET/use] |
| 0xf7d5e4 | naval strike / naval recon detection, calls 0xf7a460 / 0xf77730 (not decoded) | — | [DET/use] |
| 0xf80540 | (update) weight `max(1, round(n × k × (1 + stat37) × w))` with `n × k` in fixed point (not an integer truncation) | A-DETECT, A-REGION | [DET/use] |
| 0x1012f80 | in-range province count of a region from the base position (bounding-box precheck, distance² per province) | A-EXTERNAL | [DET/mix] |
| 0x1c6cc50 | ship escape (from 0x19739bd, carrier): moves its AirFX from group+0x58 to group+0x88 (**settled** by A-REGION-ESCAPE: the wings leave the naval strikes only and stay in the region air war) | A-REGION | [DET/mix] |
| 0xf87480 | (adds) coverage range = trunc(wing range (0xf5e5c0) / global 0x3335f50, likely MAP_SCALE_PIXEL_TO_KM 7.114) px | A-EXTERNAL | [DET/mix] |
| 0xf861d0 | one country's radar sum in a region: Σ radar records (country+0x1120) covered provinces × control share (+0x68) | A-DETECT | [DET/rad] |

| Field / id | Meaning | Finding |
|---|---|---|
| country +0x1120 / +0x112c | radar records (list, count) | [DET/rad] |
| radar record +0x00 / +0x18 / +0x30 / +0x48 / +0x50 / +0x58 / +0x60 / +0x68 | covered provinces / regions / per-region counts / state / range / level / owner / control share | [DET/rad] |
| data 0x332f5f0 / 0x332f5fc | `lock_air_det` table {country index, fixed det}, count | [DET/lock] |
| modifier 0x1 `air_detection` | region weather holder (0xf77eee); targeted T1 (0xf75926) | [DET/tot] |
| modifier 0x8 / 0x9 `air_interception_detect_factor` / `air_superiority_detect_factor` | 0xf7784b / 0xf77861 | [DET/tot] |
| modifier 0x74 / 0x75 `air_home_defence_factor` / `air_power_projection_factor` | 0xf77882 / 0xf77894 | [DET/tot] |
| modifier 0xd `air_intercept_efficiency` (targeted, T13) | 0xf75783 | [DET/use] |
| NAir.DETECT_CHANCE_FROM_OCCUPATION / _RADARS / _AIRCRAFTS_EFFECTIVE_COUNT / _AIRCRAFTS / _NIGHT, NO_SEARCH_MISSION_DETECT_FACTOR | read at 0xf777de / 0xf7780c / 0xf77cc1 / 0xf77d15 / 0xf77d81 / 0xf77869 | [DET/tot] |
| NBuildings.RADAR_RANGE_BASE / _MIN / _MAX | globals 0x3334040 / 0x33340c8 / 0x3334178, read 0x10a3fe5..0x10a3ff3 | [DET/rad] |

### Plane stats and air modifiers (A-STATS)

From [A-STATS-plane-stats-and-air-modifiers.md][AST]: how a wing's stats are built from its equipment and the owner's sub-unit bonuses (doctrines, spirits, technologies), how the combat getters read them, and which air country modifiers act in a naval battle. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x1012920 | Σ amounts of the wing's equipment list (`wing+0x1d8`) | A-STATS | [AST/cache] |
| 0x13e81b0 | stat token → `EEquipmentStats`: full enum incl. 0x2e air_range … 0x37 air_visibility_factor, 0x3e maximum_speed, 0x4c night_penalty, 0x2a naval_weather_penalty_factor | A-STATS, S-STATS | [AST/enum] |
| 0x142a580 (thunk 0x1019ef0) | add a variant's stats to a wing × weight × (1 + sub-unit stat), all 78 stats (wing twin of 0x142b5f0) | A-STATS | [AST/cache] |
| 0x142aa30 (0x1019f00) | one stat `x == 0 ? 0 : trunc(trunc((1 + sub[s]) × w) × x)` | A-STATS | [AST/cache] |
| 0x142aaa0 (0x1019f30) | per-mission array: add sub-unit mines, reliability (+ factor), fuel (+ factor) | A-STATS | [AST/cache] |
| 0x1976db0 | (update) the country's `carrier_traffic` is not read; only the weather (`combat+0xb0`) and night holders | A-SORTIE | [AST/mods] |
| 0x197d4a0 | AirFX averages `+0xc8 / +0xd0 / +0xd8 / +0xe0 / +0xe8` = plane-weighted stats 0x31 / 0x34 / 0x35 / 0x3e / 0x4c (night_penalty) | A-HIT, A-STATS | [AST/getters] |
| 0x6f2650 | country per-strategic-region modifier (`country+0x4d8` map → holder +0x50) `.Get(id)` | A-STATS | [AST/effectiveness] |
| 0xbb8380 | port-strike combat (from 0xf82ca0): external efficiency × (1 + port_strike), ≥ 0.001 | A-EXTERNAL | [AST/mods] |
| 0xc4daf0 | accident chance terms: BASE + air_accidents, (1 − rel) × RELIABILITY_MULT, region weather, CARRIER_MULT, training, air_accidents_factor (combination not decoded) | A-REGION | [AST/accident] |
| 0xc62ba0 | country: 0xf661a0 for every pool (from 0x6eab50) | A-STATS | [AST/cache] |
| 0xd23780 | targeted-modifier entry → holder `+0x130` | A-REGION | [AST/mods] |
| 0xf5dfa0 | (update) XP gain `trunc(trunc(trunc(g × FIELD_EXPERIENCE_FACTOR) × fuelRatio) × (1 + air_mission_xp_gain_factor + ace))`, clamp [0, AIR_WING_XP_MAX] | A-STATS | [AST/mods] |
| 0xf5e400 | per-mission stat array of one mission bit (null if none) | A-STATS | [AST/cache] |
| 0xf5e890 | (update) home defence if the province's region equals the base region (land: base province; carrier: fleet region 0xd6d160), else power projection, to attack and defence; M by the executing mission | A-STATS, A-REGION | [AST/getters] |
| 0xf60760 | (update) the country's `air_maximum_speed_factor` is never read; speed from `wing+0x580` (base array) × (1 + ace) | A-STATS, A-INTERCEPT | [AST/getters] |
| 0xf60a40 | stat array for a mission bit, else `wing+0x390` | A-STATS | [AST/cache] |
| 0xf61d70 | accident chance: 0 if the mission `type` is 0 or no planes; else 0xc4daf0 | A-REGION | [AST/accident] |
| 0xf63a2d (chunk of 0xf63580) | XP loss on killed planes × (1 + air_wing_xp_loss_when_killed_factor), AIR_WING_XP_LOSS_WHEN_KILLED | A-STATS | [AST/mods] |
| 0xf65f20 | (update) experience holder: `v` from AIR_WING_XP_LEVELS (−1..1, 0.01 steps); malus × (−v) × (1 + max(air_untrained_pilots_penalty_factor, −1)) if `wing+0x9c4` is set; cached `wing+0xa10` | A-STATS | [AST/xp] |
| 0xf661a0 | pool `+0x20` = the owner's sub-unit copy (`[country+0xf70]`, 0xd11ae0) | A-STATS | [AST/cache] |
| 0xf661e0 | wing stat cache `(wing, opt mask)`: per variant `stat[s] += trunc(trunc((1 + sub[s]) × w) × variant[s])` into `wing+0x390`; per-mission arrays `wing+0x968` (`mission_type_stats` deltas); then the sub-unit additive list and clamps (0x1019ea0); mask = argument or mission `type` | A-STATS | [AST/cache] |
| 0xf78500 | effectiveness: `(1 + dist)(1 + crowd)(1 + supply) × (1 − rcp) × (1 + air_mission_efficiency [region weather + country + carrier fleet admiral]) × fuel × (1 + per-region country value)`, clamp [0, 1] | A-STATS, A-EXTERNAL | [AST/effectiveness] |
| 0xf7ed40 | (update) the naval strike getters pass the mission `type` (carrier wings 0 → M = 0); 0xf5e890 passes the executing mission if not 0, else `type` | A-STATS, A-DMG | [AST/mission] |
| 0xfaa680 | naval-mission fleet spotting: spotting_chance / night_spotting_chance (navalmission.cpp), before battles | — | [AST/mods] |

| Field | Meaning | Finding |
|---|---|---|
| wing +0x390 / +0x2f0 / +0x968 | stat cache array / its stat block / per-mission arrays (0x270 bytes each) | [AST/cache] |
| wing +0x1d8 / +0x1f8 | equipment amounts (stride 0x18) / `{variant, amount}` list (stride 0x10) | [AST/cache] |
| wing +0x9c4 / +0xa10 / +0xa18 | owner tag (save `tag`, set at creation) / cached untrained factor (save `air_untrained_pilots_penalty_factor`) / its flag | [AST/xp] |
| wing +0x548 | stat 0x37 `air_visibility_factor` (A-REGION's `stat37`) | [AST/enum] |
| pool +0x20 | the owner's sub-unit copy (country +0xf70) | [AST/cache] |
| airfx +0xe8 | plane-weighted `night_penalty` (stat 0x4c) of the strike wings (A-HIT night term) | [AST/getters] |

### Carrier wings' region and mission effectiveness (A-REGION-SYNC)

From [A-REGION-SYNC-carrier-wing-region.md][SYNC]: what gives a task force's carrier wings a mission region (the naval mission tick out of combat, region and province changes), and the full mission effectiveness 0xf78500. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x1941470 / 0x19413d0 | air command A ctor (wing lists / one wing), vtable 0x2a1d570, execute 0x1946880, check 0x19488c0 → 0x1941e60 | A-REGION-SYNC | [SYNC/callers] |
| 0x1941b10 | air command per wing: SetRegion(cmd region), then EnableMission per bit from the template wing's type | A-REGION-SYNC | [SYNC/callers] |
| 0x1a0afb0 | CSupplyConsumer: `+0x30 = _ReceivedSupply(+0x40) / _AskedSupply(+0x28)`, 1 if asked = 0 | A-REGION-SYNC | [SYNC/eff] |
| 0xc00000 | InCombat(unit): `unit+0x1b4 > 0` | A-REGION-SYNC | [SYNC/tick] |
| 0xc02781 (in 0xc01dc0) | unit movement, naval unit entering a province: 0xd6fff0(tf, P) before SetLocation (vt+0xe8) | A-REGION-SYNC | [SYNC/move] |
| 0xc482a0 | CSupplyConsumer base ctor: +0x20 / +0x28 / +0x30 = 1.0, +0x38 = −1, +0x40 = 1.0, +0x1c type | A-REGION-SYNC | [SYNC/eff] |
| 0xc543e0 | base entry fuel ratio `clamp(received +0xf0 / fuel_consumption +0xe0, 0, 1)`, 1 if consumption ≤ 0 | A-REGION-SYNC | [SYNC/eff] |
| 0xc54ed0 | base entry supply ratio (entry +0x30) for a tag | A-REGION-SYNC | [SYNC/eff] |
| 0xc5c000 / 0xf63f50 | per base / per pool: 0xf643a0 for every wing (from task force SetLocation 0xd77c70) | A-REGION-SYNC | [SYNC/move] |
| 0xc60df0 | base entry (CStrategicAir base access) reader / ctor: `countries` block, +0xd0 operational_status = 1, +0xd8..+0xf0 = 0 | A-REGION-SYNC | [SYNC/eff] |
| 0xc647b0 | (update) region sync `(base, old, new)`: wings whose mission region == old → SetRegion(new, recompute); from 0xd6fff0 on a region change only, never gives a region to a wing without one | A-REGION-SYNC | [SYNC/move] |
| 0xc668b0 | base entry writer: country, operational_status +0xd0, capacity_penalty +0xd8, fuel_consumption +0xe0, received +0xf0, base_fuel_consumption +0xe8 | A-REGION-SYNC | [SYNC/eff] |
| 0xd6d160 | task force's sea region (location region; in port the port's sea zone) | A-REGION-SYNC | [SYNC/tick] |
| 0xd6fff0 | task force province change: old / new region differ → 0xc647b0 for each carrier with an air base (+0x80 set) | A-REGION-SYNC | [SYNC/move] |
| 0xd705f0 | task force hourly update (vtable 0x2963000): … 0xd79900 | A-REGION-SYNC | [SYNC/tick] |
| 0xd72000 | make a task force the child of another: clears carrier wing regions (0xc4eaa0 → 0xf5d1d0) | A-REGION-SYNC | [SYNC/callers] |
| 0xd77c70 | task force SetLocation (vt+0xe8): carrier pools → 0xf643a0 | A-REGION-SYNC | [SYNC/move] |
| 0xd79900 | task force mission tick: return if in combat; state +0x4b8 == 0 → 0xfbcb90(tf+0x360) | A-REGION-SYNC | [SYNC/tick] |
| 0xed0400 | CSupplyConsumer writer: `disrupted_supply` (+0xa0), `motorization_level` (+0x1d) | A-REGION-SYNC | [SYNC/eff] |
| 0xf63e00 | (wing, oldPlanes, xpNew, p, flag) after `wing+0x7c` = n is set: XP `+0x218 = trunc((old × xp + (n − old) × xpNew) / n)`; if n > old: region_change_penalty `+0x150 = trunc((old × pen + (n − old) × min(p, EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR 0x33382d0)) / n)` (raw fx5, p < 0 not capped); then 0xf87290(mission, flag) | A-EXTERNAL, A-COVERAGE | [SYNC/eff], [COV/refill] |
| 0xf59760, 0xf5ac50, 0xf5add0, 0xf66890, 0xf66dd0, 0xf683b8, 0xf63aa3 | plane-adding callers of 0xf63e00 (wing creation, AddAirplanes, stockpile and production refill, returned planes): pass p = EFFICIENCY_REGION_CHANGE_PENALTY_FACTOR; 0xf63af0 (from 0xf674e0, planes moved between wings) passes the source wing's `+0x150` | A-COVERAGE | [COV/refill] |
| 0xf643a0 | wing refresh: UpdateCoverage(force); if no province of its region is in range: carrier → command to the fleet's region, land → SetRegion(null); saves / restores type in +0x1b8 | A-REGION-SYNC | [SYNC/move] |
| 0xf78500 | (update) mission effectiveness, full: `cov (non-carrier) × capacity × supply × (1 − penalty: non-carrier, type & 0x60 = 0) × (1 + air_mission_efficiency: region weather (wings with a region) + country + fleet admiral) × fuel × (1 + targeted)`, clamp [0, 1]; isCarrier = base+0x68 == 0 | A-EXTERNAL, A-REGION-SYNC | [SYNC/eff] |
| 0xf7af10 | wing can be activated: base gives the country access, no training, and (0xf61600 > 0 or recon / paradrop or at war) | A-REGION-SYNC | [SYNC/tick] |
| 0xf7ec90 | region change daily gain by sub-unit flags +0x5a8 (CAS, naval bomber, tactical, fighter, strategic, maritime patrol, default) | A-EXTERNAL | [SYNC/eff] |
| 0xf80490 | region change penalty decay, hourly: `−= gain / 24`, clamp [0, 1], only type ≠ 0 and not training (carrier wings keep theirs; not read for them) | A-EXTERNAL | [SYNC/eff] |
| 0xf80920 | mission has an active region mission (`+0x30 && active`) | A-REGION-SYNC | [SYNC/tick] |
| 0xfa9e00 | assign the task force's carrier wings to region `rid` (0 = clear) via command 0x1941470 / 0x1946880 | A-REGION-SYNC | [SYNC/tick] |
| 0xfb6710 | naval mission type → map icon (0 hold, 1 patrol, 2 strike force, 3 convoy raiding, 4 convoy escort, 5 mines planting, 6 mines sweeping, 7 training, 8 reserve fleet, 9 invasion support) | A-REGION-SYNC | [SYNC/tick] |
| 0xfbcb90 | naval mission update: 0xfbe7f0 first, then (out of combat, in region) engagement per type and `hours_in_mission` +1 | A-REGION-SYNC | [SYNC/tick] |
| 0xfbe7f0 | `is_in_regions` (+0x30) by type, and the carrier region rid by type → 0xfa9e00 (hold, strike force, convoy, mines, training: the location's region; patrol: if it is the spotting region; reserve, invasion support: none) | A-REGION-SYNC | [SYNC/tick] |
| 0xfbea80 | naval mission writer: mission +0x14, is_in_regions +0x30, navy_engagement_rule +0x34, hours_in_mission +0x3c, spotting_region +0x70, hours_in_spotting_region +0x6c, accessible_regions_range +0xa8 | A-REGION-SYNC | [SYNC/tick] |

| Field / id | Meaning | Finding |
|---|---|---|
| CStrategicAir base entry (`base+0x30[country]`) +0x30 / +0xd0 / +0xd8 / +0xe0 / +0xe8 / +0xf0 | supply ratio / operational_status / capacity_penalty / fuel_consumption / base_fuel_consumption / received | [SYNC/eff] |
| modifier 0x11 `air_mission_efficiency` | 0xf78500 (country, fleet admiral, region weather) | [SYNC/eff] |

### Escaped carriers (A-REGION-ESCAPE)

From [A-REGION-ESCAPE-escaped-carrier-wings.md][ESC]: an escaped carrier stays in its task force, the task force stays in the combat, and its wings stay in the region air war. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x12a2c0 | empty function (ret), called after the AirFX move on escape | A-REGION | [ESC/state] |
| 0x13e3570 | remove a unit from a combat: RemoveCombat, unit vt+0x168, both sides vt+0x80; callers 0xbb93c1 (join a combat elsewhere), 0xbfb4a0 (unit movement), 0xc04170, 0x12bab70 | A-REGION | [ESC/tf] |
| 0x1973a27 | second write of member state 0x10 (joining member, task force's naval mission 0xfb4390 ≤ 0) | A-REGION | [ESC/state] |
| 0x19739bd | escape: member state 0x10; carrier → 0x1c6cc50; no task force or air call | A-REGION | [ESC/state] |
| 0x1c6a2ba | per-group member count that skips escaped (0x10) members | A-REGION | [ESC/state] |
| 0xc03e00 | RemoveCombat: the only remover of unit+0x1a8 / +0x1b4; callers 0x15c2980, 0x12ae860 (teardowns), 0x13e3570 / 0x13e35ff / 0x13e3990 | A-REGION | [ESC/tf] |
| 0xd72580 | move ships into a new task force (0xd68d90, 0xd730b0, 0xd71e70); callers 0xd76dd0, 0xd77ff0, 0xd776a3, 0xd77870, 0xd77900, 0xd78ba0, 0xd79de0; not from the escape | A-REGION | [ESC/tf] |
| 0xd76dd0 | repair split (`REPAIR_SPLIT_TASKFORCE_SIZE`, "pDetachedForce"), only from the mission tick 0xd79900 (out of combat) | A-REGION | [ESC/tf] |
| 0xf78360 | (update) effective planes read no member state | A-REGION | [ESC/exec] |
| 0xf79c10 | (update) carrier CanExecute reads no member state or AirFX list; asserts carrier base and task force in combat (0x69c–0x69f) | A-REGION | [ESC/exec] |
| 0xf7acc0 | ChooseCarrier: wing+0x30 available missions plus the mission type (bit 7) | A-REGION | [ESC/exec] |

| Field | Meaning | Finding |
|---|---|---|
| naval combat group +0x88 / +0x90 / +0x94 | escaped carriers' AirFX: array / capacity / count | [ESC/state] |

### Land mission effectiveness (A-COVERAGE)

From [A-COVERAGE-land-mission-effectiveness.md][COV]: a land wing's coverage of the region from its base's state, the base's crowding, supply and fuel. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x1012f80 / 0x10132d0 | in-range count: origin = state centre (land) or task force location (carrier); precheck d²(region centre) ≤ R² + hd²; per province d² ≤ R² | A-COVERAGE | [COV/count] |
| 0x118f660 | dx wrap into [−W/2, W/2] | A-COVERAGE | [COV/count] |
| 0x121a7ae | country_supply: proportional give-out to consumers → 0x1a0afb0 | A-COVERAGE | [COV/supply] |
| 0x140a3c0 | province position +0xb8 / +0xbc = truncated mean pixel (y from bottom), +0xc0 pixel count (also S-NIGHT's position) | A-COVERAGE, S-NIGHT | [COV/count] |
| 0x140a610 | province rect +0x88..+0x94 snapped to even pixels | A-COVERAGE | [COV/count] |
| 0x15a43c0 | region template centre +0xe0 / +0xe4, boxes +0xf0 / +0xfc, +0x108 | A-COVERAGE | [COV/count] |
| 0x2959fe0 (slot 0xc670f0 → 0xc44530 → 0xc45d50 / 0xc67630) | hourly parallel crowd recompute launched from 0xc5efc0 | A-COVERAGE | [COV/crowd] |
| 0x3335f50 | `NGame.MAP_SCALE_PIXEL_TO_KM` (registered at 0x8407d0) | A-COVERAGE | [COV/range] |
| 0x9d37a0 / 0xe21330 / 0xe212d0 | d² state → province / region → state / region → location province, with x wrap | A-COVERAGE | [COV/count] |
| 0xa5f7b0 | provinces.bmp run scan (map.cpp; "TOO LARGE BOX" check) | A-COVERAGE | [COV/count] |
| 0xabce00 (0xabce20) | state template +0x148 / +0x14c centre (aligned box of the state's provinces, seam variant), +0x150 = max(w, h) | A-COVERAGE | [COV/count] |
| 0xc4d490 / 0xc53790 | base entry supply location province | A-COVERAGE | [COV/supply] |
| 0xc4d770 / 0xf625b0 | base entry asked supply = Σ trunc(planes × SUPPLY_NEED_FACTOR × 0.01) × BASE_AIR_SUPPLY_MULT_FOR_TRUCK_BUFFER (0x3337100) | A-COVERAGE | [COV/supply] |
| 0xc4d910 / 0xf5c4d0 | planes at the base: all countries, pools and wings | A-COVERAGE | [COV/crowd] |
| 0xc579f0 / 0xc57b70 | crowd factor per base (P vs capacity, r > 0.9 → 0, 1 − CAPACITY_PENALTY × r) → entry+0xd8 | A-COVERAGE | [COV/crowd] |
| 0xc65490 | base capacity +0x78: Σ air base level × AIRBASE_CAPACITY_MULT (0x3333e68); rocket × ROCKETSITE_CAPACITY_MULT (0x3333ef4) | A-COVERAGE | [COV/crowd] |
| 0xc667b0 | CAirBase writer: +0x68 state, +0x60 carrier, +0x78 capacity, +0x48 countries, +0x7c base, +0x84 level, +0x88 allow_equipment_type | A-COVERAGE | [COV/crowd] |
| 0xf5ba20 | pool fuel use = Σ effective planes × fuel × FUEL_COST_MULT × MISSION_FUEL_COSTS[mission] | A-COVERAGE | [COV/fuel] |
| 0xf5e5c0 | (correction) wing range in fixed map px = air_range ×1e5 / MAP_SCALE × (1 + air_range_factor + ace) | A-COVERAGE | [COV/range] |
| 0xf87480 | UpdateCoverage: recompute if forced or the integer R changed; skips training or no region; cov = min(1, n / provinces) | A-COVERAGE | [COV/update] |
| 0x10f3080 → 0xc4bb10 | country fuel distribution sets entry received fuel (+0xf0) | A-COVERAGE | [COV/fuel] |

| Field | Meaning | Finding |
|---|---|---|
| mission +0x78 | (correction) whole map px of the last coverage computation | [COV/update] |

### Land wing accidents (A-ACCIDENT)

From [A-ACCIDENT-land-wing-accidents.md][ACC]: the hourly accident pass, its local generator, the chance and the planes lost. Not simulated yet. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x715310 | country `accidents_score` (+0x14e8) accumulator: `+= trunc(x · ACCIDENT_EFFECT_MULT)`, returns the integer part | A-ACCIDENT | [ACC/effect] |
| 0xc4daf0 | `chance = trunc(trunc((BASE + air_accidents) · trunc((1 − min(rel, 1)) · REL_MULT)) · M)`, M additive (weather > 0, carrier, training, xp, country) | A-ACCIDENT | [ACC/chance] |
| 0xc5efc0 | hourly accident pass: three base lists, seed `random_int(strategicair.cpp, 0x1be8)`, per (base, country) entry 0xc60290 | A-ACCIDENT | [ACC/pass] |
| 0xc60290 | accident per entry: local hash `next()`, gate `u < 10000`, start pool, per eligible wing draw u2, `p_raw > u2 / 10` → 0xf5dd80(next() % 1e5), stop | A-ACCIDENT | [ACC/pass] |
| 0xf5dd80 | accident effect: n = 0x715310(planes · u3); RemovePlanes; wing+0x84 += n; ace death u3 ≤ ACE_DEATH_CHANCE_BASE | A-ACCIDENT | [ACC/effect] |
| 0xf61d70 | accident chance: 0 if type 0 / no planes / mission inactive (non-training); rel = stat 0x41; xp `air_accidents_factor`; owner | A-ACCIDENT | [ACC/chance] |
| 0xf63e00 | wing XP average after a plane-count change (formula in the A-EXTERNAL row) | A-ACCIDENT | [ACC/effect] |
| 0xf80920 | mission active with a region | A-ACCIDENT | [ACC/chance] |

### Mixed plane variants in a wing (A-MIXED)

From [A-MIXED-plane-variants-in-a-wing.md][MIX]: which variants refill a wing, how it stores them, which planes are lost first, and the plane-share mix of its stats. Sorted by RVA.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x100cce0 / 0x100e900 | equipment container add / position: archetype groups, entries sorted by 0xbd1f20 | A-MIXED | [MIX/order] |
| 0x100f430 | archetype groups of a container | A-MIXED | [MIX/stock] |
| 0x100ff30 | main variant (largest amount); 0xf60710 = the wing's | A-MIXED | [MIX/order] |
| 0xbd1f20 / 0xbd1fe0 | wing entry order: version, type index, creator, origin (ascending) | A-MIXED | [MIX/order] |
| 0xbd2420 / 0xbd2430 | stockpile order (archetype ↑, year ↓, upgrades ↓, own first, creator ↑, id ↓) | A-REINFORCE, A-MIXED | [MIX/stock] |
| 0xbd7dc0 | variant better than (swap test) | A-MIXED | [MIX/stock] |
| 0xbe3c80 | variant writer: +0x41c version, +0x41d max_version, +0x41e is_frame, +0x424 obsolete, +0x427 can_upgrade_type, +0x1c creator, +0x20 origin | A-MIXED | [MIX/order] |
| 0xc924b0 | equipment type ctor: +0x418 = database index | A-MIXED | [MIX/order] |
| 0xe6f910 | return a wing's planes of one archetype to stock, worst first | A-MIXED | [MIX/stock] |
| 0xf63580 | (update) RemovePlanes: entries in container order (oldest type first), manpower × MANPOWER_LOSS_RATIO_PLANE_SHOT, XP via 0xf63e00, no cache refresh | A-MIXED, A-ACCIDENT | [MIX/remove] |

## 2. Struct fields

### Ship (ship.cpp object)

| Offset | Meaning | Finding |
|---|---|---|
| +0x8 / +0xc | handle `{type 51, id}`: the save's ship `id={ id type=51 }` | [LOG] |
| +0x40 | equipment list (entries `{equipment*, amount}` at +0x20, count +0x2c), tested by 0x10109b0 | [POTF/assign] |
| +0x88 | sub-unit index (`ship+0x80` sub-unit object +8; < 0 = none): key of the naval terrain records | [STATS/getter] |
| +0x1b8 | base surface detection | [POS/detect] |
| +0x1c8 | surface visibility | [HIT/ship] |
| +0x1d0 | sub visibility | [HIT/ship] |
| +0x1e8 / +0x1f8 / +0x208 / +0x210 / +0x318 | stats lg_attack / hg_attack / torpedo_attack / sub_attack / armor_value (0x19 / 0x1b / 0x1d / 0x1e / 0x3f), read by 0xc34a50 | [STATS/getter] |
| +0x218 | anti_air_attack stat (0x1f), read by 0xc3a470 | [STATS/getter] |
| +0x228 | shooter stat "arg4" gating sub target weight (modifier 0x29; sub detection?) | [TGT/dc] |
| +0x248 / +0x250 / +0x258 | weapon hit chance factor: light / heavy / torpedo (design stat) | [HIT/mods] |
| +0x260 | ship torpedo damage reduction (inferred) | [HIT/pierce], [DMG/conv] |
| +0x268 | ship torpedo enemy crit chance (inferred) | [HIT/crit], [DMG/conv] |
| +0x270 | stat 0x2a (weather penalty factor?) in the weather term of 0xc38760 | [STC/sum] |
| +0x308 | max HP (used as `max(·, 0.001)`) | [DMG/conv] |
| +0x328 | reliability (inferred, medium) | [DMG/conv], [HIT/crit] |
| +0x550 | sub-unit critical parts | [DMG/part] |
| +0x568 | `critical_part_damage_chance_mult` (inferred) | [DMG/part], [HIT/crit] |
| +0x628 | sub-unit `type` set (sub-unit+0x5a8): 0 convoy, 6 capital_ship, 7 submarine, 8 screen_ship, 22 carrier, 37 missile_launcher, 39 support_ship | [TGT/bb], [STATS/getter] |
| +0x664 / +0x668 / +0x66c | modifier ids `experience_gain_<sub-unit>_training_factor` / `_mission_factor` / `_combat_factor` (sub-unit definition +0x5e4 / +0x5e8 / +0x5ec, 0x1018080); 0xc3b240 reads the one for its mode from the country | [XP/gain], [POTF/xp] |
| +0x678 | per-ship hit profile multiplier ? (presumably 1.0) | [HIT/ship] |
| +0x6f8 | HP (current strength) | [DMG/conv], [HOUR/order] |
| +0x700 | org | [DMG/conv] |
| +0x708 | experience | [XP/levels] |
| +0x710 | experience cap (NAVY_MAX_XP) | [XP/levels] |
| +0x728 | → task force / fleet | [TGT/sub], [AIR/conv] |
| +0x730 | compared to air entry owner (carrier identity ?) | [XP/gain] |
| +0x738 | linked object handle ? (redirects damage, no parts) | [DMG/apply] |
| +0x740 | experience modifier holder (container +0x750), added in 0xc38760 | [STC/sum] |
| +0x750 | experience modifier list | [XP/effect] |
| +0x800 | experience-modifier dirty flag | [XP/effect] |
| +0x804 | `government_in_exile_tag` (save key; eligibility test 0xc33a50) | [POTF/assign] |
| +0x808 / +0x80c | manpower / max manpower (int32) | [DMG/conv], [HIT/hit] |
| +0x814 | pending `pride_of_the_fleet = yes` from a history/OOB ship block (consumed by 0xc34280) | [POTF/assign] |
| +0x848 | the ship's captain (officer) record, gets the captain XP in 0xc31840 (0x13e8e50) | [POTF/xp] |
| +0x8d8 | CUnitHistory (ctor 0x1445e00, dtor 0x1446450); `+0x20` (= ship+0x8f8) owns the medal object | [POTF/xp] |
| +0x8f8 | captain / leader (XP). Refined: the CUnitHistory's medal object (0x14463a0 type, deref 0xc0dc90), which the UI shows as the captain; not a unit leader | [XP/gain], [POTF/xp] |
| +0x8f8 → +0x1a0 | modifier holder of the object at +0x8f8, added in 0xc38760 (the medal object's modifiers, presumably the medals' `ship_modifiers`; also read for 0x34 / 0x35 by 0xc3b240) | [STC/sum], [POTF/xp] |
| +0x918 | damaged parts list (part, count) | [DMG/part] |

### Fire-exchange member

| Offset | Meaning | Finding |
|---|---|---|
| +0x8 | ship handle `{type, id}` (resolved with 0x221f310) | [LOG] |
| +0x10 | convoy reference (no ship) | [TGT/bb], [DMG/apply] |
| +0x18 | `CFEXMember::SCachedInfo` (vtable 0x2a20ee8; reader 0x1974d80, writer 0x1976430) | [POTF/readers] |
| +0x40 | cached `pride_of_the_fleet` flag (SCachedInfo+0x28), set at construction; no reader in the naval combat code | [POTF/readers] |
| +0xe0 | state: 1 waiting, 2 fighting, 4 joining, 8 retreating, 0x10 escaped (0x19739bd), 0x20 sunk (0x1975bea); mask 0xb = in battle, 0x34 = not present (settled by recheck) | [HOUR/hour], [ACT/bg], [TGT/bb], [POS/top] |
| +0xe4 | joining countdown (hours) | [HOUR/hour], [POS/top] |
| +0xe8 | → group | [POS/dmg] |
| +0xf0 + 8×slot | weapon charge per slot (0 heavy, 1 torpedo, 2 light/DC) | [ACT/cooldown], [DMG/pipe] |
| +0x12c | sub reveal timer: −1 non-sub, 0 hidden, > 0 revealed | [ACT/sub], [TGT/bb] |
| +0x130 | escape progress | [ACT/speed], [TGT/bb] |
| +0x138..+0x148 | last shot (optional): +0x140 target is convoy, +0x144 target group size, +0x148 present | [XP/gain] |

### Fire-exchange group

| Offset | Meaning | Finding |
|---|---|---|
| +0x8 / +0x14 | members / count | [HOUR/fex], [XP/gain] |
| +0x20 | enemy group (mutual) | [HOUR/fex] |
| +0x30 | aggressiveness-check ratio (0 for +0x10c/+0x10d combats) | [ACT/retreat] |
| +0x38 | side (combatant) | [HOUR/fex], [POS/dmg] |
| +0x40 | COMBAT_CHASE_RESIGNATION_HOURS copy (unread) | [HOUR/end] |
| +0x48 | sub detection D of the enemy | [ACT/sub] |
| +0x50 | air sub-detection term | [ACT/sub] |
| +0x58 / +0x64 | AirFX list / count | [HOUR/fex], [AIR/graph] |
| +0xa0 / +0xa4 / +0xa8 / +0xac / +0xb0 / +0xb4 / +0xb8 | visible enemy counts: capitals / screens / carriers / subs / invalid / convoys / bit-39 | [TGT/bb] |
| +0xbc | own subs in group | [ACT/retreat] |
| +0xc0 | enemy ships with depth-charge attack > 0 | [TGT/bb], [ACT/retreat] |

### Naval combatant (side)

| Offset | Meaning | Finding |
|---|---|---|
| +0x48 | country list size | [POS/pos] |
| +0xd8 | ctor flag: 1 for the side at combat+0x98 / CCombat+0x28; attacker? 0x13e1df0 picks the enemy by it (≠ 0 → combat+0x30) | [POS/top], [AIR/conv], [ACT/sub], [ADM/pos] |
| +0x118 / +0x124 | admirals of the side's task forces / count (0x161e5c0) | [ADM/reads] |
| +0xe8 / +0xf4 | groups / count | [HOUR/fex] |
| +0x100 / +0x108 | `screening_bonus` / `capital_screening_bonus` (TGT says combatant; HIT says a struct slot bound at 0x60ed2a) | [TGT/bonus], [HIT/mods] |
| +0x130 | commanding admiral (POS). ~~disagrees~~ settled: a CUnitLeader passed as the admiral to 0xc38760 / 0xc34a50 / 0xc3a470 (HIT "battle location" and AIR "battle context" were wrong) | [POS/top], [HIT/mods], [AIR/target], [STC/sum] |
| +0x138 | saved copy of the commanding admiral (written with +0x130 by 0x1620940; save writer 0x1622520, reader 0x16209e0) | [ADM/reads] |
| +0x140 | side-wide retreat flag | [ACT/retreat], [HOUR/end] |
| +0x148 | screening | [TGT/screen] |
| +0x150 | carrier screening | [TGT/screen] |
| +0x158 | positioning (ctor 1.0) | [POS/top] |
| +0x160 | newly joined penalty | [POS/joined] |
| +0x168 | convoy-escort dominance bonus | [POS/convoy] |
| +0x170 | fleet AA | [POS/aa], [AIR/reduce] |
| +0x3e8 | external planes joined (cumulative). **disagrees**: WINGS finds it reset to 0 for both sides at each air hour of the combat's region (0xc584e4), so the join cap is per air hour | [AIR/ext], [WINGS/ext] |

### Combat (CCombat / CNavalCombat)

| Offset | Meaning | Finding |
|---|---|---|
| +0x28 / +0x30 | attacker / defender combatant (CCombat) | [HOUR/chain] |
| +0x44 | hours: 1 during the first hour's Update, incremented in EndHour (observed) | [HOUR/chain], [ACT/bg], [POS/recompute], [LOG] |
| +0x98 / +0xa0 | the same two sides, typed | [HOUR/chain] |
| +0x38 | province of the battle (0x15c4ba0) → +0xc8 strategic region | [WTH/combat], [TER/terrain] |
| +0x48 | naval terrain of the battle's region (`ocean` without `naval_terrain`) | [TER/terrain] |
| +0xa8, +0xb0 | weather type (0 none; 2/3/4/5 from region-state flags +0x108/+0x110/+0x118/+0x120); +0xb0 = region weather state +0x48, the weather modifier holder (0 without a state) | [HOUR/hour], [AIR/conv], [WTH/combat] |
| +0xb8 | province darkness (> 0 = dark); always 0 with weather off (observed) | [HOUR/night], [AIR/conv], [LOG] |
| +0x10c / +0x10d | convoy-type / special combat flags (HOUR, ACT). **disagrees**: AIR calls +0x10c the port-strike flag | [HOUR/hour], [ACT/sub], [AIR/conv] |
| +0x118 | fleet distance | [HOUR/hour] |
| +0x120 | "air every hour" flag (ctor 0, no setter found) | [AIR/cadence] |

### Task force / fleet / unit

| Offset | Meaning | Finding |
|---|---|---|
| +0x8 / +0xc | handle `{type, id}` (task force type 61) | [LOG] |
| fleet +0xb8 / +0xc4 | task forces / count | [XP/mp], [LOG] |
| fleet +0xb0 | admiral handle (written by 0xd5a170; 0xd50ee0) | [ADM/eff] |
| +0x4b8 | ≠ 0: not counted by 0xd55ed0 for the admiral cap (meaning ?) | [ADM/eff] |
| +0x1a8 | combats list ([0] = first; count +0x1b4); a combat is removed only by 0xc03e00 | [XP/gain], [POTF/combat] |
| +0x1d8 / +0x1dc | owner tag / operating country tag (used if > 0 by 0xc3acc0 and the targeted lookup) | [STATS/getter] |
| +0x1e0 | country used to find the task force's combat side (0x15c3010) | [STATS/getter] |
| +0x1f0 | current province | [STATS/getter] |
| +0x1b4 | > 0 = in combat | [XP/gain] |
| +0x348 / +0x354 | ships / count | [XP/gain] |
| +0x340 | fleet (its admiral: 0xd64510) | [STC/sum] |
| +0x374 | mission: 3 convoy raiding; 4 convoy escort? | [ACT/retreat], [POS/convoy] |
| +0x391 | flag: aggressiveness-check value = 0 if set | [ACT/retreat] |
| +0x394 | aggressiveness (engagement rule) 0..4, save key `navy_engagement_rule` | [ACT/retreat], [TGT/sub] |
| +0x4ec | carrier stance index | [AIR/conv] |
| +0x4f8 / +0x500 | fuel / max fuel (0xd6c6f0) | [STATS/getter] |
| +0x658 | modifier holder added in 0xc38760 (content ?) | [STC/sum] |
| +0x728 / +0x734 | entries behind the building-module term (entry+0x40 → object with a character handle at +8) | [STC/sum] |

### Unit leader (CUnitLeader / CNavyLeader)

| Offset | Meaning | Finding |
|---|---|---|
| +0x120 | owner country tag | [STC/leader] |
| +0x128 | sub-unit adjusters from traits (0xc19ba0); a CSubUnitModifiers object (vtable 0x2955af0) holding both parts of the traits' `sub_unit_modifiers` | [STC/leader], [ADM/subunit] |
| +0x130 / +0x13c | `units` adjuster records (0x38 bytes: +0x08 sub-unit key, +0x10 adjuster attack/defence/movement at +0x10/+0x18/+0x20) / count | [ADM/subunit] |
| +0x148 / +0x154 | sub-unit modifier-block records (0xd0 bytes: +0x08 key, +0x10 holder, container +0x20; scaled by `eff`) / count | [ADM/subunit] |
| +0x160 / +0x188 | default adjuster / default holder returned on a key miss | [ADM/subunit] |
| +0x248 / +0x254 | `_TerrainAdjusters` (40-byte records: +0x10 attack, +0x18 defence, +0x20 movement) | [STATS/getter] |
| +0x260 (+0x270 / +0x278 / +0x280) | traits' `night` adjuster: attack / defence / movement | [STATS/getter] |
| +0x288 / +0x298 | holder / container B (traits' `modifier` + `non_shared_modifier` + entries' +0x3b8); read for 0x1b9, 0x168, 0x181; never scaled | [STC/leader], [ADM/eff] |
| +0x348 / +0x358 | holder / container A before the copy (scaled in place by `eff`) | [STC/leader], [ADM/eff] |
| +0x408 / +0x418 | combat holder / container: A × leader efficiency (traits, skill levels, medals, leader-bound entries; never the country) | [STC/leader], [POS/pos] |
| +0x598 / +0x658 | holder copies of B / scaled A (readers not traced) | [ADM/fields] |
| +0x708 / +0x7c8 / +0x888 / +0x948 | source holders B / A / B2 / A2 (land-only pairs +0xa08/+0xac8, +0xb88/+0xc48) | [STC/leader] |
| +0xd18 | A before scaling (copy) | [ADM/fields] |
| +0xdc8 / +0xdd4 | traits / count | [STC/leader] |
| +0xe60 | general skill level definition (`modifier` holder +0x10; level at def+0x1b8, 0xc199f0) | [STC/leader], [ADM/skill] |
| +0xe70 / +0xe74 | unit cap / group cap of the size factors (navy: 0 / −1, so both factors are 1; 0xc21d30) | [ADM/eff] |
| +0xe7c | leader type: 2 = navy (0, 1 land; 4 ?) | [STC/leader] |
| +0xe84 | modifier cooldown reason (0 none, 1 fleet/group change, 4/5 army deploy/withdraw); ≠ 0 → `eff = 0`, no adjusters | [ADM/eff] |
| +0xe90 / +0xea0 / +0xea8 | cooldown start hour / timer object / end hour | [ADM/eff] |
| +0xf48 | owning character handle | [STC/leader] |
| +0xf60 | fleet, for the task-force cap factor | [STC/leader] |
| +0xf68 / +0xf78 / +0xf88 / +0xf98 | navy attack / defense / maneuvering / coordination level (≥ 1, base + traits' adds) | [ADM/skill] |
| +0xf70 / +0xf80 / +0xf90 / +0xfa0 | navy skill-level definitions (`modifier` holder +0x10; +0xe0 container read by 0x1622d60) | [STC/leader] |
| +0xfb0 / +0xfb4 / +0xfb8 / +0xfbc | `min(0, level − 1)` per navy skill (save `<skill>_skill_temp_deficit`) | [ADM/skill] |
| +0xfc0 | navy: cached size efficiency (0xc21789; compared by 0xc1afc0) | [ADM/eff] |

### AirFX (air object)

| Offset | Meaning | Finding |
|---|---|---|
| +0x08 | group | [AIR/conv] |
| +0x10 | carrier member | [AIR/conv] |
| +0x1c | → carrier base | [AIR/conv] |
| +0x28 / +0x34 | strike wings (mission flags & 0x190: 0x10 naval strike, 0x80 kamikaze, 0x100 port strike). Refined by WINGS: the flags are the wing's available missions `wing+0x30` (equipment `allow_mission_type`), not the player's mission; averages and losses then filter on `executing_mission` `wing+0xa4` | [AIR/conv], [WINGS/strike] |
| +0x40 / +0x4c | all wings | [AIR/conv] |
| +0x5c | total planes, all wings. **disagrees** on scope: AIR "all wings on the carrier"; WINGS: one AirFX per wing pool (one sub-unit type) on the carrier, so the planes of that pool | [AIR/conv], [POS/carrier], [WINGS/strike] |
| +0x64 | country | [AIR/conv] |
| +0x80 | "already struck" byte (read only by a dead list and save code) | [AIR/cadence] |
| +0x81 | external (land-based) flag | [AIR/conv], [POS/carrier] |
| +0x58 | offered planes: Σ wing planes when built from a pool (0x1979aa0); external: the planes joined this time (0x197a6a0); caps per-wing losses in 0x197a1dc | [WINGS/ext] |
| +0x70 | date of the last join (0x197a6a0) | [WINGS/ext] |
| +0xc0 | external efficiency = 0xf78160 disruption factor of the joining mission (0x197a6da) | [AIR/ext], [WINGS/ext] |
| +0xc8..+0xe8 | plane-weighted equipment averages; +0xe8 = avg of equipment +0x260, name ? | [AIR/conv], [AIR/hit] |
| +0xf8 | cooldown hours | [AIR/cadence] |

### Other objects

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| carrier base | +0x60 | ship | [AIR/conv] |
| carrier base | +0x68 | capacity-penalty reduction applies only if 0 | [AIR/sortie] |
| carrier base | +0x78 | deck capacity | [AIR/sortie] |
| carrier base | +0xd8 | cached crowd factor (per country) | [AIR/sortie] |
| wing | +0x390 + 8×idx | raw equipment stat (idx 0x2f defence?, 0x30 attack?, 0x31 agility, 0x34 naval attack, 0x35 naval targeting) | [AIR/conv] |
| strategic region | +0x60 | region id? | [AIR/cadence] |
| strategic region | +0x140 | air phase | [AIR/cadence] |
| equipment type | +0x3f0 | `naval_dominance_factor` | [DOM] |
| variant | +0x408 bit 7 | dominance reads stat 0x49 instead of carrier size 0x48 (meaning open) | [DOM] |
| mission | +0x8 / +0x14 / +0x30 / +0x70 / +0xc8, +0xd4 | task force / type (MISSION_DOMINANCE_RATIOS index; save `mission=1` patrol) / active / region (one type) / regions, count | [DOM] |
| strategic region | +0xe8 | region dominance | [DOM] |
| region dominance | +0x40 / +0x4c / +0x78 | control snapshot (stride 0x50: country, +0x20 `current`; rebuilt daily by 0x1006280) / count / the region's own value | [DOM] |
| region dominance | +0x20 | value table (robin hood, entries 0x58; the value block V at entry+0x10) | [DOM] |
| dominance value V | +0x8 / +0x18 / +0x20 / +0x28 / +0x30 / +0x38 / +0x40 | country / current / base_target / target / individual_ratio / previous / decline_from | [DOM] |
| global | 0x332f260 | gamestate pointer | [LOG] |
| global | 0x3452520 / 0x3452524 | RNG counter / seed (the save's `multiplayer_random_count` / `multiplayer_random_seed`, observed) | [LOG] |
| global | 0x3452529 | log every `random_fixed` call (console `randomlog`) | [LOG] |
| gamestate | +0x310 / +0x31c | countries / count | [LOG] |
| country | +0x278 / +0x284 | fleets / count | [XP/mp], [LOG] |
| country | +0x290 / +0x29c | armies / count | [LOG] |
| gamestate | +0x468 | date in hours | [AIR/cadence] |
| gamestate | +0xa39 | HasGameStarted(): 1 in a running game, 0 while loading, resetting or in the menu (named by the assert at 0x1de28e) | [XP/mp] |
| gamestate | +0xa3a | set while the game state is being cleared (0x1d0e30); the ship dtor then leaves `country+0x250` alone | [POTF/sink] |
| gamestate | +0x688 | weather manager | [WTH/cmd] |
| weather manager | +0x10 | weather cells (0x180 bytes each; +0x28 active) | [WTH/cmd] |
| weather manager | +0x40 / +0x4c | region weather states (0x160 bytes each) / count | [WTH/combat] |
| weather manager | +0x308 | random seed; += random_int every hour (save `seed`) | [WTH/cmd], [WSIM/hourly] |
| weather manager | +0x58 / +0x5c | cell cursor / region-state cursor of the round robin (save `current_province` / `current_region`) | [WSIM/hourly] |
| weather manager | +0x228 / +0x230 | settings.snowing.temperature upper (1.0) / lower (−4.0) | [WSIM/roll] |
| weather manager | +0x260..+0x278 | settings.arctic_water temperature pairs (start, end) | [WSIM/roll] |
| weather manager | +0x290 … +0x2e8 | settings.duration min / max pairs: none, rain light, rain heavy, snow, blizzard, sandstorm | [WSIM/roll] |
| weather manager | +0x2f0 / +0x2f4 / +0x2f8 | provinces_per_update / regions_per_update / init_run_passes | [WSIM/hourly] |
| weather manager | +0x348 / +0x349 / +0x34a | init passes pending / second skip flag ? / weather disabled | [WTH/cmd] |
| weather manager | +0x350 / +0x358 / +0x360 | night direction x / y / z (fx5), set hourly by 0xf15eb0 | [NIGHT/sun] |
| weather manager | +0x368 | season fraction, 0 at 22 June, 1 at 22 December | [NIGHT/sun] |
| weather manager | +0x370 / +0x37c | province records (0x330 bytes each, index = province id) / count | [NIGHT/vec] |
| weather province record | +0x00..+0x23f | 24 hourly unit vectors (x, y, z fx5) of the province | [NIGHT/vec] |
| weather province record | +0x240 / +0x268 | darkness / darkness last applied to the holder | [NIGHT/dark] |
| weather province record | +0x248 / +0x250 / +0x258 | `(x − GMT_OFFSET) / W + 1` / `1 − abs(cos φ)` / `−cos φ` | [NIGHT/vec] |
| weather province record | +0x260 | local hour 1..24 | [NIGHT/dark] |
| weather province record | +0x270 (container +0x280) | province modifier holder (`night` × darkness) | [NIGHT/dark] |
| province | +0xb8 → +0xb8 / +0xbc | map position x / y (y from the bottom; = truncated mean pixel = `unitstacks.txt` type 0) | [NIGHT/vec] |
| map settings (0x3339d28) | +0x40 / +0x44 | map width / height | [NIGHT/vec] |
| region weather state | +0x48 | weather modifier holder | [WTH/combat] |
| region weather state | +0x108 / +0x110 / +0x118 / +0x120 | weather flags rain light / rain heavy / snow / blizzard → combat type 2 / 3 / 4 / 5 | [WTH/combat], [WTT/fill] |
| region weather state | +0x128 / +0x130 | sandstorm / arctic water flags (combat type 0) | [WTT/fill] |
| region weather state | +0x138 / +0x150 / +0x30 | mean temperature / hour of the next weather change / active flags of the region's `static_modifiers` | [WTT/fill] |
| region weather state | +0x8 / +0x140 | region id (roll hash input) / active in the round robin | [WSIM/roll] |
| weather period | +0x40 / +0x48, +0x58 … +0xd8 | temperature min / max; no_phenomenon, rain_light, rain_heavy, snow, blizzard, arctic_water, mud, sandstorm, min_snow_level (stride 0x10) | [WSIM/roll] |
| static modifiers | +0x28 / +0x30 / +0x38 / +0x40 / +0x48 / +0x50 / +0x3a0 | `weather_rain_light` / `_rain_heavy` / `_snow` / `_blizzard` / `_sandstorm` / `_arctic_water` / `_mud` (loader 0x60d830) | [WTT/fill] |
| strategic region | +0x58 | index into the weather manager's region states | [WTH/combat] |
| country | +0x5b8 | modifiers | [POS/pos], [XP/gain] |
| admiral | +0x418 | modifiers | [POS/pos] |
| country | +0x250 | pride-of-the-fleet ship handle; written by 0x712700 (also ctor 0x6c9cc0, reset 0x6e2de0, save reader 0x709d36); cleared by the ship dtor; save key `pride_of_the_fleet` | [STATS/getter], [POTF/readers] |
| country | +0x258 / +0x260 / +0x268 | date the PotF was lost: CDate object, hours at +0x260 (default 0x29c77f8 = 1.1.1.1 = never; written by 0x70c760), persistent base at +0x268 (save key `pride_of_the_fleet_date_lost`) | [POTF/sink], [POTF/save] |
| country | +0x270 | `pride_of_the_fleet_sunk_temporary` applied (0x6dade0) | [POTF/sink] |
| country | +0x484 | must be > 0 for the PotF command (meaning ?) | [POTF/assign] |
| country | +0xf88 | targeted-modifier table by target country index (entry holder +0x130) | [STATS/getter] |
| country | +0x15b8 / +0x15c4 | leader-bound timed entries, 0x58 bytes (+0 obj, +0x10 leader, +0x18 count, +0x48 expired; obj +0x3b8 / +0x478 holders, +0xa8 start, +0xac duration) | [STC/leader] |
| province | +0xa4 / +0xc8 / +0x188 | id / strategic region / controller tag (saved as `controller`) | [STATS/getter] |
| strategic region | +0x30 | its template; naval terrain via 0x15a5560 | [STC/mod], [TER/terrain] |
| strategic region template | +0x110 / +0x130 | `naval_terrain` name / resolved terrain (0x15a6a10) | [TER/terrain] |
| terrain | +0x10 | `naval_terrain = yes` | [TER/terrain] |
| terrain | +0xa8 / +0x168 | own modifier holder (container +0xb8) / own `units` adjuster {+0x10 attack, +0x18 defence, +0x20 movement} | [TER/terrain] |
| terrain | +0x198 / +0x1a4, +0x1b0 / +0x1b8 | per-sub-unit blocks (0x110 each: +0x20 sub-unit index, +0x28 modifier holder, +0xe8 `units`; the own block merged in by 0x143e980) / count, map sub-unit index → block | [TER/terrain] |
| naval terrain | +0x88 / +0xb8 / +0x168 / +0x1b8 | terrain index / modifier container / default sub-unit record / per-sub-unit records | [STATS/getter], [STC/mod] |
| character | +0xa8 / +0xd0 | unit-leader role / `unit_medals` object (holder +0x10) | [STC/leader] |
| trait | +0x360 / +0x420 / +0x4e0 / +0x5a0 | `modifier` / `non_shared_modifier` / `corps_commander_modifier` / `field_marshal_modifier` | [STC/leader] |
| trait | +0x768 / +0x774, +0x780 | adjuster list (`night`, terrains) / sub-unit block | [STC/leader] |
| trait | +0x788 / +0x7a0 | `sub_unit_modifiers`: `units` adjuster list / modifier-block list (CSubUnitModifiers at +0x780) | [ADM/subunit] |
| trait | +0xa20 / +0xa24 / +0xa34 | `attack_skill` / `defense_skill` / `coordination_skill` (maneuvering via 0x1dbe00) | [ADM/skill] |
| skill database | +0xe8 / +0x160 / +0x1d8 / +0x250 / +0x2c8 / +0x340 / +0x3b8 | overall / attack / defense / planning / logistics / maneuvering / coordination lists, each per type (stride 0x18; data +0, count +0xc); index = level, [0] = null object | [ADM/skill] |
| skill level entry | +0x08 / +0x10 / +0xd0 / +0x1b8 / +0x1c0 | valid / `modifier` holder / `building_module_modifier` holder / level / type (2 navy) | [ADM/skill] |
| sub-unit definition | +0x5a8 | `type` bitmask (copied to ship+0x628) | [STATS/getter] |
| modifier definitions | global 0x332ed90 | 0x78-byte entries; +0x60 flags read by 0x60fbd0 | [STC/leader] |
| modifier definition | +0x60 | flags (4th argument of 0x5574d0): bit 0 percentage, bit 1 positive is good, bit 2 neutral, bit 5 boolean (meanings inferred from all 666 definitions); +0x6c id, +0x70 token | [ADM/scale] |
| convoy | +0x20, +0x18 → +0x2e8 | current / max for the HP ratio (medium) | [TGT/weight] |
| convoy | +0x208 | "arg4" stat | [TGT/target] |
| shot profile (0x1970a90) | +0x00 / +0x08 / +0x10 / +0x18 / +0x20 / +0x28 / +0x30 / +0x38 | damage / crit chance / crit mult / hit chance / piercing / target armor / damage-reduction factor (−1 = none) / cooldown | [DMG/pipe] |
| damage record | +0x48 | 1 on kill | [DMG/apply] |
| stats | +0x124 | crit counter | [DMG/part] |
| critical part | +0x148 / +0x158 | str_damage_multiplier / str_damage (medium) | [DMG/part] |
| critical part | +0x150 / +0x160 | org_damage_multiplier / org_damage (medium) | [DMG/part] |
| critical part | +0x168 | pick weight (`chance`) | [DMG/part] |
| design / module | slot list +0x60 → module +0xb8 → +0x240 | module `critical_parts` | [DMG/part] |
| static modifiers | +0xd8 / +0xe0 / +0xe8 / +0xf0 | the four experience blocks (loaded at 0x60ec2b) | [XP/effect] |
| static modifiers | +0x58 / +0xb0 / +0xb8 / +0xc0 | `night` / `pride_of_the_fleet` / `pride_of_the_fleet_country` / `pride_of_the_fleet_sunk_temporary` (loader 0x60d830); +0xb0 read at 0xc38811, 0xc3b34c, 0xc3b372; +0xb8 at 0x6dbac0; +0xc0 at 0x6dbd39; tooltip 0x1beaba0 | [STC/sum], [POTF/static] |
| `set_pride_of_the_fleet_command` | +0x28 / +0x30 | ship handle (null = unassign) / country tag | [POTF/assign] |

### Doctrines (S-DOCTRINE)

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| track status (0x60, inline in the folder) | +0x08 | subdoctrine template (null = empty) | [DOC/fields] |
| track status | +0x10 | int unlocked rewards (save `rewards`) | [DOC/fields] |
| track status | +0x18 | fx5 mastery, cumulative since the subdoctrine was assigned (save `mastery`) | [DOC/fields] |
| track status | +0x20 | fx5 banked mastery (save `mastery_bank`) | [DOC/fields] |
| track status | +0x28 | fx5 base daily gain from units (save `daily_mastery`) | [DOC/fields] |
| track status | +0x30 / +0x3c | `leaders_daily_mastery` list / count | [DOC/fields] |
| track status | +0x48 | daily gain computed by 0xfc7d90 (not saved) | [DOC/fields] |
| track status | +0x50 / +0x58 | country doctrine status / folder status | [DOC/fields] |
| folder status (0x50) | +0x08 / +0x10 | folder template / active grand doctrine | [DOC/fields] |
| folder status | +0x18 / +0x20 / +0x24 | track statuses (stride 0x60) / capacity / count | [DOC/fields] |
| folder status | +0x30 | country doctrine status | [DOC/fields] |
| folder status | +0x38 / +0x44 | completed subdoctrines / count (never removed) | [DOC/fields] |
| country doctrine status | +0x08 | country tag | [DOC/fields] |
| country doctrine status | +0x10 / +0x1c | folder statuses (stride 0x50) / count | [DOC/fields] |
| country doctrine status | +0x28 / +0x34 | unlocked tactics / count | [DOC/fields] |
| country doctrine status | +0x58 / +0x64 | timed `add_daily_mastery` / `add_mastery_bonus` sources (stride 0x70: +0x28 filter, +0x58 amount, +0x60 bonus, +0x68 days) | [DOC/fields] |
| country doctrine status | +0x70 / +0x7c | equipment bonus records (0x18 each: +0x08 doctrine id, +0x0c index, +0x10 named-bonus handle) | [DOC/fields] |
| grand doctrine template | +0x08 / +0x28 | id / name | [DOC/fields] |
| grand doctrine template | +0x88 / +0xe0 | activation effects / their modifier block | [DOC/fields] |
| grand doctrine template | +0x300 | milestones (stride 0x190; effects +0x10, modifiers +0x68) | [DOC/fields] |
| grand doctrine template | +0x318 / +0x324 | track templates / count | [DOC/fields] |
| subdoctrine template | +0x08 / +0x28 | id / name | [DOC/fields] |
| subdoctrine template | +0x88 / +0xe0 | activation effects / their modifier block | [DOC/fields] |
| subdoctrine template | +0x310 / +0x31c | rewards (stride 0x1d8; effects +0x50, modifiers +0xa8, `mastery` +0x1d0) / count | [DOC/fields] |
| subdoctrine template | +0x340 | mastery conditions | [DOC/fields] |
| activation effects | +0x58 (+0x68) | modifier container (`modifier = {}`) (its entry list: top-level modifier keys) | [DOC/fields] |
| activation effects | +0x118 | sub-unit / category stat block (sub-units +0x08/+0x14, categories +0x20/+0x2c) | [DOC/fields] |
| activation effects | +0x150 | `enable_tactic` list (stride 0x28) | [DOC/fields] |
| activation effects | +0x168 / +0x174 | `equipment_bonus` list (stride 0x78) / count | [DOC/fields] |
| country | +0xf68 | equipment manager: +0xa0/+0xac variants, +0x160 idea bonuses, +0x178 named bonuses (0xc8 each), +0x548 last handle | [DOC/fields] |
| country | +0xf70 | sub-unit manager (save `deployment`): +0x08/+0x14 extra bonus sources (save `unit_modifiers`; stride 0x70: +0x08 block, +0x40 source type, +0x44 doctrine id, +0x48 index, +0x50 localization key), +0x20/+0x2c country sub-unit copies, +0x38 unlocked sub-units, +0x118 technology status | [DOC/fields] |
| equipment bonus (0x78) | +0x10 / +0x70 / +0x74 | key string / `instant` / key token | [DOC/fields] |
| equipment type | +0x4d8 | archetype | [DOC/effects] |
| equipment type | +0x540 / +0x548 | `type` bit masks (archetype's +0x540 when 0); `+0x548` is the default modules' `add_equipment_type` ([MIO/type]) | [DOC/effects] |
| trigger `has_doctrine` | +0x58 / +0x60 | subdoctrine / grand doctrine | [DOC/trig] |
| trigger `has_mastery` | +0x58 / +0x60 | amount (fx5) / track template | [DOC/mastery] |
| trigger `has_mastery_level` | +0x58 / +0x60 | amount (int) / subdoctrine | [DOC/mastery] |
| trigger `has_completed_track` | +0x58 | track template | [DOC/mastery] |

### MIO (S-MIO-INCLUDE)

From [S-MIO-include-and-tree.md][MIOI]. "MIO" is the database template (`industrial_org_template.cpp`); "trait" is one tree trait (0x3d8 bytes, `industrial_org_trait.cpp`); "country MIO" is the per-country instance (`industrial_organisation.cpp`).

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| MIO | +0x08 | token | [MIOI/fields] |
| MIO | +0x10 / +0x14 | `include` token / set | [MIOI/fields] |
| MIO | +0x18 / +0x24 | `delete_included_values` tokens / count | [MIOI/fields] |
| MIO | +0x30 / +0x50 / +0x88 | `name` / `icon` / `background` | [MIOI/fields] |
| MIO | +0x70 / +0x78 | `research_bonus` (fx5) / `task_capacity`; equal to DESIGN_TEAM_RESEARCH_BONUS / DEFAULT_INITIAL_TASK_CAPACITY counts as unset | [MIOI/fields] |
| MIO | +0xa8 / +0xac | `xp_research_type` (1 army, 2 navy, 3 air) / set | [MIOI/fields] |
| MIO | +0xb0 | initial trait (owned pointer, token `initial_trait`) | [MIOI/fields] |
| MIO | +0xb8 / +0xc4 | tree traits (inline, 0x3d8 each; `trait` and `add_trait`, then included ones) / count | [MIOI/fields] |
| MIO | +0xd0 | `equipment_type` filter (empty when `+0x12c` = 0) | [MIOI/fields] |
| MIO | +0x1a0 / +0x1ac | `research_categories` / count | [MIOI/fields] |
| MIO | +0x1b8 / +0x1c4 | `tree_header_text` (0x30 each) / count | [MIOI/fields] |
| MIO | +0x1d0 / +0x1dc | `remove_trait` tokens / count | [MIOI/fields] |
| MIO | +0x1e8 / +0x1f4 | `override_trait` traits not yet matched / count | [MIOI/fields] |
| MIO | +0x218 / +0x270 / +0x2c8 | `allowed` / `available` / `visible` | [MIOI/fields] |
| MIO | +0x320 / +0x378 / +0x3d0 / +0x428 / +0x480 / +0x4d8 | `on_design_team_assigned_to_tech` / `_to_variant` / `on_industrial_manufacturer_assigned` / `on_tech_research_cancelled` / `_completed` / `on_industrial_manufacturer_unassigned` | [MIOI/fields] |
| MIO | +0x530 | `ai_will_do` | [MIOI/fields] |
| MIO | +0x568 / +0x570 / +0x578 / +0x580…+0x5a8 / +0x5b0 | fallbacks to the included MIO's `allowed` / `available` / `visible` / `on_*` / `ai_will_do` | [MIOI/fields] |
| MIO database | +0x48 / +0x60 (+0x6c) / +0xa0 | token → MIO map / MIO list (count) / trait token map | [MIOI/include] |
| trait | +0x08 / +0x10 | token / owning MIO (the including MIO after a merge) | [MIOI/fields] |
| trait | +0x18 / +0x38 / +0x58 | `name` / `icon` / `special_trait_background` | [MIOI/fields] |
| trait | +0x60 | `limit_to_equipment_type` filter (empty when `+0xbc` = 0) | [MIOI/fields] |
| trait | +0x130 (+0x138 / +0x144) | stat list of `equipment_bonus` and `production_bonus` together (entries 16 bytes `{stat, fx5}` / count) | [MIOI/fields] |
| trait | +0x150 (+0x160) | `organization_modifier` holder (container) | [MIOI/fields] |
| trait | +0x210 / +0x268 / +0x2c0 / +0x318 | `available` / `visible` / `on_complete` / `ai_will_do` | [MIOI/fields] |
| trait | +0x350 / +0x358 / +0x360 / +0x368 | fallbacks to the included trait's `available` / `visible` / `on_complete` / `ai_will_do` | [MIOI/fields] |
| trait | +0x370 / +0x37c | parent groups (0x20 each: token list, `needed` at +0x18) / count; `parent`, `any_parent`, `all_parents` | [MIOI/fields] |
| trait | +0x388 / +0x394 | `mutually_exclusive` tokens / count | [MIOI/fields] |
| trait | +0x3a0 / +0x3a4 | `position` x / y (−1 = unset) | [MIOI/fields] |
| trait | +0x3a8 / +0x3ac | `relative_position_id` token / set | [MIOI/fields] |
| trait | +0x3b0 / +0x3bc | `delete_included_values` tokens / count | [MIOI/fields] |
| trait | +0x3c8 | script location (error messages; taken from the included trait on merge) | [MIOI/fields] |
| country MIO | +0x118 / +0x134 / +0x140 | template / int that must be > 0 to unlock (trait points?) / unlocked trait tokens (set). **disagrees** (detail): MIO reads `+0x118` = template + 0x218 (the `allowed`/`available`/`visible` part, 0xdb3e01) and `+0x134` = trait points (0xdbc860 asserts `_TraitPoints > 0`, decrements); settled in [README/disagree] | [MIOI/unlock], [MIO/map] |

### MIO equipment matching (S-MIO-MATCH)

From [S-MIO-equipment-matching.md][MIO]. "Equipment type" is a `common/units/equipment` entry (a hull for ships); a variant's equipment type (`variant+0x3f0`) is always a concrete one.

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| equipment type | +0x08 / +0x10 / +0x18 | name token / valid flag / name string | [MIO/map] |
| equipment type | +0x88 / +0x94 | `default_modules` pairs `{slot token, module token}` / count | [MIO/type] |
| equipment type | +0x4e0 | `parent` (defaults to self) | [MIO/type] |
| equipment type | +0x4e8 | `family` (defaults to self); the key match reads the archetype's | [MIO/match] |
| equipment type | +0x548 | OR of the own `default_modules`' `add_equipment_type` (0xc949d0) | [MIO/type] |
| equipment type | +0x555 | `is_archetype` | [MIO/type] |
| module | +0x78 | `add_equipment_type` keyword mask | [MIO/type] |
| equipment group (`CEquipmentGroup` / `CAnonymousEquipmentGroup`) | +0x50 / +0x5c | entries as written (0x28 bytes, hash at +0x20) / count | [MIO/match] |
| equipment group | +0x68 | reload listener (second vtable 0x27196c0) | [MIO/match] |
| equipment group | +0x70 (+0x88) / +0x90 (+0xa8) / +0xb0 (+0xc8) | caches: expanded tokens / expanded names / expanded names with group names (valid flags) | [MIO/match] |
| equipment database (0x22fa10, data 0x332eec0) | +0x68 / element 0 | name index / reserved null entry returned on a miss | [MIO/match] |
| equipment database | +0xe0 / +0xec | modules / count | [MIO/type] |
| country MIO | +0x110 | template + 0x88 (initial trait at its +0x28, traits at +0x30, `equipment_type` at +0x48) | [MIO/map] |
| country MIO | +0x148 | size of the unlocked-trait set (copied to the variant's `number_of_design_team_traits`) | [MIO/snap] |
| country MIO | +0x188 | attached policy id | [MIO/sum] |
| policy (status) | +0x08 / +0x38 | id / template (its bonus map: equipment and production keys together) | [MIO/sum] |
| equipment manager `[country+0xf68]` | +0x148 / +0x154 | MIO policies / count | [MIO/sum] |
| variant | +0x408 | own role mask with installed modules (0xbd3a50); not used by the MIO match | [MIO/type] |
| variant | +0x490 (+0x498 list, +0x4a4 count) | design team bonus snapshot (save `design_team_bonus`) | [MIO/snap] |
| variant | +0x4b0 | trait count when the snapshot was taken (save `number_of_design_team_traits`) | [MIO/snap] |

### Air wings, pools, bases and air missions (A-WINGS)

From [A-WINGS-carrier-wings-and-reinforcement.md][WINGS]. A wing handle resolves to `wing+0x10`.

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| wing | +0x1c | id (sort tie-break) | [WINGS/reinforce] |
| wing | +0x28 | replacement priority 0 / 1 / 2 (low / medium / high) | [WINGS/reinforce] |
| wing | +0x2c | equipment allowed missions (= `variant+0x410` of its planes) | [WINGS/strike] |
| wing | +0x30 | available missions (0xf667b0); AirFX strike list if `& 0x190` | [WINGS/strike] |
| wing | +0x38 | pool (CAirWingPool) | [WINGS/objects] |
| wing | +0x48 | transfer target base handle (null = not transferring) | [WINGS/reinforce] |
| wing | +0x60 / +0x64 | ready timer: hours needed / elapsed (meaning ?) | [WINGS/reinforce] |
| wing | +0x79 | blocks reinforcement (meaning ?) | [WINGS/reinforce] |
| wing | +0x7c | planes (equipment pool count) | [WINGS/reinforce] |
| wing | +0x88 | planes to return to the stockpile after a swap | [WINGS/reinforce] |
| wing | +0x90 | air mission object (`+0xa0` = its `type`, `+0xa4` = its `executing_mission`) | [WINGS/objects] |
| wing | +0x1c0 / +0x1cc | combats the wing is in | [WINGS/strike] |
| wing | +0x1d8 (+0x1f8 / +0x204) | equipment pool (entries {variant, amount}) | [WINGS/reinforce] |
| wing | +0x9c4 | owner tag override (exile) | [WINGS/objects] |
| wing | +0x9cc | reinforcement preference (0 obsolete only, 1 any (default), 2 only current, 3 only same) | [WINGS/reinforce] |
| wing | +0xa20 | reinforcement timer (REINFORCEMENT_DISABLING_DURATION_IN_LAND_CARRIER_TRANSFER) | [WINGS/reinforce] |
| wing | +0xa28 | must equal `variant+0x42c` to reinforce (meaning ?) | [WINGS/reinforce] |
| wing pool | +0x18 / +0x20 / +0x28, +0x34 / +0x40 | base handle / sub-unit definition / wings, count / country CStrategicAir | [WINGS/objects] |
| air base | +0x30[countryIdx] → +0xb8 / +0xc4 | that country's pools at the base / count | [WINGS/objects] |
| air base | +0x7c | base kind (1 missiles, 2 ?; else airfield or carrier) | [WINGS/ext] |
| CStrategicAir | +0x58 / +0x64, +0x70, +0x90, +0x188 | pools / count, not-ready wings, country tag, manpower budget of one delivery | [WINGS/objects] |
| air mission | +0x10 / +0x14 / +0x18 / +0x20 / +0x28 | `type` / `executing_mission` / `period` / `active` / owning wing | [WINGS/objects] |
| air mission | +0xa8 | disruption taken (not saved; inferred from the DISRUPTION_DEFENCE_* use) | [WINGS/ext] |
| air mission | +0xd8 / +0xe0 | `effectiveness` / `effective_planes_count` | [WINGS/ext] |
| variant | +0x408 | type keyword mask (0xbd3a50) | [WINGS/types] |
| variant | +0x410 | allowed missions (0xbdf456) | [WINGS/strike] |
| variant | +0x420 | manpower per plane (0xbda170) | [WINGS/reinforce] |
| variant | +0x424 / +0x42c | obsolete flag ? / matched with `wing+0xa28` | [WINGS/reinforce] |
| equipment type | +0x408 | `allowed_types` mask (read on the archetype) | [WINGS/types] |
| equipment type | +0x3c4 | level compared by the reinforcement preference (year ?) | [WINGS/reinforce] |
| module | +0x78 / +0x88 | `add_equipment_type` / a removed-types mask (probably `forbid_equipment_type`) | [WINGS/types] |
| sub-unit definition | +0x310 | `need` map (equipment → amount) | [WINGS/reinforce] |
| sub-unit definition | +0x600 / +0x604 / +0x608 / +0x60c | `land_air_wing_size` / `carrier_air_wing_size` / mega carrier / `submarine_carrier_air_wing_size` | [WINGS/reinforce] |
| country | +0xf78 | air CReinforcementStatus | [WINGS/reinforce] |
| naval combatant | +0x100 / +0x10c | external AirFX / count | [WINGS/ext] |

### Air mission save and execution (A-MISSION-SAVE)

From [A-MISSION-SAVE-air-mission-save-format.md][MSAVE]. Save keys in backticks.

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| air mission | +0x10 | `type` (assigned mission bits) | [MSAVE/write] |
| air mission | +0x14 | `executing_mission` (saved as a keyword; cleared by SetRegion, set by 0xc49e80) | [MSAVE/exec] |
| air mission | +0x18 | `period`: 0 day, 1 night, 2 day and night (ctor 2) | [MSAVE/exec] |
| air mission | +0x1c | `aggressiveness` (index into NAir.AGGRESSION_THRESHOLD) | [MSAVE/exec] |
| air mission | +0x20 | `active` (only EnableMission 0xf852a0 writes it) | [MSAVE/exec] |
| air mission | +0x30 | target strategic region (`strategic_region` = its +0x58) | [MSAVE/write] |
| air mission | +0x38 / +0x44 | in-range provinces of the region (16-byte {province, distance}) / count | [MSAVE/read] |
| air mission | +0x50 | coverage = in-range share of the region's provinces (not saved) | [MSAVE/read] |
| air mission | +0x58 / +0x64 | list built by 0xf7a760 in SetRegion / count (meaning ?) | [MSAVE/read] |
| air mission | +0x74 | `missions_done` | [MSAVE/write] |
| air mission | +0x78 | range the coverage was computed for (whole km; 0 after the ctor) | [MSAVE/read] |
| air mission | +0x7c | `stop_training_at_max_xp` | [MSAVE/write] |
| air mission | +0x80 / +0x8c | `priority` entries / count | [MSAVE/write] |
| air mission | +0xc0 | `region_change_penalty` (effectiveness × (1 − it)) | [MSAVE/eff] |
| air mission | +0xe8 | `effective_air_superiority` | [MSAVE/write] |
| air mission | +0xf0 | command power reservation | [MSAVE/read] |
| wing | +0x70 / +0x74 | `region_to_assign` / `mission_to_assign` | [MSAVE/read] |
| CStrategicAir | +0x98 | `_ActiveMissions[region id]` (24-byte lists, count +0xc): land-based missions per region | [MSAVE/lists] |
| CStrategicAir | +0xb0 / +0xc8 | `_AllActiveMissions` / `_ActiveMissionRegions` (sorted ids) | [MSAVE/lists] |
| CStrategicAir | +0xe0 | per-region records (0xa0 bytes); `+0x9a` = a non-AS / interception / training mission executes there | [MSAVE/exec] |
| CStrategicAir | +0x140 | per-region carrier mission lists (registration not decoded) | [MSAVE/exec] |
| country | +0x180 | air theatres (`air_theatre`; UI organisation, not read by mission execution) | [MSAVE/lists] |
| strategic region | +0x24 | province count (coverage denominator) | [MSAVE/read] |
| strategic region | +0x58 | region id (`strategic_region`, `spotting_region`); also the weather index ([WTH/combat]) | [MSAVE/write] |
| strategic region | +0xa0 | must be non-null for naval targets / naval patrol (meaning ?) | [MSAVE/exec] |
| gamestate | +0x2e0 / +0x2ec | strategic regions by id / count | [MSAVE/read] |
| province | +0x170 / +0xf8 | combats in the province / units (fleets) in it | [MSAVE/exec] |

### Aces (A-ACE)

From [A-ACE-aces-and-wing-experience-in-naval-strikes.md][ACE].

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| ace | +0x18 | type mask of its template | [ACE/objects] |
| ace | +0x30 | modifier container (copy of the template's `effect`) | [ACE/objects] |
| ace | +0xe0 / +0xe8 | country / its wing (handle) | [ACE/objects] |
| ace | +0xf0 / +0x110 / +0x130 | `name` / `surname` / `callsign` | [ACE/save] |
| ace | +0x150 / +0x154 | `is_female` / `portrait` | [ACE/save] |
| ace | +0x158 | alive (ctor 1; 0 after 0x61bba0; every stat getter checks it) | [ACE/objects] |
| ace | +0x15c / +0x160 / +0x168 / +0x16c | `kill_type` / `killer_name` (ace handle) / `killer_country` / `handled` | [ACE/save] |
| ace | +0x170 | template (`common/aces` entry): +0x28 type mask, +0x30 `chance`, +0x38 / +0x48 `effect` | [ACE/create] |
| wing | +0x218 | experience | [ACE/stats] |
| wing | +0x238 | experience modifier holder (0xf65f20); read in naval strikes only for `air_weather_penalty` / `air_night_penalty` | [ACE/stats] |
| wing | +0x2e8 | ace handle; saved as `ace = { id type }` | [ACE/objects], [ACE/save] |
| wing | +0x390 / +0x968 | raw stat cache (equipment; no ace) / per-mission stat tables | [ACE/stats] |
| air mission | +0x30 | strategic region (enables the region terms of the naval strike getters) | [ACE/stats], [MSAVE/read] |
| air mission | +0x98 / +0x9c / +0xa4 | pending air-to-air losses / enemy ace that hit the wing / its country (set by 0xf862a0, cleared by 0xf87550) | [ACE/rolls] |
| country | +0x12d8 / +0x12e4 | aces / count | [ACE/save] |
| gamestate | +0x720 | int from `random_int(gamestate.cpp, 0x980)`; ace seed term (meaning ?) | [ACE/rolls] |

### Region air war (A-REGION)

From [A-REGION-carrier-wings-in-the-region-air-war.md][REG].

| Object | Offset | Meaning | Finding |
|---|---|---|---|
| air mission | +0x58 / +0x64 | regions the mission flies through: 8-byte `{region id, distance in region}` entries (built by 0xf7a760); a mission is an air-war defender in each | [REG/war] |
| air mission | +0x98 | air-war losses of this hour (capped by `effective_planes_count`), applied by 0xf87550 | [REG/combat] |
| air mission | +0xa8 | disruption after escorts (0xc4c570; land list only); read by 0xf78160 (`e_i`) and 0xf846c6 | [REG/disrupt] |
| air mission | +0xb0 | disruption taken this air hour (0xf7be70 adds) | [REG/disrupt] |
| air mission | +0xb8 | escort value (0xf77360; AS missions) | [REG/disrupt] |
| air mission | +0xc8 | tag of the last country that disrupted it | [REG/combat] |
| air mission | +0xe8 | `effective_air_superiority` (0xf775d0) | [REG/order] |
| air-war entry | +0 / +8 / +0xc / +0x10 | 24 bytes: mission, planes `n`, capacity left, carrier-in-combat flag `cv` | [REG/war] |
| strategic air | +0x10 / +0x1c | the country's air bases (0xc66480) | [REG/lists] |
| strategic air | +0x110 / +0x11c | self and list A of 0xc53270 (allies?); escort / disruption sums | [REG/disrupt] |
| strategic air | +0x128 / +0x134 | list B of 0xc53270 (enemies?) | [REG/order] |
| strategic air | +0x140[r] | scrambled carrier missions per region, rebuilt hourly (0xc66480) | [REG/lists] |
| strategic air | +0xe0[r] (0xa0 each) | region record: +0x78 Σ air superiority, +0x80 ratio, +0x98 count, +0x9a strike flag | [REG/order] |
| task force | +0x1f0 → +0xc8 → +0x60 | location → region → region id (0xc66480) | [REG/lists] |
| AirFX | +0x60 | planes lost (region-war losses of its wings added by 0x1976b00) | [REG/losses] |
| wing | +0x548 | raw equipment stat 0x37 (`wing+0x390 + 8 × 0x37`; name not mapped): defender weight × (1 + it) | [REG/war] |

## 3. Modifier ids and read sites

### Modifier ids

Names come from the registration function 0x55f7f0 unless the confidence says otherwise.

| Id | Name | Reader | Finding |
|---|---|---|---|
| 0x2 | `air_maximum_speed_factor` (ace) | 0xf60760 (interception speed) | [ACE/stats] |
| 0x7 | `air_ace_generation_chance_factor` (country) | 0xf80f60 (`× (1 + v)` on the earn chance) | [ACE/rolls] |
| 0xb / 0xd / 0xf / 0x10 | `air_superiority_efficiency` / `air_intercept_efficiency` / `air_cas_efficiency` / `air_nav_efficiency` (country) | 0xf7ed40 (naval attack and agility, with a mission region) | [ACE/stats] |
| 0x15 | NAVAL_STRIKE (weather `naval_strike`) | 0x197b890 targeting mult | [AIR/conv], [AIR/hit] |
| 0x16 / 0x17 / 0x18 | NAVAL_STRIKE_ATTACK / TARGETTING / AGILITY_FACTOR | plane stat getters | [AIR/conv] |
| 0x19 | `naval_coordination` | no read through the modifier getters in combat code | [STC/names] |
| 0x22 | CARRIER_TRAFFIC | 0x1976db0 | [AIR/sortie] |
| 0x23 | CARRIER_NIGHT_TRAFFIC | 0x1976db0, 0x1977000 | [AIR/sortie], [HOUR/night] |
| 0x24 | `sortie_efficiency` (registered, loc key CARRIER_SORTIE_EFFICIENCY_FACTOR) | 0x1976db0 via 0x1972570 | [AIR/conv], [STC/names] |
| 0x28 | NAVAL_HIT_CHANCE | 0x1970b48 via 0x1972570 | [HIT/mods] |
| 0x29 | NAVAL_SPEED_FACTOR (HIT). **disagrees**: TGT uses it as the "arg4" sub-weight stat | 0x1971cc0 | [HIT/map], [TGT/target] |
| 0x2a | `naval_detection` | 0xc3aae0, 0xc3a950 | [POS/detect], [STC/names] |
| 0x34 / 0x35 | `experience_gain_navy_unit_factor` / `experience_gain_navy_unit` (confirmed; tokens 0x323e / 0x323d, flags 3 / 2; sources: operating country, ship medals, PotF block; no admiral) | 0xc3b240 | [XP/gain], [STC/names], [POTF/xp] |
| 0x38 | `submarine_attack`: flat add to the base attack of submarines (kinds 0–3) | 0xc34b66 | [STATS/getter] |
| 0x46 / 0x47 | NAVY_ORG / NAVY_ORG_FACTOR | 0xc3d4a3 | [LOG] |
| 0x57 | `naval_retreat_chance` (confirmed) | 0x1975280 | [ACT/retreat], [HOUR/end], [STC/names] |
| 0x58 | `naval_retreat_chance_after_initial_combat` (confirmed) | 0x1975280 (0x1975325) | [ACT/retreat], [HOUR/end], [STC/names] |
| 0x59 | `naval_retreat_speed_after_initial_combat` (confirmed) | 0x196fe80 | [ACT/speed], [STC/names] |
| 0x5a | `naval_enemy_positioning_in_initial_attack` (confirmed, [STC/names]). Gate **disagrees**: POS `side+0xd8 == 0`, HOUR "side not in all-retreat". ADM: read as own country + location + the **enemy** commanding admiral; with `+0xd8` = attacker it applies to the defender, `hours < COMBAT_INITIAL_DURATION` | 0x161b960 (0x161bbb5) | [POS/pos], [HOUR/end], [ADM/pos] |
| 0x5b | naval_retreat_speed (confirmed in-game: TFB `smoke_generator`'s country value, +0.2/day) | 0x196fe80 via 0x1972570 | [ACT/speed], [LOG] |
| 0x5c | `sub_retreat_speed` (confirmed) | 0x196fe80 | [ACT/speed], [STC/names] |
| 0x5d | `convoy_retreat_speed` (confirmed) | 0x196fe80 | [ACT/speed], [STC/names] |
| 0x6f / 0x70 / 0x71 | `air_attack_factor` / `air_defence_factor` / `air_agility_factor`: generic air combat only (0xf5e890, 0xf5d3b0); not read by naval strikes or naval interception | 0xf5d3b0, 0xf5e890 | [ACE/stats] |
| 0x72 | `air_ace_bonuses_factor` (country): flat add per nonzero ace entry | 0xf60760, 0xf5d3b0, 0xf5e5c0 | [ACE/stats] |
| 0x73 | `air_untrained_pilots_penalty_factor` | 0xf65f20 (experience holder) | [ACE/stats] |
| 0x7e / 0x7f | NAVAL_MORALE / NAVAL_MORALE_FACTOR | 0xc395a0 (organization regain) | [LOG] |
| 0x88 / 0x89 | `navy_anti_air_attack` / `navy_anti_air_attack_factor` | 0xc3a470 only (per-ship AA) | [STATS/getter], [STC/names] |
| 0xc2 / 0xc3 | `navy_submarine_attack_factor` / `navy_submarine_defence_factor` (type bit 7) | 0xc34a50 | [STATS/getter] |
| 0xc4 / 0xc5 | `navy_capital_ship_attack_factor` / `navy_capital_ship_defence_factor` (type bit 6) | 0xc34a50 | [STATS/getter] |
| 0xc6 / 0xc7 | `navy_screen_attack_factor` / `navy_screen_defence_factor` (type bit 8) | 0xc34a50 | [STATS/getter] |
| 0xc8 | `navy_submarine_detection_factor` | 0xc3a950 | [STC/names] |
| 0xc9 / 0xca / 0xcb | NAVAL_CARRIER_AIR_ATTACK / TARGETTING / AGILITY_FACTOR; in 0x1978e30 read from the country container only (an admiral's value has no effect) | 0x1978e30, 0x197ac80, 0x197c8b0 | [AIR/conv], [AIR/dmg], [ADM/subunit] |
| 0x104 / 0x105 / 0x106 | `air_strat_bomber_night_penalty` / `air_night_penalty` / `air_weather_penalty` | 0xf5bc80 / 0xf5c9d0 (night and weather factors) | [ACE/stats] |
| 0x107 | `naval_night_attack` × darkness of the task force's province (the former "—" row; HOUR/DMG disagreement settled: read) | 0xc34de4 | [STATS/getter], [HOUR/night], [DMG/dmg] |
| 0x10c | CARRIER_CAPACITY_PENALTY_REDUCTION | 0xc579f0 chain | [AIR/sortie] |
| 0x10d | `air_carrier_night_penalty_reduction_factor` (subtracted for carrier-based wings whose task force has a mission) | 0xf5bc80 | [ACE/stats] |
| 0x168 | `skill_bonus_factor` (unit leader); B only (country values have no effect); the only read in the executable | leader efficiency, `leader+0x298` (0xc2179e) | [STC/leader], [ADM/eff] |
| 0x181 | `reassignment_duration_factor` (flags 1); B only | 0xc0fdb0 (cooldown days) | [ADM/eff] |
| 0x18c | NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR (script `critical_receive_chance`; confirmed in-game: TFB damage_control × 0.8) | 0x19740c0; air 0x197ac80 as `(1 − v)` | [HIT/crit], [AIR/crit], [LOG], [POTF/air] |
| 0x19f | script key `navy_visibility` (loc NAVAL_VISIBILITY_FACTOR) | 0xc37006; sub visibility 0xc3aa20; surface visibility 0xc3ab80. ~~disagrees~~ settled: POS's 0x1ba/0x1bc are not in the ordinary reveal | [HIT/ship], [ACT/sub], [POS/sub], [STC/names] |
| 0x1a0 | script key `navy_casualty_on_hit` (loc NAVAL_CASUALTY_ON_HIT_FACTOR; confirmed) | 0xc3e0e0 (0xc3e305) | [DMG/xp], [XP/loss], [STC/names] |
| 0x1a1 | NAVAL_CASUALTY_ON_SINK_FACTOR (script `navy_casualty_on_sink`; static modifier `water_modifier_shark_infested` +0.6) | 0xd77ff0 | [DMG/xp] |
| 0x1b1 | `screening_efficiency` (confirmed; country + location + admiral) | 0x161ccb0 | [TGT/screen], [POS/screen], [STC/mod] |
| 0x1b2 | `positioning` (confirmed; country + location + admiral) | 0x161b960 (0x161bb4c) | [POS/pos], [STC/mod] |
| 0x1b3 | `screening_without_screens` (confirmed); country only: 0x15c70b0 called with no location and no admiral (0x161d2d6), so an admiral's value has no effect | 0x161ccb0 | [TGT/screen], [STC/mod], [ADM/pos] |
| 0x1b6 | CARRIER_FIGHTER_SORTIE_EFFICIENCY_FACTOR | 0x1976db0 | [AIR/sortie] |
| 0x1b7 | NAVAL_DAMAGE_FACTOR | 0x19740c0 and 0xc34a50 (counted twice?) | [DMG/dmg], [POS/dmg] |
| 0x1b8 | NAVAL_DEFENSE_FACTOR | 0x19740c0 | [DMG/dmg] |
| 0x1b9 | `naval_enemy_retreat_chance` (confirmed); read from the enemy commanding admiral's `+0x298` only | 0x1975280 (0x1975378) | [ACT/retreat], [STC/leader] |
| 0x1ba | NAVAL_CRITICAL_SCORE_CHANCE_FACTOR | 0x19740c0 | [HIT/crit], [DMG/conv] |
| 0x1bb | NAVAL_CRITICAL_EFFECT_FACTOR (scales part effects); `k = 1 +` a full separate 0xc38760 sum (admiral A and sub-unit block included) | 0xc38760 (0xc38b58) | [DMG/part], [ADM/part] |
| 0x1bc | `naval_torpedo_reveal_chance_factor` (confirmed) | torpedo-firing reveal only (0x1973ff7) | [ACT/sub], [POS/sub], [STC/names] |
| 0x1bd | `naval_torpedo_screen_penetration_factor` (confirmed) | 0x1972c00 torpedo MaxBox (0x1972cff) | [TGT/torp], [STC/names] |
| 0x1be | naval_torpedo_cooldown_factor | 0x1972a70 | [ACT/cooldown] |
| 0x1bf / 0x1c0 / 0x1c1 | NAVAL_TORPEDO / LIGHT_GUN / HEAVY_GUN _HIT_CHANCE_FACTOR (read via 0x1972570: TFB's `screening_bonus` entries confirmed in-game) | 0x1970b48 via 0x1972570 | [HIT/mods], [LOG] |
| 0x1c2 | NAVAL_TORPEDO_ENEMY_CRITICAL_CHANCE_FACTOR | 0x1970b48 | [HIT/crit] |
| 0x1c3 | NAVAL_TORPEDO_DAMAGE_REDUCTION_FACTOR | 0x1970b48 (0x1971338..) | [HIT/pierce] |
| 0x1c7 | NAVAL_HIT_CHANCE_AGAINST_A_COUNTRY | 0x1970b48 | [HIT/mods] |
| 0x1c8 / 0x1c9 | `navy_capital_ship_attack_factor_against` / `_defence_factor_against`: indexed by the province controller | 0xc34c29 | [STATS/getter] |
| 0x1ca / 0x1cb | `navy_screen_attack_factor_against` / `_defence_factor_against` (same lookup) | 0xc34c97 | [STATS/getter] |
| 0x1cc / 0x1cd | `navy_submarine_attack_factor_against` / `_defence_factor_against` (same lookup) | 0xc34bbb | [STATS/getter] |
| 0x1cf | CARRIER_SORTIE_HOURS_DELAY | 0x197d770 | [AIR/cadence] |
| 0x1d0 | `naval_enemy_fleet_size_ratio_penalty_factor` (confirmed; enemy admiral `+0x418` only, so country sources have no effect) | 0x161b960 (0x161bfdc) | [POS/size], [STC/leader] |
| 0x1d1 / 0x1d2 | `naval_has_potf_in_combat_attack` / `_defense`: own commanding admiral's `+0x418` only, when the owner country's PotF is afloat and its task force's combat is the same (any member state; ends when it sinks); tokens 0x3b59 / 0x3b5a, flags 3 | 0xc34a50 (0xc351b5) | [DMG/dmg], [STATS/getter], [POTF/combat] |
| 0x1d3 | NAVAL_CRITICAL_SCORE_CHANCE_FACTOR_AGAINST_A_COUNTRY | 0x19740c0 | [HIT/crit] |
| 0x248 | `navy_weather_penalty` (country only) | 0xd67310 | [STC/sum] |
| 0x25a | `ace_effectiveness_factor` (the ace's country): `× (1 + v)` on every ace value | 0x61a930 | [ACE/stats] |
| `[ship+0x664 / +0x668 / +0x66c]` | dynamic `experience_gain_<sub-unit>_training_factor` / `_mission_factor` / `_combat_factor` | 0xc3b240 (country container only) | [POTF/xp] |

### Static modifiers

| Block | Bound / read at | Effect | Finding |
|---|---|---|---|
| `screening_bonus` | bound 0x60ed2a; read 0x1972570 | `+screening × value` for boxes 2, 3, every key (vanilla naval_hit_chance 0.4, naval_retreat_speed 0.2, convoy_retreat_speed 0.2; TFB adds heavy/light gun hit chance factors 1 / 0.3, confirmed in-game) | [TGT/bonus], [HIT/mods], [LOG] |
| `capital_screening_bonus` | bound 0x60ed5d; read 0x1972570 | box 3: `+0x161ece0 value × value` (naval_retreat_speed 0.2, sortie_efficiency 0.1) | [TGT/bonus], [AIR/sortie] |
| `ship_experience_bonus_max`, `_malus_min`, `carrier_experience_bonus_max`, `_malus_min` | loaded 0x60ec2b / 0x60ec5e; applied 0xc3ce50 | level effect × `s` | [XP/effect] |
| `pride_of_the_fleet` | 0x60d830 → +0xb0; added in 0xc38760 when the ship is its owner's PotF (0xc387e2), any state; read again by 0xc3b240 for 0x35 / 0x34 | vanilla `experience_gain_navy_unit_factor` 0.25, `critical_receive_chance` −0.5 on the PotF itself | [STC/sum], [POTF/static] |
| `pride_of_the_fleet_country` | 0x60d830 → +0xb8; added to the country by 0x6dade0 while a PotF is assigned | vanilla/TFB `war_support_factor` 0.05 | [POTF/sink] |
| `pride_of_the_fleet_sunk_temporary` | 0x60d830 → +0xc0; added to the country by 0x6dade0 for PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION days after losing it | vanilla/TFB `war_support_factor` −0.10 | [POTF/sink] |
| `night` (× darkness), `weather_*` | generic gather 0xc38760 (mod 0x28); night holder 0x15c3080 for carrier_traffic | hit chance, carrier traffic | [HIT/mods], [AIR/sortie], [README/disagree] |

### Define read sites

| Define | Reader | Meaning | Finding |
|---|---|---|---|
| NNavy.COMBAT_MIN_DURATION | 0x15c28e0 | retreat gate `hours >= MIN` (HOUR said unused; settled) | [ACT/retreat], [README/disagree] |
| NNavy.COMBAT_INITIAL_DURATION | 0x161bb5f, 0x1975325, 0x196fe80 | gates mod 0x5a, 0x58, 0x59 only | [HOUR/end], [POS/recompute] |
| NNavy.COMBAT_CHASE_RESIGNATION_HOURS | group ctor → group+0x40 | unused in the tick (medium) | [HOUR/end] |
| NMilitary.COMBAT_MINIMUM_TIME | 0x161fcf0 / 0x1c6b4a0 | live mask 0xf (joining counts) while below | [HOUR/end] |
| COMBAT_MAX_GROUPS, BEST_CAPITALS_TO_SCREENS_RATIO | 0x161e6d0, 0x161fbc0 | group creation / merge | [HOUR/fex] |
| POSITIONING_PENALTY_HOURLY_DECAY_FOR_NEWLY_JOINED_SHIPS | 0x1620940 | joined penalty decay | [POS/joined] |
| POSITIONING_PENALTY_FOR_SHIPS_JOINED_COMBAT_AFTER_IT_STARTS, MAX_POSITIONING_PENALTY_FOR_NEWLY_JOINED_SHIPS | 0x161ab40 (0x161b2ec..) | joined penalty increase, cap | [POS/joined] |
| DOMINANCE_CONTROLLED_THRESHOLD_RATIO, DOMINANCE_EFFECT_ON_POSITIONING_FOR_CONVOY_ESCORT(_MAX_RATIO) | 0x161ab40 (0x161ad00..) | convoy-escort bonus | [POS/convoy] |
| DOMINANCE_PER_SHIP_PER_SPEED_NEUTRAL / _RANGE_NEUTRAL / _HEAVY_GUN_ATTACK / _CARRIER_SIZE | 0xbd5d60 (0xbd5d8d, 0xbd5e10, 0xbd5e79, 0xbd5e94); 0x1783xxx tooltip | ship dominance | [DOM] |
| MISSION_DOMINANCE_RATIOS | 0xfafde3 | mission multiplier | [DOM] |
| DOMINANCE_CONTROLLED_THRESHOLD_RATIO | 0x1003ff9 (also 0x1003690.., 0x161ab40) | control minimum `t/(1 − t)` × own value | [DOM], [POS/convoy] |
| NAVAL_DOMINANCE_ORG_RECOVERY | 0xc39943 | regain factor of the controlling side | [DOM], [LOG] |
| DOMINANCE_DAILY_GAIN_FACTOR / _LOSS_FACTOR | 0x1003cf1 / 0x1005301 | daily update: gain × target (capped at the target), loss × decline_from (no target left) | [DOM] |
| BASE_POSITIONING | 0x161b99b | positioning base | [POS/pos] |
| RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR, MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION | 0x161b960 | detection bonus (who.py misses) | [POS/detect] |
| MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY, HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR, MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO | 0x161b960 (0x161bf69..) | ship-ratio penalty | [POS/size] |
| HIGHER_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR, MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR | 0x161b960 (0x161c217..) | carrier-ratio penalty | [POS/carrier] |
| NAir.CARRIER_PLANES_AMOUNT_FOR_POSITIONING | 0x161eeb0 | deck-fill cap | [POS/carrier] |
| DAMAGE_PENALTY_ON_MINIMUM_POSITIONING | 0x161ed30 | positioning damage penalty | [POS/dmg] |
| SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING | 0x161ccb0 (0x161ce25/0x161ce48) | screening factor | [TGT/screen] |
| SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS / _CONVOYS | 0x161d16a, 0x161d459 / 0x161d193 | screens needed | [TGT/screen] |
| CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS / _CONVOYS | 0x161d81b / 0x161d844 | capitals needed | [TGT/screen] |
| SHIP_TO_FLEET_ANTI_AIR_RATIO | 0x161e400 (0x161e4d9..) | fleet AA × positioning | [POS/aa], [AIR/reduce] |
| AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING | 0x161c9e9 (tooltip only) | no gameplay effect | [POS/aa] |
| BASE_LEADER_TRAIT_GAIN_XP, FIELD_MARSHAL_XP_RATIO | 0xc1aa20 | leader XP | [HOUR/hour] |
| NAVAL_COMBAT_SUB_DETECTION_FACTOR, NAVAL_COMBAT_AIR_SUB_DETECTION_FACTOR | 0x1c6b560 (0x1c6b837..) | group sub detection | [ACT/sub] |
| SUBMARINE_REVEALED_TIMEOUT | 0x1c6b560 (0x1c6bbaa) | hour-0 sub-only reveal | [ACT/sub], [TGT/bb] |
| SUBMARINE_HIDE_TIMEOUT | 0x19736eb, 0x197408c, 0x1c6a7c0 (0x1c6a9b4..) | reveal duration | [ACT/sub], [TGT/bb] |
| SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING | 0x161ed70 | sub visibility × (1 + v(1 − pos)) | [POS/sub] |
| SUBMARINE_REVEAL_DETECTION_MULTIPLIER, SUBMARINE_BASE_STEALTH_VALUE | 0x1975400 | reveal chance | [ACT/sub] |
| SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER | 0x1973f87.. | firing reveal | [ACT/sub] |
| SUBMARINE_REVEAL_BASE_CHANCE, _POW, SUBMARINE_BASE_TORPEDO_REVEAL_CHANCE | none | deprecated, not read | [ACT/sub] |
| ALL_SHIPS / CAPITAL_ONLY_COMBAT / CARRIER_ONLY_COMBAT _ACTIVATE_TIME | 0x1971610 | activation thresholds | [ACT/activate] |
| BASE_GUN_COOLDOWNS, MIN_GUN_COOLDOWN | 0x1972a70 | cooldown | [ACT/cooldown] |
| REPAIR_AND_RETURN_PRIO_LOW / MEDIUM / HIGH_COMBAT | 0x1971816 (jump table 0x1971bd8) | HP threshold by aggressiveness | [ACT/retreat] |
| CAPITAL_SHIP_COMBAT_RETREAT_MULT | 0x197197a | ×0.5 for capitals and carriers | [ACT/retreat] |
| SUBMARINE_ESCAPE_RATIOS, AGGRESSION_SETTINGS_VALUES, AGGRESION_MULTIPLIER_FOR_COMBAT | 0x19717f0 | convoy-raid sub rule and aggressiveness check | [ACT/retreat] |
| AGGRESSION_*_EFFICIENCY_* | 0x1989c60 | strength score (not decoded) | [ACT/retreat] |
| COMBAT_RETREAT_DECISION_CHANCE | 0x1975280 | roll P | [ACT/retreat] |
| REPAIR_AND_RETURN_UNIT_DYING_STR | 0xb0f375, 0xd64ebc | not naval combat | [ACT/retreat] |
| BASE_ESCAPE_SPEED, ESCAPE_SPEED_PER_COMBAT_DAY, MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION, SPEED_TO_ESCAPE_SPEED, ESCAPE_SPEED_HIDDEN_SUB, ESCAPE_SPEED_SUB_BASE | 0x196fe80 | escape speed | [ACT/speed] |
| MIN_TRACTED_ASSIST_DAMAGE_RATIO | 0x1975480 | assist credit | [HOUR/order], [DMG/apply] |
| COMBAT_BASE_HIT_CHANCE | 0x1970dda, 0x19710eb, 0x1970ed0 | base × 100 | [HIT/hit] |
| DEPTH_CHARGES_HIT_CHANCE_MULT | 0x1970dff | DC base | [HIT/hit] |
| GUN_HIT_PROFILES, DEPTH_CHARGES_HIT_PROFILE | 0x197118a.. | weapon profile | [HIT/hit] |
| MIN_HIT_PROFILE_MULT | 0x1971185.. | squared ratio floor | [HIT/hit] |
| COMBAT_LOW_ORG_HIT_CHANCE_PENALTY, COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY | 0x1971212.. | shooter org/manpower factor | [HIT/hit] |
| COMBAT_MIN_HIT_CHANCE | 0x19712de.. | min, applied last | [HIT/hit] |
| HIT_PROFILE_SPEED_FACTOR, HIT_PROFILE_MULT, HIT_PROFILE_SPEED_BASE | 0xc37006 | ship profile | [HIT/ship] |
| CONVOY_HIT_PROFILE | 0x1ad7f00 (data 0x3334310) | convoy profile | [HIT/ship] |
| COMBAT_EVASION_TO_HIT_CHANCE | none | dead | [HIT/mods] |
| NAVY_PIERCING_THRESHOLDS, _THRESHOLD_DAMAGE_VALUES, _THRESHOLD_CRITICAL_VALUES | 0x196fd60 (count data 0x33399a4) | tier | [HIT/pierce] |
| COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION | 0x197132c..0x1971416 | inert in vanilla | [HIT/pierce] |
| DEPTH_CHARGES_DAMAGE_MULT | 0x1970c79 | DC damage | [DMG/dmg] |
| COMBAT_BASE_CRITICAL_CHANCE, COMBAT_CRITICAL_DAMAGE_MULT | 0x1970db2.. ; air 0x197ac80 | gun and air crit | [DMG/dmg], [AIR/crit] |
| COMBAT_TORPEDO_CRITICAL_CHANCE, COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT | 0x1970f23.. | torpedo crit | [DMG/dmg] |
| COMBAT_ARMOR_PIERCING_CRITICAL_BONUS | 0x197ac80 only | air crit | [HIT/crit], [AIR/crit] |
| CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT | 0x19740c0 (0x1974511..) | part chance | [DMG/part] |
| CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR | 0x1975150 | air part chance | [AIR/crit] |
| COMBAT_DAMAGE_RANDOMNESS | 0x19740c0 (0x197438a) only | damage randomness | [DMG/random] |
| COMBAT_DAMAGE_TO_STR_FACTOR, COMBAT_DAMAGE_TO_ORG_FACTOR | 0x19740c0 (ORG at 0x1974453) | split | [DMG/apply] |
| EXPERIENCE_LOSS_FACTOR, MANPOWER_LOSS_RATIO_ON_STR_LOSS (0xc3e3d3), MIN_MANPOWER_RATIO_TO_DROP (0xc3e390) | 0xc3e0e0 | XP and manpower loss | [DMG/xp], [XP/loss] |
| MANPOWER_LOSS_RATIO_ON_SUNK | 0xd77ff0 | sunk manpower | [DMG/xp] |
| OUT_OF_FUEL_ATTACK_FACTOR, OUT_OF_FUEL_TORPEDO_FACTOR | 0xc34a50 (0xc35212 / 0xc351f7: torpedo for kind 3) | attack penalty `(1 − fuel) × factor` | [DMG/dmg], [STATS/getter] |
| ADMIRAL_TASKFORCE_CAP | 0xd53880 (0xd5388f), 0xc18780, 0xd55f40, 0xd56740 | 0 disables the admiral cap factor; vanilla 10, TFB 5 | [STC/leader], [ADM/eff] |
| NMilitary.UNIT_LEADER_MODIFIER_COOLDOWN_ON_GROUP_CHANGE | 0xc25b30 (0xc25c6b), 0xc25db0 (0xc25ded), 0xc2698f; 0x1ac446b, 0x1ed8804 (UI) | days of the reason-1 modifier cooldown (`eff = 0`, no adjusters) | [ADM/eff] |
| NMilitary.CORPS_COMMANDER_DIVISIONS_CAP, CORPS_COMMANDER_ARMIES_CAP, FIELD_MARSHAL_DIVISIONS_CAP, FIELD_MARSHAL_ARMIES_CAP | 0xc21d30, 0xc10ab0 (0xc10aca) | army leader caps of the size factors; not used for a navy leader | [ADM/eff] |
| NAVY_MAX_XP | 0xc2ffe8 | XP cap | [XP/levels] |
| NMilitary.CAPTAIN_EXPERIENCE_ON_SHIP_MULT (0x3337a98), CAPTAIN_EXPERIENCE_ON_SHIP_PER_MEDAL_MULT (0x3337b48) | 0xc318c9, 0xc31916 | captain XP from ship XP | [POTF/xp] |
| NNavy.PRIDE_OF_THE_FLEET_UNASSIGN_COST (0x3332110) | 0x1359473, 0x1356891; UI 0x1bed049, 0x1bed754 | political power to replace or unassign | [POTF/assign] |
| NNavy.PRIDE_OF_THE_FLEET_LOST_TEMP_MODIFIER_DURATION (0x33321a8) | 0x6dbb59, 0x6dbcfe | days of `pride_of_the_fleet_sunk_temporary` | [POTF/sink] |
| NNavy.NAVAL_ACCIDENT_CHANCE_REDUCTION_ON_POTF (0x3332b18), NAVAL_ACCIDENT_CRITICAL_HIT_CHANCE_REDUCTION_POTF (0x3332bc0) | 0xc3c2d2, 0xc3c342 | accident factors for the PotF (missions, not battle) | [POTF/readers] |
| NMilitary.UNIT_EXP_LEVELS | 0xbf9b90 (data 0x3339500, count 0x333950c) | ship levels | [XP/levels] |
| NMilitary.ARMY_EXP_BASE_LEVEL | 0xc3ce75 | level effect base | [XP/effect] |
| UNIT_EXPERIENCE_PER_COMBAT_HOUR, UNIT_EXPERIENCE_SCALE, EXPERIENCE_FACTOR_CONVOY_ATTACK / _NON_CARRIER_GAIN / _CARRIER_GAIN | 0xc338a0, 0x15c3320 (factors fx15 via 0x2072200) | XP gain | [XP/gain] |
| LEADER_EXPERIENCE_SCALE | 0xc3be74 → 0xc0f200 | captain XP | [XP/gain] |
| FIELD_EXPERIENCE_SCALE, FIELD_EXPERIENCE_MAX_PER_DAY | 0xc3bee3 → 0x12a6740 | national XP | [XP/gain] |
| XP_GAIN_FACTOR | 0xc1133e | unrelated code | [XP/gain] |
| DAILY_MANPOWER_GAIN_RATIO | 0xc3e4e0 (0xc3e5be) | daily refill, in combat too | [XP/mp] |
| HOURS_DELAY_AFTER_EACH_COMBAT | 0xe24530, 0xe23bb0 | air clock | [AIR/cadence] |
| CARRIER_HOURS_DELAY_AFTER_EACH_COMBAT | 0x197d770 (0x197d8a9) | written, no effect | [AIR/cadence] |
| CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO, SELECTED_SORTIE_INITIAL_TIME, BASE_CARRIER_SORTIE_EFFICIENCY | 0x1976db0 | sortie base | [AIR/sortie] |
| CARRIER_OFFENSIVE_STANCE_DEFAULT_INDEX | 0xd6343b | fleet default | [AIR/stance] |
| CAPACITY_PENALTY | 0xc579f0 chain | overcrowding | [AIR/sortie] |
| NAVAL_COMBAT_PLANE_MIN_STACKING_PENALTY, SHIP_SILHOUETTE_VALUE_PLANES_*, SCREEN_CAP_REDUCTION_FACTOR | 0x161e9a0, 0x1c6a540 | stacking | [AIR/stack] |
| NAVAL_COMBAT_EXTERNAL_PLANES_MIN_CAP, JOIN_RATIO, JOIN_RATIO_PER_DAY | 0x161b5f0 | external cap | [AIR/ext] |
| CARRIER_PERCENTAGE_DEFEND, COMBAT_MULTIPLANE_CAP, CARRIER_COMBAT_DAMAGE_STATS_MULTIPLIER | 0x197b380 area | interception | [AIR/intercept] |
| BIGGEST_AGILITY_FACTOR_DIFF, COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION | 0xf77630 | interception | [AIR/intercept] |
| BIGGEST_SPEED_FACTOR_DIFF, TOP_SPEED_DAMAGE_BONUS_FACTOR | 0xf7ad60 | interception | [AIR/intercept] |
| COMBAT_DAMAGE_SCALE_CARRIER / DISRUPTION_FACTOR_CARRIER | 0xf862a0 / 0xf7be70 | generic air, not naval | [AIR/intercept] |
| NAVAL_COMBAT_AIR_*_TARGET_SCORE (base/scale, STRENGTH, LOW_AA at 0x197abc9) | 0x197a700 | air target weight | [AIR/target] |
| ANTI_AIR_TARGETING, ANTI_AIR_TARGETTING_TO_CHANCE, ANTI_AIR_ATTACK_TO_AMOUNT, AIR_NAVAL_KAMIKAZE_LOSSES_MULT | 0x197c8b0 | preemptive AA | [AIR/preempt] |
| ANTI_AIR_MULT, ANTI_AIR_POW, MAX_ANTI_AIR_REDUCTION | 0x196fca0 | AA reduction | [AIR/reduce] |
| NAVAL_STRIKE_TARGETTING_TO_AMOUNT, NAVAL_STRIKE_CARRIER_MULTIPLIER, NAVAL_STRIKE_DAMAGE_TO_STR / _ORG | 0x197ac80 | air hits and damage | [AIR/hit], [AIR/dmg] |
| AIR_AGILITY_TO_NAVAL_STRIKE_AGILITY | 0xf60bc0 | agility | [AIR/conv] |
| AIR_NAVAL_KAMIKAZE_DAMAGE_MULT | 0xf60f10 | kamikaze attack | [AIR/conv] |
| PORT_STRIKE_DAMAGE_FACTOR | port-strike branch (not decoded) | port damage | [AIR/open] |
| NDoctrines.DEFAULT_REWARD_MASTERY (global 0x3333e60) | 0x9e4dac | reward cost when the reward's `mastery` is 0 | [DOC/defines] |
| NDoctrines.MASTERY_BANK_CONVERSION_RATE (0x3334480) | 0xfc7890; UI 0xfc790c, 0x1f35e37 | × daily gain of complete or empty tracks | [DOC/defines] |
| NDoctrines.MASTERY_BANK_MAX (0x3334558) | 0x147bfe6 (leaf 0x147bfd0); UI 0x1f35e54 | bank cap | [DOC/defines] |
| NDoctrines.MIN_MASTERY_GAIN_PER_DAY (0x33341f0) | 0xfc774b | floor of a positive daily gain | [DOC/defines] |
| NDoctrines.MAX_MONTHLY_MASTERY_GAIN (0x3334110), BASE_MASTERY_GAIN_TARGET_MANPOWER (0x3333f48) | 0x1a328f0, 0x1a32ad0, 0x1a32cb0; UI 0x1f3744e | unit gain cap and diminishing returns (not decoded) | [DOC/defines] |
| NDoctrines.TRAINING_MASTERY_GAIN_FACTOR (0x3334028), NAVAL_MISSION_MASTERY_GAIN_FACTORS (0x3338668) | 0x9e39ea, 0x9e39cf | mission factors (not decoded) | [DOC/defines] |
| NDoctrines.MILITARY_ATTACHE_MASTERY_TRANSFER_FACTOR (0x3334640), THEATER_COMMANDER_UNITS_MASTERY_GAIN_FACTOR_PER_SKILL (0x3334708) | 0x11212ee, 0x1a33466; 0x1184558 | other gain sources (not decoded) | [DOC/defines] |
| NIndustrialOrganisation.DESIGN_TEAM_RESEARCH_BONUS (0x3334630), DEFAULT_INITIAL_TASK_CAPACITY (0x333476c) | 0xa541e0, 0xa54208 | an including MIO whose `research_bonus` / `task_capacity` equals the define takes the included value | [MIOI/defines] |
| NAir.REINFORCEMENT_DISABLING_DURATION_IN_LAND_CARRIER_TRANSFER | 0xf599d9, 0xf59e0c, 0xf5a169, 0xf5a3b9 (wing ctors) | wing reinforcement timer | [WINGS/reinforce] |
| NCountry.REINFORCEMENT_AIRBASE_PRIORITY_DEFAULT (global 0x3337624) | 0xf597c2 | wing replacement priority default | [WINGS/reinforce] |
| NAir.MIN_PLANE_COUNT_AIR_SUPPLY | 0xf6686c | air supply removed from the available missions | [WINGS/strike] |
| NNavy.CARRIER_OFFENSIVE_STANCE_SORTIE_RATIO | 0xf78476 | carrier wings' mission plane budget | [WINGS/ext] |
| NAir.MISSILE_LAUNCHER_SLOTS | 0xc54e73 | missile base budget | [WINGS/ext] |
| NAir.DISRUPTION_DEFENCE_ATTACK_FACTOR / _DEFENCE_FACTOR / _SPEED_FACTOR | 0xf781f1 / 0xf781f9 / 0xf78278 | external efficiency (disruption) | [WINGS/ext] |
| NAir.GIE_EXILE_AIR_START_EXPERIENCE | 0xf6410b | experience of planes added to an exile wing | [WINGS/reinforce] |
| NAir.ACE_EARN_CHANCE_BASE, ACE_EARN_CHANCE_PLANES_MULT | 0xf81117, 0xf810cf (also 0xf7c674, air combat) | earn chance `(BASE + 0.1 × damage × MULT) × (1 + gen factor)` | [ACE/rolls] |
| NAir.ACE_DEATH_CHANCE_BASE, ACE_DEATH_CHANCE_PLANES_MULT | 0xf81008, 0xf80fbd; 0xf875f3 / 0xf875a0 (air-to-air); 0xf5df61 (accident) | death chance `BASE + 0.1 × losses × MULT` | [ACE/rolls], [ACE/death] |
| NAir.ACE_DEATH_BY_OTHER_ACE_CHANCE | 0xf8105a, 0xf8762b | killed by the recorded enemy ace | [ACE/rolls] |
| NAir.ACE_WING_SIZE, ACE_WING_SIZE_MAX_BONUS | 0x61832b, 0x618315 (also 0x61a1a5, not decoded) | ace size factor `min(SIZE / planes, MAX)` | [ACE/stats] |
| NAir.DAY_NIGHT_COVERAGE_FACTOR | 0xf61503, 0xf60e62, 0xf611de (also 0xf5ec2b) | province night term in the plane stat getters | [ACE/stats] |
| NGraphics.GMT_OFFSET (int, 0x3333468) | 0xf15086 | province `tz` | [NIGHT/vec] |
| NGraphics.SOUTH_POLE_OFFSET, NORTH_POLE_OFFSET (0x33335d0, 0x3333690) | 0xf15112, 0xf15119, 0xf15138 | map y → polar angle | [NIGHT/vec] |
| NGraphics.DAY_NIGHT_FEATHER (0x3333508) | 0xf188d4 | ramp width: globals 0x333d3f0 = F/2, 0x333d3f8 = −F/2, 0x30b1dc8 = 1/F | [NIGHT/dark] |
| NAir.COMBAT_DAMAGE_SCALE / COMBAT_DAMAGE_SCALE_CARRIER | 0xf86563 / 0xf8656d | region air-war losses; CARRIER when attacker and defender are carrier wings in combat | [REG/combat] |
| NAir.DISRUPTION_FACTOR / DISRUPTION_FACTOR_CARRIER | 0xf7c500 / 0xf7c50c | region air-war disruption (carrier between carrier wings) | [REG/combat] |
| NAir.DISRUPTION_SPEED_FACTOR / _AGILITY_FACTOR / _ATTACK_FACTOR / _DETECTION_FACTOR | 0xf7c0d7 / 0xf7c108 / 0xf7c12f / 0xf7c159 (DETECTION also 0xf77450) | disruption power of an attacker; escort | [REG/combat] |
| NAir.ESCORT_FACTOR, ESCORT_SPEED_FACTOR, ESCORT_AGILITY_FACTOR, ESCORT_ATTACK_FACTOR | 0xf7756a, 0xf77438, 0xf77440, 0xf77448 | escort value `+0xb8` | [REG/disrupt] |
| NAir.COMBAT_MULTIPLANE_CAP | 0xf7c094, 0xf864e2, 0xf81bcb | engaged planes, capacity use, XP ratio | [REG/combat] |
| NAir.COMBAT_DAMAGE_STATS_MULTILPIER, BIGGEST_AGILITY_FACTOR_DIFF, COMBAT_BETTER_AGILITY_DAMAGE_REDUCTION | 0xf863be, 0xf8641a, 0xf8642e | region air-war damage | [REG/combat] |
| NAir.COMBAT_BETTER_SPEED_DAMAGE_INCREASE, AIR_WING_MAX_STATS_SPEED | 0xf7ae3e, 0xf7ad6a / 0xf60873 | speed bonus, normalised speed | [REG/combat] |
| NAir.AIR_WING_MAX_STATS_ATTACK / _DEFENCE / _AGILITY | 0xf5ef01 / 0xf5ef66 / 0xf5efb2 | normalised stats `+0x18 / +0x20 / +0x28` of 0xf5e890 | [REG/combat] |
| NAir.INTERCEPTION_DISTANCE_SCALE, INTERCEPTION_DAMAGE_SCALE | 0xf7c372, 0xf7c3c5 | defender weight when it flies through another region | [REG/combat] |
| NAir.AIR_WING_XP_AIR_VS_AIR_COMBAT_GAIN | 0xf81c26 | attacker XP | [REG/war] |
| NAir.ACCIDENT_CHANCE_BALANCE_MULT | 0xc605a9 | accident chance | [REG/accident] |

## 4. Helpers and conventions

| Item | RVA | Rule | Finding |
|---|---|---|---|
| fx5 fixed point | — | int64 × 100000; `stored = round(float32(lua) × 1e5)` | [README] |
| fixed multiply | — | `imul`, magic 0x29f16b11c6d1e109, `sar 0xe`, + sign bit = truncate toward zero | [ACT], [DMG/conv] |
| fixed divide | — | `a × 1e5 idiv b`, truncating; zero/overflow → raw 0xffffffff (42949.67) | [DMG/conv], [HIT/map] |
| /24 idiom | — | magic 0xdfb23b0979b4b03 >> 81 (escape progress) | [HOUR/hour] |
| /1e7 idiom | — | magic 0xd6bf94d5e57a42bd, `sar 23` (detection /100) | [POS/detect] |
| fx15 defines | 0x2072200 | `(int64)(f32 × 32768)`, truncated; products `sar 15` | [XP/top] |
| fx5 ↔ fx15 | 0x24ed3f0 / 0x24ed580 | conversions | [XP/top] |
| fixed → int | 0x24ed730 | round half away from zero | [DMG/conv], [AIR/conv] |
| negate | 0x24ef6f0 | stores `-x`; −1.0 is the "no value" sentinel | [DMG/conv] |
| RNG | 0x2233db0 | `random_fixed(out, file, line)`: hash of seed + counter, 31 bits, `% 100000` → U ∈ {0, …, 0.99999}; (file, line) tag per call. A save replays the identical battle; editing its `multiplayer_random_seed` gives a different one | [DMG/conv], [HIT/map], [LOG] |
| RNG lines | naval combat update | 0x37b target pick, 0x3d9 torpedo line, 0x587 hit, 0x593 damage randomness, 0x59f crit, 0x2f4 part damage, 0x78f part pick, 0xa83 retreat decision, 0x40e sinking (6–9 per sinking, no combat effect); identified from call order in the logs | [LOG] |
| RNG int | 0x2233fa0 | random int31 | [AIR/conv] |
| weighted draw | 0x24f08a0 | air target pick | [AIR/target] |
| chance checks | — | ACT: `roll <= chance`; hit: `100·U < hc`; crit: `U <= p`; part: `U < p` | [ACT], [HIT/crit], [DMG/rng] |
| RNG line tags | — | naval_fire_exchange_member.cpp 0x587 hit, 0x593 randomness, 0x59f crit; ship.cpp 0x2f4 part chance, 0x78f part pick; target 0x37b, torpedo reach 0x3d9; air 0x212, 0x21c, 0x43f, 0x45d | [DMG/rng], [TGT/target], [AIR/preempt] |
| RNG call sites | return addresses | `random_fixed`: 0xf86610 (0xacf), 0xf86660 (0xade), 0xf86735 (0xae5) in 0xf862a0; 0xf7c674 (0x82b), 0xf7c710 (0x833), 0xf7c727 (0x834) in 0xf7be70. `random_int`: 0x197ac2a (0x43f) in 0x197a700, 0xc6006e (0x12c9) in 0xc5f4c0, 0xc5f43a (0x1be8) in 0xc5efc0, 0xf7c989 (0x83f) in 0xf7be70. Found by the line immediate before each call (r8d / edx); `battle_logger.js` forces the region air war's by these (S-REPLAY-CONSTANT) | [LOG] |
| persistent vtable | — | +0x10 Write, +0x18 Read block (0x24be690), +0x20 ReadMember (token switch), +0x40 InitPostRead (runs at the end of the object's own block, so objects later in the file are not loaded yet) | [DOC/load] |
| script token ids | 0x831c0 | each keyword's id is the rdata dword loaded by the `mov edx, dword ptr [rip+X]` before its `lea r8, name`; rebuilding name ↔ id decodes token switches and save jump tables (doctrine keys 0x4182–0x41a7, e.g. `rewards` 0x4182, `mastery` 0x418c, `mastery_bank` 0x419c) | [DOC] |
| tooling caveat | — | `who.py`/`uses.json` miss reads (decode from p−12, lose alignment; leaf functions without unwind entries) | [README], [POS/top], [XP/top] |

## 5. Open / UNRESOLVED

Cross-file disagreements are flagged "**disagrees**" in the tables above; the three settled ones are in [findings/README.md][README/disagree].

- Aggressiveness-check strength score 0x1989c60 internals. [ACT/open], [README-RE]
- ~~Darkness value computation~~ settled: terminator per province, date and hour, reproduced exactly in 10 logged battles; open: whether the province position comes from `provinces.bmp` or `unitstacks.txt`, and how the battle province is chosen. [NIGHT], [HOUR/night]
- How the design's slot list is ordered (calibrated: slot names in first-appearance order of the equipment files). [DMG/part], [LOG]
- The fixed-point rounding of `0xc3ac60` and the weight products (calibrated: quotient rounded half up, products truncated; 120,090 logged picks). [TGT/weight], [LOG]
- Shooter "arg4" stat (`ship+0x228`, modifier 0x29) gating sub targets. [TGT/dc]
- Whether light guns and depth charges both fire in one slot-2 call. [ACT/open]
- ~~Ship-type flag bit 39~~ settled: sub-unit type `support_ship`. [ACT/open], [TGT/bb], [STATS/getter]
- Combatant +0xd8, combat +0x10c/+0x10d meanings; convoy-only branch of the retreat check. [ACT/open]
- Convoy-type combat end path (EndHour returns false). [HOUR/end]
- Purpose of side vt+0x100 `0x1621a70`. [HOUR/hour]
- ~~Modifier names by index~~ settled from the registration + static token table. [STC/names]
- Slot 0 of the targeted-modifier table (`country+0xf88`): whether the attack/defence `_against` factors ever apply at sea. [STATS/getter]
- ~~Which modifier definitions 0x60fbd0 scales by leader efficiency; CNavyLeader vt+0x88/+0x90 and the cap 0xc18780~~ settled: the registration flags and the per-flag rule ([ADM/scale]); both size factors are 1 for a navy leader ([ADM/eff]). [STC/leader]
- Admiral: `tf+0x4b8`; the readers of `leader+0x598`/`+0x658`/`+0xd18`/`+0xfc0`; the child containers scaled at depth 2; when the cooldown ends (vt+0xc8 0xc12470) and whether `reassigned` is given on a fleet change; the use of the speed and sub attack read in 0x1973521; mid-battle level-up rebuilds. [ADM/open]
- `ship+0x8f8` object, `tf+0x658`, 0xd6fa50, province holder 0xf197d0, the `tf+0x728` building-module entries, the identity of the `country+0x15b8` entries; any `naval_coordination` reader. [STC]
- Ship `+0x678` hit profile mult, `+0x568`, `0xc3b130`. [HIT/ship], [HIT/crit]
- ~~NAVAL_DAMAGE_FACTOR counted twice (attack getter and hit multiplier)?~~ Yes: observed in logged hits (the experience malus applies in the shot profile and again per hit). [DMG/open], [LOG]
- Ship `+0x738` link; damage to non-ship targets (`0x1ad8610`). [DMG/open]
- Experience-level damage bonus location (DMG: not in the hit path; XP: only through `naval_damage_factor`/`naval_defense_factor` modifiers). [DMG/open], [XP/effect]
- Refresh frequency of the enemy count snapshot (0x161f810) vs the firing loop. [TGT/bb]
- ~~Convoy-escort dominance helper 0x143c720~~ decoded (`own × t/(1 − t)`); the mission efficiency functions; ~~the daily dominance update~~ decoded (S-DOMINANCE-DAILY; MISSION_DOMINANCE_RATIOS is not on the daily target's path). [POS/convoy], [DOM]
- Order of the task-force update 0xd705f0 vs the combat hour (this hour's shots or last hour's). [XP/open]
- `combat+0x120` setter; `region+0x60` = region id? [AIR/open]
- `airfx+0xe8` name (equipment +0x260). [AIR/open]
- Top-speed term in 0xf7ad60; plane stat indices 0x2f/0x30. [AIR/open]
- ~~`0xf78160` (fuel?); `airfx+0xc0` source; caller and 5th argument of 0x16205c0~~ settled: disruption efficiency, caller 0xf82ca0 with 0 ([WINGS/ext]). [AIR/open]
- ~~Who sets `executing_mission` (`wing+0xa4`) of carrier wings~~ settled: 0xc49e80 → 0xf7acc0 from the available missions ([MSAVE/exec]); whether two pools of one sub-unit can sit on a carrier; bit 23 of the type map (token id 0); `variant+0x42c` / `wing+0xa28`; the two wing stats in the disruption formula (0xf5e890); priority among distributables (0xd0bdd0 / 0xd0c650) and stockpile order (0xbd2420); the variant test 0xbd7dc0 of the swap path. [WINGS/open]
- Port-strike branch (`combat+0x10c`, PORT_STRIKE_DAMAGE_FACTOR). [AIR/open]
- ~~`0xf80f60` per-wing losses/experience~~ decoded: ace death / earn rolls, no experience ([ACE/rolls]). [AIR/losses]
- `gamestate+0x720` (ace template seed term); whether a dead ace is ever detached from its wing; the content of the region weather / province night holders in the naval strike getters (`R` term); accident checks for carrier wings in battle; static blocks behind struct +0xc8 / +0xd0. [ACE/open]
- Rebuilt region weather state after `disable_weather` (`0xf1cb90`): no weather and an empty holder? (Which weather each flag is: settled, [WTT/fill]; `set_weather`: decoded, [WSIM/roll].) [WTH/open]
- Weather cell temperature model (0xf22730, 0xf22420, 0xf21c60), the arctic water ramp direction. [WSIM/open]
- Weather roll temperature thresholds (`wm+0x228` / `+0x230`, read as `settings.snowing.temperature`), duration unit. [WTT/open]
- How a non-instant equipment bonus first reaches a variant (new variants copy the parent's, 0x1492ac0); the documented `modifiers = {}` block; ~~key match `+0x4e8` and 0x22f740~~ settled: `family` and the equipment group database ([MIO/match]); the unit mastery gain (`track+0x28`) and the attaché, leader and gain-factor terms; effect re-application on save load; the idea slot reader (spirit slots are from the scripts, [DOC/spirit]); on load, whether country modifiers exist before the ships' InitPostRead and whether `deployment` may be moved before `units` ([DOC/load]). [DOC/open]
- ~~Country MIO `+0x134`~~ settled: trait points ([MIO/map]); the other arguments of `CanUnlockTrait` 0x13af360; chained includes (one pass in database order; no script uses one); ~~whether a `limit_to_equipment_type` naming only unknown equipment counts as empty~~ settled: not empty, it matches nothing ([MIO/match]). [MIOI/open]
- MIO matching: 0xbd3a50 in full (variant role mask); the roster's use of `allowed`/`visible`/`available`; when the create-variant command carries its own snapshot; production mode 0xdb7a30 consumers; the policy template base shift (map read at `+0x40`, written at `+0x50`); 0x9f8570. [MIO/open]
- Pride of the fleet: whether any order moves a task force out of a running naval combat (the escaped case assumes not); bit 37 in the eligibility test; which medal holder is `ship_modifiers`; `country+0x484`; the AI weight in 0x1310de0. [POTF/open]
- Air missions: how often the wing update 0xf62750 runs (penalty decay hourly or daily); the coverage term of 0xf78500; 0xc58660's region filter; `region+0xa0`; the combat tests 0x13e27f0 / 0x161f620; `mission+0x58` (0xf7a760); the carrier list `strategicAir+0x140` and 0xf79c10; `priority` entries. [MSAVE/open]
- Region air war: the detection value 0xf77730; `strategicAir+0x110 / +0x128` membership (0xc53270); the mission `+0x58` list of a carrier wing (double entries?); the trigger of 0xc647b0 for a task force that does not move; `stat37`; targeted modifier slots 1 / 0xd; the isCarrier branch of 0xf78500; accident chance 0xf61d70 and amount 0xf5dd80; jobs 0x2959ee0 / f20 / f40. [REG/open]

[README]: findings/README.md
[LOG]: findings/LOG-ingame-logging.md
[README/disagree]: findings/README.md#disagreements-between-findings-and-how-they-were-settled
[README-RE]: README.md#status
[HOUR]: findings/S-HOUR-order-and-end.md
[HOUR/chain]: findings/S-HOUR-order-and-end.md#0-call-chain-who-drives-the-hour
[HOUR/hour]: findings/S-HOUR-order-and-end.md#s-hour-per-hour-structure
[HOUR/order]: findings/S-HOUR-order-and-end.md#s-order-firing-order-most-important
[HOUR/fex]: findings/S-HOUR-order-and-end.md#fire-exchange-fex-groups
[HOUR/end]: findings/S-HOUR-order-and-end.md#s-end-end-of-battle
[HOUR/night]: findings/S-HOUR-order-and-end.md#night--time-of-day
[ACT]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md
[ACT/bg]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#background-member-state-machine-and-the-hour-counter-needed-by-every-item
[ACT/activate]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#s-activate--when-lines-start-firing
[ACT/cooldown]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#s-cooldown--weapon-cadence
[ACT/retreat]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#s-retreat--retreat-decision
[ACT/speed]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#s-retreat-speed--escaping
[ACT/sub]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#s-sub--submarine-hiding-and-revealing
[ACT/open]: findings/S-ACTIVATE-activation-cooldown-retreat-subs.md#unresolved
[TGT]: findings/S-TARGET-targeting-and-screening.md
[TGT/bb]: findings/S-TARGET-targeting-and-screening.md#0-building-blocks-needed-to-read-everything-below
[TGT/screen]: findings/S-TARGET-targeting-and-screening.md#s-screen-screening-efficiency
[TGT/bonus]: findings/S-TARGET-targeting-and-screening.md#the-screening-bonus-affects-s-hit-mods-s-retreat-speed-a-sortie
[TGT/target]: findings/S-TARGET-targeting-and-screening.md#s-target-overall-algorithm-picktarget-0x1971cc0-called-from-fire-0x1973e20---0x1973ef3
[TGT/light]: findings/S-TARGET-targeting-and-screening.md#s-target-light-gun-2-maxbox-0x1972c92
[TGT/heavy]: findings/S-TARGET-targeting-and-screening.md#s-target-heavy-gun-0-maxbox-0x1972dd4
[TGT/torp]: findings/S-TARGET-targeting-and-screening.md#s-target-torp-gun-1-maxbox-0x1972cb70x1972dc5
[TGT/sub]: findings/S-TARGET-targeting-and-screening.md#s-target-sub-submarine-shooters-subminbox-0x1971db40x1971e94
[TGT/dc]: findings/S-TARGET-targeting-and-screening.md#s-target-dc
[TGT/weight]: findings/S-TARGET-targeting-and-screening.md#s-target-weight-0x1971440
[HIT]: findings/S-HIT-hit-chance-and-crits.md
[HIT/map]: findings/S-HIT-hit-chance-and-crits.md#function-map-rvas
[HIT/hit]: findings/S-HIT-hit-chance-and-crits.md#s-hit-full-hit-chance-formula
[HIT/ship]: findings/S-HIT-hit-chance-and-crits.md#s-hit-ship-target-hit-profile-0xc37006
[HIT/mods]: findings/S-HIT-hit-chance-and-crits.md#s-hit-mods
[HIT/pierce]: findings/S-HIT-hit-chance-and-crits.md#s-dmg-pierce-piercing-tier-0x196fd60
[HIT/crit]: findings/S-HIT-hit-chance-and-crits.md#s-crit-critical-hits
[DMG]: findings/S-DMG-damage-and-application.md
[DMG/conv]: findings/S-DMG-damage-and-application.md#conventions-and-helpers
[DMG/pipe]: findings/S-DMG-damage-and-application.md#pipeline-overview
[DMG/dmg]: findings/S-DMG-damage-and-application.md#s-dmg--s-dmg-pierce-damage-side-base-damage-of-a-hit
[DMG/random]: findings/S-DMG-damage-and-application.md#s-dmg-random
[DMG/apply]: findings/S-DMG-damage-and-application.md#s-dmg-apply-hp-and-organization
[DMG/part]: findings/S-DMG-damage-and-application.md#s-crit-part-critical-part-damage
[DMG/xp]: findings/S-DMG-damage-and-application.md#s-xp-manpower-and-experience-loss-on-damage-0xc3e0e0-flag--0
[DMG/rng]: findings/S-DMG-damage-and-application.md#rng-call-order-per-shot-for-scripted-tests
[DMG/open]: findings/S-DMG-damage-and-application.md#unresolved
[POS]: findings/S-POS-positioning.md
[POS/top]: findings/S-POS-positioning.md
[POS/pos]: findings/S-POS-positioning.md#s-pos-positioning-formula
[POS/recompute]: findings/S-POS-positioning.md#when-positioning-is-recomputed
[POS/detect]: findings/S-POS-positioning.md#s-pos-detect-detection-bonus
[POS/size]: findings/S-POS-positioning.md#s-pos-size-ship-ratio-penalty
[POS/carrier]: findings/S-POS-positioning.md#carrier-ratio-penalty-new-spec-lists-it-as-open
[POS/joined]: findings/S-POS-positioning.md#newly-joined-ships-new
[POS/convoy]: findings/S-POS-positioning.md#convoy-escort-dominance-bonus-new
[POS/dmg]: findings/S-POS-positioning.md#s-pos-dmg-damage
[POS/screen]: findings/S-POS-positioning.md#s-pos-screen-screening
[POS/aa]: findings/S-POS-positioning.md#aa-penalty
[POS/sub]: findings/S-POS-positioning.md#sub-reveal
[XP]: findings/S-XP-experience.md
[XP/top]: findings/S-XP-experience.md
[XP/levels]: findings/S-XP-experience.md#s-xp-levels-experience-levels-of-ships
[XP/effect]: findings/S-XP-experience.md#s-xp-effect-what-each-level-does-in-combat
[XP/gain]: findings/S-XP-experience.md#s-xp-gain-experience-gain-in-combat
[XP/loss]: findings/S-XP-experience.md#s-xp-loss-experience-loss-when-damaged
[XP/mp]: findings/S-XP-experience.md#s-manpower-manpower-recovery-during-battle
[XP/open]: findings/S-XP-experience.md#open--unresolved
[AIR]: findings/A-air-combat.md
[AIR/conv]: findings/A-air-combat.md#conventions-and-helpers
[AIR/graph]: findings/A-air-combat.md#call-graph-of-one-air-step
[AIR/cadence]: findings/A-air-combat.md#a-cadence-when-planes-strike
[AIR/sortie]: findings/A-air-combat.md#a-sortie-how-many-carrier-planes-fly
[AIR/stance]: findings/A-air-combat.md#a-stance
[AIR/stack]: findings/A-air-combat.md#a-stack-stacking-factor
[AIR/ext]: findings/A-air-combat.md#a-external-land-based-planes
[AIR/intercept]: findings/A-air-combat.md#a-intercept-carrier-fighters-vs-bombers
[AIR/target]: findings/A-air-combat.md#a-target-target-selection
[AIR/preempt]: findings/A-air-combat.md#a-aa-preempt-preemptive-shoot-down
[AIR/reduce]: findings/A-air-combat.md#a-aa-reduce-fleet-aa-damage-reduction
[AIR/hit]: findings/A-air-combat.md#a-hit-how-many-planes-hit
[AIR/dmg]: findings/A-air-combat.md#a-dmg-strike-damage
[AIR/crit]: findings/A-air-combat.md#a-crit-crits-from-air
[AIR/losses]: findings/A-air-combat.md#a-losses
[AIR/open]: findings/A-air-combat.md#open-questions--unresolved
[WTH]: findings/S-WEATHER-weather-toggle.md
[NIGHT]: findings/S-NIGHT-darkness.md
[NIGHT/vec]: findings/S-NIGHT-darkness.md#province-vectors-once-0xf14fe0-called-from-0xf13500-at-map-set-up
[NIGHT/sun]: findings/S-NIGHT-darkness.md#night-direction-every-hour-0xf15eb0-called-from-0xf18800
[NIGHT/dark]: findings/S-NIGHT-darkness.md#darkness-every-hour-0xf18800--0xf20c50-per-province-threaded-when-there-are-many-provinces
[NIGHT/fx]: findings/S-NIGHT-darkness.md#fixed-point
[TER]: findings/S-TERRAIN-naval-terrain.md
[TER/terrain]: findings/S-TERRAIN-naval-terrain.md#naval-terrain
[WTH/cmd]: findings/S-WEATHER-weather-toggle.md#console-commands
[WTH/combat]: findings/S-WEATHER-weather-toggle.md#what-a-battle-reads
[WTH/open]: findings/S-WEATHER-weather-toggle.md#unresolved
[WTT]: findings/S-WEATHER-weather-terms.md
[WTT/sum]: findings/S-WEATHER-weather-terms.md#1-the-weather-term-in-the-ship-modifier-sum-0xc38760
[WTT/fill]: findings/S-WEATHER-weather-terms.md#2-how-the-regions-holder-is-filled
[WTT/read]: findings/S-WEATHER-weather-terms.md#3-which-combat-readers-see-the-region-weather
[WTT/night]: findings/S-WEATHER-weather-terms.md#4-the-province-holder-0xf197d0-is-night--darkness
[WTT/open]: findings/S-WEATHER-weather-terms.md#unresolved
[WSIM]: findings/S-WEATHER-region-simulation.md
[WSIM/hourly]: findings/S-WEATHER-region-simulation.md#the-hourly-update
[WSIM/roll]: findings/S-WEATHER-region-simulation.md#the-roll-at-a-visit-0xf20ef0
[WSIM/open]: findings/S-WEATHER-region-simulation.md#unresolved
[STATS]: findings/S-STATS-design-stats.md
[STATS/getter]: findings/S-STATS-design-stats.md#s-stats-combat-getter-the-attack-and-armor-getter-0xc34a50-fully-decoded
[STC]: findings/S-STATS-COUNTRY-modifier-sources.md
[STC/names]: findings/S-STATS-COUNTRY-modifier-sources.md#modifier-names
[STC/sum]: findings/S-STATS-COUNTRY-modifier-sources.md#the-ship-modifier-sum-0xc38760
[STC/mod]: findings/S-STATS-COUNTRY-modifier-sources.md#modid-country-location-admiral-0x15c70b0
[STC/member]: findings/S-STATS-COUNTRY-modifier-sources.md#member-getter-0x1972570
[STC/leader]: findings/S-STATS-COUNTRY-modifier-sources.md#the-admirals-containers
[DOM]: findings/S-DOMINANCE-naval-dominance.md
[DOC]: findings/S-DOCTRINE-doctrines.md
[DOC/map]: findings/S-DOCTRINE-doctrines.md#function-map
[DOC/fields]: findings/S-DOCTRINE-doctrines.md#struct-fields
[DOC/mastery]: findings/S-DOCTRINE-doctrines.md#s-doctrine-mastery
[DOC/save]: findings/S-DOCTRINE-doctrines.md#s-doctrine-save
[DOC/load]: findings/S-DOCTRINE-doctrines.md#loading
[DOC/effects]: findings/S-DOCTRINE-doctrines.md#s-doctrine-effects
[DOC/trig]: findings/S-DOCTRINE-doctrines.md#s-doctrine-triggers-has_doctrine
[DOC/spirit]: findings/S-DOCTRINE-doctrines.md#s-spirit-slots-from-the-scripts
[DOC/defines]: findings/S-DOCTRINE-doctrines.md#defines-and-constants-involved
[DOC/open]: findings/S-DOCTRINE-doctrines.md#unresolved
[MIOI]: findings/S-MIO-include-and-tree.md
[MIOI/answers]: findings/S-MIO-include-and-tree.md#answers
[MIOI/read]: findings/S-MIO-include-and-tree.md#readers
[MIOI/include]: findings/S-MIO-include-and-tree.md#include-resolution
[MIOI/merge]: findings/S-MIO-include-and-tree.md#trait-merge
[MIOI/pos]: findings/S-MIO-include-and-tree.md#positions
[MIOI/unlock]: findings/S-MIO-include-and-tree.md#unlocking-parents-and-mutual-exclusion
[MIOI/fields]: findings/S-MIO-include-and-tree.md#struct-fields
[MIOI/map]: findings/S-MIO-include-and-tree.md#function-map
[MIOI/defines]: findings/S-MIO-include-and-tree.md#defines-and-constants-involved
[MIOI/open]: findings/S-MIO-include-and-tree.md#unresolved
[MIO]: findings/S-MIO-equipment-matching.md
[MIO/map]: findings/S-MIO-equipment-matching.md#function-map-rvas
[MIO/match]: findings/S-MIO-equipment-matching.md#the-match-one-resolver-for-trait-limits-the-mios-equipment_type-and-policy-keys
[MIO/type]: findings/S-MIO-equipment-matching.md#the-type-the-bonus-is-computed-for-the-hull-not-the-design
[MIO/sum]: findings/S-MIO-equipment-matching.md#the-bonus-sum-0xdb7800-mode-1-for-a-variant
[MIO/assign]: findings/S-MIO-equipment-matching.md#which-mios-a-design-can-have
[MIO/snap]: findings/S-MIO-equipment-matching.md#when-the-bonus-is-computed-a-snapshot-per-variant
[MIO/save]: findings/S-MIO-equipment-matching.md#save-format-s-mio-save
[MIO/open]: findings/S-MIO-equipment-matching.md#unresolved
[ADM]: findings/S-ADMIRAL-leader-combat.md
[ADM/sum]: findings/S-ADMIRAL-leader-combat.md#summary-one-admiral-commanding-one-task-force
[ADM/map]: findings/S-ADMIRAL-leader-combat.md#function-map
[ADM/fields]: findings/S-ADMIRAL-leader-combat.md#struct-fields
[ADM/eff]: findings/S-ADMIRAL-leader-combat.md#1-leader-efficiency
[ADM/scale]: findings/S-ADMIRAL-leader-combat.md#2-the-scaling-0x60fbd0-and-the-modifier-flags
[ADM/skill]: findings/S-ADMIRAL-leader-combat.md#3-skill-levels
[ADM/subunit]: findings/S-ADMIRAL-leader-combat.md#4-per-sub-unit-blocks
[ADM/adjust]: findings/S-ADMIRAL-leader-combat.md#5-terrain-and-night-adjusters
[ADM/speed]: findings/S-ADMIRAL-leader-combat.md#6-combat-speed
[ADM/pos]: findings/S-ADMIRAL-leader-combat.md#7-positioning-screening-and-the-retreat-roll
[ADM/reads]: findings/S-ADMIRAL-leader-combat.md#8-other-reads-of-the-commanding-admiral
[ADM/part]: findings/S-ADMIRAL-leader-combat.md#9-the-damaged-part-effect-factor
[ADM/defines]: findings/S-ADMIRAL-leader-combat.md#defines-and-constants-involved
[ADM/save]: findings/S-ADMIRAL-leader-combat.md#save-format-s-admiral-save
[ADM/open]: findings/S-ADMIRAL-leader-combat.md#unresolved
[POTF]: findings/S-POTF-pride-of-the-fleet.md
[POTF/sum]: findings/S-POTF-pride-of-the-fleet.md#summary-what-the-simulator-must-do
[POTF/readers]: findings/S-POTF-pride-of-the-fleet.md#1-readers-of-the-pride-of-the-fleet
[POTF/combat]: findings/S-POTF-pride-of-the-fleet.md#2-in-the-same-combat
[POTF/static]: findings/S-POTF-pride-of-the-fleet.md#3-the-static-block-and-experience-gain
[POTF/xp]: findings/S-POTF-pride-of-the-fleet.md#the-experience-add-0xc3b240-and-its-caller-0xc31840
[POTF/air]: findings/S-POTF-pride-of-the-fleet.md#4-air-crit-sign
[POTF/assign]: findings/S-POTF-pride-of-the-fleet.md#5-who-can-be-the-pride-of-the-fleet
[POTF/sink]: findings/S-POTF-pride-of-the-fleet.md#6-losing-the-pride-of-the-fleet
[POTF/save]: findings/S-POTF-pride-of-the-fleet.md#7-save-format
[POTF/open]: findings/S-POTF-pride-of-the-fleet.md#unresolved
[WINGS]: findings/A-WINGS-carrier-wings-and-reinforcement.md
[WINGS/objects]: findings/A-WINGS-carrier-wings-and-reinforcement.md#objects
[WINGS/types]: findings/A-WINGS-carrier-wings-and-reinforcement.md#a-wings-types-type-keyword-bits-the-fighter-tests-read-the-sub-unit-type
[WINGS/strike]: findings/A-WINGS-carrier-wings-and-reinforcement.md#a-wings-strike-which-carrier-wings-strike
[WINGS/ext]: findings/A-WINGS-carrier-wings-and-reinforcement.md#a-wings-ext-land-based-planes-joining-a-naval-battle
[WINGS/reinforce]: findings/A-WINGS-carrier-wings-and-reinforcement.md#a-wings-reinforce-planes-from-the-stockpile
[WINGS/defines]: findings/A-WINGS-carrier-wings-and-reinforcement.md#defines-and-constants-involved
[WINGS/open]: findings/A-WINGS-carrier-wings-and-reinforcement.md#unresolved
[MSAVE]: findings/A-MISSION-SAVE-air-mission-save-format.md
[MSAVE/token]: findings/A-MISSION-SAVE-air-mission-save-format.md#token-ids
[MSAVE/write]: findings/A-MISSION-SAVE-air-mission-save-format.md#a-mission-save-write-the-writer-0xf87820
[MSAVE/read]: findings/A-MISSION-SAVE-air-mission-save-format.md#a-mission-save-read-the-reader-0xf84d40-and-what-it-does-on-load
[MSAVE/lists]: findings/A-MISSION-SAVE-air-mission-save-format.md#a-mission-save-lists-where-region-missions-are-kept
[MSAVE/exec]: findings/A-MISSION-SAVE-air-mission-save-format.md#a-mission-save-exec-what-must-hold-for-the-wing-to-execute-the-mission
[MSAVE/eff]: findings/A-MISSION-SAVE-air-mission-save-format.md#a-mission-save-eff-effectiveness-is-recomputed
[MSAVE/example]: findings/A-MISSION-SAVE-air-mission-save-format.md#what-to-write-one-naval-strike-wing-and-one-air-superiority-wing-over-region-30
[MSAVE/open]: findings/A-MISSION-SAVE-air-mission-save-format.md#unresolved
[ACE]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md
[ACE/objects]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#objects
[ACE/rolls]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#a-ace-rolls-what-0xf80f60-does-after-each-naval-strike
[ACE/create]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#a-ace-create-which-ace-0x618860
[ACE/stats]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#a-ace-stats-how-an-ace-and-wing-experience-enter-the-naval-strike-and-interception-stats
[ACE/death]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#a-ace-death-can-an-ace-die-in-naval-combat
[ACE/save]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#a-ace-save-aces-in-a-save
[ACE/sum]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#summary-what-the-simulator-must-do
[ACE/open]: findings/A-ACE-aces-and-wing-experience-in-naval-strikes.md#unresolved
[REG]: findings/A-REGION-carrier-wings-in-the-region-air-war.md
[REG/order]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-order-the-hourly-strategic-air-update-0xc57d30
[REG/lists]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-lists-which-carrier-wings-are-in-the-region
[REG/war]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-war-pairing-0xc5f4c0--0xf81a20
[REG/combat]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-combat-one-attacker-against-the-defenders-0xf7be70--0xf862a0
[REG/disrupt]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-disrupt-from-disruption-to-the-naval-strike-efficiency-e_i
[REG/losses]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-losses-when-the-planes-go-and-the-naval-combats-count
[REG/accident]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-accident-the-other-hourly-plane-loss
[REG/other]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#a-region-other-other-effects-on-the-naval-battle
[REG/open]: findings/A-REGION-carrier-wings-in-the-region-air-war.md#unresolved
[DET]: findings/A-DETECT-region-air-detection.md
[DET/tot]: findings/A-DETECT-region-air-detection.md#a-detect-tot-0xf77730out-rad-region-tag-tooltip
[DET/rad]: findings/A-DETECT-region-air-detection.md#a-detect-rad-radar-coverage-0xf7a460--0xf861d0
[DET/base]: findings/A-DETECT-region-air-detection.md#a-detect-base-controlled-territory-0xe21800region-out-tag
[DET/night]: findings/A-DETECT-region-air-detection.md#a-detect-night-region-darkness-0xe21640region-out
[DET/use]: findings/A-DETECT-region-air-detection.md#a-detect-use-where-det-enters-fills-in-a-region
[DET/lock]: findings/A-DETECT-region-air-detection.md#a-detect-lock-debug-override
[AST]: findings/A-STATS-plane-stats-and-air-modifiers.md
[AST/enum]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-enum-the-stat-indices
[AST/cache]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-cache-the-wing-stat-cache-0xf661e0
[AST/getters]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-getters-how-the-combat-reads-the-cache
[AST/mission]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-mission-eff-which-mission-efficiency-0xf7ed40
[AST/effectiveness]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-effectiveness-air-keys-in-the-mission-effectiveness-0xf78500
[AST/xp]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-xp-the-experience-holder-and-air_untrained_pilots_penalty_factor-0xf65f20
[AST/accident]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-accident-no-accidents-for-carrier-wings-without-a-mission-type
[AST/mods]: findings/A-STATS-plane-stats-and-air-modifiers.md#a-stats-mods-air-country-modifiers-in-a-naval-battle
[SYNC]: findings/A-REGION-SYNC-carrier-wing-region.md
[SYNC/callers]: findings/A-REGION-SYNC-carrier-wing-region.md#a-region-sync-callers-every-caller-of-setregion-0xf859d0
[SYNC/tick]: findings/A-REGION-SYNC-carrier-wing-region.md#a-region-sync-tick-the-naval-mission-tick-0xd705f0--0xd79900--0xfbcb90--0xfbe7f0--0xfa9e00
[SYNC/move]: findings/A-REGION-SYNC-carrier-wing-region.md#a-region-sync-move-region-change-and-province-change
[SYNC/eff]: findings/A-REGION-SYNC-carrier-wing-region.md#a-region-sync-eff-mission-effectiveness-0xf78500mission-out-iscarrier-nosupply-tooltip
[DET/mix]: findings/A-DETECT-region-air-detection.md#a-detect-mix-air_cv_land_mix-buls-land-bombers-as-defenders-follow-up-log--re
[ESC]: findings/A-REGION-ESCAPE-escaped-carrier-wings.md
[ESC/state]: findings/A-REGION-ESCAPE-escaped-carrier-wings.md#a-region-escape-state-the-escape-0x1973470-member-hourupdate
[ESC/tf]: findings/A-REGION-ESCAPE-escaped-carrier-wings.md#a-region-escape-tf-the-ship-stays-in-its-task-force-and-the-task-force-stays-in-the-combat
[ESC/exec]: findings/A-REGION-ESCAPE-escaped-carrier-wings.md#a-region-escape-exec-nothing-in-the-air-code-reads-the-carriers-combat-state
[COV]: findings/A-COVERAGE-land-mission-effectiveness.md
[COV/range]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-range-wing-range-in-map-pixels-0xf5e5c0
[COV/count]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-count-in-range-provinces-0x10132d0--0x1012f80list-region-base-rangefixed-limit--0
[COV/update]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-update-updatecoverage-0xf87480mission-force
[COV/crowd]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-crowd-capacity_penalty-0xc579f0base--0xc57b70entry-base-f
[COV/supply]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-supply-the-base-entrys-supply-ratio-entry0x30
[COV/fuel]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-fuel-and-region-change-penalty
[ACC]: findings/A-ACCIDENT-land-wing-accidents.md
[ACC/pass]: findings/A-ACCIDENT-land-wing-accidents.md#a-accident-pass-when-and-over-which-wings
[ACC/chance]: findings/A-ACCIDENT-land-wing-accidents.md#a-accident-chance-0xf61d70--0xc4daf0
[ACC/effect]: findings/A-ACCIDENT-land-wing-accidents.md#a-accident-effect-planes-lost-0xf5dd80
[COV/refill]: findings/A-COVERAGE-land-mission-effectiveness.md#a-coverage-refill-the-region-change-penalty-of-refilled-planes
[MIX]: findings/A-MIXED-plane-variants-in-a-wing.md
[MIX/stock]: findings/A-MIXED-plane-variants-in-a-wing.md#a-mixed-stock-which-variants-the-day-tick-gives-a-reinforce
[MIX/order]: findings/A-MIXED-plane-variants-in-a-wing.md#a-mixed-order-how-a-wing-stores-its-variants
[MIX/remove]: findings/A-MIXED-plane-variants-in-a-wing.md#a-mixed-remove-which-planes-are-lost-first-removeplanes-0xf63580
