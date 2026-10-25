using System.Collections;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Traitor Lord (Mantis; 空洞骑士首领新版AI设计.md §2.5):
    /// <list type="bullet">
    /// <item>at the top of his jump his dive's way is drawn: with the player under him he dives at him (sometimes where he
    /// is going), at any angle down; with the player level or over him he dashes up at him first, then, drawn again, dives;</item>
    /// <item>his two sickles fly on the line to where the player was (or is going), there and back;</item>
    /// <item>the shockwaves across the floor go 1.4 times as fast;</item>
    /// <item>a player kept up in the air gets the jump (and so the dash up) next.</item>
    /// </list>
    /// </summary>
    [Kit("TraitorLord")]
    internal sealed class TraitorLordKit : BossKit
    {
        protected override string ControlName => "Mantis";
        protected override Look Theme => Look.Blade;

        /// <summary>The vanilla dive's speed (40 across, 40 down).</summary>
        const float DiveSpeed = 56.6f;

        Vector2 diveAt, sickleAt;
        bool aimed, skyDone;
        FsmFloat diveY;

        protected override void Setup()
        {
            Tempo("Cooldown", "Slam End", "Sick Throw CD");
            var waves = State("Waves");
            for (int n = 0; n < 2 && waves != null; n++)
            {
                var x = Batch4.FloatField(Batch4.Find(waves, "SetVelocity2d", n), "x");
                if (x != null && !x.UseVariable) x.Value *= 1.4f;
            }
            OnEnter("Jump Antic", () => skyDone = false);
            OnEnter("DSlash Antic", AimDive);
            var dslash = State("DSlash");
            diveY = Batch4.FloatField(Batch4.Find(dslash, "SetVelocity2d"), "y");
            int vi = Batch4.IndexOf(dslash, "SetVelocity2d");
            if (vi >= 0) At("DSlash", vi, SteerDive);
            if (AddState("TK Sky Dash", SkyDash, "DSlash Antic") != null)
                Branch("DSlash", "TK Sky Dash", () => !skyDone && diveAt.y > Me.y - 1f);
            Branch("Attack Choice", "Jump Antic", WantAntiAir);
            OnEnter("Sickle Antic", AimSickles);
            var throwing = State("Sickle Throw");
            int second = Batch4.IndexOf(throwing, "SetVelocity2d", 1), first = Batch4.IndexOf(throwing, "SetVelocity2d", 0);
            if (second >= 0) At("Sickle Throw", second + 1, TiltThrown);
            if (first >= 0) At("Sickle Throw", first + 1, TiltThrown);
        }

        /// <summary>The top of the jump: where the dive (or the dash up) goes, drawn.</summary>
        void AimDive()
        {
            aimed = true;
            bool below = TargetCenter.y < Me.y - 1f;
            diveAt = below ? MaybeLead(0.35f) : Lock();
            MarkLine(Me, diveAt, Warn(0.45f));
        }

        /// <summary>The dive's speed, as the state sets it: at the drawn point, any angle down (never back past his facing).</summary>
        void SteerDive()
        {
            var crt = Float("DSlash Speed Crt");
            if (crt == null || diveY == null) return;
            float facing = Mathf.Sign(transform.localScale.x);
            Vector2 d = diveAt - Me;
            if (!aimed || d.y > -0.5f)
            {
                diveY.Value = -40f;
                crt.Value = 40f * facing;
                aimed = false;
                return;
            }
            d.Normalize();
            if (d.y > -0.35f) d = new Vector2(Mathf.Sign(d.x) * 0.94f, -0.35f);
            float vx = d.x * DiveSpeed;
            if (vx * facing < 0f) vx = 0f;
            crt.Value = vx;
            diveY.Value = d.y * DiveSpeed;
            aimed = false;
        }

        /// <summary>The dash up at a player over him (his body hits as the dive's), to the drawn point or a ceiling, then a moment's pause.</summary>
        IEnumerator SkyDash()
        {
            skyDone = true;
            var dh = GetComponent<DamageHero>();
            int was = dh != null ? dh.damageDealt : 1;
            try
            {
                if (dh != null) dh.damageDealt = 2;
                Vector2 from = Me, d = diveAt - from;
                float length = d.magnitude;
                d = length > 0.01f ? d / length : Vector2.up;
                if (d.y < 0.2f) d = new Vector2(d.x, 0.2f).normalized;
                const float speed = 45f;
                float max = Mathf.Min(0.7f, length / speed + 0.1f);
                for (float t = 0f; t < max; t += Time.fixedDeltaTime)
                {
                    if (Hostiles.NativeRay(Me, d, 1.2f).collider != null) break;
                    if (rb != null) rb.linearVelocity = d * speed;
                    yield return Batch4.Fixed;
                }
                if (rb != null) rb.linearVelocity = Vector2.zero;
                for (float t = 0f; t < 0.15f; t += Time.deltaTime) yield return null;
            }
            finally
            {
                if (dh != null) dh.damageDealt = was;
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        Vector2 SicklePoint()
        {
            var p = Go("Sickle Point")?.Value;
            return p != null ? (Vector2)p.transform.position : Me;
        }

        void AimSickles()
        {
            sickleAt = MaybeLead(0.6f);
            Vector2 from = SicklePoint(), d = sickleAt - from;
            if (d.sqrMagnitude < 0.01f) return;
            MarkLine(from, from + d.normalized * 30f, Warn(0.45f));
        }

        void TiltThrown()
        {
            var p = Go("Projectile")?.Value;
            if (p != null) Run(Tilt(p));
        }

        /// <summary>A sickle kept on the line through its start and the aimed point (its own script runs its speed along x, there and back).</summary>
        IEnumerator Tilt(GameObject p)
        {
            var prb = p.GetComponent<Rigidbody2D>();
            if (prb == null) yield break;
            Vector2 origin = p.transform.position;
            float dx = sickleAt.x - origin.x;
            if (Mathf.Abs(dx) < 0.5f) yield break;
            float slope = Mathf.Clamp((sickleAt.y - origin.y) / dx, -1f, 1f);
            if (Mathf.Abs(slope) < 0.05f) yield break;
            float until = Time.time + 4f;
            while (p != null && p.activeInHierarchy && Time.time < until)
            {
                var v = prb.linearVelocity;
                prb.linearVelocity = new Vector2(v.x, v.x * slope);
                yield return Batch4.Fixed;
            }
        }
    }
}
