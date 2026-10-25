## 卸载

删除空洞骑士目录下的 `BepInEx/plugins/TerraKnight`、`BepInEx/TerraKnight` 和 `空洞泰拉.bat`，以及桌面上的“空洞泰拉”快捷方式。不想要 BepInEx 的话，再删掉 `BepInEx`、`winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`changelog.txt`。mod 自己的存档是空洞骑士存档目录里的 `TerraKnight_slot*.json`。

## 第三方代码

- `src/TerraKnight/Terraria/Xnb/LzxDecoder.cs` 来自 MonoGame（移植自 libmspack），按 MS-PL 协议使用，保留了原版权头。
