# ZBoneCity chat history

This document is a project-facing export of the available Codex conversation context for ZBoneCity. It is not a verbatim platform transcript: the Codex app does not expose the full raw chat export to the local workspace. It preserves the practical history of requirements, design decisions, bug reports, and implementation passes that shaped the project.

## Project direction

The mod started as Z-BoneCity MEDIC and was renamed/reorganized into ZBoneCity. The goal became a tactical survival overhaul for BONELAB rather than a set of separate medical features.

Core goals repeatedly requested:

- A realistic integrated medical system.
- Stable BONELAB PC support through MelonLoader.
- Clean BoneMenu organization.
- LabFusion compatibility where possible.
- Proper Thunderstore/mod packaging.
- Polished HUD and visual effects.
- Bodycam as a native ZBoneCity feature.
- No duplicate standalone mods when functionality can be merged.

## Major systems requested and implemented over the conversation

### Medical overhaul

Requested systems included:

- Minor, moderate, severe, and arterial bleeding.
- Blood volume, blood pressure style feedback, pulse, oxygen, pain, shock, coma, unconsciousness, and death handling.
- Limb-specific injury tracking.
- Head trauma, concussion, dizziness, ringing ears, and fatal headshots.
- Neck trauma and rare neck snap mechanics.
- Fractures for arms, legs, ribs, and mobility penalties.
- Morphine, adrenaline, painkillers, stimulants, blood replacement, tourniquets, splints, bandages, medkits, surgical kits, and Tarkov-inspired items.
- Medical treatment paths that connect bleeding, fracture, pain, shock, and consciousness.

### Medical items and containers

Requested work included:

- Importing existing medical item models instead of creating replacement models.
- Converting items into BONELAB-style grabbable usable items.
- Adding a data-driven `MedicalContainer` system.
- Reworking the container workflow so contents are configured in the Unity Inspector instead of in-game.
- Creating an editor-style workflow similar to Grip Gizmo.
- Configuring Salewa, IFAK, Grizzly, Medkit, Auto Medkit, Surgical Medical Kit, bandages, tourniquet, splints, blood bag, morphine, adrenaline, ETG, SJ1, and painkillers.

### HUD and pain overlay

Important HUD requests included:

- Redesign the old debug-like HUD into a readable tactical medical interface.
- Remove the standalone Gore BoneMenu page.
- Restore missing HUD after regressions.
- Separate HUD from Pain Overlay so disabling the HUD does not disable injury visual effects.
- Remove `Medical Inventory` from the HUD.
- Remove the medical menu `System Log`/`Activity Log`.
- Make Pain Overlay a fullscreen screen-space effect instead of a floating world-space panel.
- Keep Pain Overlay visible to the VR player while keeping Bodycam monitor output clean.
- Improve HUD scale after it became too small in VR/mirror.

Recent HUD fixes:

- `Medical Inventory` was removed from HUD and BoneMenu.
- `Activity Log` / system log was removed from the medical menu and settings.
- HUD render mode was moved away from world-space presentation.
- Pain Overlay was kept as a fullscreen screen-space overlay and visually strengthened.

### Bodycam

Requested Bodycam work included:

- Integrate BodycamMod into ZBoneCity rather than keeping a separate mod.
- Replace the old REC banner with a native AXON Body 3 style overlay.
- Analyze the AXON Body 3 OBS overlay source and recreate it using Unity UI, TextMeshPro-style text, images, and audio.
- Keep chest and helmet camera modes, FOV, smoothing, forward offset, cinematic borders, AXON overlay, camera ID, and beep settings.
- Add presets, camera shake, digital noise, compression style, battery indicator, lens dirt/blood, and clean Bodycam rendering.
- Ensure Bodycam does not capture medical Pain Overlay.

### Audio

Requested audio work included:

- Add user-provided headshot, death, metal impact, heartbeat, gear, neck snap, and trauma sounds.
- Remove old beeps in critical/unconscious states.
- Add dynamic heartbeat based on severity.
- Add randomized death sounds with 3D spatial audio.
- Add sequential Gear Sounds during movement.
- Integrate usable audio resources from Z-City when license-compatible.

### Blood, forensics, and survival systems

Requested systems included:

- Blood pools, wall marks, footprints, handprints, blood on hands, and fade timers.
- Forensic traces after fights: bullet holes, casings, blood trails, blood loss estimation, and approximate time of death.
- Stress system with breathing, heartbeat, hand shake, and weapon stability effects.
- Weapon handling penalties from pain, stamina, and arm injuries.
- Rehabilitation after treatment instead of instant full recovery.
- Medication overdose and side effects.

### Persistent corpses and ragdoll stability

Requested work included:

- Persistent player corpses after death.
- Preserve ragdoll pose, physics, renderers, colliders, joints, and holsters.
- Do not delete old corpses during respawn.
- Allow physical item removal from body holsters.
- Clear corpses on scene change.
- Limit corpse count and lifetime for performance.

Several bug-fixing passes focused on:

- Player launching upward after respawn.
- Ragdoll jitter while unconscious.
- Toggle Ragdoll compatibility.
- Spawn protection and safe physics reset.
- Preventing ragdoll toggles from being interpreted as medical trauma.

Recent respawn fix:

- Added a forced spawn-safe unragdoll path.
- Zeroed velocity/angular velocity during spawn reset.
- Stabilized player physics before returning control.

### Z-City reference and license note

The user provided Z-City as a technical reference and later stated that its license permits using the code if attribution is preserved. The project should keep attribution that parts of the implementation are based on Z-City by sadsalat, subject to the source license.

Related docs added during the project:

- `docs/ZCityTechnicalReference.md`
- `docs/ZBoneCity_LuaPort_Report.md`
- `docs/THIRD_PARTY_NOTICES.md`

### Lua and LemonLoader exploration

Requested analysis included:

- Investigate Lua Modding Framework feasibility.
- Determine what Z-City systems could be moved to Lua.
- Use minimal C# bridge code only where Lua cannot access required BONELAB APIs.
- Create a separate LemonLoader version without modifying the MelonLoader version.

### Packaging and deployment

Packaging-related requests included:

- Build DLLs.
- Copy DLLs into the BONELAB `Mods` folder.
- Create Thunderstore packages.
- Create Mod.io/Marrow SDK package structures.
- Keep PC version specific changes where requested.
- Push the source project to GitHub.

## Current local repository target

The local project used for this export is:

```text
C:\Users\gorde\BonelabAdvancedHealth
```

The configured Git remote is:

```text
https://github.com/irembo337/BonelabAdvancedHealth.git
```

## Notes on this history export

- This file summarizes the available conversation context and project changes.
- It intentionally avoids publishing unrelated prompts found in local Codex state.
- It avoids including private temporary paths beyond project-relevant local path notes.
- A full verbatim ChatGPT/Codex transcript must be exported from the app UI if exact message-by-message history is required.
