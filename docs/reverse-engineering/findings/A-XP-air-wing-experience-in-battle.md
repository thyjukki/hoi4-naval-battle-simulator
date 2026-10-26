# A-XP: an air wing's experience during a naval battle

- **Build:** hoi4.exe 1.19.3 (SHA-256 in the index). Fixed point ×1e5, raw integers, products truncated toward zero.
- **Scope:** every change of a wing's experience (`wing+0x218`) in the hours of a naval battle, and when the stats see it.
- **Confidence:** high for the gain and blend formulas (read in full) and the step order; [LOG] every logged wing value
  of two constant-roll battles reproduced; medium for the first-air-hour fuel ratio (mechanism inferred, effect logged).
- **Why it matters:** a fresh wing carries the full experience malus (TFB −0.15 air attack, defence and agility,
  A-STATS-XP). Wings that fight in the region's air war gain experience, the malus shrinks, and from the next air hour
  their region war attack, defence, agility and escort rise (the constant-roll `air_cv_land_mix` battle: the USA land
  fighters' escort 576.64 in game against 573.71 in an engine that keeps experience fixed, from air hour 17).

## The changes

```
gain (0xf5dfa0, wing, g):
    fuel = 0xc543e0(the wing's base entry): clamp(received(+0xf0) / consumption(+0xe0), 0, 1); 1 if consumption <= 0
    add  = trunc(trunc(trunc(g × NAir.FIELD_EXPERIENCE_FACTOR) × fuel) × (1 + owner.air_mission_xp_gain_factor (0x1b5) + ace's))
    xp   = clamp(xp + add, 0, NAir.AIR_WING_XP_MAX)

1. region air war, each country's turn as attacker (0xf81a20, before the fights 0xf7be70):
    totA = Σ attacker entries' effective planes (mission+0xe0); totD = Σ defender weights (0xf80540, d.left at the start)
    g    = trunc(min(1, trunc(totD × NAir.COMBAT_MULTIPLANE_CAP) / totA) × NAir.AIR_WING_XP_AIR_VS_AIR_COMBAT_GAIN)
    gain(wing, g) for every attacker entry: a carrier fighter of a side whose carrier wings fly a region mission is on
    both lists, so it gains twice; defenders gain nothing.
2. ground mission (0xf82110, step 10, land-list missions executing & 0x1f7fa with effective planes > 0, e.g. land
   naval bombers over the battle's region; not for missionBit 0x10000):
    gain(wing, flag bit 14 of mission+0x14 ? AIR_WING_XP_RECON_MISSION_COMPLETED_GAIN : AIR_WING_XP_GROUND_MISSION_COMPLETED_GAIN)
3. any plane count change (0xf63e00(wing, old, xpNew, …), after wing+0x7c = n):
    xp = n > 0 ? max(0, (old × xp + (n − old) × xpNew) / n) : 0          // raw integer division
    losses (RemovePlanes 0xf63580 → 0xf63aa3: region war losses, AA and interception kills in naval strikes, accidents):
      xpNew = xpKilled = trunc(lossF × NAir.AIR_WING_XP_LOSS_WHEN_KILLED)
      lossF = clamp((1 − trunc(f × AIR_WING_XP_LOSS_REDUCTION_OVER_FRIENDLY_TERRITORY_FACTOR)) × (1 + owner modifier 6,
              air_wing_xp_loss_when_killed_factor), 0, 1); f = share of the mission region's provinces the owner or its
              allies control (0 over a sea region, so lossF = 1)   (A-ACCIDENT)
    refills (the daily stockpile, 0xf63fa0 Reinforce → AddAirplanes 0xf5add0(wing, variant, n, xp)):
      xpNew = wing+0x9c8 > 0 ? the global at 0x3335b40 : 0             // 0 in every logged refill
```
**No wing experience** from a naval strike itself (A-ACE) or from a land wing joining a naval combat: 0xf82ca0 calls
0xf64af0(wing, used) → `g = used × NAir.CLOSE_AIR_SUPPORT_EXPERIENCE_SCALE` → 0xf64770, which adds to the **country's**
air experience (0x12a6370) and never writes `wing+0x218` (corrects A-WINGS-EXT / A-ACE, which read it as the join's
wing experience).

## When it counts

Hourly strategic-air update 0xc57d30 (A-REGION step list): step 1 runs the wing hourly update 0xf62750, which rebuilds
the experience holder `wing+0x238` (0xf65f20) from `xp`; step 5 the air war (gains, then fights); step 8 the hour's
region losses (blend); step 10 the ground missions (gain); then the combat manager (naval strikes, whose AA and
interception kills blend again). So the region war of an air hour reads the holder of that hour's start: experience
gained in air hour A acts from the next air hour (A + 8 in the logged battles). The naval strike getters ignore the
holder (A-ACE-STATS); weatherF / nightF read it.

**First air hour** [LOG, mechanism medium]: no wing gained anything in the battle's first air hour (both logged runs,
every wing, region war and ground missions alike), while every later air hour gave the full gain. The fuel ratio of
0xf5dfa0 is the only factor that can be 0 there: the wings start executing their missions in that hour, so the base
entry's consumption becomes positive while its received fuel (set by the country's fuel distribution, 0x10f3080 →
0xc4bb10, from the need of an earlier pass) is still 0. The land wings' effectiveness of that hour was 1, so it is read
before the consumption is updated. From the second air hour on the ratio is 1 for a country with fuel in stock.

## Engine state and order

Per wing `xp` (start: the save's `experience`, 0 in make_save.py saves). In an air hour, after the whole region air war
(fights and escorts) of that hour, so this hour's stats keep the old value:
1. each attacker entry's gain (a wing listed twice gains twice; not in the battle's first air hour);
2. the region losses' blend with xpKilled;
3. the ground mission gain of land naval-strike wings with effective planes > 0 (not in the first air hour);
4. during the naval hour, every plane lost to AA or interception: the blend with xpKilled;
5. at the day change, refilled planes: the blend with xpNew 0.
The experience malus (RegionAirWarRules.ExperienceModifier) already follows 0xf65f20: v = clamp(xp / L0 − 1, −1, 0)
below the first AIR_WING_XP_LEVELS level, truncated to 0.01 (0.56 → −0.99), so it only needs the live `xp`.

## Validation

`xp_model.py` (session scratchpad) replays every logged wing from its logged plane counts:
`output/ingame/constant_roll_air_xp/u005` and `u007` (`air_cv_land_mix`, constant rolls, accidents off, clean save):
**4,857 of 4,857** and **7,004 of 7,004** logged values exact (land wings every hour in `landwings.csv`, carrier wings
at the end of every hour). Gains seen: 0.56 (one attacker entry: Japan's carrier bombers, the USA's scrambled carrier
fighters, the USA land fighters), 1.12 (Japan's carrier fighters on both lists), 0.756 = 0.56 + 0.196 (the land
tactical bombers: region war attacker and ground mission); min(1, ·) was 1 in every hour. 52 losses from xp > 0 all ended
at 0 (xpKilled 300 is far above any logged xp), so the blend's exact xpKilled is not tested by them.

## Unresolved

- What `wing+0x9c8` counts (a refill brings the global at 0x3335b40 when it is positive; not seen).
- The first-air-hour fuel ratio: the fuel distribution's timing (0x10f3080) is not read; a wing joining later in a
  battle (its base's consumption starting then) should get the same one-hour gap.
- The ratio term min(1, totD × CAP / totA) below 1 and the xpKilled value are not exercised by a log.
- Ace terms in the gain (0x61a930 on 0x1b5) and AIR_WING_XP_RECON_MISSION_COMPLETED_GAIN (recon only).
