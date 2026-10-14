using System.Collections;
using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Getting hurt and dying the Terraria way (Player.Hurt, Player.KillMe) instead of the Knight's.
    /// A hit: no time freeze, input lock or damage burst; the player is knocked back (4.5, 3.5 px
    /// per tick) and can steer straight out of it, blinks through 40 ticks of immunity, and bleeds.
    /// Death: the death cry, a burst of blood, the player falling apart in three pieces that fade,
    /// and "You were slain..." on screen. The game still handles the rest (shade, respawn).
    /// </summary>
    [HarmonyPatch]
    internal static class TerraHurt
    {
        static bool Active => TerraMovement.Enabled && TerraMod.Instance != null && World.TerrainSystem.Current != null;

        static readonly System.Action<HeroController, float> startInvulnerable =
            AccessTools.MethodDelegate<System.Action<HeroController, float>>(AccessTools.Method(typeof(HeroController), "StartInvulnerable"));
        static readonly System.Func<HeroController, bool> canTakeDamage =
            AccessTools.MethodDelegate<System.Func<HeroController, bool>>(AccessTools.Method(typeof(HeroController), "CanTakeDamage"));

        /// <summary>Player.GiveImmuneTimeForCollisionAttack: a few ticks of immunity.</summary>
        public static void Invulnerable(HeroController hero, int ticks) => startInvulnerable(hero, ticks / 60f);

        /// <summary>brainOfConfusionDodgeAnimationCounter: 300 after the Brain's dodge, a tick at a time to 0 (the ghost copies show for its first 30).</summary>
        public static int BrainDodgeTicks;

        /// <summary>Dev: dodges so far.</summary>
        public static int DevDodges;

        /// <summary>The last hit's Terraria damage, set where the game's mask damage becomes life.</summary>
        public static int LastDamage;
        static int lastHitDir;

        // Player.UpdateImmunity: while immune, immuneAlpha steps 50 a tick, bouncing between 50 and 205+.
        static int immuneAlpha, immuneDir = 1;
        static float tickAccumulator;
        static bool wasDead;
        static float slainAt = -1f;

        /// <summary>
        /// Terraria checks the Shield of Cthulhu's bonk before an enemy's touch can hurt (and the
        /// bonk gives immunity), so the enemy being dashed into never hurts. The game's contact
        /// damage comes first here; hold it off while the dash can still bonk.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static bool BeforeTakeDamage(HeroController __instance, GameObject go, int damageAmount, int hazardType)
        {
            if (!Active) return true;
            if (hazardType == 1 && TerraMovement.BonkReady && go != null && go.GetComponentInParent<HealthManager>() != null) return false;
            // The Turtle's thorns answer an enemy's touch before the hit, dodged or not (Pl:31586-31641); the
            // Brand of the Inferno's parry turns it (its own thorns ×2, no hurt).
            if (hazardType == 1 && damageAmount > 0 && go != null && canTakeDamage(__instance))
            {
                var attacker = go.GetComponentInParent<HealthManager>();
                if (attacker != null && Items.MeleeSpecials.TryParry(__instance, attacker, PlayerHooks.RawContact(damageAmount, go))) return false;
                if (attacker != null) Items.ArmorEffects.OnContact(attacker, PlayerHooks.RawContact(damageAmount, go));
            }
            // Player.Hurt's dodges, before any damage (Pl:38497-38513): the Mystic Arts Sash one in ten, the Brain of Confusion
            // one in six while its buff is off. Only an enemy's hit: a hazard's respawn can't be dodged.
            if (hazardType == 1 && damageAmount > 0 && canTakeDamage(__instance) && !__instance.cState.shadowDashing && TryDodge(__instance)) return false;
            return true;
        }

        static bool TryDodge(HeroController hero)
        {
            var b = Items.Equipment.Current;
            bool sash = b.MysticSashDodge && Random.Range(0, 10) == 0;
            // The Black Belt one in ten after the sash, before the Brain (Pl:38504-38508).
            bool belt = !sash && b.BlackBelt && Random.Range(0, 10) == 0;
            bool brain = !sash && !belt && b.BrainOfConfusion && Random.Range(0, 6) == 0 && !PlayerBuffs.Has(PlayerBuffs.BrainOfConfusion);
            // Holy Protection last (Pl:38514-38518).
            bool holy = !sash && !belt && !brain && Items.ArmorEffects.TryHolyDodge();
            if (!sash && !belt && !brain && !holy) return false;
            // SetImmuneTimeForAllTypes: 80 ticks, 120 with longInvince.
            startInvulnerable(hero, (b.LongImmunity ? 120 : 80) / 60f);
            immuneAlpha = 0;
            immuneDir = 1;
            if (holy) { }
            else if (sash) MysticSashDodge(hero);
            else if (belt)
            {
                NinjaDodge(hero);
                DevBeltDodges++;
            }
            else
            {
                // BrainOfConfusionDodge (Pl:38329-38338): the ghost copies and buff 321 for 4 s.
                BrainDodgeTicks = 300;
                PlayerBuffs.Add(PlayerBuffs.BrainOfConfusion, 240);
            }
            DevDodges++;
            return true;
        }

        /// <summary>Dev: the Black Belt's dodges.</summary>
        public static int DevBeltDodges;

        /// <summary>
        /// Vanilla fall damage goes through Player.Hurt and its dodges (Pl:25555-25565); the mod's
        /// own fall damage asks here first.
        /// </summary>
        public static bool DodgeFall(HeroController hero) => canTakeDamage(hero) && TryDodge(hero);

        /// <summary>NinjaDodge (Pl:38402-38448): a hundred grey smoke dust over the body, half of them floating, and five smoke gores; no sound.</summary>
        static void NinjaDodge(HeroController hero)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var topLeft = TerraMovement.TopLeft(hero);
            for (int i = 0; i < 100; i++)
            {
                int d = p.NewDust(topLeft, 20, 42, 31, 0f, 0f, 100, 2f);
                if (d < 0) continue;
                p[d].Position += new Vector2(Random.Range(-20, 21), Random.Range(-20, 21));
                p[d].Velocity *= 0.4f;
                p[d].Scale *= 1f + Random.Range(0, 40) * 0.01f;
                if (Random.Range(0, 2) == 0)
                {
                    p[d].Scale *= 1f + Random.Range(0, 40) * 0.01f;
                    p[d].NoGravity = true;
                }
            }
            DodgeSmoke(topLeft, 61, 0);
        }

        /// <summary>DoMysticSashDodge (Pl:38340-38387): fifty violet motes with white twins, and the smoke of a dodge.</summary>
        static void MysticSashDodge(HeroController hero)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var topLeft = TerraMovement.TopLeft(hero);
            for (int i = 0; i < 50; i++)
            {
                int d = p.NewDust(topLeft, 20, 42, 267, 0f, 0f, 255, Random.Range(10, 26) * 0.1f);
                if (d < 0) continue;
                p[d].NoGravity = true;
                p[d].Velocity *= 2.5f;
                p[d].Velocity.y -= 1.5f;
                p[d].FadeIn = 0.7f + 1.3f * Random.value;
                p[d].Scale = 0.7f + 1.3f * Random.value;
                p[d].Color = Color.Lerp(new Color(90f / 255f, 0f, 1f), new Color(40f / 255f, 0f, 1f), Random.value);
                p[d].Alpha = 200;
                p[d].Position.y += 5f;
                int w = p.Clone(d);
                if (w < 0) continue;
                p[w].Scale *= 0.35f;
                p[w].FadeIn *= 0.35f;
                p[w].Color = Color.white;
            }
            DodgeSmoke(topLeft, 11, 100);
        }

        /// <summary>
        /// The dodges' smoke: five smoke gores at 1.5 size drifting out from the middle (NinjaDodge:
        /// gores 61-63 opaque; DoMysticSashDodge: 11-13 at alpha 100).
        /// </summary>
        public static void DodgeSmoke(Vector2 topLeft, int goreBase, int alpha)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var at = topLeft + new Vector2(10f - 24f, 21f - 24f);
            foreach (var dir in new[] { Vector2.zero, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, 1.5f), new Vector2(1.5f, -1.5f), new Vector2(-1.5f, -1.5f) })
            {
                float rx = Random.Range(-50, 51) * 0.01f, ry = Random.Range(-50, 51) * 0.01f;
                var v = new Vector2(dir.x + (dir.x < 0f ? -rx : rx), dir.y + (dir.y < 0f ? -ry : ry)) * 0.4f;
                p.NewGore(at, v, goreBase + Random.Range(0, 3), 1.5f, 1f, default, true, alpha);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "StartRecoil")]
        static bool StartRecoil(HeroController __instance, GlobalEnums.CollisionSide impactSide, ref IEnumerator __result)
        {
            if (!Active) return true;
            // Hit from the left pushes right, as the game's recoil does.
            int hitDir = impactSide == GlobalEnums.CollisionSide.left ? 1 : impactSide == GlobalEnums.CollisionSide.right ? -1 : 0;
            __result = Hurt(__instance, hitDir);
            return false;
        }

        static IEnumerator Hurt(HeroController hero, int hitDir)
        {
            int damage = Mathf.Max(1, LastDamage);
            lastHitDir = hitDir;
            // Player.Hurt: 40 ticks of immunity (80 with the Cross Necklace's longInvince).
            var bonus = Items.Equipment.Current;
            startInvulnerable(hero, (damage == 1 ? 20 : bonus.LongImmunity ? 80 : 40) / 60f);
            immuneAlpha = 0;
            immuneDir = 1;
            // noKnockback (Cobalt Shield): the hit doesn't throw the player.
            if (hitDir != 0 && !bonus.NoKnockback) hero.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(4.5f * hitDir, 3.5f) * TerraMovement.PxT;
            var stats = TerraMod.Instance.Stats;
            Blood(hero, Mathf.CeilToInt(damage / (float)Mathf.Max(1, stats.LifeMax) * 100f), 2f * hitDir, -2f);
            yield break;
        }

        /// <summary>Player.Hurt, before the damage maths: the Panic Necklace's Panic! for 8 seconds.</summary>
        public static void BeforeHurt()
        {
            if (Items.Equipment.Current.Panic && TerraMod.Instance != null) TerraMod.Instance.PanicTicks = 480f;
        }

        /// <summary>
        /// Player.Hurt, after it: the Magic Cuffs give the raw damage back as mana; a real hit (not
        /// a hazard) brings down the Star Cloak's three stars and lets out the Honey Comb's bees.
        /// The stars and bees are Master Mode's (75 × 3, 13 × 2), as this world's enemies are.
        /// </summary>
        public static void AfterHurt(HeroController hero, int raw, int damage, bool retaliate)
        {
            var mod = TerraMod.Instance;
            var b = Items.Equipment.Current;
            if (mod == null || hero == null) return;
            Items.ArmorEffects.AfterHurt();
            if (b.MagicCuffs && raw > 0)
            {
                mod.Stats.RestoreMana(raw);
                UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, raw.ToString(), UI.CombatText.Mana);
            }
            if (!retaliate) return;
            var col = hero.GetComponent<BoxCollider2D>().bounds;
            var topLeft = new Vector2(col.center.x - 10f * TerraParticles.Px, col.min.y + 42f * TerraParticles.Px);
            var projectiles = Items.TerraProjectiles.Instance;
            // The Brain of Confusion (Pl:38690-38724): enemies about confused, and its illusion.
            if (b.BrainOfConfusion) BrainRetaliate(hero, damage);
            // One volley of the Star Cloak family's stars, the strongest override's kind (Pl:38725-38776).
            if (b.StarCloak && projectiles != null)
            {
                var kind = Items.TerraProjectiles.CloakStarKind(b.StarKind);
                for (int i = 0; i < 3; i++)
                {
                    // From 500-799 px above the player's top, 400 px either side, at 23 px a tick down at him (give or take 100 px).
                    var from = topLeft + new Vector2(Random.Range(-400, 400), Random.Range(500, 800)) * TerraParticles.Px;
                    var at = new Vector2(col.center.x + Random.Range(-100, 101) * TerraParticles.Px, topLeft.y - 21f * TerraParticles.Px);
                    projectiles.ShootCloakStar(kind, from, (at - from).normalized * 23f, 75 * 3, topLeft.y);
                }
            }
            if (b.HoneyComb)
            {
                // The Hive Pack's strong bees (Pl:38789-38800, 12505-12530): a fourth a third of the time, 18 for 13, half of them giant.
                bool strong = b.StrongBees;
                int bees = 1 + (Random.Range(0, 3) == 0 ? 1 : 0) + (Random.Range(0, 3) == 0 ? 1 : 0) + (strong && Random.Range(0, 3) == 0 ? 1 : 0);
                int dmg = (strong ? 18 : 13) * 2;
                for (int i = 0; i < bees; i++)
                {
                    var kind = strong && Random.Range(0, 2) == 0 ? Items.ProjKind.GiantBee : Items.ProjKind.Bee;
                    projectiles?.Shoot(kind, topLeft, new Vector2(Random.Range(-35, 36) * 0.02f, -Random.Range(-35, 36) * 0.02f),
                        dmg + (strong ? Random.Range(1, 4) : Random.Range(0, 2)), strong ? 0.5f : 0f, 4f);
                }
                mod.Stats.HoneyTicks = 300f;
            }
        }

        /// <summary>
        /// Each enemy, by chance (300 + 2·damage in 500), is confused for 1.5-5 s if within a
        /// random reach that grows with the damage (squeezed past 500, 700, 900 px); the
        /// illusion rises by the player.
        /// </summary>
        static readonly System.Collections.Generic.List<HealthManager> around = new System.Collections.Generic.List<HealthManager>();

        static void BrainRetaliate(HeroController hero, int dmg)
        {
            Vector2 c = hero.GetComponent<BoxCollider2D>().bounds.center;
            foreach (var hm in Items.AccessoryEffects.Enemies(c, 1200f, around))
            {
                if (Random.Range(0, 500) >= 300 + 2 * dmg) continue;
                float r = Random.Range(200 + dmg / 2, 301 + 2 * dmg);
                if (r > 500f) r = 500f + (r - 500f) * 0.75f;
                if (r > 700f) r = 700f + (r - 700f) * 0.5f;
                if (r > 900f) r = 900f + (r - 900f) * 0.25f;
                if (Vector2.Distance(hm.transform.position, c) / TerraParticles.Px >= r) continue;
                Items.EnemyDebuffs.Add(hm, Items.EnemyDebuffs.Confused, Random.Range(90 + dmg / 3, 300 + dmg / 2));
            }
            var v = hero.GetComponent<Rigidbody2D>().linearVelocity / TerraMovement.PxT;
            Items.TerraProjectiles.Instance?.BrainIllusion(c, v);
        }

        /// <summary>Terraria's player blood: dust 5 over the hitbox, thrown with the hit (Terraria y-down speeds).</summary>
        static void Blood(HeroController hero, int count, float speedX, float speedY)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var topLeft = TerraMovement.TopLeft(hero);
            for (int i = 0; i < count; i++) p.NewDust(topLeft, 20, 42, 5, speedX, speedY);
        }

        /// <summary>
        /// How opaque to draw the player: Terraria's immune blink (over half the time not drawn at
        /// all), on Terraria's 60 Hz tick. Fully opaque when not immune.
        /// </summary>
        /// <summary>Dev: the opacity last worked out.</summary>
        public static float LastOpacity { get; private set; } = 1f;

        public static float Opacity(HeroController hero, float dt) => LastOpacity = ComputeOpacity(hero, dt);

        static float ComputeOpacity(HeroController hero, float dt)
        {
            bool immune = hero.cState.invulnerable && !TerraMovement.IsBonkImmune && !hero.cState.dead;
            tickAccumulator += dt * 60f;
            int ticks = Mathf.FloorToInt(tickAccumulator);
            tickAccumulator -= ticks;
            for (int t = 0; t < ticks; t++)
            {
                if (!immune)
                {
                    immuneAlpha = 0;
                    continue;
                }
                immuneAlpha += immuneDir * 50;
                if (immuneAlpha <= 50) immuneDir = 1;
                else if (immuneAlpha >= 205) immuneDir = -1;
            }
            return immuneAlpha > 125 ? 0f : (255 - immuneAlpha) / 255f;
        }

        /// <summary>Every frame: notices the hero dying, and keeps the Knight's own death show hidden.</summary>
        public static void Tick(HeroController hero, TerraPlayerSprite sprite)
        {
            bool dead = hero.cState.dead;
            if (dead && !wasDead && Active) Die(hero, sprite);
            wasDead = dead;
            if (!dead)
            {
                slainAt = -1f;
                return;
            }
            // The Knight shattering, its smoke and its death sound, and the mask it drops (made apart from it).
            var show = hero.heroDeathPrefab;
            if (show != null && show.activeInHierarchy)
            {
                foreach (var r in show.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
                foreach (var a in show.GetComponentsInChildren<AudioSource>(true)) a.mute = true;
            }
            var mask = GameObject.Find("Corpse Head(Clone)");
            if (mask != null)
                foreach (var r in mask.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
        }

        static void Die(HeroController hero, TerraPlayerSprite sprite)
        {
            TerraMod.Instance.PlaySound("Player_Killed");
            Net.PvpHits.OnLocalDeath();
            Blood(hero, 100, 2f * lastHitDir, -2f);
            sprite.FallApart(lastHitDir);
            slainAt = Time.time;
            lastHitDir = 0;
        }

        static GUIStyle style;

        /// <summary>"You were slain..." fading in at the middle of the screen while dead.</summary>
        public static void DrawDeathText()
        {
            if (slainAt < 0f || Event.current.type != EventType.Repaint) return;
            var view = UI.GameView.Rect;
            float ui = UI.GameView.Scale;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            style.fontSize = Mathf.RoundToInt(52 * ui);
            // Terraria fades it in over about two seconds (immuneAlpha +2 a tick), to a soft red.
            float a = Mathf.Clamp01((Time.time - slainAt) / 2.1f) * 0.9f;
            var rect = new Rect(view.x, view.center.y - 60f * ui - 40f * ui, view.width, 80f * ui);
            style.normal.textColor = new Color(0f, 0f, 0f, a * 0.6f);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
                if (ox != 0 || oy != 0) GUI.Label(new Rect(rect.x + ox * 2f * ui, rect.y + oy * 2f * ui, rect.width, rect.height), Lang.T("你被杀死了……"), style);
            style.normal.textColor = new Color(1f, 0.55f, 0.55f, a);
            GUI.Label(rect, Lang.T("你被杀死了……"), style);
        }
    }
}
