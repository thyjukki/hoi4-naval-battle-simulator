# A-MIXED: mixed plane variants in a wing

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** A-REINFORCE, A-STATS (A-STATS-CACHE), A-LOSSES, A-REGION, new A-MIXED
- **Confidence:** high. Static reading of the stockpile order, the wing's equipment container, RemovePlanes, the swap
  path and the stat cache, **confirmed by the logs**: the strike agility average of every logged land-wing strike
  (2,147 strikes, 894 of them with a mixed wing) is reproduced to ±2 raw units by "refill with the stockpile's old
  variant, remove the oldest type first, stats = plane-share mix at the start of the hour"; removing the newest
  type first fails 1,100 of them.

Method: `tools/re/` and throw-away scripts. Logs: `output/ingame/air_batch3`, `air_batch5` (`air_land_vs_fleet`,
`air_cv_land_mix`; `raw.jsonl` `airObjects` wing plane counts, `air` strike records with `averages` =
[attack, targeting, agility] of 0x1978e30). Saves: the CL3 template
`light_attack_vs_torp_cruiser_battle_start.hoi4` and the two land scenario saves (same stockpiles). Conventions as in
[A-WINGS](A-WINGS-carrier-wings-and-reinforcement.md) and [A-STATS](A-STATS-plane-stats-and-air-modifiers.md);
fixed point ×1e5, truncation toward zero.

## A-MIXED-STOCK: which variants the day tick gives (A-REINFORCE)

The daily distribution 0xd10310 sorts the country's stockpile (`[country+0xf68]+0x200`, entries {variant, amount})
with a stable sort (insertion sort up to 32 entries, else merge sort 0xd0c970) using **0xbd2420 → 0xbd2430**, then
offers each variant **in that order**, its whole amount, to the distributables that want it (A-WINGS-REINFORCE). The
air status (0xc5c0a0) hands a variant to its wings sorted by score (`priority × 1000 + (type ≠ 0 ? 500 : 0) +
missing`, ties lower wing id), so **the loop is variant-outer, wing-inner**: the best variant goes to all wings first.

Stockpile order, `a` before `b` when the first differing key says so:
```
1. archetype index ([type+0x4d8]+0x418)            ascending       (groups variants by archetype)
2. equipment type level (type+0x3c4, the year)      DESCENDING      newest model first
3. Σ upgrade levels (variant+0x414)                 descending      (0 for both if 0xc97c90(type): designer types)
4. creator (variant+0x1c) is the stockpile's country first         own designs before foreign ones
5. creator country index (0xbb5490)                 ascending
6. variant id (+0xc)                                DESCENDING      the newest variant
```
A wing accepts a variant when (CanReinforce 0xf5cc60) its sub-unit needs the variant's archetype
(`subunit+0x310` need map via 0x100ea10), the variant's allowed missions equal the wing's (`variant+0x410 ==
wing+0x2c`), and its reinforcement preference (`wing+0x9cc`, default 1 "any") allows it. **Nothing prefers the
wing's own variant**, the creator only matters as tie-break 4/5, and other countries' variants (licensed or captured
stock) are used. So the wing gets the newest model available, and older models only once the newer stock is gone.

**Swap (CanSwap 0xf5d0a0 → Swap 0xf64190 → 0xf66dd0):** wings that cannot be reinforced (full, or a carrier whose task
force is in combat; no combat check in this path) but hold a variant worse than the offered one
(**0xbd7dc0**: higher type level; else a score — version byte for 0xc97430 types, else `+0x418 + +0x414 + +0xc4` —
else `+0x4b0`, else higher id) take `n = min(amount, planes)` of the offered variant; at the end of the same daily
distribution 0xf66dd0 returns `n` planes per archetype to the stockpile **worst first** (0xe6f910 sorts the wing's
variants with 0xbd2420 and walks from the end). The plane count is unchanged; old planes are exchanged for new ones.
So a carrier's wings in battle can still get newer planes swapped in at a day tick if the stockpile has better
planes of the same archetype and missions.

The CL3 template's stockpiles (also in `air_cv_land_mix` / `air_land_vs_fleet`):

| Country | Plane variants in stock (variant id, creator, amount) | First offered |
|---|---|---|
| BUL | `fighter_equipment_0` (1977 BUL obsolete, 10004), `fighter_equipment_0` (2486 GRE, 7), `tac_bomber_equipment_0` (2487 GRE, 18) | fighters: BUL's (own), then GRE's |
| ROM | `fighter_equipment_0` (1965 ROM obsolete, 10005), `fighter_equipment_0` (1379 ITA obsolete, 18), `tac_bomber_equipment_0` (1380 ITA obsolete, 3), `transport_plane_equipment_1` (1964 ROM, 10003) | fighters: ROM's, then ITA's |

The `obsolete` flag (`variant+0x424`, settled from the variant writer 0xbe3c80, token 0x306c) is not a sort key.

**Logs:** the land bomber wings fly `tac_bomber_equipment_2`, and the stockpile has only `tac_bomber_equipment_0`
(a different type of the same archetype and missions). ROM's wing gains exactly 3 planes in every run (ITA stock 3)
and BUL's 16–18 (GRE stock 18), all at the 00:00 day tick (`tick % 24 == 13`). So the refill takes another type and
another creator's variant without hesitation.

## A-MIXED-ORDER: how a wing stores its variants

The wing's equipment (`wing+0x1d8`, a CEquipment container) has two arrays:
- `+0x8 / +0x14`: archetype groups, stride 0x18 `{archetype, firstIndex, count, total}`, sorted by archetype index;
- `+0x20 / +0x2c` (= `wing+0x1f8 / +0x204`): the flat entries `{variant, amount}`, stride 0x10.

Add (0x100cce0, from AddAirplanes 0xf5add0 and Swap): a variant of a new archetype is appended at the end; otherwise
0x100e900 finds its place inside the archetype's range with **0xbd1f20** (and 0xbd1fe0 for the reverse test):
```
1. version (variant+0x41c, the `version` of a variant derived from a parent design)   ascending
2. equipment type index (type+0x418, the database index set by the type ctor 0xc924b0) ascending
3. creator country index                                                              ascending
4. origin country index (variant+0x20)                                                ascending
equal on all four → the same entry (amounts add up)
```
So within an archetype the entries run from the **oldest type** (lowest database index; the game's files define
`*_equipment_0, _1, _2 …` in order) to the newest. The main variant 0x100ff30 (used by 0xf60710 / 0xf60720, e.g. the
missile test) is the entry with the largest amount, the first on ties.

## A-MIXED-REMOVE: which planes are lost first (RemovePlanes 0xf63580)

```
left = n
for (variant, amount) in wing+0x1f8 in array order:           // oldest type first
    k = min(trunc(amount), left)
    container.add(variant, −k)                                 // 0x100cce0
    manpower += k × variant.manpower (0xbda170);  left −= k
    if left <= 0: break
wing.manpower(+0x80) −= round(manpower × MANPOWER_LOSS_RATIO_PLANE_SHOT)
planes(+0x7c) = trunc(Σ amount)
XP recomputed (0xf63e00, see A-ACCIDENT-EFFECT); no stat-cache refresh
```
Used by every plane loss (region air war 0xf87550, naval strike losses, accidents, SAM). **The oldest planes die
first**; a wing refilled with old models returns to its original model as it loses planes.

**Logs:** `air_cv_land_mix` s1, BUL's land bombers: at hour 109 the wing goes 86 → 100 planes (+14 GRE
`tac_bomber_equipment_0`, agility 16 vs 25); its strike agility average is 0.47480 = `(86 × 25 + 14 × 16) / 100 × 0.02`.
After losing one plane it is 0.47635 = `(86 × 25 + 13 × 16) / 99 × 0.02` (old plane removed; removing a new one would
give 0.47454). ROM's wing (88 + 3 old) logs 0.49406 and is back at 0.50000 once it has lost 11 planes.

## A-MIXED-STATS: the wing's stats are the plane-share mix (A-STATS-CACHE)

0xf661e0 (refreshed every hour by the wing update 0xf62750, step 1 of the strategic-air hour; also at creation, load
and when the executing mission is chosen; **not** by AddAirplanes or RemovePlanes):
```
total = Σ amount (0x1012920)
for (variant v, amount a) in wing+0x1f8:
    w = a ≤ 0 ? skip : trunc(a × 1e5 / total)          // raw; an overflow guard gives −1
    for each stat s with v.final[s] ≠ 0:
        stat[s] += trunc( trunc((1 + sub[s]) × w) × v.final[s] )
```
confirming `w = variant planes / wing planes` in fixed point (0xf66331: `imul 0x186a0; idiv total`). Every reader goes
through the cache: the naval strike getters 0xf60f10 / 0xf61280 / 0xf60bc0 (via 0xf60970: the mission's array if
`mission_type_stats` made one, else `wing+0x390`) and the strike averages 0x1978e30; the naval interception's
`stat(i)` and speed (`wing+0x580`); the region air war 0xf5e890; the accident reliability (A-ACCIDENT). So a mixed wing
fights with the weighted mean of its variants' stats, applied to all its current planes.

**Timing (logs):** the averages of a strike are those of the wing's mix at the **end of the previous hour**:
- in 94 strike hours where the wing's mix changed during the hour, only the previous hour's mix matches;
- in 32 strikes where the wing had lost planes in the region air war of the same hour (strike `planes` < the
  previous hour's count) with old planes in the mix, the pre-war mix matches and the post-war mix never does.

## Defines and constants involved

| Name | Role |
|---|---|
| `NCountry.REINFORCEMENT_AIRBASE_PRIORITY_DEFAULT` (1) | wing score `priority × 1000` |
| `NAir.MANPOWER_LOSS_RATIO_PLANE_SHOT` (0.1) | manpower of removed planes |
| `NAir.AIR_WING_XP_LOSS_WHEN_KILLED`, `…_REDUCTION_OVER_FRIENDLY_TERRITORY_FACTOR` | XP after losses (A-ACCIDENT-EFFECT) |
| sort keys `type+0x3c4`, `type+0x418`, `[type+0x4d8]+0x418`, `variant+0x414 / +0x41c / +0x1c / +0x20 / +0xc` | stockpile and wing orders |

## Rules for the engine

1. A wing holds planes per variant (plane ID), not one plane type. `AirWing.Plane` becomes the main variant (largest
   count, first on ties) for anything that reads "the wing's plane"; its stats are the plane-share mix
   `Σ trunc(trunc((1 + sub) × trunc(n_v / n)) × stat_v)` recomputed at the start of each hour (before the region air
   war and the strike), not when planes are added or lost.
2. Day tick (`ReinforceDaily`): sort the side's stock by (newer year first, more upgrades, own creator first,
   creator, newer variant id) within each archetype; for each stock entry in that order, give it to the wings that
   can take it (same sub-unit archetype need, same allowed missions; land wings, or carrier wings not in combat),
   sorted by `priority × 1000 + (mission ? 500 : 0) + missing` descending, until the entry is used up. Replace
   `StockCandidates`' "own plane first" with this order.
3. Swap: full wings (and carrier wings in combat) that hold a worse variant exchange up to `min(stock, planes)` planes
   for the better one, the worst planes going back to the stock. With the template stockpiles (only `_0` models)
   no swap happens.
4. Losses (region war, strike, AA, accidents): remove from the variants in ascending equipment type index (oldest
   model first), then creator, then origin.

## Differences from the current spec

- A-REINFORCE (`AirPhase.ReinforceDaily` / `StockCandidates`): the engine refills a wing with its own plane first and
  then others, wing by wing (most missing first), and the wing keeps one plane type and its stats. The game offers
  the newest stock first, variant by variant, mixes variants inside the wing (stats weighted by planes) and kills the
  oldest planes first. In the logged land scenarios the engine overstates refilled wings (old `_0` planes counted as
  `_2`), most visibly BUL's land bombers (agility −5 % with 14 old planes, naval attack 4 vs 12 per old plane).
- A-WINGS's "variant `+0x424` obsolete flag ?" is settled (obsolete, from the writer).

## UNRESOLVED

- What 0xc97c90 / 0xc97430 test exactly (type keyword bits with DLC-gated masks; read as "designer-based type").
- The priority among non-air distributables in 0xd10310 (0xd0bdd0 / 0xd0c650); irrelevant when only the air status
  wants planes.
- The `+0x4b0` key of 0xbd7dc0.
- That `type+0x3c4` is the `year` key (the reader's token switch was not followed; the values order like years).
