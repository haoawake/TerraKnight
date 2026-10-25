using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "laserbug", "focushazards", "tollprice"): the user's 2026-10-21 fifth list. The nail on Crystal Peak's
    /// laser-shooting Crystal Crawlers (LaserBug, Mines_05); Deep Focus marking the hazards red and drawing the Husk Miner's
    /// swing as it is; the geo prompts' price five times the game's.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Round5(string suite)
        {
            charmPass = charmAll = 0;
            charmTag = suite;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            switch (suite)
            {
                case "laserbug": yield return LaserBugProbe(); break;
                case "focushazards": yield return FocusHazardsTest(); break;
                case "tollprice": yield return TollPriceTest(); break;
            }
            EndNailTest();
            WearOnly();
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
        }

        static string DescribeCol(Collider2D c) =>
            $"{PathOf(c.transform)} {c.GetType().Name} 层 {c.gameObject.layer} 触发 {c.isTrigger} 开 {c.enabled && c.gameObject.activeInHierarchy} 范围 {(Vector2)c.bounds.center:F2}/{(Vector2)c.bounds.size:F2} 中心在地形里 {World.TerraWorld.IsSolidAt(c.bounds.center)}";

        // ------------------------------------------------------------------ the nail on the laser crawlers

        IEnumerator LaserBugProbe()
        {
            var hero = HeroController.instance;
            Shop.DevStage = 3;
            yield return GoTo("Mines_05", EntryGateOf("Mines_05"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1.5f);
            hero = HeroController.instance;
            var bugs = new List<HealthManager>();
            var names = new List<string>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm == null) continue;
                names.Add(hm.name + (hm.gameObject.activeInHierarchy ? "" : "(未激活)"));
                string n = hm.name.ToLowerInvariant();
                if (!hm.isDead && hm.gameObject.activeInHierarchy && (n.Contains("laser") || n.Contains("turret") || n.Contains("crawler") || n.Contains("crystal"))) bugs.Add(hm);
            }
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 场景里的敌人：{string.Join("、", names)}");
            CharmCheck("找到水晶爬虫", bugs.Count > 0, $"{bugs.Count} 只");
            foreach (var b in bugs)
            {
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 爬虫 {PathOf(b.transform)} 位置 {(Vector2)b.transform.position:F1} 血 {b.hp} 无敌 {b.IsInvincible}");
                foreach (var c in b.GetComponentsInChildren<Collider2D>(true)) Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag}   碰撞体 {DescribeCol(c)}");
            }
            if (bugs.Count == 0) yield break;
            // The one with floor nearest under it.
            HealthManager bug = null;
            Vector2 stand = default;
            float best = float.MaxValue;
            foreach (var b in bugs)
            {
                var col = b.GetComponent<Collider2D>() ?? b.GetComponentInChildren<Collider2D>();
                if (col == null) continue;
                Vector2 c = col.bounds.center;
                foreach (float dx in new[] { -1.6f, 1.6f, -2.4f, 2.4f })
                {
                    var hit = Physics2D.Raycast(c + new Vector2(dx, 0f), Vector2.down, 6f, 1 << World.ChunkColliders.TerrainLayer);
                    if (hit.collider == null) continue;
                    float d = Vector2.Distance(hit.point, c);
                    if (d < best && !World.TerraWorld.IsSolidAt(hit.point + Vector2.up * 0.6f)) { best = d; bug = b; stand = hit.point + Vector2.up * 0.7f; }
                }
            }
            if (bug == null) { CharmCheck("找到能站着砍的爬虫", false, ""); yield break; }
            var bugCol = bug.GetComponent<Collider2D>() ?? bug.GetComponentInChildren<Collider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 选了 {bug.name}：免疫方向 {bug.InvincibleFromDirection}，碰撞体 {(Vector2)bugCol.bounds.size:F2}");
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 标签 {bug.gameObject.tag}，只怕法术 {EnemyStats.ShellOnlySpells(bug)}，碰不到 {EnemyStats.Untouchable(bug)}，睡着 {BossWake.Asleep(bug)}");
            // Stood near it for a while, not striking: its state and its invincibility as they change.
            hero.transform.position = Clear(hero, stand);
            string last = "";
            for (float t = Time.time + 5f; Time.time < t;)
            {
                string now = "";
                foreach (var f in bug.GetComponents<PlayMakerFSM>()) now += f.FsmName + "=" + f.ActiveStateName + " ";
                now += $"无敌 {bug.IsInvincible}/{bug.InvincibleFromDirection}";
                if (now != last) { Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 爬虫状态 {Time.time:F2} {now}"); last = now; }
                yield return null;
            }
            InHand(ItemType.KnightNail);
            PlayerData.instance.hasNailArt = false;
            var heard = new List<string>();
            NailItem.DevProbe = s2 => { if (heard.Count < 60) heard.Add(s2); };
            int hp0 = bug.hp, hits0 = NailItem.DevHits;
            // From the side on its floor, from above (down slashes), from below (up slashes).
            Vector2 c0 = bugCol.bounds.center;
            var tries = new (string Name, Vector2 At, UnityEngine.InputSystem.Key Key)[]
            {
                ("侧面", stand, UnityEngine.InputSystem.Key.None),
                ("上面下劈", c0 + Vector2.up * 1.9f, UnityEngine.InputSystem.Key.S),
                ("下面上劈", c0 + Vector2.down * 1.9f, UnityEngine.InputSystem.Key.W),
            };
            foreach (var t0 in tries)
            {
                if (bug == null || bug.isDead) break;
                if (World.TerraWorld.IsSolidAt(t0.At)) { Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {t0.Name}：那里是实心的，跳过"); continue; }
                int h0 = NailItem.DevHits, b0 = MeleeHits.DevBlocked, hpBefore = bug.hp;
                for (float t = Time.time + 1.6f; Time.time < t;)
                {
                    hero.transform.position = new Vector3(t0.At.x, t0.At.y, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    if (t0.Key != UnityEngine.InputSystem.Key.None) KeysDown(t0.Key); else KeysDown();
                    NailAim.DevAim = c0;
                    NailItem.DevHold = true;
                    yield return null;
                }
                NailItem.DevHold = false;
                KeysDown();
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} {t0.Name}：命中 {NailItem.DevHits - h0}，被盾挡下 {MeleeHits.DevBlocked - b0}，血 {hpBefore} → {(bug != null ? bug.hp : 0)}");
                if (t0.Name == "侧面") CharmShot("laserbug");
                yield return new WaitForSeconds(0.5f);
            }
            NailItem.DevProbe = null;
            foreach (var h in heard) Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 命中 {h}");
            // A Terraria spell on it (the Knight's spells hurt what only spells get past).
            if (bug != null && !bug.isDead)
            {
                hero.transform.position = Clear(hero, stand);
                yield return new WaitForSeconds(0.4f);
                int hpMagic = bug.hp;
                InHand(ItemType.WandofSparking);
                mod.Stats.Mana = mod.Stats.ManaMax;
                for (int k = 0; k < 4 && bug != null && !bug.isDead; k++) { mod.Stats.Mana = mod.Stats.ManaMax; yield return UseAt(bugCol.bounds.center, 0.5f); }
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 魔法打它：血 {hpMagic} → {(bug != null ? bug.hp : 0)}");
                CharmCheck("魔法打得动水晶爬虫", bug == null || bug.isDead || bug.hp < hpMagic, "");
            }
            // The game's own rule: shelled all round and "Spell Vulnerable", the nail tinks off it and only spells hurt it.
            CharmCheck("骨钉打不动水晶爬虫（原版：只有法术能打）", EnemyStats.ShellOnlySpells(bug) && bug.hp == hp0, $"命中 {NailItem.DevHits - hits0}，血 {hp0} → {bug.hp}");
            Shop.DevStage = 0;
        }

        // ------------------------------------------------------------------ Deep Focus: hazards, the Husk Miner

        IEnumerator FocusHazardsTest()
        {
            var hero = HeroController.instance;
            WearOnly(CharmEffects.DeepFocus);
            if (CharmEffects.DevFocusOn) { CharmSkills.DevPress = true; yield return new WaitForSeconds(1f); }
            // King's Pass: its spikes.
            yield return GoTo("Tutorial_01", EntryGateOf("Tutorial_01"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            DamageHero spike = null;
            foreach (var dh in Object.FindObjectsByType<DamageHero>(FindObjectsSortMode.None))
                if (dh != null && dh.hazardType == 2 && dh.isActiveAndEnabled && dh.GetComponent<Collider2D>() != null) { spike = dh; break; }
            CharmCheck("国王山道有尖刺", spike != null, "");
            if (spike == null) yield break;
            var sb = spike.GetComponent<Collider2D>().bounds;
            hero.transform.position = Clear(hero, new Vector2(sb.center.x - 3f, sb.max.y + 1f));
            yield return new WaitForSeconds(0.6f);
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(1f);
            CharmShot("spikes");
            CharmCheck("尖刺标成红色", CharmEffects.Focusing && CharmEffects.DevFocusHazards > 0, $"危险 {CharmEffects.DevFocusHazards} 处");
            // Crystal Peak: a Husk Miner swinging at the player, drawn as its body is.
            Shop.DevStage = 3;
            yield return GoTo("Mines_05", EntryGateOf("Mines_05"));
            for (float w = Time.time + 8f; !GameHooks.PlayerHasControl() && Time.time < w;) yield return null;
            yield return new WaitForSeconds(1f);
            hero = HeroController.instance;
            HealthManager miner = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.name.Contains("Miner")) { miner = hm; break; }
            CharmCheck("找到躯壳矿工", miner != null, "");
            if (miner != null)
            {
                int re0 = CharmEffects.DevFocusRetextured;
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 矿工 {PathOf(miner.transform)}");
                bool shot = false;
                for (float t = Time.time + 8f; Time.time < t;)
                {
                    if (miner == null || miner.isDead) break;
                    hero.transform.position = Clear(hero, (Vector2)miner.transform.position + Vector2.left * 1.8f);
                    for (int k = 0; k < 20; k++) yield return null;
                    if (!shot && CharmEffects.DevFocusRetextured > re0) { yield return null; CharmShot("miner"); shot = true; }
                }
                if (!shot) CharmShot("miner");
                CharmCheck("矿工换动画时高亮跟着换贴图", CharmEffects.DevFocusRetextured > re0, $"{CharmEffects.DevFocusRetextured - re0} 次");
            }
            CharmSkills.DevPress = true;
            yield return new WaitForSeconds(1f);
            Shop.DevStage = 0;
        }

        // ------------------------------------------------------------------ the geo prompts

        IEnumerator TollPriceTest()
        {
            yield return CharmPlace();
            GameObject yn = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                if (f != null && f.FsmName == "Dialogue Page Control" && f.gameObject.name == "Text YN" && f.gameObject.scene.IsValid()) { yn = f.gameObject; break; }
            CharmCheck("找到吉欧确认框（Text YN）", yn != null, "");
            if (yn == null) yield break;
            PlayMakerFSM page = null;
            foreach (var f in yn.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Dialogue Page Control") page = f;
            var cost = page.FsmVariables.FindFsmInt("Toll Cost");
            Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 确认框状态 {page.ActiveStateName}，价格变量 {(cost != null ? cost.Value.ToString() : "无")}");
            if (cost == null) yield break;
            // The "Activate Geo Text?" state's own conversion, as the prompt shows a price.
            HutongGames.PlayMaker.Actions.ConvertIntToString convert = null;
            foreach (var st in page.FsmStates)
                foreach (var a in st.Actions)
                    if (a is HutongGames.PlayMaker.Actions.ConvertIntToString c && c.intVariable != null && c.intVariable.Name == "Toll Cost") convert = c;
            CharmCheck("确认框里有显示价格的那一步", convert != null, "");
            if (convert == null) yield break;
            int raised0 = HkShopPrices.DevTollsRaised;
            cost.Value = 150;
            convert.OnEnter();
            CharmCheck("收费门 150 吉欧变成 750", cost.Value == 150 * HkShopPrices.Factor && HkShopPrices.DevTollsRaised == raised0 + 1, $"{cost.Value}，显示 {convert.stringVariable?.Value}");
            convert.OnEnter();
            CharmCheck("同一次确认框再显示不会再乘", cost.Value == 150 * HkShopPrices.Factor, $"{cost.Value}");
            yield return new WaitForSeconds(2.2f);
            cost.Value = 250;
            convert.OnEnter();
            CharmCheck("下一次确认框（骨钉匠 250）照样乘", cost.Value == 250 * HkShopPrices.Factor, $"{cost.Value}");
            cost.Value = 0;
            convert.OnEnter();
            CharmCheck("不要钱的确认框不变", cost.Value == 0, "");
        }
    }
}
