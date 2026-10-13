using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Replaces the tilemap's own render meshes, which paint the inside of the terrain black. The
    /// grid draws the same black fill over every untouched original cell and leaves out the cells
    /// that were cut open, so holes show what is behind them.
    /// </summary>
    internal sealed class FillMeshes
    {
        const int ChunkCells = ChunkColliders.ChunkCells;
        const float Z = 0f;

        readonly CellGrid grid;
        readonly Transform root;
        readonly System.Func<int, int, bool> isCut;
        readonly int cols, rows;
        readonly Mesh[] meshes;
        readonly bool[] dirty;
        readonly GreedyRects greedy = new GreedyRects(ChunkCells);
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int> tris = new List<int>();
        static Material material;

        public FillMeshes(CellGrid grid, Transform root, System.Func<int, int, bool> isCut)
        {
            this.grid = grid;
            this.root = root;
            this.isCut = isCut;
            cols = (grid.Width + ChunkCells - 1) / ChunkCells;
            rows = (grid.Height + ChunkCells - 1) / ChunkCells;
            meshes = new Mesh[cols * rows];
            dirty = new bool[cols * rows];
        }

        public void BuildAll()
        {
            for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
                Rebuild(cx, cy);
        }

        public void MarkDirty(int x, int y)
        {
            if (grid.InBounds(x, y)) dirty[(y / ChunkCells) * cols + x / ChunkCells] = true;
        }

        public void RebuildDirty()
        {
            for (int i = 0; i < dirty.Length; i++)
            {
                if (!dirty[i]) continue;
                dirty[i] = false;
                Rebuild(i % cols, i / cols);
            }
        }

        void Rebuild(int cx, int cy)
        {
            int x0 = cx * ChunkCells, y0 = cy * ChunkCells;
            int x1 = Mathf.Min(x0 + ChunkCells, grid.Width), y1 = Mathf.Min(y0 + ChunkCells, grid.Height);
            verts.Clear();
            tris.Clear();
            const float c = CellGrid.CellSize;
            greedy.Run(x0, y0, x1, y1, (x, y) => grid.InClip(x, y) && grid.Get(x, y) == CellKind.Original && !isCut(x, y) && !grid.IsArt(x, y), (x, y, w, h) =>
            {
                int v = verts.Count;
                verts.Add(new Vector3(x * c, y * c, Z));
                verts.Add(new Vector3((x + w) * c, y * c, Z));
                verts.Add(new Vector3((x + w) * c, (y + h) * c, Z));
                verts.Add(new Vector3(x * c, (y + h) * c, Z));
                tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            });

            int i = cy * cols + cx;
            if (meshes[i] == null)
            {
                if (verts.Count == 0) return;
                meshes[i] = CreateChunkMesh(cx, cy);
            }
            var mesh = meshes[i];
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>The meshes are made at runtime, so they outlive their objects unless destroyed.</summary>
        public void DestroyMeshes()
        {
            foreach (var m in meshes) if (m != null) Object.Destroy(m);
        }

        Mesh CreateChunkMesh(int cx, int cy)
        {
            var go = new GameObject($"Terra Fill {cx} {cy}");
            go.transform.SetParent(root, false);
            var mesh = new Mesh { name = go.name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Material();
            return mesh;
        }

        static Material Material()
        {
            if (material != null) return material;
            material = new Material(Shader.Find("Sprites/Default")) { name = "Terra Fill", color = Color.black };
            return material;
        }
    }
}
