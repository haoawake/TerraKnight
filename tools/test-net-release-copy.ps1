param(
 [ValidateSet('networld','net2','netloot','netboss','netrelease')][string]$Suite='networld',
 [string]$BuildDir='artifacts/release-readiness-build',
 [string]$Bosses='MantisLord', [switch]$ClientOwns, [int]$TimeoutSec=480
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root $BuildDir
$template=Join-Path $root 'artifacts/single-drop-game'
$port=@{networld=7792;net2=7792;netloot=7797;netboss=7796;netrelease=7804}[$Suite]
if(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue){throw "Test port $port is already occupied; no existing process will be stopped."}
$run=Join-Path $root ('artifacts/net-release-'+$Suite+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$storage=Join-Path $run 'runtime'
$alias=Join-Path $env:TEMP ('TK-Release-'+[Guid]::NewGuid().ToString('N').Substring(0,10))
New-Item -ItemType Directory -Path $storage -Force | Out-Null
New-Item -ItemType Junction -Path $alias -Target $storage | Out-Null
$player=Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
$before=@{}
Get-ChildItem -LiteralPath $player -File | Where-Object Name -Match '^(user\d+[._]|TerraKnight_slot\d+\.)' | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash}
$outside=@(Get-Process hollow_knight -ErrorAction SilentlyContinue | ForEach-Object {$_.Id})
function Get-SharedRegistrySignature {
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Team Cherry\Hollow Knight')
 if($null -eq $key){return '<absent>'}
 try {
  $rows=@($key.GetValueNames() | Sort-Object | ForEach-Object {
   $value=$key.GetValue($_)
   if($value -is [byte[]]){$value=[Convert]::ToBase64String($value)}
   @{Name=$_;Kind=$key.GetValueKind($_).ToString();Value=$value}
  })
  return ($rows | ConvertTo-Json -Depth 5 -Compress)
 }finally{$key.Close()}
}
$registryBefore=Get-SharedRegistrySignature
$sharedBefore=@{}
foreach($n in @('AppConfig.ini','shared.dat','shared.dat.bak')){
 $p=Join-Path $player $n
 $sharedBefore[$p]=if(Test-Path -LiteralPath $p){(Get-FileHash -LiteralPath $p).Hash}else{$null}
}
$processes=@();$timedOut=$false;$exception=$null
try{
 foreach($side in @('host','client')){
  $game=Join-Path $alias $side
  New-Item -ItemType Directory -Path $game -Force | Out-Null
  foreach($file in @('hollow_knight.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini','steam_appid.txt')){Copy-Item -LiteralPath (Join-Path $template $file) -Destination $game}
  & (Join-Path $PSScriptRoot 'new-private-unity-data.ps1') -Source (Join-Path $template 'hollow_knight_Data') -Destination (Join-Path $game 'hollow_knight_Data')
  foreach($dir in @('MonoBleedingEdge','D3D12')){New-Item -ItemType Junction -Path (Join-Path $game $dir) -Target (Join-Path $template $dir) | Out-Null}
  $bep=Join-Path $game 'BepInEx'
  New-Item -ItemType Directory -Path (Join-Path $bep 'plugins/TerraKnight') -Force | Out-Null
  foreach($dir in @('core','patchers')){New-Item -ItemType Junction -Path (Join-Path $bep $dir) -Target (Join-Path $template ('BepInEx/'+$dir)) | Out-Null}
  Copy-Item -LiteralPath (Join-Path $template 'BepInEx/config') -Destination $bep -Recurse
  foreach($file in @('TerraKnight.dll','TerraKnight.pdb')){Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $bep 'plugins/TerraKnight')}
  $ini=Join-Path $game 'doorstop_config.ini'
  [IO.File]::WriteAllText($ini,[regex]::Replace([IO.File]::ReadAllText($ini),'(?m)^enabled\s*=.*$','enabled = true'))
  $saves=Join-Path $run ($side+'-saves'); New-Item -ItemType Directory -Path $saves | Out-Null
  $test=if($Suite -eq 'netboss'){
   if($side -eq 'client'){'netboss_client'}elseif($ClientOwns){'netboss_host_late_'+$Bosses}else{'netboss_host_'+$Bosses}
  }else{$Suite+$side}
  $argsList='-tk-copy "release-'+$side+'" -tk-savedir "'+$saves+'" -tk-suite "'+$test+'" -tk-slot 1 -screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile "'+(Join-Path $run ($side+'Player.log'))+'"'
  $env:SteamAppId='367520';$env:SteamGameId='367520'
  $proc=Start-Process -FilePath (Join-Path $game 'hollow_knight.exe') -WorkingDirectory $game -ArgumentList $argsList -WindowStyle Hidden -PassThru
  $processes+= $proc
  if($side -eq 'host'){
   $ready=$false;$until=[DateTime]::UtcNow.AddSeconds(150)
   $log=Join-Path $alias 'host/BepInEx/TerraKnight/dev/release-host.log'
   while([DateTime]::UtcNow -lt $until -and -not $proc.HasExited){
    $readyText=if($Suite -eq 'networld'){'world: seeded'}else{'[位置] Tutorial_01'}
    if((Test-Path -LiteralPath $log) -and (Get-Content -LiteralPath $log -Raw -Encoding utf8).Contains($readyText)){$ready=$true;break}
    Start-Sleep -Milliseconds 500
   }
   if(-not $ready){throw 'Host did not seed its late-join fixtures'}
   Start-Sleep -Seconds 2
  }
 }
 $until=[DateTime]::UtcNow.AddSeconds($TimeoutSec)
 while([DateTime]::UtcNow -lt $until -and ($processes | Where-Object {-not $_.HasExited})){Start-Sleep -Milliseconds 500}
 $timedOut=[bool]($processes | Where-Object {-not $_.HasExited})
}catch{$exception=$_.Exception.Message}
finally{
 foreach($proc in $processes){if(-not $proc.HasExited){$proc.Kill()};$proc.WaitForExit()}
 $lines=@();$done=0
 foreach($side in @('host','client')){
  $log=Join-Path $run ($side+'Player.log')
  if(Test-Path -LiteralPath $log){
   $s=Get-Content -LiteralPath $log -Encoding utf8; $lines+=$s
   if($s -match '\[NET_DONE\]'){$done++}
  }
  $dev=Join-Path $alias ($side+'/BepInEx/TerraKnight/dev/release-'+$side+'.log')
  if(Test-Path -LiteralPath $dev){Copy-Item -LiteralPath $dev -Destination (Join-Path $run ($side+'AutoTest.log'))}
 }
 $fail=@($lines | Where-Object {$_ -match '\[NET_FAIL\]|\[NET_TIMEOUT\]| at TerraKnight.Net.NetSession.Handle|\[AutoTest\].*\u4e0d\u901a\u8fc7|\u5904\u7406\u6d88\u606f.*\u51fa\u9519|\u670d\u52a1\u5668\u5904\u7406.*\u51fa\u9519'})
 $changed=@($before.Keys | Where-Object {-not(Test-Path -LiteralPath $_) -or (Get-FileHash -LiteralPath $_).Hash -ne $before[$_]})
 $new=@(Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $before.ContainsKey($_.FullName)} | ForEach-Object Name)
 $sharedChanged=@($sharedBefore.Keys | Where-Object {$actual=if(Test-Path -LiteralPath $_){(Get-FileHash -LiteralPath $_).Hash}else{$null};$actual -ne $sharedBefore[$_]})
 $registryUnchanged=(Get-SharedRegistrySignature) -eq $registryBefore
 @{Suite=$Suite;Bosses=$Bosses;ClientOwns=[bool]$ClientOwns;Run=$run;Runtime=$alias;Completed=($done -eq 2);TimedOut=$timedOut;Exception=$exception;Failures=$fail;PassMarkers=@($lines | Where-Object {$_ -match '\[AutoTest\] 联机 通过'}).Count;ChangedPlayerSaves=$changed;NewPlayerSaves=$new;SharedFilesChanged=$sharedChanged;SharedRegistryUnchanged=$registryUnchanged;SharedSettingsUnchanged=($sharedChanged.Count -eq 0 -and $registryUnchanged);OutsideGameProcesses=$outside;SharedSettingsRestored=$false;Note='Independent installations and save directories. Read-only before/after verification of shared files and preferences; never restores over concurrent games.';BuildHash=(Get-FileHash -LiteralPath (Join-Path $build 'TerraKnight.dll')).Hash} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
 Write-Output "Evidence: $run"
}
if($exception -or $timedOut -or $done -ne 2 -or $fail.Count -or $changed.Count -or $new.Count){throw 'Independent two-client release regression failed; inspect result.json'}
