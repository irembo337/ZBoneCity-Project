Z-BoneCity - Hybrid Marrow SDK + MelonLoader Package
===================================================

This package keeps the existing MelonLoader DLL mod and adds Marrow SDK compatible metadata around it:

- pallet.json
- CodexLabs.AdvancedHealthSystem.pallet.json
- catalog_CodexLabs.AdvancedHealthSystem.json
- catalog_CodexLabs.AdvancedHealthSystem.hash
- crates/ZBoneCity.RuntimePayload.crate.json
- Mods/ZBoneCity.dll

PC install:

1. Install MelonLoader for BONELAB.
2. Install BoneLib.
3. Copy Mods/ZBoneCity.dll into BONELAB/Mods/ZBoneCity.dll.
4. Copy UserData into the BONELAB folder.
5. Launch BONELAB.

The health body UI uses the exact image file packaged at:
UserData/BonelabAdvancedHealth/UI/health_body.png

mod.io note:

The root pallet.json and crate metadata are included so the archive is not a plain DLL-only upload.
The gameplay runtime remains a PC MelonLoader code mod.

Build: 1.0.0