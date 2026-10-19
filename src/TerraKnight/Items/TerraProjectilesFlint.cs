using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Flint (Dead Cells; sweep S1.F; Terraria's space, px y down): held, it is charged up
    /// over the player's head (1040, AI_198); let go, it comes down in a quick cut (1043, an AI_190
    /// slash in SwordFx, 1.78 times the damage); charged for half a second, it also sends a row of
    /// flame spikes (1041, AI_157) bursting out of the ground ahead, two tiles apart.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterFlint()
        {
            // P:9823-9835: the unseen holder, through everything.
            specs[ProjKind.FlintHold] = new Spec
            {
                Ai = Ai.Custom, W = 2, H = 2, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true, CanDamage = p => false, Look = Look.Invisible,
                Update = (t, p) => t.StepFlint(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:9836-9849, 15933-15950: a spike 200 px tall and 22 wide up from where it stands, four enemies, each every 25 ticks; harmless while it waits.
            specs[ProjKind.FlintSpike] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1041", W = 32, H = 32, Time = 3600, Penetrate = 4, TileCollide = false, OwnMove = true, AlphaStart = 1f,
                LocalCooldown = 25, Frames = 8, CanDamage = p => p.Ai0 >= 0f,
                Beam = p =>
                {
                    var v = TV(p);
                    var up = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down;
                    return (CenterPx(p), CenterPx(p) + up * 200f * p.Scale, 22f * p.Scale);
                },
                Update = (t, p) => StepFlintSpike(p), Draw = (t, fx, p, px) => DrawFlintSpike(fx, p, px), OnKill = (t, p) => { },
            };
        }

        /// <summary>
        /// AI_198 (P:53155-53233). Held: the player faces the cursor and keeps the swing at its
        /// start (itemAnimation pinned at the use's length plus up to 3), the charge chimes at 30
        /// ticks and orange motes (228) rise off the blade now and then. Let go: the cut (1043 at
        /// 1.78 times the damage) and, charged, the spikes; then the swing runs out over 5 ticks and
        /// the holder is gone after the use's length.
        /// </summary>
        void StepFlint(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.DeadCellsFlint);
            if (mod == null || !Holder(out var centre) || mod.Inventory.Current.Type != ItemType.DeadCellsFlint)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            int use = Mathf.Max(2, Mathf.RoundToInt(def.UseAnimation * 60f * Equipment.UseScale(def)));
            if (!mod.UseHeld && p.Ai0 == 0f)
            {
                bool charged = p.Ai1 >= 30f;
                p.Ai0 = 1f;
                p.Ai1 = 0f;
                SwordFx.Instance?.FlintSlash(centre, p.Dir, (int)(p.Damage * 1.78f), p.Knockback, p.Crit);
                if (charged) EmitFlintSpikes(p, centre);
                DevFlintCuts++;
            }
            if (p.Ai0 == 1f)
            {
                if (++p.Ai1 >= use)
                {
                    Kill(p);
                    return;
                }
                // SetDummyItemTime(Remap(ai[1], 0, 5, useAnimation, 0)): the swing from its start to its end in 5 ticks.
                mod.HoldSwing(def, Mathf.Clamp01(p.Ai1 / 5f), p.Dir);
                return;
            }
            p.Dir = AimPx().x >= centre.x ? 1f : -1f;
            p.Ai1++;
            if (p.Ai1 == 30f) Sound("Custom/deadcells_flint_charge", 0.5f);
            mod.HoldSwing(def, 0f, p.Dir);
            if (Random.Range(0, 10) == 0)
            {
                // itemLocation + (41 dir, -41) turned by itemRotation: the blade's far end.
                var hand = TerraParticles.ToPx(mod.HandWorld);
                var tip = hand + RotateBy(new Vector2(41f * p.Dir, -41f), mod.ItemRotation) + Random.insideUnitCircle * 12f;
                int i = PointDust(tip, 228, new Vector2(0f, Random.value * -8f - 2f), 127, 1.3f + Random.value);
                if (i >= 0)
                {
                    D(i).Color = FxBatch.C(255, 180, 0, 0);
                    D(i).NoGravity = true;
                }
            }
        }

        /// <summary>
        /// AI_198_Flint_EmitSpikes (P:53235-53280): from 30 px ahead of the player, the ground burst
        /// (ParticleOrchestra DeadCellsFlint) and then up to thirteen spikes, each on the surface
        /// found within 10 tiles up or down of the last, two tiles apart, 2 ticks later each and a
        /// little bigger (1 + 0.04 k + up to 0.2), stopping where no ground is found.
        /// </summary>
        void EmitFlintSpikes(Proj p, Vector2 centre)
        {
            int dir = p.Dir < 0f ? -1 : 1;
            var start = centre + new Vector2(dir * 30f, 0f);
            FlintBurst(start + new Vector2(16f * dir, 16f));
            int x = Mathf.FloorToInt(start.x / 16f), y = Mathf.FloorToInt(start.y / 16f);
            for (int k = 0; k <= 12; k++)
            {
                if (!FindSurface(x, ref y)) break;
                var at = new Vector2(x * 16f + 8f, y * 16f);
                var v = RotateBy(new Vector2(dir, -10f), Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.025f);
                var spike = MeleeChild(ProjKind.FlintSpike, at, v, p.Damage, p.Knockback, p.Crit);
                if (spike != null)
                {
                    spike.Ai0 = -(k * 2);
                    spike.Ai1 = 1f + k * 0.04f + Random.value * 0.2f;
                }
                DevFlintSpikes++;
                x += dir * 2;
            }
        }

        /// <summary>A walkable tile (solid or a platform) at Terraria tile (i, j).</summary>
        static bool Walkable(int i, int j)
        {
            var k = TerraWorld.Get(i, -j - 1);
            return TerraWorld.IsSolid(i, -j - 1) || TerraKnight.World.Cells.IsPlatform(k);
        }

        /// <summary>FindBlockSurface (P:53282-53310): up out of solid ground (10 at most), then down to the first walkable tile (10 at most).</summary>
        static bool FindSurface(int x, ref int y)
        {
            for (int i = 0; i < 10 && TerraWorld.IsSolid(x, -y - 1); i++) y--;
            for (int j = 0; j < 10 && !Walkable(x, y); j++) y++;
            return Walkable(x, y);
        }

        /// <summary>
        /// ParticleOrchestrator.Spawn_DeadCellsFlint (PO:1786-1895): every open block top within 40 px
        /// throws its dust up, and one time in two a smoke puff (gore 61-63) with an orange streak
        /// rising off it; if any ground was there: the release sound, thirty orange motes (228)
        /// flung up, and the monk staff's ground impact.
        /// </summary>
        static void FlintBurst(Vector2 at)
        {
            var pp = TerraParticles.Instance;
            bool any = false;
            int i0 = Mathf.FloorToInt((at.x - 32f) / 16f) - 3, i1 = Mathf.FloorToInt((at.x + 32f) / 16f) + 3;
            int j0 = Mathf.FloorToInt((at.y - 160f) / 16f) - 3, j1 = Mathf.FloorToInt((at.y + 160f) / 16f) + 3;
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (Vector2.Distance(at, new Vector2(i * 16f, j * 16f)) > 40f) continue;
                    if (!TerraWorld.IsSolid(i, -j - 1) || TerraWorld.IsSolid(i, -j)) continue;
                    any = true;
                    if (pp == null) continue;
                    int type = TerraMod.TileDustType(TerraWorld.Get(i, -j - 1));
                    var tl = new Vector2(i * 16f, j * 16f);
                    for (int k = 0; k < 3; k++)
                    {
                        int d = pp.NewDust(tl, 16, 16, type);
                        if (d < 0) continue;
                        pp[d].Velocity.y -= 7.5f;
                        pp[d].Velocity.y *= Random.value * 0.75f;
                        pp[d].Scale += 0.09f;
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        int d = pp.NewDust(tl, 16, 16, type);
                        if (d < 0) continue;
                        pp[d].Velocity.y -= 4f;
                        pp[d].Velocity.y *= Random.value * 0.75f;
                    }
                    if (Random.Range(0, 2) == 0) continue;
                    float off = Mathf.Abs(Mathf.FloorToInt(at.x / 16f) - i) / 20f;
                    var gpos = new Vector2(i * 16f + 20f, j * 16f);
                    // Gore.NewGoreDirect's own scatter, then its rise: velocity.Y -= 0.1 + 1.5 + 3 off, times a random share.
                    float vy = (-Random.Range(10, 31) * 0.1f - (0.1f + 1.5f + off * 3f)) * Random.value;
                    pp.NewGore(gpos, new Vector2(Random.Range(-20, 21) * 0.1f, vy), 61 + Random.Range(0, 3), 1f - 0.45f + off * 0.5f, exact: true);
                    FxParticles.DeadCellsFlintStreak(gpos);
                }
            if (!any) return;
            Sound("Custom/deadcells_flint_release", 0.5f);
            for (int m = 0; m < 30; m++)
            {
                var v = Vector2.Scale(Random.insideUnitCircle, new Vector2(5f, 3f));
                if (v.y > 0f) v.y = -v.y;
                PointDust(at + new Vector2(Random.Range(-7f, 7f), 0f), 228, v, 0, 0.7f + 0.8f * Random.value);
            }
            Sound("Custom/dd2_monk_staff_ground_impact_" + Random.Range(0, 3));
        }

        /// <summary>
        /// AI_157 for 1041 (P:52299-52398): it waits unseen (2 ticks per spike down the row), then
        /// grows in over 4 ticks to its size and shrinks away from 14, gone at 19; its first frame
        /// for 6 ticks, then one every 2; as it appears three motes (228) up along it and two
        /// more, and a soft whoosh.
        /// </summary>
        static void StepFlintSpike(Proj p)
        {
            bool growing = p.Ai0 < 4f && p.Ai0 >= 0f, fading = p.Ai0 >= 14f, done = p.Ai0 >= 19f;
            p.Ai0++;
            if (p.Ai0 < 0f)
            {
                p.Alpha = 1f;
                p.Scale = 0.1f * p.Ai1;
            }
            if (p.Ai0 >= 0f && ++p.FrameCounter >= (p.Frame == 0 ? 6 : 2))
            {
                p.FrameCounter = 0;
                p.Frame++;
            }
            var v = TV(p);
            var c = CenterPx(p);
            if (p.LocalAi0 == 0f && p.Ai0 >= 0f)
            {
                p.LocalAi0 = 1f;
                p.TRot = ToRot(v);
                var along = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
                for (int i = 0; i < 3; i++)
                {
                    int d = PointDust(c + Random.insideUnitCircle * 24f, 228, v * 0.25f * Mathf.Lerp(0.2f, 0.7f, Random.value));
                    if (d < 0) continue;
                    ref var dd = ref D(d);
                    dd.Velocity += Random.insideUnitCircle * 0.5f * 0.5f + along * Random.value * 2f;
                    dd.Position += along * Random.value * 60f;
                    dd.Scale = 0.8f + Random.value * 0.5f;
                }
                for (int j = 0; j < 2; j++)
                {
                    int d = PointDust(c + Random.insideUnitCircle * 24f, 228, Random.insideUnitCircle * 2f + v * 0.25f * Mathf.Lerp(0.2f, 0.5f, Random.value));
                    if (d < 0) continue;
                    ref var dd = ref D(d);
                    dd.Velocity += Random.insideUnitCircle * 0.5f * 0.5f;
                    dd.Scale = 0.8f + Random.value * 0.5f;
                    dd.Velocity += along * Random.value * 2f;
                    dd.Position += along * Random.value * 60f;
                    dd.FadeIn = 1f;
                }
                Sound("Custom/deadcells_flint_wave", 0.1f);
            }
            float opacity = 1f - p.Alpha;
            if (growing)
            {
                opacity = Mathf.Min(1f, opacity + 0.1f);
                p.Scale = opacity * p.Ai1;
            }
            if (fading) opacity = Mathf.Max(0f, opacity - 0.2f);
            p.Alpha = 1f - opacity;
            if (done) TerraProjectiles.Instance?.Kill(p);
        }

        /// <summary>M:31845-31862, P:80762-80768: its frame of eight pivoting 16 px from its foot, full white (unseen while waiting).</summary>
        static void DrawFlintSpike(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Ai0 < 0f || p.Alpha >= 1f) return;
            var whole = FxBatch.Whole("Projectile_1041");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 8;
            int frame = Mathf.Clamp(p.Frame, 0, 7);
            fx.Draw(FxBatch.Frame("Projectile_1041", 0, frame * fh, w, fh, 16f, fh / 2f), px, Color.white, p.TRot, p.Scale);
        }

        /// <summary>The Flint's holder: at the player's centre (world), facing a way.</summary>
        public void HoldFlint(Vector2 from, Vector2 vel, int damage, float knockback, float crit, float face)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(ProjKind.FlintHold, from, vel, damage, knockback, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.Dir = face;
        }

        public static int DevFlintCuts, DevFlintSpikes;
    }
}
