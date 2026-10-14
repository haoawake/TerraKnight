using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Ice Rod's bolt (80, AI_022, Terraria's space, px y down, per update): it flies to the
    /// cell the cursor was on (or the nearest open one beside it) and freezes there into a block
    /// of magic ice (tile 127) that melts after 15 seconds; a bolt that can't place its block
    /// tumbles away and shatters within a second.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterIce()
        {
            specs[ProjKind.IceRodBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_80", W = 16, H = 16, Time = 3600, TileCollide = false,
                CanDamage = p => p.LocalAi1 == 0f || p.LocalAi1 == 3f,
                Update = (t, p) => t.StepIceRodBolt(p), Draw = (t, fx, p, px) => DrawIceRodBolt(fx, p, px), OnKill = (t, p) => KillIceRodBolt(p),
            };
        }

        /// <summary>An Ice Rod bolt for a cell (the grid's).</summary>
        public void ShootIceRodBolt(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, Vector2Int cell)
        {
            var p = Spawn(ProjKind.IceRodBolt, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = cell.x;
            p.Ai1 = cell.y;
        }

        /// <summary>The 8 px core of the bolt meets a block other than magic ice (Main.tileSolid, not tileSolidTop).</summary>
        static bool IceCoreBlocked(Proj p)
        {
            const float c = CellGrid.CellSize, half = 4f * Px;
            int x0 = Mathf.FloorToInt((p.Pos.x - half) / c), x1 = Mathf.FloorToInt((p.Pos.x + half) / c);
            int y0 = Mathf.FloorToInt((p.Pos.y - half) / c), y1 = Mathf.FloorToInt((p.Pos.y + half) / c);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var k = TerraWorld.Get(x, y);
                    if (k != CellKind.MagicIce && Cells.Blocks(k)) return true;
                }
            return false;
        }

        /// <summary>WorldGen.PlaceTile for 127: an open cell, not in the player or an enemy.</summary>
        static bool CanFreeze(int x, int y)
        {
            if (!TerraWorld.InWorld(x, y) || TerraWorld.Get(x, y) != CellKind.Empty) return false;
            if (BossRooms.Locked(x, y)) return false;
            return Physics2D.OverlapBox(CellGrid.CellCenter(x, y), Vector2.one * (CellGrid.CellSize * 0.9f), 0f, (1 << 9) | (1 << 11)) == null;
        }

        /// <summary>WorldGen.KillTile for the magic ice: its shatter (Item27) and frost.</summary>
        static void Thaw(int x, int y)
        {
            if (TerraWorld.Get(x, y) != CellKind.MagicIce) return;
            TerraWorld.Set(x, y, CellKind.Empty);
            Sound("Item_27");
            var corner = TerraParticles.ToPx(new Vector2(x * CellGrid.CellSize, (y + 1) * CellGrid.CellSize));
            for (int k = 0; k < 4; k++) TerraParticles.Instance?.NewDust(corner, 16, 16, 67);
            FallingSand.Instance?.Check(x, y + 1);
        }

        /// <summary>
        /// AI_022: in flight it spins, sheds frost, and dies in a block. Its cell, if solid, moves
        /// to an open one beside it (along the flight's main axis first, towards its side); on it or
        /// past it on both axes it freezes there: unseen and harmless, it keeps the ice 899 updates
        /// and thaws it. Unable to, it tumbles and shatters (60 updates at most).
        /// </summary>
        void StepIceRodBolt(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            var tl = c - new Vector2(8f, 8f);
            if (p.LocalAi1 == 3f)
            {
                if (p.TimeLeft > 60) p.TimeLeft = 60;
                p.TRot += v.x > 0f ? 0.3f : -0.3f;
                if (IceCoreBlocked(p))
                {
                    Kill(p);
                    return;
                }
                int i = BoxDust(tl, 16, 16, 67);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 0.3f;
                }
                return;
            }
            if (p.LocalAi1 >= 1f)
            {
                if (p.LocalAi1 == 1f)
                    for (int k = 0; k < 10; k++)
                    {
                        int i = BoxDust(tl, 16, 16, 67, 0f, 0f, 0, 1.1f);
                        if (i < 0) continue;
                        D(i).NoGravity = true;
                        D(i).Velocity *= 1.3f;
                    }
                else if (Random.Range(0, 30) == 0)
                {
                    int i = BoxDust(tl, 16, 16, 67, 0f, 0f, 100);
                    if (i >= 0) D(i).Velocity *= 0.2f;
                }
                p.LocalAi1 = 2f;
                var at = CellGrid.WorldToCell(p.Pos);
                if (TerraWorld.Get(at.x, at.y) != CellKind.MagicIce)
                {
                    Kill(p);
                    return;
                }
                if (--p.LocalAi0 <= -900f)
                {
                    Thaw(at.x, at.y);
                    Kill(p);
                }
                return;
            }
            if (IceCoreBlocked(p))
            {
                Kill(p);
                return;
            }
            int d = BoxDust(tl, 16, 16, 67);
            if (d >= 0)
            {
                D(d).NoGravity = true;
                D(d).Velocity *= 0.3f;
            }
            int tx = (int)p.Ai0, ty = (int)p.Ai1;
            bool Open(int x, int y) => TerraWorld.InWorld(x, y) && !TerraWorld.IsSolid(x, y);
            // The cell's centre in world units; the grid's y runs up, Terraria's down.
            var centre = CellGrid.CellCenter(tx, ty);
            bool above = p.Pos.y > centre.y, leftOf = p.Pos.x < centre.x;
            if (TerraWorld.InWorld(tx, ty) && TerraWorld.IsSolid(tx, ty))
            {
                if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
                {
                    if (above && Open(tx, ty + 1)) ty++;
                    else if (Open(tx, ty - 1)) ty--;
                    else if (Open(tx, ty + 1)) ty++;
                    else if (leftOf && Open(tx - 1, ty)) tx--;
                    else if (Open(tx + 1, ty)) tx++;
                    else if (Open(tx - 1, ty)) tx--;
                }
                else if (leftOf && Open(tx - 1, ty)) tx--;
                else if (Open(tx + 1, ty)) tx++;
                else if (Open(tx - 1, ty)) tx--;
                else if (above && Open(tx, ty + 1)) ty++;
                else if (Open(tx, ty - 1)) ty--;
                else if (Open(tx, ty + 1)) ty++;
            }
            p.TRot += v.x > 0f ? 0.3f : -0.3f;
            var here = CellGrid.WorldToCell(p.Pos);
            // Terraria's tile y grows downwards, the grid's upwards: at or past it going down is y ≤ ty here.
            bool reached = here.x == tx && here.y == ty
                || ((v.x <= 0f && here.x <= tx) || (v.x >= 0f && here.x >= tx)) && ((v.y <= 0f && here.y >= ty) || (v.y >= 0f && here.y <= ty));
            if (!reached) return;
            if (CanFreeze(tx, ty))
            {
                TerraWorld.Set(tx, ty, CellKind.MagicIce);
                p.Damage = 0;
                p.LocalAi1 = 1f;
                p.LocalAi0 = -1f;
                p.Vel = Vector2.zero;
                p.Pos = CellGrid.CellCenter(tx, ty);
            }
            else p.LocalAi1 = 3f;
        }

        /// <summary>Its sprite, lit, spinning, while it flies or tumbles; a blue light.</summary>
        static void DrawIceRodBolt(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.LocalAi1 == 1f || p.LocalAi1 == 2f) return;
            fx.Light(px, new Color(0f, 0.4f, 0.5f), 2.5f);
            fx.Draw(Sprite("Projectile_80"), px, new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        /// <summary>P:77107: a bolt still in flight shatters (Item27, frost); the ice it sits in goes with it.</summary>
        static void KillIceRodBolt(Proj p)
        {
            var c = CenterPx(p);
            if (p.LocalAi1 == 0f || p.LocalAi1 == 3f)
            {
                Sound("Item_27");
                for (int k = 0; k < 10; k++) BoxDust(c - new Vector2(8f, 8f), 16, 16, 67);
            }
            var at = CellGrid.WorldToCell(p.Pos);
            Thaw(at.x, at.y);
        }
    }
}
