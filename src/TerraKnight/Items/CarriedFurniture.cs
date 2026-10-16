using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Terraria's furniture that gives a buff when it stands nearby (SceneMetrics: the Campfire, the Heart Lantern, the Star
        /// in a Bottle, the Bast Statue, the Sunflower, the Garden Gnome), here working from the bag (Items.CarriedFurniture);
        /// and the Water Candle, from the hotbar. Names and tooltips are zh-Hans (Terraria's own names).
        /// </summary>
        static void AddCarriedFurniture(List<ItemDef> list)
        {
            ItemDef F(ItemType t, string name, int id, int rare, string tip) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, MaxStack = 99, Rare = rare, Tooltip = tip,
            };
            list.Add(F(ItemType.HeartLantern, "红心灯笼", 1859, 0, "带在身上时提高生命再生速度"));
            list.Add(F(ItemType.StarinaBottle, "星星瓶", 1431, 0, "带在身上时提高魔力再生速度"));
            list.Add(F(ItemType.CatBast, "巴斯特雕像", 4276, 1, "带在身上时防御力增加5"));
            list.Add(F(ItemType.Sunflower, "向日葵", 63, 0, "带在身上时移动速度提高"));
            list.Add(F(ItemType.GardenGnome, "花园侏儒", 4609, 1, "带在身上时运气提高"));
            list.Add(F(ItemType.WaterCandle, "水蜡烛", 148, 1, "放在快捷栏里：每次进入场景，除首领外的敌人（包括精英和只出现一次的敌人）都会立刻重新出现"));
        }

        /// <summary>Their stage, source and value (Item.value, copper).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> CarriedFurnitureSources()
        {
            yield return (ItemType.HeartLantern, 0, ItemSource.Craft, 4000);
            yield return (ItemType.StarinaBottle, 0, ItemSource.Craft, 4000);
            yield return (ItemType.CatBast, 1, ItemSource.Drop, 10000);
            yield return (ItemType.Sunflower, 0, ItemSource.Drop, 0);
            yield return (ItemType.GardenGnome, 1, ItemSource.Drop, 10000);
            yield return (ItemType.WaterCandle, 0, ItemSource.Shop, 6000);
        }
    }

    /// <summary>
    /// The buff furniture carried (Player.UpdateBiomes' SceneMetrics buffs, here from the bag): the Campfire's Cozy Fire
    /// (life regeneration +1, its timer 10% faster), the Heart Lantern's Heart Lamp (+2), the Star in a Bottle's (mana
    /// regeneration +10), the Bast Statue's Bast Defense (+5 defense), the Sunflower's Happy! (10% faster, then ×1.1), the
    /// Garden Gnome's luck (+0.2, no buff). The Water Candle in the hotbar shows its buff and brings back, every time a scene
    /// comes in, every enemy the game keeps dead in it (PersistentBoolItem: those back only after a bench, and the one-time
    /// ones), elites too, never a boss.
    /// </summary>
    [HarmonyPatch]
    internal static class CarriedFurniture
    {
        public static IEnumerable<Recipe> Recipes()
        {
            // Recipe.cs:2666-2669, 2710-2714: by hand.
            yield return new Recipe(ItemType.StarinaBottle, 1, "徒手", 0, (ItemType.Bottle, 1), (ItemType.FallenStar, 1));
            yield return new Recipe(ItemType.HeartLantern, 1, "徒手", 0, (ItemType.LifeCrystal, 1), (ItemType.Chain, 4));
        }

        public static bool Campfire, HeartLantern, StarInBottle, Bast, Sunflower, GardenGnome, WaterCandle;
        static float nextLook;
        public static int DevRevived;

        /// <summary>The Water Candle's setting, 1 to 3: the enemies a scene coming in gets, times the doubling (EnemyRespawn).</summary>
        public static int CandleLevel = 1;

        /// <summary>The enemies per original a scene coming in gets (one-time ones aside): the candle's times, in the hotbar.</summary>
        public static int CandleFactor => WaterCandle ? Mathf.Clamp(CandleLevel, 1, 3) : 1;

        /// <summary>The Water Candle used: its setting one up (3 goes back to 1), said over the player.</summary>
        public static void CycleCandle(HeroController hero)
        {
            CandleLevel = CandleLevel >= 3 ? 1 : CandleLevel + 1;
            if (hero != null)
                UI.CombatText.Show(hero.GetComponent<BoxCollider2D>().bounds.center + Vector3.up * 1.2f,
                    Lang.F("刷怪倍率 ×{0}", CandleLevel), new Color(0.55f, 0.75f, 1f), CandleLevel == 3);
            TerraMod.Instance?.PlaySound("Item_4");
        }

        /// <summary>Each frame in play: what is carried (a quarter-second look), and its buffs kept on.</summary>
        public static void Tick(TerraMod mod)
        {
            if (mod == null) return;
            if (Time.unscaledTime >= nextLook)
            {
                nextLook = Time.unscaledTime + 0.25f;
                Campfire = HeartLantern = StarInBottle = Bast = Sunflower = GardenGnome = WaterCandle = false;
                var slots = mod.Inventory.Slots;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsEmpty) continue;
                    switch (slots[i].Type)
                    {
                        case ItemType.Campfire: Campfire = true; break;
                        case ItemType.HeartLantern: HeartLantern = true; break;
                        case ItemType.StarinaBottle: StarInBottle = true; break;
                        case ItemType.CatBast: Bast = true; break;
                        case ItemType.Sunflower: Sunflower = true; break;
                        case ItemType.GardenGnome: GardenGnome = true; break;
                        case ItemType.WaterCandle: if (i < Inventory.HotbarSize) WaterCandle = true; break;
                    }
                }
            }
            // AddBuff(type, 2) every update as SceneMetrics does: on while carried, gone a moment after.
            if (Campfire) PlayerBuffs.Add(PlayerBuffs.CozyFire, 10);
            if (HeartLantern) PlayerBuffs.Add(PlayerBuffs.HeartLamp, 10);
            if (StarInBottle) PlayerBuffs.Add(PlayerBuffs.StarInBottle, 10);
            if (Bast) PlayerBuffs.Add(PlayerBuffs.BastDefense, 10);
            if (Sunflower) PlayerBuffs.Add(PlayerBuffs.Happy, 10);
            if (WaterCandle) PlayerBuffs.Add(PlayerBuffs.WaterCandle, 10);
        }

        static readonly System.Action<PersistentBoolItem> setMyID =
            AccessTools.MethodDelegate<System.Action<PersistentBoolItem>>(AccessTools.Method(typeof(PersistentBoolItem), "SetMyID"));

        /// <summary>
        /// An enemy the game keeps dead in its scene (its PersistentBoolItem's record "activated", read as it starts): with
        /// the Water Candle in the hotbar the record is cleared first, so it is there again, unless it is a boss.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PersistentBoolItem), "Start")]
        static void Revive(PersistentBoolItem __instance)
        {
            if (!WaterCandle || TerraMod.Instance == null || SceneData.instance == null) return;
            var hm = __instance.GetComponent<HealthManager>();
            if (hm == null) return;
            var info = EnemyStats.Of(hm);
            if (info == null || info.Boss || EnemyStats.IsBossKind(info.Record, Mathf.Max(1, hm.hp))) return;
            setMyID(__instance);
            var data = SceneData.instance.FindMyState(__instance.persistentBoolData);
            if (data == null || !data.activated) return;
            data.activated = false;
            SceneData.instance.SaveMyState(data);
            DevRevived++;
        }
    }
}
