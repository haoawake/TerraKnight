using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "warriorghost", the user's 10-27 "马尔福联机的时候无法用梦之钉抽魂"): Marmu and Gorb, each beaten three ways —
    /// here (this game runs the fight), as another player's puppet killed by that player's death with its corpse kept frozen
    /// (Net.Enemies.DevNoCorpseThaw: the old way), and the same with the corpse let go — then the warrior's ghost struck with
    /// the Dream Nail: its essence taken (dream orbs up, its beaten count on) for the first and the last. Shots
    /// &lt;scene&gt;_warriorghost_&lt;who&gt;_&lt;mode&gt;. Lines "[AutoTest] 战士之魂 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly HarmonyLib.AccessTools.FieldRef<EnemyDeathEffects, GameObject> devCorpseOf = HarmonyLib.AccessTools.FieldRefAccess<EnemyDeathEffects, GameObject>("corpse");

        /// <summary>Dev (suite "warriorghost_probe"): Marmu's room walked into; her FSMs dumped, then dream-nailed where she waits.</summary>
        IEnumerator WarriorGhostProbe()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            pd.hasDreamNail = true;
            pd.SetInt("mumCaterpillarDefeated", 0);
            yield return new WaitForSeconds(1f);
            int slot = SlotOf(ItemType.DreamNail);
            if (slot >= Inventory.HotbarSize) { (inv.Slots[0], inv.Slots[slot]) = (inv.Slots[slot], inv.Slots[0]); slot = 0; }
            yield return GoTo("Fungus3_40", "right1");
            yield return new WaitForSeconds(3f);
            var hero = HeroController.instance;
            HealthManager hm = null;
            foreach (var h in Resources.FindObjectsOfTypeAll<HealthManager>())
                if (h != null && h.gameObject.scene.IsValid() && h.name.Contains("Marmu")) hm = h;
            Plugin.Log.LogInfo($"[AutoTest] 战士之魂 探测 场景 {GameManager.instance.sceneName}，马尔穆 {(hm == null ? "没有" : $"{PathOf(hm.transform)} 激活 {hm.gameObject.activeInHierarchy} 在 {(Vector2)hm.transform.position:F1} 场景 {hm.gameObject.scene.name}")}，主角 {(Vector2)hero.transform.position:F1}");
            if (hm == null) yield break;
            var bossScene = hm.gameObject.scene;
            foreach (var root in bossScene.GetRootGameObjects()) DumpFsms(root.transform);
            var reaction = hm.GetComponent<EnemyDreamnailReaction>();
            Plugin.Log.LogInfo($"[AutoTest] 战士之魂 探测 梦钉反应组件 {(reaction != null ? $"有，启用 {reaction.enabled}" : "没有")}；身上组件：{string.Join(",", hm.GetComponents<Component>().Select(c => c.GetType().Name))}");
            // Beside her, the Dream Nail.
            inv.Selected = slot;
            hero.transform.position = Clear(hero, (Vector2)hm.transform.position + new Vector2(-1.8f, 0f));
            hero.FaceRight();
            for (float t = Time.time + 2f; !hero.cState.onGround && Time.time < t;) yield return null;
            yield return new WaitForSeconds(0.5f);
            string States() => string.Join("；", hm.GetComponentsInChildren<PlayMakerFSM>(true).Select(f => $"{f.FsmName}@{f.ActiveStateName}"));
            Plugin.Log.LogInfo($"[AutoTest] 战士之魂 探测 梦钉前 {States()}，她在 {(Vector2)hm.transform.position:F1} 激活 {hm.gameObject.activeInHierarchy}，主角 {(Vector2)hero.transform.position:F1}");
            DreamNailItem.DevHold = true;
            yield return new WaitForSeconds(2.4f);
            DreamNailItem.DevHold = false;
            for (int k = 0; k < 6; k++)
            {
                yield return new WaitForSeconds(0.5f);
                Plugin.Log.LogInfo($"[AutoTest] 战士之魂 探测 梦钉后 {0.5f * (k + 1):F1}s {States()}，打中 {DreamNailItem.DevLastHit}");
            }
            DreamNailItem.DevHold = null;
            Shot(GameManager.instance.sceneName, "warriorghost_probe");
        }

        IEnumerator WarriorGhostTest()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            pd.hasDreamNail = true;
            pd.hasLantern = true;
            yield return new WaitForSeconds(1f);
            int slot = SlotOf(ItemType.DreamNail);
            if (slot < 0) { Plugin.Log.LogInfo("[AutoTest] 战士之魂 背包里没有梦之钉 不通过"); yield break; }
            if (slot >= Inventory.HotbarSize) { (inv.Slots[0], inv.Slots[slot]) = (inv.Slots[slot], inv.Slots[0]); slot = 0; }
            int ok = 0, all = 0;
            bool dumped = false;
            var cases = new[] { ("Marmu", "GhostMarmu", "mumCaterpillarDefeated", "Marmu"), ("Gorb", "GhostAladar", "aladarSlugDefeated", "Slug"), ("Xero", "GhostXero", "xeroDefeated", "Xero") };
            foreach (var (who, key, flag, part) in cases)
                foreach (int mode in new[] { 0, 1, 2 })
                {
                    string label = mode == 0 ? "本机" : mode == 1 ? "傀儡（尸体冻结，旧）" : "傀儡（尸体解冻）";
                    pd.SetInt(flag, 0);
                    Net.Enemies.DevNoCorpseThaw = mode == 1;
                    yield return GoTo("Town", "left1");
                    yield return new WaitForSeconds(1f);
                    // Into the fight by the warrior's seal (BossSummonStart begins it).
                    var seal = BossSummons.All.FirstOrDefault(x => x.Key == key);
                    Bestiary.Kills[key] = 1;
                    inv.Slots[1] = ItemStack.Of(seal.Type, 3);
                    inv.Selected = 1;
                    inv.Override = -1;
                    for (float t = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < t;) yield return null;
                    string why = BossSummons.Refusal(seal);
                    if (why != null) { all++; Plugin.Log.LogInfo($"[AutoTest] 战士之魂 {who} 封印用不了：{why} 不通过"); continue; }
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    for (float t = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < t;) yield return null;
                    string scene = GameManager.instance.sceneName;
                    var hero = HeroController.instance;
                    HealthManager hm = null;
                    for (float t = Time.time + 20f; hm == null && Time.time < t;)
                    {
                        foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (h != null && !h.isDead && h.gameObject.activeInHierarchy && h.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0) { hm = h; break; }
                        yield return null;
                    }
                    if (hm == null) { all++; Plugin.Log.LogInfo($"[AutoTest] 战士之魂 {who} {label}：{scene} 里找不到 {who} 不通过"); continue; }
                    if (!dumped)
                    {
                        dumped = true;
                        var paths = hm.GetComponentsInChildren<PlayMakerFSM>(true).Select(f => $"{RelPath(f.transform, hm.transform)}:{f.FsmName}@{f.ActiveStateName}");
                        Plugin.Log.LogInfo($"[AutoTest] 战士之魂 {hm.name}（{hm.gameObject.scene.name}）身上的状态机：{string.Join("；", paths)}");
                    }
                    yield return new WaitForSeconds(1.5f);
                    var bossScene = hm.gameObject.scene;
                    int orbs0 = pd.dreamOrbs, flag0 = pd.GetInt(flag), thawed0 = Net.Enemies.DevCorpseFsmsThawed;
                    if (mode == 0) hm.Die(0f, AttackTypes.Nail, true);
                    else Net.Enemies.DevPuppetDeath(hm, bossScene.name);
                    yield return new WaitForSeconds(4f);
                    // The warrior's ghost (Ghost Warrior NPC): back, wounded, once its warrior is beaten here.
                    GameObject npc = null;
                    foreach (var root in bossScene.GetRootGameObjects())
                    {
                        if (root.name == "Ghost Warrior NPC") npc = root;
                        var found = root.transform.Find("Ghost Warrior NPC");
                        if (npc == null && found != null) npc = found.gameObject;
                    }
                    if (npc == null)
                        foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                            if (f.gameObject.name == "Ghost Warrior NPC" && f.FsmName == "Conversation Control") npc = f.gameObject;
                    var convo = npc != null ? npc.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Conversation Control") : null;
                    var appear = npc != null ? npc.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Appear") : null;
                    Plugin.Log.LogInfo($"[AutoTest] 战士之魂 {who} {label} 打败后：{flag} {flag0}→{pd.GetInt(flag)}，灵魂 {(npc == null ? "找不到" : $"{(Vector2)npc.transform.position:F1} 现身 {appear?.ActiveStateName} 对话 {convo?.ActiveStateName}")}，解冻 {Net.Enemies.DevCorpseFsmsThawed - thawed0}");
                    if (npc == null || convo == null) { all++; continue; }
                    // The Knight beside the ghost, facing it, the Dream Nail in hand, charged and let go; its words read through.
                    inv.Selected = slot;
                    hero.transform.position = Clear(hero, (Vector2)npc.transform.position + new Vector2(-1.6f, 0f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    hero.FaceRight();
                    for (float t = Time.time + 2f; !hero.cState.onGround && Time.time < t;) yield return null;
                    yield return new WaitForSeconds(0.6f);
                    int orbs1 = pd.dreamOrbs, flag1 = pd.GetInt(flag);
                    DreamNailItem.DevHold = true;
                    yield return new WaitForSeconds(2.4f);
                    DreamNailItem.DevHold = false;
                    var visited = new List<string>();
                    bool shot = false, sent = false;
                    for (float t = Time.time + 14f; Time.time < t && pd.GetInt(flag) < 2;)
                    {
                        string st = convo.ActiveStateName;
                        if (visited.Count == 0 || visited[visited.Count - 1] != st) visited.Add(st);
                        if (st == "Defeated" && !sent) { sent = true; yield return new WaitForSeconds(1f); convo.SendEvent("CONVO_FINISH"); }
                        if (!shot && (st == "Vanish Glow" || st == "Ready" || st == "To Fight")) { shot = true; Shot(GameManager.instance.sceneName, $"warriorghost_{who}_{mode}"); }
                        yield return null;
                    }
                    DreamNailItem.DevHold = null;
                    bool took = pd.GetInt(flag) == 2 || pd.dreamOrbs > orbs1;
                    bool expected = mode == 1 ? !took : took;
                    all++;
                    if (expected) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 战士之魂 {who} {label} 梦之钉：对话 {string.Join("→", visited)}，梦之精华 {orbs1}→{pd.dreamOrbs}，{flag} {flag1}→{pd.GetInt(flag)}，"
                        + $"{(took ? "抽到了" : "没抽到")}（应{(mode == 1 ? "抽不到" : "抽到")}）{(expected ? "通过" : "不通过")}");
                    // Out of whatever fight or talk the last swing began.
                    if (!GameHooks.PlayerHasControl()) { hero.RegainControl(); hero.StartAnimationControl(); }
                }
            Net.Enemies.DevNoCorpseThaw = false;
            Plugin.Log.LogInfo($"[AutoTest] 战士之魂 合计 {ok}/{all}");
        }

        static string RelPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
            return parts.Count == 0 ? "." : string.Join("/", parts);
        }
    }
}
