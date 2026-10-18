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

            // The slime royals hop all the while they walk (AI_026 P:63206-63213: -5.1, falling 0.4 a tick); the Resplendent
            // Dessert calls both of them.
            foreach (var t in new[] { ItemType.KingSlimePetItem, ItemType.QueenSlimePetItem, ItemType.ResplendentDessert })
            {
                inv.Misc[Pets.PetSlot] = default;
                inv.Misc[Pets.LightSlot] = default;
                yield return null;
                inv.Misc[Pets.PetSlot] = ItemStack.Of(t);
                hero.transform.position = Clear(hero, start);
                yield return new WaitForSeconds(1.5f);
                // Across the ledge (the same walk as the other pets'): it walks after the player.
                hero.transform.position = Clear(hero, start + Vector2.right * 6f);
                float minVy = 0f, maxVx = 0f;
                int upTicks = 0;
                bool flew = false;
                var seen = new HashSet<int>();
                float t0 = Time.time;
                while (Time.time - t0 < 2.5f)
                {
                    yield return null;
                    var mo = Minions.DevPetMotion(false);
                    if (!mo.HasValue) continue;
                    if (mo.Value.ai0 != 0f) flew = true;
                    else
                    {
                        minVy = Mathf.Min(minVy, mo.Value.vel.y);
                        maxVx = Mathf.Max(maxVx, Mathf.Abs(mo.Value.vel.x));
                        if (mo.Value.vel.y < 0f) upTicks++;
                    }
                    var pp = Minions.DevPet(false);
                    if (pp.HasValue) seen.Add(pp.Value.frame);
                }
                var first = Minions.DevPetMotion(false);
                var twin = Minions.DevPetMotion(false, true);
                bool both = t != ItemType.ResplendentDessert || (first?.id == 881 && twin?.id == 934);
                Shot("Town", "pethop_" + t);
                yield return null;
                yield return null;
                Check($"{ItemDb.Get(t).Name} 走路时跳", minVy < -4f && maxVx > 1f && !flew && seen.Count >= 3 && both,
                    $"弹幕 {first?.id}/{twin?.id}，最大上升速度 {-minVy:F1}，上升帧数 {upTicks}，横速 {maxVx:F1}，帧 {string.Join(",", seen)}，飞了 {flew}");
            }

            // Chester (960): a right click in reach opens the Piggy Bank on him (his open frames 13-18 while he stands);
            // out of the player's reach it shuts; a click again shuts it too.
            inv.Misc[Pets.PetSlot] = default;
            inv.Misc[Pets.LightSlot] = default;
            yield return null;
            inv.Misc[Pets.PetSlot] = ItemStack.Of(ItemType.ChesterPetItem);
            hero.transform.position = Clear(hero, start);
            yield return new WaitForSeconds(2.5f);
            int opens0 = Minions.DevChesterOpens, closes0 = Minions.DevChesterCloses;
            bool clicked = Minions.Instance != null && Minions.Instance.DevClickChester();
            var bankFrames = new HashSet<int>();
            float c0 = Time.time;
            while (Time.time - c0 < 1.5f)
            {
                yield return null;
                var ch = Minions.DevChester();
                if (ch.HasValue) bankFrames.Add(ch.Value.frame);
            }
            bool bank = World.Chests.OpenBankKind == 0 && World.Chests.OpenWith == ItemType.ChesterPetItem && InventoryGui.Open;
            bool openFrames = bankFrames.Overlaps(new[] { 14, 15, 16, 17, 18 });
            Shot("Town", "pet_chester_open");
            yield return null;
            yield return null;
            Check("切斯特右键打开猪猪存钱罐", clicked && bank && openFrames && Minions.DevChesterOpens == opens0 + 1,
                $"点中 {clicked}，存储 {World.Chests.OpenBankKind}（{World.Chests.OpenWith}），帧 {string.Join(",", bankFrames)}");
            // Away past his reach (5 tiles across): shut.
            hero.transform.position = Clear(hero, start + Vector2.right * 9f);
            yield return new WaitForSeconds(0.3f);
            bool shut = World.Chests.OpenWith == ItemType.None && Minions.DevChesterCloses == closes0 + 1;
            Check("离切斯特太远关闭存钱罐", shut, $"存储 {World.Chests.OpenBankKind}（{World.Chests.OpenWith}），关闭次数 {Minions.DevChesterCloses - closes0}");
            // Back by him: open again, then a second click shuts it.
            hero.transform.position = Clear(hero, start);
            yield return new WaitForSeconds(3.5f);
            bool again = Minions.Instance != null && Minions.Instance.DevClickChester() && World.Chests.OpenWith == ItemType.ChesterPetItem;
            yield return new WaitForSeconds(0.3f);
            bool toggled = Minions.Instance != null && Minions.Instance.DevClickChester() && World.Chests.OpenWith == ItemType.None;
            var chEnd = Minions.DevChester();
            Check("再次右键切斯特关闭存钱罐", again && toggled, $"再开 {again}，再关 {toggled}，帧 {chEnd?.frame}");
            InventoryGui.Open = false;

            // Every other pet: out, following the player across the room, its frames moving.
            var tested = new HashSet<ItemType>(sample) { ItemType.KingSlimePetItem, ItemType.QueenSlimePetItem, ItemType.ResplendentDessert, ItemType.ChesterPetItem };
            int followed = 0, others = 0;
            var lost = new List<string>();
            foreach (var t in Pets.All)
            {
                if (tested.Contains(t) || t == ItemType.HkLumaflyLantern || t == ItemType.HkGrimmchild) continue;
                var def = Pets.Of(t);
                others++;
                inv.Misc[Pets.PetSlot] = default;
                inv.Misc[Pets.LightSlot] = default;
                yield return null;
                inv.Misc[def.Light ? Pets.LightSlot : Pets.PetSlot] = ItemStack.Of(t);
                hero.transform.position = Clear(hero, start);
                yield return new WaitForSeconds(1f);
                hero.transform.position = Clear(hero, start + Vector2.right * 6f);
                var frames = new HashSet<int>();
                bool finite = true;
                float t0 = Time.time;
                while (Time.time - t0 < 2.5f)
                {
                    yield return null;
                    var p = Minions.DevPet(def.Light);
                    if (!p.HasValue) continue;
                    frames.Add(p.Value.frame);
                    if (float.IsNaN(p.Value.at.x) || float.IsNaN(p.Value.at.y) || float.IsInfinity(p.Value.at.x)) finite = false;
                }
                var p1 = Minions.DevPet(def.Light);
                var heroPx = Effects.TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
                float d = p1.HasValue ? Vector2.Distance(p1.Value.at, heroPx) : -1f;
                Shot("Town", "petall_" + t);
                yield return null;
                yield return null;
                bool good = p1.HasValue && finite && d >= 0f && d < 400f;
                if (good) followed++;
                else lost.Add($"{t}({p1?.id}, {d:F0}px)");
                Plugin.Log.LogInfo($"[AutoTest] 宠物 跟随 {ItemDb.Get(t).Name}：弹幕 {p1?.id}，离主角 {d:F0} 像素，帧 {string.Join(",", frames)}，飞着 {p1?.flying} {(good ? "通过" : "不通过")}");
            }
            Check("其余宠物都跟着主角", followed == others, $"{followed}/{others}{(lost.Count > 0 ? "，没跟上：" + string.Join("、", lost) : "")}");
            inv.Misc[Pets.PetSlot] = default;
            inv.Misc[Pets.LightSlot] = default;
            Plugin.Log.LogInfo($"[AutoTest] 宠物 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
