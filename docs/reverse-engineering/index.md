# hoi4.exe lookup index

| | |
|---|---|
| Build | 1.19.3.0 "Operation Postern" |
| Executable SHA-256 | `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5` |
| Addresses | RVAs, image base 0x140000000. Valid for this build only. |

Built only from the [findings](findings/README.md). Nothing here is new evidence: follow the link for pseudocode, evidence addresses and confidence. "?" means the source finding is unsure; the confidence word is the finding's own.

**How to add to this index.** Check this index before decoding anything in hoi4.exe. Every decoding session adds each function, struct field, modifier id and define read site it decoded here (one row each, sorted, with a link), and puts the pseudocode, evidence addresses and confidence in a findings file (`findings/<SPEC-ID>-<name>.md`, from [finding-template.md](finding-template.md)). If a new reading contradicts a row, keep both meanings, flag the row "**disagrees**", and settle it in [findings/README.md][README/disagree]. After a game patch, re-run `tools/re/` and re-verify before trusting any row.

File keys: HOUR = [S-HOUR][HOUR], ACT = [S-ACTIVATE][ACT], TGT = [S-TARGET][TGT], HIT = [S-HIT][HIT], DMG = [S-DMG][DMG], POS = [S-POS][POS], XP = [S-XP][XP], AIR = [A-air-combat][AIR], WTH = [S-WEATHER][WTH], LOG = [in-game logging][LOG].

## 1. Functions

Sorted by RVA. "chunk" = a non-contiguous part of the function above it, as the finding names it.

| RVA | Name / role | Spec IDs | Finding |
|---|---|---|---|
| 0x36f0 | console command registration (`weather`, `enable_weather`, `disable_weather`, `reloadweather`, `set_weather`, …) | S-WEATHER | [WTH/cmd] |
| 0x1a5630 | "Resetting game": clears HasGameStarted (gamestate+0xa39) via 0x1edff0 | S-MANPOWER | [XP/mp] |
| 0x1d4810 | `gamestate.daily`: runs `country.daily` 0x6e76a0 for every country (0x1d59eb) | S-MANPOWER | [XP/mp] |
| 0x1dd370 | gamestate tick: date += 1 h, hourly update 0x1df400, then `gamestate.daily` 0x1d4810 only if the day changed (0x1de3f5) | S-MANPOWER, S-HOUR | [XP/mp] |
| 0x1df400 | gamestate hourly update (`DoCountryHourlyUpdates`); calls the combat manager hourly update 0xbb8ae0 (0x1e0103) | S-HOUR | [XP/mp] |
| 0x1e0610 | gamestate init (`InitGameState`): clears gamestate+0xa39 (0x1e063a) | S-MANPOWER | [XP/mp] |
| 0x1edff0 | `SetGameStarted(gamestate, value)`: writes `value` to gamestate+0xa39; 1 only from "on_startup", 0 from reset, savegame load and the frontend | S-MANPOWER | [XP/mp] |
| 0x22e040 | console `weather` / `enable_weather` / `disable_weather` handler (mode 0 toggle, 1 on, else off) | S-WEATHER | [WTH/cmd] |
| 0x243be0 | console `combat_analyzer`: loads two OOBs, runs hourly updates until the combat ends; walks armies only (land-only) | — | [LOG] |
| 0x25d2e0 | console `gamespeed <0-5>` handler (0 pauses) | — | [LOG] |
| 0x27e220 | console `run <file>`: feeds each line through the console input | — | [LOG] |
| 0x2874b0 | console `set_weather` ("Weather was set to '"); not decoded | S-WEATHER | [WTH/open] |
| 0x535c40 | resolves member+0x10 convoy reference | S-TARGET | [TGT/bb] |
| 0x5574d0 | modifier registration call `(token, id, …)` | — | [HIT/map], [DMG/conv] |
| 0x557aa0 | add modifier block scaled by `s` | S-XP-EFFECT | [XP/effect] |
| 0x55b160 | global modifier block (XP flat gain, one branch) | S-XP-GAIN | [XP/gain] |
| 0x55f7f0 | modifier-definition registration (`MODIFIER_*` → id) | — | [HIT/map], [DMG/conv] |
| 0x6e76a0 | `country.daily`: `army.daily` (slot 19 on armies), `fleets.daily` (0xd54840 per fleet, country +0x278/+0x284), `railway_guns.daily` (slot 19) | S-MANPOWER | [XP/mp] |
| 0x6ff065 | game-start init: task-force manpower refill with force = true | S-MANPOWER | [XP/mp] |
| 0x93c1d0 | define loader (only other reference to COMBAT_MIN_DURATION) | S-END | [HOUR/end] |
| 0xa3b460 | ship reliability getter `[ship+0x328]` | S-CRIT | [HIT/crit] |
| 0xbb8ae0 | combat manager hourly update: validity, Update, then EndHour on every combat | S-HOUR | [HOUR/chain] |
| 0xbba1b0 | remove combat | S-HOUR | [HOUR/chain] |
| 0xbf9b90 | level = count of `UNIT_EXP_LEVELS[i] <= xp/maxXP` | S-XP-LEVELS | [XP/levels] |
| 0xbf9d90 | progress within level (UI only) | S-XP-LEVELS | [XP/levels] |
| 0xc00000 | task force in combat (`tf+0x1b4 > 0`) | S-XP-GAIN | [XP/gain] |
| 0xc0f200 | captain XP gain (`gain × LEADER_EXPERIENCE_SCALE`) | S-XP-GAIN | [XP/gain] |
| 0xc1aa20 | leader XP (BASE_LEADER_TRAIT_GAIN_XP, FIELD_MARSHAL_XP_RATIO) | S-HOUR | [HOUR/hour] |
| 0xc2ffe8 | ship constructor site: XP cap = NAVY_MAX_XP | S-XP-LEVELS | [XP/levels] |
| 0xc31030 | merge critical-part candidate into (part, max instances); aggregation ? | S-CRIT-PART | [DMG/part] |
| 0xc31840 | add ship XP with modifiers (mode 0 = combat) → 0xc3b240 | S-XP-GAIN | [XP/gain] |
| 0xc333e0 | ship stat part by index (0x18 = carrier surface detection) | S-POS-DETECT | [POS/detect] |
| 0xc338a0 | ship combat XP gain per hour | S-XP-GAIN | [XP/gain], [HOUR/hour] |
| 0xc339d0 | level progress (UI only) | S-XP-LEVELS | [XP/levels] |
| 0xc34500 | critical part damage `(ship, &str, &org)`; true = part hit, crit mult skipped | S-CRIT-PART | [DMG/part], [HIT/crit] |
| 0xc34a50 | attack getter `base × (1 + Σ factors)`, ≥ 0 (not fully decoded) | S-DMG | [DMG/dmg] |
| 0xc36390 | ship experience level | S-XP-LEVELS | [XP/levels] |
| 0xc37000 (body 0xc37006) | ship hit profile | S-HIT-SHIP | [HIT/ship] |
| 0xc37180 | hit profile tooltip (UI only) | S-HIT-SHIP | [HIT/map] |
| 0xc37ae0 | manpower ratio `[+0x808]/[+0x80c]`, clamped [0,1] | S-HIT | [HIT/hit] |
| 0xc37e70 | max org | S-DMG-APPLY | [DMG/apply] |
| 0xc38760 | ship modifier sum / generic modifier gather (country, location, parts × (1 + 0x1bb)) | S-HIT-MODS, S-DMG, S-CRIT-PART | [HIT/mods], [DMG/dmg], [DMG/part] |
| 0xc39530 | org ratio `[ship+0x700] / maxOrg` | S-HIT, A-SORTIE | [HIT/hit], [AIR/sortie] |
| 0xc3a470 | ship AA (STAT_NAVY_ANTI_AIR_ATTACK), optional battle context | S-POS, A-TARGET, A-AA-REDUCE | [POS/aa], [AIR/target] |
| 0xc3a520 | armor | S-DMG-PIERCE | [HIT/pierce] |
| 0xc3a570 | heavy piercing | S-DMG-PIERCE | [HIT/pierce] |
| 0xc3a580 | heavy attack. **disagrees**: HOUR says light attack (settled: heavy) | S-COOLDOWN, S-DMG | [ACT/cooldown], [TGT/bb], [HIT/map], [HOUR/hour] |
| 0xc3a5c0 | light piercing | S-DMG-PIERCE | [HIT/pierce] |
| 0xc3a5d0 | light attack. **disagrees**: HOUR says torpedo attack (settled: light) | S-COOLDOWN, S-DMG | [TGT/bb], [HIT/map], [HOUR/hour] |
| 0xc3a740 | current max speed incl. modifiers | S-HIT-SHIP, S-RETREAT-SPEED | [HIT/ship], [ACT/speed] |
| 0xc3a890 | depth-charge attack (HOUR: "an anti-sub stat", unverified) | S-COOLDOWN, S-TARGET-DC | [ACT/cooldown], [TGT/bb], [HOUR/hour] |
| 0xc3a950 | member sub detection | S-SUB | [HOUR/hour] |
| 0xc3aa20 | sub visibility (STAT_NAVY_SUB_VISIBILITY) | S-SUB | [ACT/sub], [POS/sub] |
| 0xc3aae0 | effective surface detection | S-POS-DETECT | [POS/detect] |
| 0xc3ac00 | torpedo attack. **disagrees**: HOUR says heavy attack (settled: torpedo) | S-COOLDOWN, S-DMG | [ACT/cooldown], [TGT/bb], [HOUR/hour] |
| 0xc3ac60 | HP ratio `[+0x6f8] / max([+0x308], 0.001)` | S-RETREAT, S-TARGET-WEIGHT, S-DMG-APPLY | [ACT/retreat], [TGT/weight], [DMG/apply] |
| 0xc3af10 | is capital (`ship+0x628` bit 6) | S-SCREEN, A-STACK | [TGT/bb], [AIR/stack] |
| 0xc3af20 | is carrier (bit 22 or 37 + set lookup) | S-SCREEN, S-XP, A-* | [TGT/bb], [XP/effect], [AIR/stack] |
| 0xc3b130 | "no parts / convoy-like"? test; part damage skipped (DMG ties it to the +0x738 link) | S-CRIT-PART | [HIT/crit], [DMG/apply] |
| 0xc3b210 | is screen (bit 8) | S-SCREEN | [TGT/bb] |
| 0xc3b220 | is submarine (bit 7) | S-TARGET | [TGT/bb] |
| 0xc3b230 | bit-39 type (support ship?) | S-ACTIVATE, A-STACK | [TGT/bb], [ACT/activate] |
| 0xc3b240 | XP add with flat (0x35) and factor (0x34) modifiers | S-XP-GAIN | [XP/gain] |
| 0xc3bde0 | per-ship step of the task-force update (XP gain, then 0xc3ce50) | S-XP-GAIN | [XP/gain] |
| 0xc3ce50 | recompute ship experience modifiers when `ship+0x800` dirty | S-XP-EFFECT | [XP/effect] |
| 0xc3d090 | recompute ship stats `(ship, 1)` | S-CRIT-PART | [DMG/part] |
| 0xc3e0e0 | apply damage `(ship, str, org, flag)`: XP and manpower loss, HP, org | S-DMG-APPLY, S-XP-LOSS, A-DMG | [DMG/apply], [DMG/xp], [XP/loss], [HOUR/order] |
| 0xc3e4e0 | ship manpower update `(ship, refill)` | S-MANPOWER | [XP/mp] |
| 0xc3e680 | sunk check (HP ≤ 0, unless +0x738 link) | S-DMG-APPLY | [DMG/apply], [HOUR/order] |
| 0xc4d910 | planes on a carrier (sum of all wings) | S-POS, A-SORTIE | [POS/carrier], [AIR/sortie] |
| 0xc53fd0 | reads cached carrier crowd factor | A-SORTIE | [AIR/sortie] |
| 0xc54980 | port-strike external plane cap | A-EXTERNAL | [AIR/ext] |
| 0xc579f0 | carrier overcrowding factor chain | A-SORTIE | [AIR/sortie] |
| 0xc5c5c0 | fuel-related non-flying planes | A-LOSSES | [AIR/losses] |
| 0xcfe7e0 | draw manpower from national pool | S-MANPOWER | [XP/mp] |
| 0xd1b800 | reads 0x3334d70 (not COMBAT_DAMAGE_RANDOMNESS; who.py false positive) | S-DMG-RANDOM | [DMG/random] |
| 0xd54840 | fleet daily update: vtable +0x98 (DailyUpdate) on each task force (fleet +0xb8, count +0xc4) | S-MANPOWER | [XP/mp] |
| 0xd69090 | CTaskForce::DailyUpdate (vtable slot 19): first calls 0xd7a7e0(tf, false), before and regardless of the in-combat test | S-MANPOWER | [XP/mp] |
| 0xd6eeb0 | unit ship list | S-POS | [POS/joined] |
| 0xd6fa50 | task-force modifiers | S-DMG | [DMG/dmg] |
| 0xd705f0 | CTaskForce update (vtable slot 18): per-ship XP | S-XP-GAIN | [XP/gain], [HOUR/hour] |
| 0xd77ff0 | task force: manpower returned on sinking | S-XP | [DMG/xp] |
| 0xd7a7e0 | refill task-force manpower `(tf, force)`: every ship if HasGameStarted() or force | S-MANPOWER | [XP/mp] |
| 0xdd70ae | CInGameIdler::InitData | S-MANPOWER | [XP/mp] |
| 0xdd3a50 | "LOADING_MAIN_MENU" / "EXITING_TO_FRONTEND": clears HasGameStarted via 0x1edff0 | S-MANPOWER | [XP/mp] |
| 0xdd9b20 | "on_startup" (`ingameidler.cpp`): sets HasGameStarted = 1 via 0x1edff0, before the on_action fires | S-MANPOWER | [XP/mp] |
| 0xdda5d0 | "LOADING_SAVEGAME": clears HasGameStarted via 0x1edff0 | S-MANPOWER | [XP/mp] |
| 0xe23bb0 | strategic region init: `region+0x140 = region+0x60 mod (2 × HOURS_DELAY_AFTER_EACH_COMBAT)` | A-CADENCE | [AIR/cadence] |
| 0xe24530 | air clock: is this an air hour for the region | A-CADENCE | [AIR/cadence] |
| 0xf185a0 | weather manager reset: cells, region states, seed, +0x348 = 1, +0x34a = 0 | S-WEATHER | [WTH/cmd] |
| 0xf19660 | province darkness | S-NIGHT | [HOUR/night] |
| 0xf1a000 | region weather state `wm+0x40 + i × 0x160` (null out of range; leaf) | S-WEATHER | [WTH/combat] |
| 0xf1c6e0 | CWeatherManager::HourlyUpdate; skipped while +0x349 or +0x34a | S-WEATHER | [WTH/cmd] |
| 0xf1cb90 | rebuild weather cells (+0x10) and region states (+0x40) | S-WEATHER | [WTH/cmd], [WTH/open] |
| 0xf1f710 | weather on: log seed, run HourlyUpdate | S-WEATHER | [WTH/cmd] |
| 0xf218a0 | mark every weather cell dirty (+0x3a8) for the map | S-WEATHER | [WTH/cmd] |
| 0xf60760 | plane speed with modifiers | A-INTERCEPT | [AIR/conv] |
| 0xf60970 | raw plane stat `(wing, out, idx, missionFlags)` | A-* | [AIR/conv] |
| 0xf60bc0 | naval strike agility per plane | A-AA-PREEMPT | [AIR/conv] |
| 0xf60f10 | naval attack per plane (`× 0.1 × (1 + Σ)`) | A-DMG | [AIR/conv] |
| 0xf61280 | naval targeting per plane (`× 0.1 × (1 + Σ)`) | A-HIT | [AIR/conv] |
| 0xf63100 | fighter-type equipment test (flags & 0x100000c00) | A-INTERCEPT | [AIR/intercept] |
| 0xf77630 | better-agility damage reduction | A-INTERCEPT | [AIR/intercept] |
| 0xf78160 | wing efficiency (fuel?) | A-DMG, A-LOSSES | [AIR/dmg] |
| 0xf7ad60 | better-speed damage increase (top-speed term ?) | A-INTERCEPT | [AIR/intercept] |
| 0xf7be70 | generic air combat (reads DISRUPTION_FACTOR_CARRIER) | A-INTERCEPT | [AIR/intercept] |
| 0xf80f60 | per-wing losses and damage (experience; not decoded) | A-LOSSES | [AIR/losses] |
| 0xf862a0 | generic air combat (reads COMBAT_DAMAGE_SCALE_CARRIER) | A-INTERCEPT | [AIR/intercept] |
| 0x10055a0 | region dominance state | S-POS | [POS/convoy] |
| 0x12a6740 | national navy XP, capped by FIELD_EXPERIENCE_MAX_PER_DAY | S-XP-GAIN | [XP/gain] |
| 0x13e05f0 | evidence for combatant+0xd8 (side at combat+0x28) | A-SORTIE | [AIR/conv] |
| 0x13e1df0 | side.GetOpponent | S-POS | [POS/pos] |
| 0x13e28c0 | CCombat::Update: the eight side virtual calls | S-HOUR, S-ORDER | [HOUR/hour], [POS/recompute] |
| 0x13e2ba0 | combat validity: checks both sides' country lists | S-HOUR | [HOUR/chain] |
| 0x143c720 | convoy-escort dominance helper (not fully decoded) | S-POS | [POS/convoy] |
| 0x15a5560 | combat location modifier container | S-POS | [POS/pos] |
| 0x15c28e0 | CNavalCombat::CanRetreat (vt+0x60): `hours >= COMBAT_MIN_DURATION` | S-RETREAT | [ACT/retreat], [README/disagree] |
| 0x15c28f0 | CNavalCombat constructor area (sets combatant+0xd8; sites 0x15c2913/0x15c2941) | — | [AIR/conv], [POS/top] |
| 0x15c3080 | second modifier holder of the combat (night/darkness) | A-SORTIE, A-HIT | [AIR/conv] |
| 0x15c3320 | XP gain formula part | S-XP-GAIN | [XP/gain] |
| 0x15c3600 | CNavalCombat::Update (vt+0x78) | S-HOUR | [HOUR/hour], [POS/recompute] |
| 0x15c3690 | CNavalCombat::EndHour (vt+0x80): hours++, distance, end check. **disagrees**: ACT says hours++ first, then both combatants update | S-HOUR, S-END | [HOUR/end], [ACT/bg] |
| 0x15c44e0 | CNavalCombat validity (vt+0x48) → 0x13e2ba0 | S-HOUR | [HOUR/chain] |
| 0x15c52b0 | merge unpaired groups (HOUR); "per-side joins" (POS) | S-HOUR | [HOUR/hour], [POS/recompute] |
| 0x15c55d0 | pair fire-exchange groups | FEX | [HOUR/fex] |
| 0x15c5820 | refresh environment: weather type (+0xa8) and region weather holder (+0xb0) from 0xf1a000, darkness (+0xb8) | S-HOUR, S-NIGHT, S-WEATHER | [HOUR/hour], [WTH/combat] |
| 0x15c6250 | per-attacker damage record (kill/assist credit) | S-DMG-APPLY, A-DMG | [DMG/apply], [AIR/dmg] |
| 0x15c70b0 | `Mod(id; country, location, admiral)` modifier sum | S-POS | [POS/pos], [HOUR/end] |
| 0x15c7580 | fleet exempt from side-wide retreat | S-RETREAT, S-END | [ACT/retreat], [HOUR/end] |
| 0x161a4a0 | naval combatant constructor (positioning = 1.0) | S-POS | [POS/top] |
| 0x161ab40 | add fleet/ships to a side: state 4, joined penalty, dominance bonus | S-HOUR, S-POS | [HOUR/hour], [POS/joined], [POS/convoy] |
| 0x161b5f0 | external plane join cap | A-EXTERNAL | [AIR/ext] |
| 0x161b960 | ComputePositioning `(side, out, tooltip)` | S-POS | [POS/pos] |
| 0x161ccb0 | screening and carrier screening | S-SCREEN, S-POS-SCREEN | [TGT/screen], [POS/screen] |
| 0x161e0d0 / 0x161e0fb | side surface-detection sum | S-POS-DETECT | [POS/detect] |
| 0x161e2c0 | activation count helper → 0x1c6a370 | S-ACTIVATE | [ACT/activate] |
| 0x161e400 | side vt+0xb0: positioning, screening, fleet AA | S-HOUR, S-POS, A-AA-REDUCE | [HOUR/hour], [POS/recompute], [AIR/reduce] |
| 0x161e5c0 | commanding admiral (highest skill) | S-POS | [POS/top] |
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
| 0x16205c0 | external planes join (5th arg ?) | A-EXTERNAL | [AIR/ext] |
| 0x1620940 | side HourPrep: decay joined penalty, cache admiral, → 0x1622360 | S-HOUR, S-POS | [HOUR/hour], [POS/joined] |
| 0x1620fa0 | damage-dealt statistics | S-DMG-APPLY | [DMG/apply] |
| 0x1621900 | merge a group into another | FEX | [HOUR/hour] |
| 0x1621a70 | side vt+0x100: per-country ship-type tallies + modifier table (purpose ?) | S-HOUR | [HOUR/hour] |
| 0x16222e0 | end-of-battle air cleanup, both sides (HOUR). **disagrees**: ACT calls it the combatant update → 0x1c6b560 | S-END | [HOUR/end], [ACT/bg] |
| 0x1622360 | rebuild groups (called from 0x1620940) | S-POS | [POS/recompute], [HOUR/hour] |
| 0x1622d40 | warship filter (ships, not convoys) | A-EXTERNAL, A-AA-REDUCE | [AIR/ext], [AIR/reduce] |
| 0x17e4f70 | piercing tooltip (UI only) | S-DMG-PIERCE | [HIT/map] |
| 0x196f293 / 0x196f2c3 / 0x196f47b / 0x196f6f8 | member constructor sites (charge = 0, +0x12c = −1) | S-COOLDOWN, S-SUB | [ACT/cooldown], [TGT/bb] |
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
| 0x1972570 | member modifier getter + screening bonus (`screeningValue`) | S-HIT-MODS, S-RETREAT-SPEED, A-SORTIE | [HIT/map], [TGT/bonus], [AIR/sortie] |
| 0x1972a70 / 0x1972b60 | weapon cooldown (variant) | S-COOLDOWN | [ACT/cooldown] |
| 0x1972c00 | MaxBox (light 0x1972c92, torpedo 0x1972cb7, heavy 0x1972dd4) | S-TARGET-* | [TGT/light], [TGT/heavy], [TGT/torp] |
| 0x1973470 | member HourUpdate: sub timer, state machine, retreat, fire, escape | S-HOUR, S-ACTIVATE | [HOUR/hour], [ACT/bg] |
| 0x1973521 | member hourly update (TGT), caller of 0x19740c0 (HIT); relation to 0x1973470 not stated | S-HOUR | [TGT/bb], [HIT/map], [POS/sub] |
| 0x1973d30 | is submarine (air target weight) | A-TARGET | [AIR/target] |
| 0x1973e20 | Fire one weapon slot (timer, PickTarget, shot loop) | S-ORDER, S-COOLDOWN | [HOUR/order], [ACT/cooldown] |
| 0x1973eea | fire chunk: last-shot record, torpedo-firing reveal | S-XP-GAIN, S-SUB | [XP/gain], [POS/sub] |
| 0x19740c0 | hit/shot loop: hit, randomness, crit, part, apply; `(shooterMember, targetMember, weapon, shooterShip, …)`, returns 1 when the target sank | S-HIT, S-DMG, S-CRIT | [HIT/map], [DMG/pipe], [HOUR/order], [LOG] |
| 0x1975150 | air part damage (CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR) | A-CRIT | [AIR/crit] |
| 0x1975280 | retreat roll P | S-RETREAT | [ACT/retreat], [HOUR/end] |
| 0x1975400 | sub reveal chance | S-SUB | [ACT/sub] |
| 0x1975480 | kill/assist credit | S-DMG-APPLY | [HOUR/order], [DMG/apply] |
| 0x1976630 | new external AirFX | A-EXTERNAL | [AIR/ext] |
| 0x1976ae0 | air entry owner | S-XP-GAIN | [XP/gain] |
| 0x1976ca0 | recompute AirFX planes (+0x5c) | A-* | [AIR/graph] |
| 0x1976db0 | carrier sortie efficiency (no org) | A-SORTIE | [AIR/sortie] |
| 0x1977000 | sortie efficiency darkness read (+0xb8 → mod 0x23) | S-NIGHT, A-SORTIE | [HOUR/night] |
| 0x19786c0 | CarrierSortieEfficiency: 0x1976db0 × org ratio | A-SORTIE, S-POS, S-XP-GAIN | [AIR/sortie], [POS/carrier], [XP/gain] |
| 0x1978e30 | average naval attack, targeting, agility over first nWings | A-DMG | [AIR/dmg] |
| 0x1979770 | new external AirFX | A-EXTERNAL | [AIR/ext] |
| 0x197a1b0 | AirFX cooldown ≤ 0 (feeds a dead list) | A-CADENCE | [AIR/cadence] |
| 0x197a1c0 | remove plane losses from wings (type 1 intercept, 2 AA, 6 kamikaze) | A-LOSSES | [AIR/losses] |
| 0x197a6a0 | find external AirFX for an air base | A-EXTERNAL | [AIR/ext] |
| 0x197a700 | air target pick | A-TARGET | [AIR/target] |
| 0x197ac80 | air strike: hits, damage, crit | A-HIT, A-DMG, A-CRIT | [AIR/hit], [AIR/dmg], [AIR/crit] |
| 0x197b380 | interception resolve | A-INTERCEPT | [AIR/intercept] |
| 0x197b890 | main air strike `(airfx, enemyGroup, combat, F)` | A-* | [AIR/graph] |
| 0x197c8b0 | preemptive AA | A-AA-PREEMPT | [AIR/preempt] |
| 0x197d4a0 | plane-weighted equipment averages (+0xc8..+0xe8) | A-HIT | [AIR/graph] |
| 0x197d770 | set carrier cooldown (no effect) | A-CADENCE | [AIR/cadence] |
| 0x197d990 | AirFX save code | A-CADENCE | [AIR/cadence] |
| 0x1989c60 | aggressiveness-check strength score (not decoded) | S-RETREAT | [ACT/retreat] |
| 0x1989fe0 | aggressiveness-check ratio `min(own/max(enemy, 0.1), 10)` | S-RETREAT | [ACT/retreat] |
| 0x198a080 | update fleet distance (+0x118) | S-HOUR | [HOUR/hour] |
| 0x1aa8f60..0x1ab5598 | tooltips/script triggers reading ship level | S-XP-EFFECT | [XP/effect] |
| 0x1ad7f00 | convoy hit profile | S-HIT-SHIP | [HIT/ship] |
| 0x1ad7f30 | convoy base air target weight | A-TARGET | [AIR/target] |
| 0x1ad8060 | convoy AA | A-AA-PREEMPT | [AIR/preempt] |
| 0x1ad8180 | convoy reliability | S-CRIT, A-CRIT | [HIT/crit], [AIR/crit] |
| 0x1ad81e0 | convoy HP ratio (TGT); non-ship damage path (DMG) | S-TARGET-WEIGHT, S-DMG-APPLY | [TGT/weight], [DMG/apply] |
| 0x1ad8610 | convoy damage apply (not decoded) | S-DMG-APPLY, A-DMG | [DMG/apply], [AIR/dmg] |
| 0x1ad8680 | convoy damage path (not decoded) | S-DMG-APPLY | [DMG/apply] |
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
| 0x2233fa0 | random int31 | FEX, A-* | [HOUR/fex], [AIR/conv] |
| 0x24ed3f0 | fx5 → fx15 | — | [XP/top] |
| 0x24ed580 | fx15 → fx5 | — | [XP/top] |
| 0x24ed730 | fixed → int, round half away from zero | — | see §4 |
| 0x24ef6f0 | store `-x` (negate) | — | [DMG/conv], [XP/effect] |
| 0x24f08a0 | weighted random draw | A-TARGET | [AIR/target] |

### Vtables and RTTI

| RVA | Meaning | Finding |
|---|---|---|
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
| 0x631e70 | equipment bonus applies to type |
| 0x13e81b0 | token id → EEquipmentStats |
| 0x831c0 | static token registration (name → id) |
| 0xc3d090 | CShip::RefreshStats |
| 0x1019e50 / 0x1429cb0 / 0x142b5f0 | add variant stats to ship × (1 + sub-unit stat) |
| 0x1019ea0 / 0x1429d30 | add sub-unit stat set (direct or base path) + clamps |
| 0x1429580 | sub-unit additive stat list |
| 0x1019fb0 | ship stat block finalize (not decoded) |
| 0xd11ae0 | country per-sub-unit stats lookup (`country+0xf70`) |
| 0xd0e200 | create country sub-unit copy, apply techs |
| 0x101b1b0 / 0x193d370 / 0x193d2a0 | apply technology sub-unit bonuses (type and categories, × level) |
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
| country `+0x160` | idea equipment bonuses |
| country `+0xa0` | country variants |
| country `+0xf70` | per-country sub-units |
| ship `+0x120+8s` | ship stat block |
| ship `+0x6f8` / `+0x700` | current HP / current org |
| ship `+0x918`/`+0x924` | damaged critical parts {part*, count} |
| sub-unit stats `+0x60+8s` | sub-unit stat array |

## 2. Struct fields

### Ship (ship.cpp object)

| Offset | Meaning | Finding |
|---|---|---|
| +0x8 / +0xc | handle `{type 51, id}`: the save's ship `id={ id type=51 }` | [LOG] |
| +0x1b8 | base surface detection | [POS/detect] |
| +0x1c8 | surface visibility | [HIT/ship] |
| +0x1d0 | sub visibility | [HIT/ship] |
| +0x228 | shooter stat "arg4" gating sub target weight (modifier 0x29; sub detection?) | [TGT/dc] |
| +0x248 / +0x250 / +0x258 | weapon hit chance factor: light / heavy / torpedo (design stat) | [HIT/mods] |
| +0x260 | ship torpedo damage reduction (inferred) | [HIT/pierce], [DMG/conv] |
| +0x268 | ship torpedo enemy crit chance (inferred) | [HIT/crit], [DMG/conv] |
| +0x308 | max HP (used as `max(·, 0.001)`) | [DMG/conv] |
| +0x328 | reliability (inferred, medium) | [DMG/conv], [HIT/crit] |
| +0x550 | sub-unit critical parts | [DMG/part] |
| +0x568 | `critical_part_damage_chance_mult` (inferred) | [DMG/part], [HIT/crit] |
| +0x628 | type flag bits: 6 capital, 7 sub, 8 screen, 22 or 37 carrier, 39 ? | [TGT/bb] |
| +0x66c | country modifier "experience_gain_<type>_combat_factor" | [XP/gain] |
| +0x678 | per-ship hit profile multiplier ? (presumably 1.0) | [HIT/ship] |
| +0x6f8 | HP (current strength) | [DMG/conv], [HOUR/order] |
| +0x700 | org | [DMG/conv] |
| +0x708 | experience | [XP/levels] |
| +0x710 | experience cap (NAVY_MAX_XP) | [XP/levels] |
| +0x728 | → task force / fleet | [TGT/sub], [AIR/conv] |
| +0x730 | compared to air entry owner (carrier identity ?) | [XP/gain] |
| +0x738 | linked object handle ? (redirects damage, no parts) | [DMG/apply] |
| +0x750 | experience modifier list | [XP/effect] |
| +0x800 | experience-modifier dirty flag | [XP/effect] |
| +0x808 / +0x80c | manpower / max manpower (int32) | [DMG/conv], [HIT/hit] |
| +0x8f8 | captain / leader | [XP/gain] |
| +0x918 | damaged parts list (part, count) | [DMG/part] |

### Fire-exchange member

| Offset | Meaning | Finding |
|---|---|---|
| +0x8 | ship handle `{type, id}` (resolved with 0x221f310) | [LOG] |
| +0x10 | convoy reference (no ship) | [TGT/bb], [DMG/apply] |
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
| +0xd8 | ctor flag: 1 for the side at combat+0x98 / CCombat+0x28; attacker? | [POS/top], [AIR/conv], [ACT/sub] |
| +0xe8 / +0xf4 | groups / count | [HOUR/fex] |
| +0x100 / +0x108 | `screening_bonus` / `capital_screening_bonus` (TGT says combatant; HIT says a struct slot bound at 0x60ed2a) | [TGT/bonus], [HIT/mods] |
| +0x130 | commanding admiral (POS). **disagrees**: HIT "battle location", AIR "battle context" for AA | [POS/top], [HIT/mods], [AIR/target] |
| +0x140 | side-wide retreat flag | [ACT/retreat], [HOUR/end] |
| +0x148 | screening | [TGT/screen] |
| +0x150 | carrier screening | [TGT/screen] |
| +0x158 | positioning (ctor 1.0) | [POS/top] |
| +0x160 | newly joined penalty | [POS/joined] |
| +0x168 | convoy-escort dominance bonus | [POS/convoy] |
| +0x170 | fleet AA | [POS/aa], [AIR/reduce] |
| +0x3e8 | external planes joined (cumulative) | [AIR/ext] |

### Combat (CCombat / CNavalCombat)

| Offset | Meaning | Finding |
|---|---|---|
| +0x28 / +0x30 | attacker / defender combatant (CCombat) | [HOUR/chain] |
| +0x44 | hours: 1 during the first hour's Update, incremented in EndHour (observed) | [HOUR/chain], [ACT/bg], [POS/recompute], [LOG] |
| +0x98 / +0xa0 | the same two sides, typed | [HOUR/chain] |
| +0x38 | → +0xc8 strategic region of the battle (medium) | [WTH/combat] |
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
| +0x1a8 | combats list ([0] = first) | [XP/gain] |
| +0x1b4 | > 0 = in combat | [XP/gain] |
| +0x348 / +0x354 | ships / count | [XP/gain] |
| +0x374 | mission: 3 convoy raiding; 4 convoy escort? | [ACT/retreat], [POS/convoy] |
| +0x391 | flag: aggressiveness-check value = 0 if set | [ACT/retreat] |
| +0x394 | aggressiveness (engagement rule) 0..4, save key `navy_engagement_rule` | [ACT/retreat], [TGT/sub] |
| +0x4ec | carrier stance index | [AIR/conv] |

### AirFX (air object)

| Offset | Meaning | Finding |
|---|---|---|
| +0x08 | group | [AIR/conv] |
| +0x10 | carrier member | [AIR/conv] |
| +0x1c | → carrier base | [AIR/conv] |
| +0x28 / +0x34 | strike wings (mission flags & 0x190: 0x10 naval strike, 0x80 kamikaze, 0x100 port strike) | [AIR/conv] |
| +0x40 / +0x4c | all wings | [AIR/conv] |
| +0x5c | total planes, all wings | [AIR/conv], [POS/carrier] |
| +0x64 | country | [AIR/conv] |
| +0x80 | "already struck" byte (read only by a dead list and save code) | [AIR/cadence] |
| +0x81 | external (land-based) flag | [AIR/conv], [POS/carrier] |
| +0xc0 | external efficiency (source ?) | [AIR/ext] |
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
| global | 0x332f260 | gamestate pointer | [LOG] |
| global | 0x3452520 / 0x3452524 | RNG counter / seed (the save's `multiplayer_random_count` / `multiplayer_random_seed`, observed) | [LOG] |
| global | 0x3452529 | log every `random_fixed` call (console `randomlog`) | [LOG] |
| gamestate | +0x310 / +0x31c | countries / count | [LOG] |
| country | +0x278 / +0x284 | fleets / count | [XP/mp], [LOG] |
| country | +0x290 / +0x29c | armies / count | [LOG] |
| gamestate | +0x468 | date in hours | [AIR/cadence] |
| gamestate | +0xa39 | HasGameStarted(): 1 in a running game, 0 while loading, resetting or in the menu (named by the assert at 0x1de28e) | [XP/mp] |
| gamestate | +0x688 | weather manager | [WTH/cmd] |
| weather manager | +0x10 | weather cells (0x180 bytes each; +0x28 active) | [WTH/cmd] |
| weather manager | +0x40 / +0x4c | region weather states (0x160 bytes each) / count | [WTH/combat] |
| weather manager | +0x308 | random seed | [WTH/cmd] |
| weather manager | +0x348 / +0x349 / +0x34a | init passes pending / second skip flag ? / weather disabled | [WTH/cmd] |
| region weather state | +0x48 | weather modifier holder | [WTH/combat] |
| region weather state | +0x108 / +0x110 / +0x118 / +0x120 | weather flags → combat type 2 / 3 / 4 / 5 (which weather is which ?) | [WTH/combat], [WTH/open] |
| strategic region | +0x58 | index into the weather manager's region states | [WTH/combat] |
| country | +0x5b8 | modifiers | [POS/pos], [XP/gain] |
| admiral | +0x418 | modifiers | [POS/pos] |
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

## 3. Modifier ids and read sites

### Modifier ids

Names come from the registration function 0x55f7f0 unless the confidence says otherwise.

| Id | Name | Reader | Finding |
|---|---|---|---|
| 0x15 | NAVAL_STRIKE (weather `naval_strike`) | 0x197b890 targeting mult | [AIR/conv], [AIR/hit] |
| 0x16 / 0x17 / 0x18 | NAVAL_STRIKE_ATTACK / TARGETTING / AGILITY_FACTOR | plane stat getters | [AIR/conv] |
| 0x22 | CARRIER_TRAFFIC | 0x1976db0 | [AIR/sortie] |
| 0x23 | CARRIER_NIGHT_TRAFFIC | 0x1976db0, 0x1977000 | [AIR/sortie], [HOUR/night] |
| 0x24 | `sortie_efficiency` (not in table; from use) | 0x1976db0 via 0x1972570 | [AIR/conv] |
| 0x28 | NAVAL_HIT_CHANCE | 0x1970b48 via 0x1972570 | [HIT/mods] |
| 0x29 | NAVAL_SPEED_FACTOR (HIT). **disagrees**: TGT uses it as the "arg4" sub-weight stat | 0x1971cc0 | [HIT/map], [TGT/target] |
| 0x2a | surface detection stat modifier (name ?) | 0xc3aae0 | [POS/detect] |
| 0x34 / 0x35 | experience_gain_navy_unit_factor / _unit? (low-medium) | 0xc3b240 | [XP/gain] |
| 0x57 | naval_retreat_chance? (medium; HOUR: unresolved) | 0x1975280 | [ACT/retreat], [HOUR/end] |
| 0x58 | naval_retreat_chance_after_initial_combat? (medium) | 0x1975280 (0x1975325) | [ACT/retreat], [HOUR/end] |
| 0x59 | naval_retreat_speed_after_initial_combat (medium) | 0x196fe80 | [ACT/speed] |
| 0x5a | naval_enemy_positioning_in_initial_attack? (medium). Gate **disagrees**: POS `side+0xd8 == 0`, HOUR "side not in all-retreat" | 0x161b960 (0x161bbb5) | [POS/pos], [HOUR/end] |
| 0x5b | naval_retreat_speed (confirmed in-game: TFB `smoke_generator`'s country value, +0.2/day) | 0x196fe80 via 0x1972570 | [ACT/speed], [LOG] |
| 0x5c | sub_retreat_speed (medium) | 0x196fe80 | [ACT/speed] |
| 0x5d | convoy_retreat_speed (medium) | 0x196fe80 | [ACT/speed] |
| 0xc9 / 0xca / 0xcb | NAVAL_CARRIER_AIR_ATTACK / TARGETTING / AGILITY_FACTOR | 0x1978e30, 0x197ac80, 0x197c8b0 | [AIR/conv], [AIR/dmg] |
| 0x10c | CARRIER_CAPACITY_PENALTY_REDUCTION | 0xc579f0 chain | [AIR/sortie] |
| 0x18c | NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR | 0x19740c0; air 0x197ac80 as `(1 − v)` | [HIT/crit], [AIR/crit] |
| 0x19f | NAVAL_VISIBILITY_FACTOR | 0xc37006; sub reveal (ACT). **disagrees**: POS names 0x1ba/0x1bc for the reveal | [HIT/ship], [ACT/sub], [POS/sub] |
| 0x1a0 | NAVAL_CASUALTY_ON_HIT_FACTOR (DMG; XP: unresolved) | 0xc3e0e0 | [DMG/xp], [XP/loss] |
| 0x1a1 | NAVAL_CASUALTY_ON_SINK_FACTOR | 0xd77ff0 | [DMG/xp] |
| 0x1b1 | screening_efficiency? (medium) | 0x161ccb0 | [TGT/screen], [POS/screen] |
| 0x1b2 | positioning? (medium) | 0x161b960 (0x161bb4c) | [POS/pos] |
| 0x1b3 | screening_without_screens? (medium) | 0x161ccb0 | [TGT/screen] |
| 0x1b6 | CARRIER_FIGHTER_SORTIE_EFFICIENCY_FACTOR | 0x1976db0 | [AIR/sortie] |
| 0x1b7 | NAVAL_DAMAGE_FACTOR | 0x19740c0 and 0xc34a50 (counted twice?) | [DMG/dmg], [POS/dmg] |
| 0x1b8 | NAVAL_DEFENSE_FACTOR | 0x19740c0 | [DMG/dmg] |
| 0x1b9 | naval_enemy_retreat_chance (rdata order) | 0x1975280 | [ACT/retreat] |
| 0x1ba | NAVAL_CRITICAL_SCORE_CHANCE_FACTOR | 0x19740c0 | [HIT/crit], [DMG/conv] |
| 0x1bb | NAVAL_CRITICAL_EFFECT_FACTOR (scales part effects) | 0xc38760 | [DMG/part] |
| 0x1bc | naval_torpedo_reveal_chance_factor | torpedo-firing reveal (0x1973f87..) | [ACT/sub], [POS/sub] |
| 0x1bd | naval_torpedo_screen_penetration_factor? | 0x1972c00 torpedo MaxBox | [TGT/torp] |
| 0x1be | naval_torpedo_cooldown_factor | 0x1972a70 | [ACT/cooldown] |
| 0x1bf / 0x1c0 / 0x1c1 | NAVAL_TORPEDO / LIGHT_GUN / HEAVY_GUN _HIT_CHANCE_FACTOR (read via 0x1972570: TFB's `screening_bonus` entries confirmed in-game) | 0x1970b48 via 0x1972570 | [HIT/mods], [LOG] |
| 0x1c2 | NAVAL_TORPEDO_ENEMY_CRITICAL_CHANCE_FACTOR | 0x1970b48 | [HIT/crit] |
| 0x1c3 | NAVAL_TORPEDO_DAMAGE_REDUCTION_FACTOR | 0x1970b48 (0x1971338..) | [HIT/pierce] |
| 0x1c7 | NAVAL_HIT_CHANCE_AGAINST_A_COUNTRY | 0x1970b48 | [HIT/mods] |
| 0x1cf | CARRIER_SORTIE_HOURS_DELAY | 0x197d770 | [AIR/cadence] |
| 0x1d0 | naval_enemy_fleet_size_ratio_penalty_factor? (enemy admiral only) | 0x161b960 (0x161bfdc) | [POS/size] |
| 0x1d1 | admiral and pride-of-the-fleet attack terms (name not given) | 0xc34a50 | [DMG/dmg] |
| 0x1d3 | NAVAL_CRITICAL_SCORE_CHANCE_FACTOR_AGAINST_A_COUNTRY | 0x19740c0 | [HIT/crit] |
| — | `naval_night_attack` (registered at 0x1092f3; HOUR: no read mapped. **disagrees**: DMG lists NAVAL_NIGHT_ATTACK × night factor in 0xc34a50) | 0xc34a50? | [HOUR/night], [DMG/dmg] |

### Static modifiers

| Block | Bound / read at | Effect | Finding |
|---|---|---|---|
| `screening_bonus` | bound 0x60ed2a; read 0x1972570 | `+screening × value` for boxes 2, 3, every key (vanilla naval_hit_chance 0.4, naval_retreat_speed 0.2, convoy_retreat_speed 0.2; TFB adds heavy/light gun hit chance factors 1 / 0.3, confirmed in-game) | [TGT/bonus], [HIT/mods], [LOG] |
| `capital_screening_bonus` | bound 0x60ed5d; read 0x1972570 | box 3: `+0x161ece0 value × value` (naval_retreat_speed 0.2, sortie_efficiency 0.1) | [TGT/bonus], [AIR/sortie] |
| `ship_experience_bonus_max`, `_malus_min`, `carrier_experience_bonus_max`, `_malus_min` | loaded 0x60ec2b / 0x60ec5e; applied 0xc3ce50 | level effect × `s` | [XP/effect] |
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
| OUT_OF_FUEL_ATTACK_FACTOR, OUT_OF_FUEL_TORPEDO_FACTOR | 0xc34a50 | attack penalty | [DMG/dmg] |
| NAVY_MAX_XP | 0xc2ffe8 | XP cap | [XP/levels] |
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
| RNG int | 0x2233fa0 | random int31 | [AIR/conv] |
| weighted draw | 0x24f08a0 | air target pick | [AIR/target] |
| chance checks | — | ACT: `roll <= chance`; hit: `100·U < hc`; crit: `U <= p`; part: `U < p` | [ACT], [HIT/crit], [DMG/rng] |
| RNG line tags | — | naval_fire_exchange_member.cpp 0x587 hit, 0x593 randomness, 0x59f crit; ship.cpp 0x2f4 part chance, 0x78f part pick; target 0x37b, torpedo reach 0x3d9; air 0x212, 0x21c, 0x43f, 0x45d | [DMG/rng], [TGT/target], [AIR/preempt] |
| tooling caveat | — | `who.py`/`uses.json` miss reads (decode from p−12, lose alignment; leaf functions without unwind entries) | [README], [POS/top], [XP/top] |

## 5. Open / UNRESOLVED

Cross-file disagreements are flagged "**disagrees**" in the tables above; the three settled ones are in [findings/README.md][README/disagree].

- Aggressiveness-check strength score 0x1989c60 internals. [ACT/open], [README-RE]
- Darkness value computation (with weather on; it is 0 with weather off [LOG]); whether `naval_night_attack` is read anywhere. [HOUR/night], [HIT/mods]
- `0xc31030` part instance aggregation. [DMG/open]
- Shooter "arg4" stat (`ship+0x228`, modifier 0x29) gating sub targets. [TGT/dc]
- Whether light guns and depth charges both fire in one slot-2 call. [ACT/open]
- Ship-type flag bit 39 (support ship?). [ACT/open], [TGT/bb]
- Combatant +0xd8, combat +0x10c/+0x10d meanings; convoy-only branch of the retreat check. [ACT/open]
- Convoy-type combat end path (EndHour returns false). [HOUR/end]
- Purpose of side vt+0x100 `0x1621a70`. [HOUR/hour]
- Modifier names by index: 0x57/0x58/0x5a (and other positioning/retreat modifiers), 0x1b1/0x1b3/0x1bd, 0x1b2/0x1d0, 0x34/0x35. [HOUR/end], [TGT/screen], [POS/pos], [XP/open]
- Ship `+0x678` hit profile mult, `+0x568`, `0xc3b130`. [HIT/ship], [HIT/crit]
- ~~NAVAL_DAMAGE_FACTOR counted twice (attack getter and hit multiplier)?~~ Yes: observed in logged hits (the experience malus applies in the shot profile and again per hit). [DMG/open], [LOG]
- Ship `+0x738` link; damage to non-ship targets (`0x1ad8610`). [DMG/open]
- Experience-level damage bonus location (DMG: not in the hit path; XP: only through `naval_damage_factor`/`naval_defense_factor` modifiers). [DMG/open], [XP/effect]
- Refresh frequency of the enemy count snapshot (0x161f810) vs the firing loop. [TGT/bb]
- Convoy-escort dominance helper 0x143c720 and the mission enum. [POS/convoy]
- Order of the task-force update 0xd705f0 vs the combat hour (this hour's shots or last hour's). [XP/open]
- `combat+0x120` setter; `region+0x60` = region id? [AIR/open]
- `airfx+0xe8` name (equipment +0x260). [AIR/open]
- Top-speed term in 0xf7ad60; plane stat indices 0x2f/0x30. [AIR/open]
- `0xf78160` (fuel?); `airfx+0xc0` source; caller and 5th argument of 0x16205c0. [AIR/open]
- Port-strike branch (`combat+0x10c`, PORT_STRIKE_DAMAGE_FACTOR). [AIR/open]
- `0xf80f60` per-wing losses/experience. [AIR/losses]
- Rebuilt region weather state after `disable_weather` (`0xf1cb90`): no weather and an empty holder?; which weather each region flag is; `set_weather` 0x2874b0. [WTH/open]

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
[WTH/cmd]: findings/S-WEATHER-weather-toggle.md#console-commands
[WTH/combat]: findings/S-WEATHER-weather-toggle.md#what-a-battle-reads
[WTH/open]: findings/S-WEATHER-weather-toggle.md#unresolved
