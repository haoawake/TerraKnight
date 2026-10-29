using System.Collections;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Hollow Knight (Control, its Phase 1–4; 空洞骑士首领新版AI设计.md §2.9). His own three parts go on as they did, his
    /// own moves sharper (nothing of the mod's drawn: the user's, 2026-10-23, only his own pictures):
    /// <list type="bullet">
    /// <item>his jumps and stomps sometimes land where the player is going; his stands between moves at the kit's pace;</item>
    /// <item>against a player up off the floor (a platform, under the ceiling), in his first three parts: his rising slash
    /// (his own slash, its hitbox with him), straight from his choice or after a dash, up as high as the player is and
    /// across to him;</item>
    /// <item>from the second part: after the stomp's plumes, more of his own plumes one after another from under him to where
    /// the player was; the infection arcs swept so their middle comes at the player; a dash that ends under the player (or
    /// one kept up in the air) goes on up in a rising slash (his own slash, its hitbox with him);</item>
    /// <item>from the third: each self-stab throws a ring of his own infection shots, a gap in it (two rings the first
    /// time); every bounce of the puppet slams aims at where the player is as it rises;</item>
    /// <item>the last part (his weak swings, Hornet's binding, the endings) as it was.</item>
    /// </list>
    /// </summary>
    [Kit("HollowKnight")]
    internal sealed class HollowKnightKit : BossKit
    {
        protected override Look Theme => Look.Infection;

        FsmInt phase;
        int Phase => phase != null ? phase.Value : 1;
        GameObject lockMark, heroWas;

        static readonly WaitForSeconds wait012 = new WaitForSeconds(0.12f), wait035 = new WaitForSeconds(0.35f);

        /// <summary>His own plume (his stomp's Plume Gen's) and infection shot (his arcs').</summary>
        GameObject plume, shot;
        GameObject Plume => plume != null ? plume : plume = OwnShots.Of(State("Plume Gen"));
        GameObject Shot => shot != null ? shot : shot = OwnShots.Of(State("SmallShot LowHigh"));

        protected override void Setup()
        {
            phase = Int("Phase");
            // His stands between moves, at the kit's pace (his idle is a timer, not a wait: the time it waits for, shortened).
            At("Set Idle Time", 2, () => { var t = Float("Idle Time"); if (t != null) t.Value *= TempoNow; });
            Tempo("Dash Recover");
            // His jumps at the player: sometimes where he is going.
            At("Dstab Aim", 2, () => LeadHeroX(0.6f));
            At("Aim At Hero", 3, () => LeadHeroX(0.8f));
            // The second part.
            OnEnter("SmallShot LowHigh", AimArc);
            OnEnter("SmallShot HighLow", AimArc);
            if (AddState("TK Rising Slash", RisingSlash, "Idle Stance") != null)
            {
                Branch("Dash End", "TK Rising Slash", WantRise);
                foreach (var c in new[] { "Choice P1", "Choice P2", "Choice P3" })
                    if (Batch2.Has(Control, c)) Branch(c, "TK Rising Slash", RiseAtHigh);
            }
            // The third.
            OnEnter("Puppet Up", LockBounce);
            OnEnter("Puppet ReUp", LockBounce);
            foreach (var s in new[] { "PuppetSlam", "Puppet End", "Stun Start", "Idle Stance", "Fall Down", "Quick Stun" }) OnEnter(s, FreeBounce);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            FreeBounce();
        }

        void LeadHeroX(float seconds)
        {
            var x = Float("Hero X");
            if (x != null && RollLead()) x.Value = Lead(seconds).x;
        }

        // ------------------------------------------------------------------ the second part

        /// <summary>The infection arc swept so its middle comes at the player (when he is on the side it sweeps).</summary>
        void AimArc()
        {
            if (Phase < 2) return;
            var ang = Float("Angle");
            var tween = Float("Angle Tween");
            if (ang == null || tween == null) return;
            Vector2 to = Lock() - Me;
            float facing = transform.localScale.x >= 0f ? 1f : -1f;
            if (Mathf.Sign(to.x) != facing) return;
            float a = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            if (facing > 0f)
            {
                // The right's sweeps run 285°..425° (down-right up through right): a little lob up.
                if (a <= 90f) a += 360f;
                a = Mathf.Clamp(a + 12f, 280f, 440f);
            }
            else a = Mathf.Clamp(a - 12f, 110f, 255f);
            // It sweeps Angle Tween a second for 1.5 s: its middle three quarters of a second in.
            ang.Value = a - tween.Value * 0.75f;
        }



        /// <summary>A dash that ends under the player (or one kept up in the air) goes on up.</summary>
        bool WantRise()
        {
            if (Phase > 3 || Grace || rb == null || !HasTarget) return false;
            Vector2 d = TargetCenter - Me;
            if (d.y > 2.5f && Mathf.Abs(d.x) < 7f) return true;
            return AirCamping && Mathf.Abs(d.x) < 14f && WantAntiAir();
        }

        /// <summary>From his choice: a player well over him (a platform, under the ceiling) gets the rising slash now and then.</summary>
        bool RiseAtHigh()
        {
            if (Phase > 3 || Grace || rb == null || !HasTarget) return false;
            Vector2 d = TargetCenter - Me;
            return d.y > 3f && Mathf.Abs(d.x) < 14f && WantAntiAir();
        }

        /// <summary>
        /// The rising slash: his own slash's wind-up (no longer than its own picture), then up after where the player was in
        /// his own slash: its hitbox (Slash1's, the one a parry answers), its sparks and its sound, its recovery from the top
        /// of the rise down (the user's 10-25: no effect, and held a while).
        /// </summary>
        IEnumerator RisingSlash()
        {
            Vector2 at = Lock();
            float dir = at.x >= Me.x ? 1f : -1f;
            // His slash (over his head) at the player at the top of the rise: his middle a little under him.
            // (The ceiling over where he goes up to, not over where he starts: a ledge over him is not in his way across.)
            float top = Mathf.Min(at.y - 1.5f, CeilingAbove(new Vector2(at.x, Me.y)) - 1.2f);
            float warn = Mathf.Clamp(ClipSeconds("Slash1 Antic"), 0.2f, Warn(0.45f));
            MarkLine(Me, new Vector2(Me.x + dir * Mathf.Min(3f, Mathf.Abs(at.x - Me.x)), top), warn);
            // Facing the player (his sprite faces right at a positive scale), as his own slash's wind-up turns him.
            var scale = transform.localScale;
            transform.localScale = new Vector3(Mathf.Abs(scale.x) * dir, scale.y, scale.z);
            Play("Slash1 Antic");
            rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
            yield return new WaitForSeconds(warn);
            float g = Mathf.Max(1f, -Physics2D.gravity.y * rb.gravityScale);
            float h = Mathf.Clamp(top - Me.y, 2f, 30f);
            float vy = Mathf.Sqrt(2f * g * h);
            // Across to under him by the top of the rise.
            float vx = Mathf.Clamp((at.x - Me.x) / Mathf.Max(0.2f, vy / g), -26f, 26f);
            rb.linearVelocity = new Vector2(vx, vy);
            Play("Slash1");
            OwnSlash(true);
            var m = NewMove(2, Look.Void);
            m.Width = 7f;
            m.Height = 8f;
            // Live all the way up.
            var z = Strike(Hostiles.Zone.Shape.Box, Me + Vector2.up * 1.5f, 0.05f, Mathf.Max(0.45f, vy / g + 0.15f), m);
            // His own slash animation shows it: the hitbox unseen.
            if (z != null) { z.Follow = transform; z.FollowOffset = Me + Vector2.up * 1.5f - (Vector2)transform.position; z.Unseen = true; }
            float until = Time.time + 1.4f;
            bool recovering = false;
            yield return null;
            yield return null;
            try
            {
                while (Time.time < until && !(rb.linearVelocity.y <= 0.01f && Hostiles.NativeRay(Me, Vector2.down, body != null ? body.bounds.extents.y + 0.2f : 1.2f).collider != null))
                {
                    // The top of the rise: his slash's own recovery on the way down.
                    if (!recovering && rb.linearVelocity.y <= 0f)
                    {
                        recovering = true;
                        OwnSlash(false);
                        Play("Slash1 Recover");
                    }
                    yield return null;
                }
            }
            finally
            {
                OwnSlash(false);
            }
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            Play("Idle");
        }

        /// <summary>His own slash on or off: Slash1's hitbox; on, its sparks (Pt Slash1) and its sound, as his Slash1 plays them.</summary>
        void OwnSlash(bool on)
        {
            var slash = Go("Slash1")?.Value;
            var poly = slash != null ? slash.GetComponent<PolygonCollider2D>() : null;
            if (poly != null) poly.enabled = on;
            if (!on) return;
            var pt = Go("Pt Slash1")?.Value;
            var sparks = pt != null ? pt.GetComponent<ParticleSystem>() : null;
            if (sparks != null) sparks.Play();
            var sound = Batch3.Find<AudioPlayerOneShotSingle>(State("Slash1"));
            if (sound == null || sound.audioPlayer == null || sound.audioPlayer.Value == null || !(sound.audioClip?.Value is AudioClip clip)) return;
            var player = ObjectPool.Spawn(sound.audioPlayer.Value, transform.position, Quaternion.Euler(Vector3.up));
            var source = player != null ? player.GetComponent<AudioSource>() : null;
            if (source == null) return;
            source.pitch = Random.Range(sound.pitchMin.Value, sound.pitchMax.Value);
            source.volume = sound.volume.Value;
            source.PlayOneShot(clip);
        }

        /// <summary>How long one of his own clips runs (its frames at its pace).</summary>
        float ClipSeconds(string name)
        {
            var a = GetComponent<tk2dSpriteAnimator>();
            var c = a != null ? a.GetClipByName(name) : null;
            return c != null && c.fps > 0f && c.frames != null ? c.frames.Length / c.fps : 0.4f;
        }

        void Play(string clip)
        {
            var a = GetComponent<tk2dSpriteAnimator>();
            if (a != null && a.GetClipByName(clip) != null) a.Play(clip);
        }

        // ------------------------------------------------------------------ the third part



        /// <summary>Each bounce of the puppet slams goes after where the player is as it rises, not after him.</summary>
        void LockBounce()
        {
            if (Phase != 3) return;
            var hero = Go("Hero");
            if (hero == null) return;
            if (lockMark == null)
            {
                lockMark = new GameObject("TK HK Bounce Lock");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lockMark, gameObject.scene);
            }
            if (hero.Value != lockMark) heroWas = hero.Value;
            Vector2 at = Lock();
            lockMark.transform.position = at;
            hero.Value = lockMark;
            MarkGround(at, 6f, Warn(0.8f));
        }

        void FreeBounce()
        {
            var hero = Go("Hero");
            if (hero != null && lockMark != null && hero.Value == lockMark) hero.Value = heroWas != null ? heroWas : HeroController.SilentInstance?.gameObject;
            heroWas = null;
        }
    }
}
