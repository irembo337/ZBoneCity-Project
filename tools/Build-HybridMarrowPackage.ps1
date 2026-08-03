param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$Author = "CodexLabs",
    [string]$PalletName = "AdvancedHealthSystem",
    [string]$Title = "ZBoneCity",
    [string]$SdkVersion = "1.2.0",
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function New-CleanDirectory {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Value
    )
    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

function Add-ZipDirectory {
    param(
        [string]$Source,
        [string]$ZipPath
    )
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }

    $zipArchive = [System.IO.Compression.ZipFile]::Open($ZipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $Source -Recurse -File | ForEach-Object {
            $baseUri = [Uri]($Source.TrimEnd('\') + '\')
            $pathUri = [Uri]$_.FullName
            $entryName = $baseUri.MakeRelativeUri($pathUri).ToString()
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zipArchive,
                $_.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $zipArchive.Dispose()
    }
}

$barcode = "$Author.$PalletName"
$crateBarcode = "$barcode.RuntimePayload"
$releaseRoot = Join-Path $ProjectRoot "release"
$exportRoot = Join-Path $releaseRoot "HybridMarrowModIO"
$packageRoot = Join-Path $exportRoot "ZBoneCity-Hybrid"
$zipPath = Join-Path $releaseRoot "ZBoneCity-Hybrid-Marrowsdk-ModIO-v$Version.zip"
$dllSource = Join-Path $ProjectRoot "bin\$Configuration\ZBoneCity.dll"
$audioSource = Join-Path $ProjectRoot "release\Z-Bonecity\UserData\BonelabAdvancedHealth\Audio"
$uiSource = Join-Path $ProjectRoot "assets\UserData\BonelabAdvancedHealth\UI"
$iconSource = Join-Path $ProjectRoot "assets\Release\icon.png"
$sdkAssetsSource = Join-Path $ProjectRoot "marrow\AdvancedHealthSystemUnity\Assets\AdvancedHealthSystem"

if (-not (Test-Path -LiteralPath $dllSource)) {
    throw "DLL not found: $dllSource. Run dotnet build -c $Configuration first."
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-CleanDirectory $exportRoot
New-Item -ItemType Directory -Path (Join-Path $packageRoot "Mods") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot "crates") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot "assets") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot "UserData\BonelabAdvancedHealth") -Force | Out-Null

$dllTarget = Join-Path $packageRoot "Mods\ZBoneCity.dll"
Copy-Item -LiteralPath $dllSource -Destination $dllTarget -Force
if (Test-Path -LiteralPath $iconSource) {
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $packageRoot "icon.png") -Force
}

$palletJson = @"
{
  "version": 2,
  "root": {
    "ref": "1",
    "type": "pallet#0"
  },
  "objects": {
    "1": {
      "barcode": "$barcode",
      "title": "$Title",
      "description": "Hybrid Marrow SDK compatibility wrapper for the existing PC MelonLoader DLL runtime. The pallet metadata makes the archive recognizable as a BONELAB SDK-style mod while Mods/ZBoneCity.dll keeps the Harmony/MelonLoader gameplay systems intact.",
      "unlockable": false,
      "redacted": false,
      "author": "$Author",
      "version": "$Version",
      "sdkVersion": "$SdkVersion",
      "internal": false,
      "crates": [
        {
          "ref": "2",
          "type": "crate#0"
        }
      ],
      "dataCards": [],
      "tags": [
        "BONELAB",
        "MarrowSDK",
        "MelonLoader",
        "CodeMod",
        "Health"
      ],
      "changelogs": [
        {
          "version": "$Version",
          "title": "Hybrid SDK wrapper",
          "text": "Added root pallet.json, crate metadata, SDK manifest files, and a clean Mods/ZBoneCity.dll payload for mod.io upload packaging."
        }
      ],
      "isa": {
        "type": "pallet#0"
      }
    },
    "2": {
      "barcode": "$crateBarcode",
      "title": "ZBoneCity Runtime Payload",
      "description": "PC MelonLoader runtime payload included with the SDK compatibility pallet.",
      "pallet": "$barcode",
      "version": "$Version",
      "platform": "windows",
      "payload": "Mods/ZBoneCity.dll",
      "isa": {
        "type": "crate#0"
      }
    }
  }
}
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "pallet.json") -Value $palletJson
Write-Utf8NoBom -Path (Join-Path $packageRoot "$barcode.pallet.json") -Value $palletJson

$crateJson = @"
{
  "barcode": "$crateBarcode",
  "pallet": "$barcode",
  "title": "ZBoneCity Runtime Payload",
  "type": "HybridMelonLoaderRuntime",
  "version": "$Version",
  "platforms": [
    "Windows",
    "PCVR"
  ],
  "payload": "Mods/ZBoneCity.dll",
  "requires": [
    "BONELAB Patch 6",
    "MelonLoader",
    "BoneLib"
  ]
}
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "crates\ZBoneCity.RuntimePayload.crate.json") -Value $crateJson

$medicalItems = @("Bandage", "Tourniquet", "Morphine", "Painkillers", "Adrenaline", "Medkit", "Splint", "BloodPack")
foreach ($medicalItem in $medicalItems) {
    $medicalCrateBarcode = "$barcode.Medical.$medicalItem"
    $medicalCrate = @"
{
  "barcode": "$medicalCrateBarcode",
  "pallet": "$barcode",
  "title": "AHS Medical $medicalItem",
  "type": "SpawnableCrate",
  "version": "$Version",
  "unityPrefab": "Assets/AdvancedHealthSystem/MedicalItems/AHS_Medical_$medicalItem.prefab",
  "legacyFallbackPrefab": "Assets/AdvancedHealthSystem/GeneratedMedicalItems/AHS_Medical_$medicalItem.prefab",
  "runtimeFallback": "Mods/ZBoneCity.dll can create a safe physical fallback with Rigidbody, Collider, InteractableHost, grip point, and Marrow grip component.",
  "platforms": [
    "Windows",
    "PCVR"
  ]
}
"@
    Write-Utf8NoBom -Path (Join-Path $packageRoot "crates\AHS_Medical_$medicalItem.crate.json") -Value $medicalCrate
}

$catalogJson = @"
{
  "m_LocatorId": "AddressablesMainContentCatalog",
  "m_BuildResultHash": "$([Guid]::NewGuid().ToString("N"))",
  "m_InstanceProviderData": {
    "m_Id": "UnityEngine.ResourceManagement.ResourceProviders.InstanceProvider",
    "m_ObjectType": {
      "m_AssemblyName": "Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
      "m_ClassName": "UnityEngine.ResourceManagement.ResourceProviders.InstanceProvider"
    },
    "m_Data": ""
  },
  "m_SceneProviderData": {
    "m_Id": "UnityEngine.ResourceManagement.ResourceProviders.SceneProvider",
    "m_ObjectType": {
      "m_AssemblyName": "Unity.ResourceManager, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
      "m_ClassName": "UnityEngine.ResourceManagement.ResourceProviders.SceneProvider"
    },
    "m_Data": ""
  },
  "m_ResourceProviderData": [],
  "m_ProviderIds": [],
  "m_InternalIds": [],
  "m_KeyDataString": "",
  "m_BucketDataString": "",
  "m_EntryDataString": "",
  "m_ExtraDataString": "",
  "m_resourceTypes": [],
  "m_InternalIdPrefixes": []
}
"@
$catalogPath = Join-Path $packageRoot "catalog_$barcode.json"
Write-Utf8NoBom -Path $catalogPath -Value $catalogJson
$catalogHash = (Get-FileHash -LiteralPath $catalogPath -Algorithm MD5).Hash.ToLowerInvariant()
Write-Utf8NoBom -Path (Join-Path $packageRoot "catalog_$barcode.hash") -Value $catalogHash

$configText = @"
# ZBoneCity
version=$Version
package=ZBoneCity-Hybrid
loader=MelonLoader
sdkWrapper=MarrowSDK
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "UserData\BonelabAdvancedHealth\ZBoneCity.cfg") -Value $configText

$audioSources = @(
    (Join-Path $ProjectRoot "assets\Audio"),
    $audioSource
)
$publicAudioTarget = Join-Path $packageRoot "ZBoneCity\Audio"
$legacyAudioTarget = Join-Path $packageRoot "UserData\BonelabAdvancedHealth\Audio"
New-Item -ItemType Directory -Path $publicAudioTarget -Force | Out-Null
New-Item -ItemType Directory -Path $legacyAudioTarget -Force | Out-Null
foreach ($sourceRoot in $audioSources) {
    if (Test-Path -LiteralPath $sourceRoot) {
        Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object { $_.Extension -match '^\.(wav|mp3|ogg|txt)$' } | ForEach-Object {
            $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
            $publicTargetPath = Join-Path $publicAudioTarget $relative
            $legacyTargetPath = Join-Path $legacyAudioTarget $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $publicTargetPath) -Force | Out-Null
            New-Item -ItemType Directory -Path (Split-Path -Parent $legacyTargetPath) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $publicTargetPath -Force
            Copy-Item -LiteralPath $_.FullName -Destination $legacyTargetPath -Force
        }
    }
}

$legacyHeadHit = Join-Path $publicAudioTarget "zcity_headhit.wav"
$publicHeadshot = Join-Path $publicAudioTarget "Headshot.wav"
if (-not (Test-Path -LiteralPath $publicHeadshot) -and (Test-Path -LiteralPath $legacyHeadHit)) {
    Copy-Item -LiteralPath $legacyHeadHit -Destination $publicHeadshot -Force
    Copy-Item -LiteralPath $legacyHeadHit -Destination (Join-Path $legacyAudioTarget "Headshot.wav") -Force
}

if (Test-Path -LiteralPath $uiSource) {
    $uiTarget = Join-Path $packageRoot "UserData\BonelabAdvancedHealth\UI"
    New-Item -ItemType Directory -Path $uiTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $uiSource -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $uiTarget -Force
    }
}

$assetsReadme = @"
This folder is reserved for Marrow SDK asset payloads.
Medical item prefab builder/editor assets are included under assets/AdvancedHealthSystem.
The runtime DLL also generates matching grabbable medical items in-game under Mods/ZBoneCity.dll.
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "assets\README_ASSETS.txt") -Value $assetsReadme

if (Test-Path -LiteralPath $sdkAssetsSource) {
    $sdkAssetsTarget = Join-Path $packageRoot "assets\AdvancedHealthSystem"
    New-Item -ItemType Directory -Path $sdkAssetsTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $sdkAssetsSource | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $sdkAssetsTarget -Recurse -Force
    }
}

$manifest = @"
{
  "name": "ZBoneCity",
  "title": "$Title",
  "version": "$Version",
  "game": "BONELAB",
  "pallet": "$barcode",
  "marrowSdkVersion": "$SdkVersion",
  "hybridRuntime": true,
  "loader": "MelonLoader",
  "dll": "Mods/ZBoneCity.dll",
  "crateMetadata": "crates/ZBoneCity.RuntimePayload.crate.json"
}
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "manifest.json") -Value $manifest

$sdkMetadata = @"
{
  "packageKind": "HybridMarrowSdkMelonLoader",
  "barcode": "$barcode",
  "crateBarcode": "$crateBarcode",
  "palletFile": "pallet.json",
  "catalogFile": "catalog_$barcode.json",
  "catalogHashFile": "catalog_$barcode.hash",
  "dllPayload": "Mods/ZBoneCity.dll",
  "modIoUploadRoot": ".",
  "notes": "Stock Marrow SDK content recognition is provided by pallet metadata. Runtime Harmony gameplay still requires PC MelonLoader/BoneLib."
}
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "sdk_metadata.json") -Value $sdkMetadata

$readme = @"
ZBoneCity - Hybrid Marrow SDK + MelonLoader Package
===================================================

This package keeps the existing MelonLoader DLL mod and adds Marrow SDK compatible metadata around it:

- pallet.json
- $barcode.pallet.json
- catalog_$barcode.json
- catalog_$barcode.hash
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

Build: $Version
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "README.txt") -Value $readme

Add-ZipDirectory -Source $packageRoot -ZipPath $zipPath

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipRead = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @{}
    foreach ($entry in $zipRead.Entries) {
        $entries[$entry.FullName.Replace('\', '/')] = $true
    }

    $requiredEntries = @(
        "pallet.json",
        "$barcode.pallet.json",
        "catalog_$barcode.json",
        "catalog_$barcode.hash",
        "crates/ZBoneCity.RuntimePayload.crate.json",
        "crates/AHS_Medical_Bandage.crate.json",
        "crates/AHS_Medical_Tourniquet.crate.json",
        "crates/AHS_Medical_Morphine.crate.json",
        "crates/AHS_Medical_Adrenaline.crate.json",
        "crates/AHS_Medical_Medkit.crate.json",
        "crates/AHS_Medical_Splint.crate.json",
        "crates/AHS_Medical_BloodPack.crate.json",
        "assets/README_ASSETS.txt",
        "assets/AdvancedHealthSystem/Editor/MedicalItemPrefabBuilder.cs",
        "Mods/ZBoneCity.dll",
        "UserData/BonelabAdvancedHealth/UI/health_body.png",
        "manifest.json",
        "sdk_metadata.json",
        "README.txt"
    )

    foreach ($required in $requiredEntries) {
        if (-not $entries.ContainsKey($required)) {
            throw "Hybrid Marrow zip missing required entry: $required"
        }
    }
}
finally {
    $zipRead.Dispose()
}

$pallet = Get-Content -LiteralPath (Join-Path $packageRoot "pallet.json") -Raw | ConvertFrom-Json
if ($pallet.version -ne 2) {
    throw "Invalid pallet version."
}
if ($pallet.objects."1".barcode -ne $barcode) {
    throw "Pallet barcode mismatch."
}

[PSCustomObject]@{
    Zip = $zipPath
    Folder = $packageRoot
    Barcode = $barcode
    RuntimeDll = $dllTarget
    DllSha256 = (Get-FileHash -LiteralPath $dllTarget -Algorithm SHA256).Hash
    CatalogMd5 = $catalogHash
    ZipSize = (Get-Item -LiteralPath $zipPath).Length
}
