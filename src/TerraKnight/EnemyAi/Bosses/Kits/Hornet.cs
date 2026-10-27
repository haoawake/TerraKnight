using System.Collections;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Hornet, the Protector (Greenpath, GG_Hornet_1) and the Sentinel (Kingdom's Edge, GG_Hornet_2), told apart by the
    /// Sentinel's barbs in her script (Control; 空洞骑士首领新版AI设计.md §2.1, §2.5):
    /// <list type="bullet">
    /// <item>the needle flies on to the room's wall (or floor, or ceiling) before its thread pulls it back, its way drawn as
    /// she winds up;</item>
    /// <item>an aimed throw, at the player as the wind-up starts (not only straight across): the Protector's now and then,
    /// the Sentinel's mostly, and after a thread storm (four in ten), after the Sentinel's counter, and always against a
    /// player off the ground (up in the air, on a wall);</item>
    /// <item>the air dash at where the player will be (led); at a player up off the ground she dashes however far up he is
    /// (not only in reach of her);</item>
    /// <item>from the third rank a ground dash may go on into an air dash; pace ×0.85;</item>
    /// <item>the Sentinel: half as many barbs again, room for more of them up, and against the air a rise to the player's
    /// height and a thread storm there (its reach marked).</item>
    /// <item>against the air: an aimed throw, or a jump and an air dash at the player.</item>
    /// </list>
    /// </summary>
    [Kit("Hornet")]
    internal sealed class HornetKit : BossKit
    {
        protected override float Pace => 0.85f;
        protected override Look Theme => Look.Spike;

        bool sentinel, aimedNext, aimedThrow, afterSphere, afterGDash, afterCounter, dashAtAir;
        int antiAir, followUp;
        float throwAngle;
        PlayMakerFSM needle;
        Wait needleOut;
        int extraBarbs;

        protected override void Setup()
        {
            sentinel = Control != null && Control.Fsm.GetState("Barb?") != null;
            Tempo("Idle", "Run", "Dmg Idle");
            OnEnter("Throw Antic", AimThrow, first: false);
            Branch("Lock?", "Throw", () => aimedThrow);
            var n = transform.Find("Needle");
            if (n != null) foreach (var f in n.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Control") needle = f;
            if (needle != null)
            {
                needleOut = Batch1Helpers.First<Wait>(State("Out", needle));
                if (needleOut != null) OnEnter("Out", NeedleOut, true, needle);
                OnEnter("Decel", () =>
                {
                    var body2 = needle.GetComponent<Rigidbody2D>();
                    if (body2 != null) body2.linearVelocity = Vector2.zero;
                }, true, needle);
            }
            At("ADash Antic", 2, LeadDash);
            // Her air dash goes only at a player in reach of her (A Dash Range, a trigger round her); one up off the ground
            // (hanging from the ceiling on a hook, up a wall) she dashes at however far up he is.
            At("ADash Antic", 0, () =>
            {
                RaiseRoof();
                if (dashAtAir || !TargetOnGround && TargetHeight > 3f)
                {
                    var range = Bool("A Dash Range");
                    if (range != null) range.Value = true;
                }
                dashAtAir = false;
            });
            OnEnter("Sphere", () => afterSphere = true);
            OnEnter("G Dash", () => afterGDash = true);
            if (sentinel) OnEnter("CA 1", () => afterCounter = true);
            // Escalation, every move's end: a follow-up, decided and the flags spent before either branch (a branch taken leaves
            // the state at once, the rest of its actions unrun: a counter's flag left standing made every move after it a throw,
            // the user's 10-26 "国王边境那一场，打到二阶段的时候就只用飞针了").
            Branch("Escalation", "Throw Antic", () => followUp == 1);
            Branch("Escalation", "Set ADash", () => followUp == 2);
            OnEnter("Escalation", () =>
            {
                followUp = afterGDash && Rank >= 2 && Random.value < 0.5f ? 2 : FollowThrow() ? 1 : 0;
                afterSphere = afterGDash = afterCounter = false;
            });
            // Against the air, in her choice: the Sentinel's after her barbs have had their turn (Barb? before Can Throw?).
            string choice = sentinel && Batch2.Has(Control, "Can Throw?") ? "Can Throw?" : "G Sphere?";
            if (sentinel && AddState("TK Rise", Rise, "Sphere Antic A") != null) Branch(choice, "TK Rise", () => antiAir == 1);
            Branch(choice, "Throw Antic", () => antiAir == 2);
            Branch(choice, "Set ADash", () => antiAir == 3);
            OnEnter(choice, PickAntiAir);
            if (sentinel) Run(Barbs());
        }

        public static int DevRoofRaised;

        /// <summary>
        /// Her air dash stops dead at the game's roof line (Roof Y: A Dash → Hit Roof, set for its camera, 40.5 in Greenpath,
        /// 39 at the Edge) though the room goes on up: that line taken up to the room's own ceiling over her as she sets off
        /// (or the arena's top, whichever is higher; at most 8 over the arena), less her height over her middle.
        /// </summary>
        void RaiseRoof()
        {
            var roof = Float("Roof Y");
            if (roof == null || body == null) return;
            float ceiling = Mathf.Min(Mathf.Max(Arena.yMax, CeilingAbove(Me, 40f)), Arena.yMax + 8f);
            float top = ceiling - (body.bounds.max.y - transform.position.y) - 0.2f;
            if (top <= roof.Value + 0.05f) return;
            roof.Value = top;
            DevRoofRaised++;
        }

        /// <summary>Against the air: an aimed throw, a jump and an air dash at him, or (the Sentinel) her rise and storm.</summary>
        void PickAntiAir()
        {
            antiAir = 0;
            if (!WantAntiAir()) return;
            float r = Random.value;
            if (sentinel && r < 0.34f) antiAir = 1;
            else if (r < (sentinel ? 0.67f : 0.5f)) { antiAir = 2; aimedNext = true; }
            else { antiAir = 3; dashAtAir = true; }
        }

        bool FollowThrow()
        {
            if (afterCounter || afterSphere && Random.value < 0.4f)
            {
                aimedNext = true;
                return true;
            }
            return false;
        }

        /// <summary>The throw's way (the game's Angle at the player as the wind-up starts): aimed, or snapped across as its own does; drawn.</summary>
        void AimThrow()
        {
            var a = Float("Angle");
            if (a == null) return;
            // A player off the ground (up in the air, on a wall) always gets it aimed: the needle goes all the way to him.
            aimedThrow = aimedNext || !TargetOnGround || AirCamping || Random.value < (sentinel ? 0.7f : 0.4f);
            aimedNext = false;
            float angle = Mathf.Repeat(a.Value, 360f);
            throwAngle = aimedThrow ? angle : angle > 90f && angle < 270f ? 180f : 0f;
            var from = (Vector2)transform.position + Vector2.down * 0.5f;
            MarkLine(from, Batch1Helpers.RayEnd(from, throwAngle, 60f), Warn(0.45f));
        }

        /// <summary>The needle's flight out: as long as it takes at her throw's speed to reach the room's wall on its way.</summary>
        void NeedleOut()
        {
            float speed = Mathf.Max(5f, Float("Throw Speed")?.Value ?? 38f);
            Vector2 from = needle.transform.position;
            var dir = Batch1Helpers.Dir(throwAngle);
            var hit = Hostiles.NativeRay(from, dir, 60f);
            float dist = hit.collider != null ? hit.distance : 60f;
            // Up to the wall itself: a player on it is in reach.
            needleOut.time.Value = Mathf.Max(0.15f, (dist - 0.3f) / speed);
        }

        /// <summary>The air dash at where the player will be by the time she gets there.</summary>
        void LeadDash()
        {
            var a = Float("Angle");
            if (a == null) return;
            Vector2 from = transform.position;
            float speed = Mathf.Max(5f, Float("A Dash Speed")?.Value ?? 30f);
            var p = Lead(0.35f + Vector2.Distance(from, TargetCenter) / speed) + Vector2.down * 0.5f;
            var d = p - from;
            a.Value = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            MarkLine(from, Batch1Helpers.RayEnd(from, a.Value, 50f), Warn(0.4f));
        }

        /// <summary>The Sentinel's rise to the player's height (its thread storm's reach marked there), then her storm in the air.</summary>
        IEnumerator Rise()
        {
            float gravity = rb != null ? rb.gravityScale : 0f;
            bool complete = false;
            try
            {
                var t = Lock();
                float roof = CeilingAbove(Me) - 2f;
                var to = ClampToArena(new Vector2(Mathf.Lerp(Me.x, t.x, 0.6f), Mathf.Min(t.y, roof)), 1.5f);
                float warn = Warn(0.6f);
                MarkCircle(to, 7f, warn + 0.4f);
                if (rb != null) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; }
                Vector2 offset = Me - (Vector2)transform.position, start = transform.position, end = to - offset;
                const float rise = 0.35f;
                for (float time = 0f; time < rise; time += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, time / rise);
                    transform.position = new Vector3(Mathf.Lerp(start.x, end.x, k), Mathf.Lerp(start.y, end.y, k), transform.position.z);
                    yield return null;
                }
                if (rb != null) rb.linearVelocity = Vector2.zero;
                if (warn > rise) yield return new WaitForSeconds(warn - rise);
                complete = true;
            }
            finally
            {
                if (rb != null && !complete) { rb.gravityScale = gravity; rb.linearVelocity = Vector2.zero; }
            }
        }

        /// <summary>The Sentinel's barbs: the scene's Barb Region throws one or two more a time, and her room for them up is 8 (5).</summary>
        IEnumerator Barbs()
        {
            var cap = Batch1Helpers.First<IntCompare>(State("Barb?"));
            if (cap != null) cap.integer2.Value = 8;
            // The room's Barb Region (the base scene's, not the boss scene's): looked for a few times a second for a while.
            PlayMakerFSM barbs = null;
            for (int i = 0; i < 120 && barbs == null; i++)
            {
                barbs = FindBarbs();
                if (barbs == null) yield return new WaitForSeconds(0.25f);
            }
            if (barbs == null) yield break;
            OnEnter("Spawn 1", () => extraBarbs = Random.value < 0.5f ? 1 : 2, true, barbs);
            Branch("Spike", "Spawn 3", () => extraBarbs-- > 0, barbs);
        }

        static PlayMakerFSM FindBarbs()
        {
            var go = GameObject.Find("Barb Region");
            if (go == null) return null;
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Spawn Barbs") return f;
            return null;
        }
    }
}
