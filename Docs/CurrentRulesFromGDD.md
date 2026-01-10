# Current Rules From GDD (Implemented Only)

This document mirrors the GDD only where code exists today. If a GDD rule is not implemented yet, it is omitted from the “Current Rules” section and listed under “Not Yet Implemented.”

## Current Rules (Implemented)

### Match Flow
- Phases: WaitingForPlayers -> LoadoutSelect -> Countdown -> Playing -> Overtime -> RoundEnded -> MatchEnded.
- Match timer: counts down during Playing; when it reaches 0, phase switches to Overtime.
- Rounds: best-of-3 (first to 2 round wins).

### Win Conditions (Current Build)
- A round ends when a player dies or an objective zone completes a capture channel.
- A match ends immediately when a Millstone is planted at the enemy altar (primary path).
- A match can also end by destroying the enemy Citadel and completing a Throne channel.
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

### Card/Hand Scaffold (Partial)
- Each player has a deck of 8 cards and a hand of 4 cards.
- Mulligan: up to 2 swaps per match, only during LoadoutSelect.
- Cards can be played during Playing/Overtime to spend ATP, spawn the card prefab after a warmup, and replace the slot.
- Card placement is validated against a deployment ring: base ring around the player’s home, forward ring around the Gerfunklet once it crosses midline.
- Played or mulliganed cards are returned to the bottom of the deck (8-card cycle).

## Implemented But Diverging From GDD
- GDD Rally includes attack speed and ally/minion buffs; current implementation only applies move speed and only to caster-owned characters.
- Player death still ends a round (not in GDD).
- Objective zones still exist in the scene flow, but are disabled by default in `MatchManager`.
- GDD respawn delay: 8s; current build resets on round reset without a per-player respawn timer.
- GDD overtime is a fixed 1:00 with bonuses; current build caps overtime at 60s but does not apply bonuses.
- Millstone throw/drop is implemented, but reclaim/contest rules are simplified (any player can pick up a dropped head after a 1s hold).
- Throne capture uses a simple channel gated by Citadel destroyed; Citadel damage is currently just proximity damage.
- ObjectiveZone wins are disabled by default to match GDD win paths (can be re-enabled in MatchManager).

## Not Yet Implemented (GDD)
- Full Citadel/Throne flow (structure attacks, siege units, damage tuning).
- Advanced card-hand UX (drag/ghost previews, placement feedback, card cooldown/GCD UI).
- Stamina sleep/wake and feast mechanics.
- Devour and Super abilities.
- Buildables and minion roster variety.
- Overtime bonuses (ATP regen bonus, warmup/citadel modifiers).
- Remote Config, analytics events, and security rules.

## Scene/Prefab Changes Required To Use New Systems
- Add `AtpResource` to the player prefab to enable ATP regen/spend.
- Set `LocalSpawner.minionAtpCost` to match the intended minion cost (default is 2).
- Set `LocalSpawner.minionAtpCost` to match the intended minion cost (default is 2).
- Add an `AtpUI` component to your HUD and wire its slider/text fields to display ATP.
- Add `CardHand` to the player prefab and assign a `CardCatalog` (or default deck) to initialize cards.
- Add `DeploymentRules` to the player prefab and optionally set `homeAnchor`, `baseDeployRadius`, `forwardDeployRadius`, and `midlineX`.
- Add `CardPlacementController` to the player prefab or HUD and assign `placementMask` to your ground layer; optionally set input actions for place/cancel.
- Assign `spawnPrefab` and `spawnWarmupSeconds` on each `CardDefinition` asset you want to be playable.
- Add input actions named `MillstoneDrop` and `MillstoneThrow` to the Player action map if you want manual drop/throw controls.
- Add `CitadelHealth` to each Citadel object and assign each Throne’s `requiredCitadel`.
- Add `ThroneCapture` to each Throne with a trigger collider.
- Assign `GameManager.citadelA/citadelB` and `GameManager.throneA/throneB`, plus enemy Citadel/Throne UI sliders/text if you want HUD updates.
