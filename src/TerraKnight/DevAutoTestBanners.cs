using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "banners"): the kill banners (Items.Banners) and the Banner Case and the Potion Box (Items.Containers).
    /// Every record has a banner with its picture; Terraria's BannerEffect numbers at each difficulty; a record's 19th kill
    /// drops nothing and its 20th its banner; picked up it goes into the carried case (the bag without it); the damage
    /// dealt to the record's enemy and taken from it (its body, a shot it spawned) change by the numbers with the banner
    /// carried (in the bag, in the carried case) and not otherwise, through the real hit paths too; the Monster Banner
    /// buff; the panel's pages and refusals; the Potion Box: what it takes, Alt+click's favourite from 20, the held buff
    /// with no time and nothing used up, gone under 20 / unfavourited / not carried, the luck potion's own luck, one
    /// flask, Quick Heal and Quick Buff from it; the save keeping both with the favourite; English names.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        int bannerPass, bannerFail;

        void BannerCheck(string what, bool ok, string detail)
        {
            if (ok) bannerPass++;
            else bannerFail++;
            Plugin.Log.LogInfo($"[AutoTest] 旗帜 {what}：{detail}{(ok ? " 通过" : " 不通过")}");
        }

        static int CountInBag(Inventory inv, ItemType t)
        {
            int n = 0;
            foreach (var s in inv.Slots) if (!s.IsEmpty && s.Type == t) n += s.Count;
            return n;
        }

        static void ClearPages(ItemStack[][] pages)
        {
            foreach (var p in pages) System.Array.Clear(p, 0, p.Length);
        }

        IEnumerator BannersTest()
        {
            if (GameManager.instance.sceneName != "Crossroads_01")
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1.5f);
            }
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            var hero = HeroController.instance;
            string scene = GameManager.instance.sceneName;
            var diff = GameDifficulty.Current;

            // --- The items.
            int known = 0, pictures = 0;
            var missing = new List<string>();
            foreach (var t in Banners.Types())
            {
                string rec = Banners.RecordOf(t);
                if (EnemyDrops.Known(rec) || EnemyDrops.Known(rec.Split('@')[0])) known++;
                else missing.Add(rec);
                var tex = Terraria.TerrariaAssets.Texture(ItemDb.Get(t).Texture);
                if (tex != null && tex.width == 16 && tex.height == 48) pictures++;
            }
            BannerCheck("127 条图鉴记录各有一面旗帜和 16x48 贴图", Banners.Count == 127 && known == 127 && pictures == 127,
                $"旗帜 {Banners.Count}，记录在图鉴里 {known}（缺 {string.Join(",", missing)}），贴图 {pictures}");
            var crawlerBanner = ItemDb.Get(ItemType.BannerCrawler);
            BannerCheck("旗帜的名字、来源、堆叠、价值照原版", crawlerBanner.Name.EndsWith("旗") && crawlerBanner.Source == ItemSource.Drop && crawlerBanner.MaxStack == 9999
                && crawlerBanner.Value == 1000 && crawlerBanner.Rare == 1 && Banners.Of("ZombieMiner@0") == ItemType.BannerZombieMinerAt0 && Banners.Of("ZombieMiner") == ItemType.BannerZombieMiner,
                $"{crawlerBanner.Name}，来源 {crawlerBanner.Source}，堆叠 {crawlerBanner.MaxStack}，价值 {crawlerBanner.Value}，稀有度 {crawlerBanner.Rare}；{crawlerBanner.Tooltip.Replace("\n", " / ")}");
            var caseDef = ItemDb.Get(ItemType.BannerCase);
            var boxDef = ItemDb.Get(ItemType.PotionBox);
            var caseTex = Terraria.TerrariaAssets.Texture(caseDef.Texture);
            var boxTex = Terraria.TerrariaAssets.Texture(boxDef.Texture);
            bool caseRecipe = false, boxRecipe = false;
            foreach (var r in Recipes.All) { if (r.Result == ItemType.BannerCase) caseRecipe = true; if (r.Result == ItemType.PotionBox) boxRecipe = true; }
            BannerCheck("旗帜盒和药水箱：贴图、可使用、能合成", caseTex != null && caseTex.width == 32 && boxTex != null && boxTex.width == 32 && caseDef.IsUsable && boxDef.IsUsable
                && caseRecipe && boxRecipe && caseDef.Source == ItemSource.Craft && boxDef.Source == ItemSource.Craft,
                $"{caseDef.Name} {caseTex?.width}x{caseTex?.height}，{boxDef.Name} {boxTex?.width}x{boxTex?.height}，可使用 {caseDef.IsUsable}/{boxDef.IsUsable}，配方 {caseRecipe}/{boxRecipe}");

            // --- Terraria's numbers (BannerEffect(1f) sampled at Main.Difficulty).
            float[] dealt = { 1.5f, 1.75f, 2f, 2f }, taken = { 0.75f, 0.625f, 0.5f, 0.5f };
            var levels = new[] { DifficultyLevel.Normal, DifficultyLevel.Hard, DifficultyLevel.Expert, DifficultyLevel.Master };
            bool numbers = true;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                float d = Banners.DealtFactor(levels[i]), t = Banners.TakenFactor(levels[i]);
                numbers &= Mathf.Abs(d - dealt[i]) < 1e-5f && Mathf.Abs(t - taken[i]) < 1e-5f;
                sb.Append($"{GameDifficulty.NameOf(levels[i])} 伤害×{d} 受伤×{t}；");
            }
            BannerCheck("旗帜增伤减伤按原版难度曲线", numbers, sb.ToString());

            // --- An enemy of a banner's record.
            HealthManager Find(HealthManager not = null)
            {
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm == null || hm == not || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                    var info = EnemyStats.Of(hm);
                    if (info == null || info.Boss || Banners.BannerOf(hm) == ItemType.None) continue;
                    return hm;
                }
                return null;
            }
            var target = Find();
            if (target == null)
            {
                BannerCheck("场景里有带旗帜的敌人", false, scene);
                inv.Infinite = infinite;
                yield break;
            }
            var banner = Banners.BannerOf(target);
            string key = Bestiary.KeyOf(target);
            BannerCheck("敌人对上自己的旗帜", Banners.Of(key) == banner, $"{target.name} 记录 {key} → {ItemDb.Get(banner).Name}");
            const int Slot = 25, CaseSlot = 26, BoxSlot = 27;
            for (int i = Slot; i <= BoxSlot + 2; i++) inv.Slots[i] = default;
            ClearPages(inv.BannerCase);
            ClearPages(inv.PotionBox);
            float fd = Banners.DealtFactor(GameDifficulty.Current), ft = Banners.TakenFactor(GameDifficulty.Current);
            Banners.Refresh(true);
            int d0 = Banners.Dealt(target, 100), t0 = Banners.Taken(target.gameObject, 100);
            inv.Slots[Slot] = ItemStack.Of(banner);
            Banners.Refresh(true);
            int d1 = Banners.Dealt(target, 100), t1 = Banners.Taken(target.gameObject, 100);
            BannerCheck("背包里有旗帜才加成", d0 == 100 && t0 == 100 && d1 == (int)(100 * fd) && t1 == (int)(100 * ft),
                $"没旗帜 造成 {d0} 受到 {t0}；有旗帜 造成 {d1}（应 {(int)(100 * fd)}）受到 {t1}（应 {(int)(100 * ft)}）");
            inv.Slots[Slot] = default;
            inv.BannerCase[0][0] = ItemStack.Of(banner);
            Banners.Refresh(true);
            int d2 = Banners.Dealt(target, 100);
            inv.Slots[CaseSlot] = ItemStack.Of(ItemType.BannerCase);
            Banners.Refresh(true);
            int d3 = Banners.Dealt(target, 100), t3 = Banners.Taken(target.gameObject, 100);
            BannerCheck("旗帜盒里的旗帜：带着盒子才生效", d2 == 100 && d3 == (int)(100 * fd) && t3 == (int)(100 * ft), $"盒子不在身上 {d2}，在身上 {d3}/{t3}");

            // The Piggy Bank: a banner in it works, and a Banner Case lying in it as if carried.
            const int BankA = 38, BankB = 39;
            inv.Bank[BankA] = inv.Bank[BankB] = default;
            inv.Slots[CaseSlot] = default;
            inv.BannerCase[0][0] = default;
            inv.Bank[BankA] = ItemStack.Of(banner);
            Banners.Refresh(true);
            int dBank = Banners.Dealt(target, 100);
            inv.Bank[BankA] = default;
            inv.BannerCase[0][0] = ItemStack.Of(banner);
            inv.Bank[BankB] = ItemStack.Of(ItemType.BannerCase);
            Banners.Refresh(true);
            int dBankCase = Banners.Dealt(target, 100);
            inv.Bank[BankB] = default;
            Banners.Refresh(true);
            int dNone = Banners.Dealt(target, 100);
            BannerCheck("猪猪存钱罐里的旗帜、存钱罐里的旗帜盒也生效", dBank == (int)(100 * fd) && dBankCase == (int)(100 * fd) && dNone == 100,
                $"存钱罐里的旗帜 {dBank}，存钱罐里的旗帜盒 {dBankCase}，都拿走 {dNone}");
            // Picked up with the case only in the Piggy Bank: into the case (the user's 10-30 "让旗帜盒放在存钱罐里也能自动收纳旗帜").
            ClearPages(inv.BannerCase);
            inv.Bank[BankB] = ItemStack.Of(ItemType.BannerCase);
            int leftBank = Containers.Store(inv, ItemStack.Of(banner, 3), 3);
            bool roomBank = Containers.HasRoom(inv, banner);
            int caseHeld = inv.BannerCase[0][0].Type == banner ? inv.BannerCase[0][0].Count : 0;
            inv.Bank[BankB] = default;
            int leftNoCase = Containers.Store(inv, ItemStack.Of(banner, 1), 1);
            BannerCheck("旗帜盒放在存钱罐里，拾取的旗帜也收进盒子", leftBank == 0 && roomBank && caseHeld == 3 && leftNoCase == 1,
                $"收进 {3 - leftBank}/3，有空位 {roomBank}，盒子里 {caseHeld}；盒子不在身上也不在存钱罐时不收 {leftNoCase == 1}");
            ClearPages(inv.BannerCase);
            inv.BannerCase[0][0] = ItemStack.Of(banner);
            inv.Slots[CaseSlot] = ItemStack.Of(ItemType.BannerCase);
            Banners.Refresh(true);

            // A shot the enemy spawned (and a part of it) answers to its banner.
            var shot = new GameObject("Banner Test Shot");
            var part = new GameObject("Banner Test Shot Part");
            part.transform.SetParent(shot.transform, false);
            Banners.Mark(shot, target.gameObject);
            int ts = Banners.Taken(part, 100);
            BannerCheck("敌人放出的子弹也按它的旗帜减伤", Banners.SourceBanner(part) == banner && ts == (int)(100 * ft), $"子弹的旗帜 {Banners.SourceBanner(part)}，受到 {ts}；实际场景里标记过的子弹 {Banners.DevShotsMarked} 发");
            Object.Destroy(shot);

            // The Monster Banner buff.
            yield return null;
            yield return null;
            bool buffOn = Player.PlayerBuffs.Has(Banners.MonsterBannerBuff) && Player.PlayerBuffs.NoTime(Banners.MonsterBannerBuff);
            var lines = new List<string>();
            Banners.BuffLines(Banners.MonsterBannerBuff, lines);
            BannerCheck("带着旗帜有旗帜增益（不显示时间），列出怪名", buffOn && lines.Contains(Banners.MonsterName(banner)),
                $"增益 {buffOn}：{Player.PlayerBuffs.Name(Banners.MonsterBannerBuff)} {Player.PlayerBuffs.Desc(Banners.MonsterBannerBuff)} {string.Join("、", lines)}");

            // The real hit paths: a hit on it, a touch from it.
            int c0 = Banners.DevDealtCount;
            var tcol = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
            bool struck = tcol != null && mod.Melee.HitWith(tcol, 4, 0f, 0f, 1f, hero.transform.position);
            BannerCheck("打到敌人时伤害按旗帜提高（变化前）", struck && Banners.DevDealtCount == c0 + 1 && Banners.DevDealtIn == 4 && Banners.DevDealtOut == (int)(4 * fd),
                $"打中 {struck}，加成次数 {Banners.DevDealtCount - c0}，{Banners.DevDealtIn} → {Banners.DevDealtOut}");
            yield return new WaitForSeconds(0.5f);
            if (target == null || target.isDead) target = Find();
            int lost0 = -1, lost1 = -1, tc = 0;
            if (target != null)
            {
                banner = Banners.BannerOf(target);
                inv.BannerCase[0][0] = default;
                Banners.Refresh(true);
                // Hittable only for the one call (the hit is taken within it): no other enemy's touch counts.
                mod.Stats.Life = mod.Stats.LifeMax;
                int before = mod.Stats.Life;
                pd.isInvincible = false;
                hero.TakeDamage(target.gameObject, GlobalEnums.CollisionSide.left, 1, 1);
                pd.isInvincible = true;
                lost0 = before - mod.Stats.Life;
                yield return new WaitForSeconds(2.6f);
                inv.BannerCase[0][0] = ItemStack.Of(banner);
                Banners.Refresh(true);
                mod.Stats.Life = mod.Stats.LifeMax;
                before = mod.Stats.Life;
                tc = Banners.DevTakenCount;
                pd.isInvincible = false;
                if (target != null && !target.isDead) hero.TakeDamage(target.gameObject, GlobalEnums.CollisionSide.left, 1, 1);
                pd.isInvincible = true;
                lost1 = before - mod.Stats.Life;
                tc = Banners.DevTakenCount - tc;
                mod.Stats.Life = mod.Stats.LifeMax;
                yield return new WaitForSeconds(2.6f);
            }
            BannerCheck("被敌人碰到时伤害按旗帜降低（防御前）", lost0 > 0 && lost1 > 0 && lost1 < lost0 && tc == 1 && Banners.DevTakenOut == (int)(Banners.DevTakenIn * ft),
                $"没旗帜掉 {lost0}，有旗帜掉 {lost1}；每面具伤害 {Banners.DevTakenIn} → {Banners.DevTakenOut}，次数 {tc}");

            // --- Kills: the 19th nothing, the 20th the banner.
            ClearPages(inv.BannerCase);
            inv.Slots[CaseSlot] = default;
            Banners.Refresh(true);
            var a = Find();
            var b = a != null ? Find(a) : null;
            if (a != null && b != null)
            {
                string ka = Bestiary.KeyOf(a);
                Bestiary.Kills[ka] = 17;
                if (!Bestiary.Order.Contains(ka)) Bestiary.Order.Add(ka);
                int dropped0 = Banners.DevDropped;
                a.Die(null, AttackTypes.Generic, true);
                yield return new WaitForSeconds(0.5f);
                BannerCheck("第 18 次击杀不掉旗帜", Banners.DevDropped == dropped0 && Bestiary.KillsOf(ka) == 18, $"{ka} 击杀 {Bestiary.KillsOf(ka)}，掉落 {Banners.DevDropped - dropped0}");
            }
            else Plugin.Log.LogInfo("[AutoTest] 旗帜 场景里只有一个带旗帜的敌人：跳过第 18 次击杀");
            var killed = b ?? a;
            if (killed != null)
            {
                string kb = Bestiary.KeyOf(killed);
                var bb = Banners.Of(kb);
                Bestiary.Kills[kb] = 19;
                if (!Bestiary.Order.Contains(kb)) Bestiary.Order.Add(kb);
                int dropped0 = Banners.DevDropped, lying0 = WorldItems.DevCount(bb), bag0 = CountInBag(inv, bb);
                WorldItems.DevNoGrab = true;
                killed.Die(null, AttackTypes.Generic, true);
                yield return new WaitForSeconds(0.6f);
                int lying = WorldItems.DevCount(bb);
                BannerCheck("第 20 次击杀掉一面旗帜", Banners.DevDropped == dropped0 + 1 && Bestiary.KillsOf(kb) == 20 && lying == lying0 + 1,
                    $"{kb} 击杀 {Bestiary.KillsOf(kb)}，掉落 {Banners.DevDropped - dropped0}，地上 {lying0} → {lying}");
                // Picked up with the case carried: into the case, not the bag.
                inv.Slots[CaseSlot] = ItemStack.Of(ItemType.BannerCase);
                Vector2? at = null;
                foreach (var it in WorldItems.DevItems) if (it.Stack.Type == bb) at = it.At;
                WorldItems.DevNoGrab = false;
                if (at.HasValue) hero.transform.position = Clear(hero, at.Value);
                float until = Time.time + 5f;
                while (Time.time < until && WorldItems.DevCount(bb) > lying0) yield return null;
                int inCase = Containers.CountIn(inv, Containers.CaseKind, bb);
                BannerCheck("掉落的旗帜自动进旗帜盒", inCase == 1 && CountInBag(inv, bb) == bag0, $"盒里 {inCase}，背包 {CountInBag(inv, bb)}（之前 {bag0}），地上 {WorldItems.DevCount(bb)}");
                Shot(scene, "banners_kill_pickup");
            }
            WorldItems.DevNoGrab = false;

            // --- Pickups with and without the containers (the kills' own loot cleared away first).
            WorldItems.DevClear();
            hero = HeroController.instance;
            Vector2 feet = hero.GetComponent<BoxCollider2D>().bounds.center;
            IEnumerator DropAndWait(ItemStack s)
            {
                WorldItems.Drop(s, feet + Vector2.right * 0.5f);
                float until = Time.time + 4f;
                while (Time.time < until && WorldItems.DevCount(s.Type) > 0) yield return null;
            }
            inv.Slots[CaseSlot] = default;
            int bagBefore = CountInBag(inv, banner), caseBefore = Containers.CountIn(inv, Containers.CaseKind, banner);
            yield return DropAndWait(ItemStack.Of(banner, 3));
            BannerCheck("没带旗帜盒：旗帜进背包", CountInBag(inv, banner) == bagBefore + 3 && Containers.CountIn(inv, Containers.CaseKind, banner) == caseBefore,
                $"背包 {bagBefore} → {CountInBag(inv, banner)}，盒里 {caseBefore} → {Containers.CountIn(inv, Containers.CaseKind, banner)}");
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == banner) inv.Slots[i] = default;
            inv.Slots[CaseSlot] = ItemStack.Of(ItemType.BannerCase);
            inv.Slots[BoxSlot] = ItemStack.Of(ItemType.PotionBox);
            caseBefore = Containers.CountIn(inv, Containers.CaseKind, banner);
            yield return DropAndWait(ItemStack.Of(banner, 3));
            int potion0 = Containers.CountIn(inv, Containers.BoxKind, ItemType.IronskinPotion), woodBag0 = CountInBag(inv, ItemType.Wood);
            yield return DropAndWait(ItemStack.Of(ItemType.IronskinPotion, 5));
            yield return DropAndWait(ItemStack.Of(ItemType.Wood, 3));
            BannerCheck("带着盒子：旗帜进旗帜盒，药水进药水箱，别的进背包",
                Containers.CountIn(inv, Containers.CaseKind, banner) == caseBefore + 3 && CountInBag(inv, banner) == 0
                && Containers.CountIn(inv, Containers.BoxKind, ItemType.IronskinPotion) == potion0 + 5 && CountInBag(inv, ItemType.IronskinPotion) == 0
                && CountInBag(inv, ItemType.Wood) == woodBag0 + 3,
                $"盒里旗帜 {caseBefore} → {Containers.CountIn(inv, Containers.CaseKind, banner)}，箱里铁皮 {potion0} → {Containers.CountIn(inv, Containers.BoxKind, ItemType.IronskinPotion)}，背包木材 {woodBag0} → {CountInBag(inv, ItemType.Wood)}");

            // --- The panel.
            Containers.Toggle(ItemType.BannerCase);
            yield return null;
            bool opened = Chests.ContainerOpen && Chests.OpenBankKind == Containers.CaseKind && Chests.BankTitle == caseDef.Name && Chests.ContainerPages == Inventory.CasePages;
            var wood = ItemStack.Of(ItemType.Wood, 5);
            bool woodIn = Chests.Put(ref wood, false);
            var two = ItemStack.Of(ItemType.BannerCrawler, 2);
            bool bannerIn = Chests.Put(ref two, false);
            inv.Mouse = ItemStack.Of(ItemType.Wood, 1);
            int refused0 = InventoryGui.DevRefused;
            InventoryGui.DevChestClick(39);
            bool refused = InventoryGui.DevRefused == refused0 + 1 && inv.Mouse.Type == ItemType.Wood && Chests.OpenItems[39].IsEmpty;
            inv.Mouse = default;
            yield return new WaitForSeconds(0.4f);
            Shot(scene, "banners_case_panel");
            yield return null;
            InventoryGui.DevChestButton(8);
            int page1 = Chests.ContainerPage;
            bool pageItems = Chests.OpenItems == inv.BannerCase[1];
            yield return new WaitForSeconds(0.4f);
            Shot(scene, "banners_case_page2");
            yield return null;
            InventoryGui.DevChestButton(7);
            int page0 = Chests.ContainerPage;
            BannerCheck("旗帜盒面板：打开、翻页、只收旗帜", opened && !woodIn && wood.Count == 5 && bannerIn && two.IsEmpty && refused && page1 == 1 && pageItems && page0 == 0,
                $"打开 {opened}「{Chests.BankTitle}」{Chests.ContainerPages} 页，木材放进 {woodIn}，旗帜放进 {bannerIn}，光标木材被拒 {refused}，翻页 {page1}/{page0}");
            Containers.Toggle(ItemType.BannerCase);
            yield return null;
            BannerCheck("再用一次关上", !Chests.IsOpen, $"开着 {Chests.IsOpen}");

            // --- The Potion Box.
            bool kinds = Containers.IsPotion(ItemDb.Get(ItemType.IronskinPotion)) && Containers.IsPotion(ItemDb.Get(ItemType.GreaterHealingPotion))
                && Containers.IsPotion(ItemDb.Get(ItemType.FlaskofFire)) && Containers.IsPotion(ItemDb.Get(ItemType.RecallPotion))
                && !Containers.IsPotion(ItemDb.Get(ItemType.Mushroom)) && !Containers.IsPotion(ItemDb.Get(ItemType.Ale)) && !Containers.IsPotion(ItemDb.Get(ItemType.Sake))
                && !Containers.IsPotion(ItemDb.Get(ItemType.CookedFish)) && !Containers.IsPotion(ItemDb.Get(ItemType.Wood))
                && Containers.IsBuffPotion(ItemDb.Get(ItemType.IronskinPotion)) && !Containers.IsBuffPotion(ItemDb.Get(ItemType.GreaterHealingPotion))
                && !Containers.IsBuffPotion(ItemDb.Get(ItemType.BottledHoney));
            int potions = 0, buffPotions = 0;
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                var d = ItemDb.Get(t);
                if (Containers.IsPotion(d)) potions++;
                if (Containers.IsBuffPotion(d)) buffPotions++;
            }
            BannerCheck("药水箱只认药水（不收食物、蘑菇、麦芽酒），增益药水不含治疗类", kinds && potions <= Inventory.BoxPages * Inventory.ContainerPageSlots,
                $"药水 {potions} 种（箱子 {Inventory.BoxPages * Inventory.ContainerPageSlots} 格），其中增益药水 {buffPotions} 种");
            const int Ironskin = 5, Swiftness = 3, Lucky = 257, Fire = 74, Ichor = 76;
            foreach (int bf in new[] { Ironskin, Swiftness, Lucky, Fire, Ichor }) Player.PlayerBuffs.Remove(bf);
            ClearPages(inv.PotionBox);
            inv.PotionBox[0][0] = ItemStack.Of(ItemType.IronskinPotion, 19);
            Containers.Toggle(ItemType.PotionBox);
            yield return null;
            InventoryGui.DevAlt = true;
            InventoryGui.DevChestClick(0);
            bool fav19 = inv.PotionBox[0][0].Favorite;
            inv.PotionBox[0][0].Count = 20;
            InventoryGui.DevChestClick(0);
            InventoryGui.DevAlt = false;
            bool fav20 = inv.PotionBox[0][0].Favorite;
            yield return new WaitForSeconds(0.4f);
            Shot(scene, "banners_potion_box");
            bool held = Player.PlayerBuffs.Has(Ironskin) && Player.PlayerBuffs.NoTime(Ironskin) && Containers.Holds(Ironskin);
            yield return new WaitForSeconds(2f);
            bool kept = Player.PlayerBuffs.Has(Ironskin) && inv.PotionBox[0][0].Count == 20;
            BannerCheck("19 瓶不能收藏，20 瓶收藏后增益一直生效、不显示时间、不消耗", !fav19 && fav20 && held && kept,
                $"19 瓶收藏 {fav19}，20 瓶收藏 {fav20}，铁皮 {held}，2 秒后仍在且 {inv.PotionBox[0][0].Count} 瓶 {kept}");
            inv.PotionBox[0][0].Count = 19;
            yield return new WaitForSeconds(0.5f);
            bool under = !Player.PlayerBuffs.Has(Ironskin);
            inv.PotionBox[0][0].Count = 20;
            yield return new WaitForSeconds(0.3f);
            bool back = Player.PlayerBuffs.Has(Ironskin);
            inv.Slots[BoxSlot] = default;
            yield return new WaitForSeconds(0.5f);
            bool notCarried = !Player.PlayerBuffs.Has(Ironskin);
            inv.Slots[BoxSlot] = ItemStack.Of(ItemType.PotionBox);
            yield return new WaitForSeconds(0.3f);
            InventoryGui.DevAlt = true;
            InventoryGui.DevChestClick(0);
            InventoryGui.DevAlt = false;
            yield return new WaitForSeconds(0.5f);
            bool unfav = !inv.PotionBox[0][0].Favorite && !Player.PlayerBuffs.Has(Ironskin);
            BannerCheck("少于 20 瓶、药水箱不在身上、取消收藏都会失效", under && back && notCarried && unfav, $"19 瓶失效 {under}，回到 20 恢复 {back}，不带箱子失效 {notCarried}，取消收藏失效 {unfav}");

            // The Potion Box in the Piggy Bank: its favourite's buff on; a potion picked up then goes into the bag.
            Containers.Toggle(ItemType.PotionBox);
            yield return null;
            inv.Slots[BoxSlot] = default;
            inv.Bank[BankB] = ItemStack.Of(ItemType.PotionBox);
            inv.PotionBox[0][0].Favorite = true;
            yield return new WaitForSeconds(0.4f);
            bool bankBuff = Player.PlayerBuffs.Has(Ironskin);
            WorldItems.DevClear();
            int boxIron = Containers.CountIn(inv, Containers.BoxKind, ItemType.IronskinPotion), bagIron = CountInBag(inv, ItemType.IronskinPotion);
            Vector2 here = HeroController.instance.GetComponent<BoxCollider2D>().bounds.center;
            WorldItems.Drop(ItemStack.Of(ItemType.IronskinPotion, 2), here + Vector2.right * 0.5f);
            float bankUntil = Time.time + 4f;
            while (Time.time < bankUntil && WorldItems.DevCount(ItemType.IronskinPotion) > 0) yield return null;
            bool toBag = CountInBag(inv, ItemType.IronskinPotion) == bagIron + 2 && Containers.CountIn(inv, Containers.BoxKind, ItemType.IronskinPotion) == boxIron;
            inv.Bank[BankB] = default;
            yield return new WaitForSeconds(0.5f);
            bool bankGone = !Player.PlayerBuffs.Has(Ironskin);
            BannerCheck("药水箱放在猪猪存钱罐里：收藏的增益生效，捡药水不进箱子", bankBuff && toBag && bankGone,
                $"存钱罐里增益 {bankBuff}，捡的药水进背包 {toBag}（背包 {CountInBag(inv, ItemType.IronskinPotion)}），拿出存钱罐后失效 {bankGone}");
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == ItemType.IronskinPotion) inv.Slots[i] = default;
            inv.PotionBox[0][0].Favorite = false;
            inv.Slots[BoxSlot] = ItemStack.Of(ItemType.PotionBox);
            Containers.Toggle(ItemType.PotionBox);
            yield return null;

            inv.PotionBox[0][1] = new ItemStack { Type = ItemType.LuckPotionGreater, Count = 20, Favorite = true };
            inv.PotionBox[0][2] = new ItemStack { Type = ItemType.FlaskofFire, Count = 20, Favorite = true };
            inv.PotionBox[0][3] = new ItemStack { Type = ItemType.FlaskofIchor, Count = 20, Favorite = true };
            yield return new WaitForSeconds(0.4f);
            int luck = Player.PlayerBuffs.TimeLeft(Lucky);
            bool oneFlask = Player.PlayerBuffs.Has(Fire) && !Player.PlayerBuffs.Has(Ichor);
            inv.PotionBox[0][1].Favorite = false;
            yield return new WaitForSeconds(0.4f);
            bool luckGone = !Player.PlayerBuffs.Has(Lucky);
            BannerCheck("强效幸运药水给满幸运（按药水时长），只生效一种药剂，取消后幸运消失", luck > 36000 && oneFlask && luckGone,
                $"幸运剩 {luck} 帧，烈火 {Player.PlayerBuffs.Has(Fire)} 灵液 {Player.PlayerBuffs.Has(Ichor)}，取消后幸运没了 {luckGone}");
            inv.PotionBox[0][2] = default;
            inv.PotionBox[0][3] = default;

            // Quick Heal and Quick Buff from the box.
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && (inv.Slots[i].Def.HealLife > 0 || inv.Slots[i].Def.BuffType > 0)) inv.Slots[i] = default;
            inv.PotionBox[1][0] = ItemStack.Of(ItemType.GreaterHealingPotion, 3);
            inv.PotionBox[1][1] = ItemStack.Of(ItemType.SwiftnessPotion, 3);
            mod.Stats.PotionSickness = 0f;
            mod.Stats.Life = Mathf.Max(1, mod.Stats.LifeMax - 120);
            int lifeBefore = mod.Stats.Life;
            var quick = typeof(TerraMod).GetMethod("QuickUse", BindingFlags.NonPublic | BindingFlags.Instance);
            quick?.Invoke(mod, new object[] { (System.Func<ItemDef, bool>)(d => d.HealLife > 0) });
            bool healed = mod.Stats.Life > lifeBefore && inv.PotionBox[1][0].Count == 2;
            BannerCheck("快速治疗会用药水箱里的药水", quick != null && healed, $"生命 {lifeBefore} → {mod.Stats.Life}，箱里剩 {inv.PotionBox[1][0].Count}；{TerraMod.DevLastQuickUse}");
            mod.Stats.PotionSickness = 0f;
            mod.Stats.Life = mod.Stats.LifeMax;
            int drunk = mod.QuickBuff();
            bool swift = Player.PlayerBuffs.Has(Swiftness) && inv.PotionBox[1][1].Count == 2;
            BannerCheck("快速增益会用药水箱里的药水", drunk >= 1 && swift, $"喝了 {drunk} 瓶，敏捷 {Player.PlayerBuffs.Has(Swiftness)}，箱里剩 {inv.PotionBox[1][1].Count}");
            Containers.Toggle(ItemType.PotionBox);
            yield return null;

            // --- The Auto-Sell Bag.
            yield return AutoSellChecks(scene, BoxSlot + 1);

            // --- Through the save and back.
            ClearPages(inv.BannerCase);
            ClearPages(inv.PotionBox);
            ClearPages(inv.SellList);
            inv.SellList[1][7] = ItemStack.Of(ItemType.CopperBar);
            inv.Slots[BoxSlot + 1] = ItemStack.Of(ItemType.AutoSellBag);
            inv.BannerCase[2][5] = ItemStack.Of(banner, 7);
            inv.PotionBox[0][0] = new ItemStack { Type = ItemType.IronskinPotion, Count = 20, Favorite = true };
            inv.PotionBox[2][39] = ItemStack.Of(ItemType.SwiftnessPotion, 4);
            int slot = GameManager.instance.profileID;
            Save.ModSave.Write(slot, mod);
            Save.ModSave.Read(slot, mod);
            yield return new WaitForSeconds(0.6f);
            inv = mod.Inventory;
            var c = inv.BannerCase[2][5];
            var p0 = inv.PotionBox[0][0];
            var p1 = inv.PotionBox[2][39];
            bool carried = Containers.Carried(inv, ItemType.BannerCase) && Containers.Carried(inv, ItemType.PotionBox) && Containers.Carried(inv, ItemType.AutoSellBag);
            var entry = inv.SellList[1][7];
            BannerCheck("存档读回：旗帜盒、药水箱的内容和收藏、自动出售名单都在，收藏的增益接着生效",
                c.Type == banner && c.Count == 7 && p0.Type == ItemType.IronskinPotion && p0.Count == 20 && p0.Favorite && p1.Type == ItemType.SwiftnessPotion && p1.Count == 4
                && carried && Player.PlayerBuffs.Has(Ironskin) && entry.Type == ItemType.CopperBar && Containers.Sells(inv, ItemType.CopperBar),
                $"盒 {c.Type}×{c.Count}，箱 {p0.Type}×{p0.Count} 收藏 {p0.Favorite}，{p1.Type}×{p1.Count}，三样都在身上 {carried}，铁皮 {Player.PlayerBuffs.Has(Ironskin)}，名单第 2 页 {entry.Type}");

            // --- English.
            Lang.DevForce("en-US");
            yield return null;
            var en = ItemDb.Get(ItemType.BannerCrawler);
            string enName = en.Name, enTip = en.Tooltip, enCase = ItemDb.Get(ItemType.BannerCase).Name, enBox = ItemDb.Get(ItemType.PotionBox).Name;
            Lang.DevForce(null);
            yield return new WaitForSeconds(0.3f);
            BannerCheck("英文：旗帜名、原版旗帜说明、盒子名字", enName.EndsWith(" Banner") && enTip.StartsWith("Nearby players get a bonus against: ") && enCase == "Banner Case" && enBox == "Potion Box",
                $"{enName} / {enTip.Replace("\n", " / ")} / {enCase} / {enBox}");

            ClearPages(inv.BannerCase);
            ClearPages(inv.PotionBox);
            ClearPages(inv.SellList);
            for (int i = Slot; i <= BoxSlot + 2; i++) inv.Slots[i] = default;
            GameDifficulty.Current = diff;
            inv.Infinite = infinite;
            Plugin.Log.LogInfo($"[AutoTest] 旗帜 合计 通过 {bannerPass}，不通过 {bannerFail}");
        }

        /// <summary>
        /// Dev (suite "bannertip"): the Monster Banner buff's tooltip with many banners in effect (Banner Case pages filled,
        /// the buff's icon hovered by the dev pointer), against Main.MouseText_DrawBuffTooltip: every monster listed, a
        /// column under the buff's text and on into columns 220 px apart, none dropped and the whole box on screen, for 5,
        /// 70 and all 127 banners; at the largest interface scale the columns the screen holds are full and end in "…".
        /// Pictures of each.
        /// </summary>
        IEnumerator BannerTipTest()
        {
            // Any scene of play will do: wait out the opening (a death and its respawn) until the hero is back in control.
            var gm = GameManager.instance;
            float settle = Time.realtimeSinceStartup + 30f;
            yield return new WaitForSeconds(2f);
            while ((HeroController.instance == null || HeroController.instance.cState.dead || gm.IsInSceneTransition || !GameHooks.PlayerHasControl())
                   && Time.realtimeSinceStartup < settle) yield return null;
            yield return new WaitForSeconds(1f);
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            string scene = GameManager.instance.sceneName;
            InventoryGui.Open = false;
            const int CaseSlot = 26;
            var keptSlot = inv.Slots[CaseSlot];
            float keptScale = UI.GameView.UiScaleSetting != null ? UI.GameView.UiScaleSetting.Value : 1f;
            var types = new List<ItemType>(Banners.Types());
            inv.Slots[CaseSlot] = ItemStack.Of(ItemType.BannerCase);

            // The banner buff's icon under the dev pointer until the tooltip has been drawn again.
            IEnumerator Hover()
            {
                UI.TerraCursor.DevDrives = true;
                for (int tries = 0; tries < 60; tries++)
                {
                    int at = -1;
                    for (int i = 0; i < Player.PlayerBuffs.MaxBuffs; i++) if (Player.PlayerBuffs.Type[i] == Banners.MonsterBannerBuff) at = i;
                    var rect = at >= 0 ? InventoryGui.DevRectOf("Buff", at) : null;
                    if (rect.HasValue) UI.TerraCursor.DevAt = rect.Value.center;
                    int frame = Time.frameCount;
                    yield return null;
                    if (rect.HasValue && InventoryGui.DevBannerFrame >= frame && tries >= 5) yield break;
                }
            }

            bool OnScreen(Rect r) => r.xMin >= 0f && r.yMin >= 0f && r.xMax <= Screen.width && r.yMax <= Screen.height;

            foreach (int count in new[] { 5, 70, types.Count })
            {
                ClearPages(inv.BannerCase);
                for (int i = 0; i < count; i++) inv.BannerCase[i / Inventory.ContainerPageSlots][i % Inventory.ContainerPageSlots] = ItemStack.Of(types[i]);
                Banners.Refresh(true);
                yield return null;
                yield return Hover();
                int active = Banners.ActiveCount;
                var names = new List<string>();
                Banners.BuffLines(Banners.MonsterBannerBuff, names);
                bool everyName = names.Count == active;
                for (int i = 0; i < count; i++) everyName &= names.Contains(Banners.MonsterName(types[i]));
                int rows = InventoryGui.DevBannerRows, columns = InventoryGui.DevBannerColumns;
                var box = InventoryGui.DevBannerBox;
                bool flows = rows > 0 && columns == (active - 1) / rows + 1 && (active <= rows || columns > 1);
                BannerCheck($"{count} 面旗帜：增益提示列出全部怪名，一列满了换到右边新的一列，整块在屏幕内",
                    Player.PlayerBuffs.Has(Banners.MonsterBannerBuff) && everyName && InventoryGui.DevBannerNames == active && !InventoryGui.DevBannerCut && flows && OnScreen(box),
                    $"生效 {active} 面，名单 {names.Count} 个（全在 {everyName}），画出 {InventoryGui.DevBannerNames} 个，每列 {rows} 行，{columns} 列，省略 {InventoryGui.DevBannerCut}，" +
                    $"范围 {box}，屏幕 {Screen.width}x{Screen.height}，界面缩放 {UI.GameView.Scale:F2}");
                Shot(scene, $"bannertip_{count}");
                // The picture is taken at the end of this frame: nothing changes before it.
                yield return null;
                yield return new WaitForSeconds(0.3f);
            }

            // The largest interface scale: more banners than the columns on screen hold.
            UI.GameView.SetUiScale(UI.GameView.MaxUiScale);
            yield return null;
            yield return null;
            yield return Hover();
            {
                int active = Banners.ActiveCount, rows = InventoryGui.DevBannerRows, columns = InventoryGui.DevBannerColumns, shown = InventoryGui.DevBannerNames;
                var box = InventoryGui.DevBannerBox;
                bool full = InventoryGui.DevBannerCut && shown < active && shown == rows * columns;
                BannerCheck("界面放到最大：屏幕放不下的列不画，最后一列末尾是“…”，整块在屏幕内", full && columns > 1 && OnScreen(box),
                    $"生效 {active} 面，画出 {shown} 个（含省略号），每列 {rows} 行，{columns} 列，省略 {InventoryGui.DevBannerCut}，范围 {box}，" +
                    $"屏幕 {Screen.width}x{Screen.height}，界面缩放 {UI.GameView.Scale:F2}");
                Shot(scene, "bannertip_maxscale");
                yield return null;
                yield return new WaitForSeconds(0.3f);
            }

            UI.GameView.SetUiScale(keptScale);
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevAt = null;
            ClearPages(inv.BannerCase);
            inv.Slots[CaseSlot] = keptSlot;
            Banners.Refresh(true);
            Plugin.Log.LogInfo($"[AutoTest] 旗帜 合计 通过 {bannerPass}，不通过 {bannerFail}");
        }

        /// <summary>
        /// The Auto-Sell Bag: what may be listed; a stack put in listed and sold for its price there and then (the nail
        /// refused, Shift+click alike, the item buttons gone); a pickup on the list sold with the bag carried and kept
        /// without it; an entry clicked off the list, nothing back, and kept after.
        /// </summary>
        IEnumerator AutoSellChecks(string scene, int bagSlot)
        {
            var inv = mod.Inventory;
            ClearPages(inv.SellList);
            ItemType quest = ItemType.None, worthless = ItemType.None;
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                var d = ItemDb.Get(t);
                if (quest == ItemType.None && d.QuestFish) quest = t;
                if (worthless == ItemType.None && t != ItemType.None && d.Value <= 0 && !d.Permanent && !d.QuestFish && d.Name != t.ToString()) worthless = t;
            }
            // Two materials a shop pays a geo or more for.
            ItemType sellA = ItemType.None, sellB = ItemType.None;
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                var d = ItemDb.Get(t);
                if (t == ItemType.None || d.Name == t.ToString() || !d.Material || d.MaxStack <= 1 || Banners.Is(t) || Containers.IsPotion(d) || Inventory.FitsAmmoSlot(d)) continue;
                if (!Containers.Listable(ItemStack.Of(t), out _) || Shop.SellValue(ItemStack.Of(t)) < 100) continue;
                if (sellA == ItemType.None) sellA = t;
                else
                {
                    sellB = t;
                    break;
                }
            }
            string an = ItemDb.Get(sellA).Name, bn = ItemDb.Get(sellB).Name;
            var fav = ItemStack.Of(sellA, 3);
            fav.Favorite = true;
            bool rules = Containers.Listable(ItemStack.Of(sellA), out _) && !Containers.Listable(ItemStack.Of(ItemType.KnightNail), out _)
                && !Containers.Listable(ItemStack.Of(ItemType.Geo, 10), out _) && !Containers.Listable(fav, out _)
                && (quest == ItemType.None || !Containers.Listable(ItemStack.Of(quest), out _)) && (worthless == ItemType.None || !Containers.Listable(ItemStack.Of(worthless), out _))
                && !Containers.Listable(ItemStack.Of(ItemType.AutoSellBag), out _);
            BannerCheck("自动出售名单：能卖的才能加，骨钉、吉欧、收藏的、任务鱼、不值钱的、袋子本身不行", rules,
                $"{an}可加，任务鱼 {quest}，不值钱 {worthless}，{an}单价 {Shop.Money(Shop.SellValue(ItemStack.Of(sellA)))}");

            // Opened, a stack on the cursor put in: listed and sold there and then.
            inv.Slots[bagSlot] = ItemStack.Of(ItemType.AutoSellBag);
            Containers.Toggle(ItemType.AutoSellBag);
            yield return null;
            yield return null;
            bool open = Chests.SellListOpen && Chests.BankTitle == ItemDb.Get(ItemType.AutoSellBag).Name && Chests.ContainerPages == Inventory.SellPages;
            bool buttonsGone = InventoryGui.DevRectOf("ChestButton", 0) == null && InventoryGui.DevRectOf("ChestButton", 3) == null && InventoryGui.DevRectOf("ChestButton", 4) != null;
            long price5 = Shop.SellValue(ItemStack.Of(sellA, 5));
            long purse0 = Shop.Purse;
            inv.Mouse = ItemStack.Of(sellA, 5);
            InventoryGui.DevChestClick(3);
            long gained = Shop.Purse - purse0;
            bool listed = Chests.OpenItems[3].Type == sellA && inv.Mouse.IsEmpty && Containers.Listed(inv, sellA) && gained == price5 && price5 > 0;
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "banners_autosell_panel");
            // The picture is taken at the end of this frame: nothing changes before it.
            yield return null;
            inv.Mouse = ItemStack.Of(ItemType.KnightNail);
            int refused0 = InventoryGui.DevRefused;
            InventoryGui.DevChestClick(5);
            bool nailRefused = InventoryGui.DevRefused == refused0 + 1 && inv.Mouse.Type == ItemType.KnightNail && Chests.OpenItems[5].IsEmpty;
            inv.Mouse = default;
            var bars = ItemStack.Of(sellB, 4);
            long priceCopper = Shop.SellValue(bars);
            purse0 = Shop.Purse;
            bool shifted = Chests.Put(ref bars, false) && bars.IsEmpty && Containers.Listed(inv, sellB) && Shop.Purse - purse0 == priceCopper;
            BannerCheck("自动出售袋面板：放进去的那组当场卖掉并加入名单，骨钉被拒，Shift 送进去也一样，没有拿取/存放按钮", open && buttonsGone && listed && nailRefused && shifted,
                $"打开 {open}「{Chests.BankTitle}」{Chests.ContainerPages} 页，按钮隐藏 {buttonsGone}，5 个{an}卖 {Shop.Money(gained)}（应 {Shop.Money(price5)}）进名单 {listed}，骨钉被拒 {nailRefused}，Shift {bn} {shifted}");
            Containers.Toggle(ItemType.AutoSellBag);
            yield return null;

            // Picked up with the bag carried: sold; without it: kept.
            WorldItems.DevClear();
            Vector2 feet = HeroController.instance.GetComponent<BoxCollider2D>().bounds.center;
            IEnumerator Pick(ItemStack s)
            {
                WorldItems.Drop(s, feet + Vector2.right * 0.5f);
                float until = Time.time + 4f;
                while (Time.time < until && WorldItems.DevCount(s.Type) > 0) yield return null;
            }
            int bag0 = CountInBag(inv, sellA), sold0 = Containers.DevAutoSold;
            long price3 = Shop.SellValue(ItemStack.Of(sellA, 3));
            purse0 = Shop.Purse;
            yield return Pick(ItemStack.Of(sellA, 3));
            long gained3 = Shop.Purse - purse0;
            BannerCheck("带着自动出售袋：捡到名单上的物品直接卖掉，吉欧进钱包",
                CountInBag(inv, sellA) == bag0 && Containers.DevAutoSold == sold0 + 3 && gained3 == price3 && WorldItems.DevCount(sellA) == 0,
                $"背包{an} {bag0} → {CountInBag(inv, sellA)}，卖出 {Containers.DevAutoSold - sold0}，钱 +{Shop.Money(gained3)}（应 {Shop.Money(price3)}）");
            Shot(scene, "banners_autosell_pickup");
            inv.Slots[bagSlot] = default;
            sold0 = Containers.DevAutoSold;
            yield return Pick(ItemStack.Of(sellA, 2));
            BannerCheck("没带自动出售袋：不卖，进背包", CountInBag(inv, sellA) == bag0 + 2 && Containers.DevAutoSold == sold0,
                $"背包{an} {CountInBag(inv, sellA)}，卖出 {Containers.DevAutoSold - sold0}");

            // Taken off the list (nothing back): kept after.
            inv.Slots[bagSlot] = ItemStack.Of(ItemType.AutoSellBag);
            Containers.Toggle(ItemType.AutoSellBag);
            yield return null;
            int unlisted0 = InventoryGui.DevUnlisted;
            InventoryGui.DevChestClick(3);
            bool off = InventoryGui.DevUnlisted == unlisted0 + 1 && !Containers.Listed(inv, sellA) && Chests.OpenItems[3].IsEmpty && inv.Mouse.IsEmpty;
            Containers.Toggle(ItemType.AutoSellBag);
            yield return null;
            sold0 = Containers.DevAutoSold;
            int bag1 = CountInBag(inv, sellA);
            yield return Pick(ItemStack.Of(sellA, 1));
            BannerCheck("点名单里的物品移出名单（什么也不还），之后捡到不卖", off && CountInBag(inv, sellA) == bag1 + 1 && Containers.DevAutoSold == sold0,
                $"移出 {off}，背包{an} {bag1} → {CountInBag(inv, sellA)}，卖出 {Containers.DevAutoSold - sold0}");
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == sellA) inv.Slots[i] = default;
            inv.Slots[bagSlot] = default;
            ClearPages(inv.SellList);
        }
    }
}
