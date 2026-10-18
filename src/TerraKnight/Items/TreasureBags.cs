using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's treasure bags (ItemDropRule.BossBag): in Expert and Master Mode a boss leaves one bag in place of its
    /// loot, right clicked it opens to that boss's drops rolled then, by its table as the kill had it (a first kill's bag
    /// carries the mark in its stack's Forge byte, and opens to the first kill's rolls). What Terraria gives outside the
    /// bag stays outside: the relic (Master), the hearts, the healing and mana potions, life and mana crystals. One bag a
    /// boss relic (TreasureBagsGenerated.cs), Expert's rainbow (rare -12), its picture from 空洞骑士大师宝藏袋素材包-结构重制版.
    /// </summary>
    internal static partial class TreasureBags
    {
        static Dictionary<ItemType, ItemType> bagOfRelic, relicOfBag;
        static Dictionary<ItemType, (string Zh, string En)> names;
        static HashSet<ItemType> bags;

        static void Build()
        {
            if (bags != null) return;
            bags = new HashSet<ItemType>();
            bagOfRelic = new Dictionary<ItemType, ItemType>();
            relicOfBag = new Dictionary<ItemType, ItemType>();
            names = new Dictionary<ItemType, (string, string)>();
            foreach (var (bag, relic, zh, en, _) in all)
            {
                bags.Add(bag);
                bagOfRelic[relic] = bag;
                relicOfBag[bag] = relic;
                names[bag] = (zh, en);
            }
        }

        /// <summary>Main.expertMode: Expert or Master.</summary>
        public static bool Active => World.GameDifficulty.Current >= World.DifficultyLevel.Expert;

        public static bool Is(ItemType t) { Build(); return bags.Contains(t); }

        /// <summary>The bag a boss's fight leaves (its relic's: the variants by scene or boss name), or none.</summary>
        public static ItemType For(string key, string scene, string bossName)
        {
            Build();
            var relic = Relics.Of(key, scene, bossName);
            return relic.HasValue && bagOfRelic.TryGetValue(relic.Value, out var bag) ? bag : ItemType.None;
        }

        /// <summary>The drop table a bag opens to.</summary>
        public static string KeyOf(ItemType bag)
        {
            Build();
            return relicOfBag.TryGetValue(bag, out var relic) ? Relics.KeyOf(relic) : null;
        }

        /// <summary>Outside the bag, as Terraria drops them beside it: the relic, hearts, healing and mana, the crystals.</summary>
        public static bool Outside(EnemyDrops.Drop d)
        {
            if (d.Group==207 || d.Master || d.Type == ItemType.Heart) return true;
            var def = ItemDb.Get(d.Type);
            return def.HealLife > 0 || def.HealMana > 0 || def.LifeCrystal || def.ManaCrystal;
        }

        public static int DevLeft, DevOpened;

        /// <summary>A bag left where the boss fell (into the bag when there's no time to pick it up), a first kill's marked.</summary>
        public static void Leave(ItemType bag, Vector2 at, bool direct, bool first, bool godhome=false)
        {
            var s = ItemStack.Of(bag);
            s.Forge = (byte)((first ? 1 : 0) | (godhome ? 2 : 0));
            DevLeft++;
            if (EnemyDrops.DevSink != null) { EnemyDrops.DevSink(bag, 1); return; }
            // Each player's own in a LAN game (DropLocalPerClient).
            EnemyDrops.Spill(s, at, direct, perPlayer: true);
        }

        /// <summary>ItemSlot.TryOpenContainer for a bag: one opened, its boss's drops rolled into the bag (what won't fit at the feet).</summary>
        public static bool TryOpen(Inventory inv, int slot)
        {
            var stack = inv.Slots[slot];
            if (stack.IsEmpty || !Is(stack.Type)) return false;
            string key = KeyOf(stack.Type);
            if (key == null) return false;
            bool first = (stack.Forge & 1) != 0;
            bool godhome = (stack.Forge & 2) != 0;
            inv.ConsumeAt(slot);
            var hero = HeroController.SilentInstance;
            Vector2 at = hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : Vector2.zero;
            EnemyDrops.Roll(key, at, direct: true, firstKill: first, fromBag: true, scene: godhome ? "GG_Workshop" : null);
            TerraMod.Instance?.PlaySound("Grab");
            DevOpened++;
            Plugin.Log.LogInfo($"[宝藏袋] 打开 {ItemDb.Get(stack.Type).Name}（{key}{(first ? "，首次击败" : "")}）");
            return true;
        }

        /// <summary>A first kill's bag: the same bag, its tooltip saying so.</summary>
        static readonly Dictionary<ItemDef, ItemDef> firstDefs = new Dictionary<ItemDef, ItemDef>();

        public static ItemDef FirstKill(ItemDef b)
        {
            if (firstDefs.TryGetValue(b, out var c) && c.Name == b.Name) return c;
            c = b.Clone();
            c.Tooltip = (b.Tooltip ?? "") + "\n" + Lang.T("首次击败：必得首领武器");
            firstDefs[b] = c;
            return c;
        }

        static readonly Dictionary<(ItemDef,byte,string),ItemDef> markedDefs=new Dictionary<(ItemDef,byte,string),ItemDef>();
        public static ItemDef Marked(ItemDef b,byte mark) {
            var original=b;
            if((mark & 1)!=0)b=FirstKill(b);
            if((mark & 2)==0)return b;
            var key=(original,mark,Lang.Culture);
            if(markedDefs.TryGetValue(key,out var cached))return cached;
            var copy=b.Clone();
            copy.Tooltip=(b.Tooltip ?? "")+"\n"+(Lang.IsSource || Lang.Culture.StartsWith("zh")
                ? "神居战利品：开出终局装备\n夜明矿已在击败首领时发放"
                : "Godhome spoils: endgame equipment\nLuminite awarded when the boss fell");
            markedDefs[key]=copy;return copy;
        }

        /// <summary>A bag's name in the language now: Terraria's "Treasure Bag (Boss)".</summary>
        public static void Localize(ItemDef d)
        {
            Build();
            if (!names.TryGetValue(d.Type, out var n)) return;
            bool zh = Lang.IsSource || Lang.Culture.StartsWith("zh");
            d.Name = zh ? "宝藏袋（" + n.Zh + "）" : "Treasure Bag (" + n.En + ")";
            d.Tooltip = Lang.T("右键点击打开") + "\n" + Lang.T("专家");
            firstDefs.Remove(d);
        }

        public static void AddItems(List<ItemDef> list)
        {
            foreach (var (bag, _, zh, _, texture) in all)
                list.Add(new ItemDef
                {
                    Type = bag, Name = "宝藏袋（" + zh + "）", ZhName = "宝藏袋（" + zh + "）", Texture = Relics.TexturePrefix + texture,
                    MaxStack = 999, Rare = -12, Opens = true, UseSound = null, Tooltip = "右键点击打开\n专家",
                });
        }

        public static IEnumerable<(ItemType, int, ItemSource, int)> Sources()
        {
            foreach (var (bag, _, _, _, _) in all) yield return (bag, 0, ItemSource.Drop, 0);
        }
    }

    internal static partial class Relics
    {
        /// <summary>The drop table a relic's fight is keyed by (a variant's: the table it shares).</summary>
        internal static string KeyOf(ItemType relic)
        {
            foreach (var kv in byKey) if (kv.Value == relic) return kv.Key;
            foreach (var v in variants) if (v.Relic == relic) return v.Key;
            return null;
        }
    }
}
