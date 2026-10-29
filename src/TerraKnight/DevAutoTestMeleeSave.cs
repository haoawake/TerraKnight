using System.Collections;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "meleesave", the user's 10-26 "开一个存档在背包里摆满所有真近战武器"): a new game's save for trying the remade
    /// swings, made in the test copy and copied into the player's free slot afterwards. In Dirtmouth (its Target Dummy by
    /// the well), the bag holds the true melee weapons: the hotbar a showcase from the Wooden Sword to the Meowmere, the
    /// rest in the order they come in the game, the last two slots the Money Trough and the Void Bag; what the bag can't
    /// hold waits in the piggy bank and the void bag, with three Target Dummies. Then the game saved. Lines "[AutoTest] 近战存档 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MeleeSaveTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            var all = ItemDb.All.Where(d => d != null && BladeArts.Is(d)).OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage).ThenBy(d => (int)d.Type).Select(d => d.Type).ToList();
            var showcase = new[]
            {
                ItemType.WoodenSword, ItemType.CopperBroadsword, ItemType.Katana, ItemType.Muramasa, ItemType.NightsEdge,
                ItemType.BreakerBlade, ItemType.Excalibur, ItemType.TerraBlade, ItemType.InfluxWaver, ItemType.Meowmere,
            };
            var order = showcase.Where(all.Contains).Concat(all.Where(t => !showcase.Contains(t))).ToList();
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            var bank = inv.BankOf(0);
            var voidBag = inv.BankOf(3);
            for (int i = 0; i < bank.Length; i++) bank[i] = default;
            for (int i = 0; i < voidBag.Length; i++) voidBag[i] = default;
            int bagRoom = Inventory.Size - 2, k = 0;
            for (int i = 0; i < bagRoom && k < order.Count; i++) inv.Slots[i] = ItemStack.Of(order[k++]);
            inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.MoneyTrough);
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.VoidBag);
            int inBank = 0, inVoid = 0;
            for (int i = 0; i < bank.Length && k < order.Count; i++, inBank++) bank[i] = ItemStack.Of(order[k++]);
            for (int i = 0; i < voidBag.Length && k < order.Count; i++, inVoid++) voidBag[i] = ItemStack.Of(order[k++]);
            if (inVoid < voidBag.Length) voidBag[inVoid] = ItemStack.Of(ItemType.TargetDummy, 3);
            inv.Selected = 0;
            inv.RefreshEffective();
            Plugin.Log.LogInfo($"[AutoTest] 近战存档 真近战 {all.Count} 把：背包 {Mathf.Min(bagRoom, order.Count)}，存钱罐 {inBank}，虚空袋 {inVoid}，剩 {order.Count - k}；新挥砍 {BladeArts.Enabled}");
            Plugin.Log.LogInfo($"[AutoTest] 近战存档 快捷栏：{string.Join("、", inv.Slots.Take(Inventory.HotbarSize).Select(s => s.IsEmpty ? "空" : s.Def.Name))}");
            // Its name on the save list.
            Save.ModSave.WorldName = "真近战测试";
            yield return new WaitForSeconds(0.5f);
            var gm = GameManager.instance;
            bool done = false, ok = false;
            gm.SaveGame(r => { done = true; ok = r; });
            for (float t = 0f; t < 10f && !done; t += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            int slot = gm.profileID;
            bool game = File.Exists(Path.Combine(TerraMod.SaveDir, $"user{slot}.dat"));
            bool modSave = File.Exists(Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json"));
            Plugin.Log.LogInfo($"[AutoTest] 近战存档 {(ok && game && modSave && k == order.Count ? "通过" : "不通过")}：存档位 {slot} 保存 {ok}，user{slot}.dat {game}，TerraKnight_slot{slot}.json {modSave}，目录 {TerraMod.SaveDir}");
        }
    }
}
