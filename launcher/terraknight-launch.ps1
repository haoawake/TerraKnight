# Starts Hollow Knight with the TerraKnight mod, then switches BepInEx off again, so starting
# the game from Steam stays vanilla. Lives in the game folder next to doorstop_config.ini.
# Doorstop reads its config only when the game process starts, so it is switched back as soon
# as the game is up; closing this window early can't leave the mod switched on.

$ErrorActionPreference = 'Stop'
$ini = Join-Path $PSScriptRoot 'doorstop_config.ini'

function Set-Doorstop([bool]$on) {
    $value = if ($on) { 'true' } else { 'false' }
    $text = [IO.File]::ReadAllText($ini)
    $text = [Regex]::Replace($text, '(?m)^enabled\s*=.*$', "enabled = $value")
    [IO.File]::WriteAllText($ini, $text)
}

if (Get-Process hollow_knight -ErrorAction SilentlyContinue) {
    Write-Host 'Hollow Knight is already running. Close it first.'
    Start-Sleep -Seconds 5
    exit 1
}

Set-Doorstop $true
try {
    Start-Process 'steam://rungameid/367520'
    # Steam can take a while to start the game.
    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Get-Process hollow_knight -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
    }
    Start-Sleep -Seconds 10
}
finally {
    Set-Doorstop $false
}
