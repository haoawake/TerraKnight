using System.Collections;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "clothenemies"): Abyss_17's event groups (Cloth Enemies: Cloth's ambush; Battle Scene Ore:
    /// the pale ore's): each with its parents, every component on them, their FSMs and states, and the
    /// PlayerData flags Cloth's story keeps; right after arriving and a few seconds later.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ClothEnemiesTest()
        {
            const string scene = "Abyss_17";
            foreach (var gate in new[] { EntryGateOf(scene), "left1", "right1", "top1", "bot1" })
            {
                yield return GoTo(scene, gate);
                if (GameManager.instance.sceneName == scene) break;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                yield return new WaitForSeconds(pass == 0 ? 0.5f : 4f);
                PlayerData.instance.isInvincible = true;
                var sb = new StringBuilder();
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.gameObject.scene.name != scene || (t.name != "Cloth Enemies" && t.name != "Battle Scene Ore" && t.name != "Cloth Entry")) continue;
                    sb.Append($"\n  {PathOf(t)} 激活 {t.gameObject.activeSelf}/{t.gameObject.activeInHierarchy}，子物体 {t.childCount}");
                    for (var p = t; p != null; p = p.parent)
                    {
                        sb.Append($"\n    {(p == t ? "本身" : "父")} {p.name}：");
                        foreach (var c in p.GetComponents<Component>())
                        {
                            if (c == null || c is Transform) continue;
                            sb.Append(c.GetType().Name);
                            if (c is PlayMakerFSM f) sb.Append($"('{f.FsmName}' 启用 {f.enabled} 状态 {f.ActiveStateName})");
                            if (c is Behaviour be && !(c is PlayMakerFSM)) sb.Append(be.enabled ? "" : "(关)");
                            sb.Append(' ');
                        }
                    }
                }
                var pd = PlayerData.instance;
                sb.Append($"\n  存档：savedCloth {pd.GetBool("savedCloth")} clothKilled {pd.GetBool("clothKilled")} clothInTown {pd.GetBool("clothInTown")} clothLeftTown {pd.GetBool("clothLeftTown")} clothEnteredTramRoom {pd.GetBool("clothEnteredTramRoom")} metCloth {pd.GetBool("metCloth")}");
                Plugin.Log.LogInfo($"[AutoTest] 克洛斯的敌人 {(pass == 0 ? "刚进来" : "4 秒后")}：{sb}");
            }
            // Whatever in the room is Cloth's, and every FSM there whose actions name the group.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.gameObject.scene.name != scene || t.name.IndexOf("Cloth", System.StringComparison.OrdinalIgnoreCase) < 0 || t.name == "Cloth Enemies") continue;
                Plugin.Log.LogInfo($"[AutoTest] 克洛斯的敌人 物体 {PathOf(t)} 激活 {t.gameObject.activeSelf}/{t.gameObject.activeInHierarchy}");
                foreach (var f in t.GetComponents<PlayMakerFSM>()) DumpFsm(f);
            }
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != scene) continue;
                bool names = false;
                foreach (var st in f.FsmStates)
                    foreach (var act in st.Actions)
                    {
                        if (act == null) continue;
                        foreach (var fld in act.GetType().GetFields())
                        {
                            var v = fld.GetValue(act);
                            string txt = v is HutongGames.PlayMaker.FsmOwnerDefault od ? (od.GameObject?.Value != null ? od.GameObject.Value.name : "") :
                                         v is HutongGames.PlayMaker.FsmGameObject fg ? (fg.Value != null ? fg.Value.name : "") :
                                         v is HutongGames.PlayMaker.FsmString fs ? fs.Value : v is GameObject g ? g.name : null;
                            if (txt != null && txt.Contains("Cloth Enemies")) names = true;
                        }
                    }
                if (!names) continue;
                Plugin.Log.LogInfo($"[AutoTest] 克洛斯的敌人 管着这个组的 {PathOf(f.transform)} '{f.FsmName}' 启用 {f.enabled}/{f.gameObject.activeInHierarchy} 状态 {f.ActiveStateName}");
                DumpFsm(f);
            }
            Shot(scene, "clothenemies");
        }
    }
}
