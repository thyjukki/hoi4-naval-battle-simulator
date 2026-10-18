# Hour loop, firing order, fire-exchange groups, battle end, night (HOI4 1.19.3)

Area: S-HOUR, S-ORDER, S-END (plus FEX groups and night). All addresses are RVAs. Fixed point is int64 x100000. The
`*0x29f16b11c6d1e109 >> 78` idiom is a truncating division by 100000 (rounds toward zero, sign-corrected).

Helper scripts: `scratch_dis.py` (chunk-aware disassembler with define names), `scratch_up.py` (caller walk),
`scratch_vcall.py` (find functions with given virtual-call offsets). Raw dumps: `hour_*.txt`.

## 0. Call chain (who drives the hour)

1. Combat manager hourly update `0xbb8ae0`: for every combat, `vt+0x48` (validity, CNavalCombat = `0x15c44e0` -> `0x13e2ba0`,
   checks the two sides' country lists) and, if valid, `vt+0x78` = **CNavalCombat::Update `0x15c3600`**. Once every combat is
   updated, it calls `vt+0x80` = **CNavalCombat::EndHour `0x15c3690`** on each one, and removes the combat (`0xbba1b0`) when that
   returns true. A combat that fails `vt+0x48` is removed without being updated.
2. CNavalCombat vtable at `0x29ddb08` (RTTI `0x2d218e8`). Naval combatant (one side) vtable at `0x29e3e98`.
   CCombat (+0x28) is the **attacker** combatant, (+0x30) is the **defender** (see the 'attacker'/'defender' log at `0x13e3e3d`).
   CNavalCombat keeps the same two objects, typed, at +0x98 / +0xa0. Combat hour counter: CNavalCombat+0x44.

## S-HOUR: per-hour structure

**What the game does.** Pseudocode, with A = attacker and D = defender:

```
CNavalCombat::Update (0x15c3600)
  RefreshEnvironment()            0x15c5820  weather/terrain state (+0xa8, +0xb0), darkness of province (+0xb8) via 0xf19660
  PairFireExchangeGroups()        0x15c55d0  every group with no enemy group gets one from the other side (mutual link group+0x20)
  if not convoy-type combat (+0x10c/+0x10d):
      MergeUnpairedGroups(A), (D) 0x15c52b0  groups still without enemy are merged into another group (0x1621900)
  A.HourPrep(D); D.HourPrep(A)    0x1620940  newly-joined positioning penalty -= POSITIONING_PENALTY_HOURLY_DECAY_FOR_NEWLY_JOINED_SHIPS (floor 0); 0x1622360
  CCombat::Update (0x13e28c0):
    A.vt[0xb0](D); D.vt[0xb0](A)  0x161e400  positioning (0x161b960) -> +0x158, screening (0x161ccb0),
                                             fleet AA = sum(ship AA) * SHIP_TO_FLEET_ANTI_AIR_RATIO * positioning -> +0x170
    A.vt[0x98](D); D.vt[0x98](A)  0x161f810  COMBAT: for each own FEX group: group.Update (0x1c6b560)  (see S-ORDER)
    A.vt[0x70](D,..,1.0); D...    0x161f790  per fleet: leader XP (0xc1aa20, BASE_LEADER_TRAIT_GAIN_XP, FIELD_MARSHAL_XP_RATIO)
    A.vt[0x100](D); D.vt[0x100]   0x1621a70  per-country ship-type tallies plus a modifier-table update (purpose UNRESOLVED; looks like statistics or doctrine/experience bookkeeping)
CombatManager, after all combats updated:
  CNavalCombat::EndHour (0x15c3690): hours(+0x44)++, update fleet distance (+0x118, 0x198a080), end check (S-END)
```

Inside one group's update (`0x1c6b560`), in this order:
1. Clear per-hour counters, count the enemy group's composition (capitals, screens, carriers, subs, convoys) and own
   members with c3a890 > 0 (an anti-sub stat; my reading, not verified).
2. Group sub-detection value: average of members' detection (0xc3a950) * NAVAL_COMBAT_SUB_DETECTION_FACTOR, plus the air part
   (0x1c69e40 * NAVAL_COMBAT_AIR_SUB_DETECTION_FACTOR).
3. **Members loop in array order** (`0x1c6b970`): `member.HourUpdate()` (`0x1973470`). This does sub hide timers, the
   per-ship state machine, the retreat decision, firing and escape progress (below).
4. Remove members that have neither a ship nor a convoy and are not state 0x20 (sunk). Removal swaps with the last element.
5. Decrement air-wing timers (+0xf8); sub reveal (SUBMARINE_REVEALED_TIMEOUT) when only subs fight.
6. If the group has an enemy group: **air phase `0x1c6c580`** (carrier/land air strikes of this side). If no air acted and no
   own member can hurt what is left (the enemy has no capitals, screens, carriers or convoys and no visible member), all
   own members switch to retreating (state 8). This is how a fleet disengages from hidden subs it cannot fight.

Per-ship state machine (member+0xe0), `0x1973470`:

| state | meaning | per-hour action |
|---|---|---|
| 4 | joining (ships added through `0x161ab40` start here) | counter +0xe4 counts down (starts at 0); when <= 0, go to 1. If the fleet has gone, go to 0x10 |
| 1 | waiting / not engaged (convoys start here) | if side-wide retreat, go to 8 (0x10 from state 4); else if the group has an enemy and is activated (`0x1971610`, S-ACTIVATE), go to 2 (ships) or 8 (convoys). **No shot this hour** |
| 2 | fighting | escape progress reset to 0; **retreat decision first** (`0x19717f0` -> `0x1971816` / `0x1975280`). If the ship starts retreating, it stops for this hour. If the group lost its enemy, go to 1. Else fire weapon slots 0, 1, 2 |
| 8 | retreating | **still fires** slots 0, 1, 2 (if the group has an enemy and is activated), then `escape += escapeSpeed(0x196fe80) / 24`; at >= 1.0 go to 0x10 |
| 0x10 | escaped | nothing |
| 0x20 | sunk (set at the killing shot, `0x1975bea`) | nothing; stays in the member list for results |

Weapon slots: 0 = light (stat 0xc3a580), 1 = heavy (0xc3ac00), 2 = torpedo (0xc3a5d0) **or** the 0xc3a890 stat. Slot 2
covers both torpedoes and (I believe) depth charges, with one cooldown timer. The identity of the stats is inferred from
context (medium confidence).

**Evidence.** `0x15c3600`, `0x13e28c0` (the eight virtual calls), vtable slots `0x29e3f48` (0xb0) = 0x161e400, `0x29e3f30`
(0x98) = 0x161f810, `0x29e3f08` (0x70) = 0x161f790, `0x29e3f98` (0x100) = 0x1621a70. Group update `0x1c6b560`: member loop at
`0x1c6b970..0x1c6b9a2`, removal at `0x1c6b9fa`, air at `0x1c6bbd1`. State machine `0x1973759..0x1973c2b`. Escape divisor:
`0x19738fd` magic 0xdfb23b0979b4b03 >> 81 = /2 400 000 on a value x100000, so /24.

**Verdict: DIFFERS.**
- Positioning and screening are computed **once at the start of the hour for both sides**, before any shot (matches the
  engine's BeginHour). They are not recomputed after mid-hour losses.
- There is **no separate retreat phase**. Each ship decides to retreat inside its own update, before it fires. A ship that
  starts retreating does not fire through the fighting path that hour. Retreating ships keep firing every hour until they
  escape. The engine instead runs retreat after the whole surface phase, and retreating ships do not fire (`IsInBattle`).
- Air does not come before surface. **Per side**, surface fire comes first, then that side's air strikes, all inside that
  side's group update: A surface, A air, D surface, D air.
- Ships need at least one hour in state 1 before reaching state 2, so a ship cannot fire in the update where it changes
  1 -> 2. The activation timing itself belongs to S-ACTIVATE.
- Leader XP is part of the combat tick (after firing). Ship XP (`NNavy.UNIT_EXPERIENCE_PER_COMBAT_HOUR`, `0xc338a0`) is
  applied from the task-force update (`0xd705f0` chain), **not** from the combat tick (medium).

Confidence: high for the order of the phases and the state machine; medium for the stat identities and for what 0x1621a70 does.

## S-ORDER: firing order (most important)

**What the game does.**
- **Side by side, attacker first, damage applied immediately.** `CCombat::Update` calls `attacker.vt[0x98](defender)` (all of
  the attacker's groups: every ship fires, then the attacker's air) and only then `defender.vt[0x98](attacker)`. This holds
  every hour. It is not simultaneous and not interleaved.
- **Damage is applied immediately per shot.** In `0x19740c0`, each shot calls `0xc3e0e0(targetShip, strDamage, orgDamage)`.
  That writes HP (+0x6f8) and org on the spot. HP below 0 clamps to 0; damage dealt = HP before - HP after (used for stats).
  Then `0xc3e680` (HP <= 0) is checked. On a kill, `0x1975480` gives kill/assist credit (MIN_TRACTED_ASSIST_DAMAGE_RATIO) and
  sets the target's member state to **0x20**. There is no accumulation buffer.
- **Ships sunk earlier in the hour do not shoot.** The state switch in `0x1973470` has no case for 0x20, so sunk ships do
  nothing. Targeting (`0x1971cc0`, `test al,0xb` at `0x1972021`) only takes members in states 1, 2 or 8 with
  +0x12c != 0 (not hidden). So sunk (0x20) and escaped (0x10) ships cannot be picked, while retreating (8) ships can. The
  attacker therefore gets a real first-strike edge: defender ships it sinks never fire back that hour.
- **No randomisation of the iteration order.** Groups and members are iterated in their array order (insertion order:
  fleet ship order when added; removal is swap-with-last). The group or member update paths contain no shuffle or RNG
  call that affects the order.
- Per ship, the slots fire in the order light, heavy, torpedo/depth charge (`0x19737b3` loop, `ecx` 0..2). Per slot
  (`0x1973e20`): `timer[slot] += 1.0`, capped at `ceil(cooldown)`; if `timer >= cooldown`, **one target** is picked
  (`0x1971cc0`), then `0x19740c0` loops `while timer >= cooldown: timer -= cooldown; shot` **at that same target**. When the
  target sinks, the loop ends (the leftover timer carries over, still capped next hour). The target is chosen once per
  slot per hour, not once per shot.

**Evidence.** `0x13e28f2..0x13e290e` (attacker then defender `vt+0x98`). `0x161f880..0x161f9c3` (group loop),
`0x1c6b970` (member loop), `0x19746d4` (`call 0xc3e0e0`), `0xc3e1aa` (`mov [r15+0x6f8], rdx`), `0x1974731` (dead check),
`0x19748dc` -> `0x1975bea` (`mov [r12+0xe0], 0x20`), loop-back `0x197486a jge 0x1974320`, timer cap `0x1973e44..0x1973ebe`.

**Verdict: DIFFERS** from the engine default (`Interleaved`, random shuffle of all ships) and from `Simultaneous`.
`AttackerFirst` is closest, but needs these changes:
(a) the order within each side is fixed (array order), not ID-sorted or random, and only matters through the shared RNG
stream and target state;
(b) the attacker's air acts before any defender ship fires;
(c) retreating ships fire;
(d) the retreat decision is made per ship right before its own firing;
(e) the target is fixed per slot per hour.

Confidence: high.

## Fire-exchange (FEX) groups

**What the game does.** Each side (combatant) has a list of groups (+0xe8, count +0xf4). A group (`0x1c69af0` ctor) has
members (+0x8 / +0x14), an enemy group link (+0x20, mutual), its side (+0x38), air wings (+0x58 / +0x64), and composition
counters (+0xa0..+0xc0).
- Joining (`0x161e6d0`): if `groups < COMBAT_MAX_GROUPS`, a new group is created. Otherwise the ship joins the existing group
  whose capitals/screens ratio is closest to `BEST_CAPITALS_TO_SCREENS_RATIO`. Fallbacks are the smallest group, or a
  random group (`0x2233fa0`).
- `0x161fbc0`: if `groups > COMBAT_MAX_GROUPS`, the first group is merged into the last (logged as a warning).
- Each hour, `0x15c55d0` pairs every group that has no enemy with an enemy group: the "best" by `0x1c6c040` (a distance
  or score), or, in convoy combats (+0x10c/+0x10d), by the largest `0x1c6a370` value with a random fallback. Groups left
  unpaired are merged into another group (`0x15c52b0`).
- With vanilla `COMBAT_MAX_GROUPS = 1`, each side has **one group** holding all its ships, paired with the other side's
  single group. Targeting only looks at the paired enemy group, so with 1 group FEX has no effect.

**Verdict: NEW** (the spec has no such concept). It needs no engine change while `COMBAT_MAX_GROUPS = 1`. Confidence:
high for max 1 group; medium for the multi-group selection details.

## S-END: end of battle

**What the game does.** After each hourly update, `EndHour 0x15c3690` does `hours++`. Then, for a non-convoy combat, if
`IsDone(attacker) || IsDone(defender)` (`0x161fcf0`), the battle ends (`0x16222e0` cleans up air on both sides) and the
manager removes the combat.

`IsDone(side)` is true when the side has no groups, or **no group has a live member** (`0x1c6b4a0`), or **no group has an
enemy group**. A live member has state & mask != 0 and (ship HP > 0, or convoy value > 0). The mask is 0xb (states 1, 2, 8).
While `hours < NMilitary.COMBAT_MINIMUM_TIME` the mask is 0xf, so joining ships (state 4) also count.

- Retreating ships (state 8) keep the battle alive until they escape (state 0x10).
- **No max-duration cap** was found in the naval tick.
- `NNavy.COMBAT_MIN_DURATION`: **only the define loader references it** (`0x93c1d0`). No game logic reads it in 1.19.3 (unused).
- `NNavy.COMBAT_INITIAL_DURATION` gates two things:
  1. positioning (`0x161bb5f`): while `hours < COMBAT_INITIAL_DURATION` (and the side is not in all-retreat), positioning
     modifier 0x5a is added on top of the base (`0x15c70b0`);
  2. the retreat decision (`0x1975325`): only when `hours > COMBAT_INITIAL_DURATION` is the extra factor `(1 + modifier 0x58)`
     applied to `COMBAT_RETREAT_DECISION_CHANCE * (1 + modifier 0x57)`.
  Neither gates the battle end. Modifier names are UNRESOLVED (indices only).
- `NNavy.COMBAT_CHASE_RESIGNATION_HOURS` is copied into group+0x40 at construction and written to the save, but nothing else
  reads it (unused in the tick, medium).
- Convoy-type combats (+0x10c/+0x10d set): `EndHour` returns false. These end through another path (UNRESOLVED).
- Side-wide disengage: when every group of a side has all members retreating (`0x1c6bfc0`) and the side has non-sub ships,
  `0x161fb10` orders all its members to retreat (+0x140 flag: state 1/2 -> 8, state 4 -> 0x10), unless `0x15c7580` holds for
  a fleet. Also see group step 6 (disengage from targets it cannot fight).

**Verdict: MATCHES** in substance: the battle ends when a side has nothing left fighting. Ships that are still escaping
count as present, which the engine also does if `HasShipsInBattle` includes retreating ships. **DIFFERS** on:
- `MaxHours` (no game cap; keep it only as a safety net);
- COMBAT_MINIMUM_TIME keeping state-4 (joining) ships counted;
- the disengage rule against untargetable subs (NEW).

Confidence: high (end check), medium (convoy path, chase hours).

## Night / time of day

**What the game does.** `0x15c5820` stores the province darkness value (`0xf19660`, the same function land combat compares
against 0.8 to apply BASE_NIGHT_ATTACK_PENALTY) in CNavalCombat+0xb8 each hour. The only naval reader of +0xb8 is carrier
sortie efficiency (`0x1977000`: if darkness > 0, add country modifier 0x23 (carrier night traffic); tooltip
`CARRIER_EFFICIENCY_NIGHT`).

No surface-combat function (hit chance `0x1970b48`, damage `0x19740c0`, targeting, positioning) reads +0xb8, and naval code
calls `0xf19660` nowhere else. A `naval_night_attack` modifier is registered (`0x1092f3`). I could not map its runtime index
to a read, so whether it applies anywhere is UNRESOLVED; nothing in the surface path is conditioned on darkness.

**Verdict: DIFFERS.** The engine applies an [ASSUMED] -25% hit chance from 20:00 to 06:00. The game has **no night effect on
surface fire**. Night only affects carrier air (and it uses a real darkness value, not a clock window). Confidence: medium-high.

## Recommended engine changes

1. **S-ORDER**: make `AttackerFirst` the default and fix it:
   - attacker group update fully first (all ships in fleet order: light, heavy, torpedo/DC per ship; then the attacker's air
     phase when air exists), then the defender;
   - damage applied immediately;
   - sunk ships skipped for the rest of the hour;
   - no shuffle; keep a stable order (fleet/list order; the ID order is an acceptable stand-in);
   - drop `Interleaved`/`Simultaneous` from the default, and mark them [LEGACY] or test-only in the spec.
2. **Per-slot cadence**: one target chosen per weapon slot per hour; inner loop `while timer >= cooldown` shooting that
   target; stop when it sinks; `timer += 1` per hour, capped at `ceil(cooldown)`. Torpedo and depth charge share slot 2
   (one timer) [RE, medium].
3. **Retreat inside the ship update**: for each ship in state Fighting, run the retreat decision right before it fires.
   If it starts retreating, it does not fire that hour. Retreating ships fire every hour, then add `escapeSpeed/24` to
   escape progress (escaped at >= 1). Remove the separate end-of-hour retreat phase.
4. **Join states**: a ship needs one update in "waiting" (state 1) before it becomes Fighting. Fresh ships go
   joining (4) -> waiting (1) -> fighting (2), with activation (S-ACTIVATE) gating 1 -> 2. Check this against the S-ACTIVATE
   findings before changing activation hours.
5. **Night**: disable the surface night penalty (`NightEnabled = false` by default, `NightHitChanceFactor` [ASSUMED] ->
   remove or set 0). Keep darkness only for the future carrier sortie efficiency (A-* spec).
6. **S-END**: end when either side has no live members (states waiting/fighting/retreating with HP > 0); joining counts
   while `hour < NMilitary.COMBAT_MINIMUM_TIME`. Keep `MaxHours` only as a safety net. Note COMBAT_MIN_DURATION and
   COMBAT_CHASE_RESIGNATION_HOURS as unused in 1.19.3. Add the disengage rule: if a side cannot target anything (enemy only
   has hidden subs) and has no air that acted, its ships start retreating.
7. **XP**: leader XP comes from the combat tick, ship XP from the task force's hourly update. The engine's per-hour XP after
   firing is close enough in timing, but should not depend on "fired this hour" without RE evidence (owned by the S-XP
   item).
8. Spec: add a FEX section (COMBAT_MAX_GROUPS = 1 means one group per side) and update S-HOUR with the order above:
   positioning/screening both sides -> A (surface, air) -> D (surface, air) -> leader XP -> hours++ -> end check.
