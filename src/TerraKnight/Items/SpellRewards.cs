using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The spells' class weapons (four for each spell absorbed: SoulWave, Howl, Shade, Abyss) given to the player: into the
    /// bag, and what doesn't fit at the feet with a notice, as the player's own drop (a LAN partner can't take it) that the
    /// lying items' cap never clears (WorldItems.Spawn). Given only where a drop can lie, else left for the next check.
    /// The Arms Dealer sells all sixteen (TownShopExtra), each from its spell's absorbing on, not by stage: shown locked
    /// before then with the spell it waits for (TownNpcs.ShopSpell).
    /// </summary>
    internal static class SpellRewards
    {
        /// <summary>The spells in order, 1-4: the Vengeful Spirit, the Howling Wraiths, the Shade Soul, the Abyss Shriek.</summary>
        static readonly ItemType[][] bySpell = { SoulWave.Weapons, Howl.Weapons, Shade.Weapons, Abyss.Weapons };
        static Dictionary<ItemType, int> spellOf;
        public static int DevDropped;
        /// <summary>How many of the last weapons given didn't fit the bag (the spell's story beat says where they are).</summary>
        public static int LastDropped;

        /// <summary>Which spell's weapon it is (1-4), 0: none.</summary>
        public static int SpellOf(ItemType t)
        {
            if (spellOf == null)
            {
                spellOf = new Dictionary<ItemType, int>();
                for (int s = 0; s < bySpell.Length; s++) foreach (var w in bySpell[s]) spellOf[w] = s + 1;
            }
            return spellOf.TryGetValue(t, out int spell) ? spell : 0;
        }

        /// <summary>One of the spells' weapons.</summary>
        public static bool Is(ItemType t) => SpellOf(t) > 0;

        /// <summary>The sixteen in the spells' order.</summary>
        public static IEnumerable<ItemType> All
        {
            get { foreach (var ws in bySpell) foreach (var w in ws) yield return w; }
        }

        static readonly string[] nameKeys = { "INV_NAME_SPELL_FIREBALL1", "INV_NAME_SPELL_SCREAM1", "INV_NAME_SPELL_FIREBALL2", "INV_NAME_SPELL_SCREAM2" };
        static readonly string[] names = { "复仇之魂", "嚎叫幽灵", "暗影之魂", "深渊尖啸" };

        /// <summary>A spell's name (1-4) in the game's language (its inventory's), else the Chinese.</summary>
        public static string Name(int spell)
        {
            if (spell < 1 || spell > names.Length) return "";
            try
            {
                if (TeamCherry.Localization.Language.Has(nameKeys[spell - 1], "UI")) return TeamCherry.Localization.Language.Get(nameKeys[spell - 1], "UI");
            }
            catch { }
            return names[spell - 1];
        }

        /// <summary>What a shop's locked look of a spell's weapon says opens it.</summary>
        public static string LockText(int spell) => Lang.F("吸收{0}后解锁", Name(spell));

        /// <summary>The spell absorbed in this save (fireballLevel 1 / 2, screamLevel 1 / 2).</summary>
        public static bool Absorbed(int spell)
        {
            var pd = PlayerData.instance;
            if (pd == null) return false;
            switch (spell)
            {
                case 1: return pd.fireballLevel >= 1;
                case 2: return pd.screamLevel >= 1;
                case 3: return pd.fireballLevel >= 2;
                case 4: return pd.screamLevel >= 2;
                default: return false;
            }
        }

        /// <summary>For sale now: a spell's weapon once its spell is absorbed (any, with the infinite bag), else by stage as everything else.</summary>
        public static bool OnSale(ItemType t)
        {
            int spell = SpellOf(t);
            return spell > 0 ? Shop.Free || Absorbed(spell) : ProgressionBalance.Unlocked(t);
        }

        /// <summary>The weapons to the player at that point (the hero's middle); false, nothing given, while a weapon that doesn't fit could lie nowhere.</summary>
        public static bool Give(TerraMod mod, ItemType[] weapons, Vector2 at)
        {
            bool spills = false;
            foreach (var t in weapons) spills |= mod.Inventory.Room(t) < weapons.Length;
            if (spills && !WorldItems.CanDrop(at)) return false;
            int dropped = 0;
            foreach (var t in weapons)
            {
                int left = mod.Inventory.Pickup(t, 1);
                if (left < 1) UI.PopupText.Show(ItemDb.Get(t), 1, at);
                else
                {
                    WorldItems.Drop(ItemStack.Of(t, left), at, personal: true);
                    dropped++;
                }
            }
            LastDropped = dropped;
            if (dropped > 0)
            {
                UI.CombatText.Show(at + Vector2.up * 1.4f, Lang.T("背包已满：放不下的武器掉在了脚下"), UI.CombatText.Notice);
                DevDropped += dropped;
            }
            return true;
        }
    }
}
