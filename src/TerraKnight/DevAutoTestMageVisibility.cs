using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator MageVisibilityTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("isolated saves required");
            var mode = EnemyAiMode.Current;
            bool invincible = PlayerData.instance.isInvincible;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            var rows = new List<object>();
            int checks = 0, failures = 0;
            void Check(string name, bool pass) { checks++; if (!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] 灵魂法师 {name} {(pass ? "通过" : "不通过")}"); }
            try
            {
                foreach (var ai in new[] { EnemyAiKind.Vanilla, EnemyAiKind.New })
                foreach (string room in new[] { "Ruins1_25", "Ruins1_30" })
                {
                    EnemyAiMode.Current = ai;
                    yield return GoTo(room, EntryGateOf(room));
                    yield return new WaitForSeconds(1f);
                    var enemies = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Where(h => h.gameObject.scene.name == room && UI.EnemyHover.RecordOf(h) == "Mage" && h.gameObject.activeInHierarchy).ToArray();
                    Check(ai + "/" + room + "/存在原生法师", enemies.Length > 0);
                    foreach (var hm in enemies.Take(4))
                    {
                        var hero = HeroController.instance;
                        Vector2 target = hm.transform.position;
                        var stand = Clear(hero, target + Vector2.left * 3f);
                        var rb = hero.GetComponent<Rigidbody2D>();
                        bool seen = false; float hiddenRun = 0, longest = 0;
                        var states = new HashSet<string>();
                        for (float until = Time.time + 14f; hm != null && !hm.isDead && Time.time < until;)
                        {
                            hero.transform.position = stand; rb.linearVelocity = Vector2.zero;
                            // These native placements include hidden casters outside the nearest clear standing point's trigger.
                            // Use their own Sleep exit event, just as their wake detector does; never force visibility or a state.
                            foreach (var control in hm.GetComponents<PlayMakerFSM>())
                            {
                                if (control.ActiveStateName != "Sleep") continue;
                                var wake = control.Fsm.GetState("Sleep")?.Transitions.FirstOrDefault();
                                if (wake != null) control.SendEvent(wake.EventName);
                            }
                            var sprite = hm.GetComponent<tk2dBaseSprite>();
                            var renderer = sprite != null ? sprite.GetComponent<Renderer>() : hm.GetComponent<Renderer>();
                            bool visible = renderer != null && renderer.enabled && (sprite == null || sprite.color.a > .05f)
                                && Mathf.Abs(hm.transform.lossyScale.x) > .05f && Mathf.Abs(hm.transform.lossyScale.y) > .05f;
                            seen |= visible;
                            hiddenRun = visible ? 0 : hiddenRun + .1f;
                            longest = Mathf.Max(longest, hiddenRun);
                            string fsm = string.Join(";", hm.GetComponents<PlayMakerFSM>().Select(f => f.FsmName + "=" + f.ActiveStateName + "/manual=" + f.Fsm.ManualUpdate));
                            string state = fsm + "/brain=" + hm.GetComponent<EnemyAi.Brain>()?.DevState + "/visible=" + visible + "/alpha=" + sprite?.color.a + "/scale=" + hm.transform.localScale;
                            if (states.Add(state)) Plugin.Log.LogInfo("[MAGE_VIS] " + ai + " " + room + " " + hm.name + " " + state);
                            yield return new WaitForSeconds(.1f);
                        }
                        Check(ai + "/" + room + "/" + hm.name + "/可以看见", seen);
                        if (ai == EnemyAiKind.New) Check(room + "/" + hm.name + "/保留真实唤醒目标", !EnemyAi.EnemyAi.Proxies.ContainsKey(hm));
                        Check(ai + "/" + room + "/" + hm.name + "/传送后不会持续隐形", longest < 3f);
                        rows.Add(new { Ai = ai.ToString(), Room = room, Enemy = hm.name, Seen = seen, LongestHidden = longest, States = states.ToArray() });
                        Shot(room, "mage_" + ai + "_" + hm.GetInstanceID());
                        yield return new WaitForEndOfFrame();
                    }
                }
            }
            finally { EnemyAiMode.Current = mode; PlayerData.instance.isInvincible = invincible; }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "mage-visibility-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Rows = rows }, Formatting.Indented));
        }
    }
}
