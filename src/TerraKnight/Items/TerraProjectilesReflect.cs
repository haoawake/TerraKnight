using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Shots a shield sends back. A shield that stops a shot (<see cref="MeleeHits.Blocks"/>, held one way:
    /// <see cref="MeleeHits.IsShield"/>) turns the ones Terraria lets be reflected (Projectile.CanBeReflected: aiStyle 1,
    /// 2, 8, 21, 24, 28, 29, 131, and 728, 955) into the enemy's (NPC.ReflectProjectile): aimed back at the player with a
    /// little scatter, damage halved twice, one hit. Here it comes back as a slow energy orb (<see cref="ReflectSpeed"/>
    /// px a tick), not at the shot's own speed, so it can be walked away from. A reflected shot hurts only the player,
    /// doubled as any hostile shot is (Projectile.Damage; a reflected one skips the difficulty's scaling).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>How fast a reflected shot comes back (Terraria px a tick; the player runs at 3).</summary>
        public const float ReflectSpeed = 1f;

        static void RegisterReflect()
        {
            // The Magnet Sphere's orb (AI_047's frames), reddened, smaller, lit; gone on the terrain, or after six seconds.
            specs[ProjKind.ReflectedOrb] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_254", W = 22, H = 22, Scale = 0.7f, Time = 360, Penetrate = 1, NoGravity = true,
                Light = new Color(0.5f, 0.15f, 0.2f), Update = (t, p) => StepReflectedOrb(p), Draw = (t, fx, p, px) => DrawReflectedOrb(fx, p, px),
                OnKill = (t, p) => t.OrbDust(p),
            };
        }

        static void StepReflectedOrb(Proj p)
        {
            p.TRot += (p.Vel.x >= 0f ? 1f : -1f) * 0.05f;
            if (++p.FrameCounter > 6)
            {
                p.FrameCounter = 0;
                if (++p.Frame > 4) p.Frame = 0;
            }
        }

        static void DrawReflectedOrb(FxBatch fx, Proj p, Vector2 px)
        {
            float o = p.TimeLeft < 30 ? p.TimeLeft / 30f : 1f;
            fx.Draw(FxBatch.Frame("Projectile_254", 0, p.Frame * 44, 38, 44, 19f, 22f), px, new Color(1f, 0.45f, 0.5f, 0f) * o, p.TRot, p.Scale);
        }

        void OrbDust(Proj p)
        {
            var particles = TerraParticles.Instance;
            if (particles == null) return;
            for (int i = 0; i < 6; i++)
            {
                int d = particles.NewDust(TerraParticles.ToPx(p.Pos) - new Vector2(8f, 8f), 16, 16, 60);
                if (d >= 0) particles[d].Velocity *= 0.5f;
            }
        }

        /// <summary>Terraria's projectile types that can be reflected (from Projectile.SetDefaults' aiStyles).</summary>
        static readonly HashSet<int> reflectable = new HashSet<int>
        {
            1, 2, 3, 4, 5, 14, 15, 20, 21, 27, 36, 41, 48, 51, 54, 55, 69, 70, 77, 81, 82, 83, 84, 88, 89, 90, 91, 93, 94, 95, 96, 98, 100, 103, 104, 110,
            117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 128, 129, 158, 159, 160, 161, 162, 166, 172, 174, 176, 177, 178, 180, 184, 195, 207, 225, 240,
            242, 246, 248, 249, 253, 257, 258, 259, 265, 267, 270, 275, 276, 278, 279, 282, 283, 284, 285, 286, 287, 289, 299, 300, 302, 304, 306, 309, 311,
            312, 318, 323, 325, 330, 336, 337, 344, 345, 347, 348, 349, 350, 355, 357, 359, 370, 371, 389, 408, 424, 425, 426, 435, 436, 437, 438, 440, 442,
            449, 459, 462, 467, 468, 469, 471, 474, 477, 478, 479, 484, 485, 495, 497, 498, 501, 502, 504, 507, 508, 510, 520, 521, 522, 532, 572, 576, 577,
            581, 583, 585, 587, 589, 592, 593,
            594, 597, 599, 601, 605, 606, 616, 619, 620, 621, 622, 634, 635, 638, 639, 640, 645, 660, 661, 662, 664, 666, 668, 671, 675, 676, 680, 682, 684, 685,
            686, 706, 709, 710, 711, 712, 719, 728, 731, 763, 772, 811, 814, 819, 837, 861, 868, 869, 876, 909, 920, 921, 926, 936, 937, 954, 955, 962, 967, 968,
            979, 980, 981, 996, 1006, 1012, 1023, 1026, 1073, 1085, 1097, 1099, 1111, 1114, 1120, 1124, 1134,
        };

        static readonly Dictionary<ProjKind, bool> canReflect = new Dictionary<ProjKind, bool>();

        public static int DevReflected, DevReflectHits;
        /// <summary>Dev: the last reflected shot's kind, its speed before and after, its damage after, and its last hit on the player.</summary>
        public static ProjKind DevReflectKind;
        public static float DevReflectSpeedIn, DevReflectSpeedOut;
        public static int DevReflectDamage, DevReflectHitDamage;

        static GameObject reflectSource;

        /// <summary>Whether a shot can be sent back: its Terraria type (its texture's number) is one Terraria reflects.</summary>
        static bool CanReflect(Proj p)
        {
            if (p.Damage <= 0 || p.Hostile) return false;
            if (canReflect.TryGetValue(p.Kind, out bool yes)) return yes;
            string tex = p.Spec.Texture;
            yes = tex != null && tex.StartsWith("Projectile_") && int.TryParse(tex.Substring(11), out int id) && reflectable.Contains(id);
            canReflect[p.Kind] = yes;
            return yes;
        }

        /// <summary>NPC.ReflectProjectile, at <see cref="ReflectSpeed"/> of the speed.</summary>
        void Reflect(Proj p, Vector2 playerCentre)
        {
            DevReflected++;
            Sound("Item_150");
            var particles = TerraParticles.Instance;
            if (particles != null)
            {
                var size = new Vector2(p.Spec.W, p.Spec.H);
                for (int i = 0; i < 3; i++)
                {
                    int d = particles.NewDust(TerraParticles.ToPx(p.Pos) - size * 0.5f, (int)size.x, (int)size.y, 31);
                    if (d >= 0) particles[d].Velocity *= 0.3f;
                }
            }
            float speed = Mathf.Max(p.Vel.magnitude, p.OldVel.magnitude);
            DevReflectKind = p.Kind;
            DevReflectSpeedIn = speed;
            var to = playerCentre - p.Pos;
            to = to.sqrMagnitude > 1e-6f ? to.normalized : -p.Vel.normalized;
            var v = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
            v = v.sqrMagnitude > 0f ? v.normalized : Vector2.zero;
            v += to * 20f;
            v = v.sqrMagnitude > 1e-6f ? v.normalized * ReflectSpeed : -p.Vel.normalized * ReflectSpeed;
            // The shot itself is gone (no blast, no dust of its own); an orb comes back in its place.
            p.Dead = true;
            var orb = Spawn(ProjKind.ReflectedOrb, p.Pos, v, p.Damage / 2 / 2, 0f, 0f);
            if (orb == null) return;
            orb.Hostile = true;
            orb.Class = p.Class;
            DevReflectSpeedOut = v.magnitude;
            DevReflectDamage = orb.Damage;
        }

        /// <summary>A reflected shot against the player (Projectile.Damage's hostile check): its damage doubled, then gone.</summary>
        void HitPlayer(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || p.Dead || p.Damage <= 0 || hero.cState.dead) return;
            var s = p.Spec;
            var size = (p.HitSize != Vector2.zero ? p.HitSize : new Vector2((int)(s.W * s.Scale), (int)(s.H * s.Scale))) * Px;
            var shot = new Rect(p.Pos - size * 0.5f, size);
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            if (!shot.Overlaps(new Rect(b.min, b.size))) return;
            if (reflectSource == null)
            {
                reflectSource = new GameObject("Terra Reflected Shot");
                Object.DontDestroyOnLoad(reflectSource);
            }
            reflectSource.transform.position = new Vector3(p.Pos.x, p.Pos.y, 0f);
            DevReflectHits++;
            DevReflectHitDamage = p.Damage * 2;
            Player.PlayerHooks.HurtPlayer(hero, p.Damage * 2, p.Pos.x < b.center.x ? 1 : -1, reflectSource);
            Kill(p);
        }
    }
}
