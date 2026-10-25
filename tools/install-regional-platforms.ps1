param([string]$Game='F:\SteamLibrary\steamapps\common\Hollow Knight', [string]$BuildDir='artifacts/regional-platforms-build', [string]$EvidenceDir='artifacts/regional-platforms')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root $EvidenceDir
$build=Join-Path $root $BuildDir
$verification=Get-Content -LiteralPath (Join-Path $out 'verification.json') -Raw | ConvertFrom-Json
if(-not $verification.passed) {throw 'Runtime validation has not passed.'}
$candidate=Join-Path $build 'TerraKnight.dll'
$hash=(Get-FileHash -LiteralPath $candidate).Hash.ToLowerInvariant()
if($hash -ne $verification.dll_sha256) {throw 'Candidate has changed since validation.'}
$destination=Join-Path $Game 'BepInEx/plugins/TerraKnight'
if(-not (Test-Path -LiteralPath $destination)) {throw 'Configured plugin directory does not exist.'}
$backup=Join-Path $out ('installed-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach($name in @('TerraKnight.dll','TerraKnight.pdb')) {
    $existing=Join-Path $destination $name
    if(Test-Path -LiteralPath $existing) {Copy-Item -LiteralPath $existing -Destination $backup}
}
# A running Mono process maps the DLL and prevents overwriting it. Keep that mapped
# file intact under a backup name, then publish the fully staged replacement.
foreach($name in @('TerraKnight.dll','TerraKnight.pdb')) {
    $active=Join-Path $destination $name
    $staged=Join-Path $destination ($name+'.pending-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    $retired=Join-Path $destination ($name+'.old-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    $source=Join-Path $build $name
    Copy-Item -LiteralPath $source -Destination $staged
    if((Get-FileHash -LiteralPath $staged).Hash -ne (Get-FileHash -LiteralPath $source).Hash) {throw "Staged hash mismatch: $name"}
    Move-Item -LiteralPath $active -Destination $retired
    try {Move-Item -LiteralPath $staged -Destination $active}
    catch {
        if(-not (Test-Path -LiteralPath $active)) {Move-Item -LiteralPath $retired -Destination $active}
        throw
    }
}
$installed=(Get-FileHash -LiteralPath (Join-Path $destination 'TerraKnight.dll')).Hash.ToLowerInvariant()
if($installed -ne $hash) {throw 'Installed DLL hash mismatch.'}
$running=@(Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $Game 'hollow_knight.exe')}).Count -gt 0
$record=@{installed=$true;path=$destination;dll_sha256=$installed;backup=$backup;running_game_not_restarted=$running;effective_on_next_game_start=$true}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'installation.json') -Encoding utf8
$record | ConvertTo-Json -Compress
