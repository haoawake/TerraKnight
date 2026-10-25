@echo off
rem Starts Hollow Knight with the TerraKnight mod. Its script is in BepInEx\TerraKnight; an older install has it here.
if exist "%~dp0BepInEx\TerraKnight\terraknight-launch.ps1" (
    start "" /min powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BepInEx\TerraKnight\terraknight-launch.ps1"
) else (
    start "" /min powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0terraknight-launch.ps1"
)
