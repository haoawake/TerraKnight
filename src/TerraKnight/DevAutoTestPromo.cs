using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "promo"): clips for the mod's trailer, recorded frame by frame (game time held to a
    /// thirtieth of a second a frame, Time.captureFramerate, so the video is smooth however slow the
    /// capture), each into dev/rec/&lt;clip&gt;/00000.jpg...: every class beating a Master boss with its
    /// best weapon, a pickaxe straight down from Dirtmouth into the Crossroads, wings up the well, the
    /// Terraria map and a pylon's teleport, and the stations set down.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        bool recording;
        string recDir;
        int recFrame;

        /// <summary>The Chinese cut: the game's own text in Simplified Chinese too.</summary>
        bool zh;

        /// <summary>
        /// The game switched to Simplified Chinese for this session only: the language the game saves
        /// (gameSettings.gameLanguage, written by the options menu) is left as the player set it. The
        /// menu's texts, fonts and title (the mod's 泰拉圣巢) are refreshed for it.
        /// </summary>
        static void PromoChinese(GameManager gm)
        {
            TeamCherry.Localization.Language.SwitchLanguage(TeamCherry.Localization.LanguageCode.ZH);
            gm.RefreshLocalization();
            foreach (var f in Object.FindObjectsByType<ChangeFontByLanguage>(FindObjectsInactive.Include, FindObjectsSortMode.None)) f.SetFont();
            var styles = MenuStyles.Instance;
            if (styles != null) styles.SetStyle(styles.CurrentStyle, false, false);
            Plugin.Log.LogInfo($"[AutoTest] 录制 中文版：语言 {TeamCherry.Localization.Language.CurrentLanguage()}，存档里的设置仍是 {gm.gameSettings.gameLanguage}");
        }

        void StartRec(string clip)
        {
            if (zh) clip = "zh_" + clip;
            recDir = Path.Combine(outDir, "rec", clip);
            if (Directory.Exists(recDir)) Directory.Delete(recDir, true);
            Directory.CreateDirectory(recDir);
            recFrame = 0;
            recording = true;
            // One physics step (50 a second) a frame: the motion is even, frame to frame.
            Time.captureFramerate = 50;
            mod.StartCoroutine(RecordLoop());
            Plugin.Log.LogInfo($"[AutoTest] 录制 开始 {clip}");
        }

        void StopRec()
        {
            recording = false;
            Time.captureFramerate = 0;
            UI.TerraCursor.DevAt = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 结束 {Path.GetFileName(recDir)}：{recFrame} 帧");
        }

        IEnumerator RecordLoop()
        {
            while (recording)
            {
                yield return new WaitForEndOfFrame();
                if (!recording) break;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var jpg = tex.EncodeToJPG(88);
                Object.Destroy(tex);
                File.WriteAllBytes(Path.Combine(recDir, recFrame.ToString("D5") + ".jpg"), jpg);
                recFrame++;
            }
        }

        /// <summary>The weapon's aim (and the drawn cursor) on a point of the world.</summary>
        void AimAt(Vector2 world)
        {
            mod.DevAim = world;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(world);
            UI.TerraCursor.DevAt = new Vector2(sp.x, Screen.height - sp.y);
        }

        IEnumerator EnterRoom(string room)
        {
            var gm = GameManager.instance;
            float t0 = Time.time;
            while (gm.IsInSceneTransition && Time.time - t0 < 10f) yield return null;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = room, EntryGateName = EntryGateOf(room), HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            t0 = Time.time;
            yield return new WaitForSeconds(0.5f);
            while ((gm.sceneName != room || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 20f) yield return null;
            yield return new WaitForSeconds(1.5f);
        }

        static List<HealthManager> BossesOf(string record, bool includeInactive)
        {
            var found = new List<HealthManager>();
            for (int s = 0; s < USM.sceneCount; s++)
            {
                var sc = USM.GetSceneAt(s);
                if (!sc.IsValid() || !sc.isLoaded) continue;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var hm in root.GetComponentsInChildren<HealthManager>(includeInactive))
                        if (UI.EnemyHover.RecordOf(hm) == record && (includeInactive || hm.gameObject.activeInHierarchy)) found.Add(hm);
            }
            return found;
        }

        /// <summary>
        /// A class's fight for the trailer: the boss's room entered, the boss woken, then fought with the
        /// loadout's weapons (aimed at it, the player keeping a few tiles off and hopping on wings),
        /// recorded from the fight's start until a moment after it falls.
        /// </summary>
        IEnumerator PromoFight(HeroController hero, string clip, string record, string scene, int loadout, ItemType[] weapons, ItemType[] summons)
        {
            var inv = mod.Inventory;
            string room = scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;
            GameDifficulty.Current = DifficultyLevel.Master;
            PlayerData.instance.isInvincible = true;
            yield return EnterRoom(room);
            mod.TrySwitchLoadout(loadout);
            yield return null;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var all = BossesOf(record, true);
            if (all.Count == 0)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：找不到 {record}");
                yield break;
            }
            Vector2 bp = all[0].transform.position;
            int walk = 0;
            foreach (int side in new[] { -1, 1 })
            {
                var probe = bp + new Vector2(side * 6f, 3f);
                var down = Physics2D.Raycast(probe, Vector2.down, 30f, terrain);
                if (down.collider == null) continue;
                hero.transform.position = new Vector3(probe.x, down.point.y + 0.7f, hero.transform.position.z);
                walk = -side;
                break;
            }
            Player.TerraMovement.DevMove = walk;
            yield return new WaitForSeconds(1.5f);
            Player.TerraMovement.DevMove = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 20f)
            {
                PlayerData.instance.isInvincible = true;
                bool up = false;
                foreach (var hm in BossesOf(record, false)) if (!hm.isDead && !hm.IsInvincible && OnScreen(hm.transform.position)) up = true;
                if (up && GameHooks.PlayerHasControl()) break;
                yield return new WaitForSeconds(0.2f);
            }

            int Slot(ItemType t) { for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == t) return i; return -1; }
            StartRec(clip);
            // Minions first: each summon used twice (their slots).
            if (summons != null)
                foreach (var s in summons)
                {
                    int si = Slot(s);
                    if (si < 0) continue;
                    inv.Selected = si;
                    for (int k = 0; k < 2; k++)
                    {
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        AimAt((Vector2)hero.transform.position + Vector2.up * 2f);
                        mod.DevUseSelected((Vector2)hero.transform.position + Vector2.up * 2f);
                        yield return new WaitForSeconds(0.25f);
                    }
                }
            float start = Time.time, quiet = 0f, hop = 0f;
            int wi = 0;
            while (Time.time - start < 22f && quiet < 1.6f)
            {
                PlayerData.instance.isInvincible = true;
                HealthManager target = null;
                float best = float.MaxValue;
                foreach (var hm in BossesOf(record, false))
                {
                    if (hm.isDead) continue;
                    float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                    if (d < best) { best = d; target = hm; }
                }
                quiet = target == null ? quiet + Time.deltaTime : 0f;
                if (target != null)
                {
                    var col = target.GetComponent<Collider2D>();
                    Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
                    AimAt(at);
                    // The weapon: the first, a second one halfway (the fight shows both).
                    if (weapons.Length > 1 && Time.time - start > 6f) wi = 1;
                    int slot = Slot(weapons[wi]);
                    if (slot >= 0) inv.Selected = slot;
                    mod.DevHold = true;
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at);
                    float dx = at.x - hero.transform.position.x;
                    Player.TerraMovement.DevMove = Mathf.Abs(dx) > 9f ? (int)Mathf.Sign(dx) : Mathf.Abs(dx) < 4f ? -(int)Mathf.Sign(dx) : 0;
                    hop += Time.deltaTime;
                    if (hop > 2.4f && hero.cState.onGround)
                    {
                        hop = 0f;
                        Player.TerraMovement.DevJump = true;
                        Player.TerraMovement.DevGroundJump(hero);
                    }
                    else if (hop > 0.9f) Player.TerraMovement.DevJump = false;
                }
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            mod.DevHold = null;
            yield return new WaitForSeconds(1.2f);
            StopRec();
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {clip}：{record} {(quiet >= 1.6f ? "打倒了" : "没打完")}，用时 {Time.time - start:F1}s");
        }

        IEnumerator PromoTest(HeroController hero)
        {
            var inv = mod.Inventory;
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);

            // Each class against a Master boss.
            yield return PromoFight(hero, "melee_hiveknight", "HiveKnight", "Hive_05", 0, new[] { ItemType.Zenith, ItemType.StarWrath }, null);
            yield return PromoFight(hero, "ranged_dungdefender", "DungDefender", "Waterways_05_boss", 1, new[] { ItemType.SDMG, ItemType.CelebrationMk2 }, null);
            yield return PromoFight(hero, "magic_brokenvessel", "InfectedKnight", "Abyss_19", 2, new[] { ItemType.LastPrism, ItemType.LunarFlareBook }, null);
            yield return PromoFight(hero, "summon_mawlek", "Mawlek", "Crossroads_09", 3, new[] { ItemType.Kaleidoscope }, new[] { ItemType.StardustDragonStaff, ItemType.Terraprisma });

            yield return PromoMine(hero);

            // Wings straight up the well from the Crossroads into Dirtmouth.
            var town = TerrainSystem.Find("Town");
            var origin = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            hero.transform.position = Clear(hero, origin + new Vector2(185f, -13f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            StartRec("flight_well");
            AimAt((Vector2)hero.transform.position + new Vector2(3f, 6f));
            Player.TerraMovement.DevJump = true;
            Player.TerraMovement.DevGroundJump(hero);
            yield return new WaitForSeconds(5f);
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(1.5f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 飞行：到了 {GameManager.instance.sceneName} {(Vector2)hero.transform.position:F1}");

            // The Terraria map, then a pylon's teleport.
            StartRec("map_pylon");
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevZoom(0.6f);
            yield return new WaitForSeconds(1.2f);
            UI.WorldMap.DevZoom(1.6f);
            yield return new WaitForSeconds(1.2f);
            UI.WorldMap.DevSetOpen(false);
            Pylons.Pylon target = null;
            foreach (var p in Pylons.Unlocked) if (p.Scene == "Fungus1_15") target = p;
            if (target == null) foreach (var p in Pylons.Unlocked) if (p.Scene == "Crossroads_30") target = p;
            if (target != null) Pylons.Teleport(target);
            yield return new WaitForSeconds(3.5f);
            StopRec();

            yield return PromoStations(hero);
            Plugin.Log.LogInfo($"[AutoTest] 录制 全部完成");
        }
        /// <summary>Whether a point of the world is well inside the camera's view.</summary>
        static bool OnScreen(Vector2 world)
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(world);
            return v.x > 0.08f && v.x < 0.92f && v.y > 0.08f && v.y < 0.92f;
        }

        /// <summary>
        /// Dev (suite "promo2"): the clips redone after a look at the first takes: the melee fight against
        /// a boss that's on screen from the start, the pickaxe really digging, wings through the Path of
        /// Pain, the stations on Dirtmouth's flat street.
        /// </summary>
        IEnumerator Promo2Test(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return PromoFight(hero, "melee_soulwarrior", "MageKnight", "Ruins1_23", 0, new[] { ItemType.Zenith, ItemType.StarWrath }, null);
            yield return PromoMine(hero);
            yield return PromoPathOfPain(hero);
            yield return PromoStations(hero);
            Plugin.Log.LogInfo($"[AutoTest] 录制 promo2 完成");
        }

        /// <summary>Dev (suite "promo3"): the Path of Pain again (steered by the hazards' shapes) and the Gravity Globe on a ceiling.</summary>
        IEnumerator Promo3Test(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            mod.ApplyCreativeWorld();
            yield return new WaitForSeconds(1f);
            yield return PromoPathOfPain(hero);
            yield return PromoGravity(hero);
            Plugin.Log.LogInfo($"[AutoTest] 录制 promo3 完成");
        }

        /// <summary>
        /// The Gravity Globe worn in the City of Tears: Up turns gravity over, the hero falls onto the
        /// ceiling and walks along it upside down, then turns it back.
        /// </summary>
        IEnumerator PromoGravity(HeroController hero)
        {
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var inv = mod.Inventory;
            var gm = GameManager.instance;
            yield return EnterRoom("Ruins1_23");
            mod.TrySwitchLoadout(0);
            PlayerData.instance.isInvincible = true;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            var home = (Vector2)hero.transform.position;
            Vector3 under = home;
            float room = 99f, run = 0f;
            for (int k = 0; k <= 200; k++)
            {
                float dx = (k % 2 == 0 ? 1f : -1f) * (k / 2) * 0.5f;
                var spot = Clear(hero, home + new Vector2(dx, 0f));
                var head = Physics2D.Raycast((Vector2)spot + Vector2.up * 0.7f, Vector2.up, 60f, terrain);
                var feetHit = Physics2D.Raycast((Vector2)spot, Vector2.down, 3f, terrain);
                float r = head.collider != null ? head.distance : 99f;
                if (feetHit.collider == null || r < 2.5f || r > 9f) continue;
                // How far the ceiling carries on to the right at about that height.
                float len = 0f;
                for (float x = 0.5f; x < 14f; x += 0.5f)
                {
                    var up2 = Physics2D.Raycast((Vector2)spot + new Vector2(x, 0.7f), Vector2.up, 60f, terrain);
                    if (up2.collider == null || Mathf.Abs(up2.distance - r) > 1.5f) break;
                    len = x;
                }
                if (len > run) { run = len; under = spot; room = r; }
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 重力球：{gm.sceneName} {under}，头顶 {room:F1}，天花板往右 {run:F1}");
            var saved = inv.Accessories[Inventory.AccessorySlots - 1];
            inv.Accessories[Inventory.AccessorySlots - 1] = ItemStack.Of(ItemType.GravityGlobe);
            hero.transform.position = under;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            StartRec("gravity_ceiling");
            AimAt((Vector2)col.bounds.center + new Vector2(2f, 2f));
            yield return new WaitForSeconds(0.5f);
            Player.GravityGlobe.DevUpPress = true;
            float t0 = Time.time;
            while (Time.time - t0 < 1.5f && !(Player.GravityGlobe.Flipped && hero.cState.onGround && Time.time - t0 > 0.2f)) yield return null;
            bool flipped = Player.GravityGlobe.Flipped, onCeiling = hero.cState.onGround;
            yield return new WaitForSeconds(0.3f);
            float x0 = col.bounds.center.x;
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(Mathf.Clamp(run / 8f, 1.2f, 2.6f));
            Player.TerraMovement.DevMove = 0;
            float walked = col.bounds.center.x - x0;
            yield return new WaitForSeconds(0.4f);
            Player.GravityGlobe.DevUpPress = true;
            yield return new WaitForSeconds(1.2f);
            StopRec();
            inv.Accessories[Inventory.AccessorySlots - 1] = saved;
            Plugin.Log.LogInfo($"[AutoTest] 录制 重力球：翻转 {flipped}，站上天花板 {onCeiling}，倒着走了 {walked:F1}，现在翻转 {Player.GravityGlobe.Flipped}");
        }

        /// <summary>
        /// A pickaxe straight down from Dirtmouth's street into the scene under it: the street's column
        /// (two cells wide) that digs deepest picked, then dug cell by cell under the hero's feet, the
        /// way a held click with the pickaxe aimed down does, until the hero has stood a moment below.
        /// </summary>
        IEnumerator PromoMine(HeroController hero)
        {
            var inv = mod.Inventory;
            var gm = GameManager.instance;
            yield return EnterRoom("Town");
            mod.TrySwitchLoadout(0);
            yield return null;
            const float c = CellGrid.CellSize;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var town = TerrainSystem.Find("Town");
            var origin = town != null ? (Vector2)town.CellOffset * c : Vector2.zero;
            int bestCol = 0, bestDepth = -1;
            float bestTop = 0f;
            string bestBelow = "";
            for (float x = origin.x + 120f; x <= origin.x + 200f; x += c)
            {
                var top = Physics2D.Raycast(new Vector2(x + c, origin.y + 16f), Vector2.down, 16f, terrain);
                if (top.collider == null) continue;
                var cell = CellGrid.WorldToCell(new Vector2(x + 0.01f, top.point.y - 0.05f));
                int d = 0;
                string below = "";
                for (; d < 200; d++)
                {
                    int y = cell.y - d;
                    bool ok = true;
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        int gx = cell.x + dx;
                        var r = TerraWorld.RegionAt(gx, y);
                        if (r == null || (TerraWorld.IsSolid(gx, y) && !TerraWorld.CanDig(gx, y))) ok = false;
                        else below = r.SceneName;
                    }
                    if (!ok) break;
                }
                // Reaching another scene counts for more than depth alone.
                int score = d + (below != "Town" ? 1000 : 0);
                if (score > bestDepth) { bestDepth = score; bestCol = cell.x; bestTop = top.point.y; bestBelow = below; }
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 挖矿：选了第 {bestCol} 列（x {bestCol * c - origin.x:F1}），能挖 {bestDepth % 1000} 格，底下是 {bestBelow}");
            float mid = (bestCol + 1) * c;
            hero.transform.position = Clear(hero, new Vector2(mid, bestTop + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            int pick = -1;
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.PickPower > 0) { pick = i; break; }
            if (pick < 0) yield break;
            inv.Selected = pick;
            StartRec("mining_dirtmouth");
            float t0 = Time.time, arrived = -1f, stuck = 0f;
            int mined = 0;
            string reached = "Town";
            while (Time.time - t0 < 18f)
            {
                PlayerData.instance.isInvincible = true;
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                Player.TerraMovement.DevMove = b.center.x < mid - 0.12f ? 1 : b.center.x > mid + 0.12f ? -1 : 0;
                int fy = CellGrid.WorldToCell(new Vector2(b.center.x, b.min.y - 0.05f)).y;
                Vector2Int? next = null;
                for (int y = fy; y > fy - 4 && next == null; y--)
                    for (int gx = bestCol; gx <= bestCol + 1 && next == null; gx++)
                        if (TerraWorld.IsSolid(gx, y) && TerraWorld.CanDig(gx, y)) next = new Vector2Int(gx, y);
                if (next != null)
                {
                    var at = CellGrid.CellCenter(next.Value.x, next.Value.y);
                    AimAt(at);
                    if (mod.DevReady && !mod.IsUsingItem)
                    {
                        var before = TerraWorld.Get(next.Value.x, next.Value.y);
                        mod.DevUseSelected(at, false, true);
                        if (TerraWorld.Get(next.Value.x, next.Value.y) != before) mined++;
                    }
                    stuck = 0f;
                }
                else if (hero.cState.onGround) stuck += Time.deltaTime;
                var here = TerraWorld.RegionAt(CellGrid.WorldToCell(b.center).x, CellGrid.WorldToCell(b.center).y);
                if (here != null && here.SceneName != "Town" && arrived < 0f) { arrived = Time.time; reached = here.SceneName; }
                if (arrived > 0f && Time.time - arrived > 3f && hero.cState.onGround) break;
                if (stuck > 1.5f) break;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.8f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 挖矿：挖了 {mined} 格，{(arrived > 0f ? $"{arrived - t0:F1}s 进了 {reached}" : "没进别的场景")}，最后在 {gm.sceneName} {(Vector2)hero.transform.position:F1}");
        }

        /// <summary>
        /// Wings through the Path of Pain: in from its gate and straight across, the flight steered each
        /// frame toward the clear line ahead (the saws and thorns, wherever they've moved, and the walls
        /// kept out of the way), rising while the way climbs or the hero sinks.
        /// </summary>
        IEnumerator PromoPathOfPain(HeroController hero)
        {
            const string room = "White_Palace_18";
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var gm = GameManager.instance;
            yield return EnterRoom(room);
            mod.TrySwitchLoadout(0);
            PlayerData.instance.isInvincible = true;
            if (gm.sceneName != room)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛之路：没进去，在 {gm.sceneName}");
                yield break;
            }
            int dir = EntryGateOf(room).StartsWith("right") ? -1 : 1;
            var hazards = new List<Collider2D>();
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                if (dh.gameObject.scene.name == room)
                    foreach (var hc in dh.GetComponents<Collider2D>()) hazards.Add(hc);
            var rb = hero.GetComponent<Rigidbody2D>();
            var box = hero.GetComponent<BoxCollider2D>();
            Vector2 size = box.bounds.size;
            // The hazards' own shapes (a thorn wall's bounding box would cover the whole way).
            var hazardSet = new HashSet<Collider2D>(hazards);
            bool Blocked(Vector2 p)
            {
                foreach (var hit in Physics2D.OverlapBoxAll(p, size * 1.2f + new Vector2(0.3f, 0.3f), 0f))
                    if (hit.gameObject.layer == ChunkColliders.TerrainLayer || hazardSet.Contains(hit)) return true;
                return false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛之路：危险物 {hazards.Count} 个，往{(dir < 0 ? "左" : "右")}飞");
            yield return new WaitForSeconds(0.6f);
            StartRec("flight_pathofpain");
            Vector2 start = box.bounds.center;
            Player.TerraMovement.DevJump = true;
            Player.TerraMovement.DevGroundJump(hero);
            float t0 = Time.time, far = 0f;
            int hits = 0;
            while (Time.time - t0 < 14f && !gm.IsInSceneTransition && gm.sceneName == room)
            {
                PlayerData.instance.isInvincible = true;
                Vector2 p = box.bounds.center;
                // The first clear heading in a fan around straight ahead (upward first on a tie).
                Vector2 way = new Vector2(dir, 0f);
                bool found = false;
                foreach (float deg in new[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 85f, -85f, 110f, -110f })
                {
                    var d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad) * dir, Mathf.Sin(deg * Mathf.Deg2Rad));
                    bool clear = true;
                    for (int s = 1; s <= 8 && clear; s++)
                        if (Blocked(p + d * (0.4f * s))) clear = false;
                    if (clear) { way = d; found = true; break; }
                }
                if (!found) hits++;
                float vy = rb.linearVelocity.y;
                Player.TerraMovement.DevMove = !found ? 0 : way.x > 0.35f ? 1 : way.x < -0.35f ? -1 : 0;
                Player.TerraMovement.DevJump = !found || way.y > 0.3f || (way.y > -0.3f && vy < -0.5f);
                AimAt(p + way * 4f);
                far = Mathf.Max(far, (p.x - start.x) * dir);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.5f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 苦痛之路：飞了 {far:F1} 格远，{Time.time - t0:F1}s，没路的帧 {hits}，现在 {gm.sceneName} {(Vector2)hero.transform.position:F1}");
        }

        /// <summary>
        /// The stations set down on Dirtmouth's street from a plain (not creative) inventory: each in the
        /// first spot beside the hero it fits, then the inventory open on what they let you craft.
        /// </summary>
        IEnumerator PromoStations(HeroController hero)
        {
            var inv = mod.Inventory;
            yield return EnterRoom("Town");
            var town = TerrainSystem.Find("Town");
            var origin = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            hero.transform.position = Clear(hero, origin + new Vector2(150f, 9f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            mod.ApplyCreative(false);
            mod.SetInfiniteItems(false);
            inv.Clear();
            inv.Slots[20] = ItemStack.Of(ItemType.Wood, 300);
            inv.Slots[21] = ItemStack.Of(ItemType.StoneBlock, 99);
            inv.Slots[22] = ItemStack.Of(ItemType.Gel, 30);
            inv.Slots[23] = ItemStack.Of(ItemType.IronBar, 30);
            inv.Slots[1] = ItemStack.Of(ItemType.WorkBench, 1);
            inv.Slots[2] = ItemStack.Of(ItemType.Furnace, 1);
            inv.Slots[3] = ItemStack.Of(ItemType.IronAnvil, 1);
            yield return null;
            int placed0 = Stations.DevPlaced;
            StartRec("stations");
            yield return new WaitForSeconds(0.4f);
            var center = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            foreach (var (slot, kind, side) in new[] { (1, StationKind.WorkBench, 1), (2, StationKind.Furnace, -1), (3, StationKind.IronAnvil, 1) })
            {
                Vector2? spot = null;
                for (float dx = 1f; dx <= 4.5f && spot == null; dx += 0.25f)
                {
                    var aim = center + new Vector2(side * dx, -0.3f);
                    if (Stations.FindSpot(kind, hero, aim, out _)) spot = aim;
                }
                inv.Selected = slot;
                if (spot == null) continue;
                AimAt(spot.Value);
                yield return new WaitForSeconds(0.35f);
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(spot.Value);
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(0.4f);
            InventoryGui.Open = true;
            yield return new WaitForSeconds(3f);
            InventoryGui.Open = false;
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制 工作站：放下 {Stations.DevPlaced - placed0} 个，{Stations.DevDescribe()}");
            mod.ApplyCreative(true);
        }
    }
}
