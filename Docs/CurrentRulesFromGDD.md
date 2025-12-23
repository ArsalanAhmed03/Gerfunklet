# Current Rules From GDD (Implemented Only)

This document mirrors the GDD only where code exists today. If a GDD rule is not implemented yet, it is omitted from the “Current Rules” section and listed under “Not Yet Implemented.”

## Current Rules (Implemented)

### Match Flow
- Phases: WaitingForPlayers -> LoadoutSelect -> Countdown -> Playing -> Overtime -> RoundEnded -> MatchEnded.
- Match timer: counts down during Playing; when it reaches 0, phase switches to Overtime.
- Rounds: best-of-3 (first to 2 round wins).

### Win Conditions (Current Build)
- A round ends when a player dies or an objective zone completes a capture channel.
- A match ends when a player reaches the round win threshold.

### Loadout System
- Each player selects 4 unique abilities during LoadoutSelect.
- Loadouts are validated on the server (no duplicates, allowed IDs only).
- Loadouts lock when both players submit; countdown starts immediately.

### Abilities (Implemented)
- Stomp: AoE damage and short stun (server-authoritative), with client-side stomp animation.
- Rally: move speed buff applied only to characters owned by the caster.
- Parry: timed defense window, server-authoritative.
- Throw: spawns a server-authoritative projectile.
- Fortify: damage reduction for a duration.

### Spawning
- Players are spawned as player objects by the server at scene load.
- Minions can be spawned by a player action (server authoritative).

### ATP (Economy – Partial)
- ATP is server-authoritative with defaults from the GDD: cap 10, regen 0.9/s, start 4, global spend GCD 0.5s.
- Minion spawn cost is enforced when an AtpResource component is present on the player.

## Implemented But Diverging From GDD
- GDD Rally includes attack speed and ally/minion buffs; current implementation only applies move speed and only to caster-owned characters.
- GDD uses the Millstone/Citadel/Throne objective flow; current build uses capture zones and death to resolve rounds.
- GDD respawn delay: 8s; current build resets on round reset without a per-player respawn timer.

## Not Yet Implemented (GDD)
- Millstone carry/throw/reclaim flow, Citadel/Throne flow.
- ATP hand/deck system (cards, mulligan, global hand GCD usage in UI).
- Stamina sleep/wake and feast mechanics.
- Devour and Super abilities.
- Buildables and minion roster variety.
- Overtime bonuses (ATP regen bonus, warmup/citadel modifiers).
- Remote Config, analytics events, and security rules.

## Scene/Prefab Changes Required To Use New Systems
- Add `AtpResource` to the player prefab to enable ATP regen/spend.
- Set `LocalSpawner.minionAtpCost` to match the intended minion cost (default is 2).
- Add ATP UI bindings (not yet implemented) if you want players to see ATP state.
