param([string]$Candidate='artifacts/release-candidate-4.4.2-20261024-r1',[string]$Net2Result,[switch]$BossesFirst)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$matrix=Join-Path $root ($Candidate+'/regression-matrix.json')
$rows=[System.Collections.Generic.List[object]]::new()
function Record-Run($label,$path,$errorText){
 $result=if(Test-Path -LiteralPath $path){Get-Content -LiteralPath $path -Raw | ConvertFrom-Json}else{$null}
 $rows.Add(@{Case=$label;Result=$path;Completed=$result.Completed;TimedOut=$result.TimedOut;Failures=$result.Failures;PassMarkers=$result.PassMarkers;BuildHash=$result.BuildHash;Error=$errorText})
 $rows | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $matrix -Encoding utf8
 Write-Output ('Finished: '+$label+'; completed='+$result.Completed+'; failures='+$result.Failures.Count)
}
function Run-WorldSuites {
foreach($suite in @('networld','net2','netloot','netrelease')){
 if($suite -eq 'net2' -and $Net2Result){Record-Run $suite (Join-Path $root $Net2Result) $null;continue}
 $errorText=$null
 try{& (Join-Path $PSScriptRoot 'test-net-release-copy.ps1') -Suite $suite -BuildDir ($Candidate+'/build') -TimeoutSec 600}
 catch{$errorText=$_.Exception.Message}
 $latest=Get-ChildItem (Join-Path $root 'artifacts') -Directory -Filter ('net-release-'+$suite+'-*') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
 Record-Run $suite (Join-Path $latest.FullName 'result.json') $errorText
}
}
if(-not $BossesFirst){Run-WorldSuites}
$bosses='FalseKnight.MantisLord.MageLord.InfectedKnight.HollowKnight'
foreach($clientOwns in @($false,$true)){
 $errorText=$null
 try{& (Join-Path $PSScriptRoot 'test-net-release-copy.ps1') -Suite netboss -BuildDir ($Candidate+'/build') -Bosses $bosses -ClientOwns:$clientOwns -TimeoutSec 1800}
 catch{$errorText=$_.Exception.Message}
 $latest=Get-ChildItem (Join-Path $root 'artifacts') -Directory -Filter 'net-release-netboss-*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
 Record-Run ('netboss-clientOwns='+$clientOwns) (Join-Path $latest.FullName 'result.json') $errorText
}
if($BossesFirst){Run-WorldSuites}
$game=Join-Path $env:TEMP 'TK-QuestFish-20261024'
if(-not(Test-Path -LiteralPath $game)){New-Item -ItemType Junction -Path $game -Target (Join-Path $root 'artifacts/questfish-game') | Out-Null}
foreach($suite in @('releasecheck','shimmeraudit','recipevariants','terradialogue')){
 $errorText=$null
 try{& (Join-Path $PSScriptRoot 'test-single-drop-copy.ps1') -Suite $suite -BuildDir ($Candidate+'/build') -GameRoot $game -TimeoutSec 900}
 catch{$errorText=$_.Exception.Message}
 $latest=Get-ChildItem (Join-Path $root 'artifacts') -Directory -Filter ('single-copy-'+$suite+'-*') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
 Record-Run $suite (Join-Path $latest.FullName 'result.json') $errorText
}
if($rows | Where-Object {-not $_.Completed -or $_.TimedOut -or $_.Failures.Count -or $_.Error}){throw 'Readiness matrix contains failures; inspect each retained result.'}
