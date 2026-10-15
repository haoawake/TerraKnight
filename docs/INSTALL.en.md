## Download and installation

Windows (64-bit) only. The whole process takes about five minutes.

### 1. What you need

- **Hollow Knight**, Steam version, current stable release 1.5.12620.
- **Terraria**, Steam version 1.4.x, **must be installed on this computer**: the mod's textures, sounds, item names and text are all read directly from your local Terraria files and are not packaged with the mod, so there is no need to worry about copyrighted assets, but without Terraria installed you cannot play. When both games are in the same Steam library, Terraria is found automatically.
- It is best to play the mod on a new save and to back up your vanilla progress first: saves are in `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight`. Progress in a save played with the mod follows the mod's rules (for example, no dash and no spells); the mod's own data is stored separately in `TerraKnight_slot*.json` in the same folder.

### 2. Install BepInEx (the mod loader)

1. Open the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) and download `BepInEx_win_x64_5.4.23.x.zip` from **BepInEx 5.4.23.x** (not 6.x and not the x86 build).
2. In Steam, right-click Hollow Knight → Manage → Browse local files to open the game folder (it contains `hollow_knight.exe`).
3. Extract everything in the archive into this folder. Afterwards the folder contains a new `BepInEx` folder, `winhttp.dll` and `doorstop_config.ini`.
4. Launch Hollow Knight once from Steam, wait for the main menu, then quit. BepInEx creates `plugins`, `config` and other folders inside `BepInEx`.

### 3. Install TerraKnight

1. Open this project's [Releases](https://github.com/haoawake/TerraKnight/releases) and download the latest `TerraKnight-vX.Y.Z.zip`.
2. Extract everything in the archive into **the same game folder**. It should then look like this:
   ```
   Hollow Knight/
   ├─ hollow_knight.exe
   ├─ 泰拉圣巢.bat                 ← launcher
   ├─ terraknight-launch.ps1
   └─ BepInEx/
      └─ plugins/
         └─ TerraKnight/
            └─ TerraKnight.dll
   ```
3. Double-click `泰拉圣巢.bat` (the TerraKnight launcher) to start the game. The first time you reach the main menu, the large Chinese title changes to "泰拉圣巢" (the English interface keeps its original title); when you create a new save you first choose a difficulty (Normal, Hard, Expert, Master), and the "Creative Mode" button at the bottom right of the save screen starts a Creative save with unlimited items.

Launching straight from Steam gives you vanilla Hollow Knight: the launcher switches BepInEx on only at the moment it starts the game, and switches it off again as soon as the game is up (see "Launching" below). Right after you install BepInEx the switch is on, so until you first use the launcher, launching from Steam also loads the mod.

### 4. Troubleshooting

- **You are still the Knight in the game / nothing has changed**: make sure you launched with `泰拉圣巢.bat`; then look at `BepInEx\LogOutput.log`, which should contain `TerraKnight` loading entries. If that file does not exist, BepInEx is not installed correctly (most likely it was extracted into the wrong folder, or the x86 / 6.x build was downloaded).
- **The second line of the log is `Running under Unity v2020...`, there is no `com.haoawake.terraknight.cfg`, and the log contains `Could not find method for type HeroController and name SendHeroInPosition`**: your Hollow Knight is the old 1.5.78 version (the beta branch that old Modding API mods require). TerraKnight supports only the current stable release 1.5.12620 (Unity 6000). In your Steam library, right-click Hollow Knight → Properties → Betas, choose "None", wait for the update to finish, then launch with `泰拉圣巢.bat`; the cfg is created only after the first successful load. When a recent version of the mod meets the old game version, it says so directly in the log and does not load.
- **An error on start-up: `MissingMethodException: Method not found: Mono.Cecil.MethodDefinition MonoMod.Utils.DynamicMethodDefinition.get_Definition()` (in `BepInEx.Preloader` / `ConsoleSetOutFix`)**: the Hollow Knight Modding API is installed (by a mod manager such as Scarab or Lumafly). The MonoMod and Mono.Cecil it puts into `hollow_knight_Data\Managed` load before BepInEx's own versions, and BepInEx fails to start. Turn the Modding API off in the mod manager, or verify Hollow Knight's game files in Steam and then delete `MonoMod*.dll`, `Mono.Cecil*.dll` and `MMHOOK_*.dll` from `hollow_knight_Data\Managed`; then start the game with `泰拉圣巢.bat`. The launcher detects these files and explains this in a message box instead of starting with the conflict.
- **The log says Terraria cannot be found**: in `BepInEx\config\com.haoawake.terraknight.cfg`, set `TerrariaDir` to your Terraria folder (the one containing `Terraria.exe` and the `Content` folder), for example `D:\SteamLibrary\steamapps\common\Terraria`.
- **Double-clicking the bat only flashes a window and the game does not start**: make sure Steam is running; if the game is already running, the launcher does not start it a second time.
- **Language**: the mod follows Hollow Knight's own language setting (in-game Options → Game → Language), and Terraria item names and text switch to the matching language along with it.
- **Updating**: download the new archive and extract it over the game folder; your saves are kept.
- **Uninstalling**: see "Uninstalling" at the end of this document.

