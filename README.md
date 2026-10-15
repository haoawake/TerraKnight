# 泰拉圣巢 · TerraKnight

<div align="right">
  <strong>中文</strong> | <a href="./README.en.md">English</a>
</div>

<div align="center">
  <img src="./terraknight-banner.png" alt="泰拉圣巢 · TerraKnight — Terraria × Hollow Knight" width="100%"/>
</div>

> **把《泰拉瑞亚》带进《空洞骑士》：在圣巢挖矿、建造、探索、战斗。**

你不再扮演小骑士，而是以泰拉瑞亚冒险者的身份闯入圣巢：用铜镐挖开地形、放置方块、制作装备、挑战首领，并沿着泰拉瑞亚的成长路线探索空洞骑士的世界。

**[⬇️ 下载最新版本](https://github.com/haoawake/TerraKnight/releases)** · **[📖 安装教程](docs/INSTALL.md)** · **[🎮 操作指南](docs/GAMEPLAY.md)** · **[🧩 完整功能](docs/FEATURES.md)** · **[📺 视频演示](https://www.bilibili.com/video/BV1H4pr6LEAM/)**

## 📑 目录

- [✨ 项目特色](#-项目特色)
- [🚀 下载与安装](#-下载与安装)
- [🎮 游戏玩法与操作](#-游戏玩法与操作)
- [🌐 多人联机](#-多人联机)
- [📰 更新日志](#-更新日志)
- [📚 攻略与数据文档](#-攻略与数据文档)
- [❓ 常见问题](#-常见问题)
- [🛠️ 技术与开发](#️-技术与开发)
- [🧹 卸载与第三方代码](#-卸载与第三方代码)

---

## ✨ 项目特色

| 特色系统 | 功能介绍 |
| :--- | :--- |
| **⚔️ 泰拉瑞亚成长体系** | 四种职业、上千件物品、武器与饰品、原版配方、矿石和材料，以及按冒险阶段安排的掉落。 |
| **⛏️ 挖掘与建造** | 用镐子挖掘圣巢地形、放置方块、搭建平台和工作站；探索不再只有一种路线。 |
| **👑 战斗与首领** | 泰拉瑞亚风格的生命、魔力、装备数值和伤害体系，与空洞骑士原有首领和探索流程结合。 |
| **🎚️ 难度与模式** | 普通、困难、专家、大师四种难度，以及适合测试、探索和展示的创造模式。 |
| **🗺️ 探索与便利功能** | 怪物图鉴、城镇 NPC 和商店、晶塔传送、多个存档位、装备配装切换和物品整理。 |
| **🤝 多人联机** | 最多 16 人 IP 联机，支持局域网与虚拟局域网连接，协同探索、战斗和建造。 |

> 以上是主要特色概览。具体机制、数值和限制请参阅 **[🧩 完整功能文档](docs/FEATURES.md)**。

## 🚀 下载与安装

### 运行环境

**支持平台：Windows 64 位。**

需要在同一台电脑上安装 Steam 版《空洞骑士》与《泰拉瑞亚》。Mod 运行时会读取本机泰拉瑞亚的贴图、音效和文字资源，因此**仅安装空洞骑士是不够的**。

| 所需组件 | 要求 |
| :--- | :--- |
| **空洞骑士** | Steam 正式版 **1.5.12620** |
| **泰拉瑞亚** | Steam **1.4.x** |
| **Mod 加载器** | **BepInEx 5.4.23.x**，Windows x64 |
| **操作系统** | **Windows 64 位** |

### 快速安装

1. 从 [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases) 下载对应的 **5.4.23.x win_x64** 版本，解压至空洞骑士游戏目录。
2. 启动一次空洞骑士，进入主菜单后退出，以初始化 BepInEx。
3. 从 [TerraKnight Releases](https://github.com/haoawake/TerraKnight/releases) 下载最新版压缩包，解压覆盖至同一个游戏目录。
4. 双击游戏目录中的 **`泰拉圣巢.bat`** 启动 Mod。

> [!IMPORTANT]
> **建议使用新存档，并提前备份原版存档。**
>
> 如果遇到无法加载、游戏版本不匹配或 MonoMod / Modding API 冲突，请阅读完整排查步骤。

**📖 [查看安装说明、目录示例与故障排查 →](docs/INSTALL.md)**

## 🎮 游戏玩法与操作

在国王山道开始新的冒险，以泰拉瑞亚武器和饰品代替原有的一部分移动与战斗能力。你可以切换职业配装、挖掘与建造、收集材料并合成新装备。

| 指南 | 内容 |
| :--- | :--- |
| **[🎮 游戏操作指南](docs/GAMEPLAY.md)** | 启动方式、操作键位和游玩提示 |
| **[🧩 完整功能说明](docs/FEATURES.md)** | 完整游戏机制与全部功能说明 |
| **[📺 实机视频演示](https://www.bilibili.com/video/BV1H4pr6LEAM/)** | 在 Bilibili 观看 TerraKnight 演示视频 |

## 🌐 多人联机

支持**最多 16 人**的泰拉瑞亚风格 IP 直连联机，默认端口为 **`7777`**，可以设置房间密码。

玩家各自使用自己的角色和存档，在相同场景中一起探索、战斗和建造。

> [!NOTE]
> 多人模式仍有部分同步限制，例如部分首领机关与商店行为。建议所有玩家使用相同的 Mod 版本。

**🌐 [开服、加入、同步规则与已知限制 →](docs/MULTIPLAYER.md)**

## 📰 更新日志

这里记录各个版本的改动、功能新增和修复。最新版本及宣传片请以 [Releases 页面](https://github.com/haoawake/TerraKnight/releases) 为准。

| 版本 | 主要更新 |
| :--- | :--- |
| **2.6.1** | 各职业配装制作栏都列出完整配方。 |
| **2.6.0** | 重做分阶段掉落、材料合成与吉欧分配。 |
| **2.5.x—2.4.x** | 首领机制、地图、光照、工作站、梦之钉等改进。 |
| **2.0** | 引入多人模式、按键设置及更多存档位。 |

**[📜 查看完整更新日志](docs/CHANGELOG.md)** · **[⬇️ 查看所有 Releases](https://github.com/haoawake/TerraKnight/releases)**

## 📚 攻略与数据文档

以下文档提供更详细的机制介绍、游玩进度和测试资料。

| 文档 | 内容 |
| :--- | :--- |
| **[完整功能一览](docs/FEATURES.md)** | 职业、道具、世界、战斗、经济、创造模式等完整机制 |
| **[阶段对照](阶段对照.md)** | 空洞骑士探索进度与泰拉瑞亚阶段的对应关系 |
| **[怪物遭遇顺序](怪物遭遇顺序.md)** | 按探索进度安排的敌人与遭遇信息 |
| **[掉落记录](掉落记录.md)** | 怪物掉落物、来源和掉落概率 |
| **[平衡性测试报告](平衡性测试报告.md)** | 数值与战斗平衡测试数据 |
| **[更新日志](docs/CHANGELOG.md)** | 各版本新增内容与修复 |

## ❓ 常见问题

<details>
<summary><strong>进入游戏后还是原版小骑士？</strong></summary>

确认通过 `泰拉圣巢.bat` 启动，并查看 `BepInEx/LogOutput.log` 是否存在 TerraKnight 加载记录。

</details>

<details>
<summary><strong>可以直接从 Steam 启动原版吗？</strong></summary>

可以。启动器会在启动 Mod 时临时启用 BepInEx；在首次使用启动器前，刚安装的 BepInEx 可能默认仍为开启状态。

</details>

<details>
<summary><strong>为什么必须安装泰拉瑞亚？</strong></summary>

因为 Mod 会直接从你电脑上安装的泰拉瑞亚读取游戏素材和本地化资源，而不是将这些素材打包在 Mod 中。

</details>

<details>
<summary><strong>与 Scarab / Lumafly 或旧版 Modding API 冲突怎么办？</strong></summary>

请参考安装文档中的 MonoMod、Mono.Cecil 和 Modding API 依赖冲突排查步骤，避免混用不兼容的加载环境。

</details>

**🔧 [查看完整问题排查 →](docs/INSTALL.md#4-可能遇到的问题)**

## 🛠️ 技术与开发

TerraKnight 基于 **BepInEx**，运行空洞骑士本体，并在运行时实现泰拉瑞亚风格的系统；场景地形、碰撞、资源读取和部分地图处理都有专门的实现。

### 代码目录

| 目录 | 内容 |
| :--- | :--- |
| [`src/`](src/) | 项目源码 |
| [`launcher/`](launcher/) | 启动器相关代码 |
| [`tools/`](tools/) | 相关工具 |
| [`promo/`](promo/) | 宣传相关资源与工具 |

**🛠️ [实现原理、环境要求、配置选项与开发说明 →](docs/DEVELOPMENT.md)**

## 🧹 卸载与第三方代码

需要卸载时，请先阅读说明，避免误删存档或其他 Mod 文件。第三方代码来源及协议说明也在这里。

**📦 [卸载步骤与第三方代码声明 →](docs/UNINSTALL.md)**

---

<div align="center">
  <p><strong>作者 Bilibili 主页：</strong> <a href="https://space.bilibili.com/438616101">访问作者空间</a></p>
  <p><strong>TerraKnight 为非官方粉丝创作项目，与 Re-Logic 或 Team Cherry 无官方关联。</strong></p>
</div>

[⬆ 返回顶部](#泰拉圣巢--terraknight)
