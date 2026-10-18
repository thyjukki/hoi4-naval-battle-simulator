# Findings: target selection and screening (HOI4 1.19.3 hoi4.exe)

All addresses are RVAs. Fixed point = int64 scale 100000. "trunc" = the usual `imul magic; sar; add sign-bit` idiom, i.e. division by 100000 rounding toward zero.

## 0. Building blocks (needed to read everything below)

### Member state (`member+0xe0`)
The per-ship fire-exchange member has a state word. It is set in the hourly member update 0x1973521 (switch at 0x197375f):
- `1` = waiting: not activated yet. On activation (0x1971610, S-ACTIVATE) it becomes `2` for a ship and `8` for a convoy (0x1973c1b..0x1973c2b: `state = ship ? 2 : 8`).
- `2` = fighting. Fires guns 0..2 (0x1973ac5 loop). Retreat decision 0x19717f0 runs here.
- `8` = retreating. Still fires guns 0..2 (0x19737ac loop, same Fire call 0x1973e20) and accumulates escape progress `+0x130` (0x19738db, 0x196fe80). At progress >= 1.0 the state becomes `0x10` (escaped).
- `4` = countdown state (`+0xe4` counter, then back to 1 or to 0x10); `0x10`, `0x20` = out of battle.
- Mask `0xb` (= 1|2|8) means "ship-like member in battle" and is used by every count and target loop below.
- Convoys go straight to state 8 when they activate, so convoys are always "retreating" for targeting purposes.

### Visibility (`member+0x12c`)
- Initialised to -1 in the member constructors (0x196f2c3, 0x196f47b, 0x196f6f8), so surface ships are always visible.
- Submarines: -1 becomes 0 (hidden) in the hourly update (0x19735e3). While positive it counts down by 1 per hour (0x197360a). It is set to `SUBMARINE_HIDE_TIMEOUT` when the sub reveals itself by firing (0x197408c) or by a detection roll (0x19736eb), and to `SUBMARINE_REVEALED_TIMEOUT` in 0x1c6bbaa. At member creation it is `SUBMARINE_HIDE_TIMEOUT` instead of 0 if combatant flags `+0x10c/+0x10d` are set (0x1c6a9b4..0x1c6a9d9). Other details are for S-SUB.
- Targeting and the per-hour enemy counts require `+0x12c != 0`. So hidden subs (0) cannot be targeted; revealed subs (>0) and surface ships (-1) can.

### Ship class tests (bits of `ship+0x628`)
`c3af10` bit 6 = capital; `c3b210` bit 8 = screen; `c3b220` bit 7 = submarine; `c3af20` (bit 22 or bit 37, plus a set lookup) = carrier; `c3b230` bit 39 = another type (unidentified, not used by vanilla hulls as far as I can tell). Convoys are members with a convoy reference at `member+0x10` (0x535c40) and no ship.
The names come from how each count is used in the screening formula (section S-SCREEN). They also match the weights the wiki gives.

### ECombatBox (0x19723b0): which line a member is in, by state
- States 1/2/8: convoy -> **3**; carrier that is not a sub -> **3**; submarine -> **4** if hidden (`+0x12c == 0`), **5** if visible; screen -> **1**; bit-39 type -> **3**; anything else (capital) -> **2**.
- State 4 or 0x10 -> 7; state 0x20 -> 6; anything else asserts "invalid ECombatBox" and returns 0.
- So box 1 = screen line, 2 = battle line, 3 = carrier line (carriers + convoys), 4/5 = submarines (hidden/revealed).

### Enemy line counts snapshot (0x1c6b560)
For each fire-exchange group, `group+0xa0..0xb8` holds counts of the **enemy** group's members (`group+0x20`) in state mask 0xb **and visible**:
`+0xa0` capitals, `+0xa4` screens, `+0xa8` carriers, `+0xac` visible subs, `+0xb0` invalid, `+0xb4` convoys, `+0xb8` bit-39 types. `+0xc0` counts enemy ships with depth-charge attack > 0.
The snapshot is refreshed from the side update 0x161f810 (call at 0x161f88e), not after each shot. Reach decisions (below) use this snapshot. Candidate lists are built live from the member list.

### Gun index
The hourly loops fire gun 0, 1, 2 in that order (0x1973ad0 / 0x19737b3).
- Gun 0 = heavy: fires if heavy attack (`c3a580`, attack #1) > 0.
- Gun 1 = torpedo: fires if torpedo attack (`c3ac00`, attack #3) > 0.
- Gun 2 = light: fires if light attack (`c3a5d0`, attack #0) > 0 **or** depth-charge attack (`c3a890`, attack #2) > 0. Depth charges therefore share the light slot.
- This matches `BASE_GUN_COOLDOWNS = {heavy, torpedo, light}` and the weight code (gun 2 gets the "light" weights).

### RNG (0x2233db0)
Integer hash of a global counter, 31 bits, `raw = h % 100000`. It returns a fixed value r in {0, ..., 99999}/100000, i.e. [0, 0.99999].

---

## S-SCREEN: screening efficiency
**What the game does** (0x161ccb0, called from 0x161e400 right after positioning; results stored at `combatant+0x148` = screening and `combatant+0x150` = carrierScreening; positioning is `combatant+0x158`):
```
counts over own fire-exchange groups (0x1c6a370, mask 0xb, visibility NOT required):
  C = capitals, S = screens, K = carriers, V = convoys      (subs and the bit-39 type are ignored)
factor = (hasCountry ? 1 + mod_0x1b1 : 1)                    // mod 0x1b1 = screening_efficiency modifier (medium confidence on the name)
         - SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING * (1 - positioning)
factor = max(factor, 0.01)                                   // 0x3e8 floor, 0x161d014

if C + K + V == 0: screening = 1.0                           // factor not applied
else:
  need = ceil(SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CAPITALS * (C + K)
              + SCREEN_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * V)   // integer ceiling, 0x161d1b2..0x161d1e9
  screening = clamp(trunc(trunc(S / need) * factor), 0, 1)
  if screening == 0 and hasCountry and <own side has no screens> (0x161ec40):
      screening += max(0, mod_0x1b3)                         // likely screening_without_screens; added after the clamp

if K + V == 0: carrierScreening = 1.0
else:
  need2 = ceil(CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CARRIERS * K
               + CAPITAL_RATIO_FOR_FULL_SCREENING_FOR_CONVOYS * V)
  carrierScreening = clamp(trunc(trunc(C / need2) * factor), 0, 1)
```
**Evidence.** Define reads: SCREEN_RATIO_*_CAPITALS at 0x161d16a / 0x161d459, *_CONVOYS at 0x161d193; CAPITAL_RATIO_*_CARRIERS at 0x161d81b, *_CONVOYS at 0x161d844 (who.py misses some of these); positioning penalty at 0x161ce25/0x161ce48. The counts come from 0x1c6a370 (capital `[0]`, screen `[4]`, carrier `[8]`, convoy `[0x14]`).
**Verdict.** DIFFERS:
1. The required counts are rounded **up to an integer** (one convoy needs ceil(0.5) = 1 screen; one capital plus one convoy needs ceil(3.5) = 4).
2. The positioning factor also includes the `screening_efficiency` modifier and is floored at 0.01. Mathematically it is the same as multiplying each ship's contribution.
3. There is an optional `screening_without_screens` fallback.
4. Counts include ships in every in-battle state (waiting, fighting, retreating), not only "active" ones.
The ratio-times-factor shape matches the spec. **Confidence:** high (formula), medium (modifier names 0x1b1/0x1b3).

### The screening bonus (affects S-HIT-MODS, S-RETREAT-SPEED, A-SORTIE)
0x1972570 is the member stat getter. For a member in box **2 or 3** (battle line or carrier line), each stat `s` gets:
`s += screening * screening_bonus[s]`, plus for box 3 only `+ (1 - (1-screening)*(1-carrierScreening)) * capital_screening_bonus[s]`.
- The combined value comes from 0x161ece0.
- The modifier tables are static modifiers `screening_bonus` (`combatant+0x100`) and `capital_screening_bonus` (`+0x108`), bound at 0x60ed2a / 0x60ed5d.
- Vanilla `common/modifiers/00_static_modifiers.txt`: `screening_bonus = { naval_retreat_speed 0.2, convoy_retreat_speed 0.2, naval_hit_chance 0.4 }` and `capital_screening_bonus = { naval_retreat_speed 0.2, sortie_efficiency 0.1 }`.

So the **screening hit chance bonus is `+0.4 * screening` added to the `naval_hit_chance` stat** of battle-line and carrier-line ships (not screens, not subs). It is data, not a hard-coded constant: read it from the static modifier.
- The carrier line uses the combined `1-(1-s)(1-c)` value for `capital_screening_bonus`, not `carrierScreening` alone.
- Verdict vs spec line 83 / 125: DIFFERS in source (static modifier, not [WIKI] constant). The escape-speed bonus for the carrier line uses the combined value. **Confidence:** high.

---

## S-TARGET: overall algorithm (PickTarget 0x1971cc0, called from Fire 0x1973e20 -> 0x1973ef3)
```
PickTarget(shooter, gun, enemyGroup, arg4 = shooter stat at Ship+0x228 / modifier 0x29, convoy: +0x208):
  if shooter has no light, heavy or torpedo attack > 0: return none
  maxBox = MaxBox(shooter, gun, enemyGroup)                  // 0x1972c00
  minBox = shooter is sub ? SubMinBox(...) : 1
  for each enemy member m (list order):
     require m.visible (+0x12c != 0) and m.state in {1,2,8}
     b = ECombatBox(m)
     if b in 1..3 and (b < minBox or b > maxBox): skip       // box 4/5 (subs) are NOT range-filtered
     if m is a submarine ship:
        allowed if gun == 2 (light/DC slot)
        else allowed only if shooter is a sub and m.state == 8
     w = Weight(...)                                         // 0x1971440
     if w > 0: add (m, w)
  if no candidates or sum <= 0: return none                  // the shot is not fired at anything
  r = rand()  (0x2233db0, line 0x37b);  T = trunc(r * sum)
  if maxW >= T: return first candidate with the max weight
  cum = maxW
  for candidates in order, skipping the max one: cum += w; if cum >= T: return it
  return the max candidate
```
The weighted pick is a normal cumulative pick in which the heaviest candidate comes first (ties go to the earliest). In practice that is plain weight-proportional selection, with a tiny bias from integer rounding.

**There is no branch on the shooter's own state.** Retreating (state 8) shooters run exactly the same code.

### S-TARGET-LIGHT (gun 2), MaxBox 0x1972c92
`maxBox = enemyScreens > 0 ? 1 : (enemyCapitals > 0 ? 2 : 3)`, using the visible-count snapshot. All members in boxes up to maxBox are candidates; with screens present that means screens only.
**Verdict.** "Closest non-empty group" MATCHES. "Carrier and convoy shooters never target the enemy carrier group" is **not in the binary** (no shooter-type check in 0x1971cc0 or 0x1972c00): DIFFERS. **Confidence:** high.

### S-TARGET-HEAVY (gun 0), MaxBox 0x1972dd4
`maxBox = (enemyCapitals == 0 or enemyScreens == 0) ? 3 : 2`. That is the first two non-empty lines.
**Verdict.** MATCHES, but group emptiness comes from the start-of-hour visible-count snapshot, not live. **Confidence:** high.

### S-TARGET-TORP (gun 1), MaxBox 0x1972cb7..0x1972dc5
```
pen = shooter ship stat 0x1bd via 0x1972570 (naval_torpedo_screen_penetration_factor, likely); convoys: pen = 0
s = enemy screening, c = enemy carrierScreening
if pen != 0: s = max(0, 1 - max(0, 1-s) * (1+pen));  c = max(0, 1 - max(0, 1-c) * (1+pen))
r = rand()  (one roll, line 0x3d9)
maxBox = r <= s ? 1 : (r <= c ? 2 : 3)
```
**Verdict.** DIFFERS:
1. One roll, not two independent rolls. The carrier line is reached when `r > max(s, c)`, so it is not chained as `P(1-s) * P(1-c)`.
2. The roll only sets the maximum line: screens (box 1) are always candidates when present, and boxes 2/3 join when reached.
3. There is no "closest group" fallback. If nothing is in range (for example, a roll of 1 with no screens) the torpedo has no target.
4. A screen-penetration modifier scales `(1 - screening)`.
**Confidence:** high (the modifier name is medium).

### S-TARGET-SUB (submarine shooters), SubMinBox 0x1971db4..0x1971e94
The aggressiveness is `ship+0x728` (task force) `+0x394`. The same field is "Aggressiveness level" in 0x1971816 (retreat threshold 0/1 -> LOW, 2 -> MEDIUM, 3/4 -> HIGH; the REPAIR_AND_RETURN_PRIO_* define names notwithstanding, this is not the repair priority). It is the 5 UI aggressiveness settings (Do Not Engage, Engage At Low Risk, Engage At Medium Risk, Engage At High Risk, Always Engage) stored as 0..4, `navy_engagement_rule` in saves.

`carrierLine = visible enemy carriers + convoys + bit-39 types`; `capOrCarrier = carrierLine + capitals`:
- level <= 1: `minBox = carrierLine > 0 ? 3 : (capOrCarrier > 0 ? 2 : 1)`
- level == 2: `minBox = capOrCarrier > 0 ? 2 : 1`
- level >= 3: `minBox = 1`

The filter is `minBox <= box <= maxBox` for boxes 1..3. Torpedo maxBox (the screening roll) applies to subs as well. Since convoys and carriers sit in box 3, a low-risk sub fires only when the roll beats `max(screening, carrierScreening)`; otherwise it fires at nothing that hour (min > max). The minimum is relaxed to lower lines only when the higher lines are **empty**, not when they are screened.

**Verdict.** DIFFERS:
1. It is a minimum-line rule with an empty-line fallback (low risk can shoot capitals or screens if no carrier-line ships exist; medium can shoot screens if no capitals or carrier-line ships exist).
2. The screening roll is one roll (as above).
3. The minBox also applies to the sub's gun 2.
4. Subs can only shoot subs with heavy or torpedo guns if the target is retreating (state 8); weight then also needs arg4 > 0, so this is practically never.
**Confidence:** high for the logic; high for the 0..4 level mapping (from the retreat switch and the loc keys `SHIP_ENGAGEMENT_BUTTON_AGGRESSIVENESS_1..5`; confirmed for Always Engage = 4 by a recorded battle, see S-RETREAT [CAL] in surface.md).

### S-TARGET-DC
Depth charges are not a separate gun: gun 2 fires if light **or** depth-charge attack > 0. With gun 2, **revealed** subs (box 5) are always candidates alongside the closest surface line (subs are not range-filtered). Their weight is 4 only if the shooter's arg4 stat (`Ship+0x228`, modifier 0x29; convoys `+0x208`) is > 0, else 0. Hidden subs are never candidates.
**Verdict.** DIFFERS: the light and DC slot is shared, and only revealed subs count (the spec says all subs are revealed and only DCs target them). Which attack value (light or DC) is used for damage against a given target belongs to the damage function and is outside this area. The identity of the arg4 stat is UNRESOLVED; sub detection is likely. **Confidence:** high (targeting), low (arg4 name).

### S-TARGET-WEIGHT (0x1971440)
Ship target:
```
base = capital 2 | screen 3 | sub (4 if arg4 > 0, else weight 0) | other (carrier, bit-39) 1
gun 0 or 1 (heavy/torpedo): if capital or carrier: base *= 15        -> capital 30, carrier 15, screen 3, sub 4
gun 2 (light/DC):           if screen: base *= 2                     -> capital 2, carrier 1, screen 6, sub 4
dmg = 2 - hpRatio            hpRatio = ship+0x6f8 / max(ship+0x308, 0.001)   (c3ac60)
```
Convoy target:
```
base = shooter is sub ? 40 : 4;   gun 0/1: base *= 15                -> 60 / 600 (light 4 / 40)
dmg = 1 + 10 * (1 - ratio)        ratio = convoy+0x20 / max(convoy+0x18 -> +0x2e8, 0.001)   (1ad81e0; note the factor 10)
```
Then `w = trunc(base * dmg)` and the target state multiplier: state 2 (fighting) **x1.5**, state 8 (retreating) **x0.5**, state 1 (waiting) x1. Each step truncates.

**Verdict.**
- Base table MATCHES the wiki exactly: 30/2, 3/6, 15/1, 60/4, 600/40, sub 4.
- DIFFERS (a): the sub weight needs the shooter's arg4 stat > 0.
- DIFFERS (b): the damage factor for convoys is `1 + 10*(1-ratio)` (up to x11), not `1 + missing`.
- DIFFERS (c): the "retreating x0.5" and "fighting x1.5" are **mutually exclusive states**. A retreating ship never gets x1.5, and a ship that has not activated yet gets x1.
- DIFFERS (d): convoys are in state 8 once active, so they always take x0.5 (effective 30 heavy/torpedo, 300 from subs).
- Ship damage factor `2 - hpRatio` MATCHES "+100% at 0 HP". The +0x6f8 / +0x308 pair is the ship's current/max strength by usage; it was not checked against org.
**Confidence:** high (constants), medium (the convoy ratio fields).

### S-TARGET-RETREATING
**Verdict.** DIFFERS (not present). Retreating shooters (state 8) call the same Fire -> PickTarget path (0x19737ac..0x19738bb) with the same random weighted pick. There is no "only active targets" or "take highest" rule. Retreating targets get x0.5. **Confidence:** high.

### S-TARGET general ("live group membership")
**Verdict.** DIFFERS. The reach decisions (light, heavy and sub minimum line) use the enemy line counts snapshot `group+0xa0..` from 0x1c6b560, which is refreshed in the side update (0x161f810), not after each kill. Candidates are the live member list (sunk or escaped members drop out of states 1/2/8). If the only reachable line is wiped out mid-hour, later shots that hour find no candidate and are not fired. **Confidence:** medium (the refresh frequency of 0x161f810 relative to the firing loop was not traced end-to-end).

---

## Recommended engine changes
1. **ScreeningRules:**
   - Required screens = `ceil(3*(C+K) + 0.5*V)` and required capitals = `ceil(1*K + 0.25*V)`, as integers.
   - `factor = max(0.01, 1 + screeningEfficiencyMod - 0.5*(1-positioning))`, then `screening = clamp(S/need * factor, 0, 1)`; same for carrierScreening with C. Set 1.0 when need is empty (no factor).
   - Count all in-battle ships (waiting, fighting, retreating), excluding subs.
   - Optional: `screening_without_screens` is added when screening == 0 and the side has no screens.
2. **Screening bonus:** apply `+0.4*screening` to `naval_hit_chance` (additive with other `naval_hit_chance`) for battle-line and carrier-line ships. Retreat speed for both lines: `+0.2*screening`; the carrier line also gets `+0.2*(1-(1-s)(1-c))`. Sortie: `+0.1*(1-(1-s)(1-c))`. Load these from `screening_bonus` / `capital_screening_bonus` instead of hard-coding them.
3. **TargetingRules.ReachableGroups:** replace with `maxBox` (light: screens ? 1 : capitals ? 2 : 3; heavy: capitals && screens ? 2 : 3; torpedo: single roll `r` vs penetration-adjusted `s` and `c`) and take every surface line <= maxBox.
   - Remove the "closest group" fallback for torpedoes, and the carrier/convoy light-gun exclusion.
   - Use visible-count emptiness, ideally snapshotted per hour.
4. **Submarine shooters:** the minimum line comes from aggressiveness: level <= 1 -> 3, falling back to 2 or 1 if the higher lines are empty; level 2 -> 2, falling back to 1; level >= 3 -> 1. Intersect with the torpedo maxBox. If the range is empty, no shot. Map the engine's EngageAtLowRisk / EngageAtMediumRisk / EngageAtHighRisk to 1 / 2 / 3.
5. **Depth charges:** merge into the light slot. With the light gun, revealed subs are always candidates (weight 4, needs the shooter's arg4 stat > 0); hidden subs never are. The surface-line range filter does not apply to subs.
6. **TargetWeight:**
   - Use the state multiplier: fighting x1.5, retreating x0.5, waiting x1 (exclusive). Convoys count as retreating once active.
   - Convoy damage factor `1 + 10*(1-ratio)`; ship factor `2 - hpRatio`.
   - Constants into `HardcodedValues` as [RE]: 2, 3, 1, 4, 40 (sub->convoy), x15 heavy/torpedo for capital/carrier/convoy, x2 light for screens, 1.5, 0.5, 10.
7. **SelectTarget:**
   - Remove the retreating-shooter special case.
   - Pick: `T = floor(r*sum)` with r in [0, 0.99999]. Test the heaviest candidate first (`maxW >= T`), then accumulate the others in list order until `cum >= T`.
   - If there are no candidates or sum == 0, the weapon does not fire at anything.
