using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>A cell's liquid (Tile.liquidType): water, or Hallownest's acid in the place of Terraria's lava.</summary>
    internal enum LiquidKind : byte
    {
        Water = 0,
        Acid = 1,
    }

    /// <summary>
    /// Terraria's liquid (Liquid.Update, UpdateLiquid, AddWater, DelWater, LiquidCheck) over one grid's cells: each cell holds
    /// 0..255 of one liquid; the cells that may still move are kept in a list and stepped every tick, falling first, then
    /// levelling with up to three cells either side; a cell that stays the same for 8 rounds leaves the list. Acid moves
    /// as lava does, every sixth step. Water and acid meeting turn to obsidian. Terraria's y runs down and the grid's up:
    /// "below" here is y - 1. Out of the grid, and past a joined gate, is solid.
    /// </summary>
    internal sealed class LiquidSim
    {
        const int MaxLiquid = 25000, Cycles = 10, KillAfter = 8;
        /// <summary>The chunk side the renderer redraws by.</summary>
        public const int ChunkCells = 16;

        readonly CellGrid g;
        readonly int w, h;
        byte[] liq, kind;
        bool[] checking, skip;
        int[] xs = new int[64], ys = new int[64], kills = new int[64], delays = new int[64];
        int count;
        readonly Queue<int> buffer = new Queue<int>();
        int wetCounter;
        readonly System.Random rand = new System.Random();

        readonly int cols;
        readonly bool[] dirty;
        public bool AnyDirty { get; private set; }

        /// <summary>Water met acid somewhere: the cell turned to obsidian (grid cell).</summary>
        public event System.Action<int, int> Merged;


        public LiquidSim(CellGrid grid)
        {
            g = grid;
            w = grid.Width;
            h = grid.Height;
            cols = (w + ChunkCells - 1) / ChunkCells;
            dirty = new bool[cols * ((h + ChunkCells - 1) / ChunkCells)];
        }

        public int Active => count + buffer.Count;

        void Ensure()
        {
            if (liq != null) return;
            g.EnsureLiquids();
            liq = g.LiquidArray;
            kind = g.LiquidKindArray;
            checking = new bool[w * h];
            skip = new bool[w * h];
        }

        // ------------------------------------------------------------------ cells

        bool Solid(int x, int y) => !g.InClip(x, y) || Cells.Blocks(g.Get(x, y));
        int L(int x, int y) => x >= 0 && y >= 0 && x < w && y < h ? liq[y * w + x] : 0;
        LiquidKind K(int x, int y) => x >= 0 && y >= 0 && x < w && y < h ? (LiquidKind)kind[y * w + x] : LiquidKind.Water;
        bool Is(int x, int y, LiquidKind k) => L(x, y) > 0 && K(x, y) == k;
        /// <summary>Open, holding some of the same liquid (the far neighbours a level reaches across).</summary>
        bool OpenSame(int x, int y, LiquidKind k) => !Solid(x, y) && L(x, y) > 0 && K(x, y) == k;

        void Put(int x, int y, int amount)
        {
            int i = y * w + x;
            if (liq[i] == amount) return;
            liq[i] = (byte)amount;
            Mark(x, y);
        }

        void PutKind(int x, int y, LiquidKind k)
        {
            int i = y * w + x;
            if (kind[i] == (byte)k) return;
            kind[i] = (byte)k;
            Mark(x, y);
        }

        public void Mark(int x, int y)
        {
            dirty[(y / ChunkCells) * cols + x / ChunkCells] = true;
            AnyDirty = true;
        }

        /// <summary>The chunks changed since the last call, cleared (the renderer's and the save's).</summary>
        public void TakeDirty(List<int> into)
        {
            if (!AnyDirty) return;
            AnyDirty = false;
            for (int i = 0; i < dirty.Length; i++)
                if (dirty[i]) { dirty[i] = false; into.Add(i); }
        }

        public int ChunkCols => cols;

        // ------------------------------------------------------------------ the outside world

        /// <summary>Liquid set into a cell from outside (a bucket, a load): it and its neighbours start moving.</summary>
        public void Set(int x, int y, int amount, LiquidKind k)
        {
            if (!g.InBounds(x, y)) return;
            Ensure();
            Put(x, y, Mathf.Clamp(amount, 0, 255));
            PutKind(x, y, amount > 0 ? k : LiquidKind.Water);
            Wake(x, y);
        }

        /// <summary>A cell and its four neighbours back into the list.</summary>
        public void Wake(int x, int y)
        {
            if (liq == null) return;
            AddWater(x, y);
            AddWater(x - 1, y);
            AddWater(x + 1, y);
            AddWater(x, y + 1);
            AddWater(x, y - 1);
        }

        /// <summary>
        /// The grid changed a cell: a block set into liquid deletes it there (Terraria's way); a cell opened lets the liquid
        /// around flow in.
        /// </summary>
        public void CellChanged(int x, int y)
        {
            if (liq == null || !g.InBounds(x, y)) return;
            if (Solid(x, y))
            {
                if (liq[y * w + x] > 0) { Put(x, y, 0); PutKind(x, y, LiquidKind.Water); }
            }
            Wake(x, y);
        }

        /// <summary>Every cell holding liquid back into the list (after a load).</summary>
        public void WakeAll()
        {
            if (!g.HasLiquids) return;
            Ensure();
            for (int i = 0; i < liq.Length; i++)
                if (liq[i] > 0)
                {
                    AddWater(i % w, i / w);
                    Mark(i % w, i / w);
                }
        }

        // ------------------------------------------------------------------ Liquid.UpdateLiquid

        /// <summary>One game tick (Terraria runs it 60 times a second).</summary>
        public void Step()
        {
            if (liq == null || count == 0 && buffer.Count == 0) return;
            wetCounter++;
            int per = MaxLiquid / Cycles;
            int from = per * (wetCounter - 1), to = per * wetCounter;
            if (wetCounter == Cycles) to = count;
            if (to > count)
            {
                to = count;
                wetCounter = Cycles;
            }
            for (int n = from; n < to; n++)
            {
                int i = ys[n] * w + xs[n];
                if (!skip[i]) Update(n);
                else skip[i] = false;
            }
            if (wetCounter < Cycles) return;
            wetCounter = 0;
            for (int n = count - 1; n >= 0; n--)
            {
                if (kills[n] < KillAfter) continue;
                int i = ys[n] * w + xs[n];
                if (liq[i] == 254) Put(xs[n], ys[n], 255);
                DelWater(n);
            }
            int room = MaxLiquid - count;
            while (room-- > 0 && buffer.Count > 0)
            {
                int b = buffer.Dequeue();
                checking[b] = false;
                AddWater(b % w, b / w);
            }
        }

        // ------------------------------------------------------------------ Liquid.AddWater

        void AddWater(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = y * w + x;
            if (checking[i] || liq[i] == 0 || Solid(x, y)) return;
            if (count >= MaxLiquid - 1)
            {
                buffer.Enqueue(i);
                return;
            }
            checking[i] = true;
            skip[i] = false;
            if (count == xs.Length)
            {
                System.Array.Resize(ref xs, count * 2);
                System.Array.Resize(ref ys, count * 2);
                System.Array.Resize(ref kills, count * 2);
                System.Array.Resize(ref delays, count * 2);
            }
            xs[count] = x;
            ys[count] = y;
            kills[count] = 0;
            delays[count] = 0;
            count++;
        }

        // ------------------------------------------------------------------ Liquid.Update

        void Update(int n)
        {
            int x = xs[n], y = ys[n], i = y * w + x;
            if (Solid(x, y))
            {
                kills[n] = 999;
                return;
            }
            int start = liq[i];
            if (start == 0)
            {
                kills[n] = 999;
                return;
            }
            var me = (LiquidKind)kind[i];
            if (me == LiquidKind.Acid)
            {
                LiquidCheck(x, y, LiquidKind.Acid);
                if (delays[n] < 5)
                {
                    delays[n]++;
                    return;
                }
                delays[n] = 0;
            }
            else
            {
                if (Is(x - 1, y, LiquidKind.Acid)) AddWater(x - 1, y);
                if (Is(x + 1, y, LiquidKind.Acid)) AddWater(x + 1, y);
                if (Is(x, y + 1, LiquidKind.Acid)) AddWater(x, y + 1);
                if (Is(x, y - 1, LiquidKind.Acid)) AddWater(x, y - 1);
            }

            // Down first, as much as the cell below takes.
            int by = y - 1;
            if (!Solid(x, by) && (L(x, by) <= 0 || K(x, by) == me) && L(x, by) < 255)
            {
                bool hold = false;
                int num = 255 - L(x, by);
                if (num > liq[i]) num = liq[i];
                if (num == 1 && liq[i] == 255) hold = true;
                if (!hold) Put(x, y, liq[i] - num);
                Put(x, by, L(x, by) + num);
                PutKind(x, by, me);
                AddWater(x, by);
                skip[by * w + x] = true;
                skip[i] = true;
                if (!hold)
                {
                    AddWater(x - 1, y);
                    AddWater(x + 1, y);
                }
            }

            // Then level with the sides.
            if (liq[i] > 0)
            {
                bool left = true, right = true, left2 = true, right2 = true;
                if (Solid(x - 1, y)) left = false;
                else if (L(x - 1, y) > 0 && K(x - 1, y) != me) left = false;
                else if (Solid(x - 2, y)) left2 = false;
                else if (L(x - 2, y) == 0) left2 = false;
                else if (K(x - 2, y) != me) left2 = false;
                if (Solid(x + 1, y)) right = false;
                else if (L(x + 1, y) > 0 && K(x + 1, y) != me) right = false;
                else if (Solid(x + 2, y)) right2 = false;
                else if (L(x + 2, y) == 0) right2 = false;
                else if (K(x + 2, y) != me) right2 = false;
                int bias = liq[i] < 3 ? -1 : 0;
                if (liq[i] > 250)
                {
                    left2 = false;
                    right2 = false;
                }
                if (left && right)
                {
                    if (left2 && right2)
                    {
                        if (OpenSame(x - 3, y, me) && OpenSame(x + 3, y, me))
                            Level(x, y, me, bias, Seven, true);
                        else
                            Level(x, y, me, bias, Five, true);
                    }
                    else if (left2) LevelLoose(x, y, me, bias, FourLeft);
                    else if (right2) LevelLoose(x, y, me, bias, FourRight);
                    else
                    {
                        float num = Round((L(x - 1, y) + L(x + 1, y) + liq[i] + bias) / 3f);
                        if (num == 254f && rand.Next(30) == 0) num = 255f;
                        PutKind(x - 1, y, me);
                        if (L(x - 1, y) != (int)num) { Put(x - 1, y, (int)num); AddWater(x - 1, y); }
                        PutKind(x + 1, y, me);
                        if (L(x + 1, y) != (int)num) { Put(x + 1, y, (int)num); AddWater(x + 1, y); }
                        Put(x, y, (int)num);
                    }
                }
                else if (left) LevelPair(x, y, -1, me, bias);
                else if (right) LevelPair(x, y, 1, me, bias);
            }

            if (liq[i] != start)
            {
                if (liq[i] == 254 && start == 255) kills[n]++;
                else
                {
                    AddWater(x, y + 1);
                    kills[n] = 0;
                }
            }
            else kills[n]++;
        }

        static float Round(float v) => (float)System.Math.Round(v);

        static readonly int[] Seven = { -1, 1, -2, 2, -3, 3 }, Five = { -1, 1, -2, 2 }, FourLeft = { -1, 1, -2 }, FourRight = { -1, 1, 2 };

        /// <summary>The 7- and 5-cell levels: every cell to the mean; the middle keeps its level when all the rest already had it and nothing is above.</summary>
        void Level(int x, int y, LiquidKind me, int bias, int[] offsets, bool keepIfSame)
        {
            int i = y * w + x;
            int sum = liq[i] + bias;
            foreach (int d in offsets) sum += L(x + d, y);
            int num = (int)Round(sum / (float)(offsets.Length + 1));
            int same = 0;
            foreach (int d in offsets)
            {
                PutKind(x + d, y, me);
                if (L(x + d, y) != num)
                {
                    Put(x + d, y, num);
                    AddWater(x + d, y);
                }
                else same++;
            }
            foreach (int d in offsets)
                if (L(x + d, y) != num || liq[i] != num) AddWater(x + d, y);
            if (!keepIfSame || same != offsets.Length || L(x, y + 1) <= 0) Put(x, y, num);
        }

        /// <summary>The 4-cell levels (one far neighbour): the near ones and the far one set whenever either differs.</summary>
        void LevelLoose(int x, int y, LiquidKind me, int bias, int[] offsets)
        {
            int i = y * w + x;
            int sum = liq[i] + bias;
            foreach (int d in offsets) sum += L(x + d, y);
            int num = (int)Round(sum / 4f);
            foreach (int d in offsets)
            {
                PutKind(x + d, y, me);
                if (L(x + d, y) != num || liq[i] != num)
                {
                    Put(x + d, y, num);
                    AddWater(x + d, y);
                }
            }
            Put(x, y, num);
        }

        /// <summary>One open side: the two cells to their mean.</summary>
        void LevelPair(int x, int y, int d, LiquidKind me, int bias)
        {
            int i = y * w + x;
            int num = (int)Round((L(x + d, y) + liq[i] + bias) / 2f);
            if (L(x + d, y) != num) Put(x + d, y, num);
            PutKind(x + d, y, me);
            if (liq[i] != num || L(x + d, y) != num) AddWater(x + d, y);
            Put(x, y, num);
        }

        // ------------------------------------------------------------------ Liquid.DelWater

        void DelWater(int n)
        {
            int x = xs[n], y = ys[n], i = y * w + x;
            const int b = 2;
            int self = liq[i];
            if (self < b)
            {
                Put(x, y, 0);
                if (L(x - 1, y) < b) { if (x > 0) Put(x - 1, y, 0); }
                else AddWater(x - 1, y);
                if (L(x + 1, y) < b) { if (x + 1 < w) Put(x + 1, y, 0); }
                else AddWater(x + 1, y);
            }
            else if (self < 20)
            {
                if ((L(x - 1, y) < self && !Solid(x - 1, y)) || (L(x + 1, y) < self && !Solid(x + 1, y)) || (L(x, y - 1) < 255 && !Solid(x, y - 1)))
                    Put(x, y, 0);
            }
            else if (L(x, y - 1) < 255 && !Solid(x, y - 1) && !Solid(x, y))
            {
                kills[n] = 0;
                return;
            }
            if (liq[i] < 250 && L(x, y + 1) > 0) AddWater(x, y + 1);
            if (liq[i] == 0) PutKind(x, y, LiquidKind.Water);
            else
            {
                if (L(x + 1, y) > 0 && L(x + 1, y) < 250 && !Solid(x + 1, y) && liq[i] != L(x + 1, y)) AddWater(x + 1, y);
                if (L(x - 1, y) > 0 && L(x - 1, y) < 250 && !Solid(x - 1, y) && liq[i] != L(x - 1, y)) AddWater(x - 1, y);
                if ((LiquidKind)kind[i] == LiquidKind.Acid) LiquidCheck(x, y, LiquidKind.Acid);
            }
            count--;
            checking[i] = false;
            xs[n] = xs[count];
            ys[n] = ys[count];
            kills[n] = kills[count];
            delays[n] = delays[count];
        }

        // ------------------------------------------------------------------ Liquid.LiquidCheck

        /// <summary>
        /// Two liquids meeting at a cell: what touches it from the sides or above, of the other kind, is taken; enough of it
        /// (24) and the cell turns to obsidian. Over a pool of the other kind, the cell and the one under it turn to it.
        /// </summary>
        void LiquidCheck(int x, int y, LiquidKind me)
        {
            if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1 || Solid(x, y)) return;
            int i = y * w + x;
            bool other(int ox, int oy) => L(ox, oy) > 0 && K(ox, oy) != me;
            if (other(x - 1, y) || other(x + 1, y) || other(x, y + 1))
            {
                int num = 0;
                if (other(x - 1, y)) { num += L(x - 1, y); Put(x - 1, y, 0); }
                if (other(x + 1, y)) { num += L(x + 1, y); Put(x + 1, y, 0); }
                if (other(x, y + 1)) { num += L(x, y + 1); Put(x, y + 1, 0); }
                if (num >= 24 && g.Get(x, y) == CellKind.Empty)
                {
                    Put(x, y, 0);
                    MakeObsidian(x, y);
                }
                return;
            }
            if (!other(x, y - 1) || g.Get(x, y - 1) != CellKind.Empty) return;
            if (liq[i] < 24)
            {
                Put(x, y, 0);
                PutKind(x, y, LiquidKind.Water);
                return;
            }
            Put(x, y, 0);
            Put(x, y - 1, 0);
            MakeObsidian(x, y - 1);
        }

        void MakeObsidian(int x, int y)
        {
            PutKind(x, y, LiquidKind.Water);
            Merged?.Invoke(x, y);
        }
    }
}
