using System.Collections;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dreamfinalprobe"): the Radiance's dream (Dream_Final_Boss) as a seal's fight takes the player there; its
    /// FSMs' states that test the save's records or change the scene, dumped as the scene loads (it sends the player on to
    /// Dream_Final), and the scenes gone through.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamFinalProbe()
        {
            PlayerData.instance.isInvincible = true;
            PlayerData.instance.hasDreamNail = true;
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                Plugin.Log.LogInfo($"[AutoTest] 辐光探查 载入 {scene.name}（{mode}）");
                if (scene.name != "Dream_Final_Boss") return;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                        foreach (var st in f.FsmStates)
                        {
                            var sb = new System.Text.StringBuilder();
                            foreach (var a in st.Actions)
                            {
                                string n = a.GetType().Name;
                                if (n.Contains("PlayerData") || n.Contains("Scene") || n.Contains("Transition") || n.Contains("Bool"))
                                {
                                    sb.Append(' ').Append(n).Append('(');
                                    foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                    {
                                        object v = field.GetValue(a);
                                        string text = v is HutongGames.PlayMaker.NamedVariable nv ? (nv.UsesVariable ? "$" + nv.Name : nv.ToString())
                                            : v is HutongGames.PlayMaker.FsmEvent ev ? "ev:" + ev.Name : v?.ToString();
                                        if (!string.IsNullOrEmpty(text) && text.Length < 60) sb.Append(field.Name).Append('=').Append(text).Append(' ');
                                    }
                                    sb.Append(')');
                                }
                            }
                            if (sb.Length == 0) continue;
                            var trans = new System.Text.StringBuilder();
                            foreach (var t in st.Transitions) trans.Append($" {t.EventName}->{t.ToState}");
                            Plugin.Log.LogInfo($"[AutoTest] 辐光探查 {PathOf(f.transform)} '{f.FsmName}' 状态 {st.Name}：{sb} |{trans}");
                        }
            }
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += Loaded;
            try
            {
                yield return SealTravel("Town", "left1");
                Bestiary.Kills["FinalBoss"] = 1;
                BossArena.Fight("FinalBoss", true);
                for (float t = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < t;)
                {
                    yield return new WaitForSecondsRealtime(1f);
                    Plugin.Log.LogInfo($"[AutoTest] 辐光探查 现在 {GameManager.instance.sceneName} 转场中 {GameManager.instance.IsInSceneTransition}");
                    if (GameManager.instance.sceneName == "Dream_Final" && !GameManager.instance.IsInSceneTransition) break;
                }
            }
            finally { UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Loaded; }
            yield return SealTravel("Town", "left1");
        }
    }
}
