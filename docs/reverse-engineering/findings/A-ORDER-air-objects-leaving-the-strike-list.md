# A-ORDER: air objects leaving the group's list (sunk and escaped carriers), and the experience share's steps

- **Game version:** 1.19.3
- **Executable SHA-256:** 7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5
- **Spec items affected:** A-WINGS (strike order), A-STACK (which planes count), A-STATS-XP (correction: the experience
  share is truncated to 0.1, not 0.01), S-REPLAY (the replay reader's air damage)
- **Confidence:** high for the list removal (read in full, 309 logged removals) and the 0.1 step (read in full; the
  logged escort of a land fighter wing over 11 air hours); medium for what 0xc51710 looks up (only its branch that
  removes the air objects was seen in logs).
- **Why it matters:** the constant-roll `air_cv_land_mix` battles (`output/ingame/constant_roll_air_xp/u005`, `u007`)
  still differed after A-XP: the USA land fighters' escort was 0.46 % above the engine's from the second day, and at
  U 0.07 the strikes after a carrier sank came in another order with other AA losses. With the three fixes below both
  battles match every strike (102 of 102, 107 of 107) and every shot.

## A-STATS-XP correction: v in steps of 0.1 (0xf65f20)

```
0xf65fa6  ratio = xp_raw × 100000 / L0_raw                 // xp below the first AIR_WING_XP_LEVELS level
          v_raw = clamp(ratio − 100000, −100000, 0)
0xf6602d  t     = (int)(v_raw × 1000000 / 10000000000)      // v × 10 as an integer, truncated toward zero
          v_raw = t × 100000 × 100000 / 1000000             // back to fixed point: t / 10
```
So `v = trunc(10 × v) / 10`: a fresh wing (xp 0) has v = −1, every xp in (0, 0.1 × L0] gives −0.9, the next tenth
−0.8, and so on (the bonus branch the same). [A-STATS-XP](A-STATS-plane-stats-and-air-modifiers.md#a-stats-xp-the-experience-holder-and-air_untrained_pilots_penalty_factor-0xf65f20)
read it as 0.01.

[LOG] The USA land fighters (wing 264, 57 effective planes, `landwings.csv` of the u005 run) logged escorts 575.04365
(xp 0), 574.082 (xp 0, the next air hour), then 576.64215, 576.27125, … 572.6747 for xp 0.56 to 5.6. Divided by the
engine's escort with the same detection, the game's stat factor is 1.005125 above the xp 0 value at every one of the
nine hours with xp > 0, i.e. flat in xp: with the TFB malus 0.15, `(1 + 2 × 28 × (1 − m) / 500)(1 + 2 × 60 × (1 − m) /
400)` gives that factor for m = 0.135 = 0.9 × 0.15. The 0.01 steps (v −0.99 … −0.94) had made the engine's escort rise
by 0.03 % per step and miss the first step's jump.

## Removing a carrier's air objects (0x1c6ae50 sink, 0x1c6cc50 escape)

The group's air objects (`group+0x58`, count `+0x64`) are the list the strikes walk (0x1c6b560) and the stacking factor
sums (A-STACK). A carrier leaves it at once:

```
sink   0x1975480 (combat sink handler, also reached from the air strike 0x197b890) → 0x1c6ae50(group, member):
           ship = 0x1622d40(member); base = ship+0x730; if none: return
           objs = the list's air objects whose member (+0x10) is this one, in list order
           pool = 0xc51710(base, owner tag of the ship's task force)        // a lookup by country tag
           for each obj in objs:
               if pool: move each of obj's wings into it (0xc648b0); obj stays on the list   // not seen in logs
               else:    remove obj from +0x58 by swap and pop (0x1c6b220: the slot takes the last entry, count −1),
                        0x19795d0(obj), append obj to the escaped list +0x88
           member+0x128 = pool ? its province : −1
escape 0x19739bd → 0x1c6cc50(group, member): for i over +0x58: if list[i].member == member:
           list[i] = list[last], count −1, check slot i again; append the object to +0x88 (A-REGION-ESCAPE)
```
A wing removed from its air object (0x1620270 → 0x1c6c3d0) also swap-pops that object when it is left with 0 planes
(0x1c6c49e), but no logged removal was of that kind.

[LOG] Every logged change of the list (309, in all batches with air object addresses, the invalid s17 run and the
end-of-battle teardown left out) is exactly this swap and pop: 156 for an escaped carrier (state 0x10), 147 for a sunk
one (0x20), with one, two or three objects, and 6 with two sunk carriers between two logged hours. Example (u007, carrier 5633
sinks in hour 89): `[5633 bombers, 5633 fighters, 5635 b, 5635 f, 5637 b, 5637 f, 5639 b, 5639 f, land]` becomes
`[land, 5639 f, 5635 b, 5635 f, 5637 b, 5637 f, 5639 b]`, so the land bombers strike before the carriers.

**Stacking** (A-STACK): the sum of the side's carrier air objects' counted planes runs over this list, so a sunk or
escaped carrier's planes stop counting at once. [LOG] The defender's factor (logged per strike as `stacking`) went from
0.89285 to 0.96153 when carrier 5633 with 80 planes sank; with the engine's old sum (all groups) its land bombers'
AA base was ceil(24 × 0.89285) = 22 planes instead of the game's 24, one plane fewer shot down.

## Replay reader: a strike's damage (S-REPLAY)

Logs since 2026-10-20 attach each strike's damage to its entry and also list it in the hour's `damage` array, which
older logs had only. The reader's fallback for old logs (hand out the hour's list in strike order) also ran for a strike
that missed, so a carrier strike without hits took the next strike's damage at the same target when it was below 1 HP
(`damageInt` 0): in u005 the three "mismatched" strikes were the land bombers' damage counted twice. The list now
leaves out every damage record a strike carries itself.

## Rules for the engine

1. `RegionAirWarRules.ExperienceModifier`: `v = trunc(v / 0.1) × 0.1` (`HardcodedValues.AirWingExperienceStep`).
2. `AirPhase` keeps a strike order per side, first the groups as built. `CarrierLeft(side, carrier)` (from
   `BattleSession` when a ship sinks or escapes, and at the start of each strike pass for carriers out of the battle
   that nobody reported, e.g. a state a replay set) swap-pops the carrier's groups in list order. Strikes walk this
   order; the groups off it never strike.
3. Stacking sums the counted planes of the non-external groups still on the order list.
4. The groups themselves stay (the region air war, positioning, interception and deck crowding read them as before).

## Verification

- Constant-roll air battles, free replay from the game's first hour: `air_cv_land_mix` u005 102 of 102 strikes (99
  before, the 3 being the reader's error) and 506 of 506 shots (504), u007 107 of 107 (103 after rule 1, 106 after
  rule 2) and 561 of 561 shots; `air_cv_mirror`, `air_cv_bombers_vs_fighters` (105 of 105 at U 0.07, 104 before) and
  `air_land_vs_fleet` every strike and shot.
- The random-dice air replays (`air_batch5`–`7`) give the same strikes and shots as before; the surface constant-roll
  battles still match every shot.
- 100 simulator seeds of `air_cv_land_mix` against the 20 clean game seeds: air HP lost USA 794 ± 287 vs 754 ± 276,
  Japan 794 ± 199 vs 795 ± 204 (before: 794 / 787).

## Unresolved

- What 0xc51710 returns (a pool of the owner on the carrier's base, by country tag?) and when it is set: in that
  branch the sunk carrier's air objects stay on the list with their wings moved. Not seen in any log.
- S-POS (0x161eeb0) says a sunk carrier's air objects stay in the carrier ratio sums with sortie 0 ([LOG] there);
  with the sink removal above they are on the escaped list, so 0x161eeb0 probably walks `+0x88` too and skips escaped
  members by state. Not read; the engine keeps its matched behaviour.
