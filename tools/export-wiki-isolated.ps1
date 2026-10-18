param([string]$BuildDir='artifacts/broken-vessel-build')
$ErrorActionPreference='Stop'
$game=Join-Path ([IO.Path]::GetTempPath()) 'TKWiki410Game'
$manager=Join-Path $game 'hollow_knight_Data/globalgamemanagers'
$raw=[Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($manager))
if($raw.Contains('Team Cherry') -or -not $raw.Contains('TKWiki410XX')){throw 'Company isolation not verified'}
foreach($suite in @('wikiassets_zh','healthbook')){
 & (Join-Path $PSScriptRoot 'test-single-drop-copy.ps1') -Suite $suite -TimeoutSec 1200 -GameRoot $game -BuildDir $BuildDir
}
