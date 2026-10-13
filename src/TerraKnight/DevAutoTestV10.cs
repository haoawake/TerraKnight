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
            yield return BorderTest();
            yield return BaldurTest();
            yield return MapIslandTest();
            BossStatsReport();
            mod.DevNoFallDamage = false;
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
