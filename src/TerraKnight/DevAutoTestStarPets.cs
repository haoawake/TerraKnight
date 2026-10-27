using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>Dev (suite "starpets"): the star pets (Estee, the Everscream sapling, the Junimo) called one at a time beside the player and photographed.</summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StarPetsTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            var start = (Vector2)hero.transform.position + Vector2.right * 4f;
            foreach (var t in new[] { ItemType.CelestialWand, ItemType.EverscreamPetItem, ItemType.JunimoPetItem })
            {
                var def = Pets.Of(t);
                inv.Misc[Pets.PetSlot] = default;
                inv.Misc[Pets.LightSlot] = default;
                yield return new WaitForSeconds(0.5f);
                inv.Misc[def.Light ? Pets.LightSlot : Pets.PetSlot] = ItemStack.Of(t);
                hero.transform.position = Clear(hero, start);
                yield return new WaitForSeconds(2.5f);
                var p = Minions.DevPet(def.Light);
                Plugin.Log.LogInfo($"[AutoTest] 星星宠物 {ItemDb.Get(t).Name}：弹幕 {p?.id}，帧 {p?.frame}，位置 {p?.at}，飞着 {p?.flying}");
                Shot("Town", "starpet_" + t);
                yield return new WaitForSeconds(0.3f);
            }
            inv.Misc[Pets.PetSlot] = default;
            inv.Misc[Pets.LightSlot] = default;
        }
    }
}
