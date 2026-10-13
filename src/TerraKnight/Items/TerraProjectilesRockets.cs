using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The rocket launchers' shots (sweep R4b; AI_016 in Terraria's space, px y down, per update):
    /// the Grenade Launcher's grenades (133...), the Rocket Launcher's rockets (134...), the
    /// Proximity Mine Launcher's mines (135...) and the Snowman Cannon's homing snowmen (338...),
    /// each from Rockets I-IV, Cluster Rockets I-II, Mini Nukes I-II and the Wet, Lava, Honey and
    /// Dry Rockets (AmmoID.Sets.SpecificLauncherAmmoProjectileMatches), and the cluster fragments
    /// (779, 783, 862, 863). Blast boxes, knockback and the blocks they break follow
    /// PrepareBombToBlow and Kill; the blast also catches the player, as Terraria's explosives do
    /// (not the snowmen's). The liquid rockets spill their liquid where they burst: Hallownest has
    /// no liquids of Terraria's own, so it lies there a while as a pool (sweep R6b).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        enum RocketFamily : byte { Grenade, Rocket, Mine, Snowman, Fragment }

        /// <summary>What each rocket-launcher projectile is: its family, its ammo's tier (0-3 Rockets I-IV, 4-5 Cluster, 6-7 Mini Nuke, 8-11 Wet, Lava, Honey, Dry), its texture id.</summary>
        static readonly Dictionary<ProjKind, (RocketFamily Fam, int Tier, int Id)> rockets = new Dictionary<ProjKind, (RocketFamily, int, int)>();

        /// <summary>Launcher → ammo tier → projectile.</summary>
        static readonly Dictionary<ItemType, ProjKind[]> launcherShots = new Dictionary<ItemType, ProjKind[]>();

        public static int DevRocketBlasts, DevClusterFragments, DevRocketBlocks, DevLiquidPools, DevPoolsDried, DevLavaBurns, DevWaterDousings;

        /// <summary>Dev: the middle of the last pool spilt (world), and how the last spill went.</summary>
        public static Vector2 DevLastPool;
        public static string DevSpillNote = "";

        static void RegisterRockets()
        {
            void Family(ItemType launcher, RocketFamily fam, (ProjKind Kind, int Id)[] tiers)
            {
                var shots = new ProjKind[tiers.Length];
                for (int t = 0; t < tiers.Length; t++)
                {
                    var (kind, id) = tiers[t];
                    shots[t] = kind;
                    rockets[kind] = (fam, t, id);
                    specs[kind] = RocketSpec(kind, fam, t, id);
                }
                launcherShots[launcher] = shots;
            }
            // P:2224-2340, 4310, 7940-7952 (SetDefaults): 14 px, through everything; the grenades (and the cluster and nuke ones) each enemy once.
            Family(ItemType.GrenadeLauncher, RocketFamily.Grenade, new[]
            {
                (ProjKind.LauncherGrenade1, 133), (ProjKind.LauncherGrenade2, 136), (ProjKind.LauncherGrenade3, 139), (ProjKind.LauncherGrenade4, 142),
                (ProjKind.LauncherGrenadeCluster1, 777), (ProjKind.LauncherGrenadeCluster2, 781), (ProjKind.LauncherGrenadeNuke1, 794), (ProjKind.LauncherGrenadeNuke2, 797),
                (ProjKind.LauncherGrenadeWet, 785), (ProjKind.LauncherGrenadeLava, 788), (ProjKind.LauncherGrenadeHoney, 791), (ProjKind.LauncherGrenadeDry, 800),
            });
            Family(ItemType.RocketLauncher, RocketFamily.Rocket, new[]
            {
                (ProjKind.Rocket1, 134), (ProjKind.Rocket2, 137), (ProjKind.Rocket3, 140), (ProjKind.Rocket4, 143),
                (ProjKind.RocketCluster1, 776), (ProjKind.RocketCluster2, 780), (ProjKind.RocketNuke1, 793), (ProjKind.RocketNuke2, 796),
                (ProjKind.RocketWet, 784), (ProjKind.RocketLava, 787), (ProjKind.RocketHoney, 790), (ProjKind.RocketDry, 799),
            });
            Family(ItemType.ProximityMineLauncher, RocketFamily.Mine, new[]
            {
                (ProjKind.Mine1, 135), (ProjKind.Mine2, 138), (ProjKind.Mine3, 141), (ProjKind.Mine4, 144),
                (ProjKind.MineCluster1, 778), (ProjKind.MineCluster2, 782), (ProjKind.MineNuke1, 795), (ProjKind.MineNuke2, 798),
                (ProjKind.MineWet, 786), (ProjKind.MineLava, 789), (ProjKind.MineHoney, 792), (ProjKind.MineDry, 801),
            });
            Family(ItemType.SnowmanCannon, RocketFamily.Snowman, new[]
            {
                (ProjKind.SnowmanRocket1, 338), (ProjKind.SnowmanRocket2, 339), (ProjKind.SnowmanRocket3, 340), (ProjKind.SnowmanRocket4, 341),
                (ProjKind.SnowmanCluster1, 803), (ProjKind.SnowmanCluster2, 804), (ProjKind.SnowmanNuke1, 808), (ProjKind.SnowmanNuke2, 809),
                (ProjKind.SnowmanWet, 805), (ProjKind.SnowmanLava, 806), (ProjKind.SnowmanHoney, 807), (ProjKind.SnowmanDry, 810),
            });
            // 779, 783, 862, 863: the cluster fragments, four frames.
            foreach (var (kind, id, tier) in new[] { (ProjKind.ClusterFragment1, 779, 4), (ProjKind.ClusterFragment2, 783, 5), (ProjKind.SnowmanFragment1, 862, 4), (ProjKind.SnowmanFragment2, 863, 5) })
            {
                rockets[kind] = (RocketFamily.Fragment, tier, id);
                specs[kind] = RocketSpec(kind, RocketFamily.Fragment, tier, id);
            }
            // A liquid rocket's spill, lying where it fell (see SpillLiquid).
            specs[ProjKind.SpilledLiquid] = new Spec
            {
                Ai = Ai.Custom, W = 16, H = 16, Time = 1260, Penetrate = -1, TileCollide = false, OwnMove = true, Look = Look.Invisible, CanDamage = p => false,
                Update = (t, p) => t.StepPool(p), Draw = (t, fx, p, px) => DrawPool(fx, p, px), OnKill = (t, p) => { },
            };
        }

        static Spec RocketSpec(ProjKind kind, RocketFamily fam, int tier, int id)
        {
            // P:7941-7952: of the liquid grenades only the wet, lava and dry ones hit each enemy once (791 is left off the list).
            bool once = fam == RocketFamily.Grenade && id != 791;
            return new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_" + id, W = 14, H = 14, Time = 3600, Penetrate = -1, OncePerNpc = once,
                Scale = fam == RocketFamily.Snowman ? 0.9f : 1f, Frames = fam == RocketFamily.Fragment ? 4 : 1,
                Update = (t, p) => t.StepRocket(p), Draw = (t, fx, p, px) => DrawRocket(fx, p, px), OnKill = (t, p) => t.KillRocket(p),
                OnHit = (t, p, hm) =>
                {
                    // aiStyle 16 on a hit (P:13153-13158): it goes off in 3 updates.
                    if (p.TimeLeft > 3) p.TimeLeft = 3;
                },
                TileHit = (t, p, hx, hy, before) => RocketTile(p, hx, hy, before),
                ModifyHit = (t, p, hm, dealt, crit) =>
                {
                    // P:14505-14512: a rocket's own hit counts double, a mine's triple once it has settled.
                    if (fam == RocketFamily.Rocket && p.TimeLeft > 3) dealt *= 2;
                    else if (fam == RocketFamily.Mine && TV(p).magnitude < 0.5f) dealt *= 3;
                    return (dealt, crit);
                },
            };
        }

        /// <summary>The launcher's own projectile for the ammo used (PickAmmo, Pl:54092-54127).</summary>
        public static ProjKind LauncherShot(ItemType launcher, ItemType ammo)
        {
            if (!launcherShots.TryGetValue(launcher, out var shots)) return ProjKind.None;
            int tier = ammo switch
            {
                ItemType.RocketI => 0, ItemType.RocketII => 1, ItemType.RocketIII => 2, ItemType.RocketIV => 3,
                ItemType.ClusterRocketI => 4, ItemType.ClusterRocketII => 5, ItemType.MiniNukeI => 6, ItemType.MiniNukeII => 7,
                ItemType.WetRocket => 8, ItemType.LavaRocket => 9, ItemType.HoneyRocket => 10, ItemType.DryRocket => 11, _ => 0,
            };
            return shots[tier];
        }

        /// <summary>
        /// AI_016 for the launchers' shots (P:47976-48390): the snowmen fade in, trail fire (cluster
        /// ones cyan, 228) and smoke, and after 30 updates steer a twelfth of the way an update toward
        /// the nearest enemy they can see within 600 px (across plus down), or straight on, at 16 px;
        /// the rockets trail fire and smoke while fast and speed up 10% an update to 15; the grenades
        /// smoke, and after 15 updates fall 0.2 (rolling to a stop on the ground); the mines fall 0.2,
        /// drag 3% and settle, smoking while they move and fading out when still; the fragments
        /// sparkle and smoke, and fall after 10 updates. Grenades, mines and fragments tumble.
        /// </summary>
        void StepRocket(Proj p)
        {
            var (fam, tier, id) = rockets[p.Kind];
            var v = TV(p);
            var c = CenterPx(p);
            var tl = c - new Vector2(7f, 7f);
            // P:47974-47990: the cluster ones' fire cyan; the wet, lava and honey ones' two parts in three water (33), lava (35), honey (152).
            int trail = tier == 4 || tier == 5 ? 228 : tier >= 8 && tier <= 10 && Random.Range(0, 3) != 0 ? (tier == 8 ? 33 : tier == 9 ? 35 : 152) : 6;
            p.Ai0++;
            switch (fam)
            {
                case RocketFamily.Snowman:
                {
                    if (++p.LocalAi1 > 6f) p.Alpha = 0f;
                    else p.Alpha = Mathf.Min(1f, (255f - 42f * p.LocalAi1 + 100f) / 255f);
                    if (p.LocalAi1 > 9f)
                        for (int k = 0; k < 2; k++)
                        {
                            var off = k == 1 ? v * 0.5f : Vector2.zero;
                            var at = tl + new Vector2(3f, 3f) + off - v * 0.5f;
                            if (Random.Range(0, 2) == 0)
                            {
                                int i = BoxDust(at, 6, 6, trail, 0f, 0f, 100);
                                if (i >= 0)
                                {
                                    D(i).Scale *= 1.4f + Random.Range(0, 10) * 0.1f;
                                    D(i).Velocity *= 0.2f;
                                    D(i).NoGravity = true;
                                    if (tier == 6 || tier == 7) NukeSparks(i, p);
                                }
                            }
                            if (Random.Range(0, 2) == 0)
                            {
                                int i = BoxDust(at, 6, 6, 31, 0f, 0f, 100, 0.5f);
                                if (i >= 0)
                                {
                                    D(i).FadeIn = 0.5f + Random.Range(0, 5) * 0.1f;
                                    D(i).Velocity *= 0.05f;
                                }
                            }
                        }
                    var aim = c + v * 100f;
                    if (p.Ai0 > 30f)
                    {
                        p.Ai0 = 30f;
                        float best = 600f;
                        foreach (var hm in EnemiesNear(c, 900f))
                        {
                            if (!Chaseable(hm)) continue;
                            var e = EnemyCenterPx(hm);
                            float d = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                            if (d < best && InSight(c, e))
                            {
                                best = d;
                                aim = e;
                            }
                        }
                    }
                    var to = aim - c;
                    v = Vector2.Lerp(v, (to.sqrMagnitude > 1e-6f ? to.normalized : new Vector2(0f, -1f)) * 16f, 1f / 12f);
                    if (v.x < 0f)
                    {
                        p.Dir = -1f;
                        p.TRot = Mathf.Atan2(-v.y, -v.x) - 1.57f;
                    }
                    else
                    {
                        p.Dir = 1f;
                        p.TRot = Mathf.Atan2(v.y, v.x) + 1.57f;
                    }
                    break;
                }
                case RocketFamily.Rocket:
                    if (Mathf.Abs(v.x) >= 8f || Mathf.Abs(v.y) >= 8f)
                        for (int k = 0; k < 2; k++)
                        {
                            var at = tl + new Vector2(3f, 3f) + (k == 1 ? v * 0.5f : Vector2.zero) - v * 0.5f;
                            int i = BoxDust(at, 6, 6, trail, 0f, 0f, 100);
                            if (i >= 0)
                            {
                                D(i).Scale *= 2f + Random.Range(0, 10) * 0.1f;
                                D(i).Velocity *= 0.2f;
                                D(i).NoGravity = true;
                                if (tier == 6 || tier == 7) NukeSparks(i, p);
                            }
                            i = BoxDust(at, 6, 6, 31, 0f, 0f, 100, 0.5f);
                            if (i >= 0)
                            {
                                D(i).FadeIn = 1f + Random.Range(0, 5) * 0.1f;
                                D(i).Velocity *= 0.05f;
                            }
                        }
                    if (Mathf.Abs(v.x) < 15f && Mathf.Abs(v.y) < 15f) v *= 1.1f;
                    if (v != Vector2.zero) p.TRot = ToRot(v) + 1.57f;
                    break;
                case RocketFamily.Grenade:
                {
                    int i = BoxDust(tl, 14, 14, 31, 0f, 0f, 100);
                    if (i >= 0)
                    {
                        D(i).Scale *= 1f + Random.Range(0, 10) * 0.1f;
                        D(i).Velocity *= 0.2f;
                        D(i).NoGravity = true;
                    }
                    if (p.Ai0 > 15f)
                    {
                        if (v.y == 0f) v.x *= 0.95f;
                        v.y += 0.2f;
                    }
                    p.TRot += v.x * 0.1f;
                    break;
                }
                case RocketFamily.Mine:
                    if (Mathf.Abs(v.x) < 0.2f && Mathf.Abs(v.y) < 0.2f) p.Alpha = Mathf.Min(200f / 255f, p.Alpha + 2f / 255f);
                    else
                    {
                        p.Alpha = 0f;
                        int i = BoxDust(tl + new Vector2(3f, 3f) - v * 0.5f, 6, 6, 31, 0f, 0f, 100);
                        if (i >= 0)
                        {
                            D(i).Scale *= 1.6f + Random.Range(0, 5) * 0.1f;
                            D(i).Velocity *= 0.05f;
                            D(i).NoGravity = true;
                        }
                    }
                    v.y += 0.2f;
                    v *= 0.97f;
                    if (Mathf.Abs(v.x) < 0.1f) v.x = 0f;
                    if (Mathf.Abs(v.y) < 0.1f) v.y = 0f;
                    p.TRot += v.x * 0.1f;
                    break;
                default:
                {
                    if (Random.Range(0, 25) == 0)
                    {
                        int i = BoxDust(tl, 14, 14, 228, -v.x / 10f, -v.y / 10f, 100);
                        if (i >= 0)
                        {
                            D(i).NoGravity = true;
                            D(i).Velocity = Vector2.zero;
                            D(i).Scale = 1.3f;
                        }
                    }
                    if (Random.Range(0, 5) == 0)
                    {
                        int i = BoxDust(tl, 14, 14, 31, -v.x / 10f, -v.y / 10f, 100);
                        if (i >= 0)
                        {
                            D(i).NoGravity = true;
                            D(i).Velocity = Vector2.zero;
                            D(i).Scale = 1.3f;
                        }
                    }
                    if (p.LocalAi0 == 0f)
                    {
                        p.LocalAi0 = 1f;
                        p.Frame = Random.Range(0, 4);
                    }
                    if (p.Ai0 > 10f)
                    {
                        p.Ai0 = 10f;
                        if (v.y == 0f && v.x != 0f)
                        {
                            v.x *= 0.97f;
                            if (Mathf.Abs(v.x) < 0.01f) v.x = 0f;
                        }
                        v.y += 0.2f;
                    }
                    p.TRot += v.x * 0.1f;
                    break;
                }
            }
            p.Vel = Flip(v);
            if (p.TimeLeft <= 3) p.Collide = false;
        }

        /// <summary>The Mini Nukes' trail: two more of each fire mote, one thrown any way, one round with the fuse (P:48114-48126).</summary>
        static void NukeSparks(int i, Proj p)
        {
            for (int k = 0; k < 2; k++)
            {
                int j = PointDust(D(i).Position, D(i).Type, D(i).Velocity, D(i).Alpha, D(i).Scale);
                if (j < 0) continue;
                D(j).NoGravity = true;
                D(j).Velocity = k == 0 ? Random.insideUnitCircle * 3f : RotateBy(Vector2.right, Mathf.PI * 2f * (p.TimeLeft / 20f)) * 3f;
            }
        }

        /// <summary>
        /// aiStyle 16 against a block (P:19863-19890): off walls at 40%, off the floor at 40% when
        /// coming down faster than 0.7; a rocket or a snowman stops dead there, unseen, and goes off.
        /// </summary>
        static void RocketTile(Proj p, bool hx, bool hy, Vector2 before)
        {
            var fam = rockets[p.Kind].Fam;
            var b = new Vector2(before.x, -before.y);
            var v = TV(p);
            if (hx) v.x = b.x * -0.4f;
            if (hy && b.y > 0.7f) v.y = b.y * -0.4f;
            if (fam == RocketFamily.Rocket || fam == RocketFamily.Snowman)
            {
                v = Vector2.zero;
                p.Alpha = 1f;
                p.TimeLeft = 3;
            }
            p.Vel = Flip(v);
        }

        /// <summary>PrepareBombToBlow (P:47640-47710): the blast box (px) and knockback for each, and the blocks it breaks (radius in tiles).</summary>
        static (float Size, float Knockback, int Blocks) RocketBlast(RocketFamily fam, int tier, int id)
        {
            if (fam == RocketFamily.Fragment) return (48f, 8f, tier == 5 ? 3 : 0);
            if (id == 809) return (14f, 4f, 7); // left out of PrepareBombToBlow's lists: no bigger
            if (tier >= 8) return (48f, 12f, 0); // P:47695-47703: the liquid ones, a small blow and no blocks broken
            return tier switch
            {
                0 => (128f, 8f, 0), 1 => (128f, 8f, 3), 2 => (200f, 10f, 0), 3 => (200f, 10f, 5),
                4 => (128f, 12f, 0), 5 => (128f, 12f, 3), 6 => (250f, 12f, 0), _ => (250f, 12f, 7),
            };
        }

        /// <summary>
        /// Kill for the launchers' shots (P:71780-71792, 79020-79960): the blast (and the player in
        /// it, but for the snowmen), the blocks broken, then by kind the smoke, fire and smoke gores of
        /// Rockets I-II and III-IV, the cluster ones' cyan sparks and six fragments at half damage, the
        /// Mini Nukes' mushroom of black smoke and rings of fire, the fragments' small pops.
        /// </summary>
        void KillRocket(Proj p)
        {
            var (fam, tier, id) = rockets[p.Kind];
            var (size, kb, blocks) = RocketBlast(fam, tier, id);
            Blast(p, size, p.Damage, kb);
            DevRocketBlasts++;
            bool selfSafe = fam == RocketFamily.Snowman || id == 862 || id == 863;
            var hero = HeroController.SilentInstance;
            if (!selfSafe && hero != null && !hero.cState.dead && !hero.cState.invulnerable)
            {
                var hb = hero.GetComponent<BoxCollider2D>().bounds;
                float half = size * 0.5f * Px;
                if (new Rect(p.Pos - Vector2.one * half, Vector2.one * half * 2f).Overlaps(new Rect(hb.min, hb.size)))
                    Player.PlayerHooks.HurtPlayer(hero, p.Damage, hb.center.x >= p.Pos.x ? 1 : -1, gameObject);
            }
            if (blocks > 0)
            {
                DevRocketBlocks += BlastTiles(p.Pos, blocks);
                BreakSpecialFloors(p.Pos, blocks * CellGrid.CellSize);
            }
            var c = CenterPx(p);
            var pp = TerraParticles.Instance;
            bool fragment = fam == RocketFamily.Fragment, cluster = tier == 4 || tier == 5, nuke = tier == 6 || tier == 7;
            bool pop = id == 133 || id == 136 || id == 139 || id == 777 || id == 781 || id == 794 || id == 797 || id == 785 || id == 788 || id == 791 || id == 800 || fragment;
            Sound(pop ? "Item_62" : "Item_14");
            var light = Glow.Create(transform, new Color(1f, 0.75f, 0.4f, 1f), size * Px * 0.9f);
            light.transform.position = new Vector3(p.Pos.x, p.Pos.y, Glow.Z);
            light.gameObject.AddComponent<FadeOut>().Begin(0.4f);
            if (tier >= 8)
            {
                KillLiquidRocket(c, tier - 8);
                return;
            }
            if (fragment)
            {
                var tl = c - new Vector2(11f, 11f);
                for (int k = 0; k < 15; k++)
                {
                    int i = BoxDust(tl, 22, 22, 31, 0f, 0f, 100, 0.8f);
                    if (i < 0) continue;
                    D(i).FadeIn = 0f;
                    D(i).Velocity *= 0.5f;
                }
                for (int k = 0; k < 5; k++)
                {
                    int i = BoxDust(tl, 22, 22, 228, 0f, 0f, 100, 2.5f);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).Velocity *= 2.5f;
                    }
                    i = BoxDust(tl, 22, 22, 228, 0f, 0f, 100, 1.1f);
                    if (i < 0) continue;
                    D(i).Velocity *= 2f;
                    D(i).NoGravity = true;
                }
                for (int k = 0; k < 3; k++)
                {
                    int i = BoxDust(tl, 22, 22, 226, 0f, 0f, 100, 1.1f);
                    if (i < 0) continue;
                    D(i).Velocity *= 2f;
                    D(i).NoGravity = true;
                }
                if (pp != null)
                    foreach (var d in Diagonals)
                        if (Random.Range(0, 5) == 0) pp.NewGore(tl, Vector2.zero, Random.Range(61, 64), 0.65f, 0.2f, d * 0.5f);
                return;
            }
            if (nuke)
            {
                var tl = c - new Vector2(25f, 25f);
                const float up = 3f;
                for (int k = 0; k < 50; k++)
                {
                    int i = BoxDust(tl, 50, 50, 31, 0f, 0f, 100, 2f);
                    if (i < 0) continue;
                    var o = D(i).Position - c;
                    D(i).Velocity = (o.sqrMagnitude > 1e-6f ? o.normalized : Vector2.zero) * (2f + Random.Range(0, 5) * 0.1f);
                    D(i).Velocity.y -= up * 0.5f;
                    D(i).Color = new Color(0f, 0f, 0f, 0.9f);
                    if (Random.Range(0, 2) == 0)
                    {
                        D(i).Scale = 0.5f;
                        D(i).FadeIn = 1f + Random.Range(0, 10) * 0.1f;
                        D(i).Color = new Color(0f, 0f, 0f, 0.8f);
                    }
                }
                for (int k = 0; k < 30; k++)
                {
                    int i = BoxDust(tl, 50, 50, 6, 0f, 0f, 100);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).FadeIn = 1.4f;
                        var o = D(i).Position - c;
                        D(i).Velocity = (o.sqrMagnitude > 1e-6f ? o.normalized : Vector2.zero) * (5.5f + Random.Range(0, 61) * 0.1f);
                        D(i).Velocity.y -= up * 0.5f;
                    }
                    i = BoxDust(tl, 50, 50, 6, 0f, 0f, 100);
                    if (i >= 0)
                    {
                        var o = D(i).Position - c;
                        D(i).Velocity = o.sqrMagnitude > 1e-6f ? o.normalized : Vector2.zero;
                        D(i).Velocity.y -= up * 0.25f;
                        D(i).Velocity *= 1.5f + Random.Range(0, 5) * 0.1f;
                        D(i).FadeIn = 0f;
                        D(i).Scale = 0.6f;
                    }
                    i = BoxDust(tl, 50, 50, 6, 0f, 0f, 100, 1.5f);
                    if (i < 0) continue;
                    D(i).NoGravity = k % 2 == 0;
                    var o2 = D(i).Position - c;
                    D(i).Velocity = (o2.sqrMagnitude > 1e-6f ? o2.normalized : Vector2.zero) * (3f + Random.Range(0, 21) * 0.2f);
                    D(i).Velocity.y -= up * 0.5f;
                    D(i).FadeIn = 1.2f;
                    if (!D(i).NoGravity)
                    {
                        D(i).Scale = 0.4f;
                        D(i).FadeIn = 0f;
                    }
                    else
                    {
                        D(i).Velocity *= 2f + Random.Range(0, 5) * 0.2f;
                        D(i).Velocity.y -= up * 0.5f;
                    }
                }
                for (int ring = 1; ring <= 3; ring++)
                {
                    float spin = Mathf.PI * 2f * Random.value;
                    for (float k = 0f; k < 1f; k += 1f / 11f)
                    {
                        var sp = RotateBy(Vector2.right, Mathf.PI * 2f * k + spin);
                        sp = RotateBy(new Vector2(sp.x, sp.y * 0.4f), -Mathf.PI);
                        var upDir = RotateBy(Vector2.right, -Mathf.PI / 2f);
                        int i = PointDust(c, 6, sp);
                        if (i < 0) continue;
                        D(i).FadeIn = 1.8f;
                        D(i).NoGravity = true;
                        D(i).Velocity = (D(i).Velocity * ring * (Random.value * 2f + 0.2f) + upDir * 0.8f * ring) * 2f;
                    }
                }
                if (pp != null)
                    for (int ring = 1; ring <= 3; ring++)
                        foreach (var d in Diagonals) pp.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, ring / 3f, d);
                return;
            }
            if (cluster)
            {
                var tl = c - new Vector2(11f, 11f);
                for (int k = 0; k < 30; k++)
                {
                    int i = BoxDust(tl, 22, 22, 31, 0f, 0f, 100, 1.5f);
                    if (i >= 0) D(i).Velocity *= 1.4f;
                }
                for (int k = 0; k < 40; k++)
                {
                    int i = BoxDust(tl, 22, 22, 228, 0f, 0f, 100, 3.5f);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).Velocity *= 7f;
                    }
                    i = BoxDust(tl, 22, 22, 228, 0f, 0f, 100, 1.3f);
                    if (i < 0) continue;
                    D(i).Velocity *= 4f;
                    D(i).NoGravity = true;
                }
                for (int k = 0; k < 8; k++)
                {
                    int i = BoxDust(tl, 22, 22, 226, 0f, 0f, 100, 1.3f);
                    if (i < 0) continue;
                    D(i).Velocity *= 4f;
                    D(i).NoGravity = true;
                }
                if (pp != null)
                    foreach (var d in Diagonals) pp.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, 0.8f, d);
                // Six fragments flung round and up at half damage, their fuses a little short.
                var frag = fam == RocketFamily.Snowman ? (tier == 5 ? ProjKind.SnowmanFragment2 : ProjKind.SnowmanFragment1) : tier == 5 ? ProjKind.ClusterFragment2 : ProjKind.ClusterFragment1;
                float start = Random.value * Mathf.PI * 2f;
                var keep = SpawnClass;
                SpawnClass = p.Class;
                for (float k = 0f; k < 1f; k += 1f / 6f)
                {
                    var fv = RotateBy(Vector2.right, start + k * Mathf.PI * 2f) * (4f + Random.value * 2f) + new Vector2(0f, -1f);
                    var q = Spawn(frag, p.Pos, Flip(fv), p.Damage / 2, 0f, p.Crit);
                    if (q == null) continue;
                    q.TimeLeft -= Random.Range(0, 30);
                    DevClusterFragments++;
                }
                SpawnClass = keep;
                return;
            }
            if (tier <= 1)
            {
                var tl = c - new Vector2(11f, 11f);
                for (int k = 0; k < 30; k++)
                {
                    int i = BoxDust(tl, 22, 22, 31, 0f, 0f, 100, 1.5f);
                    if (i >= 0) D(i).Velocity *= 1.4f;
                }
                for (int k = 0; k < 20; k++)
                {
                    int i = BoxDust(tl, 22, 22, 6, 0f, 0f, 100, 3.5f);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).Velocity *= 7f;
                    }
                    i = BoxDust(tl, 22, 22, 6, 0f, 0f, 100, 1.5f);
                    if (i >= 0) D(i).Velocity *= 3f;
                }
                if (pp != null)
                    for (int r = 0; r < 2; r++)
                        foreach (var d in Diagonals) pp.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, r == 0 ? 0.4f : 0.8f, d);
                return;
            }
            {
                var tl = c - new Vector2(40f, 40f);
                for (int k = 0; k < 40; k++)
                {
                    int i = BoxDust(tl, 80, 80, 31, 0f, 0f, 100, 2f);
                    if (i < 0) continue;
                    D(i).Velocity *= 3f;
                    if (Random.Range(0, 2) == 0)
                    {
                        D(i).Scale = 0.5f;
                        D(i).FadeIn = 1f + Random.Range(0, 10) * 0.1f;
                    }
                }
                for (int k = 0; k < 70; k++)
                {
                    int i = BoxDust(tl, 80, 80, 6, 0f, 0f, 100, 3f);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).Velocity *= 5f;
                    }
                    i = BoxDust(tl, 80, 80, 6, 0f, 0f, 100, 2f);
                    if (i >= 0) D(i).Velocity *= 2f;
                }
                if (pp != null)
                    for (int r = 0; r < 3; r++)
                        foreach (var d in Diagonals) pp.NewGore(c - new Vector2(24f, 24f), Vector2.zero, Random.Range(61, 64), 1f, (r + 1) / 3f, d);
            }
        }

        // ------------------------------------------------------------------ the liquid rockets (784-801, 805-807, 810)

        /// <summary>
        /// P:79466-79620: smoke, then a burst of the liquid itself flung out (water 33 and honey
        /// 152 big, lava 35 and the dry one's smoke small; the water's thrown up), the smoke gores,
        /// and the liquid spread three tiles round (the dry one soaks up three and a half's).
        /// </summary>
        void KillLiquidRocket(Vector2 c, int liquid)
        {
            var tl = c - new Vector2(11f, 11f);
            for (int k = 0; k < 30; k++)
            {
                int i = BoxDust(tl, 22, 22, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 1.4f;
            }
            int dust = liquid switch { 0 => 33, 1 => 35, 2 => 152, _ => 31 };
            bool big = liquid == 0 || liquid == 2;
            for (int k = 0; k < 80; k++)
            {
                int i = BoxDust(tl, 22, 22, dust, 0f, 0f, 100, big ? 2.2f : 1.2f);
                if (i >= 0)
                {
                    if (liquid == 0)
                    {
                        D(i).NoGravity = true;
                        D(i).Velocity.y -= 1.2f;
                    }
                    D(i).Velocity *= 7f;
                }
                i = BoxDust(tl, 22, 22, dust, 0f, 0f, 100, big ? 1.3f : 0.3f);
                if (i < 0) continue;
                if (liquid == 0) D(i).Velocity.y -= 1.2f;
                D(i).Velocity *= 4f;
            }
            var pp = TerraParticles.Instance;
            if (pp != null)
                for (int r = 0; r < 2; r++)
                    foreach (var d in Diagonals) pp.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, r == 0 ? 0.4f : 0.8f, d);
            if (liquid < 3) SpillLiquid(c, liquid);
            else DryUp(c);
        }

        /// <summary>
        /// DelegateMethods.SpreadWater / SpreadLava / SpreadHoney over three tiles round the
        /// burst: about 28 tiles of liquid, which here runs down to the floor below and spreads
        /// along it (ten tiles each way at most, stopped by walls and drops) into a pool that lies
        /// 20 seconds and then sinks away.
        /// </summary>
        void SpillLiquid(Vector2 c, int liquid)
        {
            // Hallownest's own ground is colliders as much as cells: the terrain layer is asked, not the grid.
            const int mask = 1 << ChunkColliders.TerrainLayer;
            const float cell = CellGrid.CellSize;
            var w = TerraParticles.ToWorld(c);
            DevSpillNote = $"({w.x:F1},{w.y:F1})";
            for (int up = 0; up < 3 && Physics2D.OverlapPoint(w, mask) != null; up++) w.y += cell;
            if (Physics2D.OverlapPoint(w, mask) != null)
            {
                DevSpillNote += " 埋在方块里";
                return;
            }
            var hit = Physics2D.Raycast(w, Vector2.down, 20f, mask);
            if (hit.collider == null)
            {
                DevSpillNote += " 下方 20 格没有地面";
                return;
            }
            float floorY = hit.point.y, x0 = hit.point.x, left = x0, right = x0;
            bool Floor(float x) => Physics2D.Raycast(new Vector2(x, floorY + cell * 0.6f), Vector2.down, cell * 1.2f, mask).collider != null;
            bool Wall(float from, float dir) => Physics2D.Raycast(new Vector2(from, floorY + cell * 0.5f), new Vector2(dir, 0f), cell, mask).collider != null;
            for (int k = 0; k < 10 && !Wall(left, -1f) && Floor(left - cell); k++) left -= cell;
            for (int k = 0; k < 10 && !Wall(right, 1f) && Floor(right + cell); k++) right += cell;
            float width = (right - left + cell) / Px;
            float depth = Mathf.Clamp(28f * 256f / width, 6f, 64f);
            var keep = SpawnClass;
            SpawnClass = DamageClass.None;
            var p = Spawn(ProjKind.SpilledLiquid, new Vector2((left + right) / 2f, floorY + depth * Px / 2f), Vector2.zero, 0, 0f, 0f);
            SpawnClass = keep;
            if (p == null)
            {
                DevSpillNote += " 生成失败";
                return;
            }
            DevSpillNote += $" 地面 y{floorY:F2} 宽{width / 16f:F0}格 深{depth:F0}px";
            p.Ai0 = liquid;
            p.HitSize = new Vector2(width, depth);
            DevLiquidPools++;
            DevLastPool = p.Pos;
        }

        /// <summary>DelegateMethods.SpreadDry over three and a half tiles: the pools it reaches soak away.</summary>
        void DryUp(Vector2 c)
        {
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != ProjKind.SpilledLiquid) continue;
                var pc = CenterPx(q);
                var half = q.HitSize * 0.5f + new Vector2(56f, 56f);
                if (Mathf.Abs(pc.x - c.x) > half.x || Mathf.Abs(pc.y - c.y) > half.y) continue;
                q.TimeLeft = Mathf.Min(q.TimeLeft, 20);
                DevPoolsDried++;
            }
        }

        /// <summary>Player.lavaTime: the Lava Charm's seconds in lava, spent while in it and won back out of it.</summary>
        static int lavaTime, lavaTimeTick = -1;

        /// <summary>
        /// What lying in the liquid does. Lava (N:94457-94478, Pl:27884-27923): an enemy burns for
        /// 7 seconds and takes 50 every half second; the player takes 80 (35 with an Obsidian
        /// Rose) once the Lava Charm's 7 seconds are spent. Water puts out an enemy's fires
        /// (N:94420-94432). Honey gives the player Honey for 30 seconds (Pl:27951-27955).
        /// </summary>
        void StepPool(Proj p)
        {
            p.Alpha = p.TimeLeft < 60 ? 1f - p.TimeLeft / 60f : 0f;
            var c = CenterPx(p);
            var half = p.HitSize * 0.5f;
            var rect = new Rect(TerraParticles.ToWorld(c + new Vector2(-half.x, half.y)), p.HitSize * Px);
            int liquid = (int)p.Ai0;
            if (liquid != 2)
                foreach (var hm in EnemiesNear(c, Mathf.Max(half.x, half.y) + 64f))
                {
                    var col = hm.GetComponent<Collider2D>();
                    if (col == null || !rect.Overlaps(new Rect(col.bounds.min, col.bounds.size))) continue;
                    if (liquid == 0)
                    {
                        if (EnemyDebuffs.Has(hm, EnemyDebuffs.OnFire) || EnemyDebuffs.Has(hm, EnemyDebuffs.Hellfire)) DevWaterDousings++;
                        EnemyDebuffs.Remove(hm, EnemyDebuffs.OnFire);
                        EnemyDebuffs.Remove(hm, EnemyDebuffs.Hellfire);
                        continue;
                    }
                    int id = hm.GetInstanceID();
                    if (p.Immune.ContainsKey(id)) continue;
                    EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 420);
                    if (StrikeEnemy(p, hm, 50, 0f))
                    {
                        p.Immune[id] = 30;
                        DevLavaBurns++;
                    }
                }
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || hero.cState.dead || mod == null) return;
            var hb = hero.GetComponent<BoxCollider2D>().bounds;
            bool inIt = p.Alpha < 1f && rect.Overlaps(new Rect(hb.min, hb.size));
            if (liquid == 2 && inIt) mod.Stats.HoneyTicks = Mathf.Max(mod.Stats.HoneyTicks, 1800f);
            if (liquid != 1) return;
            int max = Equipment.Current.LavaImmuneTicks;
            // Once a tick, over every pool: spent while in lava, won back out of it.
            int tick = Time.frameCount;
            if (inIt)
            {
                lavaTimeTick = tick;
                if (lavaTime > 0) lavaTime--;
                else if (!hero.cState.invulnerable && !Equipment.Current.LavaImmune)
                    Player.PlayerHooks.HurtPlayer(hero, Equipment.Current.LavaRose ? 35 : 80, hb.center.x >= p.Pos.x ? 1 : -1, gameObject);
            }
            else if (lavaTimeTick != tick && lavaTime < max) lavaTime++;
            if (lavaTime > max) lavaTime = max;
            if (Random.Range(0, 10) == 0)
            {
                // The lava's own bubbles (35) at its face.
                int i = BoxDust(c - new Vector2(half.x, half.y + 4f), (int)p.HitSize.x, 8, 35, 0f, -1f, 0, 1f);
                if (i >= 0) D(i).Velocity.y = -Random.value * 2f;
            }
        }

        /// <summary>
        /// The pool, drawn with the liquid's own tile (Liquid_0 water, _1 lava, _11 honey) at
        /// LiquidRenderer's opacity (0.6, 0.95, 0.95); the lava lights itself and its place.
        /// </summary>
        static void DrawPool(FxBatch fx, Proj p, Vector2 px)
        {
            int liquid = (int)p.Ai0;
            string tex = liquid == 1 ? "Liquid_1" : liquid == 2 ? "Liquid_11" : "Liquid_0";
            float o = (liquid == 0 ? 0.6f : 0.95f) * (1f - p.Alpha);
            float lit = liquid == 1 ? 1f : L;
            var spr = FxBatch.Frame(tex, 0, 0, 16, 16, 8f, 8f);
            fx.Draw(spr, px, new Color(lit * o, lit * o, lit * o, o), 0f, new Vector2(p.HitSize.x / 16f, p.HitSize.y / 16f));
            if (liquid == 1) fx.Light(px, new Color(0.55f, 0.33f, 0.11f) * (1f - p.Alpha), Mathf.Max(3f, p.HitSize.x / 32f));
        }

        static readonly Vector2[] Diagonals = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) };

        /// <summary>The shot about its middle, lit, as it shows; the fragments' frame; the snowmen mirrored going left.</summary>
        static void DrawRocket(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / p.Spec.Frames;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale,
                flipX: rockets[p.Kind].Fam == RocketFamily.Snowman && p.Dir < 0f);
        }
    }
}
