using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The charms that change the player's body (护符设计记录.md):
    /// Spore Shroom (17): its skill: the player sinks into a cloud of spores for two seconds, unseen, untouchable by
    /// anything, every enemy in the cloud poisoned and stung each quarter second (a third of a nail blow). 30 s.
    /// Shape of Unn (28): riding a mount, common enemies and elites leave the player alone (they don't notice him, and
    /// their touch doesn't hurt) until he strikes them; bosses as ever. Its skill: five seconds as a ball of Unn's green
    /// light, flying where W A S D point (no fall), hurt as ever, no weapon. 15 s.
    /// Hiveblood (29): hurt, the next healing potion heals twice over (then 60 s before it can again); three seconds
    /// unhurt, the honey's quicker regeneration until the next hurt. An amber drop at the chest while the potion is
    /// doubled, amber drips while the honey holds.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int ShapeOfUnn = 28, Hiveblood = 29;
        const float SporeTime = 2f, SporeCooldown = 30f, SporeRadiusPx = 72f, SporeTick = 0.25f, SporeShare = 0.33f;
        const float UnnTime = 5f, UnnCooldown = 15f, UnnSpeed = 18f, HiveQuiet = 3f, HiveCooldown = 60f;
        static readonly Color SporeColor = new Color(0.55f, 0.95f, 0.45f), UnnColor = new Color(0.45f, 1f, 0.55f), HiveColor = new Color(1f, 0.72f, 0.2f);
        public static int DevSpores, DevSporeHits, DevUnns, DevHiveDoubled, DevCalmRefused;

        // ------------------------------------------------------------------ Spore Shroom

        static float sporeUntil = -1f, sporeTickAt, sporeEndAt = -10f;
        static readonly string[] SporeTextures = { "Projectile_569", "Projectile_570", "Projectile_571" };
        static Vector2 sporeLast;
        static string sporeScene;

        static bool Spored => On(SporeShroom) && Time.time < sporeUntil;

        /// <summary>No harm reaches the player (TerraHurt.BeforeTakeDamage, the fall): the spore cloud.</summary>
        public static bool Untouchable => Spored;

        /// <summary>The player isn't drawn (TerraMod.PosePlayer): in the spores, or Unn's light.</summary>
        public static bool HidePlayer => Spored || UnnFlying;

        static void StartSpores(HeroController hero)
        {
            sporeUntil = Time.time + SporeTime;
            sporeTickAt = Time.time;
            sporeScene = SceneNow;
            DevSpores++;
            CharmSkills.Active(SporeShroom, Charms.Name(SporeShroom), Lang.T("遁入孢子毒雾：免疫所有伤害"), SporeTime);
            CharmSkills.Cool(SporeShroom, SporeCooldown);
            Mod.PlaySound("Item_" + 20, 0.4f, 0.7f);
            SporePuff(Centre(hero), 18);
        }

        static void TickSpores(HeroController hero)
        {
            if (sporeUntil > 0f && (!On(SporeShroom) || hero == null || hero.cState.dead || SceneNow != sporeScene || Time.time >= sporeUntil))
            {
                bool ended = Time.time >= sporeUntil;
                sporeUntil = -1f;
                sporeEndAt = Time.time;
                CharmSkills.EndActive(SporeShroom);
                if (hero != null && ended) SporePuff(Centre(hero), 12);
            }
            if (!Spored || hero == null) return;
            sporeLast = Centre(hero);
            if (Time.time < sporeTickAt) return;
            sporeTickAt = Time.time + SporeTick;
            int damage = StageDamage(SporeShare);
            foreach (var hm in AccessoryEffects.Enemies(sporeLast, SporeRadiusPx, swarmFound))
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                if (col == null) continue;
                EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 300);
                ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.None };
                if (Mod.Melee.HitWith(col, damage, 0f, 0f, hm.transform.position.x >= sporeLast.x ? 1f : -1f, sporeLast, autonomous: true)) DevSporeHits++;
            }
        }

        static void SporePuff(Vector2 at, int n)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(at);
            for (int k = 0; k < n; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                int d = p.NewDust(px - new Vector2(8f, 8f), 16, 16, 107, Mathf.Cos(a) * 2.5f, Mathf.Sin(a) * 2.5f, 100, 1.1f);
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
            }
        }

        // ------------------------------------------------------------------ Shape of Unn

        static float unnUntil = -1f, unnGravity = -1f, unnEndAt = -10f;
        static Vector2 unnLast, unnVel;
        static string unnScene;
        static readonly Vector2[] unnTrail = new Vector2[6];

        static bool UnnFlying => On(ShapeOfUnn) && Time.time < unnUntil;

        static void StartUnn(HeroController hero)
        {
            if (Player.Mounts.Riding) Player.Mounts.Dismount();
            unnUntil = Time.time + UnnTime;
            unnScene = SceneNow;
            unnLast = Centre(hero);
            for (int k = 0; k < unnTrail.Length; k++) unnTrail[k] = unnLast;
            DevUnns++;
            CharmSkills.Active(ShapeOfUnn, Charms.Name(ShapeOfUnn), Lang.T("乌恩的光芒：W A S D 飞行"), UnnTime);
            CharmSkills.Cool(ShapeOfUnn, UnnCooldown);
            Mod.PlaySound("Item_" + 8, 0.45f, 1.2f);
        }

        static void EndUnn(HeroController hero, bool burst)
        {
            unnUntil = -1f;
            unnEndAt = Time.time;
            CharmSkills.EndActive(ShapeOfUnn);
            var rb = hero != null ? hero.GetComponent<Rigidbody2D>() : null;
            if (rb != null && unnGravity >= 0f) rb.gravityScale = unnGravity > 0.01f ? unnGravity : hero.DEFAULT_GRAVITY;
            unnGravity = -1f;
            if (rb != null && burst) rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, Mathf.Min(rb.linearVelocity.y, 4f));
            if (burst && hero != null) Pop(Centre(hero), UnnColor);
        }

        static void TickUnn(HeroController hero)
        {
            if (unnUntil > 0f && (!On(ShapeOfUnn) || hero == null || hero.cState.dead || SceneNow != unnScene || Time.time >= unnUntil || hero.cState.transitioning))
                EndUnn(hero, hero != null && !hero.cState.dead);
            if (!UnnFlying || hero == null) return;
            unnLast = Centre(hero);
            for (int k = unnTrail.Length - 1; k > 0; k--) unnTrail[k] = unnTrail[k - 1];
            unnTrail[0] = unnLast;
        }

        /// <summary>Dev: the keys held for Unn's flight (x: −1/0/1 left/right, y: −1/0/1 down/up), null: the real ones.</summary>
        public static Vector2? DevUnnKeys;

        /// <summary>After each physics step (HeroController.FixedUpdate): Unn's light flying where the keys point, no fall.</summary>
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static class UnnFlight
        {
            static void Postfix(HeroController __instance, Rigidbody2D ___rb2d)
            {
                if (!UnnFlying || ___rb2d == null || !__instance.acceptingInput) return;
                if (unnGravity < 0f) unnGravity = ___rb2d.gravityScale;
                ___rb2d.gravityScale = 0f;
                Vector2 k;
                if (DevUnnKeys.HasValue) k = DevUnnKeys.Value;
                else
                {
                    k.x = (Player.TerraKeys.Held(Player.TerraKeys.Action.Right) ? 1f : 0f) - (Player.TerraKeys.Held(Player.TerraKeys.Action.Left) ? 1f : 0f);
                    k.y = (Player.TerraKeys.Held(Player.TerraKeys.Action.Up) ? 1f : 0f) - (Player.TerraKeys.Held(Player.TerraKeys.Action.Down) ? 1f : 0f);
                }
                var want = k.sqrMagnitude > 0.01f ? k.normalized * UnnSpeed : Vector2.zero;
                unnVel = Vector2.Lerp(___rb2d.linearVelocity, want, 0.35f);
                ___rb2d.linearVelocity = unnVel;
                __instance.cState.jumping = false;
                __instance.cState.doubleJumping = false;
            }
        }

        // ------------------------------------------------------------------ calm on a mount

        static readonly Dictionary<int, HealthManager> calmOf = new Dictionary<int, HealthManager>();
        /// <summary>Dev: counted as riding for a test.</summary>
        public static bool DevRiding;

        /// <summary>An enemy left calm by Shape of Unn: the player riding, it neither a boss nor struck yet.</summary>
        static bool Calm(Component c)
        {
            if (c == null || !(Player.Mounts.Riding || DevRiding) || !On(ShapeOfUnn)) return false;
            int id = c.GetInstanceID();
            if (!calmOf.TryGetValue(id, out var hm))
            {
                if (calmOf.Count > 512) calmOf.Clear();
                calmOf[id] = hm = c.GetComponentInParent<HealthManager>();
            }
            return CalmEnemy(hm);
        }

        static bool CalmEnemy(HealthManager hm)
        {
            if (hm == null || hm.isDead) return false;
            var info = EnemyStats.Of(hm);
            return !info.Boss && info.LastHit < -50f && !World.EnemyAggro.IsAngry(hm);
        }

        /// <summary>A touch from an enemy Shape of Unn leaves calm (TerraHurt.BeforeTakeDamage): no hurt.</summary>
        public static bool CalmHit(GameObject go)
        {
            if (go == null || !(Player.Mounts.Riding || DevRiding) || !On(ShapeOfUnn)) return false;
            if (!CalmEnemy(go.GetComponentInParent<HealthManager>())) return false;
            DevCalmRefused++;
            return true;
        }

        [HarmonyPatch(typeof(AlertRange), "get_IsHeroInRange")]
        static class CalmAlert
        {
            static void Postfix(AlertRange __instance, ref bool __result) { if (__result && Calm(__instance)) __result = false; }
        }

        [HarmonyPatch(typeof(LineOfSightDetector), "get_CanSeeHero")]
        static class CalmSight
        {
            static void Postfix(LineOfSightDetector __instance, ref bool __result) { if (__result && Calm(__instance)) __result = false; }
        }

        // ------------------------------------------------------------------ Hiveblood

        static bool hiveArmed;
        static float hiveDoubledAt = -10f, hiveDripAt;
        static bool hiveShown;

        /// <summary>Honey's quicker regeneration (TerraStats.Tick): three seconds unhurt with Hiveblood, until hurt.</summary>
        public static bool HiveHoney
        {
            get
            {
                var st = Stats;
                return On(Hiveblood) && st != null && st.Life > 0 && Time.time - Mathf.Max(lifeLostAt, hiveHurtAt) >= HiveQuiet;
            }
        }

        static float hiveHurtAt = -10f;

        static void HurtHive()
        {
            hiveHurtAt = Time.time;
            if (On(Hiveblood) && !CharmSkills.Cooling(Hiveblood)) hiveArmed = true;
        }

        /// <summary>A healing potion drunk (TerraMod.Drink): Hiveblood's after a hurt, twice over.</summary>
        public static int PotionHeal(ItemDef def, int amount, HeroController hero)
        {
            if (!On(Hiveblood) || !hiveArmed || def == null || def.HealLife <= 0 || amount <= 0) return amount;
            hiveArmed = false;
            hiveDoubledAt = Time.time;
            DevHiveDoubled++;
            CharmSkills.Cool(Hiveblood, HiveCooldown);
            Mod.PlaySound("Item_" + 3, 0.4f, 0.8f);
            if (hero != null) Pop(Centre(hero), HiveColor);
            return amount * 2;
        }

        public static bool DevHiveArmed => hiveArmed;

        static void TickHive(HeroController hero)
        {
            if (!On(Hiveblood) || hero == null || hero.cState.dead) { hiveArmed = false; if (hiveShown) { hiveShown = false; CharmSkills.EndActive(Hiveblood); } return; }
            bool honey = HiveHoney;
            if (honey != hiveShown)
            {
                hiveShown = honey;
                if (honey) CharmSkills.Hold(Hiveblood, Charms.Name(Hiveblood) + Lang.T("：蜂蜜"), Lang.T("生命再生加快，受伤为止"));
                else CharmSkills.EndActive(Hiveblood);
            }
            // Amber drips now and then while the honey holds.
            if (honey && Playing(hero) && Time.time >= hiveDripAt)
            {
                hiveDripAt = Time.time + 0.45f;
                var p = TerraParticles.Instance;
                if (p != null)
                {
                    var b = hero.GetComponent<BoxCollider2D>().bounds;
                    var tl = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
                    int d = p.NewDust(tl, Mathf.RoundToInt(b.size.x / TerraParticles.Px), Mathf.RoundToInt(b.size.y / TerraParticles.Px * 0.6f), 153, 0f, 0f, 0, 1.3f);
                    if (d >= 0) { ref var dust = ref p[d]; dust.Velocity = new Vector2(0f, 0.6f); }
                }
            }
        }

        // ------------------------------------------------------------------ the looks

        static void DrawForms(FxBatch fx, Vector2 c, float opacity)
        {
            if (On(Hiveblood) && hiveArmed)
            {
                // An amber drop at the chest: the next potion is doubled.
                fx.OnTop = true;
                float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 3f);
                var at = c + new Vector2(0f, -4f);
                fx.Draw(Dot, at, Glow(HiveColor, 0.9f * pulse * opacity), 0f, new Vector2(0.55f, 0.75f), overPlayer: true);
                fx.Draw(Dot, at + new Vector2(0f, 1f), Glow(Color.white, 0.55f * pulse * opacity), 0f, 0.18f, overPlayer: true);
            }
            float doubled = Mathf.Clamp01(1f - (Time.time - hiveDoubledAt) / 0.5f);
            if (doubled > 0f)
            {
                fx.OnTop = true;
                fx.Draw(Ring, c, Glow(HiveColor, 0.8f * doubled * opacity), 0f, Mathf.Lerp(16f, 60f, 1f - doubled) / 64f, overPlayer: true);
            }
        }

        /// <summary>The spore cloud and Unn's light, drawn in the world (the player isn't).</summary>
        static void DrawFormsWorld(FxBatch fx)
        {
            float t = Time.time;
            bool spores = Spored || t - sporeEndAt < 0.3f;
            bool unn = UnnFlying || t - unnEndAt < 0.25f;
            if (!spores && !unn) return;
            bool top = fx.OnTop;
            fx.OnTop = true;
            if (spores && sporeScene == SceneNow)
            {
                float fade = Spored ? Mathf.Clamp01((t - (sporeUntil - SporeTime)) / 0.15f) * Mathf.Clamp01((sporeUntil - t) / 0.3f + 0.2f) : 1f - (t - sporeEndAt) / 0.3f;
                var c = TerraParticles.ToPx(sporeLast);
                // Puffs of green spore cloud turning slowly over the place the player is.
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.PI * 2f / 7f + t * (i % 2 == 0 ? 0.7f : -0.5f);
                    float r = 22f + 12f * Mathf.Sin(t * 1.3f + i);
                    var at = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.75f);
                    var spr = FxBatch.Whole(SporeTextures[i % 3]);
                    float al = 0.55f * fade;
                    fx.Draw(spr, at, new Color(al, al, al, al), a, 1.5f + 0.3f * Mathf.Sin(t * 2f + i));
                }
                fx.Draw(Dot, c, Glow(SporeColor, 0.35f * fade), 0f, SporeRadiusPx * 2f / 32f);
                for (int i = 0; i < 5; i++)
                {
                    float ph = Mathf.Repeat(t * 0.6f + i * 0.2f, 1f);
                    var at = c + new Vector2(Mathf.Sin(i * 2.3f + t) * 40f, 24f - ph * 70f);
                    fx.Draw(FxBatch.Whole("Projectile_567"), at, new Color(fade * (1f - ph), fade * (1f - ph), fade * (1f - ph), fade * (1f - ph)), t * 2f + i, 0.7f);
                }
                fx.Light(c, SporeColor * 0.5f * fade, 4f);
            }
            if (unn && unnScene == SceneNow)
            {
                float fade = UnnFlying ? Mathf.Clamp01((unnUntil - t) / 0.4f + 0.3f) : 1f - (t - unnEndAt) / 0.25f;
                var c = TerraParticles.ToPx(unnLast);
                for (int k = unnTrail.Length - 1; k >= 1; k--)
                    fx.Draw(Dot, TerraParticles.ToPx(unnTrail[k]), Glow(UnnColor, 0.4f * fade * (1f - k / (float)unnTrail.Length)), 0f, 0.9f - k * 0.1f);
                int frame = (int)(t * 10f) % 4;
                var orb = FxBatch.Frame("Projectile_229", 0, frame * 30, 30, 30, 15f, 15f);
                fx.Draw(Dot, c, Glow(UnnColor, 0.55f * fade), 0f, 2.2f);
                fx.Draw(orb, c, new Color(fade, fade, fade, fade), t * 2f, 1.15f);
                fx.Draw(Dot, c, Glow(Color.white, 0.5f * fade), 0f, 0.5f);
                fx.Light(c, UnnColor * fade, 5f);
                // The last second: it flickers, about to end.
                if (UnnFlying && unnUntil - t < 1f && Mathf.Repeat(t * 8f, 1f) < 0.5f) fx.Draw(Ring, c, Glow(UnnColor, 0.5f), 0f, 0.7f);
            }
            fx.OnTop = top;
        }

        // ------------------------------------------------------------------ the game's own charm records left out

        /// <summary>
        /// HeroController.CharmUpdate: with the charm bag on, the Fragile Heart's two masks and Joni's Blessing's single
        /// mask (and its blue health) are left out: their life is the bag's own (TerraStats, CharmEffects.MaskLifeBonus,
        /// Joni's +35%). A spike's share of the life counts by the masks (PlayerHooks.SpikeHit), which Joni's single mask
        /// would have made the whole of it.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CharmUpdate))]
        static class NoVanillaMasks
        {
            static readonly AccessTools.FieldRef<HeroController, bool> joniBeam = AccessTools.FieldRefAccess<HeroController, bool>("joniBeam");

            static void Postfix(HeroController __instance)
            {
                var pd = PlayerData.instance;
                if (!Charms.Enabled || pd == null) return;
                if (pd.maxHealth == pd.maxHealthBase && pd.joniHealthBlue == 0) return;
                pd.maxHealth = pd.maxHealthBase;
                pd.joniHealthBlue = 0;
                joniBeam(__instance) = false;
                pd.health = Mathf.Min(Mathf.Max(pd.health, 1), pd.maxHealth);
                if (pd.health < pd.maxHealth) pd.health = pd.maxHealth;
                pd.UpdateBlueHealth();
            }
        }
    }
}
