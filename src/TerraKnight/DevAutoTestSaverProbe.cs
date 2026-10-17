using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_saverprobe"): Hornet's arena in Greenpath (Fungus1_04) before any fight: every collider there that
    /// stops the Knight and isn't the ground (no tilemap chunk), with its layer, parents and state, and every state machine
    /// action that names one of their objects (switches it on or off), with its state.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SaverProbe()
        {
            const string room = "Fungus1_04";
            yield return GoTo(room, "left1");
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            int mask = Physics2D.GetLayerCollisionMask(hero.gameObject.layer);
            var names = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!c.gameObject.scene.name.StartsWith(room) || c.isTrigger || (mask & (1 << c.gameObject.layer)) == 0) continue;
                string path = PathOf(c.transform);
                if (path.Contains("TileMap") || path.StartsWith("TerraKnight") || path.Contains("Terra ")) continue;
                names.Add(c.gameObject.name);
                if (c.transform.parent != null) names.Add(c.transform.parent.name);
                sb.Append($"\n  {c.gameObject.scene.name}/{path}（{c.GetType().Name} 层{c.gameObject.layer} 开{(c.enabled && c.gameObject.activeInHierarchy ? 1 : 0)} {(Vector2)c.bounds.min:F1}..{(Vector2)c.bounds.max:F1}{FsmStates(c.gameObject, false)}）");
            }
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 挡路的 {room}：{sb}");
            var acts = new StringBuilder();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!f.gameObject.scene.name.StartsWith(room)) continue;
                foreach (var st in f.FsmStates)
                    foreach (var act in st.Actions)
                        foreach (var field in act.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            var v = field.GetValue(act);
                            GameObject go = v is HutongGames.PlayMaker.FsmGameObject fg ? fg.Value : v is HutongGames.PlayMaker.FsmOwnerDefault od && od.OwnerOption == HutongGames.PlayMaker.OwnerDefaultOption.SpecifyGameObject ? od.GameObject.Value : null;
                            string text = v is HutongGames.PlayMaker.FsmString fs ? fs.Value : null;
                            if ((go != null && (names.Contains(go.name) || go.name.Contains("Saver"))) || (text != null && text.Contains("Saver")))
                                acts.Append($"\n  {PathOf(f.transform)} '{f.FsmName}' 状态 {st.Name}：{act.GetType().Name}.{field.Name} = {(go != null ? go.name : text)}（现在 {f.ActiveStateName}）");
                        }
            }
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 管它们的状态机：{acts}");
            FxCheck("挡路的东西列出来了", names.Count > 0, $"{names.Count} 个名字");
        }
    }
}
