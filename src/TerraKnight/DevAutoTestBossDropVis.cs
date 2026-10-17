using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossdropvis", or "bossdropvis_Key1.Key2"): every world arena's boss fought through its seal and struck
    /// down through the mod's own hits (its own death, cutscene and all), nothing picked up; then each drop it left: drawn,
    /// on a camera, in the view, covered by the scenery in front of it, how lit its spot is. Photographed.
    /// Dreams and Godhome are left out: their loot goes into the bag by design (EnemyDrops.LeavesOnItsOwn).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossDropVisTest(string only)
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 首领掉落可见需要隔离存档，不通过"); yield break; }
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite, dreamNail = PlayerData.instance.hasDreamNail;
            int stage = Shop.DevStage, selected = inv.Selected;
            int crystals = mod.Stats.LifeCrystals, fruits = mod.Stats.LifeFruits;
            var kills = new Dictionary<string, int>(Bestiary.Kills);
            int ok = 0, all = 0, drops = 0, hidden = 0;
            var bad = new List<string>();
            void Check(string key, bool pass, string detail)
            {
                all++;
                if (pass) ok++; else if (!bad.Contains(key)) bad.Add(key);
                Plugin.Log.LogInfo($"[AutoTest] 首领掉落可见 {key} {detail} {(pass ? "通过" : "不通过")}");
            }
            var keys = new HashSet<string>();
            if (only != null) foreach (var k in only.Split('.')) keys.Add(k);
            else foreach (var k in BossArena.DevKeys()) if (BossArena.DevWorldArena(k, out _, out _)) keys.Add(k);
            // The ending's fight leaves the world by itself.
            if (only == null) keys.Remove("HollowKnight");
            bool alive = true;
            IEnumerator Alive()
            {
                while (alive)
                {
                    mod.Stats.FullHeal();
                    if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
                    yield return null;
                }
            }
            mod.StartCoroutine(Alive());
            try
            {
                yield return SealTravel("Town", "left1");
                inv.Infinite = false;
                PlayerData.instance.hasDreamNail = true;
                mod.Stats.LifeCrystals = TerraKnight.Player.TerraStats.MaxLifeCrystals;
                mod.Stats.LifeFruits = 20;
                WorldItems.DevNoGrab = true;
                foreach (var s in BossSummons.All)
                {
                    if (!keys.Contains(s.Key)) continue;
                    string record = s.Key.Split('@')[0];
                    Bestiary.Kills[s.Key] = 1;
                    Shop.DevStage = s.Stage;
                    inv.Slots[0] = ItemStack.Of(s.Type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    for (float ready = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready;) yield return null;
                    if (!GameHooks.PlayerHasControl() && HeroController.instance != null) { HeroController.instance.RegainControl(); HeroController.instance.StartAnimationControl(); }
                    string why = BossSummons.Refusal(s);
                    if (why != null) { Check(s.Key, false, "不能使用：" + why); continue; }
                    WorldItems.DevClear();
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    for (float there = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < there;) yield return null;
                    if (!BossArena.Arrived(s.Key)) { Check(s.Key, false, "没进竞技场"); continue; }
                    string scene = GameManager.instance.sceneName;
                    var h = HeroController.instance;
                    // Struck until the game has it beaten, as BossRun does: the book counted it and none of it is left standing
                    // (the Mantis Lords, the Watcher Knights); its words turned with jump; struck whether or not the player is held.
                    var parts = new List<HealthManager>();
                    bool beaten = false;
                    int strikes = 0, kills0 = KillsOf(record);
                    float nextHit = 0f, nextTalk = 0f;
                    for (float end = Time.realtimeSinceStartup + 150f; Time.realtimeSinceStartup < end && !beaten;)
                    {
                        if (GameManager.instance.sceneName != scene) break;
                        parts.Clear();
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (hm != null && (UI.EnemyHover.RecordOf(hm) == record || hm.name == "Head" && hm.gameObject.scene.name.StartsWith(scene))) parts.Add(hm);
                        if (KillsOf(record) > kills0 && !parts.Exists(hm => !hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm))) { beaten = true; break; }
                        if (!GameHooks.PlayerHasControl() && UI.HudDialogue.DevOpen && Time.time >= nextTalk)
                        {
                            nextTalk = Time.time + 1f;
                            yield return Tap(UnityEngine.InputSystem.Key.Space);
                        }
                        if (Time.time >= nextHit)
                        {
                            nextHit = Time.time + 0.35f;
                            foreach (var hm in parts)
                            {
                                if (hm.isDead || EnemyStats.Defeated(hm) || !hm.gameObject.activeInHierarchy) continue;
                                var col = hm.GetComponent<Collider2D>();
                                if (col == null || !col.enabled)
                                {
                                    col = null;
                                    foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                                        if (c.enabled && c.GetComponentInParent<HealthManager>() == hm) { col = c; break; }
                                }
                                if (col != null && mod.Melee.HitWith(col, hm.name == "Head" ? 5000 : 1500, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center)) strikes++;
                            }
                        }
                        yield return null;
                    }
                    if (!beaten)
                    {
                        var left = new StringBuilder();
                        foreach (var hm in parts) left.Append($" [{hm.name} 血{hm.hp} 死{(hm.isDead ? 1 : 0)} 开{(hm.gameObject.activeInHierarchy ? 1 : 0)}{FsmStates(hm.gameObject, false)}]");
                        Check(s.Key, false, $"没打完（打了 {strikes} 下，击杀 {KillsOf(record) - kills0}，场景 {GameManager.instance.sceneName}，可操作 {GameHooks.PlayerHasControl()}）：{left}");
                        yield return SealTravel("Town", "left1");
                        continue;
                    }
                    // The death played out and the loot come to rest.
                    for (float settle = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < settle;) yield return null;
                    var cam = Camera.main;
                    var lines = new StringBuilder();
                    int n = 0, unseen = 0;
                    foreach (var sr in WorldItems.DevRenderers)
                    {
                        if (sr == null) continue;
                        n++;
                        Vector2 c = sr.bounds.center;
                        Vector3 vp = cam != null ? cam.WorldToViewportPoint(c) : Vector3.zero;
                        bool inView = vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
                        string over = CoveredBy(sr, out bool overLayer);
                        float light = World.LightMap.DevLightAt(c);
                        // What a layer over the game's own covers (its foreground, a death's flash, a haze) covers the player
                        // too: not the drops'. One far from the player on another floor (the False Knight's fall through
                        // his floor, the Collector's down from his ceiling) is out of the view, not unseen.
                        float far = Vector2.Distance(c, h.transform.position);
                        bool seen = far > 12f || sr.enabled && sr.isVisible && sr.color.a > 0.5f && (over == null || overLayer);
                        if (!seen) unseen++;
                        if (lines.Length < 1200 || !seen)
                            lines.Append($" [{sr.name.Replace("Terra World Item ", "")} {c:F1} 画{(sr.enabled ? 1 : 0)} 相机{(sr.isVisible ? 1 : 0)} 画面内{(inView ? 1 : 0)} 透明{sr.color.a:F1} 亮{light:F2} 离主角{far:F0}{(over != null ? (overLayer ? " 在 " + over + " 后面（前景，主角一样）" : " 被 " + over + " 挡住") : "")} 场景 {sr.gameObject.scene.name} 激活{(sr.gameObject.activeInHierarchy ? 1 : 0)} 层{sr.gameObject.layer}/{sr.sortingLayerName}/{sr.sortingOrder} z{sr.transform.position.z:F2} 根 {PathOf(sr.transform.parent)}（{(sr.transform.parent != null && sr.transform.parent.gameObject.activeInHierarchy ? "开" : "关")}） 相机z {(cam != null ? cam.transform.position.z : 0f):F1} 遮罩{(cam != null ? cam.cullingMask : 0)}]");
                    }
                    drops += n;
                    hidden += unseen;
                    Check(s.Key, n > 0 && unseen == 0, $"{scene} 打了 {strikes} 下打完；主角 {(Vector2)h.transform.position:F1}；掉落 {n} 个，看不见 {unseen}：{lines}");
                    Shot(scene, "bossdropvis_" + s.Key.Replace("@", "_"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    WorldItems.DevClear();
                    for (float ctl = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return null;
                    yield return SealTravel("Town", "left1");
                    for (float clean = Time.realtimeSinceStartup + 10f; (BossArena.Visiting || BossSummons.Pending) && Time.realtimeSinceStartup < clean;) yield return null;
                }
            }
            finally
            {
                alive = false;
                WorldItems.DevNoGrab = false;
                System.Array.Copy(saved, inv.Slots, saved.Length);
                inv.Selected = selected;
                inv.Infinite = infinite;
                Shop.DevStage = stage;
                mod.Stats.LifeCrystals = crystals;
                mod.Stats.LifeFruits = fruits;
                PlayerData.instance.hasDreamNail = dreamNail;
                Bestiary.Kills.Clear();
                foreach (var kv in kills) Bestiary.Kills[kv.Key] = kv.Value;
                Plugin.Log.LogInfo($"[AutoTest] 首领掉落可见 合计 {ok}/{all}，掉落 {drops} 个，看不见 {hidden} 个 {(ok == all ? "通过" : "不通过：" + string.Join("、", bad))}");
            }
        }

        /// <summary>The scenery renderer drawn over a drop (in front of it and later in the draw order), or null.</summary>
        static string CoveredBy(SpriteRenderer sr, out bool overLayer)
        {
            overLayer = false;
            Vector2 c = sr.bounds.center;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.isVisible || r == sr || r is ParticleSystemRenderer) continue;
                if (r.name.StartsWith("Terra") || r.transform.root.name.StartsWith("Terra") || r.gameObject.layer == 9) continue;
                if (r.name.StartsWith("Masker")) continue;
                string lower = r.name.ToLowerInvariant();
                if (lower.Contains("light") || lower.Contains("glow")) continue;
                var b = r.bounds;
                if (c.x < b.min.x || c.x > b.max.x || c.y < b.min.y || c.y > b.max.y) continue;
                if (b.size.x > 60f || b.size.y > 40f) continue;
                bool later = r.sortingLayerID == sr.sortingLayerID ? (r.sortingOrder > sr.sortingOrder || r.sortingOrder == sr.sortingOrder && r.transform.position.z < sr.transform.position.z - 0.01f)
                                                                    : SortingLayer.GetLayerValueFromID(r.sortingLayerID) > SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
                if (!later) continue;
                overLayer = r.sortingLayerID != sr.sortingLayerID;
                return $"{r.name}（层 {SortingLayer.IDToName(r.sortingLayerID)}/{r.sortingOrder}，z {r.transform.position.z:F2}）";
            }
            return null;
        }
    }
}
