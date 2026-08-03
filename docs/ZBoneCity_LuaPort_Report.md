# ZBoneCity Lua Port Report

## Source Review

Reviewed Z-City workshop source/content from:

- `C:\Program Files (x86)\Steam\steamapps\workshop\content\4000\3657285193\gmpublisher`
- `C:\Program Files (x86)\Steam\steamapps\workshop\content\4000\3544105055\gmpublisher`
- `C:\Program Files (x86)\Steam\steamapps\workshop\content\4000\3657294321\Z-City Content 1`
- `C:\Program Files (x86)\Steam\steamapps\workshop\content\4000\3657897364\Z-City Content 2`

Reviewed Lua Modding Framework API from:

- `C:\Users\gorde\AppData\Local\Temp\Rar$DRa19296.37146.rartemp\Mods\LuaMod`

## Lua Framework Capabilities

LuaMod supports `LuaBehaviour` lifecycle functions, custom event subscription/invocation, Unity object lookup, component lookup/addition by registered type name, spawn-by-barcode, physics casts, basic player rig access, 3D one-shot audio, renderer material assignment, basic input, BoneMenu, and safe file access.

Useful APIs:

- `API_Events.BL_SubscribeEvent`
- `API_Events.BL_InvokeEvent`
- `API_GameObject.BL_CreateEmptyGameObject`
- `API_GameObject.BL_GetComponent`
- `API_GameObject.BL_AddComponent`
- `API_GameObject.BL_SpawnByBarcode`
- `API_Player.BL_GetPhysicsRig`
- `API_Player.BL_PlayerHealth`
- `API_Physics.BL_RayCast`, `BL_SphereCast`, `BL_SphereCastAll`
- `API_Audio.BL_Play3DOneShot`

## Hard Limits

The Lua Framework does not expose enough direct API for a full one-to-one Z-City port:

- no direct Harmony patching from Lua;
- no reliable low-level BONELAB damage pipeline replacement from Lua alone;
- no direct LabFusion RPC/module registration API;
- no complete player ragdoll/muscle/IK control API;
- no Marrow pallet/crate/addressable editing from Lua;
- no safe way to replace ZBoneCity's existing C# HUD renderer from Lua without a bridge;
- no direct access to all internal ZBoneCity `HealthManager` fields unless exposed by C#.

## Portability Matrix

| System | Lua only | Needs C# bridge | Notes |
| --- | --- | --- | --- |
| Organism state math | Yes | No | Ported as `lua/ZBoneCity/Medical/organism.lua`. |
| Blood loss decay/regeneration | Yes | Optional | Ported as `lua/ZBoneCity/Blood/bleeding.lua`; C# owns actual damage events. |
| Pain decay/analgesia/adrenaline model | Yes | Optional | Ported as `lua/ZBoneCity/Pain/pain.lua`. |
| Shock progression | Yes | Optional | Ported as `lua/ZBoneCity/Shock/shock.lua`. |
| Fracture gameplay math | Partial | Yes | Lua can model effects; C# must apply BONELAB movement/hand effects. |
| Medical item definitions | Yes | Yes for physical use | Lua data added; C# still handles VR item touch/use. |
| Container contents | Yes | Yes for spawning prefabs | Lua defaults added; C# still opens/spawns real BONELAB items. |
| HUD data model | Yes | Yes for rendering | Lua can build status model; C# renders VR-safe UI. |
| Damage hooks | No | Yes | Requires existing Harmony/BONELAB hooks. |
| Ragdoll/unconscious control | No | Yes | Must remain in C# to avoid rig instability. |
| LabFusion sync | No | Yes | Must remain in C# Fusion bridge. |
| Audio playback | Partial | Yes for bank loading | Lua can play clips if provided; C# currently owns external audio bank. |
| Blood decals/pools | Partial | Yes | Lua can request; C# must pool/render decals safely. |

## Implemented Port Layer

Added Lua project structure:

- `lua/ZBoneCity/Core`
- `lua/ZBoneCity/Medical`
- `lua/ZBoneCity/Pain`
- `lua/ZBoneCity/Shock`
- `lua/ZBoneCity/Blood`
- `lua/ZBoneCity/Fractures`
- `lua/ZBoneCity/Inventory`
- `lua/ZBoneCity/Containers`
- `lua/ZBoneCity/HUD`
- `lua/ZBoneCity/Bridge`

Added a C# bridge:

- `src/LuaBridgeSystem.cs`

The bridge is optional and reflection-based. It does not add a hard dependency on LuaMod or MoonSharp. If LuaMod is absent, ZBoneCity continues running normally. If LuaMod is present, C# sends compact events:

- `ZBC_MedicalSnapshot`
- `ZBC_DamageProcessed`
- `ZBC_TreatmentApplied`

## Next Safe Step

Use Lua for portable gameplay formulas and balancing, then gradually let C# consume Lua outputs. Do not move Harmony patches, LabFusion networking, ragdoll control, Marrow item setup, or VR HUD rendering fully to Lua unless Lua Framework exposes stable APIs for them.
