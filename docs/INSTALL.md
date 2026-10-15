## 下载和安装

只支持 Windows（64 位）。整个过程大约五分钟。

### 1. 准备

- **空洞骑士** Steam 版，当前正式版 1.5.12620。
- **泰拉瑞亚** Steam 版 1.4.x，**必须装在这台电脑上**：mod 的贴图、音效、物品名和文字都直接读本机泰拉瑞亚的文件，不随 mod 打包，所以不用担心版权素材的问题，但没装泰拉瑞亚就玩不了。两款游戏装在同一个 Steam 库里时会自动找到。
- 建议用新存档玩 mod，并先备份原版进度：存档在 `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight`。mod 玩过的存档里的进度是按 mod 的规则来的（比如没有冲刺和法术），mod 自己的数据另存在同目录的 `TerraKnight_slot*.json`。

### 2. 装 BepInEx（mod 加载器）

1. 打开 [BepInEx 的发布页](https://github.com/BepInEx/BepInEx/releases)，下载 **BepInEx 5.4.23.x** 的 `BepInEx_win_x64_5.4.23.x.zip`（不要下 6.x 或 x86 版）。
2. 在 Steam 里右键空洞骑士 → 管理 → 浏览本地文件，打开游戏目录（里面有 `hollow_knight.exe`）。
3. 把压缩包里的东西全部解压到这个目录，解压后目录里会多出 `BepInEx` 文件夹、`winhttp.dll` 和 `doorstop_config.ini`。
4. 从 Steam 启动一次空洞骑士，等到主菜单再退出。BepInEx 会在 `BepInEx` 里建好 `plugins`、`config` 等文件夹。

### 3. 装泰拉圣巢

1. 打开本项目的 [Releases](https://github.com/haoawake/TerraKnight/releases)，下载最新的 `TerraKnight-vX.Y.Z.zip`。
2. 把压缩包里的东西全部解压到**同一个游戏目录**。解压后应该是这样：
   ```
   Hollow Knight/
   ├─ hollow_knight.exe
   ├─ 泰拉圣巢.bat                 ← 启动器
   ├─ terraknight-launch.ps1
   └─ BepInEx/
      └─ plugins/
         └─ TerraKnight/
            └─ TerraKnight.dll
   ```
3. 双击 `泰拉圣巢.bat` 开始游戏。第一次进主菜单时，中文的大标题会变成“泰拉圣巢”（英文界面保持原样）；新建存档时会先让你选难度（普通、困难、专家、大师），存档界面右下角的“创造模式”按钮可以开一个物品用不完的创造存档。

从 Steam 直接启动就是原版空洞骑士：启动器只在它启动游戏的那一刻打开 BepInEx，游戏起来后马上关回去（见下面的“启动”）。刚装好 BepInEx 时开关是开着的，所以在第一次用启动器之前，从 Steam 启动也会带着 mod。

### 4. 可能遇到的问题

- **进游戏还是小骑士 / 没有任何变化**：确认是用 `泰拉圣巢.bat` 启动的；再看 `BepInEx\LogOutput.log`，里面应该有 `TerraKnight` 的加载记录。没有这个文件说明 BepInEx 没装对（多半是解压错了目录，或者下成了 x86 / 6.x 版）。
- **日志第二行是 `Running under Unity v2020...`、没有 `com.haoawake.terraknight.cfg`、日志里有 `Could not find method for type HeroController and name SendHeroInPosition`**：空洞骑士是 1.5.78 旧版本（老的 Modding API 模组要的那个测试版分支）。泰拉圣巢只支持 1.5.12620 当前正式版（Unity 6000）。在 Steam 库里右键空洞骑士 → 属性 → 测试版，选“无”，等它更新完再用 `泰拉圣巢.bat` 启动；第一次正常加载后 cfg 才会生成。新版 mod 遇到旧版游戏时在日志里写明这一点，并且不加载。
- **启动时报错 `MissingMethodException: Method not found: Mono.Cecil.MethodDefinition MonoMod.Utils.DynamicMethodDefinition.get_Definition()`（出现在 `BepInEx.Preloader` / `ConsoleSetOutFix`）**：游戏里装着空洞骑士的 Modding API（Scarab、Lumafly 等模组管理器安装的），它放进 `hollow_knight_Data\Managed` 的 MonoMod、Mono.Cecil 先于 BepInEx 自带的版本被加载，BepInEx 因此启动失败。在模组管理器里关闭 Modding API，或者在 Steam 里对空洞骑士“验证游戏文件的完整性”后删掉 `hollow_knight_Data\Managed` 里的 `MonoMod*.dll`、`Mono.Cecil*.dll`、`MMHOOK_*.dll`，再用 `泰拉圣巢.bat` 启动。启动器检测到这些文件时会弹窗说明，不会带着冲突启动。
- **日志里说找不到泰拉瑞亚**：在 `BepInEx\config\com.haoawake.terraknight.cfg` 里把 `TerrariaDir` 填成泰拉瑞亚的目录（里面有 `Terraria.exe` 和 `Content` 文件夹），比如 `D:\SteamLibrary\steamapps\common\Terraria`。
- **双击 bat 一闪而过、游戏没起来**：确认 Steam 开着；游戏已经在运行时启动器不会重复启动。
- **语言**：mod 跟着空洞骑士自己的语言设置走（游戏的选项 → 游戏 → 语言），泰拉瑞亚的物品名和文字会一起换成对应语言。
- **更新**：下载新版压缩包，覆盖解压到游戏目录即可；存档保留。
- **卸载**：见文末的“卸载”。

