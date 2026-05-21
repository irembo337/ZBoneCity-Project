# Architecture Plan

## Goals

The mod is a single MelonLoader DLL for BONELAB IL2CPP. It uses generated BONELAB assemblies from the local installation and patches existing Marrow/BONELAB runtime classes with Harmony.

## Core Model

Each actor has a `HealthManager` with six `LimbHealth` records:

- Head
- Torso
- LeftArm
- RightArm
- LeftLeg
- RightLeg

Each limb tracks HP, fracture state, bleeding multiplier, and pain multiplier.

## Systems

`BleedingSystem`

- fixed bleed source pool
- stackable bleeding
- blood volume in milliliters
- timer-based blood loss
- unconsciousness and death thresholds

`ConsciousnessSystem`

- awake, blackout, unconscious, dead state machine
- forced player ragdoll through `PhysicsRig.RagdollRig`
- recovery probability based on blood and pain
- world-space visual overlay plus generated heartbeat/breathing audio clips

`FractureSystem`

- maps fractured legs to Marrow `Health.SetUsage` leg penalties
- maps fractured arms to hand grip reduction
- maps rib/torso fractures to breathing pain and spine usage reduction

`DamageProcessor`

- consumes real Marrow `Attack` and Unity `Collision` data
- maps BONELAB body-part enums to the mod body-part model
- classifies bullet, blunt, explosion, stab, and fall damage

`MedicalSystem`

- runtime-spawned Unity objects with Marrow grip components
- proximity-based VR application
- item behavior for bandage, tourniquet, morphine, and medkit

`HUDSystem`

- world-space Unity UI parented to the headset
- compact VR display for blood, pulse, pain, consciousness state, limb HP, bleeding, and fractures

## BONELAB Adapters

Harmony patches are deliberately thin. They convert game events into `DamageInfo` and forward them to the core health model. Game-facing effects are isolated in `MainMod`, `NPCHealth`, `HUDSystem`, and `MedicalSystem`.

This keeps gameplay logic independent from BONELAB internals and reduces the number of places that need updates if Marrow changes method internals.
