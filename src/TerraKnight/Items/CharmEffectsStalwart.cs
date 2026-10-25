using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Stalwart Shell (charm 4): a hit's immunity a fifth of a second longer. Its skill: an amber shell round the player for
    /// three seconds that takes every hit whole (nothing off the life, no knockback, the amount shown in gold); the skill
    /// again lets it all go as a wave of light toward the cursor, bursting on the first enemy or wall it meets (or at the end
    /// of its way) for the whole amount on every enemy round it. What the shell took and wasn't let go waits ten seconds,
    /// glowing in the hand, then fades. Twenty seconds before it comes again; the shell with nothing taken ends quietly when
    /// its three seconds are up (a press meanwhile keeps it).
    /// </summary>
    internal static partial class CharmEffects
    {
        const float ShellTime = 3f, ShellHold = 10f, ShellCooldown = 20f, ExtraImmunitySeconds = 0.2f;
        const float WaveSpeed = 30f, WaveReach = 26f, WaveBlastPx = 96f, WaveRadius = 0.7f;
        static readonly Color ShellColor = new Color(1f, 0.72f, 0.3f);
        static float shellUntil = -1f, holdUntil = -1f, shellFlash = -10f;
        static int shellStored;
        static string shellScene;
        public static int DevShellAbsorbed, DevWaves, DevWaveHits, DevWaveDamage, DevShellKept;
        public static Vector2? DevAim;
        public static int DevWavesInFlight => waves.Count;

        /// <summary>A wave let go: where it is, where it goes, how far it has gone, what it carries.</summary>
        sealed class Wave
        {
            public Vector2 At, Dir;
            public float Gone;
            public int Damage;
            public string Scene;
        }
        static readonly List<Wave> waves = new List<Wave>();
        /// <summary>Bursts drawn: where, when.</summary>
        static readonly List<(Vector2 At, float Time, string Scene)> bursts = new List<(Vector2, float, string)>();
        static readonly List<HealthManager> blastFound = new List<HealthManager>();

        /// <summary>The shell up now, taking every hit.</summary>
        static bool ShellUp => On(Stalwart) && Time.time < shellUntil;
        /// <summary>What was taken, waiting to be let go.</summary>
        static bool ShellCharged => On(Stalwart) && shellStored > 0 && (Time.time < shellUntil || Time.time < holdUntil);

        /// <summary>Seconds a hit's immunity is made longer by the worn charms.</summary>
        public static float ExtraImmunity => On(Stalwart) ? ExtraImmunitySeconds : 0f;

        /// <summary>A hit's damage (after defense) about to come off the shields and the life: true when the shell took it.</summary>
        public static bool Absorb(int damage)
        {
            if (!ShellUp || damage <= 0) return false;
            shellStored += damage;
            shellFlash = Time.time;
            DevShellAbsorbed += damage;
            softHitFrame = Time.frameCount;
            softHitText = Lang.F("吸收 {0}", damage);
            softHitColor = ShellColor;
            softHitSound = "Tink_*";
            return true;
        }

        static void StartShell(HeroController hero)
        {
            shellUntil = Time.time + ShellTime;
            holdUntil = -1f;
            shellStored = 0;
            shellScene = GameManager.instance != null ? GameManager.instance.sceneName : null;
            CharmSkills.Active(Stalwart, Charms.Name(Stalwart), Lang.T("吸收受到的所有伤害"), ShellTime);
            Mod.PlaySound("Item_37", 0.45f);
        }

        /// <summary>The skill again while the shell holds something: the wave goes. True: the press was the shell's.</summary>
        static bool PressStalwart(HeroController hero)
        {
            if (!On(Stalwart) || !(ShellUp || ShellCharged)) return false;
            // Nothing taken yet: the shell stays up its three seconds.
            if (shellStored <= 0) { DevShellKept++; return true; }
            LetGo(hero);
            EndShell();
            return true;
        }

        static void EndShell()
        {
            shellUntil = holdUntil = -1f;
            shellStored = 0;
            CharmSkills.EndActive(Stalwart);
            CharmSkills.Cool(Stalwart, ShellCooldown);
        }

        static void LetGo(HeroController hero)
        {
            var from = Centre(hero);
            Vector2 aim = DevAim ?? (TerraMod.MouseWorld(out var m) ? m : from + Vector2.right * (hero.cState.facingRight ? 1f : -1f));
            var dir = aim - from;
            if (dir.sqrMagnitude < 0.01f) dir = hero.cState.facingRight ? Vector2.right : Vector2.left;
            waves.Add(new Wave { At = from, Dir = dir.normalized, Damage = shellStored, Scene = GameManager.instance.sceneName });
            DevWaves++;
            Mod.PlaySound("Item_43", 0.6f);
            Plugin.Log.LogInfo($"[护符] 坚硬外壳放出光波：{shellStored} 伤害，朝 {dir.normalized:F2}");
        }

        static void TickStalwart(HeroController hero)
        {
            string scene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
            // Taken off, or dead: the shell and what it holds are gone (the cooldown as if used).
            if ((shellUntil > 0f || holdUntil > 0f) && (!On(Stalwart) || hero == null || hero.cState.dead))
            {
                shellUntil = holdUntil = -1f;
                shellStored = 0;
                CharmSkills.EndActive(Stalwart);
            }
            if (On(Stalwart) && shellUntil > 0f && Time.time >= shellUntil)
            {
                // The three seconds over: nothing taken ends it; something taken waits in the hand.
                shellUntil = -1f;
                if (shellStored <= 0) EndShell();
                else holdUntil = Time.time + ShellHold;
            }
            if (On(Stalwart) && holdUntil > 0f && Time.time >= holdUntil)
            {
                Plugin.Log.LogInfo($"[护符] 坚硬外壳：{shellStored} 没放出去，散了");
                EndShell();
            }
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                // Another room: the wave is gone with the old one.
                if (w.Scene != scene) { waves.RemoveAt(i); continue; }
                float step = WaveSpeed * Time.deltaTime;
                var next = w.At + w.Dir * step;
                bool burst = w.Gone + step >= WaveReach;
                var wall = Physics2D.CircleCast(w.At, WaveRadius * 0.5f, w.Dir, step, 1 << World.ChunkColliders.TerrainLayer);
                // A wall (not a platform one jumps up through) stops it.
                if (wall.collider != null && !wall.collider.isTrigger && !wall.collider.usedByEffector) { next = wall.centroid; burst = true; }
                if (!burst && AccessoryEffects.Enemies(next, WaveRadius / TerraParticles.Px, blastFound).Count > 0) burst = true;
                w.At = next;
                w.Gone += step;
                if (burst)
                {
                    Burst(w);
                    waves.RemoveAt(i);
                }
            }
            for (int i = bursts.Count - 1; i >= 0; i--)
                if (Time.time - bursts[i].Time > 0.6f || bursts[i].Scene != scene) bursts.RemoveAt(i);
        }

        static void Burst(Wave w)
        {
            var melee = Mod?.Melee;
            int hit = 0;
            if (melee != null)
                foreach (var hm in AccessoryEffects.Enemies(w.At, WaveBlastPx, blastFound))
                {
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    if (melee.HitWith(col, w.Damage, 8f, 0f, hm.transform.position.x >= w.At.x ? 1f : -1f, w.At)) hit++;
                }
            DevWaveHits += hit;
            DevWaveDamage = w.Damage;
            bursts.Add((w.At, Time.time, w.Scene));
            Mod.PlaySound("Item_14", 0.55f);
            var p = TerraParticles.Instance;
            if (p != null)
            {
                var px = TerraParticles.ToPx(w.At);
                for (int k = 0; k < 14; k++)
                {
                    float a = k / 14f * Mathf.PI * 2f;
                    int d = p.NewDust(px - new Vector2(4f, 4f), 8, 8, 228, Mathf.Cos(a) * 4f, Mathf.Sin(a) * 4f, 0, 1.1f);
                    if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
                }
            }
            Plugin.Log.LogInfo($"[护符] 坚硬外壳光波炸开：{w.Damage} 伤害，在 {w.At:F1}（飞了 {w.Gone:F1}），打中 {hit} 个");
        }

        static void DrawStalwart(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(Stalwart)) return;
            float t = Time.time;
            fx.OnTop = true;
            if (ShellUp)
            {
                // The shell: a soft amber sphere, brighter for what it has taken, a flash at each hit, fading in its last half second.
                float left = shellUntil - t;
                float k = Mathf.Clamp01(left / 0.5f) * Mathf.Clamp01((ShellTime - left) / 0.15f) * opacity;
                float flash = Mathf.Clamp01(1f - (t - shellFlash) / 0.25f);
                float fill = Mathf.Clamp01(shellStored / 300f);
                float r = 30f + Mathf.Sin(t * 6f) * 0.8f;
                // A faint amber body (seen on a bright room too), its rim, and the light inside.
                float body = (0.16f + 0.1f * fill) * k;
                fx.Draw(Dot, c, new Color(ShellColor.r * body, ShellColor.g * body, ShellColor.b * body, body), 0f, r * 2.1f / 32f, overPlayer: true);
                fx.Draw(Ring, c, Glow(ShellColor, (0.85f + 0.6f * flash) * k), t * 0.4f, r * 2f / 64f, overPlayer: true);
                fx.Draw(Dot, c, Glow(ShellColor, (0.15f + 0.2f * fill + 0.35f * flash) * k), 0f, r * 2f / 32f, overPlayer: true);
            }
            else if (ShellCharged)
            {
                // Held: a small amber light pulsing at the chest, waiting.
                float pulse = 0.6f + 0.4f * Mathf.Sin(t * 8f);
                float fade = Mathf.Clamp01((holdUntil - t) / 1.5f);
                var at = c + new Vector2(0f, -2f);
                fx.Draw(Dot, at, Glow(ShellColor, 0.9f * pulse * fade * opacity), 0f, 0.8f, overPlayer: true);
                fx.Draw(Dot, at, Glow(Color.white, 0.6f * pulse * fade * opacity), 0f, 0.25f, overPlayer: true);
                // A thin ring drawn in to it now and then: something is waiting here.
                float ringK = Mathf.Repeat(t, 1.2f) / 1.2f;
                fx.Draw(Ring, at, Glow(ShellColor, 0.6f * (1f - ringK) * fade * opacity), 0f, Mathf.Lerp(40f, 10f, ringK) / 64f, overPlayer: true);
            }
        }

        /// <summary>The waves and their bursts, wherever they are (every frame, the player seen or not).</summary>
        static void DrawStalwartWorld(FxBatch fx)
        {
            if (waves.Count == 0 && bursts.Count == 0) return;
            bool top = fx.OnTop;
            fx.OnTop = true;
            float t = Time.time;
            foreach (var w in waves)
            {
                var px = TerraParticles.ToPx(w.At);
                var d = new Vector2(w.Dir.x, -w.Dir.y);
                float rot = Mathf.Atan2(d.y, d.x);
                for (int k = 5; k >= 1; k--)
                    fx.Draw(Dot, px - d * (k * 7f), Glow(ShellColor, 0.4f * (1f - k / 6f)), rot, new Vector2(0.9f - k * 0.1f, 0.45f - k * 0.05f));
                fx.Draw(Dot, px, Glow(ShellColor, 0.9f), rot, new Vector2(1.3f, 0.6f));
                fx.Draw(Dot, px, Glow(Color.white, 0.7f), rot, new Vector2(0.6f, 0.22f));
                fx.Light(px, ShellColor * 0.8f, 4f);
            }
            foreach (var b in bursts)
            {
                float k = Mathf.Clamp01((t - b.Time) / 0.45f);
                var px = TerraParticles.ToPx(b.At);
                float radius = Mathf.Lerp(10f, WaveBlastPx, 1f - (1f - k) * (1f - k));
                fx.Draw(Ring, px, Glow(ShellColor, 0.9f * (1f - k)), 0f, radius * 2f / 64f);
                fx.Draw(Dot, px, Glow(Color.white, 0.8f * (1f - k) * (1f - k)), 0f, radius * 1.2f / 32f);
            }
            fx.OnTop = top;
        }
    }
}
