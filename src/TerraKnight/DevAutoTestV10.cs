using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "v10"): the 1.0 fixes. Dirtmouth's sky is the world's edge (World/WorldBorder)
    /// while its gate to King's Pass still leads on; an Elder Baldur shut in its shell is hurt by
    /// arrows and chased by minions; the map keeps following the player where no gate joins a room
    /// to the rest (the White Palace) and in Greenpath's door from the Crossroads, and shows the rooms
    /// behind doors only from inside; the bosses have their Terraria stand-ins' life.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator V10Test(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            yield return JuggleTest(hero);
            yield return BorderTest();
            yield return BaldurTest();
            yield return MapIslandTest();
            BossStatsReport();
            mod.DevNoFallDamage = false;
        }

        /// <summary>
        /// NPC.StrikeNPC: hits on an enemy already thrown up throw it no faster than one hit does (a
        /// big hit adds the push up to the push; a small one sets a smaller speed), and a hit on one
        /// falling only slows its fall by the lift. Three runs for a small hit and a big one: one hit;
        /// five a tenth of a second apart (the fastest rise seen, against one hit's); one hit, then
        /// one more once it falls (how fast it goes after).
        /// </summary>
        IEnumerator JuggleTest(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            HealthManager target = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || EnemyStats.Unstoppable(EnemyStats.Of(hm)) || TerraKnockback.Immune(hm)) continue;
                var body = hm.GetComponent<Rigidbody2D>();
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic || body.gravityScale <= 0.01f) continue;
                target = hm;
                break;
            }
            if (target == null) { Plugin.Log.LogInfo("[AutoTest] 击飞叠加：这里没有能打飞的敌人"); yield break; }
            var ecol = target.GetComponent<Collider2D>();
            var trb = target.GetComponent<Rigidbody2D>();
            var info = EnemyStats.Of(target);
            float kb = ItemDb.Get(ItemType.CopperBroadsword).Knockback;
            foreach (int dmg in new[] { 5, 100000 })
            {
                var peak = new float[3];
                var rise = new float[3];
                float afterFall = 0f, fallSpeed = 0f;
                for (int run = 0; run < 3; run++)
                {
                    if (target == null || target.isDead) break;
                    target.hp = Mathf.Max(target.hp, 9999);
                    // Landed and settled first.
                    float settle = Time.time + 3f;
                    while (Time.time < settle && (target.GetComponent<KnockFlight>()?.Active ?? false)) yield return null;
                    yield return new WaitForSeconds(0.3f);
                    var eb = ecol.bounds;
                    hero.transform.position += (Vector3)(new Vector2(eb.min.x - col.bounds.extents.x - 0.35f, eb.min.y + col.bounds.extents.y + 0.05f) - (Vector2)col.bounds.center);
                    yield return null;
                    float start = target.transform.position.y;
                    int total = run == 1 ? 5 : 1, done = 0;
                    bool second = false;
                    for (float t = 0f; t < 1.5f && target != null; t += Time.deltaTime)
                    {
                        while (done < total && done <= (int)(t / 0.1f))
                        {
                            target.hp = Mathf.Max(target.hp, 9999);
                            mod.Melee.HitWith(ecol, dmg, kb, 0f, 1f, col.bounds.center);
                            done++;
                        }
                        // The third run: hit again once it has been falling a while.
                        if (run == 2 && !second && t > 0.15f && trb.linearVelocity.y < -2f)
                        {
                            second = true;
                            fallSpeed = trb.linearVelocity.y;
                            target.hp = Mathf.Max(target.hp, 9999);
                            mod.Melee.HitWith(ecol, dmg, kb, 0f, 1f, col.bounds.center);
                            yield return new WaitForFixedUpdate();
                            yield return new WaitForFixedUpdate();
                            afterFall = trb.linearVelocity.y;
                        }
                        peak[run] = Mathf.Max(peak[run], target.transform.position.y - start);
                        rise[run] = Mathf.Max(rise[run], trb.linearVelocity.y);
                        yield return null;
                    }
                }
                bool big = dmg * (GameDifficulty.Current >= DifficultyLevel.Expert ? 15 : 10) > EnemyStats.LifeMax(info);
                Plugin.Log.LogInfo($"[AutoTest] 击飞叠加 {info.Name}（{(big ? "大伤害" : "小伤害")} {dmg}）：挨一下 上升最快 {rise[0]:F2} 最高 {peak[0]:F2}；" +
                                   $"上升中连挨五下 上升最快 {rise[1]:F2}（不应超过挨一下的） 最高 {peak[1]:F2}；" +
                                   $"下落中（{fallSpeed:F2}）再挨一下后 {afterFall:F2}（应是下落速度加上抬升，低于 {rise[0]:F2}，不应重新抛到满速）");
            }
        }

        static TerrainRegion RegionOfHero(HeroController hero)
        {
            var cell = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            return TerraWorld.RegionAt(cell.x, cell.y);
        }

        IEnumerator BorderTest()
        {
            yield return GoTo("Town", "left1");
            var hero = HeroController.instance;
            yield return new WaitForSeconds(2.5f);
            var region = RegionOfHero(hero);
            if (region == null) { Plugin.Log.LogInfo("[AutoTest] 空气墙：德特茅斯没有格子"); yield break; }
            const float c = CellGrid.CellSize;
            float top = region.CellRect.yMax * c;
            var rb = hero.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            // Up as wings would take it, from the open air over the town.
            hero.transform.position = Clear(hero, new Vector2(hero.transform.position.x + 12f, top - 8f));
            Physics2D.SyncTransforms();
            int stops0 = WorldBorder.DevStops;
            float highest = float.MinValue;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                rb.linearVelocity = new Vector2(0f, 20f);
                highest = Mathf.Max(highest, col.bounds.max.y);
                yield return null;
            }
            var after = RegionOfHero(hero);
            Plugin.Log.LogInfo($"[AutoTest] 空气墙 德特茅斯上方：场景顶 {top:F1}，飞到最高 {highest:F2}（应不超过顶），挡下 {WorldBorder.DevStops - stops0} 次（{WorldBorder.DevLast}），之后在 {after?.SceneName ?? "世界外"}（应为 Town）");
            Shot("Town", "v10_1_sky_edge");
            yield return new WaitForSeconds(0.3f);

            // The way to King's Pass is a gate: walking out of the town's left side still leads on.
            Transform gate = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.gameObject.scene.name == "Town" && tp.name == "left1") gate = tp.transform;
            if (gate == null) { Plugin.Log.LogInfo("[AutoTest] 空气墙：德特茅斯没有 left1 门"); yield break; }
            hero.transform.position = Clear(hero, (Vector2)gate.position + new Vector2(4f, 0f));
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(0.5f);
            string before = RegionOfHero(hero)?.SceneName;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                if (GameManager.instance.IsInSceneTransition) break;
                rb.linearVelocity = new Vector2(-9f, rb.linearVelocity.y);
                if (RegionOfHero(hero)?.SceneName != "Town") break;
                yield return null;
            }
            float wait = Time.time + 8f;
            while ((GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            yield return new WaitForSeconds(0.5f);
            hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 空气墙 德特茅斯左门：从 {before} 往左走，到了 {RegionOfHero(hero)?.SceneName ?? "世界外"}（场景 {GameManager.instance.sceneName}；应为 Tutorial_01）");
        }

        IEnumerator BaldurTest()
        {
            yield return GoTo("Crossroads_11_alt", "right1");
            var hero = HeroController.instance;
            yield return new WaitForSeconds(3f);
            var live = TerraWorld.Live;
            Plugin.Log.LogInfo($"[AutoTest] 苍绿之径入口 Crossroads_11_alt：地形 {TerrainSystem.Current?.SceneName ?? "无"}，格子区域 {live?.SceneName ?? "无"}，地图 {UI.WorldMap.DevReport()}");
            HealthManager baldur = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!hm.isDead && hm.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(hm) == "Blocker") { baldur = hm; break; }
            if (baldur == null) { Plugin.Log.LogInfo("[AutoTest] 巴德尔长者：没找到"); yield break; }
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 at = baldur.transform.position;
            float side = hero.transform.position.x < at.x ? -1f : 1f;
            hero.transform.position = Clear(hero, at + new Vector2(side * 6f, 0f));
            Physics2D.SyncTransforms();
            // Close enough for it to shut itself in.
            yield return new WaitForSeconds(2f);
            var parts = new System.Text.StringBuilder();
            foreach (var c in baldur.GetComponentsInChildren<Collider2D>(true))
                parts.Append($" {c.name}:{LayerMask.LayerToName(c.gameObject.layer)}:{(c.isTrigger ? "触发" : "实心")}:{(c.enabled && c.gameObject.activeInHierarchy ? "开" : "关")}:命中层{((MeleeHits.HitMask >> c.gameObject.layer) & 1) == 1}");
            Plugin.Log.LogInfo($"[AutoTest] 巴德尔长者 碰撞体：{parts}");
            Plugin.Log.LogInfo($"[AutoTest] 巴德尔长者：无敌 {baldur.IsInvincible}，缩壳 {EnemyStats.Shelled(baldur)}，打不到 {EnemyStats.Untouchable(baldur)}（应为 False），血 {baldur.hp}");

            var inv = mod.Inventory;
            int slot = Inventory.HotbarSize - 1, ammoSlot = Inventory.Size - 1;
            var saved = inv.Slots[slot];
            var savedAmmo = inv.Slots[ammoSlot];
            int savedSelected = inv.Selected;
            inv.Slots[ammoSlot] = ItemStack.Of(ItemType.WoodenArrow, 200);
            int hp0 = baldur.hp;
            baldur.hp = Mathf.Max(baldur.hp, 2000);
            hp0 = baldur.hp;
            for (int n = 0; n < 8; n++)
            {
                inv.Slots[slot] = ItemStack.Of(ItemType.WoodenBow, 1);
                inv.Selected = slot;
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(baldur.transform.position);
                yield return new WaitForSeconds(0.35f);
            }
            yield return new WaitForSeconds(0.8f);
            Plugin.Log.LogInfo($"[AutoTest] 巴德尔长者 缩壳时用木弓射 8 箭：血 {hp0} -> {baldur.hp}（应减少），还无敌 {baldur.IsInvincible}");
            Shot("Crossroads_11_alt", "v10_2_baldur_arrows");
            yield return null;

            foreach (var staff in new[] { ItemType.SlimeStaff, ItemType.CattivaStaff, ItemType.FoxsparksStaff })
            {
                Minions.Instance.Clear();
                PlayerBuffs.Clear();
                baldur.hp = Mathf.Max(baldur.hp, 2000);
                int before = baldur.hp, hits0 = Minions.DevHits;
                inv.Slots[slot] = ItemStack.Of(staff, 1);
                inv.Selected = slot;
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(baldur.transform.position);
                for (float t = 0f; t < 10f; t += Time.deltaTime)
                {
                    baldur.hp = Mathf.Max(baldur.hp, 500);
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 巴德尔长者 {ItemDb.Get(staff).Name}：10 秒召唤物命中 {Minions.DevHits - hits0} 次（应大于 0），血 {before} -> {baldur.hp}");
                if (staff == ItemType.CattivaStaff) Shot("Crossroads_11_alt", "v10_3_baldur_minions");
            }
            Minions.Instance.Clear();
            PlayerBuffs.Clear();
            inv.Slots[slot] = saved;
            inv.Slots[ammoSlot] = savedAmmo;
            inv.Selected = savedSelected;
            _ = col;
        }

        IEnumerator MapIslandTest()
        {
            yield return GoTo("White_Palace_01", "left1");
            var hero = HeroController.instance;
            yield return new WaitForSeconds(3f);
            string r0 = UI.WorldMap.DevReport();
            var rb = hero.GetComponent<Rigidbody2D>();
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                rb.linearVelocity = new Vector2(8f, rb.linearVelocity.y);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 地图 白色宫殿：走之前 {r0}");
            Plugin.Log.LogInfo($"[AutoTest] 地图 白色宫殿：往右走 1.5 秒后 {UI.WorldMap.DevReport()}（头像应定位到、位置变了）");
            Shot("White_Palace_01", "v10_4_palace_minimap");
            yield return null;
            foreach (var room in new[] { "Room_shop", "Room_Sly_Storeroom", "Grimm_Main_Tent", "Town" })
                Plugin.Log.LogInfo($"[AutoTest] 地图 {room}：门后房间 {UI.WorldMap.DevDoorRoom(room)}，现在画出 {UI.WorldMap.DevShown(room)}");
        }

        static void BossStatsReport()
        {
            foreach (var rec in new[] { "FalseKnight", "Hornet", "DungDefender", "MantisLord", "BlackKnight", "Hornet@4", "InfectedKnight", "HollowKnight", "Nailsage", "FinalBoss", "FalseKnight@5" })
            {
                var s = Bestiary.StatsOf(rec);
                var r = BossStats.Of(rec, EnemyDrops.StageOf(rec));
                Plugin.Log.LogInfo($"[AutoTest] 首领数值 {Bestiary.NameOf(rec)}（{rec}）= 泰拉 {r?.Boss.Name ?? "无"}：图鉴生命 {s.Life}，防御 {s.Defense}（{GameDifficulty.NameOf(GameDifficulty.Current)}）");
            }
        }
    }
}
