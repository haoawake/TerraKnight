using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator HkProtectedTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            yield return AbilityBagTest();
            yield return GoTo("Town", "left1");
            var inv=mod.Inventory; inv.Clear();
            inv.Selected=0; inv.Override=-1; InventoryGui.Open=true;
            int checks=0,failures=0;
            void Check(string name,bool pass)
            {
                checks++; if(!pass)failures++;
                Plugin.Log.LogInfo("[AutoTest] 圣巢保管/"+name+(pass?" 通过":" 不通过"));
            }
            // Control: the real throw path is active, so protected throw assertions cannot pass vacuously.
            inv.Mouse=ItemStack.Of(ItemType.Wood,7);
            int ground=WorldItems.DevCount(ItemType.Wood);
            mod.ThrowHeld(false);
            Check("普通物品仍能主动丢弃",inv.Mouse.IsEmpty && WorldItems.DevCount(ItemType.Wood)>ground);
            yield return new WaitForSeconds(.25f);
            foreach(var type in new[]{ItemType.ShadeCloak,ItemType.CrystalHeart,ItemType.HallownestShield})
            {
                var stack=ItemStack.Of(type); stack.Prefix=65;
                ground=WorldItems.DevCount(type);
                inv.Mouse=stack;
                mod.ThrowHeld(false);
                Check("鼠标物品不可丢弃/"+type,inv.Mouse.Type==type && WorldItems.DevCount(type)==ground);
                inv.Mouse=default; inv.Slots[0]=stack;
                mod.ThrowHeld(false);
                Check("快捷栏物品不可丢弃/"+type,inv.Slots[0].Type==type && WorldItems.DevCount(type)==ground);
                InventoryGui.DevSell(0);
                Check("快捷出售保护/"+type,inv.Slots[0].Type==type);
                inv.Mouse=stack; inv.Slots[0]=default; inv.Trash=default;
                InventoryGui.DevClickTrash();
                Check("鼠标不能放进垃圾桶/"+type,inv.Mouse.Type==type && inv.Trash.IsEmpty);
                inv.Mouse=default; inv.Slots[0]=stack;
                InventoryGui.DevCtrl=true;
                try { InventoryGui.DevLeftClick(0); } finally { InventoryGui.DevCtrl=false; }
                Check("Ctrl快捷删除保护/"+type,inv.Trash.IsEmpty && (inv.Mouse.Type==type || inv.Slots[0].Type==type));
                inv.Mouse=default; inv.Slots[0]=default;
                Check("仍可重铸饰品/"+type,ItemDb.Get(type).IsAccessory && !ItemDb.Get(type).Permanent);
            }
            // Clear existing unique tools before moving their only copy during this synchronous section.
            Array.Clear(inv.Slots,0,inv.Slots.Length); Array.Clear(inv.HkBag,0,inv.HkBag.Length);
            foreach(var type in new[]{ItemType.KnightNail,ItemType.DreamNail,ItemType.HallownestShield})
            {
                int slot=type==ItemType.DreamNail?HkItems.DreamNailSlot:HkItems.EquipmentBagSlot(type);
                var stack=ItemStack.Of(type); stack.Prefix=65; stack.Favorite=true;
                Check("背包接受/"+type,HkItems.CanStoreInBag(stack) && HkItems.BagAccepts(slot,stack));
                inv.Mouse=stack; InventoryGui.DevClickHkBag(slot);
                Check("拖入专用格保留属性/"+type,inv.Mouse.IsEmpty && inv.HkBag[slot].Type==type && inv.HkBag[slot].Prefix==65 && inv.HkBag[slot].Favorite);
                Check("专用格拒绝木材/"+type,!HkItems.BagAccepts(slot,ItemStack.Of(ItemType.Wood)));
                InventoryGui.DevClickHkBag(slot);
                inv.Slots[0]=inv.Mouse; inv.Mouse=default;
                InventoryGui.DevLeftClick(0,shift:true);
                Check("Shift快速收纳/"+type,inv.Slots[0].IsEmpty && inv.HkBag[slot].Type==type);
                InventoryGui.DevClickHkBag(slot);
                for(int i=0;i<Inventory.Total;i++) { inv.Slots[i]=ItemStack.Of(ItemType.StoneBlock,999); inv.Slots[i].Favorite=true; }
                mod.ReturnOrDropMouseItem();
                Check("满包关闭安全保管/"+type,inv.Mouse.IsEmpty && inv.HkBag[slot].Type==type && inv.HkBag[slot].Prefix==65 && inv.HkBag[slot].Favorite);
                Array.Clear(inv.Slots,0,inv.Slots.Length);
            }
            int profile=GameManager.instance.profileID;
            Save.ModSave.Write(profile,mod);
            Array.Clear(inv.HkBag,0,inv.HkBag.Length);
            Save.ModSave.Read(profile,mod);
            Check("存档恢复骨钉梦钉圣巢盾",inv.HkBag[HkItems.KnightNailSlot].Type==ItemType.KnightNail
                && inv.HkBag[HkItems.DreamNailSlot].Type==ItemType.DreamNail && inv.HkBag[HkItems.HallownestShieldSlot].Type==ItemType.HallownestShield);
            string path=Path.Combine(TerraMod.SaveDir,$"TerraKnight_slot{profile}.json");
            var old=JObject.Parse(File.ReadAllText(path)); old["HkBag"]=new JArray(((JArray)old["HkBag"]).Take(29));
            File.WriteAllText(path,old.ToString()); Save.ModSave.Read(profile,mod);
            Check("旧29格存档兼容且梦钉不移位",inv.HkBag.Length==31 && inv.HkBag[20].Type==ItemType.DreamNail && inv.HkBag[29].IsEmpty && inv.HkBag[30].IsEmpty);
            InventoryGui.Open=false;
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"hk-protected-result.json"),new JObject { ["Checks"]=checks,["Failures"]=failures }.ToString());
            Plugin.Log.LogInfo($"[AutoTest] 圣巢保管合计 {checks-failures}/{checks} {(failures==0?"通过":"不通过")}");
        }
    }
}
