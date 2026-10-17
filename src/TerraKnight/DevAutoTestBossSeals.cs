using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossSealsTest()
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 首领召唤物需要隔离存档，不通过"); yield break; }
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite, dreamNail = PlayerData.instance.hasDreamNail;
            int stage = Shop.DevStage, selected = inv.Selected;
            var kills = new Dictionary<string, int>(Bestiary.Kills);
            string folder = Path.Combine(DevCopy.SaveDir, "boss-seals");
            Directory.CreateDirectory(folder);
            var results = new List<object>();
            int failed = 0;
            void Check(bool ok, string message)
            {
                results.Add(new { ok, message });
                if (!ok) failed++;
                Plugin.Log.LogInfo("[AutoTest] 首领召唤物 " + message + (ok ? " 通过" : " 不通过"));
            }
            try
            {
                inv.Infinite = false;
                inv.Mouse = default;
                inv.UseMouse = false;
                InventoryGui.Open = false;
                Shop.DevStage = 5;
                PlayerData.instance.hasDreamNail = true;
                PlayerData.instance.isInvincible = true;
                mod.DevNoFallDamage = true;
                Check(BossSummons.All.Length == 40, "40个定义");
                var types = new HashSet<ItemType>();
                var keys = new HashSet<string>();
                var atlas = new Texture2D(480, 300, TextureFormat.RGBA32, false);
                var atlasPixels = new Color32[480 * 300];
                var manifest = new List<object>();
                int n = 0;
                foreach (var s in BossSummons.All)
                {
                    var d = ItemDb.Get(s.Type);
                    var recipe = Recipes.For(s.Type);
                    bool valid = types.Add(s.Type) && keys.Add(s.Key) && BossArena.Has(s.Key)
                        && d.BossSummonKey == s.Key && d.Consumable && d.IsUsable && !d.IsSummon && !d.AutoReuse
                        && d.Source == ItemSource.Craft && recipe != null
                        && (d.Tooltip.Contains("消耗1个") || d.Tooltip.Contains("Consumes one"));
                    var scratch = new Inventory();
                    System.Array.Clear(scratch.Slots, 0, scratch.Slots.Length);
                    if (recipe != null)
                    {
                        foreach (var (type, count) in recipe.Ingredients) scratch.Add(type, count);
                        valid &= Recipes.HasIngredients(recipe, scratch);
                        Recipes.Consume(recipe, scratch);
                        valid &= !Recipes.HasIngredients(recipe, scratch);
                        scratch.Add(recipe.Result, recipe.Count);
                        valid &= Recipes.Count(scratch, s.Type) == 1;
                    }
                    Check(valid, "定义与合成 " + s.Key);
                    var portrait = UI.EnemyHover.PortraitOf(s.Key);
                    Check(portrait != null, "原版肖像 " + s.Key);
                    var texture = TerrariaAssets.Texture(d.Texture);
                    var rt = RenderTexture.GetTemporary(48, 48, 0, RenderTextureFormat.ARGB32);
                    var prev = RenderTexture.active;
                    var image = new Texture2D(48, 48, TextureFormat.RGBA32, false);
                    try
                    {
                        Graphics.Blit(texture, rt);
                        RenderTexture.active = rt;
                        image.ReadPixels(new Rect(0, 0, 48, 48), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(folder, s.Type + ".png"), image.EncodeToPNG());
                        var pix = image.GetPixels32();
                        int ox = (n % 8) * 60 + 6, oy = (4 - n / 8) * 60 + 6;
                        for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) atlasPixels[(oy + y) * 480 + ox + x] = pix[y * 48 + x];
                    }
                    finally { RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.Destroy(image); }
                    manifest.Add(new { index = n + 1, type = s.Type.ToString(), key = s.Key, name = s.Name, english = s.En,
                        stage = s.Stage, scene = BossArena.SceneOf(s.Key), portrait = portrait != null ? portrait.name : null });
                    n++;
                }
                atlas.SetPixels32(atlasPixels); atlas.Apply();
                File.WriteAllBytes(Path.Combine(folder, "contact-sheet.png"), atlas.EncodeToPNG());
                Object.Destroy(atlas);
                File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
                Check(BossSummons.DevPortraitIcons == 40, "40张召唤图标均使用原版肖像");

                inv.Selected = 0;
                inv.Slots[0] = ItemStack.Of(ItemType.BossSigilFalseKnight, 3);
                Bestiary.Kills["FalseKnight"] = 0;
                mod.DevUseSelected(HeroController.instance.transform.position);
                Check(inv.Slots[0].Count == 3 && !BossSummons.Pending, "未首杀拒绝且不消耗");
                Bestiary.Kills["FalseKnight"] = 1;
                // Usable anywhere (a portal takes the player to the arena): here in King's Pass too.
                Check(BossSummons.Refusal(BossSummons.All[0]) == null, "任意地点可召唤 " + BossSummons.Refusal(BossSummons.All[0]));

                yield return SealTravel("Town", "left1");
                inv.Slots[0] = ItemStack.Of(ItemType.BossSigilPureVessel, 3);
                Bestiary.Kills["HollowKnightPrime"] = 1;
                Shop.DevStage = 0;
                mod.DevUseSelected(HeroController.instance.transform.position);
                Check(inv.Slots[0].Count == 3 && !BossSummons.Pending, "进度不足拒绝且不消耗");
                Shop.DevStage = 5;
                PlayerData.instance.hasDreamNail = false;
                mod.DevUseSelected(HeroController.instance.transform.position);
                Check(inv.Slots[0].Count == 3 && !BossSummons.Pending, "缺梦之钉拒绝且不消耗");
                PlayerData.instance.hasDreamNail = true;
                for (int i = 0; i < 40; i++) inv.Slots[i] = ItemStack.Of(BossSummons.All[i].Type, 3);
                InventoryGui.Open = true;
                yield return new WaitForSecondsRealtime(0.8f);
                Shot("Town", "boss_seals_inventory");
                yield return new WaitForSecondsRealtime(0.3f);
                InventoryGui.Open = false;
                yield return null;

                // World multi-pool, dream return setup, and Godhome controller, through the real item-use route.
                foreach (var type in new[] { ItemType.BossSigilFalseKnight, ItemType.BossSigilHornet, ItemType.BossSigilFailedChampion, ItemType.BossSigilPureVessel })
                {
                    var s = BossSummons.Find(type);
                    Bestiary.Kills[s.Key] = 1;
                    inv.Slots[0] = ItemStack.Of(type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    float ready = Time.realtimeSinceStartup + 15f;
                    while (!GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready) yield return null;
                    Check(BossSummons.Refusal(s) == null, "入场条件 " + s.Key + " " + BossSummons.Refusal(s));
                    int consumed = BossSummons.DevConsumed;
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    // Straight into the arena (no front door, no portal), one spent on arrival (the fight's start: suite "sealstart").
                    float until = Time.realtimeSinceStartup + 45f;
                    while (BossSummons.Pending && Time.realtimeSinceStartup < until) yield return null;
                    Check(BossSummons.DevConsumed == consumed + 1 && inv.Slots[0].Count == 2
                        && BossArena.Arrived(s.Key), "活体入场只扣一个 " + s.Key + " " + BossSummons.DevLast);
                    if (!s.Dream)
                        Check(GameManager.instance.sceneName == BossArena.SceneOf(s.Key) && !Player.OpeningPortal.Active,
                              $"直接进竞技场 {s.Key}：{GameManager.instance.sceneName}，没有传送门仪式");
                    bool second = BossSummons.TryUse(ItemDb.Get(type), inv);
                    Check(!second && inv.Slots[0].Count == 2, "重复召唤拒绝且不消耗 " + s.Key);
                    yield return new WaitForSecondsRealtime(1f);
                    Shot(GameManager.instance.sceneName, "boss_seal_" + type);
                    yield return new WaitForSecondsRealtime(0.2f);
                    yield return SealTravel("Town", "left1");
                    Check(!BossArena.Visiting && !BossSummons.Pending, "离场清理 " + s.Key);
                }

                // A second seal while the first's fight is on: refused, nothing spent (checked above per seal).
            }
            finally
            {
                System.Array.Copy(saved, inv.Slots, saved.Length);
                inv.Selected = selected;
                inv.Infinite = infinite;
                Shop.DevStage = stage;
                PlayerData.instance.hasDreamNail = dreamNail;
                Bestiary.Kills.Clear();
                foreach (var kv in kills) Bestiary.Kills[kv.Key] = kv.Value;
                File.WriteAllText(Path.Combine(folder, "results.json"), JsonConvert.SerializeObject(new { failures = failed, checks = results }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 首领召唤物总计 {results.Count} 项，失败 {failed}" + (failed > 0 ? " 不通过" : " 通过"));
            }
        }

        IEnumerator SealTravel(string scene, string gate)
        {
            var gm = GameManager.instance;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo { SceneName = scene, EntryGateName = gate,
                HeroLeaveDirection = GlobalEnums.GatePosition.unknown, EntryDelay = 0f, WaitForSceneTransitionCameraFade = true });
            float until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until && (gm.sceneName != scene || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()))
                yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            if (gm.sceneName != scene || gm.IsInSceneTransition) Plugin.Log.LogError("[AutoTest] 首领召唤物转场超时 " + scene + " 不通过");
        }
    }
}
