using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator EnemyVisibilityTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("isolated saves required");
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            World.EnemyAiMode.Current = Environment.GetEnvironmentVariable("TK_VIS_OLD") == "1" ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            var table = (IDictionary)AccessTools.Field(typeof(EnemyDrops), "table").GetValue(null);
            var kinds = (Environment.GetEnvironmentVariable("TK_VIS_KINDS") ?? "Crawler;Buzzer;ZombieRunner;ZombieGuard;MossCharger;MossKnight;MossmanRunner;MushroomBrawler;Mantis;FlyingSentrySword;Mage;MageBalloon;MageBlob;Inflater;CrystalFlyer;BeamMiner;SpiderMini;SpiderCorpse;LesserMawlek;Sibling;BeeStinger;BigBee;HeavyMantis;MantisHeavyFlyer").Split(';');
            var rows = new List<object>();
            foreach (string kind in kinds)
            {
                if (!table.Contains(kind)) { Plugin.Log.LogInfo("[VIS] no entry " + kind); continue; }
                string scene = ((EnemyDrops.Entry)table[kind]).Scene;
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1f);
                var hm = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .FirstOrDefault(h => !h.isDead && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == kind);
                if (hm == null) { Plugin.Log.LogInfo("[VIS] no placement " + kind + " " + scene); continue; }
                var hero = HeroController.instance;
                var rb = hero.GetComponent<Rigidbody2D>();
                Vector2 StandAt()
                {
                    Vector2 at = (Vector2)hm.transform.position + Vector2.left * 3f;
                    // Native spawn protection may use a trigger (balloons) or distance (dirtcarvers).
                    // Leave it physically; do not bypass its condition with FINISHED/SPAWN.
                    if (hm.GetComponents<PlayMakerFSM>().Any(f => f.ActiveStateName == "Check Saver"))
                    {
                        var saver = hm.GetComponentsInChildren<Collider2D>().FirstOrDefault(c => c.name == "Hero Saver");
                        at.x = Mathf.Max(hm.transform.position.x + 9f, saver != null ? saver.bounds.max.x + 2f : 0f);
                    }
                    return Clear(hero, at);
                }
                var states = new HashSet<string>();
                float longest = 0, run = 0;
                bool seen = false;
                for (float until = Time.time + 10f; hm != null && !hm.isDead && Time.time < until;)
                {
                    hero.transform.position = StandAt();
                    rb.linearVelocity = Vector2.zero;
                    World.EnemyAggro.Struck(hm, EnemyStats.Of(hm));
                    var sprites = hm.GetComponentsInChildren<tk2dBaseSprite>();
                    bool visible = sprites.Any(s => s.GetComponent<Renderer>() != null && s.GetComponent<Renderer>().enabled && s.color.a > .05f && Mathf.Abs(s.transform.lossyScale.x) > .05f && Mathf.Abs(s.transform.lossyScale.y) > .05f);
                    seen |= visible;
                    run = visible ? 0 : run + .1f;
                    longest = Mathf.Max(longest, run);
                    string state = "visible=" + visible + " brain=" + hm.GetComponent<EnemyAi.Brain>()?.DevState + " " + string.Join(";", hm.GetComponentsInChildren<PlayMakerFSM>().Select(f => f.FsmName + "=" + f.ActiveStateName + "/manual=" + f.Fsm.ManualUpdate)) + " sprites=" + string.Join(";", sprites.Select(s => s.name + "/alpha=" + s.color.a + "/enabled=" + s.GetComponent<Renderer>()?.enabled + "/scale=" + s.transform.lossyScale));
                    if (states.Add(state)) Plugin.Log.LogInfo("[VIS] " + kind + " " + state);
                    yield return new WaitForSeconds(.1f);
                }
                bool waiting = !seen && hm != null && hm.GetComponents<PlayMakerFSM>().Any(f => new[] { "Manual Sleep", "Sleep", "Spawn Wait", "Hide" }.Contains(f.ActiveStateName));
                if (waiting)
                {
                    // Arena placements deliberately wait for an external wake/spawn event in both AI modes.
                    // Exercise that native entry event explicitly; never enable a renderer or jump to a state.
                    foreach (var f in hm.GetComponents<PlayMakerFSM>())
                    {
                        if (!new[] { "Manual Sleep", "Sleep", "Spawn Wait", "Hide" }.Contains(f.ActiveStateName)) continue;
                        var transition = f.Fsm.ActiveState.Transitions.FirstOrDefault();
                        Plugin.Log.LogInfo("[VIS] native activation " + kind + " " + f.ActiveStateName + " -> " + transition?.EventName);
                        if (transition != null) f.SendEvent(transition.EventName);
                    }
                    run = longest = 0;
                    for (float until = Time.time + 10f; hm != null && !hm.isDead && Time.time < until;)
                    {
                        hero.transform.position = StandAt();
                        rb.linearVelocity = Vector2.zero;
                        var sprite = hm.GetComponent<tk2dBaseSprite>();
                        bool visible = sprite != null && sprite.GetComponent<Renderer>().enabled && sprite.color.a > .05f && Mathf.Abs(sprite.transform.lossyScale.x) > .05f && Mathf.Abs(sprite.transform.lossyScale.y) > .05f;
                        seen |= visible;
                        run = visible ? 0 : run + .1f;
                        longest = Mathf.Max(longest, run);
                        yield return new WaitForSeconds(.1f);
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] 敌人显示 {kind} nativeActivation={waiting} seen={seen} hidden={longest:F2} {(seen && longest < 3f ? "通过" : "不通过")}");
                bool? released = null;
                var brain = hm != null ? hm.GetComponent<EnemyAi.Brain>() : null;
                if (brain != null && new[] { "Buzzer", "MossKnight", "ZombieGuard", "MageBalloon" }.Contains(kind))
                {
                    for (float until = Time.time + 8f; Time.time < until && !brain.DevState.Contains(":");) yield return null;
                    if (brain.DevState.Contains(":") && !brain.DevState.StartsWith("Blink") && !brain.DevState.StartsWith("Burrow"))
                    {
                        var renderer = hm.GetComponent<Renderer>();
                        bool wasEnabled = renderer.enabled;
                        try
                        {
                            // Simulate a native animation event hiding the actor during a takeover.
                            // The native FSM must be released instead of frozen invisible until the move ends.
                            renderer.enabled = false;
                            yield return null;
                            yield return null;
                            released = !brain.DevState.Contains(":") && hm.GetComponents<PlayMakerFSM>().All(f => !EnemyAi.EnemyAi.Paused.Contains(f.Fsm));
                            Plugin.Log.LogInfo($"[AutoTest] 显现中断释放 {kind} {(released == true ? "通过" : "不通过")}");
                        }
                        finally { renderer.enabled = wasEnabled; }
                    }
                }
                rows.Add(new { Kind = kind, Scene = scene, Seen = seen, LongestHidden = longest, NativeActivation = waiting, ReleasedOnNativeHide = released, States = states });
                Shot(scene, "visibility_" + kind);
                yield return new WaitForEndOfFrame();
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "enemy-visibility-result.json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
        }
    }
}
