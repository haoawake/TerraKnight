using System.Collections;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bags"): creative's four bags. The showcase kit's bag for each loadout (best weapon,
    /// pickaxe, the rest), F1-F4 switching the whole bag, the bags sharing nothing, all four through
    /// a save and back, an old creative save (one bag) given the other three, and a normal save's
    /// one bag for every loadout.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BagsTest(HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            mod.ApplyCreative(true);
            ShowcaseKit.Give(mod);
            yield return new WaitForSeconds(0.5f);

            string Bag() => $"{BagName(inv.Slots[0])}、{BagName(inv.Slots[1])}、{BagName(inv.Slots[2])}… 共 {Filled()} 格，选中 {inv.Selected + 1}";
            int Filled() { int n = 0; foreach (var s in inv.Slots) if (!s.IsEmpty) n++; return n; }
            int Count(ItemType t) { int n = 0; foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) n += s.Count; return n; }
            string Signature()
            {
                var sb = new StringBuilder();
                sb.Append(inv.CurrentLoadout).Append('|');
                foreach (var s in inv.Slots) sb.Append(s.IsEmpty ? "-" : s.Type + "x" + s.Count).Append(',');
                foreach (var l in inv.Loadouts) { sb.Append('|').Append(l.Selected); foreach (var s in l.Bag) sb.Append(s.IsEmpty ? "-" : s.Type + "x" + s.Count).Append(','); }
                return sb.ToString();
            }
            IEnumerator Switch(int l)
            {
                float t0 = Time.time;
                while (!mod.TrySwitchLoadout(l) && inv.CurrentLoadout != l && Time.time - t0 < 3f) yield return null;
                yield return null;
            }

            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                yield return Switch(l);
                Plugin.Log.LogInfo($"[AutoTest] 四背包 配装{l + 1}：{Bag()}；盔甲 {BagName(inv.Armor[0])}");
            }

            // Something put in the ranged bag stays there, and nowhere else.
            yield return Switch(1);
            inv.Slots[35] = ItemStack.Of(ItemType.Wood, 7);
            inv.Selected = 4;
            yield return Switch(2);
            int woodInMagic = Count(ItemType.Wood);
            yield return Switch(0);
            int woodInMelee = Count(ItemType.Wood);
            yield return Switch(1);
            Plugin.Log.LogInfo($"[AutoTest] 四背包 不共享：远程背包放 7 个木材后，魔法背包 {woodInMagic} 个、近战背包 {woodInMelee} 个（都应为 0），回到远程 {Count(ItemType.Wood)} 个（应为 7），选中 {inv.Selected + 1}（应为 5）");

            // Through a save and back.
            int slot = GameManager.instance.profileID;
            string before = Signature();
            Save.ModSave.Write(slot, mod);
            Save.ModSave.Read(slot, mod);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 四背包 存档读回：一样 {Signature() == before}，当前配装{inv.CurrentLoadout + 1}，{Bag()}，背包各自 {inv.BagPerLoadout}");

            // An old creative save: no bags kept for the loadouts.
            string path = System.IO.Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json");
            string text = System.IO.File.ReadAllText(path);
            text = System.Text.RegularExpressions.Regex.Replace(text, "\"Bag\":\\[[^\\]]*\\],?", "");
            text = text.Replace("\"Version\":6", "\"Version\":5");
            System.IO.File.WriteAllText(path, text);
            Save.ModSave.Read(slot, mod);
            yield return new WaitForSeconds(0.5f);
            var old = new StringBuilder();
            for (int l = 0; l < Inventory.LoadoutCount; l++)
            {
                yield return Switch(l);
                old.Append($" 配装{l + 1} {BagName(inv.Slots[0])}/{BagName(inv.Slots[1])} {Filled()} 格；");
            }
            Plugin.Log.LogInfo($"[AutoTest] 四背包 旧创造存档：{old}（当前的远程保留原背包，木材 {CountIn(1, ItemType.Wood)} 个）");

            // A normal save: one bag for all.
            mod.ApplyCreative(false);
            inv.Clear();
            inv.Slots[0] = ItemStack.Of(ItemType.CopperShortsword);
            inv.Slots[5] = ItemStack.Of(ItemType.Wood, 3);
            yield return Switch(2);
            Plugin.Log.LogInfo($"[AutoTest] 四背包 普通存档：换到配装3 后背包还是 {BagName(inv.Slots[0])}、木材 {Count(ItemType.Wood)} 个（应为 铜短剑、3），背包各自 {inv.BagPerLoadout}（应为 False）");
            yield return Switch(0);

            int CountIn(int loadout, ItemType t)
            {
                var slots = inv.CurrentLoadout == loadout ? inv.Slots : inv.Loadouts[loadout].Bag;
                int n = 0;
                foreach (var s in slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
                return n;
            }
        }

        static string BagName(ItemStack s) => s.IsEmpty ? "空" : ItemDb.Get(s.Type).Name;
    }
}
