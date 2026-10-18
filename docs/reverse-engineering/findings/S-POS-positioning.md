# Positioning (S-POS, S-POS-SIZE, S-POS-DETECT, S-POS-SCREEN, S-POS-DMG): hoi4.exe 1.19.3 findings

All addresses are RVAs. "fixed" means int64 with scale 100000. Unless noted, fixed multiply is
`trunc(a*b / 100000)` (magic 0x29f16b11c6d1e109, `sar 14`, plus sign fix, so it truncates toward zero) and
fixed divide is `trunc(a*100000 / b)` (`idiv`).

Side object = `naval_combatant` (vtable 0x29e3e98). Fields used here:

| field | meaning |
|---|---|
| `+0x158` | positioning (fixed). The constructor 0x161a4a0 sets it to 1.0 (`mov qword [rdi+0x158], 0x186a0` at 0x161a57e). |
| `+0x160` | accumulated penalty for newly joined ships |
| `+0x168` | convoy-escort dominance bonus |
| `+0x170` | fleet AA |
| `+0x148`, `+0x150` | screening and carrier screening |
| `+0x130` | commanding admiral: the unit leader with the highest skill among the side's leaders (0x161e5c0, cached by 0x1620940) |
| `+0xd8` | constructor flag: 1 for the side stored at battle+0x98, 0 for the side at battle+0xa0 (0x15c2913 / 0x15c2941). Most likely "is attacker". |
| `+0xe8` / `+0xf4` | combat groups |

Member (ship) state `member+0xe0`: 1 = active, 4 = still arriving (hours to join in `+0xe4`, set at 0x161aef3/0x161af02,
switched to 1 at 0x1973a49), 8 = retreating (set after the retreat decision, 0x1971bc4), 0x10 = sunk
(0x19739bd, after the kill call), 0x20 = left the battle (0x1975bea). The mask `0x34` (arriving, sunk, left) means "not present".

Tooling caveat: `uses.json`/`who.py` miss many reads (the verifier disassembles from p-12 and loses alignment).
For example it reports 0 uses of MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION and none of
RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR or HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR in 0x161b960, but
all three are read there. I used an unverified raw-disp32 scan (`scratch_pos_xr.py`, `scratch_pos_defx.py`) to list every read.

---

## S-POS: positioning formula

Function 0x161b960 `ComputePositioning(side, out, tooltip)` (the tooltip pointer may be null). The value lands in `+0x158` via 0x161e400.

```
enemy = side.GetOpponent()                       // 0x13e1df0
pos   = BASE_POSITIONING                         // 0x161b99b
if side.countries.size != 0:                     // [side+0x48] != 0, effectively always
    pos += Mod(0x1b2; own country, combat location, own admiral)            // 0x161bb4c
    if battle.hour < COMBAT_INITIAL_DURATION and side.flag_d8 == 0:          // 0x161bb5f..0x161bb72
        pos += Mod(0x5a; own country, combat location, ENEMY admiral)       // 0x161bbb5
    pos += side.convoyEscortDominanceBonus                                   // [side+0x168], 0x161bbd0
pos += DetectionBonus      (S-POS-DETECT, only if > 0)                       // 0x161bd7d
pos -= ShipRatioPenalty    (S-POS-SIZE, only if > 0)                         // 0x161c041
pos -= CarrierRatioPenalty (only if > 0)                                     // 0x161c2e4
pos -= side.newlyJoinedPenalty   ([side+0x160], only if > 0)                 // 0x161c4db
pos  = clamp(pos, 0, 1)                                                      // 0x161c63a..0x161c655
```

- `Mod(id; country, location, admiral)` is 0x15c70b0. It sums the modifier `id` from the country (`country+0x5b8`), from two
  containers reached through the combat location (`[r9+0xc8]`, by state/region id and via 0x15a5560), and from the
  admiral (`admiral+0x418`).
- **Modifier names (inferred, medium confidence).** Modifier ids could not be mapped to names statically, because the names
  only appear in a token table.
  - 0x1b2 is most likely the `positioning` modifier.
  - 0x5a is applied only during the initial phase, using the *enemy* admiral, and only to the side with flag `+0xd8 == 0`
    (probably the defender). Its semantics match `naval_enemy_positioning_in_initial_attack`.
- **Order of terms.** No clamp is applied between terms. All terms are added up first and clamped to [0, 1] once at the end.
  So a detection bonus can offset penalties, but positioning never exceeds 1.
- **Tooltip keys.** `POSITIONING_SURFACE_DETECTION_SUPERIORITY`, `POSITIONING_SHIP_RATIO_PENALTY`, `POSITIONING_CARRIER_RATIO_PENALTY`,
  `POSITIONING_RECENTLY_JOINED_SHIPS`, `CONVOY_ESCORT_POSITIONING_BONUS` and `POSITIONING_PENALTY_INFO` confirm the term list.

**Verdict: DIFFERS.** The spec and engine lack the modifiers, the carrier-ratio term, the newly joined term and the convoy dominance term.
**Confidence: high** for structure and order. **Medium** for the modifier names and for reading `+0xd8` as "attacker".

### When positioning is recomputed

- Hourly battle tick 0x15c3600:
  1. ... per-side joins (0x15c52b0)
  2. `0x1620940(sideA)` and `0x1620940(sideB)`: decay the newly joined penalty, rebuild groups (0x1622360)
  3. tail-jumps to 0x13e28c0
- 0x13e28c0 calls `vtbl+0xb0`, which is 0x161e400, for side 0x98 (with side 0xa0 as opponent) and then for side 0xa0.
  0x161e400 recomputes positioning, then screening (0x161ccb0), then fleet AA.
- 0x13e28c0 then calls `vtbl+0x98`, then `vtbl+0x70` (with 1.0), then `vtbl+0x100` on both sides. These later calls
  are very likely the firing and other phases.
- So positioning is recomputed **every hour, before firing**, for both sides from the same pre-fire state.
  It is not updated within the hour.
- **COMBAT_INITIAL_DURATION** (6) in positioning only gates the initial-attack modifier 0x5a (`battle+0x44 < 6`).
  Its other uses are in 0x196fe80 (escape speed) and 0x1975280 (retreat).

Confidence: high that the recompute is hourly and happens in 0x161e400. Medium that `vtbl+0x70` is the fire phase.
UNRESOLVED: whether `battle+0x44` is 0 or 1 during the first hour, which shifts the initial window by one hour.

---

## S-POS-DETECT: detection bonus

```
D(side) = Σ over all members m of all groups with ship(m) != null and (m.state & 0x34) == 0
              ship.EffectiveSurfaceDetection()          // 0x161e0d0 / 0x161e0fb, getter 0xc3aae0
diff    = D(own) - D(enemy)                              // fixed
bonus   = trunc(diff / 100)  ×fixed  RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR
bonus   = min(bonus, MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION);  applied only if bonus > 0
```

- **Aggregation is a SUM over every present ship** (active or retreating), not the best ship.
- **The difference is divided by 100 before the factor.** At 0x161bd19 the code does `diff*100000` and then divides by
  1e7 with magic 0xd6bf94d5e57a42bd, `sar 23`. I checked this numerically. With vanilla 0.01:
  `bonus = 0.0001 × (ΣD_own − ΣD_enemy)`, capped at 0.25.
  Example: ten ships at 40 detection against ten at 20 gives +0.02.
- The smaller side gets nothing (no negative term). The enemy's own bonus is computed symmetrically in its own call.
- **0xc3aae0 is the ship's effective surface detection.** It is the getter the ship view uses for
  `STAT_NAVY_SURFACE_DETECTION` (0x1bf18a1 with stat id 0x13; the stat-name table is at 0x30b33f0):
  `max(0, baseSurfaceDetection[ship+0x1b8] × (1 + statMod(0x2a)) + carrierPart)`.
  `carrierPart` (0xc333e0 with index 0x18, STAT_NAVY_CARRIER_SURFACE_DETECTION) is
  `carrier_surface_detection × min(1, planesOnCarrier / deckCapacity)`. Medium confidence on these sub-terms.

**Verdict: DIFFERS.** The engine uses max per side and no /100. **Confidence: high.**

---

## S-POS-SIZE: ship-ratio penalty

```
own   = count of members (all groups, all ship types, incl. carriers/subs) with (state & 0x34) == 0   // 0x161ebd0
enemy = same for the enemy
if enemy > 0 and own >= MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY:          // int compare, 0x161bf69
    ratio = own / enemy                                                    // fixed divide
    pen   = (ratio - 1) ×fixed HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR
    if enemyAdmiral: pen = pen ×fixed (1 + enemyAdmiral.Mod(0x1d0))        // 0x161bfdc..0x161c027, admiral container only
    pen = min(pen, MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO)
    if pen > 0: pos -= pen
```

- Ships are counted **unweighted** (capitals count 1), over **all lines**, including retreating ships.
  Ships still arriving are excluded.
- No explicit `own > enemy` check exists. A ratio below 1 gives a negative penalty, which is then ignored.
  The result is equivalent to the spec.
- The modifier is read from the **enemy's commanding admiral only**. It is almost certainly
  `naval_enemy_fleet_size_ratio_penalty_factor`, the only modifier with these semantics.

**Verdict: MATCHES the spec's shape.** NEW: the enemy-admiral multiplier, and the exclusion of arriving ships from the
count while sunk and departed ships are dropped. **Confidence: high** (modifier name medium).

---

## Carrier-ratio penalty (NEW; spec lists it as open)

Helper 0x161eeb0 `(side, &A, &B)` loops over every group's air-member list (`group+0x58`, count `+0x64`), skipping
members with `byte[m+0x81] != 0`:

```
eff_m   = CarrierSortieEfficiency(m)                 // 0x19786c0 (CARRIER_EFFICIENCY tooltips)
A      += eff_m × planes_m                           // planes_m = int [m+0x5c]
fill_m  = min(1, planesOnCarrier / CARRIER_PLANES_AMOUNT_FOR_POSITIONING)   // 0xc4d910 sums all wings on the carrier (0x161efb0)
B      += fill_m × eff_m ;  n++
B       = B / n          // average over air members, 0x161f062
```

In 0x161b960 (0x161c217..0x161c2e4):

```
(A_en, B_en) = helper(enemy)
if A_en > 0:
    (A_own, _) = helper(own)
    pen = (1 - A_own / A_en) ×fixed HIGHER_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR ×fixed B_en
    pen = min(pen, MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR)
    if pen > 0: pos -= pen
```

This is the efficiency-weighted plane ratio. Only the side with weaker carrier air takes the penalty, which is linear in
`1 − own/enemy` and scaled by the enemy's average deck-fill factor. With vanilla values the maximum is 0.2 × 1.0 = 0.2,
reached when the own side has no carrier air and the enemy's carriers hold at least 50 planes.

**Verdict: NEW.** **Confidence: high** for the formula. Medium for what exactly `planes_m` (`+0x5c`) and the efficiency include;
those belong to the air area.

---

## Newly joined ships (NEW)

- **Increase** (0x161ab40 → 0x161b2ec..0x161b34b). When a fleet joins and `battle.hour > 0`:
  `pen += shipsInJoiningUnit × POSITIONING_PENALTY_FOR_SHIPS_JOINED_COMBAT_AFTER_IT_STARTS` (0.01).
  `pen` is then clamped to at most `MAX_POSITIONING_PENALTY_FOR_NEWLY_JOINED_SHIPS` (0.25).
  The count is the joining unit's whole ship list (`0xd6eeb0(unit)+0xc`), counted when the unit is added. That may be
  before its ships finish arriving.
- **Decay** (0x1620940, called from the hourly tick 0x15c3600 for both sides before positioning is recomputed):
  `if pen > 0: pen = max(0, pen − POSITIONING_PENALTY_HOURLY_DECAY_FOR_NEWLY_JOINED_SHIPS)` (0.05).
- **Effect**: `pos -= pen` (see S-POS).

**Verdict: NEW.** **Confidence: high.**

## Convoy-escort dominance bonus (NEW)

0x161ab40 (0x161ad00..0x161ae52) sets `side+0x168` once, when a unit joins and all of these hold:
- `battle.hour <= 1`
- the bonus is still 0
- the unit's mission is `[unit+0x374] == 4`, probably convoy escort
- the side controls the region (0x10055a0: dominance state == 1)

```
ratio = ownDominance / f(DOMINANCE_CONTROLLED_THRESHOLD_RATIO, competingDominance)   // 0x143c720 helper
bonus = DOMINANCE_EFFECT_ON_POSITIONING_FOR_CONVOY_ESCORT
        × min(1, (ratio − 1) / DOMINANCE_EFFECT_ON_POSITIONING_FOR_CONVOY_ESCORT_MAX_RATIO)
```

The bonus is added to positioning every hour. The tooltip only shows it when it is above 0.01.
**Verdict: NEW. Confidence: medium.** The helper 0x143c720 and the mission enum were not fully decoded.
This bonus is irrelevant for scripted sea battles without missions.

---

## S-POS-DMG: damage

Getter 0x161ed30 returns `DAMAGE_PENALTY_ON_MINIMUM_POSITIONING − D ×fixed pos`, which is `D × (1 − pos)`.
It is used in the damage function 0x19740c0 (0x19741a8..0x19742e4):

```
posShooter = shooterMember.side.positioning          // [[r15+0xe8]+0x38]+0x158
A = 1 − D·(1 − posShooter) + shooter.Mod(0x1b7) − target.Mod(0x1b8)       // additive, via 0x1972570
damage = max(0, trunc(baseDamage(0x1970a90) ×fixed A))
... then the randomness factor (COMBAT_DAMAGE_RANDOMNESS) multiplies this
```

The second product computed there, `B = 1 + shooter.Mod(0x1ba) + Mod(0x1d3) + target.Mod(0x18c)`, multiplies the other output
of 0x1970a90. That output is the critical-chance value later compared to a roll, and positioning does not touch it.

- The factor is **linear**: `1 − 0.5·(1 − pos)`, as the spec says, and it uses the **shooter's** side positioning.
- It is **added** to the shooter's and target's damage modifiers, not multiplied with them.
- It applies to every weapon that goes through 0x19740c0 (all surface shots).

**Verdict: MATCHES** for the positioning part. NEW: additive combination with modifiers 0x1b7/0x1b8, and the clamp at 0.
**Confidence: high.**

## S-POS-SCREEN: screening

In 0x161ccb0 (0x161cdb0..0x161d01c):

```
eff = max(0.01, (1 + Mod(0x1b1; country, location, own admiral)) − SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING·(1 − pos))
screening        = clamp(screens / ceil(SCREEN_RATIO_CAPITALS·(capitals+carriers) + SCREEN_RATIO_CONVOYS·convoys) ×fixed eff, 0, 1)   // 0x161d15d..0x161d27d
carrierScreening = clamp((capitals / needed2) ×fixed eff, 0, 1)                                                                // 0x161d8e4
```

0x1b1 is very likely `screening_efficiency`. 0x1b3, used when screening is 0 and the side has no screens, is very likely `screening_without_screens`.

- The positioning penalty is **subtracted from (1 + screening-efficiency modifier)**, floored at 0.01.
- It multiplies the screen/capital *ratio*, not each ship's contribution.
- Note the `ceil` on the screens-needed count.

**Verdict: MATCHES** for the positioning part (linear, 0.5 at 0%). DIFFERS in detail: the term is additive with the modifier,
has a 0.01 floor, and the needed screens are rounded up (screening area). **Confidence: high.**

## AA penalty

- `AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING` is read **only** by the tooltip code (0x161c9e9, `AA_MODIFIER` line) and by the define loader.
- The real fleet AA (0x161e4d9..0x161e517) is `fleetAA = (Σ ship AA (0xc3a470, STAT_NAVY_ANTI_AIR_ATTACK) over all members) ×fixed SHIP_TO_FLEET_ANTI_AIR_RATIO ×fixed pos`.
  It is multiplied by **raw positioning**, so at 0% positioning fleet AA is 0, not 30% (as the define would suggest).
- That loop applies no member-state filter.

**Verdict: NEW / DIFFERS from what the define suggests. Confidence: high** that the define has no gameplay read.
Medium on how `+0x170` is consumed (air area).

## Sub reveal

Getter 0x161ed70 returns `SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING × (1 − pos)`. It is used in 0x1973521 (0x1973655..0x19736d7)
and 0x1973eea (0x1973fb5..0x1974011, the torpedo-firing reveal):

```
vis = subShip.SubVisibility (0xc3aa20, STAT_NAVY_SUB_VISIBILITY) ×fixed (1 + 1.0·(1 − posOwnSide))
```

`posOwnSide` is the submarine's own side. `vis` is then multiplied by `(1 + Mod(0x1ba/0x1bc))` and fed to the reveal roll
(0x1975400, or the SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER path). With the vanilla 1.0, a submarine side at
0% positioning has double visibility.

**Verdict: NEW** for the spec. **Confidence: high** for the factor. The roll itself belongs to S-SUB.

---

## Recommended engine changes

1. **`PositioningRules.DetectionBonus`**: sum `SurfaceDetection` over all present ships, both active and retreating. Use
   `bonus = min(MAX, (ΣD_own − ΣD_enemy) / 100 × RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR)` and apply it only
   if it is above 0. Put the `/100` in `HardcodedValues` with tag [RE].
   Carriers' detection should include `carrier_surface_detection × min(1, planes/capacity)`.
2. **`FleetSizePenalty`**:
   - Count every present ship of either side, unweighted (as now), including retreating ships and excluding not-yet-arrived ones.
   - Optional `enemyAdmiralFleetSizeFactor`: `pen *= 1 + mod`.
3. **Sum, then clamp once.** `pos = BASE + positioningMod + initialAttackMod + dominance + detBonus − sizePen − carrierPen − joinedPen`,
   then clamp to [0, 1] (the current code already clamps once).
4. **Carrier-ratio penalty** (when air is modelled), per the formula above. It uses `CARRIER_PLANES_AMOUNT_FOR_POSITIONING`
   (NAir) as a per-carrier deck-fill cap averaged over air wings.
5. **Newly joined ships**: keep a per-side `JoinedPenalty`.
   - On a reinforcement joining at hour > 0: `+= ships × 0.01`, capped at 0.25.
   - Each hour before recomputing positioning: `= max(0, x − 0.05)`.
6. **Recompute** positioning, screening and fleet AA at the start of every hour from the pre-fire state, for both sides.
   This matches the current engine. Initial positioning is 1.0.
7. **`DamageFactor`**: keep `1 − D(1 − pos)` with the shooter's positioning. When damage modifiers are added, combine
   additively: `max(0, base × (1 − D(1 − pos) + shooterDmgMod − targetDefMod))`.
8. **`ScreeningContributionFactor`**: change to
   `eff = max(0.01, 1 + screeningEfficiencyMod − 0.5(1 − pos))` and apply it to the ratio. Use
   `ceil(3·(capitals + carriers) + 0.5·convoys)` as the requirement (coordinate with the screening finding).
9. **Fleet AA**: `fleetAA = ΣAA × SHIP_TO_FLEET_ANTI_AIR_RATIO × pos`. Do not use AA_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING;
   document it as tooltip-only [RE].
10. **Sub visibility**: `× (1 + SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING × (1 − ownPos))` in the reveal check.
11. **Spec (`surface.md` S-POS)**: replace the [ASSUMED] "best ship" with [RE] "sum / 100". Add the carrier, joined,
    dominance and modifier terms, the AA and sub effects, and the evidence addresses.
