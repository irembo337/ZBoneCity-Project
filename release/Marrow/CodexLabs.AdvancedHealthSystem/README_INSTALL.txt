Advanced Health System
======================

This archive is structured as a Marrow SDK packed pallet so mod.io and BONELAB can recognize it as a BONELAB mod:

- CodexLabs.AdvancedHealthSystem/pallet.json
- CodexLabs.AdvancedHealthSystem/CodexLabs.AdvancedHealthSystem.pallet.json
- CodexLabs.AdvancedHealthSystem/catalog_CodexLabs.AdvancedHealthSystem.json
- CodexLabs.AdvancedHealthSystem/catalog_CodexLabs.AdvancedHealthSystem.hash

Gameplay runtime:
- The advanced health overhaul is a Harmony/MelonLoader runtime DLL.
- Official Marrow SDK pallets cannot execute Harmony patches by themselves.
- On PC, install the payload DLL from:
  CodexLabs.AdvancedHealthSystem/platforms/windows/MelonLoader/Mods/BonelabAdvancedHealth.dll
  into:
  BONELAB/Mods/BonelabAdvancedHealth.dll
- Optional custom trauma audio is included under:
  CodexLabs.AdvancedHealthSystem/platforms/windows/MelonLoader/UserData/BonelabAdvancedHealth/Audio
  and should be copied into:
  BONELAB/UserData/BonelabAdvancedHealth/Audio

Quest/Android:
- The Marrow pallet metadata is platform-neutral.
- The gameplay code requires a compatible code-mod loader. Stock BONELAB on Quest does not load MelonLoader DLLs from Marrow pallets.

Built:
2026-05-25 18:38:44 +02:00