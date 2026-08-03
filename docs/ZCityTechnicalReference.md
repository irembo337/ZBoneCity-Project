# Z-City Technical Reference Notes

Z-City is used as a technical and licensing reference for ZBoneCity's medical gameplay work. The user confirmed the available Z-City source/license as MIT-3, so ZBoneCity may adapt and port Z-City code where it fits the BONELAB/MelonLoader architecture, provided attribution is kept in the project and release package.

Attribution text for releases:

```text
Parts of ZBoneCity's medical-system behavior and code are based on and adapted from the original Z-City project by sadsalat, used under the MIT-3 license.
```

## Relevant Architecture Observations

- Z-City keeps the character medical state in a central organism object, then lets smaller modules update blood, pulse, pain, lungs, liver, stamina, and random medical events.
- Damage routing is separated from ongoing vital simulation. Hit processing adds wounds, pain, organ trauma, shock, and internal bleeding; later ticks derive blood loss, consciousness risk, pulse, and critical state.
- Network/UI data is sent as a reduced state snapshot instead of forcing each visual system to recalculate the full medical model.
- Blood loss and arterial wounds are driven by pulse/heartbeat, which creates a more consistent relationship between panic, shock, bleeding, and consciousness.
- Screen effects and audio respond to derived medical state such as pain, shock, blood loss, oxygen loss, brain trauma, and unconsciousness.

## ZBoneCity Adaptation

- Added `MedicalTelemetrySystem` as a ZBoneCity-native equivalent of a reduced organism snapshot.
- Added `ZCityOrganismProfile` as the first direct C# port/adaptation layer for Z-City organism behavior under the confirmed MIT-3 attribution.
- Ported and integrated the Z-City-style organism fields that make sense in BONELAB:
  - blood type
  - consciousness
  - fear/fearAdd
  - shock
  - average pain
  - analgesia
  - adrenaline
  - hemotransfusion shock
  - temperature
  - pulse/heartbeat
  - critical/incapacitated state
  - recoil/leg/melee output multipliers
- The telemetry snapshot is derived from existing ZBoneCity systems rather than replacing them:
  - `BleedingSystem`
  - `ShockSystem`
  - `StressSystem`
  - `TacticalVitalsSystem`
  - `PainSystem`
  - `LungDamageSystem`
  - `BrainTraumaSystem`
  - `ConsciousnessSystem`
  - `ZCityOrganismProfile`
- HUD pulse, shock, blood-loss display, and heartbeat audio now consume `HealthManager.TelemetrySnapshot`.
- This keeps ZBoneCity's current architecture intact while making future HUD, Bodycam, audio, and LabFusion sync work from one consistent medical state.
- Pain is now more persistent and less instantly "healed" by stimulants. Adrenaline masks pain temporarily instead of clearing the underlying trauma.
- Fractures now create an immediate systemic reaction: fracture sound, pain spike, shock pressure, stress, and brief collapse pressure for serious leg fractures.
- Medkits were adjusted toward Z-City behavior: they stabilize and treat light bleeding, but no longer act as an instant full fracture repair or blood replacement.
- Fullscreen trauma effects now use the telemetry snapshot so pain, blood loss, shock, oxygen loss, and consciousness risk drive one coherent visual state.
- Weapon handling now consumes Z-City-style `recoilmul`, `legstrength`, and `meleespeed` outputs so fear, pain, medication, and fractures affect weapon stability and movement through the existing ZBoneCity systems.
- Blood now slowly regenerates only when bleeding is controlled and the simulated pulse is strong enough, matching the Z-City concept that blood recovery is physiological rather than an instant medkit effect.

## Porting Rule

Z-City logic may be ported only when it is adapted into ZBoneCity's C# systems and BONELAB/BoneLib/Marrow SDK architecture. Do not blindly paste Lua implementation details that depend on Garry's Mod runtime behavior; preserve attribution and keep the port maintainable.
