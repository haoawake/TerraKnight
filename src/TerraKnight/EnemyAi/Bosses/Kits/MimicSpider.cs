using System.Collections;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// Nosk (Mimic Spider) and the Winged Nosk (Hornet Nosk; 空洞骑士首领新版AI设计.md §2.4):
    /// <list type="bullet">
    /// <item>from the roof he drops on where the player is as the drop is warned (marked under it), not where his crawl
    /// along the roof left him; the drips he lets fall up there (his own goo) come on the player too;</item>
    /// <item>his spit throws three more of his own goo globs at where the player is;</item>
    /// <item>now and then (and against a player kept up in the air) he leaps, hangs a moment over the player, and dives
    /// straight at where the player is as he hangs;</item>
    /// <item>the Winged Nosk's swoop dips to the player's height as it starts (marked along its way).</item>
    /// </list>
    /// He goes up into the roof, out of sight (Roams).
    /// </summary>
    [Kit("MimicSpider")]
    internal sealed class MimicSpiderKit : BossKit
    {
        protected override string ControlName => Batch3.FsmOn(gameObject, "Hornet Nosk") != null ? "Hornet Nosk" : "Mimic Spider";
        protected override bool Roams => true;

        bool Winged => Control != null && Control.FsmName == "Hornet Nosk";
        /// <summary>His own goo (his spit's), for the drips.</summary>
        GameObject gooShot;
        GameObject GooShot => gooShot != null ? gooShot : gooShot = OwnShots.Of(State("Spit 1"));

        protected override void Setup()
        {
            if (Winged)
            {
                At("Swoop Direction", 0, AimSwoop);
                return;
            }
            OnEnter("Fall Antic", LockDrop);
            OnEnter("Roof Drop", () => Run(Drips()), first: false);
            OnEnter("Spit 1", () => Run(GooAt()), first: false);
            AddState("TK Dive", Dive, "Land 2");
            Branch("Idle", "TK Dive", () => !Grace && (WantAntiAir() || Rank >= 1 && Random.value < 0.2f));
        }

        /// <summary>With his spit, three more of his own goo globs lobbed at where the player is (spread a little).</summary>
        IEnumerator GooAt()
        {
            if (GooShot == null || Grace) yield break;
            var at = OnGround(Lock());
            for (int i = -1; i <= 1; i++)
            {
                OwnShots.Lob(GooShot, Me + Vector2.up, at + new Vector2(i * 1.6f, 0f), 0.9f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        /// <summary>Out of the roof on the player: put over him (unseen up there) as the drop's wait begins.</summary>
        void LockDrop()
        {
            float x = Mathf.Clamp(Lock().x, Arena.xMin + 1.5f, Arena.xMax - 1.5f);
            var p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
            if (rb != null) rb.linearVelocity = Vector2.zero;
            MarkGround(new Vector2(x, TargetFeet.y + 0.3f), 6f, 1.3f);
        }

        /// <summary>While he crawls the roof: drips of his own goo on the player now and then.</summary>
        IEnumerator Drips()
        {
            yield return new WaitForSeconds(0.6f);
            float every = Rank >= 2 ? 0.7f : Rank >= 1 ? 0.9f : 1.1f;
            while (StateNow == "Roof Drop")
            {
                float x = Lock().x + Random.Range(-1f, 1f);
                float top = Mathf.Max(Me.y, CeilingAbove(new Vector2(x, TargetCenter.y + 1f), 40f) - 0.6f);
                OwnShots.Throw(GooShot, new Vector2(x, top), new Vector2(0f, -2f));
                yield return new WaitForSeconds(every);
            }
        }

        /// <summary>Up, a hang over the player (the line of his dive drawn), and straight down at where he was then.</summary>
        IEnumerator Dive()
        {
            if (rb == null) yield break;
            float gravity = rb.gravityScale;
            try
            {
                Batch3.Play(this, "Jump");
                rb.gravityScale = 0f;
                float ceiling = CeilingAbove(Me, 40f);
                float peak = Mathf.Min(ceiling - 2f, Mathf.Max(Me.y + 4f, TargetCenter.y + 4f));
                for (float t = 0f; t < 1f && Me.y < peak; t += Time.deltaTime)
                {
                    rb.linearVelocity = new Vector2(0f, 28f);
                    yield return null;
                }
                rb.linearVelocity = Vector2.zero;
                float hang = Warn(0.35f);
                Vector2 target = Lock();
                Vector2 dir = target - Me;
                // Down at him, or across (a little up at most) at a player level with him under the ceiling.
                if (dir.y > 0.3f * dir.magnitude) dir.y = 0.3f * dir.magnitude;
                dir.Normalize();
                var hit = Hostiles.NativeRay(Me, dir, 40f);
                MarkLine(Me, hit.collider != null ? hit.point : Me + dir * 25f, hang);
                for (float t = 0f; t < hang; t += Time.deltaTime)
                {
                    rb.linearVelocity = Vector2.zero;
                    yield return null;
                }
                float half = body != null ? body.bounds.extents.y : 1f;
                for (float t = 0f; t < 1.2f; t += Time.fixedDeltaTime)
                {
                    rb.linearVelocity = dir * 38f;
                    yield return new WaitForFixedUpdate();
                    if (dir.y < 0f && Hostiles.NativeRay(Me, Vector2.down, half + 0.25f).collider != null) break;
                    if (Hostiles.NativeRay(Me, dir, half + 0.4f).collider != null) break;
                }
                rb.linearVelocity = Vector2.zero;
            }
            finally
            {
                if (rb != null) { rb.linearVelocity = Vector2.zero; rb.gravityScale = gravity; }
            }
        }

        /// <summary>The Winged Nosk's swoop brought down (or up) to where the player is as it starts.</summary>
        void AimSwoop()
        {
            var height = Float("Swoop Height");
            if (height == null) return;
            Vector2 at = Lock();
            float ground = GroundBelow(at);
            height.Value = Mathf.Max(ground + 1.2f, at.y - 0.2f);
            float dir = Mathf.Sign(at.x - Me.x);
            if (dir == 0f) dir = 1f;
            MarkLine(new Vector2(Me.x, height.Value), new Vector2(Me.x + dir * 18f, height.Value), Warn(0.45f));
        }
    }
}
