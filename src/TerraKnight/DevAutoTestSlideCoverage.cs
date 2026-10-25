using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        static readonly string[] SlideRooms = { "Tutorial_01", "Crossroads_01", "Crossroads_15", "Fungus1_01", "Fungus1_17", "Fungus2_05", "Deepnest_East_06" };

        // Use the real native action on an actual enemy body. Temporarily holding AI lets
        // us test a single stop instruction instead of a fresh AI jump masking the result.
        static void SlideCommand(HealthManager hm, float? x, float? y = null)
        {
            var fsm = hm.GetComponents<PlayMakerFSM>().FirstOrDefault();
            if (fsm == null) { fsm = hm.gameObject.AddComponent<PlayMakerFSM>(); fsm.enabled = false; }
            var a = new SetVelocity2d
            {
                gameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject { Value = hm.gameObject } },
                vector = new FsmVector2 { UseVariable = true },
                x = x.HasValue ? new FsmFloat { Value = x.Value } : new FsmFloat { UseVariable = true },
                y = y.HasValue ? new FsmFloat { Value = y.Value } : new FsmFloat { UseVariable = true },
            };
            a.Init(fsm.Fsm.States[0]);
            a.OnEnter();
        }

        static List<Behaviour> SlideHold(HealthManager hm)
        {
            var list = new List<Behaviour>();
            foreach (var b in hm.GetComponentsInChildren<MonoBehaviour>(true))
                if (b.enabled && b.GetComponentInParent<HealthManager>() == hm && !(b is HealthManager) && !(b is KnockFlight) && !(b is SteadyFade) && !(b is tk2dBaseSprite) && !(b is tk2dSpriteAnimator))
                {
                    list.Add(b);
                    // Disabling PlayMaker alone still allows animation-triggered events.
                    // Use the same event hold as real network puppets for this probe.
                    if (b is PlayMakerFSM f)
                    {
                        HarmonyLib.AccessTools.Method(typeof(Net.Enemies), "Freeze").Invoke(null, new object[] { b });
                        EnemyAi.EnemyAi.Paused.Add(f.Fsm);
                    }
                    b.enabled = false;
                }
            return list;
        }

        static void SlideRelease(List<Behaviour> list)
        {
            foreach (var b in list) if (b != null)
            {
                if (b is PlayMakerFSM f)
                {
                    EnemyAi.EnemyAi.Paused.Remove(f.Fsm);
                    HarmonyLib.AccessTools.Method(typeof(Net.Enemies), "Thaw").Invoke(null, new object[] { b });
                }
                b.enabled = true;
            }
        }

        static GameObject SlideFloor(HealthManager hm)
        {
            var c = hm.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled && !c.isTrigger);
            if (c == null) return null;
            var floor = new GameObject("TK Slide Test Floor") { layer = 8 };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, hm.gameObject.scene);
            floor.transform.position = new Vector3(c.bounds.center.x, c.bounds.min.y - 0.25f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(12f, 0.2f);
            return floor;
        }

        IEnumerator SlideCoverageTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.Vanilla;
            var seen = new HashSet<string>();
            int movable = 0, immune = 0, flyers = 0, walkers = 0;
            foreach (string room in SlideRooms)
            {
                yield return SealTravel(room, EntryGateOf(room));
                yield return new WaitForSeconds(0.4f);
                var targets = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Where(h => h != null && !h.isDead && h.gameObject.scene.name == room && h.gameObject.activeInHierarchy).ToArray();
                foreach (var hm in targets)
                {
                    if (hm == null || hm.isDead) continue;
                    var info = EnemyStats.Of(hm);
                    var rb = hm.GetComponent<Rigidbody2D>();
                    string record = UI.EnemyHover.RecordOf(hm);
                    if (info == null || rb == null || string.IsNullOrEmpty(record) || !seen.Add(record)) continue;
                    if (!hm.GetComponents<Collider2D>().Any(c => c.enabled)) { Plugin.Log.LogInfo($"[AutoTest] SlideCoverage no direct hit collider {record}"); continue; }
                    if (EnemyStats.NoKnockback(info)) { Plugin.Log.LogInfo($"[AutoTest] SlideCoverage protected {record} boss/elite"); continue; }
                    var scripts = SlideHold(hm);
                    var floor = SlideFloor(hm);
                    Vector2 origin = rb.position;
                    float gravity = rb.gravityScale;
                    var type = rb.bodyType;
                    try
                    {
                        rb.linearVelocity = Vector2.zero;
                        bool resists = TerraKnockback.Immune(hm);
                        TerraKnockback.Push(hm, 2f, 1f);
                        var flight = hm.GetComponent<KnockFlight>();
                        if (resists)
                        {
                            immune++;
                            yield return new WaitForSeconds(0.15f);
                            FxCheck("SlideCoverage immune " + record, (flight == null || !flight.Active) && hm.GetComponent<KnockSlide>()?.Active != true, $"reason={TerraKnockback.DevWhy(hm)}");
                            continue;
                        }
                        if (rb.bodyType != RigidbodyType2D.Dynamic) { Plugin.Log.LogInfo($"[AutoTest] SlideCoverage non-dynamic {record} {rb.bodyType}"); continue; }
                        movable++; if (gravity <= 0.01f) flyers++; else walkers++;
                        FxCheck("SlideCoverage launches " + record, flight != null && flight.Active, $"g={gravity} type={type}");
                        yield return new WaitForSeconds(0.08f);
                        SlideCommand(hm, 0f, gravity <= 0.01f ? (float?)0f : null);
                        for (float end = Time.time + 3f; Time.time < end && flight != null && flight.Active;) yield return null;
                        float x0 = rb.position.x;
                        yield return new WaitForSeconds(0.3f);
                        FxCheck("SlideCoverage stop " + record, flight != null && !flight.Active && Mathf.Abs(rb.linearVelocity.x) < 0.03f && Mathf.Abs(rb.position.x - x0) < 0.03f, $"g={gravity} vx={rb.linearVelocity.x:F3} drift={rb.position.x - x0:F3}");
                        FxCheck("SlideCoverage physics restored " + record, Mathf.Abs(rb.gravityScale - gravity) < 0.001f && rb.bodyType == type, $"g={rb.gravityScale}/{gravity} type={rb.bodyType}/{type}");
                    }
                    finally
                    {
                        if (floor != null) Object.Destroy(floor);
                        if (hm != null) { rb.position = origin; rb.linearVelocity = Vector2.zero; SlideRelease(scripts); }
                    }
                    yield return new WaitForSeconds(0.2f);
                }
            }
            FxCheck("SlideCoverage types", movable >= 8 && flyers > 0 && walkers > 0 && immune > 0, $"records={seen.Count} movable={movable} flyers={flyers} walkers={walkers} immune={immune}");
            Plugin.Log.LogInfo($"[AutoTest] SlideCoverage records {string.Join(",", seen)}");
        }
    }
}
