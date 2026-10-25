param([string]$Bosses='MantisLord', [string]$BuildDir='artifacts/net-mantis-audit-build', [int]$TimeoutSec=480, [switch]$ClientOwns, [switch]$Slide)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root $BuildDir
$source=Join-Path $root 'artifacts/single-drop-game'
$run=Join-Path $root ('artifacts/netboss-copy-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$runtime=Join-Path $env:TEMP ('TK-NetBoss-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$runtimeStorage=Join-Path $run 'runtime'
New-Item -ItemType Directory -Path $run,$runtimeStorage -Force | Out-Null
New-Item -ItemType Junction -Path $runtime -Target $runtimeStorage | Out-Null
$player=Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
$before=@{}
Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)'} | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash}
$before | Export-Clixml -LiteralPath (Join-Path $run 'player-saves-before.xml')
$processes=@()
$timedOut=$false
$shared=@{}
foreach($name in @('AppConfig.ini','shared.dat','shared.dat.bak')) {
    $path=Join-Path $player $name
    $shared[$path]=if(Test-Path -LiteralPath $path) {[IO.File]::ReadAllBytes($path)} else {$null}
}
$regPath='Software\Team Cherry\Hollow Knight'
$key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regPath)
$registry=@{}
if($key) {foreach($name in $key.GetValueNames()) {$registry[$name]=@{Value=$key.GetValue($name);Kind=$key.GetValueKind($name)}};$key.Close()}
try {
    foreach($side in @('host','client')) {
        $game=Join-Path $runtime $side
        New-Item -ItemType Directory -Path $game -Force | Out-Null
        foreach($file in @('hollow_knight.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini','steam_appid.txt')) {Copy-Item -LiteralPath (Join-Path $source $file) -Destination $game}
        foreach($dir in @('hollow_knight_Data','MonoBleedingEdge','D3D12')) {New-Item -ItemType Junction -Path (Join-Path $game $dir) -Target (Join-Path $source $dir) | Out-Null}
        $bep=Join-Path $game 'BepInEx'
        New-Item -ItemType Directory -Path (Join-Path $bep 'plugins/TerraKnight') -Force | Out-Null
        foreach($dir in @('core','patchers')) {New-Item -ItemType Junction -Path (Join-Path $bep $dir) -Target (Join-Path $source ('BepInEx/'+$dir)) | Out-Null}
        Copy-Item -LiteralPath (Join-Path $source 'BepInEx/config') -Destination $bep -Recurse -Force
        foreach($file in @('TerraKnight.dll','TerraKnight.pdb')) {Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $bep 'plugins/TerraKnight')}
        $saves=Join-Path $run ($side+'-saves')
        New-Item -ItemType Directory -Path $saves -Force | Out-Null
        $suite=if($side -eq 'client') {'netboss_client'} elseif($ClientOwns) {'netboss_host_late_'+$Bosses} else {'netboss_host_'+$Bosses}
        if($Slide) {$suite=if($side -eq 'client') {'netslide_client'} elseif($ClientOwns) {'netslide_host_late'} else {'netslide_host'}}
        $arguments='-tk-copy "audit-'+$side+'" -tk-savedir "'+$saves+'" -tk-suite "'+$suite+'" -tk-slot 1 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "'+(Join-Path $run ($side+'Player.log'))+'"'
        $env:SteamAppId='367520'
        $env:SteamGameId='367520'
        $processes+=Start-Process -FilePath (Join-Path $game 'hollow_knight.exe') -WorkingDirectory $game -ArgumentList $arguments -WindowStyle Hidden -PassThru
    }
    $until=[DateTime]::UtcNow.AddSeconds($TimeoutSec)
    while([DateTime]::UtcNow -lt $until -and ($processes | Where-Object {-not $_.HasExited})) {Start-Sleep -Milliseconds 500}
    $timedOut=[bool]($processes | Where-Object {-not $_.HasExited})
} finally {
    foreach($proc in $processes) {if(-not $proc.HasExited) {$proc.Kill()};$proc.WaitForExit()}
    foreach($path in $shared.Keys) {
        if($null -ne $shared[$path]) {[IO.File]::WriteAllBytes($path,$shared[$path])}
        elseif(Test-Path -LiteralPath $path) {Remove-Item -LiteralPath $path -Force}
    }
    $key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($regPath)
    foreach($name in $key.GetValueNames()) {if(-not $registry.ContainsKey($name)) {$key.DeleteValue($name)}}
    foreach($name in $registry.Keys) {
        $value=$registry[$name].Value
        if($registry[$name].Kind -eq [Microsoft.Win32.RegistryValueKind]::DWord) {$value=[BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]([long]$value -band 4294967295)),0)}
        elseif($registry[$name].Kind -eq [Microsoft.Win32.RegistryValueKind]::Binary) {$value=[byte[]]$value}
        $key.SetValue($name,$value,$registry[$name].Kind)
    }
    $key.Close()
    $settingsRestored=$true
    foreach($path in $shared.Keys) {
        if($null -eq $shared[$path]) {if(Test-Path -LiteralPath $path) {$settingsRestored=$false}}
        elseif([Convert]::ToBase64String([IO.File]::ReadAllBytes($path)) -ne [Convert]::ToBase64String([byte[]]$shared[$path])) {$settingsRestored=$false}
    }
    $allLines=@()
    $completed=0
    foreach($side in @('host','client')) {
        $log=Join-Path $runtime ($side+'/BepInEx/TerraKnight/dev/audit-'+$side+'.log')
        if(Test-Path -LiteralPath $log) {
            Copy-Item -LiteralPath $log -Destination (Join-Path $run ($side+'AutoTest.log'))
            $lines=Get-Content -LiteralPath $log -Encoding UTF8
            $allLines+=$lines
            if($lines -match '\[NET_DONE\]') {$completed++}
        }
    }
    $failures=@($allLines | Where-Object {$_ -match '\[NET_FAIL\]|\[NET_TIMEOUT\]| at TerraKnight.Net.NetSession.Handle|\u5904\u7406\u6d88\u606f.*\u51fa\u9519|\u670d\u52a1\u5668\u5904\u7406.*\u51fa\u9519'} | ForEach-Object { '{0}' -f $_ })
    $changed=@($before.Keys | Where-Object {-not (Test-Path -LiteralPath $_) -or (Get-FileHash -LiteralPath $_).Hash -ne $before[$_]})
    $new=@(Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $before.ContainsKey($_.FullName)})
    @{Run=$run;Runtime=$runtime;Bosses=$Bosses;ClientOwns=[bool]$ClientOwns;Completed=($completed -eq 2);TimedOut=$timedOut;Failures=$failures;ChangedPlayerSaves=$changed;NewPlayerSaves=[string[]]@($new | ForEach-Object {$_.Name});RestoredSharedConfig=$settingsRestored;BuildHash=(Get-FileHash -LiteralPath (Join-Path $build 'TerraKnight.dll')).Hash} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding UTF8
    Write-Output "Evidence: $run"
}
if($timedOut -or $completed -ne 2 -or $failures.Count -or $changed.Count -or $new.Count -or -not $settingsRestored) {throw 'Two-client boss verification failed; inspect result.json and both logs.'}
