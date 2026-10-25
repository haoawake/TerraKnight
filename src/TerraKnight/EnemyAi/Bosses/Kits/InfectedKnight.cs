using System.Collections;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Broken Vessel and the Lost Kin (IK Control; 空洞骑士首领新版AI设计.md §2.4):
    /// <list type="bullet">
    /// <item>its dashes go where the player is (sometimes where he is going) as their wind-up begins, its way drawn: on the
    /// ground at his side of the room, in the air straight at him, at a slant if need be; a dash up into the ceiling
    /// turns back down;</item>
    /// <item>it lets an infection balloon go as it springs back (the Lost Kin two);</item>
    /// <item>its overhead slash comes round to where the player was as it wound up;</item>
    /// <item>a new move, now and then and against a player kept up in the air: it leaps to a wall, clings a moment with its
    /// line drawn, and dashes off the wall at him;</item>
    /// <item>while it shakes out its infection, its own blobs fall on the player too;</item>
    /// <item>a player well up (on a high platform, hanging under the ceiling) gets its air dash at his level: it leaps up
    /// to his height and dashes across at him from there; its wall cling is at his height too;</item>
    /// <item>the Lost Kin is quicker (waits ×0.85).</item>
    /// </list>
    /// </summary>
    [Kit("InfectedKnight")]
    internal sealed class InfectedKnightKit : BossKit
    {
        protected override string ControlName => "IK Control";
        bool LostKin => Dream || Scene == "GG_Lost_Kin";
        protected override float Pace => LostKin ? 0.85f : 1f;

        /// <summary>Its dashes' upward speed (its script's dash sets only the sideways one): 0 on the ground.</summary>
        readonly FsmFloat dashY = new FsmFloat { Value = 0f };
        bool air;
        Vector2 slashAt;
        GameObject balloon;
        /// <summary>Its own blobs, the ones its shaking drops (its Shake Projectiles script's).</summary>
        GameObject blobShot;
        bool blobLooked;
        /// <summary>Its air dash's own height while one is taken up to a player well up (restored as it comes down).</summary>
        float savedAirDash = float.NaN;
        GameObject BlobShot
        {
            get
            {
                if (blobLooked) return blobShot;
                blobLooked = true;
                var shake = FindFsm("Shake Projectiles U");
                return blobShot = shake != null ? OwnShots.Of(State("Spawn", shake)) : null;
            }
        }

        protected override void Setup()
        {
            foreach (var s in new[] { "Dash Attack", "Dash Attack 2", "Dash Attack 3" })
            {
                var v = Batch3.Find<SetVelocity2d>(State(s));
                if (v != null) v.y = dashY;
            }
            OnEnter("Dash Antic", () => { air = false; dashY.Value = 0f; });
            OnEnter("Set Height", () => air = true);
            OnEnter("Dash Antic 2", AimDash);
            OnEnter("Dash Recover", () => dashY.Value = 0f);
            // A player well up: the dash it picks is the air one, and up at his level.
            Branch("Air Dash?", "Jump Antic", HighDash);
            OnEnter("Jump", LeapHigh, first: false);
            OnEnter("Dash Recover", RestoreAirDash);
            OnEnter("Land", RestoreAirDash);
            // Its waits between moves count down a float (Idle Counter), not a Wait: shortened where it is set.
            At("Set Counter", 1, () => { var c = Float("Idle Counter"); if (c != null) c.Value *= TempoNow; });
            OnEnter("Evade Dash", Balloons);
            OnEnter("Ohead Antic", () => slashAt = Lock());
            At("Ohead Slashing", 0, () => Batch3.Face(transform, slashAt.x - Me.x, true));
            OnEnter("Shaking Spawn", () => Run(Rain()), first: false);
            AddState("TK Wall Dash", WallDash, "Set Counter");
            Branch("Attack Choice", "TK Wall Dash", () => !Grace && (WantAntiAir() || Random.value < 0.18f));
            var spawner = FindFsm("Spawn Balloon");
            balloon = Batch3.PoolPrefab(spawner, "Spawn");
        }

        float DashSpeed => Mathf.Abs(Float("Dash Speed")?.Value ?? 32f);

        /// <summary>The player well over the floor (3 and more): its air dash at his height, as long as the room has it.</summary>
        bool HighDash()
        {
            RestoreAirDash();
            var h = Float("Air Dash Height");
            var will = Bool("Will Air Dash");
            if (h == null || will == null || !HasTarget || Grace) return false;
            float floor = GroundBelow(Me + Vector2.up * 0.3f);
            if (TargetCenter.y - floor < 3f || TargetCenter.y <= h.Value + 1f) return false;
            float half = body != null ? body.bounds.extents.y : 1.2f;
            float top = CeilingAbove(new Vector2(TargetCenter.x, TargetCenter.y), 30f);
            savedAirDash = h.Value;
            h.Value = Mathf.Min(TargetCenter.y, top - half - 0.3f);
            will.Value = true;
            BossAi.DevAntiAir++;
            return true;
        }

        /// <summary>The jump strong enough to rise past the air dash's height taken up to the player.</summary>
        void LeapHigh()
        {
            if (float.IsNaN(savedAirDash) || rb == null) return;
            var h = Float("Air Dash Height");
            if (h == null) return;
            float g = Batch2.GravityOf(rb);
            float rise = h.Value - Me.y + 1.5f;
            if (rise <= 0f) return;
            float vy = Mathf.Sqrt(2f * g * rise);
            var v = rb.linearVelocity;
            if (vy > v.y) rb.linearVelocity = new Vector2(v.x * 0.5f, vy);
        }

        void RestoreAirDash()
        {
            if (float.IsNaN(savedAirDash)) return;
            var h = Float("Air Dash Height");
            if (h != null) h.Value = savedAirDash;
            savedAirDash = float.NaN;
        }

        /// <summary>The dash's way at the start of its wind-up: at the player's side (on the ground), at him (in the air).</summary>
        void AimDash()
        {
            Vector2 target = MaybeLead(0.8f);
            float antic = LostKin ? 0.4f : 0.65f;
            var crt = Float("Dash Speed Crt");
            if (crt == null) return;
            if (!air)
            {
                float dir = Mathf.Sign(target.x - Me.x);
                if (dir == 0f) dir = 1f;
                Batch3.Face(transform, dir, true);
                crt.Value = DashSpeed * dir;
                dashY.Value = 0f;
                MarkLine(Me, new Vector2(WallTowards(Me, dir, 40f), Me.y), Mathf.Max(antic, BossAi.MinWarn));
                return;
            }
            Vector2 v = target - Me;
            if (v.sqrMagnitude < 0.01f) v = Vector2.down;
            v.Normalize();
            Batch3.Face(transform, v.x, true);
            crt.Value = v.x * DashSpeed;
            dashY.Value = v.y * DashSpeed;
            var hit = Hostiles.NativeRay(Me, v, 40f);
            MarkLine(Me, hit.collider != null ? hit.point : Me + v * 30f, Mathf.Max(antic, BossAi.MinWarn));
        }

        protected override void Tick()
        {
            // A dash up into the ceiling turns back down.
            if (dashY.Value > 0f && body != null && StateNow != null && StateNow.StartsWith("Dash Attack")
                && Hostiles.NativeRay(Me, Vector2.up, body.bounds.extents.y + 0.3f).collider != null)
                dashY.Value = -dashY.Value;
        }

        /// <summary>A balloon (two for the Lost Kin) let go as it springs back, while the room has few.</summary>
        void Balloons()
        {
            if (balloon == null) return;
            int max = LostKin ? 5 : 3;
            if (GameObject.FindGameObjectsWithTag("Extra Tag").Length >= max) return;
            int n = LostKin ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                var b = ObjectPool.Spawn(balloon, (Vector3)(Me + new Vector2((i - (n - 1) / 2f) * 1.5f, 1.5f)));
                if (b != null) BossAi.DevMoves++;
            }
        }

        /// <summary>While it shakes: its own blobs from the ceiling on the player.</summary>
        IEnumerator Rain()
        {
            float every = LostKin ? 0.45f : 0.6f;
            yield return new WaitForSeconds(0.4f);
            while (StateNow == "Shaking Spawn")
            {
                float x = Lock().x + Random.Range(-1.5f, 1.5f);
                float top = CeilingAbove(new Vector2(x, TargetCenter.y + 1f), 40f) - 0.6f;
                OwnShots.Throw(BlobShot, new Vector2(x, top), new Vector2(0f, -2f));
                yield return new WaitForSeconds(every);
            }
        }

        /// <summary>To the wall behind it, a cling with its line drawn, and a dash off the wall at the player.</summary>
        IEnumerator WallDash()
        {
            if (rb == null) yield break;
            float gravity = rb.gravityScale;
            bool complete = false;
            try
            {
                float dir = Mathf.Sign(Me.x - TargetCenter.x);
                if (dir == 0f) dir = 1f;
                float wall = WallTowards(Me, dir, 40f);
                float top = CeilingAbove(Me, 40f);
                // At the player's height when he is up there (a high platform, under the ceiling).
                Vector2 cling = new Vector2(wall - dir * 0.9f, Mathf.Min(top - 2f, Mathf.Max(Me.y + 6f, TargetCenter.y)));
                Batch3.Play(this, "Jump");
                Batch3.Face(transform, dir, true);
                rb.gravityScale = 0f;
                Vector2 from = Me;
                for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    rb.linearVelocity = (cling - from) / 0.35f;
                    yield return null;
                }
                rb.linearVelocity = Vector2.zero;
                Batch3.Play(this, "Dash Antic 2");
                float warn = Warn(0.5f);
                Vector2 target = MaybeLead(warn + 0.3f);
                Vector2 v = target - Me;
                if (v.sqrMagnitude < 0.01f) v = new Vector2(-dir, -0.3f);
                v.Normalize();
                Batch3.Face(transform, v.x, true);
                var hit = Hostiles.NativeRay(Me, v, 40f);
                MarkLine(Me, hit.collider != null ? hit.point : Me + v * 30f, warn);
                for (float t = 0f; t < warn; t += Time.deltaTime)
                {
                    rb.linearVelocity = Vector2.zero;
                    yield return null;
                }
                Batch3.Play(this, "Dash Attack 1");
                float speed = DashSpeed;
                float half = body != null ? body.bounds.extents.y : 1f;
                for (float t = 0f; t < 0.7f; t += Time.fixedDeltaTime)
                {
                    rb.linearVelocity = v * speed;
                    yield return new WaitForFixedUpdate();
                    if (v.y < 0f && Hostiles.NativeRay(Me, Vector2.down, half + 0.2f).collider != null) break;
                }
                rb.linearVelocity = Vector2.zero;
                rb.gravityScale = Float("Gravity")?.Value ?? 2.5f;
                Batch3.Play(this, "Land");
                yield return new WaitForSeconds(0.25f);
                complete = true;
            }
            finally
            {
                if (rb != null) { rb.gravityScale = complete ? Float("Gravity")?.Value ?? 2.5f : gravity; rb.linearVelocity = Vector2.zero; }
            }
        }
    }
}
