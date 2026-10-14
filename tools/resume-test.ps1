param([string]$Suite = 'dreamnail', [int]$TimeoutSec = 600)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$hk = 'F:\SteamLibrary\steamapps\common\Hollow Knight'
$playerSaves = Join-Path $env:USERPROFILE 'AppData\LocalLow\Team Cherry\Hollow Knight'
if (Get-Process hollow_knight -ErrorAction SilentlyContinue) { throw 'Game already running; refusing to interrupt it.' }
$run = Join-Path $root ('artifacts\resume-' + $Suite + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$saveDir = Join-Path $run 'saves'
New-Item -ItemType Directory -Path $saveDir | Out-Null
$name = 'resume-' + $Suite
$dev = Join-Path $hk 'BepInEx\TerraKnight\dev'
$backups = @{}
foreach ($path in @((Join-Path $hk 'doorstop_config.ini'), (Join-Path $hk 'BepInEx\config\com.haoawake.terraknight.cfg'), (Join-Path $hk 'BepInEx\LogOutput.log'), (Join-Path $playerSaves 'shared.dat'), (Join-Path $playerSaves 'shared.dat.bak'), (Join-Path $playerSaves 'AppConfig.ini'))) {
    $backups[$path] = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
}
$appid = Join-Path $hk 'steam_appid.txt'
$backups[$appid] = if (Test-Path -LiteralPath $appid) { [IO.File]::ReadAllBytes($appid) } else { $null }
$backups | Export-Clixml -LiteralPath (Join-Path $run 'files-before.xml')
$hashes = @{}
Get-ChildItem -LiteralPath $playerSaves -File | Where-Object { $_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' } | ForEach-Object { $hashes[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName).Hash }
$regPath = 'Software\Team Cherry\Hollow Knight'
$key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regPath)
$reg = @{}
foreach ($n in $key.GetValueNames()) { $reg[$n] = @{ Value = $key.GetValue($n); Kind = $key.GetValueKind($n) } }
$key.Close()
$reg | Export-Clixml -LiteralPath (Join-Path $run 'registry-before.xml')
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'save-hashes-before.json')
$proc = $null
$timedOut = $false
try {
    Copy-Item -LiteralPath (Join-Path $root 'artifacts\resume-build\TerraKnight.dll') -Destination (Join-Path $hk 'BepInEx\plugins\TerraKnight\TerraKnight.dll') -Force
    Copy-Item -LiteralPath (Join-Path $root 'artifacts\resume-build\TerraKnight.pdb') -Destination (Join-Path $hk 'BepInEx\plugins\TerraKnight\TerraKnight.pdb') -Force
    $ini = Join-Path $hk 'doorstop_config.ini'
    [IO.File]::WriteAllText($ini, [regex]::Replace([IO.File]::ReadAllText($ini), '(?m)^enabled\s*=.*$', 'enabled = true'))
    $argsList = '-tk-copy "' + $name + '" -tk-savedir "' + $saveDir + '" -tk-suite "' + $Suite + '" -tk-slot 1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "' + (Join-Path $run 'Player.log') + '"'
    $env:SteamAppId = '367520'
    $env:SteamGameId = '367520'
    [IO.File]::WriteAllText($appid, '367520')
    $proc = Start-Process -FilePath (Join-Path $hk 'hollow_knight.exe') -WorkingDirectory $hk -ArgumentList $argsList -PassThru
    if (-not $proc.WaitForExit($TimeoutSec * 1000)) { $timedOut = $true; $proc.Kill(); $proc.WaitForExit() }
} finally {
    if ($proc -and -not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit() }
    foreach ($path in $backups.Keys) {
        if ($null -ne $backups[$path]) { [IO.File]::WriteAllBytes($path, $backups[$path]) }
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
    $settingsMatch = $true
    foreach ($path in $backups.Keys) {
        if ($null -eq $backups[$path]) { if (Test-Path -LiteralPath $path) { $settingsMatch = $false } }
        elseif (-not (Test-Path -LiteralPath $path) -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -ne [Convert]::ToBase64String([byte[]]$backups[$path])) { $settingsMatch = $false }
    }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regPath)
    if ($key.GetValueNames().Count -ne $reg.Count) { $settingsMatch = $false }
    foreach ($n in $reg.Keys) {
        $actual = $key.GetValue($n)
        $expected = $reg[$n].Value
        if ($reg[$n].Kind -eq [Microsoft.Win32.RegistryValueKind]::Binary) {
            if ([Convert]::ToBase64String([byte[]]$actual) -ne [Convert]::ToBase64String([byte[]]$expected)) { $settingsMatch = $false }
        } elseif ($reg[$n].Kind -eq [Microsoft.Win32.RegistryValueKind]::DWord) {
            if (([long]$actual -band 4294967295) -ne ([long]$expected -band 4294967295)) { $settingsMatch = $false }
        } elseif ($actual -ne $expected) { $settingsMatch = $false }
    }
    $key.Close()
    if (Test-Path -LiteralPath (Join-Path $dev ($name + '.log'))) { Copy-Item -LiteralPath (Join-Path $dev ($name + '.log')) -Destination (Join-Path $run 'AutoTest.log') }
    if (Test-Path -LiteralPath (Join-Path $dev $name)) { Copy-Item -LiteralPath (Join-Path $dev $name) -Destination (Join-Path $run 'screenshots') -Recurse }
    $changed = @($hashes.Keys | Where-Object { -not (Test-Path -LiteralPath $_) -or (Get-FileHash -LiteralPath $_).Hash -ne $hashes[$_] })
    $new = @(Get-ChildItem -LiteralPath $playerSaves -File | Where-Object { $_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $hashes.ContainsKey($_.FullName) })
    $testLog = Join-Path $run 'AutoTest.log'
    $failures = @()
    $completed = $false
    if (Test-Path -LiteralPath $testLog) {
        $lines = Get-Content -LiteralPath $testLog
        $failures = @($lines | Where-Object { $_ -match '\[AutoTest\].*不通过' })
        $completed = [bool]($lines | Where-Object { $_ -match '\[AutoTest\] 完成' })
    }
    @{ Suite = $Suite; TimedOut = $timedOut; Completed = $completed; Failures = $failures; Run = $run; ChangedPlayerSaves = $changed; NewPlayerSaves = [string[]]@($new | ForEach-Object { $_.Name }); RestoredSettings = $settingsMatch } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
    Write-Output "Evidence: $run"
    Write-Output "TimedOut=$timedOut; changed player saves=$($changed.Count); new player saves=$($new.Count); settings restored"
}
if ($timedOut) { throw 'Autotest timed out; inspect the saved logs.' }
if (-not $completed -or $failures.Count -or $changed.Count -or $new.Count -or -not $settingsMatch) { throw 'Verification failed; inspect result.json and the saved logs.' }
