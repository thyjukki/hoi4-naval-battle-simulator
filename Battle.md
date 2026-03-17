# Naval Combat Plan (from HOI4 Naval Battle wiki)

Source used: `https://hoi4.paradoxwikis.com/Naval_battle` (text snapshot fetched 2026-03-17).

## Scope Strategy
- Implement in vertical slices; keep each slice playable from `Program.cs`.
- Keep deterministic mode first (fixed target rules), then add RNG as a toggle.
- Match wiki formulas where practical, but keep constants centralized for tuning.

## Current Baseline (already in repo)
- Hourly loop in `NavySimulator/Domain/Battles/BattleSimulator.cs`.
- Simple screening detection by hull ID (`destroyer`, `light_cruiser`).
- Light attack only, equal damage split, sink by HP.

## Phase 1 - Proper Battle Lines + Screening Efficiency
- Add explicit ship roles: `Screen`, `Capital`, `Carrier`, `Submarine`, `Convoy`.
- Split fleet into 4 groups each hour (screening, battle-line, carrier, submarine).
- Implement screening ratio and carrier screening ratio:
  - Full screen target: `3` screens per capital/carrier + `0.5` per convoy.
  - Full carrier screen target: `1` capital per carrier + `0.25` per convoy.
- Convert ratios to efficiencies `0..1`; keep positioning factor as a multiplier hook.
- Apply only effects needed now:
  - Torpedo bypass chance hook: `1 - screeningEfficiency`.
  - Hit chance modifier hook for screened groups (up to +40% at full screening).
- Acceptance: battle logs include group counts, screening ratios, efficiencies.

## Phase 2 - Target Selection + Cooldowns
- Add per-weapon cooldowns:
  - Light/heavy: 1 hour.
  - Torpedo: 4 hours.
- Implement weapon-specific target constraints:
  - Light guns -> closest non-empty valid group.
  - Heavy guns -> first two non-empty groups.
  - Torpedoes -> can bypass based on screening checks.
- Start with deterministic target pick (highest weight target), then optional weighted RNG.
- Acceptance: logs show selected target group and skipped attacks due cooldown/invalid targets.

## Phase 3 - Hit Chance Model
- Implement base hit chances:
  - Guns/torpedoes: 10%.
  - Depth charges: 11%.
- Add ship hit profile:
  - `hitProfile = 100 * visibility / (0.5 * speed + 20)`.
- Add weapon hit profiles:
  - Light 45, Heavy 80, Torpedo 100, Depth charge 100.
- Hit chance floor: 5%.
- Add modifiers pipeline (night/weather/screening/commander/doctrine) as composable multipliers.
- Acceptance: per-shot logs show final hit chance and hit/miss result.

## Phase 4 - Damage, Armor/Piercing, Organization
- Add piercing-vs-armor threshold tables (damage and crit multipliers).
- Apply damage to HP and Org using wiki proportions:
  - HP receives 60% of final damage.
  - Org receives 100% of final damage, with low-HP scaling behavior.
- Add damage randomness (`-2.5%` to `+47.5%`).
- Acceptance: battle logs show raw damage, multipliers, final HP/Org deltas.

## Phase 5 - Critical Hits
- Base crit chance:
  - Torpedo 10%.
  - Other ship weapons 5% (modified by reliability and piercing outcome).
- Add regular crit damage and part-critical effects as status modifiers.
- Persist critical damage effects in ship state until repair.
- Acceptance: logs show crit roll, crit type, and applied temporary/permanent penalties.

## Phase 6 - Submarine Reveal/Hide Loop
- Hidden submarines are untargetable except by reveal logic.
- Implement reveal timers:
  - Defender-start reveal window.
  - Torpedo-fire reveal chance.
  - Passive hourly reveal chance.
- Add hide timeout behavior.
- Acceptance: logs show reveal state transitions and anti-sub interactions.

## Phase 7 - Carrier and Naval Strike Layer
- Add sortie cadence and initial-stage behavior (carriers act first).
- Implement naval strike targeting weights by class/HP/AA.
- Add targeted ship AA + fleet AA reduction model.
- Add carrier stacking and sortie efficiency hooks.
- Acceptance: logs show strike waves, plane losses, and ship damage from air.

## Implementation Notes for This Repo
- Extend `NavySimulator/Domain/Stats/ShipStats.cs` before adding resolver complexity.
- Keep combat orchestration in `NavySimulator/Domain/Battles/BattleSimulator.cs`.
- Add focused resolvers as classes, e.g.:
  - `ScreeningResolver`, `TargetingResolver`, `HitChanceResolver`, `DamageResolver`, `CriticalHitResolver`.
- Keep all tunable constants in one place (`NavyCombatDefines`) mirroring wiki/defines values.
- Expand output with structured battle events so later UI/analysis tooling can consume them.

