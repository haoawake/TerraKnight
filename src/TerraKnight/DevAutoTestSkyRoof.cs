using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "skyroof", the user's 11-01 "让代达罗斯弓，血雨弓，星怒，群星之怒等从上往下攻击的武器忽略上方的攻击直达鼠标位置，别被天花板挡住"):
    /// under a roof (TK_SKY_ROOM, Crossroads_01 by default), an enemy held still beside the Knight with the cursor on it,
    /// and each weapon that brings its shots down from the sky (TK_SKY_ITEMS) used at it for TK_SKY_SECONDS (4): its shots
    /// made, how many struck the enemy, how many frames went through blocks. As now, then as before 11-01
    /// (TerraProjectiles.DevSkyOld). Lines "[AutoTest] 天降 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SkyRoofTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 天降 {what} {(pass ? "通过" : "不通过")}");
            }
            string room = Environment.GetEnvironmentVariable("TK_SKY_ROOM") ?? "Crossroads_01";
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_SKY_SECONDS"), out var sec) ? sec : 4f;
            var items = (Environment.GetEnvironmentVariable("TK_SKY_ITEMS") ?? "DaedalusStormbow,BloodRainBow,Starfury,StarWrath,MeteorStaff,BlizzardStaff,LunarFlareBook")
                .Split(',').Select(n => (ItemType)Enum.Parse(typeof(ItemType), n)).ToArray();
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            var st = mod.Stats;
            bool infinite = inv.Infinite;
            inv.Infinite = true;
            st.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            st.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            var kinds = (ProjKind[])Enum.GetValues(typeof(ProjKind));
            int Hits() => kinds.Sum(k => TerraProjectiles.DevHit(k));
            int Made() => kinds.Sum(k => TerraProjectiles.DevMade(k));
            try
            {
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(1f);
                var hero = HeroController.instance;
                PlayerData.instance.isInvincible = true;
                BookSpawner.Spawn("Crawler");
                HealthManager foe = null;
                for (float until = Time.time + 16f; Time.time < until && foe == null;)
                {
                    foe = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .FirstOrDefault(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.name.Contains("图鉴") && UI.EnemyHover.RecordOf(h) == "Crawler");
                    yield return null;
                }
                if (foe == null) { Check(false, "没生成靶子"); yield break; }
                // The Knight on his floor, the enemy 6 to his side on the same floor; what roof is over it.
                var box = hero.GetComponent<BoxCollider2D>();
                Vector2 stand = hero.transform.position;
                float feet = box.bounds.min.y;
                var spot = new Vector2(stand.x + 6f, feet + 0.6f);
                if (World.TerraWorld.IsSolidAt(spot) || Physics2D.Linecast(box.bounds.center, spot, 1 << World.ChunkColliders.TerrainLayer)) spot.x = stand.x - 6f;
                var roof = Physics2D.Raycast(spot, Vector2.up, 60f, 1 << World.ChunkColliders.TerrainLayer);
                float roofAt = roof.collider != null ? roof.distance : -1f;
                Plugin.Log.LogInfo($"[AutoTest] 天降 {room}：主角 {stand:F1}，靶子在 {spot:F1}，头顶岩顶 {(roofAt >= 0f ? $"{roofAt:F1} 格单位高" : "没有")}");
                int hp = foe.hp;
                foreach (var type in items)
                {
                    var def = ItemDb.Get(type);
                    if (def == null) { Check(false, $"{type} 没有这件物品"); continue; }
                    var results = new (int Made, int Hits, int Through)[2];
                    foreach (bool old in new[] { true, false })
                    {
                        TerraProjectiles.DevSkyOld = old;
                        for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                        inv.Slots[0] = ItemStack.Of(type);
                        if (def.UseAmmo != AmmoKind.None)
                        {
                            var ammo = ItemDb.All.Where(a => a != null && ItemDb.Get(a.Type) == a && a.Ammo == def.UseAmmo).OrderBy(a => a.Damage).ThenBy(a => (int)a.Type).FirstOrDefault();
                            if (ammo != null) inv.Slots[1] = ItemStack.Of(ammo.Type, ammo.MaxStack);
                        }
                        inv.Selected = 0;
                        inv.Override = -1;
                        int made0 = Made(), hits0 = Hits(), through0 = TerraProjectiles.DevSkyThrough;
                        mod.DevHold = true;
                        for (float end = Time.time + seconds; Time.time < end;)
                        {
                            hero.transform.position = new Vector3(stand.x, stand.y, hero.transform.position.z);
                            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                            if (foe != null && !foe.isDead)
                            {
                                var c = foe.GetComponent<Collider2D>();
                                float lift = c != null ? foe.transform.position.y - c.bounds.min.y : 0.5f;
                                foe.transform.position = new Vector3(spot.x, feet + lift, foe.transform.position.z);
                                var rb = foe.GetComponent<Rigidbody2D>();
                                if (rb != null) rb.linearVelocity = Vector2.zero;
                                foe.hp = Mathf.Max(foe.hp, hp);
                            }
                            st.Mana = st.ManaMax;
                            mod.Stats.FullHeal();
                            var aim = foe != null ? TerraProjectiles.EnemyCentre(foe) : spot;
                            mod.DevAim = aim;
                            if (mod.DevReady && !mod.IsUsingItem && !BladeSkills.HoldsLeft) mod.DevUseSelected(aim);
                            yield return null;
                        }
                        mod.DevHold = null;
                        mod.DevAim = null;
                        // Shots still on their way land.
                        yield return new WaitForSeconds(1.5f);
                        results[old ? 0 : 1] = (Made() - made0, Hits() - hits0, TerraProjectiles.DevSkyThrough - through0);
                        TerraProjectiles.DevSkyOld = false;
                    }
                    var o = results[0];
                    var n = results[1];
                    Plugin.Log.LogInfo($"[AutoTest] 天降 {def.Name}：以前 出 {o.Made} 发 打中 {o.Hits}；现在 出 {n.Made} 发 打中 {n.Hits}，穿过方块 {n.Through} 帧");
                    Check(n.Made > 0 && n.Hits > 0 && n.Hits >= o.Hits, $"{def.Name} 穿过天花板打到鼠标处（打中 {o.Hits} → {n.Hits}）");
                }
            }
            finally
            {
                TerraProjectiles.DevSkyOld = false;
                mod.DevHold = null;
                mod.DevAim = null;
                inv.Infinite = infinite;
                Player.TerraStats.DevImmortal = false;
                Plugin.Log.LogInfo($"[AutoTest] 天降 合计 {ok}/{all}");
            }
        }
    }
}
