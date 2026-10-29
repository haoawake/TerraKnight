using System.Collections;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The warrior dreams' ghosts (空洞骑士首领新版AI设计.md §2.6): they float through the room's walls and come and go by
    /// warping, so the watchdog leaves them be (in the ground or out of the arena is their way); each kit's own work below.
    /// </summary>
    internal abstract class WarriorDreamKit : BossKit
    {
        protected override bool Burrows => true;
        protected override bool Roams => true;
        protected override string ControlName => "Attacking";
    }

    /// <summary>
    /// Xero: his swords fly at where the player was as each winds up, back as they were, down through the floor to a
    /// player dug in under it; four of them from two thirds of his life (his own half's summon, sooner); below a third,
    /// after each attack one more of his swords at home is sent at the player. His waits 0.85. He attacks wherever the
    /// player is in the room (his script's own box of where it attacks left out: under the ceiling, out at the sides).
    /// </summary>
    [Kit("GhostXero")]
    internal sealed class GhostXeroKit : WarriorDreamKit
    {
        protected override float Pace => 0.85f;
        protected override Look Theme => Look.Blade;

        FsmBool rage;
        readonly List<PlayMakerFSM> swords = new List<PlayMakerFSM>(4);

        protected override void Setup()
        {
            rage = Bool("Rage");
            Tempo("Wait", "Wait Rage", "Recover");
            if (Batch2.Has(Control, "Check Hero Pos")) Branch("Check Hero Pos", "Antic", () => Engaged && HasTarget);
            // With all four swords away, Antic Rage cancels. Going straight back through
            // our unconditional position branch retries in the same frame forever. Use his
            // own rage cooldown before checking again, as the two-sword cancellation does.
            if (Batch2.Has(Control, "Antic Rage") && Batch2.Has(Control, "Wait Rage"))
                Divert("Antic Rage", "CANCEL", "Wait Rage");
            OnEnter("Recover", () => { if (Rank >= 2) Run(OneMore()); });
            Run(HookSwords());
        }

        /// <summary>One of his own swords back home sent at the player again (its own attack, as his Attacking sends it).</summary>
        IEnumerator OneMore()
        {
            yield return new WaitForSeconds(0.3f);
            if (Grace) yield break;
            int start = Random.Range(0, Mathf.Max(1, swords.Count));
            for (int i = 0; i < swords.Count; i++)
            {
                var s = swords[(start + i) % swords.Count];
                if (s == null || !s.gameObject.activeInHierarchy || s.ActiveStateName != "Home") continue;
                s.SendEvent("ATTACK");
                BossAi.DevMoves++;
                yield break;
            }
        }

        protected override void Tick()
        {
            if (rage != null && !rage.Value && Rank >= 1) rage.Value = true;
        }

        /// <summary>Each of the four swords, once his script has found them: aimed at a stand-in put where the player was.</summary>
        IEnumerator HookSwords()
        {
            for (int i = 0; i < 600 && Go("Sword 1")?.Value == null; i++) yield return null;
            for (int k = 1; k <= 4; k++)
            {
                var sword = Go("Sword " + k)?.Value;
                if (sword == null) continue;
                PlayMakerFSM nail = null;
                foreach (var f in sword.GetComponents<PlayMakerFSM>()) if (f.FsmName == "xero_nail") nail = f;
                if (nail == null) continue;
                var proxy = Batch4.Proxy(this, "Xero Sword " + k);
                if (Batch4.Retarget(nail, proxy, "Antic Spin", "Antic Point") == 0) continue;
                swords.Add(nail);
                var s = sword;
                OnEnter("Antic Spin", () =>
                {
                    // Its aim adds half a unit up to the point it aims at: the stand-in that much under the lock.
                    var at = Lock();
                    proxy.transform.position = at + Vector2.down * 0.5f;
                    Vector2 from = s.transform.position;
                    MarkLine(from, from + (at - from).normalized * 30f, Warn(0.6f));
                }, true, nail);
                // Its flight stops at a height of the room's floor (the game's own: Self Y under 10.25): a player dug in
                // under the floor is gone after, the sword through the ground to under him.
                var floor = Batch1Helpers.First<HutongGames.PlayMaker.Actions.FloatCompare>(State("Shoot", nail));
                if (floor != null)
                {
                    float own = floor.float2.Value;
                    OnEnter("Shoot", () => floor.float2.Value = Mathf.Min(own, TargetCenter.y - 1.5f), true, nail);
                }
            }
        }
    }

    /// <summary>
    /// Gorb: his aimed single spear waits a moment on its drawn line (where the player was then) before it flies; he warps
    /// three to five tiles from the player. His second spear ring turns 22.5° into the first's gaps, as it did. Where he
    /// hovers follows the player (beside him and a little over him, within his own hovering room), so his rings reach him
    /// anywhere, up under the ceiling too.
    /// </summary>
    [Kit("GhostAladar")]
    internal sealed class GhostAladarKit : WarriorDreamKit
    {
        protected override Look Theme => Look.Spike;

        float spearAngle;
        bool locked;

        protected override void Setup()
        {
            var distance = FindFsm("Distance Attack");
            if (distance != null && AddState("TK Spear Lock", SpearLock, "Attack", distance) != null)
            {
                Divert("Away", "ATTACK", "TK Spear Lock", distance);
                int si = Batch4.IndexOf(State("Attack", distance), "SpawnObjectFromGlobalPool");
                if (si >= 0)
                    At("Attack", si, () =>
                    {
                        var a = Float("Angle", distance);
                        if (locked && a != null) a.Value = spearAngle;
                        locked = false;
                    }, distance);
            }
            var movement = FindFsm("Movement");
            if (movement != null)
            {
                At("Warp", 1, () => NearWarp(movement), movement);
                var hover = State("Hover", movement);
                if (hover != null)
                {
                    // His hovering room: where his script brings him back from (Self X under/over, Self Y under: RETURN).
                    foreach (var a in hover.Actions)
                    {
                        if (!(a is HutongGames.PlayMaker.Actions.FloatCompare fc) || fc.float1 == null) continue;
                        bool x = fc.float1.Name == "Self X", y = fc.float1.Name == "Self Y";
                        if (fc.lessThan != null && fc.lessThan.Name == "RETURN") { if (x) roomXMin = fc.float2.Value; else if (y) roomYMin = fc.float2.Value; }
                        if (fc.greaterThan != null && fc.greaterThan.Name == "RETURN" && x) roomXMax = fc.float2.Value;
                    }
                    OnEnter("Hover", () => FollowHover(movement), first: false, f: movement);
                }
            }
        }

        float roomXMin = float.NegativeInfinity, roomXMax = float.PositiveInfinity, roomYMin = float.NegativeInfinity;

        /// <summary>His hover's target (one of his set points) moved to beside the player, a little over him, in his room.</summary>
        void FollowHover(PlayMakerFSM movement)
        {
            var target = Go("Target", movement)?.Value;
            if (target == null || !HasTarget || !Engaged) return;
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = TargetCenter.x + side * Random.Range(4f, 7f);
            x = Mathf.Clamp(x, roomXMin + 1f, roomXMax - 1f);
            float roof = CeilingAbove(TargetCenter, 30f) - 2f;
            float y = Mathf.Min(TargetCenter.y + Random.Range(1f, 4f), roof);
            y = Mathf.Max(y, roomYMin + 1f);
            target.transform.position = new Vector3(x, y, target.transform.position.z);
        }

        IEnumerator SpearLock()
        {
            var at = Lock();
            // His spear turns to its target less 90° and flies along its up.
            spearAngle = Batch4.AngleTo(Me, at + Vector2.down * 0.5f) - 90f;
            locked = true;
            float warn = Warn(0.5f);
            MarkLine(Me, at, warn);
            for (float t = 0f; t < warn; t += Time.deltaTime) yield return null;
        }

        /// <summary>The warp's spot: six to ten tiles from the player, over him more than not, in the arena and out of the ground.</summary>
        void NearWarp(PlayMakerFSM movement)
        {
            var pos = Vec3("Warp Pos", movement);
            if (pos == null) return;
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(15f, 165f) * Mathf.Deg2Rad;
                var p = ClampToArena(TargetCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3f, 5f), 1.5f);
                if (Hostiles.NativeSolidAt(p) || Vector2.Distance(p, TargetCenter) < 2.5f) continue;
                pos.Value = new Vector3(p.x, p.y, pos.Value.z);
                return;
            }
        }
    }

    /// <summary>
    /// Elder Hu: his rings closing in from both sides close faster. His ring rows keep their own patterns: rings switched
    /// off and on again to move a row's gap restarted their scripts and missed the row's fall (no ring of a row ever hit,
    /// 10-25). A ring of his comes in at a fixed height (its Reset: y 13) and slams down from there, under a player up in a
    /// tall hall (Godhome's, its top at 21.6; the user's 10-28 "新版ai依旧有boss打不到高处，比如胡长老"): one that would come in
    /// under him comes in over his head instead, under the hall's top, and slams down through him as from its own height.
    /// </summary>
    [Kit("GhostHu")]
    internal sealed class GhostHuKit : WarriorDreamKit
    {
        protected override Look Theme => Look.Soul;

        public static int DevLifted;

        protected override void Setup()
        {
            ScaleFloat("Mega Pause", 0.7f);
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "Ring Holder") continue;
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    if (f.FsmName != "Control") continue;
                    var ring = f.transform;
                    OnEnter("Antic", () => Lift(ring), true, f);
                }
            }
        }

        void Lift(Transform ring)
        {
            if (!HasTarget) return;
            float over = TargetCenter.y + TargetHeight * 0.5f + 1.2f;
            if (over <= ring.position.y) return;
            float y = Mathf.Min(over, Arena.yMax - 1f);
            if (y <= ring.position.y) return;
            ring.position = new Vector3(ring.position.x, y, ring.position.z);
            DevLifted++;
        }
    }

    /// <summary>
    /// Marmu: her roll is fired at where the player is going (drawn as she winds up) and, off a wall she meets while
    /// rolling, she rolls again at where he is.
    /// </summary>
    [Kit("GhostMarmu")]
    internal sealed class GhostMarmuKit : WarriorDreamKit
    {
        protected override string ControlName => "Control";
        protected override Look Theme => Look.Spike;

        Vector2 rollAt;
        bool aimed;
        float nextBounce;

        protected override void Setup()
        {
            OnEnter("Antic", () =>
            {
                rollAt = MaybeLead(1f);
                aimed = true;
                MarkLine(Me, rollAt, Warn(0.75f));
            });
            int fi = Batch4.IndexOf(State("Fire"), "SetVelocityAsAngle");
            if (fi >= 0)
                At("Fire", fi, () =>
                {
                    var a = Float("Angle To Hero");
                    if (aimed && a != null) a.Value = Batch4.AngleTo(Me, rollAt);
                    aimed = false;
                });
        }

        protected override void Tick()
        {
            if (rb == null || StateNow != "Chase" || Time.time < nextBounce) return;
            var v = rb.linearVelocity;
            if (v.sqrMagnitude < 9f) return;
            if (Hostiles.NativeRay(Me, v.normalized, 0.9f).collider == null) return;
            nextBounce = Time.time + 0.6f;
            Vector2 d = TargetCenter - Me;
            rb.linearVelocity = (d.sqrMagnitude > 0.01f ? d.normalized : -v.normalized) * 40f;
        }
    }

    /// <summary>
    /// No Eyes: her ghosts come thicker (a quarter sooner each).
    /// </summary>
    [Kit("GhostNoEyes")]
    internal sealed class GhostNoEyesKit : WarriorDreamKit
    {
        protected override Look Theme => Look.Void;

        protected override void Setup()
        {
            var spawn = FindFsm("Shot Spawn");
            if (spawn != null)
            {
                TempoFixed(spawn, "Spawn L", 0.75f);
                TempoFixed(spawn, "Spawn R", 0.75f);
            }
        }
    }

    /// <summary>
    /// Galien: his scythe's rests a little shorter (0.85); his little scythes, every few seconds, rush at where the player
    /// was (their line drawn) before drifting on as before.
    /// </summary>
    [Kit("GhostGalien")]
    internal sealed class GhostGalienKit : WarriorDreamKit
    {
        protected override string ControlName => "Movement";
        protected override float Pace => 0.85f;
        protected override Look Theme => Look.Blade;

        readonly HashSet<int> adopted = new HashSet<int>();

        protected override void Setup()
        {
            PlayMakerFSM hammer = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.FsmName == "Attack" && f.gameObject.name.StartsWith("Galien Hammer")) { hammer = f; break; }
                if (hammer != null) break;
            }
            if (hammer != null) TempoIn(hammer, "Decel", "Recel", "Anim");
            else Plugin.Log.LogInfo("[首领AI] 加利安：没找到镰刀的状态机，镰刀按原版");
            var minis = FindFsm("Summon Minis");
            if (minis != null)
            {
                OnEnter("Summon", () => Run(Adopt()), false, minis);
                OnEnter("Summon 2", () => Run(Adopt()), false, minis);
            }
        }

        /// <summary>The little scythes just made (the scene's new "Mini" bodies): each its own rushes.</summary>
        IEnumerator Adopt()
        {
            yield return null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == null || !root.activeInHierarchy || adopted.Contains(root.GetInstanceID())) continue;
                if (root.name.IndexOf("Mini", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var mrb = root.GetComponent<Rigidbody2D>();
                if (mrb == null) continue;
                adopted.Add(root.GetInstanceID());
                Run(Rush(root, mrb));
            }
        }

        IEnumerator Rush(GameObject mini, Rigidbody2D mrb)
        {
            while (mini != null && mini.activeInHierarchy)
            {
                float wait = Random.Range(2.2f, 3.2f);
                for (float t = 0f; t < wait; t += Time.deltaTime) yield return null;
                if (mini == null || !mini.activeInHierarchy) yield break;
                if (Grace) continue;
                var at = Lock();
                Vector2 from = mini.transform.position;
                float warn = Warn(0.5f);
                MarkLine(from, at, warn);
                for (float t = 0f; t < warn; t += Time.deltaTime) yield return null;
                for (float t = 0f; t < 0.7f && mini != null && mini.activeInHierarchy; t += Time.fixedDeltaTime)
                {
                    Vector2 d = at - (Vector2)mini.transform.position;
                    if (d.sqrMagnitude < 0.25f) break;
                    mrb.linearVelocity = d.normalized * 16f;
                    yield return Batch4.Fixed;
                }
            }
        }
    }

    /// <summary>
    /// Markoth: each nail he throws aims at where the player was as it comes (its line drawn), not after him; his shields
    /// spin out a third wider when they attack.
    /// </summary>
    [Kit("GhostMarkoth")]
    internal sealed class GhostMarkothKit : WarriorDreamKit
    {
        protected override Look Theme => Look.Soul;

        readonly FsmGameObject thrown = new FsmGameObject();
        readonly List<(Transform T, Vector3 Scale)> grown = new List<(Transform, Vector3)>();

        protected override void Setup()
        {
            var nail = State("Nail");
            int si = Batch4.IndexOf(nail, "SpawnObjectFromGlobalPool");
            if (si >= 0 && Batch4.SetField(nail.Actions[si], "storeObject", thrown)) At("Nail", si + 1, AimNail);
            var shield = FindFsm("Shield Attack");
            if (shield != null)
            {
                OnEnter("Send Attack", Grow, true, shield);
                OnEnter("Reset", Shrink, true, shield);
            }
        }

        /// <summary>The nail just thrown: its script aims at a stand-in where the player is now.</summary>
        void AimNail()
        {
            var nail = thrown.Value;
            if (nail == null) return;
            int id = nail.GetInstanceID();
            var proxy = Batch4.Proxy(this, "Markoth Nail " + id);
            if (proxy.GetComponent<KitAimProxy>() is not KitAimProxy tracker || !tracker.HasBindings)
            {
                int n = 0;
                foreach (var f in nail.GetComponentsInChildren<PlayMakerFSM>(true)) n += Batch4.Retarget(f, proxy);
                if (n == 0) Plugin.Log.LogInfo("[首领AI] 马科斯：飞钉没有瞄准主角的动作，按原版");
            }
            var at = Lock();
            proxy.transform.position = at;
            Vector2 from = nail.transform.position;
            MarkLine(from, from + (at - from).normalized * 30f, Warn(0.8f));
        }

        void Grow()
        {
            Shrink();
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                grown.Add((c, c.localScale));
                c.localScale *= 1.3f;
            }
        }

        void Shrink()
        {
            foreach (var (t, scale) in grown) if (t != null) t.localScale = scale;
            grown.Clear();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Shrink();
        }
    }
}
