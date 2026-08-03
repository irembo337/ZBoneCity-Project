# BONELAB Advanced Health System

Realistic trauma overhaul for BONELAB with a Tarkov-style health monitor, advanced limb damage, bleeding, organs, fractures, pain shock, unconsciousness, fullscreen trauma effects and BoneMenu controls.

## Requirements

- MelonLoader 0.7.x or newer
- BoneLib 3.x
- BONELAB on PC

## Features

- BoneMenu tab: `Advanced Health System`
- Live body status, organ status, bleeding, fractures, pulse, blood volume, oxygen, consciousness and pain data
- Fullscreen pain FX: blood pulse, tunnel vision, double vision, chromatic split, blur veil, focus loss and camera shake
- Pain backend with movement, aim, breathing and blackout penalties
- Head trauma and concussion logic with ringing, disorientation and delayed collapse
- Organ monitor for brain, heart, lungs, liver, stomach and muscles
- Native-first blood FX with pooled fallback spray and round blood pools
- Knife penetration backend with embedded wound state and bleeding spike on removal
- Persistent settings through MelonPreferences and BoneMenu

## Installation

Extract the archive into the BONELAB game folder so the DLL ends up here:

`BONELAB/Mods/BonelabAdvancedHealth.dll`

Launch the game, then open BoneMenu:

`Advanced Health System`

## Optional Audio

Optional local trauma tracks can be placed in:

`BONELAB/UserData/BonelabAdvancedHealth/Audio`

Supported file names:

- `zcity_unconscious.wav`
- `zcity_pain.wav`
- `zcity_headhit.wav`

Audio is not included in this package. Only distribute audio you own or have permission to use.

## Important

This is a MelonLoader code mod, not a standard MarrowSDK pallet. Users must install MelonLoader and BoneLib separately.
