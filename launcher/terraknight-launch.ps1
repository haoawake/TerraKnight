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

function Show-Message([string]$text) {
    Add-Type -AssemblyName System.Windows.Forms
    [void][System.Windows.Forms.MessageBox]::Show($text, 'TerraKnight')
}

if (Get-Process hollow_knight -ErrorAction SilentlyContinue) {
    Write-Host 'Hollow Knight is already running. Close it first.'
    Start-Sleep -Seconds 5
    exit 1
}

# The Hollow Knight Modding API (installed by mod managers such as Scarab or Lumafly) puts its own
# MonoMod and Mono.Cecil into the game's Managed folder. Mono loads those before BepInEx's own, and
# BepInEx's preloader then fails (MissingMethodException: DynamicMethodDefinition.get_Definition).
$managed = Join-Path $PSScriptRoot 'hollow_knight_Data\Managed'
$conflicts = @()
foreach ($pattern in 'MonoMod*.dll', 'Mono.Cecil*.dll', 'MMHOOK_*.dll', '0Harmony*.dll') {
    $conflicts += Get-ChildItem -Path $managed -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }
}
if ($conflicts.Count -gt 0) {
    $list = ($conflicts | Sort-Object -Unique) -join ', '
    Show-Message ("检测到空洞骑士的 Modding API（Scarab、Lumafly 等模组管理器安装的）。它放进 hollow_knight_Data\Managed 的这些文件和 BepInEx 冲突，BepInEx 会启动失败：`r`n$list`r`n`r`n" +
        "处理办法：在模组管理器里关闭 Modding API；或者在 Steam 里对空洞骑士「验证游戏文件的完整性」，再删掉上面这些文件。然后重新双击 泰拉圣巢.bat。`r`n`r`n" +
        "The Hollow Knight Modding API (installed by a mod manager such as Scarab or Lumafly) is present. These files it put into hollow_knight_Data\Managed conflict with BepInEx, which fails to start:`r`n$list`r`n`r`n" +
        "Turn the Modding API off in the mod manager, or verify Hollow Knight's game files in Steam and then delete the files above. Then double-click 泰拉圣巢.bat again.")
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
