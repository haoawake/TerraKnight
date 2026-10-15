using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "v241"): the Nail item's hits (Terraria damage through the mod, its stage and the
    /// Nailsmith's upgrades; the recoil off an enemy ending as the game's), the Awoken Dream Nail's
    /// damage, the Life Crystals (24 on the ground, eaten as masks and bosses allow), the Nailsmith's
    /// offer back, and the Brooding Mawlek's room left by its right side after the fight.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator V241Test()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 2.4.1 {what} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            var inv = mod.Inventory;

            // The Nail's damage: the stage's, a quarter more for each upgrade; the Awoken Dream Nail half as much again.
            int up0 = pd.nailSmithUpgrades;
            pd.nailSmithUpgrades = 0;
            int d0 = NailItem.DamageNow();
            pd.nailSmithUpgrades = 4;
            int d4 = NailItem.DamageNow();
            pd.nailSmithUpgrades = up0;
            bool awoken0 = pd.dreamNailUpgraded;
            pd.dreamNailUpgraded = false;
            int dn0 = DreamNailItem.DamageNow();
            pd.dreamNailUpgraded = true;
            int dn1 = DreamNailItem.DamageNow();
            pd.dreamNailUpgraded = awoken0;
            Check(d0 > 0 && d4 == Mathf.RoundToInt(d0 * 2f) && dn1 == Mathf.RoundToInt(dn0 * 1.5f), $"骨钉伤害 原版骨钉 {d0}、纯粹骨钉 {d4}；梦之钉 {dn0}、觉醒后 {dn1}（阶段 {Shop.UnlockedStage()}）");

            // The Nail on an enemy: the mod's hit (a Terraria number, not the game's nail damage), and the recoil short.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            pd.isInvincible = true;
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            HealthManager target = null;
            float bestD = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != "Crossroads_01") continue;
                var info = EnemyStats.Of(hm);
                if (info == null || info.Boss) continue;
                // One on the ground (a wall crawler's wall would take the slash).
                if (!EnemyRespawn.Instance.DevBody(hm, out _, out _, out _, out bool walker) || !walker) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < bestD) { bestD = d; target = hm; }
            }
            if (target == null) Check(false, "骨钉打怪：找不到怪");
            else
            {
                inv.Slots[0] = ItemStack.Of(ItemType.KnightNail);
                inv.Selected = 0;
                var tb = target.GetComponent<Collider2D>().bounds;
                target.hp = Mathf.Max(target.hp, 500);
                // Held still where it stands.
                var trb = target.GetComponent<Rigidbody2D>();
                if (trb != null) { trb.linearVelocity = Vector2.zero; trb.bodyType = RigidbodyType2D.Kinematic; }
                hero.transform.position = Clear(hero, new Vector2(tb.min.x - 0.9f, tb.center.y));
                hero.FaceRight();
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.3f);
                int hits0 = NailItem.DevHits, stops0 = NailItem.DevRecoilStops, hp0 = target.hp;
                float x0 = hero.transform.position.x, xMin = x0;
                NailItem.DevHold = true;
                float struckAt = -1f;
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    if (t > 0.3f) NailItem.DevHold = false;
                    if (struckAt < 0f && NailItem.DevHits > hits0) struckAt = Time.time;
                    xMin = Mathf.Min(xMin, hero.transform.position.x);
                    yield return null;
                }
                NailItem.DevHold = null;
                float back = x0 - xMin;
                var info = EnemyStats.Of(target);
                Check(NailItem.DevHits > hits0 && target.hp < hp0 && back < 1.5f && NailItem.DevRecoilStops > stops0,
                    $"骨钉打怪 {target.name}：骨钉打中被接管 {NailItem.DevIntercepted} 次（被拒 {NailItem.DevLastRefused}），经过 mod 的伤害 {NailItem.DevHits - hits0} 下，原版血 {hp0}→{target.hp}（泰拉瑞亚血量是原版的 {info?.Scale:F1} 倍），后退最多 {back:F2}，后退被收住 {NailItem.DevRecoilStops - stops0} 次");
            }

            // Life Crystals: 24 in this save; eaten only as far as the masks and bosses allow.
            var crystalScenes = LifeCrystals.DevScenes();
            int later = crystalScenes.Count(sc => Areas.LevelOf(sc) > LifeCrystals.LastStage);
            Check(LifeCrystals.DevCount == LifeCrystals.PerSave && later == 0,
                $"地上的生命水晶 {LifeCrystals.DevCount} 个（应 {LifeCrystals.PerSave}），在阶段 {LifeCrystals.LastStage} 之后的场景 {later} 个：{string.Join("、", crystalScenes.Select(sc => sc + "/" + Areas.LevelOf(sc)))}");
            int masks0 = pd.maxHealthBase, eaten0 = mod.Stats.LifeCrystals;
            pd.maxHealthBase = 5;
            mod.Stats.LifeCrystals = LifeCrystals.Allowance();
            int allowance = LifeCrystals.Allowance();
            inv.Slots[1] = ItemStack.Of(ItemType.LifeCrystal, 3);
            inv.Selected = 1;
            int refused0 = TerraMod.DevCrystalRefused;
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at); yield return null; }
            bool refused = TerraMod.DevCrystalRefused > refused0 && mod.Stats.LifeCrystals == allowance && inv.Slots[1].Count == 3;
            pd.maxHealthBase = 6;
            int allowance2 = LifeCrystals.Allowance();
            for (float t = 0f; t < 0.8f && mod.Stats.LifeCrystals < allowance2; t += Time.deltaTime) { if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at); yield return null; }
            // The sixth mask also gives a crystal (GrantMaskCrystals): count what was eaten, not the bag.
            bool eatenMore = mod.Stats.LifeCrystals == allowance2;
            Check(refused && eatenMore && allowance2 == allowance + 1,
                $"生命水晶上限：5 个面具、已击败首领 {allowance - LifeCrystals.GroundAllowance} 个时能吃 {allowance} 个，再吃被拒绝 {refused}；多一个面具后上限 {allowance2}，吃下 {eatenMore}（背包剩 {inv.Slots[1].Count}）");
            pd.maxHealthBase = masks0;
            mod.Stats.LifeCrystals = eaten0;
            inv.Slots[1] = default;
            inv.Selected = 0;

            // The Nailsmith offers his upgrades again.
            yield return GoTo("Room_nailsmith", EntryGateOf("Room_nailsmith"));
            yield return new WaitForSeconds(1.5f);
            PlayMakerFSM smith = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Conversation Control" && f.gameObject.name == "Nailsmith") smith = f;
            if (smith == null) Check(false, "骨钉匠：没找到他的对话状态机");
            else
            {
                string greet = Player.NailServices.DevTarget(smith, "Greet", "CONVO_FINISH"), meet = Player.NailServices.DevTarget(smith, "Meet 3", "CONVO_FINISH");
                Check(greet != "Talk Finish 2" && meet != "Talk Finish 2", $"骨钉匠：打招呼之后去 {greet}，初见之后去 {meet}（不再直接结束对话）");
            }

            // The Brooding Mawlek's room: the fight won, out by its right side as by its left.
            yield return GoTo("Crossroads_09", EntryGateOf("Crossroads_09"));
            yield return new WaitForSeconds(2f);
            pd.isInvincible = true;
            hero = HeroController.instance;
            HealthManager mawlek = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (UI.EnemyHover.RecordOf(hm) == "Mawlek" && hm.gameObject.scene.name == "Crossroads_09") mawlek = hm;
            var gates = new List<TransitionPoint>();
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.gameObject.scene.name == "Crossroads_09") gates.Add(tp);
            Plugin.Log.LogInfo($"[AutoTest] 2.4.1 毛里克的房间：门 {string.Join("，", gates.ConvertAll(g => $"{g.name} {(Vector2)g.transform.position:F1}"))}；毛里克 {(mawlek != null ? $"{mawlek.name} 血 {mawlek.hp} 激活 {mawlek.gameObject.activeInHierarchy}" : "没找到")}");
            if (mawlek != null)
            {
                // Woken by walking to it, then killed.
                float tw = Time.time;
                while (!mawlek.gameObject.activeInHierarchy && Time.time - tw < 20f)
                {
                    hero.transform.position = Clear(hero, (Vector2)mawlek.transform.position + Vector2.left * 6f);
                    yield return new WaitForSeconds(1f);
                }
                yield return new WaitForSeconds(4f);
                for (int k = 0; k < 40 && !mawlek.isDead; k++)
                {
                    mawlek.hp = 1;
                    var col = mawlek.GetComponent<Collider2D>() ?? mawlek.GetComponentInChildren<Collider2D>();
                    if (col != null && GameHooks.PlayerHasControl()) mod.Melee.HitWith(col, 99999, 0f, 0f, 1f, hero.transform.position);
                    yield return new WaitForSeconds(0.5f);
                }
                tw = Time.time;
                while ((!GameHooks.PlayerHasControl() || Time.time - tw < 4f) && Time.time - tw < 20f) yield return null;
            }
            // Every side gate from inside: walked to, then through (found again by name after coming back: the room is loaded anew).
            var names = gates.ConvertAll(g => g.name);
            foreach (var gateName in names)
            {
                if (gateName.StartsWith("top") || gateName.StartsWith("bot") || gateName.StartsWith("door")) continue;
                if (GameManager.instance.sceneName != "Crossroads_09")
                {
                    yield return GoTo("Crossroads_09", EntryGateOf("Crossroads_09"));
                    yield return new WaitForSeconds(2f);
                    hero = HeroController.instance;
                    pd.isInvincible = true;
                }
                TransitionPoint g = null;
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                    if (tp.gameObject.scene.name == "Crossroads_09" && tp.name == gateName) g = tp;
                if (g == null) { Check(false, $"毛里克的房间里找不到 {gateName}"); continue; }
                var gb = g.GetComponent<Collider2D>().bounds;
                int dir = gateName.StartsWith("right") ? 1 : -1;
                // A breakable wall on the way: what shows of it, and whether a Terraria sword breaks it.
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                {
                    if (fsm.FsmName != "break_floor" || fsm.gameObject.scene.name != "Crossroads_09") continue;
                    var wcol = fsm.GetComponentInChildren<Collider2D>();
                    if (wcol == null || Mathf.Abs(wcol.bounds.center.x - gb.center.x) > 10f) continue;
                    var shows = new System.Text.StringBuilder();
                    foreach (var r in fsm.GetComponentsInChildren<Renderer>(true)) shows.Append($" {r.name}:{(r.enabled && r.gameObject.activeInHierarchy ? "显示" : "不显示")}");
                    hero.transform.position = Clear(hero, new Vector2(wcol.bounds.center.x - dir * 2.5f, wcol.bounds.min.y + 1f));
                    Player.TerraCamera.DevLook = wcol.bounds.center;
                    yield return new WaitForSeconds(0.6f);
                    yield return CloseUpAt("Crossroads_09", "mawlek_break_" + fsm.name, wcol.bounds.center, 160, 2);
                    string st0 = fsm.ActiveStateName;
                    inv.Slots[0] = ItemStack.Of(ItemType.IronBroadsword);
                    inv.Selected = 0;
                    for (float t = 0f; t < 3f && wcol.enabled && wcol.gameObject.activeInHierarchy; t += Time.deltaTime)
                    {
                        mod.DevAim = wcol.bounds.center;
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(wcol.bounds.center);
                        yield return null;
                    }
                    mod.DevAim = null;
                    Plugin.Log.LogInfo($"[AutoTest] 2.4.1 毛里克房间 {gateName} 旁的可破坏墙 {fsm.name} {(Vector2)wcol.bounds.center:F1}：贴图{shows}；状态 {st0} → {fsm.ActiveStateName}，挥剑后碰撞{(wcol != null && wcol.enabled && wcol.gameObject.activeInHierarchy ? "还在" : "没了")}");
                    Player.TerraCamera.DevLook = null;
                }
                hero.transform.position = Clear(hero, new Vector2(gb.center.x - dir * 8f, gb.min.y + 1f));
                yield return new WaitForSeconds(0.6f);
                Player.TerraMovement.DevMove = dir;
                float tw = Time.time;
                var trace = new System.Text.StringBuilder();
                float nextTrace = 0f;
                while (GameManager.instance.sceneName == "Crossroads_09" && !GameManager.instance.IsInSceneTransition && Time.time - tw < 6f)
                {
                    if (Time.time >= nextTrace) { nextTrace = Time.time + 0.5f; trace.Append($" {(Vector2)hero.transform.position:F1}"); }
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                bool through = GameManager.instance.IsInSceneTransition || GameManager.instance.sceneName != "Crossroads_09";
                string blockers = through ? "" : Blockers(hero, gb);
                Check(through, $"毛里克打完后走 {gateName}：{(through ? "出去了" : "没出去，挡着的：" + blockers)}；路上{trace}");
                while (GameManager.instance.IsInSceneTransition) yield return null;
                yield return new WaitForSeconds(1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 2.4.1 合计 {ok}/{all}");
        }
    }
}
