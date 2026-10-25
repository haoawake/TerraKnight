## Download and installation

Windows (64-bit) only. The whole process takes about five minutes.

### 1. What you need

- **Hollow Knight**, Steam version, current stable release 1.5.12620.
- **Terraria**, Steam version 1.4.x, **must be installed on this computer**: the mod's textures, sounds, item names and text are all read directly from your local Terraria files and are not packaged with the mod, so there is no need to worry about copyrighted assets, but without Terraria installed you cannot play. The installer finds it in the Steam libraries on every drive.
- It is best to play the mod on a new save and to back up your vanilla progress first: saves are in `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight`. Progress in a save played with the mod follows the mod's rules (for example, no dash and no spells); the mod's own data is stored separately in `TerraKnight_slot*.json` in the same folder.

### 2. One-click install (recommended)

1. Open this project's [Releases](https://github.com/haoawake/TerraKnight/releases) and download the latest `TerraKnight-vX.Y.Z-Setup.exe`.
2. Close Hollow Knight and run the installer. It reads Steam's library list and looks for Hollow Knight and Terraria on every drive, so the two games may be on different drives; if it misses one, click "浏览…" (Browse) and pick that game's folder.
3. Click "安装" (Install). The installer puts the BepInEx 5 framework, TerraKnight and the launcher into Hollow Knight's game folder, writes Terraria's location into the mod's settings, and, when ticked, creates a "空洞泰拉" shortcut on the desktop. When the game folder needs administrator rights to write to (inside `C:\Program Files (x86)\Steam`, for example), Windows asks for them once. The game folder afterwards:
   ```
   Hollow Knight/
   ├─ hollow_knight.exe
   ├─ 空洞泰拉.bat                 ← launcher
   ├─ winhttp.dll, doorstop_config.ini …   (BepInEx framework)
   └─ BepInEx/
      ├─ core/
      ├─ plugins/
      │  └─ TerraKnight/
      │     └─ TerraKnight.dll
      └─ TerraKnight/
         └─ terraknight-launch.ps1    ← the launcher's script
   ```
4. Double-click `空洞泰拉.bat` (the TerraKnight launcher) or the "空洞泰拉" desktop shortcut to start the game. The first time you reach the main menu, the large Chinese title changes to "空洞泰拉" (the English interface keeps its original title); when you create a new save you first choose a difficulty (Normal, Hard, Expert, Master), and the "Creative Mode" button at the bottom right of the save screen starts a Creative save with unlimited items.

The installer is not digitally signed, so Windows may show "Windows protected your PC": click "More info" → "Run anyway".

Launching straight from Steam gives you vanilla Hollow Knight: the launcher switches BepInEx on only at the moment it starts the game, and switches it off again as soon as the game is up (see "Launching" below).

### 3. Manual install (archive)

To install without the installer, download `TerraKnight-vX.Y.Z.zip` from the same releases page:

1. Extract everything in the archive into Hollow Knight's game folder (the folder with `hollow_knight.exe`; in Steam, right-click Hollow Knight → Manage → Browse local files opens it). The archive includes the BepInEx 5 framework, and the folder ends up as shown above.
2. When Terraria is not in the same Steam library as Hollow Knight (on another drive, say), write `TerrariaDir = <Terraria's game folder>` under `[Paths]` in `BepInEx\config\com.haoawake.terraknight.cfg` (if the file does not exist, create it with `[Paths]` and that line).
3. Double-click `空洞泰拉.bat` to start the game.

### 4. Troubleshooting

- **You are still the Knight in the game / nothing has changed**: make sure you launched with `空洞泰拉.bat`; then look at `BepInEx\LogOutput.log`, which should contain `TerraKnight` loading entries. If that file does not exist, BepInEx is not installed correctly (most likely it went into the wrong folder): run the installer again and check that it picked the folder with `hollow_knight.exe`.
- **The second line of the log is `Running under Unity v2020...`, there is no `com.haoawake.terraknight.cfg`, and the log contains `Could not find method for type HeroController and name SendHeroInPosition`**: your Hollow Knight is the old 1.5.78 version (the beta branch that old Modding API mods require). TerraKnight supports only the current stable release 1.5.12620 (Unity 6000). In your Steam library, right-click Hollow Knight → Properties → Betas, choose "None", wait for the update to finish, then launch with `空洞泰拉.bat`; the cfg is created only after the first successful load. When a recent version of the mod meets the old game version, it says so directly in the log and does not load.
- **An error on start-up: `MissingMethodException: Method not found: Mono.Cecil.MethodDefinition MonoMod.Utils.DynamicMethodDefinition.get_Definition()` (in `BepInEx.Preloader` / `ConsoleSetOutFix`)**: the Hollow Knight Modding API is installed (by a mod manager such as Scarab or Lumafly). The MonoMod and Mono.Cecil it puts into `hollow_knight_Data\Managed` load before BepInEx's own versions, and BepInEx fails to start. Turn the Modding API off in the mod manager, or verify Hollow Knight's game files in Steam and then delete `MonoMod*.dll`, `Mono.Cecil*.dll` and `MMHOOK_*.dll` from `hollow_knight_Data\Managed`; then start the game with `空洞泰拉.bat`. The launcher detects these files and explains this in a message box instead of starting with the conflict.
- **The log says Terraria cannot be found**: run the installer again and pick Terraria's game folder in its Terraria field; or, in `BepInEx\config\com.haoawake.terraknight.cfg`, set `TerrariaDir` to your Terraria folder (the one containing `Terraria.exe` and the `Content` folder), for example `D:\SteamLibrary\steamapps\common\Terraria`.
- **Double-clicking the bat only flashes a window and the game does not start**: make sure Steam is running; if the game is already running, the launcher does not start it a second time.
- **Language**: the mod follows Hollow Knight's own language setting (in-game Options → Game → Language), and Terraria item names and text switch to the matching language along with it.
- **Updating**: run the new version's installer (or extract the new archive over the game folder); your saves and settings are kept.
- **Uninstalling**: see "Uninstalling" at the end of this document.

