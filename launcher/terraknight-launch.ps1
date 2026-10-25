# Starts Hollow Knight with the TerraKnight mod, then switches BepInEx off again, so starting
# the game from Steam stays vanilla. Lives in BepInEx\TerraKnight (started by 空洞泰拉.bat in the game folder).
# Doorstop reads its config only when the game process starts, so it is switched back as soon
# as the game is up; closing this window early can't leave the mod switched on.

$ErrorActionPreference = 'Stop'
# The game folder: this script's own (an older install), or two up from BepInEx\TerraKnight where the installer puts it.
$game = if (Test-Path (Join-Path $PSScriptRoot 'hollow_knight.exe')) { $PSScriptRoot } else { (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
$ini = Join-Path $game 'doorstop_config.ini'

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

# Only this folder's own Hollow Knight counts: a copy elsewhere (a test copy) or one the system still holds after a
# crash (no path to read) doesn't stop this one starting.
function Get-OwnGame {
    Get-Process hollow_knight -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($game, [StringComparison]::OrdinalIgnoreCase)
    }
}

if (Get-OwnGame) {
    Write-Host 'Hollow Knight is already running. Close it first.'
    Start-Sleep -Seconds 5
    exit 1
}

# The Hollow Knight Modding API (installed by mod managers such as Scarab or Lumafly) puts its own
# MonoMod and Mono.Cecil into the game's Managed folder. Mono loads those before BepInEx's own, and
# BepInEx's preloader then fails (MissingMethodException: DynamicMethodDefinition.get_Definition).
$managed = Join-Path $game 'hollow_knight_Data\Managed'
$conflicts = @()
foreach ($pattern in 'MonoMod*.dll', 'Mono.Cecil*.dll', 'MMHOOK_*.dll', '0Harmony*.dll') {
    $conflicts += Get-ChildItem -Path $managed -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }
}
if ($conflicts.Count -gt 0) {
    $list = ($conflicts | Sort-Object -Unique) -join ', '
    Show-Message ("检测到空洞骑士的 Modding API（Scarab、Lumafly 等模组管理器安装的）。它放进 hollow_knight_Data\Managed 的这些文件和 BepInEx 冲突，BepInEx 会启动失败：`r`n$list`r`n`r`n" +
        "处理办法：在模组管理器里关闭 Modding API；或者在 Steam 里对空洞骑士「验证游戏文件的完整性」，再删掉上面这些文件。然后重新双击 空洞泰拉.bat。`r`n`r`n" +
        "The Hollow Knight Modding API (installed by a mod manager such as Scarab or Lumafly) is present. These files it put into hollow_knight_Data\Managed conflict with BepInEx, which fails to start:`r`n$list`r`n`r`n" +
        "Turn the Modding API off in the mod manager, or verify Hollow Knight's game files in Steam and then delete the files above. Then double-click 空洞泰拉.bat again.")
    exit 1
}

# An optional folder for Unity's log (terraknight-logdir.txt in the game folder, one line, e.g. E:\Hollow Log): the game
# is then started directly with -logFile there instead of AppData's Player.log (steam_appid.txt in the game folder keeps
# Steam from restarting it without the argument). Without the file, Steam starts it as before.
$logDirFile = Join-Path $game 'terraknight-logdir.txt'
$logDir = if (Test-Path $logDirFile) { ([IO.File]::ReadAllText($logDirFile)).Trim() } else { '' }
$exe = Join-Path $game 'hollow_knight.exe'
$direct = $logDir -and (Test-Path (Join-Path $game 'steam_appid.txt')) -and (Test-Path $exe)

Set-Doorstop $true
try {
    if ($direct) {
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        # Steamworks needs Steam up: started first when it isn't.
        if (-not (Get-Process steam -ErrorAction SilentlyContinue)) {
            Start-Process 'steam://open/main'
            $up = (Get-Date).AddSeconds(60)
            while (-not (Get-Process steam -ErrorAction SilentlyContinue) -and (Get-Date) -lt $up) { Start-Sleep -Seconds 1 }
            Start-Sleep -Seconds 8
        }
        Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList ('-logFile "' + (Join-Path $logDir 'Player.log') + '"')
    }
    else {
        Start-Process 'steam://rungameid/367520'
    }
    # Steam can take a while to start the game.
    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Get-OwnGame) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
    }
    Start-Sleep -Seconds 10
}
finally {
    Set-Doorstop $false
}
