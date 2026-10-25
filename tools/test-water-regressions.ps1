param([string]$Candidate='artifacts/water-regression-fix-20261024-r3')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$rows=[System.Collections.Generic.List[object]]::new()
foreach($suite in @('water','acid','acidhit','shimmerbucket')){
 $errorText=$null
 try{& (Join-Path $PSScriptRoot 'test-single-drop-copy.ps1') -Suite $suite -BuildDir ($Candidate+'/build') -TimeoutSec 600}
 catch{$errorText=$_.Exception.Message}
 $latest=Get-ChildItem (Join-Path $root 'artifacts') -Directory -Filter ('single-copy-'+$suite+'-*') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
 $path=Join-Path $latest.FullName 'result.json'
 $r=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
 $rows.Add(@{Suite=$suite;Result=$path;Completed=$r.Completed;TimedOut=$r.TimedOut;Failures=$r.Failures;Error=$errorText;BuildHash=$r.BuildHash})
 $rows | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $root ($Candidate+'/water-compatibility.json')) -Encoding utf8
 Write-Output ('Finished '+$suite+'; completed='+$r.Completed+'; failures='+$r.Failures.Count)
}
if($rows | Where-Object {-not $_.Completed -or $_.TimedOut -or $_.Failures.Count -or $_.Error}){throw 'Water compatibility regression failed; retained result files describe each case.'}
