using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "features"): the inventory's Terraria clicks and the crafting column, the swords'
    /// slashes and projectiles on a real enemy with its health bar, the flying carpet, the hook
    /// leaving gravity alone, and a pylon unlocked at Dirtmouth's bench, shown on the map and
    /// teleported to.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// T throws the held stack (out of reach for 100 ticks, then pulled back in and picked up
        /// with a popup); a right click off the inventory throws the cursor's; closing a full
        /// inventory drops what doesn't fit; the item on the ground shows its name under the mouse.
        /// </summary>
        IEnumerator DropTest(string scene, HeroController hero)
        {
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            WorldItems.Clear();

            // T: the selected hotbar stack.
            inv.Slots[5] = ItemStack.Of(ItemType.Torch, 30);
            inv.Selected = 5;
            yield return null;
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            mod.ThrowHeld(false);
            Plugin.Log.LogInfo($"[AutoTest] 丢弃：按 T 后快捷栏第 6 格 {inv.Slots[5].Type}，地上 {WorldItems.Count} 个：{WorldItems.DevDescribe()}");
            // Walk away so it isn't pulled back while it lands.
            yield return new WaitForSeconds(1.2f);
            Plugin.Log.LogInfo($"[AutoTest] 丢弃 1.2s 后（还在 100 帧拾取延迟内）：地上 {WorldItems.DevDescribe()} 主角 {from:F2}，格子里火把 {inv.Slots[5].Type}");
            if (WorldItems.DevFirst is Vector2 lying)
            {
                // The mouse over it shows its name.
                UI.EnemyHover.DevAt = lying;
                yield return null;
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 鼠标指着地上的火把：显示「{UI.EnemyHover.DevShown}」");
            }
            Shot(scene, "d1_thrown_torch");
            yield return new WaitForSeconds(0.2f);
            UI.EnemyHover.DevAt = null;
            // Past the delay: walk up to it and it comes in.
            if (WorldItems.DevFirst is Vector2 at) hero.transform.position = Clear(hero, at + Vector2.up * 0.3f);
            yield return new WaitForSeconds(1.2f);
            int torches = 0;
            foreach (var s in inv.Slots) if (s.Type == ItemType.Torch) torches += s.Count;
            Plugin.Log.LogInfo($"[AutoTest] 丢弃 2.6s 后：地上 {WorldItems.Count} 个，背包里火把 {torches}（应为 30，被吸回来了）");

            // A right click off the inventory: the cursor's stack.
            InventoryGui.Open = true;
            inv.Mouse = ItemStack.Of(ItemType.Wood, 20);
            yield return null;
            mod.ThrowHeld(true);
            Plugin.Log.LogInfo($"[AutoTest] 右键丢光标物品：光标 {inv.Mouse.Type}，地上 {WorldItems.DevDescribe()}");
            yield return new WaitForSeconds(0.9f);
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.3f);

            // Closing a full inventory with a stack on the cursor: it drops at the player, grabbable at once.
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < Inventory.Size; i++) if (inv.Slots[i].IsEmpty) inv.Slots[i] = ItemStack.Of(ItemType.DirtBlock, 999);
            InventoryGui.Open = true;
            inv.Mouse = ItemStack.Of(ItemType.Rope, 15);
            yield return null;
            InventoryGui.Open = false;
            mod.ReturnOrDropMouseItem();
            Plugin.Log.LogInfo($"[AutoTest] 满背包关背包：光标 {inv.Mouse.Type}，地上 {WorldItems.DevDescribe()}");
            yield return new WaitForSeconds(0.8f);
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            Shot(scene, "d2_dropped_on_ground");
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForSeconds(2.5f);
            int ropes = 0;
            foreach (var s in inv.Slots) if (s.Type == ItemType.Rope) ropes += s.Count;
            Plugin.Log.LogInfo($"[AutoTest] 腾出背包 2.5s 后：脚边掉的绳子回到背包 {ropes}（应为 15）；地上还剩 {WorldItems.DevDescribe()}（74 px 外的木材够不着，原版也一样）");
            WorldItems.Clear();
        }

        IEnumerator FeaturesTest(string scene, HeroController hero)
        {
            var inv = mod.Inventory;
            var rb = hero.GetComponent<Rigidbody2D>();
            mod.DevNoFallDamage = true;

            // --- Inventory and crafting. ---
            InventoryGui.Open = true;
            yield return null;
            inv.Mouse = default;
            bool tb = InventoryGui.DevCraft(ItemType.TerraBlade);
            InventoryGui.DevLeftClick(2);
            InventoryGui.DevCraft(ItemType.NightsEdge);
            InventoryGui.DevLeftClick(3);
            InventoryGui.DevCraft(ItemType.Excalibur);
            InventoryGui.DevLeftClick(4);
            for (int k = 0; k < 12; k++) InventoryGui.DevCraft(ItemType.Wood);
            int woodOnMouse = inv.Mouse.Count;
            InventoryGui.DevLeftClick(6);
            InventoryGui.DevCraft(ItemType.FlyingCarpet);
            InventoryGui.DevLeftClick(15);
            InventoryGui.DevRightClick(15);
            bool carpetOn = false;
            foreach (var a in inv.Accessories) carpetOn |= a.Type == ItemType.FlyingCarpet;
            InventoryGui.DevAlt = true;
            InventoryGui.DevLeftClick(2);
            InventoryGui.DevAlt = false;
            bool fav = inv.Slots[2].Favorite;
            InventoryGui.DevCtrl = true;
            // A favourite isn't trashed: Ctrl does nothing special on it, so it's picked up as by a plain click.
            InventoryGui.DevLeftClick(2);
            var favTrash = inv.Trash.Type;
            var favMouse = inv.Mouse.Type;
            InventoryGui.DevCtrl = false;
            InventoryGui.DevLeftClick(2);
            InventoryGui.DevCtrl = true;
            InventoryGui.DevLeftClick(1); // the pickaxe: into the trash
            InventoryGui.DevCtrl = false;
            InventoryGui.DevRightClick(6);
            Plugin.Log.LogInfo($"[AutoTest] 物品栏：制作栏 {InventoryGui.RecipeCount} 种，拿泰拉刃 {tb}，木材连拿 12 次光标上 {woodOnMouse}；" +
                               $"右键装备飞毯 {carpetOn}；Alt 收藏泰拉刃 {fav}，Ctrl 点收藏的：垃圾桶 {favTrash}（应为 None）、拿到光标上 {favMouse}；放回后仍收藏 {inv.Slots[2].Favorite}；" +
                               $"Ctrl 扔铜镐进垃圾桶 {inv.Trash.Type}/格 {inv.Slots[1].Type}；" +
                               $"右键拿一个木材 光标 {inv.Mouse.Type}×{inv.Mouse.Count}，格里剩 {inv.Slots[6].Count}");
            inv.Slots[1] = ItemStack.Of(ItemType.CopperPickaxe);
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "f1_inventory_crafting");
            yield return new WaitForSeconds(0.2f);
            InventoryGui.DevFilterShot("f1e_filter_empty", 8);   // 时装：圣巢里没有这一类，格子应当空着（不弹任何提示）
            yield return new WaitForSeconds(0.4f);
            InventoryGui.DevFilterShot("f1f_filter_cleared", -1);
            yield return new WaitForSeconds(0.3f);
            inv.ReturnMouseItem();
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.3f);

            // --- Throwing items out and picking them up. ---
            yield return DropTest(scene, hero);

            // --- Swords on an enemy, and its health bar. ---
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                var hurt = hm.GetComponent<Collider2D>();
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hurt == null || !hurt.enabled || hm.gameObject.layer != 11) continue;
                if (hm.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) continue;
                var seen = EnemyStats.Of(hm);
                Plugin.Log.LogInfo($"[AutoTest] 场景里的敌人 {seen.Name}（{UI.EnemyHover.NameOf(hm)}）：HK 血量 {seen.MaxHp} → 泰拉血量 {EnemyStats.LifeMax(seen)}，防御 {seen.Defense}");
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null) Plugin.Log.LogInfo("[AutoTest] 没有敌人，跳过武器特效测试");
            else
            {
                var info = EnemyStats.Of(target);
                Plugin.Log.LogInfo($"[AutoTest] 敌人 {target.name}：层级 {info.Level}，HK 血量 {target.hp}（最多 {info.MaxHp}），泰拉血量 {EnemyStats.Life(target, info)}/{EnemyStats.LifeMax(info)}，" +
                                   $"防御 {info.Defense}，Boss={info.Boss}{(info.Ref != null ? "（泰拉 " + info.Ref.Boss.Name + "）" : "")}");
                // The cursor over it, held in front of the player: its name and life follow the cursor.
                var front = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 2.5f;
                target.transform.position = new Vector3(front.x, front.y, target.transform.position.z);
                yield return null;
                var tr = target.GetComponent<Renderer>();
                UI.EnemyHover.DevAt = tr != null ? (Vector2)tr.bounds.center : (Vector2)target.transform.position;
                yield return null;
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 鼠标指着 {target.name}：显示「{UI.EnemyHover.DevShown}」");
                var bossNames = new System.Text.StringBuilder();
                foreach (var b in new[] { "Hornet Boss 1", "Hornet Boss 2", "False Knight New", "Mega Moss Charger", "Mantis Lord", "Mawlek Body", "Giant Fly", "Giant Buzzer",
                             "Mage Lord", "Dung Defender", "Infected Knight", "Mimic Spider", "Black Knight 1", "Jar Collector", "Lost Kin", "Grimm Boss", "Radiance", "Hollow Knight Boss",
                             "Fluke Mother", "Mega Jellyfish", "Hive Knight", "Grey Prince", "White Defender", "Ghost Warrior Marmu", "Ghost Warrior Xero", "Oro", "Mato", "False Knight Dream", "Dream Mage Lord", "Absolute Radiance" })
                    bossNames.Append($"{b}={UI.EnemyHover.DevNameFor(b)}；");
                Plugin.Log.LogInfo($"[AutoTest] Boss 名字：{bossNames}");
               Shot(scene, "f2a_hover_name");
                yield return null;
                UI.EnemyHover.DevAt = null;
                target.hp = Mathf.Max(target.hp, 200);
                var trb = target.GetComponent<Rigidbody2D>();
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                foreach (var sword in new[] { ItemType.TerraBlade, ItemType.NightsEdge, ItemType.Excalibur, ItemType.LightsBane, ItemType.BladeofGrass,
                             ItemType.Muramasa, ItemType.FieryGreatsword, ItemType.CopperShortsword })
                {
                    if (target == null || target.isDead) break;
                    Vector2 hold = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 2.2f;
                    int life0 = EnemyStats.Life(target, EnemyStats.Of(target));
                    target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                    yield return null;
                    mod.DevUse(sword, hold);
                    int live = SwordFx.Instance.DevLive;
                    for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                    {
                        if (target == null) break;
                        target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                        if (trb != null) trb.linearVelocity = Vector2.zero;
                        if (t < 0.13f && t + Time.deltaTime >= 0.13f && (sword == ItemType.TerraBlade || sword == ItemType.NightsEdge || sword == ItemType.Excalibur || sword == ItemType.LightsBane))
                            Shot(scene, "f2_" + sword);
                        yield return null;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] {ItemDb.Get(sword).Name}：特效弹幕 {live} 个，闪光 {SwordFx.Instance.DevFlashes}，敌人泰拉血量 {life0} -> {(target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0)}");
                }
                Shot(scene, "f3_health_bar");

                // --- Knockback: how far each weapon pushes, by Terraria's rule, against the nail's own push. ---
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                    var inf = EnemyStats.Of(hm);
                    float resist = Items.EnemyStats.Unstoppable(inf) ? 0f : Areas.KnockbackTaken(inf.Level);
                    var line = new System.Text.StringBuilder($"[AutoTest] 击退 {inf.Name}：自身后坐 {TerraKnockback.DevRecoil(hm) ?? "无"}，抗性 {resist}");
                    foreach (var (w, dmg) in new[] { (ItemType.CopperShortsword, 5), (ItemType.CopperBroadsword, 8), (ItemType.TerraBlade, 93) })
                    {
                        var d = ItemDb.Get(w);
                        float v = TerraKnockback.Speed(d.Knockback, resist, EnemyStats.Strike(inf, dmg, false), false, EnemyStats.LifeMax(inf));
                        line.Append($"；{d.Name}(击退 {d.Knockback}) 速度 {v:F2} px/帧，推 {TerraKnockback.Distance(v) * Effects.TerraParticles.Px:F2} 格单位，倍率 {TerraKnockback.Magnitude(hm, v):F2}");
                    }
                    Plugin.Log.LogInfo(line.ToString());
                }
                foreach (var w in new[] { ItemType.CopperBroadsword, ItemType.TerraBlade })
                {
                    if (target == null || target.isDead) break;
                    target.hp = Mathf.Max(target.hp, 200);
                    Vector2 at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 1.2f;
                    target.transform.position = new Vector3(at.x, at.y, target.transform.position.z);
                    if (trb != null) trb.linearVelocity = Vector2.zero;
                    yield return null;
                    float x0 = target.transform.position.x, far = 0f;
                    mod.DevUse(w, at + Vector2.right);
                    for (float t = 0f; t < 0.7f && target != null; t += Time.deltaTime)
                    {
                        far = Mathf.Max(far, target.transform.position.x - x0);
                        yield return null;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 击退实测 {ItemDb.Get(w).Name} 打 {target?.name}：0.7s 内最多被推开 {far:F2}");
                    yield return new WaitForSeconds(0.3f);
                }
            }

            // The enemy left alive would otherwise end the rest of the test (enemies hit hard now).
            PlayerData.instance.isInvincible = true;
            mod.Stats.FullHeal();

            // --- The flying carpet. ---
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.up * 0.2f);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            rb.linearVelocity = new Vector2(0f, 6f * Player.TerraMovement.PxT);
            Player.TerraMovement.DevJump = true;
            float rideStart = -1f, y0 = 0f, maxFall = 0f;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                // The carpet comes out once every jump is used: the Cloud in a Bottle's too.
                if (!hero.cState.onGround) HarmonyLib.Traverse.Create(hero).Field("doubleJumped").SetValue(true);
                if (Player.TerraMovement.OnCarpet && rideStart < 0f)
                {
                    rideStart = t;
                    y0 = hero.transform.position.y;
                }
                if (rideStart >= 0f) maxFall = Mathf.Max(maxFall, y0 - hero.transform.position.y);
                if (rideStart >= 0f && t - rideStart > 0.5f && t - rideStart - Time.deltaTime <= 0.5f) Shot(scene, "f4_carpet");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 飞毯：{(rideStart >= 0f ? $"{rideStart:F2}s 铺开" : "没铺开")}，之后最多下降 {maxFall:F2}（应≈0），帧 {Player.TerraMovement.CarpetFrame}");
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(1.5f);

            // --- The hook leaves gravity alone. ---
            float g0 = rb.gravityScale;
            yield return HookTest(hero);
            Plugin.Log.LogInfo($"[AutoTest] 抓钩前后重力 {g0} -> {rb.gravityScale}（应不变）");

            // --- A pylon at Dirtmouth's bench. ---
            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            PlayMakerFSM bench = null;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (fsm.FsmName == "Bench Control" && fsm.gameObject.scene.name == "Town") bench = fsm;
            if (bench == null) Plugin.Log.LogInfo("[AutoTest] 德特茅斯没找到长椅");
            else
            {
                var pd = PlayerData.instance;
                Plugin.Log.LogInfo($"[AutoTest] 长椅 {bench.gameObject.name} 标签 {bench.gameObject.tag} 位置 {bench.transform.position}");
                hero.transform.position = Clear(hero, (Vector2)bench.transform.position + Vector2.left * 3f);
                yield return new WaitForSeconds(0.5f);
                // As the bench does it: seated first, the respawn point set a moment later.
                pd.atBench = true;
                yield return new WaitForSeconds(0.5f);
                pd.SetBenchRespawn(bench.gameObject.name, "Town", 1, true);
                yield return new WaitForSeconds(0.5f);
                pd.atBench = false;
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 晶塔：解锁 {Pylons.Unlocked.Count} 个{(Pylons.Unlocked.Count > 0 ? $"，{Pylons.Unlocked[0].Name} 在 {Pylons.Unlocked[0].Local}" : "")}");
                Shot("Town", "f5_pylon");
                // The townsfolk panel's ground: a click in the air, one away from the flat street, one nowhere.
                Plugin.Log.LogInfo($"[AutoTest] 居民落点：空中 {Town.TownNpcs.DevPlaceAt("PartyGirl", new Vector2(150f, 26f))}");
                Plugin.Log.LogInfo($"[AutoTest] 居民落点：街外 {Town.TownNpcs.DevPlaceAt("Golfer", new Vector2(66f, 12f))}");
                Plugin.Log.LogInfo($"[AutoTest] 居民落点：半空远处 {Town.TownNpcs.DevPlaceAt("Painter", new Vector2(4f, 30f))}");
                UI.FullMap.OpenForPylons();
                yield return new WaitForSeconds(0.6f);
                Shot("Town", "f6_pylon_map");
                yield return new WaitForSeconds(0.2f);
                UI.FullMap.DevSetOpen(false);
                yield return new WaitForSeconds(0.5f);
                if (Pylons.Unlocked.Count > 0)
                {
                    yield return GoTo("Tutorial_01", "right1");
                    Pylons.Teleport(Pylons.Unlocked[0]);
                    float wait = Time.time + 30f;
                    var gm = GameManager.instance;
                    while ((gm.sceneName != "Town" || gm.IsInSceneTransition) && Time.time < wait) yield return null;
                    yield return new WaitForSeconds(3f);
                    hero = HeroController.instance;
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔传送：到了 {gm.sceneName}，主角 {hero.transform.position:F1}，坐在长椅上={PlayerData.instance.atBench}");
                    Shot(gm.sceneName, "f7_after_teleport");
                }
            }
            mod.DevNoFallDamage = false;
            PlayerData.instance.isInvincible = false;
        }
    }
}
