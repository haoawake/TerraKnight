using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "animprobe"): where the vanilla cutscene poses are played, looked into before recording them: in the Snail
    /// Shaman's temple, King's Pass and the False Knight's arena, the pickups (Shiny Control and other FSMs on objects named
    /// for shinies, spells, charms, heart pieces) with where they are, their state, and their states' transitions.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AnimProbeTest()
        {
            PlayerData.instance.isInvincible = true;
            foreach (var scene in new[] { "Crossroads_ShamanTemple", "Tutorial_01", "Crossroads_10" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                Plugin.Log.LogInfo($"[AutoTest] 动画探查 场景 {GameManager.instance.sceneName}，主角 {(Vector2)hero.transform.position:F1}");
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != scene) continue;
                    string n = f.gameObject.name;
                    bool shiny = f.FsmName.Contains("Shiny") || n.Contains("Shiny") || n.Contains("Spell") || n.Contains("Charm") || n.Contains("Heart Piece") || n.Contains("Vengeful") || n.Contains("Fireball");
                    if (!shiny) continue;
                    Plugin.Log.LogInfo($"[AutoTest] 动画探查   {PathOf(f.transform)} '{f.FsmName}' 激活 {f.gameObject.activeInHierarchy} 状态 {f.ActiveStateName} 位置 {(Vector2)f.transform.position:F1}");
                    foreach (var s in f.FsmStates)
                    {
                        var tr = new System.Text.StringBuilder();
                        foreach (var t in s.Transitions) tr.Append($" {t.EventName}->{t.ToState}");
                        var acts = new System.Text.StringBuilder();
                        foreach (var a in s.Actions) acts.Append(a.GetType().Name).Append(' ');
                        Plugin.Log.LogInfo($"[AutoTest] 动画探查     [{s.Name}]{tr} | {acts}");
                    }
                }
            }
            Plugin.Log.LogInfo("[AutoTest] 动画探查 结果 完成");
        }
    }
}
