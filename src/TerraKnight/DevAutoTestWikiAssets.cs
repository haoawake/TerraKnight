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
                    Places = def.Places.ToString(), PlacesStation = def.PlacesStation.ToString(), def.PlacesWall,
                    def.UseTime, def.UseAnimation, def.Channel, def.NoUseMana, def.PayManaAfterCheck, def.ManaEveryOtherShot, def.ShotsPerUse };
            int magicCount = 0, manaErrors = 0;
            foreach (var def in ItemDb.All)
                if (def.Class == DamageClass.Magic && def.IsWeapon)
                {
                    magicCount++;
                    if (def.Texture == null || !def.Texture.StartsWith("Item_") || !int.TryParse(def.Texture.Substring(5), out int id)
                        || !VanillaWeaponDamage.Values.TryGetValue(id, out var original) || def.Mana != original.Mana)
                    {
                        manaErrors++;
                        Plugin.Log.LogError($"[AutoTest] 魔法蓝耗 {def.Type}={def.Mana} 与原版不一致，不通过");
                    }
                }
            Plugin.Log.LogInfo($"[AutoTest] 魔法蓝耗核对 {magicCount} 件，差异 {manaErrors} 件");
            var recipeData = new List<object>();
            foreach (var r in Recipes.All)
            {
                var ingredients = new List<object>();
                foreach (var ing in r.Ingredients) ingredients.Add(new { Type = ing.Type.ToString(), ing.Count });
                recipeData.Add(new { Result = r.Result.ToString(), r.Count, r.Station, r.StationStage, Environment = r.Environment.ToString(), Ingredients = ingredients });
            }
            var dropData = new Dictionary<string, object>();
            int originalStage = Shop.DevStage;
            Shop.DevStage = 5;
            foreach (var record in EnemyDrops.DevRecords)
            {
                var rows = new List<object>();
                foreach (var d in EnemyDrops.Of(record)) rows.Add(new { Type = d.Type.ToString(), d.Num, d.Den, d.Min, d.Max, d.Group, d.LockStage, d.Master, d.Weight, d.MeanCount });
                dropData[record] = rows;
            }
            Shop.DevStage = originalStage;
            var stageDrops = new Dictionary<int, Dictionary<string, object>>();
            var regionDrops = new Dictionary<int, Dictionary<string, object>>();
            var originalDifficulty = World.GameDifficulty.Current;
            World.GameDifficulty.Current = World.DifficultyLevel.Normal;
            for (int stage = 0; stage < 6; stage++)
            {
                Shop.DevStage = stage;
                var records = new Dictionary<string, object>();
                var regions = new Dictionary<string, object>();
                foreach (var record in EnemyDrops.DevRecords)
                {
                    var drops = EnemyDrops.At(record, stage, true, stage >= 4, stage >= 5);
                    var rows = new List<object>();
                    foreach (var d in drops)
                        rows.Add(new { Type = d.Type.ToString(), d.Num, d.Den, d.Min, d.Max, d.Group, d.LockStage, d.Master,
                            d.Weight, d.MeanCount, Probability = EnemyDrops.MarginalChance(drops, d) });
                    records[record] = rows;
                    var homes = new List<object>();
                    foreach (var home in BestiaryHabitats.Of(record))
                    {
                        var local = EnemyDrops.At(record, stage, true, stage >= 4, stage >= 5, home.Scene);
                        var localRows = new List<object>();
                        foreach (var d in local)
                            localRows.Add(new { Type = d.Type.ToString(), d.Num, d.Den, d.Min, d.Max, d.Group, d.LockStage, d.Master,
                                d.Weight, d.MeanCount, Probability = EnemyDrops.MarginalChance(local, d) });
                        homes.Add(new { home.Area, home.Scene, Drops = localRows });
                    }
                    regions[record] = homes;
                }
                stageDrops[stage] = records;
                regionDrops[stage] = regions;
            }
            // Each crate at each stage, opened CrateOpens times (Fishing.DevContents, as a right click opens it): how often
            // each thing comes out of one, how many when it does. The shops at each stage, as the townsfolk sell then.
            const int CrateOpens = 600, Casts = 3000, ChestOpens = 300, FishingPower = 100;
            var crateStages = new Dictionary<int, Dictionary<string, object>>();
            var fishingStages = new Dictionary<int, Dictionary<string, object>>();
            var chestStages = new Dictionary<int, Dictionary<string, object>>();
            // As an ordinary save opens them: the autotests' endless items would unlock every stage's (Shop.Free).
            bool wasInfinite = mod.Inventory.Infinite;
            mod.Inventory.Infinite = false;
            var shopStages = new Dictionary<int, object>();
            for (int stage = 0; stage < 6; stage++)
            {
                Shop.DevStage = stage;
                var byCrate = new Dictionary<string, object>();
                foreach (var crate in Fishing.DevCrateTypes)
                {
                    var seen = new Dictionary<ItemType, (int Times, int Min, int Max, long Sum)>();
                    for (int k = 0; k < CrateOpens; k++)
                    {
                        var once = new Dictionary<ItemType, int>();
                        foreach (var s in Fishing.DevContents(crate)) once[s.Type] = (once.TryGetValue(s.Type, out int n) ? n : 0) + s.Count;
                        foreach (var kv in once)
                        {
                            seen.TryGetValue(kv.Key, out var e);
                            seen[kv.Key] = (e.Times + 1, e.Times == 0 ? kv.Value : Mathf.Min(e.Min, kv.Value), Mathf.Max(e.Max, kv.Value), e.Sum + kv.Value);
                        }
                    }
                    var rows = new List<object>();
                    foreach (var kv in seen)
                        rows.Add(new { Type = kv.Key.ToString(), Chance = kv.Value.Times / (float)CrateOpens, kv.Value.Min, kv.Value.Max, Mean = kv.Value.Sum / (float)kv.Value.Times });
                    byCrate[crate.ToString()] = rows;
                }
                crateStages[stage] = byCrate;
                // What each area's water gives (a big pond, fishing power FishingPower, no quest), and the acid with the gear for it.
                var byArea = new Dictionary<string, object>();
                foreach (var area in Fishing.DevFishingAreas)
                {
                    byArea[area] = CatchRows(Fishing.DevSimulate(area, false, FishingPower, 1500, Casts), Casts);
                    yield return null;
                }
                byArea["酸水"] = CatchRows(Fishing.DevSimulate("遗忘十字路", true, FishingPower, 1500, Casts), Casts);
                fishingStages[stage] = byArea;
                // Each area's chests (World.RegionalChests): its weapon chest, its accessory chest, a supplies chest.
                var byChest = new Dictionary<string, object>();
                foreach (var a in World.AreaBlocks.All)
                    for (int part = 0; part < 3; part++)
                    {
                        var seen = new Dictionary<ItemType, (int Times, int Min, int Max, long Sum)>();
                        for (int k = 0; k < ChestOpens; k++)
                        {
                            var once = new Dictionary<ItemType, int>();
                            foreach (var s in World.RegionalChests.Contents(a.Name, part, stage))
                                if (!s.IsEmpty) once[s.Type] = (once.TryGetValue(s.Type, out int n) ? n : 0) + s.Count;
                            foreach (var kv in once)
                            {
                                seen.TryGetValue(kv.Key, out var e);
                                seen[kv.Key] = (e.Times + 1, e.Times == 0 ? kv.Value : Mathf.Min(e.Min, kv.Value), Mathf.Max(e.Max, kv.Value), e.Sum + kv.Value);
                            }
                        }
                        var rows = new List<object>();
                        foreach (var kv in seen)
                            rows.Add(new { Type = kv.Key.ToString(), Chance = kv.Value.Times / (float)ChestOpens, kv.Value.Min, kv.Value.Max, Mean = kv.Value.Sum / (float)kv.Value.Times });
                        byChest[a.Name + "|" + part] = rows;
                    }
                chestStages[stage] = byChest;
                var stocks = new List<object>();
                foreach (var (npc, type) in Town.TownNpcs.DevSourcesAt(stage)) stocks.Add(new { Npc = npc, Type = type.ToString() });
                shopStages[stage] = stocks;
                yield return null;
            }
            mod.Inventory.Infinite = wasInfinite;
            Shop.DevStage = originalStage;
            World.GameDifficulty.Current = originalDifficulty;
            File.WriteAllText(Path.Combine(folder, "runtime-data.json"), JsonConvert.SerializeObject(new { items = itemData, recipes = recipeData, drops = dropData, drop_stages = stageDrops, drop_regions = regionDrops,
                crate_stages = crateStages, crate_opens = CrateOpens, shop_stages = shopStages,
                fishing_stages = fishingStages, fishing_casts = Casts, fishing_power = FishingPower, chest_stages = chestStages, chest_opens = ChestOpens }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] Wiki 资源导出 {count} 张，缺失 {failures} 张{(failures > 0 ? "，不通过" : "")}");
        }

        /// <summary>A simulated fishing's catches as rows: each item and how often a cast brought it up (nothing caught left out).</summary>
        static List<object> CatchRows(Dictionary<ItemType, int> counts, int casts)
        {
            var rows = new List<object>();
            foreach (var kv in counts) if (kv.Key != ItemType.None) rows.Add(new { Type = kv.Key.ToString(), Chance = kv.Value / (float)casts });
            return rows;
        }
    }
}
