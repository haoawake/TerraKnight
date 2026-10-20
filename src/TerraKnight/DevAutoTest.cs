using System.Collections;
using System.IO;
using BepInEx;
using GlobalEnums;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Development only (off unless Dev.AutoTestSlot is set). Boots straight into a dedicated save
    /// slot, edits the terrain around the hero with a fixed script, checks the hero physics and
    /// writes screenshots and log lines, so changes can be verified without anyone playing.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        readonly TerraMod mod;
        readonly int slot;
        readonly bool quitAfter;
        readonly string outDir;
        readonly string suite;
        bool running, done;
        readonly bool suspendInputInBackground;
        readonly UnityEngine.InputSystem.InputSettings.BackgroundBehavior inputBackgroundBehavior;

        public DevAutoTest(TerraMod mod, int slot, bool quitAfter, string suite)
        {
            this.mod = mod;
            this.slot = slot;
            this.quitAfter = quitAfter;
            this.suite = suite ?? "all";
            // A suite ending in "_zh" runs with the game in Simplified Chinese (the trailer's Chinese cut).
            if (this.suite.EndsWith("_zh"))
            {
                zh = true;
                this.suite = this.suite.Substring(0, this.suite.Length - 3);
            }
            outDir = Path.Combine(Paths.BepInExRootPath, "TerraKnight", "dev");
            // A second copy of the game (DevCopy) keeps its pictures apart.
            if (DevCopy.Name != null) outDir = Path.Combine(outDir, DevCopy.Name);
            Directory.CreateDirectory(outDir);
            // Background suites inject keyboard events too. Unity keeps simulating, but
            // InControl otherwise stops updating its actions when the test window loses focus.
            Application.runInBackground = true;
            suspendInputInBackground = InControl.InputManager.SuspendInBackground;
            InControl.InputManager.SuspendInBackground = false;
            inputBackgroundBehavior = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && !keyboard.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
            // The window takes the focus as the game starts: clicks meant for something else must not use items.
            TerraMod.DevNoRealMouse = true;
            mod.StartCoroutine(Boot());
        }

        IEnumerator Boot()
        {
            GameManager gm;
            float bootProbeAt=Time.realtimeSinceStartup+8f;
            while ((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Menu_Title" || UIManager.instance == null)
            {
                if(suite=="mounts" && Time.realtimeSinceStartup>=bootProbeAt) {
                    bootProbeAt=Time.realtimeSinceStartup+15f;
                    Plugin.Log.LogInfo("[AutoTest] mount boot gm="+(gm!=null?gm.sceneName:"null")+" active="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" ui="+(UIManager.instance!=null));
                    ScreenCapture.CaptureScreenshot(Path.Combine(outDir,"mount_boot.png"));
                }
                yield return null;
            }
            yield return new WaitForSeconds(4f);
            if (zh) PromoChinese(gm);
            // A second copy of the game (DevCopy) never starts a game unless its saves go to its own folder.
            if (DevCopy.Name != null && !DevCopy.Verified)
            {
                Plugin.Log.LogError($"[AutoTest] 测试副本的存档目录没改成功（{(global::Platform.Current != null ? global::Platform.Current.GetType().Name : "无平台")}），不开游戏，退出");
                Application.Quit();
                yield break;
            }
            // The title screen: its logo (the mod's 空洞泰拉 for Chinese) and the pictures behind it.
            UI.MenuLogo.DevDump(outDir);
            Shot("Menu_Title", "menu");
            yield return null;
            yield return null;

            string save = Path.Combine(TerraMod.SaveDir, $"user{slot}.dat");
            // dev-run.ps1 found the test slot empty before starting the game: a save there older than
            // that is Steam Cloud bringing back one deleted outside the game. Cleared with the game
            // running, it goes from the cloud too.
            string emptyMark = Path.Combine(outDir, "slot_was_empty.txt");
            if (File.Exists(emptyMark))
            {
                var markedAt = File.GetLastWriteTimeUtc(emptyMark);
                File.Delete(emptyMark);
                bool stale = false;
                foreach (var f in Directory.GetFiles(TerraMod.SaveDir, $"user{slot}*"))
                {
                    string n = Path.GetFileName(f);
                    if ((n.StartsWith($"user{slot}.") || n.StartsWith($"user{slot}_")) && File.GetLastWriteTimeUtc(f) < markedAt) stale = true;
                }
                if (stale)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 存档位 {slot} 是 Steam 云同步回来的旧存档（开测前是空的），在游戏里删掉它");
                    bool cleared = false;
                    gm.ClearSaveFile(slot, ok => cleared = true);
                    float until = Time.realtimeSinceStartup + 10f;
                    while (!cleared && Time.realtimeSinceStartup < until) yield return null;
                    // The game's own copies of it (user2_1.5.12620.dat, its .bak files) go too.
                    foreach (var f in Directory.GetFiles(TerraMod.SaveDir, $"user{slot}*"))
                    {
                        string n = Path.GetFileName(f);
                        if ((n.StartsWith($"user{slot}.") || n.StartsWith($"user{slot}_")) && File.GetLastWriteTimeUtc(f) < markedAt) File.Delete(f);
                    }
                    foreach (var ext in new[] { "json", "map" })
                    {
                        var modFile = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.{ext}");
                        if (File.Exists(modFile) && File.GetLastWriteTimeUtc(modFile) < markedAt) File.Delete(modFile);
                    }
                }
            }
            // The LAN game's menu and server, before the game starts (DevAutoTestNet).
            if (suite == "net" || suite == "netsummon") yield return NetBoot();
            if (suite == "net2host" || suite == "net2client" || suite == "networldhost" || suite == "networldclient") yield return Net2Boot();
            if (suite == "netloothost" || suite == "netlootclient") yield return NetLootBoot();
            if (suite == "netaudit_host" || suite == "netaudit_client") yield return NetAuditBoot();
            if (suite.StartsWith("netboss_host") || suite == "netboss_client") yield return NetBossBoot();
            if (suite == "slots") yield return SlotsBoot();
            if (suite == "menuflow")
            {
                yield return MenuFlowTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "promoA")
            {
                // The trailer's opening: the profiles, the creative panel, the difficulty, recorded (DevAutoTestPromo2).
                yield return PromoMenu();
            }
            else if (suite == "creative")
            {
                // The save profiles screen, its creative mode button and panel, a creative game in the test slot.
                UIManager.instance.UIGoToProfileMenu();
                yield return new WaitForSeconds(3f);
                Shot("Menu_Title", "creative_1_profiles");
                yield return null;
                UI.CreativeMenu.DevOpen();
                yield return new WaitForSeconds(0.6f);
                Shot("Menu_Title", "creative_2_panel");
                yield return null;
                yield return null;
                UI.CreativeMenu.DevChoose(slot);
                yield return new WaitForSeconds(0.3f);
                Shot("Menu_Title", "creative_3a_confirm");
                yield return null;
                yield return null;
                UI.CreativeMenu.DevConfirm();
                yield return new WaitForSeconds(0.3f);
                Shot("Menu_Title", "creative_3b_difficulty");
                yield return null;
                yield return null;
                UI.CreativeMenu.DevPick(World.DifficultyLevel.Expert);
            }
            else if (File.Exists(save))
            {
                Plugin.Log.LogInfo($"[AutoTest] 读取测试存档 {slot}");
                gm.LoadGameFromUI(slot);
            }
            else
            {
                Plugin.Log.LogInfo($"[AutoTest] 存档位 {slot} 是空的，开新游戏");
                gm.profileID = slot;
                gm.StartNewGame();
                mod.StartCoroutine(OpeningShots());
                mod.StartCoroutine(IntroShots());
            }

            // Skip the opening cinematic as soon as it allows it.
            float timeout = Time.realtimeSinceStartup + 240f;
            while (!done && Time.realtimeSinceStartup < timeout)
            {
                var g = GameManager.UnsafeInstance;
                if (g != null && g.gameState == GameState.CUTSCENE && g.inputHandler != null
                    && g.inputHandler.skipMode == SkipPromptMode.SKIP_PROMPT)
                {
                    g.SkipCutscene();
                    yield return new WaitForSecondsRealtime(1f);
                }
                yield return null;
            }
        }

        /// <summary>What the screen shows between New Game and King's Pass (the opening's cinematics are skipped).</summary>
        IEnumerator IntroShots()
        {
            float start = Time.realtimeSinceStartup, wait = start + 60f;
            int n = 0;
            while (Time.realtimeSinceStartup < wait && n < 30)
            {
                var g = GameManager.UnsafeInstance;
                if (g != null && g.sceneName == "Tutorial_01") break;
                if (g != null && g.sceneName == "Opening_Sequence" && n % 1 == 0) Shot("Opening_Sequence", $"intro_{n:D2}");
                n++;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 开场：点新游戏后 {Time.realtimeSinceStartup - start:F1} 秒进入国王山道，跳过过场 {Player.SkipIntro.DevSkipped} 段");
        }

        /// <summary>A new game drops the player into King's Pass from above: what is on screen on the way down.</summary>
        IEnumerator OpeningShots()
        {
            float wait = Time.realtimeSinceStartup + 120f;
            GameManager g;
            while (((g = GameManager.UnsafeInstance) == null || g.sceneName != "Tutorial_01" || HeroController.SilentInstance == null) && Time.realtimeSinceStartup < wait)
                yield return null;
            var hero = HeroController.SilentInstance;
            if (hero != null)
            {
                // What the grid made of the drop: each cell of the hero's column, and the colliders there.
                var region = TerraWorld.Live;
                var at = CellGrid.WorldToCell(hero.transform.position);
                var column = new System.Text.StringBuilder();
                for (int y = at.y + 4; y >= at.y - 130; y -= 2)
                {
                    var r = TerraWorld.RegionAt(at.x, y);
                    char ch = r == null ? '?' : TerraWorld.Get(at.x, y) == CellKind.Empty ? '.' : r.Grid.IsArt(at.x - r.CellOffset.x, y - r.CellOffset.y) ? 'a' : '#';
                    column.Append(ch);
                }
                Plugin.Log.LogInfo($"[AutoTest] 开局下落这一列（从 y={at.y + 4} 往下每 2 格，? 不在任何场景，# 实心，a 手放碰撞体，. 空）：{column}；本场景范围 {region?.CellRect}");
                foreach (var probe in new[] { 92f, 85f, 78f })
                {
                    var pt = new Vector2(hero.transform.position.x, probe);
                    var hits = new System.Text.StringBuilder();
                    foreach (var c2 in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (c2.bounds.Contains(new Vector3(pt.x, pt.y, c2.bounds.center.z)))
                            hits.Append($"\n    {PathOf(c2.transform)} [{c2.GetType().Name} 层{c2.gameObject.layer} trigger={c2.isTrigger} 启用={c2.enabled && c2.gameObject.activeInHierarchy} 范围 {c2.bounds.min:F1}..{c2.bounds.max:F1}]");
                    Plugin.Log.LogInfo($"[AutoTest] 开局 ({pt.x:F1},{pt.y:F1}) 处范围内的碰撞体：{hits}");
                }
            }
            for (int i = 0; i < 14 && hero != null; i++)
            {
                var col = hero.GetComponent<BoxCollider2D>();
                var cell = CellGrid.WorldToCell(col.bounds.center);
                Plugin.Log.LogInfo($"[AutoTest] 开局 {i * 0.5f:F1}s：主角 {hero.transform.position:F2}，格 {cell} 实心={TerraWorld.IsSolid(cell.x, cell.y)}，" +
                                   $"速度 {hero.GetComponent<Rigidbody2D>().linearVelocity:F1}，onGround={hero.cState.onGround}，可操作={GameHooks.PlayerHasControl()}，状态 {g.gameState}");
                var cam = GameCameras.instance.mainCamera;
                var fade = GameCameras.instance.cameraFadeFSM;
                Plugin.Log.LogInfo($"[AutoTest] 开局 {i * 0.5f:F1}s 镜头 {cam.transform.position:F2}，淡入 FSM 状态 {fade?.ActiveStateName}");
                if (i == 1 || i == 4)
                {
                    // Everything on the ray through the middle of the screen, nearest first.
                    var cp = cam.transform.position;
                    var list = new System.Collections.Generic.List<(float z, string s)>();
                    foreach (var rd in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!rd.enabled || !rd.gameObject.activeInHierarchy || rd.gameObject.layer == 5) continue;
                        var bb = rd.bounds;
                        if (!bb.Contains(new Vector3(cp.x, cp.y, bb.center.z))) continue;
                        Color colr = rd is SpriteRenderer sr2 ? sr2.color : rd.sharedMaterial != null && rd.sharedMaterial.HasProperty("_Color") ? rd.sharedMaterial.color : Color.white;
                        string what = rd is SpriteRenderer sr3 && sr3.sprite != null ? sr3.sprite.name : rd.sharedMaterial != null ? rd.sharedMaterial.name : "?";
                        list.Add((bb.center.z, $"{PathOf(rd.transform)} [{rd.GetType().Name} {what} z={bb.center.z:F1} 层{rd.sortingLayerName}/{rd.sortingOrder} 色{colr} 范围 {bb.min:F1}..{bb.max:F1}]"));
                    }
                    list.Sort((a, b) => a.z.CompareTo(b.z));
                    var sbr = new System.Text.StringBuilder();
                    foreach (var e in list) if (sbr.Length < 6000) sbr.Append("\n    ").Append(e.s);
                    Plugin.Log.LogInfo($"[AutoTest] 开局 {i * 0.5f:F1}s 屏幕中心射线上的渲染器 {list.Count} 个：{sbr}");
                }
                Shot("Tutorial_01", $"00_opening_{i:D2}");
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        public void OnSceneReady(GameManager gm)
        {
            if (running || done) return;
            running = true;
            mod.StartCoroutine(Run(gm.sceneName));
        }

        IEnumerator Run(string scene)
        {
            // The menu flow plays its saves itself (Boot): nothing else runs in them.
            if (suite == "menuflow") yield break;
            yield return new WaitForSeconds(3f);
            // A loaded save starts on its bench: up, as a player gets up (a key press).
            float nextGetUp = Time.time + 3f;
            while (!GameHooks.PlayerHasControl())
            {
                if (Time.time >= nextGetUp && PlayerData.instance != null && PlayerData.instance.atBench)
                {
                    nextGetUp = Time.time + 3f;
                    yield return Tap(UnityEngine.InputSystem.Key.Space);
                }
                yield return null;
            }

            var terrain = TerrainSystem.Current;
            var hero = HeroController.instance;
            if (terrain == null)
            {
                Plugin.Log.LogError($"[AutoTest] {scene} 没有地形系统");
                Finish();
                yield break;
            }
            LogDiagnostics(hero);
            Plugin.Log.LogInfo($"[AutoTest] 进场时本场景已有 {World.WorldEdits.For(scene).Count} 处存档改动");
            Shot(scene, "1_start");
            yield return new WaitForSeconds(0.3f);
            if (suite.StartsWith("fx_"))
            {
                yield return Fix1016(suite.Substring(3));
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite.StartsWith("p4_"))
            {
                yield return Promo4(suite.Substring(3));
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "currencies") { yield return CurrenciesTest(); Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break; }
            if (suite == "rtorch" || suite.StartsWith("rtorch_")) { yield return RegionalTorchTest(suite.Length > 7 ? suite.Substring(7) : "all"); Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break; }
            if (suite == "housing")
            {
                yield return HousingTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "banners")
            {
                yield return BannersTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "crystalair")
            {
                yield return CrystalAirTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "lifeblood")
            {
                yield return LifebloodTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "maskhearts")
            {
                yield return MaskHeartsTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "dreamwake")
            {
                yield return DreamWakeTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "dreamexitprobe")
            {
                yield return DreamExitProbe();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "dreamfk")
            {
                yield return DreamFkTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "wallswing")
            {
                yield return WallSwingTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "dreamfinalprobe")
            {
                yield return DreamFinalProbe();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite.StartsWith("sealprobe_"))
            {
                yield return SealProbe(suite.Substring(10));
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "bossdropvis" || suite.StartsWith("bossdropvis_"))
            {
                yield return BossDropVisTest(suite.Length > 12 ? suite.Substring(12) : null);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "sealstart" || suite.StartsWith("sealstart_"))
            {
                yield return SealStartTest(suite.Length > 10 ? suite.Substring(10) : null);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "bossseals")
            {
                yield return BossSealsTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "building")
            {
                yield return BuildingTest();
                yield return GhostTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "vanillarecipes")
            {
                yield return VanillaRecipesTest();
                yield return WikiAssetsExport();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "craftsupplement")
            {
                yield return CraftingSupplementTest();
                yield return WikiAssetsExport();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "wikiassets")
            {
                yield return WikiAssetsExport();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "bookstage")
            {
                yield return BookStageTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "resourcestages")
            {
                yield return ResourceStagesTest();
                yield return MaterialDropsTest();
                yield return ProgressionAuditTest(false);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "materialcontinuity")
            {
                yield return ResourceStagesTest();
                yield return MaterialContinuityTest();
                yield return ProgressionAuditTest(false);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "materialdrops")
            {
                yield return MaterialDropsTest();
                yield return AccessoryDropsTest();
                yield return ProgressionAuditTest(false);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if(suite=="novicecampaign" || suite=="noviceretake" || suite=="novicephase" || suite=="noviceinteract")
            {
                yield return NoviceCampaignReplay();
                Plugin.Log.LogInfo("[AutoTest] 完成");Finish();yield break;
            }
            if(suite=="noviceaudit")
            {
                yield return NoviceNumericalAudit();
                yield return VanillaRecipesTest();
                yield return NoviceTimberComparison();
                yield return LootBalanceTest(false);
                yield return LootQualityTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");Finish();yield break;
            }
            if(suite=="novice" || suite=="noviceall" || suite=="noviceacquire")
            {
                yield return NoviceTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();yield break;
            }
            if(suite=="lootquality" || suite=="lootregions")
            {
                yield return CombatUpgradeTest();
                yield return LootQualityTest();
                yield return LootBalanceTest(false);
                yield return CraftProgressionTest();
                if(suite=="lootquality")yield return WeaponBalanceTest(true,true);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();yield break;
            }
            if (suite == "combatupgrade")
            {
                yield return CombatUpgradeTest();
                yield return LootBalanceTest();
                yield return CraftProgressionTest();
                yield return VanillaRecipesTest();
                yield return WeaponBalanceTest(true);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "craftprogression")
            {
                yield return CraftProgressionTest();
                yield return ProgressionAuditTest(false);
                yield return VanillaRecipesTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "accessorydrops")
            {
                yield return AccessoryDropsTest();
                yield return ProgressionAuditTest(false);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "progressionfinal")
            {
                yield return ProgressionAuditTest(false);
                yield return VanillaRecipesTest();
                yield return BalanceTest(GameManager.instance.sceneName,HeroController.instance);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "stageaudit" || suite == "progressionbalance")
            {
                yield return ProgressionAuditTest(suite == "progressionbalance");
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "mapspatial")
            {
                yield return MapSpatialTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bosslandings" || suite.StartsWith("bosslandings_"))
            {
                yield return BossLandingAudit(suite == "bosslandings" ? null : suite.Substring("bosslandings_".Length));
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bosslandingsurvey" || suite.StartsWith("bosslandingsurvey_"))
            {
                yield return BossLandingSurvey(suite == "bosslandingsurvey" ? null : suite.Substring("bosslandingsurvey_".Length));
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bestiarysync")
            {
                yield return BestiarySyncTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bestiarydrops")
            {
                yield return BestiaryDropStageTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "utilityblacklist")
            {
                yield return UtilityBlacklistTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "observatory")
            {
                yield return ObservatoryTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "godhomemap")
            {
                yield return GodhomeMapSurvey();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "godhomeverify")
            {
                yield return GodhomeMapVerify();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "projectilevisibility")
            {
                yield return ProjectileVisibilityTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bosslifecycle")
            {
                yield return BossLifecycleTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "hkprotected")
            {
                yield return HkProtectedTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "abilitybag")
            {
                yield return AbilityBagTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "lootbalance")
            {
                yield return LootBalanceTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "godhomebalance")
            {
                yield return GodhomeBalanceTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "miniongrowth")
            {
                yield return MinionGrowthTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bosscurve" || suite == "bosscurvebefore")
            {
                yield return BossCurveTest(suite == "bosscurvebefore");
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "mountvisibility")
            {
                yield return MountVisibilityTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "mounts")
            {
                yield return MountTest();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "soularrival")
            {
                yield return SoulArrivalInspect();
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "masterattack")
            {
                yield return BossLoadoutTest(false, true);
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "masterattackforge")
            {
                yield return BossLoadoutTest(false, true, true);
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "masterattackvariants")
            {
                yield return BossLoadoutTest(false, true, false, true);
                Plugin.Log.LogInfo("[AutoTest] 完成"); Finish(); yield break;
            }
            if (suite == "bossloadouts" || suite == "bossloadoutsendgame")
            {
                yield return BossLoadoutTest(suite == "bossloadoutsendgame");
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "armorrestore")
            {
                yield return ArmorRestoreTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "weaponbalance" || suite == "weaponintegration")
            {
                yield return WeaponBalanceTest(suite=="weaponintegration");
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if(suite=="lootmagic")
            {
                yield return LootCombatTest((label,pass,detail)=>Plugin.Log.LogInfo($"[AutoTest] {label} {detail} {(pass ? "通过" : "不通过")}"),new System.Collections.Generic.List<object>(),5,1);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "healthbook" || suite == "healthmath")
            {
                yield return HealthBookTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "hitframes")
            {
                yield return NoHitFramesTest(scene);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "profile")
            {
                // Crossings make the neighbours load; their start-up is what gets timed. Respawning
                // in Dirtmouth loads King's Pass as a neighbour too.
                yield return SeamlessTest(scene, hero);
                yield return DeathTest(hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "pit")
            {
                yield return PitTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "conveyor")
            {
                yield return ConveyorTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "geobank")
            {
                yield return GeoBankTest(hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "dreamentry")
            {
                yield return DreamEntryTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "rope")
            {
                yield return RopeTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "fkdeath")
            {
                yield return FalseKnightDeathTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "batch5")
            {
                yield return Batch5Test();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite.StartsWith("gatecheck_") || suite.StartsWith("gatewon_"))
            {
                // gatewon_: with every world boss beaten (the ways their fights open).
                if (suite.StartsWith("gatewon_")) Items.BossArena.DevAllBeaten();
                yield return GateCheckTest(suite.Substring(suite.IndexOf('_') + 1).Split('+'));
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "revek2")
            {
                yield return RevekTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "revek")
            {
                // Revek in the Spirits' Glade: his FSMs, his health manager, what his slash is made of.
                PlayerData.instance.SetBool("hasDreamNail", true);
                PlayerData.instance.isInvincible = true;
                yield return GoTo("RestingGrounds_08", EntryGateOf("RestingGrounds_08"));
                for (int i = 0; i < 8; i++)
                {
                    yield return new WaitForSeconds(3f);
                    var sb = new System.Text.StringBuilder();
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (f.gameObject.scene.name.StartsWith("RestingGrounds_08") && (f.gameObject.name.Contains("Revek") || f.transform.root.name.Contains("Revek") || f.FsmName.Contains("Revek")))
                            sb.Append($" {PathOf(f.transform)}/{f.FsmName}:{f.ActiveStateName}{(f.gameObject.activeInHierarchy ? "" : "(关)")}");
                    Plugin.Log.LogInfo($"[AutoTest] 瑞维克 {i}：主角 {(Vector2)HeroController.instance.transform.position:F1}{sb}");
                    if (i == 2 || i == 7)
                        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetSceneByName("RestingGrounds_08").GetRootGameObjects())
                            if (root.name.Contains("Revek") || root.GetComponentInChildren<PlayMakerFSM>(true) != null && root.GetComponentsInChildren<PlayMakerFSM>(true).Length > 0 && System.Array.Exists(root.GetComponentsInChildren<Transform>(true), t => t.name.Contains("Revek")))
                            {
                                foreach (var hm in root.GetComponentsInChildren<HealthManager>(true)) Plugin.Log.LogInfo($"[AutoTest] 瑞维克 血量管理器 {PathOf(hm.transform)} hp {hm.hp} 无敌 {hm.IsInvincible}");
                                foreach (var c in root.GetComponentsInChildren<Collider2D>(true)) Plugin.Log.LogInfo($"[AutoTest] 瑞维克 碰撞 {PathOf(c.transform)} 层 {LayerMask.LayerToName(c.gameObject.layer)} 标签 {c.tag} 触发 {c.isTrigger} 开 {c.enabled}");
                                if (i == 2) DumpFsms(root.transform);
                            }
                    Shot("RestingGrounds_08", $"revek_{i}");
                }
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "hornetenc")
            {
                // Hornet's encounter over the False Knight's room after Greenpath: what it waits for.
                PlayerData.instance.SetBool("hornet1Defeated", true);
                PlayerData.instance.SetBool("killedHornet", true);
                PlayerData.instance.isInvincible = true;
                TransitionPoint.lastEntered = "left1";
                yield return GoTo("Crossroads_10", "left1");
                for (int i = 0; i < 6; i++)
                {
                    yield return new WaitForSeconds(i == 0 ? 2f : 4f);
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (f.gameObject.name.Contains("Encounter"))
                        {
                            Plugin.Log.LogInfo($"[AutoTest] 遭遇 {i}：{f.gameObject.scene.name}/{f.gameObject.name}/{f.FsmName}:{f.ActiveStateName}（开 {f.gameObject.activeInHierarchy}），位置 {(Vector2)f.transform.position:F1}，主角 {(Vector2)HeroController.instance.transform.position:F1}，可操作 {GameHooks.PlayerHasControl()}");
                            if (i == 0 || i == 5) DumpFsm(f);
                        }
                    Shot("Crossroads_10", $"hornetenc_{i}");
                }
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "arenabk")
            {
                // The creative challenge of the Watcher Knights: their arena loaded, the knights there.
                yield return ArenaVisit("BlackKnight", "Ruins2_03", "BlackKnight", () => "");
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "gruzwake")
            {
                yield return GruzWakeTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "fxtop")
            {
                yield return FxTopTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "mousebind")
            {
                yield return MouseBindTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "water")
            {
                yield return WaterTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "fkbody")
            {
                // The False Knight fought with a Minishark aimed at his body the whole time, his head out or not.
                flowWeapon = Items.ItemType.Minishark;
                flowAimBody = true;
                yield return BossFlowTest(new[] { "FalseKnight" });
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "bossflow" || suite.StartsWith("bossflow_"))
            {
                yield return BossFlowTest(suite.Length > 9 ? suite.Substring(9).Split('+') : null);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "bossrun" || suite.StartsWith("bossrun_"))
            {
                yield return BossRunTest(suite.Length > 8 ? suite.Substring(8).Split('+') : null);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "quit1")
            {
                yield return QuitTest(1);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "ui")
            {
                yield return UiTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "items")
            {
                EquipAccessories();
                yield return ItemsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "fixes")
            {
                yield return FixesTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "features")
            {
                EquipAccessories();
                yield return FeaturesTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "ammo")
            {
                EquipAccessories();
                yield return AmmoTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "progression")
            {
                yield return ProgressionTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "tuning")
            {
                yield return TuningTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "summon")
            {
                yield return SummonTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "loadout")
            {
                yield return LoadoutTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "accvisual")
            {
                yield return AccessoriesTest(scene, hero, true);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "thrown3")
            {
                yield return Thrown3Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "guns")
            {
                yield return GunsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "rockets")
            {
                yield return RocketsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "torch" || suite == "door" || suite == "pylon" || suite == "fsmdump" || suite == "prompts" || suite == "balance" || suite == "wall" || suite == "onewall" || suite == "blockers" || suite == "blockfix" || suite == "route" || suite == "tunnel" || suite == "layout" || suite == "layoutmissing" || suite == "v10" || suite == "bossgates" || suite == "arena" || suite == "ammoslots" || suite == "worldmap" || suite == "showcase" || suite == "welldump" || suite == "well" || suite == "creative2" || suite == "holdpose" || suite == "pylonall" || suite == "bags" || suite.StartsWith("tour") || suite == "boss1" || suite == "boss2" || suite == "stations" || suite == "l10n" || suite == "promo" || suite == "promo2" || suite == "promo3" || suite == "promoA" || suite == "promoB" || suite == "promoC" || suite == "promoD" || suite == "promoE" || suite == "promoF" || suite == "pylonfade" || suite == "stag" || suite == "fk" || suite == "bell" || suite == "plat" || suite == "guide" || suite == "respawn" || suite == "respawnblock" || suite == "lightmap" || suite == "worldspawn" || suite == "hookjump" || suite == "terrarecoil" || suite == "v241" || suite == "dropscroll" || suite == "shopcheck" || suite == "dropvisible" || suite == "craftchest" || suite == "crystalmove" || suite == "v250" || suite.StartsWith("npcfloor") || suite.StartsWith("quakeall") || suite == "quakepick" || suite == "quakedump" || suite == "essence" || suite == "sealscan" || suite == "mapbuy" || suite == "shopsell" || suite == "spaenemies" || suite == "lemm" || suite == "hatchling" || suite == "quirrel" || suite == "sealroom" || suite == "clothenemies" || suite == "v250e" || suite == "v250f" || suite == "v250d" || suite == "v250c" || suite == "v250b" || suite == "hook" || suite == "v240fix" || suite == "quit" || suite == "suicide" || suite == "bossrecipes" || suite == "dreamnailfull" || suite == "dreamnail" || suite == "lag" || suite.StartsWith("lag_") || suite == "sentryscan" || suite == "dreamers" || suite == "dreamwalk" || suite == "getup" || suite == "dreamreturn" || suite == "dreamers1" || suite == "regionalfurniture" || suite == "furniture" || suite == "fkhead" || (suite == "drops2" || suite == "dropsgeo") || suite == "skystars" || suite == "skyscan" || suite == "sewer" || suite == "wormscan" || suite == "wormkill" || suite == "carvers" || suite == "chain" || suite == "flinx" || suite == "v270a" || suite == "v270b" || suite == "bosszones" || suite == "bookscan" || suite == "shotgun" || suite == "killscan" || suite == "manatest" || suite == "maplayers" || suite == "acid" || suite == "talkaway" || suite == "relics" || suite == "ghost" || suite == "liquidscan" || suite == "arenadoors" || (suite == "fishing" || suite == "fishingangler") || suite == "hksprites" || suite == "hkitems" || suite == "invnails" || suite == "reforge" || suite == "quicksell" || suite == "crafttele" || suite == "keys" || suite == "pets" || suite == "bossprops" || suite == "coinslot" || suite == "guardianwake" || suite == "food" || suite == "v281" || suite == "meteor" || suite == "forge" || suite == "gghkbag" || suite == "treasurebags" || suite == "torchfar" || suite == "townall" || suite == "artref" || suite.StartsWith("bossvis_") || suite == "ggkills" || suite.StartsWith("ggkills_") || suite == "v251" || suite == "v251diag" || suite == "hkflagdump" || suite == "invlag" || suite == "nailhits" || suite == "hkuse" || suite == "v301" || suite == "spikymenu" || suite == "prefixkeep" || suite == "stationaudit" || suite == "craftrule" || suite == "sourceaudit" || suite == "tombstone" || suite == "abilityprobe" || suite == "dashes" || suite == "furniture2" || suite == "geostack" || suite == "campfire")
            {
                if (suite == "fsmdump") yield return FsmDumpTest();
                else if (suite == "stag") yield return StagTest(hero);
                else if (suite == "fk") yield return FalseKnightGates();
                else if (suite == "bell") yield return BellTest();
                else if (suite == "plat") { yield return BellTest(); yield return PlatformDropTest(); }
                else if (suite == "guide") yield return GuideTest();
                else if (suite == "respawn") yield return RespawnTest();
                else if (suite == "respawnblock") yield return RespawnBlockTest();
                else if (suite == "lightmap") yield return LightMapTest();
                else if (suite == "hook") yield return HookTest(HeroController.instance);
                else if (suite == "worldspawn") yield return WorldSpawnTest();
                else if (suite == "hookjump") yield return HookJumpTest();
                else if (suite == "terrarecoil") yield return TerraRecoilTest();
                else if (suite == "v241") yield return V241Test();
                else if (suite == "dropscroll") yield return DropScrollTest();
                else if (suite == "shopcheck") yield return ShopCheckTest();
                else if (suite == "dropvisible") yield return DropVisibleTest();
                else if (suite == "craftchest") yield return CraftChestTest();
                else if (suite == "crystalmove") yield return CrystalMoveTest();
                else if (suite == "v250")
                {
                    yield return DropVisibleTest();
                    yield return CraftChestTest();
                    yield return CrystalMoveTest();
                    yield return V241Test();
                }
                else if (suite.StartsWith("npcfloor") && int.TryParse(suite.Substring(8, 1), out int npcPart)) yield return NpcFloorTest(npcPart, suite.Contains("@") ? suite.Substring(suite.IndexOf('@') + 1) : null);
                else if (suite.StartsWith("quakeall") && int.TryParse(suite.Substring(8, 1), out int quakePart)) yield return QuakeAllTest(quakePart, suite.Contains("@") ? suite.Substring(suite.IndexOf('@') + 1) : null);
                else if (suite == "quakedump") yield return QuakeDumpTest();
                else if (suite == "quakepick") yield return QuakePickTest();
                else if (suite == "essence") yield return EssenceTest();
                else if (suite == "sealscan") yield return SealScanTest();
                else if (suite == "mapbuy") yield return MapBuyTest();
                else if (suite == "shopsell") yield return ShopSellTest();
                else if (suite == "spaenemies") yield return SpaEnemiesTest();
                else if (suite == "lemm") yield return LemmTest();
                else if (suite == "hatchling") yield return HatchlingTest();
                else if (suite == "quirrel") yield return QuirrelTest();
                else if (suite == "sealroom") yield return SealRoomTest();
                else if (suite == "clothenemies") yield return ClothEnemiesTest();
                else if (suite == "lag" || suite.StartsWith("lag_")) yield return LagTest(suite.Length > 4 ? suite.Substring(4) : null);
                else if (suite == "sentryscan") yield return SentryScanTest();
                else if (suite == "dreamers") yield return DreamersTest();
                else if (suite == "dreamers1") yield return DreamersTest("Ruins2_Watcher_Room");
                else if (suite == "dreamwalk") yield return DreamersWalkInTest();
                else if (suite == "getup") yield return GetUpTest();
                else if (suite == "regionalfurniture") yield return RegionalFurnitureTest();
                else if (suite == "furniture") yield return FurnitureTest();
                else if (suite == "fkhead") yield return FalseKnightHeadTest();
                else if ((suite == "drops2" || suite == "dropsgeo")) yield return Drops2Test();
                else if (suite == "skystars" || suite == "skyscan") yield return SkyStarsTest(suite);
                else if (suite == "sewer") yield return SewerTest();
                else if (suite == "wormscan") yield return WormScanTest();
                else if (suite == "wormkill") yield return WormKillTest();
                else if (suite == "carvers") yield return CarversTest();
                else if (suite == "chain") yield return ChainTest();
                else if (suite == "flinx") yield return FlinxTest();
                else if (suite == "v270a") yield return V270aTest();
                else if (suite == "v270b") yield return V270bTest();
                else if (suite == "bosszones") yield return BossZonesTest();
                else if (suite == "bookscan") yield return BookScanTest();
                else if (suite == "shotgun") yield return ShotgunTest();
                else if (suite == "killscan") yield return KillScanTest();
                else if (suite == "manatest") yield return ManaTest();
                else if (suite == "maplayers") yield return MapLayersTest();
                else if (suite == "acid") yield return AcidTest();
                else if (suite == "talkaway") yield return TalkAwayTest();
                else if (suite == "relics") yield return RelicsTest();
                else if (suite == "ghost") yield return GhostTest();
                else if (suite == "liquidscan") yield return LiquidScanTest();
                else if (suite == "arenadoors") yield return ArenaDoorsScan();
                else if ((suite == "fishing" || suite == "fishingangler")) yield return FishingTest();
                else if (suite == "hksprites") yield return HkSpritesScan();
                else if (suite == "hkitems") yield return HkItemsTest();
                else if (suite == "hkflagdump") yield return HkFlagDump();
                else if (suite == "stationaudit") yield return StationAudit();
                else if (suite == "abilityprobe") yield return AbilityProbe();
                else if (suite == "sourceaudit") yield return SourceAudit();
                else if (suite == "geostack") yield return GeoStackTest();
                else if (suite == "campfire") yield return CampfireTest();
                else if (suite == "v301" || suite == "invlag" || suite == "nailhits" || suite == "hkuse" || suite == "spikymenu" || suite == "prefixkeep" || suite == "craftrule" || suite == "tombstone" || suite == "dashes" || suite == "furniture2") yield return V301Test(suite == "v301" ? null : suite);
                else if (suite == "invnails") yield return InvNailsTest();
                else if (suite == "reforge") yield return ReforgeTest();
                else if (suite == "quicksell") yield return QuickSellTest();
                else if (suite == "crafttele") yield return CraftTeleTest();
                else if (suite == "keys") yield return KeysTest();
                else if (suite == "pets") yield return PetsTest();
                else if (suite == "bossprops") yield return BossPropsTest();
                else if (suite == "coinslot") yield return CoinSlotTest();
                else if (suite == "guardianwake") yield return GuardianWakeTest();
                else if (suite == "food") yield return FoodTest();
                else if (suite == "v281") yield return V281Test();
                else if (suite == "meteor") yield return MeteorTest();
                else if (suite == "forge") yield return ForgeTest();
                else if (suite == "gghkbag") yield return GodhomeBagTest();
                else if (suite == "treasurebags") yield return TreasureBagsTest();
                else if (suite == "torchfar") yield return TorchFarTest();
                else if (suite == "townall") yield return TownAllTest();
                else if (suite == "artref") yield return ArtRefExport();
                else if (suite.StartsWith("bossvis_")) yield return BossVisTest(suite.Substring(8));
                else if (suite == "ggkills" || suite.StartsWith("ggkills_")) yield return GGKillsTest(suite.Length > 8 ? suite.Substring(8).Split('+') : null);
                else if (suite == "v251")
                {
                    yield return FurnitureTest();
                    yield return PlatformDropTest();
                    yield return CraftChestTest();
                    yield return RespawnTest();
                    yield return HatchlingTest();
                    yield return DreamersTest("Ruins2_Watcher_Room");
                }
                else if (suite == "dreamreturn") yield return DreamReturnTest();
                else if (suite == "v251diag")
                {
                    yield return DreamersTest();
                    yield return LagTest();
                    yield return SentryScanTest();
                }
                else if (suite == "v250f")
                {
                    yield return ClothEnemiesTest();
                    yield return HatchlingTest();
                    yield return QuirrelTest();
                }
                else if (suite == "v250e")
                {
                    yield return ClothEnemiesTest();
                    yield return QuirrelTest();
                    yield return HatchlingTest();
                    yield return EssenceTest();
                }
                else if (suite == "v250d")
                {
                    yield return QuirrelTest();
                    yield return SealRoomTest();
                    yield return HatchlingTest();
                    yield return ShopSellTest();
                    yield return EssenceTest();
                }
                else if (suite == "v250c")
                {
                    yield return QuirrelTest();
                    yield return ShopSellTest();
                    yield return HatchlingTest();
                    yield return EssenceTest();
                    yield return SealScanTest();
                }
                else if (suite == "v250b")
                {
                    yield return QuakeDumpTest();
                    yield return LemmTest();
                    yield return MapBuyTest();
                    yield return ShopSellTest();
                    yield return SpaEnemiesTest();
                    yield return EssenceTest();
                    yield return SealScanTest();
                }
                else if (suite == "v240fix")
                {
                    yield return WorldSpawnTest();
                    yield return HookJumpTest();
                    yield return TerraRecoilTest();
                    yield return RespawnBlockTest();
                    yield return LightMapTest();
                }
                else if (suite == "quit") yield return QuitTest();
                else if (suite == "suicide") yield return SuicideTest();
                else if (suite == "bossrecipes") yield return BossRecipesTest();
                else if (suite == "dreamnailfull") yield return DreamNailFullTest();
                else if (suite == "dreamnail") yield return DreamNailTest();
                else if (suite == "holdpose") yield return HoldPoseTest(hero);
                else if (suite == "pylonall") yield return PylonAllTest(hero);
                else if (suite == "bags") yield return BagsTest(hero);
                else if (suite.StartsWith("tour") && int.TryParse(suite.Substring(4), out int tourPart)) yield return TourTest(hero, tourPart);
                else if (suite == "boss1" || suite == "boss2") yield return BossTest(hero, suite == "boss1" ? 1 : 2);
                else if (suite == "stations") yield return StationsTest(hero);
                else if (suite == "l10n") yield return L10nTest(hero);
                else if (suite == "promo") yield return PromoTest(hero);
                else if (suite == "promo2") yield return Promo2Test(hero);
                else if (suite == "promo3") yield return Promo3Test(hero);
                else if (suite == "promoA") yield return PromoATest(hero);
                else if (suite == "promoB") yield return PromoBTest(hero);
                else if (suite == "promoC") yield return PromoCTest(hero);
                else if (suite == "promoD") yield return PromoDTest(hero);
                else if (suite == "promoE") yield return PromoETest(hero);
                else if (suite == "pylonfade") yield return PylonFadeTest(hero);
                else if (suite == "promoF") yield return PromoFTest(hero);
                else if (suite == "creative2") yield return Creative2Test(hero);
                else if (suite == "well") yield return WellTest(hero);
                else if (suite == "welldump") yield return WellDumpTest();
                else if (suite == "showcase") yield return ShowcaseTest(hero);
                else if (suite == "worldmap") yield return WorldMapTest(hero);
                else if (suite == "layout") yield return LayoutSurvey();
                else if (suite == "layoutmissing") yield return LayoutSurvey(missingOnly: true);
                else if (suite == "v10") yield return V10Test(hero);
                else if (suite == "bossgates") yield return BossGatesTest();
                else if (suite == "arena") yield return ArenaTest(hero);
                else if (suite == "ammoslots") yield return AmmoSlotsTest(hero);
                else if (suite == "tunnel") yield return TunnelTest(hero);
                else if (suite == "route") yield return RouteTest(hero);
                else if (suite == "blockers") yield return BlockerSurvey();
                else if (suite == "blockfix") yield return BlockerFixTest(hero);
                else if (suite == "wall") yield return WallTest(scene, hero);
                else if (suite == "onewall") yield return OneWayWallTest(hero);
                else if (suite == "balance") yield return BalanceTest(scene, hero);
                else if (suite == "prompts") yield return PromptsTest();
                else if (suite == "torch") yield return TorchTest(scene, hero);
                else if (suite == "pylon") yield return PylonTest(hero);
                else yield return DoorTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "creative")
            {
                yield return CreativeTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "r6")
            {
                yield return R6Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "slots")
            {
                yield return SlotsTest(scene);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "networldhost" || suite == "networldclient")
            {
                if (suite == "networldhost") yield return NetWorldHost(scene, hero);
                else yield return NetWorldClient(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成 [NET_DONE]");
                Finish();
                yield break;
            }
            if (suite.StartsWith("netboss_host") || suite == "netboss_client")
            {
                if (suite == "netboss_client") yield return NetBossClient(scene, hero);
                else yield return NetBossHost(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成 [NET_DONE]");
                Finish();
                yield break;
            }
            if (suite == "netaudit_host" || suite == "netaudit_client")
            {
                if (suite == "netaudit_host") yield return NetAuditHost(scene, hero);
                else yield return NetAuditClient(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成 [NET_DONE]");
                Finish();
                yield break;
            }
            if (suite == "netloothost" || suite == "netlootclient")
            {
                if (suite == "netloothost") yield return NetLootHost(scene, hero);
                else yield return NetLootClient(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成 [NET_DONE]");
                Finish();
                yield break;
            }
            if (suite == "net2host" || suite == "net2client")
            {
                if (suite == "net2host") yield return Net2Host(scene, hero);
                else yield return Net2Client(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成 [NET_DONE]");
                Finish();
                yield break;
            }
            if (suite == "netsummon")
            {
                yield return NetSummonTest();
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "net")
            {
                yield return NetTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "r7")
            {
                yield return R7Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "walls")
            {
                yield return WallsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "knock")
            {
                yield return KnockTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "book")
            {
                yield return BookTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "summon3")
            {
                yield return Summon3Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "magic5")
            {
                yield return Magic5Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "ammo2")
            {
                yield return Ammo2Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "tools")
            {
                yield return ToolsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2g")
            {
                yield return Melee2gTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2f")
            {
                yield return Melee2fTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2e")
            {
                yield return Melee2eTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2d")
            {
                yield return Melee2dTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2c")
            {
                yield return Melee2cTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2b")
            {
                yield return Melee2bTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "melee2")
            {
                yield return Melee2Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "yoyo")
            {
                yield return YoyoTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "armor2")
            {
                yield return Armor2Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "armor")
            {
                yield return ArmorTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "mobility")
            {
                yield return MobilityTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "wings")
            {
                yield return WingsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "acc2")
            {
                yield return Acc2Test(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "accessories")
            {
                yield return AccessoriesTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "weapons")
            {
                EquipAccessories();
                yield return WeaponsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "magic")
            {
                EquipAccessories();
                yield return MagicTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "ranged")
            {
                EquipAccessories();
                yield return RangedTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "swords")
            {
                EquipAccessories();
                yield return SwordsTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "platform")
            {
                EquipAccessories();
                yield return PlatformTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "visual")
            {
                EquipAccessories();
                yield return VisualTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "lift")
            {
                yield return CliffsLiftTest(hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "move")
            {
                EquipAccessories();
                yield return MoveFeelTest(hero);
                yield return PlatformAndRockTest(scene, hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }
            if (suite == "seamless")
            {
                EquipAccessories();
                yield return SmartCursorTest(hero);
                yield return ZenithTest(hero);
                yield return HookTest(hero);
                yield return MoveFeelTest(hero);
                yield return HandPlacedTest(scene);
                yield return ClawsTest(scene, hero);
                yield return SeamlessTest(scene, hero);
                yield return LiftTest(hero);
                yield return DeathTest(hero);
                yield return GreatDoorTest(hero);
                Plugin.Log.LogInfo("[AutoTest] 完成");
                Finish();
                yield break;
            }

            var startPos = hero.transform.position;
            yield return AccessoryFeelTest(scene, hero);
            hero.transform.position = startPos;
            yield return SmartCursorTest(hero);
            hero.transform.position = startPos;
            yield return ClawsTest(scene, hero);
            yield return GeoRockTest(scene, hero);
            hero.transform.position = startPos;
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);

            var grid = terrain.Grid;
            var col = hero.GetComponent<BoxCollider2D>();
            var heroCell = CellGrid.WorldToCell(hero.transform.position);
            int ground = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y - 0.05f)).y;
            Plugin.Log.LogInfo($"[AutoTest] 主角格 {heroCell}，脚下一格 y={ground} 是 {grid.Get(heroCell.x, ground)}");

            // A pit to the right, a dirt staircase to the left, a stone pillar and a wooden shelf overhead.
            int dug = 0;
            for (int x = heroCell.x + 4; x <= heroCell.x + 9; x++)
            for (int y = ground - 5; y <= ground; y++)
                if (terrain.CanDig(x, y)) { grid.Set(x, y, CellKind.Empty); dug++; }
            for (int step = 0; step < 5; step++)
            for (int y = ground + 1; y <= ground + 1 + step; y++)
                Place(new Vector2Int(heroCell.x - 3 - step, y), CellKind.Dirt);
            for (int y = ground + 1; y <= ground + 6; y++)
                Place(new Vector2Int(heroCell.x + 11, y), CellKind.Stone);
            // Blocks must touch another block, so the shelf hangs off a post, built left to right.
            int shelfY = ground + 8;
            for (int y = ground + 1; y < shelfY; y++)
                Place(new Vector2Int(heroCell.x - 4, y), CellKind.Wood);
            for (int x = heroCell.x - 4; x <= heroCell.x + 4; x++)
                Place(new Vector2Int(x, shelfY), CellKind.Wood);
            Plugin.Log.LogInfo($"[AutoTest] 挖掉 {dug} 格，放置完成");

            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 抠洞遮罩 {terrain.Holes.MaskCount} 个");
            Shot(scene, "2_edited");
            yield return null;
            yield return null;

            // Drop the hero onto the wooden shelf and see if it holds him.
            var rb = hero.GetComponent<Rigidbody2D>();
            float shelfTop = (shelfY + 1) * CellGrid.CellSize;
            hero.transform.position = new Vector3(CellGrid.CellCenter(heroCell.x, 0).x, shelfTop + 1.5f, Constants.HERO_Z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            float feet = col.bounds.min.y;
            Plugin.Log.LogInfo($"[AutoTest] 站在木架上：onGround={hero.cState.onGround} 脚底 y={feet:F3} 木架顶 y={shelfTop:F3} 差 {feet - shelfTop:F3}");
            Shot(scene, "3_on_shelf");

            // Mine the shelf out from under him with the real pickaxe logic.
            var pick = Items.ItemDb.Get(Items.ItemType.CopperPickaxe);
            int footCell = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, 0)).x;
            for (int x = footCell - 1; x <= footCell + 1; x++)
                for (int hit = 0; hit < 3; hit++)
                    mod.Mine(pick, new Vector2Int(x, shelfY));
            yield return new WaitForSeconds(1.2f);
            Plugin.Log.LogInfo($"[AutoTest] 挖穿木架后：onGround={hero.cState.onGround} 脚底 y={col.bounds.min.y:F3}（应落回地面 y≈{(ground + 1) * CellGrid.CellSize:F3}）");
            Shot(scene, "4_fell");

            // Into the pit: he must be visible inside the cut-open terrain and land on its floor.
            float pitFloor = (ground - 5) * CellGrid.CellSize;
            hero.transform.position = new Vector3(CellGrid.CellCenter(heroCell.x + 6, 0).x, pitFloor + 1.2f, Constants.HERO_Z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            Plugin.Log.LogInfo($"[AutoTest] 坑底：onGround={hero.cState.onGround} 脚底 y={col.bounds.min.y:F3} 坑底 y={pitFloor:F3}");
            Shot(scene, "5_in_pit");
            yield return null;

            // Held-item poses: early, middle and late in a swing, both directions.
            mod.TestSwing(Items.ItemType.CopperPickaxe, 1f);
            yield return new WaitForSeconds(0.05f);
            Shot(scene, "5b_swing_start");
            yield return new WaitForSeconds(0.16f);
            Shot(scene, "5c_swing_end");
            yield return new WaitForSeconds(0.5f);
            mod.TestSwing(Items.ItemType.CopperBroadsword, -1f);
            yield return new WaitForSeconds(0.17f);
            Shot(scene, "5d_swing_left");
            yield return new WaitForSeconds(0.5f);

            yield return InventoryTest(scene);
            yield return FrameRateTest();
            yield return PropsTest(scene, hero, rb);
            yield return CombatTest(scene, hero, rb);
            yield return HandPlacedTest(scene);
            yield return SeamlessTest(scene, hero);
            yield return DeathTest(hero);
            yield return GreatDoorTest(hero);

            // Save so the next run loads this slot and replays the edits.
            GameManager.instance.SaveGame();
            yield return new WaitForSeconds(1f);

            Plugin.Log.LogInfo("[AutoTest] 完成");
            Finish();
        }

        IEnumerator InventoryTest(string scene)
        {
            var inv = mod.Inventory;
            Items.InventoryGui.Open = true;
            yield return null;
            yield return null;
            Shot(scene, "8_inventory_closed_accessories");
            yield return null;

            EquipAccessories();
            yield return null;
            yield return null;
            Shot(scene, "9_inventory_equipped");
            Plugin.Log.LogInfo($"[AutoTest] 饰品生效：克苏鲁之盾冲刺={Player.TerraMovement.ShieldDash} 二段跳={Player.PlayerHooks.Abilities.DoubleJump} " +
                               $"蹬墙={Player.PlayerHooks.Abilities.WallCling} 最高跑速={Player.TerraMovement.AccRunSpeed:F2} px/tick");
            var pd = PlayerData.instance;
            Plugin.Log.LogInfo($"[AutoTest] 存档里的原版能力没被改：canDash={pd.canDash} hasDoubleJump={pd.hasDoubleJump} hasWalljump={pd.hasWalljump}");
            Items.InventoryGui.Open = false;
            yield return null;
        }

        /// <summary>Moves the four movement accessories from the second row into the accessory slots (once).</summary>
        void EquipAccessories()
        {
            // The movement accessories straight into the accessory slots (a new character starts
            // with only its tools; everything else comes from the crafting column).
            var inv = mod.Inventory;
            var kit = new[] { Items.ItemType.HermesBoots, Items.ItemType.CloudinaBottle, Items.ItemType.ClimbingClaws, Items.ItemType.ShieldOfCthulhu };
            for (int i = 0; i < kit.Length; i++)
                if (inv.Accessories[i].IsEmpty) inv.Accessories[i] = Items.ItemStack.Of(kit[i]);
            if (!inv.Owns(Items.ItemType.GrapplingHook)) inv.Add(Items.ItemType.GrapplingHook);
        }

        /// <summary>
        /// Hermes Boots sprint, Shield of Cthulhu dash and Cloud in a Bottle jump, against Terraria's
        /// numbers: speeds are logged in px/tick (1 px/tick = 1.875 u/s).
        /// </summary>
        IEnumerator AccessoryFeelTest(string scene, HeroController hero)
        {
            EquipAccessories();
            var rb = hero.GetComponent<Rigidbody2D>();
            var start = hero.transform.position;
            const float PxT = Player.TerraMovement.PxT;
            var particles = Effects.TerraParticles.Instance;
            yield return new WaitForSeconds(0.3f);

            // Sprint: hold right for four seconds on the flat start ledge, stepping back now and
            // then (velocity kept) so the run never reaches a wall.
            Player.TerraMovement.DevMove = 1;
            float t0 = Time.time, nextLog = 0f;
            bool shot = false;
            var speeds = new System.Text.StringBuilder();
            while (Time.time - t0 < 4.2f)
            {
                if (hero.transform.position.x > start.x + 3f)
                    hero.transform.position = new Vector3(hero.transform.position.x - 6f, start.y + 0.05f, start.z);
                float t = Time.time - t0;
                if (t >= nextLog)
                {
                    speeds.Append($" {t:F1}s={rb.linearVelocity.x / PxT:F2}");
                    nextLog += 0.5f;
                }
                if (!shot && t > 3.6f)
                {
                    Shot(scene, "11_hermes_sprint");
                    shot = true;
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Plugin.Log.LogInfo($"[AutoTest] 赫尔墨斯靴冲刺速度(px/tick，原版 3 后慢慢升到 6):{speeds}");
            yield return new WaitForSeconds(1.2f);

            // Shield dash from standing: 14.5 px/tick, bleeding off to running speed.
            hero.transform.position = start + Vector3.left * 3f;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            Player.TerraMovement.RequestDash(1);
            t0 = Time.time;
            var dash = new System.Text.StringBuilder();
            shot = false;
            while (Time.time - t0 < 0.7f)
            {
                dash.Append($" {Time.time - t0:F2}s={rb.linearVelocity.x / PxT:F1}/影{Player.TerraMovement.ShadowCount}");
                if (!shot && Time.time - t0 > 0.07f)
                {
                    Shot(scene, "12_shield_dash");
                    shot = true;
                }
                yield return new WaitForSeconds(0.05f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 克苏鲁之盾冲刺(px/tick，原版 14.5 起):{dash} 冷却 dashDelay={Player.TerraMovement.DashDelay:F0}");
            yield return new WaitForSeconds(0.8f);

            // Cloud jump: from mid-air, holding jump.
            hero.transform.position = start + Vector3.up * 2.5f;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.25f);
            int dustBefore = CountDust(particles);
            Player.TerraMovement.DevJump = true;
            HarmonyLib.AccessTools.Method(typeof(HeroController), "DoDoubleJump").Invoke(hero, null);
            t0 = Time.time;
            var rise = new System.Text.StringBuilder();
            shot = false;
            while (Time.time - t0 < 0.45f)
            {
                rise.Append($" {Time.time - t0:F2}s={rb.linearVelocity.y / PxT:F1}");
                if (!shot && Time.time - t0 > 0.1f)
                {
                    Shot(scene, "13_cloud_jump");
                    Plugin.Log.LogInfo($"[AutoTest] 二段跳截图时：invulnerable={hero.cState.invulnerable} 生命={mod.Stats.Life} 残影={Player.TerraMovement.ShadowCount}");
                    shot = true;
                }
                yield return new WaitForSeconds(0.03f);
            }
            Player.TerraMovement.DevJump = false;
            Plugin.Log.LogInfo($"[AutoTest] 云朵瓶二段跳上升速度(px/tick，原版 5.01 持续 11 tick):{rise}，新增尘土 {CountDust(particles) - dustBefore}");
            yield return new WaitForSeconds(1f);
            hero.transform.position = start;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
        }

        static int CountDust(Effects.TerraParticles p)
        {
            if (p == null) return 0;
            int n = 0;
            foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>()) if (sr.enabled) n++;
            return n;
        }

        /// <summary>Geo rocks: what their FSM listens for, and whether pickaxe strikes knock geo out.</summary>
        IEnumerator GeoRockTest(string scene, HeroController hero)
        {
            var rocks = Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None);
            Plugin.Log.LogInfo($"[AutoTest] 场景里有 {rocks.Length} 块吉欧石");
            if (rocks.Length == 0) yield break;
            GeoRock rock = null;
            float best = float.MaxValue;
            foreach (var r in rocks)
            {
                float d = Vector2.Distance(r.transform.position, hero.transform.position);
                if (r.gameObject.activeInHierarchy && d < best) { best = d; rock = r; }
            }
            if (rock == null) yield break;
            var fsm = rock.GetComponent<PlayMakerFSM>();
            var col = rock.GetComponentInChildren<Collider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 吉欧石 '{rock.name}' 位置 {rock.transform.position} layer={rock.gameObject.layer} tag={rock.tag} " +
                               $"碰撞体 {(col != null ? col.name + " layer=" + col.gameObject.layer + " trigger=" + col.isTrigger : "无")}");
            foreach (var f in rock.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                Plugin.Log.LogInfo($"    FSM '{f.FsmName}' 当前状态 {f.ActiveStateName}");
                foreach (var g in f.FsmGlobalTransitions) Plugin.Log.LogInfo($"      全局 {g.EventName} -> {g.ToState}");
                foreach (var s in f.FsmStates)
                {
                    var actions = new System.Text.StringBuilder();
                    foreach (var a in s.Actions) actions.Append(a.GetType().Name).Append(' ');
                    var transitions = new System.Text.StringBuilder();
                    foreach (var tr in s.Transitions) transitions.Append($"{tr.EventName}->{tr.ToState} ");
                    Plugin.Log.LogInfo($"      状态 {s.Name}：动作 {actions}| 跳转 {transitions}");
                }
            }
            if (col == null || fsm == null) yield break;

            var rb = hero.GetComponent<Rigidbody2D>();
            var rp = col.bounds.center;
            hero.transform.position = Clear(hero, new Vector2(rp.x - 1.5f, rp.y + 0.5f));
            rb.linearVelocity = Vector2.zero;
            float gravity = rb.gravityScale;
            rb.gravityScale = 0f;
            yield return new WaitForSeconds(0.5f);
            int geoBefore = PlayerData.instance.geo;
            for (int i = 0; i < 6; i++)
            {
                int hits = fsm.FsmVariables.GetFsmInt("Hits").Value;
                mod.TestStrike(col, 1f);
                yield return new WaitForSeconds(0.45f);
                if (i == 0) Shot(scene, "14_georock_strike");
                Plugin.Log.LogInfo($"[AutoTest] 镐子敲吉欧石第 {i + 1} 下：Hits {hits} -> {fsm.FsmVariables.GetFsmInt("Hits").Value}，状态 {fsm.ActiveStateName}");
            }
            yield return new WaitForSeconds(1.5f); // geo falls and is picked up
            Plugin.Log.LogInfo($"[AutoTest] 吉欧 {geoBefore} -> {PlayerData.instance.geo}");
            Shot(scene, "15_georock_after");
            rb.gravityScale = gravity;
            yield return null;
        }

        /// <summary>
        /// The live world: the neighbour behind the right gate is loaded and running before the
        /// player gets there; walking over makes it the current scene without loading or stopping;
        /// dug cells come along; walking back works. Then down Dirtmouth's well into the
        /// Crossroads, whose enemies must already be moving while it is still only a neighbour.
        /// </summary>
        IEnumerator SeamlessTest(string scene, HeroController hero)
        {
            var world = LiveWorld.Instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            float wait = Time.time + 30f;
            while (!world.TryGetNeighbour(GlobalEnums.GatePosition.right, out _, out _, out _, out _) && Time.time < wait) yield return null;
            if (!world.TryGetNeighbour(GlobalEnums.GatePosition.right, out var next, out var side, out var gate, out var offset))
            {
                Plugin.Log.LogInfo("[AutoTest] 右边没有接好的相邻场景，跳过无缝测试");
                yield break;
            }
            int dir = 1;
            Plugin.Log.LogInfo($"[AutoTest] 无缝测试：{scene} -> {next}（{side}），{next} 偏移 {offset}，门 {gate}，已载入 {world.LoadedCount} 个场景");
            LogRunning(next);

            yield return StandInsideGate(hero, gate, dir);
            Shot(scene, "16_edge_view");

            // Dig into the neighbour's ceiling just past the gate line (the floor stays walkable).
            var dugCells = new System.Collections.Generic.List<Vector2Int>();
            int seamCell = Mathf.RoundToInt(gate.x / CellGrid.CellSize);
            int floorY = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.min + Vector3.up * 0.1f).y;
            for (int k = 0; k < 4; k++)
            {
                int x = seamCell + k, top = floorY;
                while (top < floorY + 30 && !TerraWorld.IsSolid(x, top)) top++;
                for (int h = 0; h < 2; h++)
                    if (TerraWorld.CanDig(x, top + h))
                    {
                        TerraWorld.Set(x, top + h, CellKind.Empty);
                        dugCells.Add(new Vector2Int(x, top + h));
                    }
            }
            Plugin.Log.LogInfo($"[AutoTest] 在 {next} 里挖了 {dugCells.Count} 格");
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "17_dug_neighbour");

            yield return Cross(hero, scene, dir, 0, "18");
            var arrived = TerrainSystem.Current;
            if (arrived != null && arrived.SceneName == next)
            {
                int kept = 0;
                foreach (var c in dugCells)
                {
                    int lx = c.x - offset.x, ly = c.y - offset.y;
                    if (arrived.Grid.Get(lx, ly) == CellKind.Empty && arrived.Grid.IsDug(lx, ly)) kept++;
                }
                Plugin.Log.LogInfo($"[AutoTest] 挖进 {next} 的 {dugCells.Count} 格，进来后还在的 {kept} 格");
            }
            yield return new WaitForSeconds(1.5f);
            Shot(GameManager.instance.sceneName, "20_arrived_settled");

            // And back again, and on past the line: nothing may stop the hero there.
            yield return Cross(hero, GameManager.instance.sceneName, -dir, 0, "21");
            yield return WalkOn(hero, -dir, 1.2f, "往回越线后继续走");
            yield return new WaitForSeconds(1f);

            // Over to Dirtmouth again, then down the well.
            yield return Cross(hero, GameManager.instance.sceneName, dir, 0, "22");
            wait = Time.time + 30f;
            while (!world.TryGetNeighbour(GlobalEnums.GatePosition.bottom, out _, out _, out _, out _) && Time.time < wait) yield return null;
            if (!world.TryGetNeighbour(GlobalEnums.GatePosition.bottom, out var below, out _, out var well, out _))
            {
                Plugin.Log.LogInfo($"[AutoTest] {GameManager.instance.sceneName} 下面没有接好的场景，跳过竖直测试");
                yield break;
            }
            Plugin.Log.LogInfo($"[AutoTest] 竖直测试：{GameManager.instance.sceneName} -> {below}，井口 {well}");
            yield return LogEnemiesMoving(below, 1.5f);
            // The drop would cost Terraria fall damage (some 100 life); this test is about the crossing.
            mod.Stats.FullHeal();
            mod.DevNoFallDamage = true;
            hero.transform.position = new Vector3(well.x, well.y + 5f, Constants.HERO_Z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.1f);
            Shot(GameManager.instance.sceneName, "23_above_well");
            var wellCell = CellGrid.WorldToCell(well);
            LogCellMap(wellCell.x - 12, wellCell.x + 12, wellCell.y - 6, wellCell.y + 24, int.MinValue);
            yield return Cross(hero, GameManager.instance.sceneName, 0, -1, "24");
            yield return new WaitForSeconds(1.5f);
            mod.DevNoFallDamage = false;
            Shot(GameManager.instance.sceneName, "25_below_settled");
            yield return LogEnemiesMoving(GameManager.instance.sceneName, 1f);

            // Out of the left side too, and on past the line.
            wait = Time.time + 30f;
            while (!world.TryGetNeighbour(GlobalEnums.GatePosition.left, out _, out _, out _, out _) && Time.time < wait) yield return null;
            if (world.TryGetNeighbour(GlobalEnums.GatePosition.left, out var leftScene, out _, out var leftGate, out _))
            {
                Plugin.Log.LogInfo($"[AutoTest] 往左：{GameManager.instance.sceneName} -> {leftScene}，门 {leftGate}");
                // The drop down the well left a sliver of life; a Crossroads enemy shouldn't end the test.
                mod.Stats.FullHeal();
                yield return StandInsideGate(hero, leftGate, -1);
                yield return Cross(hero, GameManager.instance.sceneName, -1, 0, "26");
                yield return WalkOn(hero, -1, 1.2f, "往左越线后继续走");
            }
        }

        /// <summary>
        /// Keeps walking one way and reports how far the hero got; when something stops him,
        /// what he is touching (collider, its scene and layer) and the cells around him.
        /// </summary>
        IEnumerator WalkOn(HeroController hero, int dir, float seconds, string label)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            float x0 = hero.transform.position.x, minSpeed = float.MaxValue;
            var trace = new System.Text.StringBuilder();
            Player.TerraMovement.DevMove = dir;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t > 0.3f) minSpeed = Mathf.Min(minSpeed, Mathf.Abs(rb.linearVelocity.x));
                if (trace.Length < 300) trace.Append($" {hero.transform.position.x:F2}");
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            float moved = (hero.transform.position.x - x0) * dir;
            Plugin.Log.LogInfo($"[AutoTest] {label}：{seconds}s 走了 {moved:F2}（跑满应 ≈{seconds * 5.6f:F1}），最慢 {minSpeed / Player.TerraMovement.PxT:F2} px/tick，" +
                               $"场景 {GameManager.instance.sceneName}，x:{trace}");
            if (moved > seconds * 3f) yield break;
            var contacts = new System.Collections.Generic.List<ContactPoint2D>();
            rb.GetContacts(contacts);
            var sb = new System.Text.StringBuilder();
            foreach (var c in contacts)
            {
                var col = c.collider;
                sb.Append($"\n    {PathOf(col.transform)}（{col.GetType().Name}，场景 {col.gameObject.scene.name}，层 {col.gameObject.layer}，" +
                          $"触发器 {col.isTrigger}，范围 {col.bounds.min:F2}..{col.bounds.max:F2}，法线 {c.normal:F2}）");
            }
            Plugin.Log.LogInfo($"[AutoTest] {label}：被挡住，主角 {hero.transform.position:F2}，碰到 {contacts.Count} 个：{sb}");
            var cell = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            LogCellMap(cell.x - 12, cell.x + 12, cell.y - 4, cell.y + 6, int.MinValue);
            var seen = new System.Collections.Generic.HashSet<Transform>();
            foreach (var c in contacts)
            {
                // The FSMs of whatever owns the blocking collider: on it or the nearest parent that has any.
                var t = c.collider.transform;
                if (t.GetComponentInParent<TerraOwned>() != null || !seen.Add(t)) continue;
                for (var u = t; u != null; u = u.parent) DumpComponents(u);
                while (t != null && t.GetComponentsInChildren<PlayMakerFSM>(true).Length == 0) t = t.parent;
                if (t != null) DumpFsms(t);
            }
        }

        /// <summary>
        /// From Dirtmouth back into King's Pass in a new game: the great door is still shut there
        /// (normally it's broken on the way out), so it stops the hero until it is broken.
        /// </summary>
        IEnumerator GreatDoorTest(HeroController hero)
        {
            var world = LiveWorld.Instance;
            float wait = Time.time + 30f;
            while (!world.TryGetNeighbour(GlobalEnums.GatePosition.left, out _, out _, out _, out _) && Time.time < wait) yield return null;
            if (GameManager.instance.sceneName != "Town" || !world.TryGetNeighbour(GlobalEnums.GatePosition.left, out var left, out _, out var gate, out _) || left != "Tutorial_01")
            {
                Plugin.Log.LogInfo($"[AutoTest] 不在德特茅斯（{GameManager.instance.sceneName}），跳过大门测试");
                yield break;
            }
            yield return StandInsideGate(hero, gate, -1);
            yield return Cross(hero, "Town", -1, 0, "28");
            yield return WalkOn(hero, -1, 1.2f, "从德特茅斯进国王山道后继续走");
            yield return BreakGreatDoor(hero, -1);
        }

        /// <summary>
        /// King's Pass's great door, still shut in a new game: Terraria weapons must break it like
        /// the nail does (three hits). The game then moves on to Dirtmouth its own way. Only looks;
        /// leaves the door alone when it's already open.
        /// </summary>
        IEnumerator BreakGreatDoor(HeroController hero, int dir)
        {
            PlayMakerFSM door = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Great Door" && f.gameObject.activeInHierarchy) door = f;
            var col = door != null ? door.GetComponent<Collider2D>() : null;
            if (col == null || !col.enabled)
            {
                Plugin.Log.LogInfo("[AutoTest] 国王山道的大门已经开了（或不在这里）");
                yield break;
            }
            var b = col.bounds;
            Shot(GameManager.instance.sceneName, "21b_great_door");
            DumpFsms(door.transform);
            // The door's FSM breaks it on the 13th hit (cracks at 4 and 8), as with the nail.
            for (int i = 0; i < 15 && GameManager.instance.sceneName == "Tutorial_01" && door != null && col.enabled && door.ActiveStateName != "Move"; i++)
            {
                var rb = hero.GetComponent<Rigidbody2D>();
                hero.transform.position = new Vector3(b.center.x - dir * 1.2f, b.min.y + 0.8f, Constants.HERO_Z);
                rb.linearVelocity = Vector2.zero;
                mod.TestSwing(Items.ItemType.CopperBroadsword, dir);
                yield return new WaitForSeconds(0.6f);
                // Once it breaks the game moves on to Dirtmouth, and King's Pass (the door with it) goes.
                if (door == null) break;
                Plugin.Log.LogInfo($"[AutoTest] 砍大门第 {i + 1} 下：状态 {door.ActiveStateName}，Hits={door.FsmVariables.GetFsmInt("Hits").Value}，碰撞体启用 {col.enabled}");
            }
            float wait = Time.time + 15f;
            while (GameManager.instance.sceneName == "Tutorial_01" && Time.time < wait) yield return null;
            while ((GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < wait + 20f) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 砍开大门后：现在在 {GameManager.instance.sceneName}，主角 {hero.transform.position:F1}");
            yield return new WaitForSeconds(1f);
        }

        /// <summary>An object's components, and the simple public settings of its scripts.</summary>
        static void DumpComponents(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var comp in t.GetComponents<Component>())
            {
                if (comp == null) continue;
                sb.Append("\n      ").Append(comp.GetType().Name);
                if (comp is Behaviour be) sb.Append(be.enabled ? "" : "(关)");
                if (!(comp is MonoBehaviour)) continue;
                foreach (var field in comp.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    var ft = field.FieldType;
                    if (!(ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || typeof(Object).IsAssignableFrom(ft))) continue;
                    object v = field.GetValue(comp);
                    string text = v is Object o ? (o != null ? o.name : "null") : v?.ToString();
                    sb.Append($" {field.Name}={text}");
                }
            }
            Plugin.Log.LogInfo($"    物体 {PathOf(t)}（启用 {t.gameObject.activeSelf}，位置 {t.position:F2}）：{sb}");
        }

        /// <summary>Every FSM under an object: state, states with their actions (and the actions' settings), transitions.</summary>
        static void DumpFsms(Transform root)
        {
            foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true)) DumpFsm(f);
        }

        static void DumpFsm(PlayMakerFSM f)
        {
            {
                Plugin.Log.LogInfo($"    FSM {PathOf(f.transform)} '{f.FsmName}' 启用 {f.enabled}/{f.gameObject.activeInHierarchy} 当前状态 {f.ActiveStateName}");
                foreach (var g in f.FsmGlobalTransitions) Plugin.Log.LogInfo($"      全局 {g.EventName} -> {g.ToState}");
                foreach (var s in f.FsmStates)
                {
                    var actions = new System.Text.StringBuilder();
                    foreach (var a in s.Actions)
                    {
                        actions.Append(a.GetType().Name).Append('(');
                        foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            object v = field.GetValue(a);
                            if (v is System.Array arr && !(v is string))
                            {
                                var items = new System.Collections.Generic.List<string>();
                                foreach (var e in arr)
                                    items.Add(e is HutongGames.PlayMaker.NamedVariable env ? (env.UsesVariable ? "$" + env.Name : env.ToString())
                                        : e is HutongGames.PlayMaker.FsmEvent eev ? eev.Name : e?.ToString());
                                v = "[" + string.Join(",", items) + "]";
                            }
                            string text;
                            // A setting naming an object that is gone (destroyed with its scene) reads as such.
                            try
                            {
                                text = v is HutongGames.PlayMaker.NamedVariable nv ? (nv.UsesVariable ? "$" + nv.Name : nv.ToString())
                                    : v is HutongGames.PlayMaker.FsmEvent ev ? "ev:" + ev.Name
                                    : v is string str ? str
                                    : v is HutongGames.PlayMaker.FsmOwnerDefault od ? (od.OwnerOption == HutongGames.PlayMaker.OwnerDefaultOption.UseOwner ? "owner" : od.GameObject?.Value?.name)
                                    : null;
                            }
                            catch (System.Exception) { text = "（没了）"; }
                            if (!string.IsNullOrEmpty(text)) actions.Append(field.Name).Append('=').Append(text).Append(' ');
                        }
                        actions.Append(") ");
                    }
                    var transitions = new System.Text.StringBuilder();
                    foreach (var tr in s.Transitions) transitions.Append($"{tr.EventName}->{tr.ToState} ");
                    Plugin.Log.LogInfo($"      状态 {s.Name}：{actions}| 跳转 {transitions}");
                }
            }
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            for (var u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
            return p;
        }

        /// <summary>
        /// Dying in a scene entered by crossing a line: the game respawns the player its own way,
        /// the shade stays in that scene's own coordinates, and standing still at the respawn point
        /// while the neighbours load must not flip between scenes.
        /// </summary>
        IEnumerator DeathTest(HeroController hero)
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            var world = LiveWorld.Instance;
            string diedIn = gm.sceneName;

            // A hit from the left first: Terraria's knockback, immunity and blink, no freeze.
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.cState.invulnerable = false;
            mod.Stats.FullHeal();
            int lifeBefore = mod.Stats.Life;
            float t0 = Time.unscaledTime;
            hero.TakeDamage(null, CollisionSide.left, 1, 1);
            yield return new WaitForFixedUpdate();
            Vector2 kick = rb.linearVelocity / Player.TerraMovement.PxT;
            float timeScaleMin = Time.timeScale;
            var blink = new System.Text.StringBuilder();
            while (hero.cState.invulnerable && Time.unscaledTime - t0 < 3f)
            {
                timeScaleMin = Mathf.Min(timeScaleMin, Time.timeScale);
                if (blink.Length < 120) blink.Append(Player.TerraHurt.LastOpacity > 0f ? '#' : '.');
                if (Time.unscaledTime - t0 > 0.15f && Time.unscaledTime - t0 < 0.15f + Time.unscaledDeltaTime) Shot(gm.sceneName, "27a_hurt");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 受击（从左边）：生命 {lifeBefore} -> {mod.Stats.Life}，击退 ({kick.x:F2}, {kick.y:F2}) px/tick（原版 4.5, 3.5），" +
                               $"无敌 {Time.unscaledTime - t0:F2}s（原版 40 tick = 0.67s），最小 timeScale {timeScaleMin:F2}（不应冻结），能操作={GameHooks.PlayerHasControl()}，闪烁 {blink}");
            yield return new WaitForSeconds(0.5f);

            Plugin.Log.LogInfo($"[AutoTest] 死亡测试：在 {diedIn} 死，复活点 {pd.respawnScene}/{pd.respawnMarkerName}");
            hero.cState.invulnerable = false;
            hero.TakeDamage(null, CollisionSide.right, 99, 1);
            float wait = Time.realtimeSinceStartup + 40f;
            while (!hero.cState.dead && Time.realtimeSinceStartup < wait) yield return null;
            Vector2 diedAt = hero.transform.position;
            yield return new WaitForSeconds(0.35f);
            Shot(gm.sceneName, "27b_dying");
            yield return new WaitForSeconds(1.5f);
            Shot(gm.sceneName, "27c_slain_text");
            if (hero.cState.dead)
                for (float dy = 0f; dy <= 1.6f; dy += 0.8f) LogRenderersAt(diedAt + Vector2.up * dy, $"死亡处+{dy}", 2f);
            while ((hero.cState.dead || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSeconds(0.5f);
            int crosses = world.CrossCount;
            Vector3 p0 = hero.transform.position;
            yield return new WaitForSeconds(6f);
            Plugin.Log.LogInfo($"[AutoTest] 复活后在 {gm.sceneName} {p0:F1}，站着 6s 越线 {world.CrossCount - crosses} 次（应为 0），现在 {hero.transform.position:F1}，" +
                               $"已载入 {world.LoadedCount} 个场景，影子在 {pd.shadeScene} ({pd.shadePositionX:F1}, {pd.shadePositionY:F1})");
            Shot(gm.sceneName, "27_respawned");
        }

        /// <summary>Stand a few units inside a side gate, on open floor level with its opening.</summary>
        IEnumerator StandInsideGate(HeroController hero, Vector2 gate, int dir)
        {
            int seamCell = Mathf.RoundToInt(gate.x / CellGrid.CellSize);
            int gateRow = CellGrid.WorldToCell(new Vector2(0f, gate.y)).y;
            int col = seamCell - dir * 5, floor = -1;
            for (int dy = 0; dy <= 12 && floor < 0; dy++)
                foreach (int row in new[] { gateRow - dy, gateRow + dy })
                    if (!TerraWorld.IsSolid(col, row) && !TerraWorld.IsSolid(col, row + 1) && !TerraWorld.IsSolid(col, row + 2) && TerraWorld.IsSolid(col, row - 1))
                    {
                        floor = row;
                        break;
                    }
            if (floor < 0)
            {
                floor = gateRow;
                while (floor > 0 && !TerraWorld.IsSolid(col, floor - 1)) floor--;
            }
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = new Vector3((col + 0.5f) * CellGrid.CellSize, floor * CellGrid.CellSize + 0.8f, Constants.HERO_Z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            Plugin.Log.LogInfo($"[AutoTest] 站在门内：格 ({col},{floor})，主角 {hero.transform.position} onGround={hero.cState.onGround}");
        }

        /// <summary>
        /// Walks (or falls) across a gate line and reports what the player would feel: how long it
        /// took, the longest frame on the way, whether the game ever went into a scene transition,
        /// and whether the camera and speed carried straight on.
        /// </summary>
        IEnumerator Cross(HeroController hero, string from, int dirX, int dirY, string shot)
        {
            var world = LiveWorld.Instance;
            var gm = GameManager.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var cam = GameCameras.instance.mainCamera.transform;
            Vector2 camBefore = cam.position - hero.transform.position;
            Player.TerraMovement.DevMove = dirX;
            float t0 = Time.time, worstFrame = 0f;
            bool transition = false;
            while (gm.sceneName == from && Time.time - t0 < 12f)
            {
                worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                transition |= gm.IsInSceneTransition;
                yield return null;
            }
            float after = Time.time;
            // A little further, watching the frames right after the switch too.
            while (Time.time - after < 0.4f)
            {
                worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                transition |= gm.IsInSceneTransition;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Vector2 camAfter = cam.position - hero.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 越界 {from} -> {gm.sceneName}：用时 {after - t0:F2}s，切换本身 {world.LastPromoteMs} ms，最长一帧 {worstFrame * 1000f:F0} ms，" +
                               $"原版切换={transition}，速度 ({rb.linearVelocity.x / Player.TerraMovement.PxT:F2}, {rb.linearVelocity.y / Player.TerraMovement.PxT:F2}) px/tick，" +
                               $"相机相对主角 {camBefore} -> {camAfter}，已载入 {world.LoadedCount} 个场景");
            Shot(gm.sceneName, shot + "_crossed");
        }

        /// <summary>How much of a scene is actually running: started FSMs and enemies.</summary>
        static void LogRunning(string sceneName)
        {
            var s = LiveWorld.Instance.SceneOf(sceneName);
            if (!s.IsValid()) return;
            int fsms = 0, running = 0, enemies = 0;
            foreach (var go in s.GetRootGameObjects())
            {
                foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(false))
                {
                    fsms++;
                    if (!string.IsNullOrEmpty(f.ActiveStateName)) running++;
                }
                foreach (var hm in go.GetComponentsInChildren<HealthManager>(false)) if (!hm.isDead) enemies++;
            }
            Plugin.Log.LogInfo($"[AutoTest] {sceneName} 正在运行：FSM {running}/{fsms} 个在跑，活着的敌人 {enemies} 个");
        }

        /// <summary>Do a scene's enemies move over a while? (They should, even before the player enters.)</summary>
        IEnumerator LogEnemiesMoving(string sceneName, float seconds)
        {
            var s = LiveWorld.Instance.SceneOf(sceneName);
            if (!s.IsValid()) s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (!s.IsValid()) yield break;
            var enemies = new System.Collections.Generic.List<(HealthManager hm, Vector3 pos)>();
            foreach (var go in s.GetRootGameObjects())
                foreach (var hm in go.GetComponentsInChildren<HealthManager>(false))
                    if (!hm.isDead) enemies.Add((hm, hm.transform.position));
            LogRunning(sceneName);
            yield return new WaitForSeconds(seconds);
            int moved = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var (hm, pos) in enemies)
            {
                if (hm == null) continue;
                float d = Vector3.Distance(hm.transform.position, pos);
                if (d > 0.05f) moved++;
                if (sb.Length < 400) sb.Append($" {hm.name}@{pos:F0}移{d:F1}");
            }
            Plugin.Log.LogInfo($"[AutoTest] {sceneName} 的敌人 {seconds}s 内动了的 {moved}/{enemies.Count} 个：{sb}");
        }

        /// <summary>
        /// Climbing Claws: falling fast into a wall slide must not launch the player upwards
        /// (the old slide easing overshot), and the slide holds Terraria's slow speed.
        /// </summary>
        IEnumerator ClawsTest(string scene, HeroController hero)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            // A tall stone wall right of the hero, up in the open air of the start area.
            var start = CellGrid.WorldToCell(col.bounds.center);
            int wallX = start.x + 2;
            for (int y = start.y - 2; y < start.y + 24; y++)
                if (TerraWorld.Get(wallX, y) == CellKind.Empty) TerraWorld.Set(wallX, y, CellKind.Stone);
            yield return new WaitForSeconds(0.2f);
            float wallLeft = wallX * CellGrid.CellSize;
            hero.transform.position = new Vector3(wallLeft - col.bounds.extents.x - col.offset.x - 0.01f, hero.transform.position.y + 8f, Constants.HERO_Z);
            hero.FaceRight();
            rb.linearVelocity = new Vector2(0f, -8f);
            yield return new WaitForFixedUpdate();
            // Wall slide on the right wall, holding towards it, falling fast.
            var touchingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("touchingWallR");
            var slidingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingR");
            touchingR(hero) = true;
            slidingR(hero) = true;
            hero.cState.wallSliding = true;
            hero.cState.touchingWall = true;
            Player.TerraMovement.DevMove = 1;
            float y0 = hero.transform.position.y, maxRise = 0f, maxVy = float.MinValue;
            var trace = new System.Text.StringBuilder();
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                maxRise = Mathf.Max(maxRise, hero.transform.position.y - y0);
                maxVy = Mathf.Max(maxVy, rb.linearVelocity.y);
                if (trace.Length < 300) trace.Append($" {rb.linearVelocity.y / Player.TerraMovement.PxT:F1}");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 攀爬爪滑墙：最多上升 {maxRise:F2}，最大上升速度 {maxVy / Player.TerraMovement.PxT:F2} px/tick，滑墙={hero.cState.wallSliding}，" +
                               $"速度(px/tick，原版滑墙 -0.5):{trace}");
            Shot(scene, "26_claws_slide");
            Player.TerraMovement.DevMove = 0;
            for (int y = start.y - 2; y < start.y + 24; y++)
                if (TerraWorld.Get(wallX, y) == CellKind.Stone) TerraWorld.Set(wallX, y, CellKind.Empty);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// Terraria's pickaxe smart cursor, aimed straight sideways at a stone wall: it should clear
        /// whole three-high columns (chest, feet, head in turn), a tunnel to walk along.
        /// </summary>
        IEnumerator SmartCursorTest(HeroController hero)
        {
            yield return new WaitForSeconds(0.5f);
            var col = hero.GetComponent<BoxCollider2D>();
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            var placed = new System.Collections.Generic.List<Vector2Int>();
            for (int x = feet.x + 2; x <= feet.x + 5; x++)
                for (int y = feet.y; y <= feet.y + 5; y++)
                    if (TerraWorld.Get(x, y) == CellKind.Empty) { TerraWorld.Set(x, y, CellKind.Stone); placed.Add(new Vector2Int(x, y)); }
            yield return new WaitForSeconds(0.2f);
            Vector2 mouse = (Vector2)col.bounds.center + Vector2.right * 2.6f;
            var order = new System.Text.StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                if (!mod.DevSmartTarget(mouse, out var c)) { order.Append(" 无"); break; }
                order.Append($" ({c.x - feet.x},{c.y - feet.y})");
                if (i == 0) Shot(GameManager.instance.sceneName, "26a_smart_cursor");
                TerraWorld.Set(c.x, c.y, CellKind.Empty);
            }
            Plugin.Log.LogInfo($"[AutoTest] 智能光标朝右水平挖（相对脚下格，原版每列按 胸(+1) 脚(0) 头(+2)）：{order}");
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// The grappling hook thrown at a stone ledge up and to the right: it should latch on its
        /// cell's centre and pull the player up against it at up to 11 px a tick.
        /// </summary>
        IEnumerator HookTest(HeroController hero)
        {
            var hook = Player.GrapplingHook.Instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            yield return new WaitForSeconds(0.3f);
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            var placed = new System.Collections.Generic.List<Vector2Int>();
            for (int x = feet.x + 4; x <= feet.x + 8; x++)
            {
                var c = new Vector2Int(x, feet.y + 10);
                if (TerraWorld.Get(c.x, c.y) == CellKind.Empty) { TerraWorld.Set(c.x, c.y, CellKind.Stone); placed.Add(c); }
            }
            yield return new WaitForSeconds(0.2f);
            var target = CellGrid.CellCenter(feet.x + 6, feet.y + 10);
            Vector2 start = col.bounds.center;
            hook.Throw(hero, target);
            float t0 = Time.time, latchedAt = -1f, maxSpeed = 0f;
            while (Time.time - t0 < 2.5f)
            {
                if (latchedAt < 0f && hook.DevState == "Latched") latchedAt = Time.time - t0;
                if (latchedAt >= 0f) maxSpeed = Mathf.Max(maxSpeed, rb.linearVelocity.magnitude / Player.TerraMovement.PxT);
                if (latchedAt >= 0f && Time.time - t0 > latchedAt + 0.25f && Time.time - t0 < latchedAt + 0.25f + Time.deltaTime) Shot(GameManager.instance.sceneName, "26d_hook");
                yield return null;
            }
            Vector2 end = col.bounds.center;
            Plugin.Log.LogInfo($"[AutoTest] 抓钩：{(latchedAt >= 0f ? $"{latchedAt:F2}s 钩住" : "没钩住")}，状态 {hook.DevState}，钩点 {hook.DevHookPosition:F2}（目标格中心 {target:F2}），" +
                               $"主角 {start:F2} -> {end:F2}，离钩点 {Vector2.Distance(end, hook.DevHookPosition):F2}，最快 {maxSpeed:F1} px/tick（原版 11）");
            hook.DevRelease(hero);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(1f);
        }

        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, bool> heroDoubleJumped = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("doubleJumped");
        static readonly HarmonyLib.AccessTools.FieldRef<HeroController, int> heroJumpedSteps = HarmonyLib.AccessTools.FieldRefAccess<HeroController, int>("jumped_steps");
        static readonly System.Func<HeroController, bool> heroCanDoubleJump =
            HarmonyLib.AccessTools.MethodDelegate<System.Func<HeroController, bool>>(HarmonyLib.AccessTools.Method(typeof(HeroController), "CanDoubleJump"));

        /// <summary>
        /// Terraria's movement details: letting go of jump keeps the speed gained, a grappling
        /// hook's pull carries on after jumping off and leaves the double jump, a wall slide goes
        /// into a gap in the wall.
        /// </summary>
        IEnumerator MoveFeelTest(HeroController hero)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            var hook = Player.GrapplingHook.Instance;
            float pxt = Player.TerraMovement.PxT;
            yield return new WaitForSeconds(0.5f);

            // 1. Jump let go of early: rising at the jump speed with the button up, one frame later.
            hero.cState.jumping = true;
            heroJumpedSteps(hero) = 10;
            rb.linearVelocity = new Vector2(0f, 5.01f * pxt);
            yield return new WaitForFixedUpdate();
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 松开跳跃：一帧后竖直速度 {rb.linearVelocity.y / pxt:F2} px/tick（原版只停止加速，约 4.6；空洞骑士会清零）");
            yield return new WaitForSeconds(1f);

            // 2. Hook up and to the right, jumping off halfway.
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            var placed = new System.Collections.Generic.List<Vector2Int>();
            for (int x = feet.x + 8; x <= feet.x + 12; x++)
            {
                var c = new Vector2Int(x, feet.y + 16);
                if (TerraWorld.Get(c.x, c.y) == CellKind.Empty) { TerraWorld.Set(c.x, c.y, CellKind.Stone); placed.Add(c); }
            }
            yield return new WaitForSeconds(0.2f);
            hook.Throw(hero, CellGrid.CellCenter(feet.x + 10, feet.y + 16));
            float t0 = Time.time;
            while (hook.DevState != "Latched" && Time.time - t0 < 1.5f) yield return null;
            while (rb.linearVelocity.magnitude < 9f * pxt && Time.time - t0 < 2.5f) yield return null;
            Vector2 atRelease = rb.linearVelocity / pxt;
            hook.DevJumpRelease(hero);
            var trace = new System.Text.StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.05f);
                var v = rb.linearVelocity / pxt;
                trace.Append($" ({v.x:F1},{v.y:F1})");
            }
            // The accessories' abilities are only swapped into the save data while the hero updates.
            var pd = PlayerData.instance;
            bool savedDouble = pd.hasDoubleJump;
            pd.hasDoubleJump = Player.PlayerHooks.Abilities.DoubleJump;
            bool canDouble = heroCanDoubleJump(hero);
            pd.hasDoubleJump = savedDouble;
            Plugin.Log.LogInfo($"[AutoTest] 抓钩半路起跳：松开时 ({atRelease.x:F1},{atRelease.y:F1}) px/tick，之后{trace}（应带着惯性慢慢减速），" +
                               $"已用二段跳={heroDoubleJumped(hero)}，带云朵瓶={Player.PlayerHooks.Abilities.DoubleJump}，能二段跳={canDouble}，抓钩 {hook.DevState}");
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(1.5f);

            // 3. Sliding down a wall past a gap in it, holding towards it.
            feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int wallX = feet.x + 2, gapY = feet.y + 8;
            placed.Clear();
            for (int x = wallX; x <= wallX + 3; x++)
                for (int y = feet.y; y < feet.y + 22; y++)
                {
                    bool gap = y >= gapY && y < gapY + 3 && x < wallX + 3;
                    if (!gap && TerraWorld.Get(x, y) == CellKind.Empty) { TerraWorld.Set(x, y, CellKind.Stone); placed.Add(new Vector2Int(x, y)); }
                }
            yield return new WaitForSeconds(0.3f);
            float wallLeft = wallX * CellGrid.CellSize;
            hero.transform.position = new Vector3(wallLeft - col.bounds.extents.x - col.offset.x - 0.01f, (feet.y + 16) * CellGrid.CellSize + 1f, Constants.HERO_Z);
            hero.FaceRight();
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            var touchingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("touchingWallR");
            var slidingR = HarmonyLib.AccessTools.FieldRefAccess<HeroController, bool>("wallSlidingR");
            touchingR(hero) = true;
            slidingR(hero) = true;
            hero.cState.wallSliding = true;
            hero.cState.touchingWall = true;
            Player.TerraMovement.DevMove = 1;
            t0 = Time.time;
            trace.Clear();
            float nextTrace = 0f;
            while (Time.time - t0 < 4f && col.bounds.min.x < wallLeft + 0.05f)
            {
                if (Time.time - t0 >= nextTrace && trace.Length < 1500)
                {
                    nextTrace += 0.15f;
                    trace.Append($"\n  {Time.time - t0:F2}s 脚y={col.bounds.min.y:F2} 滑墙={hero.cState.wallSliding} 贴墙={hero.cState.touchingWall} 地面={hero.cState.onGround} vy={rb.linearVelocity.y / pxt:F1} | {Player.TerraMovement.DevLastOpening}");
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Player.TerraMovement.DevMove = 0;
            Plugin.Log.LogInfo($"[AutoTest] 滑墙过程：{trace}");
            bool inside = col.bounds.min.x >= wallLeft - 0.05f && col.bounds.min.y >= gapY * CellGrid.CellSize - 0.1f && col.bounds.max.y <= (gapY + 3) * CellGrid.CellSize + 0.1f;
            Plugin.Log.LogInfo($"[AutoTest] 滑墙经过墙上的洞：进洞={inside}，主角脚 ({col.bounds.min.x:F2},{col.bounds.min.y:F2})，洞 x≥{wallLeft:F2} y {gapY * CellGrid.CellSize:F2}..{(gapY + 3) * CellGrid.CellSize:F2}，{Time.time - t0:F1}s");
            Shot(GameManager.instance.sceneName, "26e_wall_gap");
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// The Terraria items: shooting (bow, gun, staff with mana), a bomb blowing up blocks and
        /// King's Pass's breakable floor, falling sand, platforms, rope, torch light, armor,
        /// potions, the shop and rocket boots.
        /// </summary>
        IEnumerator ItemsTest(string scene, HeroController hero)
        {
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            var stats = mod.Stats;
            inv.Add(Items.ItemType.WoodenArrow, 100);
            inv.Add(Items.ItemType.MusketBall, 100);
            yield return new WaitForSeconds(0.5f);

            // --- Shooting at the nearest enemy. ---
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null) Plugin.Log.LogInfo("[AutoTest] 场景里没有敌人可打");
            else
            {
                var tc = target.GetComponent<Collider2D>();
                // A target held still in the open, five units in front of the player.
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                Vector2 hold = (Vector2)col.bounds.center + Vector2.right * 5f;
                var trb = target.GetComponent<Rigidbody2D>();
                foreach (var weapon in new[] { Items.ItemType.IronBow, Items.ItemType.FlintlockPistol, Items.ItemType.AmethystStaff, Items.ItemType.SpaceGun, Items.ItemType.WaterBolt,
                             Items.ItemType.MoltenFury, Items.ItemType.Minishark, Items.ItemType.PhoenixBlaster, Items.ItemType.WandofSparking, Items.ItemType.DiamondStaff,
                             Items.ItemType.DemonScythe, Items.ItemType.FlowerofFire, Items.ItemType.EnchantedBoomerang, Items.ItemType.TerraBlade, Items.ItemType.Grenade })
                {
                    stats.Mana = stats.ManaMax;
                    if (target == null || target.isDead)
                    {
                        // Killed: the next nearest enemy stands in.
                        target = null;
                        best = float.MaxValue;
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        {
                            if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                            float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                            if (d < best) { best = d; target = hm; }
                        }
                        if (target == null) break;
                        trb = target.GetComponent<Rigidbody2D>();
                    }
                    string tname = target.name;
                    // Kept alive through the whole test: plenty of health for every weapon to show its hit.
                    target.hp = 60;
                    int hp = target.hp, mana = stats.Mana, live0 = Items.TerraProjectiles.Instance.Count;
                    target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                    yield return null;
                    mod.DevUse(weapon, hold);
                    int shots = Items.TerraProjectiles.Instance.Count - live0;
                    for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                    {
                        if (target == null) break;
                        target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                        if (trb != null) trb.linearVelocity = Vector2.zero;
                        // How each kind is held out (Main.DrawPlayerItemPos offsets).
                        if (t < 0.1f && t + Time.deltaTime >= 0.1f && (weapon == Items.ItemType.IronBow || weapon == Items.ItemType.FlintlockPistol || weapon == Items.ItemType.Minishark ||
                                weapon == Items.ItemType.AmethystStaff || weapon == Items.ItemType.SpaceGun || weapon == Items.ItemType.WaterBolt))
                            Shot(scene, "30a_held_" + weapon);
                        yield return null;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] {Items.ItemDb.Get(weapon).Name} 射向 {tname}：发射 {shots} 颗，HK 血量 {hp} -> {(target != null && !target.isDead ? target.hp : 0)}，魔力 {mana} -> {stats.Mana}");
                }
            }

            // --- A bomb in a block of stone. ---
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int bx = feet.x + 6, by = feet.y + 1;
            var placed = new System.Collections.Generic.List<Vector2Int>();
            for (int x = bx - 5; x <= bx + 5; x++)
                for (int y = by; y <= by + 8; y++)
                    if (TerraWorld.Get(x, y) == CellKind.Empty) { TerraWorld.Set(x, y, CellKind.Stone); placed.Add(new Vector2Int(x, y)); }
            TerraWorld.Set(bx, by + 4, CellKind.Empty);
            yield return new WaitForSeconds(0.3f);
            int solidBefore = 0;
            foreach (var p in placed) if (TerraWorld.IsSolid(p.x, p.y)) solidBefore++;
            Items.TerraProjectiles.Instance.Shoot(Items.ProjKind.Bomb, CellGrid.CellCenter(bx, by + 4), Vector2.zero, 0, 0f, 0f);
            float lifeBefore = stats.Life;
            yield return new WaitForSeconds(3.4f);
            int solidAfter = 0;
            foreach (var p in placed) if (TerraWorld.IsSolid(p.x, p.y)) solidAfter++;
            Plugin.Log.LogInfo($"[AutoTest] 炸弹：石块 {solidBefore} -> {solidAfter}（半径 4 格的圆，应炸掉约 45 格），{Items.TerraProjectiles.DevLastBlast}，主角生命 {lifeBefore} -> {stats.Life}");
            Shot(scene, "30b_bomb");
            // The screenshot lands a frame or two later: keep the blocks until then.
            yield return new WaitForSeconds(0.15f);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(0.5f);

            // --- King's Pass's breakable floor, blown open. ---
            var breakFloor = GameObject.Find("Break Floor 1");
            var bf = breakFloor != null ? breakFloor.GetComponent<PlayMakerFSM>() : null;
            if (bf != null)
            {
                string before = bf.ActiveStateName;
                var bfc = breakFloor.GetComponentInChildren<Collider2D>();
                Vector2 at = bfc != null ? (Vector2)bfc.bounds.center + Vector2.up * 0.8f : (Vector2)breakFloor.transform.position + Vector2.up;
                hero.transform.position = Clear(hero, at + new Vector2(-6f, 1f));
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                Items.TerraProjectiles.Instance.Shoot(Items.ProjKind.Bomb, at, Vector2.zero, 0, 0f, 0f);
                yield return new WaitForSeconds(3.6f);
                Plugin.Log.LogInfo($"[AutoTest] 炸可破地板 'Break Floor 1'：状态 {before} -> {bf.ActiveStateName}，{Items.TerraProjectiles.DevLastBlast}");
                Shot(scene, "30c_break_floor");
            }

            // --- Falling sand. ---
            feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int sx = feet.x + 4, sy = feet.y + 6;
            placed.Clear();
            void Put(int x, int y, CellKind k) { if (TerraWorld.Get(x, y) == CellKind.Empty) { TerraWorld.Set(x, y, k); placed.Add(new Vector2Int(x, y)); } }
            Put(sx, sy, CellKind.Stone);
            Put(sx, sy + 1, CellKind.Sand);
            Put(sx, sy + 2, CellKind.Sand);
            yield return new WaitForSeconds(0.3f);
            int ground = sy;
            while (ground > feet.y - 10 && !TerraWorld.IsSolid(sx, ground - 1)) ground--;
            TerraWorld.Set(sx, sy, CellKind.Empty);
            yield return new WaitForSeconds(0.15f);
            int falling = World.FallingSand.Instance.Falling;
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 沙子：撤掉托着的石块后 {falling} 块在落，落地后 ({sx},{ground})={TerraWorld.Get(sx, ground)} ({sx},{ground + 1})={TerraWorld.Get(sx, ground + 1)}（地面在 {ground - 1}）");
            TerraWorld.Set(sx, ground, CellKind.Empty);
            TerraWorld.Set(sx, ground + 1, CellKind.Empty);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);

            // --- Platforms: stand on them, drop through with down. ---
            feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int py = feet.y + 6;
            placed.Clear();
            for (int x = feet.x - 4; x <= feet.x + 4; x++) Put(x, py, CellKind.Platform);
            yield return new WaitForSeconds(0.3f);
            hero.transform.position = new Vector3(hero.transform.position.x, (py + 1) * CellGrid.CellSize + 1.2f, Constants.HERO_Z);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            float standY = col.bounds.min.y;
            bool stood = hero.cState.onGround && Mathf.Abs(standY - (py + 1) * CellGrid.CellSize) < 0.1f;
            Shot(scene, "30d_platform");
            Player.TerraMovement.DevDown = true;
            yield return new WaitForSeconds(0.8f);
            Player.TerraMovement.DevDown = false;
            Plugin.Log.LogInfo($"[AutoTest] 平台：站上去={stood}（脚 {standY:F2}，平台顶 {(py + 1) * CellGrid.CellSize:F2}），按下后脚 {col.bounds.min.y:F2}（应掉到下面）");
            // From below: jump up through it.
            yield return new WaitForSeconds(0.6f);
            float belowY = col.bounds.min.y;
            rb.linearVelocity = new Vector2(0f, 9f * Player.TerraMovement.PxT);
            yield return new WaitForSeconds(0.9f);
            Plugin.Log.LogInfo($"[AutoTest] 平台：从下面往上跳，脚 {belowY:F2} -> {col.bounds.min.y:F2}，onGround={hero.cState.onGround}（应站到平台上 {(py + 1) * CellGrid.CellSize:F2}）");
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(0.8f);

            // --- Rope: climb up it. ---
            feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int rx = CellGrid.WorldToCell(col.bounds.center).x;
            placed.Clear();
            Put(rx, feet.y + 14, CellKind.Stone);
            for (int y = feet.y + 13; y >= feet.y; y--) Put(rx, y, CellKind.Rope);
            yield return new WaitForSeconds(0.3f);
            float y0 = col.bounds.min.y;
            Player.TerraMovement.DevUp = true;
            yield return new WaitForSeconds(1.2f);
            Player.TerraMovement.DevUp = false;
            float climbed = col.bounds.min.y - y0;
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 绳子：按上 1.2 秒爬了 {climbed:F2}（2.5 px/tick 应约 5.6），松手后 0.5 秒还在绳上={Player.TerraMovement.OnRope}，竖直速度 {rb.linearVelocity.y:F2}");
            LogVisibility(hero, CellGrid.CellCenter(rx, feet.y + 6));
            Shot(scene, "30e_rope");
            yield return new WaitForSeconds(0.15f);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            yield return new WaitForSeconds(1f);

            // --- Torch. ---
            feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            bool canTorch = TerraMod.CanPlace(new Vector2Int(feet.x + 2, feet.y), CellKind.Torch);
            if (canTorch) TerraWorld.Set(feet.x + 2, feet.y, CellKind.Torch);
            yield return new WaitForSeconds(0.3f);
            int glows = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) if (sr.name == "Terra Glow") glows++;
            Plugin.Log.LogInfo($"[AutoTest] 火把：能放={canTorch}，光晕 {glows} 个，材质加色={World.Glow.Additive}");

            // --- Armor. ---
            int defBefore = stats.Defense;
            inv.Armor[0] = Items.ItemStack.Of(Items.ItemType.IronHelmet);
            inv.Armor[1] = Items.ItemStack.Of(Items.ItemType.IronChainmail);
            inv.Armor[2] = Items.ItemStack.Of(Items.ItemType.IronGreaves);
            yield return null;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 铁套：防御 {defBefore} -> {stats.Defense}（饰品 + 2+3+2 + 套装 2），套装 {Items.Equipment.FullSet}");
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "30f_armor");
            yield return new WaitForSeconds(0.15f);
            inv.Armor[0] = Items.ItemStack.Of(Items.ItemType.MiningHelmet);
            inv.Armor[1] = Items.ItemStack.Of(Items.ItemType.GoldChainmail);
            inv.Armor[2] = Items.ItemStack.Of(Items.ItemType.MoltenGreaves);
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 挖矿头盔：灯笼={PlayerData.instance.GetBool("hasLantern")}，防御 {stats.Defense}");
            Shot(scene, "30g_helmet");

            // --- Potion. ---
            stats.TakeDirect(60);
            int lifeHurt = stats.Life;
            stats.PotionSickness = 0f;
            mod.DevUse(Items.ItemType.LesserHealingPotion, hero.transform.position);
            int afterOne = stats.Life;
            mod.DevUse(Items.ItemType.LesserHealingPotion, hero.transform.position);
            Plugin.Log.LogInfo($"[AutoTest] 弱效治疗药水：{lifeHurt} -> {afterOne}（+50），药水病 {stats.PotionSickness:F0}s，再喝一瓶 -> {stats.Life}（应不变）");

            // --- Crafting column. ---
            var savedMouse = inv.Mouse;
            inv.Mouse = default;
            bool crafted = Items.InventoryGui.DevCraft(Items.ItemType.Dynamite);
            bool again = Items.InventoryGui.DevCraft(Items.ItemType.Dynamite);
            Plugin.Log.LogInfo($"[AutoTest] 制作栏：{Items.InventoryGui.RecipeCount} 种，拿雷管 {crafted}/{again}，光标上 {inv.Mouse.Def.Name}×{inv.Mouse.Count}，解锁阶段 {Items.Shop.UnlockedStage()}");
            inv.Mouse = savedMouse;

            // --- Rocket boots. ---
            var savedAcc = inv.Accessories[4];
            inv.Accessories[4] = Items.ItemStack.Of(Items.ItemType.RocketBoots);
            yield return null;
            rb.linearVelocity = new Vector2(0f, 5f * Player.TerraMovement.PxT);
            yield return new WaitForSeconds(0.05f);
            float start = col.bounds.min.y, top = start;
            Player.TerraMovement.DevJump = true;
            float t0 = Time.time;
            while (Time.time - t0 < 2f)
            {
                top = Mathf.Max(top, col.bounds.min.y);
                yield return null;
            }
            Player.TerraMovement.DevJump = false;
            Plugin.Log.LogInfo($"[AutoTest] 火箭靴：按住跳 2 秒，最高升了 {top - start:F2}（不带靴子只会落下）");
            Shot(scene, "30h_rocket");
            inv.Accessories[4] = savedAcc;
            yield return new WaitForSeconds(1.5f);
        }

        /// <summary>
        /// The fullscreen map, the inventory with the equipment column and the shop (in
        /// Dirtmouth), and a real dive floor (Crossroads_52's Quake Floor) blown open by a bomb.
        /// </summary>
        IEnumerator UiTest(string scene, HeroController hero)
        {
            yield return new WaitForSeconds(1f);
            UI.FullMap.DevSetOpen(true);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 地图：打开={UI.FullMap.Open}，房间 {UI.FullMap.RoomCount} 个，显示 {UI.FullMap.ShownRooms} 个，主角已能操作={GameHooks.PlayerHasControl()}");
            Shot(scene, "31a_map");
            UI.FullMap.DevZoom(0.25f);
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "31b_map_out");
            UI.FullMap.DevSetOpen(false);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 地图关了，主角能操作={GameHooks.PlayerHasControl()}");

            // Dirtmouth, where the shop is open.
            var gm0 = GameManager.instance;
            if (gm0.sceneName != "Town")
            {
                gm0.BeginSceneTransition(new GameManager.SceneLoadInfo
                {
                    SceneName = "Town", EntryGateName = "left1", HeroLeaveDirection = GlobalEnums.GatePosition.right,
                    EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
                });
                float w0 = Time.time + 30f;
                while ((gm0.sceneName != "Town" || !GameHooks.PlayerHasControl()) && Time.time < w0) yield return null;
                hero = HeroController.instance;
                yield return new WaitForSeconds(1.5f);
            }
            var inv = mod.Inventory;
            inv.Armor[0] = Items.ItemStack.Of(Items.ItemType.MoltenHelmet);
            inv.Armor[1] = Items.ItemStack.Of(Items.ItemType.MoltenBreastplate);
            inv.Armor[2] = Items.ItemStack.Of(Items.ItemType.MoltenGreaves);
            Items.InventoryGui.Open = true;
            for (int f = 0; f < 5; f++)
            {
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 打开物品栏后第 {f + 1} 帧：Open={Items.InventoryGui.Open}");
            }
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 物品栏：Open={Items.InventoryGui.Open}，商店可用={Items.Shop.Available(hero)}（{GameManager.instance.sceneName}），防御 {mod.Stats.Defense}，套装 {Items.Equipment.FullSet}");
            Shot(scene, "31c_inventory");
            yield return new WaitForSeconds(0.15f);
            Items.InventoryGui.Open = false;
            yield return new WaitForSeconds(0.3f);

            // Crossroads_52: the Quake Floor, through the game's own transition.
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = "Crossroads_52", EntryGateName = "left1", HeroLeaveDirection = GlobalEnums.GatePosition.right,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            float wait = Time.time + 30f;
            while ((gm.sceneName != "Crossroads_52" || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            hero = HeroController.instance;
            yield return new WaitForSeconds(1f);
            var qf = GameObject.Find("Quake Floor");
            var fsm = qf != null ? qf.GetComponent<PlayMakerFSM>() : null;
            if (fsm == null) { Plugin.Log.LogInfo("[AutoTest] 没找到 Quake Floor"); yield break; }
            var qc = qf.GetComponent<Collider2D>();
            string before = fsm.ActiveStateName;
            Vector2 top = new Vector2(qc.bounds.center.x, qc.bounds.max.y + 0.3f);
            hero.transform.position = Clear(hero, top + new Vector2(-7f, 0.5f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            Shot("Crossroads_52", "31d_quake_before");
            Items.TerraProjectiles.Instance.Shoot(Items.ProjKind.Bomb, top, Vector2.zero, 0, 0f, 0f);
            yield return new WaitForSeconds(3.8f);
            Plugin.Log.LogInfo($"[AutoTest] 下砸地板：状态 {before} -> {fsm.ActiveStateName}，碰撞体启用={qc.enabled && qc.gameObject.activeInHierarchy}，{Items.TerraProjectiles.DevLastBlast}");
            Shot("Crossroads_52", "31e_quake_after");
        }

        /// <summary>Why the player or a placed block might not show: their renderers, and what is drawn in front of them.</summary>
        static void LogVisibility(HeroController hero, Vector2 cellPoint)
        {
            var player = GameObject.Find("Terra Player");
            int on = 0, total = 0;
            float maxAlpha = 0f;
            if (player != null)
                foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(false))
                {
                    total++;
                    if (sr.enabled && sr.sprite != null) on++;
                    maxAlpha = Mathf.Max(maxAlpha, sr.color.a);
                }
            int cells = 0;
            string cellInfo = "";
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr.name != "Terra Cell" || !sr.gameObject.activeInHierarchy) continue;
                if (Vector2.Distance(sr.transform.position, cellPoint) > 3f) continue;
                cells++;
                if (cellInfo.Length == 0) cellInfo = $"{sr.transform.position:F2} sprite={(sr.sprite != null ? sr.sprite.name : "null")} 色{sr.color} 层{sr.sortingLayerName}/{sr.sortingOrder} 材质 {sr.sharedMaterial?.name}";
            }
            var cam = GameCameras.instance.mainCamera.transform.position;
            var front = new System.Text.StringBuilder();
            var hp = (Vector2)hero.transform.position;
            foreach (var rd in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!rd.enabled || !rd.gameObject.activeInHierarchy) continue;
                var b = rd.bounds;
                if (b.center.z > 0.003f || !b.Contains(new Vector3(hp.x, hp.y, b.center.z))) continue;
                if (front.Length < 2500) front.Append($"\n    {PathOf(rd.transform)} z={b.center.z:F2} {(rd is SpriteRenderer s2 && s2.sprite != null ? s2.sprite.name : rd.sharedMaterial?.name)} 色{(rd is SpriteRenderer s3 ? s3.color.ToString() : "")}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 可见性：Terra Player 激活={(player != null && player.activeInHierarchy)} 渲染器 {on}/{total} 最大透明度 {maxAlpha:F2} 位置 {(player != null ? player.transform.position.ToString("F2") : "-")}，" +
                               $"主角 {hp:F2}，镜头 {cam:F2}；附近格子贴图 {cells} 个 {cellInfo}；主角前面的渲染器：{front}");
        }

        /// <summary>
        /// Howling Cliffs' lifts (the stag station lift and plat_lift_03 in Cliffs_03) are moved by
        /// their FSMs. Reached seamlessly, Cliffs_03 sits offset from its own coordinates: dump the
        /// FSMs and watch where the lifts go.
        /// </summary>
        IEnumerator CliffsLiftTest(HeroController hero)
        {
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = "Cliffs_02",
                EntryGateName = "right1",
                HeroLeaveDirection = GlobalEnums.GatePosition.left,
                EntryDelay = 0f,
                WaitForSceneTransitionCameraFade = true,
                Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            float wait = Time.time + 30f;
            while ((gm.sceneName != "Cliffs_02" || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 到了 {gm.sceneName}，主角 {hero.transform.position:F1}");
            // Over to the gate to Cliffs_03, so that is the neighbour that loads.
            TransitionPoint left2 = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.name == "left2" && tp.gameObject.scene.name == "Cliffs_02") left2 = tp;
            if (left2 != null)
            {
                var gb = left2.GetComponent<Collider2D>().bounds;
                hero.transform.position = Clear(hero, new Vector2(gb.max.x + 3f, gb.min.y + 1f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                Plugin.Log.LogInfo($"[AutoTest] 放到 left2 门旁 {hero.transform.position:F1}（门 {gb.min:F1}..{gb.max:F1}）");
            }
            wait = Time.time + 40f;
            while (GameObject.Find("Stag Lift") == null && Time.time < wait) yield return null;
            yield return new WaitForSeconds(2f);
            var lift = GameObject.Find("Stag Lift");
            if (lift == null) { Plugin.Log.LogInfo("[AutoTest] 找不到 Stag Lift"); yield break; }
            var liftFsm = lift.GetComponent<PlayMakerFSM>();
            var off = World.SceneFrames.Of(lift);
            Plugin.Log.LogInfo($"[AutoTest] Stag Lift 在 {lift.scene.name}（场景偏移 {off:F2}），位置 {lift.transform.position:F2}，" +
                               $"折回场景坐标 {lift.transform.position - off:F2}，状态 {liftFsm.ActiveStateName}，" +
                               $"Top Start Y={liftFsm.FsmVariables.GetFsmFloat("Top Start Y").Value:F2} Bot Start Y={liftFsm.FsmVariables.GetFsmFloat("Bot Start Y").Value:F2}");
            Shot("Cliffs_02", "29a_lift_far");

            // Walk over into Cliffs_03 and ride the lift down.
            yield return Cross(hero, "Cliffs_02", -1, 0, "29b");
            var lc = lift.GetComponent<Collider2D>();
            hero.transform.position = Clear(hero, new Vector2(lc.bounds.center.x, lc.bounds.max.y + 1.2f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            float t0 = Time.time;
            var trace = new System.Text.StringBuilder();
            string lastState = "";
            while (Time.time - t0 < 25f)
            {
                if (liftFsm.ActiveStateName != lastState)
                {
                    lastState = liftFsm.ActiveStateName;
                    trace.Append($"\n    {Time.time - t0:F1}s {lastState} 升降梯 y={lift.transform.position.y:F2}（场景 {lift.transform.position.y - off.y:F2}） 主角脚 {hero.GetComponent<BoxCollider2D>().bounds.min.y:F2}");
                }
                if (lastState == "Down" && Time.time - t0 > 3f) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            var hb = hero.GetComponent<BoxCollider2D>().bounds;
            var below = CellGrid.WorldToCell(new Vector2(lc.bounds.max.x + 0.75f, lc.bounds.max.y - 0.05f));
            Plugin.Log.LogInfo($"[AutoTest] 坐升降梯：{trace}\n    最后 升降梯顶 {lc.bounds.max.y:F2}，主角脚 {hb.min.y:F2}，梯子右边一格 {below} 实心={TerraWorld.IsSolid(below.x, below.y)} 上一格实心={TerraWorld.IsSolid(below.x, below.y + 1)}");
            Shot(GameManager.instance.sceneName, "29c_lift_bottom");
        }

        /// <summary>
        /// The hanging platforms of the Crossroads shafts (LiftPlatform) dip when landed on and
        /// come back to where they started: their collider, art and remembered start must agree.
        /// </summary>
        IEnumerator LiftTest(HeroController hero)
        {
            var lifts = Object.FindObjectsByType<LiftPlatform>(FindObjectsSortMode.None);
            string scene = GameManager.instance.sceneName;
            Plugin.Log.LogInfo($"[AutoTest] 挂绳平台：{scene} 等已载入场景里共 {lifts.Length} 个");
            if (lifts.Length == 0) yield break;
            var start1 = HarmonyLib.AccessTools.FieldRefAccess<LiftPlatform, float>("part1_start_y");
            LiftPlatform best = null;
            float bestD = float.MaxValue;
            foreach (var l in lifts)
            {
                var c = l.GetComponent<Collider2D>();
                var p1 = l.part1 != null ? l.part1.transform.position : Vector3.zero;
                Plugin.Log.LogInfo($"[AutoTest]   {l.gameObject.scene.name}/{l.name}：根 {l.transform.position:F2}，碰撞体 {(c != null ? c.bounds.min.ToString("F2") + ".." + c.bounds.max.ToString("F2") + " 启用=" + c.enabled : "无")}，" +
                                   $"部件1 {p1:F2} 记的起始 y {start1(l):F2}");
                float d = Vector2.Distance(l.transform.position, hero.transform.position);
                if (l.gameObject.scene.name == scene && d < bestD) { bestD = d; best = l; }
            }
            if (best == null) yield break;
            var col = hero.GetComponent<BoxCollider2D>();
            var lc = best.GetComponent<Collider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, new Vector2(lc.bounds.center.x, lc.bounds.max.y + 2f));
            rb.linearVelocity = Vector2.zero;
            float t0 = Time.time;
            float lowest = float.MaxValue;
            while (Time.time - t0 < 1.5f)
            {
                if (best.part1 != null) lowest = Mathf.Min(lowest, best.part1.transform.position.y);
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 落到 {best.name} 上：主角脚 y {col.bounds.min.y:F2}，平台碰撞体顶 {lc.bounds.max.y:F2}，onGround={hero.cState.onGround}，" +
                               $"部件1 最低 {lowest:F2} 现在 {best.part1.transform.position.y:F2}，记的起始 {start1(best):F2}");
            Shot(scene, "27_lift_platform");
        }

        /// <summary>
        /// A floating platform is grid like any terrain and digs away; a geo rock takes no hits
        /// from a sword, only from a pickaxe.
        /// </summary>
        IEnumerator PlatformAndRockTest(string scene, HeroController hero)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            float gravity = rb.gravityScale;
            DustyPlatform plat = null;
            float best = float.MaxValue;
            foreach (var p in Object.FindObjectsByType<DustyPlatform>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(p.transform.position, hero.transform.position);
                if (p.gameObject.activeInHierarchy && d < best) { best = d; plat = p; }
            }
            if (plat == null) Plugin.Log.LogInfo("[AutoTest] 场景里没有浮空平台");
            else
            {
                var sr = plat.GetComponent<SpriteRenderer>();
                var pcol = plat.GetComponent<Collider2D>();
                var c = CellGrid.WorldToCell(sr.bounds.center);
                hero.transform.position = Clear(hero, (Vector2)sr.bounds.center + new Vector2(0f, 2.5f));
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = 0f;
                yield return new WaitForSeconds(0.4f);
                int dug = 0, cells = 0;
                for (int x = CellGrid.WorldToCell(sr.bounds.min).x; x <= CellGrid.WorldToCell(sr.bounds.max).x; x++)
                {
                    if (!TerraWorld.IsSolid(x, c.y)) continue;
                    cells++;
                    if (x % 3 != 0 || !TerraWorld.CanDig(x, c.y)) continue;
                    TerraWorld.Set(x, c.y, CellKind.Empty);
                    dug++;
                }
                Plugin.Log.LogInfo($"[AutoTest] 浮空平台 '{plat.name}' @ {sr.bounds.center:F1}：中线 {cells} 格实心，原碰撞体启用={pcol != null && pcol.enabled}，隔两格挖掉 {dug} 格");
                yield return new WaitForSeconds(0.4f);
                Shot(scene, "26f_platform_dug");
                yield return new WaitForSeconds(0.15f);
                rb.gravityScale = gravity;
            }

            GeoRock rock = null;
            best = float.MaxValue;
            foreach (var r in Object.FindObjectsByType<GeoRock>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(r.transform.position, hero.transform.position);
                if (r.gameObject.activeInHierarchy && d < best) { best = d; rock = r; }
            }
            var fsm = rock != null ? rock.GetComponent<PlayMakerFSM>() : null;
            var rcol = rock != null ? rock.GetComponentInChildren<Collider2D>() : null;
            if (fsm == null || rcol == null) { Plugin.Log.LogInfo("[AutoTest] 附近没有吉欧石"); yield break; }
            var rp = rcol.bounds.center;
            hero.transform.position = Clear(hero, new Vector2(rp.x - 0.9f, rp.y + 0.3f));
            hero.FaceRight();
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = 0f;
            yield return new WaitForSeconds(0.5f);
            int before = fsm.FsmVariables.GetFsmInt("Hits").Value;
            for (int i = 0; i < 3; i++)
            {
                mod.TestSwing(Items.ItemType.CopperBroadsword, 1f);
                yield return new WaitForSeconds(0.45f);
            }
            int afterSword = fsm.FsmVariables.GetFsmInt("Hits").Value;
            mod.TestStrike(rcol, 1f);
            yield return new WaitForSeconds(0.45f);
            int afterPick = fsm.FsmVariables.GetFsmInt("Hits").Value;
            Plugin.Log.LogInfo($"[AutoTest] 吉欧石 '{rock.name}'：阔剑挥 3 下 Hits {before} -> {afterSword}（应不变），镐子敲 1 下 -> {afterPick}（应 +1）");
            rb.gravityScale = gravity;
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>One use of the Zenith aimed six units to the right: three swords swoop out and back.</summary>
        IEnumerator ZenithTest(HeroController hero)
        {
            var swords = Items.ZenithSwords.Instance;
            Vector2 center = hero.GetComponent<BoxCollider2D>().bounds.center;
            mod.DevAim = center + Vector2.right * 6f;
            mod.TestSwing(Items.ItemType.Zenith, 1f);
            float t0 = Time.time, reach = 0f;
            int most = 0;
            bool shot1 = false, shot2 = false;
            while (Time.time - t0 < 3f)
            {
                most = Mathf.Max(most, swords.ActiveCount);
                reach = Mathf.Max(reach, swords.FarthestReach);
                if (!shot1 && Time.time - t0 > 0.2f) { shot1 = true; Shot(GameManager.instance.sceneName, "26b_zenith_out"); }
                if (!shot2 && Time.time - t0 > 0.45f) { shot2 = true; Shot(GameManager.instance.sceneName, "26c_zenith_back"); }
                if (Time.time - t0 > 0.6f && swords.ActiveCount == 0) break;
                yield return null;
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 天顶剑：同时最多 {most} 把（原版一次使用 3 把），最远飞出 {reach:F1}（目标 6），{Time.time - t0:F2}s 后全部消失");
        }

        /// <summary>Walls made of the scene's hand-placed colliders dig away like the rest.</summary>
        IEnumerator HandPlacedTest(string scene)
        {
            var terrain = TerrainSystem.Current;
            var col = terrain.FirstHandPlaced;
            if (col == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 这个场景没有手放的地形碰撞体");
                yield break;
            }
            var b = terrain.FirstHandPlacedBounds;
            var center = CellGrid.WorldToCell(b.center);
            bool solidBefore = TerraWorld.IsSolid(center.x, center.y);
            var lo = CellGrid.WorldToCell(b.min);
            var hi = CellGrid.WorldToCell(b.max);
            int dug = 0;
            for (int y = lo.y; y <= hi.y && dug < 400; y++)
                for (int x = lo.x; x <= hi.x && dug < 400; x++)
                    if (TerraWorld.CanDig(x, y)) { TerraWorld.Set(x, y, CellKind.Empty); dug++; }
            yield return new WaitForSeconds(0.2f);
            // Solid colliders only: triggers don't stop anyone.
            var hits = new System.Collections.Generic.List<Collider2D>();
            Physics2D.OverlapPoint(CellGrid.CellCenter(center.x, center.y), new ContactFilter2D { useLayerMask = true, layerMask = 1 << 8, useTriggers = false }, hits);
            Plugin.Log.LogInfo($"[AutoTest] 手放碰撞体 '{col.name}'（{col.GetType().Name}，{b.min:F1}..{b.max:F1}）：挖前实心={solidBefore}，挖了 {dug} 格，" +
                               $"挖后中心还挡人的碰撞={(hits.Count > 0 ? hits[0].name : "无")}，原碰撞体启用={col.enabled}");
        }

        /// <summary>The world's cells around a spot as text: # this scene solid, % another scene solid, . open, : dug, space outside every region, | the gate line.</summary>
        static void LogCellMap(int x0, int x1, int y0, int y1, int seam)
        {
            var sb = new System.Text.StringBuilder();
            for (int y = y1; y >= y0; y--)
            {
                sb.Append($"\n    {y,4} ");
                for (int x = x0; x <= x1; x++)
                {
                    if (x == seam) sb.Append('|');
                    var r = TerraWorld.RegionAt(x, y);
                    sb.Append(r == null ? ' ' : TerraWorld.IsDug(x, y) ? ':' : TerraWorld.IsSolid(x, y) ? (r == TerraWorld.Live ? '#' : '%') : '.');
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 门附近的格子（# 本场景实心，% 隔壁实心，. 空，| 门线）x {x0}..{x1}：{sb}");
        }


        IEnumerator FrameRateTest()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            int start = Time.frameCount;
            yield return new WaitForSecondsRealtime(3f);
            float fps = (Time.frameCount - start) / 3f;
            Plugin.Log.LogInfo($"[AutoTest] 帧率 {fps:F1}，vSync={QualitySettings.vSyncCount} targetFrameRate={Application.targetFrameRate} " +
                               $"显示器 {Screen.currentResolution.width}x{Screen.currentResolution.height}@{Screen.currentResolution.refreshRateRatio.value:F0}Hz");
        }

        /// <summary>Finds pots, levers and other props that react to the nail, and swings at the nearest.</summary>
        IEnumerator PropsTest(string scene, HeroController hero, Rigidbody2D rb)
        {
            var found = new System.Collections.Generic.List<(float dist, string what, GameObject go)>();
            Vector2 hp = hero.transform.position;
            foreach (var b in Object.FindObjectsByType<BreakableObject>(FindObjectsSortMode.None))
                found.Add((Vector2.Distance(hp, b.transform.position), "BreakableObject", b.gameObject));
            foreach (var b in Object.FindObjectsByType<Breakable>(FindObjectsSortMode.None))
                found.Add((Vector2.Distance(hp, b.transform.position), "Breakable", b.gameObject));
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string n = f.FsmName.ToLowerInvariant();
                if (n.Contains("break") || n.Contains("lever") || n.Contains("switch"))
                    found.Add((Vector2.Distance(hp, f.transform.position), "FSM " + f.FsmName, f.gameObject));
            }
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            found.RemoveAll(f => f.go.scene != active);
            found.Sort((a, b) => a.dist.CompareTo(b.dist));
            Plugin.Log.LogInfo($"[AutoTest] 场景里会被骨钉打中的物件 {found.Count} 个：");
            for (int i = 0; i < found.Count && i < 12; i++)
                Plugin.Log.LogInfo($"    {found[i].what} '{found[i].go.name}' 距离 {found[i].dist:F1} 位置 {found[i].go.transform.position}");

            foreach (var (_, what, go) in found)
            {
                if (what == "Breakable" || what == "BreakableObject")
                {
                    var col = go.GetComponent<Collider2D>();
                    var tp = go.transform.position;
                    hero.transform.position = Clear(hero, new Vector2(tp.x - 1.0f, tp.y + 0.3f));
                    rb.linearVelocity = Vector2.zero;
                    float gravity = rb.gravityScale;
                    rb.gravityScale = 0f;
                    yield return new WaitForSeconds(0.6f);
                    bool before = col != null && col.enabled && go.activeInHierarchy;
                    mod.TestSwing(Items.ItemType.CopperBroadsword, 1f);
                    yield return new WaitForSeconds(0.15f);
                    Shot(scene, "10_prop_swing");
                    yield return new WaitForSeconds(0.4f);
                    bool after = col != null && col.enabled && go.activeInHierarchy;
                    rb.gravityScale = gravity;
                    Plugin.Log.LogInfo($"[AutoTest] 砍 {what} '{go.name}'：碰撞体启用 {before} -> {after}（变 False 说明打碎了）");
                    break;
                }
            }
        }

        IEnumerator CombatTest(string scene, HeroController hero, Rigidbody2D rb)
        {
            // Taking a hit: one mask's worth should cost Terraria life, not masks.
            var pd = PlayerData.instance;
            int masksBefore = pd.health, lifeBefore = mod.Stats.Life;
            pd.TakeHealth(1);
            Plugin.Log.LogInfo($"[AutoTest] 受击：生命 {lifeBefore} -> {mod.Stats.Life}，面具 {masksBefore} -> {pd.health}");

            // Swinging at the nearest living enemy.
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                // Skip enemies that haven't woken up yet: their hurtbox is still switched off.
                // Only the scene being played: walking up to one in a neighbour would cross into it.
                var hurt = hm.GetComponent<Collider2D>();
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hurt == null || !hurt.enabled || hm.gameObject.layer != 11) continue;
                if (hm.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 场景里没有活着的敌人，跳过武器测试");
                yield break;
            }
            var tp = target.transform.position;
            Plugin.Log.LogInfo($"[AutoTest] 目标 '{target.name}' hp={target.hp} 位置 {tp}，距离 {best:F1}");
            Items.MeleeHits.DebugLog = msg => Plugin.Log.LogInfo("[AutoTest] " + msg);
            // Freeze the hero beside it so neither falls away during the swings.
            hero.transform.position = Clear(hero, new Vector2(tp.x - 1.1f, tp.y + 0.3f));
            rb.linearVelocity = Vector2.zero;
            var gravity = rb.gravityScale;
            rb.gravityScale = 0f;
            yield return new WaitForSeconds(0.2f);

            // Shield of Cthulhu bonk: dash into it from three units away, from whichever side is open.
            int from = -1;
            foreach (int s in new[] { -1, 1 })
            {
                bool clear = true;
                for (float d = 0.6f; d <= 3.4f && clear; d += 0.25f)
                    for (float h = -0.4f; h <= 0.9f && clear; h += 0.4f)
                        if (TerraWorld.IsSolidAt(new Vector2(tp.x + s * d, tp.y + h))) clear = false;
                if (clear) { from = s; break; }
            }
            hero.transform.position = Clear(hero, new Vector2(tp.x + from * 3f, tp.y + 0.3f));
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            int hpBefore = target.hp;
            Plugin.Log.LogInfo($"[AutoTest] 撞击前 dashDelay={Player.TerraMovement.DashDelay:F0}，从{(from < 0 ? "左" : "右")}边冲");
            Player.TerraMovement.RequestDash(-from);
            float minVx = 0f, maxVx = 0f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float vx = rb.linearVelocity.x / Player.TerraMovement.PxT;
                minVx = Mathf.Min(minVx, vx);
                maxVx = Mathf.Max(maxVx, vx);
                yield return null;
            }
            if (from > 0) (minVx, maxVx) = (-maxVx, -minVx);
            Plugin.Log.LogInfo($"[AutoTest] 克苏鲁之盾撞击：hp {hpBefore} -> {target.hp}，冲刺最快 {maxVx:F1}，弹回最快 {minVx:F1} px/tick（原版冲 14.5，弹回 -9）");
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);

            for (int i = 0; i < 4 && !target.isDead; i++)
            {
                int before = target.hp;
                var weapon = i < 2 ? Items.ItemType.CopperBroadsword : Items.ItemType.Zenith;
                float dir = target.transform.position.x < hero.transform.position.x ? -1f : 1f;
                hero.transform.position = Clear(hero, new Vector2(target.transform.position.x - 1.1f * dir, target.transform.position.y + 0.3f));
                rb.linearVelocity = Vector2.zero;
                mod.TestSwing(weapon, dir);
                yield return new WaitForSeconds(0.12f);
                if (i == 0) Shot(scene, "6_swing");
                yield return new WaitForSeconds(0.45f);
                Plugin.Log.LogInfo($"[AutoTest] 用 {Items.ItemDb.Get(weapon).Name} 砍：hp {before} -> {target.hp} dead={target.isDead}");
            }
            rb.gravityScale = gravity;
            Items.MeleeHits.DebugLog = null;
            Shot(scene, "7_after_combat");
            yield return null;
        }

        void Finish()
        {
            KeysDown();
            InControl.InputManager.SuspendInBackground = suspendInputInBackground;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = inputBackgroundBehavior;
            done = true;
            if (quitAfter) mod.StartCoroutine(QuitSoon());
        }

        static IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        /// <summary>
        /// Where to put the hero near a point without being inside solid cells: the point itself,
        /// or the first spot straight above it the hero fits in.
        /// </summary>
        static Vector3 Clear(HeroController hero, Vector2 pos)
        {
            const float c = CellGrid.CellSize;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            Vector2 offset = (Vector2)b.center - (Vector2)hero.transform.position;
            for (int k = 0; k < 40; k++)
            {
                var center = pos + offset + Vector2.up * (k * c);
                var lo = CellGrid.WorldToCell(center - (Vector2)b.extents);
                var hi = CellGrid.WorldToCell(center + (Vector2)b.extents);
                bool solid = false;
                for (int y = lo.y; y <= hi.y && !solid; y++)
                    for (int x = lo.x; x <= hi.x && !solid; x++)
                        solid = TerraWorld.IsSolid(x, y);
                if (!solid) return new Vector3(pos.x, pos.y + k * c, Constants.HERO_Z);
            }
            return new Vector3(pos.x, pos.y, Constants.HERO_Z);
        }

        static void Place(Vector2Int cell, CellKind kind)
        {
            if (TerraMod.CanPlace(cell)) TerraWorld.Set(cell.x, cell.y, kind);
        }

        void Shot(string scene, string name)
        {
            string path = Path.Combine(outDir, $"{scene}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Plugin.Log.LogInfo($"[AutoTest] 截图 {path}");
        }

        /// <summary>Lists every renderer whose bounds cover a point, nearest the camera first.</summary>
        static void LogRenderersAt(Vector2 p, string label, float maxAbsZ = 3f)
        {
            var hits = new System.Collections.Generic.List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y && Mathf.Abs(b.center.z) < maxAbsZ) hits.Add(r);
            }
            hits.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            Plugin.Log.LogInfo($"[AutoTest] {label} {p} 处的渲染器（{hits.Count} 个，|z|<{maxAbsZ}）：");
            foreach (var r in hits)
            {
                var mat = r.sharedMaterial;
                string color = r is SpriteRenderer sr ? $" color={sr.color} sprite={(sr.sprite != null ? sr.sprite.name : "-")}" : "";
                Plugin.Log.LogInfo($"    z={r.transform.position.z:F4} {r.GetType().Name} '{r.name}' layer={r.gameObject.layer} sort={r.sortingLayerName}/{r.sortingOrder} " +
                                   $"shader={(mat != null ? mat.shader.name : "-")} bounds x {r.bounds.min.x:F1}..{r.bounds.max.x:F1} y {r.bounds.min.y:F1}..{r.bounds.max.y:F1}{color} " +
                                   $"parent='{(r.transform.parent != null ? r.transform.parent.name : "")}' scene={r.gameObject.scene.name}");
            }
        }

        static void LogDiagnostics(HeroController hero)
        {
            var cam = GameCameras.instance.mainCamera;
            var col = hero.GetComponent<BoxCollider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 相机 {cam.transform.position} 透视={!cam.orthographic} fov={cam.fieldOfView:F2} sortMode={cam.transparencySortMode}");
            Plugin.Log.LogInfo($"[AutoTest] 主角 pos={hero.transform.position} 碰撞盒 size={col.size} offset={col.offset}");
            Plugin.Log.LogInfo($"[AutoTest] Physics2D queriesStartInColliders={Physics2D.queriesStartInColliders} callbacksOnDisable={Physics2D.callbacksOnDisable} " +
                               $"忽略碰撞 8-9={Physics2D.GetIgnoreLayerCollision(8, 9)} 8-11={Physics2D.GetIgnoreLayerCollision(8, 11)} 17-11={Physics2D.GetIgnoreLayerCollision(17, 11)}");
            var rb = hero.GetComponent<Rigidbody2D>();
            Plugin.Log.LogInfo($"[AutoTest] 移动参数 RUN={hero.RUN_SPEED} WALK={hero.WALK_SPEED} JUMP={hero.JUMP_SPEED} MIN_JUMP={hero.MIN_JUMP_SPEED} JUMP_STEPS={hero.JUMP_STEPS} JUMP_STEPS_MIN={hero.JUMP_STEPS_MIN} " +
                               $"JUMP_TIME={hero.JUMP_TIME} MAX_FALL={hero.MAX_FALL_VELOCITY} gravityScale={rb.gravityScale} g={Physics2D.gravity} fixedDt={Time.fixedDeltaTime}");
            var sprite = hero.GetComponent<tk2dSprite>();
            if (sprite != null && sprite.CurrentSprite != null)
                Plugin.Log.LogInfo($"[AutoTest] 主角贴图 PPU ≈ {1f / sprite.CurrentSprite.texelSize.x:F1}");
        }
    }
}
