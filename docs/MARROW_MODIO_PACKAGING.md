# Marrow SDK / mod.io Packaging

This project contains two layers:

- `BonelabAdvancedHealth.dll`: the PC MelonLoader/Harmony runtime.
- `release/Marrow/CodexLabs.AdvancedHealthSystem`: a Marrow SDK-compatible pallet wrapper used by BONELAB/mod.io package validation.

Official Marrow SDK content is organized as pallets with crates and data cards. Stress Level Zero's documentation says the SDK packs a pallet into a folder named in the format `Author.PalletName`, and that this folder should be zipped with its structure intact for mod.io distribution.

## Build

```powershell
dotnet build -c Release -p:InstallAfterBuild=false
powershell -ExecutionPolicy Bypass -File .\tools\Build-MarrowPackage.ps1 -Configuration Release -Version 1.0.0
```

Output:

```text
release\BonelabAdvancedHealth_Marrow_ModIO_v1.0.0.zip
```

Expected zip root:

```text
CodexLabs.AdvancedHealthSystem/
  pallet.json
  CodexLabs.AdvancedHealthSystem.pallet.json
  catalog_CodexLabs.AdvancedHealthSystem.json
  catalog_CodexLabs.AdvancedHealthSystem.hash
  mod_metadata.json
  README_INSTALL.txt
  platforms/
    windows/
      MelonLoader/
        Mods/
          BonelabAdvancedHealth.dll
    android/
      README_ANDROID.txt
  UserData/
    BonelabAdvancedHealth/
      Audio/
```

## Compatibility Notes

The Marrow pallet makes the archive recognizable as a BONELAB SDK mod. The health overhaul itself is still a runtime code mod and requires MelonLoader-compatible loading on PC. Stock Quest/Android BONELAB does not execute MelonLoader DLLs from a Marrow pallet.
