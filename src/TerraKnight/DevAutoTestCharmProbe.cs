using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "charmfsm"): what the game's own charm companions are made of, for the charms that keep them (Thorns of
    /// Agony, Glowing Womb, Dreamshield, Weaversong, Grimmchild): the Knight's Charm Effects machines, then each companion
    /// found once worn (its tree, components, damage and machines), and the thorns set off once.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CharmFsmProbe()
        {
            yield return GoTo("Crossroads_04", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            pd.SetInt("grimmChildLevel", 3);
            WearOnly(12, 22, 38, 39, 40);
            hero.AddMPCharge(99);
            yield return new WaitForSeconds(4f);
            var fx = hero.transform.Find("Charm Effects");
            Plugin.Log.LogInfo($"[AutoTest] 护符探针 Charm Effects {(fx != null ? fx.name : "没有")}");
            if (fx != null)
            {
                DumpTree(fx, 0, 3);
                foreach (var f in fx.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    string n = f.FsmName;
                    if (n.Contains("Thorn") || n.Contains("Hatchling") || n.Contains("Orbit") || n.Contains("Shield") || n.Contains("Weaver") || n.Contains("Grimm"))
                        DumpFsm(f);
                    else Plugin.Log.LogInfo($"    FSM {PathOf(f.transform)} '{n}'（不展开）");
                }
            }
            var seen = new HashSet<string>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = t.name;
                if (!(n.StartsWith("Orbit Shield") || n.StartsWith("Weaverling") || n.StartsWith("Grimmchild") || n.StartsWith("Knight Hatchling") || n.StartsWith("Grimm Ball"))) continue;
                if (t.parent != null && (t.parent.name.StartsWith("Orbit Shield") || t.parent.name.StartsWith("Weaverling") || t.parent.name.StartsWith("Grimmchild"))) continue;
                string kind = n.Split(' ', '(')[0] + n.Split(' ')[System.Math.Min(1, n.Split(' ').Length - 1)];
                Plugin.Log.LogInfo($"[AutoTest] 护符探针 找到 {PathOf(t)} 激活 {t.gameObject.activeInHierarchy} 位置 {(Vector2)t.position}");
                DumpTree(t, 0, 4);
                if (!seen.Add(kind)) continue;
                foreach (var f in t.GetComponentsInChildren<PlayMakerFSM>(true)) DumpFsm(f);
            }
            Shot(GameManager.instance.sceneName, "charmfsm_worn");
            // The thorns set off by hand.
            if (fx != null)
                foreach (var f in fx.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.FsmName.Contains("Thorn")) { f.SendEvent("THORN COUNTER"); Plugin.Log.LogInfo($"[AutoTest] 护符探针 给 {PathOf(f.transform)} 发 THORN COUNTER，状态 {f.ActiveStateName}"); }
            for (int k = 0; k < 6; k++)
            {
                yield return new WaitForSeconds(0.1f);
                if (fx == null) break;
                foreach (var f in fx.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.FsmName.Contains("Thorn")) Plugin.Log.LogInfo($"[AutoTest] 护符探针 荆棘 {k}: 状态 {f.ActiveStateName}，主角状态 {hero.hero_state} 能控制 {GameHooks.PlayerHasControl()}");
                if (k == 1) { Shot(GameManager.instance.sceneName, "charmfsm_thorns"); DumpTree(fx, 0, 3); }
            }
            Plugin.Log.LogInfo("[AutoTest] 护符探针 结果 完成");
        }

        /// <summary>Dev (suite "dreamfsm"): the Knight's Dream Nail machine dumped, and a charge on the ground watched frame by frame.</summary>
        IEnumerator DreamFsmProbe()
        {
            yield return CharmPlace();
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            pd.hasDreamNail = true;
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Dream Nail") DumpFsm(f);
            InHand(Items.ItemType.DreamNail);
            Items.DreamNailItem.DevHold = true;
            PlayMakerFSM dn = null;
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Dream Nail") dn = f;
            string last = "";
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                string st = dn != null ? dn.ActiveStateName : "?";
                string line = $"{st} 输入 {hero.acceptingInput} 交出控制 {hero.controlReqlinquished} 状态 {hero.hero_state} 重力 {hero.GetComponent<Rigidbody2D>().gravityScale:F2}";
                if (line != last) { Plugin.Log.LogInfo($"[AutoTest] 梦钉探针 {t:F2}s {line}"); last = line; }
                if (t > 1.6f) Items.DreamNailItem.DevHold = false;
                yield return null;
            }
            Items.DreamNailItem.DevHold = false;
            yield return new WaitForSeconds(1.5f);
            // Dream Wielder worn: in the air, walking while it charges.
            WearOnly(Items.CharmEffects.DreamWielder);
            hero.transform.position += Vector3.up * 4f;
            yield return new WaitForSeconds(0.1f);
            float x0 = hero.transform.position.x;
            Items.DreamNailItem.DevHold = true;
            Player.TerraMovement.DevMove = 1;
            last = "";
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                string st = dn != null ? dn.ActiveStateName : "?";
                string line = $"{st} 输入 {hero.acceptingInput} 交出控制 {hero.controlReqlinquished} 着地 {hero.cState.onGround} 状态 {hero.hero_state}";
                if (line != last) { Plugin.Log.LogInfo($"[AutoTest] 梦钉探针 空中 {t:F2}s {line} 走了 {hero.transform.position.x - x0:F2}"); last = line; }
                if (t > 1.6f) Items.DreamNailItem.DevHold = false;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Items.DreamNailItem.DevHold = false;
            Plugin.Log.LogInfo($"[AutoTest] 梦钉探针 空中蓄力 {Items.CharmEffects.DevAirDreams} 次，走了 {hero.transform.position.x - x0:F2}");
            Plugin.Log.LogInfo("[AutoTest] 梦钉探针 结果 完成");
        }

        static void DumpTree(Transform t, int depth, int maxDepth)
        {
            var comps = new System.Text.StringBuilder();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                comps.Append(c.GetType().Name);
                if (c is DamageEnemies de) comps.Append($"[伤害 {de.damageDealt} {de.attackType} 方向 {de.direction} 倍率 {de.magnitudeMult}]");
                if (c is Collider2D col) comps.Append($"[{(col.enabled ? "开" : "关")}{(col.isTrigger ? " 触发" : "")} 层 {t.gameObject.layer}]");
                comps.Append(' ');
            }
            Plugin.Log.LogInfo($"    {new string(' ', depth * 2)}- {t.name} {(t.gameObject.activeSelf ? "" : "（关）")} 缩放 {t.localScale} | {comps}");
            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++) DumpTree(t.GetChild(i), depth + 1, maxDepth);
        }
    }
}
