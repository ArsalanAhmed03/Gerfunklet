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
- Match end shows result text and reward ratio (winner 100% / loser 25%).

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

### Respawn (GDD core)
- When the Gerfunklet is defeated, it respawns after 8 seconds at its Millstone Pedestal.
- If the Gerfunklet is carrying the Millstone, it drops on death.

### Buildables (Partial)
- Buildable cards can enforce a per-card active cap (default max 2) using `BuildableInstance`.

### ATP (Economy – Partial)
- ATP is server-authoritative with defaults from the GDD: cap 10, regen 0.9/s, start 4, global spend GCD 0.5s.
- Minion spawn cost is enforced when an AtpResource component is present on the player.

### Resource Nodes (Harvesters/Scouts)
- Resource nodes store a small amount of energy (default 3) and respawn after a delay when depleted.
- Harvesters gather from friendly/neutral nodes, then carry ATP back to their team deposit.
- Scouts can steal from enemy-owned nodes when `allowTheft` is enabled.
- Low-HP harvesters/scouts retreat toward their deposit.

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
- Sleep transition uses a 0.8s delay before the sleeping state fully applies (configurable).
- Feast ring: allied minions can deliver food piles; on wake up to 5 piles are consumed to restore stamina and grant Well-Fed (+1.0/s for 6s per stack, max 5 stacks).

## Implemented But Diverging From GDD
- Objective zones still exist in the scene flow, but are disabled by default in `MatchManager`.
- Throne capture uses a simple channel gated by Citadel destroyed; Citadel damage is currently just proximity damage.
- ObjectiveZone wins are disabled by default to match GDD win paths (can be re-enabled in MatchManager).

## Not Yet Implemented (GDD)
- Full Citadel/Throne flow (structure attacks, siege units, damage tuning).
- Advanced card-hand UX (drag/ghost previews, placement feedback, card cooldown/GCD UI).
- Expanded minion roster variety (new units, advanced per-role AI behaviors).
- Remote Config, analytics events, and security rules.

## Scene/Prefab Changes Required To Use New Systems
- Add `AtpResource` to the player prefab to enable ATP regen/spend.
- Set `LocalSpawner.minionAtpCost` to match the intended minion cost (default is 2).
- Add an `AtpUI` component to your HUD and wire its slider/text fields to display ATP.
- Assign `GameManager.staminaBar` if you want the stamina UI to update.
- Assign `GameManager.sleepingIndicator` (any GameObject) if you want a visible sleeping overlay.
- Assign `GameManager.lowStaminaIndicator` and add `PulseUI` if you want the pulsing low-stamina warning at <=10%.
- Add `CardHand` to the player prefab and assign a `CardCatalog` (or default deck) to initialize cards.
- Add `DeploymentRules` to the player prefab and optionally set `homeAnchor`, `baseDeployRadius`, `forwardDeployRadius`, and `midlineX`.
- Add `CardPlacementController` to the player prefab or HUD and assign `placementMask` to your ground layer; optionally set input actions for place/cancel.
- Assign a `placementIndicator` (quad/mesh) on `CardPlacementController` if you want green/red valid placement feedback.
- Add `ForwardBeacon` to the Forward Beacon buildable prefab so it unlocks the forward deploy ring for its owner.
- If you want blocked/hazard placement checks, set `DeploymentRules.blockingMask` to a layer that contains non-placeable colliders.
- Assign `spawnPrefab` and `spawnWarmupSeconds` on each `CardDefinition` asset you want to be playable.
- On `CardHandUI`, optionally assign `slotCooldownFills` (Image overlays) to show the global hand GCD.
- Add input actions named `MillstoneDrop` and `MillstoneThrow` to the Player action map if you want manual drop/throw controls.
- Add `MillstoneStatusUI` to your HUD; assign `carriedIcon` (top bar), and a world-space `arrowIndicator` (optional).
- Add `MillstoneAltarHalo` to each Millstone Altar and assign a child `haloVisual` to show when the carrier is in range.
- Add input actions named `Rest` and `Wake` to the Player action map if you want manual sleep/force-wake controls.
- Add `FeastRing` to the player prefab with a trigger collider (1.5 radius); it auto-assigns owner from the player `NetworkObject`.
- Add `MinionOwner` and `FoodCarrier` to minion prefabs if you want them to pick up and deliver food.
- Add `MinionForageAgent` to minion prefabs to allow server-directed food foraging.
- Add `MinionGatherer` to Harvester/Scout prefabs to enable resource harvesting/stealing.
- Add `ResourceNode` objects to the scene (NetworkObject) and set `maxEnergy`, `respawnSeconds`, and `autoAssignOwner` as needed.
- Add `ResourceDeposit` objects (trigger + NetworkObject) near each base so harvesters can deposit ATP.
- Add `ForageModeController` to the player prefab to control Protect/Balanced/Max Forage modes.
- Add `ForageModeUI` to your HUD and wire its three buttons/highlights to change forage mode while sleeping.
- Add `FeastCounterUI` to your HUD if you want to display stored feast piles.
- Add `MinionStats` to minion prefabs to set per-unit damage/speed/attack range and targeting (e.g., Brute = StructuresFirst).
- For Spewer-like AoE, enable `MinionStats.useAoeAttack` and tune `aoeDamage/aoeRadius/aoeThreshold`.
- For Acolyte-like healing, enable `MinionStats.canHealAllies` and tune `healAmount/interval/range/threshold`.
- Add `BuffReceiver` to minion prefabs if you want Rally to affect their move/attack speed.
- Sleeping Gerfunklet auto-forms a protect ring: non-foraging minions guard around the player and engage nearby enemies.
- Add `BuildableInstance` to buildable prefabs and set the corresponding `CardDefinition.isBuildable = true` and `maxActive` cap.
- Add `BuildableHealth` to buildable prefabs so minions and Boulder Pitch can damage/destroy them.
- Flame Siphon/Brazier: add `BuildableAuraDamage` and tune radius/damage/tick.
- Obelisk Turret: add `BuildableTurret` and tune range/damage/interval.
- Rally Banner: add `BuildableRallyAura` and tune radius/move/attack buffs.
- Food Cache: add `BuildableFoodCache` and assign FoodPile prefabs (small/medium/big).
- Ward Totem: add `WardTotem` to block enemy placement within its radius (DeploymentRules handles it).
- Add `Barricade` to barricade prefabs if you want Stomp to break them.
- Add `FoodDropper` to minions/buildables/crates to spawn food piles on death (assign small/medium/big `FoodPile` prefabs).
- Add `FoodPile` prefabs (NetworkObject + trigger collider) to the scene to test delivery.
- Add `CitadelHealth` to each Citadel object and assign each Throne’s `requiredCitadel`.
- Add `ThroneCapture` to each Throne with a trigger collider.
- Add `SuperCharge` to the player prefab to track charge.
- Add `SuperController` to the player prefab and assign a `SuperAbilityCatalog` with entries for Seismic Quake, Boulder Pitch, and Gorge.
- Add `KnockbackReceiver` to the player prefab to allow Seismic Quake knockback.
- Add a `BoulderPitchProjectile` prefab with `NetworkObject` + `NetworkTransform`, and assign it to your Super definition asset.
- Assign optional `impactFxPrefab` on thrown objects (Millstone Head, Throw targets, Boulder Pitch) if you want hit feedback.
- Add `SuperUI` to your HUD, and wire its `chargeBar`, `chargeText`, and `superButton` (optional) to show/activate Supers.
- Add an input action named `Super` to the Player action map if you want to trigger Supers.
- Add an input action named `Ability5` to the Player action map to support the 5th Gerfunklet ability.
- Update `AbilityHotbarUI` to have 5 icon slots and wire the 5th slot sprite.
- Add `ThrownObject` is runtime-added by the Throw ability, but thrown targets must be networked (`NetworkObject`) to replicate.
- Add results UI to the HUD and wire `GameManager.resultsRoot` + `GameManager.rewardsText` (optional `rematchButton`).
- Respawn uses the `MillstonePedestal` assigned to the player’s ownerClientId; ensure pedestals are present and owned.
- Assign `GameManager.citadelA/citadelB` and `GameManager.throneA/throneB`, plus enemy Citadel/Throne UI sliders/text if you want HUD updates.
- Add `PlayerMeleeAttack` to the player prefab to enable Gerfunklet basic attacks (150 damage, 3s interval).
- Ensure `PlayerMovement.moveSpeed = 2.1` and `carryMoveSpeedMultiplier = 1.3/2.1` on the player prefab (GDD move speeds).
### Ability Activation (Current Build)
- Abilities are bound to 5 hotbar slots (`Ability1`..`Ability5`) and can be used during Playing/Overtime.
- Super is bound to a separate `Super` input or Super HUD button when charge is full.
