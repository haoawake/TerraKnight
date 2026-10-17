using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Saves from before the masks were life of their own (TerraStats.MaskLife, the cyan hearts): up to 3.1.1 each mask made
    /// beyond the first five also gave a Life Crystal, once (the granted record "Mask6" to "Mask9"; none in creative). Those
    /// crystals are taken back once, the first time such a save is played: from the uneaten ones in the inventory and the
    /// chests first, then from the eaten count (20 life each). The player is told in the chat.
    /// </summary>
    internal static class MaskCrystalReturn
    {
        const string Done = "MaskCrystalsReturned";

        public static int DevReturned, DevFromItems, DevFromEaten;

        public static void Run(TerraMod mod)
        {
            if (mod == null) return;
            var granted = mod.GrantedAbilities;
            if (granted.Contains(Done)) return;
            granted.Add(Done);
            if (mod.Creative) return;
            int owed = 0;
            for (int mask = 6; mask <= 9; mask++) if (granted.Contains("Mask" + mask)) owed++;
            if (owed == 0) return;
            int fromItems = Take(mod.Inventory.Slots, owed);
            foreach (var slots in World.Chests.AllSlots())
            {
                if (fromItems >= owed) break;
                fromItems += Take(slots, owed - fromItems);
            }
            var stats = mod.Stats;
            int fromEaten = Mathf.Min(owed - fromItems, stats.LifeCrystals);
            stats.LifeCrystals -= fromEaten;
            int back = fromItems + fromEaten;
            DevReturned += back;
            DevFromItems += fromItems;
            DevFromEaten += fromEaten;
            Plugin.Log.LogInfo($"[生命水晶] 老存档：面具换来的 {owed} 颗生命水晶收回 {back} 颗（没吃的 {fromItems}，已吃的 {fromEaten}），面具现在是单独的生命");
            if (back > 0) Net.NetChat.Add(Lang.F("旧版本中面具换来的 {0} 颗生命水晶已收回：面具现在是单独的青蓝色生命", back), new Color(1f, 0.94f, 0.08f));
        }

        static int Take(ItemStack[] slots, int want)
        {
            int taken = 0;
            for (int i = 0; i < slots.Length && taken < want; i++)
            {
                if (slots[i].IsEmpty || slots[i].Type != ItemType.LifeCrystal) continue;
                int k = Mathf.Min(slots[i].Count, want - taken);
                slots[i].Count -= k;
                if (slots[i].Count <= 0) slots[i] = default;
                taken += k;
            }
            return taken;
        }
    }
}
