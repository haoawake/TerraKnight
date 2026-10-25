using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Baldur Shell (charm 5): the skill key held raises a curved shell toward the cursor, for at most 0.8 s, moving at 60%
    /// while it is up. An enemy's hit from in front of it takes 70% less. Met in its first 0.15 s (the shell bright white
    /// then), the hit is a perfect guard: none of it lands, a short guard after it, and a shot (an enemy's moving attack with
    /// no body of its own) goes back the way the shell faces, at the enemies. Five seconds before it comes again, three after
    /// a perfect guard.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float GuardTime = 0.8f, PerfectWindow = 0.15f, GuardCooldown = 5f, PerfectCooldown = 3f, GuardKeep = 0.3f, GuardFront = 80f;
        static readonly Color GuardColor = new Color(0.78f, 0.9f, 1f);
        static float guardStart = -1f, guardEnd = -10f, guardFlash = -10f, perfectFlash = -10f;
        static bool guarding, guardPerfect, guardNeedsRelease;
        static Vector2 guardDir = Vector2.right;
        static int guardScaleFrame = -1;
        public static int DevGuards, DevPerfects, DevReflects;

        /// <summary>Moving while the shell is up: 60%.</summary>
        public static float MoveSpeedMult => guarding ? 0.6f : 1f;

        static void StartGuard(HeroController hero)
        {
            guarding = true;
            guardPerfect = false;
            guardNeedsRelease = true;
            guardStart = Time.time;
            guardDir = AimFrom(hero);
            Mod.PlaySound("Item_1", 0.35f);
        }

        static Vector2 AimFrom(HeroController hero)
        {
            var from = Centre(hero);
            Vector2 aim = DevAim ?? (TerraMod.MouseWorld(out var m) ? m : from + (hero.cState.facingRight ? Vector2.right : Vector2.left));
            var d = aim - from;
            return d.sqrMagnitude < 0.01f ? (hero.cState.facingRight ? Vector2.right : Vector2.left) : d.normalized;
        }

        static void EndGuard()
        {
            if (!guarding) return;
            guarding = false;
            guardEnd = Time.time;
            CharmSkills.Cool(Baldur, guardPerfect ? PerfectCooldown : GuardCooldown);
        }

        static void TickBaldur(HeroController hero)
        {
            if (!CharmSkills.Held) guardNeedsRelease = false;
            if (!guarding) return;
            if (!On(Baldur) || hero == null || hero.cState.dead) { guarding = false; guardEnd = Time.time; return; }
            guardDir = AimFrom(hero);
            if (!CharmSkills.Held || Time.time - guardStart >= GuardTime) EndGuard();
        }

        /// <summary>
        /// An enemy's hit about to land (TerraHurt.BeforeTakeDamage): 0 not guarded; 1 a perfect guard (refuse the hit); 2 guarded
        /// (it lands at 30%: <see cref="ScaleHit"/>).
        /// </summary>
        public static int Guard(HeroController hero, GameObject go, int damageAmount)
        {
            if (!guarding || go == null || hero == null) return 0;
            var centre = Centre(hero);
            var col = go.GetComponent<Collider2D>();
            Vector2 src = col != null ? (Vector2)col.bounds.ClosestPoint(centre) : (Vector2)go.transform.position;
            var to = src - centre;
            if (to.sqrMagnitude > 0.0001f && Vector2.Angle(to, guardDir) > GuardFront) return 0;
            DevGuards++;
            var p = TerraParticles.Instance;
            var at = TerraParticles.ToPx(centre + guardDir * 0.85f);
            if (Time.time - guardStart <= PerfectWindow)
            {
                guardPerfect = true;
                perfectFlash = Time.time;
                DevPerfects++;
                Player.TerraHurt.Invulnerable(hero, Mathf.RoundToInt(GuardKeep * 60f));
                Mod.PlaySound("Item_37", 0.7f);
                if (p != null)
                    for (int k = 0; k < 10; k++)
                    {
                        float a = Mathf.Atan2(-guardDir.y, guardDir.x) + Random.Range(-1.1f, 1.1f);
                        int d = p.NewDust(at - new Vector2(3f, 3f), 6, 6, 15, Mathf.Cos(a) * 3.5f, Mathf.Sin(a) * 3.5f, 0, 1f);
                        if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
                    }
                if (go.GetComponentInParent<HealthManager>() == null) Reflect(go, Player.PlayerHooks.RawContact(Mathf.Max(1, damageAmount), go));
                return 1;
            }
            guardFlash = Time.time;
            guardScaleFrame = Time.frameCount;
            Mod.PlaySound("NPC_Hit_4", 0.5f);
            if (p != null)
                for (int k = 0; k < 4; k++)
                {
                    int d = p.NewDust(at - new Vector2(2f, 2f), 4, 4, 15, Random.Range(-2f, 2f), Random.Range(-2f, 2f), 0, 0.8f);
                    if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
                }
            return 2;
        }

        /// <summary>A hit's damage as it lands (TerraStats): 30% of it when the shell guarded it.</summary>
        public static int ScaleHit(int damage) => guardScaleFrame == Time.frameCount ? Mathf.Max(1, Mathf.RoundToInt(damage * 0.3f)) : damage;

        /// <summary>A shot sent back: its way turned to the shell's, its harm to the player off, its harm to the enemies on.</summary>
        static void Reflect(GameObject go, int raw)
        {
            var rb = go.GetComponentInParent<Rigidbody2D>();
            if (rb == null) return;
            var shot = rb.GetComponent<ReflectedShot>() ?? rb.gameObject.AddComponent<ReflectedShot>();
            shot.Begin(guardDir, Mathf.Max(rb.linearVelocity.magnitude, 14f), Mathf.Min(1000, raw * 2));
            DevReflects++;
            Plugin.Log.LogInfo($"[护符] 巴德尔之壳反弹 {go.name}：{Mathf.Min(1000, raw * 2)} 伤害");
        }

        static void DrawBaldur(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(Baldur)) return;
            float t = Time.time;
            float since = guarding ? t - guardStart : t - guardEnd;
            float k = guarding ? Mathf.Clamp01(since / 0.06f) : Mathf.Clamp01(1f - since / 0.18f);
            float perfect = Mathf.Clamp01(1f - (t - perfectFlash) / 0.35f);
            if (k <= 0f && perfect <= 0f) return;
            fx.OnTop = true;
            var d = new Vector2(guardDir.x, -guardDir.y);
            float rot = Mathf.Atan2(d.y, d.x);
            var at = c + d * 4f;
            // In its first 0.15 s the shell is white: a hit met now is a perfect guard.
            bool window = guarding && since <= PerfectWindow;
            var colour = window ? Color.white : GuardColor;
            float flash = Mathf.Clamp01(1f - (t - guardFlash) / 0.2f);
            float body = 0.2f * k;
            fx.Draw(Shell, at, new Color(colour.r * body, colour.g * body, colour.b * body, body), rot, 1f, overPlayer: true);
            fx.Draw(Shell, at, Glow(colour, (window ? 1f : 0.6f + 0.4f * flash) * k * opacity), rot, 1f, overPlayer: true);
            if (perfect > 0f)
            {
                var spot = c + d * 26f;
                float r = Mathf.Lerp(6f, 30f, 1f - perfect * perfect);
                fx.Draw(Ring, spot, Glow(Color.white, perfect), 0f, r * 2f / 64f, overPlayer: true);
                fx.Draw(Dot, spot, Glow(GuardColor, 0.8f * perfect), 0f, 0.9f * perfect, overPlayer: true);
            }
        }

        static Sprite shell;

        /// <summary>The guard's shell: a curved band 110° wide round the player's front (pointing right), 30 px out.</summary>
        static Sprite Shell => shell != null ? shell : shell = Make(80, (x, y) =>
        {
            var v = new Vector2(x - 39.5f, y - 39.5f);
            float r = v.magnitude, a = Mathf.Abs(Mathf.Atan2(v.y, v.x)) * Mathf.Rad2Deg;
            float band = 1f - Mathf.Abs(r - 30f) / 4f;
            float ends = Mathf.Clamp01((55f - a) / 12f);
            return Mathf.Clamp01(band) * ends;
        });

        /// <summary>
        /// A shot the Baldur Shell sent back: flies the shell's way, harmless to the player, striking each enemy it passes once for
        /// twice what it would have dealt; three seconds of it, or until the game puts it away (a pooled shot is left as it was).
        /// </summary>
        internal sealed class ReflectedShot : MonoBehaviour
        {
            DamageHero harm;
            int wasHarm, damage;
            float until;
            Vector2 dir;
            float speed;
            readonly HashSet<HealthManager> struck = new HashSet<HealthManager>();
            static readonly List<HealthManager> near = new List<HealthManager>();
            public static int DevStruck;

            public void Begin(Vector2 way, float spd, int dmg)
            {
                harm = GetComponentInChildren<DamageHero>();
                if (harm != null && until <= 0f) wasHarm = harm.damageDealt;
                if (harm != null) harm.damageDealt = 0;
                dir = way;
                speed = spd;
                damage = dmg;
                until = Time.time + 3f;
                struck.Clear();
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = dir * speed;
            }

            void FixedUpdate()
            {
                if (Time.time > until) { Done(); return; }
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType != RigidbodyType2D.Static) rb.linearVelocity = dir * speed;
                var melee = TerraMod.Instance != null ? TerraMod.Instance.Melee : null;
                if (melee == null) return;
                foreach (var hm in AccessoryEffects.Enemies(transform.position, 20f, near))
                {
                    if (!struck.Add(hm)) continue;
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col != null && melee.HitWith(col, damage, 6f, 0f, dir.x >= 0f ? 1f : -1f, transform.position)) DevStruck++;
                }
            }

            void Done()
            {
                if (harm != null) harm.damageDealt = wasHarm;
                Destroy(this);
            }

            void OnDisable() => Done();
        }
    }
}
