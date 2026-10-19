# S-ADMIRAL: the admiral in naval combat (efficiency, scaling flags, skill levels, sub-unit blocks, adjusters)

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-ADMIRAL, S-ADMIRAL-SKILL, S-ADMIRAL-TRAIT, S-ADMIRAL-MOD, S-ADMIRAL-ADJUST, S-STATS-COMBAT (speed), S-STATS-COUNTRY, S-POS-MOD, S-POS-SIZE, S-SCREEN, S-RETREAT, S-CRIT-PART
- **Confidence:** read from code only. High for every pseudocode block (each term read off the disassembly). The meaning of the
  modifier definition flag bits (percentage / positive-is-good / neutral) is inferred from the 666 registered definitions
  (high); what the scaling does with each flag value is read directly (high). Medium where marked.

Builds on [S-STATS-COUNTRY](S-STATS-COUNTRY-modifier-sources.md#the-admirals-containers) (the two containers A `leader+0x418`
and B `leader+0x298`, their sources, no country term) and
[S-STATS-COMBAT-GETTER](S-STATS-design-stats.md#s-stats-combat-getter-the-attack-and-armor-getter-0xc34a50-fully-decoded);
that is not repeated here. Method: `tools/re/` (`hoi4re.py`, disassembly with define names, static token ids rebuilt from
0x831c0, the modifier registration 0x55f7f0 scanned for every call to 0x5574d0), checked against the vanilla and TFB
`common/unit_leader` files and defines. Fixed point 1e5, truncating multiply (findings README).

## Summary: one admiral commanding one task force

For an admiral assigned to the fleet long before the battle, whose fleet has one task force:

1. **Efficiency.** `eff = 1 + skill_bonus_factor` of container B (traits' `modifier` + `non_shared_modifier`, e.g. status
   traits `wounded`/`sick` −0.5, `reassigned` −1), clamped at 0. The two size factors are exactly 1 for a navy leader
   (its caps stay 0 and −1) and the task-force cap factor is 1 while the fleet has at most `ADMIRAL_TASKFORCE_CAP`
   (vanilla 10, TFB 5) non-empty task forces. A country `skill_bonus_factor` (e.g. NOR's academy spirit) never reaches it.
   With no `skill_bonus_factor`, `eff` is exactly 1.0 and nothing is scaled.
2. **Scaling when `eff != 1`** (container A and the per-sub-unit blocks): per modifier definition flag. "Positive is good"
   modifiers (flag bit 1: `naval_damage_factor`, `positioning`, `screening_efficiency`, hit chances, …) are multiplied by
   `eff` whatever their sign. "Positive is bad" modifiers (bit 1 clear, bit 2 clear: `navy_visibility`,
   `naval_critical_effect_factor`, `critical_receive_chance`, `naval_torpedo_cooldown_factor`, …) have only their negative
   (beneficial) entries multiplied; positive entries stay. Neutral modifiers (bit 2) are never scaled. Container B is never
   scaled. The flag of every naval combat modifier is in [the table below](#2-the-scaling-0x60fbd0-and-the-modifier-flags).
3. **Reassignment cooldown.** If the admiral was moved from another fleet (or his previous fleet got a new admiral) less than
   `UNIT_LEADER_MODIFIER_COOLDOWN_ON_GROUP_CHANGE` (15) days × (1 + his `reassignment_duration_factor`) ago, `eff = 0` and
   his terrain, night and `units` adjusters are all 0. "Assigned long before" means no cooldown.
4. **Skill levels.** Each of attack, defense, maneuvering, coordination is `base + Σ traits' <skill>_skill`; the lookup uses
   `max(1, level)` and caps at the highest level the skill file defines (vanilla and TFB: 10). Level ≤ 0 reads level 1.
   No define caps it.
5. **Per-sub-unit blocks** (`sub_unit_modifiers = { destroyer = { … units = { … } } }`) live in one structure at
   `leader+0x128`: the modifiers part is added to every ship modifier read of a ship whose sub-unit is the key (fallback
   to the fleet admiral when no admiral is passed), scaled by `eff` like A; the `units` part (attack/defence) is added to
   the ship's attacks only (never to armor). Air sub-unit keys (`cv_cas`, `cv_nav_bomber`) are never read in naval combat.
6. **Terrain and night blocks** inside `modifier` are adjusters (`leader+0x248`, `+0x260`), not modifiers; they are scaled
   by the size factors only (1 here), not by `skill_bonus_factor`.
7. **Combat speed** gets only the admiral's `naval_speed_factor` (A and the sub-unit block, e.g. `cruiser_captain`); no
   terrain or night movement.
8. **Side reads:** own admiral for `positioning` and `screening_efficiency`; no admiral (and no location) for
   `screening_without_screens`; the **attacker's** admiral for the **defender's** `naval_enemy_positioning_in_initial_attack`
   (first `COMBAT_INITIAL_DURATION` hours); the enemy admiral's A for the fleet-size penalty; the enemy admiral's B for
   the retreat roll, as a separate factor after the two retreat-chance factors.
9. **Damaged parts:** `naval_critical_effect_factor` is a full, separate modifier-sum read (admiral included), so an
   admiral's `crisis_magician` (−0.5) halves every damaged part's modifiers.
10. Nothing in naval combat reads the admiral's skill directly, except the choice of the commanding admiral (highest
    overall skill, once per battle). Everything else goes through the containers.

## Function map

| RVA | What |
|---|---|
| 0xc10ab0 | CNavyLeader/CUnitLeader vt+0x88 `(leader, out, useCorpsCap)`: unit-count cap factor; cap = `CORPS_COMMANDER_DIVISIONS_CAP` if the flag, else vt+0x60 (`leader+0xe70`); n = vt+0x70; `cap <= 0` or `n <= cap` → 1, else `max(0, 1 − min(1, (n − cap)/cap))` |
| 0xc10a10 | vt+0x90: the same with cap = vt+0x68 (`leader+0xe74`), n = vt+0x78 |
| 0xc18790 / 0xc186d0 | vt+0x60 / vt+0x68: `leader+0xe70` / `leader+0xe74` (leaf) |
| 0xc16d10 | CNavyLeader vt+0x70: Σ over the leader's fleet's task forces of `0xd6eeb0(tf)+0xc` (ship count, medium) |
| 0x120540 | CNavyLeader vt+0x78: returns 0 |
| 0xc18780 | `ADMIRAL_TASKFORCE_CAP` getter (leaf; ignores its argument) |
| 0xc21d30 | leader caps: `e70 = 0`, `e74 = −1`; corps commander (type 1) / field marshal (type 0) get their divisions/armies caps (+ B `army_leader_max_army_size` 0x15f / `_group_size` 0x160); navy (type 2) keeps 0 / −1 |
| 0xc19fa0 | size efficiency `vt+0x88(false) × vt+0x90 × (navy with a fleet: 0xd53880(fleet))` |
| 0xd53880 | task-force cap factor `(fleet)`: 1 if `ADMIRAL_TASKFORCE_CAP == 0` or the fleet has no admiral (`fleet+0xb0`); else `n = 0xd55ed0(fleet)`, `n <= cap` → 1, else `1 − clamp((n − cap)/cap, 0, 1)` |
| 0xd55ed0 | number of the fleet's task forces (`fleet+0xb8`, count `+0xc4`) with a non-empty ship list and `tf+0x4b8 == 0` |
| 0xc21570 | combine (navy path decoded in full below) |
| 0xc1afc0 | navy leader: if `0xd53880(fleet) != leader+0xfc0` → rebuild (0xc21fc0, vt+0xe0) |
| 0xc1fd60 | start the modifier cooldown `(leader, reason, days, scaleByLeader)` → `leader+0xe84 = reason`, end hour `leader+0xea8` |
| 0xc0fdb0 | cooldown days `days + int(days × B.reassignment_duration_factor (0x181))` |
| 0xc25b30 | leader → fleet assignment `(leader, fleet, startCooldown)`: leaves the old fleet; reason 1 cooldown if it had another fleet |
| 0xc25db0 | leader's fleet set (used when a fleet replaces its admiral): reason 1 cooldown if it had another fleet |
| 0xd5a170 | fleet SetAdmiral: old admiral → 0xc25db0(old, 0); `fleet+0xb0 = new`; new admiral → 0xc25b30(new, fleet, 1) |
| 0x60fbd0 | scale a modifier container `(container, factor, depth)` by definition flags (below); `factor == 1.0` is a no-op |
| 0x5574d0 | modifier registration `(token, id, name, flags → def+0x60, a5 → +0x74, a6 → +0x64, a7 → +0x20, a8 → +0x40, a9 → +0x68)`; `def+0x6c = id`, `def+0x70 = token` |
| 0xc24870 / 0xc256b0 / 0xc26200 / 0xc253b0 | CNavyLeader SetAttack / SetDefense / SetManeuvering / SetCoordination skill `(leader, level, rebuild)` |
| 0xc24740 / 0xc25580 / 0xc26c50 / 0xc260d0 | the army versions (attack / defense / planning / logistics) |
| 0xc1e650 / 0xc1e7c0 | CNavyLeader vt+0x100 / vt+0x108: add / remove a trait's skill adds |
| 0xc271b0 | SetSkillLevel (overall skill) `(leader, level)` → `leader+0xe60` |
| 0xc199f0 | overall skill level `= [leader+0xe60]+0x1b8` (leaf) |
| 0xae2e50 | skill list Get(i): `list[i]` if `1 <= i < count`, else `list[0]` (the null object) |
| 0xae9da0 / 0xae7d60 / 0xae7fb0 / 0xae80b0 / 0xae7d80 | skill lookup `(db, level, type)` in the overall / attack / defense / maneuvering / coordination list |
| 0xae94f0 / 0xae9510 / 0xae9530 / 0xae9500 | entry count (null object included) of the attack / defense / maneuvering / coordination list |
| 0xae50f0 | `common/unit_leader` skill reader: `leader_skills` → db+0xe8, `leader_attack_skills` +0x160, `_defense_` +0x1d8, `_planning_` +0x250, `_logistics_` +0x2c8, `_maneuvering_` +0x340, `_coordination_` +0x3b8 (each × type stride 0x18) |
| 0xaed2c0 | skill block reader: `N = { … }` with integer N ≥ 1, entry `+0x1b8 = N`, `+0x1c0 = type`; `list[type]` grown to N + 1 with the null object, `list[type][N] = entry` |
| 0x1019140 | CSubUnitModifiers constructor (vtable 0x2955af0; at `trait+0x780` and `leader+0x128`) |
| 0x101cc90 | CSubUnitModifiers reader (vt+0x18): `<sub-unit> = { units = { … } <modifier> = … }`; key = the sub-unit definition's `+0x58c`; "Invalid sub unit definition", "already specified" |
| 0x10169f0 | adjuster reader: `attack` → +0x10, `defence` → +0x18, `movement` → +0x20 |
| 0x1016900 | scale an adjuster by f: each of attack/defence/movement `v > 0 → v × f`, `v < 0 → v × (2 − f)` |
| 0x101a0b0 / 0x101a0f0 | clear the adjuster list / the modifier-block list of a CSubUnitModifiers |
| 0x101afc0 / 0x101b5e0 | find by sub-unit key in the adjuster list / the modifier-block list (sorted, binary search; miss → the default at +0x38 / +0x60) |
| 0x101cbf0 / 0x101cc20 | merge another CSubUnitModifiers' adjuster list / modifier-block list |
| 0x1019270 | sub-unit key of a sub-unit object: `[obj+0x58c]` |
| 0xc19ba0 / 0xc19bf0 | admiral's adjuster record / modifier-block holder for a sub-unit (`leader+0x128` via 0x101afc0 / 0x101b5e0) |
| 0xc3a740 | combat speed (decoded below) |
| 0x13e1df0 | enemy side: `side+0xd8 ? combat+0x30 : combat+0x28` |
| 0x15c31b0 | other side `(combat, side)`: `side == combat+0x98 ? combat+0xa0 : combat+0x98` |
| 0x161e5c0 | commanding admiral: the leader of `side+0x118` (count `+0x124`) with the highest overall skill (0xc199f0), first on ties |
| 0x1972900 | a member's attack for a weapon slot (light 0xc3a5d0 / torpedo 0xc3ac00 / heavy 0xc3a580) with the side's commanding admiral |
| 0x19895d0 | fire-exchange group stat accumulation (cooldowns 0x1972b60 with the commanding admiral, attacks × 1/cooldown) |

## Struct fields

| Object | Offset | Meaning |
|---|---|---|
| leader | +0x128 | CSubUnitModifiers: +0x130 / +0x13c adjuster records (0x38 bytes: +0x08 key, +0x10 adjuster), +0x148 / +0x154 modifier-block records (0xd0 bytes: +0x08 key, +0x10 holder, container +0x20), +0x160 default adjuster, +0x188 default holder |
| leader | +0x598 / +0x658 | holder copies of B / scaled A (filled in 0xc21570; readers not traced) |
| leader | +0xd18 | A before scaling (copy made before 0x60fbd0) |
| leader | +0xe70 / +0xe74 | unit cap / group cap (navy: 0 / −1, so both size factors are 1) |
| leader | +0xe84 | modifier cooldown reason (0 none, 1 group change, 4 / 5 army deploy / withdraw); ≠ 0 → `eff = 0`, no adjusters |
| leader | +0xe90 / +0xea0 / +0xea8 | cooldown start hour / timer object / end hour |
| leader | +0xf68 / +0xf78 / +0xf88 / +0xf98 | navy attack / defense / maneuvering / coordination level (≥ 1); the definitions at +0xf70 / +0xf80 / +0xf90 / +0xfa0 |
| leader | +0xfb0 / +0xfb4 / +0xfb8 / +0xfbc | `min(0, level − 1)`, the save's `<skill>_skill_temp_deficit` |
| leader | +0xfc0 | navy: cached size efficiency (before `skill_bonus_factor`) |
| trait | +0x780 | CSubUnitModifiers (`sub_unit_modifiers`): adjusters +0x788, modifier blocks +0x7a0 |
| trait | +0xa20 / +0xa24 / +0xa34 | `attack_skill` / `defense_skill` / `coordination_skill` (maneuvering through 0x1dbe00) |
| skill level entry | +0x08 / +0x10 / +0xd0 / +0x1b8 / +0x1c0 | valid / `modifier` holder / `building_module_modifier` holder (container +0xe0) / level / type (2 navy) |
| skill database | +0xe8 / +0x160 / +0x1d8 / +0x250 / +0x2c8 / +0x340 / +0x3b8 | overall / attack / defense / planning / logistics / maneuvering / coordination lists, each × type 0x18 (base, data, count +0xc) |
| modifier definition | +0x60 | flags: bit 0 percentage, bit 1 positive is good, bit 2 neutral, bit 5 boolean (meanings inferred) |
| fleet | +0xb0 / +0xb8, +0xc4 | admiral handle / task forces, count |
| task force | +0x4b8 | if ≠ 0 the task force is not counted by 0xd55ed0 (meaning not decoded) |
| side | +0x118 / +0x124 | admirals of the side's task forces / count (0x161e5c0) |
| side | +0x138 | saved copy of the commanding admiral (written with +0x130, read by the save writer 0x1622520) |

## 1. Leader efficiency

```
// 0xc21570, navy path (leader+0xe7c == 2); A = +0x358 (holder +0x348), B = +0x298
A = S_A + S_A2 + medals; B = S_B + S_B2                        // as in S-STATS-COUNTRY
0xc21d30(leader)                                               // 0xc2175f: navy keeps e70 = 0, e74 = -1
if leader+0xe84 == 0:                                          // 0xc2176b
    size = 0xc19fa0(leader)                                    // 0xc21778
    leader+0xfc0 = size                                        // 0xc21789 (navy only)
    eff = size + B.Get(0x168 skill_bonus_factor)               // 0xc2179e
    if eff < 0: eff = 0                                        // 0xc217b1
else:
    eff = 0                                                    // 0xc217b3
+0xd18 = copy(A)                                               // unscaled, 0xc217dc..
0x60fbd0(A, eff, depth 2)                                      // 0xc21815
for rec in leader+0x148 (0xd0 records): 0x60fbd0(rec+0x20, eff, 2)   // 0xc2181a..0xc21862: sub-unit modifier blocks
+0x658 = A; +0x598 = B
leader+0x418 = copy(A); 0x60fbd0(+0x418, 1.0, …)               // 0xc21b1b..0xc21b57: the second factor is 1.0 for a navy
                                                               // leader (army: LEADER_MOD_COMMS_SCALING / _NO_HQ_SCALING)

// 0xc19fa0
size = trunc(vt+0x88(leader, false) × vt+0x90(leader))         // fixed point
if leader+0xe7c == 2 and fleet = leader+0xf60: size = trunc(size × 0xd53880(fleet))
```

`vt+0x88` = 0xc10ab0 and `vt+0x90` = 0xc10a10 (shared by all leader classes) return 1 whenever their cap is ≤ 0. A navy
leader's caps are 0 and −1 (0xc21d30 resets them in every combine, 0xc2175f, before 0xc19fa0; nothing else writes them
for a navy leader), so both are exactly 1.0 for any fleet size. Only the task-force cap factor can lower a navy leader's size efficiency: with `n` non-empty task forces
in the fleet (0xd55ed0) and `cap = ADMIRAL_TASKFORCE_CAP` (vanilla 10, TFB 5), `1 − clamp((n − cap)/cap, 0, 1)` when
`n > cap`. **For one task force `size` is exactly 1.0.** A change in the factor triggers a rebuild (0xc1afc0 compares
it with `leader+0xfc0`).

`skill_bonus_factor` (0x168) is read from B only, and B has no country term (S-STATS-COUNTRY): traits' `modifier` and
`non_shared_modifier` (vanilla and TFB `sick`/`wounded` −0.5, `reassigned` −1; vanilla SIA −0.33, SWE −0.75 traits) and
the leader-bound timed entries. Country values (vanilla `NOR_flexible_officers_academy_spirit` +0.1, GOE country leader
trait +0.3) never reach it. It is the only read of 0x168 in the executable (imm scan).

**Reassignment cooldown** (`leader+0xe84`). 0xc1fd60 sets it:

```
// 0xc1fd60(leader, reason, days, scaleByLeader)
if scaleByLeader: days = 0xc0fdb0(leader, days) = days + int(days × B.Get(0x181 reassignment_duration_factor))
end = now + days × 24                                          // hours
if end > leader+0xea8:
    if days <= 0: leader+0xe84 = 0
    else: leader+0xe90 = now; leader+0xea8 = end; leader+0xe84 = reason
```

Callers for a navy leader: 0xc25b30 (assigned to a fleet while it had another one; reason 1,
`UNIT_LEADER_MODIFIER_COOLDOWN_ON_GROUP_CHANGE`, 0xc25c6b) and 0xc25db0 (its fleet is changed to another or to none,
e.g. when a fleet gets a new admiral, 0xd5a170 → old admiral; reason 1, same define, 0xc25ded). An unassigned admiral
assigned to a fleet gets no new cooldown, but one still running from his removal elsewhere keeps running. Reasons 4/5
(0xc1ea90, 0xc20060) are the army deploy/withdraw cooldowns. While `e84 != 0`: `eff = 0` (A and the sub-unit blocks
scaled by 0 per flag, see 2), and 0xc21280 returns before summing any adjuster (terrain, night, `units` all 0).
`reassignment_duration_factor` is also read from B only, so country values (SOV +0.25, NOR −0.3) do not change it.

**Is `eff` exactly 1?** Yes, when the admiral has no `skill_bonus_factor` (no status trait), is not in a cooldown and the
fleet has at most `ADMIRAL_TASKFORCE_CAP` non-empty task forces: `size = 0x186a0` exactly and `B(0x168) = 0`, and
0x60fbd0 returns at once for a factor of exactly 1.0, so not even rounding happens.

## 2. The scaling 0x60fbd0 and the modifier flags

```
// 0x60fbd0(container, factor, depth)
if factor != 1.0:
    for (id, v) in container.entries (16 bytes each):
        f = defs[id]+0x60                                      // global 0x332ed90, 0x78-byte definitions
        if f & 2:            if v != 0: v = trunc(v × factor)  // 0x60fc42
        elif f & 4:          keep v                            // 0x60fc30
        elif v < 0:          v = trunc(v × factor)             // 0x60fc32..0x60fc3b
        else:                keep v
if depth > 1: for child in container+0x18 (count +0x24): 0x60fbd0(child+0x10, factor, depth − 1)
```

The flags are the fourth argument (`r9d`) of the registration call 0x5574d0 (stored at `def+0x60`, 0x557624), set in
0x55f7f0 as an immediate for every modifier; each id is registered four times with the same flags (checked for all 666
ids). Across all definitions: 3 (350 ids), 1 (160), 2 (79), 5 (26), 0 (8), 34, 130, 32, 4, 128, 11, 13, 17, 77, 261, 8.
The values fit bit 0 = shown as a percentage, bit 1 = positive is good, bit 2 = neutral (no colour), bit 5 = boolean:
`research_speed_factor` 3, `political_power_gain` 2, `puppet_cost_factor` 1, `political_power_cost` 0, the AI focus
factors 5, `disable_idea_taking` 32. So the rule reads: **a "positive is good" modifier is scaled whatever its sign, a
neutral one never, and a "positive is bad" one only where it is negative (a benefit).**

Naval combat modifiers (flags; S = every entry × eff, N = negative entries only, — = never; B-only = read from container B,
which is not scaled):

| Id | Script key | Flags | Scaling |
|---|---|---|---|
| 0x1b7 / 0x1b8 | `naval_damage_factor` / `naval_defense_factor` | 3 / 3 | S |
| 0x1b2 | `positioning` | 3 | S |
| 0x1b1 / 0x1b3 | `screening_efficiency` / `screening_without_screens` | 3 / 3 | S (0x1b3 is not read from an admiral, see 7) |
| 0x28 | `naval_hit_chance` | 3 | S |
| 0x1bf / 0x1c0 / 0x1c1 | `naval_torpedo_hit_chance_factor` / `naval_light_gun_…` / `naval_heavy_gun_hit_chance_factor` | 3 | S |
| 0x46 / 0x47 | `navy_org` / `navy_org_factor` | 2 / 3 | S |
| 0x7e / 0x7f | `naval_morale` / `naval_morale_factor` | 2 / 3 | S |
| 0x29 | `naval_speed_factor` | 3 | S |
| 0x5b / 0x59 | `naval_retreat_speed` / `_after_initial_combat` | 3 / 3 | S |
| 0x5c / 0x5d | `sub_retreat_speed` / `convoy_retreat_speed` | 3 / 3 | S |
| 0x57 / 0x58 | `naval_retreat_chance` / `_after_initial_combat` | 3 / 3 | S |
| 0x1b9 | `naval_enemy_retreat_chance` | 1 | B-only (would be N) |
| 0x5a | `naval_enemy_positioning_in_initial_attack` | 3 | S |
| 0x1d0 | `naval_enemy_fleet_size_ratio_penalty_factor` | 3 | S |
| 0x1d1 / 0x1d2 | `naval_has_potf_in_combat_attack` / `_defense` | 3 / 3 | S |
| 0xc2 / 0xc3 | `navy_submarine_attack_factor` / `_defence_factor` | 3 / 3 | S |
| 0xc4 / 0xc5 | `navy_capital_ship_attack_factor` / `_defence_factor` | 3 / 3 | S |
| 0xc6 / 0xc7 | `navy_screen_attack_factor` / `_defence_factor` | 3 / 3 | S |
| 0x38 | `submarine_attack` | 2 | S |
| 0x107 | `naval_night_attack` | 3 | S |
| 0x88 / 0x89 | `navy_anti_air_attack` / `navy_anti_air_attack_factor` | 2 / 3 | S |
| 0x19f | `navy_visibility` | 1 | N |
| 0x2a / 0xc8 | `naval_detection` / `navy_submarine_detection_factor` | 3 / 3 | S |
| 0x1ba | `naval_critical_score_chance_factor` | 3 | S |
| 0x1bb | `naval_critical_effect_factor` | 1 | N |
| 0x18c | `critical_receive_chance` | 1 | N |
| 0x1bc | `naval_torpedo_reveal_chance_factor` | 1 | N |
| 0x1bd | `naval_torpedo_screen_penetration_factor` | 3 | S |
| 0x1be | `naval_torpedo_cooldown_factor` | 1 | N |
| 0x1c2 | `naval_torpedo_enemy_critical_chance_factor` | 1 | N |
| 0x1c3 | `naval_torpedo_damage_reduction_factor` | 3 | S |
| 0x24 / 0x1b6 | `sortie_efficiency` / carrier fighter sortie efficiency | 3 / 3 | S |
| 0x22 / 0x23 | carrier traffic / night traffic | 3 / 3 | S |
| 0x10c | carrier capacity penalty reduction | 1 | N |
| 0x1cf | carrier sortie hours delay | 2 | S |
| 0xc9 / 0xca / 0xcb | `navy_carrier_air_attack/targetting/agility_factor` | 3 | S (read from the country only, see 4) |
| 0x34 / 0x35 | `experience_gain_navy_unit_factor` / `experience_gain_navy_unit` | 3 / 2 | S |
| 0x1a0 / 0x1a1 | `navy_casualty_on_hit` / `_on_sink` | 1 / 1 | N |
| 0x19 | `naval_coordination` | 3 | S (no combat reader) |
| 0x168 | `skill_bonus_factor` | 3 | B-only |
| 0x181 | `reassignment_duration_factor` | 1 | B-only |

The multiply is the usual truncating fixed-point one. With `depth 2` the container's child containers (`+0x18`) are
scaled too; for the naval keys above the children were not seen used.

## 3. Skill levels

```
// 0xc24870 SetAttackSkill(leader, x, rebuild); 0xc256b0 / 0xc26200 / 0xc253b0 are the same for defense / maneuvering /
// coordination (levels +0xf78 / +0xf88 / +0xf98, deficits +0xfb4 / +0xfb8 / +0xfbc, definitions +0xf80 / +0xf90 / +0xfa0)
leader+0xfb0 = min(0, x − 1)                                   // 0xc248a0, "attack_skill_temp_deficit"
leader+0xf68 = max(1, x)                                       // 0xc248aa
n   = db.attack[leader+0xe7c].count                            // 0xae94f0, includes the null object at [0]
def = db.attack[type].Get(min(leader+0xf68, n − 1))            // 0xae7d60 → 0xae2e50
if def.valid: leader+0xf70 = def                               // else assert "pAttackSkill && pAttackSkill->IsValid()", keep the old one
if rebuild: 0xc21fc0; vt+0xe0

// CNavyLeader vt+0x100 = 0xc1e650 (trait added); vt+0x108 = 0xc1e7c0 subtracts
if trait.attack_skill (+0xa20):       SetAttackSkill(leader, f68 + fb0 + trait.attack_skill, 0)
if trait.defense_skill (+0xa24):      SetDefenseSkill(leader, f78 + fb4 + trait.defense_skill, 0)
if trait.maneuvering_skill (0x1dbe00): SetManeuveringSkill(leader, f88 + fb8 + …, 0)
if trait.coordination_skill (+0xa34): SetCoordinationSkill(leader, f98 + fbc + …, 0)
```

`level + deficit` always equals the unclamped sum, so the level is **the base skill plus every trait's
`<skill>_skill`**, and the definition used is `clamp(that, 1, highest level in the file)`. The skill files are read by
0xae50f0 → 0xaed2c0: each `N = { … }` entry with `type = navy` is stored at `list[navy][N]`; the list starts with the null
object at index 0 and gaps are filled with it. So:

- **Above the highest level** (vanilla and TFB define navy levels 1–10 in every skill file: TFB overrides attack and
  defense, maneuvering and coordination are vanilla's): the highest level's definition.
- **Level ≤ 0** (traits taking the sum below 1): level 1's definition; the shortfall sits in the deficit and is undone
  when the trait is removed.
- A gap in the file (a missing level): the definition stays whatever it was (assert).
- No define is read in these functions (no cap define).

The overall skill (`leader+0xe60`, SetSkillLevel 0xc271b0) is looked up without the clamp: a level outside 1…count−1
returns the null object and the definition is not changed. Its navy entries are empty in vanilla and TFB (commented out),
so it acts only through the choice of the commanding admiral (0x161e5c0: highest `[+0xe60]+0x1b8`, strict, first wins).
`attack_skill_factor` and the like are not read here.

## 4. Per-sub-unit blocks

Both parts of `sub_unit_modifiers` live in one CSubUnitModifiers object (vtable 0x2955af0): `trait+0x780` for the trait,
`leader+0x128` for the admiral's sum. Its reader 0x101cc90 takes `<sub-unit> = { … }` blocks; the sub-unit is looked up
by name in the sub-unit database (0xac4e50; "Invalid sub unit definition"), and its key is the definition's `+0x58c`;
inside, `units = { attack defence movement }` (token 0x2faa) becomes the **adjuster** record, every other key a
**modifier** in the block's holder (0x6104e0).

```
// rebuild
0xc21fc0: clear leader+0x148 list (0x101a0f0); for trait: 0x101cc20(leader+0x128, trait+0x7a0)   // modifier blocks
0xc21570: for rec in leader+0x148: 0x60fbd0(rec+0x20, eff, 2)                                     // scaled like A (section 2)
0xc21280 (vt+0xe0): clear leader+0x130 list (0x101a0b0); if leader+0xe84 == 0:
    0xc0fb70: for trait: 0x101cbf0(leader+0x128, trait+0x788)                                     // adjusters
    size = 0xc19fa0(leader); if size != 1.0: for rec in leader+0x130: 0x1016900(rec+0x10, size)

// reads
0xc38760(ship, id, admiral): admiral = admiral or 0xd64510(tf)                     // 0xc38ab0..0xc38abe
    key = [ship+0x80+0x58c]                                                         // 0x1019270, the ship's sub-unit
    Σ += 0xc19bf0(admiral, key).Get(id)                                             // 0xc38afe..0xc38b1c
0xc34a50(…, admiral): if admiral != 0:                                              // 0xc3502c
    Σ += 0xc19ba0(admiral, key).attack | .defence                                   // 0xc35048
```

- **Which ship:** the key is the ship's sub-unit definition (the ship type's sub-unit: `destroyer`, `light_cruiser`,
  `heavy_cruiser`, `battleship`, `submarine`, TFB `battle_cruiser`, `sh_battleship`, …). The modifier part applies to
  every ship modifier read of such a ship (attack, armor, hit chance, crit, retreat, speed, AA, visibility, cooldown,
  sortie efficiency, …), the `units` part only to its attacks.
- **Scaling:** the modifier part by the full `eff` and the flags (section 2); the `units` part by the size efficiency only
  (`v > 0 → v × size`, `v < 0 → v × (2 − size)`), not by `skill_bonus_factor`, and it is empty during a cooldown.
- **Admiral 0:** the modifier part falls back to the fleet admiral (the same holder lookup with `r13` replaced by
  0xd64510(tf)); the `units` part has no fallback, so the armor read (admiral 0, 0x1970c12) never gets `units.defence`.
- **Air sub-units** (`cv_cas`, `cv_nav_bomber`, `cv_fighter`): no ship has that key, 0xc19bf0 / 0xc19ba0 are called only by
  0xc38760 / 0xc34a50 and their lookups 0x101b5e0 / 0x101afc0 only by them, so these blocks are never read in naval combat.
  The plane getters (0xf60f10, 0xf61280, 0xf60bc0) read the wing's stat, its ace (0x61a930), the country `+0x5b8`, the
  wing's region holder (0xf7ed40), weather, darkness and the province holder, no leader; the carrier air factors
  0xc9–0xcb are read from the country container only (0x1978e30, 0x1979478..0x19794f9). An admiral's
  `navy_carrier_air_*_factor` therefore has no effect wherever it is written; `sortie_efficiency` in his `modifier` (or in
  a `carrier = { }` block) does, through the carrier's member getter.

## 5. Terrain and night adjusters

Confirmed: in the trait parser 0xaebc20, a key inside `modifier = { }` for which 0x1016a40 creates an adjuster (a naval
terrain name or `night`) goes to the trait's adjuster list `+0x768` (0xaeca3f..0xaecb28; assert "Type == _modifier_");
every other key is read into the `modifier` container `+0x360` (0xaecc16). The adjusters never enter A or B.

```
// 0xc21280 (vt+0xe0)
clear night +0x270/+0x278/+0x280, every terrain record +0x10/+0x18/+0x20, the units adjusters
if leader+0xe84 != 0: return                                    // cooldown: all adjusters stay 0
0xc0fb70(traits, +0x128, +0x248, +0x260, 1.0)                    // night 0xae94a0, terrain 0xaea5f0, units (section 4)
size = 0xc19fa0(leader)
if size != 1.0: 0x1016900(each terrain record, size); 0x1016900(night, size); 0x1016900(each units record, size)
```

So they are scaled by the size efficiency (1 for one task force) with the positive/negative rule of 0x1016900, **not**
by `eff` and not by `skill_bonus_factor`. Their only combat reader is the attack/armor getter 0xc34a50 (attack +0x10,
defence +0x18; the night ones × darkness), with a non-zero admiral only, i.e. for attacks. `movement` (+0x20, +0x280) has
no naval combat reader (see 6).

## 6. Combat speed

```
// 0xc3a740(ship, out, admiral)
v = Mod(0x29 naval_speed_factor; ship, admiral)                 // 0xc38760: country, A of the admiral (or the fleet admiral), his sub-unit block, …
if province:
    if ship+0x88 >= 0: v += navalTerrain(region).subUnit[ship+0x88].movement   // 0x143e4a0 +0x20, 00_terrain.txt
    else:              v += 0xd6eee0(tf, terrain)                               // task-force fallback
fuel = 0xd6c6f0(tf); if fuel < 1: v += (1 − fuel) × NNavy.OUT_OF_FUEL_SPEED_FACTOR
out = ship.stat[naval_speed] (ship+0x228) × max(0.1, 1 + v)
```

The only admiral term is his `naval_speed_factor` through the modifier sum: container A (traits, skill levels) and the
per-sub-unit block (vanilla `cruiser_captain` +0.1, TFB +0.05, for light and heavy cruisers; TFB `destroyer_leader` +0.05).
No terrain or night `movement` adjuster is read. Callers: the member update 0x1973521 passes the side's commanding
admiral (0x197355f via 0xc39140); the escape speed 0x196fe80 (0x1970411) and 0xc37006 pass 0 (the fleet admiral).

## 7. Positioning, screening and the retreat roll

`Mod(id; country, location, admiral)` = 0x15c70b0 skips a null location and a null admiral.

| Read | Country | Location | Admiral |
|---|---|---|---|
| `positioning` 0x1b2 (0x161bb4c) | own (`side+0x38`) | battle province | own commanding admiral `side+0x130` (0x161bb0b) |
| `naval_enemy_positioning_in_initial_attack` 0x5a (0x161bbb5) | own | battle province | **enemy** commanding admiral (`0x13e1df0(side)+0x130`, read in the prologue 0x161b9b1) |
| fleet-size penalty × (1 + `naval_enemy_fleet_size_ratio_penalty_factor`) (0x161bfdc) | — | — | enemy commanding admiral, A only |
| `screening_efficiency` 0x1b1 (0x161ce0f) | own | battle province | own commanding admiral (0x161cdb7) |
| `screening_without_screens` 0x1b3 (0x161d2d6) | own | **none** | **none** |

The 0x5a term is added only while `hours < COMBAT_INITIAL_DURATION` and `side+0xd8 == 0`. 0x13e1df0 returns `combat+0x30`
when `side+0xd8 != 0` and `combat+0x28` otherwise, so `+0xd8` is the attacker flag and the term applies to the
**defender's** positioning, with the attacker's admiral (and the defender's own country and location values).

```
// 0x1975280 retreat roll (member, isShip)
enemyAdmiral = 0x15c31b0(combat, side)+0x130
p = NNavy.COMBAT_RETREAT_DECISION_CHANCE
if isShip:
    p = trunc(p × (1 + M(0x57 naval_retreat_chance)))                               // M = 0x1972570, own commanding admiral
    if combat.hours > NNavy.COMBAT_INITIAL_DURATION:
        p = trunc(p × (1 + M(0x58 naval_retreat_chance_after_initial_combat)))
if enemyAdmiral: p = trunc(p × (1 + enemyAdmiral.B.Get(0x1b9 naval_enemy_retreat_chance)))   // 0x1975378
p = clamp(p, 0, 1)
retreat = U <= p                                                                    // U from 0x2233db0
```

The enemy admiral's factor is a third multiplicative factor after the two retreat-chance factors; a convoy gets only that
factor. It is read from B (unscaled, traits only: vanilla and TFB `gentlemanly` +0.2).

## 8. Other reads of the commanding admiral

Every `[side+0x130]` read in the naval combat code (0x15c0000–0x1630000, 0x196f000–0x1990000, by displacement scan,
each checked):

| Site | Use |
|---|---|
| 0x1620940 (HourPrep) | if `side+0x130` is null: `side+0x130 = side+0x138 = vt+0xe8(side)` (0x161e5c0, highest overall skill among `side+0x118`); chosen once, kept for the battle |
| 0x161b960 (0x161b9b1, 0x161bb0b) | positioning, 0x5a, fleet-size penalty (section 7) |
| 0x161ccb0 (0x161cdb7) | `screening_efficiency` (section 7) |
| 0x1970a90 (0x1970b12) | shot profile: the shooter's attacks 0xc34a50 with the commanding admiral; target armor with 0 |
| 0x1972570 (0x19725d7, 0x19725f6) | member getter: 0xc38760 with the commanding admiral (every member modifier read) |
| 0x1972900 (0x197292d), 0x1972a70 (0x1972a9d) | weapon-slot attack value and weapon cooldown (`naval_torpedo_cooldown_factor`) through 0xc38760 |
| 0x1973470 chunk 0x1973521 (0x1973545) | member update: combat speed 0xc39140 and sub attack 0xc3a890 with the commanding admiral (their use in the update not traced) |
| 0x1975280 (0x19752d1) | retreat roll, the enemy's admiral B (section 7) |
| 0x197a700 (0x197a869), 0x197c8b0 (0x197c901) | air targeting and preemptive AA: ship AA 0xc3a470 with the commanding admiral |
| 0x197d770 chunk 0x197d7ad (0x197d834) | carrier sortie hours delay: Mod(0x1cf) of the carrier with the commanding admiral |
| 0x16223af (0x1622405, 0x1622434) | fire-exchange group stats 0x19895d0 (cooldowns with the commanding admiral) |

The other `+0x130` hits are different objects (member escape progress, save code, UI). A scan of the same ranges for the
leader fields `+0xe58`, `+0xe5c`, `+0xe60`, `+0xf68`, `+0xf78`, `+0xf98`, `+0xfc0`, `+0xe84`, `+0xdc8` found no combat
read (the `+0xf88` hits are the country's targeted-modifier table). So, besides the containers and adjusters, the only
direct skill read is the choice of the commanding admiral. `naval_coordination` still has no reader. Medals (`unit_medals`)
act only through A. The leader experience step (side vt+0x70) does not feed back into the battle's numbers except
through a level-up rebuild (not traced).

## 9. The damaged-part effect factor

```
// 0xc38760, damaged-parts term (0xc38b21..0xc38bef)
if ship+0x924 != 0:
    k = 1.0
    if id != 0x1bb: k = 1 + 0xc38760(ship, 0x1bb, admiral', 0, 0)        // a full, separate sum (0xc38b58)
    for (part, count) in ship+0x918: v += trunc(trunc(part+0x98 .Get(id) × count) × k)
```

`admiral'` is the resolved admiral: the argument, or the fleet admiral that replaced it at 0xc38abe. The recursive call is
the whole ship modifier sum for `naval_critical_effect_factor`: country, PotF static, experience, `ship+0x8f8` object,
task force, naval terrain × sub-unit, weather, province, `tf+0x658`, the admiral's A, his sub-unit block, the damaged
parts' own 0x1bb values (with `k = 1`) and the building-module term. So **an admiral's `naval_critical_effect_factor`
does scale the damaged-part modifiers**: vanilla and TFB `crisis_magician` (−0.5, flag 1, a negative entry so × `eff`)
gives `k = 0.5` with `eff = 1`, plus whatever the country has.

## Defines and constants involved

| Name | Role |
|---|---|
| `NNavy.ADMIRAL_TASKFORCE_CAP` | task-force cap factor (0xd5388f, 0xc18780); vanilla 10, TFB 5; 0 disables |
| `NMilitary.UNIT_LEADER_MODIFIER_COOLDOWN_ON_GROUP_CHANGE` | cooldown days on a fleet change (0xc25c6b, 0xc25ded); vanilla 15, TFB does not change it |
| `NMilitary.CORPS_COMMANDER_DIVISIONS_CAP`, `CORPS_COMMANDER_ARMIES_CAP`, `FIELD_MARSHAL_DIVISIONS_CAP`, `FIELD_MARSHAL_ARMIES_CAP` | army caps (0xc21d30, 0xc10aca); never used for a navy leader |
| `NMilitary.LEADER_MOD_COMMS_SCALING`, `LEADER_MOD_NO_HQ_SCALING`, `FIELD_MARSHAL_ARMY_BONUS_RATIO` | army-only factors in 0xc21570 / 0xc21280 |
| `NNavy.COMBAT_RETREAT_DECISION_CHANCE`, `COMBAT_INITIAL_DURATION` | retreat roll (0x1975280) |
| `NNavy.OUT_OF_FUEL_SPEED_FACTOR` | combat speed (0xc3a804) |
| hard-coded `2.0` (0x30d40) | the negative-adjuster rule `v × (2 − f)` of 0x1016900 |

## How it was verified

Static reading only. The efficiency through CNavyLeader's vtable (slots +0x60/+0x68/+0x70/+0x78/+0x88/+0x90 dumped from
0x2955dc0) and every writer of `leader+0xe70` / `+0xe84` (displacement scan). The flags by scanning every 0x5574d0 call in
0x55f7f0 (2668 calls, register tracking of `edx`, `r9d` and the name string), joined with the existing id → script key
table. The skill list layout from the skill reader 0xaed2c0 and the list Get 0xae2e50, the token → list mapping from
0xae50f0, and the vanilla/TFB skill files (10 navy levels each). The sub-unit structure from its constructor, reader,
lookups and merges, and an xref check that its lookups have no other callers. The combat reads by displacement scans of
the naval combat ranges. In-game checks that would confirm the main points: an admiral with `wounded` should give half his
`naval_damage_factor` (and half a negative `navy_visibility`) but his full positive `navy_visibility`; an admiral moved
between two fleets should show no trait or skill bonus for 15 days; `crisis_magician` should halve a damaged part's
`naval_speed_factor` penalty in a logged battle.

## Differences from the current spec

- **S-ADMIRAL-MOD:** "positive entries × efficiency, negative entries kept [ASSUMED]" is wrong. Per flag: positive-is-good
  keys (`naval_damage_factor`, `naval_defense_factor`, `positioning`, `screening_efficiency`, hit chances, org, speed,
  retreat, attack/defence factors, …) scale every entry; positive-is-bad keys (`navy_visibility`,
  `naval_critical_effect_factor`, `critical_receive_chance`, `naval_torpedo_cooldown_factor`,
  `naval_torpedo_reveal_chance_factor`, `naval_torpedo_enemy_critical_chance_factor`, casualty keys) scale only negative
  entries; neutral keys none. The sub-unit modifier blocks are scaled the same way. Add the reassignment cooldown
  (`eff = 0`, no adjusters) if the spec ever models fleet changes; for "assigned long before" it is off.
- **S-ADMIRAL-SKILL:** "at least 0; level 0 reads nothing" is wrong: the lookup level is at least 1 (a sum ≤ 0 reads
  level 1). "Above the highest reads the highest" is confirmed [RE] (drop [ASSUMED]).
- **S-ADMIRAL-MOD / S-SCREEN:** `screening_without_screens` is read from the country only (no admiral, no location); the
  admiral's value has no effect.
- **S-ADMIRAL-ADJUST:** the `units`, terrain and night adjusters are scaled by the size efficiency only (1 for one task
  force), never by `skill_bonus_factor`; `wounded` does not halve them.
- **S-POS (Not modelled list):** `naval_enemy_positioning_in_initial_attack` is now fully read: on the defender only,
  first `COMBAT_INITIAL_DURATION` hours, own country + location + the attacker's commanding admiral's A. No vanilla or TFB
  script uses the key.
- **S-STATS-COMBAT speed:** the "leader term" is only `naval_speed_factor` through the modifier sum (A + sub-unit block);
  the trait terrain `movement` is not read in combat.
- **S-CRIT-PART:** `k = 1 + naval_critical_effect_factor` is the full ship modifier sum, admiral included.
- **S-STATS-COUNTRY:** a country `skill_bonus_factor` or `reassignment_duration_factor` has no effect on the admiral
  (read from B only).
- **Air:** an admiral's `navy_carrier_air_attack/targetting/agility_factor` has no effect (country-only read in
  0x1978e30), in `modifier` or in an air sub-unit block.
- **S-STATS-COUNTRY finding:** its description of 0x60fbd0 ("bit 1 = scale, bit 2 = keep negatives") and its open item on
  the vt+0x88/+0x90/cap functions are superseded by sections 1–2.

## Save format (S-ADMIRAL-SAVE)

Decoded on 2026-10-20 for `tools/ingame/make_save.py` (writing a scenario's admiral into a battle save).

```
character_manager={ historical={ character={
    id={ id=C type=73 } token="..." name="..." country="TAG" ...
    navy_leader={                                  # writer 0xc283f0 (after the common leader writer 0xc1ce70)
        id={ id=L type=4713 }                      # the navy leader handle
        name="..." female=no skill=S script_id=…   # 0xc1ce70: name, desc, …, female, skill, experience, script_id, link,
        traits={ t1 t2 ... }                       #   max_traits, dynamic_modifier, cooldown_reason, leader_modifier_enable_date,
        in_progress={ ... }                        #   leader_cooldown_start_date, traits, in_progress, traits_to_remove,
        legacy_id=0                                #   trait_xp_factor, government_in_exile_tag, sub_unit_modifiers, legacy_id
        attack_skill=… defense_skill=… maneuvering_skill=… coordination_skill=…
        <skill>_skill_temp_deficit=…               # 0xc283f0, then naval_headquarter={ … }, penalty
        penalty=1
    }
} } }
countries={ TAG={ units={ fleet={
    id={ id=F type=61 }
    leader={ id=L type=4713 }                      # fleet writer 0xd5ebc0: right after the fleet's id, only when set (fleet +0xb0)
    name="..." ...
} } } }
```

The fleet reader 0xd582b0 (`leader`, token 0x28b7) reads the handle into `fleet+0xb0` as written; the leader's own fleet
back pointer (`+0xf60`) is not saved. No assignment date is saved either: `leader_modifier_enable_date` only exists while a
cooldown runs, so a leader written as a fleet's admiral loads with full efficiency (section 1).

## Unresolved

- The meaning of `tf+0x4b8` (task forces excluded from the cap count) and the exact rounding of 0x24ed730 in the cooldown
  days.
- What the member update 0x1973521 does with the commanding-admiral speed and sub attack it computes (0x197355f,
  0x1973599).
- The readers of the copies `leader+0x598`, `+0x658`, `+0xd18` and of `leader+0xfc0` outside the rebuild check (UI?).
- What the child containers of a modifier container (`+0x18`) hold, scaled with `depth 2`.
- When the cooldown ends (vt+0xc8 0xc12470 clears `+0xe84`; its caller and timing not traced) and whether the game also
  gives the `reassigned` status trait on a fleet change.
- Whether a skill level-up of the commanding admiral during a battle rebuilds his containers mid-battle (likely, through
  the trait/skill setters; not traced).
