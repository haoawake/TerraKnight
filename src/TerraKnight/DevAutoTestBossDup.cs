using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossdup_KEY", the players' 10-26 Mantis Lords "开打的时候各种问题"; the two-copy runs showed each lord
    /// twice): the boss's seal used alone; every two seconds for half a minute each body of its record (scene, path,
    /// on, its FSM's state) and the scenes loaded. Lines "[AutoTest] 首领重复 …".
    /// </summary>
    [HarmonyLib.HarmonyPatch]
    internal static class DevSecondHealth
    {
        public static bool On;
        static int said;

        /// <summary>A HealthManager waking on an object that has one already: who put it there.</summary>
        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HealthManager), "Awake")]
        static void Woke(HealthManager __instance)
        {
            if (!On || said >= 6 || __instance == null || __instance.GetComponents<HealthManager>().Length < 2) return;
            said++;
            Plugin.Log.LogInfo($"[AutoTest] 首领重复 第二个生命组件 {__instance.name}#{__instance.GetInstanceID()}：{string.Join(" < ", new System.Diagnostics.StackTrace(1, false).GetFrames().Take(12).Select(f => f.GetMethod()).Where(m => m != null).Select(m => m.DeclaringType?.Name + "." + m.Name))}");
        }
    }

    internal sealed partial class DevAutoTest
    {
        void DupLook(string record, string when)
        {
            var bodies = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(h => h != null && UI.EnemyHover.RecordOf(h) == record).ToList();
            var roots = new List<string>();
            for (int s = 0; s < USM.sceneCount; s++)
                foreach (var r in USM.GetSceneAt(s).GetRootGameObjects())
                    if (bodies.Any(h => h.transform.root == r.transform)) roots.Add($"{USM.GetSceneAt(s).name}:{r.name}#{r.GetInstanceID()}({r.transform.childCount} 子)");
            Plugin.Log.LogInfo($"[AutoTest] 首领重复 {when}：{bodies.Count} 个身体（{string.Join("、", bodies.GroupBy(h => h.name).Select(g => g.Key + "×" + g.Count() + "[" + string.Join(",", g.Select(h => $"物体{h.gameObject.GetInstanceID()}/组件{h.GetInstanceID()}{(h.enabled ? "" : "停")}hp{h.hp}")) + "]"))}），根 {string.Join("、", roots)}");
        }

        IEnumerator BossDupTest()
        {
            string key = suite.Length > "bossdup_".Length ? suite.Substring("bossdup_".Length) : "MantisLord";
            string record = key.Split('@')[0];
            NbPrepare();
            DevSecondHealth.On = true;
            // First the room walked into, no seal: how many of each body its scenes hold as they load.
            if (BossArena.DevWorldArena(key, out string gate, out var needs))
            {
                foreach (var (name, v) in needs ?? new (string, bool)[0]) PlayerData.instance.SetBool(name, v);
                yield return GoTo(BossArena.SceneOf(key), gate);
                yield return new WaitForSeconds(2f);
                DupLook(record, "直接进房");
            }
            yield return SealTravel("Town", "left1");
            var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
            if (seal == null) { Plugin.Log.LogInfo($"[AutoTest] 首领重复 没有 {key} 的召唤物"); yield break; }
            Shop.DevStage = seal.Stage;
            Bestiary.Kills[key] = Mathf.Max(1, KillsOf(key));
            var inv = mod.Inventory;
            inv.Slots[0] = ItemStack.Of(seal.Type, 3);
            inv.Selected = 0;
            inv.Override = -1;
            for (float ready = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready;) yield return null;
            mod.DevUseSelected(HeroController.instance.transform.position);
            for (float there = Time.realtimeSinceStartup + 40f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < there;) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 首领重复 {key} 到场 {BossArena.Arrived(key)} {GameManager.instance.sceneName}");
            DupLook(record, "召唤到场");
            int most = 0;
            for (int i = 0; i < 16; i++)
            {
                var scenes = new List<string>();
                for (int s = 0; s < USM.sceneCount; s++) scenes.Add(USM.GetSceneAt(s).name);
                var bodies = Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(h => h != null && UI.EnemyHover.RecordOf(h) == record).ToList();
                var byName = bodies.GroupBy(h => h.name).Select(g => g.Count()).DefaultIfEmpty(0).Max();
                most = Mathf.Max(most, byName);
                string Path(Transform t) { var p = new List<string>(); for (; t != null; t = t.parent) p.Add(t.name); p.Reverse(); return string.Join("/", p); }
                string Fsm(HealthManager h) => string.Join(",", h.GetComponents<PlayMakerFSM>().Select(f => f.FsmName + ":" + f.ActiveStateName));
                Plugin.Log.LogInfo($"[AutoTest] 首领重复 {i * 2}s 场景 [{string.Join(", ", scenes)}]；{string.Join(" | ", bodies.Select(h => $"#{h.GetInstanceID()} {h.gameObject.scene.name}:{Path(h.transform)} {(h.gameObject.activeInHierarchy ? "开" : "关")} hp{h.hp} {Fsm(h)}"))}");
                if (!GameHooks.PlayerHasControl()) yield return Tap(UnityEngine.InputSystem.Key.Space);
                yield return new WaitForSeconds(2f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 首领重复 {key} 同名最多 {most} 个 {(most <= 1 ? "通过" : "不通过")}");
        }
    }
}
