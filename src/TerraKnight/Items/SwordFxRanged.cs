using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade ranged weapons' shots drawn (RangedArts; nothing here hits), all of Terraria's dusts: as each shot leaves,
    /// a bow's string throws its motes forward; a gun's muzzle smokes and sparks, its spent case flung up and back (a
    /// shotgun's red shell as it is pumped); a launcher's smoke and fire blast out behind the shoulder as well as ahead; a
    /// cannon's motes ring its mouth; a blowpipe puffs; a thrown thing leaves a spray of its motes from the hand. A sprayer's
    /// pilot flame flickers at its nozzle while it pours. A full-power shot (RangedFire's Power) opens a ring of its motes.
    /// The thrown weapons' next one drawn in the hand as it is brought back over the shoulder.
    /// </summary>
    internal sealed partial class SwordFx
    {
        int rangedShots;
        ItemDef rangedShotItem;
        bool rangedPower, rangedPumped;
        int rangedUse = -1;
        readonly Dictionary<ItemType, Sprite> heldThrown = new Dictionary<ItemType, Sprite>();

        /// <summary>Dev: shots drawn, full-power rings, thrown ones drawn in the hand, shells pumped out.</summary>
        public static int DevRangedFx, DevRangedRings, DevThrownDraws, DevShells;

        /// <summary>A tracer's segments and the lightning arcs (Terraria px), each fading from when it was made.</summary>
        readonly List<(Vector2 A, Vector2 B, Color C, float At)> tracers = new List<(Vector2, Vector2, Color, float)>();
        readonly List<(Vector2 A, Vector2 B, Color C, float At, float Seed)> arcs = new List<(Vector2, Vector2, Color, float, float)>();
        public static int DevTracers, DevArcs;
        const float TracerLife = 0.35f, ArcLife = 0.25f;

        /// <summary>A tracer's segment (a remade shot's path, RangedShot.Tracer).</summary>
        public void RangedTracer(Vector2 a, Vector2 b, Color c)
        {
            if ((a - b).sqrMagnitude < 1f) return;
            if (tracers.Count > 400) tracers.RemoveAt(0);
            tracers.Add((a, b, c, Time.time));
            DevTracers++;
        }

        /// <summary>A lightning arc between two enemies (RangedShot.Chain), with its sparks.</summary>
        public void RangedArc(Vector2 a, Vector2 b, ItemDef item)
        {
            var look = item != null ? RangedArts.Of(item) : default;
            var c = item != null ? Color.Lerp(look.A, Color.white, 0.4f) : new Color(0.7f, 0.9f, 1f);
            if (arcs.Count > 60) arcs.RemoveAt(0);
            arcs.Add((a, b, c, Time.time, Random.value * 100f));
            for (int k = 0; k < 6; k++)
                Dust(Vector2.Lerp(a, b, Random.value), 226, Random.insideUnitCircle * 1.5f, 80, Random.Range(0.6f, 0.9f), true);
            DevArcs++;
        }

        void DrawLines(FxBatch fx)
        {
            float now = Time.time;
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                var s = tracers[i];
                float age = (now - s.At) / TracerLife;
                if (age >= 1f) { tracers.RemoveAt(i); continue; }
                Streak(fx, s.A, s.B, s.C, 5f * (1f - age * 0.5f), 0.7f * (1f - age));
            }
            for (int i = arcs.Count - 1; i >= 0; i--)
            {
                var s = arcs[i];
                float age = (now - s.At) / ArcLife;
                if (age >= 1f) { arcs.RemoveAt(i); continue; }
                // A jagged line: five legs, each corner thrown aside, flickering.
                var d = s.B - s.A;
                var side = new Vector2(-d.y, d.x).normalized;
                var prev = s.A;
                for (int k = 1; k <= 5; k++)
                {
                    float f = k / 5f;
                    var at = k == 5 ? s.B : s.A + d * f + side * (Mathf.PerlinNoise(s.Seed + k, now * 30f) - 0.5f) * 18f;
                    Streak(fx, prev, at, s.C, 4f, 0.8f * (1f - age));
                    prev = at;
                }
            }
        }

        /// <summary>A shot left a remade ranged weapon (RangedFire): its muzzle's look on the next tick.</summary>
        public void RangedShot(ItemDef def)
        {
            rangedShots++;
            rangedShotItem = def;
        }

        /// <summary>The shot is a full-power one (RangedFire's Power): a ring of its motes too.</summary>
        public void RangedPower(ItemDef def) => rangedPower = true;

        /// <summary>A thrown weapon's arm now (degrees from level for the way he faces), for TerraMod's hold.</summary>
        public bool RangedThrowAngle(out float deg)
        {
            deg = 0f;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            if (mod == null || hero == null || !mod.IsUsingItem || mod.SwingItem == null || !RangedArts.Flung(mod.SwingItem)) return false;
            deg = RangedArts.ThrowAngle(mod.UseProgress, AimDeg(hero, mod.UseDirection), RangedArts.Of(mod.SwingItem).Kind == RangedArts.Kind.Lob);
            return true;
        }

        /// <summary>The weapon's mouth (Terraria px) and the aim's way: the held-out picture's far end, else ahead of the shoulder.</summary>
        bool RangedMuzzle(HeroController hero, ItemDef def, out Vector2 tip, out Vector2 way)
        {
            var mod = TerraMod.Instance;
            float facing = mod.UseDirection;
            way = Along(ScreenAngle(AimDeg(hero, facing), facing));
            var held = mod.PlayerLook != null ? mod.PlayerLook.HeldTip : null;
            if (held.HasValue && !RangedArts.Flung(def) && mod.IsUsingItem && mod.SwingItem == def)
            {
                tip = TerraParticles.ToPx(held.Value);
                return true;
            }
            // Held out for an art (HeldGun's picture): its far end.
            if ((BladeSkills.RangedShown(out var shown) && shown == def || BladeSkills.Charging && BladeSkills.ChargeAimed && BladeSkills.ChargeItem == def.Type) && !RangedArts.Flung(def))
            {
                float deg = BladeSkills.Charging ? BladeSkills.ChargeAngle(hero, facing) : BladeSkills.ShownAngle(hero, facing);
                float screen = ScreenAngle(deg, facing);
                var pic = TerraMod.ItemSprite(def.Type);
                way = Along(screen);
                tip = ShoulderPx(FeetPx(hero), facing) + way * (4f + (pic != null ? pic.rect.width : 24f) * Equipment.ItemScale(def));
                return true;
            }
            tip = ShoulderPx(FeetPx(hero), facing) + way * 14f;
            return true;
        }

        void TickRanged(HeroController hero)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            if (mod.UseSerial != rangedUse)
            {
                rangedUse = mod.UseSerial;
                rangedPumped = false;
            }
            var using_ = mod.IsUsingItem ? mod.SwingItem : null;
            if (using_ != null && RangedArts.Active(using_))
            {
                var look = RangedArts.Of(using_);
                if (look.Kind == RangedArts.Kind.Sprayer && Random.value < 0.35f && RangedMuzzle(hero, using_, out var nozzle, out var w))
                    Mote(look, nozzle, w * Random.Range(0.3f, 1f) + new Vector2(0f, -0.4f), Random.Range(0.4f, 0.7f));
                // The shotgun pumped: its spent shell out the side.
                if (look.Kind == RangedArts.Kind.Shotgun && !rangedPumped && mod.UseProgress > 0.55f && RangedMuzzle(hero, using_, out var m, out var w2))
                {
                    rangedPumped = true;
                    Case(m - w2 * 22f, w2, new Color(0.85f, 0.2f, 0.15f));
                    DevShells++;
                }
            }
            if (rangedShots <= 0 || rangedShotItem == null) return;
            var def = rangedShotItem;
            int shots = Mathf.Min(rangedShots, 3);
            rangedShots = 0;
            if (!RangedMuzzle(hero, def, out var tip, out var way)) return;
            var side = new Vector2(-way.y, way.x);
            var lk = RangedArts.Of(def);
            bool rapid = def.UseTime * 60f * RangedArts.UseScale(def) <= 8f;
            for (int s = 0; s < shots; s++)
            {
                DevRangedFx++;
                switch (lk.Kind)
                {
                    case RangedArts.Kind.Bow:
                    {
                        // The string's motes, thrown forward along it.
                        var hand = ShoulderPx(FeetPx(hero), mod.UseDirection) + way * 10f;
                        for (int k = 0; k < 9; k++) Mote(lk, hand + side * Random.Range(-12f, 12f), way * Random.Range(1.2f, 2.8f), Random.Range(0.7f, 1f));
                        for (int k = 0; k < 3; k++) Mote(lk, tip, way * Random.Range(2f, 3.5f) + side * Random.Range(-0.4f, 0.4f), 0.8f);
                        break;
                    }
                    case RangedArts.Kind.Repeater:
                        for (int k = 0; k < 6; k++) Mote(lk, tip, way * Random.Range(1.5f, 3.5f) + side * Random.Range(-0.8f, 0.8f), Random.Range(0.7f, 1f));
                        Smoke(tip, way, 1, 0.6f);
                        break;
                    case RangedArts.Kind.Pistol:
                    case RangedArts.Kind.Rifle:
                    case RangedArts.Kind.Auto:
                    {
                        bool auto = lk.Kind == RangedArts.Kind.Auto;
                        if (!rapid || Random.value < 0.5f) Smoke(tip, way, auto ? 1 : lk.Kind == RangedArts.Kind.Rifle ? 5 : 3, auto ? 0.7f : 1f);
                        Sparks(lk, tip, way, side, auto ? 1 : lk.Kind == RangedArts.Kind.Rifle ? 4 : 3, lk.Kind == RangedArts.Kind.Rifle ? 6f : 4.5f);
                        if (!rapid || Random.value < 0.4f) Case(tip - way * (lk.Kind == RangedArts.Kind.Rifle ? 26f : 14f), way, new Color(0.95f, 0.75f, 0.3f));
                        break;
                    }
                    case RangedArts.Kind.Shotgun:
                        Smoke(tip, way, 8, 1.2f);
                        Sparks(lk, tip, way, side, 8, 5f, 1.8f);
                        break;
                    case RangedArts.Kind.Launcher:
                    {
                        Smoke(tip, way, 6, 1.2f);
                        Sparks(lk, tip, way, side, 4, 4f);
                        // The backblast, out behind the shoulder.
                        var back = ShoulderPx(FeetPx(hero), mod.UseDirection) - way * 14f;
                        Smoke(back, -way, 8, 1.3f);
                        for (int k = 0; k < 5; k++) Dust(back, 6, -way * Random.Range(2f, 4f) + side * Random.Range(-1.2f, 1.2f), 100, Random.Range(0.9f, 1.4f), true);
                        break;
                    }
                    case RangedArts.Kind.Cannon:
                        for (int k = 0; k < 8; k++)
                        {
                            float a = k / 8f * Mathf.PI * 2f;
                            Mote(lk, tip, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f + way * 1.2f, Random.Range(0.6f, 0.9f));
                        }
                        Smoke(tip, way, 2, 0.9f);
                        break;
                    case RangedArts.Kind.Blowpipe:
                        Smoke(tip, way, 3, 0.6f);
                        for (int k = 0; k < 2; k++) Mote(lk, tip, way * Random.Range(1.5f, 2.5f), 0.55f);
                        break;
                    case RangedArts.Kind.Thrown:
                    case RangedArts.Kind.Lob:
                    {
                        var hand = ShoulderPx(FeetPx(hero), mod.UseDirection) + way * 12f;
                        for (int k = 0; k < 8; k++) Mote(lk, hand, way * Random.Range(1.5f, 3.2f) + side * Random.Range(-0.9f, 0.9f), Random.Range(0.7f, 1f));
                        break;
                    }
                }
            }
            if (rangedPower)
            {
                rangedPower = false;
                DevRangedRings++;
                var centre = tip + way * 6f;
                for (int k = 0; k < 14; k++)
                {
                    float a = k / 14f * Mathf.PI * 2f;
                    var r = side * Mathf.Cos(a) * 9f + way * Mathf.Sin(a) * 4f;
                    Mote(lk, centre + r, r.normalized * 1.2f + way * 1.5f, Random.Range(0.7f, 1f));
                }
                Fx?.Light(tip, lk.A * 0.6f, 3f, true);
            }
        }

        /// <summary>One of the weapon's motes (278 tinted with its colours, else the dust's own).</summary>
        static void Mote(RangedArts.Look look, Vector2 at, Vector2 vel, float scale)
        {
            int m = Dust(at, look.Dust, vel, look.Dust == 278 ? 40 : 90, scale, true, look.Dust == 278 ? Color.Lerp(look.A, look.B, Random.value * 0.6f) : default);
            FadeIn(m, scale * 0.8f);
        }

        /// <summary>Gun smoke (dust 31) drifting out along the way and up.</summary>
        static void Smoke(Vector2 at, Vector2 way, int n, float scale)
        {
            for (int k = 0; k < n; k++)
            {
                int d = Dust(at + Random.insideUnitCircle * 3f, 31, way * Random.Range(0.4f, 1.6f) + new Vector2(Random.Range(-0.3f, 0.3f), -Random.Range(0.2f, 0.7f)), 120, scale * Random.Range(0.8f, 1.2f), true);
                if (d >= 0 && TerraParticles.Instance != null) TerraParticles.Instance[d].Velocity *= 0.9f;
            }
        }

        /// <summary>Sparks off the muzzle (the weapon's fire, dust 6, or its own motes), in a cone along the way.</summary>
        static void Sparks(RangedArts.Look look, Vector2 at, Vector2 way, Vector2 side, int n, float speed, float spread = 0.9f)
        {
            int type = look.Dust == 278 ? 6 : look.Dust;
            for (int k = 0; k < n; k++)
                Dust(at, type, way * Random.Range(speed * 0.5f, speed) + side * Random.Range(-spread, spread), 100, Random.Range(0.6f, 1f), true);
        }

        /// <summary>A spent case flung up and back over the shoulder, falling (278 tinted brass, a shotgun's shell red).</summary>
        static void Case(Vector2 at, Vector2 way, Color c)
        {
            int d = Dust(at, 278, new Vector2(-way.x * Random.Range(0.8f, 1.6f), -Random.Range(2f, 3f)), 0, 0.7f, false, c);
            if (d >= 0 && TerraParticles.Instance != null) TerraParticles.Instance[d].FadeIn = 0f;
        }

        /// <summary>A heavy shot's smoke (a slug, a musket ball, the sniper's): a thick cloud at the muzzle and sparks.</summary>
        public void RangedBlast(ItemDef def)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || def == null || !RangedMuzzle(hero, def, out var tip, out var way)) return;
            var look = RangedArts.Of(def);
            var side = new Vector2(-way.y, way.x);
            Smoke(tip, way, 12, 1.4f);
            Sparks(look, tip, way, side, 10, 6f, 2.2f);
        }

        /// <summary>An aimed gun's sight: a thin line of its colour from the muzzle to the cursor, firmer as the aim steadies.</summary>
        void DrawSight(FxBatch fx, HeroController hero)
        {
            if (!BladeSkills.GunAiming(out var def, out float c) || def == null) return;
            if (!RangedMuzzle(hero, def, out var tip, out var way)) return;
            var mod = TerraMod.Instance;
            var aim = TerraParticles.ToPx(mod.DevAim ?? mod.AimPoint);
            var look = RangedArts.Of(def);
            var col = Color.Lerp(look.A, Color.white, 0.3f);
            Streak(fx, tip, aim, col, 3f + 2f * c, 0.3f + 0.4f * c);
            // Steady: a small cross at the cursor, and its motes.
            if (c >= 1f)
            {
                for (int k = 0; k < 4; k++)
                {
                    var arm = new Vector2(Mathf.Cos(k * Mathf.PI / 2f), Mathf.Sin(k * Mathf.PI / 2f));
                    Streak(fx, aim + arm * 4f, aim + arm * 11f, col, 3f, 0.75f);
                }
                if (Random.value < 0.3f) Mote(look, aim + Random.insideUnitCircle * 4f, Random.insideUnitCircle * 0.5f, 0.6f);
            }
        }

        void DrawRanged(FxBatch fx, HeroController hero, Vector2 feet)
        {
            DrawLines(fx);
            DrawSight(fx, hero);
            var mod = TerraMod.Instance;
            if (mod == null || !mod.IsUsingItem || mod.SwingItem == null || !RangedArts.Flung(mod.SwingItem)) return;
            // The next one in the hand as the arm brings it back (none just after it was let go).
            if (mod.UseProgress < 0.45f || !RangedThrowAngle(out float deg)) return;
            var def = mod.SwingItem;
            if (!heldThrown.TryGetValue(def.Type, out var pic))
            {
                var s = TerraMod.ItemSprite(def.Type);
                pic = s == null ? null : Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                heldThrown[def.Type] = pic;
            }
            if (pic == null) return;
            float facing = mod.UseDirection;
            float screen = ScreenAngle(deg, facing);
            var hand = ShoulderPx(feet, facing) + Along(screen) * 11f;
            // A tall one (a knife, a javelin) along the arm, point out; a round one as it is.
            bool tall = pic.rect.height > pic.rect.width * 1.3f;
            float rot = tall ? screen + Mathf.PI / 2f : 0f;
            float scale = Equipment.ItemScale(def) * (pic.rect.width > 30f || pic.rect.height > 30f ? 0.75f : 1f);
            fx.Draw(pic, hand, new Color(L, L, L, 1f), rot, new Vector2(scale, scale), flipX: facing < 0f, overPlayer: true);
            DevThrownDraws++;
        }
    }
}
