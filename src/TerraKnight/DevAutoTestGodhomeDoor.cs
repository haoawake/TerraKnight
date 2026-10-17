using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_ggdoor"): the Simple Key's door on the way to Godhome (the Junk Pit's, through GG_Pipeway). Every
    /// state machine there naming the Simple Key is found; the Knight stood at it with a Simple Key in hand presses Up
    /// (and Space through the prompts); then whether the key was spent, the door's record set, and the way through open.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static IEnumerable<string> Texts(object v)
        {
            if (v is HutongGames.PlayMaker.FsmString fs) yield return fs.Value;
            else if (v is string str) yield return str;
            else if (v is HutongGames.PlayMaker.FsmVar fv) yield return fv.stringValue;
            else if (v is System.Collections.IEnumerable list && !(v is string))
                foreach (var o in list)
                    foreach (var t in Texts(o)) yield return t;
        }

        IEnumerator GodhomeDoorTest()
        {
            var pd = PlayerData.instance;
            mod.DevNoFallDamage = true;
            pd.simpleKeys = 1;
            HkItemsDev.ToHotbar(ItemType.HkSimpleKey);
            int slot = -1;
            for (int i = 0; i < Inventory.HotbarSize; i++) if (!mod.Inventory.Slots[i].IsEmpty && mod.Inventory.Slots[i].Type == ItemType.HkSimpleKey) slot = i;
            if (slot >= 0) mod.Inventory.Selected = slot;
            var keyed = new List<PlayMakerFSM>();
            string scene = null;
            foreach (var (room, gate) in new[] { ("Waterways_08", EntryGateOf("Waterways_08")), ("GG_Pipeway", "right1"), ("GG_Waterways", "door1") })
            {
                yield return GoTo(room, gate);
                yield return new WaitForSeconds(1.5f);
                pd.isInvincible = true;
                scene = GameManager.instance.sceneName;
                if (scene != room) { Plugin.Log.LogInfo($"[AutoTest] 修复1016 神居门 没进 {room}（在 {scene}）"); continue; }
                // Who reads the Simple Key here.
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != scene || f.GetComponentsInParent<HealthManager>(true).Length > 0 || f.transform.root.name == "TK Enemy Templates") continue;
                    bool hit = false;
                    foreach (var st in f.FsmStates)
                        foreach (var act in st.Actions)
                            foreach (var field in act.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                foreach (string text in Texts(field.GetValue(act)))
                                    if (text != null && (text.Contains("simpleKey") || text.Contains("SimpleKey") || text.Contains("godseeker") || text.Contains("Junk"))) hit = true;
                    Plugin.Log.LogInfo($"[AutoTest] 修复1016 神居门 {scene} 状态机 {PathOf(f.transform)} '{f.FsmName}' {f.ActiveStateName} 在 {(Vector2)f.transform.position:F1}{(hit ? " ← 认钥匙" : "")}");
                    if (!hit) continue;
                    keyed.Add(f);
                    DumpFsm(f);
                }
                if (keyed.Count > 0) break;
            }
            FxCheck("找到认简单钥匙的门", keyed.Count > 0, $"{keyed.Count} 个，在 {scene}");
            if (keyed.Count == 0) yield break;
            var door = keyed[0];
            var hero = HeroController.instance;
            hero.transform.position = new Vector3(door.transform.position.x + 0.8f, door.transform.position.y + 0.5f, hero.transform.position.z);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            Shot(scene, "fx_ggdoor_before");
            yield return null;
            string before = door.ActiveStateName;
            int keys0 = pd.GetInt("simpleKeys");
            var seen = new List<string>();
            string last = null;
            for (float held = 0f; held < 0.35f; held += Time.unscaledDeltaTime)
            {
                KeysDown(UnityEngine.InputSystem.Key.W);
                yield return null;
            }
            KeysDown();
            float t0 = Time.realtimeSinceStartup, next = t0 + 1.2f;
            while (Time.realtimeSinceStartup - t0 < 10f && GameManager.instance.sceneName == scene)
            {
                string now = door != null ? door.ActiveStateName : "-";
                if (now != last) { last = now; seen.Add($"{Time.realtimeSinceStartup - t0:F1}s {now}"); }
                if (Time.realtimeSinceStartup > next) { next = Time.realtimeSinceStartup + 0.8f; mod.StartCoroutine(Tap(UnityEngine.InputSystem.Key.Space)); }
                yield return null;
            }
            int keys1 = pd.simpleKeys;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 神居门 拿着钥匙按上：{before} → {string.Join(" → ", seen)}；手里 {mod.Inventory.Current.Def?.Name}，钥匙 {keys0} → {keys1}，现在在 {GameManager.instance.sceneName}");
            if (GameManager.instance.sceneName == scene) Shot(scene, "fx_ggdoor_after");
            yield return null;
            FxCheck("拿着简单钥匙打开了去神居的门", keys1 == keys0 - 1, $"钥匙 {keys0} → {keys1}，门 {before} → {last}");
        }
    }
}
