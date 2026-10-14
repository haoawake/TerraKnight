using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's whips (spec-summoner C): the crack (AI_165) lives exactly the swing's
    /// itemAnimationMax ticks at two (Durendal three) updates a tick; its chain of control points
    /// (FillWhipControlPoints) curls out from the hand, straightens at two thirds of the swing
    /// and whips round as it comes back. Every enemy any point passes (this update's and the
    /// last's 18 px boxes) takes one hit per crack, in the player's sight: no crit, damage
    /// falling off with each enemy, the whip's debuff, its tag on the enemy and, for some, a
    /// proc, the snowflake or the player buff; and the enemy becomes the minions' target.
    /// </summary>
    internal sealed class Whips : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        const float L = 0.874f;

        public static Whips Instance { get; private set; }

        /// <summary>Per whip: projectile id, segments, range multiplier, updates a tick, damage kept per enemy, line colour.</summary>
        sealed class Setting
        {
            public int Id, Segments, Updates, Frames = 5;
            public float RangeMult, Falloff;
            public Color Line;
            /// <summary>DrawWhip's flag: the line stops one point short of the tip; its colour's opacity (the Slime Whip's ×0.5).</summary>
            public bool LineShort;
            public float LineAlpha = 1f;
            /// <summary>The crack's sound at half the swing (Item153 unless set) and its volume.</summary>
            public string Sound = "Item_153";
            public float SoundVolume = 1f;
            /// <summary>DrawWhip's flag2 / flag3: no line at all (the Moon Lord whip); the line's colour taken as is, unlit (the Constellation).</summary>
            public bool NoLine, LineUnlit;
        }

        static readonly Dictionary<WhipKind, Setting> settings = new Dictionary<WhipKind, Setting>
        {
            [WhipKind.Leather] = new Setting { Id = 841, Segments = 20, RangeMult = 0.75f, Updates = 2, Falloff = 0.5f, Line = Color.white },
            [WhipKind.Snapthorn] = new Setting { Id = 914, Segments = 20, RangeMult = 1f, Updates = 2, Falloff = 0.6f, Line = FxBatch.C(34, 139, 34) },
            [WhipKind.SpinalTap] = new Setting { Id = 952, Segments = 20, RangeMult = 0.95f, Updates = 2, Falloff = 0.9f, Line = FxBatch.C(210, 180, 140) },
            [WhipKind.Firecracker] = new Setting { Id = 913, Segments = 20, RangeMult = 1.4f, Updates = 2, Falloff = 0.66f, Line = FxBatch.C(178, 34, 34) },
            [WhipKind.CoolWhip] = new Setting { Id = 912, Segments = 20, RangeMult = 1.5f, Updates = 2, Falloff = 0.7f, Line = FxBatch.C(173, 216, 230) },
            [WhipKind.Durendal] = new Setting { Id = 847, Segments = 20, RangeMult = 1.75f, Updates = 3, Falloff = 0.8f, Line = FxBatch.C(255, 69, 0) },
            [WhipKind.MorningStar] = new Setting { Id = 848, Segments = 40, RangeMult = 1.6f, Updates = 2, Falloff = 0.95f, Line = FxBatch.C(0, 0, 139) },
            [WhipKind.DarkHarvest] = new Setting { Id = 849, Segments = 30, RangeMult = 2.15f, Updates = 3, Falloff = 0.9f, Line = Color.black },
            [WhipKind.Kaleidoscope] = new Setting { Id = 915, Segments = 40, RangeMult = 2.2f, Updates = 3, Falloff = 0.8f, Line = Color.white },
            [WhipKind.SlimeWhip] = new Setting { Id = 1104, Segments = 36, RangeMult = 0.75f, Updates = 2, Falloff = 0.4f, Line = FxBatch.C(78, 136, 255), LineAlpha = 0.5f },
            [WhipKind.CobWhip] = new Setting { Id = 1028, Segments = 12, RangeMult = 0.75f, Updates = 2, Falloff = 0.4f, Line = new Color(0.5f, 0.5f, 0.5f), Frames = 6, LineShort = true },
            [WhipKind.CorruptWhip] = new Setting { Id = 1029, Segments = 20, RangeMult = 0.95f, Updates = 2, Falloff = 0.6f, Line = FxBatch.C(128, 0, 128), Frames = 6, LineShort = true, Sound = "NPC_Killed_1", SoundVolume = 0.6f },
            [WhipKind.CrimsonWhip] = new Setting { Id = 1030, Segments = 20, RangeMult = 0.85f, Updates = 2, Falloff = 0.6f, Line = Color.red, Frames = 6, LineShort = true, Sound = "NPC_Hit_9" },
            [WhipKind.MeteorWhip] = new Setting { Id = 1031, Segments = 20, RangeMult = 1.1f, Updates = 2, Falloff = 0.7f, Line = FxBatch.C(88, 85, 155), Frames = 6, LineShort = true },
            [WhipKind.FlowerWhip] = new Setting { Id = 1032, Segments = 20, RangeMult = 1.8f, Updates = 2, Falloff = 0.85f, Line = FxBatch.C(143, 215, 29), Frames = 5, LineShort = true },
            [WhipKind.EelWhip] = new Setting { Id = 1033, Segments = 45, RangeMult = 2f, Updates = 2, Falloff = 0.95f, Line = Color.blue, Frames = 5, LineShort = true, Sound = "Item_193" },
            [WhipKind.ConstellationWhip] = new Setting { Id = 1034, Segments = 20, RangeMult = 2.2f, Updates = 2, Falloff = 0.9f, Line = new Color(0f, 61f / 255f, 153f / 255f, 12f / 255f), Frames = 6, LineShort = true, LineUnlit = true },
            [WhipKind.MoonLordWhip] = new Setting { Id = 1035, Segments = 30, RangeMult = 2.2f, Updates = 2, Falloff = 0.95f, Frames = 7, NoLine = true },
        };

        sealed class Crack
        {
            public WhipKind Kind;
            public Setting S;
            public ItemDef Item;
            /// <summary>Velocity in Terraria px per update (y down): the aim times the whip's shootSpeed.</summary>
            public Vector2 Vel;
            public float Ai0, Ai1, Rot;
            public int SpriteDir, AnimMax, Damage;
            public float Knockback;
            public bool FirstHit, Dead;
            public readonly HashSet<int> Hit = new HashSet<int>();
            /// <summary>The Constellation's stars released this crack; the Moon Lord whip's clones (and how many it has made) and its tip.</summary>
            public bool StarsDone, Clone;
            public int Spawned, TipVariant = 1;
            public Vector2 AimWorld;
            /// <summary>The Snake Band's second crack: its reach goes by the held whip's swing (FillWhipControlPoints uses HeldItem).</summary>
            public ItemDef ReachItem;
            /// <summary>A Moon Lord whip clone lasts 30 ticks whatever the swing's speed (P:49296).</summary>
            public int T => Clone ? 30 * S.Updates : AnimMax * S.Updates;
        }

        readonly List<Crack> pending = new List<Crack>();
        Vector2 pVel;

        readonly List<Crack> live = new List<Crack>();
        readonly List<Vector2> points = new List<Vector2>(), oldPoints = new List<Vector2>(), drawPoints = new List<Vector2>(), visPoints = new List<Vector2>();
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        float tickAccumulator;
        Vector2 pCenter;
        int pDir = 1;

        /// <summary>Dev: enemies struck by whips so far.</summary>
        public static int DevStrikes;
        public int Count => live.Count;

        public static Whips Create(Transform parent)
        {
            var go = new GameObject("Terra Whips");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<Whips>();
            return Instance;
        }

        /// <summary>
        /// The shot on the swing's first tick: toward the mouse at the whip's shootSpeed, a random
        /// (visual only) curl, the use's damage and knockback, living animMax ticks.
        /// </summary>
        public void Strike(ItemDef whip, Vector2 aimWorld, int animMax)
        {
            if (!settings.TryGetValue(whip.Whip, out var s) || !Owner()) return;
            var dir = TerraParticles.ToPx(aimWorld) - pCenter;
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(pDir, 0f);
            float ai1 = 0.6f + 0.4f * Random.value;
            // Pl:49152: one time in three the curl turns back hard, but never Dark Harvest's.
            if (whip.Whip != WhipKind.DarkHarvest && Random.Range(0, 3) == 0) ai1 *= -2.5f;
            var c = new Crack
            {
                Kind = whip.Whip, S = s, Item = whip, Vel = dir.normalized * whip.ShootSpeed, Ai1 = ai1, AnimMax = Mathf.Max(1, animMax),
                Damage = Equipment.Damage(whip), Knockback = WhipKnockback(whip), AimWorld = aimWorld, TipVariant = 1 + Random.Range(0, 3),
            };
            c.SpriteDir = c.Vel.x < 0f ? -1 : 1;
            c.Rot = Mathf.Atan2(c.Vel.y, c.Vel.x) + Mathf.PI / 2f;
            live.Add(c);
            // The Snake Band (Pl:49180-49186, 28622-28628): each swing counts up (from -1 after 120 ticks without one); on every
            // third, from the first, a second crack goes out once the swing is half done (Pl:43039-43062).
            if (Time.time - lastSwing > 2f) repeatSwings = -1;
            lastSwing = Time.time;
            repeatSwings++;
            echoIn = Equipment.Current.SnakeBand && repeatSwings % 3 == 0 ? Mathf.Max(1, animMax - (int)(animMax * 0.5f)) : 0;
            if (Equipment.Current.SnakeBand) DevEchoLog += $" 挥{repeatSwings}:{echoIn}";
            echoAnim = Mathf.Max(1, animMax);
        }

        int repeatSwings = -1, echoIn, echoAnim;
        float lastSwing = -10f;

        /// <summary>Dev: the Snake Band's second cracks so far, and why they came or didn't.</summary>
        public static int DevEchoes;
        public static string DevEchoLog = "";

        /// <summary>A whip's knockback: the Wicked Armlet's +1 (Pl:49155-49161).</summary>
        static float WhipKnockback(ItemDef whip) => Equipment.Knockback(whip) + (Equipment.Current.WickedArmlet ? 1f : 0f);

        /// <summary>
        /// The Snake Band's crack (ItemCheck_Shoot without its sound, Pl:43039-43062): the held
        /// whip again, or with a second tag slot the next whip in the inventory after it; toward
        /// the cursor give or take 0.314 rad; living twice the swing; 30% of the damage (at least
        /// 3) and a quarter of the knockback.
        /// </summary>
        void Echo()
        {
            var mod = TerraMod.Instance;
            var held = mod?.Inventory.Current.Def;
            if (held == null || !held.IsWhip || !settings.ContainsKey(held.Whip) || !mod.IsUsingItem)
            {
                DevEchoLog += $" 不出({held?.Name},{mod?.IsUsingItem})";
                return;
            }
            var item = held;
            if (Equipment.Current.MaxTagEffects > 0)
            {
                var inv = mod.Inventory;
                for (int i = inv.Held + 1; i < Inventory.Size; i++)
                {
                    var d = inv.Slots[i].IsEmpty ? null : inv.Slots[i].Def;
                    if (d == null || !d.IsWhip || !settings.ContainsKey(d.Whip)) continue;
                    item = d;
                    break;
                }
            }
            var aim = mod.DevAim ?? mod.AimPoint;
            var dir = TerraParticles.ToPx(aim) - pCenter;
            if (dir.sqrMagnitude < 1e-4f) dir = new Vector2(pDir, 0f);
            dir = Rotate(dir.normalized, Random.value * 0.314f - Random.value * 0.314f);
            float ai1 = 0.6f + 0.4f * Random.value;
            if (item.Whip != WhipKind.DarkHarvest && Random.Range(0, 3) == 0) ai1 *= -2.5f;
            var c = new Crack
            {
                Kind = item.Whip, S = settings[item.Whip], Item = item, ReachItem = held, Vel = dir * item.ShootSpeed, Ai1 = ai1, AnimMax = echoAnim * 2,
                Damage = Mathf.Max(3, (int)(Equipment.Damage(item) * 0.3f)), Knockback = WhipKnockback(item) * 0.25f, AimWorld = aim, TipVariant = 1 + Random.Range(0, 3),
            };
            c.SpriteDir = c.Vel.x < 0f ? -1 : 1;
            c.Rot = Mathf.Atan2(c.Vel.y, c.Vel.x) + Mathf.PI / 2f;
            pending.Add(c);
            DevEchoes++;
        }

        bool Owner()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            pCenter = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var rb = hero.GetComponent<Rigidbody2D>();
            pVel = rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Px / 60f : Vector2.zero;
            var mod = TerraMod.Instance;
            pDir = mod != null && mod.UseDirection < 0f ? -1 : 1;
            return true;
        }

        void Update()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm != null && !gm.isPaused && live.Count > 0)
            {
                tickAccumulator += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
                while (tickAccumulator >= 1f)
                {
                    tickAccumulator -= 1f;
                    Tick();
                }
            }
            else if (live.Count == 0) tickAccumulator = 0f;
            Draw();
        }

        void Tick()
        {
            if (!Owner())
            {
                live.Clear();
                return;
            }
            if (echoIn > 0 && --echoIn == 0) Echo();
            foreach (var c in live)
                for (int u = 0; u < c.S.Updates && !c.Dead; u++) Step(c);
            live.RemoveAll(c => c.Dead);
            live.AddRange(pending);
            pending.Clear();
        }

        void Step(Crack c)
        {
            c.Rot = Mathf.Atan2(c.Vel.y, c.Vel.x) + Mathf.PI / 2f;
            c.Ai0++;
            c.SpriteDir = c.Vel.x < 0f ? -1 : 1;
            if (c.Ai0 >= c.T)
            {
                c.Dead = true;
                return;
            }
            Points(c, points, true, 0);
            if (c.Ai0 == (int)(c.T / 2f)) TerraMod.Instance?.PlaySound(c.S.Sound, c.S.SoundVolume);
            if (c.Kind == WhipKind.MoonLordWhip && !c.Clone && c.Spawned < c.Ai0 / c.T * 3f) MoonLordClone(c);
            if (c.Kind == WhipKind.SlimeWhip && c.Ai0 == (int)(c.T / 3f) * 2) SlimeSpark(c);
            Dust(c);
            Points(c, oldPoints, true, -1);
            Collide(c);
        }

        /// <summary>
        /// P:48542-48587: the Moon Lord whip's three clone cracks, the first on its first update and
        /// one more at each third of the swing, toward the cursor at that moment, each its own whip
        /// (the whip's damage then, its own hits and falloff) living 30 ticks.
        /// </summary>
        void MoonLordClone(Crack c)
        {
            c.Spawned++;
            var aim = c.AimWorld;
            var mod = TerraMod.Instance;
            if (mod != null && !TerraMod.DevNoRealMouse && TerraMod.MouseWorld(out var w)) aim = w;
            var dir = TerraParticles.ToPx(aim) - pCenter;
            if (dir.sqrMagnitude < 1e-4f) dir = c.Vel;
            var clone = new Crack
            {
                Kind = c.Kind, S = c.S, Item = c.Item, Vel = dir.normalized * c.Vel.magnitude, Ai1 = Random.Range(-1f, 1f), AnimMax = c.AnimMax,
                Damage = c.Damage, Knockback = c.Knockback, AimWorld = aim, Clone = true, TipVariant = 1 + Random.Range(0, 3),
            };
            clone.SpriteDir = clone.Vel.x < 0f ? -1 : 1;
            clone.Rot = Mathf.Atan2(clone.Vel.y, clone.Vel.x) + Mathf.PI / 2f;
            pending.Add(clone);
        }

        /// <summary>Player.GetArmPosition for the swing's body frame (start / middle / end), px from the player's centre.</summary>
        Vector2 Arm(Crack c)
        {
            if (c.Kind == WhipKind.MoonLordWhip)
            {
                // useStyle 5 (Pl:36696-36723): one held pose by the aim's elevation, steeply up, level or steeply down.
                float e = Mathf.Atan2(c.Vel.y, Mathf.Abs(c.Vel.x));
                var held = e < -0.75f ? new Vector2(4f, -8f) : e > 0.6f ? new Vector2(4f, 10f) : new Vector2(6f, 6f);
                held.x *= c.SpriteDir;
                return pCenter + held;
            }
            float left = 1f - c.Ai0 / c.T;
            Vector2 v = left >= 0.666f ? new Vector2(-10f, -8f) : left >= 0.333f ? new Vector2(4f, -8f) : new Vector2(6f, 6f);
            v.x *= pDir;
            return pCenter + v;
        }

        /// <summary>
        /// FillWhipControlPoints: the chain at this update (frameOffset −1: the last one); a
        /// collision chain is uncurled (the random curl is only drawn).
        /// </summary>
        void Points(Crack c, List<Vector2> list, bool collision, int frameOffset)
        {
            list.Clear();
            int segments = c.S.Segments;
            float t = (c.Ai0 + frameOffset) / c.T;
            // P:49163: the Cob Whip's swing starts 30% out and stops at 95%.
            if (c.Kind == WhipKind.CobWhip) t = Mathf.Lerp(0.3f, 0.95f, Mathf.Clamp01(t));
            float k4 = 10f * Mathf.PI * (1f - 1.5f * t) * -c.SpriteDir / segments;
            float ext = 1.5f * t, flick = 0f;
            if (ext > 1f)
            {
                flick = (ext - 1f) / 0.5f;
                ext = Mathf.Lerp(1f, 0f, flick);
            }
            float baseAnim = Mathf.Round((c.ReachItem ?? c.Item).UseAnimation * 60f);
            float reach = baseAnim * 2f * t * (1f + Equipment.Current.WhipRange);
            float seg = c.Vel.magnitude * reach * ext * c.S.RangeMult / segments;
            var arm = Arm(c);
            Vector2 a = arm, b = arm, cc = arm;
            float angA = -Mathf.PI / 2f, angB = Mathf.PI / 2f + Mathf.PI / 2f * c.SpriteDir, angC = Mathf.PI / 2f;
            list.Add(arm);
            float curl = collision ? 1f : c.Ai1;
            float q = 1f - ext, e = 1f - q * q;
            float turn = c.Rot + 1.5f * Mathf.PI * flick * flick * c.SpriteDir;
            for (int i = 0; i < segments; i++)
            {
                float d = k4 * ((float)i / segments) * curl;
                var a2 = a + Dir(angA) * seg;
                var c2 = cc + Dir(angC) * (2f * seg);
                var b2 = b + Dir(angB) * (2f * seg);
                var v = Vector2.Lerp(c2, a2, e * 0.9f + 0.1f);
                var w = Vector2.Lerp(b2, v, e * 0.7f + 0.3f);
                var p = arm + Vector2.Scale(w - arm, new Vector2(1f, 1.5f));
                list.Add(arm + Rotate(p - arm, turn));
                angA += d;
                angC += d;
                angB += d;
                a = a2;
                cc = c2;
                b = b2;
            }
        }

        static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        static Vector2 Rotate(Vector2 v, float a)
        {
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
        }

        static readonly ContactFilter2D hitFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };

        /// <summary>
        /// Colliding: each point's box now and last update (18 px) against the enemies; then
        /// Damage_PVE_Inner for each new one in the player's sight (CanHitWithMeleeWeapon).
        /// </summary>
        void Collide(Crack c)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            var filter = hitFilter;
            filter.layerMask = MeleeHits.HitMask;
            for (int i = 0; i < points.Count; i++)
            {
                var now = points[i];
                var old = i < oldPoints.Count ? oldPoints[i] : now;
                var min = Vector2.Min(now, old) - new Vector2(9f, 9f);
                var max = Vector2.Max(now, old) + new Vector2(9f, 9f);
                overlaps.Clear();
                Physics2D.OverlapBox(TerraParticles.ToWorld((min + max) / 2f), (max - min) * Px, 0f, filter, overlaps);
                foreach (var col in overlaps)
                {
                    var hm = col.GetComponentInParent<HealthManager>();
                    if (hm == null && c.Damage > 0) PropHits.Pulse(col, TerraParticles.ToWorld(now), c.Vel.x < 0f ? -1f : 1f, c.Damage);
                    if (hm == null || hm.isDead) continue;
                    int id = hm.GetInstanceID();
                    if (c.Hit.Contains(id)) continue;
                    var at = TerraParticles.ToPx(col.bounds.center);
                    if (Vector2.Distance(at, now) > 1000f || !Clear(pCenter, at)) continue;
                    c.Hit.Add(id);
                    StrikeEnemy(c, col, hm);
                }
            }
        }

        static readonly RaycastHit2D[] rayHits = new RaycastHit2D[8];
        static readonly ContactFilter2D terrainFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << ChunkColliders.TerrainLayer };

        static bool Clear(Vector2 a, Vector2 b)
        {
            if (!TerraWorld.LineClearPx(a, b)) return false;
            int n = Physics2D.Linecast(TerraParticles.ToWorld(a), TerraParticles.ToWorld(b), terrainFilter, rayHits);
            for (int i = 0; i < n; i++)
            {
                var col = rayHits[i].collider;
                if (col.usedByEffector || col.GetComponentInParent<TerraOwned>() != null) continue;
                return false;
            }
            return true;
        }

        void StrikeEnemy(Crack c, Collider2D col, HealthManager hm)
        {
            // StatusNPC (ApplyWhipDebuffs) before the damage.
            if (c.Kind == WhipKind.Firecracker) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 240);
            if (c.Kind == WhipKind.CoolWhip) EnemyDebuffs.Add(hm, EnemyDebuffs.Frostbite, 240);
            if (c.Kind == WhipKind.Snapthorn && Random.Range(0, 5) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 180);
            if (c.Kind == WhipKind.DarkHarvest) EnemyDebuffs.Add(hm, EnemyDebuffs.ScytheMark, 240);
            if (c.Kind == WhipKind.EelWhip) EnemyDebuffs.Add(hm, EnemyDebuffs.EelMark, 240);
            if (c.Kind == WhipKind.SlimeWhip)
            {
                // P:11255: slimed; and set alight by the tip in the three updates round two thirds of the swing.
                EnemyDebuffs.Add(hm, EnemyDebuffs.Slimed, 180);
                int at = (int)(c.T / 3f) * 2;
                if (c.Ai0 >= at - 1 && c.Ai0 <= at + 1)
                {
                    var tipBox = new Rect(points[points.Count - 1] - new Vector2(25f, 25f), new Vector2(50f, 50f));
                    var b = col.bounds;
                    var tl = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
                    if (tipBox.Overlaps(new Rect(tl, new Vector2(b.size.x, b.size.y) / Px))) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 60 * Random.Range(3, 6));
                }
            }
            SummonTags.Apply(c.Kind, hm);
            float facing = c.Vel.x < 0f ? -1f : 1f;
            // 1.4: the flasks touch whip hits too (ProjectileID.Sets.IsAWhip).
            ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Summon, Whip = true };
            TerraMod.Instance.Melee.HitWith(col, c.Damage, c.Knockback, 0f, facing, TerraParticles.ToWorld(points[points.Count - 1]));
            DevStrikes++;
            // The on-hit specials.
            if (!hm.isDead)
            {
                if ((c.Kind == WhipKind.Firecracker || c.Kind == WhipKind.MeteorWhip || c.Kind == WhipKind.FlowerWhip) && !c.FirstHit) SummonTags.EnableProc(c.Kind, hm);
                if (c.Kind == WhipKind.Durendal || c.Kind == WhipKind.EelWhip || c.Kind == WhipKind.MoonLordWhip) SummonTags.EnableProc(c.Kind, hm);
                if (c.Kind == WhipKind.MoonLordWhip)
                {
                    var b = col.bounds;
                    var at = TerraParticles.ToPx(new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y)));
                    FxParticles.MoonLordWhipHit(at, Mathf.Atan2(c.Vel.y, c.Vel.x) + Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.1f);
                }
                // P:14002-14009: the first enemy struck this crack with the cosmic energy grown on it releases the stars.
                if (c.Kind == WhipKind.ConstellationWhip && !c.StarsDone && SummonTags.CanProc(c.Kind, hm))
                {
                    c.StarsDone = true;
                    ConstellationStars(c, hm, (int)(c.Damage * c.S.Falloff) * 2);
                    SummonTags.RemoveAllProcs(c.Kind);
                }
                if (c.Kind == WhipKind.CoolWhip) Minions.Instance?.CoolWhipStrike(hm);
                if (c.Kind == WhipKind.CobWhip)
                {
                    // P:13991: one little spider while the buff is off, and the buff on every hit.
                    if (!PlayerBuffs.Has(PlayerBuffs.CobWhip)) Minions.Instance?.SpawnCobSpider(TerraParticles.ToPx(col.bounds.center));
                    PlayerBuffs.Add(PlayerBuffs.CobWhip, 180);
                }
                Minions.Instance?.SetTarget(hm);
            }
            c.FirstHit = true;
            c.Damage = (int)(c.Damage * c.S.Falloff);
        }

        /// <summary>
        /// ConstellationWhip_SpawnConstellationStarsTowards (P:15014-15036): six stars from every
        /// third link of the drawn chain, flung sideways (some backwards), each 0.22 of the damage
        /// given, homing on the enemy (one inside a block starts at the player).
        /// </summary>
        void ConstellationStars(Crack c, HealthManager hm, int damage)
        {
            Points(c, visPoints, false, 0);
            int each = (int)(damage * 0.22f);
            for (int i = 2; i < visPoints.Count - 1; i += 3)
            {
                var pos = visPoints[i];
                var seg = visPoints[i + 1] - visPoints[i];
                var v = Rotate(seg, -Mathf.PI / 2f * pDir);
                v = (v.sqrMagnitude > 0f ? v.normalized : Vector2.zero) * (1f + Random.Range(-1f, 1f) * 3f);
                if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(pos))) pos = pCenter;
                TerraProjectiles.Instance?.ShootConstellationStar(pos, v, each, c.Knockback, hm, 0.02f + 0.09f * Random.value);
            }
        }

        /// <summary>The tip's swell over the crack: 0 → 1 at t = 0.7 → 0 at 0.9.</summary>
        static float Swell(Crack c)
        {
            float t = c.Ai0 / c.T;
            return Mathf.Clamp01(Mathf.InverseLerp(0.1f, 0.7f, t)) * Mathf.Clamp01(Mathf.InverseLerp(0.9f, 0.7f, t));
        }

        /// <summary>AI_165's per-update dust at the tip, by whip.</summary>
        void Dust(Crack c)
        {
            var p = TerraParticles.Instance;
            if (p == null || points.Count < 2) return;
            float k = Swell(c);
            var tip = points[points.Count - 1];
            var d = (tip - points[points.Count - 2]).normalized;
            int Box(int type, int size, float scale, int alpha = 0)
            {
                int i = p.NewDust(tip - new Vector2(size, size) / 2f, size, size, type, 0f, 0f, alpha, scale);
                return i;
            }
            switch (c.Kind)
            {
                case WhipKind.Snapthorn:
                    if (k > 0.1f && Random.value < k / 2f)
                    {
                        int i = Box(39, 30, 1.2f);
                        if (i >= 0) { p[i].NoGravity = Random.Range(0, 3) == 0; p[i].Velocity += d * 2f; }
                    }
                    break;
                case WhipKind.SpinalTap:
                    if (k > 0.15f && Random.value < k)
                    {
                        int i = Box(26, 30, 0.7f);
                        if (i >= 0)
                        {
                            if (Random.Range(0, 3) == 0) { p[i].NoGravity = true; p[i].Scale += 0.4f; }
                            p[i].Velocity += d * 2f;
                        }
                    }
                    break;
                case WhipKind.Firecracker:
                    if (k > 0.1f && Random.value < k)
                        for (int n = 0; n < 3; n++)
                        {
                            if (Random.Range(0, 3) != 0) continue;
                            if (Random.Range(0, 7) == 0)
                            {
                                int i = Box(31, 20, 1f);
                                if (i >= 0) { p[i].Velocity = p[i].Velocity / 2f + d * 2f; p[i].FadeIn = 1f + Random.value * 0.6f; p[i].NoGravity = true; }
                            }
                            else
                            {
                                int i = Box(6, 20, 1.2f);
                                if (i < 0) continue;
                                p[i].Velocity += d * 2f;
                                if (Random.Range(0, 3) != 0) { p[i].FadeIn = 0.7f + Random.value * 0.9f; p[i].Scale = 0.6f; p[i].NoGravity = true; }
                            }
                        }
                    break;
                case WhipKind.CoolWhip:
                    if (k > 0.1f && Random.value < k / 2f)
                    {
                        for (int n = 0; n < 3; n++)
                        {
                            int i = Box(16, 30, 1.2f);
                            if (i >= 0) { p[i].NoGravity = true; p[i].Velocity += d * 2f; }
                        }
                        int j = Box(13, 30, 0.8f);
                        if (j >= 0) p[j].Velocity += d * 2f;
                        for (int n = 0; n < 3; n++)
                        {
                            if (Random.Range(0, 2) != 0) continue;
                            int i = Box(261, 30, 0.8f);
                            if (i >= 0) { p[i].Velocity = (p[i].Velocity + d * 2f) * 0.3f; p[i].NoGravity = true; }
                        }
                    }
                    break;
                case WhipKind.Durendal:
                    if (k > 0.5f && Random.Range(0, 3) != 0)
                    {
                        int at = Mathf.Max(1, points.Count - 1 - Random.Range(0, 10));
                        var pt = points[at];
                        var prev = points[at - 1];
                        int i = p.NewDust(pt, 0, 0, Random.Range(0, 3) == 0 ? 43 : 57, 0f, 0f, 100, 1f);
                        if (i >= 0)
                        {
                            p[i].Position = pt;
                            p[i].FadeIn = 0.3f;
                            p[i].NoGravity = true;
                            p[i].Velocity = (p[i].Velocity * 0.5f + Rotate(pt - prev, c.SpriteDir * Mathf.PI / 2f)) * 0.5f;
                        }
                    }
                    break;
                case WhipKind.MorningStar:
                    if (k > 0.1f && Random.value < k / 2f)
                    {
                        int i = Box(172, 30, 1.5f, 100);
                        if (i >= 0) { p[i].NoGravity = true; p[i].Velocity /= 2f; }
                    }
                    break;
                case WhipKind.DarkHarvest:
                    // P:48699: from 40% of the swing, purple-black motes and black lightning off the tip.
                    if (c.Ai0 / c.T > 0.4f && Random.Range(0, 9) != 0)
                    {
                        int i = Box(191, 30, 1.3f);
                        if (i >= 0) { p[i].NoGravity = true; p[i].Velocity += d * 2f; }
                        if (Random.Range(0, 2) == 0) FxParticles.BlackLightningSmall(tip, d);
                        FxBatch.Instance?.Light(tip, new Color(0.2f, 0f, 0.4f), 2f);
                    }
                    break;
                case WhipKind.Kaleidoscope:
                    // P:48723: a rainbow mote (and its white twin) every update once the tip swells.
                    if (k > 0.1f)
                    {
                        int i = Box(267, 30, 1f);
                        if (i >= 0)
                        {
                            p[i].Color = FxParticles.HslToRgb(Frac(MiscCounter / 300f * 9f), 1f, 0.5f);
                            p[i].Velocity *= Random.value * 0.8f;
                            p[i].NoGravity = true;
                            p[i].Scale = 0.9f + 0.9f * Random.value;
                            p[i].FadeIn = 0.9f * Random.value;
                            p[i].Velocity += d * 2f;
                            var twin = p[i];
                            int j = p.NewDust(tip, 0, 0, 267);
                            if (j >= 0)
                            {
                                twin.Scale /= 2f;
                                twin.FadeIn *= 0.85f;
                                twin.Color = Color.white;
                                p[j] = twin;
                            }
                        }
                    }
                    break;
                case WhipKind.SlimeWhip:
                    // P:49114: slime off every third link of the drawn chain, the tip always.
                    if (k > 0.15f && Random.value < k && Random.Range(0, 2) == 0)
                    {
                        Points(c, visPoints, false, 0);
                        for (int n = visPoints.Count - 1; n >= 1; n -= 3)
                        {
                            if (n != visPoints.Count - 1 && Random.Range(0, 20) != 0) continue;
                            var dn = (visPoints[n] - visPoints[n - 1]).normalized;
                            int i = p.NewDust(visPoints[n] - new Vector2(15f, 15f), 30, 30, 4, 0f, 0f, 84, 1f + 0.3f * Random.value);
                            if (i < 0) continue;
                            p[i].Color = FxBatch.C(78, 136, 255, 127);
                            p[i].Velocity += dn * Random.value * 5f;
                            if (Random.Range(0, 4) == 0) p[i].NoGravity = true;
                            else
                            {
                                p[i].Velocity = p[i].Velocity * 0.3f + c.Vel * 0.5f;
                                p[i].Scale -= 0.4f;
                            }
                        }
                    }
                    break;
                case WhipKind.CobWhip:
                    if (k > 0.15f && Random.value < k && Random.Range(0, 2) == 0)
                    {
                        int i = Box(30, 30, 0.7f);
                        if (i >= 0)
                        {
                            if (Random.Range(0, 3) == 0) { p[i].NoGravity = true; p[i].Scale += 0.3f; }
                            p[i].Velocity += d * 2f;
                        }
                    }
                    break;
                default:
                    MoreDust(c, p, k, d);
                    break;
            }
            if (c.Kind == WhipKind.CoolWhip) FxBatch.Instance?.Light(tip, new Color(0.1f, 0.1f, 0.2f), 2f);
        }

        /// <summary>AI_165's dust for the 1.4.5 whips (P:48786-49113).</summary>
        void MoreDust(Crack c, TerraParticles p, float k, Vector2 d)
        {
            int count = points.Count;
            var tip = points[count - 1];
            int At(Vector2 centre, int size, int type, float scale, int alpha = 0) => p.NewDust(centre - new Vector2(size, size) / 2f, size, size, type, 0f, 0f, alpha, scale);
            switch (c.Kind)
            {
                case WhipKind.CorruptWhip:
                    // Corrupt gibs and Eater bits off the tip.
                    if (k > 0.6f && Random.Range(0, 2) == 0)
                        for (int n = 0; n < 2; n++)
                        {
                            int type = Random.Range(0, 2) == 0 ? 18 : 184;
                            int i = At(tip, 30, type, 0.7f);
                            if (i < 0) continue;
                            p[i].NoGravity = type == 184 || Random.Range(0, 3) == 0;
                            if (p[i].NoGravity) p[i].Scale += 0.3f;
                            p[i].Velocity += d * 2f;
                        }
                    break;
                case WhipKind.CrimsonWhip:
                    // Blood and crimson off the outer half of the chain (the tip always, four at once).
                    if (k > 0.6f && Random.Range(0, 4) != 0)
                        for (int n = count / 2; n < count; n++)
                        {
                            if (Random.Range(0, 6) != 0 && n != count - 1) continue;
                            var dn = (points[n] - points[n - 1]).normalized;
                            int reps = n == count - 1 ? 4 : 1;
                            for (int r = 0; r < reps; r++)
                            {
                                int i = At(points[n], 30, Random.Range(0, 2) == 0 ? 115 : 5, 0.3f + 0.3f * Random.value);
                                if (i < 0) continue;
                                p[i].Velocity += dn * 3f * Random.value;
                                p[i].Velocity *= 0.5f;
                                p[i].Velocity.x += pDir * 2f;
                                p[i].Velocity += pVel * Random.value;
                                p[i].FadeIn = p[i].Scale * 1.1f;
                                if (r > 0) p[i].Alpha = Random.Range(0, 200);
                            }
                        }
                    break;
                case WhipKind.MeteorWhip:
                    // Embers off the tip.
                    if (k > 0.1f && Random.value < k)
                        for (int n = 0; n < 4; n++)
                        {
                            if (Random.Range(0, 3) != 0) continue;
                            int i = At(tip, 20, 6, 1f);
                            if (i < 0) continue;
                            p[i].Velocity += d * 1.5f;
                            if (Random.Range(0, 3) != 0)
                            {
                                p[i].Velocity += d;
                                p[i].FadeIn = 0.7f + 0.9f * Random.value;
                                p[i].Scale = 0.7f;
                                p[i].NoGravity = true;
                            }
                        }
                    break;
                case WhipKind.FlowerWhip:
                    // Petals and grass along the chain, mostly near the tip.
                    if (k > 0.6f && Random.value < k && Random.Range(0, 3) != 0)
                        for (int l = 0; l < count - 1; l += 2)
                        {
                            if (Random.value >= (float)l / count * ((float)l / count)) continue;
                            var dl = (points[l + 1] - points[l]).normalized;
                            int[] types = { Random.Range(0, 2) == 0 ? 167 : 166, Random.Range(0, 2) == 0 ? 167 : 166, 166, 167 };
                            foreach (int type in types)
                            {
                                int i = At(points[l + 1], 30, type, 1f);
                                if (i < 0) continue;
                                p[i].NoGravity = Random.Range(0, 3) != 0;
                                if (p[i].NoGravity) p[i].Scale += 0.2f;
                                p[i].Velocity += dl * 2f;
                            }
                        }
                    break;
                case WhipKind.EelWhip:
                    // Sparks along the chain from the tenth link out, thicker as the swing goes on.
                    {
                        float t = c.Ai0 / c.T;
                        for (int n = 10; n < count - 1; n++)
                        {
                            if (Random.Range(0, 5) != 0 || Random.value > t) continue;
                            var dn = (points[n + 1] - points[n]).normalized;
                            if (Random.Range(0, 3) == 0)
                            {
                                float a = Random.value * Mathf.PI - Mathf.PI / 2f;
                                var s = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3, 8);
                                int i = p.NewDust(points[n + 1] - new Vector2(15f, 15f), 30, 30, 217, s.x * 2f, s.y * 2f, 100, 1.4f);
                                if (i < 0) continue;
                                p[i].NoGravity = true;
                                p[i].Velocity = p[i].Velocity / 4f + dn * 3f;
                            }
                            else if (Random.Range(0, 4) == 0)
                            {
                                int i = At(points[n + 1], 30, 226, 0.7f);
                                if (i < 0) continue;
                                if (Random.Range(0, 3) == 0)
                                {
                                    p[i].NoGravity = true;
                                    p[i].Scale += 0.3f;
                                }
                                p[i].Velocity += Rotate(dn, Random.Range(-Mathf.PI / 16f, Mathf.PI / 16f)) * 2f;
                            }
                        }
                    }
                    break;
                case WhipKind.ConstellationWhip:
                    // Ice-torch sparks off one of the last ten links, pushed sideways.
                    if (k > 0.5f && Random.Range(0, 3) != 0)
                    {
                        int n = Mathf.Max(1, Random.Range(count - 10, count));
                        int i = At(points[n], 30, 135, 1.5f, 100);
                        if (i >= 0)
                        {
                            p[i].Position = points[n];
                            p[i].FadeIn = 0.3f;
                            p[i].NoGravity = true;
                            p[i].Velocity = (p[i].Velocity * 0.5f + Rotate(points[n] - points[n - 1], pDir * Mathf.PI / 2f * Mathf.Sign(c.Ai1))) * 0.5f;
                        }
                    }
                    break;
                case WhipKind.MoonLordWhip:
                    // Teal light-disc sparks and vortex motes off one of the last eight links.
                    if (k > 0.5f && Random.Range(0, 3) != 0)
                    {
                        int n = Mathf.Max(1, Random.Range(count - 8, count));
                        int i = At(points[n], 20, 306, 1.6f, 100);
                        if (i < 0) break;
                        p[i].Position = points[n];
                        p[i].Color = FxBatch.C(147, 255, 220);
                        p[i].NoGravity = Random.Range(0, 2) == 0;
                        if (!p[i].NoGravity) p[i].Scale *= 0.5f;
                        p[i].Velocity = Rotate(c.Vel.normalized * (points[n] - points[n - 1]).magnitude * 0.5f, Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.1f);
                        int j = p.NewDust(points[n], 0, 0, 229);
                        if (j < 0) break;
                        p[j].Position = points[n];
                        p[j].NoGravity = true;
                        p[j].Scale = 0.3f + 1.1f * Random.value;
                        p[j].Velocity = p[i].Velocity * p[j].Scale * Random.value;
                    }
                    break;
            }
        }

        /// <summary>Player.miscCounter: 0..299, a tick each.</summary>
        static int MiscCounter => (int)(Time.time * 60f) % 300;

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>P:48610: the Slime Whip's flare at two thirds of the swing, at the drawn tip: fire and smoke.</summary>
        void SlimeSpark(Crack c)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            Points(c, visPoints, false, 0);
            var tip = visPoints[visPoints.Count - 1];
            for (int n = 0; n < 15; n++)
            {
                int i = p.NewDust(tip, 0, 0, 6);
                if (i < 0) continue;
                p[i].Position = tip;
                p[i].Velocity = Random.insideUnitCircle * 4f;
                p[i].Scale = 1f + Random.value;
                if (Random.Range(0, 15) == 0) p[i].Scale *= 0.4f;
                if (Random.Range(0, 2) == 0) p[i].NoGravity = true;
                else
                {
                    p[i].Velocity *= 0.3f;
                    p[i].Scale -= 0.5f;
                }
                p[i].Velocity += c.Vel * 0.7f;
                p[i].Position -= p[i].Velocity * 2f;
            }
            for (int n = 0; n < 6; n++)
            {
                int i = p.NewDust(tip, 0, 0, 31);
                if (i < 0) continue;
                p[i].Position = tip;
                p[i].Velocity = Random.insideUnitCircle * 4f;
                p[i].Scale = 0.3f + 0.3f * Random.value;
                if (Random.Range(0, 2) == 0) p[i].NoGravity = true;
                else p[i].Velocity = p[i].Velocity * 0.3f + c.Vel * 0.6f;
            }
        }

        // ------------------------------------------------------------------ drawing (DrawWhip)

        void Draw()
        {
            var fx = FxBatch.Instance;
            if (fx == null || live.Count == 0 || !Owner()) return;
            foreach (var c in live)
            {
                Points(c, drawPoints, false, 0);
                if (drawPoints.Count < 2) continue;
                // The line: a 2 px white column of FishingLine stretched between the points.
                var line = FxBatch.Frame("FishingLine", 0, 0, 12, 12, 6f, 2f);
                var lc = c.S.Line * L;
                lc.a = 1f;
                lc *= c.S.LineAlpha;
                if (c.S.LineUnlit) lc = c.S.Line;
                int lineEnd = c.S.NoLine ? 0 : drawPoints.Count - (c.S.LineShort ? 2 : 1);
                // The Moon Lord whip's clones are drawn in the ordinary projectile pass, behind the player.
                bool over = !c.Clone;
                for (int i = 0; i < lineEnd; i++)
                {
                    var dv = drawPoints[i + 1] - drawPoints[i];
                    fx.Draw(line, drawPoints[i], lc, Mathf.Atan2(dv.y, dv.x) - Mathf.PI / 2f, new Vector2(1f, (dv.magnitude + 2f) / 12f), overPlayer: over);
                }
                if (c.Kind == WhipKind.EelWhip || c.Kind == WhipKind.ConstellationWhip || c.Kind == WhipKind.MoonLordWhip) DrawSegments2(fx, c, over);
                else DrawSegments(fx, c);
            }
        }

        void DrawSegments(FxBatch fx, Crack c)
        {
            string tex = "Projectile_" + c.S.Id;
            var t = Terraria.TerrariaAssets.Texture(tex);
            int w = t.width, fh = t.height / c.S.Frames;
            int count = drawPoints.Count;
            float k = Swell(c);
            var lit = new Color(L, L, L, 1f);
            var color = c.Kind == WhipKind.Firecracker ? Color.white : c.Kind == WhipKind.CoolWhip ? Color.Lerp(lit, Color.white, 0.25f) : lit;
            bool flipKind = c.Kind == WhipKind.Leather || c.Kind == WhipKind.MorningStar;
            for (int i = 0; i < count - 1; i++)
            {
                int frame;
                float scale = 1f, xScale = 1f;
                bool tip = i == count - 2;
                switch (c.Kind)
                {
                    case WhipKind.Leather:
                        frame = tip ? 4 : i == 0 ? 0 : i <= 10 ? 1 : i <= 20 ? 2 : 3;
                        if (tip) scale = Mathf.Lerp(0.5f, 1.5f, k);
                        break;
                    case WhipKind.Snapthorn:
                        frame = tip ? 4 : i == 0 ? 0 : 1 + i % 3;
                        scale = i == 0 ? 1f : tip ? 1.1f : 0.8f;
                        break;
                    case WhipKind.SpinalTap:
                        frame = tip ? 4 : i == 0 ? 0 : 1 + i % 3;
                        break;
                    case WhipKind.MorningStar:
                        if (!tip && i != 0 && i % 2 == 1) continue;
                        frame = tip ? 4 : i == 0 ? 0 : i <= 10 ? 1 : i <= 20 ? 2 : 3;
                        if (tip) scale = Mathf.Lerp(0.5f, 2f, k);
                        break;
                    case WhipKind.DarkHarvest:
                        // DrawWhip_WhipScythe: even links only, flipped like the Leather Whip.
                        if (!tip && i != 0 && i % 2 == 1) continue;
                        frame = tip ? 4 : i == 0 ? 0 : i <= 10 ? 1 : i <= 20 ? 2 : 3;
                        if (tip) scale = Mathf.Lerp(0.5f, 1.5f, k);
                        break;
                    case WhipKind.Kaleidoscope:
                        if (!tip && i != 0 && i % 2 == 1) continue;
                        DrawRainbowLink(fx, i, tip, count);
                        continue;
                    case WhipKind.SlimeWhip:
                        if (!tip && i != 0 && i % 3 != 0) continue;
                        frame = tip ? 4 : i == 0 ? 0 : Mathf.RoundToInt(Mathf.Lerp(1f, 3f, (i - 1) / (float)(count - 3)));
                        break;
                    case WhipKind.CobWhip:
                        // DrawWhip_GenericWhip(6): every link but the second.
                        if (i == 1) continue;
                        frame = tip ? 5 : i == 0 ? 0 : 1 + i % 4;
                        break;
                    case WhipKind.CorruptWhip:
                    case WhipKind.CrimsonWhip:
                        // DrawWhip_GenericWhip(6, startWidth 0.5): the chain thickening toward the tip.
                        if (i == 1) continue;
                        frame = tip ? 5 : i == 0 ? 0 : 1 + i % 4;
                        if (i != 0 && !tip) xScale = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(5f, 15f, i));
                        break;
                    case WhipKind.MeteorWhip:
                        // DrawWhip_MeteorWhip: every third link, fullbright, no handle offset.
                        if (!tip && i != 0 && i % 3 != 0) continue;
                        frame = tip ? 5 : i == 0 ? 0 : Mathf.RoundToInt(Mathf.Lerp(1f, 4f, (i - 1) / 18f));
                        break;
                    case WhipKind.FlowerWhip:
                        // DrawWhip_FlowerWhip: even links, the flower head pushed 8 px on.
                        if (!tip && i != 0 && i % 2 == 1) continue;
                        frame = tip ? 4 : i == 0 ? 0 : 1 + i % 3;
                        if (i != 0 && !tip) xScale = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(5f, 15f, i));
                        break;
                    default:
                        // Firecracker, Cool Whip, Durendal: only the handle, every other link and the tip.
                        if (i == 0) frame = 0;
                        else if (tip) frame = 4;
                        else if (i == 3 || i == 5 || i == 7) frame = 1;
                        else if (i == 9 || i == 11 || i == 13) frame = 2;
                        else if (i == 15 || i == 17) frame = 3;
                        else continue;
                        break;
                }
                int drawnH = fh - 2;
                float oy = drawnH / 2f - (i == 0 && c.Kind != WhipKind.SlimeWhip && c.Kind != WhipKind.MeteorWhip ? 4f : 0f);
                if (tip && c.Kind == WhipKind.FlowerWhip) oy -= 8f;
                var spr = FxBatch.Frame(tex, 0, fh * frame, w, drawnH, w / 2f, oy);
                var dv = drawPoints[i + 1] - drawPoints[i];
                var col = c.Kind == WhipKind.SlimeWhip ? color * 0.5f : c.Kind == WhipKind.MeteorWhip ? Color.white : color;
                bool flip = (flipKind || c.Kind == WhipKind.DarkHarvest) && c.SpriteDir == 1;
                fx.Draw(spr, drawPoints[i], col, Mathf.Atan2(dv.y, dv.x) - Mathf.PI / 2f, new Vector2(scale * xScale, scale), flipX: flip, overPlayer: true);
            }
        }

        /// <summary>
        /// DrawWhip_EelWhip (M:37203-37260), DrawWhip_Constellation (M:37030-37102) and
        /// DrawWhip_MoonLordWhip (M:37104-37154): the eel (handle at the second point sliding back at
        /// mid-swing, a widening body, the glowmask added, flipped right); the constellation (half-size
        /// added links and a blue star flash at every third, peaking at mid-swing); the Moon Lord
        /// whip (column 0 of its sheet stretched to 1.25, one of three tips).
        /// </summary>
        void DrawSegments2(FxBatch fx, Crack c, bool over)
        {
            int count = drawPoints.Count;
            float t = c.Ai0 / c.T;
            var lit = new Color(L, L, L, 1f);
            for (int i = 0; i < count - 1; i++)
            {
                bool tip = i == count - 2;
                var dv = drawPoints[i + 1] - drawPoints[i];
                float ang = Mathf.Atan2(dv.y, dv.x), rot = ang - Mathf.PI / 2f;
                var at = drawPoints[i];
                switch (c.Kind)
                {
                    case WhipKind.EelWhip:
                    {
                        if (i == 0) continue;
                        int frame = i == 1 ? 0 : tip ? 4 : i < 25 ? 1 : i < 35 ? 2 : 3;
                        if (i == 1) at += Rotate(new Vector2(-6f * (1f - Mathf.Abs(2f * t - 1f)), 0f), ang);
                        float xs = Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(2f, count - 1 - 5, i));
                        bool flip = c.SpriteDir == 1;
                        fx.Draw(FxBatch.Frame("Projectile_1033", 0, 24 * frame, 22, 22, 11f, 11f), at, lit, rot, new Vector2(xs, 1f), flipX: flip, overPlayer: over);
                        fx.Draw(FxBatch.Frame("Glow_358", 0, 24 * frame, 22, 22, 11f, 11f), at, new Color(1f, 1f, 1f, 0f), rot, new Vector2(xs, 1f), flipX: flip, overPlayer: over);
                        break;
                    }
                    case WhipKind.ConstellationWhip:
                    {
                        if (i == 1) continue;
                        float fat = 1f;
                        int frame;
                        Color col;
                        float s = 1f;
                        if (i == 0)
                        {
                            frame = 0;
                            fat = 0f;
                            col = lit;
                        }
                        else if (tip)
                        {
                            frame = 5;
                            fat = 2f;
                            col = lit;
                        }
                        else
                        {
                            frame = 1 + i % 4;
                            col = new Color(1f, 1f, 1f, 50f / 255f);
                            s = 0.5f;
                        }
                        fat *= 1f + 0.03f * i;
                        fx.Draw(FxBatch.Frame("Projectile_1034", 0, 36 * frame, 22, 34, 11f, 17f - (i == 0 ? 4f : 0f)), at, col, rot, s, overPlayer: over);
                        if ((i + 1) % 3 == 0)
                            FxParticles.DrawStarSparkle(fx, 1f, at, new Color(1f, 1f, 1f, 0f) * 0.5f, FxParticles.HslToRgb(0.6f, 1f, 0.5f), t, 0f, 0.5f, 0.5f, 1f,
                                Mathf.PI / 2f, new Vector2(2f * (1f - t), 4f * (1f - t)), new Vector2(fat, fat), over);
                        break;
                    }
                    case WhipKind.MoonLordWhip:
                    {
                        int row = i == 0 ? 0 : tip ? 3 + c.TipVariant : 1 + i % 3;
                        fx.Draw(FxBatch.Frame("Projectile_1035", 0, 30 * row, 18, 28, 9f, 14f - (i == 0 ? 4f : 0f)), at, lit, rot, new Vector2(1f, 1.25f), overPlayer: over);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// DrawWhip_RainbowWhip: each link half lit, half its rainbow hue (running along the chain
        /// and cycling with miscCounter), more solid towards the tip; a faint added glow a size
        /// bigger; the tip a third glow bigger again.
        /// </summary>
        void DrawRainbowLink(FxBatch fx, int i, bool tip, int count)
        {
            const string tex = "Projectile_915";
            int frame = tip ? 4 : i == 0 ? 0 : 1 + i % 3;
            var spr = FxBatch.Frame(tex, 0, 32 * frame, 22, 30, 11f, 15f - (i == 0 ? 4f : 0f));
            var dv = drawPoints[i + 1] - drawPoints[i];
            float rot = Mathf.Atan2(dv.y, dv.x) - Mathf.PI / 2f;
            float h = MiscCounter / 300f;
            var hue = FxParticles.HslToRgb(Frac(h * 5f + i * 0.05f), 1f, 0.5f);
            float w = Mathf.Clamp01(Mathf.InverseLerp(count - 10, count - 2, i));
            var c2 = new Color(hue.r, hue.g, hue.b, 127f / 255f * w);
            fx.Draw(spr, drawPoints[i], Color.Lerp(new Color(L, L, L, 1f), c2, 0.5f), rot, 1f, overPlayer: true);
            fx.Draw(spr, drawPoints[i], new Color(hue.r, hue.g, hue.b, 0f) * 0.1f, rot, 1.2f, overPlayer: true);
            if (tip) fx.Draw(spr, drawPoints[i], new Color(hue.r, hue.g, hue.b, 127f / 255f) * 0.3f, rot, 1.4f, overPlayer: true);
        }

        public void Clear()
        {
            live.Clear();
            pending.Clear();
            echoIn = 0;
        }
    }
}
