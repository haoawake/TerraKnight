using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dreamexitprobe"): the Failed Champion's dream as a seal reaches it; every FSM state of the dream (and the
    /// Knight's) that changes the scene or names a gate, logged, with the save's dream return records the visit set; then
    /// whether the scenes a dream may send the player back to have the dream's return gate (door_dreamReturn).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamExitProbe()
        {
            PlayerData.instance.isInvincible = true;
            PlayerData.instance.hasDreamNail = true;
            yield return SealTravel("Town", "left1");
            Bestiary.Kills["FalseKnight@5"] = 1;
            BossArena.Fight("FalseKnight@5", true);
            for (float t = Time.realtimeSinceStartup + 20f; !BossArena.Arrived("FalseKnight@5") && Time.realtimeSinceStartup < t;) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            var pd = PlayerData.instance;
            Plugin.Log.LogInfo($"[AutoTest] 梦醒探查 在 {GameManager.instance.sceneName}：dreamReturnScene={pd.dreamReturnScene} bossReturnEntryGate={pd.bossReturnEntryGate}");
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                bool knight = f.gameObject == HeroController.instance.gameObject;
                if (!sn.StartsWith("Dream_01") && !knight) continue;
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        string type = a.GetType().Name;
                        string vals = "";
                        foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            object v = field.GetValue(a);
                            string text = v is HutongGames.PlayMaker.NamedVariable nv ? (nv.UsesVariable ? "$" + nv.Name : nv.ToString()) : v is HutongGames.PlayMaker.FsmEvent ev ? "ev:" + ev.Name : v?.ToString();
                            if (text != null && text.Length < 50) vals += $"{field.Name}={text} ";
                        }
                        if (type.Contains("Scene") || type.Contains("Transition") || vals.Contains("door_dream") || vals.Contains("dreamReturn") || vals.Contains("DREAM"))
                            Plugin.Log.LogInfo($"[AutoTest] 梦醒探查 {sn}/{PathOf(f.transform)} '{f.FsmName}' 状态 {st.Name}：{type}({vals})");
                    }
            }
            // Where a seal may send the player back to: a gate of the dream's return there?
            foreach (var scene in new[] { "Town", "Crossroads_10", "Crossroads_01" })
            {
                yield return SealTravel(scene, EntryGateOf(scene));
                bool has = false;
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (tp.gameObject.scene.name == scene && tp.name == "door_dreamReturn") has = true;
                Plugin.Log.LogInfo($"[AutoTest] 梦醒探查 {scene} 有 door_dreamReturn：{has}");
            }
        }
    }
}
