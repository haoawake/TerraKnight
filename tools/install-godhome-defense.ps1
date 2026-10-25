param([string]$GameRoot = 'F:\SteamLibrary\steamapps\common\Hollow Knight', [string]$BuildDir, [string]$ReportFile)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = if ($BuildDir) { $BuildDir } else { Join-Path $root 'artifacts/godhome-defense-build' }
$report = if ($ReportFile) { $ReportFile } else { Join-Path $root 'artifacts/godhome-defense-delivery.json' }
$exe = Join-Path $GameRoot 'hollow_knight.exe'
$dllHash = (Get-FileHash -LiteralPath (Join-Path $build 'TerraKnight.dll')).Hash
$atlasHash = (Get-FileHash -LiteralPath (Join-Path $build 'worldatlas.bin')).Hash
@{ State = 'WaitingForGameExit'; DLL = $dllHash; Atlas = $atlasHash } | ConvertTo-Json | Set-Content -LiteralPath $report -Encoding utf8
try {
    while ($running = @(Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })) {
        foreach ($process in $running) { $process.WaitForExit() }
    }
    $backup = Join-Path $root ('artifacts/installed-before-godhome-defense-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $targets = @{
        'TerraKnight.dll' = Join-Path $GameRoot 'BepInEx/plugins/TerraKnight/TerraKnight.dll'
        'TerraKnight.pdb' = Join-Path $GameRoot 'BepInEx/plugins/TerraKnight/TerraKnight.pdb'
        'worldatlas.bin' = Join-Path $GameRoot 'BepInEx/TerraKnight/worldatlas.bin'
    }
    foreach ($name in $targets.Keys) {
        $target = $targets[$name]
        if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $backup $name) }
        Copy-Item -LiteralPath (Join-Path $build $name) -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath (Join-Path $build $name)).Hash) { throw "Hash mismatch: $name" }
    }
    @{ State = 'InstalledAndVerified'; DLL = $dllHash; Atlas = $atlasHash; Backup = $backup; Rooms = 451; GodhomeRooms = 72 } | ConvertTo-Json | Set-Content -LiteralPath $report -Encoding utf8
} catch {
    @{ State = 'Failed'; Error = $_.ToString(); DLL = $dllHash; Atlas = $atlasHash } | ConvertTo-Json | Set-Content -LiteralPath $report -Encoding utf8
    throw
}
