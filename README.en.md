# TerraKnight

[中文](README.md) | **English**

> **Terraria meets Hollow Knight. Dig, build, fight, and explore Hallownest as a Terrarian.**

Replace the Knight with a Terraria adventurer. Dig through Hallownest with a Copper Pickaxe, build with blocks, craft weapons and accessories, and fight Hollow Knight's enemies with Terraria-style progression.

**[⬇️ Latest Release](https://github.com/haoawake/TerraKnight/releases)** · **[📖 Installation Guide](docs/INSTALL.en.md)** · **[🎮 Controls](docs/GAMEPLAY.en.md)** · **[🧩 All Features](docs/FEATURES.en.md)**

## 📑 Table of Contents

- [Highlights](#-highlights)
- [Download and Installation](#-download-and-installation)
- [Gameplay and Controls](#-gameplay-and-controls)
- [Multiplayer](#-multiplayer)
- [Changelog](#-changelog)
- [Guides and Reference](#-guides-and-reference)
- [FAQ](#-faq)
- [Technical Details and Development](#-technical-details-and-development)
- [Uninstallation and Credits](#-uninstallation-and-credits)

## ✨ Highlights

- **Terraria progression in Hallownest:** four classes, over a thousand Terraria items, equipment, crafting recipes, ores, materials, and stage-based drops.
- **Dig and build:** mine terrain, place blocks and platforms, and set up workstations while exploring Hallownest in new ways.
- **Reimagined combat and bosses:** Terraria-style health, mana, weapon damage, defenses, and equipment combined with Hollow Knight encounters.
- **Four difficulty levels and Creative mode:** Normal, Difficult, Expert, Master, and an unrestricted mode for experimenting and exploration.
- **Exploration and quality-of-life systems:** a Bestiary, town NPCs, shops, pylon teleportation, loadouts, inventory management, and additional save slots.
- **Direct-IP multiplayer for up to 16 players:** explore and fight together on a LAN or virtual LAN.

For the full breakdown of game systems and mechanics, see **[All Features](docs/FEATURES.en.md)**.

## 🚀 Download and Installation

**Platform: Windows 64-bit.** You need Steam installations of both **Hollow Knight** and **Terraria** on the same PC. TerraKnight loads Terraria's assets, audio, and localization from your installed game at runtime; Hollow Knight alone is not sufficient.

| Requirement | Version |
| --- | --- |
| Hollow Knight | Steam stable release **1.5.12620** |
| Terraria | Steam **1.4.x** |
| Mod loader | **BepInEx 5.4.23.x**, Windows x64 |
| Operating system | **Windows 64-bit** |

**Quick installation:**

1. Download **BepInEx 5.4.23.x win_x64** from [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases) and extract it into the Hollow Knight game directory.
2. Launch Hollow Knight once and quit after reaching the main menu so BepInEx can initialize.
3. Download the latest TerraKnight ZIP from [Releases](https://github.com/haoawake/TerraKnight/releases) and extract it into the same game directory.
4. Launch the mod by double-clicking **`泰拉圣巢.bat`** in the game directory.

**A fresh save slot is recommended; back up your original saves first.** For installation paths, conflicts with the Modding API, and troubleshooting steps, see the full guide.

**[Full installation guide and troubleshooting →](docs/INSTALL.en.md)**

## 🎮 Gameplay and Controls

Start a new journey in King's Pass as a Terrarian. Terraria weapons, armor, accessories, and movement mechanics replace much of the Knight's original combat and traversal.

- **[Launching, controls, and gameplay tips](docs/GAMEPLAY.en.md)**
- **[Complete mechanics and feature reference](docs/FEATURES.en.md)**

## 🌐 Multiplayer

TerraKnight supports **up to 16 players** via direct IP connection, using port `7777` by default, with optional passwords. Players can bring their own saves and explore, build, and fight together.

Some features, including certain boss encounters, environmental events, and shops, are not fully synchronized. All players should use the same TerraKnight version.

**[Hosting, joining, synchronization, and known limitations →](docs/MULTIPLAYER.en.md)**

## 📰 Changelog

Version history, improvements, and fixes are documented separately. Check Releases for current downloads and the trailer.

- **2.6.1:** All loadouts display the full crafting recipe list.
- **2.6.0:** Reworked stage-based loot, crafting materials, and Geo distribution.
- **2.5.x–2.4.x:** Changes to bosses, maps, lighting, workstations, the Dream Nail, and more.
- **2.0:** Multiplayer, customizable controls, and expanded save slots.

**[Full changelog →](docs/CHANGELOG.en.md)** · **[Releases →](https://github.com/haoawake/TerraKnight/releases)**

## 📚 Guides and Reference

| Document | What you'll find |
| --- | --- |
| [All Features](docs/FEATURES.en.md) | Classes, items, combat, world systems, economy, and Creative mode |
| [Stage Comparison](阶段对照.en.md) | Terraria progression stages matched to Hollow Knight exploration |
| [Enemy Encounter Order](怪物遭遇顺序.en.md) | Enemy encounters organized by progression |
| [Drop Records](掉落记录.md) | Enemy loot sources and drop probabilities |
| [Balance Testing Report](平衡性测试报告.en.md) | Combat statistics and balance testing |
| [Changelog](docs/CHANGELOG.en.md) | Version-by-version changes |

## ❓ FAQ

**Q: Why am I still playing as the Knight?**

Launch the game via `泰拉圣巢.bat` and check `BepInEx/LogOutput.log` for TerraKnight loading messages.

**Q: Can I still launch unmodded Hollow Knight through Steam?**

Yes. The launcher temporarily enables BepInEx when starting the mod. Note that a newly installed BepInEx setup may initially be enabled until the launcher has run.

**Q: Why do I need Terraria installed?**

The mod reads textures, sound, and localized strings directly from your local Terraria installation rather than redistributing those assets.

**Q: What if it conflicts with Scarab, Lumafly, or an older Modding API?**

See the installation guide for MonoMod, Mono.Cecil, and Modding API dependency conflicts.

**[Full troubleshooting guide →](docs/INSTALL.en.md#4-troubleshooting)**

## 🛠️ Technical Details and Development

TerraKnight runs Hollow Knight with **BepInEx** and implements Terraria-style systems in the game. Its terrain conversion, collision handling, asset loading, and scene systems are custom integrations.

Relevant source and tooling directories: [src/](src/), [launcher/](launcher/), [tools/](tools/), and [promo/](promo/).

**[Implementation, requirements, configuration, and development →](docs/DEVELOPMENT.en.md)**

## 🧹 Uninstallation and Credits

Follow the uninstallation instructions to avoid accidentally removing save data or other mods. Third-party code acknowledgments and licensing notes are included.

**[Uninstallation and third-party code →](docs/UNINSTALL.en.md)**

---

**TerraKnight is an unofficial fan-made project and is not affiliated with Re-Logic or Team Cherry.**

[⬆ Back to top](#terraknight)
