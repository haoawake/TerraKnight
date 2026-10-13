using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Drills and chainsaws (sweep R2; Terraria's space, px y down): AI_020's head held out at the
    /// cursor shootSpeed px from the player while the button is held, buzzing (drills Item190, saws
    /// Item189), shaking a little and smoking; the mining is the item's own, a stroke every useTime.
    /// The Butcher's Chainsaw throws sparks (504) off what it cuts; the Laser Drill (445, AI_075)
    /// reaches for the block under the cursor within its range on a twin green and blue beam.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterDrills()
        {
            // DefaultToDrillOrChainsaw (P:1396-1440, 1948, 3013-3095, 3353, 4532, 4992-5032, 5789): through everything, only what the player can see.
            void Drill(ProjKind k, int id, int w, int h, float scale, string glow = null, int frames = 1) => specs[k] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_" + id, W = w, H = h, Scale = scale, Time = 60, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                OwnerHitCheck = true, Frames = frames, Glow = glow, Update = (t, p) => t.StepDrill(p), Draw = (t, fx, p, px) => DrawDrill(fx, p, px), OnKill = (t, p) => { },
            };
            Drill(ProjKind.CobaltChainsawProj, 57, 18, 18, 1f);
            Drill(ProjKind.MythrilChainsawProj, 58, 18, 18, 1.08f);
            Drill(ProjKind.CobaltDrillProj, 59, 22, 22, 0.9f);
            Drill(ProjKind.MythrilDrillProj, 60, 22, 22, 0.9f);
            Drill(ProjKind.AdamantiteChainsawProj, 61, 18, 18, 1.16f);
            Drill(ProjKind.AdamantiteDrillProj, 62, 22, 22, 0.9f);
            Drill(ProjKind.DraxProj, 107, 22, 22, 1.1f);
            Drill(ProjKind.PalladiumDrillProj, 213, 22, 22, 0.92f);
            Drill(ProjKind.PalladiumChainsawProj, 214, 18, 18, 1f);
            Drill(ProjKind.OrichalcumDrillProj, 216, 22, 22, 0.93f);
            Drill(ProjKind.OrichalcumChainsawProj, 217, 18, 18, 1.12f);
            Drill(ProjKind.TitaniumDrillProj, 219, 22, 22, 0.95f);
            Drill(ProjKind.TitaniumChainsawProj, 220, 18, 18, 1.2f);
            Drill(ProjKind.ChlorophyteDrillProj, 223, 22, 22, 1f);
            Drill(ProjKind.ChlorophyteChainsawProj, 224, 18, 18, 1.1f);
            Drill(ProjKind.ChlorophyteJackhammerProj, 252, 18, 18, 1.1f, frames: 4);
            Drill(ProjKind.SawtoothSharkProj, 369, 22, 22, 1f);
            Drill(ProjKind.VortexDrillProj, 428, 26, 54, 1f, "Glow_3");
            Drill(ProjKind.NebulaDrillProj, 430, 30, 54, 1f, "Glow_8");
            Drill(ProjKind.SolarFlareDrillProj, 432, 30, 54, 1f);
            Drill(ProjKind.StardustDrillProj, 609, 30, 54, 1f, "Glow_180");
            // P:5789-5795: the Butcher's Chainsaw, any enemy every 10 ticks for all of them, two frames.
            Drill(ProjKind.ButchersChainsawProj, 509, 22, 22, 1.2f, frames: 2);
            specs[ProjKind.ButchersChainsawProj].TypeCooldown = 10;
            specs[ProjKind.ButchersChainsawProj].OnHit = (t, p, hm) => t.ButcherSparks(p);
            // P:5164-5176: the Laser Drill's head, 10×10, through everything, only what the player can see.
            specs[ProjKind.LaserDrillProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_445", W = 10, H = 10, Time = 60, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true, OwnerHitCheck = true,
                Update = (t, p) => t.StepLaserDrill(p), Draw = (t, fx, p, px) => DrawLaserDrill(fx, p, px), OnKill = (t, p) => { },
            };
            // P:23765-23803: the Butcher's sparks, as the Wand of Sparking's (954) but melee, any enemy every 10 ticks for all of them.
            specs[ProjKind.ButcherSpark] = new Spec { Ai = Ai.Spark, Look = Look.Invisible, Penetrate = 2, Time = 3600, Dust = 6, W = 10, H = 10, TypeCooldown = 10 };
        }

        static ItemType DrillItem(ProjKind k) => k switch
        {
            ProjKind.CobaltChainsawProj => ItemType.CobaltChainsaw, ProjKind.MythrilChainsawProj => ItemType.MythrilChainsaw, ProjKind.CobaltDrillProj => ItemType.CobaltDrill,
            ProjKind.MythrilDrillProj => ItemType.MythrilDrill, ProjKind.AdamantiteChainsawProj => ItemType.AdamantiteChainsaw, ProjKind.AdamantiteDrillProj => ItemType.AdamantiteDrill,
            ProjKind.DraxProj => ItemType.Drax, ProjKind.PalladiumDrillProj => ItemType.PalladiumDrill, ProjKind.PalladiumChainsawProj => ItemType.PalladiumChainsaw,
            ProjKind.OrichalcumDrillProj => ItemType.OrichalcumDrill, ProjKind.OrichalcumChainsawProj => ItemType.OrichalcumChainsaw, ProjKind.TitaniumDrillProj => ItemType.TitaniumDrill,
            ProjKind.TitaniumChainsawProj => ItemType.TitaniumChainsaw, ProjKind.ChlorophyteDrillProj => ItemType.ChlorophyteDrill, ProjKind.ChlorophyteChainsawProj => ItemType.ChlorophyteChainsaw,
            ProjKind.ChlorophyteJackhammerProj => ItemType.ChlorophyteJackhammer, ProjKind.SawtoothSharkProj => ItemType.SawtoothShark, ProjKind.VortexDrillProj => ItemType.VortexDrill,
            ProjKind.NebulaDrillProj => ItemType.NebulaDrill, ProjKind.SolarFlareDrillProj => ItemType.SolarFlareDrill, ProjKind.StardustDrillProj => ItemType.StardustDrill,
            ProjKind.ButchersChainsawProj => ItemType.ButchersChainsaw, _ => ItemType.LaserDrill,
        };

        /// <summary>
        /// AI_020 (P:25804-25904): held while the button is; the buzz every 15 ticks for a drill,
        /// 20 for a saw; out shootSpeed × its size toward the cursor, turned along it, the player
        /// facing it; it shakes ±3% across each tick and puffs smoke (31) one tick in six. The
        /// Jackhammer runs four frames of 4 ticks, the Butcher's Chainsaw two of 2.
        /// </summary>
        void StepDrill(Proj p)
        {
            var item = DrillItem(p.Kind);
            var def = ItemDb.Get(item);
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || !Channel(item))
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 60;
            if (p.Spec.Frames > 1)
            {
                int every = p.Kind == ProjKind.ButchersChainsawProj ? 2 : 4;
                if (++p.FrameCounter >= every)
                {
                    p.FrameCounter = 0;
                    if (++p.Frame >= p.Spec.Frames) p.Frame = 0;
                }
            }
            if (--p.SoundDelay <= 0)
            {
                bool drill = def.PickPower > 0;
                Sound(drill ? "Item_190" : "Item_189");
                p.SoundDelay = drill ? 15 : 20;
            }
            var aim = AimPx() - centre;
            aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(mod.UseDirection, 0f);
            var v = aim * def.ShootSpeed * p.Scale;
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = ToRot(v) + 1.57f;
            mod.KeepUsing(def, Flip(v));
            // UpdatePosition moves it on by its velocity after the shake.
            v.x *= 1f + Random.Range(-3, 4) * 0.01f;
            p.Vel = Flip(v);
            p.Pos = TerraParticles.ToWorld(centre + v);
            if (Random.Range(0, 6) != 0) return;
            int w = (int)(p.Spec.W * p.Scale), h = (int)(p.Spec.H * p.Scale);
            var tl = centre - new Vector2(w / 2f, h / 2f) + v * Random.Range(6, 10) * 0.1f;
            int d = BoxDust(tl, w, h, 31, 0f, 0f, 80, 1.4f);
            if (d < 0) return;
            D(d).Position.x -= 4f;
            D(d).NoGravity = true;
            D(d).Velocity *= 0.2f;
            D(d).Velocity.y = -Random.Range(7, 13) * 0.15f;
        }

        /// <summary>Main.DrawProj's generic rule, lit, mirrored as it faces; a glowmask added over it.</summary>
        static void DrawDrill(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / p.Spec.Frames;
            int w = (int)(p.Spec.W * p.Spec.Scale), h = (int)(p.Spec.H * p.Spec.Scale);
            bool flip = p.Dir < 0f;
            var at = px + new Vector2((tw - w) * 0.5f, 0f);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * fh, tw, fh, tw * 0.5f, h * 0.5f), at, new Color(L, L, L, 1f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            if (p.Spec.Glow != null)
                fx.Draw(FxBatch.Frame(p.Spec.Glow, 0, p.Frame * fh, tw, fh, tw * 0.5f, h * 0.5f), at, new Color(250f / 255f, 250f / 255f, 250f / 255f, 0f), p.TRot, p.Scale,
                    flipX: flip, overPlayer: true);
        }

        /// <summary>P:13219-13230: two to five sparks off the cut, half its damage, a fifth of its knockback, flung its way.</summary>
        void ButcherSparks(Proj p)
        {
            var c = CenterPx(p);
            var v = TV(p);
            for (int k = Random.Range(2, 6); k > 0; k--)
            {
                var dir = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101)) + v * 3f;
                dir = dir.normalized * Random.Range(35, 81) * 0.1f;
                MeleeChild(ProjKind.ButcherSpark, c, dir, (int)(p.Damage * 0.5f), p.Knockback * 0.2f, p.Crit);
                DevButcherSparks++;
            }
        }

        // ------------------------------------------------------------------ 445: the Laser Drill (AI_075)

        /// <summary>
        /// AI_075 for 445 (P:67285-67401): its whine every 23 ticks; a green-to-blue light pulsing
        /// over a second; gone with the button. Every few ticks it picks where to reach: the block
        /// the player is mining if there is one (then not again for 2 ticks), else toward the
        /// cursor, eased 70% from where it was, at least 30 px out and kept within the player's
        /// tile reach plus its own (11 more tiles).
        /// </summary>
        void StepLaserDrill(Proj p)
        {
            var def = ItemDb.Get(ItemType.LaserDrill);
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || !Channel(ItemType.LaserDrill))
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 60;
            if (--p.SoundDelay <= 0)
            {
                Sound("Item_132");
                p.SoundDelay = 23;
            }
            if (++p.LocalAi0 >= 60f) p.LocalAi0 = 0f;
            var v = TV(p);
            if (p.LocalAi1 > 0f) p.LocalAi1--;
            else
            {
                var to = AimPx() - centre;
                var cell = CellGrid.WorldToCell(TerraParticles.ToWorld(AimPx()));
                if (TerraWorld.IsSolid(cell.x, cell.y))
                {
                    to = TerraParticles.ToPx(CellGrid.CellCenter(cell.x, cell.y)) - centre;
                    p.LocalAi1 = 2f;
                }
                to = Vector2.Lerp(to, v, 0.7f);
                if (to.sqrMagnitude < 1e-6f) to = Vector2.down;
                if (to.magnitude < 30f) to = to.normalized * 30f;
                // Player.tileRangeX 5 and tileRangeY 4, plus the item's tileBoost, 12 px of slack.
                int boost = def.TileBoost;
                float x0 = (-5 - boost + 1) * 16 - 12, x1 = (5 + boost - 1) * 16 + 12, y0 = (-4 - boost) * 16 - 12, y1 = (4 + boost - 1) * 16 + 12;
                if (to.x < x0 || to.y < y0 || to.x > x1 || to.y > y1)
                {
                    var n = to.normalized;
                    float k = -1f;
                    if (n.x < 0f && (x0 / n.x < k || k == -1f)) k = x0 / n.x;
                    if (n.x > 0f && (x1 / n.x < k || k == -1f)) k = x1 / n.x;
                    if (n.y < 0f && (y0 / n.y < k || k == -1f)) k = y0 / n.y;
                    if (n.y > 0f && (y1 / n.y < k || k == -1f)) k = y1 / n.y;
                    to = n * k;
                }
                v = to;
            }
            p.Vel = Flip(v);
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            p.Pos = TerraParticles.ToWorld(centre + v);
            mod.KeepUsing(def, Flip(v));
            float pulse = p.LocalAi0 / 60f;
            if (pulse > 0.5f) pulse = 1f - pulse;
            var light = Color.Lerp(new Color(0f, 1f, 0.7f), new Color(0f, 0.7f, 1f), 1f - pulse * 2f) * 0.5f;
            if (v.magnitude >= 5f) FxBatch.Instance?.Light(centre + v, light, 3f);
        }

        /// <summary>
        /// Main.DrawProj for 445 (M:34750-34833): two thin beams (lime and cornflower, half added)
        /// from 25 px out to the head, side by side and turning round each other; the drill itself
        /// at the hand with its glowmask (Glow_39); two more beams; the head, its glow (Glow_40)
        /// taking over as the pulse falls.
        /// </summary>
        static void DrawLaserDrill(FxBatch fx, Proj p, Vector2 px)
        {
            if (!Holder(out var player)) return;
            var d = px - player;
            float len = d.magnitude, rot = Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f;
            var n = len > 1e-4f ? d / len : Vector2.down;
            const float back = -5f, start = back + 30f;
            var mid = Vector2.Lerp(px, player + n * start, 0.5f);
            float spin = p.LocalAi0 / 60f * Mathf.PI;
            var corner = new Vector2(Mathf.Sin(spin), -Mathf.Cos(spin));
            Vector2 Corner(int k) => RotateBy(corner, Mathf.PI / 2f * k);
            var pixel = FxBatch.Frame("MagicPixel", 0, 0, 1, 1, 0.5f, 0.5f);
            var lime = FxBatch.C(50, 205, 50, 128) * 0.5f;
            var blue = FxBatch.C(100, 149, 237, 128) * 0.5f;
            void Beams(int from)
            {
                if (len <= start) return;
                for (int k = from; k < from + 2; k++)
                {
                    var off = RotateBy(new Vector2(Corner(k).x, 0f), rot) * 4f;
                    fx.Draw(pixel, mid + off, k % 2 == 0 ? lime : blue, rot, new Vector2(2f, len - start), overPlayer: true);
                }
            }
            Beams(0);
            var item = FxBatch.Whole("Item_2798");
            if (item != null)
            {
                bool flip = p.Dir < 0f;
                float iw = item.rect.width, ih = item.rect.height;
                // Its back corner at the hand, turned along the beam (the art points the other way when not mirrored); XNA's origin (w when mirrored) is Unity's 0 either way.
                float irot = p.TRot + Mathf.PI / 2f + (flip ? 0f : Mathf.PI);
                var spr = FxBatch.Frame("Item_2798", 0, 0, (int)iw, (int)ih, 0f, ih / 2f + 1f);
                var glow = FxBatch.Frame("Glow_39", 0, 0, (int)iw, (int)ih, 0f, ih / 2f + 1f);
                var at = player + n * back;
                fx.Draw(spr, at, new Color(L, L, L, 1f), irot, 1f, flipX: flip, overPlayer: true);
                fx.Draw(glow, at, new Color(1f, 1f, 1f, 0f), irot, 1f, flipX: flip, overPlayer: true);
            }
            Beams(2);
            float pulse = p.LocalAi0 / 60f;
            if (pulse > 0.5f) pulse = 1f - pulse;
            var head = FxBatch.Whole("Projectile_445");
            if (head != null) fx.Draw(head, px, new Color(1f, 1f, 1f, 0.5f) * (pulse * 2f), p.TRot, p.Scale, overPlayer: true);
            var headGlow = FxBatch.Whole("Glow_40");
            if (headGlow != null) fx.Draw(headGlow, px, new Color(1f, 1f, 1f, 0.5f) * ((0.5f - pulse) * 2f), p.TRot, p.Scale, overPlayer: true);
        }

        public static int DevButcherSparks;
    }
}
