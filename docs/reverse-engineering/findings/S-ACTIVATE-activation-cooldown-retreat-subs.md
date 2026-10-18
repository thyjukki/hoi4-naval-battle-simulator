# Findings: activation, cooldowns, retreat, escape, submarine hiding (HOI4 1.19.3)

Addresses are RVAs in hoi4.exe 1.19.3. "fixed" = int64 fixed point, scale 100000. Fixed multiply is
`imul` then a magic-number divide by 100000 with truncation toward zero (`0x29f16b11c6d1e109`, `sar 0xe`, add sign bit).
Fixed divide is `a*100000 / b` (idiv, truncating), overflow yields 0xffffffff.
RNG: `random_fixed` at 0x2233db0 returns `(r31 mod 100000)` as fixed, i.e. uniform over {0, 0.00001, ..., 0.99999};
every chance check here is `roll <= chance`, so the real probability is `(chance_fixed + 1) / 100000` (≈ chance).

## Background: member state machine and the hour counter (needed by every item)

- Combat tick 0x15c3690 (CNavalCombat vtable+0x80): `combat.hour (+0x44) += 1` **first**, then both combatants update
  (0x16222e0 → group update 0x1c6b560 → member update 0x1973470 for each member, one call per member per tick).
  So during the k-th hourly tick the counter equals k (k = 1, 2, ...).
- Member state `[member+0xe0]` (bit values): 1 = waiting (not yet fighting), 2 = fighting, 4 = arriving
  (counts down `[+0xe4]` hours, then becomes 1), 8 = retreating, 16 = escaped, 32 = (other, not used here).
  Combat box (0x19723b0): state 4/16 → box 7, 32 → 6; otherwise convoy → 3, carrier (non-sub) → 3, submarine → 4
  (hidden) / 5 (revealed), screen → 1, "bit-39 type" (probably `support_ship`, unconfirmed) → 3, else (capital) → 2.
- Member update order (0x1973470..0x1973c52), per tick: (a) submarine hide/reveal timer step (S-SUB), (b) state switch:
  - state 1: side-wide retreat order check (0x1971760); else if an enemy group exists and the line is active
    (0x1971610) → state 2 (ship) or **8 (convoy: convoys start escaping as soon as they activate)**. No firing this tick.
  - state 2: escape progress `[+0x130] = 0`; if `combat.CanRetreat()` → retreat check 0x19717f0 (if it fires: state 8,
    **no shots this tick**); else for weapon slots 0,1,2 with attack > 0 → fire (0x1973e20).
  - state 8: if enemy group exists and line active → fire all slots (retreating ships keep shooting); then escape step.

## S-ACTIVATE — when lines start firing

**What the game does** (0x1971610 / chunk 0x1971652):
```
h = combat.hour
if h > ALL_SHIPS_ACTIVATE_TIME: return active
count over BOTH sides, members in state {1,2,8} that are visible (reveal timer != 0; non-subs are -1):
   nCarrierish = carriers (non-sub) + bit-39 ships           (count slots 2 and 6)
   nCapitalish = capital ships + submarines                    (count slots 0 and 3)
tCarrier = CARRIER_ONLY_COMBAT_ACTIVATE_TIME (0); tCapital = CAPITAL_ONLY_COMBAT_ACTIVATE_TIME (6); tAll = ALL_SHIPS_ACTIVATE_TIME (8)
if nCarrierish == 0:
    tAll    = tAll + tCarrier - tCapital      (8+0-6 = 2)
    tCapital = tCarrier                        (0)
    if nCapitalish == 0: tAll = tCarrier       (0)
threshold = convoy            -> tAll
            box 3 (carrier grp)-> tCarrier
            box 2,4,5 (capital, subs) -> tCapital
            else (screens, ...) -> tAll
active = h > threshold     (strictly greater)
```
Hidden submarines are not counted (count filter requires `[+0x12c] != 0`, 0x1c6a3df), nor are arriving/escaped ships.
The composition is re-evaluated every tick (a carrier sinking/escaping can shift the thresholds), but a ship that already
reached state 2 never re-checks activation.

**Timing.** Activation flips state 1→2 at the first tick with `hour > threshold`; weapons are processed from the
**next** tick. With carriers present: carriers fire from tick 2, capitals/subs from tick 8, screens/convoys from tick 10.
No carriers but capitals/subs: capitals/subs tick 2, screens tick 4. Neither: everyone tick 2.
In the engine's 0-based hour (h = tick−1): carriers h1, capitals h7, screens h9 (all-present case).

**Evidence.** 0x1971630 hour read, 0x1971634 early-out, 0x197168a/0x19716b0 counts (0x161e2c0 → 0x1c6a370, mask 0xb,
visible-only), 0x19716c4..0x19716f0 threshold shift, 0x197170d..0x1971736 per-box choice and `setg`; state switch
0x1973bf5..0x1973c2b; tick 0x15c36ae.

**Verdict.** DIFFERS. Composition does matter (BattleRules.ActivationDependsOnComposition is closer, but not exact):
when there are no carriers, screens activate at `ALL - (CAPITAL - CARRIER)` = 2 (not CAPITAL = 6); subs count
as capitals (both for their own threshold and for the composition test); both sides are pooled; only visible ships count;
the comparison is strict and firing starts one tick after activation. **Confidence: high** (box/flag identity of
"bit 39" = support ship: low).

## S-COOLDOWN — weapon cadence

**What the game does.**
- Cooldown (0x1972a70, also 0x1972b60 variant): `cd = BASE_GUN_COOLDOWNS[slot]`; if `slot == 1` (torpedo) and the member
  is a ship: `cd *= 1 + naval_torpedo_cooldown_factor` (modifier index 0x1be); `cd = max(cd, MIN_GUN_COOLDOWN)`.
  No other modifiers apply to heavy/light cooldowns.
- Slot order {0 heavy, 1 torpedo, 2 light}: the per-tick loop (0x19737b3 / 0x1973ad0) tests slot 0 with heavy attack
  (0xc3a580), slot 1 with torpedo attack (0xc3ac00), **slot 2 with light attack OR depth-charge/sub attack (0xc3a890)**,
  so depth charges share the light slot and light cooldown.
- Per member (ship) and per slot accumulator `progress[slot]` at `[member+0xf0+8*slot]`, initialised to 0 when the member
  is created (0x196f293). On each tick a slot with attack > 0 is processed (0x1973e20):
  ```
  progress += 1.0
  progress = min(progress, ceil(cd))          # no banking beyond one cooldown
  if progress < cd: no fire
  else: pick target once (0x1971cc0); shot loop 0x19740c0: while progress >= cd: progress -= cd; resolve one shot
  ```
- Consequences: first shot is on the first processed tick for cd ≤ 1 (guns), on the 4th processed tick for torpedoes
  (cd 4). Fractional cooldowns carry over (cd 3.2 → average 1 shot / 3.2 ticks); cd < 1 gives several shots in one tick
  (e.g. cd 0.5 → 2 shots), all at the one target picked for that slot that tick. Progress is not reset by retreating.

**Evidence.** 0x1972aca–0x1972b48 (cooldown), 0x1973e44 (+1.0), 0x1973e55–0x1973ec9 (ceil cap, compare),
0x19742fc–0x1974337 / 0x197485f (while-loop subtract), gun-info cooldown field +0x38 set at 0x1970f7e/0x1971160.

**Verdict.** Index order MATCHES; depth-charge cooldown = light MATCHES the assumption. DIFFERS: (1) first shot timing:
guns at the first active tick, torpedo 3 ticks later (neither "at activation" nor "activation + cooldown"); (2) fractional
cooldowns accumulate and can give >1 shot per hour; (3) `naval_torpedo_cooldown_factor` (torpedo only) is missing.
**Confidence: high** (heavy-vs-light identity of slots 0/2 medium-high; torpedo slot high).

## S-RETREAT — retreat decision

Called only for members in state 2 when `combat.CanRetreat()` = `hour >= COMBAT_MIN_DURATION` (vtable+0x60,
0x15c28e0: `cmp [rcx+0x44], COMBAT_MIN_DURATION; setge`). `who.py` misses this reader. Per tick, in order (0x19717f0):

0. **Side-wide retreat order** (0x1971760): if the own combatant has its retreat flag (`[combatant+0x140]`) set and the
   ship is not exempt (0x15c7580) → state 8 (or 16 if arriving). Also checked in states 1 and 4, without the min-duration
   gate. NEW (player/AI "retreat" order).
1. **HP threshold** by the task force's *aggressiveness* (its engagement rule) `L = taskforce[+0x394]` (the same field the
   submarine targeting rule uses, 0x1971dff; assert text "Aggressiveness level"): jump table 0x1971bd8
   `L 0 (Do Not Engage), 1 (Engage At Low Risk) → REPAIR_AND_RETURN_PRIO_LOW_COMBAT (0.9); L 2 (Engage At Medium Risk) → ..._MEDIUM_COMBAT (0.6);
   L 3 (Engage At High Risk), 4 (Always Engage) → ..._HIGH_COMBAT (0.4)`; L 5 asserts. Convoys (no ship) use HIGH.
   If the ship is a capital ship or a carrier: `thr *= CAPITAL_SHIP_COMBAT_RETREAT_MULT (0.5)` (0x197197a).
2. **Convoy-raiding submarines** (ship is sub and task-force mission `[+0x374] == 3` = CONVOY RAIDING):
   retreat **immediately, no roll**, if `ownSubsInGroup <= round(enemyActiveShipsWithDepthCharges * SUBMARINE_ESCAPE_RATIOS[L])`
   (rounding half away from zero, 0x24ed730; counts from group +0xbc / +0xc0, 0x1c6b7b0 / 0x1c6b5d0). NEW.
3. If `hp / maxHp < thr` (strict; ratio 0xc3ac60 = `[+0x6f8] / max([+0x308], 0.001)`): roll P (below); success → retreat.
   Organisation is not used.
4. (Convoys only: a further check on carrier-box presence/attack capability, not decoded.)
5. **Aggressiveness check** (ships, reached when step 3 did not trigger, including a failed roll — so a second, independent
   roll can happen in the same tick):
   - if `L != 0` and the ship is a sub and the enemy has no active depth-charge ships → no retreat;
   - if `L == 4` → never; if `L == 0` → roll P every tick;
   - else `a = AGGRESSION_SETTINGS_VALUES[L] (0 / 0.6 / 1.25 / 2.0 / 10000)` (0 if task-force flag +0x391 set) `* AGGRESION_MULTIPLIER_FOR_COMBAT`,
     `ratio = min(score(own vs enemy) / max(score(enemy vs own), 0.1), 10)` (0x1989fe0; score 0x1989c60 uses the
     AGGRESSION_*_EFFICIENCY_* defines; stored in group +0x30, 0 for special combats flagged at combat +0x10c/+0x10d);
     if `a * ratio < 1` → roll P.
   NEW: healthy ships of an outmatched task force retreat at P per hour (Engage At Low Risk: own/enemy < 1.67, Medium Risk < 0.8, High Risk < 0.5).
- **Roll P** (0x1975280): `P = COMBAT_RETREAT_DECISION_CHANCE (0.2) × (1 + naval_retreat_chance[ship, idx 0x57])`
  `× (1 + naval_retreat_chance_after_initial_combat[ship, idx 0x58], only if hour > COMBAT_INITIAL_DURATION)`
  `× (1 + naval_enemy_retreat_chance[opponent combatant, idx 0x1b9])`, clamped to [0,1]; success if `roll <= P`.
  Each factor is a truncating fixed multiply. Modifier names: 0x1b9 from the rdata name order (naval_enemy_retreat_chance,
  …, naval_torpedo_reveal_chance_factor = 0x1bc, naval_torpedo_cooldown_factor = 0x1be — consistent); 0x57/0x58 from
  their use (the second is gated by the "initial combat" duration); medium confidence on names.
- On success state = 8 (retreating); escape progress starts from 0.

**Verdict.** DIFFERS: threshold is selected by the task force aggressiveness (engagement rule), not repair priority,
mapping {0,1}→0.9, 2→0.6, {3,4}→0.4 (game file values; the spec text's 0.6/0.3/0.1 and 0.22 are stale, the engine's
defaults 0.9/0.6/0.4 and 0.2 are right); capital ships and carriers use half the threshold; P has three modifier factors;
the deciding ship does not fire that tick; NEW aggressiveness-check and convoy-raid-sub retreat paths and side-wide order.
Gate MATCHES (`hour >= 8`, game counter; engine 0-based hour ≥ 7) but only active (state-2) ships are checked.
`COMBAT_INITIAL_DURATION` only gates the "after initial combat" modifier. `REPAIR_AND_RETURN_UNIT_DYING_STR` is not read
by naval combat code (readers 0xb0f375, 0xd64ebc are elsewhere). **Confidence: high** for structure/thresholds/gates,
medium for modifier names and strength-score internals.

## S-RETREAT-SPEED — escaping

**What the game does** (0x196fe80, daily speed; tooltip strings BASE_ESCAPE_SPEED, ESCAPE_SPEED_COMBAT_DURATION,
ESCAPE_SPEED_FROM_SHIP_SPEED, HIDDEN_SUBS_ESCAPE_BONUS, SUBS_ESCAPE_BONUS):
```
s  = BASE_ESCAPE_SPEED (0.05)
s += min(hour * ESCAPE_SPEED_PER_COMBAT_DAY / 24, MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION)   # continuous in hours
if ship:
    if sub: s += sub_retreat_speed modifier (idx 0x5c)
    else:   s += naval_retreat_speed (0x5b); if hour > COMBAT_INITIAL_DURATION: s += naval_retreat_speed_after_initial_combat (0x59)
    s += shipSpeed * SPEED_TO_ESCAPE_SPEED / 100          # shipSpeed = current max speed incl. modifiers (0xc3a740)
elif convoy: s += convoy_retreat_speed (0x5d) + convoySpeed * SPEED_TO_ESCAPE_SPEED / 100
if revealTimer == 0 (hidden sub): s += ESCAPE_SPEED_HIDDEN_SUB (0.35)
elif sub:                         s += ESCAPE_SPEED_SUB_BASE (0.1)
s = max(s, 0)
```
Screening enters through the modifier getter 0x1972570: for boxes 2 (capitals) and 3 (carrier group, convoys) it adds
`screening × static modifier screening_bonus`, and for box 3 also `carrierScreening × capital_screening_bonus`
(common/modifiers/00_static_modifiers.txt: `screening_bonus = { naval_retreat_speed 0.2, convoy_retreat_speed 0.2, naval_hit_chance 0.4 }`,
`capital_screening_bonus = { naval_retreat_speed 0.2, sortie_efficiency 0.1 }`). So the bonus is **additive**:
capitals +0.2·screening, carriers +0.2·screening +0.2·carrierScreening, convoys +0.2·screening, screens/subs none.
Per tick in state 8: `progress += trunc(s / 24)`; escaped (state 16) when `progress >= 1.0` (0x19738db–0x1973931,
magic 0xdfb23b0979b4b03 = /24). Progress is reset to 0 whenever the member is in state 2.

**Verdict.** DIFFERS: subs add their bonus on top of BASE (not replace it); hidden subs use ESCAPE_SPEED_HIDDEN_SUB;
combat-duration term uses hours/24 (not whole days); screening bonus is additive per role as above (not ×(1+…));
retreat-speed modifiers (incl. "after initial combat") are added. Per-hour = daily/24 and threshold 1.0 MATCH.
Example: 30 kn capital, hour 10, screening 1: 0.05 + 0.00625 + 0.15 + 0.2 = 0.406/day ≈ 59 h to escape.
**Confidence: high** (modifier index→name mapping medium; confirmed by the screening static modifier keys).

## S-SUB — submarine hiding and revealing

Reveal timer `t = [member+0x12c]`: −1 for non-subs; `t == 0` = hidden (box 4), `t > 0` = revealed (box 5).
- On joining a group (0x1c6a7c0): subs start hidden (`t = 0`), or revealed for SUBMARINE_HIDE_TIMEOUT if the combat has
  flag +0x10c/+0x10d (special combat types).
- At combat hour 0 (group update 0x1c6ba97..0x1c6bbb0): if the own combatant flag +0xd8 is 0 and the battle has
  submarines but no capital/screen/carrier/bit-39 ships, own subs get `t = SUBMARINE_REVEALED_TIMEOUT (12)`
  (the define's "defender side" case; side flag meaning not confirmed).
- Every tick, for a sub in state 1/2/8 (0x19735d8..0x1973701): `if t > 0: t -= 1`; then if `t == 0` and the enemy group
  detection `D > 0`: effective visibility
  `V = sub_visibility × (1 + mod 0x19f) × (1 + SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING × (1 − ownPositioning))`
  (0xc3aa20, 0x161ed70; `ownPositioning` = combatant +0x158), roll; revealed if `roll <= chance` → `t = SUBMARINE_HIDE_TIMEOUT (8)`.
- After a sub fires (0x1973f87..0x1974092): same with `V' = V × (1 + naval_torpedo_reveal_chance_factor)` and numerator
  multiplied by SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER (1.1); revealed → `t = SUBMARINE_HIDE_TIMEOUT` (resets).
- Chance (0x1975400): `chance = min(1, D × SUBMARINE_REVEAL_DETECTION_MULTIPLIER (0.065) [× 1.1] / max(SUBMARINE_BASE_STEALTH_VALUE (100) − V, 0.1))`.
- `D` (group +0x48, 0x1c6b837..0x1c6b944): mean sub-detection over the enemy group's active ships with sub detection > 0,
  `× NAVAL_COMBAT_SUB_DETECTION_FACTOR`, plus `NAVAL_COMBAT_AIR_SUB_DETECTION_FACTOR × air term` (+0x50).
- Hidden subs are excluded from activation counts and group stats; escape bonus above.
- The deprecated SUBMARINE_REVEAL_BASE_CHANCE / SUBMARINE_REVEAL_POW / SUBMARINE_BASE_TORPEDO_REVEAL_CHANCE are not read here.

**Verdict.** NEW (the spec has only the "always revealed" stand-in). Example: D 20, visibility 10, positioning 1 →
1.3/90 = 1.4 %/h passive. **Confidence: high** for the timer and formula; medium for the positioning field and the
hour-0 sub-only rule; low for flag +0xd8 meaning.

## Recommended engine changes

1. `WeaponCadenceRules.ActivationHour`: implement the exact rule: pool both sides' visible (non-hidden-sub, present)
   ships; carrierish = carriers (+ support ships), capitalish = capitals + subs; thresholds as above; active iff
   `tick > threshold`; recompute each hour until a ship is active. Ship starts firing the tick after activation.
   With engine hour h = tick − 1: active at `h >= threshold`, first weapon processing at `h >= threshold + 1`.
2. Replace `NextFireHour` with a per-ship, per-slot accumulator: each processed hour `p = min(p + 1, ceil(cd))`;
   `while p >= cd: p -= cd; shoot` (one target per slot per hour). Slot 2 = light + depth charges. Add
   `naval_torpedo_cooldown_factor` to the torpedo cooldown, clamp at MIN_GUN_COOLDOWN. Drop `FirstShotWaitsForCooldown`.
3. `RetreatRules`: key the threshold on task-force aggressiveness (DoNotEngage/EngageAtLowRisk → LOW_COMBAT, EngageAtMediumRisk → MEDIUM,
   EngageAtHighRisk/AlwaysEngage → HIGH), multiply by CAPITAL_SHIP_COMBAT_RETREAT_MULT for capitals and carriers; only active
   ships, gate `tick >= COMBAT_MIN_DURATION`; deciding ship skips its shots that hour; P with the three modifiers.
   Add the aggressiveness check (needs the strength score from 0x1989c60, or a BattleRules variant until decoded),
   the convoy-raid sub rule with SUBMARINE_ESCAPE_RATIOS (add the define + AGGRESSION_SETTINGS_VALUES), and a
   side-wide retreat order. Convoys enter "retreating" when they activate.
4. `EscapeSpeedPerDay`: additive formula above (hours/24 duration term, sub/hidden-sub bonuses on top of BASE,
   additive 0.2 screening bonuses per role from the static modifiers, retreat-speed modifiers); keep `/24` per hour
   and escape at 1.0; order per hour: fire (if active), then advance; progress starts the tick after the decision.
5. S-SUB: add reveal timer per sub (hidden at start), hourly passive reveal and torpedo-firing reveal with the formula
   above, hidden subs untargetable and excluded from activation counting.
6. Spec text `docs/mechanics/surface.md`: update the stale numbers (0.6/0.3/0.1, 0.22, 0.045, 0.95) to the 1.19.3
   values the engine already uses (0.9/0.6/0.4, 0.2, 0.05, 0.5).

## Unresolved
- Identity of ship-type flag bit 39 (counted with carriers, box 3) — probably `support_ship`.
- Exact meaning of combatant +0xd8 and combat +0x10c/+0x10d flags; convoy-only branch in the retreat check.
- Strength score 0x1989c60 internals (aggressiveness ratio) not decoded here.
- Whether slot 2 with both light guns and depth charges fires both in one call (inside 0x19740c0, damage area).
