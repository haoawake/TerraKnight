using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator EnemySlideTest()
        {
            Player.TerraStats.DevImmortal = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.Vanilla;
            yield return SealTravel("Hive_05", EntryGateOf("Hive_05"));
            var globs = Resources.FindObjectsOfTypeAll<PlayMakerFSM>().Where(f => f != null && f.gameObject.scene.name == "Hive_05" && f.name.StartsWith("Hive Knight Glob") && f.FsmName == "Control").ToArray();
            FxCheck("Slide glob objects", globs.Length == 3, $"found={globs.Length}");
            foreach (var glob in globs)
            {
                glob.gameObject.SetActive(true);
                glob.enabled = true;
                yield return null;
                glob.SetState("Init");
                yield return new WaitForSeconds(0.1f);
                glob.SetState("Tween Up");
                bool formed = false, burst = false;
                float x = glob.transform.position.x, max = 0f;
                for (float end = Time.time + 5f; Time.time < end;)
                {
                    if (glob.ActiveStateName == "Form 2" || glob.ActiveStateName == "Antic") formed = true;
                    if (glob.ActiveStateName == "Burst") { burst = true; break; }
                    max = Mathf.Max(max, Mathf.Abs(glob.transform.position.x - x));
                    yield return null;
                }
                FxCheck("Slide unhit " + glob.name, formed && burst && max < 0.35f, $"formed={formed} burst={burst} maxX={max:F3}");
                glob.SetState("Set Inert");
                foreach (float facing in new[] { 1f, -1f })
                {
                    glob.SetState("Tween Up");
                    for (float end = Time.time + 3f; Time.time < end && glob.ActiveStateName != "Form 2"; ) yield return null;
                    var col = glob.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled);
                    x = glob.transform.position.x;
                    var nativeMove = glob.FsmStates.First(s => s.Name == (facing > 0f ? "Tink R" : "Tink L")).Actions.First(a => a.GetType().Name == "iTweenMoveBy");
                    var nativeVector = (HutongGames.PlayMaker.FsmVector3)HarmonyLib.AccessTools.Field(nativeMove.GetType(), "vector").GetValue(nativeMove);
                    float expected = nativeVector.Value.x;
                    int events = PropHits.DevTinkEvents;
                    PropHits.Pulse(col, (Vector2)glob.transform.position - new Vector2(facing * 2f, 0f), facing, 1);
                    string state = glob.ActiveStateName;
                    yield return new WaitForSeconds(0.3f);
                    float dx = glob.transform.position.x - x;
                    FxCheck("Slide hit " + glob.name + " " + facing, PropHits.DevTinkEvents > events && dx * facing > 1f && dx * facing < Mathf.Abs(expected) * 1.2f && glob.ActiveStateName == "Burst", $"state={state}->{glob.ActiveStateName} dx={dx:F3} native={expected:F3} scale={glob.transform.lossyScale}");
                    glob.SetState("Set Inert");
                    yield return new WaitForSeconds(0.3f);
                }
            }
            yield return SealTravel("Deepnest_East_06", EntryGateOf("Deepnest_East_06"));
            yield return new WaitForSeconds(1f);
            var hopper = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && UI.EnemyHover.RecordOf(h) == "Hopper" && h.gameObject.activeInHierarchy);
            FxCheck("Slide native Hopper found", hopper != null, hopper != null ? hopper.name : "missing");
            if (hopper == null) yield break;
            var rb = hopper.GetComponent<Rigidbody2D>();
            var fsm = hopper.GetComponents<PlayMakerFSM>().First(f => f.FsmName == "Hopper");
            Plugin.Log.LogInfo($"[AutoTest] Slide Hopper body={rb.bodyType} gravity={rb.gravityScale} immune={TerraKnockback.DevWhy(hopper)} pos={rb.position}");
            // Sleep is a native stationary state. A stop issued while airborne must survive handoff.
            rb.linearVelocity = new Vector2(4f, 0f);
            TerraKnockback.Push(hopper, 2f, -1f);
            yield return new WaitForSeconds(0.1f);
            fsm.SetState("Sleep");
            for (float end = Time.time + 3f; Time.time < end && hopper.GetComponent<KnockFlight>()?.Active == true;) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] Slide Hopper handoff state={fsm.ActiveStateName} v={rb.linearVelocity} pos={rb.position} active={hopper.GetComponent<KnockFlight>()?.Active}");
            float x0 = rb.position.x;
            yield return new WaitForSeconds(0.2f);
            Plugin.Log.LogInfo($"[AutoTest] Slide Hopper after state={fsm.ActiveStateName} v={rb.linearVelocity} dx={rb.position.x - x0:F3}");
            FxCheck("Slide Hopper native stop survives knockback", hopper.GetComponent<KnockFlight>()?.Active == false && Mathf.Abs(rb.linearVelocity.x) < 0.05f && Mathf.Abs(rb.position.x - x0) < 0.05f, $"state={fsm.ActiveStateName} dx={rb.position.x - x0:F3} vx={rb.linearVelocity.x:F3}");
            fsm.SetState("Left Or Right?");
            bool rose = false;
            for (float end = Time.time + 1f; Time.time < end;) { rose |= rb.linearVelocity.y > 1f; yield return null; }
            FxCheck("Slide Hopper can jump again", rose, $"state={fsm.ActiveStateName} v={rb.linearVelocity}");
        }
    }
}
