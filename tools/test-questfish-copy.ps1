param([string]$Suite='questfish', [int]$TimeoutSec=420)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Join-Path $root 'artifacts/single-drop-game'
$storage=Join-Path $root 'artifacts/questfish-game'
# Mono/Harmony cannot import assemblies from a Unicode installation path.
$runtime=Join-Path $env:TEMP 'TK-QuestFish-20261024'
if(-not (Test-Path -LiteralPath $runtime)) {
    New-Item -ItemType Directory -Path $storage -Force | Out-Null
    New-Item -ItemType Junction -Path $runtime -Target $storage | Out-Null
    foreach($file in @('hollow_knight.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini','steam_appid.txt')) {
        Copy-Item -LiteralPath (Join-Path $source $file) -Destination $runtime
    }
    foreach($dir in @('hollow_knight_Data','MonoBleedingEdge','D3D12')) {
        New-Item -ItemType Junction -Path (Join-Path $runtime $dir) -Target (Join-Path $source $dir) | Out-Null
    }
    $bep=Join-Path $runtime 'BepInEx'
    New-Item -ItemType Directory -Path (Join-Path $bep 'plugins/TerraKnight') -Force | Out-Null
    foreach($dir in @('core','patchers')) {
        New-Item -ItemType Junction -Path (Join-Path $bep $dir) -Target (Join-Path $source ('BepInEx/'+$dir)) | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $source 'BepInEx/config') -Destination $bep -Recurse
}
& (Join-Path $PSScriptRoot 'test-single-drop-copy.ps1') -Suite $Suite -BuildDir artifacts/questfish-build -GameRoot $runtime -TimeoutSec $TimeoutSec
