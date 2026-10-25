using System.Collections;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// What the kits of the Troupe, the Grey Prince and Godhome's nailmasters share (Grimm.cs, NightmareGrimm.cs,
    /// GreyPrince.cs, NailBros.cs, Paintmaster.cs, Nailsage.cs).
    /// </summary>
    internal static class Batch5
    {
        /// <summary>A body turned to face a point: its sprite faces left at a positive x scale, unless it faces right.</summary>
        public static void Face(Transform t, float x, bool facesRight = false)
        {
            var s = t.localScale;
            float side = x >= t.position.x ? 1f : -1f;
            s.x = Mathf.Abs(s.x) * (facesRight ? side : -side);
            t.localScale = s;
        }

        /// <summary>The first FloatCompare's threshold in a state (a height a jump goes to), or null.</summary>
        public static FsmFloat CompareTo(FsmState s)
        {
            if (s == null) return null;
            foreach (var a in s.Actions)
                if (a is HutongGames.PlayMaker.Actions.FloatCompare fc) return fc.float2;
            return null;
        }

        /// <summary>
        /// A nailmaster's jump (his Jump Y Dstab up, Jump X across, Topslash Y where it turns into its strike) as fast as it
        /// takes to reach a raised top, under the player there: the room's gravity (60) keeps his own leap well short of it.
        /// His own speed when the top is his own.
        /// </summary>
        public static void HighJump(BossKit kit, Rigidbody2D rb, float ownTop, float ownJumpY)
        {
            var jy = kit.Float("Jump Y Dstab");
            var top = kit.Float("Topslash Y");
            if (jy == null) return;
            jy.Value = ownJumpY;
            if (top == null || rb == null || top.Value <= ownTop + 0.01f) return;
            float g = Batch2.GravityOf(rb);
            float h = top.Value + 0.6f - kit.transform.position.y;
            if (h <= 0f) return;
            float vy = Mathf.Sqrt(2f * g * h);
            if (vy <= ownJumpY) return;
            jy.Value = vy;
            var jx = kit.Float("Jump X");
            float hx = kit.Float("Hero X")?.Value ?? kit.TargetCenter.x;
            // Across as far as the game's own jump goes (its Jump X, 2.75 a unit apart): at 30 a player up at the arena's far
            // side was struck at from 11 units short of him.
            if (jx != null) jx.Value = Mathf.Clamp((hx - kit.transform.position.x) * g / vy, -60f, 60f);
        }

        /// <summary>
        /// A raised jump that stops short of its top (the ceiling over him, his leap spent): its strike where it is, at the
        /// top of the leap, as the game's own at its top.
        /// </summary>
        public static IEnumerator TopOut(BossKit kit, Rigidbody2D rb, float ownTop, string jump = "Jump")
        {
            var top = kit.Float("Topslash Y");
            if (top == null || top.Value <= ownTop + 0.01f) yield break;
            yield return null;
            while (kit.Active && kit.StateNow == jump && rb != null)
            {
                if (rb.linearVelocity.y <= 0.5f && kit.transform.position.y < top.Value)
                {
                    top.Value = kit.transform.position.y - 0.05f;
                    yield break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// A nailmaster's "Topslash Y" (the height his jumps and cyclone go to before he strikes down; his transform's, his
        /// body hangs well under it): over the player kept up in the air (his feet more than three units over the ground),
        /// his body's underside just over the player's head, so the strike comes down onto him (a top 1.5 over the player's
        /// middle stopped his body level with the player's face, the strike falling past under him: the user's, 10-25);
        /// under the room's own ceiling over the player (open above within reach: no cap), never under the game's own.
        /// </summary>
        public static void RaiseTop(BossKit kit, FsmFloat top, float own)
        {
            if (top == null) return;
            if (kit.TargetHeight < 3f) { top.Value = own; return; }
            var body = kit.GetComponent<Collider2D>();
            bool sized = body != null && body.enabled;
            float under = sized ? kit.transform.position.y - body.bounds.min.y : 1.6f;
            float over = sized ? body.bounds.max.y - kit.transform.position.y : 0.8f;
            float head = 2f * kit.TargetCenter.y - kit.TargetFeet.y;
            float want = head + under + 0.4f;
            var roof = Hostiles.NativeRay(new Vector2(kit.TargetCenter.x, head), Vector2.up, 40f);
            if (roof.collider != null) want = Mathf.Min(want, roof.point.y - over - 0.2f);
            top.Value = Mathf.Max(own, want);
        }

        /// <summary>
        /// Something flying straight sideways (a firebat) carried up or down as it goes, so it crosses a height over a time
        /// (no faster than <paramref name="maxVy"/> units a second); left alone once there, or once gone back to its pool.
        /// </summary>
        public static IEnumerator Steer(GameObject go, float fromY, float toY, float seconds, float maxVy)
        {
            float vy = Mathf.Clamp((toY - fromY) / Mathf.Max(0.05f, seconds), -maxVy, maxVy);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (go == null || !go.activeInHierarchy) yield break;
                go.transform.position += new Vector3(0f, vy * Time.deltaTime, 0f);
                yield return null;
            }
        }

        static readonly Collider2D[] near = new Collider2D[24];

        /// <summary>
        /// The thrown things round a point just let go (a burst's fireballs, a splash's paint): their sideways speed moved by
        /// <paramref name="dx"/> (bodies with a rigidbody of their own that hurt, not the thrower, not the player).
        /// </summary>
        public static int Bias(Vector2 at, float radius, float dx, GameObject thrower)
        {
            int n = Physics2D.OverlapCircleNonAlloc(at, radius, near);
            int moved = 0;
            var hero = HeroController.SilentInstance;
            for (int i = 0; i < n; i++)
            {
                var c = near[i];
                if (c == null) continue;
                var rb = c.attachedRigidbody;
                if (rb == null || rb.bodyType == RigidbodyType2D.Static) continue;
                var go = rb.gameObject;
                if (go == thrower || hero != null && go == hero.gameObject || go.GetComponent<HealthManager>() != null) continue;
                if (go.GetComponentInChildren<DamageHero>() == null) continue;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x + dx, rb.linearVelocity.y);
                moved++;
            }
            return moved;
        }

        /// <summary>A float field of an action (an HK action's speed), multiplied, by its name (no-op if it has none).</summary>
        public static bool ScaleField(FsmStateAction a, string field, float by)
        {
            var f = a?.GetType().GetField(field);
            if (f == null || f.FieldType != typeof(FsmFloat)) return false;
            var v = (FsmFloat)f.GetValue(a);
            if (v == null || v.UsesVariable) return false;
            v.Value *= by;
            return true;
        }

        public static readonly WaitForSeconds Drip = new WaitForSeconds(0.07f);
    }
}
