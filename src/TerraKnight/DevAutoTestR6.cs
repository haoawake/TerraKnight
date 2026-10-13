using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "r6", sweep R6): the Paintball Gun, the Barrel Launcher (at an enemy, then into
    /// the floor to bounce), the Killing Deck's four throws in turn, the Rainbow and Shimmer
    /// Flares, the four liquid rockets (a lava pool burning an enemy set in it, the hero standing
    /// in honey, water putting an enemy's fire out, a dry rocket soaking a pool up), Flairon's two
    /// forms swapped in the inventory and its thrown form, and the Gravity Globe turning the hero
    /// onto the ceiling, walking and jumping there, and back.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator R6Test(string scene, HeroController hero)
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
            var savedArmor = (ItemStack[])inv.Armor.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            const int slot = Inventory.HotbarSize - 1;
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] R6：场景里没有敌人");
                yield break;
            }
            var trb = target.GetComponent<Rigidbody2D>();
            var tp = TerraProjectiles.Instance;
            var far = C() + new Vector2(side * 4f, 0.5f);
            Vector2 pinAt = far;
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
            void Take(ItemType type)
            {
                inv.Slots[slot] = ItemStack.Of(type);
                inv.Selected = slot;
            }
            IEnumerator Use(Vector2 aim)
            {
                while (!mod.DevReady)
                {
                    Hold();
                    yield return null;
                }
                mod.DevAim = aim;
                mod.DevUseSelected(aim);
                yield return null;
            }

            // --- The specialist bonus: the Shroomite helmet's 12% on the Paintball Gun and the Barrel Launcher (Pl:54045).
            var paintDef = ItemDb.Get(ItemType.PaintballGun);
            var barrelDef = ItemDb.Get(ItemType.BarrelLauncher);
            int p0 = Equipment.Damage(paintDef), b0 = Equipment.Damage(barrelDef);
            inv.Armor[0] = ItemStack.Of(ItemType.ShroomiteHelmet);
            for (int f = 0; f < 3; f++) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] R6 专家伤害：彩弹枪 {p0} -> {Equipment.Damage(paintDef)}，炸药桶发射器 {b0} -> {Equipment.Damage(barrelDef)}（蘑菇矿头盔 ×1.12）");
            for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = savedArmor[i];

            // --- The Paintball Gun: two uses, three shots each.
            tp.DevClear();
            Take(ItemType.PaintballGun);
            int paint0 = TerraProjectiles.DevPaintballs, paintHit0 = TerraProjectiles.DevHit(ProjKind.Paintball);
            yield return Use(far);
            yield return Wait(0.5f);
            yield return Use(far);
            yield return Wait(0.15f);
            yield return CloseUpAt(scene, "r6_paintball", (C() + far) / 2f, 200, 2);
            yield return Wait(1f);
            Plugin.Log.LogInfo($"[AutoTest] R6 彩弹枪：2 次使用射出 {TerraProjectiles.DevPaintballs - paint0} 发（原版 每次 3 发），命中 {TerraProjectiles.DevHit(ProjKind.Paintball) - paintHit0}");

            // --- The Barrel Launcher: at the enemy, then down into the floor to bounce.
            tp.DevClear();
            Take(ItemType.BarrelLauncher);
            int bl0 = TerraProjectiles.DevBarrelBlasts, bh0 = TerraProjectiles.DevBarrelBlastHits, bd0 = TerraProjectiles.DevHit(ProjKind.DeadCellsBarrel), bb0 = TerraProjectiles.DevBarrelBounces;
            yield return Use(far);
            bool shot = false;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                Hold();
                if (!shot && TerraProjectiles.DevBarrelBlasts > bl0)
                {
                    shot = true;
                    for (int f = 0; f < 4; f++) yield return null;
                    yield return CloseUpAt(scene, "r6_barrel_blast", pinAt, 260, 1);
                }
                yield return null;
            }
            pinAt = C() + new Vector2(-side * 6f, 3f);
            yield return Use(C() + new Vector2(side * 1.5f, -2f));
            yield return Wait(3.2f);
            pinAt = far;
            Plugin.Log.LogInfo($"[AutoTest] R6 炸药桶发射器：爆炸 {TerraProjectiles.DevBarrelBlasts - bl0} 次，直击 {TerraProjectiles.DevHit(ProjKind.DeadCellsBarrel) - bd0}，爆炸波及 {TerraProjectiles.DevBarrelBlastHits - bh0}，弹跳 {TerraProjectiles.DevBarrelBounces - bb0} 次");

            // --- The Killing Deck: four uses, the throws in turn (one, two, six, all back).
            tp.DevClear();
            Take(ItemType.KillingDeck);
            int c0 = TerraProjectiles.DevCardsThrown, s0 = TerraProjectiles.DevCardsStuck, r0 = TerraProjectiles.DevCardsRecalled, ch0 = TerraProjectiles.DevHit(ProjKind.KillingDeckCard);
            var thrown = new System.Text.StringBuilder();
            for (int k = 0; k < 4; k++)
            {
                int before = TerraProjectiles.DevCardsThrown, rb0 = TerraProjectiles.DevCardsRecalled, type = TerraProjectiles.KillingCardFireType;
                mod.Stats.Mana = mod.Stats.ManaMax;
                yield return Use(far);
                yield return Wait(k == 2 ? 0.3f : 0.6f);
                if (k == 2) yield return CloseUpAt(scene, "r6_cards", (C() + far) / 2f, 220, 2);
                thrown.Append($"[{type}:出{TerraProjectiles.DevCardsThrown - before}召{TerraProjectiles.DevCardsRecalled - rb0}] ");
                yield return Wait(0.4f);
            }
            yield return Wait(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] R6 击杀牌叠：{thrown}共出牌 {TerraProjectiles.DevCardsThrown - c0}（原版 1+2+6），粘住 {TerraProjectiles.DevCardsStuck - s0}，召回 {TerraProjectiles.DevCardsRecalled - r0}，命中 {TerraProjectiles.DevHit(ProjKind.KillingDeckCard) - ch0}，" +
                               $"之后招式 {TerraProjectiles.KillingCardFireType}（应回到 0），还在场 {tp.DevCount(ProjKind.KillingDeckCard)}");

            // --- The flares.
            foreach (var (ammo, kind) in new[] { (ItemType.RainbowFlare, ProjKind.RainbowFlareProj), (ItemType.ShimmerFlare, ProjKind.ShimmerFlareProj) })
            {
                tp.DevClear();
                Take(ItemType.FlareGun);
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ammo, 20);
                int m0 = TerraProjectiles.DevMade(kind), h0 = TerraProjectiles.DevHit(kind);
                yield return Use(far);
                yield return Wait(0.9f);
                // One more into the floor close by, to look at as it burns there.
                yield return Use(C() + new Vector2(side * 2f, -2f));
                yield return Wait(0.5f);
                yield return CloseUpAt(scene, "r6_" + ammo, tp.DevNewest(kind) ?? C(), 120, 3);
                Plugin.Log.LogInfo($"[AutoTest] R6 {ItemDb.Get(ammo).Name}：射出 {TerraProjectiles.DevMade(kind) - m0}，命中 {TerraProjectiles.DevHit(kind) - h0}，着火 {EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)}");
            }

            // --- The liquid rockets from the Rocket Launcher, into the floor a little way off.
            Take(ItemType.RocketLauncher);
            var floorAim = C() + new Vector2(side * 2.5f, -3f);
            // Lava: the enemy set in the pool.
            tp.DevClear();
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.LavaRocket, 20);
            pinAt = C() + new Vector2(-side * 6f, 3f);
            int pools0 = TerraProjectiles.DevLiquidPools, lava0 = TerraProjectiles.DevLavaBurns;
            int lavaMade0 = TerraProjectiles.DevMade(ProjKind.RocketLava), blast0 = TerraProjectiles.DevRocketBlasts;
            yield return Use(floorAim);
            for (float t = 0f; t < 2f && TerraProjectiles.DevLiquidPools == pools0; t += Time.deltaTime)
            {
                Hold();
                yield return null;
            }
            bool lavaPool = TerraProjectiles.DevLiquidPools > pools0;
            if (lavaPool) pinAt = TerraProjectiles.DevLastPool + new Vector2(0f, 0.3f);
            yield return Wait(0.5f);
            yield return CloseUpAt(scene, "r6_lava_pool", TerraProjectiles.DevLastPool, 220, 2);
            yield return Wait(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] R6 熔岩火箭：射出 {TerraProjectiles.DevMade(ProjKind.RocketLava) - lavaMade0}，爆炸 {TerraProjectiles.DevRocketBlasts - blast0}（{TerraProjectiles.DevSpillNote}），熔岩池 {(lavaPool ? "有" : "无")}，敌人被烫 {TerraProjectiles.DevLavaBurns - lava0} 次（原版 每 30 tick 一次 50 伤害），着火 {EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)}");
            // Water: the enemy on fire, in the next pool.
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WetRocket, 20);
            pinAt = C() + new Vector2(-side * 6f, 3f);
            tp.DevClear();
            pools0 = TerraProjectiles.DevLiquidPools;
            int douse0 = TerraProjectiles.DevWaterDousings;
            yield return Use(floorAim);
            for (float t = 0f; t < 2f && TerraProjectiles.DevLiquidPools == pools0; t += Time.deltaTime)
            {
                Hold();
                yield return null;
            }
            bool waterPool = TerraProjectiles.DevLiquidPools > pools0;
            if (waterPool) pinAt = TerraProjectiles.DevLastPool + new Vector2(0f, 0.3f);
            EnemyDebuffs.Add(target, EnemyDebuffs.OnFire, 600);
            yield return Wait(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] R6 湿火箭：水池 {(waterPool ? "有" : "无")}（{TerraProjectiles.DevSpillNote}），浇灭 {TerraProjectiles.DevWaterDousings - douse0} 次，敌人仍着火 {EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)}（应为 False）");
            yield return CloseUpAt(scene, "r6_water_pool", TerraProjectiles.DevLastPool, 220, 2);
            // Dry: soaks the water up.
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.DryRocket, 20);
            pinAt = C() + new Vector2(-side * 6f, 3f);
            int dried0 = TerraProjectiles.DevPoolsDried;
            yield return Use(floorAim);
            yield return Wait(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] R6 干火箭：吸干液池 {TerraProjectiles.DevPoolsDried - dried0} 个，余下液池 {tp.DevCount(ProjKind.SpilledLiquid)}");
            // Honey: the hero standing in it.
            tp.DevClear();
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.HoneyRocket, 20);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return Wait(0.4f);
            mod.Stats.HoneyTicks = 0f;
            pools0 = TerraProjectiles.DevLiquidPools;
            yield return Use(C() + new Vector2(side * 1.2f, -3f));
            yield return Wait(1.2f);
            Plugin.Log.LogInfo($"[AutoTest] R6 蜂蜜火箭：蜂蜜池 {TerraProjectiles.DevLiquidPools - pools0}，站在里面的蜂蜜增益 {mod.Stats.HoneyTicks:F0} tick（原版 1800）");
            yield return CloseUp(scene, "r6_honey", hero, 200, 2);
            tp.DevClear();
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            pinAt = far;
            yield return Wait(0.4f);

            // --- Flairon: the two forms swapped by a right click in the inventory, then the thrown one.
            inv.Slots[slot] = ItemStack.Of(ItemType.FlaironFlail);
            InventoryGui.DevRightClick(slot);
            var first = inv.Slots[slot].Type;
            InventoryGui.DevRightClick(slot);
            var second = inv.Slots[slot].Type;
            InventoryGui.DevRightClick(slot);
            inv.Selected = slot;
            int heads0 = TerraProjectiles.DevFlaironHeads, hb0 = TerraProjectiles.DevFlaironHeadBubbles, fh0 = TerraProjectiles.DevHit(ProjKind.FlaironHead), fb0 = TerraProjectiles.DevHit(ProjKind.FlaironBubble);
            yield return Use(far);
            yield return Wait(0.35f);
            yield return CloseUpAt(scene, "r6_flairon", (C() + far) / 2f, 220, 2);
            yield return Wait(2f);
            Plugin.Log.LogInfo($"[AutoTest] R6 猪龙鱼链球：右键切换 猪鲨链球 -> {first} -> {second} -> {inv.Slots[slot].Type}；投出 {TerraProjectiles.DevFlaironHeads - heads0}，泡泡 {TerraProjectiles.DevFlaironHeadBubbles - hb0}，" +
                               $"链球命中 {TerraProjectiles.DevHit(ProjKind.FlaironHead) - fh0}，泡泡命中 {TerraProjectiles.DevHit(ProjKind.FlaironBubble) - fb0}，还在场 {tp.DevCount(ProjKind.FlaironHead)}");

            // --- The Gravity Globe: up to the ceiling, walk, jump, and back down; off with the Globe taken off.
            tp.DevClear();
            inv.Slots[slot] = default;
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = i == 0 ? ItemStack.Of(ItemType.GravityGlobe) : default;
            // Somewhere with a ceiling 2-8 units overhead (the scene starts under the open sky).
            var under = home;
            float room = 99f;
            for (int k = 0; k <= 160 && room > 8f; k++)
            {
                float dx = (k % 2 == 0 ? 1f : -1f) * (k / 2) * 0.5f;
                var spot = Clear(hero, (Vector2)home + new Vector2(dx, 0f));
                var head = Physics2D.Raycast((Vector2)spot + Vector2.up * 0.7f, Vector2.up, 60f, terrain);
                var feetHit = Physics2D.Raycast((Vector2)spot, Vector2.down, 3f, terrain);
                float r = head.collider != null ? head.distance : 99f;
                if (feetHit.collider != null && r >= 2f && r <= 8f)
                {
                    under = spot;
                    room = r;
                }
            }
            hero.transform.position = under;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            Plugin.Log.LogInfo($"[AutoTest] R6 重力球 测试点 {under}（头顶 {(room > 60f ? "无" : (room / Pxu).ToString("F0") + "px")}）");
            float floorY = col.bounds.center.y;
            int toggles0 = GravityGlobe.DevToggles;
            GravityGlobe.DevUpPress = true;
            float topY = floorY, landT = -1f;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                topY = Mathf.Max(topY, col.bounds.center.y);
                if (GravityGlobe.Flipped && hero.cState.onGround && t > 0.2f && landT < 0f) landT = t;
                if (landT >= 0f && t > landT + 0.3f) break;
                yield return null;
            }
            bool flipped = GravityGlobe.Flipped, onCeiling = hero.cState.onGround;
            float gs = rb.gravityScale;
            Plugin.Log.LogInfo($"[AutoTest] R6 重力球：翻转 {flipped}（切换 {GravityGlobe.DevToggles - toggles0} 次），上升 {(topY - floorY) / Pxu:F0}px，贴住天花板站立 {onCeiling}（{landT:F2}s），重力 {gs:F2}");
            yield return CloseUp(scene, "r6_gravity", hero, 160, 2);
            // Walking along the ceiling, then a jump down from it and back up.
            float x0 = col.bounds.center.x;
            float way = Room(1f) >= Room(-1f) ? 1f : -1f;
            TerraMovement.DevMove = (int)way;
            yield return new WaitForSeconds(0.5f);
            TerraMovement.DevMove = 0;
            float walked = (col.bounds.center.x - x0) * way / Pxu;
            bool stillOn = hero.cState.onGround;
            float y1 = col.bounds.center.y, lowest = y1;
            TerraMovement.DevJump = true;
            TerraMovement.DevGroundJump(hero);
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                lowest = Mathf.Min(lowest, col.bounds.center.y);
                yield return null;
            }
            TerraMovement.DevJump = false;
            bool back = false;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                lowest = Mathf.Min(lowest, col.bounds.center.y);
                if (t > 0.3f && hero.cState.onGround)
                {
                    back = true;
                    break;
                }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] R6 重力球 倒立：沿天花板走 {walked:F0}px（仍站住 {stillOn}），往下跳 {(y1 - lowest) / Pxu:F0}px，落回天花板 {back}");
            // Back the right way up.
            GravityGlobe.DevUpPress = true;
            bool down = false;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                if (!GravityGlobe.Flipped && t > 0.3f && hero.cState.onGround)
                {
                    down = true;
                    break;
                }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] R6 重力球 复原：翻回 {!GravityGlobe.Flipped}，落地 {down}，离起点高度 {(col.bounds.center.y - floorY) / Pxu:F0}px，重力 {rb.gravityScale:F2}");
            // Taken off while turned: set right at once.
            GravityGlobe.DevUpPress = true;
            yield return new WaitForSeconds(0.4f);
            bool wasFlipped = GravityGlobe.Flipped;
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = default;
            for (int f = 0; f < 5; f++) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] R6 重力球 摘下：摘前翻转 {wasFlipped}，摘下后 {GravityGlobe.Flipped}（应为 False）");
            yield return new WaitForSeconds(1.5f);

            // --- Drops of a fight that leaves its scene by itself (a dream boss): straight into the inventory, the rest at the feet.
            var scenes = new[] { "Dream_04_White_Defender", "Dream_Final_Boss", "GG_Hollow_Knight", "Grimm_Nightmare", "Room_Final_Boss_Core", "GG_Lurker", "GG_Waterways", "Grimm_Main_Tent", "Tutorial_01" };
            var leaves = new System.Text.StringBuilder();
            foreach (var sc in scenes) leaves.Append(sc).Append(EnemyDrops.LeavesOnItsOwn(sc) ? "=直接 " : "=地上 ");
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = i < Inventory.Size - 2 ? ItemStack.Of(ItemType.DirtBlock, 1) : default;
            int given0 = EnemyDrops.DevGiven, ground0 = WorldItems.Count;
            for (int k = 0; k < 6; k++) EnemyDrops.Roll("WhiteDefender", C() + Vector2.right * 3f, true);
            int inBag = 0;
            for (int i = Inventory.Size - 2; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty) inBag++;
            int afterGive = WorldItems.Count;
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] R6 自动离场的 Boss 掉落：{leaves}；白色防御者掷 6 次，直接进背包 {EnemyDrops.DevGiven - given0} 件（空格填了 {inBag}/2），" +
                               $"掷完时地上物品 {ground0} -> {afterGive}，回到能操作的普通场景后放不下的掉在脚边 -> {WorldItems.Count}");

            mod.DevAim = null;
            tp.DevClear();
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = savedSlots[i];
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = savedArmor[i];
            inv.Selected = savedSelected;
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            mod.DevNoFallDamage = false;
        }
    }
}
