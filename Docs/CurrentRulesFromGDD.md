# Current Rules From GDD (Implemented Only)

This document mirrors the GDD only where code exists today. If a GDD rule is not implemented yet, it is omitted from the “Current Rules” section and listed under “Not Yet Implemented.”

## Current Rules (Implemented)

### Match Flow
- Phases: WaitingForPlayers -> Ready/Mulligan (LoadoutSelect) -> Countdown -> Playing -> Overtime -> RoundEnded -> MatchEnded.
- Match timer: counts down during Playing; when it reaches 0, phase switches to Overtime.
- Single-match flow (rounds disabled by default).
- RoundEnded is only used if `MatchManager.enableRounds` is turned on.
- Super choice is selected during Ready/Mulligan (no separate loadout screen).
- Gerfunklet ability set is fixed to 5 core abilities (Stomp, Devour, Rally, Parry, Throw).

### Win Conditions (Current Build)
- A match ends immediately when a Millstone is planted at the enemy altar (primary path).
- A match can also end by destroying the enemy Citadel and completing a Throne channel.
- Overtime ending can still force a draw if no win condition is completed.

### Citadel (Partial)
- Citadel health uses GDD max HP (2000) and can drive optional tier visuals at 75/50/25% HP.

### Ability Loadout
- Ability loadout UI is disabled; Gerfunklet abilities are fixed to 5 core abilities during the match.

### Abilities (Implemented)
- Stomp: AoE damage and short stun (server-authoritative), breaks `Barricade` objects in range, and adds +8% Super charge per hit; with client-side stomp animation.
- Rally: 10s aura; +10% move speed and +15% attack speed to caster-owned allies; doubled for minions carrying objects.
- Parry: timed defense window; successful parry stuns the attacker for 0.4s (applies to player attacks and minion attacks with `StunReceiver`).
- Throw: throws nearby objects or small/medium minions in an arc, dealing impact damage and knockback.
- Devour: cone grab that eats small/medium minions, heals per unit, can consume food, and optionally drops bone scrap.
- Super: separate from the 4-slot loadout; charge builds from damage dealt/taken and Millstone throws.
  - Seismic Quake: wide shockwave that knocks targets back.
  - Boulder Pitch: long-arc throw that deals heavy damage to structures (and optional player damage).
  - Gorge: rapid Devour chain with 2s CC immunity (blocks stun/knockback during the window).
- Non-GDD abilities (like Fortify) are blocked by loadout validation and not usable.

### Spawning
- Players are spawned as player objects by the server at scene load.
- Minions can be spawned by a player action (server authoritative).
- Minion behavior can be customized per prefab via `MinionStats` (damage, speed, attack range, targeting).

### Buildables (Partial)
- Buildable cards can enforce a per-card active cap (default max 2) using `BuildableInstance`.

### ATP (Economy – Partial)
- ATP is server-authoritative with defaults from the GDD: cap 10, regen 0.9/s, start 4, global spend GCD 0.5s.
- Minion spawn cost is enforced when an AtpResource component is present on the player.

### Card/Hand Scaffold (Partial)
- Each player has a deck of 8 cards and a hand of 4 cards.
- Mulligan: up to 2 swaps per match, only during LoadoutSelect.
- Cards can be played during Playing/Overtime to spend ATP, spawn the card prefab after a warmup, and replace the slot.
- Card placement is validated against a deployment ring: base ring around the player’s home, forward ring around the Gerfunklet once it crosses midline.
- Played or mulliganed cards are returned to the bottom of the deck (8-card cycle).

### Stamina (GDD core)
- Stamina is server-authoritative (max 600) and drains while active; extra drain applies while carrying the Millstone.
- At 0 stamina, the Gerfunklet sleeps (invulnerable, inert).
- Stamina regens while sleeping (throne vs ground rates) with an under-fire penalty.
- Auto-wake occurs at 25% stamina when safe; optional forced wake and manual rest are available via `Rest`/`Wake` input actions.
- Feast ring: allied minions can deliver food piles; on wake up to 5 piles are consumed to restore stamina and grant Well-Fed (+1.0/s for 6s per stack, max 5 stacks).

## Implemented But Diverging From GDD
- Objective zones still exist in the scene flow, but are disabled by default in `MatchManager`.
- GDD respawn delay: 8s; current build resets on round reset without a per-player respawn timer.
- GDD overtime is a fixed 1:00; current build applies ATP regen +15%, warmup -50%, and citadel damage +10%.
- Millstone throw/drop is implemented, but reclaim/contest rules are simplified (any player can pick up a dropped head after a 1s hold).
- Throne capture uses a simple channel gated by Citadel destroyed; Citadel damage is currently just proximity damage.
- ObjectiveZone wins are disabled by default to match GDD win paths (can be re-enabled in MatchManager).

## Not Yet Implemented (GDD)
- Full Citadel/Throne flow (structure attacks, siege units, damage tuning).
- Advanced card-hand UX (drag/ghost previews, placement feedback, card cooldown/GCD UI).
- Buildables and minion roster variety.
- Overtime bonuses (ATP regen bonus, warmup/citadel modifiers).
- Remote Config, analytics events, and security rules.

## Scene/Prefab Changes Required To Use New Systems
- Add `AtpResource` to the player prefab to enable ATP regen/spend.
- Set `LocalSpawner.minionAtpCost` to match the intended minion cost (default is 2).
- Add an `AtpUI` component to your HUD and wire its slider/text fields to display ATP.
- Assign `GameManager.staminaBar` if you want the stamina UI to update.
- Assign `GameManager.sleepingIndicator` (any GameObject) if you want a visible sleeping overlay.
- Add `CardHand` to the player prefab and assign a `CardCatalog` (or default deck) to initialize cards.
- Add `DeploymentRules` to the player prefab and optionally set `homeAnchor`, `baseDeployRadius`, `forwardDeployRadius`, and `midlineX`.
- Add `CardPlacementController` to the player prefab or HUD and assign `placementMask` to your ground layer; optionally set input actions for place/cancel.
- Assign `spawnPrefab` and `spawnWarmupSeconds` on each `CardDefinition` asset you want to be playable.
- Add input actions named `MillstoneDrop` and `MillstoneThrow` to the Player action map if you want manual drop/throw controls.
- Add input actions named `Rest` and `Wake` to the Player action map if you want manual sleep/force-wake controls.
- Add `FeastRing` to the player prefab with a trigger collider (1.5 radius); it auto-assigns owner from the player `NetworkObject`.
- Add `MinionOwner` and `FoodCarrier` to minion prefabs if you want them to pick up and deliver food.
- Add `MinionStats` to minion prefabs to set per-unit damage/speed/attack range and targeting (e.g., Brute = StructuresFirst).
- Add `BuffReceiver` to minion prefabs if you want Rally to affect their move/attack speed.
- Add `BuildableInstance` to buildable prefabs and set the corresponding `CardDefinition.isBuildable = true` and `maxActive` cap.
- Add `Barricade` to barricade prefabs if you want Stomp to break them.
- Add `FoodPile` prefabs (NetworkObject + trigger collider) to the scene to test delivery.
- Add `CitadelHealth` to each Citadel object and assign each Throne’s `requiredCitadel`.
- Add `ThroneCapture` to each Throne with a trigger collider.
- Add `SuperCharge` to the player prefab to track charge.
- Add `SuperController` to the player prefab and assign a `SuperAbilityCatalog` with entries for Seismic Quake, Boulder Pitch, and Gorge.
- Add `KnockbackReceiver` to the player prefab to allow Seismic Quake knockback.
- Add a `BoulderPitchProjectile` prefab with `NetworkObject` + `NetworkTransform`, and assign it to your Super definition asset.
- Add `SuperUI` to your HUD, and wire its `chargeBar`, `chargeText`, and `superButton` (optional) to show/activate Supers.
- Add an input action named `Super` to the Player action map if you want to trigger Supers.
- Add an input action named `Ability5` to the Player action map to support the 5th Gerfunklet ability.
- Update `AbilityHotbarUI` to have 5 icon slots and wire the 5th slot sprite.
- Add `ThrownObject` is runtime-added by the Throw ability, but thrown targets must be networked (`NetworkObject`) to replicate.
- Assign `GameManager.citadelA/citadelB` and `GameManager.throneA/throneB`, plus enemy Citadel/Throne UI sliders/text if you want HUD updates.
### Ability Activation (Current Build)
- Abilities are bound to 5 hotbar slots (`Ability1`..`Ability5`) and can be used during Playing/Overtime.
- Super is bound to a separate `Super` input or Super HUD button when charge is full.
