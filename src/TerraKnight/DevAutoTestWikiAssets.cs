using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator WikiAssetsExport()
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] Wiki 资源导出需要隔离存档，不通过"); yield break; }
            string folder = Path.Combine(DevCopy.SaveDir, "wiki-assets");
            Directory.CreateDirectory(folder);
            NailItem.Tick(TerraMod.Instance, HeroController.SilentInstance);
            DreamNailItem.EnsureIcon();
            GeoItem.EnsureIcon();
            var manifest = new Dictionary<string, object>();
            int count = 0, failures = 0;
            foreach (var def in ItemDb.All)
            {
                if (def.Type == ItemType.None) continue;
                string id = "item-" + def.Type.ToString().ToLowerInvariant();
                Texture2D texture = TerrariaAssets.Texture(def.Texture);
                int frames = ItemFrames.FramesOf(def.Texture);
                int width = texture.width, height = texture.height / frames;
                float scale = Mathf.Min(1f, 128f / Mathf.Max(width, height));
                int w = Mathf.Max(1, Mathf.RoundToInt(width * scale)), h = Mathf.Max(1, Mathf.RoundToInt(height * scale));
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                Texture2D image = null;
                try
                {
                    Graphics.Blit(texture, rt, new Vector2(1f, 1f / frames), new Vector2(0f, 1f - 1f / frames));
                    RenderTexture.active = rt;
                    image = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, id + ".png"), image.EncodeToPNG());
                    manifest[id] = new { kind = "item", item = def.Type.ToString(), name = def.Name, texture = def.Texture, frames, width = w, height = h };
                    count++;
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                    if (image != null) Object.Destroy(image);
                }
                if (count % 100 == 0) yield return null;
            }
            foreach (string record in EnemyDrops.DevRecords)
            {
                string id = "enemy-" + record.ToLowerInvariant().Replace('@', '-');
                var portrait = EnemyHover.PortraitOf(record);
                if (portrait == null) { failures++; Plugin.Log.LogError("[AutoTest] Wiki 肖像缺失 " + record + "，不通过"); continue; }
                float scale = Mathf.Min(1f, 128f / Mathf.Max(portrait.rect.width, portrait.rect.height));
                int height = Mathf.Max(1, Mathf.RoundToInt(portrait.rect.height * scale));
                var image = DreamNailItem.Capture(portrait, height, id);
                if (image == null) { failures++; Plugin.Log.LogError("[AutoTest] Wiki 肖像导出失败 " + record + "，不通过"); continue; }
                File.WriteAllBytes(Path.Combine(folder, id + ".png"), image.EncodeToPNG());
                manifest[id] = new { kind = "enemy", record, sprite = portrait.name, width = image.width, height = image.height, source = "Hunter journal portrait" };
                Object.Destroy(image);
                count++;
                if (count % 20 == 0) yield return null;
            }
            File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
            var itemData = new Dictionary<string, object>();
            foreach (var def in ItemDb.All)
                if (def.Type != ItemType.None) itemData[def.Type.ToString()] = new { def.Stage, Source = def.Source.ToString(), def.MaxStack, def.Texture,
                    def.Name, def.Tooltip, def.Value, def.Price, def.Damage, def.Defense, def.Mana, def.PickPower, def.HealLife, def.HealMana,
                    Class = def.Class.ToString(), Accessory = def.Accessory.ToString(),
                    Places = def.Places.ToString(), PlacesStation = def.PlacesStation.ToString(), def.PlacesWall };
            var recipeData = new List<object>();
            foreach (var r in Recipes.All)
            {
                var ingredients = new List<object>();
                foreach (var ing in r.Ingredients) ingredients.Add(new { Type = ing.Type.ToString(), ing.Count });
                recipeData.Add(new { Result = r.Result.ToString(), r.Count, r.Station, r.StationStage, Environment = r.Environment.ToString(), Ingredients = ingredients });
            }
            var dropData = new Dictionary<string, object>();
            foreach (var record in EnemyDrops.DevRecords)
            {
                var rows = new List<object>();
                foreach (var d in EnemyDrops.Of(record)) rows.Add(new { Type = d.Type.ToString(), d.Num, d.Den, d.Min, d.Max, d.Group, d.LockStage, d.Master });
                dropData[record] = rows;
            }
            File.WriteAllText(Path.Combine(folder, "runtime-data.json"), JsonConvert.SerializeObject(new { items = itemData, recipes = recipeData, drops = dropData }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] Wiki 资源导出 {count} 张，缺失 {failures} 张{(failures > 0 ? "，不通过" : "")}");
        }
    }
}
