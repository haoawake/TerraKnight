param([string]$Suite='accessorydrops', [int]$TimeoutSec=600, [string]$GameRoot, [string]$BuildDir='artifacts/single-drop-build', [switch]$ShowGame, [ValidatePattern('^[A-Za-z0-9-]+$')][string]$TempPrefix='TK-Solo-')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$game=if($GameRoot) {$GameRoot} else {Join-Path $root 'artifacts/single-drop-game'}
$build=if([IO.Path]::IsPathRooted($BuildDir)) {$BuildDir} else {Join-Path $root $BuildDir}
$exe=Join-Path $game 'hollow_knight.exe'
$candidateBuildHash=(Get-FileHash -LiteralPath (Join-Path $build 'TerraKnight.dll') -Algorithm SHA256).Hash
if(Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe}) {throw 'This independent copy is already running.'}
$run=Join-Path $root ('artifacts/single-copy-'+$Suite+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$saves=Join-Path $run 'saves'
New-Item -ItemType Directory -Path $saves -Force | Out-Null
$template=$game
$storage=Join-Path $run 'runtime'
New-Item -ItemType Directory -Path $storage | Out-Null
$game=Join-Path $env:TEMP ($TempPrefix+[Guid]::NewGuid().ToString('N').Substring(0,10))
New-Item -ItemType Junction -Path $game -Target $storage | Out-Null
foreach($file in @('hollow_knight.exe','UnityPlayer.dll','winhttp.dll','doorstop_config.ini','steam_appid.txt')){Copy-Item -LiteralPath (Join-Path $template $file) -Destination $game}
& (Join-Path $PSScriptRoot 'new-private-unity-data.ps1') -Source (Join-Path $template 'hollow_knight_Data') -Destination (Join-Path $storage 'hollow_knight_Data')
foreach($dir in @('MonoBleedingEdge','D3D12')){New-Item -ItemType Junction -Path (Join-Path $game $dir) -Target (Join-Path $template $dir) | Out-Null}
$bep=Join-Path $game 'BepInEx'
New-Item -ItemType Directory -Path (Join-Path $bep 'plugins/TerraKnight') -Force | Out-Null
foreach($dir in @('core','patchers')){New-Item -ItemType Junction -Path (Join-Path $bep $dir) -Target (Join-Path $template ('BepInEx/'+$dir)) | Out-Null}
Copy-Item -LiteralPath (Join-Path $template 'BepInEx/config') -Destination $bep -Recurse
$exe=Join-Path $game 'hollow_knight.exe'
$player=Join-Path $env:USERPROFILE 'AppData/LocalLow/Team Cherry/Hollow Knight'
$before=@{}
function Read-OriginalPreferences {
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Team Cherry\Hollow Knight')
 try {
  $values=[ordered]@{}
  if($key){foreach($name in ($key.GetValueNames() | Sort-Object)){
   $values[$name]=[ordered]@{Kind=$key.GetValueKind($name).ToString();Value=$key.GetValue($name)}
  }}
  return ($values | ConvertTo-Json -Depth 7 -Compress)
 } finally {if($key){$key.Close()}}
}
$registryBefore=Read-OriginalPreferences
[IO.File]::WriteAllText((Join-Path $run 'original-preferences-before.json'),$registryBefore)
$sharedBefore=@{}
foreach($n in @('AppConfig.ini','shared.dat','shared.dat.bak')){
 $p=Join-Path $player $n
 $sharedBefore[$p]=if(Test-Path -LiteralPath $p){(Get-FileHash -LiteralPath $p).Hash}else{$null}
}
Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)'} | ForEach-Object {$before[$_.FullName]=(Get-FileHash -LiteralPath $_.FullName).Hash}
foreach($file in @('TerraKnight.dll','TerraKnight.pdb')) {Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $game ('BepInEx/plugins/TerraKnight/'+$file)) -Force}
$ini=Join-Path $game 'doorstop_config.ini'
[IO.File]::WriteAllText($ini,[regex]::Replace([IO.File]::ReadAllText($ini),'(?m)^enabled\s*=.*$','enabled = true'))
$name='single-'+$Suite
$log=Join-Path $game ('BepInEx/TerraKnight/dev/'+$name+'.log')
if(Test-Path -LiteralPath $log) {Move-Item -LiteralPath $log -Destination (Join-Path $run 'previous-AutoTest.log')}
$arguments='-tk-copy "'+$name+'" -tk-savedir "'+$saves+'" -tk-suite "'+$Suite+'" -tk-slot 1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "'+(Join-Path $run 'Player.log')+'"'
$env:SteamAppId='367520'
$env:SteamGameId='367520'
$proc=Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList $arguments -WindowStyle $(if($ShowGame){'Normal'}else{'Hidden'}) -PassThru
$timeout=-not $proc.WaitForExit($TimeoutSec*1000)
if($timeout) {$proc.Kill();$proc.WaitForExit()}
$log=Join-Path $game ('BepInEx/TerraKnight/dev/'+$name+'.log')
Copy-Item -LiteralPath $log -Destination (Join-Path $run 'AutoTest.log')
$diagnostics=Join-Path $game ('BepInEx/TerraKnight/dev/'+$name)
if(Test-Path -LiteralPath $diagnostics) {
    Get-ChildItem -LiteralPath $diagnostics -File -Filter '*.json' | Copy-Item -Destination $run
}
$lines=Get-Content -LiteralPath $log -Encoding UTF8
$failures=@($lines | Where-Object {$_ -match '\[AutoTest\].*\u4e0d\u901a\u8fc7'} | ForEach-Object {[string]$_})
$completed=[bool]($lines | Where-Object {$_ -match '\[AutoTest\] \u5b8c\u6210'})
if(-not ($lines | Where-Object { $_.Contains($saves) })) { $completed=$false }
$changed=@($before.Keys | Where-Object {-not (Test-Path -LiteralPath $_) -or (Get-FileHash -LiteralPath $_).Hash -ne $before[$_]})
$new=@(Get-ChildItem -LiteralPath $player -File | Where-Object {$_.Name -match '^(user\d+[._]|TerraKnight_slot\d+\.)' -and -not $before.ContainsKey($_.FullName)})
$registryAfter=Read-OriginalPreferences
[IO.File]::WriteAllText((Join-Path $run 'original-preferences-after.json'),$registryAfter)
$registryUnchanged=$registryAfter -eq $registryBefore
$sharedChanged=@($sharedBefore.Keys | Where-Object {$actual=if(Test-Path -LiteralPath $_){(Get-FileHash -LiteralPath $_).Hash}else{$null};$actual -ne $sharedBefore[$_]})
@{Suite=$Suite;Run=$run;BuildHash=$candidateBuildHash;Completed=$completed;TimedOut=$timeout;Failures=$failures;ChangedPlayerSaves=$changed;NewPlayerSaves=[string[]]@($new | ForEach-Object {$_.Name});IndependentInstallation=$true;SharedSettingsRestored=$false;Note='No original-install configuration, shared save, or registry snapshot is written or restored while other tests run.'} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
Write-Output "Evidence: $run"
$result=Get-Content -LiteralPath (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
$result | Add-Member -NotePropertyName SharedRegistryUnchanged -NotePropertyValue $registryUnchanged
$result | Add-Member -NotePropertyName SharedFilesChanged -NotePropertyValue $sharedChanged
$result | Add-Member -NotePropertyName RestoredSettings -NotePropertyValue ($registryUnchanged -and $sharedChanged.Count -eq 0)
$result | Add-Member -NotePropertyName PreferenceIsolation -NotePropertyValue 'Private Unity company namespace; original preferences require no restoration'
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
if($timeout -or -not $completed -or $failures.Count -or $changed.Count -or $new.Count) {throw 'Independent-copy verification failed; inspect result.json.'}
