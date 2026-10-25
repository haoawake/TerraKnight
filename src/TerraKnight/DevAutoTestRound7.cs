using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "garpede", "killables"): the user's 2026-10-21 seventh list: the White Palace's Wingmoulds and Deepnest's
    /// Garpede, unkillable in the game, made killable. "garpede" looks at how the Garpede is put together; "killables"
    /// strikes both down.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Round7(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            switch (suite)
            {
                case "garpede": yield return GarpedeProbe(); break;
                case "killables": yield return KillablesTest(); break;
                case "itemdump": ItemDump(); break;
            }
            EndNailTest();
            mod.Inventory.Slots[0] = default;
            Shop.DevStage = 0;
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        static string Components(GameObject go)
        {
            var names = new List<string>();
            foreach (var c in go.GetComponents<Component>()) if (c != null && !(c is Transform)) names.Add(c.GetType().Name);
            return string.Join("、", names);
        }

        void DumpTree(Transform t, int depth, System.Text.StringBuilder sb)
        {
            sb.Append($"\n{new string(' ', depth * 2)}- {t.name}{(t.gameObject.activeInHierarchy ? "" : "(未激活)")} 层{t.gameObject.layer} 位置 {(Vector2)t.position:F1}：{Components(t.gameObject)}");
            var hm = t.GetComponent<HealthManager>();
            if (hm != null) sb.Append($" ｜血 {hm.hp} 无敌 {hm.IsInvincible}/{hm.InvincibleFromDirection} 死了 {hm.isDead} 标签 {t.tag}");
            var dh = t.GetComponent<DamageHero>();
            if (dh != null) sb.Append($" ｜伤害 {dh.damageDealt} 类型 {dh.hazardType}");
            foreach (var c in t.GetComponents<Collider2D>()) sb.Append($" ｜{c.GetType().Name} 触发 {c.isTrigger} 开 {c.enabled} {(Vector2)c.bounds.size:F2}");
            if (depth < 4) for (int i = 0; i < t.childCount && i < 40; i++) DumpTree(t.GetChild(i), depth + 1, sb);
        }

        void DumpFsms(GameObject go)
        {
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                var sb = new System.Text.StringBuilder($"[AutoTest] 护符{charmTag} 状态机 {PathOf(f.transform)} {f.FsmName}（现在 {f.ActiveStateName}）");
                foreach (var v in f.FsmVariables.IntVariables) sb.Append($" int {v.Name}={v.Value}");
                foreach (var v in f.FsmVariables.BoolVariables) sb.Append($" bool {v.Name}={v.Value}");
                foreach (var e in f.FsmGlobalTransitions) sb.Append($"\n    全局 {e.EventName} → {e.ToState}");
                foreach (var s in f.FsmStates)
                {
                    sb.Append($"\n    {s.Name}：");
                    foreach (var act in s.Actions) sb.Append(act.GetType().Name).Append(DescribeAction(act)).Append(' ');
                    foreach (var tr in s.Transitions) sb.Append($" [{tr.EventName} → {tr.ToState}]");
                }
                Plugin.Log.LogInfo(sb.ToString());
            }
        }

        IEnumerator GarpedeProbe()
        {
            Shop.DevStage = 3;
            yield return GoTo("Deepnest_44", EntryGateOf("Deepnest_44"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var names = new List<string>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hm != null) names.Add(hm.name + (hm.gameObject.activeInHierarchy ? "" : "(未激活)"));
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 场景里的 HealthManager：{string.Join("、", names)}");
            var roots = new List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || !t.gameObject.scene.IsValid() || t.gameObject.scene.name != "Deepnest_44") continue;
                string n = t.name.ToLowerInvariant();
                if (!(n.Contains("centipede") || n.Contains("garpede"))) continue;
                // Only the highest of a chain of such names.
                var p = t.parent;
                bool under = false;
                for (; p != null; p = p.parent) if (p.name.ToLowerInvariant().Contains("centipede")) under = true;
                if (!under) roots.Add(t);
            }
            CharmCheck("找到加皮德", roots.Count > 0, $"{roots.Count} 个");
            foreach (var r in roots)
            {
                var sb = new System.Text.StringBuilder($"[AutoTest] 护符{charmTag} 加皮德 {PathOf(r)}");
                DumpTree(r, 0, sb);
                Plugin.Log.LogInfo(sb.ToString());
                DumpFsms(r.gameObject);
            }
            if (roots.Count == 0) yield break;
            // Struck: a sword at its nearest part with a collider.
            Collider2D part = null;
            float best = float.MaxValue;
            foreach (var r in roots)
                foreach (var c in r.GetComponentsInChildren<Collider2D>())
                {
                    if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
                    float d = Vector2.Distance(c.bounds.center, hero.transform.position);
                    if (d < best) { best = d; part = c; }
                }
            if (part == null) yield break;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 打 {PathOf(part.transform)}（{part.GetType().Name} 层 {part.gameObject.layer}）");
            InHand(ItemType.NightsEdge);
            int blocked = MeleeHits.DevBlocked;
            for (int k = 0; k < 6; k++)
            {
                if (part == null) break;
                hero.transform.position = Clear(hero, (Vector2)part.bounds.center + Vector2.left * 2f);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return UseAt(part.bounds.center, 0.4f);
            }
            var phm = part != null ? part.GetComponentInParent<HealthManager>() : null;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 打完：被挡 {MeleeHits.DevBlocked - blocked}，血 {(phm != null ? phm.hp.ToString() : "无 HealthManager")}");
            CharmShot("garpede");
        }

        /// <summary>Every item the mod has, with the Terraria texture it is drawn from, for comparing with the game's own list.</summary>
        void ItemDump()
        {
            // Where each item really comes from (SourceAudit's sources): the first found.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            var src = new Dictionary<ItemType, string>();
            void Add(ItemType ty, string how) { if (!src.ContainsKey(ty)) src[ty] = how; }
            foreach (var r in Recipes.All) Add(r.Result, r.IsBought ? "购买配方" : "合成");
            foreach (var d in Shop.Basics) Add(d.Type, "长椅买");
            foreach (var name in new[] { "roster", "more" })
                foreach (var npc in (Town.NpcDef[])typeof(Town.TownNpcs).GetField(name, flags).GetValue(null))
                    if (npc.Items != null) foreach (var st in npc.Items) Add(st.Type, "商店 " + npc.Zh);
            foreach (var (ty, _) in ((ItemType Type, int Stage)[])typeof(Town.TownNpcs).GetField("travelPool", flags).GetValue(null)) Add(ty, "旅商");
            foreach (var ty in Town.Angler.DevRewardSources()) Add(ty, "渔夫任务");
            foreach (var record in EnemyDrops.DevRecords)
                for (int stage = 0; stage <= 5; stage++)
                    foreach (bool first in new[] { true, false })
                        foreach (var d in EnemyDrops.At(record, stage, first, stage >= 4, stage >= 5)) Add(d.Type, $"掉落 {record}@{stage}");
            foreach (var rule in (Rule[])typeof(Fishing).GetField("rules", flags).GetValue(null))
                if (rule.Items != null) foreach (var ty in rule.Items) Add(ty, "钓鱼");
            foreach (System.Collections.DictionaryEntry kv in (System.Collections.IDictionary)typeof(Fishing).GetField("crates", flags).GetValue(null))
            {
                var c = (CrateDef)kv.Value;
                if (c.Special != ItemType.None) Add(c.Special, "宝匣 " + kv.Key);
                if (c.Pool != null) foreach (var ty in c.Pool) Add(ty, "宝匣 " + kv.Key);
            }
            var rows = new List<object>();
            foreach (var d in ItemDb.All)
                rows.Add(new
                {
                    Type = d.Type.ToString(), d.Name, d.Texture, d.Damage, Defense = d.Defense, d.Stage, Source = d.Source.ToString(), Class = d.Class.ToString(),
                    d.IsWeapon, d.IsArmor, d.IsAccessory, Ammo = d.Ammo.ToString(), From = src.TryGetValue(d.Type, out var how) ? how : null,
                });
            string path = System.IO.Path.Combine(outDir, "itemdump.json");
            System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(rows, Newtonsoft.Json.Formatting.Indented));
            CharmCheck("导出物品表", rows.Count > 0, $"{rows.Count} 件 → {path}");
        }

        IEnumerator KillablesTest()
        {
            var hero = HeroController.instance;
            // Strikes at a collider's middle (followed as it moves) until a condition holds or the time runs out.
            IEnumerator StrikeUntil(System.Func<Collider2D> target, System.Func<bool> done, float seconds, ItemType weapon)
            {
                InHand(weapon);
                for (float t = Time.time + seconds; Time.time < t && !done();)
                {
                    var c = target();
                    if (c == null) break;
                    hero = HeroController.instance;
                    hero.transform.position = new Vector3(c.bounds.center.x - 1.8f, c.bounds.center.y, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    hero.FaceRight();
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    yield return UseAt(c.bounds.center, 0.25f);
                }
            }

            // ---- the Wingmoulds
            Shop.DevStage = 5;
            yield return GoTo("White_Palace_01", "right1");
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1.5f);
            hero = HeroController.instance;
            var flies = new List<HealthManager>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.name.StartsWith("White Palace Fly")) flies.Add(hm);
            CharmCheck("找到翅膀傀儡", flies.Count > 0, $"{flies.Count} 只");
            if (flies.Count > 0)
            {
                var fly = flies[0];
                CharmCheck($"翅膀傀儡的血是 {KillableObstacles.WingmouldHp}（不是原版的 999）", fly.hp == KillableObstacles.WingmouldHp, $"{fly.hp}");
                Collider2D FlyCol() => fly != null && fly.gameObject.activeInHierarchy ? fly.GetComponent<Collider2D>() : null;
                int hp0 = fly.hp;
                yield return StrikeUntil(FlyCol, () => fly == null || fly.hp < hp0, 3f, ItemType.NightsEdge);
                int hit = fly != null ? fly.hp : 0;
                // It settles back (Wound, Wait, Audio, Idle): its health stays as the hit left it.
                yield return new WaitForSeconds(2f);
                CharmCheck("打中后血少了，飘回原位也不回满", fly != null && hit < hp0 && fly.hp == hit, $"{hp0} → {hit} → {(fly != null ? fly.hp : -1)}");
                // Its last points.
                if (fly != null && !fly.isDead) fly.hp = 1;
                yield return StrikeUntil(FlyCol, () => fly == null || fly.isDead || !fly.gameObject.activeInHierarchy, 6f, ItemType.NightsEdge);
                yield return null;
                CharmShot("wingmould_dead");
                CharmCheck("翅膀傀儡能打死", fly == null || fly.isDead || !fly.gameObject.activeInHierarchy, fly != null ? $"血 {fly.hp} 死了 {fly.isDead}" : "没了");
                // Another one by the nail.
                HealthManager second = null;
                foreach (var hm in flies) if (hm != null && hm != fly && !hm.isDead && hm.gameObject.activeInHierarchy) { second = hm; break; }
                if (second != null)
                {
                    second.hp = 1;
                    Collider2D SecondCol() => second != null && second.gameObject.activeInHierarchy ? second.GetComponent<Collider2D>() : null;
                    InHand(ItemType.KnightNail);
                    PlayerData.instance.hasNailArt = false;
                    for (float t = Time.time + 5f; Time.time < t && second != null && !second.isDead && second.gameObject.activeInHierarchy;)
                    {
                        var c = SecondCol();
                        if (c == null) break;
                        hero.transform.position = new Vector3(c.bounds.center.x - 1.4f, c.bounds.center.y, hero.transform.position.z);
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        NailAim.DevAim = c.bounds.center;
                        NailItem.DevHold = true;
                        yield return null;
                    }
                    NailItem.DevHold = false;
                    NailAim.DevAim = null;
                    CharmCheck("骨钉也能打死翅膀傀儡", second == null || second.isDead || !second.gameObject.activeInHierarchy, second != null ? $"血 {second.hp}" : "没了");
                }
            }

            // ---- the Garpede
            Shop.DevStage = 3;
            yield return GoTo("Deepnest_44", EntryGateOf("Deepnest_44"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(2.5f);
            hero = HeroController.instance;
            HealthManager garpede = null;
            foreach (var c in Object.FindObjectsByType<BigCentipede>(FindObjectsSortMode.None))
            {
                var hm = c != null ? c.GetComponent<HealthManager>() : null;
                if (hm != null && !hm.isDead) { garpede = hm; break; }
            }
            CharmCheck($"加皮德有了血量（{KillableObstacles.GarpedeHp}）", garpede != null && garpede.hp == KillableObstacles.GarpedeHp, garpede != null ? $"{garpede.hp}，层 {garpede.gameObject.layer}" : "没有");
            if (garpede == null) yield break;
            Collider2D GarCol() => garpede != null && garpede.gameObject.activeInHierarchy ? garpede.GetComponent<Collider2D>() : null;
            int g0 = garpede.hp;
            yield return StrikeUntil(GarCol, () => garpede == null || garpede.hp < g0, 4f, ItemType.NightsEdge);
            CharmCheck("泰拉瑞亚武器打得动加皮德", garpede != null && garpede.hp < g0, $"{g0} → {(garpede != null ? garpede.hp : -1)}");
            // A shot too (projectiles look on the enemies' layer).
            int g1 = garpede.hp;
            yield return StrikeUntil(GarCol, () => garpede == null || garpede.hp < g1, 4f, ItemType.WandofSparking);
            CharmCheck("魔法弹幕也打得到加皮德", garpede != null && garpede.hp < g1, $"{g1} → {(garpede != null ? garpede.hp : -1)}");
            int killed = KillableObstacles.DevGarpedesKilled;
            garpede.hp = 1;
            yield return StrikeUntil(GarCol, () => garpede == null || garpede.isDead, 5f, ItemType.NightsEdge);
            yield return null;
            CharmShot("garpede_dead");
            CharmCheck("加皮德能打死，整条消失", KillableObstacles.DevGarpedesKilled == killed + 1 && (garpede == null || !garpede.gameObject.activeInHierarchy),
                $"打死 {KillableObstacles.DevGarpedesKilled - killed}，还在 {(garpede != null && garpede.gameObject.activeInHierarchy)}");
        }
    }
}
