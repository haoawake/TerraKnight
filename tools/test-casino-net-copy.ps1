param([int]$TimeoutSec=600,[string]$BuildDir='artifacts/casino-build')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root $BuildDir
$template=Join-Path $root 'artifacts/single-drop-game'
$run=Join-Path $root ('artifacts/casino-net-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$runtime=Join-Path $root 'artifacts/casino-net-runtime'
$alias=Join-Path $env:TEMP 'tk-casino-net'
New-Item -ItemType Directory -Path $run,$runtime -Force | Out-Null
if(-not(Test-Path -LiteralPath $alias)){New-Item -ItemType Junction -Path $alias -Target $runtime | Out-Null}
if((Get-Item -LiteralPath $alias).Target -ne $runtime){throw 'Unexpected runtime junction target'}
$player=Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
$before=@{}
Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)'} | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash}
$processes=@()
try {
 foreach($side in @('host','client')) {
  $game=Join-Path $alias $side
  $exe=Join-Path $game 'hollow_knight.exe'
  if(Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe}){throw 'Casino network copy already running'}
  New-Item -ItemType Directory -Path $game -Force | Out-Null
  foreach($file in @('hollow_knight.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini')){Copy-Item -LiteralPath (Join-Path $template $file) -Destination $game -Force}
  foreach($dir in @('hollow_knight_Data','MonoBleedingEdge','D3D12')){if(-not(Test-Path -LiteralPath (Join-Path $game $dir))){New-Item -ItemType Junction -Path (Join-Path $game $dir) -Target (Join-Path $template $dir) | Out-Null}}
  $bep=Join-Path $game 'BepInEx';New-Item -ItemType Directory -Path (Join-Path $bep 'plugins/TerraKnight') -Force | Out-Null
  foreach($dir in @('core','patchers')){if(-not(Test-Path -LiteralPath (Join-Path $bep $dir))){New-Item -ItemType Junction -Path (Join-Path $bep $dir) -Target (Join-Path $template ('BepInEx/'+$dir)) | Out-Null}}
  Copy-Item -LiteralPath (Join-Path $template 'BepInEx/config') -Destination $bep -Recurse -Force
  foreach($file in @('TerraKnight.dll','TerraKnight.pdb')){Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $bep 'plugins/TerraKnight') -Force}
  $ini=Join-Path $game 'doorstop_config.ini';[IO.File]::WriteAllText($ini,[regex]::Replace([IO.File]::ReadAllText($ini),'(?m)^enabled\s*=.*$','enabled = true'))
  $saves=Join-Path $run ($side+'-saves');New-Item -ItemType Directory -Path $saves -Force | Out-Null
  $suite=if($side -eq 'host'){'casinonethost'}else{'casinonetclient'}
  $arguments='-tk-copy "casino-'+$side+'" -tk-savedir "'+$saves+'" -tk-suite "'+$suite+'" -tk-slot 1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "'+(Join-Path $run ($side+'Player.log'))+'"'
  $env:SteamAppId='367520';$env:SteamGameId='367520'
  $processes+=Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList $arguments -WindowStyle Hidden -PassThru
 }
 $until=[DateTime]::UtcNow.AddSeconds($TimeoutSec)
 while([DateTime]::UtcNow -lt $until -and ($processes | Where-Object {-not $_.HasExited})){Start-Sleep -Milliseconds 500}
 $timedOut=[bool]($processes | Where-Object {-not $_.HasExited})
} finally {
 foreach($proc in $processes){if(-not $proc.HasExited){$proc.Kill()};$proc.WaitForExit()}
 $allLines=@();$completed=0
 foreach($side in @('host','client')) {
  $log=Join-Path $alias ($side+'/BepInEx/TerraKnight/dev/casino-'+$side+'.log')
  if(Test-Path -LiteralPath $log){Copy-Item -LiteralPath $log -Destination (Join-Path $run ($side+'AutoTest.log'));$lines=Get-Content -LiteralPath $log -Encoding UTF8;$allLines+=$lines;if($lines -match '\[AutoTest\] 完成'){$completed++}}
 }
 $failures=@($allLines | Where-Object {$_ -match '\[AutoTest\].*不通过| at TerraKnight.Net.NetSession.Handle|处理消息.*出错|服务器处理.*出错'})
 $changed=@($before.Keys | Where-Object {-not(Test-Path -LiteralPath $_)-or(Get-FileHash -LiteralPath $_).Hash -ne $before[$_]})
 $new=@(Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $before.ContainsKey($_.FullName)})
 @{Run=$run;Completed=($completed -eq 2);TimedOut=$timedOut;Failures=$failures;ChangedPlayerSaves=$changed;NewPlayerSaves=[string[]]@($new|ForEach-Object {$_.Name});SharedSettingsRestored=$false;Note='Two private game installations and save directories; existing game processes and shared settings are never stopped or restored.';BuildHash=(Get-FileHash -LiteralPath (Join-Path $build 'TerraKnight.dll')).Hash}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
 Write-Output "Evidence: $run"
}
if($timedOut -or $completed -ne 2 -or $failures.Count -or $changed.Count -or $new.Count){throw 'Casino two-game verification failed; inspect result.json.'}
