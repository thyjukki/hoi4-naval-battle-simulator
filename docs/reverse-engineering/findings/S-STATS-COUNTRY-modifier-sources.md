# S-STATS-COUNTRY: where combat modifiers are read from (country, admiral, location)

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-STATS-COUNTRY, S-STATS-COMBAT, S-POS, S-POS-SIZE, S-SCREEN, S-RETREAT, S-SUB, S-HIT-MODS, A-AA-REDUCE
- **Confidence:** read from code only. Modifier names: high (registration table + static token table). Sources of each sum: high. Leader-efficiency scaling (0x60fbd0): medium.

Question this answers: which combat modifiers a country source (national spirit, doctrine, technology top-level key) can
change, and which are read only from an admiral. The attack/armor getter itself is in
[S-STATS-COMBAT-GETTER](S-STATS-design-stats.md#s-stats-combat-getter-the-attack-and-armor-getter-0xc34a50-fully-decoded).

## Modifier names

`0x55f7f0` registers each modifier four times (plain, `MOD_DAYS`, `MOD_HOUR`, `MOD_DAILY`) with
`0x5574d0(token, id, &"MODIFIER_*", …)`. The first argument is a static token read from `.rdata`; the static token
registration `0x831c0` binds every token value to its script keyword (`0x14bd50(value)` then `0x24c4b70(…, keyword)`).
Joining the two gives each id its **script key** and its loc key (2668 registrations, 10765 tokens). The ids used by naval
combat:

| Id | Script key | Loc key (if different) | Combat reader |
|---|---|---|---|
| 0x19 | `naval_coordination` | | none found in combat code |
| 0x24 | `sortie_efficiency` | `CARRIER_SORTIE_EFFICIENCY_FACTOR` | 0x1976db0 via 0x1972570 |
| 0x2a | `naval_detection` | | 0xc3a950, 0xc3aae0 |
| 0x34 / 0x35 | `experience_gain_navy_unit_factor` / `experience_gain_navy_unit` | `MODIFIER_XP_GAIN_NAVY_UNIT(_FACTOR)` | 0xc3b240 |
| 0x38 | `submarine_attack` | | 0xc34b66 (flat, on the base stat) |
| 0x57 / 0x58 | `naval_retreat_chance` / `naval_retreat_chance_after_initial_combat` | | 0x1975280 via 0x1972570 |
| 0x59 / 0x5b | `naval_retreat_speed_after_initial_combat` / `naval_retreat_speed` | | 0x196fe80 via 0x1972570 |
| 0x5a | `naval_enemy_positioning_in_initial_attack` | | 0x161bbac via 0x15c70b0 |
| 0x5c / 0x5d | `sub_retreat_speed` / `convoy_retreat_speed` | | 0x196fe80 |
| 0x88 / 0x89 | `navy_anti_air_attack` / `navy_anti_air_attack_factor` | | 0xc3a470 only |
| 0xc2 / 0xc3 | `navy_submarine_attack_factor` / `navy_submarine_defence_factor` | | 0xc34a50 |
| 0xc4 / 0xc5 | `navy_capital_ship_attack_factor` / `navy_capital_ship_defence_factor` | | 0xc34a50 |
| 0xc6 / 0xc7 | `navy_screen_attack_factor` / `navy_screen_defence_factor` | | 0xc34a50 |
| 0xc8 | `navy_submarine_detection_factor` | `MODIFIERS_SUBMARINE_DETECTION_FACTOR` | 0xc3a950 |
| 0x107 | `naval_night_attack` | | 0xc34de4 (× darkness) |
| 0x168 | `skill_bonus_factor` | `MODIFIER_UNIT_LEADER_SKILL_BONUS_FACTOR` | leader rebuild 0xc2179e (leader `+0x298`) |
| 0x19f | `navy_visibility` | `MODIFIER_NAVAL_VISIBILITY_FACTOR` | 0xc37067, 0xc3aa50, 0xc3abb0 |
| 0x1a0 / 0x1a1 | `navy_casualty_on_hit` / `navy_casualty_on_sink` | `MODIFIER_NAVAL_CASUALTY_ON_HIT/SINK_FACTOR` | 0xc3e305 / 0xd782d7 (manpower only) |
| 0x1b1 | `screening_efficiency` | | 0x161ce06 via 0x15c70b0 |
| 0x1b2 | `positioning` | | 0x161bb43 via 0x15c70b0 |
| 0x1b3 | `screening_without_screens` | | 0x161d2cd via 0x15c70b0 |
| 0x1b7 / 0x1b8 | `naval_damage_factor` / `naval_defense_factor` | | 0xc34e3b; 0x19741d9 / 0x197427b via 0x1972570 |
| 0x1b9 | `naval_enemy_retreat_chance` | | 0x1975378, enemy commanding admiral `+0x298` only |
| 0x1bc | `naval_torpedo_reveal_chance_factor` | | 0x1973ff7 via 0x1972570 (torpedo-firing reveal only) |
| 0x1bd | `naval_torpedo_screen_penetration_factor` | | 0x1972cff via 0x1972570 |
| 0x1be | `naval_torpedo_cooldown_factor` | | 0x1972b00, 0x1972ba0 via 0xc38760 |
| 0x1c7 | `naval_hit_chance_against` | | 0x1970bd7 (target ship's country) |
| 0x1c8 / 0x1c9 | `navy_capital_ship_attack_factor_against` / `_defence_factor_against` | | 0xc34c29 (province controller) |
| 0x1ca / 0x1cb | `navy_screen_attack_factor_against` / `_defence_factor_against` | | 0xc34c97 (province controller) |
| 0x1cc / 0x1cd | `navy_submarine_attack_factor_against` / `_defence_factor_against` | `MODIFIERS_SUBMARINE_*_AGAINST_A_COUNTRY` | 0xc34bbb (province controller) |
| 0x1d0 | `naval_enemy_fleet_size_ratio_penalty_factor` | | 0x161bfe3, enemy commanding admiral `+0x418` only |
| 0x1d1 / 0x1d2 | `naval_has_potf_in_combat_attack` / `naval_has_potf_in_combat_defense` | | 0xc351b5, own commanding admiral `+0x418` only |
| 0x1d3 | `naval_critical_score_chance_factor_against` | | 0x1974254 (target ship's country) |
| 0x248 | `navy_weather_penalty` | | 0xd67325 (country only) |

"Combat reader" lists every site where the id is loaded as an immediate in front of a modifier getter (`0x55e360`
container get, `0xc38760`, `0x15c70b0`, `0x1972570`), from a whole-`.text` scan for the id as imm32. Note the script key of
0x19f is `navy_visibility` and of 0x1a0/0x1a1 `navy_casualty_on_hit`/`_on_sink`, not their loc-key spellings.

## The modifier sums

### The ship modifier sum 0xc38760

`0xc38760(out, ship, id, admiral, tooltip, tooltipArg)`. Each holder adds `holder+0x10 .Get(id)` (0xc327e0).

```
tf = ship+0x728; owner = country(tf+0x1d8)
v  = owner container (+0x5a8 holder, +0x5b8)                     // 0xc387c7: ideas, spirits, doctrines, technologies' country keys …
   + static `pride_of_the_fleet`        if ship == owner+0x250   // 0xc387e2..0xc38818 (0x55b160()+0xb0)
   + ship experience holder +0x740                               // 0xc3883a: the four experience blocks (S-XP-EFFECT)
   + holder +0x1a0 of the object at ship+0x8f8                   // 0xc38868 (S-XP calls it the captain/leader; not named)
   + 0xd6fa50(tf, id)                                            // task-force modifiers
   if province = tf+0x1f0:
   + terrain(region).subUnitHolder[ship+0x88]                    // 0xc388c2 0x143d340: the naval terrain's per-sub-unit block
   + regionWeather[id] ×fx (ship.stat[0x2a] − 1 + clamp(1 + owner.navy_weather_penalty, 0, 1))   // 0xf19fd0, 0xd67310
   + 0xf197d0(weatherManager, province.id) holder                // province holder (content not decoded)
   + tf holder +0x658 (flag 1)                                   // 0xc38aab
   + admiral holder +0x408 (container +0x418)                    // 0xc38ae3; admiral argument, else the fleet's admiral 0xd64510
   + admiral per-sub-unit holder 0xc19bf0(admiral, sub-unit)     // 0xc38b1c
   + Σ damaged parts: part+0x98 .Get(id) × count × (1 + v(0x1bb))  // S-CRIT-PART
   + max over the entries of tf+0x728 (count +0x734) of 0x1622d60  // building-module admirals: Σ of the leader's four skill
                                                                   // objects' +0xe0 containers (`building_module_modifier`)
```

### Mod(id; country, location, admiral) 0x15c70b0

`= country+0x5b8 .Get(id) + regionWeather(+0x10) .Get(id) + navalTerrain(region)+0xb8 .Get(id) + admiral+0x418 .Get(id)`
(0x15c70e6, 0x15c72ed, 0x15c73c0, 0x15c7494). Used for `positioning` (0x1b2), `naval_enemy_positioning_in_initial_attack`
(0x5a, with the enemy admiral), `screening_efficiency` (0x1b1), `screening_without_screens` (0x1b3), and by the member
getter for convoys. Country and admiral are added separately, so no source is counted twice.

### Member getter 0x1972570

For a ship: `0xc38760(ship, id, side+0x130)` (0x19725e9), i.e. with the side's commanding admiral. For a convoy:
`0x15c70b0(country, location, admiral)` (0x1972636). Then the screening bonuses (S-TARGET).

### Reads of one leader container only (no country, no location)

| Site | Container | Modifier |
|---|---|---|
| 0x161bfdc | enemy commanding admiral `+0x418` | `naval_enemy_fleet_size_ratio_penalty_factor` (S-POS-SIZE) |
| 0xc351ae | own side's commanding admiral `+0x418` | `naval_has_potf_in_combat_attack/defense` (S-STATS-COMBAT) |
| 0x1975371 | enemy commanding admiral `+0x298` | `naval_enemy_retreat_chance` (S-RETREAT roll) |

## The admiral's containers

A navy leader (CNavyLeader, vtable 0x2955dc0, `leader+0xe7c == 2`) keeps two combat containers, rebuilt by 0xc21fc0
(sources) and 0xc21570 (combine):

```
// 0xc21fc0
for trait in leader+0xdc8:                                        // count +0xdd4
    S_A  += trait.modifier (+0x360);            S_B  += trait.modifier          // holders +0x7c8 / +0x708
    S_A2 += trait.non_shared_modifier (+0x420); S_B2 += trait.non_shared_modifier // holders +0x948 / +0x888
    (corps_commander_modifier +0x4e0, field_marshal_modifier +0x5a0 -> the land-only holders)
S_A += [leader+0xe60].modifier                                    // general skill level (0xae9da0 lookup)
S_A += attack / defence / maneuvering / coordination level modifier   // vt+0xf0 = 0xc11540: [+0xf70], [+0xf80], [+0xf90], [+0xfa0]
0x70ba00(owner, leader, S_A, S_B):                                // country+0x15b8 entries bound to this leader
    for e in owner+0x15b8 (0x58 bytes): if e+0x48 == 0 and e+0x10 == leader:
        S_A += e.obj+0x478 × w;  S_B += e.obj+0x3b8 × w           // w = min(1, e+0x18); timed (obj+0xa8 start, +0xac duration)
// 0xc21570
A = S_A + S_A2 + [character(leader+0xf48)+0xd0]+0x20              // the character's `unit_medals` object (parser 0xfa50b0)
B = S_B + S_B2
eff = vt+0x88 × vt+0x90 × taskforceCapFactor + B.Get(skill_bonus_factor 0x168), >= 0   // 0xc19fa0, 0xd53880
0x60fbd0(A, eff): entry × eff for definitions flagged at def+0x60 bit 1, and negative entries unless bit 2
leader+0x418 = A        // read by 0x15c70b0, 0xc38760 (holder +0x408), 0x161bfdc, 0xc351ae
leader+0x298 = B        // read by the retreat roll (0x1b9)
```

`taskforceCapFactor` (0xd53880) is `1 − clamp((n − cap) / cap, 0, 1)` when the fleet has more task forces `n` (0xd55ed0)
than `cap` (0xc18780; `NNavy.ADMIRAL_TASKFORCE_CAP` = 0 disables it), else 1.

**No step reads the country container `+0x5b8`.** The country reads inside `unitleader.cpp` are single values for
leader bookkeeping (e.g. `promote_cost_factor` 0x16a at 0xc11bab, `experience_gain_factor` 0x2d at 0xc1fa08, where the
country value and the leader's `+0x298` value are explicitly added). The entries of `country+0x15b8` are bound to one
leader and timed; national ideas are not among them. The vanilla navy skill files put
`naval_enemy_fleet_size_ratio_penalty_factor` and `screening_efficiency` in `building_module_modifier`, which only reaches
the building-module term of 0xc38760 (0x1622d60), not the containers above.

**Verdict.** Country modifiers never reach `leader+0x418` or `leader+0x298`. So:
- `naval_enemy_fleet_size_ratio_penalty_factor` from a country source has **no battle effect**: vanilla
  `naval_tripwire_navy_spirit` (0.2) and the carrier doctrine reward `localized_sea_power` (0.2); TFB's
  `navy_spirits.txt` (0.20) and carrier doctrine (0.1). Only the enemy admiral's traits (vanilla `lone_wolf` 0.1),
  medals and leader-bound entries count.
- `naval_has_potf_in_combat_attack/defense` from a country source has **no effect**: vanilla doctrine `line_of_battle`
  (0.10), ideas in `sweden.txt` and `british_raj_goe.txt`; TFB `line_of_battle` (0.05) and its RAJ idea. Only admiral
  traits count (vanilla `navy_media_personality` 0.1 / 0.1).
- `naval_enemy_retreat_chance` is read only from the enemy commanding admiral's `+0x298` (vanilla: traits only).
- Everything else read through 0xc38760, 0x1972570 or 0x15c70b0 includes the country: `positioning`,
  `screening_efficiency`, `screening_without_screens`, `naval_enemy_positioning_in_initial_attack`, `naval_night_attack`,
  the capital/screen/submarine attack and defence factors, `naval_damage_factor`, `naval_defense_factor`,
  `navy_anti_air_attack(_factor)`, the hit chance, crit, torpedo, retreat and speed modifiers.
- The `_against` attack/defence factors look up the province controller (see S-STATS-COMBAT-GETTER) and are likely
  inert at sea; `naval_hit_chance_against` and `naval_critical_score_chance_factor_against` work against the target ship's
  country.

**Confidence.** High that no country term is added (every source of 0xc21570/0xc21fc0/0x70ba00/0xc11540 traced).
Medium on the identity of the `country+0x15b8` entries (leader-bound, timed; the UI 0x15f4200 names them
`spirit_modifier_entry`) and on which modifiers the 0x60fbd0 scaling touches.

## How it was verified

Static reading only: the registration and token tables joined by script (no hand-copied names); every id read site
found by an imm32 scan of `.text` and checked in disassembly; the leader rebuild traced through CNavyLeader's vtable
(RTTI `.?AVCNavyLeader@@`). An in-game check: a country with `naval_tripwire_navy_spirit` and no admiral trait should
show the same positioning penalty as without it in a logged battle.

## Differences from the current spec

- S-STATS-COUNTRY: the engine can treat doctrine and spirit modifiers as country modifiers for every key in the
  "includes the country" list above, but must ignore (or warn about) country values of
  `naval_enemy_fleet_size_ratio_penalty_factor`, `naval_has_potf_in_combat_attack/defense` and
  `naval_enemy_retreat_chance`, which only an admiral can carry. `naval_coordination` has no combat reader.
- S-POS / S-SCREEN: `positioning`, `screening_efficiency`, `screening_without_screens` and
  `naval_enemy_positioning_in_initial_attack` are country + region weather + naval terrain + admiral.

## Unresolved

- What slot 0 of the targeted-modifier table (`country+0xf88`) holds, i.e. whether the `_against` attack/defence factors
  can ever apply at sea.
- Which modifier definitions have the scaling flags read by 0x60fbd0, and the size efficiencies at CNavyLeader vt+0x88 /
  vt+0x90 (0xc10ab0, 0xc10a10) and the cap 0xc18780.
- The object at `ship+0x8f8` (holder +0x1a0), the task-force holder `tf+0x658`, 0xd6fa50, the province holder 0xf197d0, and
  the `tf+0x728` entry list behind the building-module term.
- `naval_coordination` (0x19): no read through the modifier getters in combat code; a read through a computed id is not
  excluded.
