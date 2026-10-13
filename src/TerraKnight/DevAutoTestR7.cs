using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "r7", sweep R7): the hooks (one, two at once, the Lunar Hook's several), the
    /// permanent boosters (the Life Fruit past the crystals, the Demon Heart's sixth slot, the
    /// others once each), the potions' effects (defence, speed, life, knockback, the flasks on a
    /// sword hit and in place of one another, the Inferno ring, the slow fall, Battle and
    /// Calming, the Teleportation Potion), one herb for every monster, the chest (set down,
    /// opened, filled and emptied, kept through the save, broken only when empty) and patting
    /// the Cattiva.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator R7Test(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            Vector2 pinAt = C() + new Vector2(side * 2.5f, 0.3f);
            void Hold()
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 30000);
                Pin(target, trb, pinAt);
            }
            IEnumerator Wait(float s)
            {
                for (float t = 0f; t < s; t += Time.deltaTime)
                {
                    Hold();
                    yield return null;
                }
            }
            void Home()
            {
                hero.transform.position = home;
                rb.linearVelocity = Vector2.zero;
            }

            // --- The hooks: a ledge of stone up and ahead; one hook, then two at once, then the Lunar Hook's several.
            var hook = GrapplingHook.Instance;
            var feet = CellGrid.WorldToCell(new Vector2(C().x, col.bounds.min.y + 0.1f));
            int dir = side > 0f ? 1 : -1;
            var placed = new List<Vector2Int>();
            for (int k = 3; k <= 12; k++)
            {
                var c = new Vector2Int(feet.x + k * dir, feet.y + 10);
                if (TerraWorld.Get(c.x, c.y) == CellKind.Empty) { TerraWorld.Set(c.x, c.y, CellKind.Stone); placed.Add(c); }
            }
            yield return new WaitForSeconds(0.2f);
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.DualHook);
            Plugin.Log.LogInfo($"[AutoTest] R7 勾爪：背包里放了双钩，E 键会用 {GrapplingHook.HookItem(inv)}");
            foreach (var (item, throws) in new[] { (ItemType.AmethystHook, 1), (ItemType.DualHook, 2), (ItemType.LunarHook, 3), (ItemType.StaticHook, 2), (ItemType.QueenSlimeHook, 1) })
            {
                hook.DevRelease(hero);
                Home();
                yield return new WaitForSeconds(0.4f);
                int latch0 = GrapplingHook.DevLatches;
                for (int n = 0; n < throws; n++)
                {
                    hook.Throw(hero, item, CellGrid.CellCenter(feet.x + (4 + n * 3) * dir, feet.y + 10));
                    yield return new WaitForSeconds(0.25f);
                }
                yield return new WaitForSeconds(0.35f);
                if (item == ItemType.LunarHook) yield return CloseUpAt(scene, "r7_lunar_hook", (C() + hook.DevHookPosition) / 2f, 220, 1);
                Plugin.Log.LogInfo($"[AutoTest] R7 勾爪 {ItemDb.Get(item).Name}：抛出 {throws} 次，钩住 {GrapplingHook.DevLatches - latch0} 次，现在挂着 {hook.DevHolding} 个（共 {hook.DevOut} 个在外），状态 {hook.DevState}");
            }
            hook.DevRelease(hero);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
            inv.Slots[Inventory.Size - 1] = default;
            Home();
            yield return new WaitForSeconds(0.8f);

            // --- The boosters.
            var st = mod.Stats;
            int savedCrystals = st.LifeCrystals, savedFruits = st.LifeFruits;
            var savedBoosts = new HashSet<PermanentBoost>(st.Boosts);
            st.Boosts.Clear();
            st.LifeFruits = 0;
            st.LifeCrystals = 3;
            yield return null;
            int fruitsBefore = st.LifeFruits;
            mod.DevUse(ItemType.LifeFruit, C());
            string earlyFruit = st.LifeFruits == fruitsBefore ? "不能吃（原版要先吃满 15 个生命水晶）" : "吃下了（不对）";
            st.LifeCrystals = TerraStats.MaxLifeCrystals;
            yield return new WaitForSeconds(0.1f);
            int life0 = st.LifeMax;
            mod.DevUse(ItemType.LifeFruit, C());
            yield return new WaitForSeconds(0.1f);
            int life1 = st.LifeMax;
            int open0 = Inventory.OpenAccessories;
            mod.DevUse(ItemType.DemonHeart, C());
            int def0 = Equipment.Current.Defense;
            mod.DevUse(ItemType.AegisFruit, C());
            yield return new WaitForSeconds(0.1f);
            int def1 = Equipment.Current.Defense;
            foreach (var b in new[] { ItemType.AegisCrystal, ItemType.ArcaneCrystal, ItemType.GalaxyPearl, ItemType.GummyWorm, ItemType.Ambrosia, ItemType.ArtisanLoaf }) mod.DevUse(b, C());
            int boostsAfter = st.Boosts.Count;
            int twice0 = PotionBuffs.DevBoosters;
            mod.DevUse(ItemType.AegisFruit, C());
            yield return new WaitForSeconds(0.1f);
            float luck = Equipment.Current.Luck;
            Plugin.Log.LogInfo($"[AutoTest] R7 永久强化：水晶没吃满时生命果{earlyFruit}；吃满后生命上限 {life0} -> {life1}（+5），饰品栏 {open0} -> {Inventory.OpenAccessories}（恶魔之心 +1），" +
                               $"神盾果 防御 {def0} -> {def1}（+4），共用掉 {boostsAfter} 种（应 9 种中除生命果外的 8 种），再吃一次神盾果 {(PotionBuffs.DevBoosters == twice0 ? "不能再吃" : "又吃了（不对）")}，星系珍珠运气 {luck:F2}");
            st.Boosts.Clear();
            st.Boosts.UnionWith(savedBoosts);
            st.LifeFruits = savedFruits;
            st.LifeCrystals = savedCrystals;
            inv.RefreshEffective();
            yield return new WaitForSeconds(0.2f);

            // --- The potions.
            PlayerBuffs.Clear();
            yield return null;
            var swordDef = ItemDb.Get(ItemType.IronBroadsword);
            int pd0 = Equipment.Current.Defense;
            float ms0 = Equipment.Current.MoveSpeed, kb0 = Equipment.Knockback(swordDef), end0 = Equipment.Current.Endurance, md0 = Equipment.Current.MeleeDamage, mc0 = Equipment.Current.MeleeCrit;
            int lm0 = st.LifeMax;
            foreach (var p in new[] { ItemType.IronskinPotion, ItemType.SwiftnessPotion, ItemType.TitanPotion, ItemType.LifeforcePotion, ItemType.EndurancePotion, ItemType.WrathPotion, ItemType.RagePotion,
                                      ItemType.GravitationPotion, ItemType.SpelunkerPotion, ItemType.HunterPotion, ItemType.TrapsightPotion, ItemType.ShinePotion, ItemType.NightOwlPotion, ItemType.LuckPotion })
                mod.DevUse(p, C());
            yield return new WaitForSeconds(0.3f);
            var e = Equipment.Current;
            Plugin.Log.LogInfo($"[AutoTest] R7 药水：防御 {pd0} -> {e.Defense}（铁皮 +8），移速 {ms0:F2} -> {e.MoveSpeed:F2}（敏捷 +0.25），击退 {kb0:F2} -> {Equipment.Knockback(swordDef):F2}（泰坦 ×1.5），" +
                               $"生命上限 {lm0} -> {st.LifeMax}（生命力 +20%），减伤 {end0:F2} -> {e.Endurance:F2}（耐力 +0.1），近战伤害 {md0:F2} -> {e.MeleeDamage:F2}（怒气 +0.1），近战暴击 {mc0:F2} -> {e.MeleeCrit:F2}（暴怒 +0.1），" +
                               $"重力控制 {e.GravityControl}，宝藏 {e.FindTreasure}，猎人 {e.DetectCreature}，危险感 {e.DangerSense}，光芒 {e.Shine}，夜视 {e.NightVision}，运气 {e.Luck:F2}（幸运药水剩余时间长 0.3），身上 Buff {CountBuffs()} 个");
            if (target != null)
            {
                pinAt = C() + new Vector2(side * 1.2f, 0.2f);
                yield return Wait(0.5f);
                yield return CloseUpAt(scene, "r7_senses", C(), 260, 1);
            }
            // The flasks: Fire on a sword hit; Ichor drunk after takes its place.
            int imb0 = PotionBuffs.DevImbues;
            mod.DevUse(ItemType.FlaskofFire, C());
            bool fireBuff = PlayerBuffs.Has(74);
            bool onFire = false;
            if (target != null)
            {
                for (int k = 0; k < 4 && !onFire; k++)
                {
                    mod.DevUse(ItemType.IronBroadsword, pinAt);
                    yield return Wait(0.4f);
                    onFire = EnemyDebuffs.Has(target, EnemyDebuffs.OnFire);
                }
            }
            mod.DevUse(ItemType.FlaskofIchor, C());
            yield return new WaitForSeconds(0.1f);
            Plugin.Log.LogInfo($"[AutoTest] R7 灌注药剂：烈火 Buff {fireBuff}，剑砍中触发灌注 {PotionBuffs.DevImbues - imb0} 次，敌人着火 {onFire}；喝灵液后 烈火 {PlayerBuffs.Has(74)}（应 False），灵液 {PlayerBuffs.Has(76)}，近战附魔 {Equipment.Current.MeleeEnchant}（5）");
            // The Inferno ring: the pinned enemy within 200 px catches hellfire and is struck once a second.
            int inf0 = PotionBuffs.DevInfernoHits;
            mod.DevUse(ItemType.InfernoPotion, C());
            pinAt = C() + new Vector2(side * 3.5f, 0.2f);
            yield return Wait(1.2f);
            if (target != null) yield return CloseUpAt(scene, "r7_inferno", C(), 560, 1);
            yield return Wait(1.4f);
            Plugin.Log.LogInfo($"[AutoTest] R7 狱火药水：2.6 秒内火圈击中 {PotionBuffs.DevInfernoHits - inf0} 次（每秒一次，20 伤害），敌人地狱火 {(target != null && EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire))}");
            PlayerBuffs.Remove(PotionBuffs.Inferno);
            // Battle and Calming on the enemies' return.
            float f0 = PotionBuffs.RespawnFactor;
            mod.DevUse(ItemType.BattlePotion, C());
            yield return null;
            float f1 = PotionBuffs.RespawnFactor;
            PlayerBuffs.Remove(PotionBuffs.Battle);
            mod.DevUse(ItemType.CalmingPotion, C());
            yield return null;
            float f2 = PotionBuffs.RespawnFactor;
            Plugin.Log.LogInfo($"[AutoTest] R7 战斗/镇静：敌人复活等待倍数 {f0:F2} -> 战斗 {f1:F2}（0.5）/ 镇静 {f2:F2}（1.3）");
            PlayerBuffs.Clear();
            // Featherfall: a fall from high up without, then with the potion.
            float FallSpeed()
            {
                return -rb.linearVelocity.y / TerraMovement.PxT;
            }
            Vector2 sky = C();
            for (float h = 8f; h >= 3f; h -= 1f)
            {
                var up = C() + Vector2.up * h;
                if (Physics2D.OverlapBox(up, col.bounds.size * 1.2f, 0f, terrain) == null && Physics2D.Raycast(C(), Vector2.up, h, terrain).collider == null) { sky = up; break; }
            }
            hero.transform.position += (Vector3)(sky - C());
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.35f);
            float plain = FallSpeed();
            Home();
            yield return new WaitForSeconds(0.4f);
            mod.DevUse(ItemType.FeatherfallPotion, C());
            hero.transform.position += (Vector3)(sky - C());
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.35f);
            float slow = FallSpeed();
            Plugin.Log.LogInfo($"[AutoTest] R7 羽落药水：从 {(sky.y - home.y):F1} 单位高处落下 0.35 秒后的下落速度 不喝 {plain:F1} / 喝了 {slow:F1} px/tick（重力 1/3）");
            PlayerBuffs.Clear();
            Home();
            yield return new WaitForSeconds(0.6f);
            // The Teleportation Potion: elsewhere in this scene, on ground.
            int tel0 = PotionBuffs.DevTeleports;
            Vector2 before = C();
            mod.DevUse(ItemType.TeleportationPotion, C());
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] R7 传送药水：传送 {PotionBuffs.DevTeleports - tel0} 次，从 {before:F1} 到 {C():F1}（{Vector2.Distance(before, C()):F1} 单位），落地 {hero.cState.onGround}");
            Home();
            // Honey and the potion sickness.
            st.PotionSickness = 0f;
            st.HoneyTicks = 0f;
            int lifeBefore = st.Life = Mathf.Max(1, st.LifeMax - 100);
            mod.DevUse(ItemType.BottledHoney, C());
            Plugin.Log.LogInfo($"[AutoTest] R7 瓶装蜂蜜：生命 {lifeBefore} -> {st.Life}（+80），蜂蜜 {st.HoneyTicks:F0} tick，药水病 {st.PotionSickness:F0} 秒");
            st.PotionSickness = 0f;
            st.FullHeal();
            yield return new WaitForSeconds(0.5f);

            // --- The herbs: one kind for every monster.
            var herbs = new HashSet<ItemType> { ItemType.Daybloom, ItemType.Moonglow, ItemType.Blinkroot, ItemType.Deathweed, ItemType.Waterleaf, ItemType.Fireblossom, ItemType.Shiverthorn };
            int records = 0, withOne = 0, odd = 0;
            var byHerb = new Dictionary<ItemType, int>();
            foreach (var r in EnemyDrops.DevRecords)
            {
                if (EnemyDrops.KindOf(r) == "non_enemy") continue;
                records++;
                int n = 0;
                foreach (var d in EnemyDrops.Of(r))
                    if (herbs.Contains(d.Type))
                    {
                        n++;
                        byHerb[d.Type] = byHerb.TryGetValue(d.Type, out var v) ? v + 1 : 1;
                    }
                if (n == 1) withOne++;
                else odd++;
            }
            var herbText = new System.Text.StringBuilder();
            foreach (var kv in byHerb) herbText.Append(ItemDb.Get(kv.Key).Name).Append(' ').Append(kv.Value).Append(' ');
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            for (int k = 0; k < 60; k++) EnemyDrops.Roll("Crawler", C(), true);
            int gotHerb = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && herbs.Contains(s.Type)) gotHerb += s.Count;
            Plugin.Log.LogInfo($"[AutoTest] R7 草药：{records} 种怪物中 {withOne} 种恰好掉一种草药，{odd} 种不是（应 0）；分布 {herbText}；爬虫掉 60 次得到草药 {gotHerb} 株（1/4 几率 1-2 株，约 22）");
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            yield return new WaitForSeconds(0.3f);

            // --- The chest: bought, set down beside the player, filled and emptied, kept through the save, broken when empty.
            Home();
            yield return new WaitForSeconds(0.3f);
            int chests0 = Chests.Count;
            var groundAim = new Vector2(C().x + side * 1.5f, col.bounds.min.y + 0.2f);
            bool canGo = Chests.FindSpot(hero, groundAim, out var spot);
            inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(ItemType.Chest, 2);
            inv.Selected = Inventory.HotbarSize - 1;
            mod.DevAim = groundAim;
            mod.DevUseSelected(groundAim);
            yield return new WaitForSeconds(0.3f);
            mod.DevAim = null;
            int left = inv.Slots[Inventory.HotbarSize - 1].Count;
            Plugin.Log.LogInfo($"[AutoTest] R7 箱子 放置：能放 {canGo}（{spot:F2}），箱子 {chests0} -> {Chests.Count}，手里剩 {left}（2 -> 1），来源 {ItemDb.Get(ItemType.Chest).Source}（Shop），价格 {Shop.Money(ItemDb.Get(ItemType.Chest).Value)}");
            bool opened = Chests.DevOpenNear(C());
            yield return new WaitForSeconds(0.4f);
            int frame = Chests.DevOpenFrame;
            inv.Slots[20] = ItemStack.Of(ItemType.Daybloom, 50);
            inv.Slots[31] = ItemStack.Of(ItemType.IronBar, 30);
            inv.Slots[32] = ItemStack.Of(ItemType.Waterleaf, 7);
            inv.Slots[32].Favorite = true;
            InventoryGui.DevShift = true;
            InventoryGui.DevLeftClick(20);
            InventoryGui.DevShift = false;
            bool movedIn = inv.Slots[20].IsEmpty;
            InventoryGui.DevChestButton(1);
            bool deposited = inv.Slots[31].IsEmpty, favKept = !inv.Slots[32].IsEmpty;
            yield return null;
            Shot(scene, "r7_chest_open");
            yield return new WaitForSeconds(0.2f);
            int held = 0;
            foreach (var s in Chests.OpenItems) if (!s.IsEmpty) held++;
            // Kept through the save.
            var saved = Chests.Save();
            Chests.Load(saved);
            yield return null;
            int afterLoad = Chests.Count;
            Chests.DevOpenNear(C());
            int heldAfter = 0;
            foreach (var s in Chests.OpenItems ?? new ItemStack[0]) if (!s.IsEmpty) heldAfter++;
            // A full chest can't be broken.
            var pick = ItemDb.Get(ItemType.CopperPickaxe);
            var chestAt = Chests.DevNearest(C()) ?? C();
            Chests.Close(false);
            for (int k = 0; k < 4; k++) Chests.TryMine(pick, chestAt, hero);
            int afterFullMine = Chests.Count;
            Chests.DevOpenNear(C());
            InventoryGui.DevChestButton(0);
            int backIn = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && (s.Type == ItemType.Daybloom || s.Type == ItemType.IronBar)) backIn += s.Count;
            Chests.Close(false);
            InventoryGui.Open = false;
            int dropped0 = WorldItems.Count, broken0 = Chests.DevBroken;
            int chestItems0 = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == ItemType.Chest) chestItems0 += s.Count;
            for (int k = 0; k < 4; k++) Chests.TryMine(pick, chestAt, hero);
            int droppedNow = WorldItems.Count;
            yield return new WaitForSeconds(1f);
            int chestItems1 = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == ItemType.Chest) chestItems1 += s.Count;
            Plugin.Log.LogInfo($"[AutoTest] R7 箱子 使用：打开 {opened}，盖子帧 {frame}（2 全开），Shift 点击存入 {movedIn}，全部存放 {deposited}（收藏的留下 {favKept}），箱子里 {held} 格；" +
                               $"存档往返后 箱子 {afterLoad} 个、{heldAfter} 格；有东西时挖 4 下还剩 {afterFullMine} 个；全部拿取后背包里 {backIn} 个（80）；空箱子挖 4 下（铜镐 35）碎了 {Chests.DevBroken - broken0} 个，掉出物品 {dropped0} -> {droppedNow}，一秒后背包里的宝箱 {chestItems0} -> {chestItems1}（被吸回来）");
            yield return new WaitForSeconds(0.5f);

            // --- Patting the Cattiva.
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            Home();
            mod.DevUse(ItemType.CattivaStaff, C() + new Vector2(side * 1.5f, 0.5f));
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) yield return null;
            int pets0 = Minions.DevPets;
            Vector2 heroBefore = C();
            bool petted = false;
            var why = new System.Text.StringBuilder();
            for (float t = 0f; t < 3f && !petted; t += 0.25f)
            {
                petted = Minions.Instance != null && Minions.Instance.DevPet(hero, Minions.Kind.Cattiva);
                if (!petted) why.Append(Minions.DevPetWhy).Append("; ");
                if (!petted) yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.25f);
            bool petting = Minions.Petting;
            yield return CloseUp(scene, "r7_pet", hero, 120, 3);
            yield return new WaitForSeconds(0.4f);
            bool still = Minions.Petting;
            hero.transform.position += Vector3.right * side * 2f;
            yield return new WaitForSeconds(0.2f);
            Plugin.Log.LogInfo($"[AutoTest] R7 摸捣蛋猫：摸了 {petted}（次数 +{Minions.DevPets - pets0}，没摸成的原因 {why}），主角挪到 {heroBefore:F2} -> 猫旁，正在摸 {petting}，0.4 秒后仍在摸 {still}，走开后 {Minions.Petting}（应 False）");
            PlayerBuffs.Remove(PlayerBuffs.CattivaMinion);
            Minions.Instance?.Clear();

            // --- Wings with the Gravity Globe: drawn upside down with the player.
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.GravityGlobe);
            inv.Accessories[1] = ItemStack.Of(ItemType.AngelWings);
            inv.RefreshEffective();
            Home();
            yield return new WaitForSeconds(0.6f);
            GravityGlobe.DevUpPress = true;
            yield return new WaitForSeconds(0.2f);
            bool flippedNow = GravityGlobe.Flipped;
            yield return CloseUp(scene, "r7_wings_flipped", hero, 90, 3);
            GravityGlobe.DevUpPress = true;
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] R7 重力球加翅膀：翻转 {flippedNow}（截图里翅膀应跟人一起倒过来），之后翻回 {!GravityGlobe.Flipped}");
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = default;
            inv.RefreshEffective();
            yield return new WaitForSeconds(1.2f);

            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = savedSlots[i];
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            inv.Selected = savedSelected;
            inv.RefreshEffective();
            Home();
            mod.DevNoFallDamage = false;
        }

        static int CountBuffs()
        {
            int n = 0;
            for (int i = 0; i < PlayerBuffs.MaxBuffs; i++) if (PlayerBuffs.Type[i] != 0) n++;
            return n;
        }
    }
}
