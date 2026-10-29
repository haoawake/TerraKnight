using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        static void AddHomeEverywhere(List<ItemDef> list)
        {
            list.Add(new ItemDef { Type = ItemType.HomeEverywhere, Name = "哪都是家", Texture = "HomeEverywhere_Item",
                MaxStack = 9999, Consumable = true, PlacesStation = World.StationKind.HomeEverywhere,
                UseAnimation = 15 * Tick, UseTime = 10 * Tick, AutoReuse = false, Rare = 5,
                Tooltip = "放置后连接上下左右10格内的储物空间与制作站\n右键放置物或在背包里点家园按钮，远程存取这片区域的物品，并用它的材料与工作站制作\n仓库里点「颜色 / 名称」可换光环颜色（64种，各家园不重复）和改名；区域之间不混用材料和工作站" });
        }
    }

    /// <summary>Export the generated art into the game's pixel lattice at load time; keep the originals intact.</summary>
    internal static class HomeEverywhereArt
    {
        static Texture2D item, placed;
        public static Texture2D Texture(bool world)
        {
            if (world ? placed != null : item != null) return world ? placed : item;
            var source = TerraKnight.Terraria.TerrariaAssets.Texture(world ? "HomeEverywhere_PlacedSource" : "HomeEverywhere_ItemSource");
            int left = source.width, bottom = source.height, right = 0, top = 0;
            var pixels = source.GetPixels32();
            for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
                if (pixels[y * source.width + x].a > 128) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y); }
            int size = world ? 48 : 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = world ? "HomeEverywhere_Placed" : "HomeEverywhere_Item" };
            var output = new Color32[size * size];
            float scale = Mathf.Max(right-left+1, top-bottom+1) / (float)(size-2);
            // The placed artwork fills its full 3x3-tile footprint; the inventory icon keeps its aspect and inset.
            float scaleX = world ? (right-left+1)/(float)size : scale;
            float scaleY = world ? (top-bottom+1)/(float)size : scale;
            float cx = (left+right)*.5f, cy = (bottom+top)*.5f;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                int sx = Mathf.RoundToInt(cx + (x-(size-1)*.5f)*scaleX), sy = Mathf.RoundToInt(cy + (y-(size-1)*.5f)*scaleY);
                if (sx < 0 || sy < 0 || sx >= source.width || sy >= source.height) continue;
                var c = pixels[sy*source.width+sx]; c.a = c.a >= 128 ? (byte)255 : (byte)0;
                output[y*size+x] = c;
            }
            texture.SetPixels32(output); texture.Apply();
            if (world) placed = texture; else item = texture;
            return texture;
        }
    }
}
