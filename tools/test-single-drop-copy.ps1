param([string]$Suite='accessorydrops', [int]$TimeoutSec=600, [string]$GameRoot, [string]$BuildDir='artifacts/single-drop-build')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$game=if($GameRoot) {$GameRoot} else {Join-Path $root 'artifacts/single-drop-game'}
$build=if([IO.Path]::IsPathRooted($BuildDir)) {$BuildDir} else {Join-Path $root $BuildDir}
$exe=Join-Path $game 'hollow_knight.exe'
if(Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe}) {throw 'This independent copy is already running.'}
$run=Join-Path $root ('artifacts/single-copy-'+$Suite+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$saves=Join-Path $run 'saves'
New-Item -ItemType Directory -Path $saves -Force | Out-Null
$player=Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
$before=@{}
Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)'} | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash}
foreach($file in @('TerraKnight.dll','TerraKnight.pdb')) {Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $game ('BepInEx/plugins/TerraKnight/'+$file)) -Force}
$ini=Join-Path $game 'doorstop_config.ini'
[IO.File]::WriteAllText($ini,[regex]::Replace([IO.File]::ReadAllText($ini),'(?m)^enabled\s*=.*$','enabled = true'))
$name='single-'+$Suite
$arguments='-tk-copy "'+$name+'" -tk-savedir "'+$saves+'" -tk-suite "'+$Suite+'" -tk-slot 1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "'+(Join-Path $run 'Player.log')+'"'
$env:SteamAppId='367520'
$env:SteamGameId='367520'
$proc=Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList $arguments -WindowStyle Hidden -PassThru
$timeout=-not $proc.WaitForExit($TimeoutSec*1000)
if($timeout) {$proc.Kill();$proc.WaitForExit()}
$log=Join-Path $game ('BepInEx/TerraKnight/dev/'+$name+'.log')
Copy-Item -LiteralPath $log -Destination (Join-Path $run 'AutoTest.log')
$lines=Get-Content -LiteralPath $log
$failures=@($lines | Where-Object {$_ -match '\[AutoTest\].*不通过'})
$completed=[bool]($lines | Where-Object {$_ -match '\[AutoTest\] 完成'})
$changed=@($before.Keys | Where-Object {-not (Test-Path -LiteralPath $_) -or (Get-FileHash -LiteralPath $_).Hash -ne $before[$_]})
$new=@(Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $before.ContainsKey($_.FullName)})
@{Suite=$Suite;Run=$run;Completed=$completed;TimedOut=$timeout;Failures=$failures;ChangedPlayerSaves=$changed;NewPlayerSaves=[string[]]@($new | ForEach-Object {$_.Name});IndependentInstallation=$true;SharedSettingsRestored=$false;Note='No original-install configuration, shared save, or registry snapshot is written or restored while other tests run.'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
Write-Output "Evidence: $run"
if($timeout -or -not $completed -or $failures.Count -or $changed.Count -or $new.Count) {throw 'Independent-copy verification failed; inspect result.json.'}
