# S-NIGHT: province darkness (the day/night terminator)

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-NIGHT, S-HIT-MODS (the `night` block), A-SORTIE, S-STATS-COMBAT (`naval_night_attack × darkness`)
- **Confidence:** high. Read from code, then reproduced **exactly** (to the logged 5 decimals, every hour) in all 10 logged
  TFB battles of `air_cv_mirror_weather` (2,934 combat hours, 1936-05-06 to 1936-06-05), with an emulation of the game's
  fixed point (below).

## What the game does

The weather manager (`gamestate+0x688`) keeps one record per province (`wm+0x370`, 0x330 bytes each, index = province
id, count `wm+0x37c`). Darkness is a per-province value in that record, recomputed every hour by the weather manager's
hourly update. Combat only reads it (`0xf19660` is a leaf: `return record[id]+0x240`).

### Province vectors, once (0xf14fe0, called from 0xf13500 at map set-up)

For each province, from its position `(x, y)` (ints at `[province+0xb8]+0xb8 / +0xbc`, see below) and the map size
`W, H` (`[0x3339d28]+0x40 / +0x44`):

```
tz   = (x - GMT_OFFSET) / W + 1                       // record+0x248, fraction of a day east of Greenwich
lat  = (y / H) × (NORTH_POLE_OFFSET - SOUTH_POLE_OFFSET) + SOUTH_POLE_OFFSET
c    = -cos(lat × π)                                   // record+0x258
r    = 1 - |c|                                         // record+0x250 (not sin: the game's own approximation)
for h in 0..23:                                        // record+0x00 .. +0x23f, 24 × 24 bytes
    a       = 2π' × frac(tz + h / 24)                  // 2π' = 6.28318 (0x9965e), one less than the 2π of sin/cos
    v[h]    = normalize(r × sin a, c, r × cos a)       // 0x24fc2f0
```

### Night direction, every hour (0xf15eb0, called from 0xf18800)

```
d = day of the year (0 = 1 Jan; 365-day years, the date is hours since a base with 8760 per year; changes at 00:00)
J = day of 22 June (172), D = day of 22 December (355)  // 0x533c60(date, 21) with a 0-based day of month
if J <= d <= D:   s = 10 × (d - J) / (D - J) - 5
elif d > D:       s = 5 - 10 × (d - D) / (J + 365 - D)
else:             s = 5 - 10 × (d - (D - 365)) / (J - (D - 365))
θ = s degrees (s / 180 × π)                            // so a ±5° "tilt": −5° at 22 June, +5° at 22 December
n = (0, sin θ, cos θ)                                   // wm+0x350 / +0x358 / +0x360: (0,0,1) rotated about x
wm+0x368 = (s / 5 + 1) / 2                             // season fraction, 0 at 22 June, 1 at 22 December (other readers)
```

### Darkness, every hour (0xf18800 → 0xf20c50 per province, threaded when there are many provinces)

```
F       = DAY_NIGHT_FEATHER                            // 0.024; 0x333d3f0 = F/2, 0x333d3f8 = -F/2, 0x30b1dc8 = 1/F
h       = game hour of day = (date - base) mod 24
dot     = n · v[h]
dark    = clamp((dot + F/2) / F, 0, 1)                 // record+0x240
record+0x260 = trunc(frac(tz + h/24) × 24) + 1         // local hour 1..24 (read by 0xf197b0, not by naval combat)
if dark != record+0x268:                               // cache of the value last applied
    clear the province holder (record+0x270, container +0x280)
    if dark > 0: add static modifier `night` (static block +0x58) × dark to it
    record+0x268 = dark
```

So `night` × darkness reaches the ship modifier sum `0xc38760` through the province holder `0xf197d0` (= `record+0x270`),
which settles the "province holder, content not decoded" item of [S-STATS-COUNTRY](S-STATS-COUNTRY-modifier-sources.md).
Midnight (darkest) is where `frac(tz + h/24) = 0`, i.e. at Greenwich (`x = GMT_OFFSET`) at 00:00 game time.

### Weather off

`CWeatherManager::HourlyUpdate 0xf1c6e0` returns before calling `0xf18800` while `wm+0x349` or `wm+0x34a` (weather
disabled) is set, so nothing writes `record+0x240` after the console's reset/rebuild ([S-WEATHER](S-WEATHER-weather-toggle.md)):
darkness stays 0 and the province holder empty. This is the code behind the [LOG] observation "no night with weather off".

### Fixed point

Everything is fx5 int64 with truncation toward zero (`a × b / 100000`, `a × 100000 / b`). Defines are `round(float32 ×
100000)` (S 17000, N 93000, F 2400; GMT_OFFSET is an int). sin is the 3-term Taylor series `x − x³/6 + x⁵/120`
(`0x24ef700`) on `[0, π/2]` after range reduction with π = 3.14159, π/2 = 1.5708, 2π = 6.28319 (`0x24efd30`; cos
`0x24ef840` adds π/2 first). The sun's sin/cos are clamped to [−1, 1]. `normalize` divides each component by
`isqrt(Σ raw²)` (Newton, truncated to int32). The emulation used for the check (`tools/re/darkness.py`) follows
these steps literally; a double-precision version differs by up to about 0.01 in ramp hours, because one pixel of `x`
moves the ramp by 0.03.

## Defines and constants involved

| Name | Role in the formula |
|---|---|
| `NGraphics.GMT_OFFSET` (2793, int, global 0x3333468) | map x of Greenwich, `tz` |
| `NGraphics.SOUTH_POLE_OFFSET` (0.17, 0x33335d0) / `NORTH_POLE_OFFSET` (0.93, 0x3333690) | map y range → polar angle |
| `NGraphics.DAY_NIGHT_FEATHER` (0.024, 0x3333508) | width of the twilight ramp in `dot` |
| map width / height | `provinces.bmp` size (5632 × 2048 in vanilla and TFB) |
| hard-coded 22 June / 22 December, ±5°, 365-day year | season |

The province position matches, for every province checked, the **truncated mean pixel position** of the province in
`map/provinces.bmp` with y counted from the bottom row (`y = H − 1 − row`), which is also the type-0 line of
`map/unitstacks.txt` (`<id>;0;<x>;<height>;<y>;…`: 8256 → 3300, 1371; 7906 → 3260, 1382). Which of the two the game
loads into `[province+0xb8]+0xb8` is not traced; a mod that edits `provinces.bmp` without regenerating `unitstacks.txt`
would tell them apart.

## How it was verified

`output/ingame/air_batch6/air_cv_mirror_weather_battle_start_20261020_034851/s1..s10/sides.csv` (TFB, region 30 Black
Sea, save date 1936.5.6.12): the logged `combat+0xb8` at combat tick `t` equals the formula at the save time plus `t`
hours (tick 0 ↔ 12:00 on 6 May) in **every** logged hour of every run, for one province of region 30 per run:

| Runs | Province that matches every hour |
|---|---|
| s8 (251 h) | 8256 only; its dusk hour 16:00 gives 0.33208, 0.30249, 0.27208, … 0.03291 on 6–16 May, the logged values to the last digit |
| s2, s3, s6 (714 h, into June), s9 | 7906 (or 5412 / 8131, which give the same 0/1 hours) |
| s1, s4, s5, s7, s10 | 8282 / 8308 / 8334 (dark already at 16:00), not 7906 |

So the battle's province (`combat+0x38`) was not 7906, where the fleets were saved, in 6 of the 10 runs: the fleets meet
somewhere in the region. With 7906 the night is 17:00–03:00 in early May; with the eastern provinces 16:00–03:00.

## Differences from the current spec

S-NIGHT used a fixed window (`rules.nightStartHour`/`nightEndHour`, default 20–6). The game's darkness is a function of
the battle province's position, the date and the hour, with a fractional ramp in at most one hour at dusk or dawn. The
simulator needs, per scenario, the battle province (or its `x, y`), the start date (day of year) and the start hour, plus
`GMT_OFFSET`, the pole offsets, `DAY_NIGHT_FEATHER` (from `common/defines/00_graphics.lua`, NGraphics) and the map size.
The darkness also enters through the province holder (`night` × darkness), which is what S-HIT-MODS and S-RETREAT-SPEED
already read.

## Unresolved

- Whether `[province+0xb8]+0xb8/+0xbc` is computed from `provinces.bmp` or read from `unitstacks.txt` (identical here).
- How `combat+0x38` (the battle province) is chosen when fleets meet; in logged runs it varied within the region.
- Other readers of `wm+0x368` (season fraction) and `record+0x260` (local hour, `0xf197b0`): weather, not naval combat.
- The parallel task path of `0xf18800` (used when the province count exceeds the task split; it hands the same arguments
  to a task object) was not read in detail; the direct path calls `0xf20c50` for provinces 1 .. count − 1.
