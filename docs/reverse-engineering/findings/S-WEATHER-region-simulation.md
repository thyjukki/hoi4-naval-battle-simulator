# S-WEATHER-REGION: how a strategic region's weather changes over time

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-WEATHER (simulated weather pattern), S-WEATHER-REGION
- **Confidence:** high for the update schedule (round robin, 7 regions an hour), the roll order, the hash and the duration
  formula, the period lookup and the period field layout (every field offset is confirmed by a reader: temperature
  +0x40/+0x48 in 0xf11390, arctic +0xa8, sandstorm +0xc8, min_snow_level +0xd8); checked against 10 logged battles
  (below). Medium for the temperature that enters the roll (the cell temperature model is only outlined) and for the
  direction of the arctic water ramp.
- Builds on [S-WEATHER-weather-terms.md](S-WEATHER-weather-terms.md) section 2 (flags, blocks, the roll in outline),
  which this file refines: the roll's random number does **not** take the hour; it takes the weather seed, which moves
  every hour.

## The hourly update

```
CWeatherManager::HourlyUpdate 0xf1c6e0(wm, advanceSeed):
    if wm+0x349 or wm+0x34a: return                      // disabled (S-WEATHER toggle)
    if advanceSeed: wm+0x308 += random_int()            // 0x2233fa0: the game's global counter generator (seed 0x3452524,
                                                        // counter 0x3452520), the one combat uses; the hourly tick 0x1df400
                                                        // passes advanceSeed = 1 (0x1dfcf6); the load paths 0xf1cb74 /
                                                        // 0xf1f710 pass 0 and set wm+0x348 (init)
    0xf18800(wm)                                        // province darkness (S-NIGHT finding)
    passes = wm+0x348 ? wm+0x2f8 (init_run_passes = 14) : 1
    for every active weather cell (wm+0x10, 0x180 bytes, +0x28 active): 0xf11390(cell, gs, wm, passes − 1)   // temperature, snow depth, mud water
    0xf18ad0(wm, passes)
    wm+0x348 = 0

0xf18ad0(wm, passes):                                  // "DoUpdatePasses"
    nCells   = init ? all : wm+0x2f0 (provinces_per_update = 584)
    nRegions = init ? all : wm+0x2f4 (regions_per_update = 7)
    for pass = passes − 1 down to 0:
        next nCells cells from cursor wm+0x58 (wraps at wm+0x1c)  → cell update jobs, 0xf21c60
        next nRegions region states from cursor wm+0x5c (wraps at wm+0x4c; state+0x140 = active) → 0xf20ef0(state, gs, wm, pass)
```

So outside the initial passes a region is **visited once every `regionStates / 7` hours**: TFB has 315 region states
(ids 0..314), so every 45 hours; vanilla about the same. A region's weather can change only at a visit. The cursors are
saved (`current_province`, `current_region`), so the visit hours of a region are fixed by the save.

## The roll at a visit (0xf20ef0)

```
0xf20ef0(state, gs, wm, pass):
    now = gs+0x468                                      // date in hours
    if now < state+0x150: return                        // next_weather_change not reached: keep the weather
    region = wm+0x2e0[state+8]
    period = 0xf191f0(region, now − 24 × pass)          // first period whose [start, end] holds the day of year
                                                        // (year and hour stripped); no interpolation; "No weather period found"
    // period fields (fixed point): +0x40/+0x48 temperature min/max, +0x58 no_phenomenon, +0x68 rain_light,
    // +0x78 rain_heavy, +0x88 snow, +0x98 blizzard, +0xa8 arctic_water, +0xb8 mud, +0xc8 sandstorm, +0xd8 min_snow_level
    T = mean of cell+0x118 over the region's provinces' active cells; state+0x138 = T      // saved as region `temperature`
    rl, rh, sn, bz = period rain_light, rain_heavy, snow, blizzard
    if rl + rh + sn + bz + arctic > 0:
        t = (clamp(T, wm+0x230, wm+0x228) − wm+0x230) / (wm+0x228 − wm+0x230)   // settings.snowing.temperature {1.0 −4.0}
        sn, bz ×= 1 − t                                 // full below −4 °C, none from 1 °C
        if T < wm+0x228 (1.0): rl = rh = 0              // no rain below 1 °C
        if arctic > 0:                                  // arctic water, independent of the rest
            (a, b) = state+0x130 ? arctic_water_end_temperature (wm+0x270/0x278) : arctic_water_temperature (wm+0x260/0x268)
            if U1 < arctic × clamp(1 − (T − b)/(a − b), 0, 1): state+0x130 = 1  else 0   // 1 at the cold end (medium)
        rl ×= M(2); rh ×= M(3); sn ×= M(4); bz ×= M(5)  // M = 0xf15b40: 1 + Σ terrain share × weather.txt terrain_modifiers; 1 at sea
        clear flags +0x108..+0x120
        x = U1 × (no_phenomenon + rl + rh + sn + bz)    // weights normalised by their sum; mud, arctic and sandstorm are not in it
        x ≤ rl → rain light (+0x108); ≤ rl+rh → rain heavy (+0x110); ≤ … + sn → snow (+0x118); ≤ … + bz → blizzard (+0x120); else none
    else: clear the four flags
    if no flag: sandstorm (+0x128) = (every province has terrain flag +0xd2 bit 0) and U1 < sandstorm × M(7)   // never at sea
    else: sandstorm = 0
    state+0x150 = now + Duration(state)                 // 0xf15c50, for the weather just picked
    if any flag changed: 0xf220d0 (refill the region's modifier holder, WTT)

Duration 0xf15c50 (hours, truncated):
    (min, max) = settings.duration of the current weather: rain light wm+0x2a0/0x2a8, rain heavy +0x2b0/0x2b8,
                 snow +0x2c0/0x2c8, blizzard +0x2d0/0x2d8, sandstorm +0x2e0/0x2e8, none (no_phenomenon) +0x290/0x298
    duration = 24 × min + min(U1, U2) × 24 × (max − min)            // days in the script, min of two uniforms

U1, U2 = H(wm+0x308 + state+8 + pass)                   // 0xf19f00 is U1 alone; one input for the pick, the arctic test,
                                                        // the sandstorm test and the duration
H(x): A = mixA(x × 0x7eb8da44), B = mixB(x × 0x205ad647) × 0x4ad685b3, r = (A ^ A>>8) − B,
      U1 = fin(r) mod 100000 / 100000, U2 = fin(r − 0x4ad685b3) mod 100000 / 100000
      (fin is the final mix of the global generator 0x2233fa0; exact constants in 0xf15d27..0xf15e36)
```

Consequences:

- The pick and the duration use the same `U1`, so a rain light spell (small `U1`) is short: with TFB's Black Sea weights
  (0.5 / 0.25 / 0.1) rain light needs `U1 ≤ 0.294` and lasts at most `5 + 0.294 × 15 = 9.4` days, rain heavy at most
  `2 + 0.412 × 13 = 7.4` days, and no weather `5 + min(U1, U2) × 7` days with `U1 > 0.412`.
- `settings.duration` is in days (vanilla `weather.txt`: no_phenomenon 5–12, rain_light 5–20, rain_heavy 2–15, snow 5–20,
  blizzard 2–15, sandstorm 2–10). TFB has no `common/weather.txt`, so vanilla's apply.
- A spell ends at the first visit at or after `next_weather_change`, up to 45 hours late; the new roll can pick the same
  weather again.
- The weather seed moves every hour by the game's global random number, so after the first change the weather of a
  battle depends on the run's `multiplayer_random_seed` and on every other random draw: not reproducible outside the game.
- The initial passes on load (14, with the save's seed) roll each region once at `pass = 13` (the later passes return early
  because `state+0x150` was just set), with the period of 13 days earlier.

## The save

A plain-text save keeps the state under `weather={ provinces={ "<id>"={ province temperature temperature_offset } … }
regions={ "<id>"={ region rain_light=yes|rain_heavy=yes|snow=yes|blizzard=yes|sandstorm=yes|arctic_water=yes|mud=yes
temperature next_weather_change="y.m.d.h" } … } current_province current_region seed }`. A save written with weather off
(every save of `tools/ingame` so far) has all temperatures 0, `next_weather_change="2.1.1.1"`, cursors 1 and `seed=0`.
A TFB autosave of 1 June 1936 had 71 regions with rain light, 53 rain heavy, 2 arctic water (temperatures −1.8 and −3.6),
4 sandstorm.

## Check against the log

`air_cv_mirror_weather` (TFB, Black Sea region 30, save of 6 May 1936 12:00 with the blank weather state above, weather
on at load): every one of the 10 runs had weather type 2 (rain light) from battle hour 0 to 183, then a new roll.

- Init roll: `x = seed 0 + region 30 + pass 13 = 43`: `U1 = 0.17168`, `U2 = 0.24715`; `0.17168 × 0.85 = 0.146 ≤ 0.25`, so rain
  light, duration `120 + 0.17168 × 360 = 181.8` hours.
- Visits: cursor 1 at load, 7 states an hour → state 30 at update 5, then every 45 hours: 50, 95, 140, **185**. The first
  visit after 181.8 hours is update 185, i.e. battle hour 184 (battle hour 0 = first update). Matches all runs.
- At hour 184 the seed depends on the run: 5 runs that lasted got no weather, one (s5) rain light again (to its end at
  218); expected shares with TFB May weights 0.59 / 0.29 / 0.12.

## Recommended simulator approximation

Per battle, for the battle's sea region (scenario: region, start date, optional initial weather and hours until its next
change, optional visit phase):

```
period = first period of the region holding the day of year
T = (period.min + period.max) / 2                 // the game adds a bounded random offset (cells); fine at sea
roll(): U1, U2 uniform (own RNG, not the combat dice)
        weights as above (rain only at T ≥ 1, snow/blizzard × (1 − t)), pick by U1 × total, arctic by U1
        duration = 24 × (min + min(U1, U2) × (max − min)) hours, settings.duration of weather.txt (vanilla)
start:  weather and remaining hours from the scenario, else roll() and start at a uniform point of the spell
every hour h: if h ≡ phase (mod V) and h ≥ nextChange: roll(), with V = region states / 7 (45 for TFB), phase uniform
        in [0, V) unless given
```

This is the game's process with the game's weights, durations and change schedule; only the temperature offset and the
global seed path are not reproduced.

## Unresolved

- The cell temperature (`cell+0x118`): base `(min + max)/2` of the period plus terrain offset (weather.txt
  `terrain_modifiers.temperature`), a clamped random walk (`temperature_variation`, `max_temperature_change`) and
  neighbour smoothing (0xf22730, 0xf22420, 0xf21c60), not decoded in detail. Matters only near 1 °C (rain / snow) and
  for arctic water.
- The arctic water ramp direction (which of each settings pair is `a`).
- Whether `0xf15b40`'s terrain list index 6 exists (mud, sandstorm and temperature lists; the roll uses 2, 3, 4, 5, 7).
