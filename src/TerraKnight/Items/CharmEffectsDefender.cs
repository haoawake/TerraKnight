using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Defender's Crest (charm 10): the ground's harm takes nothing, and the ground goes anywhere (the user's: "要直接站在地形上
    /// 哪都能去"): spikes and thorns don't touch the player at all (stood on, walked through, no putting back, no poison), acid
    /// is swum in as with Isma's Tear; a pit (nothing to stand on) still puts the player back, unharmed, and a meteorite's heat
    /// burns nothing. An amber shelter shows round the player while it meets them. With an enemy near, now and then (four to
    /// eight seconds) a cloud of poison (Terraria's Toxic Flask cloud) rises where the player stands: an enemy in it is poisoned.
    /// The game's own dung cloud is the charm's no more.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float CloudTime = 3f, CloudRadiusPx = 64f, CloudReachPx = 300f, PoisonTicks = 240;
        static readonly Color ShelterColor = new Color(0.95f, 0.75f, 0.35f);
        static float shelterAt = -10f, shelterSoundAt = -10f, nextCloudAt;
        public static int DevShelters, DevClouds, DevPoisoned;
        public static int DevCloudsUp => clouds.Count;
        /// <summary>Dev: clouds of another room than the one played (none should outlive their room).</summary>
        public static int DevCloudsElsewhere
        {
            get
            {
                string scene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
                int n = 0;
                foreach (var c in clouds) if (c.Scene != scene) n++;
                return n;
            }
        }
        public static void DevPuffHere()
        {
            var hero = HeroController.SilentInstance;
            if (hero != null) Puff(Centre(hero), GameManager.instance.sceneName);
        }

        sealed class Cloud
        {
            public Vector2 At;
            public float Start, NextPoison;
            public string Scene;
            public readonly Vector2[] Puff = new Vector2[4];
            public readonly float[] Spin = new float[4];
            public readonly int[] Tex = new int[4];
        }
        static readonly List<Cloud> clouds = new List<Cloud>();
        static readonly string[] cloudTextures = { "Projectile_511", "Projectile_512", "Projectile_513" };
        static readonly List<HealthManager> cloudFound = new List<HealthManager>();

        /// <summary>The ground's harm met (PlayerHooks.TakeHealth, the meteorites): true when Defender's Crest shelters from it.</summary>
        public static bool Sheltered(HeroController hero)
        {
            // In Spore Shroom's cloud nothing harms (a pit still puts the player back), without the Crest's own look.
            if (Untouchable && !On(Defender) && hero != null)
            {
                softHitFrame = Time.frameCount;
                softHitText = null;
                softHitSound = null;
                return true;
            }
            if (!On(Defender) || hero == null) return false;
            shelterAt = Time.time;
            softHitFrame = Time.frameCount;
            softHitText = null;
            softHitSound = null;
            DevShelters++;
            ShelterSound();
            if (DevTrace) Plugin.Log.LogInfo($"[护符] 防御者纹章：不扣血，照常送回（{new System.Diagnostics.StackTrace(1, false).GetFrame(0)?.GetMethod()?.Name}）");
            return true;
        }

        public static bool DevTrace;

        /// <summary>A sound as a touch begins (not again while it goes on: stood on spikes, it is met every frame).</summary>
        static void ShelterSound()
        {
            if (Time.time - shelterSoundAt > 1f) Mod.PlaySound("Tink_*", 0.4f);
            shelterSoundAt = Time.time;
        }

        /// <summary>Spikes, thorns, acid met (TerraHurt.BeforeTakeDamage): true when Defender's Crest refuses them whole.</summary>
        public static bool WalksHazards(HeroController hero)
        {
            if (!On(Defender) || hero == null) return false;
            shelterAt = Time.time;
            DevShelters++;
            DevWalked++;
            if (DevTrace) Plugin.Log.LogInfo($"[护符] 防御者纹章：地形伤害整个挡掉（第 {DevWalked} 次）");
            ShelterSound();
            return true;
        }

        public static int DevWalked;

        /// <summary>Acid swum in as with Isma's Tear (Player.AcidLiquid).</summary>
        public static bool AcidSwim => On(Defender);

        static HeroController dungOf;
        static readonly List<GameObject> dung = new List<GameObject>();

        /// <summary>The game's own dung cloud of the charm (the Knight's "Dung" and "Dung Recharge") kept off while the bag has the charm.</summary>
        static void NoVanillaDung(HeroController hero)
        {
            if (hero == null || !Charms.Enabled) return;
            if (dungOf != hero)
            {
                dungOf = hero;
                dung.Clear();
                foreach (var t in hero.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Dung" || t.name == "Dung Recharge") dung.Add(t.gameObject);
            }
            foreach (var go in dung) if (go != null && go.activeSelf) go.SetActive(false);
        }

        static void TickDefender(HeroController hero)
        {
            NoVanillaDung(hero);
            string scene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var c = clouds[i];
                if (c.Scene != scene || Time.time - c.Start > CloudTime) { clouds.RemoveAt(i); continue; }
                if (Time.time < c.NextPoison) continue;
                c.NextPoison = Time.time + 0.5f;
                foreach (var hm in AccessoryEffects.Enemies(c.At, CloudRadiusPx, cloudFound))
                    if (!EnemyDebuffs.Has(hm, EnemyDebuffs.Poisoned)) { EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, (int)PoisonTicks); DevPoisoned++; }
                    else EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, (int)PoisonTicks);
            }
            if (!On(Defender) || !Playing(hero)) return;
            // In the acid only for the charm: the shelter shows the while.
            if (!PlayerData.instance.hasAcidArmour && Player.AcidLiquid.Swimming(hero)) shelterAt = Time.time;
            if (nextCloudAt <= 0f) nextCloudAt = Time.time + Random.Range(4f, 8f);
            if (Time.time < nextCloudAt) return;
            nextCloudAt = Time.time + Random.Range(4f, 8f);
            var at = Centre(hero) + Vector2.down * 0.4f;
            // Only with an enemy near enough to care.
            if (AccessoryEffects.Enemies(at, CloudReachPx, cloudFound).Count == 0) return;
            Puff(at, scene);
        }

        static void Puff(Vector2 at, string scene)
        {
            if (clouds.Count >= 3) clouds.RemoveAt(0);
            var c = new Cloud { At = at, Start = Time.time, NextPoison = Time.time + 0.15f, Scene = scene };
            for (int k = 0; k < c.Puff.Length; k++)
            {
                float a = k / (float)c.Puff.Length * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                c.Puff[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(10f, 30f);
                c.Spin[k] = Random.Range(-0.8f, 0.8f);
                c.Tex[k] = Random.Range(0, cloudTextures.Length);
            }
            clouds.Add(c);
            DevClouds++;
            Mod.PlaySound("Item_20", 0.3f);
        }

        static void DrawDefender(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(Defender)) return;
            float k = Mathf.Clamp01(1f - (Time.time - shelterAt) / 0.5f);
            if (k <= 0f) return;
            // The shelter: an amber dome, quick in, slowly out.
            fx.OnTop = true;
            float swell = Mathf.Clamp01((Time.time - shelterAt) / 0.08f);
            float r = 26f + 6f * swell;
            float body = 0.22f * k * opacity;
            fx.Draw(Dot, c, new Color(ShelterColor.r * body, ShelterColor.g * body, ShelterColor.b * body, body), 0f, r * 2.1f / 32f, overPlayer: true);
            fx.Draw(Ring, c, Glow(ShelterColor, 0.9f * k * opacity), 0f, r * 2f / 64f, overPlayer: true);
        }

        /// <summary>The clouds, wherever they are.</summary>
        static void DrawDefenderWorld(FxBatch fx)
        {
            if (clouds.Count == 0) return;
            bool top = fx.OnTop;
            fx.OnTop = true;
            float t = Time.time;
            string scene = GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;
            foreach (var c in clouds)
            {
                if (c.Scene != scene) continue;
                float age = (t - c.Start) / CloudTime;
                // In over a fifth of its time, out over the last third.
                float k = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((1f - age) / 0.35f);
                var px = TerraParticles.ToPx(c.At);
                for (int i = 0; i < c.Puff.Length; i++)
                {
                    var tex = cloudTextures[c.Tex[i]];
                    var drift = c.Puff[i] * (1f + age * 0.35f) + new Vector2(0f, -age * 8f);
                    float a = 0.3f * k;
                    fx.Draw(FxBatch.Whole(tex), px + drift, new Color(0.62f * a, 0.85f * a, 0.5f * a, a), c.Spin[i] * t, 1.15f + age * 0.35f);
                }
            }
            fx.OnTop = top;
        }
    }
}
