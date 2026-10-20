using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight
{
    [BepInPlugin(Guid, ModName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.haoawake.terraknight";
        public const string ModName = "TerraKnight";
        public const string Version = "4.3.0";

        internal static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            // Before anything of the mod's runs (Mono sets up the classes a method touches when it compiles it, and some
            // look up the game's methods as they do): this build is for Hollow Knight 1.5.12620 on Unity 6.
            if (!GameVersionOk()) return;
            Load();
        }

        /// <summary>
        /// The game this build is for: Hollow Knight 1.5.12620 (Steam's current version, Unity 6000). An older one (1.5.78
        /// on Unity 2020, the branch the old Modding API mods want) lacks what the mod hooks: said in the log, nothing loaded.
        /// </summary>
        static bool GameVersionOk()
        {
            string unity = Application.unityVersion ?? "";
            bool hook = typeof(HeroController).GetMethod("SendHeroInPosition", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) != null;
            if (unity.StartsWith("6000") && hook) return true;
            Log.LogError($"空洞泰拉 {Version} 需要空洞骑士 1.5.12620（Steam 当前正式版，Unity 6000），这台电脑上的游戏是 Unity {unity}" +
                         $"（{(unity.StartsWith("2020") ? "1.5.78 旧版本" : "版本不对")}），mod 没有加载。请在 Steam 库里右键空洞骑士 → 属性 → 测试版，选“无”，等它更新到当前版本后再启动。");
            Log.LogError($"TerraKnight {Version} needs Hollow Knight 1.5.12620 (Steam's current version, Unity 6000); this game runs Unity {unity}. " +
                         "Nothing was loaded. In Steam: Hollow Knight > Properties > Betas > None, let it update, then start it again.");
            return false;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        void Load()
        {
            // Dev: a second copy of the game for a LAN test keeps its own saves and runs its own suite.
            DevCopy.Read();
            var terrariaDirSetting = Config.Bind("Paths", "TerrariaDir", "",
                "泰拉瑞亚安装目录。留空时自动在空洞骑士所在的 Steam 库里找。\nTerraria's install folder. Left empty, it is looked for in the Steam library Hollow Knight is in.");
            // The old Dev.InfiniteItems (on by default) made every save endless; creative mode is a save's own now.
            var infiniteItems = Config.Bind("Dev", "InfiniteItemsAllSaves", false,
                "开发用：所有存档都像创造模式一样（物品用不完，制作栏全部解锁、不要吉欧）。平时用存档界面的“创造模式”按钮创建创造模式存档。\nDev: every save plays like Creative mode (endless items, the whole crafting menu unlocked, no Geo cost). For normal play, create Creative mode saves with the \"Creative mode\" button on the save screen.");
            var autoTestSlot = Config.Bind("Dev", "AutoTestSlot", 0,
                "开发用：填 1-50 时启动后自动进入这个存档位（空的就开新游戏）并跑一遍自动测试、截图。0 = 关闭。\nDev: 1-50 enters this save slot right after start-up (a new game if it is empty) and runs the autotest with screenshots. 0 = off.");
            var quitAfterTest = Config.Bind("Dev", "QuitAfterAutoTest", true,
                "开发用：自动测试跑完后退出游戏。\nDev: quit the game when the autotest is done.");
            var testSuite = Config.Bind("Dev", "AutoTestSuite", "all",
                "开发用：自动测试跑哪些项目。all = 全部，seamless = 只测无缝拼接，profile = 无缝拼接并给后台载入的脚本初始化计时，move = 移动手感，items = 武器和物品，ui = 界面，lift = 电梯，fixes = 手放地形 / 挖掉的尖刺 / 席奥小屋，features = 背包和制作栏 / 剑气 / 血条 / 飞毯 / 晶塔 / 丢东西，platform = 平台和楼梯，ammo = 全部箭和子弹，weapons = 新武器，accessories = 新饰品，accvisual = 饰品外观，loadout = 四套配装和按职业切换的制作栏，summon = 召唤师（法杖、召唤物、鞭子、标记、增益栏），progression = 阶段进度（配方制作栏、德特茅斯 NPC 商店、怪物掉落和图鉴、刷怪翻倍），tuning = 晶塔解锁 / 敌人强度和首领霸体 / 刷新和尸体 / 雷达，swords = 困难模式近战（真永夜刃、真断钢剑、无头骑士剑、种子弯刀、波涌之刃、彩虹猫之刃、狂星之怒、矿剑、长枪、回旋镖），ranged = 困难模式枪弓（巨兽鲨、发条步枪、狙击、霰弹、连弩、风暴弓、海啸、骸骨弓等），magic = 困难模式魔法（水晶风暴、诅咒焰、黄金雨、毒牙法杖、邪恶三叉戟、流星法杖、幽灵法杖、狱火叉等），visual = 越线时的画面和镜头。\nDev: which autotest suites run. all = everything, seamless = the seamless joins only, profile = seamless joins with timing of the background-loaded scripts' start-up, move = movement feel, items = weapons and items, ui = interface, lift = lifts, fixes = hand-placed terrain / dug-out spikes / Sheo's hut, features = inventory and crafting menu / sword beams / health bars / Flying Carpet / pylons / dropping items, platform = platforms and stairs, ammo = every arrow and bullet, weapons = new weapons, accessories = new accessories, accvisual = how accessories look, loadout = the four loadouts and the class-based crafting menu, summon = summoner (staffs, minions, whips, tags, buff bar), progression = stage progression (recipes, Dirtmouth NPC shops, enemy drops and the Bestiary, doubled enemies), tuning = pylon unlocking / enemy strength and boss super armour / respawns and corpses / radar, swords = Hardmode melee (True Night's Edge, True Excalibur, The Horseman's Blade, Seedler, Influx Waver, Meowmere, Star Wrath, Hardmode ore swords, spears, boomerangs), ranged = Hardmode guns and bows (Megashark, Clockwork Assault Rifle, Sniper Rifle, shotguns, repeaters, Daedalus Stormbow, Tsunami, Marrow and more), magic = Hardmode magic (Crystal Storm, Cursed Flames, Golden Shower, Venom Staff, Unholy Trident, Meteor Staff, Spectre Staff, Inferno Fork and more), visual = the picture and camera when crossing gate lines.");
            var terrariaMovement = Config.Bind("Gameplay", "TerrariaMovement", true,
                "用泰拉瑞亚的移动手感（跑速、加速度、跳跃高度、下落速度）。关闭后使用空洞骑士的手感。\nTerraria's movement feel (run speed, acceleration, jump height, fall speed). Off: Hollow Knight's own feel.");
            Player.TerraMovement.Enabled = terrariaMovement.Value;
            // 2.3.1: the game's own camera by default, as its rooms are one at a time again (a new key, as for the world below).
            var centeredCamera = Config.Bind("Gameplay", "CenteredCameraExperimental", false,
                "泰拉瑞亚式镜头，默认关：人物始终在画面中央，适合和无缝地图一起用（单独用时走到房间边上会看到房间外面）。关闭时是空洞骑士原版的镜头（前瞻、房间锁定）。\nTerraria-style camera, off by default: the player stays in the centre of the screen. It suits the seamless world; on its own, the view shows beyond a room's edges near them. Off: Hollow Knight's vanilla camera (look-ahead, room locks).");
            Player.TerraCamera.Enabled = centeredCamera.Value;
            // 2.3.1: the original game's scene changes again by default (the stitched world put some players back
            // where they came from at gates); a new key, so the old "SeamlessWorld = true" in players' configs doesn't carry.
            var seamless = Config.Bind("Gameplay", "SeamlessWorldExperimental", false,
                "实验功能，默认关：相连的场景（左右和上下）拼成一张连续的地图，隔壁场景提前载入，走过去不读取。关闭时是空洞骑士原版的场景切换（过门读取）。\nExperimental, off by default: connected scenes (side by side and above each other) join into one continuous map; neighbouring scenes load ahead and are walked into without loading. Off: Hollow Knight's vanilla room transitions (loading at each door).");
            World.LiveWorld.Enabled = seamless.Value;
            var barsAlways = Config.Bind("Gameplay", "EnemyHealthBarsAlways", false,
                "敌人的泰拉瑞亚血条一直显示。关闭时和泰拉瑞亚一样，敌人掉血后才显示。\nEnemies' Terraria health bars always show. Off: as in Terraria, a bar shows once the enemy has lost health.");
            var autoReuse = Config.Bind("Gameplay", "AutoReuseAllItems", false,
                "泰拉瑞亚设置里的“自动开火”（自动挥舞）：开启时所有武器按住鼠标就一直挥舞、射击。游戏里物品栏右下角的“设置”里也能切换。\nAuto Reuse from Terraria's settings: with it on, every weapon keeps swinging and firing while the mouse button is held. It can also be switched in \"Settings\" at the bottom right of the inventory.");
            UI.IngameOptions.Bind(autoReuse, barsAlways, centeredCamera);
            UI.AutoPause.Bind(Config.Bind("Gameplay", "AutoPause", false,
                "泰拉瑞亚设置里的“自动暂停”：开启时单人游戏打开背包会暂停游戏（泰拉瑞亚设置窗口本身总会暂停）。多人模式不暂停。\nAutopause from Terraria's settings: with it on, opening the inventory pauses a single-player game (the settings window itself always pauses). Never in multiplayer."));
            Net.NetSettings.Bind(Config);
            Player.TerraKeys.Bind(Config);
            var multiplier = Config.Bind("Gameplay", "EnemyMultiplier", 2,
                "普通敌人的数量倍数：2 = 每个原版敌人旁边再多一只，1 = 原版数量。首领、竞技场、苦痛之路等特殊敌人不受影响。\nMultiplier for ordinary enemies: 2 = one more beside every vanilla enemy, 1 = vanilla numbers. Bosses, arenas, the Path of Pain and other special enemies are not affected.");
            World.EnemyRespawn.Multiplier = Mathf.Clamp(multiplier.Value, 1, 4);
            var respawnScale = Config.Bind("Gameplay", "EnemyRespawnScale", 1f,
                "普通敌人死后多久重新刷出：按强弱分档的时间（原版血量 10 以下 30 秒、25 以下 45 秒、50 以下 70 秒、更结实的 100 秒、精英 4 分钟）乘以这个倍数，0 为不刷新。刷出来前 3 秒，出生点先出现它的半透明虚影；主角站在出生点旁时，等主角走开再刷。\nHow long ordinary enemies take to respawn: the time by toughness (vanilla health under 10: 30 seconds, under 25: 45 seconds, under 50: 70 seconds, tougher: 100 seconds, elites: 4 minutes) times this factor; 0 = no respawns. 3 seconds before an enemy respawns, its translucent ghost appears on its spawn point; with the player standing beside the spot, it waits until the player has moved away.");
            var respawnSeconds = Config.Bind("Gameplay", "EnemyRespawnSeconds", 5f, "保留的旧设置：只有填 0（不刷新）时生效；刷新时间由 EnemyRespawnScale 决定。\nKept from older versions: only 0 (no respawns) has an effect; respawn times come from EnemyRespawnScale.");
            World.EnemyRespawn.Scale = respawnSeconds.Value <= 0f ? 0f : Mathf.Max(0f, respawnScale.Value);
            var bossDamage = Config.Bind("Gameplay", "BossDamage", 2f,
                "首领的伤害是同区域普通敌人的几倍（每面具换算的伤害乘这个数，首领的弹幕也算）。\nBoss damage as a multiple of ordinary enemies' in the same area (the damage per mask times this; bosses' projectiles count too).");
            World.Areas.BossDamage = Mathf.Clamp(bossDamage.Value, 0.5f, 5f);
            var eliteDamage = Config.Bind("Gameplay", "EliteDamage", 1.5f,
                "精英（复仇蝇之王、灵魂战士、大哨兵这类小首领和原本血量 70 以上的强敌）的伤害是同区域普通敌人的几倍。\nElite damage (mini-bosses such as the Vengefly King, the Soul Warrior and Great Husk Sentries, and strong enemies with 70+ vanilla health) as a multiple of ordinary enemies' in the same area.");
            World.Areas.EliteDamage = Mathf.Clamp(eliteDamage.Value, 0.5f, 5f);
            var skipIntro = Config.Bind("Gameplay", "SkipOpeningCutscenes", true,
                "新游戏跳过开场的序章诗和开场动画，加载好就直接在国王山道从传送门里走出来。\nA new game skips the opening poem and cutscene: once loaded, the player steps out of a portal in King's Pass.");
            Player.SkipIntro.Enabled = skipIntro.Value;
            var terrariaMap = Config.Bind("Gameplay", "TerrariaMap", true,
                "泰拉瑞亚式全屏地图（M）：照实际地形一格一格画，走到哪里揭开到哪里，长椅、路灯、门等画成方块，鼠标指着显示名字。关闭时使用空洞骑士地图块拼成的地图。\nTerraria-style full map (M): drawn tile by tile from the real terrain and revealed where the player goes; benches, lamps, doors and the like are drawn as blocks, and pointing at one shows its name. Off: the map assembled from Hollow Knight's map pieces.");
            UI.WorldMap.Enabled = terrariaMap.Value;
            UI.WorldMap.StyleSetting = Config.Bind("Gameplay", "MapStyle", 1,
                "泰拉瑞亚地图的显示方式（Tab 切换，会记住）：0 = 不显示，1 = 右上角小地图，2 = 半透明覆盖在画面上。\nHow the Terraria map shows (Tab cycles it and is remembered): 0 = hidden, 1 = minimap at the top right, 2 = translucent overlay over the screen.");
            Player.TerraCamera.Bind(Config);
            UI.TerraCursor.Bind(Config);
            UI.GameView.Bind(Config);
            Items.Charms.Bind(Config);
            UI.DialogueBroadcast.Bind(Config);
            Items.NailAim.Bind(Config);
            World.HoleShapes.Bind(Config);
            var darkness = Config.Bind("Gameplay", "HollowKnightDarkness", false,
                "保留空洞骑士的黑暗：没有光蝇灯笼时，黑暗区域（呼啸悬崖边缘、深巢、水晶山峰的矿道等）把视野缩成人物周围一小圈。默认关：不缩视野；开着泰拉瑞亚光照时，这些地方按泰拉瑞亚的方式是暗的，由火把、头灯、光蝇灯笼或法术的光照亮。\nHollow Knight's darkness: without the Lumafly Lantern, dark areas (the dark edges of the Howling Cliffs, Deepnest, Crystal Peak's mines and so on) close the view to a small circle around the player. Off by default: no narrowed view; with Terraria lighting on, these places are dark the Terraria way and are lit by torches, the Mining Helmet, the Lumafly Lantern or spells.");
            World.SceneMood.KeepDarkness = darkness.Value;
            var lighting = Config.Bind("Gameplay", "TerrariaLighting", true,
                "泰拉瑞亚式光照：按格子计算光，火把、头灯、子弹和法术的光从光源一格一格柔和地照亮四周，穿过方块衰减得很快；挖开的隧道、放下的方块和黑暗区域里光照不到的地方是暗的。关闭时使用简单的光晕。\nTerraria lighting: light is worked out per tile; torches, the Mining Helmet, bullets and spells light their surroundings softly, tile by tile, fading fast through blocks; dug tunnels, placed blocks and dark areas are dark where no light reaches. Off: a simple glow around lights.");
            World.LightMap.Enabled = lighting.Value;
            var frameRate = Config.Bind("Video", "TargetFrameRate", 144,
                "游玩时锁定的帧率（会关闭游戏自己的垂直同步）。0 = 使用游戏自己的设置。\nFrame rate cap while playing (switches off the game's own vsync). 0 = the game's own setting.");

            string terrariaDir = string.IsNullOrWhiteSpace(terrariaDirSetting.Value)
                // 同一个 Steam 库：...\steamapps\common\Hollow Knight -> ...\steamapps\common\Terraria
                ? Path.GetFullPath(Path.Combine(Paths.GameRootPath, "..", "Terraria"))
                : terrariaDirSetting.Value;
            if (!TerrariaAssets.Init(terrariaDir))
            {
                Log.LogError($"没找到泰拉瑞亚素材（{terrariaDir}）。请在 BepInEx/config/{Guid}.cfg 里填 TerrariaDir。");
                return;
            }

            if (!Terraria.TerrariaText.Init(terrariaDir)) Log.LogWarning("[语言] 没读到 Terraria.exe 的语言文件，非中文时泰拉瑞亚的物品名会显示中文");
            Lang.Changed += Items.ItemDb.Relocalize;
            var host = new GameObject("TerraKnight");
            DontDestroyOnLoad(host);
            var mod = host.AddComponent<TerraMod>();
            // The autotests keep their endless items.
            int testSlot = DevCopy.Name != null ? DevCopy.Slot : autoTestSlot.Value;
            string suite = DevCopy.Name != null && DevCopy.Suite != null ? DevCopy.Suite : testSuite.Value;
            TerraMod.DevAllInfinite = infiniteItems.Value || testSlot > 0;
            mod.SetInfiniteItems(TerraMod.DevAllInfinite);
            mod.TargetFrameRate = frameRate.Value;
            if (testSlot >= 1 && testSlot <= UI.SaveSlots.Max)
                mod.AutoTest = new DevAutoTest(mod, testSlot, quitAfterTest.Value, suite);

            var harmony = new Harmony(Guid);
            harmony.PatchAll();
            World.EnemyBlockers.Init();
            UI.TerraCursor.Install(host, harmony);
            if (mod.AutoTest != null && testSuite.Value == "profile") DevLoadProfiler.Install(harmony);
            bool lagSuite = mod.AutoTest != null && (suite == "lag" || suite.StartsWith("lag_") || suite == "sentryscan" || suite == "v251diag");
            // A player's slow room looked into: every script's per-frame time, written out with each slow stretch (PerfProbe).
            var frameProfiler = Config.Bind("Debug", "FrameProfiler", false,
                "开启后，进游戏时给每个脚本的每帧方法加计时（启动会慢几秒、运行稍慢）；每 5 秒里出现掉帧，就把最耗时的方法、敌人和刚体数量写进 BepInEx/LogOutput.log。排查卡顿用，平时关着。\nWhen on, every script's per-frame methods are timed (a few seconds more to start, a little slower to run); each 5 s with slow frames writes the costliest methods and the counts of enemies and bodies to BepInEx/LogOutput.log. For finding a slow room; keep it off otherwise.");
            if (lagSuite || frameProfiler.Value) DevFrameProfiler.Install(harmony);
            PerfProbe.Detailed = frameProfiler.Value && !lagSuite;
            Log.LogInfo($"{ModName} {Version} 已加载，泰拉瑞亚素材：{TerrariaAssets.ImagesDir}");
        }
    }
}
