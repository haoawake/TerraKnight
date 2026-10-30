using System;
using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The townsfolk on the map, as Terraria's Main.DrawNPCHeadOnMap draws them: each resident's head (Images/NPC_Head_n,
    /// the housing menu's) where it is (Town.TownNpcs.MapHeads), on the full map and the corner maps alike, over the rooms
    /// and the map's marks, under the players' heads; on the full map its name under the mouse.
    /// </summary>
    internal static partial class WorldMap
    {
        static readonly Dictionary<string, Texture2D> mapNpcHeads = new Dictionary<string, Texture2D>();

        /// <summary>
        /// A townsperson's head picture (InventoryGui's housing heads): its NPCHeadID's NPC_Head_n, the Skeleton Merchant
        /// (no head of its own in Terraria) the Skull item.
        /// </summary>
        static Texture2D NpcHead(string internalName)
        {
            if (mapNpcHeads.TryGetValue(internalName, out var tex)) return tex;
            tex = null;
            try
            {
                if (internalName == "SkeletonMerchant") tex = TerrariaAssets.Texture("Item_1274");
                else
                {
                    int index = TerrariaText.HeadIndex(internalName);
                    if (index >= 0) tex = TerrariaAssets.Texture("NPC_Head_" + index);
                }
            }
            catch (Exception) { tex = null; }
            mapNpcHeads[internalName] = tex;
            return tex;
        }

        /// <summary>Where a head stands on the map (global cells), if its scene is on the map's layer drawn.</summary>
        static bool NpcCell(Town.TownNpcs.MapHead h, out Vector2 cell)
        {
            cell = default;
            if (!Shown(h.Scene, h.Local)) return false;
            if (!h.AtTownBench) return ScenePoint(h.Scene, h.Local, out cell);
            // On Hollow Knight's map: over the scene's bench pin.
            if (elsewhere != null)
            {
                foreach (var b in BenchPins)
                    if (b.Scene == h.Scene) return ScenePoint(h.Scene, b.Local + new Vector2(0f, 1f), out cell);
                return false;
            }
            // Not seen yet this session on Dirtmouth's street: at the town's bench (a thing of the town's room), as the townsfolk gather there.
            if (!rooms.TryGetValue(h.Scene, out var room) || !layout.TryGetValue(h.Scene, out var at)) return false;
            foreach (var o in room.Things)
                if (o.Kind == Obj.Bench) { cell = OnMap(h.Scene, at, new Vector2(o.Cells.center.x, o.Cells.yMin + 2f)); return true; }
            return false;
        }

        static readonly List<(string name, Rect rect)> drawnNpcs = new List<(string, Rect)>();

        /// <summary>The townsfolk's heads at a scale; returns the name of the one under the mouse (when one is given).</summary>
        static string DrawNpcHeads(Func<Vector2, Vector2> cellToScreen, float scale, Rect? clip, Vector2? mouse)
        {
            // The full map's heads (it passes the mouse) are kept for the dev checks; the corner maps' too when it is shut.
            if (mouse.HasValue || !Open) drawnNpcs.Clear();
            string hovered = null;
            var prev = GUI.color;
            List<Town.TownNpcs.MapHead> heads;
            try { heads = Town.TownNpcs.MapHeads(); }
            catch (Exception) { return null; }
            // Main.DrawMap draws them in turn, the later ones over the earlier; the one under the mouse is the last of them there.
            foreach (var h in heads)
            {
                if (!NpcCell(h, out var cell)) continue;
                var at = cellToScreen(cell);
                if (clip.HasValue && !clip.Value.Contains(at)) continue;
                var tex = NpcHead(h.Internal);
                if (tex == null) continue;
                // The Skull item stands in at a head's size.
                float k = h.Internal == "SkeletonMerchant" ? 26f / Mathf.Max(tex.width, tex.height) : 1f;
                var r = new Rect(at.x - tex.width * k * scale / 2f, at.y - tex.height * k * scale / 2f, tex.width * k * scale, tex.height * k * scale);
                GUI.color = new Color(1f, 1f, 1f, prev.a);
                GUI.DrawTexture(r, tex);
                if (mouse.HasValue || !Open) drawnNpcs.Add((h.Name, r));
                if (mouse.HasValue && r.Contains(mouse.Value)) hovered = h.Name;
            }
            GUI.color = prev;
            return hovered;
        }

        /// <summary>Dev: the townsfolk heads drawn in the last frame of a map (count), and their names.</summary>
        public static string DevNpcHeads()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, rect) in drawnNpcs) sb.Append($" {name}@({rect.center.x:F0},{rect.center.y:F0})");
            return $"{drawnNpcs.Count} 个：{sb}";
        }

        public static int DevNpcHeadCount => drawnNpcs.Count;

        /// <summary>Dev: where a drawn head is (GUI point), by its place in the last frame's list.</summary>
        public static Vector2? DevNpcHeadScreen(int i) => i >= 0 && i < drawnNpcs.Count ? drawnNpcs[i].rect.center : (Vector2?)null;

        /// <summary>Dev: the full map's hover line in its last frame.</summary>
        public static string DevLabel;

        /// <summary>Dev: every townsperson the map would put a head for: name, scene, cell on the map (or why not).</summary>
        public static string DevNpcPlaces()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var h in Town.TownNpcs.MapHeads())
            {
                bool ok = NpcCell(h, out var cell);
                sb.Append($" {h.Name}:{h.Scene}{(h.AtTownBench ? "(长椅)" : "")}{(ok ? $"@{cell.x:F0},{cell.y:F0}" : "(不画)")}{(NpcHead(h.Internal) == null ? "[无头像]" : "")}");
            }
            return sb.ToString();
        }
    }
}
