using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Vengefly King (Big Buzzer; 空洞骑士首领新版AI设计.md §2.1): his swoop's lowest point is where the player will be
    /// (led as its wind-up starts, drawn as a line from him to it), its sideways speed made to reach it there; from the second
    /// rank a swoop is now and then followed by another, aimed afresh; no more than three Vengeflies of his up at once; pace
    /// ×0.9; a player kept up in the air gets a swoop at his height; in the fight he goes for the player wherever he is in
    /// the room (up under the ceiling, on a platform over his alert range), never off to lurk while the player is there;
    /// against a player well up (under the ceiling, where his U-shaped swoop can't come up to) he rams straight at him
    /// (his own body and swoop pictures).
    /// </summary>
    [Kit("BigBuzzer")]
    internal sealed class VengeflyKingKit : BossKit
    {
        protected override string ControlName => "Big Buzzer";
        protected override float Pace => 0.9f;
        protected override Look Theme => Look.Rock;
        protected override bool Roams => true;

        /// <summary>The swoop's low point, and the swoop's time down to it (the game's iTween, 0.8 s).</summary>
        Vector2 aim;
        bool aimed, again, chained;
        const float SwoopTime = 0.8f;
        /// <summary>His alert range's own "In Alert Range" (read every frame by his Idle and Lurk).</summary>
        FsmBool inRange;

        protected override void Setup()
        {
            Tempo("Idle");
            OnEnter("Swoop Antic", AimSwoop, first: false);
            // The low point behind rock from where he is: during the wind-up he flies round to a clear line first.
            OnEnter("Swoop Antic", () =>
            {
                if (!aimed || Grace || !ClearSpot(aim, 10f, Level, out var spot)) return;
                Run(FlyTo(spot, 0.5f, 22f));
            }, first: false);
            At("Swoop Direction", 0, () =>
            {
                var h = Float("Swoop Height");
                if (aimed && h != null) h.Value = aim.y;
                // Facing the low point from where he is now (he may have flown round it).
                var face = Bool("Face Right");
                if (aimed && face != null && Mathf.Abs(aim.x - Me.x) > 0.5f) face.Value = aim.x > Me.x;
            });
            OnEnter("Swoop L", SwoopSpeed, first: false);
            // His idle's flight after the player (the game's 6 a second) a half faster: across his long room in time.
            var fly = Batch1Helpers.First<HutongGames.PlayMaker.Actions.DistanceFly>(State("Idle"));
            if (fly != null && fly.speedMax != null) fly.speedMax.Value *= 1.5f;
            OnEnter("Swoop R", SwoopSpeed, first: false);
            OnEnter("Swoop Rise", () =>
            {
                again = !chained && Rank >= 1 && Random.value < 0.4f;
                chained = false;
            });
            Branch("Idle", "Swoop Antic", () =>
            {
                if (!again) return false;
                again = false;
                chained = true;
                return true;
            });
            if (AddState("TK Ram", Ram, "Idle") != null)
                Branch("Choose Attack", "TK Ram", () => !Grace && HasTarget && (TargetHeight > 4f || TargetCenter.y - Me.y > 3f) && Random.value < 0.6f);
            Branch("Choose Attack", "Swoop Antic", WantAntiAir);
            // Lurking (the player out of his alert range): straight back after him while he is in the room.
            Branch("Lurk", "Idle", InRoom);
            var alert = Go("Alert Range")?.Value;
            if (alert != null)
                foreach (var f in alert.GetComponents<PlayMakerFSM>())
                    if ((inRange = f.FsmVariables.FindFsmBool("In Alert Range")) != null) break;
            // Not when the swoop would send him straight back here (too many swoops in a row): no loop.
            Branch("Summon Antic", "Swoop Antic", () => (Int("Swoops in A Row")?.Value ?? 0) <= 3 && Vengeflies() >= 3);
        }

        /// <summary>The fight on and the player in the room (over its floor up to well above its top).</summary>
        bool InRoom() => Engaged && HasTarget && new Rect(Arena.xMin - 4f, Arena.yMin - 2f, Arena.width + 8f, Arena.height + 14f).Contains(TargetCenter);

        /// <summary>In the room, the player counts as in his alert range (his Idle sends him off to lurk otherwise).</summary>
        protected override void Tick()
        {
            if (inRange != null && !inRange.Value && InRoom()) inRange.Value = true;
        }

        /// <summary>Where the swoop bottoms out: where the player will be once it gets there, kept off the floor and ceiling.</summary>
        void AimSwoop()
        {
            var p = Lead(0.45f + SwoopTime);
            float floor = GroundBelow(p + Vector2.up * 0.5f) + 1.2f, roof = CeilingAbove(p) - 1.5f;
            p.y = Mathf.Clamp(p.y, floor, Mathf.Max(floor, roof));
            aim = p;
            aimed = true;
            MarkLine(Me, aim, Warn(0.5f));
        }

        /// <summary>The swoop's sideways speed (the game's ±20): enough to be over the low point as it gets down to it.</summary>
        void SwoopSpeed()
        {
            if (!aimed || rb == null) return;
            aimed = false;
            float face = Bool("Face Right")?.Value == true ? 1f : -1f;
            float speed = Mathf.Clamp(Mathf.Abs(aim.x - Me.x) / SwoopTime, 10f, 28f);
            if (Mathf.Sign(aim.x - Me.x) != face) speed = 10f;
            rb.linearVelocity = new Vector2(face * speed, rb.linearVelocity.y);
        }

        /// <summary>The ways round the low point a swoop can come in from: level with it or a little over or under.</summary>
        static readonly float[] Level = { 0f, 20f, -20f, 180f, 160f, -160f, 40f, 140f };

        /// <summary>
        /// The player behind the room's rock from here: a free spot with a clear line to him (round him, the nearest to him
        /// first), to fly to before he swoops.
        /// </summary>
        bool ClearSpot(Vector2 target, float reach, float[] angles, out Vector2 spot)
        {
            spot = Me;
            var to = target - Me;
            if (Hostiles.NativeRay(Me, to.normalized, to.magnitude).collider == null) return false;
            float best = float.MaxValue;
            bool found = false;
            foreach (float deg in angles)
            {
                var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                var hit = Hostiles.NativeRay(target, dir, reach);
                float len = hit.collider != null ? hit.distance - 2f : reach;
                if (len < 4f) continue;
                var p = target + dir * len;
                if (Hostiles.NativeSolidAt(p)) continue;
                float d = (p - Me).sqrMagnitude;
                if (d < best) { best = d; spot = p; found = true; }
            }
            return found;
        }

        /// <summary>Flown to a spot (his own flight) through his wind-up, for a while at most.</summary>
        System.Collections.IEnumerator FlyTo(Vector2 spot, float seconds, float speed)
        {
            // Only through the wind-up it started in (the move's own speed is set as the next state starts).
            string state = StateNow;
            for (float until = Time.time + seconds; rb != null && Time.time < until && Active && StateNow == state;)
            {
                var d = spot - Me;
                if (d.sqrMagnitude < 0.25f) break;
                rb.linearVelocity = d.normalized * Mathf.Min(speed, d.magnitude / Mathf.Max(Time.deltaTime, 0.01f));
                yield return null;
            }
            if (rb != null && StateNow == state) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>A short wind-up turned to the player, then straight at where he will be, until the room's rock stops him.</summary>
        System.Collections.IEnumerator Ram()
        {
            if (rb == null) yield break;
            Batch2.Play(gameObject, "Swoop Antic");
            float warn = Warn(0.45f);
            Batch5.Face(transform, TargetCenter.x);
            for (float t = 0f; t < warn && Active; t += Time.deltaTime)
            {
                rb.linearVelocity = Vector2.zero;
                yield return null;
            }
            Vector2 dir = Lead(0.3f) - Me;
            if (dir.sqrMagnitude < 0.01f) yield break;
            dir.Normalize();
            Batch5.Face(transform, Me.x + dir.x);
            Batch2.Play(gameObject, "Swoop");
            BossAi.DevMoves++;
            // His body hurts all the way (his own swoop's pictures show it: the hitbox unseen); on until the room's rock.
            var escort = Batch2.Escort(this, transform, Me - (Vector2)transform.position, 2.2f, 1.45f, Look.Rock);
            try
            {
                for (float until = Time.time + 1.3f; Time.time < until && Active;)
                {
                    rb.linearVelocity = dir * 26f;
                    if (Hostiles.NativeRay(Me, dir, 0.7f).collider != null) break;
                    yield return null;
                }
            }
            finally
            {
                if (escort != null) Destroy(escort.gameObject);
            }
            rb.linearVelocity = Vector2.zero;
        }

        /// <summary>His Vengeflies up in the room now.</summary>
        int Vengeflies()
        {
            int n = 0;
            foreach (var info in Items.EnemyStats.All)
                if (info.Hm != null && !info.Hm.isDead && info.Record == "Buzzer" && info.Hm.gameObject.activeInHierarchy && info.Hm.gameObject.scene == gameObject.scene) n++;
            return n;
        }
    }
}
