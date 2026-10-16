using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pets"): Terraria's pets and light pets. Each of a walker, an orb, the fairy bell, flyers, held ones and the
    /// worms worn in its misc slot: out, following the player across the room, its frames moving, photographed. A pet item
    /// used calls its pet and sends it away again; another of its kind replaces it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PetsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 宠物 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            var start = (Vector2)hero.transform.position;
            var sample = new[]
            {
                ItemType.Carrot, ItemType.DogWhistle, ItemType.CompanionCube, ItemType.ShadowOrb, ItemType.FairyBell, ItemType.EatersBone, ItemType.ZephyrFish,
                ItemType.SkeletronPetItem, ItemType.SuspiciousLookingTentacle, ItemType.DD2PetDragon, ItemType.MagicLantern, ItemType.EaterOfWorldsPetItem,
                ItemType.DestroyerPetItem, ItemType.TwinsPetItem, ItemType.MoonLordPetItem, ItemType.BabyGrinchMischiefWhistle,
            };
            foreach (var t in sample)
            {
                var def = Pets.Of(t);
                if (def == null) { Check("有 " + t, false); continue; }
                int slot = def.Light ? Pets.LightSlot : Pets.PetSlot;
                inv.Misc[Pets.PetSlot] = default;
                inv.Misc[Pets.LightSlot] = default;
                yield return null;
                inv.Misc[slot] = ItemStack.Of(t);
                hero.transform.position = Clear(hero, start);
                yield return new WaitForSeconds(1.5f);
                var p0 = Minions.DevPet(def.Light);
                // Across the room: it follows.
                hero.transform.position = Clear(hero, start + Vector2.right * 6f);
                var frames = new HashSet<int>();
                float t0 = Time.time;
                while (Time.time - t0 < 2.5f)
                {
                    yield return null;
                    var p = Minions.DevPet(def.Light);
                    if (p.HasValue) frames.Add(p.Value.frame);
                }
                var p1 = Minions.DevPet(def.Light);
                var heroPx = Effects.TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
                float d = p1.HasValue ? Vector2.Distance(p1.Value.at, heroPx) : -1f;
                Shot("Town", "pet_" + t);
                yield return null;
                yield return null;
                bool buff = Player.PlayerBuffs.Has(def.Buff);
                Check($"{ItemDb.Get(t).Name}（{def.Projs[0].Move}）", p0.HasValue && p1.HasValue && d >= 0f && d < 400f && buff,
                    $"弹幕 {p1?.id}，离主角 {d:F0} 像素，帧 {string.Join(",", frames)}，飞着 {p1?.flying}，增益 {buff}");
            }
            inv.Misc[Pets.PetSlot] = default;
            inv.Misc[Pets.LightSlot] = default;
            yield return new WaitForSeconds(0.5f);
            // Terraria keeps a summoned pet's buff after its equipment is removed.
            Check("取下装备保留已召唤宠物", Minions.DevPet(false).HasValue && Pets.Current(false) != ItemType.None);
            var petOut = Pets.Current(false);
            var lightOut = Pets.Current(true);
            if (petOut != ItemType.None) Player.PlayerBuffs.Remove(Pets.Of(petOut).Buff);
            if (lightOut != ItemType.None) Player.PlayerBuffs.Remove(Pets.Of(lightOut).Buff);
            yield return new WaitForSeconds(0.5f);
            Check("取消增益收回宠物", !Minions.DevPet(false).HasValue && !Minions.DevPet(true).HasValue);

            // Used: called, a second of the kind replaces it, used again sent away.
            Pets.Use(ItemDb.Get(ItemType.Carrot));
            yield return new WaitForSeconds(0.5f);
            bool bunny = Minions.DevPet(false)?.id == 111;
            Pets.Use(ItemDb.Get(ItemType.DogWhistle));
            yield return new WaitForSeconds(0.5f);
            bool puppy = Minions.DevPet(false).HasValue && Minions.DevPet(false)?.id != 111 && !Player.PlayerBuffs.Has(Pets.Of(ItemType.Carrot).Buff);
            Pets.Use(ItemDb.Get(ItemType.DogWhistle));
            yield return new WaitForSeconds(0.5f);
            Check("使用物品召唤、换一只、再用收回", bunny && puppy && !Minions.DevPet(false).HasValue, $"兔子 {bunny}，小狗 {puppy}");
            Check("掉率", Mathf.Abs(Pets.KillChance - 1f / 10000f) < 1e-9f && Pets.Eggs.Count == 84, $"每只 1/10000，共 {Pets.Eggs.Count} 只");
            Plugin.Log.LogInfo($"[AutoTest] 宠物 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
