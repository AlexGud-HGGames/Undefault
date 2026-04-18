# CS2 GSI Events For UndefaultIt

This document summarizes the practical Counter-Strike 2 GSI event space for UndefaultIt profile work and future **gameplay-driven music behavior**. In this repo, “scenario” (when it appears below) means **a behavior you implement with normalized events, `RulesEngine.ActionMap`, and profile JSON**—not a separate YAML scenario engine.

Primary upstream reference:

- `antonpup/CounterStrike2GSI` README and its implemented event list

This is not a copy of the upstream text. It is a project-oriented reference that answers:

- what CS2 signals exist upstream
- which ones UndefaultIt already maps
- which groups are the best next candidates for console-first music scenarios

## How To Use This Document

When adding a new profile rule or extending event coverage:

1. pick the gameplay moment you care about
2. check the relevant event family below
3. decide whether the signal can be used directly from current UndefaultIt payload mapping
4. if not, add the smallest new DTO/module/detector work needed before expanding the profile

## Current UndefaultIt Coverage

The host currently accepts a narrow subset of the full CS2 payload:

- `provider`
- `map`
- `player`

Current local snapshot mapping:

- `RoundModule` from `map.round` and `map.phase`
- `VitalsModule` from `player.state.health` and `player.state.armor`
- `PositionModule` from `player.position`
- `CombatModule` from `player.activity`

Current normalized events:

- `round_start`
- `death`
- `combat` available in the detector but disabled by default
- `idle` available in the detector but disabled by default

## Best Event Families For Music Scenarios

These are the most useful upstream categories for future profile work.

| Event family | Upstream examples | Why it matters for music scenarios | Current UndefaultIt status |
|---|---|---|---|
| Map and round flow | `RoundStarted`, `RoundConcluded`, `MapPhaseChanged`, `FreezetimeStarted`, `WarmupStarted`, `Gameover` | Best source for predictable transitions between tense and relaxed music states | Partially available now through `map.phase` and `map.round` |
| Player life-state | `PlayerDied`, `PlayerRespawned`, `PlayerHealthChanged`, `PlayerTookDamage` | Good for mute, resume, danger, recovery, or low-health scenarios | `death` is implemented; broader health/damage logic is not |
| Combat pressure | `PlayerGotKill`, `KillFeed`, `PlayerActiveWeaponChanged`, `PlayerWeaponsPickedUp` | Good for clutch, streak, combat, and post-fight transitions | Only coarse combat hint exists today |
| Bomb flow | `BombPlanting`, `BombPlanted`, `BombDefusing`, `BombDefused`, `BombExploded` | Strong candidates for high-signal scenario changes | Not mapped yet |
| Countdown and phase timing | `PhaseCountdownsUpdated`, `FreezetimeStarted`, `TimeoutStarted`, `IntermissionStarted` | Useful for fade-in, fade-out, countdown, and setup transitions | Not mapped yet |
| Match context | `MatchStarted`, `RoundChanged`, `Gameover`, `GamemodeChanged` | Good for big scene changes, pre-match, and end-of-match behavior | Only partial round/map coverage exists |

## Practical Event Catalog

### Round And Map Flow

Upstream events worth knowing:

- `RoundChanged`
- `RoundStarted`
- `RoundConcluded`
- `MapPhaseChanged`
- `FreezetimeStarted`
- `FreezetimeOver`
- `WarmupStarted`
- `WarmupOver`
- `TimeoutStarted`
- `TimeoutOver`
- `IntermissionStarted`
- `IntermissionOver`
- `MatchStarted`
- `Gameover`

Best uses in UndefaultIt:

- start round music ducking or pause logic
- restore or transition music after round conclusion
- lower music during freeze-time, then restore when the round goes live
- switch to calmer behavior during timeout, intermission, or warmup
- trigger special end-of-match or match-start scenarios

Current implementation notes:

- `round_start` already comes from `map.round` and `map.phase`
- future round-end scenarios likely need explicit round conclusion detection, not only round increment detection

### Player State And Survival

Upstream events worth knowing:

- `PlayerHealthChanged`
- `PlayerDied`
- `PlayerRespawned`
- `PlayerTookDamage`
- `PlayerArmorChanged`
- `PlayerHelmetChanged`
- `PlayerFlashAmountChanged`
- `PlayerSmokedAmountChanged`
- `PlayerBurningAmountChanged`
- `PlayerMoneyAmountChanged`
- `PlayerEquipmentValueChanged`
- `PlayerDefusekitChanged`

Best uses in UndefaultIt:

- pause or duck on death
- resume or restore after respawn if a future mode needs that
- react to low-health or damage spikes with more aggressive ducking
- use flash, smoke, or burn states for short-lived audio suppression rules

Current implementation notes:

- only `health` and `armor` are mapped today
- `death` already works from alive-to-dead state transition
- low-health, flash, smoke, and burn scenarios would require DTO expansion first

### Combat And Weapons

Upstream events worth knowing:

- `PlayerGotKill`
- `PlayerKillsChanged`
- `KillFeed`
- `PlayerWeaponChanged`
- `PlayerActiveWeaponChanged`
- `PlayerWeaponsPickedUp`
- `PlayerStatsChanged`

Best uses in UndefaultIt:

- spike music after a kill or clutch moment
- suppress music during intense combat windows
- build scenario packs around weapon classes or pickups later

Current implementation notes:

- current `CombatModule` only infers combat from `player.activity`
- there is no direct kill, damage-dealt, or weapon-state mapping yet

### Bomb Flow

Upstream events worth knowing:

- `BombPlanting`
- `BombPlanted`
- `BombDefusing`
- `BombDefused`
- `BombDropped`
- `BombPickedup`
- `BombExploded`
- `BombStateUpdated`

Best uses in UndefaultIt:

- instantly duck or pause on plant/defuse tension
- restore music after defuse or explosion
- create late-round objective-specific scenarios

Current implementation notes:

- no bomb DTOs or snapshot modules exist yet
- this is one of the highest-value future expansions after the default round/death flow

### Countdown And Timing

Upstream events worth knowing:

- `PhaseCountdownsUpdated`

Best uses in UndefaultIt:

- delay restores until a countdown ends
- pre-round or post-round fade timing
- timeout/intermission automation without guessing from map phase alone

Current implementation notes:

- not mapped yet
- useful when the project needs more precise timing than phase strings alone

### All Players, Grenades, And Spectator-Heavy Data

Upstream categories:

- `AllPlayersUpdated`
- `PlayerConnected`
- `PlayerDisconnected`
- `AllGrenadesUpdated`
- `GrenadeUpdated`
- `NewGrenade`
- `ExpiredGrenade`

Best uses in UndefaultIt:

- spectator-oriented scenario systems
- observer tools
- team-wide or grenade-density music logic

Current implementation notes:

- low priority for the current console-first local-player workflow
- should only be added when there is a clear scenario need

## Recommended Next Event Expansions

If future agents need more scenarios, the lowest-friction additions are:

1. round-end and map-phase transitions from the existing `map` payload
2. richer player-state signals such as low health and flash amount
3. bomb state mapping
4. countdown mapping for better timing control

## Suggested Future Normalized Event Keys

These are reasonable future normalized keys if the backend expands:

- `round_end`
- `freezetime_start`
- `freezetime_end`
- `bomb_planted`
- `bomb_defusing`
- `bomb_defused`
- `bomb_exploded`
- `low_health`
- `player_damaged`
- `player_kill`
- `match_start`
- `match_end`

These are suggestions, not a locked contract. Keep the key set small and only add one when a real profile needs it.
