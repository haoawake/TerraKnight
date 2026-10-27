using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Godhome's bosses pay the player's own stage (ProgressionLoot), and on top of it what its difficulty is worth (the
    /// user's): more of the stackable materials, more geo (paid straight into the bag, as the fight sends the player back
    /// before coins could be picked up), and a chance at one of the stage's boss rewards (a weapon or an accessory of the
    /// bosses of the player's stage). The Hall of Gods' Attuned, Ascended and Radiant and the five pantheons
    /// (GodhomeBalance's profiles) each their own.
    /// </summary>
    internal static class GodhomeRewards
    {
        internal sealed class Bonus
        {
            public string Key, Name;
            /// <summary>Stackable materials this much more (0.25: a quarter more).</summary>
            public float Materials;
            /// <summary>The kill's geo times this.</summary>
            public float Geo;
            /// <summary>The chance of one of the stage's boss rewards.</summary>
            public float Reward;
        }

        static Bonus B(string key, string name, float materials, float geo, float reward) =>
            new Bonus { Key = key, Name = name, Materials = materials, Geo = geo, Reward = reward };

        /// <summary>In GodhomeBalance's order (its profiles' keys); a treasure bag keeps the index + 1 (TreasureBags.Leave).</summary>
        static readonly Bonus[] all =
        {
            B("attuned", "众神堂·调谐", .25f, 1.5f, .10f),
            B("ascended", "众神堂·飞升", .5f, 2f, .20f),
            B("radiant", "众神堂·辐辉", 1f, 3f, .35f),
            B("pantheon1", "一门", .1f, 1.5f, .075f),
            B("pantheon2", "二门", .2f, 1.875f, .10f),
            B("pantheon3", "三门", .3f, 2.25f, .125f),
            B("pantheon4", "四门", .4f, 2.625f, .15f),
            B("pantheon5", "五门", .5f, 3f, .20f),
        };

        /// <summary>The bonus being paid out now (EnemyDrops.Roll sets it around a Godhome boss's drops), else null.</summary>
        public static Bonus Active;

        /// <summary>A bag's bonus to pay when it is opened (TreasureBags.TryOpen), else null.</summary>
        public static Bonus Bag;

        public static int DevRewards, DevGeo, DevScaled;

        /// <summary>The bonus of the Godhome fight in a scene now: its tier's, null outside Godhome's fights.</summary>
        public static Bonus For(string scene)
        {
            if (string.IsNullOrEmpty(scene) || !ProgressionLoot.GodhomeScene(scene) || !EnemyDrops.LeavesOnItsOwn(scene)) return null;
            var p = GodhomeBalance.Current(scene);
            foreach (var b in all) if (b.Key == p.Key) return b;
            return null;
        }

        public static int IndexOf(Bonus b) => b == null ? 0 : System.Array.IndexOf(all, b) + 1;
        public static Bonus OfIndex(int i, bool legacy = false)
        {
            if (i < 1 || i > all.Length) return null;
            var b = all[i - 1];
            return legacy && b.Key.StartsWith("pantheon") ? B(b.Key,b.Name,b.Materials*2,b.Geo,b.Reward*2) : b;
        }

        // A terminal fight pays terminal loot; earlier rematches keep their stage's
        // ordinary equipment even after another boss has been beaten.
        internal static bool LunarFight(string key, bool godhome, int stage) => godhome && stage >= 5
            && (key == "FinalBoss" || key == "HollowKnightPrime");
        internal static bool LunarEquipment(ItemType t)
        {
            foreach (var w in ProgressionBalance.LunarWeapons) if (w == t) return true;
            foreach (var armor in ProgressionBalance.LunarArmor) foreach (var a in armor) if (a == t) return true;
            return false;
        }

        /// <summary>What the materials bonus takes: stackable crafting materials (ores, bars, souls, fragments...), not equipment, healing or money.</summary>
        static bool Scales(ItemType t)
        {
            var d = ItemDb.Get(t);
            if (d == null || d.MaxStack <= 1 || d.IsWeapon || d.IsArmor || d.IsAccessory) return false;
            if (d.HealLife > 0 || d.HealMana > 0 || d.LifeCrystal || d.ManaCrystal || d.Boost != PermanentBoost.None) return false;
            if (t == ItemType.Geo || t == ItemType.Heart || t == ItemType.LunarCraftingStation) return false;
            return Recipes.IsMaterial(t);
        }

        /// <summary>A stack about to be paid (EnemyDrops.Put): more of it when it is a material and a bonus is being paid.</summary>
        public static int Scale(ItemType t, int n)
        {
            var b = Active;
            if (b == null || n <= 0 || !Scales(t)) return n;
            float more = n * (1f + b.Materials);
            int whole = Mathf.FloorToInt(more);
            if (Random.value < more - whole) whole++;
            if (whole > n) DevScaled++;
            return Mathf.Max(n, whole);
        }

        /// <summary>The geo a Godhome boss's kill pays: the table's times the tier's, 0 outside Godhome's fights.</summary>
        public static int Geo(HealthManager hm, int geo)
        {
            if (hm == null || geo <= 0 || TerraMod.Instance == null || TerraMod.Instance.Creative) return 0;
            var b = For(hm.gameObject.scene.name);
            return b == null ? 0 : Mathf.RoundToInt(geo * b.Geo);
        }

        /// <summary>
        /// After a Godhome boss's drops: at the tier's chance, another piece of that boss's own gear (its weapons and armour as
        /// its world fight gives them, unlocked at the player's stage), not its signature, a shop's weapon or lunar gear. Boss
        /// items come only from bosses (BossDropPlan.BossOnly keeps them out of crates and chests): this one is from the boss
        /// just beaten.
        /// </summary>
        public static ItemType RollReward(Bonus b,string record=null)
        {
            if (b == null || Random.value >= b.Reward) return ItemType.None;
            if(record==null || !VanillaBossProgression.All.TryGetValue(record,out var profile)
                || profile.Step>VanillaBossProgression.Current || profile.Band>LootStage.Current
                || !VanillaBossProgression.Cleared(record))return ItemType.None;
            int stage = LootStage.Current;
            var pool = new List<ItemType>();
            foreach(var t in profile.Gear)Add(pool,t,stage);
            if (pool.Count == 0) return ItemType.None;
            DevRewards++;
            return pool[Random.Range(0, pool.Count)];
        }

        static void Add(List<ItemType> pool, ItemType t, int stage)
        {
            if (t == ItemType.None || pool.Contains(t)) return;
            if (CraftOnlyLoot.Blocked(t)) return;
            if (LunarEquipment(t)) return;
            if (ProgressionBalance.ShopOnlyWeapons.Contains(t) || ProgressionBalance.BasicBossExcluded.Contains(t)) return;
            foreach (var s in ProgressionBalance.BossSignatures.Values) if (s == t) return;
            if (!ProgressionBalance.Unlocked(t, stage, ProgressionBalance.EntryClear, ProgressionBalance.FinalClear)) return;
            pool.Add(t);
        }
    }
}
