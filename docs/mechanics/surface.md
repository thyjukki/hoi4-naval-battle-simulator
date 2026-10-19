# Surface combat spec

Status: implemented (`src/NavySimulator.Engine/Battles/`) and checked against **hoi4.exe 1.19.3** by static analysis. Evidence for every [RE] item is in [`docs/reverse-engineering/findings/`](../reverse-engineering/findings/README.md). Tags are explained in [README.md](README.md). Numbers in parentheses are vanilla 1.19.3 values; the engine reads all of them from `GameDefines`, so loading other defines (or a mod) changes them.

"Tick" is the game's combat hour counter. It is 1 during battle hour 0, 2 during hour 1, and so on.

## S-DESIGN: hulls, module slots and ship types [PDX + ASSUMED]

The game's ship designer rules, read from the script files and the game's own `common/units/equipment/_documentation.md`; none of it is decoded from hoi4.exe yet. Code: `DesignRules` (`Engine/Ships/`), checked by the loader (warnings) and the Designs page. Data: `hulls/`, `modules/` and `ship-types/` are written by `NavySimulator import-ship-data` (GameData `Import/`) from the game and mods, with the game's IDs.
- **S-DESIGN-HULL** [PDX]: a hull is a `ship_hull_*` equipment of a naval archetype (`ship_hull_cruiser`, `ship_hull_heavy`, ...). Its stats are the archetype's values overridden by its own [ASSUMED]: this reproduces all 27 hand-entered TFB hulls, while reading them along `parent` would give TFB's heavy hulls the pre-dreadnought's speed of 18 ([finding](../reverse-engineering/findings/S-STATS-design-stats.md), Unresolved). Equipment of the same archetypes without the `ship_hull_` prefix (`light_cruiser_1`, `battleship_1`, ...) belongs to the tech tree for players without Man the Guns (`technologies/naval.txt`) and is not imported. There is no battleship, battlecruiser, light or heavy cruiser hull: the modules decide (S-DESIGN-TYPE).
- **S-DESIGN-SLOT** [PDX]: `module_slots` lists the slots, each `required` or not, with its `allowed_module_categories`; a module fits a slot if its `category` is allowed, and a required slot must be filled. `module_slots = inherit` and `slot = inherit` take the parent equipment's slots (the archetype's when there is no `parent`); `slot = other_slot` copies an earlier slot. Slots named `fixed_*` are the designer's fixed slots, the rest custom slots.
- **S-DESIGN-FORBID** [PDX]: a module's `forbid_module_categories` excludes those categories from every other slot of the design, e.g. TFB's light cruiser armor forbids `ship_medium_battery` and vanilla's medium battery forbids light batteries.
- **S-DESIGN-LIMIT** [PDX]: `module_count_limit` counts the slots matching any of its conditions (`module`, `category`, `any`, `empty`) and requires `count < n`, `> n` or `= n`; limits are inherited along `parent`, a child's limit with the same conditions replaces the parent's, and `count = any` cancels it.
- **S-DESIGN-TYPE** [PDX + ASSUMED]: the ship type is a naval sub-unit of `common/units`. Candidates are the sub-units whose `need`/`need_equipment` names the hull's archetype; a candidate qualifies if the design has one of its `need_equipment_modules = { any = { ... } }` modules (when listed) and each of its `type`s is among the design's equipment types (the archetype's `type` plus every module's `add_equipment_type`); the highest `priority` wins. TFB tells light from heavy cruisers and battleships from battlecruisers by the armor module; vanilla makes a cruiser with a medium battery (`add_equipment_type = capital_ship`) a heavy cruiser by priority. The type gives the role (`screen_ship` → screen, `capital_ship` → capital, `carrier`, `submarine`) and the organization (`max_organisation`, S-STATS-SHIP). Research, MIO and spirit type filters match the ship type, the hull or the archetype ID. If no type qualifies (the game would refuse the design), the simulator uses the highest-priority candidate whose unit types match, else any candidate, and reports it.
- **Overrides**: a design with `allowAnyModule` may break S-DESIGN-SLOT/FORBID/LIMIT and the required slots; without it, the loader only warns. Modules in slots the hull does not have are an error.
- **Open**: whether the game's sub-unit pick works exactly so (the subset and priority rule is inferred from the scripts, not decoded); the hull stat inheritance (above).

## S-STATS: ship stats before combat [RE]

Finding: [S-STATS-design-stats.md](../reverse-engineering/findings/S-STATS-design-stats.md). Code: `ShipDesign.ComputeStats`, `Ship.ApplyExternalModifiers`.
- **S-STATS-DESIGN** (0xbdfc80): `variant = (hull base + Σ module add_stats + avg + MIO hit chance factors) × (1 + Σ module multiply_stats + MIO + equipment bonuses)`.
  - `avg = Σ add_average_stats / modules declaring the stat`.
  - All multipliers form one additive sum; nothing compounds.
  - Carrier size: `floor(add + avg + multiplier)` (its multiplier is a flat add).
- **S-STATS-MIO**: the MIO's `equipment_bonus` sum. Light, heavy and torpedo hit chance factors are added; every other stat joins the multiplier, so a percentage on a stat the design has 0 of does nothing. The JSON's `statModifiers`/`statMultipliers` split is notation only; both are summed.
- **S-STATS-BONUS**: idea and named equipment bonuses (`add_equipment_bonus`, e.g. TFB special projects) are multiplicative for every stat, hit chance factors included. In the data they are researches with `"equipmentBonus": true`.
- **S-STATS-SHIP** (0xc3d090): technology blocks are sub-unit bonuses. `ship = variant × (1 + tech) + tech` for the additive set: org, HP, reliability, hit chance factors, torpedo damage reduction, torpedo enemy crit chance and naval weather penalty. Organization comes from the sub-unit (the ship type's `max_organisation`, S-DESIGN-TYPE), so it is never scaled.
- **S-STATS-CLAMP**: reliability ≥ 0, production cost ≥ 0.1.
- **S-STATS-PARTS** [RE + CAL] (0xc3d340): a damaged part's `stat_penalties` multiply the stat by `max(0, 1 + count × penalty)` (count: how often that part is damaged; two damaged heavy gun instances at −0.5 leave 0). Every stat takes them. [CAL] In 11 replayed TFB battles: heavy guns ×0.5 and secondaries' light attack ×0.75 in every later shot; a short-circuited radar (`shortcircuited_radar_2`: surface detection −7, hit chance factors −0.025) takes its ship's surface detection to 0, which raised the enemy side's logged positioning by exactly that share of the detection difference (S-POS-DETECT) together with its screening and fleet AA, and lowers the ship's own light and heavy hit chance factors to 0.975 ×. (A first reading, "attacks only", came from part picks in the wrong slot order.) Code: `ShipStats.PartPenaltyBonus`, `Ship.DamagePart`.
- **S-STATS-COMBAT**: attack (`max(0, stat × (1 + Σ attack factors + naval_damage_factor + …))`), armor (`× max(0, 1 + defence factors + naval_defense_factor + …)`) and speed (`× max(0.1, 1 + naval_speed_factor + …)`) modifiers are combat-time, not design stats. Of the attack and armor terms, only the experience ones are modelled: `naval_damage_factor` in attack and `naval_defense_factor` in armor (the armor the piercing tier is read against). Both enter the S-DMG hit multiplier again, so each counts twice [RE + CAL: logged in-game hits, [LOG](../reverse-engineering/findings/LOG-ingame-logging.md)]. `naval_night_attack` and the rest are not modelled. Spirits are still applied as [LEGACY] stat modifiers; their real effects belong here. The ship's own getters read the ship modifier sum `0xc38760` (country modifiers, night × darkness, damaged parts' modifiers; `MemberModifierRules.ShipModifier`, `CombatStatRules`):
  - combat speed (0xc3a740) `speed × max(0.1, 1 + naval_speed_factor)`, read by the hit profile (S-HIT-SHIP) and the escape speed (S-RETREAT-SPEED); admiral and fuel terms are not modelled;
  - maximum organization (0xc3d4a3) `max(0.001, (max_organisation + navy_org) × (1 + navy_org_factor))`, the cap of organization and its regain, and the crew factor's ratio (S-HIT-MODS);
  - the retreat decision chance (S-RETREAT) and organization regain (S-ORG-REGAIN).
- **S-STATS-COUNTRY** [RE + CAL]: country modifiers. A technology's top-level keys (e.g. `smoke_generator`'s `naval_retreat_speed = 0.2`) are modifiers of the country, not of an equipment or sub-unit, and the ship modifier sum `0xc38760` gathers them for every ship of the country; the combat getters read them through it (the member modifier getter `0x1972570` adds the screening bonuses on top). Values of the same key add up over the side's researches. In the data a research lists them as `countryModifiers` by game key; they apply to every ship of the side, whatever the research's `appliesToRoles`/`appliesToTypes` (which scope only its stat blocks). The engine reads the retreat speeds of S-RETREAT-SPEED, the hit chance terms of S-HIT-MODS (`naval_hit_chance`, `naval_light_gun_hit_chance_factor`, `naval_heavy_gun_hit_chance_factor`, `naval_torpedo_hit_chance_factor`), the crit terms of S-CRIT (`naval_critical_score_chance_factor`, `critical_receive_chance`, `naval_critical_effect_factor`) and the S-STATS-COMBAT getters (`naval_speed_factor`, `navy_org`, `navy_org_factor`, `naval_morale`, `naval_morale_factor`, `naval_retreat_chance`, `naval_retreat_chance_after_initial_combat`); the loader warns about any other key and ignores it (`BattleParticipant.ModelledCountryModifiers`). Doctrines, spirits and the other country sources will join the same sum.

## S-HOUR: hour structure [RE]

Each hour:
1. Positioning (S-POS), screening (S-SCREEN) and submarine detection (S-SUB) for both sides, from the same pre-fire state. They are not recomputed after losses during the hour.
2. **Attacker's turn**, then **defender's turn** (S-ORDER). In a turn, every ship of the side, in fleet order:
   1. steps its submarine reveal timer (S-SUB);
   2. **Waiting**: activates if its line is active (S-ACTIVATE). Ships become *fighting*; convoys go straight to *retreating*. A ship does not fire in the hour it activates;
   3. **Fighting**: escape progress is reset; from tick `COMBAT_MIN_DURATION` (8) it may decide to retreat (S-RETREAT). A ship that starts retreating does not fire this hour. Otherwise it fires (S-COOLDOWN, S-TARGET);
   4. **Retreating**: fires, then advances its escape (S-RETREAT-SPEED).
   
   After its ships, the side's air step runs in air hours (A-*, [air.md](air.md)). If the enemy has only hidden submarines left (nothing the side can target) and no air acted, all of the side's ships start retreating.
3. Experience gain (S-XP), organization regain (S-ORG-REGAIN), end check (S-END).

The experience level the combat modifiers use is taken at the start of the hour (S-XP). A battle the game creates during play (two hostile task forces meeting) runs one update with hour counter 0 that rolls and fires nothing, so its battle hour 0 is the game hour after it; a scenario that reproduces such a battle sets `rules.startHourOfDay` to that hour (the save's hour + 1) [CAL: the daily manpower refill of a replayed battle, [LOG](../reverse-engineering/findings/LOG-ingame-logging.md)].

- **S-ORG-REGAIN** [RE + CAL] (0xc395a0): after the hour, every ship that is not fighting (game state ≠ 2: waiting, retreating, escaped) regains `(NMilitary.SHIP_MORALE_TO_ORG_REGAIN_BASE (0.2) + naval_morale) × max(0, 1 + naval_morale_factor + dominance)` organization, up to its maximum organization (S-STATS-COMBAT). A fire (S-CRIT-PART, `naval_morale_factor −0.8`) cuts it to 0.04. `dominance` is `NAVAL_DOMINANCE_ORG_RECOVERY` (0.1) for the side with naval dominance in the region (0.22 an hour in the logged battle: the USA side until about hour 125, then Japan); it is not modelled, dominance being a region-wide value. A ship value at `+0x120` in the same factor is not identified. Retreating ships still fire, and organization enters their hit chance through the crew factor (S-HIT).

**Code.** `BattleSession.cs` (one hour per `StepHour`; `BattleEngine` runs a session to its end). Fire-exchange groups: with `COMBAT_MAX_GROUPS` (1) each side forms one group paired with the other side's, so groups have no effect in vanilla and are not modelled.

## S-ORDER: firing order [RE]

The attacker's whole side fires before the defender's, every hour. Damage applies immediately per shot, so a defender ship sunk by the attacker never fires that hour. Ships are processed in fleet (list) order, not shuffled. This gives the attacker a real first-strike edge; `BattleEngineTests.Simulate_MirrorMatchFavoursTheAttacker` measures it.

The previous `Interleaved` and `Simultaneous` variants are removed.

## S-POS: positioning [RE]

`positioning = clamp(BASE_POSITIONING - sizePenalty + detectionBonus, 0, 1)`, summed and clamped once.
- **S-POS-SIZE**: every ship in the battle counts 1, all lines and retreating ships included. If `own >= MIN_SHIPS_FOR_HIGHER_SHIP_RATIO_PENALTY` (4): `sizePenalty = clamp((own/enemy - 1) × HIGHER_SHIP_RATIO_POSITIONING_PENALTY_FACTOR (0.25), 0, MAX_POSITIONING_PENALTY_FROM_HIGHER_SHIP_RATIO (0.75))`.
- **S-POS-DETECT**: `bonus = (ΣownSurfaceDetection - ΣenemySurfaceDetection) / 100 × RELATIVE_SURFACE_DETECTION_TO_POSITIONING_FACTOR (0.01)`, at most `MAX_POSITIONING_BONUS_FROM_SURFACE_DETECTION` (0.25), applied only if positive. Ten ships at 40 against ten at 20 gives only +0.02. Damaged parts lower it (S-STATS-PARTS): a short-circuited radar takes its ship's detection to 0 [CAL].
- **S-POS-DMG**: `DAMAGE_PENALTY_ON_MINIMUM_POSITIONING (0.5) × (1 - positioning)` is subtracted inside the additive damage multiplier (S-DMG).

[CAL] In 12 logged TFB battles (`jukki_usa_vs_bc_japan`) positioning matched the formula in all 2,240 side-hours, counting the ships at the end of the previous hour as above, with TFB's values (size factor 0.4, at most 1.2, only from 60 own ships; detection bonus at most 0.1), and every gun hit fitted the damage multiplier with TFB's penalty 0.8 at positioning 0.65 to 1 ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)).
- **S-POS-SCREEN**: see S-SCREEN.

**Carrier ratio** [RE] (0x161c217): `penalty = clamp((1 - ownA / enemyA) × HIGHER_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR × enemyB, 0, MAX_CARRIER_RATIO_POSITIONING_PENALTY_FACTOR)`, where `A` = Σ sortie efficiency × planes over a side's carriers and `B` = their mean sortie efficiency × min(1, planes / CARRIER_PLANES_AMOUNT_FOR_POSITIONING). No penalty if the enemy has no carrier air.

**Not modelled** (no inputs for them yet): country/admiral positioning modifiers; the defender's initial-phase modifier during `COMBAT_INITIAL_DURATION`; the penalty for ships joining a running battle (`POSITIONING_PENALTY_FOR_SHIPS_JOINED_COMBAT_AFTER_IT_STARTS` with hourly decay); the convoy-escort dominance bonus. Carriers' detection should include `carrier_surface_detection` scaled by how full the deck is (needs air).

## S-SCREEN: screening efficiency [RE]

- `factor = max(0.01, 1 + screening_efficiency - SCREENING_EFFICIENCY_PENALTY_ON_MINIMUM_POSITIONING (0.5) × (1 - positioning))`.
- `screening = clamp(screens / ceil(3 × (capitals + carriers) + 0.5 × convoys) × factor, 0, 1)`; 1 if nothing needs screening.
- `carrierScreening = clamp(capitals / ceil(1 × carriers + 0.25 × convoys) × factor, 0, 1)`; 1 if nothing needs it.
- The required counts are **rounded up** (one capital plus one convoy needs 4 screens). Ships in every in-battle state count; submarines do not.
- Effects come from the static modifiers `screening_bonus` (`naval_hit_chance 0.4`, `naval_retreat_speed 0.2`, `convoy_retreat_speed 0.2`) scaled by screening, for battle-line and carrier-line ships, and `capital_screening_bonus` (`naval_retreat_speed 0.2`, `sortie_efficiency 0.1`) scaled by `1 - (1 - screening)(1 - carrierScreening)` for carrier-line ships. They are loaded from `common/modifiers` (`GameDefines.StaticModifiers`). Every key of the two blocks applies, because the combat rules read each modifier through the member modifier getter `0x1972570` (`MemberModifierRules`). TFB's `screening_bonus` has `naval_hit_chance 0.3`, `naval_heavy_gun_hit_chance_factor 1`, `naval_light_gun_hit_chance_factor 0.3` and `naval_retreat_speed 0.25`; [CAL] in the `jukki_usa_vs_bc_japan` game run the capitals' heavy and light hit chances fit `1 + (0.3 + 1) × screening + 0.15` and `1 + (0.3 + 0.3) × screening + 0.15` exactly for both sides (screening 0.64 and 0.97), while screens matched without them.

**Code.** `ScreeningRules.cs`. **Not modelled:** the `screening_efficiency` modifier and the `screening_without_screens` fallback.

## S-ACTIVATE: when lines start firing [RE]

Thresholds come from `CARRIER_ONLY_COMBAT_ACTIVATE_TIME` (0), `CAPITAL_ONLY_COMBAT_ACTIVATE_TIME` (6) and `ALL_SHIPS_ACTIVATE_TIME` (8), and depend on what is in the battle. Composition is pooled over **both sides**, counting only visible ships still in the battle; carrier-like = carriers, capital-like = capitals and submarines.
- With carriers present: carriers 0, capitals and submarines 6, screens and convoys 8.
- No carriers: capitals and submarines 0, screens and convoys `8 + 0 - 6` = 2.
- No carriers and no capital-like ships: everything 0.

A waiting ship activates at the first tick **strictly above** its threshold (always once the tick passes `ALL_SHIPS_ACTIVATE_TIME`) and fires from the next hour. So in a battle with carriers, carriers fire from hour 1, capitals from hour 7 and screens from hour 9; in a screens-only battle, from hour 1.

**Code.** `WeaponCadenceRules.ActivationThreshold`, `IsActivated`. Support ships (counted with carriers in the game) do not exist in the data model yet.

## S-COOLDOWN: weapon cadence [RE]

Three slots per ship, processed in this order: **heavy, torpedo, light**. Depth charges share the light slot. `cooldown = max(BASE_GUN_COOLDOWNS[slot] ({1, 4, 1}), MIN_GUN_COOLDOWN)`; only torpedoes have a modifier (`naval_torpedo_cooldown_factor`, not modelled).

Each hour, for a slot with attack: `charge = min(charge + 1, ceil(cooldown))`. If `charge >= cooldown`, **one target** is picked for the slot, then the slot fires `while charge >= cooldown: charge -= cooldown` at that target, stopping if it sinks. Guns fire every hour; torpedoes on the fourth processed hour and every fourth after; fractional cooldowns carry over; a cooldown below 1 gives several shots in one hour. Charge is per ship and slot, starts at 0 and is not reset by retreating.

**Code.** `WeaponCadenceRules.Charge`, `TrySpendShot`; `BattleSession.FireSlots`/`FireAt`.

## S-TARGET: target selection [RE]

Lines: 1 screens, 2 capitals, 3 carriers and convoys. Submarines are not on a line. Only visible ships in the battle (waiting, fighting or retreating) are candidates; hidden submarines never are. Reach decisions use the enemy's visible line counts **taken at the start of the side's turn**, so if the only reachable line is wiped out during the turn, later shots find no target and are not fired.

- **S-TARGET-LIGHT**: lines up to the closest non-empty one (`screens ? 1 : capitals ? 2 : 3`). Any shooter, carriers included.
- **S-TARGET-HEAVY**: up to line 2 when both screens and capitals are present, otherwise up to line 3.
- **S-TARGET-TORP**: **one roll** `r`: up to line 1 if `r <= screening`, else line 2 if `r <= carrierScreening`, else line 3. Screens are always candidates when present. There is no fallback: a torpedo with nothing in reach fires at nothing.
- **S-TARGET-SUB**: submarines have a nearest line from their task force aggressiveness (S-RETREAT). Do Not Engage and Engage At Low Risk: line 3, or 2 if the carrier line is empty, or 1 if capitals are also gone. Engage At Medium Risk: line 2, or 1 if capitals and the carrier line are empty. Engage At High Risk and Always Engage: line 1. That combines with the torpedo roll, so a low-aggressiveness submarine facing a fully screened convoy fires at nothing. Submarines also use their light slot under the same rule.
- **S-TARGET-DC**: the light slot may target revealed submarines as well as its surface lines; the submarine's weight needs a shooter stat above zero. That stat is not identified; sub detection is [ASSUMED]. A ship whose only weapon is depth charges never picks a target.
- **S-TARGET-WEIGHT** (`HardcodedValues`):
  - Ships: base capital 2, screen 3, submarine 4, other (carrier) 1; ×15 for heavy and torpedo against capitals and carriers; ×2 for the light slot against screens. Then `× (2 - hpRatio)`.
  - Convoys: base 4, 40 from submarines; ×15 for heavy and torpedo. Then `× (1 + 10 × (1 - hpRatio))`.
  - Then an exclusive state multiplier: fighting ×1.5, retreating ×0.5, waiting ×1. Active convoys are always retreating.
  - In the game's fixed point (fx5): `hpRatio` is the fx5 quotient rounded half up, each product is truncated [RE + CAL].
  - The resulting table is the wiki's (30/2, 3/6, 15/1, 60/4, 600/40, 4).
- **Pick**: `T = trunc(U × Σweights)` in fx5; the heaviest candidate takes `[0, maxWeight]`, then the rest accumulate in list order. That is weight-proportional. [CAL] Of 120,090 picks in 11 replayed battles, double arithmetic flips one at a boundary and a truncated `hpRatio` another; the rounded ratio with truncated products fits all (`FixedPoint`). [CAL] Replayed shot by shot on 12 logged TFB battles (`jukki_usa_vs_bc_japan`, about 127,000 shots), the weights predict every shooter → target split and every HP and state bin within noise ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)).
- **Retreating shooters** use exactly the same rules (the wiki's "highest weight among active targets" rule is not in the game).

**Code.** `TargetingRules.cs`. **Not modelled:** `naval_torpedo_screen_penetration_factor` (scales `1 - screening` for the roll).

## S-HIT: hit chance [RE]

```
hit = base × profileFactor × crewFactor × modifierSum, then at least COMBAT_MIN_HIT_CHANCE (0.005)
shot hits when U < hit   (no upper cap: hit >= 1 always hits)
```
- **S-HIT-BASE**: `COMBAT_BASE_HIT_CHANCE` (0.1); against a submarine (light slot, depth charges) `× DEPTH_CHARGES_HIT_CHANCE_MULT` (1.1).
- **S-HIT-SHIP**: `100 × visibility / (max(combat speed × HIT_PROFILE_SPEED_FACTOR (0.5), 0.1) + HIT_PROFILE_SPEED_BASE (20))`; submarines use sub visibility; convoys `CONVOY_HIT_PROFILE` (85). The wiki's 1936 light cruiser (visibility 15, 32 kn → 41.7) is a unit test. Not modelled: `naval_visibility_factor` and a per-ship hit-profile multiplier of unidentified source.
- **S-HIT-WEAPON**: `GUN_HIT_PROFILES[slot]` {heavy 70, torpedo 100, light 45}; depth charges `DEPTH_CHARGES_HIT_PROFILE` (100).
- `profileFactor = clamp((shipProfile / weaponProfile)², MIN_HIT_PROFILE_MULT (0), 1)`.
- `crewFactor = max(0, 1 + COMBAT_LOW_ORG_HIT_CHANCE_PENALTY (-0.5) × missingOrg + COMBAT_LOW_MANPOWER_HIT_CHANCE_PENALTY (-0.25) × missingManpower)`, both the shooter's, **added** into one factor.
- **S-HIT-MODS**: `modifierSum = 1 + naval_hit_chance + the shooter's hit chance factor for the slot` (depth charges use the light factor), one **unclamped sum**. The slot's factor is the design stat plus the modifier `naval_light_gun|heavy_gun|torpedo_hit_chance_factor`. Both modifiers are read through the member modifier getter, so each is the sum of:
  - the country modifiers (S-STATS-COUNTRY);
  - `screening_bonus` × screening (vanilla `naval_hit_chance` 0.4 at full screening) for battle-line and carrier-line shooters, and `capital_screening_bonus` × combined carrier screening for the carrier line; their per-weapon entries add to the slot's factor ([CAL] with TFB's block, see S-SCREEN);
  - `night` × darkness (−0.25 at full dark), see S-NIGHT.

  Admiral skills and traits, doctrines and weather (S-WEATHER) would add to the same sum; they are not modelled.
- `COMBAT_EVASION_TO_HIT_CHANCE` has no reader in 1.19.3 (dead define).

**Code.** `HitChanceRules.cs`; the modifier getter is `MemberModifierRules.Get`.

## S-NIGHT: night [RE effect, ASSUMED darkness, CAL: none with weather off]

The static modifier `night` is applied to the battle province "multiplied by amount of darkness": `naval_hit_chance -0.25`, `naval_retreat_speed +0.1` (and `carrier_traffic -1.0` for air), vanilla values; the block is read from the loaded game/mod folder (TFB: `carrier_traffic -0.75`, `naval_torpedo_hit_chance_factor -0.15`). The per-weapon entries (`naval_light_gun|heavy_gun|torpedo_hit_chance_factor`) add to the slot's hit factor in S-HIT-MODS. The engine uses darkness 1 from 20:00 to 06:00 and 0 otherwise (`HardcodedValues.NightStartHour/NightEndHour`, `BattleRules.StartHourOfDay`). The game computes darkness per province from the day/night terminator; that is not decoded. `BattleRules.NightEnabled` turns night off.

[CAL] **No night with weather off.** The darkness comes from the weather manager (`0xf19660`), and after `weather` / `disable_weather` it stays 0: in-game logs of the CL3 battle (Black Sea, May, 341 and 395 hours) read darkness 0 every hour, and the light-gun hit chance was the same at every hour of day ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)). The engine therefore has no night when `BattleRules.WeatherEnabled` is false.

## S-WEATHER: weather [RE toggle; terms not modelled]

Each hour the battle reads the weather of its strategic region: a weather type and the region's modifier holder ([RE finding](../reverse-engineering/findings/S-WEATHER-weather-toggle.md)). The weather static modifiers add `naval_hit_chance` to S-HIT-MODS (vanilla `weather_rain_light` −0.05, `weather_rain_heavy` −0.2, `weather_snow` −0.05, `weather_blizzard` −0.1) and change `carrier_traffic` (A-SORTIE) and `naval_strike` (A-HIT). None of these terms is modelled yet; `battleScenario.weather` only shows up in the notes.

The console commands `weather` (toggle) and `disable_weather` reset the weather, rebuild every region's weather state and stop the hourly weather update, so the battle reads a region state that nothing has filled: no weather [RE, medium: the rebuilt state is not decoded]. `BattleRules.WeatherEnabled = false` (scenario `rules.weatherEnabled`) is that setting: the scenario's weather is ignored, and so will the weather terms be once they are modelled. It also turns night off (S-NIGHT). Use it for battles recorded with weather off.

## S-DMG: damage of a hit [RE]

Per slot and target the game computes, once before its shots:
- heavy: `heavyAttack × pierce(heavyPiercing, armor)`; nothing against submarines;
- light: `lightAttack × pierce(lightPiercing, armor)`; against a submarine: `depthChargeAttack × DEPTH_CHARGES_DAMAGE_MULT (0.7) × pierce(lightPiercing, subArmor)`. Depth charges **do** go through armor;
- torpedo: `torpedoAttack`, no armor; later `× (1 - torpedo damage reduction)`;
- the attack and armor above are the combat values of S-STATS-COMBAT: `attack × max(0, 1 + shooter naval_damage_factor)` and `armor × max(0, 1 + target naval_defense_factor)`;
- `base = max(0, damage × max(0, 1 - positioningPenalty + shooter naval_damage_factor - target naval_defense_factor))`. The modifiers come from experience (S-XP); admirals and doctrines would add to them. Each factor therefore counts twice [RE + CAL]: with TFB's green malus (`naval_damage_factor -0.1` only) every logged in-game hit is `profile × 0.9 × 0.9 × randomness`, the profile already including one 0.9 ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)). In vanilla the green malus also has `naval_defense_factor -0.1`, so between two green fleets the hit multiplier terms cancel but attack and armor are each ×0.9.

Then per hit: `d = base × (1 + COMBAT_DAMAGE_RANDOMNESS (0.5) × (U - 0.05))` (range ×0.975 to ×1.475); crit roll (S-CRIT); torpedo reduction; split into HP and organization (S-DMG-APPLY); critical multiplier on both.

- **S-DMG-PIERCE**: `ratio = piercing / armor` (armor 0 counts as ratio 100); the first `i` with `ratio >= NAVY_PIERCING_THRESHOLDS[i]` ({2, 1, 0.75, 0.5, 0.1, 0}) selects `NAVY_PIERCING_THRESHOLD_DAMAGE_VALUES[i]` ({1, 1, 0.7, 0.4, 0.3, 0.1}) and `..._CRITICAL_VALUES[i]` ({2, 1, 0.75, 0.5, 0.1, 0}). If nothing matches (possible only with modded tables) the value is `table[0] + 1`.
- **S-DMG-RANDOM**: `1 + R × (U - 0.05)` is confirmed (`DamageRandomnessModel.Game`). The define comment's "7–10" reading is wrong for 1.19.3.
- `COMBAT_ARMOR_PIERCING_DAMAGE_REDUCTION` (0, "deprecated") is still read; not modelled.

**Code.** `DamageRules.Profile` (`AttackFactor`, `CombatArmor`, `DamageMultiplier`), `RollHit`.

## S-CRIT: critical hits [RE]

- Guns and depth charges: chance `COMBAT_BASE_CRITICAL_CHANCE (0.05) × (1 - reliability) × CRITICAL_VALUES[tier]`; multiplier `1 + COMBAT_CRITICAL_DAMAGE_MULT (5) × (1 - reliability)`. At reliability 0.8 that is ×2.0, not the ×1.8 the old spec had.
- Torpedoes: chance `COMBAT_TORPEDO_CRITICAL_CHANCE (0.1) × max(0, 1 + target torpedo-crit modifier)`; multiplier `COMBAT_TORPEDO_CRITICAL_DAMAGE_MULT` (2), no reliability.
- Reliability is the target's, clamped to 1. A crit is rolled only when chance and damage are positive, with `U <= chance`, and it multiplies **both** HP and organization damage.
- All crit chances are also multiplied by `1 + naval_critical_score_chance_factor` of the shooter `+ critical_receive_chance` of the target (modifier 0x18c, `MODIFIER_NAVAL_CRITICAL_RECEIVE_CHANCE_FACTOR`), both read through the member modifier getter, in the shot loop (the logged shot profile does not include it) [RE + CAL]. TFB's `damage_control_1` and `_2` give −0.1 each: in a replayed battle torpedo crits landed for rolls up to 0.785 of the profile's chance and failed from 0.83, i.e. at 0.8 × the chance, and the part roll followed only those ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)). `naval_critical_score_chance_factor_against` a country is not modelled.
- **S-CRIT-PART** [RE + CAL]: on a crit, a part is damaged with chance `CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT (0.1) × critical_part_damage_chance_mult / reliability` (0x1974511; no roll and always if that is ≥ 1 or reliability is 0; convoys never).
  - **Parts** come from `common/units/critical_parts/*.txt` (`data/critical-parts/`, imported with the ship data). A key a part leaves out takes the constructor's default (0x1019290): `chance` 1, `str_damage_multiplier`/`org_damage_multiplier` 1, `str_damage`/`org_damage` 0, `base_damage_instance` 1, `damage_instance_per_added_module` 0, `max_damage_instance` −1 (unlimited).
  - **Candidates** (0xc31030): the sub-unit's `critical_parts` (ship type), then each module's, in the game's slot order. A part listed by several modules is **one** candidate: the first brings `base_damage_instance` instances, each further module adds `damage_instance_per_added_module`, capped at `max_damage_instance`. A part damaged its number of instances is no longer a candidate; with none left the regular crit multiplier applies.
  - **Slot order** [CAL]: the game walks a design's slots in the order the slot names first appear in `common/units/equipment/*.txt` (files in load order), not the hull's own order. For TFB: anti-air, radar, engine, secondaries, front_1, mid_1, mid_2, rear_1, mid_3, rear_2, armor, front_2, battery, fire control, torpedo. All 26 part picks of 18 logged TFB battles fit it; the hull order put the SHBB's heavy guns and the light cruiser's secondaries on wrong rolls. The import records it per slot (`gameOrder`), and designs list their modules in it.
  - **Pick** (0xc34500): `r = trunc(U × Σ chance)` in fx5, then walk the candidates subtracting each `chance`; the first that takes `r` below 0 is damaged.
  - **Damage**: the part's values **replace** the crit multiplier: `str = str × str_damage_multiplier + str_damage`, `org = org × org_damage_multiplier + org_damage`. TFB's `destroyed_ammo_storage` (chance 0.1) does ×10 to both, `on_fire` org ×2 + 5.
  - **Lasting effects** for the rest of the battle: the stat penalties (S-STATS-PARTS), and each part modifier × count × `(1 + naval_critical_effect_factor)` in the ship modifier sum (S-STATS-COMBAT), e.g. `broken_propeller` (`naval_speed_factor −0.9`), `rudder_jammed` (`naval_retreat_chance −0.9`, `naval_speed_factor −0.5`), `on_fire` (`navy_org_factor −0.5`, `naval_morale_factor −0.8`). [CAL] A broken propeller under TFB's `damage_control_1`/`_2` (−0.1 each) leaves `1 − 0.9 × 0.8 = 0.28` of the speed, which the logged escape of a slow CL3 shows. Organization above the new maximum is cut to it at once (0xc3d4a3).
  - Parts are repaired between battles (`Ship.ResetCombatState`). Air attacks' crits (`CHANCE_TO_DAMAGE_PART_ON_CRITICAL_HIT_FROM_AIR`) do not damage parts in the engine.
  - Code: `CriticalPartRules`, `ShipDesign.CriticalPartCandidates`, `Ship.DamagePart`.

**Code.** `DamageRules.Profile`, `RollHit`, `RollPartDamage`.

## S-DMG-APPLY: HP, organization, manpower [RE]

- `strDamage = d × COMBAT_DAMAGE_TO_STR_FACTOR (0.6)`; `orgDamage = d × (1 - hpBeforeShot / maxHp) × COMBAT_DAMAGE_TO_ORG_FACTOR (1)` ([CAL] on every hit of 12 logged TFB battles; nothing else costs organization in combat, [LOG](../reverse-engineering/findings/LOG-ingame-logging.md)). The wiki's 100 HP / 50 org example (30 HP, then 0, 15, 30 org) is a unit test.
- Before HP is reduced, experience and manpower losses use the **unclamped** `strDamage` (S-XP). Then HP and organization are reduced and clamped at 0.
- A ship sinks at 0 HP; it stops being a target and does not fire for the rest of the hour.

**Code.** `DamageRules.Apply`, `ExperienceRules.ApplyDamageLosses`.

## S-RETREAT: retreat decision [RE + CAL]

Only fighting ships, from tick `COMBAT_MIN_DURATION` (8), before they fire:
- Threshold by the task force **aggressiveness**, the game's "Select aggressiveness" setting (its engagement rule): Do Not Engage and Engage At Low Risk `REPAIR_AND_RETURN_PRIO_LOW_COMBAT` (0.9); Engage At Medium Risk `..._MEDIUM_COMBAT` (0.6); Engage At High Risk and Always Engage `..._HIGH_COMBAT` (0.4). Capital ships and carriers multiply it by `CAPITAL_SHIP_COMBAT_RETREAT_MULT` (0.5).
- If `hp / maxHp < threshold`: roll `U <= COMBAT_RETREAT_DECISION_CHANCE (0.2) × (1 + naval_retreat_chance) × (1 + naval_retreat_chance_after_initial_combat)` (the last factor only after `COMBAT_INITIAL_DURATION`), clamped to [0, 1] (0x1975280; a jammed rudder, S-CRIT-PART, takes it to 0.02; the enemy's `naval_enemy_retreat_chance` is not modelled); success → retreating, no shots this hour. Organization is not used. [CAL] In 12 logged TFB battles 20 % of fighting ship-hours below the threshold started a retreat and none above it ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)).
- The task force's **repair priority** (Never repair, Low, Medium, High; save key `repair_mode`) is a different setting despite the `REPAIR_AND_RETURN_PRIO_*_COMBAT` define names. It decides when a task force goes home for repairs and is not read in combat. Scenarios record it per side (`repairPriority`, default High: the 230 task forces left at their defaults in the recorded save all had `repair_mode=3`, with `navy_engagement_rule=2`, the aggressiveness default), and the engine keeps it with the side without using it.
- [CAL] The aggressiveness, not the repair priority, sets the threshold. In a recorded TFB battle (75 vs 75 light cruisers, `data/battle-scenario-la-vs-torp-cl3.json`), both task forces were on Always Engage (`navy_engagement_rule=4`) and Never repair (`repair_mode=0`). The battle ended with all 74 surviving Romanian ships at or below 40% HP (highest 52.8 of 132). That matches 0.4; Never repair under the repair-priority reading would have been 0.9.

**Not modelled** (documented in the finding): the retreat-chance modifiers (`naval_retreat_chance`, the after-initial-combat one, the enemy's); the side-wide retreat order; the **aggressiveness check**, which makes healthy ships of an outmatched task force retreat (it needs a strength score that is not decoded); and the convoy-raiding submarine rule (`SUBMARINE_ESCAPE_RATIOS`). `REPAIR_AND_RETURN_UNIT_DYING_STR` is not read by naval combat.

**Code.** `RetreatRules.TryStartRetreat`. `BattleRules.RetreatEnabled = false` disables retreat (tooling).

## S-RETREAT-SPEED: escaping [RE]

Daily speed, additive:
```
BASE_ESCAPE_SPEED (0.05) + min(tick × ESCAPE_SPEED_PER_COMBAT_DAY (0.015) / 24, MAX_ESCAPE_SPEED_FROM_COMBAT_DURATION (0.5))
+ retreat speed modifier: naval_retreat_speed for warships, sub_retreat_speed for submarines, convoy_retreat_speed for
  convoys; warships also add naval_retreat_speed_after_initial_combat once tick > COMBAT_INITIAL_DURATION (6)
+ speed × SPEED_TO_ESCAPE_SPEED (0.5) / 100
+ submarines: ESCAPE_SPEED_SUB_BASE (0.1), or ESCAPE_SPEED_HIDDEN_SUB (0.35) while hidden
```
Each retreat speed modifier is read through the member modifier getter `0x1972570`, so it is the sum of:
- the country modifiers (S-STATS-COUNTRY), e.g. the `smoke_generator` technology (vanilla and TFB): `naval_retreat_speed = 0.2`;
- the `night` static modifier × darkness (vanilla has only `naval_retreat_speed 0.1`, so only warships get it);
- `screening_bonus` × screening for the battle line and carrier line, and `capital_screening_bonus` × combined carrier screening for the carrier line.

Each hour a retreating ship adds `speed / 24` to its progress and escapes at 1. A 30-knot capital with full screening at tick 10 escapes in about 59 hours.

The modifier ids are mapped to names with medium confidence, except `naval_retreat_speed` (0x5b), which is confirmed: [CAL] in the CL3 battle both countries had `smoke_generator`, and the logged escape progress of every retreating cruiser grew by exactly 0.2 a day more than the formula without it ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)). With the country modifier, 200 simulator seeds escape in the same time as the 12 game seeds for every decision hour (decided before hour 100: 36.0 h against the game's 36.1; 100–200: 33.7 and 33.7; 200–300: 31.7 and 31.7; later: 30 and 30).

**Code.** `RetreatRules.EscapeSpeedPerDay`, `AdvanceRetreat`, `MemberModifierRules.Get`; country modifiers in `BattleParticipant.CountryModifiers` and `Ship.CountryModifiers`.

## S-SUB: submarines [RE]

- Submarines start **hidden** (reveal timer 0) and can only be targeted while revealed. Hidden submarines are not counted for activation or line counts.
- At the start of each of its updates, a revealed submarine's timer counts down. If it is hidden and the enemy's detection `D > 0`, it is revealed for `SUBMARINE_HIDE_TIMEOUT` (8) hours when `U <= chance`. The same roll happens after it fires torpedoes, with the numerator × `SUBMARINE_REVEAL_TORPEDO_FIRING_DETECTION_MULTIPLIER` (1.1).
- `chance = min(1, D × SUBMARINE_REVEAL_DETECTION_MULTIPLIER (0.065) / max(SUBMARINE_BASE_STEALTH_VALUE (100) - V, 0.1))`, `V = subVisibility × (1 + SUBMARINE_REVEAL_ON_MINIMUM_POSITIONING (1) × (1 - ownPositioning))`.
- `D` = mean sub detection over the enemy side's ships in battle that have any, × `NAVAL_COMBAT_SUB_DETECTION_FACTOR` (plus an air term, milestone 2).
- A side that faces only hidden submarines disengages (S-HOUR).

**Code.** `SubmarineRules.cs`. **Not modelled:** the sub-only-battle rule at hour 0 (`SUBMARINE_REVEALED_TIMEOUT`), visibility modifiers. The old `SUBMARINE_REVEAL_BASE_CHANCE`/`POW` defines are not read by 1.19.3.

## S-XP: experience [RE]

- **Levels**: `level = count(NMilitary.UNIT_EXP_LEVELS {0.1, 0.3, 0.75, 0.9} <= xp / NAVY_MAX_XP)`, i.e. 0–4. The level modifiers use the ship's experience at the start of the combat hour, so experience lost to hits during the hour changes them only from the next hour [CAL: in a logged TFB battle the attack in all 8,572 shot profiles matched the shooter's start-of-hour level, while the end-of-hour level differed 185 times, [LOG](../reverse-engineering/findings/LOG-ingame-logging.md)].
- **Effects**: level `ARMY_EXP_BASE_LEVEL` (1) has none. Below it, the static modifier `ship_experience_malus_min` (`naval_damage_factor -0.1`, `naval_defense_factor -0.1`) × `(base - level) / base`. Above it, `ship_experience_bonus_max` (`+0.3`, `+0.2`) × `(level - base) / (4 - base)`. Damage and defense factors enter the additive damage multiplier (S-DMG). Experience has no other combat effect; carriers get `carrier_experience_*` sortie modifiers instead (air).
- **Gain**: each hour, a ship that fired gains `UNIT_EXPERIENCE_PER_COMBAT_HOUR (10) × UNIT_EXPERIENCE_SCALE (1) × (enemyGroupSize + 1) / (ownGroupSize + 1) × EXPERIENCE_FACTOR_NON_CARRIER_GAIN (0.04)`, × `EXPERIENCE_FACTOR_CONVOY_ATTACK` (0.04) if its last target was a convoy. Damage dealt does not matter. Group size counts every ship the side brought (sunk ships stay in the group's list; medium confidence). Carriers that did not fire gain from sortie efficiency × `EXPERIENCE_FACTOR_CARRIER_GAIN` (air). `XP_GAIN_FACTOR` is not used here.
- **Loss** on each hit, before HP drops: `xp × clamp(hp - EXPERIENCE_LOSS_FACTOR (1) × strDamage, 0, maxHp) / hp`. Manpower loss: `round(maxManpower × trunc5(strDamage / maxHp) × MANPOWER_LOSS_RATIO_ON_STR_LOSS (0.5))`, the ratio truncated to fx5 [CAL: rounding the exact ratio misses 48 of 22087 logged losses, the truncated ratio none], never below `round(maxManpower × MIN_MANPOWER_RATIO_TO_DROP (0.1))`, and nothing once at that floor. Rounding is half away from zero.

**Code.** `ExperienceRules.cs`.

## S-MANPOWER: reinforcement during battle [RE + CAL]

Ships regain manpower **once a day, in combat too**, despite the define's comment ("ships not in combat"). The game's daily update (`gamestate.daily` → `country.daily` → `fleets.daily` → `CTaskForce::DailyUpdate`) refills every task force without checking for combat:
- `gain = min(maxManpower - manpower, ceil(maxManpower × DAILY_MANPOWER_GAIN_RATIO (0.05)))`, the ceiling taken in fx5; 60 a day for a 1200-crew ship.
- It runs after the combat hour of the tick that starts a new day (00:00), so after battle hour `h` with `(startHourOfDay + h) % 24 == 0`.
- Every ship afloat refills, including escaped ones (still in their task force).
- **A sinking refills the task force too** [RE + CAL]: the combat's sink handler `0x1975480` (which sets the member's state to sunk) calls the task force's remove-ship function `0xd77ff0` (at 0x1975879), and that ends with the same refill `0xd7a7e0(taskForce, force = false)` (at 0xd78b71), which refills every ship once the game has started. So every sinking gives the rest of the sunk ship's task force the daily gain at once, in the middle of the hour; in a replayed battle every manpower rise outside 00:00 came with a sinking, and later shots that hour saw the new crews ([LOG](../reverse-engineering/findings/LOG-ingame-logging.md)).
- The manpower is drawn from the country's pool. [ASSUMED] The pool is never short. Before the refill, `0xd77ff0` returns `round(manpower × (1 − clamp(MANPOWER_LOSS_RATIO_ON_SUNK (0.5) × (1 + navy_casualty_on_sink), 0, 1)))` of the sunk ship's crew to the pool; the strategic-region static modifier `water_modifier_shark_infested` (`navy_casualty_on_sink = 0.6`, Pacific regions in vanilla and TFB) makes that 20 % instead of 50 %. It only matters to the pool, so the engine does not model it.

Calibration: in the CL3 battle (`battle-scenario-la-vs-torp-cl3.json`) the save from the battle's last hour had 74 of 75 BUL and 71 of 74 ROM ships at full manpower after losing 25% and 72% of their HP. Without the refill the simulator left almost every ship short; with it, 74.5 and 70.1 on average (200 seeds).

**Code.** `ExperienceRules.DailyManpowerGain`, `BattleSession.ApplyDailyManpowerGain`, `BattleSession.ReinforceAfterSinking`. Manpower matters in combat through the crew factor in hit chance (S-HIT-MODS).

## S-REPLAY: replaying a logged game battle [tooling]

`tools/ingame/battle_logger.js` records, for every combat hour, each member's state at the start (after the daily update) and the end of the hour, every `random_fixed` call of the combat update on its thread in call order with its source line, and every shot. `NavySimulator replay --log <run>` (`GameData/Replay/GameBattleLog`, `Engine/Battles/Replay/BattleReplay`) builds the scenario with the game's attacker, orders each fleet like the game's members, and fights each hour from the game's start state with the game's rolls (`ReplayRandomSource`): every engine draw names its `RollKind`, which must be the kind of the game's next roll. Line tags (hoi4.exe 1.19.3): 0x37b target pick, 0x3d9 torpedo line, 0x587 hit, 0x593 damage randomness, 0x59f crit, 0x2f4 part damage, 0x78f part pick (skipped only for data without parts), 0xa83 retreat decision, 0x40e the 6–9 rolls of a sinking (skipped). The game's logged hit chance is compared with the engine's (tolerance 2e-5). With parts, 11 replayed `jukki_usa_vs_bc_japan` battles (seeds 1 and 18–27, 120,090 shots) match every shot, and each side's HP damage within 0.1 HP; what differs is organization regain (naval dominance, not modelled), in the last hour the game skipping the end-of-hour regain and experience once the battle has ended, and 4 manpower values off by one (the engine's damage is not computed in fx5). Shots are compared one by one (shooter, weapon, target, hit, HP and organization lost) and end-of-hour states against the next hour's start. Setting the state every hour keeps fixed-point rounding from accumulating.

## S-END: end of battle [RE]

The battle ends when either side has no ship left in it (waiting, fighting or retreating with HP above 0). Retreating ships keep the battle going until they escape. There is no maximum duration in the game; `BattleRules.MaxHours` is a safety stop (`Timeout`). `ContinueAfterRetreat` (tooling) puts escaped ships back in.

## Changes from the first version of this spec

| Item | Milestone 1 (wiki-based) | Now (hoi4.exe 1.19.3) |
|---|---|---|
| S-ORDER | interleaved random order | attacker side first, fleet order |
| S-HOUR | retreat phase after firing; retreating ships do not fire | decision before firing; retreating ships fire |
| S-ACTIVATE | 0/6/8 regardless of composition | composition-dependent, pooled, strict `>` |
| S-COOLDOWN | next-fire hour | per-slot charge; one target per slot per hour; multi-shot |
| S-TARGET | live line membership, two torpedo rolls, carrier light-gun exclusion, retreating-shooter rule | turn snapshot, one roll, none of those rules |
| S-TARGET-WEIGHT | retreat ×0.5 and active ×1.5 could stack | exclusive states; convoy damage ×(1+10·missing) |
| S-HIT-MODS | multiplicative, −25% night, org and manpower separate | one additive sum; org and manpower added |
| S-HIT | capped at 100% | no cap; minimum 0.005 applied last |
| S-CRIT | ×(1 + 4·(1−rel)); depth charges ignored armor | ×(1 + 5·(1−rel)); depth charges use piercing tiers |
| S-DMG | separate positioning multiplier; ×(level damage) | additive (1 − penalty + damage − defense) |
| S-RETREAT | repair priority 0.6/0.3/0.1 | aggressiveness 0.9/0.6/0.4, capitals ×0.5 |
| S-RETREAT-SPEED | multiplicative screening bonus; subs replace base | additive; subs add to base; hidden subs faster |
| S-SUB | always revealed | hidden/revealed with reveal rolls |
| S-XP | 7 legacy levels, linear damage bonus, gain ∝ enemy/own | 5 levels, static modifiers, gain ∝ (enemy+1)/(own+1), HP-based loss |
| Screening | `ceil` missing | requirements rounded up, factor floor 0.01 |
