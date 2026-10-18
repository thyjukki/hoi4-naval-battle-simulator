# S-WEATHER: the weather console toggle and the weather a battle reads

- **Game version:** 1.19.3.0 "Operation Postern"
- **Executable SHA-256:** `7DC947BE34970DA1E1C787BCDDBE7F62B610FF258AEBF8F06AA8B348F3A031D5`
- **Spec items affected:** S-WEATHER, S-HOUR (environment refresh)
- **Confidence:** read from code only. High for the command flow and the combat read; medium for "weather off means no weather in any region" (the rebuilt region state is not decoded).

## Console commands

The console registration function `0x36f0` registers `weather` ("Toggle weather simulation"), `enable_weather`, `disable_weather`, `reloadweather` and `set_weather <weather_type>`. The three toggle commands share handler `0x22e040(out, mode)`, mode 0 = toggle, 1 = enable, other = disable. The weather manager is `gamestate+0x688`.

```
0x22e040(mode):
    wm = gamestate+0x688
    wasDisabled = wm+0x34a                      // only read for mode 0
    Reset(wm)                0xf185a0           // clears cells (+0x10) and region states (+0x40), seed +0x308 = 0,
                                                // +0x348 = 1 (init pending), +0x34a = 0
    Rebuild(wm)              0xf1cb90           // re-creates the cells and one state per region (count from settings +0x2ec)
    MarkAllCellsDirty(wm)    0xf218a0           // list of cell indices at +0x3a8 for the map
    if mode == 1 or (mode == 0 and wasDisabled):
        wm+0x34a = 0
        0xf1f710(wm, seed)                       // logs "_nRandomSeed = %u", runs HourlyUpdate now
        print "Weather simulation is ON."
    else:
        wm+0x34a = 1
        print "Weather simulation is OFF."

CWeatherManager::HourlyUpdate 0xf1c6e0(wm):
    if wm+0x349 or wm+0x34a: log "skipping (_bDisabled=%d)"; return
    ...                                          // init passes (+0x2f8) while +0x348, cell updates 0xf11390, 0xf18ad0
```

So turning weather off resets the weather, rebuilds every region's state from scratch and then never updates it again. Turning it back on runs the update at once.

## What a battle reads

`CNavalCombat` refresh environment `0x15c5820`, every hour (S-HOUR):

```
combat+0xa8 = 0; combat+0xb0 = 0
region = [combat+0x38]+0xc8                     // strategic region of the battle (medium)
state  = RegionWeather(wm, region+0x58)         // 0xf1a000: wm+0x40 + index × 0x160, or null when out of range
if state:
    combat+0xa8 = state+0x110 ? 3 : state+0x108 ? 2 : state+0x120 ? 5 : state+0x118 ? 4 : 0   // weather type
    combat+0xb0 = state + 0x48                  // the region's weather modifier holder
combat+0xb8 = darkness of the province          // 0xf19660, S-NIGHT
```

The air steps read `carrier_traffic` and `naval_strike` from the holder at `combat+0xb0` ([A-air-combat](A-air-combat.md)). Surface hit chance gets the weather's `naval_hit_chance` through the generic modifier gather of the battle location ([S-HIT](S-HIT-hit-chance-and-crits.md), medium: that this is the same holder is not traced). A battle therefore sees weather only through the region's state. With weather off, that state is the freshly rebuilt one, which no hourly update has filled, so the reading is: no weather type and no weather modifiers. Not decoded: whether `0xf1cb90` itself sets the region flags or the holder (it uses the random generator, which the cell set-up may explain).

## How it was verified

Static reading only. In-game check: after `disable_weather`, a sea region's weather tooltip should show no weather, and the battle's hit chance tooltip should list no weather line.

## Differences from the current spec

None in the formulas: weather terms are not modelled. The engine gets `BattleRules.WeatherEnabled` (scenario `rules.weatherEnabled`) for battles recorded with weather off ([surface.md](../../mechanics/surface.md) S-WEATHER).

## Unresolved

- The rebuilt region state (`0xf1cb90`): does it start with no weather and an empty holder?
- Which weather each region flag (+0x108, +0x110, +0x118, +0x120) stands for, and how the hourly update sets them.
- `set_weather` (message "Weather was set to '…'", in `0x2874b0`): not decoded. It could pin a weather for calibration.
- `wm+0x349` (a second skip flag).
