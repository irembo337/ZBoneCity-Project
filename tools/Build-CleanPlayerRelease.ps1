param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
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

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$releaseRoot = Join-Path $ProjectRoot "release"
$exportRoot = Join-Path $releaseRoot "CleanPlayerRelease"
$packageRoot = Join-Path $exportRoot "ZBoneCity"
$modsDir = Join-Path $packageRoot "Mods"
$userDataDir = Join-Path $packageRoot "UserData\ZBoneCity"
$bodycamAxonSource = Join-Path $ProjectRoot "assets\Bodycam\Axon"
$bodycamAxonTarget = Join-Path $modsDir "ZBoneCity\Axon"
$audioSources = @(
    (Join-Path $ProjectRoot "assets\Audio")
)
$uiSource = Join-Path $ProjectRoot "assets\UserData\BonelabAdvancedHealth\UI"
$iconSource = Join-Path $ProjectRoot "assets\Release\icon.png"
$dllSource = Join-Path $ProjectRoot "bin\$Configuration\ZBoneCity.dll"
$dllTarget = Join-Path $modsDir "ZBoneCity.dll"
$zipPath = Join-Path $releaseRoot "ZBoneCity-Release.zip"

if (-not (Test-Path -LiteralPath $dllSource)) {
    throw "DLL not found: $dllSource. Run dotnet build -c $Configuration first."
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-CleanDirectory $exportRoot
New-Item -ItemType Directory -Path $modsDir -Force | Out-Null
New-Item -ItemType Directory -Path $userDataDir -Force | Out-Null

Copy-Item -LiteralPath $dllSource -Destination $dllTarget -Force
if (Test-Path -LiteralPath $iconSource) {
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $packageRoot "icon.png") -Force
}

$configText = @"
# ZBoneCity
# Runtime config is created by MelonPreferences after first launch.
# You can change settings in-game through BoneMenu / ZBoneCity.

version=$Version
package=ZBoneCity
loader=MelonLoader
game=BONELAB
"@
Write-Utf8NoBom -Path (Join-Path $userDataDir "ZBoneCity.cfg") -Value $configText

$audioTarget = Join-Path $userDataDir "Audio"
New-Item -ItemType Directory -Path $audioTarget -Force | Out-Null
foreach ($sourceRoot in $audioSources) {
    if (Test-Path -LiteralPath $sourceRoot) {
        Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object { $_.Extension -match '^\.(wav|mp3|ogg|txt)$' } | ForEach-Object {
            $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
            $targetPath = Join-Path $audioTarget $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $targetPath) -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $targetPath -Force
        }
    }
}

if (Test-Path -LiteralPath $uiSource) {
    $uiTarget = Join-Path $userDataDir "UI"
    New-Item -ItemType Directory -Path $uiTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $uiSource -File | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $uiTarget -Force
    }
}

if (Test-Path -LiteralPath $bodycamAxonSource) {
    New-Item -ItemType Directory -Path $bodycamAxonTarget -Force | Out-Null
    Get-ChildItem -LiteralPath $bodycamAxonSource -File | Where-Object { $_.Extension -match '^\.(png|wav|ttf|js|txt)$' } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $bodycamAxonTarget -Force
    }
}

$readme = @"
ZBoneCity
==========================================

INSTALL:

1. Install MelonLoader for BONELAB.
2. Open your BONELAB install folder.
   Usually:
   C:\Program Files (x86)\Steam\steamapps\common\BONELAB
3. Copy:
   Mods\ZBoneCity.dll
   into:
   BONELAB\Mods\ZBoneCity.dll
4. Copy the UserData folder from this archive into the BONELAB folder.
   Final path should look like:
   BONELAB\UserData\ZBoneCity\
5. Launch BONELAB.

WHAT IS INCLUDED:

- Mods\ZBoneCity.dll
- icon.png
- THIRD_PARTY_NOTICES.txt
- UserData\ZBoneCity\ZBoneCity.cfg
- UserData\ZBoneCity\UI\health_body.png and HUD status icons
- UserData\ZBoneCity\Audio\heartthump.wav / heartthump-heavy.wav
- UserData\ZBoneCity\Audio\*.wav / *.mp3 / *.ogg
- Mods\ZBoneCity\Axon\logo.png / beep.wav / KlartextMonoBold.ttf / config.js

WHAT IS NOT INCLUDED:

- source code
- project files
- build cache
- SDK folders
- developer tools

NOTES:

This is the clean public player release.
It is a MelonLoader code mod package for PC BONELAB.

THIRD-PARTY NOTICE:

Parts of ZBoneCity's medical-system behavior, code, and selected audio resources are based on and adapted from the original Z-City project by sadsalat, used under the MIT-3 license.
Original Z-City workshop reference:
https://steamcommunity.com/workshop/filedetails/?id=3657285193

Build: $Version
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "README.txt") -Value $readme

$thirdPartyNotices = @"
ZBoneCity Third-Party Notices
==========================================

Z-City
------

Parts of ZBoneCity's medical-system behavior, code, and selected audio resources are based on and adapted from the original Z-City project by sadsalat, used under the MIT-3 license.

Original Z-City workshop reference:
https://steamcommunity.com/workshop/filedetails/?id=3657285193

This notice must remain included in public ZBoneCity releases.
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "THIRD_PARTY_NOTICES.txt") -Value $thirdPartyNotices

$manifest = @"
{
  "name": "ZBoneCity",
  "version": "$Version",
  "game": "BONELAB",
  "loader": "MelonLoader",
  "dll": "Mods/ZBoneCity.dll",
  "userData": "UserData/ZBoneCity",
  "publicRelease": true
}
"@
Write-Utf8NoBom -Path (Join-Path $packageRoot "manifest.json") -Value $manifest

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$zipArchive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $packageRoot -Recurse -File | ForEach-Object {
        $baseUri = [Uri]($packageRoot.TrimEnd('\') + '\')
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

$requiredEntries = @(
    "Mods/ZBoneCity.dll",
    "icon.png",
    "UserData/ZBoneCity/Audio/Headshot.wav",
    "UserData/ZBoneCity/Audio/Death/death1.wav",
    "UserData/ZBoneCity/Audio/Gear/gear1.wav",
    "Mods/ZBoneCity/Axon/logo.png",
    "Mods/ZBoneCity/Axon/beep.wav",
    "Mods/ZBoneCity/Axon/KlartextMonoBold.ttf",
    "Mods/ZBoneCity/Axon/config.js",
    "UserData/ZBoneCity/ZBoneCity.cfg",
    "UserData/ZBoneCity/UI/health_body.png",
    "UserData/ZBoneCity/UI/health_head.png",
    "UserData/ZBoneCity/UI/status_shock.png",
    "UserData/ZBoneCity/UI/status_pain_icon.png",
    "UserData/ZBoneCity/Audio/heartthump.wav",
    "UserData/ZBoneCity/Audio/heartthump-heavy.wav",
    "README.txt",
    "THIRD_PARTY_NOTICES.txt"
)

$zipRead = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @{}
    foreach ($entry in $zipRead.Entries) {
        $entries[$entry.FullName.Replace('\', '/')] = $true
    }
    foreach ($required in $requiredEntries) {
        if (-not $entries.ContainsKey($required)) {
            throw "Release zip missing required entry: $required"
        }
    }
}
finally {
    $zipRead.Dispose()
}

[PSCustomObject]@{
    Zip = $zipPath
    Folder = $packageRoot
    Dll = $dllTarget
    DllSha256 = (Get-FileHash -LiteralPath $dllTarget -Algorithm SHA256).Hash
    ZipSize = (Get-Item -LiteralPath $zipPath).Length
}
