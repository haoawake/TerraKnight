using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator NewAiBossAudit()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            var keys = (Environment.GetEnvironmentVariable("TK_NEWAI_BOSSES") ?? "BigFly;FalseKnight").Split(';');
            foreach (string key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                Plugin.Log.LogInfo($"[NEWAI_BOSS_BEGIN] {key}");
                string record = key.Split('@')[0];
                if (BossArena.Has(key)) FxCheck("NewAI boss native arena entry " + key, BossArena.Fight(key, true), BossArena.SceneOf(key));
                else
                {
                    string scene = EnemyDrops.SceneOf(record);
                    yield return GoTo(scene, EntryGateOf(scene));
                    FxCheck("NewAI boss native arena entry " + key, GameManager.instance.sceneName == scene, scene);
                }
                BossKit kit = null;
                float until = Time.realtimeSinceStartup + 50f;
                while (Time.realtimeSinceStartup < until)
                {
                    kit = Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).FirstOrDefault(k => k.Record == record && k.gameObject.activeInHierarchy);
                    if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                    yield return null;
                }
                FxCheck("NewAI boss native kit present " + key, kit != null, kit != null ? kit.Scene : "not loaded");
                if (kit == null) continue;
                var hero = HeroController.instance;
                var hbody = hero.GetComponent<Rigidbody2D>();
                // Move the isolated tester inside the arena to trigger its native encounter.
                var at = kit.ClampToArena(kit.Me, 3f);
                at.y = kit.GroundBelow(at) + 1.2f;
                hero.transform.position = new Vector3(at.x - 3f, at.y, hero.transform.position.z);
                if (hbody != null) hbody.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(2f);
                int fails = BossAi.DevFails;
                var states = new HashSet<string>();
                Vector2 prev = kit.transform.position;
                float maxStep = 0f;
                int jumps = 0;
                float end = Time.realtimeSinceStartup + 45f;
                while (kit != null && kit.Hm != null && !kit.Hm.isDead && Time.realtimeSinceStartup < end)
                {
                    if (!string.IsNullOrEmpty(kit.StateNow) && states.Add(kit.StateNow))
                        Plugin.Log.LogInfo($"[NEWAI_BOSS_STATE] {key}: {kit.StateNow} at {kit.Me}");
                    Vector2 pos = kit.transform.position;
                    float step = Vector2.Distance(pos, prev);
                    maxStep = Mathf.Max(maxStep, step);
                    // These first two bosses have no native teleport; other kits need their own exclusions.
                    if ((key == "BigFly" || key == "FalseKnight") && step > 100f * Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) + 0.5f)
                    {
                        jumps++;
                        Plugin.Log.LogWarning($"[NEWAI_BOSS_JUMP] {key}: {step} dt={Time.deltaTime} state={kit.StateNow} prev={prev} now={pos}");
                    }
                    prev = pos;
                    yield return null;
                }
                FxCheck("NewAI boss native state progression " + key, states.Count >= 5, string.Join(",", states));
                FxCheck("NewAI boss native update errors " + key, BossAi.DevFails == fails, $"new failures={BossAi.DevFails - fails}");
                if (key == "MantisLord" && kit != null)
                {
                    float left = kit.Float("Dstab X Min")?.Value ?? kit.Me.x;
                    float right = kit.Float("Dstab X Max")?.Value ?? kit.Me.x;
                    float floor = kit.Float("Land Y")?.Value ?? kit.Me.y;
                    FxCheck("NewAI mantis arena includes native combat span", kit.Arena.Contains(new Vector2(left, floor)) && kit.Arena.Contains(new Vector2(right, floor)), $"arena={kit.Arena} native=({left},{right}) floor={floor}");
                }
                if (key == "BigFly" || key == "FalseKnight")
                    FxCheck("NewAI boss native no abrupt movement " + key, jumps == 0, $"suspiciousSteps={jumps} maxStep={maxStep}");
                AiRenderCapture("newai_boss_" + key.Replace('@', '_') + ".png");
                Plugin.Log.LogInfo($"[NEWAI_BOSS_END] {key}: 45s observation only; phases, kill, multiplayer and all effects not established");
            }
            Plugin.Log.LogInfo("[NEWAI_DONE]");
        }
    }
}
