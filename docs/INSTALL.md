## 下载和安装

只支持 Windows（64 位）。整个过程大约五分钟。

### 1. 准备

- **空洞骑士** Steam 版，当前正式版 1.5.12620。
- **泰拉瑞亚** Steam 版 1.4.x，**必须装在这台电脑上**：mod 的贴图、音效、物品名和文字都直接读本机泰拉瑞亚的文件，不随 mod 打包，所以不用担心版权素材的问题，但没装泰拉瑞亚就玩不了。安装器会在各个盘的 Steam 库里找到它。
- 建议用新存档玩 mod，并先备份原版进度：存档在 `%USERPROFILE%\AppData\LocalLow\Team Cherry\Hollow Knight`。mod 玩过的存档里的进度是按 mod 的规则来的（比如没有冲刺和法术），mod 自己的数据另存在同目录的 `TerraKnight_slot*.json`。

### 2. 一键安装（推荐）

1. 打开本项目的 [Releases](https://github.com/haoawake/TerraKnight/releases)，下载最新的 `TerraKnight-vX.Y.Z-Setup.exe`。
2. 关掉空洞骑士，双击运行安装器。它读取 Steam 的库列表，在各个盘里找空洞骑士和泰拉瑞亚，两款游戏装在不同的盘也能找到；没找到的那个点“浏览…”选它的游戏目录。
3. 点“安装”。安装器把 BepInEx 5 框架、空洞泰拉和启动器放进空洞骑士的游戏目录，把泰拉瑞亚的位置写进 mod 的设置，勾选时还会在桌面建一个“空洞泰拉”快捷方式。游戏目录需要管理员权限才能写入时（比如装在 `C:\Program Files (x86)\Steam` 里），Windows 会请求一次管理员权限。装完后的游戏目录：
   ```
   Hollow Knight/
   ├─ hollow_knight.exe
   ├─ 空洞泰拉.bat                 ← 启动器
   ├─ winhttp.dll、doorstop_config.ini …   （BepInEx 框架）
   └─ BepInEx/
      ├─ core/
      ├─ plugins/
      │  └─ TerraKnight/
      │     └─ TerraKnight.dll
      └─ TerraKnight/
         └─ terraknight-launch.ps1    ← 启动器的脚本
   ```
4. 双击 `空洞泰拉.bat`（或桌面上的“空洞泰拉”）开始游戏。第一次进主菜单时，中文的大标题会变成“空洞泰拉”（英文界面保持原样）；新建存档时会先让你选难度（普通、困难、专家、大师），存档界面右下角的“创造模式”按钮可以开一个物品用不完的创造存档。

安装器没有数字签名，Windows 可能弹出“Windows 已保护你的电脑”：点“更多信息”→“仍要运行”。

从 Steam 直接启动就是原版空洞骑士：启动器只在它启动游戏的那一刻打开 BepInEx，游戏起来后马上关回去（见下面的“启动”）。

### 3. 手动安装（压缩包）

不想用安装器时，下载同一个发布页里的 `TerraKnight-vX.Y.Z.zip`：

1. 把压缩包里的东西全部解压到空洞骑士的游戏目录（`hollow_knight.exe` 所在的文件夹；在 Steam 里右键空洞骑士 → 管理 → 浏览本地文件可以打开它）。压缩包里已经带着 BepInEx 5 框架，解压后的目录和上面一样。
2. 泰拉瑞亚和空洞骑士不在同一个 Steam 库时（比如在另一个盘），在 `BepInEx\config\com.haoawake.terraknight.cfg` 的 `[Paths]` 下写 `TerrariaDir = 泰拉瑞亚的游戏目录`（文件不存在就新建一个，内容是 `[Paths]` 和这一行）。
3. 双击 `空洞泰拉.bat` 开始游戏。

### 4. 可能遇到的问题

- **进游戏还是小骑士 / 没有任何变化**：确认是用 `空洞泰拉.bat` 启动的；再看 `BepInEx\LogOutput.log`，里面应该有 `TerraKnight` 的加载记录。没有这个文件说明 BepInEx 没装对（多半是解压错了目录）：重新运行安装器，确认它选的是 `hollow_knight.exe` 所在的目录。
- **日志第二行是 `Running under Unity v2020...`、没有 `com.haoawake.terraknight.cfg`、日志里有 `Could not find method for type HeroController and name SendHeroInPosition`**：空洞骑士是 1.5.78 旧版本（老的 Modding API 模组要的那个测试版分支）。空洞泰拉只支持 1.5.12620 当前正式版（Unity 6000）。在 Steam 库里右键空洞骑士 → 属性 → 测试版，选“无”，等它更新完再用 `空洞泰拉.bat` 启动；第一次正常加载后 cfg 才会生成。新版 mod 遇到旧版游戏时在日志里写明这一点，并且不加载。
- **启动时报错 `MissingMethodException: Method not found: Mono.Cecil.MethodDefinition MonoMod.Utils.DynamicMethodDefinition.get_Definition()`（出现在 `BepInEx.Preloader` / `ConsoleSetOutFix`）**：游戏里装着空洞骑士的 Modding API（Scarab、Lumafly 等模组管理器安装的），它放进 `hollow_knight_Data\Managed` 的 MonoMod、Mono.Cecil 先于 BepInEx 自带的版本被加载，BepInEx 因此启动失败。在模组管理器里关闭 Modding API，或者在 Steam 里对空洞骑士“验证游戏文件的完整性”后删掉 `hollow_knight_Data\Managed` 里的 `MonoMod*.dll`、`Mono.Cecil*.dll`、`MMHOOK_*.dll`，再用 `空洞泰拉.bat` 启动。启动器检测到这些文件时会弹窗说明，不会带着冲突启动。
- **日志里说找不到泰拉瑞亚**：重新运行安装器，在“泰拉瑞亚”一栏选它的游戏目录；或者在 `BepInEx\config\com.haoawake.terraknight.cfg` 里把 `TerrariaDir` 填成泰拉瑞亚的目录（里面有 `Terraria.exe` 和 `Content` 文件夹），比如 `D:\SteamLibrary\steamapps\common\Terraria`。
- **双击 bat 一闪而过、游戏没起来**：确认 Steam 开着；游戏已经在运行时启动器不会重复启动。
- **语言**：mod 跟着空洞骑士自己的语言设置走（游戏的选项 → 游戏 → 语言），泰拉瑞亚的物品名和文字会一起换成对应语言。
- **更新**：运行新版的安装器（或者把新版压缩包覆盖解压到游戏目录）；存档和设置保留。
- **卸载**：见文末的“卸载”。

