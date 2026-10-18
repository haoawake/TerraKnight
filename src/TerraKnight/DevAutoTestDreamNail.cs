using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dreamnail"): the Dream Nail item. Given once the Knight has the Dream Nail;
    /// in hand the button held charges the game's own Dream Nail and let go slashes, reading an
    /// enemy and hurting it by the progress; it can't be thrown or trashed and comes back if lost;
    /// with S held it sets the Dreamgate.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamNailTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            pd.hasDreamNail = true;
            yield return new WaitForSeconds(1.5f);
            int slot = SlotOf(ItemType.DreamNail);
            Plugin.Log.LogInfo($"[AutoTest] 梦钉 拿到梦之钉后：背包第 {slot} 格{(slot >= 0 ? "（通过）" : "（没给，不通过）")}，图标 {Terraria.TerrariaAssets.Texture(DreamNailItem.Icon).width}x{Terraria.TerrariaAssets.Texture(DreamNailItem.Icon).height}");
            if (slot < 0) yield break;

            // Into the hotbar's first slot and in hand.
            if (slot >= Inventory.HotbarSize)
            {
                (inv.Slots[0], inv.Slots[slot]) = (inv.Slots[slot], inv.Slots[0]);
                slot = 0;
            }
            inv.Selected = slot;
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.5f);
            Shot(GameManager.instance.sceneName, "dreamnail_1_bag");
            yield return null;
            InventoryGui.Open = false;

            // An enemy on the ground in the Crossroads' first room.
            const string room = "Crossroads_01";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            HealthManager target = null;
            foreach (var hm in World.EnemyRespawn.Instance.DevLive(room))
                if (hm.name.StartsWith("Zombie") && (target == null || Vector2.Distance(hm.transform.position, hero.transform.position) < Vector2.Distance(target.transform.position, hero.transform.position)))
                    target = hm;
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 梦钉 找不到地上的怪，不通过");
                yield break;
            }
            var fsm = DreamFsm(hero);
            if (fsm != null) DumpFsm(fsm);
            Plugin.Log.LogInfo($"[AutoTest] 梦钉 主角的梦钉状态机：{(fsm != null ? fsm.FsmName + " 在 " + fsm.ActiveStateName : "没有")}；目标 {target.name}");

            // The hero stays where he came in (on the ground); the others go, and the target is brought before him, standing still.
            foreach (var other in World.EnemyRespawn.Instance.DevLive(room)) if (other != target && Vector2.Distance(other.transform.position, hero.transform.position) < 25f) Kill(other);
            foreach (var f in target.GetComponentsInChildren<PlayMakerFSM>()) f.enabled = false;
            // Unity still delivers trigger callbacks to disabled MonoBehaviours. Remove the
            // test target's contact damage, keeping its collider and dream reaction intact.
            foreach (var d in target.GetComponentsInChildren<DamageHero>()) Object.Destroy(d);
            foreach (var (stage, label) in new[] { (0, "阶段0"), (3, "阶段3") })
            {
                Shop.DevStage = stage;
                yield return new WaitForSeconds(1.2f);
                if (target == null || target.isDead) break;
                var tb = target.GetComponent<Collider2D>().bounds;
                var feet = hero.GetComponent<BoxCollider2D>().bounds;
                hero.FaceRight();
                target.transform.position += new Vector3(feet.max.x + 1.5f + tb.extents.x - tb.center.x, feet.min.y + tb.extents.y + 0.05f - tb.center.y, 0f);
                var trb = target.GetComponent<Rigidbody2D>();
                if (trb != null) trb.linearVelocity = Vector2.zero;
                float settle = Time.time + 3f;
                while ((!hero.cState.onGround || Mathf.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.y) > 0.05f) && Time.time < settle) yield return null;
                yield return new WaitForSeconds(0.4f);
                Plugin.Log.LogInfo($"[AutoTest] 梦钉 {label} 开始前：拿着梦钉={DreamNailItem.Holding()}，CanDreamNail={hero.CanDreamNail()}，在地上={hero.cState.onGround}，主角 {(Vector2)hero.transform.position:F1}，目标 {(Vector2)target.transform.position:F1}");
                int hp0 = target.hp, hits0 = DreamNailItem.DevHits;
                var states = new List<string>();
                string last = null;
                float t0 = Time.time;
                DreamNailItem.DevHold = true;
                float charge = 0f;
                float enteredAt = 0f;
                bool shotCharge = false, shotSlash = false;
                var slashFx = hero.transform.Find("Dream Effects/Slash");
                while (Time.time - t0 < 3.2f)
                {
                    string now = fsm != null ? fsm.ActiveStateName : "?";
                    if (now != last)
                    {
                        states.Add($"{Time.time - t0:F2}s {now}"); last = now; enteredAt = Time.time;
                        if (now == "Inactive" && Time.time - t0 > 0.1f && Time.time - t0 < 1f)
                            Plugin.Log.LogInfo($"[AutoTest] 梦钉 早停诊断：持有={DreamNailItem.Holding()} DevHold={DreamNailItem.DevHold} 地面={hero.cState.onGround} 速度={hero.GetComponent<Rigidbody2D>().linearVelocity:F3} 可操作={GameHooks.PlayerHasControl()} 血={mod.Stats.Life} UI={InventoryGui.Open}");
                    }
                    if (!shotCharge && now == "Charge" && Time.time - enteredAt > 0.3f) { shotCharge = true; Shot(room, $"dreamnail_2_charge_{label}"); }
                    if (!shotSlash && now == "Slash" && Time.time - enteredAt > 0.06f)
                    {
                        shotSlash = true;
                        var r = slashFx != null ? slashFx.GetComponent<Renderer>() : null;
                        Plugin.Log.LogInfo($"[AutoTest] 梦钉 {label} 挥砍时的特效：{(slashFx == null ? "找不到 Dream Effects/Slash" : $"激活={slashFx.gameObject.activeInHierarchy} 显示={(r != null && r.enabled)} 位置 {(Vector2)slashFx.position:F1} 层 {r?.sortingLayerName}/{r?.sortingOrder}")}");
                        Shot(room, $"dreamnail_3_slash_{label}");
                    }
                    if (DreamNailItem.DevHold == true && Time.time - t0 > 2.4f) { DreamNailItem.DevHold = false; charge = Time.time - t0; }
                    yield return null;
                }
                DreamNailItem.DevHold = null;
                int lost = target != null ? hp0 - target.hp : hp0;
                Plugin.Log.LogInfo($"[AutoTest] 梦钉 {label}（伤害 {Equipment.Damage(ItemDb.Get(ItemType.DreamNail))}）按住 {charge:F1} 秒再松开：状态 {string.Join(" → ", states)}；打中 {DreamNailItem.DevHits - hits0} 下（{DreamNailItem.DevLastHit}），敌人掉了原版血量 {lost}（{hp0} → {(target != null ? target.hp : 0)}）{(DreamNailItem.DevHits > hits0 && lost > 0 ? " 通过" : " 不通过")}");
            }
            Shop.DevStage = -1;

            // Kept for good: thrown, trashed, closed on a full bag, lost.
            slot = SlotOf(ItemType.DreamNail);
            inv.Selected = slot;
            mod.ThrowHeld(false);
            yield return new WaitForSeconds(0.3f);
            bool keptThrow = SlotOf(ItemType.DreamNail) == slot;
            inv.Mouse = inv.Slots[slot];
            inv.Slots[slot] = default;
            AccessTools.Method(typeof(InventoryGui), "ClickTrash").Invoke(null, null);
            bool keptTrash = inv.Mouse.Type == ItemType.DreamNail && inv.Trash.Type != ItemType.DreamNail;
            // A full bag: it takes a plain item's place, that one drops.
            var saved = (ItemStack[])inv.Slots.Clone();
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].IsEmpty) inv.Slots[i] = ItemStack.Of(ItemType.DirtBlock, 1);
            mod.ReturnOrDropMouseItem();
            bool keptFull = (SlotOf(ItemType.DreamNail) >= 0 || inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail) && inv.Mouse.IsEmpty;
            for (int i = 0; i < inv.Slots.Length; i++) if (inv.Slots[i].Type == ItemType.DirtBlock && saved[i].IsEmpty) inv.Slots[i] = default;
            // Safekeeping in the Hallownest bag is intentional; move it back after making room.
            if (inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail && inv.Pickup(inv.HkBag[HkItems.DreamNailSlot]) == 0)
                inv.HkBag[HkItems.DreamNailSlot] = default;
            // Lost (taken out by hand): it comes back within a second or two.
            int s2 = SlotOf(ItemType.DreamNail);
            if (s2 >= 0) inv.Slots[s2] = default;
            yield return new WaitForSeconds(2.2f);
            bool back = SlotOf(ItemType.DreamNail) >= 0 || World.Chests.Holds(ItemType.DreamNail);
            Plugin.Log.LogInfo($"[AutoTest] 梦钉 永久保留：按丢弃键 {(keptThrow ? "还在" : "丢出去了")}，扔垃圾桶 {(keptTrash ? "不收" : "收了")}，背包满时关背包 {(keptFull ? "放回了背包" : "没放回")}，弄丢后 {(back ? "补回来了" : "没补")}{(keptThrow && keptTrash && keptFull && back ? " 通过" : " 不通过")}");

            // The Dreamgate: S held as it is swung.
            pd.hasDreamGate = true;
            slot = SlotOf(ItemType.DreamNail);
            if (slot >= Inventory.HotbarSize) { (inv.Slots[0], inv.Slots[slot]) = (inv.Slots[slot], inv.Slots[0]); slot = 0; }
            inv.Selected = slot;
            // A few steps into the room (from its left gate: rightwards), on its floor.
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 3f);
            yield return new WaitForSeconds(0.8f);
            {
                // What the FSM's "Can Set?" looks at: the map's room flag, a parent, the floor 0.3 either side.
                var hp = (Vector2)hero.transform.position;
                bool l = Physics2D.Raycast(hp + new Vector2(-0.3f, 0f), Vector2.down, 2f, 1 << 8).collider != null;
                bool r = Physics2D.Raycast(hp + new Vector2(0.3f, 0f), Vector2.down, 2f, 1 << 8).collider != null;
                var map = GameManager.instance.gameMap != null ? GameManager.instance.gameMap.GetComponent<GameMap>() : null;
                Plugin.Log.LogInfo($"[AutoTest] 梦钉 设门前：主角 {hp:F1}，在小房间 {(map != null && map.GetInRoom())}，父物体 {(hero.transform.parent != null ? hero.transform.parent.name : "无")}，左脚下有地 {l}，右脚下有地 {r}");
            }
            string gate0 = pd.dreamGateScene;
            var gateStates = new List<string>();
            string lastGate = null;
            KeysDown(Key.S);
            yield return new WaitForSeconds(0.15f);
            DreamNailItem.DevHold = true;
            float g0 = Time.time;
            while (Time.time - g0 < 4f)
            {
                string now = fsm != null ? fsm.ActiveStateName : "?";
                if (now != lastGate) { gateStates.Add($"{Time.time - g0:F2}s {now}"); lastGate = now; }
                if (Time.time - g0 > 3f) DreamNailItem.DevHold = false;
                yield return null;
            }
            DreamNailItem.DevHold = null;
            KeysDown();
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 梦钉 按住 S 挥梦钉：状态 {string.Join(" → ", gateStates)}；梦之门 {gate0 ?? "无"} → {pd.dreamGateScene ?? "无"}{(pd.dreamGateScene == room ? " 通过" : " 不通过")}");

            // Travel from another room using the actual Dream Nail listener, not a direct warp.
            Vector2 gateAt = new Vector2(pd.dreamGateX, pd.dreamGateY);
            pd.dreamOrbs = 10;
            yield return GoTo("Town", EntryGateOf("Town"));
            yield return new WaitForSeconds(1f);
            KeysDown(Key.W);
            yield return new WaitForSeconds(0.15f);
            DreamNailItem.DevHold = true;
            float warpEnd = Time.realtimeSinceStartup + 20f;
            var warpStates = new List<string>();
            string lastWarp = null;
            var warpFsm = DreamFsm(HeroController.instance);
            float warpStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - warpStart < 4.3f)
            {
                string now = warpFsm != null ? warpFsm.ActiveStateName : "?";
                if (now != lastWarp) { warpStates.Add($"{Time.realtimeSinceStartup - warpStart:F2}s {now}"); lastWarp = now; }
                yield return null;
            }
            DreamNailItem.DevHold = false;
            KeysDown();
            while (Time.realtimeSinceStartup < warpEnd && (GameManager.instance.sceneName != room || !GameHooks.PlayerHasControl())) yield return null;
            DreamNailItem.DevHold = null;
            yield return new WaitForSeconds(1f);
            var arrived = HeroController.instance;
            float distance = Vector2.Distance(arrived.transform.position, gateAt);
            bool warped = GameManager.instance.sceneName == room && GameHooks.PlayerHasControl() && distance < 3f && pd.dreamOrbs == 9;
            Plugin.Log.LogInfo($"[AutoTest] 梦钉 按住 W 传回：状态 {string.Join(" → ", warpStates)}；场景 {GameManager.instance.sceneName}，离门 {distance:F2}，精华 {pd.dreamOrbs}（应9），可操作={GameHooks.PlayerHasControl()}{(warped ? " 通过" : " 不通过")}");
            Shot(GameManager.instance.sceneName, "dreamnail_4_warp");
            yield return null;

            // Quit/reload through the game's menus verifies both saves, including transient drops.
            WorldItems.Drop(ItemStack.Of(ItemType.Wood, 23), (Vector2)arrived.transform.position + Vector2.right * 8f);
            yield return QuitTest(1);
            pd = PlayerData.instance;
            bool restored = pd.hasDreamNail && pd.hasDreamGate && pd.dreamGateScene == room && SlotOf(ItemType.DreamNail) >= 0;
            bool dropsCleared = !WorldItems.Lying(ItemType.Wood);
            Plugin.Log.LogInfo($"[AutoTest] 梦钉 退出读档后：能力={pd.hasDreamNail}，物品格={SlotOf(ItemType.DreamNail)}，梦之门={pd.dreamGateScene}，掉落木材清空={dropsCleared}{(restored && dropsCleared ? " 通过" : " 不通过")}");
        }

        IEnumerator DreamEntryTest()
        {
            var pd = PlayerData.instance;
            pd.hasDreamNail = true;
            pd.falseKnightDefeated = true;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return new WaitForSeconds(1.5f);
            int itemSlot = SlotOf(ItemType.DreamNail);
            if (itemSlot < 0) { Plugin.Log.LogError("[AutoTest] 梦境入口 梦钉物品缺失 不通过"); yield break; }
            (mod.Inventory.Slots[0], mod.Inventory.Slots[itemSlot]) = (mod.Inventory.Slots[itemSlot], mod.Inventory.Slots[0]);
            mod.Inventory.Selected = 0;
            yield return GoTo("Crossroads_10", "left1");
            // Inspect the corpse's actual dream-entry FSM; use its collider as the swing target.
            PlayMakerFSM entry = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!f.gameObject.scene.name.StartsWith("Crossroads_10")) continue;
                bool dream = false;
                foreach (var v in f.FsmVariables.StringVariables) if (v.Value == "Dream_01_False_Knight") dream = true;
                foreach (var state in f.FsmStates)
                    foreach (var action in state.Actions)
                    {
                        var field = AccessTools.Field(action.GetType(), "sceneName");
                        if (field?.GetValue(action) is HutongGames.PlayMaker.FsmString destination && destination.Value == "Dream_01_False_Knight") dream = true;
                    }
                string path = PathOf(f.transform);
                if (!dream && (path.ToLowerInvariant().Contains("dream") || path.ToLowerInvariant().Contains("corpse")))
                {
                    Plugin.Log.LogInfo($"[AutoTest] 梦境入口 查找 {path} FSM={f.FsmName} 激活={f.gameObject.activeInHierarchy}");
                    DumpFsm(f);
                }
                if (!dream) continue;
                Plugin.Log.LogInfo($"[AutoTest] 梦境入口 候选 {PathOf(f.transform)} FSM={f.FsmName} 状态={f.ActiveStateName} 激活={f.gameObject.activeInHierarchy}");
                DumpFsm(f);
                if (f.gameObject.activeInHierarchy) entry = f;
            }
            if (entry == null) { Plugin.Log.LogError("[AutoTest] 梦境入口 找不到假骑士尸体入口 不通过"); yield break; }
            var hero = HeroController.instance;
            foreach (var hm in World.EnemyRespawn.Instance.DevLive("Crossroads_10")) if (Vector2.Distance(hm.transform.position, entry.transform.position) < 20f) Kill(hm);
            var col = entry.GetComponent<Collider2D>() ?? entry.GetComponentInChildren<Collider2D>();
            Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)entry.transform.position;
            hero.transform.position = Clear(hero, at + Vector2.left * 1.8f);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            hero.FaceRight();
            yield return new WaitForSeconds(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 梦境入口 挥砍前 主角={hero.transform.position:F1} 目标={at:F1} 地面={hero.cState.onGround} FSM={entry.ActiveStateName}");
            DreamNailItem.DevHold = true;
            yield return new WaitForSecondsRealtime(2.5f);
            DreamNailItem.DevHold = false;
            float until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until && (GameManager.instance.sceneName != "Dream_01_False_Knight" || !GameHooks.PlayerHasControl())) yield return null;
            DreamNailItem.DevHold = null;
            bool entered = GameManager.instance.sceneName == "Dream_01_False_Knight" && GameHooks.PlayerHasControl();
            Plugin.Log.LogInfo($"[AutoTest] 梦境入口 梦钉击中假骑士尸体：场景={GameManager.instance.sceneName} 可操作={GameHooks.PlayerHasControl()}{(entered ? " 通过" : " 不通过")}");
            Shot(GameManager.instance.sceneName, "dreamnail_5_dream_entry");
            yield return null;
        }

        static PlayMakerFSM DreamFsm(HeroController hero)
        {
            foreach (var f in hero.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f.FsmName == "Dream Nail") return f;
            return null;
        }
    }
}
