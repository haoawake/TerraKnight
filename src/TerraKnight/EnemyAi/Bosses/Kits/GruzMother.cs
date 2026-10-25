using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Gruz Mother (Big Fly Control; 空洞骑士首领新版AI设计.md §2.1): her moves as they are, a little more often (pace
    /// ×0.85); her charge's way, taken at its wind-up's start as the game's own does, drawn as a line for that wind-up; from the
    /// third rank her bouncing run 15% faster; a player kept up in the air (hanging under the ceiling, on a platform) gets
    /// her charge more often than the bouncing run (floor to ceiling), the charge aimed afresh as it sets off.
    /// </summary>
    [Kit("BigFly")]
    internal sealed class GruzMotherKit : BossKit
    {
        protected override string ControlName => "Big Fly Control";
        protected override float Pace => 0.85f;
        protected override Look Theme => Look.Rock;

        float slamSpeed = 50f;

        protected override void Setup()
        {
            Tempo("Buzz", "Super End", "Slam End");
            var s = Float("Slam Speed");
            if (s != null) slamSpeed = s.Value;
            OnEnter("Charge Antic", ChargeLine, first: false);
            OnEnter("Slam Antic", () =>
            {
                var v = Float("Slam Speed");
                if (v != null) v.Value = slamSpeed * (Rank >= 2 ? 1.15f : 1f);
            });
            Branch("Super Choose", "Slam Antic", () => (Int("Slams In A Row")?.Value ?? 0) < 2 && WantAntiAir());
            // Checked first: most of the time the charge, straight at him.
            Branch("Super Choose", "Charge Antic", () => (Int("Charges In A Row")?.Value ?? 0) < 3 && AirCamping && Random.value < 0.6f);
            // The player behind rock from where she is: during her charge's wind-up she flies round to a clear line first.
            OnEnter("Charge Antic", () =>
            {
                if (!HasTarget || Grace || !ClearSpot(Lead(0.8f), 9f, Round, out var spot)) return;
                moved = true;
                Run(FlyTo(spot, 0.6f, 24f));
            }, first: false);
            OnEnter("Charge", AimAgain);
            // Her charge ends only on hitting the room: one sent out into open air (up past the arena) comes round after a while.
            OnEnter("Charge", () => Run(ChargeLimit()), first: false);
        }

        static readonly float[] Round = { -150f, -120f, -90f, -60f, -30f, 0f, 30f, 150f, 180f };
        /// <summary>Still charging after 1.6 s: ended as if she had hit the room the way she is flying.</summary>
        System.Collections.IEnumerator ChargeLimit()
        {
            float until = Time.time + 1.6f;
            while (Time.time < until)
            {
                if (!Active || StateNow != "Charge") yield break;
                yield return null;
            }
            if (rb == null || StateNow != "Charge") yield break;
            var v = rb.linearVelocity;
            string side = Mathf.Abs(v.y) > Mathf.Abs(v.x) ? (v.y > 0f ? "UP" : "DOWN") : (v.x > 0f ? "RIGHT" : "LEFT");
            Control.SendEvent(side);
        }

        bool moved;

        /// <summary>
        /// The player behind the room's rock from here: a free spot with a clear line to him (round him, the nearest to her
        /// first), to fly to before she goes at him.
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

        /// <summary>Flown to a spot (her own flight) through her wind-up, for a while at most.</summary>
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

        /// <summary>A player up off the floor (or she flew round first): her charge's way taken again as she sets off, at where he will be.</summary>
        void AimAgain()
        {
            bool was = moved;
            moved = false;
            var a = Float("Charge Angle");
            if (a == null || !HasTarget || !(AirCamping || was) || Grace) return;
            var d = Lead(0.3f) - Me;
            if (d.sqrMagnitude > 0.01f) a.Value = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        /// <summary>The charge's way (Charge Angle, at the player as the wind-up starts) out to the room's wall.</summary>
        void ChargeLine()
        {
            var a = Float("Charge Angle");
            if (a == null) return;
            MarkLine(Me, Batch1Helpers.RayEnd(Me, a.Value), 0.75f);
        }
    }
}

