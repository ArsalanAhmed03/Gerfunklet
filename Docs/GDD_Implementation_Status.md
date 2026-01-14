# GDD Implementation Status (Index)

This file tracks GDD features, their implementation status, and exact deltas vs the GDD. Update this file whenever a related change is made.

Legend:
- Status: Complete | Partial | Not Started
- Deltas: what differs from the GDD or is missing

---

## GDD-01 Match Flow
- Status: Complete
- Implemented:
  - Phases: WaitingForPlayers -> LoadoutSelect (Mulligan) -> Countdown -> Playing -> Overtime -> MatchEnded
  - Mulligan + countdown flow
- Results:
  - Match end shows result text and reward ratio (winner 100% / loser 25%)
- Key Files:
  - `Assets/Scripts/Managers/MatchManager.cs`
  - `Assets/Scripts/Managers/GameManager.cs`
  - `Assets/Scripts/UI/CardHandUI.cs`

## GDD-02 Win Conditions (Millstone Plant, Throne Capture)
- Status: Complete
- Implemented:
  - Millstone Plant: 2.5s channel
  - Throne Capture: 4.0s channel gated by Citadel destroyed
- Key Files:
  - `Assets/Scripts/Objectives/MillstoneAltar.cs`
  - `Assets/Scripts/Objectives/ThroneCapture.cs`
  - `Assets/Scripts/Objectives/CitadelHealth.cs`

## GDD-03 Loss Conditions
- Status: Complete
- Implemented:
  - Player death exists
  - 8s respawn timer at Millstone Pedestal
  - Millstone drops on death if carried
- Key Files:
  - `Assets/Scripts/Player/PlayerStatsManager.cs`
  - `Assets/Scripts/Objectives/MillstonePedestal.cs`
  - `Assets/Scripts/Objectives/MillstoneCarrier.cs`

## GDD-04 Millstone Carry Rules
- Status: Complete
- Implemented:
  - Carry, drop, throw
  - Drop on stun/knockback
  - Carry move penalty
  - Contested ring + 1s reclaim hold on dropped heads
  - UI feedback (top bar icon, altar halo, arrow ping)
- Key Files:
  - `Assets/Scripts/Objectives/MillstoneHead.cs`
  - `Assets/Scripts/Objectives/MillstoneCarrier.cs`
  - `Assets/Scripts/Objectives/MillstoneAltarHalo.cs`
  - `Assets/Scripts/UI/MillstoneStatusUI.cs`
  - `Assets/Scripts/Player/PlayerMovement.cs`

## GDD-05 ATP + Hand System
- Status: Complete
- Implemented:
  - ATP cap 10, regen 0.9/s, start 4, GCD 0.5s
  - Deck size 8, hand size 4
  - Mulligan up to 2 swaps
- Key Files:
  - `Assets/Scripts/Player/AtpResource.cs`
  - `Assets/Scripts/Cards/CardHand.cs`
  - `Assets/Scripts/UI/CardHandUI.cs`

## GDD-06 Summoning & Zones
- Status: Complete
- Implemented:
  - Deployment ring validation
  - Placement feedback indicator (valid/invalid)
  - Card spawn warmups
- Key Files:
  - `Assets/Scripts/Cards/DeploymentRules.cs`
  - `Assets/Scripts/Cards/CardHand.cs`
  - `Assets/Scripts/Cards/CardPlacementController.cs`
  - `Assets/Scripts/Buildables/ForwardBeacon.cs`

## GDD-07 Match Pacing (Overtime Bonuses)
- Status: Complete
- Implemented:
  - Base duration 3:30 (210s)
  - Overtime duration 60s
  - Overtime bonuses (ATP +15%, warmup -50%, Citadel damage +10%)
- Key Files:
  - `Assets/Scripts/Managers/MatchManager.cs`
  - `Assets/Scripts/Objectives/CitadelHealth.cs`

## GDD-08 Gerfunklet Core Stats
- Status: Complete
- Implemented:
  - GDD stat values encoded (HP 5000, attack power 150, attack interval 3s, move speed 2.1/1.3)
  - Gerfunklet melee attack system
- Key Files:
  - `Assets/Scripts/Player/PlayerStatsManager.cs`
  - `Assets/Scripts/Player/PlayerMovement.cs`
  - `Assets/Scripts/Player/PlayerMeleeAttack.cs`

## GDD-09 Stamina & Sleep/Wake
- Status: Complete
- Implemented:
  - Stamina drain/regen
  - Sleep/wake + under-fire penalty
  - Manual Rest/Wake inputs
- Key Files:
  - `Assets/Scripts/Player/PlayerStatsManager.cs`
  - `Assets/Scripts/Player/PlayerMovement.cs`
  - `Assets/Scripts/UI/PulseUI.cs`

## GDD-10 Food & Feast
- Status: Complete
- Implemented:
  - Food piles + delivery
  - Feast ring consume + well-fed buff
  - Food drop sources (via FoodDropper)
  - Forage mode toggles + UI
- Key Files:
  - `Assets/Scripts/Economy/FoodPile.cs`
  - `Assets/Scripts/Enemy/FoodCarrier.cs`
  - `Assets/Scripts/Player/FeastRing.cs`
  - `Assets/Scripts/Economy/FoodDropper.cs`
  - `Assets/Scripts/Enemy/MinionForageAgent.cs`
  - `Assets/Scripts/Player/ForageModeController.cs`
  - `Assets/Scripts/UI/ForageModeUI.cs`
  - `Assets/Scripts/UI/FeastCounterUI.cs`

## GDD-11 Gerfunklet Abilities (Stomp/Devour/Rally/Parry/Throw)
- Status: Complete
- Implemented:
  - All five core abilities with GDD-aligned rules
  - Throw now throws objects/minions in an arc
- Key Files:
  - `Assets/Scripts/Abilities/Definitions/StompAbilityDefinition.cs`
  - `Assets/Scripts/Abilities/Definitions/DevourAbilityDefinition.cs`
  - `Assets/Scripts/Abilities/Definitions/RallyAbilityDefinition.cs`
  - `Assets/Scripts/Abilities/Definitions/ParryAbilityDefinition.cs`
  - `Assets/Scripts/Abilities/Definitions/ThrowAbilityDefinition.cs`
  - `Assets/Scripts/Abilities/ThrownObject.cs`
  - `Assets/Scripts/Player/PlayerMeleeAttack.cs`

## GDD-12 Super Abilities (Seismic/Boulder/Gorge)
- Status: Complete
- Implemented:
  - Super charge sources + activation
  - Seismic knockback, Boulder structure damage, Gorge devour chain + CC immunity
- Key Files:
  - `Assets/Scripts/Abilities/Receivers/SuperCharge.cs`
  - `Assets/Scripts/Abilities/SuperController.cs`
  - `Assets/Scripts/Abilities/Definitions/SuperAbilityDefinition.cs`

## GDD-13 Respawn Logic
- Status: Complete
- Implemented:
  - 8s respawn timer at Millstone Pedestal
- Key Files:
  - `Assets/Scripts/Player/PlayerStatsManager.cs`

## GDD-14 Minion Roster & AI
- Status: Complete
- Implemented:
  - Minion stats + basic AI
  - Auto-targeting vs units/structures
  - Spewer-style AoE attack support
  - Acolyte-style ally heal support
  - Sleeping protect ring + forage behavior
  - Harvester resource nodes (gather -> carry -> deposit ATP)
  - Scout resource theft from enemy nodes
  - Harvester/Scout retreat to base when low HP
  - Expanded roster abilities (Jester/Shriek, Skirmisher/Swift Strike, Brawler/Frenzy, Infiltrator/Overload, Avian Scout/Mark, Crusher/Charge, Runner/Impact, Sentinel/Zone, Gloom/Bind, Beetle/Burst, Siphon/Energy Drain, Plasma/Overcharge)
  - Role-specific helpers (kite behavior, retreat on low HP, preferred target roles)
- Key Files:
  - `Assets/Scripts/Enemy/MinionStats.cs`
  - `Assets/Scripts/Enemy/MinionAI.cs`
  - `Assets/Scripts/Enemy/MinionForageAgent.cs`
  - `Assets/Scripts/Enemy/MinionGatherer.cs`
  - `Assets/Scripts/Enemy/MinionSonicShriek.cs`
  - `Assets/Scripts/Enemy/MinionSwiftStrike.cs`
  - `Assets/Scripts/Enemy/MinionFrenziedAssault.cs`
  - `Assets/Scripts/Enemy/MinionSystemOverload.cs`
  - `Assets/Scripts/Enemy/MinionMarkOnHit.cs`
  - `Assets/Scripts/Enemy/MinionUnstoppableCharge.cs`
  - `Assets/Scripts/Enemy/MinionBurstingImpact.cs`
  - `Assets/Scripts/Enemy/MinionZoneDeployment.cs`
  - `Assets/Scripts/Enemy/ZoneField.cs`
  - `Assets/Scripts/Enemy/MinionShadowBind.cs`
  - `Assets/Scripts/Enemy/MinionVolatileBurst.cs`
  - `Assets/Scripts/Enemy/BurningPatch.cs`
  - `Assets/Scripts/Enemy/MinionEnergySiphon.cs`
  - `Assets/Scripts/Enemy/MinionOvercharge.cs`
  - `Assets/Scripts/Enemy/MinionKiteBehavior.cs`
  - `Assets/Scripts/Enemy/MinionRetreatOnLowHealth.cs`
  - `Assets/Scripts/Enemy/MinionTargetingProfile.cs`
  - `Assets/Scripts/Economy/ResourceNode.cs`
  - `Assets/Scripts/Economy/ResourceDeposit.cs`
  - `Assets/Scripts/Player/ForageModeController.cs`

## GDD-15 Buildables & Objective Props
- Status: Complete
- Implemented:
  - Buildable caps
  - Buildable health + destruction
  - Minion targeting against buildables
  - Objective props (Millstone, Pedestal, Altar, Citadel, Throne)
  - Buildable behaviors (Flame Siphon, Food Cache, Rally Banner, Obelisk Turret, Brazier, Ward Totem)
- Key Files:
  - `Assets/Scripts/Buildables/BuildableInstance.cs`
  - `Assets/Scripts/Buildables/BuildableHealth.cs`
  - `Assets/Scripts/Buildables/BuildableAuraDamage.cs`
  - `Assets/Scripts/Buildables/BuildableTurret.cs`
  - `Assets/Scripts/Buildables/BuildableRallyAura.cs`
  - `Assets/Scripts/Buildables/BuildableFoodCache.cs`
  - `Assets/Scripts/Buildables/WardTotem.cs`
  - `Assets/Scripts/Enemy/MinionAI.cs`
  - `Assets/Scripts/Objectives/*`

## GDD-16 Object Interaction System
- Status: Complete
- Implemented:
  - Millstone pickup/drop/throw
  - Throw ability for objects/minions
  - Collision feedback hooks for thrown objects
- Key Files:
  - `Assets/Scripts/Objectives/MillstoneHead.cs`
  - `Assets/Scripts/Abilities/Definitions/ThrowAbilityDefinition.cs`
  - `Assets/Scripts/Abilities/ThrownObject.cs`
  - `Assets/Scripts/Abilities/BoulderPitchProjectile.cs`

## GDD-17 UI/UX (HUD + Feedback)
- Status: Partial
- Implemented:
  - Core HUD elements + phase gating
- Deltas:
  - Super HUD polish missing
- Key Files:
  - `Assets/Scripts/Managers/GameManager.cs`
  - `Assets/Scripts/UI/*`
