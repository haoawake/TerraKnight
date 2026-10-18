using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's falling sand: a sand block with nothing under it turns into a falling sand
    /// ball (projectile 31, aiStyle 10: gravity 0.41 a tick up to 10 px/tick) that becomes a
    /// block again where it lands. Removing the cell under a column of sand drops the whole
    /// column, one block after another.
    /// </summary>
    internal sealed class FallingSand : MonoBehaviour
    {
        const float Px = Effects.TerraParticles.Px;

        public static FallingSand Instance { get; private set; }

        sealed class Ball
        {
            public Vector2 Pos;
            public float Vy;
            public SpriteRenderer Sr;
        }

        readonly List<Ball> balls = new List<Ball>();
        readonly Queue<Vector2Int> toCheck = new Queue<Vector2Int>();
        float tickAccumulator;
        Sprite sprite;

        public int Falling => balls.Count;

        public static FallingSand Create(Transform parent)
        {
            var go = new GameObject("Terra Falling Sand");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<FallingSand>();
            TerraWorld.Changed += Instance.OnChanged;
            return Instance;
        }

        void OnChanged(int x, int y, CellKind kind)
        {
            // Something left this cell: the sand above it may fall now.
            if (!Cells.Blocks(kind) && kind != CellKind.Platform) toCheck.Enqueue(new Vector2Int(x, y + 1));
        }

        /// <summary>Looks at one cell: sand over empty space starts falling.</summary>
        public void Check(int x, int y) => toCheck.Enqueue(new Vector2Int(x, y));

        void Update()
        {
            int guard = 0;
            while (toCheck.Count > 0 && guard++ < 200)
            {
                var c = toCheck.Dequeue();
                if (TerraWorld.Get(c.x, c.y) != CellKind.Sand || TerraWorld.Get(c.x, c.y - 1) != CellKind.Empty || !TerraWorld.InWorld(c.x, c.y - 1)) continue;
                TerraWorld.Set(c.x, c.y, CellKind.Empty);
                Spawn(CellGrid.CellCenter(c.x, c.y));
            }
            if (balls.Count == 0) return;
            tickAccumulator += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                for (int i = balls.Count - 1; i >= 0; i--) Step(i);
            }
            foreach (var b in balls) b.Sr.transform.position = new Vector3(b.Pos.x, b.Pos.y, CellVisuals.BlockZ - 0.001f);
        }

        void Spawn(Vector2 at)
        {
            if (sprite == null)
            {
                var tex = Terraria.TerrariaAssets.Texture("Projectile_31");
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), CellVisuals.PixelsPerUnit);
            }
            var go = new GameObject("Terra Sand Ball");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            balls.Add(new Ball { Pos = at, Vy = 0f, Sr = sr });
        }

        void Step(int i)
        {
            var b = balls[i];
            b.Vy = Mathf.Min(b.Vy + 0.41f, 10f);
            const float c = CellGrid.CellSize;
            var next = b.Pos + Vector2.down * b.Vy * Px;
            // Land when the bottom of the ball reaches a cell that isn't free.
            int cx = Mathf.FloorToInt(b.Pos.x / c);
            int nowCell = Mathf.FloorToInt(b.Pos.y / c);
            int bottomCell = Mathf.FloorToInt((next.y - c * 0.5f + 0.001f) / c);
            for (int y = nowCell - 1; y >= bottomCell; y--)
            {
                if (TerraWorld.Get(cx, y) == CellKind.Empty && TerraWorld.InWorld(cx, y)) continue;
                Land(i, cx, y + 1);
                return;
            }
            b.Pos = next;
            // Fell out of every known scene.
            if (!TerraWorld.InWorld(cx, Mathf.FloorToInt(b.Pos.y / c)) || b.Pos.y < -500f) Remove(i, false, 0, 0);
        }

        void Land(int i, int x, int y)
        {
            var center = CellGrid.CellCenter(x, y);
            bool blocked = Physics2D.OverlapBox(center, Vector2.one * (CellGrid.CellSize * 0.9f), 0f, (1 << 9) | (1 << 11)) != null;
            bool placed = !blocked && TerraWorld.Get(x, y) == CellKind.Empty && TerraWorld.InWorld(x, y);
            if (placed)
            {
                TerraWorld.Set(x, y, CellKind.Sand);
                TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3), 0.5f);
            }
            Remove(i, !placed, x, y);
        }

        void Remove(int i, bool dropItem, int x, int y)
        {
            var b = balls[i];
            Destroy(b.Sr.gameObject);
            balls.RemoveAt(i);
            var mod = TerraMod.Instance;
            if (dropItem && mod != null && !mod.Inventory.Infinite) Items.WorldItems.Drop(Items.ItemStack.Of(Items.ItemType.SandBlock), CellGrid.CellCenter(x, y));
        }
    }
}
