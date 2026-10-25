using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "roarprobe"): which early bosses hold the Knight with a roar, before recording one: each one's room
    /// entered with the boss not beaten, and every FSM there whose state sends the hero ROAR ENTER (the Roar Lock) logged.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RoarProbeTest()
        {
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            foreach (var key in new[] { "Mawlek", "BigFly", "Hornet", "MegaMossCharger", "DungDefender", "MageLord", "InfectedKnight", "JarCollector", "TraitorLord" })
            {
                string scene = Items.BossArena.WorldSceneOf(key);
                if (scene == null) continue;
                Items.BossArena.DevSetBeaten(key, false);
                yield return GoTo(scene, EntryGateOf(scene));
                for (float wait = Time.time + 4f; Time.time < wait && !UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene + "_boss").isLoaded;) yield return null;
                yield return new WaitForSeconds(1f);
                int found = 0;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!f.gameObject.scene.name.StartsWith(scene)) continue;
                    foreach (var s in f.FsmStates)
                        foreach (var a in s.Actions)
                        {
                            object ev = a.GetType().GetField("sendEvent")?.GetValue(a);
                            if (ev == null || ev.ToString() != "ROAR ENTER") continue;
                            Plugin.Log.LogInfo($"[AutoTest] 吼叫探查 {key} {scene}: {PathOf(f.transform)} '{f.FsmName}' [{s.Name}] {a.GetType().Name}");
                            found++;
                        }
                }
                Plugin.Log.LogInfo($"[AutoTest] 吼叫探查 {key} {scene}: 共 {found} 处");
            }
            Player.TerraStats.DevImmortal = false;
            Plugin.Log.LogInfo("[AutoTest] 吼叫探查 结果 完成");
        }
    }
}
