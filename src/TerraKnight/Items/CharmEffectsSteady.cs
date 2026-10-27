using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Steady Body (charm 14, four notches here): no inertia, as the Knight moves: a run speeds up as it would, stopped or turned
    /// at once, a turn going on at the speed it had before it and the speed kept half a second after the keys are let go (Player.TerraMovement,
    /// on a mount too: Player.Mounts), a little dust at the feet as it turns or stops on the ground. Its skill: for three seconds every dash (the Terraria dashes, the shadow dash, the super dash, the down dash)
    /// goes through the enemies, their hits and shots as if the player weren't there, with no bonk, "免疫" over the player as
    /// one goes through; the player gold all over then, gold light round it and an afterimage trailing each dash. Ten seconds
    /// before it comes again.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float SteadyTime = 3f, SteadyCooldown = 10f;
        static readonly Color SteadyColor = new Color(1f, 0.84f, 0.4f);
        static float steadyUntil = -1f, steadyAt = -10f, stepDustAt, passAt = -10f, immuneSaidAt = -10f;
        static Vector2 passSpot;
        public static int DevPassed, DevSteadyDust, DevImmuneSaid;

        /// <summary>No inertia (Player.TerraMovement.Horizontal, Player.Mounts.Move: on foot and on a mount).</summary>
        public static bool NoInertia => On(SteadyBody);

        /// <summary>How long the speed is kept after the keys are let go (seconds): a turn by the other key keeps it.</summary>
        const float SteadyKeepTime = 0.5f;
        static float keepSpeed, keepUntil;
        public static float DevKeptSpeed => keepSpeed;
        /// <summary>Dev (suite steadydash): the kept speed held on while a way is held, as before 10-26.</summary>
        public static bool DevOldKeep;

        /// <summary>
        /// The no-inertia run (px a tick): start is the speed before this tick's run, vx after it. A way held speeds up as it
        /// would; a turn goes on at the speed it had before the turn, only its way changed (and after a let-go, half a second
        /// at the speed of a moment ago); none held, stopped at once.
        /// </summary>
        public static float SteadyVelocity(float start, float vx, int way)
        {
            if (way != 0)
            {
                float speed = start * way < 0f ? Mathf.Abs(start) : Mathf.Abs(vx);
                // Pressed again within half a second of a let-go: the speed of then. Held on, the run as it goes (a dash's
                // speed falls back to the run's as in Terraria: kept, a shadow dash ran on at its speed, the user's 10-26).
                bool resumed = DevOldKeep ? Time.time < keepUntil : !float.IsPositiveInfinity(keepUntil) && Time.time < keepUntil;
                if (resumed) speed = Mathf.Max(speed, keepSpeed);
                keepSpeed = speed;
                keepUntil = float.PositiveInfinity;
                return way * speed;
            }
            // Just let go: the speed waits half a second.
            if (float.IsPositiveInfinity(keepUntil)) keepUntil = Time.time + SteadyKeepTime;
            return 0f;
        }

        /// <summary>The skill's three seconds: every dash goes through.</summary>
        public static bool DashPassActive => On(SteadyBody) && Time.time < steadyUntil;

        static bool Dashing(HeroController hero) =>
            Player.TerraMovement.IsDashing || Player.TerraMovement.DownDashing || hero != null && (hero.cState.superDashing || hero.cState.shadowDashing);

        /// <summary>An enemy's hit to refuse now (TerraHurt.BeforeTakeDamage): dashing in the skill's three seconds.</summary>
        public static bool DashImmune(HeroController hero) => DashPassActive && Dashing(hero);

        /// <summary>A hit gone through: a spark where it met.</summary>
        public static void PassedThrough(GameObject go)
        {
            DevPassed++;
            var hero = HeroController.SilentInstance;
            passAt = Time.time;
            passSpot = go != null ? (Vector2)go.transform.position : hero != null ? Centre(hero) : Vector2.zero;
            // "免疫" over the player (a touch goes on every frame it overlaps: once in a while).
            if (hero == null || Time.time - immuneSaidAt < 0.4f) return;
            immuneSaidAt = Time.time;
            var box = hero.GetComponent<BoxCollider2D>();
            var at = box != null ? new Vector3(box.bounds.center.x, box.bounds.max.y + 0.2f, hero.transform.position.z) : hero.transform.position + Vector3.up;
            UI.CombatText.Show(at, Lang.T("免疫"), SteadyColor);
            DevImmuneSaid++;
        }

        static void StartSteady(HeroController hero)
        {
            steadyUntil = Time.time + SteadyTime;
            steadyAt = Time.time;
            CharmSkills.Active(SteadyBody, Charms.Name(SteadyBody), Lang.T("冲刺时无敌，穿过敌人"), SteadyTime);
            CharmSkills.Cool(SteadyBody, SteadyCooldown);
            Mod.PlaySound("Item_8", 0.4f);
        }

        /// <summary>A step of the no-inertia movement: a turn or a stop on the ground raises a little dust.</summary>
        public static void SteadyStep(HeroController hero, float before, float after, bool grounded)
        {
            if (!grounded || Time.time < stepDustAt) return;
            bool turned = before * after < 0f && Mathf.Abs(before) > 1f;
            bool stopped = after == 0f && Mathf.Abs(before) > 1.5f;
            if (!turned && !stopped) return;
            stepDustAt = Time.time + 0.15f;
            var p = TerraParticles.Instance;
            if (p == null || hero == null) return;
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            var feet = TerraParticles.ToPx(new Vector2(box.center.x, box.min.y));
            float away = before > 0f ? -1f : 1f;
            for (int k = 0; k < 3; k++)
            {
                int d = p.NewDust(feet + new Vector2(-4f, -4f), 8, 4, 16, away * Random.Range(0.6f, 1.6f), -Random.Range(0.2f, 0.8f), 120, 0.7f);
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
            }
            DevSteadyDust++;
        }

        static void TickSteady(HeroController hero)
        {
            if (!On(SteadyBody) || hero == null || hero.cState.dead)
            {
                steadyUntil = -1f;
                keepSpeed = keepUntil = 0f;
                CharmSkills.EndActive(SteadyBody);
            }
            // The skill's three seconds: the player gold all over, in and out quickly.
            float gold = DashPassActive ? Mathf.Clamp01((steadyUntil - Time.time) / 0.3f) : 0f;
            Player.TerraPlayerSprite.GoldTint = Mathf.MoveTowards(Player.TerraPlayerSprite.GoldTint, gold, Time.deltaTime * 6f);
        }

        static void DrawSteady(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(SteadyBody)) return;
            float t = Time.time;
            float pass = Mathf.Clamp01(1f - (t - passAt) / 0.25f);
            if (!DashPassActive && pass <= 0f) return;
            fx.OnTop = true;
            var hero = HeroController.SilentInstance;
            if (DashPassActive)
            {
                // A quiet gold light round the player, fading in and out of the three seconds.
                float k = Mathf.Clamp01((t - steadyAt) / 0.15f) * Mathf.Clamp01((steadyUntil - t) / 0.4f) * opacity;
                fx.Draw(Ring, c, Glow(SteadyColor, (0.35f + 0.1f * Mathf.Sin(t * 9f)) * k), 0f, 50f / 64f, overPlayer: true);
                // Dashing: an afterimage of light trailing the dash.
                if (hero != null && Dashing(hero))
                {
                    var v = hero.GetComponent<Rigidbody2D>().linearVelocity;
                    var back = v.sqrMagnitude > 0.01f ? new Vector2(-v.x, v.y).normalized : Vector2.zero;
                    for (int i = 1; i <= 4; i++)
                        fx.Draw(Dot, c + back * (i * 9f), Glow(SteadyColor, 0.35f * (1f - i / 5f) * k), Mathf.Atan2(back.y, back.x), new Vector2(1.1f, 1.5f), overPlayer: false);
                }
            }
            if (pass > 0f)
            {
                var at = TerraParticles.ToPx(passSpot);
                fx.Draw(Dot, at, Glow(Color.white, 0.6f * pass), 0f, 0.8f * pass, overPlayer: true);
            }
        }
    }
}
