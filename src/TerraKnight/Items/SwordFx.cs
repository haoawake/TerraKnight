using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>What a sword does beyond its plain swing, by Terraria's own projectiles.</summary>
    internal enum MeleeFx : byte
    {
        None,
        /// <summary>Projectile 972: the purple overhead slash, plus a spinning one thrown at the cursor.</summary>
        NightsEdge,
        /// <summary>Projectile 982: the golden slash.</summary>
        Excalibur,
        /// <summary>Projectiles 984 (the slash) and 985 (the crescent it throws).</summary>
        TerraBlade,
        /// <summary>Projectile 974: a cut that appears ahead of the player, or on an enemy near there.</summary>
        LightsBane,
        /// <summary>Projectile 976: a leaf curling from the player's feet towards the front.</summary>
        BladeOfGrass,
        /// <summary>Projectile 977: a blue cut through the first enemy hit each swing.</summary>
        Muramasa,
        /// <summary>Projectile 978: a fireball bursting on the first enemy hit each swing.</summary>
        Volcano,
        /// <summary>Projectile 938: the shortsword's stab towards the cursor (useStyle 13).</summary>
        Shortsword,
        /// <summary>True Night's Edge: the Night's Edge slash (972) and a spinning crescent flung at the cursor (973, AI_191).</summary>
        TrueNightsEdge,
        /// <summary>True Excalibur: its pink slash (983) over Excalibur's golden one (982, no damage).</summary>
        TrueExcalibur,
        /// <summary>The Horseman's Blade: its fiery slash (997); each enemy it hits calls a flaming jack-o'-lantern (321).</summary>
        HorsemansBlade,
        /// <summary>The Chlorophyte Claymore: its green slash (1126) shedding spores (1128); every few swings a slam that bursts spores out of the ground.</summary>
        ChlorophyteClaymore,
    }

    /// <summary>
    /// Terraria 1.4.5's sword projectiles and swing effects, worked on Terraria's 60 Hz tick in its
    /// own space (pixels, y down) and drawn layer by layer as Main.DrawProj does, through
    /// <see cref="FxBatch"/>: Night's Edge, Excalibur and the Terra Blade sweep a crescent over
    /// the player's head (AI_190) and the Terra Blade throws one too (AI_191); Light's Bane cuts
    /// ahead (AI_188), the Blade of Grass sends a leaf (AI_152), Muramasa and the Volcano answer
    /// their first hit with a cut and a fireball; hits flash (ParticleOrchestrator's bursts),
    /// swords shed their dust, and fire and poison keep hurting (On Fire!, Poisoned).
    /// </summary>
    internal sealed partial class SwordFx : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        const float Pi = Mathf.PI;

        public static SwordFx Instance { get; private set; }

        enum Kind { Swing, TerraShot, LightsBaneCut, Leaf, MuramasaCut, VolcanoBlast, Stab, TrueNightsShot, CloseArt }

        sealed class Proj
        {
            public Kind Kind;
            public int Type;             // Terraria projectile id, for the per-type branches
            public Vector2 Center, Velocity; // Terraria px, px per update
            public float Ai0, Ai1, Ai2 = 1f, LocalAi0, LocalAi1, LocalAi2;
            public float Rotation, Scale = 1f, Opacity = 1f;
            public int Damage, Penetrate = -1, Frame, FrameCounter, TimeLeft = 600;
            public float Knockback, Crit;
            public bool Dead;
            public readonly HashSet<int> Hit = new HashSet<int>();
            public readonly List<Vector2> OldPos = new List<Vector2>();
            public readonly List<float> OldRot = new List<float>();
            /// <summary>usesLocalNPCImmunity's cooldown (ticks): an enemy can be hit again once it runs out; 0 = once per enemy.</summary>
            public int Cooldown;
            public readonly Dictionary<int, int> Recent = new Dictionary<int, int>();
        }

        sealed class Flash
        {
            public Vector2 Pos, Velocity, Scale;
            public float Rotation, Additive;
            public Color Tint;
            public int Age;
            /// <summary>PrettySparkleParticle.TimeToLive: 30 ticks, the True Excalibur's pink pair 36.</summary>
            public float Ttl = 30f;
        }

        readonly List<Proj> live = new List<Proj>();
        readonly List<Flash> flashes = new List<Flash>();
        /// <summary>Player.chlorophyteBladeCounter: a tick at a time down to 0 (Pl:43159-43162).</summary>
        public static int ClaymoreCounter;
        /// <summary>Dev: the Claymore's slams so far, and its ground bursts.</summary>
        public static int DevSlams, DevSporeBursts;
        /// <summary>Dev: stabs made, by projectile type.</summary>
        public static readonly Dictionary<int, int> DevStabs = new Dictionary<int, int>();
        /// <summary>Dev: the Flint's cuts so far.</summary>
        public static int DevFlintSlashes;
        readonly List<Collider2D> overlaps = new List<Collider2D>();
        float tickAccumulator;

        public static SwordFx Create(Transform parent)
        {
            var go = new GameObject("Terra Sword Fx");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<SwordFx>();
            return Instance;
        }

        // ------------------------------------------------------------------ spawning

        static Vector2 PlayerCenterPx(HeroController hero) => TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);

        /// <summary>Player.ItemCheck_Shoot for a sword, once at the start of each use.</summary>
        /// <param name="artSlash">The remade swing (BladeArts) is the sword's slash: only its other shots (the Terra Blade's beam, Night's Edge's slash at the cursor, the True Night's Edge's blade).</param>
        public void OnUse(ItemDef item, HeroController hero, float direction, Vector2 aimWorld, bool artSlash = false)
        {
            var p = PlayerCenterPx(hero);
            var aimPx = TerraParticles.ToPx(aimWorld) - p;
            var aim = aimPx.sqrMagnitude > 1e-4f ? aimPx.normalized : new Vector2(direction, 0f);
            int damage = Equipment.Damage(item);
            float crit = Equipment.Crit(item);
            float anim = Mathf.Max(1f, Mathf.Round(item.UseAnimation * 60f * Equipment.UseScale(item)));
            switch (item.Fx)
            {
                case MeleeFx.NightsEdge:
                    if (!artSlash) Add(Kind.Swing, 972, p, new Vector2(direction, 0f), damage, Equipment.Knockback(item), crit, direction, anim, 2);
                    Add(Kind.Swing, 972, p, aim * 5f, damage, Equipment.Knockback(item), crit, direction * 0.1f, 30f, 2);
                    break;
                case MeleeFx.Excalibur:
                    if (!artSlash) Add(Kind.Swing, 982, p, new Vector2(direction, 0f), damage, Equipment.Knockback(item), crit, direction, anim, 3);
                    break;
                case MeleeFx.TerraBlade:
                    if (!artSlash) Add(Kind.Swing, 984, p, new Vector2(direction, 0f), damage, Equipment.Knockback(item), crit, direction, anim, 3);
                    var shot = Add(Kind.TerraShot, 985, p, aim * 12f * 5f, damage, Equipment.Knockback(item), crit, direction, 18f, -1);
                    shot.Opacity = 0f;
                    break;
                case MeleeFx.LightsBane:
                    SpawnLightsBaneCut(hero, p, direction, damage, Equipment.Knockback(item), crit);
                    break;
                case MeleeFx.BladeOfGrass:
                    SpawnLeaf(hero, p, direction, damage, Equipment.Knockback(item), crit);
                    break;
                case MeleeFx.TrueNightsEdge:
                {
                    // Player.ItemCheck_Shoot (Pl:49422): the Night's Edge slash, and 973 at the cursor for half the damage.
                    if (!artSlash) Add(Kind.Swing, 972, p, new Vector2(direction, 0f), damage, Equipment.Knockback(item), crit, direction, anim, 2);
                    var blade = Add(Kind.TrueNightsShot, 973, p, aim * 14f * Equipment.UseScale(item), Mathf.Max(1, damage / 2), Equipment.Knockback(item), crit, direction, 32f, 3);
                    blade.Rotation = 0f;
                    blade.Opacity = 0f;
                    blade.Cooldown = 10;
                    break;
                }
                case MeleeFx.TrueExcalibur:
                    // Pl:49430: its own slash, and Excalibur's (982) beneath it for nothing.
                    if (artSlash) break;
                    Add(Kind.Swing, 983, p, new Vector2(direction, 0f), damage, Equipment.Knockback(item), crit, direction, anim, 6);
                    Add(Kind.Swing, 982, p, new Vector2(direction, 0f), 0, Equipment.Knockback(item), crit, direction, anim, 0);
                    break;
                case MeleeFx.HorsemansBlade:
                    // Pl:49398.
                    if (!artSlash) Add(Kind.Swing, 997, p, new Vector2(direction, 0f), damage, Equipment.Knockback(item), crit, direction, anim, 3);
                    break;
                case MeleeFx.ChlorophyteClaymore:
                {
                    // Pl:49405-49420: with chlorophyteBladeCounter run out, a slam (velocity (dir, 0)) half as hard again,
                    // a quarter more knockback, and the counter set three uses on; otherwise the plain slash (dir, 1).
                    if (artSlash) break;
                    bool slam = ClaymoreCounter <= 0;
                    float kb = Equipment.Knockback(item) * (slam ? 1.25f : 1f);
                    var swing = Add(Kind.Swing, 1126, p, new Vector2(direction, slam ? 0f : 1f), slam ? (int)(damage * 1.5f) : damage, kb, crit, direction, anim, -1);
                    swing.Rotation = Mathf.Atan2(0f, direction);
                    if (slam) ClaymoreCounter += (int)anim * 3;
                    break;
                }
                case MeleeFx.Shortsword:
                {
                    // The remade stabs' techniques (SwordFxClose), with the remade swings on.
                    if (CloseStabUse(item, p, aim, damage, crit)) break;
                    // The rapiers stab every useTime of the use (ItemCheck_Shoot each time itemTime runs out), each aimed anew.
                    int type = StabType(item.Type);
                    Stab(item, p, aim, damage, crit, type);
                    int useTicks = Mathf.Max(1, Mathf.RoundToInt(item.UseTime * 60f * Equipment.UseScale(item)));
                    for (int k = 1; k * useTicks < (int)anim; k++) pendingStabs.Add((item, k * useTicks));
                    break;
                }
            }
        }

        /// <summary>The projectile each shortsword stabs with (Item.shoot: 938-945, the Gladius's 802, the Ruler's 842, the Chlorophyte Saber's 1127).</summary>
        static int StabType(ItemType t)
        {
            switch (t)
            {
                case ItemType.TinShortsword: return 939;
                case ItemType.IronShortsword: return 940;
                case ItemType.LeadShortsword: return 941;
                case ItemType.SilverShortsword: return 942;
                case ItemType.TungstenShortsword: return 943;
                case ItemType.GoldShortsword: return 944;
                case ItemType.PlatinumShortsword: return 945;
                case ItemType.Gladius: return 802;
                case ItemType.Ruler: return 842;
                case ItemType.ChlorophyteSaber: return 1127;
                default: return 938;
            }
        }

        /// <summary>Stabs still to come in the current use: the item and the ticks until each.</summary>
        readonly List<(ItemDef Item, int Ticks)> pendingStabs = new List<(ItemDef, int)>();

        /// <summary>
        /// A shortsword's stab (Pl:47462-47466, 47743-47756, 49346-49350): shootSpeed (2.1, the rapiers
        /// 2.4) faster with melee speed; the rapiers' scattered up to a quarter turn about the aim and
        /// up to 0.7 px faster or slower (the Chlorophyte Saber 0.65 of that turn, less with melee
        /// speed, and 0.5 px, for 70% of the damage), carrying the melee speed in ai[1].
        /// </summary>
        void Stab(ItemDef item, Vector2 playerPx, Vector2 aim, int damage, float crit, int type)
        {
            float scale = Mathf.Max(0.1f, Equipment.UseScale(item));
            bool rapier = type == 802 || type == 842 || type == 1127;
            float speed = (rapier ? 2.4f : 2.1f) / scale;
            var v = aim * speed;
            if (rapier)
            {
                float turn = Pi / 4f, spread = 0.7f;
                if (type == 1127)
                {
                    spread = 0.5f;
                    turn = 0.65f * Remap(scale, 1f, 1f / 3f, 1f, 0.25f);
                }
                v = Rotate(aim, turn * (Random.value - 0.5f)) * (speed - Random.Range(-1f, 1f) * spread);
            }
            if (type == 1127) damage = (int)(damage * 0.7f);
            DevStabs[type] = (DevStabs.TryGetValue(type, out int made) ? made : 0) + 1;
            var stab = Add(Kind.Stab, type, playerPx, v, damage, Equipment.Knockback(item), crit, 0f, scale, -1);
            stab.Opacity = 0f;
        }

        /// <summary>The rapiers' later stabs, while the same item's use goes on, aimed where the cursor is then.</summary>
        void StabsDue(HeroController hero)
        {
            var mod = TerraMod.Instance;
            for (int i = pendingStabs.Count - 1; i >= 0; i--)
            {
                var (item, ticks) = pendingStabs[i];
                if (mod == null || mod.SwingItem != item)
                {
                    pendingStabs.RemoveAt(i);
                    continue;
                }
                if (--ticks > 0)
                {
                    pendingStabs[i] = (item, ticks);
                    continue;
                }
                pendingStabs.RemoveAt(i);
                var p = PlayerCenterPx(hero);
                var aimPx = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint) - p;
                float dir = aimPx.x < 0f ? -1f : 1f;
                Stab(item, p, aimPx.sqrMagnitude > 1e-4f ? aimPx.normalized : new Vector2(dir, 0f), Equipment.Damage(item), Equipment.Crit(item), StabType(item.Type));
            }
        }

        /// <summary>The stab in flight, for the player's pose: its direction (Terraria px), or zero.</summary>
        public Vector2 StabDirection
        {
            get
            {
                foreach (var p in live) if (p.Kind == Kind.Stab && !p.Dead) return p.Velocity;
                return CloseStabDirection();
            }
        }

        Proj Add(Kind kind, int type, Vector2 center, Vector2 velocity, int damage, float kb, float crit, float ai0, float ai1, int penetrate)
        {
            var proj = new Proj
            {
                Kind = kind, Type = type, Center = center, Velocity = velocity, Damage = damage, Knockback = kb, Crit = crit,
                Ai0 = ai0, Ai1 = ai1, Penetrate = penetrate, Rotation = Mathf.Atan2(velocity.y, velocity.x),
            };
            live.Add(proj);
            return proj;
        }

        /// <summary>The Flint's cut (AI_198, P:53170): an AI_190 slash at the player for 6 ticks, ai[0] the way it faces, two enemies at most.</summary>
        public void FlintSlash(Vector2 centrePx, float dir, int damage, float kb, float crit)
        {
            Add(Kind.Swing, 1043, centrePx, new Vector2(dir, 0f), damage, kb, crit, dir, 6f, 2);
            DevFlintSlashes++;
        }

        /// <summary>
        /// Main.DrawProj_DeadCellsFlintSlash (M:28700-28727): the Excalibur's layers in the Flint's
        /// warm colours (227,177,140 behind, 255,230,120 on top, white in the middle) and its glint,
        /// an orange star at the blade's end.
        /// </summary>
        void DrawFlintSlash(FxBatch fx, Proj p, float t, float n, float S, float rot, float d, bool flip, Vector2 pos)
        {
            var whole = FxBatch.Whole("Projectile_1043");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 4;
            var r0 = FxBatch.Frame("Projectile_1043", 0, 0, w, fh, w / 2f, fh / 2f);
            var r3 = FxBatch.Frame("Projectile_1043", 0, 3 * fh, w, fh, w / 2f, fh / 2f);
            float lf = Remap(L, 0.2f, 1f, 0f, 1f);
            fx.Draw(r0, pos, FxBatch.C(227, 177, 140) * (lf * n), rot + d * (Pi / 4f) * -1f * (1f - t), S * 0.975f, flip);
            var c4 = Color.white * (n * 0.5f);
            c4.a *= 1f - lf;
            fx.Draw(r0, pos, c4 * (lf * 0.5f) * 0.15f, rot + d * 0.01f, S, flip);
            fx.Draw(r0, pos, Color.white * (lf * n * 0.3f), rot, S * 0.8f, flip);
            fx.Draw(r0, pos, FxBatch.C(255, 230, 120) * (lf * n * 0.7f), rot, S * 0.975f, flip);
            fx.Draw(r3, pos, Color.white * (0.3f * n * (1f - lf * 0.7f)), rot + d * 0.01f, S, flip);
            float a = rot + Remap(t, 0f, 1f, 0f, Pi / 2f) * d;
            fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (w * 0.5f - 4f) * S, new Color(1f, 1f, 1f, 0f) * (n * 0.5f), FxBatch.C(255, 150, 0), t,
                0f, 0.5f, 0.5f, 1f, Pi / 4f, new Vector2(2f, 2f), Vector2.one);
        }

        /// <summary>Light's Bane: a cut 70 px ahead and 40 px up, or on an enemy within 50 px of there (Pl:49351).</summary>
        void SpawnLightsBaneCut(HeroController hero, Vector2 p, float dir, int damage, float kb, float crit)
        {
            var v = new Vector2(dir, 4f).normalized;
            v = Rotate(v, 2f * Pi * Dir() * 0.05f);
            var at = p + new Vector2(70f * dir, -40f) + v * -10f;
            var target = NearestEnemy(at, 50f);
            if (target.HasValue) at = RandomIn(target.Value);
            else at += Random.insideUnitCircle * 20f;
            bool isCrit = Random.value * 100f < crit;
            var proj = Add(Kind.LightsBaneCut, 974, at, v * 0.001f, Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f)) * (isCrit ? 2 : 1), kb, 0f, isCrit ? 2f : 1f, 0f, 2);
            proj.TimeLeft = 36;
        }

        /// <summary>Blade of Grass: a leaf from the player's feet curling to a point ahead (or an enemy near it) (Pl:49446).</summary>
        void SpawnLeaf(HeroController hero, Vector2 p, float dir, int damage, float kb, float crit)
        {
            var target = p + new Vector2(70f * dir, -40f);
            var enemy = NearestEnemy(target, 150f);
            bool hasTarget = enemy.HasValue;
            target = hasTarget ? RandomIn(enemy.Value) : target + Random.insideUnitCircle * 20f;
            var start = p + new Vector2(Dir() * 10f, 21f);
            var d = target - start;
            float turn = (Pi + 2f * Pi * Random.value * 1.5f) * -dir;
            float w = turn / 60f;
            if (Mathf.Abs(w) >= 0.17f) w *= 0.7f;
            var step = new Vector2(16f, 0f);
            var acc = step;
            int n = 0;
            while (acc.magnitude < d.magnitude && n < 60)
            {
                n++;
                acc += step;
                step = Rotate(step, w);
            }
            var vel = Rotate(d.normalized, -Mathf.Atan2(acc.y, acc.x) - w) * 16f;
            if (n == 60) vel = new Vector2(dir, 0f) * 16f;
            if (!hasTarget)
            {
                start.y -= 24f;
                vel = Rotate(vel, dir * 2f * Pi * 0.14f);
            }
            var proj = Add(Kind.Leaf, 976, start, vel, Mathf.Max(1, Mathf.RoundToInt(damage * 0.25f)), kb, crit, w, n, 2);
            proj.TimeLeft = 60;
            proj.Scale = 1f + Random.Range(0, 30) * 0.01f;
            for (int i = 0; i < 10; i++)
                Dust(proj.Center, 40, Rotate(vel.normalized, Random.value * Pi / 2f) * Random.value * 3f, 0, 0.7f, false);
        }

        /// <summary>A sword's first enemy hit in a swing: Muramasa's cut, the Volcano's fireball (Pl:44874, 44496).</summary>
        public void OnFirstHit(ItemDef item, Collider2D target, int damage, float direction)
        {
            var b = target.bounds;
            var center = TerraParticles.ToPx(b.center);
            if (item.Fx == MeleeFx.Muramasa)
            {
                var box = new Rect(TerraParticles.ToPx(new Vector2(b.min.x, b.max.y)) - new Vector2(30f, 16f + 8f), new Vector2(b.size.x / Px + 60f, b.size.y / Px + 32f));
                var c = box.center;
                var from = new Vector2(box.x + Random.value * box.width, box.y + Random.value * box.height);
                var v = (c - from).sqrMagnitude > 1e-3f ? (c - from).normalized : new Vector2(direction, 1f).normalized;
                v = Rotate(v * 8f, Pi / 4f);
                float turn = (Random.value < 0.5f ? -1f : 1f) * (Pi / 5f + 4f * Pi / 5f * Random.value) * 0.5f;
                var p = c;
                for (int k = 0; k < 15; k++)
                {
                    p -= v;
                    v = Rotate(v, -turn / 30f);
                }
                var proj = Add(Kind.MuramasaCut, 977, p, v, Mathf.Max(1, Mathf.RoundToInt(damage * 0.5f)), 0f, 0f, turn, 0f, 1);
                proj.TimeLeft = 30;
                proj.Scale = 1f + Random.Range(0, 30) * 0.01f;
                Sound("Item_1");
            }
            else if (item.Fx == MeleeFx.Volcano)
            {
                var proj = Add(Kind.VolcanoBlast, 978, center, new Vector2(0f, -1f), Mathf.Max(1, Mathf.RoundToInt(damage * 0.75f)), Equipment.Knockback(item), Equipment.Crit(item), 0f, 2f, 3);
                proj.TimeLeft = 30;
                proj.Opacity = 0f;
                Sound("Custom/dd2_explosive_trap_explode_" + Random.Range(0, 3));
            }
        }

        // ------------------------------------------------------------------ update

        void Update()
        {
            var gm = GameManager.UnsafeInstance;
            if (gm != null && gm.isPaused) { DrawAll(); return; }
            tickAccumulator += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            var hero = HeroController.SilentInstance;
            while (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                if (hero != null)
                    for (int i = 0; i < live.Count; i++)
                        if (!live[i].Dead) Tick(live[i], hero);
                live.RemoveAll(q => q.Dead);
                for (int i = flashes.Count - 1; i >= 0; i--)
                {
                    var f = flashes[i];
                    f.Pos += f.Velocity;
                    if (++f.Age >= f.Ttl) flashes.RemoveAt(i);
                }
                EnemyDebuffs.Tick();
                TickArts(hero);
                SwingDust(hero);
                if (ClaymoreCounter > 0) ClaymoreCounter--;
                if (hero != null && pendingStabs.Count > 0) StabsDue(hero);
            }
            DrawAll();
        }

        void Tick(Proj p, HeroController hero)
        {
            switch (p.Kind)
            {
                case Kind.Swing: TickSwing(p, hero); break;
                case Kind.TerraShot: TickTerraShot(p, hero); break;
                case Kind.LightsBaneCut: TickLightsBane(p); break;
                case Kind.Leaf: TickLeaf(p); break;
                case Kind.MuramasaCut:
                    // extraUpdates 2: three updates a tick.
                    for (int u = 0; u < 3 && !p.Dead; u++) TickMuramasa(p);
                    break;
                case Kind.VolcanoBlast: TickVolcano(p); break;
                case Kind.Stab:
                    // extraUpdates 1: two updates a tick.
                    for (int u = 0; u < 2 && !p.Dead; u++) TickStab(p, hero);
                    break;
                case Kind.TrueNightsShot: TickTrueNights(p, hero); break;
                case Kind.CloseArt: TickClose(p, hero); break;
            }
            if (p.Recent.Count > 0)
            {
                recentKeys.Clear();
                recentKeys.AddRange(p.Recent.Keys);
                foreach (int k in recentKeys) if (--p.Recent[k] <= 0) p.Recent.Remove(k);
            }
        }

        static readonly List<int> recentKeys = new List<int>();

        /// <summary>
        /// AI_161: the blade slides out along the aim from the player's centre, fading in and out over
        /// 16 updates; the Chlorophyte Saber's timing goes by its melee speed (ai[1]): it reaches out
        /// 10 px further, lights green, sheds spores (157) and swishes every 12 updates.
        /// </summary>
        void TickStab(Proj p, HeroController hero)
        {
            p.Rotation = Mathf.Atan2(p.Velocity.y, p.Velocity.x) + Pi / 2f;
            p.Ai0++;
            int n = 16, n2 = 12, n3 = 7;
            float at = p.Ai0;
            bool saber = p.Type == 1127;
            if (saber)
            {
                n = (int)(16 * p.Ai1) + 5;
                at = Mathf.Min(at, (int)(16 * p.Ai1));
                n3 = (int)(7 * p.Ai1);
                n2 = (int)(12 * p.Ai1);
            }
            p.Opacity = Mathf.Clamp01(Mathf.InverseLerp(0f, n3, p.Ai0)) * Mathf.Clamp01(Mathf.InverseLerp(n, n2, p.Ai0));
            p.Center = PlayerCenterPx(hero) + p.Velocity * (at - 1f);
            if (saber)
            {
                float f = p.Rotation - Pi / 2f;
                p.Center += new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 10f;
            }
            if (p.Ai0 >= n)
            {
                p.Dead = true;
                return;
            }
            if (saber)
            {
                Fx.Light(p.Center, FxBatch.C(32, 108, 0));
                float f = p.Rotation - Pi / 2f;
                var dp = p.Center - new Vector2(9f, 9f) + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * Remap(p.Ai1, 1f, 1f / 3f, 10f, Remap(p.Ai0, 0f, n, 30f, 90f)) + p.Velocity * Mathf.Max(0f, p.Ai0 - at);
                int i = TerraParticles.Instance != null ? TerraParticles.Instance.NewDust(dp, 18, 18, 157) : -1;
                if (i >= 0)
                {
                    ref var d = ref TerraParticles.Instance[i];
                    d.NoGravity = true;
                    d.Velocity = p.Velocity * Random.value;
                    d.Scale = 1f + Random.value * 0.5f * Remap(p.Ai1, 1f, 1f / 3f, 0.75f, 0.5f);
                }
                if (--p.FrameCounter <= 0)
                {
                    Sound("Item_1");
                    p.FrameCounter = 12;
                }
            }
            // Its own 18 px box, or the line from it 6 updates ahead.
            HitSegment(p, p.Center, p.Center + p.Velocity * 6f, 10f);
            HitBox(p, 18f);
        }

        /// <summary>AI_190: the crescent sweeps over the player in exactly the item's use time.</summary>
        void TickSwing(Proj p, HeroController hero)
        {
            if (p.LocalAi0 == 0f && p.Type == 984) Sound("Item_60", 0.65f);
            if (p.LocalAi0 == 0f && p.Type == 1126)
            {
                // The slam comes with no rise in its velocity (P:42747-42751).
                p.LocalAi2 = p.Velocity.y == 0f ? 1f : 0f;
                p.Velocity.y = 0f;
                if (p.LocalAi2 == 1f) DevSlams++;
            }
            p.LocalAi0++;
            float t = p.LocalAi0 / p.Ai1, d = p.Ai0;
            p.Rotation = Pi * d * t + Mathf.Atan2(p.Velocity.y, p.Velocity.x) + d * Pi;
            // AI_190's scale: from num6 by num5 over the swing (972 0.2; 983 from 1.2 by 1; 1126 from 0.85 by 0.6; the rest 0.6).
            float growth = p.Type == 972 || p.Type == 1043 ? 0.2f : p.Type == 983 ? 1f : 0.6f;
            float baseScale = p.Type == 983 ? 1.2f : p.Type == 1126 ? 0.85f : 1f;
            p.Center = PlayerCenterPx(hero);
            p.Scale = baseScale + t * growth;
            if (p.Type == 972 && Mathf.Abs(d) < 0.2f)
            {
                // The Night's Edge thrust: spins twice and slides 45 px towards the cursor.
                p.Rotation += 4f * Pi * d * 10f * t;
                float k = Remap(p.LocalAi0, 10f, p.Ai1 - 5f, 0f, 1f);
                p.Center += p.Velocity.normalized * 45f * k;
                p.Scale += 0.4f * k;
            }
            float f = p.Rotation + Dir() * (Pi / 2f) * 0.7f;
            var fv = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
            var tang = new Vector2(Mathf.Cos(f + d * Pi / 2f), Mathf.Sin(f + d * Pi / 2f));
            var heroVel = hero.GetComponent<Rigidbody2D>().linearVelocity / Player.TerraMovement.PxT;
            heroVel.y = -heroVel.y;
            switch (p.Type)
            {
                case 972:
                    if (Random.value < 0.5f)
                    {
                        var at = p.Center + fv * 84f * p.Scale;
                        if (Random.Range(0, 5) == 0) Dust(at, 14, Random.insideUnitCircle * 2f, 150, 1.4f, false);
                        if (Random.Range(0, 2) == 0) Dust(at, 27, new Vector2(heroVel.x * 0.2f + Mathf.Sign(d) * 3f, heroVel.y * 0.2f), 100, 1.4f, true);
                    }
                    break;
                case 982:
                    if (Random.value < 0.5f)
                    {
                        var c = Color.Lerp(FxBatch.C(255, 215, 0), Color.white, Random.value * 0.3f);
                        int i = Dust(p.Center + fv * (Random.value * 80f + 20f) * p.Scale, 278, tang, 100, 0.4f, true, c);
                        FadeIn(i, 0.4f + Random.value * 0.15f);
                    }
                    if (Random.value * 1.5f < 1f) Dust(p.Center + fv * 84f * p.Scale, 43, tang, 100, 1.2f, false, Color.white);
                    break;
                case 983:
                {
                    // Projectile.cs:42952.
                    if (Random.value < p.Opacity)
                    {
                        var c = Color.Lerp(FxBatch.C(255, 105, 180), Color.white, Random.value * 0.3f);
                        int i = Dust(p.Center + fv * (Random.value * 80f + 20f) * p.Scale, 278, tang, 100, 0.4f, true, c);
                        FadeIn(i, 0.4f + Random.value * 0.15f);
                    }
                    if (Random.value * 1.5f < p.Opacity) Dust(p.Center + fv * 84f * p.Scale, 43, tang, 100, 1.2f * p.Opacity, false, Color.white * p.Opacity);
                    break;
                }
                case 997:
                    // Projectile.cs:42831: sparks streaming off the fiery blade.
                    if (Random.value * 2f < p.Opacity) Dust(p.Center + fv * (Random.value * 80f + 20f) * p.Scale, 6, tang * 4f, 0, 1.4f, true);
                    break;
                case 1126:
                    TickClaymore(p, t, d, fv, tang, heroVel);
                    break;
                case 984:
                    Fx.Light(p.Center, new Color(15 / 255f, 84 / 255f, 125 / 255f));
                    if (Random.value < 0.5f)
                    {
                        var c = Color.Lerp(Color.Lerp(FxBatch.C(15, 84, 125), FxBatch.C(64, 220, 96), Remap(t, 0f, 0.6f, 0f, 1f)), Color.white, Remap(t, 0.6f, 0.8f, 0f, 0.5f));
                        c = Color.Lerp(c, Color.white, Random.value * 0.3f);
                        int i = Dust(p.Center + fv * (Random.value * 80f + 20f) * p.Scale, 278, tang, 100, 0.4f, true, c);
                        FadeIn(i, 0.4f + Random.value * 0.15f);
                    }
                    {
                        var vel = tang * 3f + heroVel * 0.1f + new Vector2(Mathf.Sign(d), 0f);
                        int i = Dust(p.Center + fv * 85f * p.Scale - vel * 6f, 107, vel, 100, 0.8f, true);
                    }
                    break;
            }
            p.Scale *= p.Ai2;
            // Hits: a 90-degree cone of radius 94 a little ahead of the drawn blade, and while the
            // swing is young a second one 45 degrees behind it.
            float axis = p.Rotation + 2f * Pi / 25f * p.Ai0;
            float lag = Remap(p.LocalAi0, p.Ai1 * 0.3f, p.Ai1 * 0.5f, 1f, 0f);
            HitCone(p, 94f * p.Scale, axis, Pi / 4f, lag > 0f ? axis - Pi / 4f * p.Ai0 * lag : float.NaN, hero, ownerSight: true);
            if (t >= 1f) p.Dead = true;
        }

        /// <summary>
        /// AI_191 for the True Night's Edge (973): the crescent flies out from the player along the
        /// aim, easing (77 times its velocity at the end), spinning faster as it brightens, fading
        /// out over its last 15 ticks; it stops hurting after 80 ticks, or as soon as a wall stands
        /// in its way (then it also fades quicker). Hits within 100 px of its centre, in sight.
        /// </summary>
        void TickTrueNights(Proj p, HeroController hero)
        {
            const float N1 = 50f, N2 = 15f, N5 = 77f;
            float n3 = p.Ai1 + N1, n4 = n3 + N2;
            if (p.LocalAi0 == 0f) Sound("Item_8");
            p.LocalAi0++;
            if (p.Damage == 0 && p.LocalAi0 < Mathf.Lerp(n3, n4, 0.5f)) p.LocalAi0 += 6f;
            p.Opacity = Remap(p.LocalAi0, 0f, p.Ai1, 0f, 1f) * Remap(p.LocalAi0, n3, n4, 1f, 0f);
            if (p.LocalAi0 >= n4)
            {
                p.Dead = true;
                return;
            }
            if (p.Damage != 0 && p.LocalAi0 >= N5 + 3f) p.Damage = 0;
            if (p.Damage != 0)
            {
                bool clear = false;
                float rv = Mathf.Atan2(p.Velocity.y, p.Velocity.x);
                for (float k = -1f; k <= 1f; k += 0.5f)
                {
                    float a = rv + k * (Pi / 4f) * 0.25f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    if (!BlockedPx(p.Center, 0f) && ClearPx(p.Center + dir * 40f * p.Scale, p.Center + dir * 80f * p.Scale))
                    {
                        clear = true;
                        break;
                    }
                }
                if (!clear) p.Damage = 0;
            }
            p.LocalAi1++;
            float n6 = Remap(p.LocalAi1, p.Ai1 * 0.4f, n4, 0f, 1f);
            p.Center = PlayerCenterPx(hero) - p.Velocity + p.Velocity * n6 * n6 * N5;
            p.Rotation += p.Ai0 * (2f * Pi) * (4f + p.Opacity * 4f) / 90f;
            p.Scale = Remap(p.LocalAi0, p.Ai1 + 2f, n4, 1.12f, 1f) * p.Ai2;
            Trail(p, 20);
            float f = p.Rotation + Dir() * (Pi / 2f) * 0.7f;
            var at = p.Center + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 84f * p.Scale;
            if (Random.Range(0, 5) == 0) Dust(at, 14, Vector2.zero, 150, 1.4f, false);
            var v2 = p.Velocity.sqrMagnitude > 1e-4f ? p.Velocity.normalized : Vector2.right;
            for (int i = 0; i < 3f * p.Opacity; i++)
                Dust(at, Random.value < p.Opacity ? 75 : 27, p.Velocity * 0.2f + v2 * 3f, 100, 1.4f, true);
            if (p.Damage > 0) HitCircle(p, 100f * p.Scale);
        }

        /// <summary>AI_191 for the Terra Blade's crescent: out at 60 px a tick, slowing, fading after about 36 ticks.</summary>
        void TickTerraShot(Proj p, HeroController hero)
        {
            const float A = 18f, B = 43f;
            p.LocalAi0++;
            if (p.LocalAi1 == 1f) p.LocalAi0 += 2f;
            if (p.LocalAi0 >= B)
            {
                p.Dead = true;
                return;
            }
            float q = Remap(p.LocalAi0, A * 0.4f, B, 0f, 1f);
            if (p.LocalAi0 >= Mathf.Lerp(A, B, 0.65f)) p.Damage = 0;
            p.Rotation = Mathf.Atan2(p.Velocity.y, p.Velocity.x);
            p.Scale = Remap(1f - (1f - q) * (1f - q), 0f, 1f, 1.5f, 1f) * p.Ai2;
            p.Opacity = Remap(p.LocalAi0, 0f, 9f, 0f, 1f) * Remap(p.LocalAi0, 31f, B, 1f, 0f);
            if (p.Velocity.magnitude > 8f)
            {
                p.Velocity *= 0.94f;
                if (p.LocalAi1 == 1f && p.Velocity.magnitude > 8f) p.Velocity *= 0.8f;
                if (p.LocalAi1 == 1f) p.Velocity *= 0.88f;
            }
            var r = new Vector2(Mathf.Cos(p.Rotation), Mathf.Sin(p.Rotation));
            Fx.Light(p.Center + r * 85f * p.Scale, FxBatch.C(64, 220, 96));
            for (int k = 0; k < 3; k++)
            {
                if (Random.value >= p.Opacity + 0.1f) continue;
                float a = p.Rotation + Dir() * (Pi / 2f) * 0.9f;
                var tip = p.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 85f * p.Scale;
                var heroVel = hero.GetComponent<Rigidbody2D>().linearVelocity / Player.TerraMovement.PxT;
                heroVel.y = -heroVel.y;
                var vel = p.Velocity * 0.7f + heroVel * 0.1f;
                int i = Dust(tip - vel * 6f, 107, vel, 100, 0.8f * p.Opacity * 0.7f, true);
            }
            if (p.Damage == 0)
            {
                p.LocalAi0 += 3f;
                p.Velocity *= 0.76f;
            }
            // Movement, stopping dead against a block.
            var next = p.Center + p.Velocity;
            if (BlockedPx(next, 4f) && p.LocalAi1 == 0f)
            {
                p.Center = next;
                p.Velocity *= 0.01f;
                p.LocalAi1 = 1f;
            }
            else p.Center = next;
            if (p.Damage > 0) HitCone(p, 90f * p.Scale, p.Rotation, Pi / 4f, float.NaN, hero, lineOfSight: true);
        }

        /// <summary>AI_188: Light's Bane's cut, twelve frames of three ticks, purple streaks for its first nine.</summary>
        void TickLightsBane(Proj p)
        {
            if (p.LocalAi0 == 0f) Sound("Item_60", 0.15f * p.Ai0);
            p.Scale = p.Ai0;
            p.LocalAi0++;
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 12)
                {
                    p.Dead = true;
                    return;
                }
            }
            p.Rotation = Mathf.Atan2(p.Velocity.y, p.Velocity.x);
            var r = new Vector2(Mathf.Cos(p.Rotation), Mathf.Sin(p.Rotation));
            float k = p.LocalAi0 / 36f * 4f;
            if (k >= 0f && k <= 1f)
            {
                var at = Vector2.Lerp(p.Center - r * 46f * p.Scale, p.Center + r * 46f * p.Scale, p.LocalAi0 / 36f);
                int i = Dust(at, 278, Rotate(r, 2f * Pi * 0.02f * Dir()) * 8f * Random.value, 0, 0.7f * k, true, FxBatch.C(60, 0, 150));
            }
            if (p.Penetrate != 0) HitSegment(p, p.Center - r * 46f * p.Scale, p.Center + r * 46f * p.Scale, 8f * p.Scale);
        }

        /// <summary>AI_152 for the leaf: turning steadily, slowing near its mark, fading out over its minute.</summary>
        void TickLeaf(Proj p)
        {
            p.Velocity = Rotate(p.Velocity, p.Ai0);
            if (++p.FrameCounter >= 2)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 7;
            }
            float age = 60 - p.TimeLeft;
            float s = Remap(age, p.Ai1, p.Ai1 + 20f, 0f, 1f) * Remap(age, p.Ai1, p.Ai1 + 60f, 1f, 0f);
            p.Velocity = p.Velocity.normalized * (4f + 12f * (1f - s));
            p.Opacity = Remap(age, 0f, 10f, 0f, 1f) * Remap(age, 30f, 60f, 1f, 0f);
            if (Random.Range(0, 3) == 0) Dust(p.Center + Random.insideUnitCircle * 10f, 40, p.Velocity * 0.5f, 0, 1.2f, true);
            p.Rotation = Mathf.Atan2(p.Velocity.y, p.Velocity.x);
            Trail(p, 18);
            p.Center += p.Velocity;
            if (p.Penetrate != 0) HitBox(p, 20f * p.Scale, poisonChance: 0.25f);
            if (--p.TimeLeft <= 0) p.Dead = true;
        }

        /// <summary>AI_152 for Muramasa's cut: a curved streak, three updates a tick.</summary>
        void TickMuramasa(Proj p)
        {
            p.Velocity = Rotate(p.Velocity, p.Ai0 / 30f) * 0.96f;
            if (Random.Range(0, 8) == 0) Dust(p.Center, 172, Random.insideUnitCircle + p.Velocity * 0.5f, 100, 0.9f, true);
            p.Rotation = Mathf.Atan2(p.Velocity.y, p.Velocity.x) + Pi / 2f;
            Trail(p, 18);
            p.Center += p.Velocity;
            if (p.Penetrate != 0) HitBox(p, 20f * p.Scale);
            if (--p.TimeLeft <= 0) p.Dead = true;
        }

        /// <summary>AI_189: the Volcano's fireball, five frames of two ticks, setting what it touches on fire.</summary>
        void TickVolcano(Proj p)
        {
            float a0 = p.LocalAi0;
            float alpha = 255f * (1f - Remap(a0, 0f, 2f, 0f, 1f) * Remap(a0, 6f, 10f, 1f, 0f));
            p.Opacity = 1f - alpha / 255f;
            p.Scale = 1f + a0 / 15f;
            if (++p.FrameCounter >= 2)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 5)
                {
                    p.Dead = true;
                    return;
                }
            }
            p.LocalAi0++;
            var up = new Vector2(0f, -1f);
            if (p.LocalAi0 == 1f)
            {
                for (int i = 0; i < 20; i++)
                    Dust(p.Center + Random.insideUnitCircle * 8f, new[] { 6, 259, 158 }[Random.Range(0, 3)], Rotate(up, Dir() * 0.6f * Pi / 4f) * (2f + 5f * Random.value), 200, 1f + Random.value, false);
                int n = Random.Range(3, 7);
                for (int i = 0; i < 5 * n; i++)
                    Dust(p.Center + Random.insideUnitCircle * 8f, 31, Rotate(up, Dir() * 0.6f * Pi / 4f) * (2f + 5f * Random.value) * 3f, 50, 1f + Random.value, true);
                var particles = TerraParticles.Instance;
                if (particles != null)
                    for (int i = 0; i < n; i++)
                        particles.NewGore(p.Center + Random.insideUnitCircle * 8f - new Vector2(16f, 16f), Rotate(up, Dir() * Pi / 4f) * 4f * Random.value, Random.Range(61, 64));
            }
            for (int k = 0; k < 2; k++)
                if (Random.Range(0, 3) != 0)
                    Dust(p.Center + Random.insideUnitCircle * 8f, 6, Rotate(up, Dir() * Pi / 4f) * (1.5f + 2f * Random.value), 90, 2.5f, true);
            for (int k = 0; k < 2; k++)
                if (Random.value < 0.5f)
                {
                    var v = Rotate(up, Dir() * Pi / 8f) * (1f + 2f * Random.value);
                    Dust(p.Center + Random.insideUnitCircle * 8f, new[] { 6, 259, 158 }[Random.Range(0, 3)], new Vector2(v.x * 0.6f, v.y * 1.5f), 200, 1f + Random.value, false);
                }
            Fx.Light(p.Center, new Color(1f, 0.6f, 0.2f) * p.Opacity, 6f);
            if (p.Penetrate != 0) HitBox(p, 160f, onFireChance: 1f);
        }

        void Trail(Proj p, int length)
        {
            p.OldPos.Insert(0, p.Center);
            p.OldRot.Insert(0, p.Rotation);
            if (p.OldPos.Count > length + 1)
            {
                p.OldPos.RemoveAt(p.OldPos.Count - 1);
                p.OldRot.RemoveAt(p.OldRot.Count - 1);
            }
        }

        // ------------------------------------------------------------------ hitting

        /// <summary>Utils.IntersectsConeFastInaccurate on the enemies' boxes; each enemy once per projectile.</summary>
        void HitCone(Proj p, float radius, float axis, float half, float axis2, HeroController hero, bool lineOfSight = false, bool ownerSight = false)
        {
            if (p.Penetrate == 0 || p.Damage <= 0) return;
            var centerWorld = TerraParticles.ToWorld(p.Center);
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapCircle(centerWorld, radius * Px, filter, overlaps);
            foreach (var col in overlaps)
            {
                if (p.Penetrate == 0) return;
                var b = col.bounds;
                var rect = new Rect(TerraParticles.ToPx(new Vector2(b.min.x, b.max.y)), new Vector2(b.size.x / Px, b.size.y / Px));
                if (!InCone(p.Center, radius, axis, half, rect) && (float.IsNaN(axis2) || !InCone(p.Center, radius, axis2, half, rect))) continue;
                if (lineOfSight && Physics2D.Linecast(centerWorld, b.center, 1 << ChunkColliders.TerrainLayer)) continue;
                // Projectile.ownerHitCheck: only what the player can see (CharmEffects.MeleeReaches; Longnail through walls).
                if (ownerSight && !CharmEffects.MeleeReaches(col)) continue;
                Strike(p, col);
            }
        }

        static bool InCone(Vector2 tip, float radius, float axis, float half, Rect r)
        {
            var closest = new Vector2(Mathf.Clamp(tip.x, r.xMin, r.xMax), Mathf.Clamp(tip.y, r.yMin, r.yMax));
            if (Within(tip, radius, axis, half, closest)) return true;
            return Within(tip, radius, axis, half, new Vector2(r.xMin, r.yMin)) || Within(tip, radius, axis, half, new Vector2(r.xMax, r.yMin))
                || Within(tip, radius, axis, half, new Vector2(r.xMin, r.yMax)) || Within(tip, radius, axis, half, new Vector2(r.xMax, r.yMax));
        }

        static bool Within(Vector2 tip, float radius, float axis, float half, Vector2 point)
        {
            var d = point - tip;
            if (d.sqrMagnitude > radius * radius) return false;
            if (d.sqrMagnitude < 1f) return true;
            float a = Mathf.DeltaAngle(axis * Mathf.Rad2Deg, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return Mathf.Abs(a) <= half * Mathf.Rad2Deg;
        }

        /// <summary>Projectile.Colliding for 973: an enemy whose box comes within the radius of the centre, in sight of it.</summary>
        void HitCircle(Proj p, float radius)
        {
            if (p.Penetrate == 0 || p.Damage <= 0) return;
            var centerWorld = TerraParticles.ToWorld(p.Center);
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapCircle(centerWorld, radius * Px, filter, overlaps);
            foreach (var col in overlaps)
            {
                if (p.Penetrate == 0) return;
                if (Physics2D.Linecast(centerWorld, col.bounds.center, 1 << ChunkColliders.TerrainLayer)) continue;
                Strike(p, col);
            }
        }

        void HitSegment(Proj p, Vector2 a, Vector2 b, float width)
        {
            var wa = TerraParticles.ToWorld(a);
            var wb = TerraParticles.ToWorld(b);
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            var mid = (wa + wb) / 2f;
            float len = Vector2.Distance(wa, wb);
            Physics2D.OverlapBox(mid, new Vector2(len, width * Px), Mathf.Atan2(wb.y - wa.y, wb.x - wa.x) * Mathf.Rad2Deg, filter, overlaps);
            foreach (var col in overlaps)
            {
                if (p.Penetrate == 0) return;
                Strike(p, col);
            }
        }

        void HitBox(Proj p, float size, float onFireChance = 0f, float poisonChance = 0f)
        {
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapBox(TerraParticles.ToWorld(p.Center), Vector2.one * size * Px, 0f, filter, overlaps);
            foreach (var col in overlaps)
            {
                if (p.Penetrate == 0) return;
                var hm = Strike(p, col);
                if (hm == null) continue;
                if (onFireChance > 0f && Random.value < onFireChance) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 180);
                if (poisonChance > 0f && Random.value < poisonChance) EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 420);
            }
        }

        /// <summary>One hit of a projectile on an enemy (or a prop that answers hits), once per enemy.</summary>
        HealthManager Strike(Proj p, Collider2D col)
        {
            var hm = col.GetComponentInParent<HealthManager>();
            int id = hm != null ? hm.GetInstanceID() : col.gameObject.GetInstanceID();
            if (hm != null && hm.isDead) return null;
            if (p.Cooldown > 0)
            {
                if (p.Recent.ContainsKey(id)) return null;
                p.Recent[id] = p.Cooldown;
            }
            else if (!p.Hit.Add(id)) return null;
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return null;
            float facing = p.Velocity.x >= 0f ? 1f : -1f;
            if (p.Kind == Kind.Swing) facing = Mathf.Sign(p.Ai0) == 0f ? 1f : Mathf.Sign(p.Ai0);
            // A melee projectile's hit, for the armor sets (Beetle Might, the Frost set, the on-hit hub).
            ArmorEffects.Context = new ArmorEffects.Hit { Class = DamageClass.Melee, FromProjectile = true, ProjDamage = p.Damage };
            bool enemy = melee.HitWith(col, p.Damage, p.Knockback, p.Crit, facing, TerraParticles.ToWorld(p.Center));
            if (!enemy) return null;
            DevHits[p.Type] = (DevHits.TryGetValue(p.Type, out int dh) ? dh : 0) + 1;
            // Projectile.StatusNPC: melee projectiles and the Magma Stone.
            EnemyDebuffs.Magma(hm, true);
            if (p.Penetrate > 0) p.Penetrate--;
            // The Terra Blade's crescent loses a quarter of its damage with each enemy.
            if (p.Kind == Kind.TerraShot) p.Damage = Mathf.Max(1, Mathf.RoundToInt(p.Damage * 0.75f));
            var b = col.bounds;
            var at = TerraParticles.ToPx(new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y)));
            if (p.Type == 972) BurstNightsEdge(at);
            else if (p.Type == 982) BurstExcalibur(at);
            else if (p.Type == 984 || p.Type == 985) BurstTerraBlade(at, p.Velocity);
            else if (p.Type == 983) BurstTrueExcalibur(at);
            else if (p.Type == 973) BurstTrueNightsEdge(at);
            else if (p.Type == 997) SpawnPumpkin(hm, p.Damage, p.Knockback);
            return hm;
        }

        // ------------------------------------------------------------------ hit flashes (ParticleOrchestrator)

        void AddFlash(Vector2 pos, Vector2 vel, float rotation, Vector2 scale, Color tint, float additive, float ttl = 30f)
        {
            flashes.Add(new Flash { Pos = pos, Velocity = vel, Rotation = rotation, Scale = scale, Tint = tint, Additive = additive, Ttl = ttl });
        }

        /// <summary>ParticleOrchestrator.Spawn_TrueExcalibur: two pink streaks crossing, then Excalibur's golden burst.</summary>
        void BurstTrueExcalibur(Vector2 at)
        {
            for (int k = 0; k < 2; k++)
            {
                float a = Pi / 2f * k + Pi / 4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                AddFlash(at, Vector2.zero, a, new Vector2(5f, 0.5f) * 1.1f, new Color(1f, 0f, 0.3f, 1f), 0.35f, 36f);
                AddFlash(at, Vector2.zero, a, new Vector2(3f, 0.5f) * 0.7f, new Color(1f, 0.5f, 0.8f, 1f), 1f, 36f);
                if (Random.Range(0, 2) != 0)
                {
                    Dust(at, 242, Rotate(dir, Dir() * 2f * Pi * 0.025f) * 4f * Random.value, 0, 1.4f, true);
                    Dust(at, 242, -Rotate(dir, Dir() * 2f * Pi * 0.025f) * 4f * Random.value, 0, 1.4f, true);
                }
            }
            BurstExcalibur(at);
        }

        /// <summary>ParticleOrchestrator.Spawn_TrueNightsEdge: two green streaks flying apart, with green dust.</summary>
        void BurstTrueNightsEdge(Vector2 at)
        {
            foreach (float n in new[] { 0f, 2f })
            {
                float a = Pi / 4f + Pi / 4f * n;
                var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f;
                AddFlash(at - vel * 30f * 0.25f, vel, a, new Vector2(4f, 1f) * 1.1f, new Color(0.3f, 0.6f, 0.3f, 0.5f), 0.35f);
                AddFlash(at - vel * 30f * 0.25f, vel, a, new Vector2(4f, 1f) * 0.7f, new Color(0.6f, 1f, 0.2f, 1f), 1f);
                for (int i = 0; i < 2; i++)
                {
                    Dust(at, 75, Rotate(vel, Dir() * 2f * Pi * 0.025f) * Random.value, 0, 1.4f, true);
                    Dust(at, 75, -Rotate(vel, Dir() * 2f * Pi * 0.025f) * Random.value, 0, 1.4f, true);
                }
            }
        }

        /// <summary>
        /// Player.HorsemansBlade_SpawnPumpkin: a flaming jack-o'-lantern from a corner just past the
        /// edge of the view, at 8 px a tick for the enemy struck, the slash's damage.
        /// </summary>
        void SpawnPumpkin(HealthManager hm, int damage, float knockback)
        {
            var hero = HeroController.SilentInstance;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (hm == null || hero == null || cam == null) return;
            float halfW = cam.orthographic ? cam.orthographicSize * cam.aspect : Mathf.Abs(cam.transform.position.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            float halfH = halfW / Mathf.Max(0.1f, cam.aspect);
            Vector2 heroPos = hero.transform.position;
            var from = heroPos + new Vector2((Random.Range(0, 2) != 0 ? 1f : -1f) * (halfW + 1f), (Random.Range(0, 2) != 0 ? 1f : -1f) * (halfH + 1f));
            var col = hm.GetComponent<Collider2D>();
            Vector2 target = col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
            var vel = (target - from).normalized * 8f;
            TerraProjectiles.Instance?.ShootFlamingJack(from, vel, damage, knockback, hm);
        }

        void BurstNightsEdge(Vector2 at)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < 3; i++)
                {
                    float a = Pi / 4f + i * Pi / 4f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var vel = dir * 4f;
                    var scale = pass == 0 ? new Vector2(2f, 1f) * 1.1f : new Vector2(2f, 1f) * 0.7f;
                    if (i == 1) { scale *= 1.5f; vel *= 1.5f; }
                    var pos = at - dir * 4f * 7.5f;
                    if (i == 1) pos -= vel * 4f;
                    AddFlash(pos, vel, a, scale, pass == 0 ? new Color(0.25f, 0.1f, 0.5f, 0.5f) : new Color(0.5f, 0.25f, 1f, 1f), pass == 0 ? 0.35f : 1f);
                }
        }

        /// <summary>
        /// Durendal's proc (WhipTagEffect_SwordWhip.MegaSlash): a True Excalibur hit flash at a
        /// random point of the enemy (drawn with the Excalibur burst).
        /// </summary>
        public void ProcFlash(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col == null) return;
            var b = col.bounds;
            BurstExcalibur(TerraParticles.ToPx(new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y))));
        }

        void BurstExcalibur(Vector2 at)
        {
            for (int k = 0; k < 4; k++)
            {
                float a = k * Pi / 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool odd = k % 2 == 1;
                var scale = new Vector2(odd ? 4f : 2f, 0.5f);
                AddFlash(at, -dir * 0.8f * (odd ? 1.5f : 1f), a, scale * 1.1f * (odd ? 1.5f : 1f), new Color(0.9f, 0.85f, 0.4f, 0.5f), 0.35f);
                AddFlash(at, dir * 0.8f * (odd ? 1.5f : 1f), a, scale * 0.7f * (odd ? 1.5f : 1f), new Color(1f, 1f, 0.2f, 1f), 1f);
                for (int j = 0; j < 2; j++) Dust(at, 169, Rotate(dir, 2f * Pi * 0.025f * Dir()) * 4f * Random.value * (j == 0 ? 1f : -1f), 0, 1.4f, true);
            }
        }

        void BurstTerraBlade(Vector2 at, Vector2 movement)
        {
            float b = Mathf.Atan2(movement.y, movement.x) + Pi / 2f;
            for (int k = 0; k < 4; k++)
            {
                float a = b + k * Pi / 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool odd = k % 2 == 1;
                AddFlash(at, -dir * 0.8f * (odd ? 2f : 1f), a, new Vector2(3f, 0.5f) * 1.1f * (odd ? 1.5f : 1f), new Color(0.2f, 0.85f, 0.4f, 0.5f), 0.35f);
                AddFlash(at, dir * 0.8f * (odd ? 2f : 1f), a, new Vector2(3f, 0.5f) * 0.7f * (odd ? 1.5f : 1f), new Color(0.2f, 1f, 0.2f, 1f), 1f);
                Dust(at, 107, dir * 4f * Random.value, 0, 0.8f, true);
                Dust(at, 107, -dir * 4f * Random.value, 0, 1.4f, true);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                float a = b + s * Pi / 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                AddFlash(at, Rotate(dir * 2f, Pi / 2f) * 0.5f, a, new Vector2(3f, 0.5f) * 1.1f, new Color(0.4f, 1f, 0.4f, 0.5f), 0.35f);
            }
        }

        // ------------------------------------------------------------------ swing dust (ItemCheck_EmitUseVisuals)

        void SwingDust(HeroController hero)
        {
            var mod = TerraMod.Instance;
            if (mod == null || hero == null || !mod.IsUsingItem) return;
            var item = mod.SwingItem;
            if (item == null || item.NoMelee) return;
            float dir = mod.UseDirection;
            var box = mod.Melee.SwingBox;
            if (box.width <= 0f) return;
            var r = new Rect(TerraParticles.ToPx(new Vector2(box.xMin, box.yMax)), new Vector2(box.width / Px, box.height / Px));
            var heroVel = hero.GetComponent<Rigidbody2D>().linearVelocity / Player.TerraMovement.PxT;
            heroVel.y = -heroVel.y;
            var push = new Vector2(heroVel.x * 0.2f + dir * 3f, heroVel.y * 0.2f);
            // The Magma Stone's flames off any melee blade drawn in hand.
            if (Equipment.Current.MagmaStone && item.Class == DamageClass.Melee && !item.NoUseGraphic && Random.Range(0, 3) != 0)
            {
                int m = BoxDust(r, 6, push, 100, 2.5f, true);
                if (m >= 0) TerraParticles.Instance[m].Velocity *= 2f;
            }
            switch (item.Type)
            {
                case ItemType.LightsBane:
                case ItemType.NightmarePickaxe:
                    if (Random.Range(0, 15) == 0) BoxDust(r, 14, new Vector2(dir * 2f, 0f), 150, 1.3f, false);
                    break;
                case ItemType.BladeofGrass:
                    BoxDust(r, 40, push, 0, 1.2f, true);
                    break;
                case ItemType.MoltenPickaxe:
                    BoxDust(r, 6, push, 100, 1.9f, true);
                    break;
                case ItemType.Muramasa:
                {
                    int i = BoxDust(r, 172, push, 100, 0.9f, true);
                    if (i >= 0) TerraParticles.Instance[i].Velocity *= 0.1f;
                    break;
                }
                case ItemType.Starfury:
                    if (Random.Range(0, 5) == 0) BoxDust(r, 58, Vector2.zero, 150, 1.2f, false);
                    if (Random.Range(0, 10) == 0) TerraParticles.Instance?.NewGore(r.position, Vector2.zero, Random.Range(16, 18));
                    break;
                case ItemType.EnchantedSword:
                {
                    if (Random.Range(0, 5) != 0) break;
                    int i = BoxDust(r, new[] { 15, 57, 58 }[Random.Range(0, 3)], new Vector2(dir * 2f, 0f), 150, 1.3f, false);
                    if (i >= 0) TerraParticles.Instance[i].Velocity *= 0.2f;
                    break;
                }
                case ItemType.IceBlade:
                {
                    if (Random.Range(0, 5) != 0) break;
                    int i = BoxDust(r, 67, push, 90, 1.5f, true);
                    if (i >= 0) TerraParticles.Instance[i].Velocity *= 0.2f;
                    break;
                }
                case ItemType.InfluxWaver:
                {
                    if (Random.Range(0, 2) != 0) break;
                    int i = BoxDust(r, Random.Range(0, 2) == 0 ? 226 : 229, new Vector2(dir * 2f, 0f), 150, 1f, true);
                    if (i >= 0) TerraParticles.Instance[i].Velocity *= 0.2f;
                    break;
                }
                case ItemType.Meowmere:
                {
                    // Rainbow motes (hsl at full saturation, half lightness).
                    int i = BoxDust(r, 66, Vector2.zero, 150, 0.85f, true);
                    if (i < 0) break;
                    TerraParticles.Instance[i].Color = Color.HSVToRGB(Random.value, 1f, 1f);
                    TerraParticles.Instance[i].Velocity /= 2f;
                    break;
                }
                case ItemType.StarWrath:
                {
                    int i = BoxDust(r, 58, Vector2.zero, 150, 1.2f, false);
                    if (i >= 0) TerraParticles.Instance[i].Velocity *= 0.5f;
                    if (Random.Range(0, 8) == 0)
                        TerraParticles.Instance?.NewGore(r.center, Vector2.zero, 16, 1f, 0.5f, new Vector2(dir, 0f));
                    break;
                }
                case ItemType.FieryGreatsword:
                {
                    // GetPointOnSwungItemPath: flames stream off the blade along its whole length.
                    float rot = mod.ItemRotation;
                    float off = rot + (dir > 0f ? -Pi / 4f : -3f * Pi / 4f);
                    var outward = new Vector2(Mathf.Cos(off), Mathf.Sin(off));
                    var hand = TerraParticles.ToPx(mod.HandWorld);
                    for (int k = 0; k < 2; k++)
                    {
                        var at = hand + outward * 99f * (0.2f + 0.8f * Random.value) * item.Scale;
                        Dust(at, 6, Rotate(outward, Pi / 2f * dir) * 4f, 100, 2.5f * 0.7f, true);
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ drawing

        static FxBatch Fx => FxBatch.Instance;

        /// <summary>The swings' effects, over the game's scenery (FxBatch.OnTopOrder).</summary>
        void DrawAll()
        {
            var top = Fx;
            bool previousEffect = top != null && top.WeaponEffect;
            if (top != null) top.WeaponEffect = true;
            if (top != null) top.OnTop = true;
            try { DrawAllOnTop(); }
            finally { if (top != null) { top.OnTop = false; top.WeaponEffect = previousEffect; } }
        }

        void DrawAllOnTop()
        {
            var fx = Fx;
            if (fx == null) return;
            DrawArts(fx);
            foreach (var p in live)
            {
                if (p.Dead) continue;
                switch (p.Kind)
                {
                    case Kind.Swing: DrawSwing(fx, p); break;
                    case Kind.TerraShot: DrawTerraShot(fx, p); break;
                    case Kind.TrueNightsShot: DrawTrueNights(fx, p); break;
                    case Kind.LightsBaneCut: DrawLightsBane(fx, p); break;
                    case Kind.Leaf: DrawLeaf(fx, p); break;
                    case Kind.MuramasaCut: DrawMuramasa(fx, p); break;
                    case Kind.Stab:
                    {
                        // Main.DrawProj's generic branch: about its centre, mirrored by spriteDirection; the shortswords' and the
                        // saber's art is diagonal, turned back a quarter (M:33303-33306), the Gladius's and the Ruler's not.
                        float sd = p.Velocity.x < 0f ? -1f : 1f;
                        var light = new Color(L, L, L, 1f) * p.Opacity;
                        bool diagonal = (p.Type >= 938 && p.Type <= 945) || p.Type == 1127;
                        fx.Draw(FxBatch.Whole("Projectile_" + p.Type), p.Center, light, p.Rotation - (diagonal ? Pi / 4f * sd : 0f), 1f, flipX: sd < 0f, overPlayer: false);
                        break;
                    }
                    case Kind.CloseArt: DrawClose(fx, p); break;
                    case Kind.VolcanoBlast:
                        fx.Draw(FxBatch.Frame("Projectile_978", 0, p.Frame * 52, 52, 52, 26f, 26f), p.Center, FxBatch.C(255, 255, 255, 127) * p.Opacity, 0f, p.Scale);
                        break;
                }
            }
            foreach (var f in flashes) DrawFlash(fx, f);
            DrawCloseGhosts(fx);
        }

        /// <summary>The environment's light at the slash (Hallownest has no light map: the dust's constant).</summary>
        const float L = 0.874f;

        static Sprite SlashFrame(int type, int frame) => FxBatch.Frame("Projectile_" + type, 0, frame * 170, 170, 170, 85f, 85f);

        void DrawSwing(FxBatch fx, Proj p)
        {
            float t = p.LocalAi0 / p.Ai1;
            float n = Remap(t, 0f, 0.6f, 0f, 1f) * Remap(t, 0.6f, 1f, 1f, 0f);
            float S = p.Scale * 1.1f, rot = p.Rotation, d = p.Ai0;
            bool flip = d < 0f;
            var r0 = SlashFrame(p.Type, 0);
            var r3 = SlashFrame(p.Type, 3);
            var pos = p.Center;
            if (p.Type == 972)
            {
                float Lf = Remap(L, 0.2f, 1f, 0f, 1f);
                fx.Draw(r0, pos, FxBatch.C(40, 20, 60) * (Lf * n), rot - d * (Pi / 4f) * (1f - t), S * 0.975f, flip);
                fx.Draw(r0, pos, Layer2(n, Lf, false) * 0.15f, rot + d * 0.01f, S, flip);
                fx.Draw(r0, pos, FxBatch.C(80, 30, 160) * (Lf * n * 0.3f), rot, S * 0.8f, flip);
                fx.Draw(r0, pos, FxBatch.C(80, 40, 180) * (Lf * n * 0.7f), rot, S * 0.975f, flip);
                fx.Draw(r3, pos, Color.white * (0.3f * n * (1f - 0.7f * Lf)), rot + d * 0.01f, S, flip);
                float a = rot + Remap(t, 0f, 1f, 0f, Pi / 2f) * d;
                fx.Sparkle(1f, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 81f * S, new Color(1f, 1f, 1f, 0f) * (n * 0.5f), FxBatch.C(80, 40, 180), t, 0f, 0.5f, 0.5f, 1f, Pi / 4f, new Vector2(2f, 2f), Vector2.one);
                return;
            }
            if (p.Type == 983)
            {
                DrawTrueExcalibur(fx, p, t, n, S, rot, d, flip, r0, r3, pos);
                return;
            }
            if (p.Type == 997)
            {
                DrawHorsemansBlade(fx, p, t, n, S, rot, d, flip, r0, r3, pos);
                return;
            }
            if (p.Type == 1126)
            {
                DrawClaymore(fx, p, t, S, rot, d, flip, pos);
                return;
            }
            if (p.Type == 1043)
            {
                DrawFlintSlash(fx, p, t, n, S, rot, d, flip, pos);
                return;
            }
            bool terra = p.Type == 984;
            float lf = terra ? Remap(0.5f + 0.5f * L, 0.2f, 1f, 0f, 1f) : Remap(L, 0.2f, 1f, 0f, 1f);
            var shadow = terra ? FxBatch.C(45, 124, 205) : FxBatch.C(180, 160, 60);
            var c3 = terra ? FxBatch.C(34, 177, 76) : FxBatch.C(255, 255, 80);
            var c2 = terra ? FxBatch.C(181, 230, 29) : FxBatch.C(255, 240, 150);
            fx.Draw(r0, pos, shadow * (lf * n), rot - d * (Pi / 4f) * (1f - t), terra ? S * 0.95f : S, flip);
            fx.Draw(r0, pos, Layer2(n, lf, true) * 0.15f, rot + d * 0.01f, S, flip);
            fx.Draw(r0, pos, c3 * (lf * n * 0.3f), rot, S, flip);
            fx.Draw(r0, pos, c2 * (lf * n * 0.5f), rot, S * 0.975f, flip);
            fx.Draw(r3, pos, Color.white * (0.6f * n), rot + d * 0.01f, S, flip);
            fx.Draw(r3, pos, Color.white * (0.5f * n), rot - d * 0.05f, S * 0.8f, flip);
            fx.Draw(r3, pos, Color.white * (0.4f * n), rot - d * 0.1f, S * 0.6f, flip);
            int glints = terra ? 12 : 8;
            for (int k = 0; k < glints; k++)
            {
                float a = terra ? rot + d * (k - 2) * (-2f * Pi * 0.025f) + Remap(t, 0f, 1f, 0f, Pi / 4f) * d
                    : rot - d * k * (2f * Pi * 0.025f) + Remap(t, 0f, 1f, 0f, Pi / 4f) * d;
                fx.Sparkle(1f, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 79f * S, new Color(1f, 1f, 1f, 0f) * (n * (k / (terra ? 12f : 9f))), c3, t, 0f, 0.5f, 0.5f, 1f,
                    a, new Vector2(0f, Remap(t, 0f, 1f, 3f, 0f)) * S, new Vector2(S, S));
            }
            float am = rot + Remap(t, 0f, 1f, 0f, Pi / 4f) * d;
            fx.Sparkle(1f, pos + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * 81f * S, new Color(1f, 1f, 1f, 0f) * (n * 0.5f), c3, t, 0f, 0.5f, 0.5f, 1f,
                0f, new Vector2(2f, Remap(t, 0f, 1f, 4f, 1f)) * S, terra ? new Vector2(1.5f * S, 1.5f * S) : new Vector2(S, S));
        }

        /// <summary>
        /// AI_190 for 1126 (P:42843-42945): the slam sheds spore dust (157) off the blade and, as it
        /// passes 80% of its sweep, bursts spores out of the ground near its tip (or the nearest
        /// ground within a few tiles); up to 80% of any sweep, a harmless spore puff (1128) drifts off
        /// the blade now and then (one in 4, down to one in 2 at three times melee speed), carried
        /// with the player; a green light, brighter as the slam goes.
        /// </summary>
        void TickClaymore(Proj p, float t, float d, Vector2 fv, Vector2 tang, Vector2 heroVel)
        {
            const float Mark = 0.8f;
            float reach = (170f * 0.5f - 4f) * p.Scale;
            if (p.LocalAi2 == 1f)
            {
                if (Random.value * 2f < p.Opacity) Dust(p.Center + fv * (Random.value * 80f + 20f) * p.Scale, 157, tang * 4f, 0, 1.4f, true);
                float before = (p.LocalAi0 - 1f) / p.Ai1;
                if (t >= Mark && before < Mark) ClaymoreGround(p, reach - 4f, d, tang);
                if (t <= Mark)
                {
                    float r = Mathf.Lerp(reach * 0.5f, reach, t / Mark);
                    float a = p.Rotation + (Remap(t / Mark, 0f, 1f, -Pi / 4f, 0f) + Remap(t, 0f, 1f, 0f, Pi / 4f)) * d;
                    var at = p.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    for (int k = 0; k < 4; k++) Dust(at + new Vector2(Random.Range(-4f, 4f), Random.Range(-4f, 4f)), 157, tang * 4f, 0, 1.4f, true);
                }
            }
            if (t <= Mark)
            {
                float r = Mathf.Lerp(reach * 0.5f, reach, t / Mark);
                float a = p.Rotation + (Remap(t / Mark, 0f, 1f, -Pi / 4f, 0f) + Remap(t, 0f, 1f, 0f, Pi / 4f)) * d;
                var at = p.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                // Remap(meleeSpeed, 1, 1/3, 4, 2): the use-time multiplier's odds.
                int odds = (int)Remap(Equipment.UseScale(ItemDb.Get(ItemType.ChlorophyteClaymore)), 1f, 1f / 3f, 4f, 2f);
                if (Random.Range(0, Mathf.Max(1, odds)) == 0)
                    TerraProjectiles.Instance?.ClaymoreSpore(at, heroVel * 0.77f + tang * 5f, (int)(p.Damage * 0.75f), p.Crit, 90 + Random.Range(0, 60));
            }
            var light = FxBatch.C(0, 96, 0);
            if (p.LocalAi2 == 1f) light = Color.Lerp(light, FxBatch.C(34, 128, 0), t);
            Fx.Light(p.Center, light);
        }

        /// <summary>
        /// FindChlorophyteSporeCloudSpawn / SpawnChlorophyteSporeCloud (P:42566-42733): ground near
        /// the blade's tip (a block with open air over it, up to five tiles behind and two up or
        /// down); there, every such surface tile throws up dust and a ring of green motes and lets
        /// out one or two spores that hurt (75%); then the impact (1129, 128×96) and Item_167.
        /// </summary>
        void ClaymoreGround(Proj p, float radius, float d, Vector2 tang)
        {
            float baseA = p.Rotation + Remap(0.8f, 0f, 1f, 0f, Pi / 4f) * d;
            const float step = Pi / 64f;
            Vector2 spot = default;
            bool found = false;
            Vector2 At(float off) => p.Center + new Vector2(Mathf.Cos(baseA + off * d), Mathf.Sin(baseA + off * d)) * radius;
            if (FindGround(p, At(0f), out spot)) found = true;
            for (float k = step; !found && (k <= step * 20f || k <= step * 12f); k += step)
            {
                if (k <= step * 20f && FindGround(p, At(-k), out spot)) found = true;
                else if (k <= step * 12f && FindGround(p, At(k), out spot)) found = true;
            }
            if (!found) return;
            DevSporeBursts++;
            var tp = TerraProjectiles.Instance;
            int ti = Mathf.FloorToInt(spot.x / 16f), tj = Mathf.FloorToInt(spot.y / 16f);
            int i0 = ti, i1 = ti, j0 = tj - 2, j1 = tj + 2;
            if (p.Velocity.x > 0f) i0 -= 5; else i1 += 5;
            bool any = false;
            int surfaces = 0;
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                    if (Surface(i, j)) surfaces++;
            float chance = 1f;
            for (int i = i0; i <= i1; i++)
            {
                int here = 0;
                for (int j = j0; j <= j1; j++) if (Surface(i, j)) here++;
                for (int j = j0; j <= j1; j++)
                {
                    if (!Surface(i, j)) continue;
                    var tileTop = new Vector2(i * 16 + 8, j * 16);
                    for (int k = 0; k < 12; k++)
                    {
                        int di = Dust(tileTop + new Vector2(Random.Range(-8f, 8f), Random.Range(0f, 8f)), 0, new Vector2(Random.Range(-1f, 1f), -(3f + 4f) * Random.value), 0, 1f + 4 * 0.03f, false);
                        if (di >= 0) TerraParticles.Instance[di].Velocity.x *= 0.5f;
                    }
                    for (int k = 0; k < 25; k++)
                    {
                        float a = k * Pi * 2f / 25f;
                        int di = Dust(tileTop + new Vector2(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f * 0.7f), 55, new Vector2(Random.Range(-0.5f, 0.5f), -3f), 0, 1f, false, FxBatch.C(20, 255, 100, 200));
                        if (di >= 0) TerraParticles.Instance[di].FadeIn = 0.5f + Random.value * 0.5f;
                    }
                    any = true;
                    int n = here == 1 ? 2 : 1;
                    for (int s = 0; s < n; s++)
                    {
                        if (Random.value > chance) continue;
                        float lo = -0.25f * p.Velocity.x, hi = 0.25f * p.Velocity.x - lo;
                        float vx = Random.value * hi + lo + Mathf.Sign(d) * 4f * Random.value;
                        float vy = Random.Range(-45, 0) * 0.1f * 2.3f;
                        tp?.ClaymoreSpore(new Vector2(i * 16 + Random.value * 12f + 4f, j * 16 - (4 * s - 4)), new Vector2(vx, vy), (int)(p.Damage * 0.75f), p.Crit, 0f);
                    }
                }
            }
            if (!any) return;
            float r2 = (170f * 0.5f - 4f) * p.Scale - 16f;
            var dir = new Vector2(Mathf.Cos(baseA), Mathf.Sin(baseA));
            var at = p.Center + dir * r2;
            at.y -= 48f;
            if (dir.x > 0f) at.x -= 112f;
            at.x += dir.x > 0f ? 48f : -48f;
            tp?.ClaymoreSlam(at, p.Damage, p.Crit, new Vector2(128f, 96f));
            Sound("Item_167");
        }

        /// <summary>A block with no block on it (IsTileSolidForImpact on (i, j) and not on (i, j - 1)), Terraria tile coordinates.</summary>
        static bool Surface(int i, int j) => SolidTile(i, j) && !SolidTile(i, j - 1);

        static bool SolidTile(int i, int j) => TerraWorld.IsSolid(i, -j - 1);

        /// <summary>The nearest surface tile to a point, up to five tiles behind the swing and two up or down; the spot just over it.</summary>
        static bool FindGround(Proj p, Vector2 near, out Vector2 spot)
        {
            spot = near;
            int ti = Mathf.FloorToInt(near.x / 16f), tj = Mathf.FloorToInt(near.y / 16f);
            int i0 = 0, i1 = 0;
            if (p.Velocity.x > 0f) i0 = -5; else i1 = 5;
            float best = float.MaxValue;
            bool ok = false;
            for (int i = i0; i <= i1; i++)
                for (int j = -2; j <= 2; j++)
                {
                    if (!Surface(ti + i, tj + j)) continue;
                    float dist = (near - new Vector2((ti + i) * 16 + 8, (tj + j) * 16 - 8)).magnitude;
                    if (dist >= best) continue;
                    best = dist;
                    spot = new Vector2(ti * 16 + 8, (tj + j) * 16 - 8);
                    ok = true;
                }
            return ok;
        }

        /// <summary>
        /// Main.DrawProj_ChlorophyteClaymoreBlade: the green slash in layers (deeper green, brighter
        /// as the slam goes), its white edges, eight glints along it (faint but for the slam) and on
        /// the slam a star at the tip.
        /// </summary>
        void DrawClaymore(FxBatch fx, Proj p, float t, float S, float rot, float d, bool flip, Vector2 pos)
        {
            var r0 = SlashFrame(1126, 0);
            var r3 = SlashFrame(1126, 3);
            float n = Remap(t, 0f, 0.6f, 0f, 1f) * Remap(t, 0.6f, 1f, 1f, 0f);
            bool slam = p.LocalAi2 == 1f;
            if (!slam) n *= 0.5f;
            n *= 0.75f;
            float lf = Remap(L, 0.2f, 1f, 0f, 1f);
            var c = FxBatch.C(0, 96, 0);
            if (slam) c = Color.Lerp(c, FxBatch.C(65, 217, 0), t);
            var c2 = FxBatch.C(59, 148, 20);
            var c3 = FxBatch.C(165, 209, 89);
            if (slam)
            {
                c2 = Color.Lerp(c2, FxBatch.C(81, 255, 5), t);
                c3 = Color.Lerp(c3, FxBatch.C(211, 255, 149), t);
            }
            var c4 = Color.white * (n * 0.5f);
            c4.a *= 1f - lf;
            var c5 = c4 * (lf * 0.5f);
            fx.Draw(r0, pos, c * n, rot + d * (Pi / 4f) * -1f * (1f - t), S, flip);
            fx.Draw(r0, pos, c5 * 0.15f, rot + d * 0.01f, S, flip);
            fx.Draw(r0, pos, c3 * (lf * n * 0.3f), rot, S, flip);
            fx.Draw(r0, pos, c2 * (lf * n * 0.5f), rot, S * 0.975f, flip);
            fx.Draw(r3, pos, Color.white * (0.6f * n), rot + d * 0.01f, S, flip);
            fx.Draw(r3, pos, Color.white * (0.5f * n), rot + d * -0.05f, S * 0.8f, flip);
            fx.Draw(r3, pos, Color.white * (0.4f * n), rot + d * -0.1f, S * 0.6f, flip);
            for (int k = 0; k < 8; k++)
            {
                float a = rot + d * k * (-2f * Pi) * 0.025f + Remap(t, 0f, 1f, 0f, Pi / 4f) * d;
                float w = k / 9f * (slam ? 1f : 0.25f);
                fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (170f * 0.5f - 6f) * S, new Color(1f, 1f, 1f, 0f) * (n * w), c3, t, 0f, 0.5f, 0.5f, 1f,
                    a, new Vector2(0f, Remap(t, 0f, 1f, 3f, 0f)) * S, Vector2.one * S);
            }
            if (!slam) return;
            float am = rot + Remap(t, 0f, 1f, 0f, Pi / 4f) * d;
            fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * (170f * 0.5f - 4f) * S, new Color(1f, 1f, 1f, 0f) * n, c3, t, 0f, 0.5f, 0.5f, 1f,
                Pi / 4f, new Vector2(Remap(t, 0f, 1f, 4f, 1f), Remap(t, 0f, 1f, 4f, 1f)) * S, Vector2.one * S);
        }

        /// <summary>Main.DrawProj_TrueExcalibur: a crimson-to-pink slash with twelve glints along its edge.</summary>
        void DrawTrueExcalibur(FxBatch fx, Proj p, float t, float n, float S, float rot, float d, bool flip, Sprite r0, Sprite r3, Vector2 pos)
        {
            float lf = Remap(L, 0.2f, 1f, 0f, 1f);
            fx.Draw(r0, pos, Color.Lerp(FxBatch.C(180, 50, 90), FxBatch.C(180, 30, 60), n) * (lf * n), rot - d * (Pi / 4f) * (1f - t), S, flip);
            var c2 = Color.Lerp(FxBatch.C(255, 240, 150), FxBatch.C(255, 60, 170), n);
            var c3 = Color.Lerp(FxBatch.C(255, 255, 80), FxBatch.C(255, 60, 190), n);
            fx.Draw(r0, pos, Layer2(n, lf, true) * 0.15f, rot + d * 0.01f, S, flip);
            fx.Draw(r0, pos, c3 * (lf * n * 0.3f), rot, S, flip);
            fx.Draw(r0, pos, c2 * (lf * n * 0.5f), rot, S * 0.975f, flip);
            fx.Draw(r3, pos, Color.white * (0.6f * n), rot + d * 0.01f, S, flip);
            fx.Draw(r3, pos, Color.white * (0.5f * n), rot - d * 0.05f, S * 0.8f, flip);
            fx.Draw(r3, pos, Color.white * (0.4f * n), rot - d * 0.1f, S * 0.6f, flip);
            float s5 = S * 0.75f;
            for (int k = 0; k < 12; k++)
            {
                float a = rot + d * k * (-2f * Pi) * 0.025f + Remap(t, 0f, 0.6f, 0f, 0.95504415f) * d;
                fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (85f - 6f) * S, new Color(1f, 1f, 1f, 0f) * (n * (k / 12f)), c3, t, 0f, 0.5f, 0.5f, 1f,
                    a, new Vector2(0f, Remap(t, 0f, 1f, 3f, 0f)) * s5, Vector2.one * s5);
            }
            float am = rot + Remap(t, 0f, 0.6f, 0f, 0.95504415f) * d;
            fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * (85f - 4f) * S, new Color(1f, 1f, 1f, 0f) * (n * 0.5f), c3, t, 0f, 0.5f, 0.5f, 1f,
                0f, new Vector2(2f, Remap(t, 0f, 1f, 4f, 1f)) * s5, Vector2.one * s5);
        }

        /// <summary>Main.DrawProj_TheHorsemansBlade: a red-orange slash with eight glints.</summary>
        void DrawHorsemansBlade(FxBatch fx, Proj p, float t, float n, float S, float rot, float d, bool flip, Sprite r0, Sprite r3, Vector2 pos)
        {
            float lf = Remap(L, 0.2f, 1f, 0f, 1f);
            fx.Draw(r0, pos, FxBatch.C(193, 43, 43) * (lf * n), rot - d * (Pi / 4f) * (1f - t), S, flip);
            var c2 = FxBatch.C(247, 115, 0);
            var c3 = FxBatch.C(255, 202, 130);
            fx.Draw(r0, pos, Layer2(n, lf, true) * 0.15f, rot + d * 0.01f, S, flip);
            fx.Draw(r0, pos, c3 * (lf * n * 0.3f), rot, S, flip);
            fx.Draw(r0, pos, c2 * (lf * n * 0.5f), rot, S * 0.975f, flip);
            fx.Draw(r3, pos, Color.white * (0.6f * n), rot + d * 0.01f, S, flip);
            fx.Draw(r3, pos, Color.white * (0.5f * n), rot - d * 0.05f, S * 0.8f, flip);
            fx.Draw(r3, pos, Color.white * (0.4f * n), rot - d * 0.1f, S * 0.6f, flip);
            for (int k = 0; k < 8; k++)
            {
                float a = rot + d * k * (-2f * Pi) * 0.025f + Remap(t, 0f, 1f, 0f, Pi / 4f) * d;
                fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (85f - 6f) * S, new Color(1f, 1f, 1f, 0f) * (n * (k / 9f)), c3, t, 0f, 0.5f, 0.5f, 1f,
                    a, new Vector2(0f, Remap(t, 0f, 1f, 3f, 0f)) * S, Vector2.one * S);
            }
            float am = rot + Remap(t, 0f, 1f, 0f, Pi / 4f) * d;
            float len = Remap(t, 0f, 1f, 4f, 1f);
            fx.Sparkle(p.Opacity, pos + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * (85f - 4f) * S, new Color(1f, 1f, 1f, 0f) * (n * 0.5f), c3, t, 0f, 0.5f, 0.5f, 1f,
                Pi / 4f, new Vector2(len, len) * S, Vector2.one * S);
        }

        /// <summary>
        /// Main.DrawProj_TrueNightsEdge: the crescent and two fainter copies a little behind it along
        /// the flight, each drawn three times round its turn, purple turning green as it fades in;
        /// a green sparkle at its tip.
        /// </summary>
        void DrawTrueNights(FxBatch fx, Proj p)
        {
            var r0 = SlashFrame(973, 0);
            var r3 = SlashFrame(973, 3);
            float S = p.Scale * 1.1f, d = p.Ai0;
            bool flip = d < 0f;
            float lf = Remap(L, 0.2f, 1f, 0f, 1f);
            float n3 = Mathf.Min(0.15f + lf * 0.85f, Remap(p.LocalAi0, 30f, 96f, 1f, 0f));
            for (int k = 2; k >= 0; k--)
            {
                if (k >= p.OldRot.Count) continue;
                var pos = p.Center - p.Velocity * 0.5f * k;
                float r6 = p.OldRot[k] + d * (2f * Pi) * 0.1f * -k;
                float n7 = 1f - k / 2f;
                float n8 = p.Opacity * n7 * n7 * 0.85f;
                float amount = p.Opacity * p.Opacity;
                fx.Draw(r0, pos, Color.Lerp(FxBatch.C(40, 20, 60, 120), FxBatch.C(80, 160, 50, 120), amount) * (n3 * n8), r6 - d * (Pi / 4f), S * 0.975f, flip);
                var c2 = Color.Lerp(FxBatch.C(80, 40, 180), FxBatch.C(155, 255, 100), amount);
                var c3 = Color.white * (n8 * 0.5f);
                c3.a *= 1f - n3;
                var c4 = c3 * (n3 * 0.5f);
                c4.g *= n3;
                c4.r *= 0.25f + n3 * 0.75f;
                var mid = Color.Lerp(FxBatch.C(80, 30, 160), FxBatch.C(200, 255, 0), amount);
                for (float m = -2f * Pi + 2f * Pi / 3f; m < 0f; m += 2f * Pi / 3f)
                {
                    float w = Remap(m, -2f * Pi, 0f, 0f, 0.5f);
                    fx.Draw(r0, pos, c4 * (0.15f * w), r6 + d * 0.01f + m, S, flip);
                    fx.Draw(r0, pos, mid * (lf * n8 * w), r6 + m, S * 0.8f, flip);
                    fx.Draw(r0, pos, c2 * (lf * n8 * Mathf.Lerp(0.05f, 0.4f, lf) * w), r6 + m, S * 0.975f, flip);
                    fx.Draw(r3, pos, Color.white * (Mathf.Lerp(0.05f, 0.5f, lf) * n8 * w), r6 + m, S, flip);
                }
                fx.Draw(r0, pos, c4 * 0.15f, r6 + d * 0.01f, S, flip);
                fx.Draw(r0, pos, mid * (n3 * n8), r6, S * 0.8f, flip);
                fx.Draw(r0, pos, c2 * (lf * n8 * Mathf.Lerp(0.05f, 0.4f, n3)), r6, S * 0.975f, flip);
                fx.Draw(r3, pos, Color.white * (Mathf.Lerp(0.05f, 0.5f, n3) * n8), r6, S, flip);
            }
            float n12 = Mathf.Max(0.5f, 1f - p.LocalAi0 / 80f);
            float ta = p.Rotation + 0.47123894f * d;
            var tip = p.Center + new Vector2(Mathf.Cos(ta), Mathf.Sin(ta)) * (85f - 4f) * S * n12;
            float n13 = Mathf.Min(n3, Mathf.Lerp(1f, lf, Remap(p.LocalAi0, 0f, 80f, 0f, 1f)));
            fx.Sparkle(p.Opacity, tip, new Color(1f, 1f, 1f, 0f) * (p.Opacity * 0.5f * n13), FxBatch.C(150, 255, 100) * n13, p.Opacity, 0f, 1f, 1f, 2f, Pi / 4f, new Vector2(2f, 2f), Vector2.one);
        }

        /// <summary>The faint second layer of a slash: white, weaker the brighter it is around (with the per-sword channel quirks).</summary>
        static Color Layer2(float n, float lf, bool excaliburRule)
        {
            var c4 = Color.white * (n * 0.5f);
            c4.a *= 1f - lf;
            var c5 = c4 * (lf * 0.5f);
            c5.g *= lf;
            if (excaliburRule) c5.b = c5.r * (0.25f + 0.75f * lf);
            else c5.r *= 0.25f + 0.75f * lf;
            return c5;
        }

        void DrawTerraShot(FxBatch fx, Proj p)
        {
            float u = Remap(p.LocalAi0, 0f, p.Ai1 + 30f, 0f, 1f), o = p.Opacity, sc = p.Scale, w = 1f - u, rot = p.Rotation, d = p.Ai0;
            float lf = Remap(0.5f + 0.5f * L, 0.2f, 1f, 0f, 1f);
            bool eA = d < 0f, eB = !eA;
            var r0 = SlashFrame(985, 0);
            var r3 = SlashFrame(985, 3);
            var pos = p.Center;
            var shadow = FxBatch.C(45, 124, 205) * (lf * o);
            fx.Draw(r0, pos, shadow, rot - d * (Pi / 4f) * 0.5f * w, sc * 0.95f, eA);
            fx.Draw(r0, pos, shadow, rot + d * (Pi / 4f) * 0.5f * w, sc * 0.95f, eB);
            var c4 = Color.white * (o * 0.5f);
            c4.a *= 1f - lf;
            var c5 = c4 * (lf * 0.5f);
            c5.g *= lf;
            c5.b = c5.r * (0.25f + 0.75f * lf);
            fx.Draw(r0, pos, c5 * 0.15f, rot + d * 0.01f, sc, eA);
            fx.Draw(r0, pos, c5 * 0.15f, rot - d * 0.01f, sc, eB);
            var c3 = FxBatch.C(34, 177, 76);
            fx.Draw(r0, pos, c3 * (lf * o * 0.3f), rot + d * 0.25f * w, sc, eA);
            fx.Draw(r0, pos, c3 * (lf * o * 0.3f), rot - d * 0.25f * w, sc, eB);
            fx.Draw(r0, pos, FxBatch.C(181, 230, 29) * (lf * o * 0.5f), rot + d * 0.15f * w, sc * 0.975f, eA);
            fx.Draw(r3, pos, Color.white * (0.6f * o), rot + d * 0.05f * w, sc, eA);
            fx.Draw(r3, pos, Color.white * (0.5f * o), rot - d * 0.05f, sc * 0.8f, eA);
            fx.Draw(r3, pos, Color.white * (0.4f * o), rot - d * 0.1f, sc * 0.6f, eA);
            for (int k = -9; k <= 8; k++)
            {
                float a = rot + d * k * (-2f * Pi * 0.025f);
                fx.Sparkle(o, pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 79f * sc, new Color(1f, 1f, 1f, 0f) * (o * Mathf.Abs(k) / 9f), c3, u, 0f, 0.5f, 0.5f, 1f,
                    a, new Vector2(0f, Remap(u, 0f, 1f, 3f, 0f)) * sc, new Vector2(sc, sc));
            }
            foreach (float m in new[] { -1f, -0.5f, 0.5f, 1f })
            {
                float a = rot + m * 0.75f * Pi * u;
                var at = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 81f * sc;
                float z = Remap(Mathf.Abs(m), 0f, 1f, 1f, 0.5f);
                float len = Remap(u, 0f, 1f, 4f, 1f);
                fx.Sparkle(o, at, new Color(1f, 1f, 1f, 0f) * (o * 0.5f), c3, u, 0f, 0.5f, 0.5f, 0.75f, Pi / 4f, new Vector2(len, len) * sc * z, Vector2.one * sc * z);
                fx.Sparkle(o, at, new Color(1f, 1f, 1f, 0f) * (o * 0.5f), c3, u, 0f, 0.5f, 0.5f, 0.75f, 0f, new Vector2(2f, len) * sc * z, Vector2.one * sc * z);
            }
            var tip = pos + new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)) * 81f * sc;
            float tl = Remap(u, 0f, 1f, 4f, 1f);
            fx.Sparkle(o, tip, new Color(1f, 1f, 1f, 0f) * (o * 0.5f), c3, u, 0f, 0.5f, 0.5f, 1f, Pi / 4f, new Vector2(tl, tl) * sc, Vector2.one * 1.5f * sc);
            fx.Sparkle(o, tip, new Color(1f, 1f, 1f, 0f) * (o * 0.5f), c3, u, 0f, 0.5f, 0.5f, 1f, 0f, new Vector2(2f, tl) * sc, Vector2.one * 1.5f * sc);
        }

        /// <summary>
        /// Light's Bane's cut is drawn subtracting green from what is behind it (a blend Unity's own
        /// shaders don't have) and adding magenta; over Hallownest's dark that reads as a purple
        /// slash, drawn here as one.
        /// </summary>
        void DrawLightsBane(FxBatch fx, Proj p)
        {
            var v2 = new Vector2(0.7f, 0.7f) * p.Scale;
            float g = Remap(p.Frame, 0f, 3f, 0f, 1f) * Remap(p.Frame, 4f, 12f, 1f, 0f);
            var glow = FxBatch.Frame("Projectile_974", 0, 12 * 54, 171, 54, 85.5f, 27f);
            var frame = FxBatch.Frame("Projectile_974", 0, p.Frame * 54, 171, 54, 85.5f, 27f);
            fx.Draw(glow, p.Center, FxBatch.C(60, 0, 150) * (0.5f * g), p.Rotation, new Vector2(1f, 6f) * v2);
            fx.Draw(glow, p.Center, FxBatch.C(60, 0, 150) * (0.5f * g), p.Rotation, new Vector2(2f, 2f) * v2);
            fx.Draw(frame, p.Center, FxBatch.C(40, 0, 90, 170), p.Rotation, v2);
            fx.Draw(frame, p.Center, FxBatch.C(255, 0, 255, 0) * 0.6f, p.Rotation, v2);
            fx.Draw(frame, p.Center, FxBatch.C(255, 0, 255, 0) * 0.6f, p.Rotation, v2);
        }

        void DrawLeaf(FxBatch fx, Proj p)
        {
            var light = new Color(L, L, L, 1f) * p.Opacity;
            for (int i = 18; i >= 3; i -= 3)
            {
                if (i >= p.OldPos.Count) continue;
                int frame = (p.Frame + (18 - i) / 3) % 7;
                fx.Draw(FxBatch.Frame("Projectile_976", 0, frame * 20, 32, 18, 16f, 9f), p.OldPos[i], light * ((18 - i) / 90f), p.OldRot[i], Mathf.Lerp(p.Scale, 1.3f, i / 15f));
            }
            fx.Draw(FxBatch.Frame("Projectile_976", 0, p.Frame * 20, 32, 18, 16f, 9f), p.Center, light, p.Rotation, p.Scale);
        }

        void DrawMuramasa(FxBatch fx, Proj p)
        {
            var sprite = FxBatch.Frame("Projectile_977", 0, 0, 18, 72, 9f, 36f);
            var c = FxBatch.C(255, 255, 255, 200);
            for (int i = 18; i >= 2; i -= 2)
            {
                if (i >= p.OldPos.Count) continue;
                fx.Draw(sprite, p.OldPos[i], c * ((18 - i) / 45f), p.OldRot[i], Mathf.Lerp(p.Scale, 1.3f, i / 15f));
            }
            fx.Draw(sprite, p.Center, c, p.Rotation, p.Scale);
        }

        /// <summary>PrettySparkleParticle: a streak along its rotation flashing for 15 ticks.</summary>
        void DrawFlash(FxBatch fx, Flash f)
        {
            float age = f.Age, ttl = f.Ttl;
            float opacity = Mathf.Clamp01(Mathf.InverseLerp(0f, 5e-6f, age / ttl)) * Mathf.Clamp01(Mathf.InverseLerp(1f, 0.95f, age / ttl));
            var cw = Color.white * (opacity * 0.9f);
            cw.a /= 2f;
            var c3 = cw * 0.5f;
            var c2 = f.Tint * (opacity * 0.5f);
            c2.a *= 1f - f.Additive;
            float tt = age / ttl * 60f;
            float k = Mathf.Clamp01(Mathf.InverseLerp(0f, 15f, tt)) * Mathf.Clamp01(Mathf.InverseLerp(30f, 15f, tt));
            var v1 = new Vector2(0.3f * f.Scale.x, 2f * f.Scale.y) * k;
            var tex = FxBatch.Whole("Extra_98");
            fx.Draw(tex, f.Pos, c2, f.Rotation + Pi / 2f, v1, overPlayer: true);
            fx.Draw(tex, f.Pos, c3, f.Rotation + Pi / 2f, v1 * 0.6f, overPlayer: true);
        }

        // ------------------------------------------------------------------ helpers

        static float Dir() => Random.value * 2f - 1f;

        static float Remap(float v, float a, float b, float c, float d)
        {
            float t = Mathf.Approximately(a, b) ? (v >= b ? 1f : 0f) : Mathf.Clamp01((v - a) / (b - a));
            return Mathf.Lerp(c, d, t);
        }

        static Vector2 Rotate(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static Vector2 RandomIn(Rect r) => new Vector2(r.x + Random.value * r.width, r.y + Random.value * r.height);

        /// <summary>The box (Terraria px) of the live enemy nearest a point within a distance.</summary>
        Rect? NearestEnemy(Vector2 at, float within)
        {
            var world = TerraParticles.ToWorld(at);
            Rect? best = null;
            float bestD = within * Px;
            foreach (var col in Physics2D.OverlapCircleAll(world, within * Px, 1 << 11))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || (EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm)) || ArenaGuard.Outside(hm) || ArenaGuard.OffMap(hm)) continue;
                float dd = Vector2.Distance(col.bounds.center, world);
                if (dd > bestD) continue;
                bestD = dd;
                var b = col.bounds;
                best = new Rect(TerraParticles.ToPx(new Vector2(b.min.x, b.max.y)), new Vector2(b.size.x / Px, b.size.y / Px));
            }
            return best;
        }

        static bool ClearPx(Vector2 a, Vector2 b) => !Physics2D.Linecast(TerraParticles.ToWorld(a), TerraParticles.ToWorld(b), 1 << ChunkColliders.TerrainLayer);

        static bool BlockedPx(Vector2 px, float half)
        {
            var w = TerraParticles.ToWorld(px);
            var c = CellGrid.WorldToCell(w);
            return TerraWorld.IsSolid(c.x, c.y);
        }

        /// <summary>Dust.NewDustPerfect: a dust of a type exactly at a point (Terraria px) with a velocity.</summary>
        static int Dust(Vector2 at, int type, Vector2 velocity, int alpha, float scale, bool noGravity, Color color = default)
        {
            var p = TerraParticles.Instance;
            if (p == null) return -1;
            int i = p.NewDust(at - new Vector2(4f, 4f), 0, 0, type, 0f, 0f, alpha, scale);
            if (i < 0) return -1;
            ref var d = ref p[i];
            d.Position = at;
            d.Velocity = velocity;
            d.NoGravity = noGravity;
            if (color.a > 0f) d.Color = color;
            return i;
        }

        static int BoxDust(Rect r, int type, Vector2 speed, int alpha, float scale, bool noGravity)
        {
            var p = TerraParticles.Instance;
            if (p == null) return -1;
            int i = p.NewDust(r.position, Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height), type, speed.x, speed.y, alpha, scale);
            if (i >= 0) p[i].NoGravity = noGravity;
            return i;
        }

        static void FadeIn(int i, float fadeIn)
        {
            if (i < 0 || TerraParticles.Instance == null) return;
            TerraParticles.Instance[i].FadeIn = fadeIn;
        }

        static void Sound(string name, float volume = 1f) => TerraMod.Instance?.PlaySound(name, volume);

        public void Clear()
        {
            live.Clear();
            closes.Clear();
            closeGhosts.Clear();
            flashes.Clear();
            arts.Clear();
        }

        /// <summary>Dev: projectiles and flashes alive.</summary>
        public int DevLive => live.Count;
        /// <summary>Dev: enemy hits so far by vanilla projectile type (972 the Night's Edge slash, 973 the True Night's Edge blade...).</summary>
        public static readonly Dictionary<int, int> DevHits = new Dictionary<int, int>();
        public int DevFlashes => flashes.Count;
    }
}
