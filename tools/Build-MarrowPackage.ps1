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

function Get-RelativeForwardPath {
    param(
        [string]$Base,
        [string]$Path
    )
    $baseUri = [Uri]((Resolve-Path $Base).Path.TrimEnd('\') + '\')
    $pathUri = [Uri](Resolve-Path $Path).Path
    return $baseUri.MakeRelativeUri($pathUri).ToString()
}

$barcode = "$Author.$PalletName"
$releaseRoot = Join-Path $ProjectRoot "release"
$marrowRoot = Join-Path $releaseRoot "Marrow"
$palletDir = Join-Path $marrowRoot $barcode
$zipPath = Join-Path $releaseRoot "ZBoneCity_Marrow_ModIO_v$Version.zip"
$dllPath = Join-Path $ProjectRoot "bin\$Configuration\ZBoneCity.dll"
$audioRoot = Join-Path $ProjectRoot "release\Z-Bonecity\UserData\BonelabAdvancedHealth\Audio"

if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "DLL not found: $dllPath. Build the C# mod first with dotnet build -c $Configuration."
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-CleanDirectory $marrowRoot
New-Item -ItemType Directory -Path $palletDir -Force | Out-Null

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
      "description": "ZBoneCity compatibility pallet. The official Marrow pallet lets BONELAB/mod.io recognize this package, while the gameplay runtime is included as a PC MelonLoader payload.",
      "unlockable": false,
      "redacted": false,
      "author": "$Author",
      "version": "$Version",
      "sdkVersion": "$SdkVersion",
      "internal": false,
      "crates": [],
      "dataCards": [],
      "tags": [
        "CodeMod",
        "Gameplay",
        "Health",
        "PC"
      ],
      "changelogs": [
        {
          "version": "$Version",
          "title": "Marrow SDK packaging compatibility",
          "text": "Converted the mod.io package to a Marrow SDK pallet layout with pallet.json, catalog, hash, and platform payload folders."
        }
      ],
      "isa": {
        "type": "pallet#0"
      }
    }
  }
}
"@
$palletJsonPath = Join-Path $palletDir "pallet.json"
Write-Utf8NoBom -Path $palletJsonPath -Value $palletJson
Write-Utf8NoBom -Path (Join-Path $palletDir "$barcode.pallet.json") -Value $palletJson

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
$catalogPath = Join-Path $palletDir "catalog_$barcode.json"
Write-Utf8NoBom -Path $catalogPath -Value $catalogJson
$catalogHash = (Get-FileHash -LiteralPath $catalogPath -Algorithm MD5).Hash.ToLowerInvariant()
Write-Utf8NoBom -Path (Join-Path $palletDir "catalog_$barcode.hash") -Value $catalogHash

$windowsPayload = Join-Path $palletDir "platforms\windows\MelonLoader\Mods"
New-Item -ItemType Directory -Path $windowsPayload -Force | Out-Null
Copy-Item -LiteralPath $dllPath -Destination (Join-Path $windowsPayload "ZBoneCity.dll") -Force

if (Test-Path -LiteralPath $audioRoot) {
    $audioTarget = Join-Path $palletDir "UserData\BonelabAdvancedHealth\Audio"
    $windowsAudioTarget = Join-Path $palletDir "platforms\windows\MelonLoader\UserData\BonelabAdvancedHealth\Audio"
    New-Item -ItemType Directory -Path $audioTarget -Force | Out-Null
    New-Item -ItemType Directory -Path $windowsAudioTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $audioRoot -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $audioTarget -Force
        Copy-Item -LiteralPath $_.FullName -Destination $windowsAudioTarget -Force
    }
}

$readme = @"
$Title
======================

This archive is structured as a Marrow SDK packed pallet so mod.io and BONELAB can recognize it as a BONELAB mod:

- $barcode/pallet.json
- $barcode/$barcode.pallet.json
- $barcode/catalog_$barcode.json
- $barcode/catalog_$barcode.hash

Gameplay runtime:
- The advanced health overhaul is a Harmony/MelonLoader runtime DLL.
- Official Marrow SDK pallets cannot execute Harmony patches by themselves.
- On PC, install the payload DLL from:
  $barcode/platforms/windows/MelonLoader/Mods/ZBoneCity.dll
  into:
  BONELAB/Mods/ZBoneCity.dll
- Optional custom trauma audio is included under:
  $barcode/platforms/windows/MelonLoader/UserData/BonelabAdvancedHealth/Audio
  and should be copied into:
  BONELAB/UserData/BonelabAdvancedHealth/Audio

Quest/Android:
- The Marrow pallet metadata is platform-neutral.
- The gameplay code requires a compatible code-mod loader. Stock BONELAB on Quest does not load MelonLoader DLLs from Marrow pallets.

Built:
$(Get-Date -Format "yyyy-MM-dd HH:mm:ss K")
"@
Write-Utf8NoBom -Path (Join-Path $palletDir "README_INSTALL.txt") -Value $readme

$androidReadme = @"
Android/Quest compatibility note
================================

This folder is intentionally metadata-only for official Marrow SDK compatibility.
The advanced health runtime is a PC MelonLoader DLL and is not executed by stock Quest BONELAB.
"@
Write-Utf8NoBom -Path (Join-Path $palletDir "platforms\android\README_ANDROID.txt") -Value $androidReadme

$metadata = @"
{
  "name": "$Title",
  "barcode": "$barcode",
  "author": "$Author",
  "version": "$Version",
  "marrowSdkVersion": "$SdkVersion",
  "palletFile": "pallet.json",
  "catalogFile": "catalog_$barcode.json",
  "catalogHashFile": "catalog_$barcode.hash",
  "windowsPayload": "platforms/windows/MelonLoader/Mods/ZBoneCity.dll",
  "android": "Marrow metadata only; runtime DLL requires a platform code-mod loader."
}
"@
Write-Utf8NoBom -Path (Join-Path $palletDir "mod_metadata.json") -Value $metadata

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipArchive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $palletDir -Recurse -File | ForEach-Object {
        $entryName = Get-RelativeForwardPath -Base $marrowRoot -Path $_.FullName
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

$pallet = Get-Content -LiteralPath (Join-Path $palletDir "pallet.json") -Raw | ConvertFrom-Json
if ($pallet.version -ne 2) {
    throw "Invalid pallet version."
}
if ($pallet.objects."1".barcode -ne $barcode) {
    throw "Pallet barcode mismatch."
}
if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Zip was not created."
}

$zipListing = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $hasPallet = $false
    foreach ($entry in $zipListing.Entries) {
        $normalizedEntryName = $entry.FullName.Replace('\', '/')
        if ($normalizedEntryName -eq "$barcode/pallet.json") {
            $hasPallet = $true
            break
        }
    }
    if (-not $hasPallet) {
        throw "Zip does not contain $barcode/pallet.json at the expected root."
    }
}
finally {
    $zipListing.Dispose()
}

[PSCustomObject]@{
    Zip = $zipPath
    Pallet = $palletDir
    Barcode = $barcode
    DllSha256 = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash
    CatalogMd5 = $catalogHash
    ZipSize = (Get-Item -LiteralPath $zipPath).Length
}
