param([Parameter(Mandatory=$true)][string[]]$Runs, [string]$Output='artifacts/charm-audit-summary.json')
$ErrorActionPreference='Stop'
$caseResults=@{}
$records=@()
foreach($runPath in $Runs) {
    $logPath=Join-Path $runPath 'AutoTest.log'
    $lines=Get-Content -LiteralPath $logPath
    $result=Get-Content -LiteralPath (Join-Path $runPath 'result.json') -Raw | ConvertFrom-Json
    $checks=@()
    foreach($line in $lines) {
        if($line -match '\[AutoTest\] 护符(?<id>\d+) 结果 (?<passed>\d+)/(?<total>\d+)') {
            $caseResults[[int]$Matches.id]=[ordered]@{Id=[int]$Matches.id;Passed=[int]$Matches.passed;Total=[int]$Matches.total;Run=$runPath}
        }
        if($line -match '\[AutoTest\] (?<tag>护符\S*|指南针) (?<name>.*?) (?<outcome>不通过|通过)(?: (?<detail>.*))?$') {
            $checks += [ordered]@{Tag=$Matches.tag;Name=$Matches.name;Passed=($Matches.outcome -eq '通过');Detail=$Matches.detail}
        }
    }
    $records += [ordered]@{Run=$runPath;Completed=$result.Completed;TimedOut=$result.TimedOut;Failures=$result.Failures;ChangedPlayerSaves=$result.ChangedPlayerSaves;NewPlayerSaves=$result.NewPlayerSaves;Checks=$checks}
}
$cases=@($caseResults.Keys | Sort-Object | ForEach-Object {$caseResults[$_]})
$passed=0; $total=0
foreach($case in $cases) {$passed+=$case.Passed; $total+=$case.Total}
$summary=[ordered]@{Scope='Latest result per numbered charm case; pair matrix and extra cases retained per run';Cases=$cases;LatestNumberedPassed=$passed;LatestNumberedTotal=$total;Runs=$records}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Output -Encoding utf8
Write-Output "Numbered cases: $($cases.Count), checks: $passed/$total, report: $Output"
