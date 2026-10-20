# S-WEATHER: the weather terms in naval combat

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-WEATHER, S-HIT-MODS, S-HIT-SHIP, S-RETREAT-SPEED, S-POS, S-NIGHT, A-SORTIE, A-HIT
- **Confidence:** high for the ship modifier sum's weather term, the flag → block mapping, the holder fill and the combat
  readers (read from code); high for the explanation of the in-game hit chances (it reproduces the logged values to four
  digits); medium for the weather roll details (temperature rule, duration), which a simulator does not need.

Follows [S-WEATHER-weather-toggle.md](S-WEATHER-weather-toggle.md), which decoded the console toggle and what a battle reads
each hour (`combat+0xa8` weather type, `combat+0xb0` the region's weather holder, `combat+0xb8` darkness).

## 1. The weather term in the ship modifier sum (0xc38760)

```
// 0xc389cb..0xc38a52, only when the task force has a province (tf+0x1f0)
holder = 0xf19fd0(weatherManager, region(province)+0x58)       // region state + 0x48, null out of range
if holder:
    w      = holder.Get(id)                                     // 0xc327e0
    factor = ship[+0x270] − 1 + clamp(1 + owner.navy_weather_penalty, 0, 1)    // 0xd67310: modifier 0x248, country only
    sum   += w × factor                                         // fixed point, truncated
// 0xc38a55..0xc38a85
sum += 0xf197d0(weatherManager, province.id).Get(id)            // province holder: night × darkness (section 4), no factor
```

`ship+0x270` is ship stat 0x2a (`ship+0x120 + 8 × 0x2a`), `naval_weather_penalty_factor` (token 0x3fcd → stat 0x2a in
0x13e81b0). It is in the additive set of the ship block (0xc3d090), so `ship = variant × (1 + sub) + sub`.

**Default value.** The game's hull archetypes carry `naval_weather_penalty_factor = 1` with the comment "this value gets
-1 before its used so 1.5 will increase the effects of weather penalties by 0.5. this is base value is required for MIO
stat modifiers" (vanilla and TFB `ship_hull_carrier`, `_escort_carrier`, `_cruiser`, `_heavy`, `_light`, `_submarine`,
TFB `ship_hull_carrier_super`). So a normal ship gets **weather × 1**. Modules subtract (TFB/vanilla ice carrier decks
−0.05/−0.1/−0.15), as do MIO policies, doctrines' sub-unit blocks and special projects (`naval_weather_penalty_factor`),
and the country's `navy_weather_penalty` (vanilla spirit −0.4) lowers the clamp term. A hull without the key has stat 0
and gets **weather × 0** (no weather at all, positive or negative) unless the country term is negative, in which case the
factor goes negative: TFB's `ship_hull_super_heavy` archetype has no key, so TFB super-heavy battleships ignore weather.

## 2. How the region's holder is filled

Region weather state (stride 0x160 at `wm+0x40`), flags (block names from the name builder 0xf197f0, static offsets from
the loader 0x60d830):

| state flag | weather | static block (offset) | combat type (`combat+0xa8`, 0x15c5820) |
|---|---|---|---|
| +0x108 | rain light | `weather_rain_light` (+0x28) | 2 |
| +0x110 | rain heavy | `weather_rain_heavy` (+0x30) | 3 |
| +0x118 | snow | `weather_snow` (+0x38) | 4 |
| +0x120 | blizzard | `weather_blizzard` (+0x40) | 5 |
| +0x128 | sandstorm | `weather_sandstorm` (+0x48) | 0 |
| +0x130 | arctic water | `weather_arctic_water` (+0x50) | 0 |

So the logged weather type 2 is **rain light**. Combat type priority: heavy rain, light rain, blizzard, snow, else 0.

```
0xf220d0(state):                                  // called when any flag changed
    clear(state+0x58)                             // the holder's container (holder = state+0x48, container at +0x10)
    for each flag set: add(state+0x58, static block, × 1.0)        // whole block, not scaled
    for each of the region's scripted static_modifiers (strategic region +0x30 → +0x138, stride 0x50) whose trigger
        held at the last update (state+0x30 word per entry): add(state+0x58, modifier, × 1.0)   // "Null weather modifier for region."
```

Rain light, rain heavy, snow and blizzard are exclusive (one roll); sandstorm only when none of them is active; arctic
water independently. So a region can have, for example, rain light + arctic water at once. The region's
`static_modifiers` (e.g. vanilla `water_modifier_shark_infested = always` in Pacific regions) sit in the same holder.

### The roll (0xf20ef0, per region, when the date reaches `state+0x150`)

```
period = 0xf191f0(region, date)                   // "No weather period found for region"; fields below
w = { light: period+0x68, heavy: +0x78, snow: +0x88, blizzard: +0x98 }    // the period's rain_light, rain_heavy, snow, blizzard
T = mean over the region's active weather cells of cell+0x118 (temperature); state+0x138 = T
lo, hi = wm+0x230, wm+0x228                        // weather.txt settings.snowing.temperature = { 1.0 -4.0 } (medium)
t = (clamp(T, lo, hi) − lo) / (hi − lo)
snow, blizzard ×= 1 − t;  if T < hi: light = heavy = 0
w[k] ×= 0xf15b40(state, k)                         // 1 + Σ terrain share × weather.txt terrain_modifiers value (1 at sea)
U = hash(region index + hour + seed) mod 1        // deterministic, not the battle RNG
total = period+0x58 (no_phenomenon) + Σ w
pick the first cumulative weight above U × total in the order light, heavy, snow, blizzard; else none
sandstorm (when none): province desert flag test, then U < period+0xc8 × 0xf15b40(state, 7)
arctic water: U < period+0xa8 × clamp(1 − (T − a)/(b − a), 0, 1), with (a, b) = settings.arctic_water
    arctic_water_temperature (flag off) or arctic_water_end_temperature (flag on)
state+0x150 = now + 0xf15c50(state)               // duration of the new weather, from weather.txt settings.duration
                                                  // (wm+0x290.. pairs per weather, random between min and max, hours)
```

## 3. Which combat readers see the region weather

| Reader | Path | Ship factor of section 1? |
|---|---|---|
| Hit chance `naval_hit_chance`, per-weapon `naval_*_hit_chance_factor` (S-HIT-MODS) | member getter 0x1972570 → 0xc38760 | yes |
| Combat speed `naval_speed_factor` (0xc3a740: `speed × max(0.1, 1 + Σ)`); so the target's hit profile (S-HIT-SHIP) and escape speed | 0xc38760 (id 0x29) | yes |
| Retreat speed `naval_retreat_speed` (0x196fe80) | member getter → 0xc38760 | yes |
| Detection `naval_detection` (0xc3aae0, 0xc3a950) | 0xc38760 (id 0x2a) | yes |
| Attack / armor factors (S-STATS-COMBAT getter 0xc34a50) | 0xc38760 | yes (no vanilla weather key) |
| `positioning`, `screening_efficiency`, `screening_without_screens`, `naval_enemy_positioning_in_initial_attack` | `Mod` 0x15c70b0: country + region holder + naval terrain + admiral | **no** (whole value) |
| Carrier sorties `carrier_traffic` (0x1976f84) | `combat+0xb0` holder: `weather = clamp(1 + carrier_traffic, 0, 1)`, a factor beside night ([AIR](A-air-combat.md)) | no |
| Naval strike targeting `naval_strike` (0x197bf38) | `combat+0xb0` holder: `tMult = 1 + naval_strike(region) + (1 + airfx+0xe8) × naval_strike(night holder)` | no |
| Plane naval attack / targeting / agility (0xf60bc0 / 0xf60f10 / 0xf61280) | region holder × the wing's weather factor ([ACE](A-ACE-aces-and-wing-experience-in-naval-strikes.md)); no weather block has those keys | no |

## 4. The province holder 0xf197d0 is night × darkness

`0xf197d0(wm, province) = wm+0x370[province] (stride 0x330) + 0x270`. The province darkness update 0xf20c50 writes
`+0x240` darkness, `+0x260` the local hour, and refills the holder's container `+0x280` with the static `night` block
(+0x58) × darkness (0xf20dfe..0xf20e3a) when the darkness changed. Nothing else adds to it there, so in the ship
modifier sum night is added whole (no weather factor). The darkness computation itself is decoded in
[S-NIGHT-darkness.md](S-NIGHT-darkness.md).

## 5. The logged TFB weather battles explained

`air_cv_mirror_weather` (Black Sea region, May 1936, TFB, 10 seeds): weather type 2 = rain light. The game's logged
`naval_hit_chance` per ship (modifiers.csv) is −0.05 in rain hours and −0.30 at night in rain, `naval_torpedo_hit_chance_factor`
−0.05 / −0.20: TFB's `weather_rain_light` block whole (factor 1 for both the CV and CL hulls), plus night × darkness.

The game's hit chances are nonetheless above the weather-free engine because TFB's rain light also has
`naval_speed_factor −0.05`, which slows the **target**, and TFB's hit profile `100 × vis / (HIT_PROFILE_SPEED_FACTOR 0.85
× speed + HIT_PROFILE_SPEED_BASE 5)` is dominated by the speed term. For the CL (visibility 17.5, speed 30.99) against
light guns (TFB light gun profile 60):

```
profile² factor: (31.3415 / (0.85 × 30.99 × 0.95 + 5))² = 1.04387² = 1.0897
day:   1.0897 × (1.15 − 0.05) / 1.15 = 1.0423      logged game / engine: 1.0423 (844 of 871 shots)
night: 1.0897 × (0.90 − 0.05) / 0.90 = 1.0291      logged game / engine: 1.0291 (807 of 823 shots)
```

(`1.15` = 1 + the CL's light hit factor 0.15; night adds −0.25. Replay `NavySimulator replay` of seed 1, shots compared
side by side.) The −20 % naval strike damage is the rain's `naval_strike −0.2` in the targeting multiplier. The escape
differences come from the rain's `naval_retreat_speed +0.05` and the slower ships.

## How it was verified

Static reading of 0xc38760, 0xd67310, 0xf19fd0, 0xf197d0, 0xf197f0, 0xf20c50, 0xf20ef0, 0xf220d0, 0xf15b40, 0xf15c50,
0xc3a740, 0x1976db0, 0x197b890. Game files: hull archetypes, `00_static_modifiers.txt`, `common/weather.txt`, strategic
regions. In-game: the logged modifier sums and per-shot hit chances of `output/ingame/air_batch6` (seed 1 replayed),
reproduced to four digits by the formula above.

## Differences from the current spec

- S-WEATHER: weather terms are modelled nowhere; the spec says hit chance, carrier traffic and naval strike. Add: every
  ship modifier read gets `region block × (naval_weather_penalty_factor − 1 + clamp(1 + navy_weather_penalty, 0, 1))`,
  which covers hit chance, combat speed (so the hit profile and the escape speed), retreat speed and detection;
  positioning and screening read the block whole; air reads carrier traffic and naval strike whole.
- S-NIGHT: night × darkness enters the ship sum through the province holder, unaffected by the weather factor (matches
  the engine).

## Unresolved

- Weather temperature thresholds `wm+0x228` / `+0x230` read as `settings.snowing.temperature` (medium); the duration unit.
- `airfx+0xe8` in the night naval strike term (already open in [AIR](A-air-combat.md)).
