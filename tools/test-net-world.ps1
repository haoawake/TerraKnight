param([int]$TimeoutSec = 360, [string]$BuildDir = 'artifacts\net-fix-build', [ValidateSet('networld','net2','all')][string]$Suite = 'networld')
$ErrorActionPreference = 'Stop'
if ($Suite -eq 'all') {
    & $PSCommandPath -TimeoutSec $TimeoutSec -BuildDir $BuildDir -Suite networld
    & $PSCommandPath -TimeoutSec $TimeoutSec -BuildDir $BuildDir -Suite net2
    return
}
$root = Split-Path $PSScriptRoot -Parent
$hk = 'F:\SteamLibrary\steamapps\common\Hollow Knight'
$testBuild = Join-Path $root $BuildDir
$clientRoot = Join-Path $env:TEMP 'TerraKnight-NetClient-01a14090'
$clientDev = Join-Path $clientRoot 'BepInEx\TerraKnight\dev'
# Unity/Mono cannot reliably load this mod from a non-ASCII application directory.
# Separate BepInEx logs also avoid two instances competing for the same log file.
if (-not (Test-Path -LiteralPath (Join-Path $clientRoot 'hollow_knight.exe'))) {
    New-Item -ItemType Directory -Path $clientRoot -Force | Out-Null
    foreach ($file in @('hollow_knight.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini')) { Copy-Item -LiteralPath (Join-Path $hk $file) -Destination $clientRoot }
    foreach ($dir in @('hollow_knight_Data','MonoBleedingEdge','D3D12')) { New-Item -ItemType Junction -Path (Join-Path $clientRoot $dir) -Target (Join-Path $hk $dir) | Out-Null }
    $clientBep = Join-Path $clientRoot 'BepInEx'
    New-Item -ItemType Directory -Path $clientBep -Force | Out-Null
    foreach ($dir in @('core','plugins','patchers')) { New-Item -ItemType Junction -Path (Join-Path $clientBep $dir) -Target (Join-Path $hk ('BepInEx\' + $dir)) | Out-Null }
    Copy-Item -LiteralPath (Join-Path $hk 'BepInEx\config') -Destination $clientBep -Recurse -Force
}
$clientIni = Join-Path $clientRoot 'doorstop_config.ini'
[IO.File]::WriteAllText($clientIni, [regex]::Replace([IO.File]::ReadAllText($clientIni), '(?m)^enabled\s*=.*$', 'enabled = true'))
$clientCfg = Join-Path $clientRoot 'BepInEx\config\com.haoawake.terraknight.cfg'
$terraria = Join-Path (Split-Path $hk -Parent) 'Terraria'
[IO.File]::WriteAllText($clientCfg, [regex]::Replace([IO.File]::ReadAllText($clientCfg), '(?m)^TerrariaDir\s*=.*$', ('TerrariaDir = ' + $terraria)))
[IO.File]::WriteAllText((Join-Path $clientRoot 'steam_appid.txt'), '367520')
$playerSaves = Join-Path $env:USERPROFILE 'AppData\LocalLow\Team Cherry\Hollow Knight'
if (Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $hk 'hollow_knight.exe') }) { throw 'Game already running; refusing to interrupt it.' }
$run = Join-Path $root ('artifacts\' + $Suite + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$dev = Join-Path $hk 'BepInEx\TerraKnight\dev'
$tag = Get-Date -Format 'HHmmss'
$hostName = 'nwH-' + $tag
$clientName = 'nwC-' + $tag
$backups = @{}
foreach ($path in @((Join-Path $hk 'doorstop_config.ini'), (Join-Path $hk 'BepInEx\config\com.haoawake.terraknight.cfg'), (Join-Path $hk 'BepInEx\LogOutput.log'), (Join-Path $hk 'steam_appid.txt'), (Join-Path $playerSaves 'shared.dat'), (Join-Path $playerSaves 'shared.dat.bak'), (Join-Path $playerSaves 'AppConfig.ini'))) {
    $backups[$path] = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
}
$backups | Export-Clixml -LiteralPath (Join-Path $run 'files-before.xml')
$hashes = @{}
Get-ChildItem -LiteralPath $playerSaves -File | Where-Object { $_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' } | ForEach-Object { $hashes[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash }
$regPath = 'Software\Team Cherry\Hollow Knight'
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regPath)
$reg = @{}
foreach ($n in $key.GetValueNames()) { $reg[$n] = @{ Value = $key.GetValue($n); Kind = $key.GetValueKind($n) } }
$key.Close()
$reg | Export-Clixml -LiteralPath (Join-Path $run 'registry-before.xml')
$processes = @()
$timedOut = $false
try {
    Copy-Item -LiteralPath (Join-Path $testBuild 'TerraKnight.dll') -Destination (Join-Path $hk 'BepInEx\plugins\TerraKnight\TerraKnight.dll') -Force
    Copy-Item -LiteralPath (Join-Path $testBuild 'TerraKnight.pdb') -Destination (Join-Path $hk 'BepInEx\plugins\TerraKnight\TerraKnight.pdb') -Force
    $ini = Join-Path $hk 'doorstop_config.ini'
    [IO.File]::WriteAllText($ini, [regex]::Replace([IO.File]::ReadAllText($ini), '(?m)^enabled\s*=.*$', 'enabled = true'))
    [IO.File]::WriteAllText((Join-Path $hk 'steam_appid.txt'), '367520')
    $env:SteamAppId = '367520'
    $env:SteamGameId = '367520'
    $hostSaves = Join-Path $run 'host-saves'
    $clientSaves = Join-Path $run 'client-saves'
    New-Item -ItemType Directory -Path $hostSaves,$clientSaves -Force | Out-Null
    $hostArgs = '-tk-copy "' + $hostName + '" -tk-savedir "' + $hostSaves + '" -tk-suite ' + $Suite + 'host -tk-slot 1 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "' + (Join-Path $run 'HostPlayer.log') + '"'
    $hostProc = Start-Process -FilePath (Join-Path $hk 'hollow_knight.exe') -WorkingDirectory $hk -ArgumentList $hostArgs -WindowStyle Hidden -PassThru
    $processes += $hostProc
    $until = [DateTime]::UtcNow.AddSeconds(120)
    $seeded = $false
    $hostLog = Join-Path $dev ($hostName + '.log')
    $seedMarker = if ($Suite -eq 'networld') { '\[AutoTest\] world: seeded' } else { '\[AutoTest\].*7792' }
    while ([DateTime]::UtcNow -lt $until -and -not $hostProc.HasExited) {
        if ((Test-Path -LiteralPath $hostLog) -and ((Get-Content -LiteralPath $hostLog -Raw -Encoding UTF8) -match $seedMarker)) { $seeded = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $seeded) { throw 'Host did not seed fixtures; inspect HostPlayer.log.' }
    $clientArgs = '-tk-copy "' + $clientName + '" -tk-savedir "' + $clientSaves + '" -tk-suite ' + $Suite + 'client -tk-slot 1 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "' + (Join-Path $run 'ClientPlayer.log') + '"'
    $clientProc = Start-Process -FilePath (Join-Path $clientRoot 'hollow_knight.exe') -WorkingDirectory $clientRoot -ArgumentList $clientArgs -WindowStyle Hidden -PassThru
    $processes += $clientProc
    $until = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
    while ([DateTime]::UtcNow -lt $until -and ($processes | Where-Object { -not $_.HasExited })) { Start-Sleep -Milliseconds 500 }
    $timedOut = [bool]($processes | Where-Object { -not $_.HasExited })
} finally {
    foreach ($proc in $processes) { if (-not $proc.HasExited) { $proc.Kill() }; $proc.WaitForExit() }
    foreach ($path in $backups.Keys) {
        if ($null -ne $backups[$path]) {
            for ($attempt = 0; $attempt -lt 20; $attempt++) {
                try { [IO.File]::WriteAllBytes($path, $backups[$path]); break }
                catch [IO.IOException] { if ($attempt -eq 19) { throw }; Start-Sleep -Milliseconds 200 }
            }
        }
        elseif (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regPath, $true)
    foreach ($n in $key.GetValueNames()) { if (-not $reg.ContainsKey($n)) { $key.DeleteValue($n) } }
    foreach ($n in $reg.Keys) {
        $value = $reg[$n].Value
        if ($reg[$n].Kind -eq [Microsoft.Win32.RegistryValueKind]::DWord) { $value = [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]([long]$value -band 4294967295)), 0) }
        elseif ($reg[$n].Kind -eq [Microsoft.Win32.RegistryValueKind]::Binary) { $value = [byte[]]$value }
        $key.SetValue($n, $value, $reg[$n].Kind)
    }
    $key.Close()
    $allLines = @()
    foreach ($side in @(@($hostName,'Host'), @($clientName,'Client'))) {
        $sideDev = if ($side[1] -eq 'Client') { $clientDev } else { $dev }
        $log = Join-Path $sideDev ($side[0] + '.log')
        if (Test-Path -LiteralPath $log) {
            Copy-Item -LiteralPath $log -Destination (Join-Path $run ($side[1] + 'AutoTest.log'))
            $allLines += @(Get-Content -LiteralPath $log -Encoding UTF8)
        }
        $shots = Join-Path $sideDev $side[0]
        if (Test-Path -LiteralPath $shots) { Copy-Item -LiteralPath $shots -Destination (Join-Path $run ($side[1] + 'Screenshots')) -Recurse }
    }
    $changed = @($hashes.Keys | Where-Object { -not (Test-Path -LiteralPath $_) -or (Get-FileHash -LiteralPath $_).Hash -ne $hashes[$_] })
    $new = @(Get-ChildItem -LiteralPath $playerSaves -File | Where-Object { $_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $hashes.ContainsKey($_.FullName) })
    $settingsMatch = $true
    foreach ($path in $backups.Keys) {
        if ($null -eq $backups[$path]) { if (Test-Path -LiteralPath $path) { $settingsMatch = $false } }
        elseif ([Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -ne [Convert]::ToBase64String([byte[]]$backups[$path])) { $settingsMatch = $false }
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regPath)
    if ($key.GetValueNames().Count -ne $reg.Count) { $settingsMatch = $false }
    foreach ($n in $reg.Keys) {
        if ($key.GetValueKind($n) -ne $reg[$n].Kind) { $settingsMatch = $false; continue }
        $actual = $key.GetValue($n)
        $expected = $reg[$n].Value
        if ($reg[$n].Kind -eq [Microsoft.Win32.RegistryValueKind]::Binary) {
            if ([Convert]::ToBase64String([byte[]]$actual) -ne [Convert]::ToBase64String([byte[]]$expected)) { $settingsMatch = $false }
        } elseif ($reg[$n].Kind -eq [Microsoft.Win32.RegistryValueKind]::DWord) {
            if (([long]$actual -band 4294967295) -ne ([long]$expected -band 4294967295)) { $settingsMatch = $false }
        } elseif ($actual -ne $expected) { $settingsMatch = $false }
    }
    $key.Close()
    $failures = @($allLines | Where-Object { $_ -match '\[NET_FAIL\]|\[NET_TIMEOUT\]| at TerraKnight.Net.NetSession.Handle' } | ForEach-Object { '{0}' -f $_ })
    $completed = @($allLines | Where-Object { $_ -match '\[NET_DONE\]' }).Count -eq 2
    @{ TimedOut=$timedOut; Completed=$completed; Failures=$failures; ChangedPlayerSaves=$changed; NewPlayerSaves=[string[]]@($new | ForEach-Object { $_.Name }); RestoredSettings=$settingsMatch; Run=$run } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Evidence: $run"
}
if ($timedOut -or -not $completed -or $failures.Count -or $changed.Count -or $new.Count -or -not $settingsMatch) { throw 'Verification failed; inspect result.json and both logs.' }
