using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (3.0.1): suite "hkflagdump" dumps the state machines of a few scenes that read a Hallownest key item's record (the
    /// Abyss's gate to the King's Brand, the Birthplace's floor to the Kingsoul); "invlag" walks with the inventory open and times
    /// the crafting column; "nailhits" counts the Nail's and a sword's hits on one enemy a swing; "hkuse" walks into the room with
    /// both bags full, lets the King's Brand and the Kingsoul come into a freed hotbar slot, and the room answers; "v301" runs the last three.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] hkFlags =
        {
            "hasLantern", "hasCityKey", "simpleKeys", "hasWhiteKey", "hasLoveKey", "hasSlykey", "hasTramPass", "hasKingsBrand",
            "hasXunFlower", "rancidEggs", "ore", "trinket1", "trinket2", "trinket3", "trinket4", "equippedCharm_36",
        };

        /// <summary>Whether any action of the FSM names one of the key items' records.</summary>
        static bool ReadsHkFlag(PlayMakerFSM f)
        {
            foreach (var s in f.FsmStates)
                foreach (var a in s.Actions)
                {
                    if (!a.GetType().Name.Contains("PlayerData")) continue;
                    foreach (var field in a.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    {
                        var v = field.GetValue(a);
                        if (v is HutongGames.PlayMaker.FsmString fs && !fs.UsesVariable && System.Array.IndexOf(hkFlags, fs.Value) >= 0) return true;
                        if (v is HutongGames.PlayMaker.FsmString[] arr)
                            foreach (var e in arr) if (e != null && !e.UsesVariable && System.Array.IndexOf(hkFlags, e.Value) >= 0) return true;
                    }
                }
            return false;
        }

        IEnumerator HkFlagDump()
        {
            foreach (var name in new[] { "Abyss_06_Core", "Room_Wyrm", "Deepnest_East_12" })
            {
                var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (load == null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 圣巢记录 {name} 载入不了");
                    continue;
                }
                while (!load.isDone) yield return null;
                yield return new WaitForSeconds(0.5f);
                var scene = USM.GetSceneByName(name);
                int n = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (!ReadsHkFlag(f)) continue;
                        n++;
                        Plugin.Log.LogInfo($"[AutoTest] 圣巢记录 {name}：{PathOf(f.transform)} '{f.FsmName}' 位置 {f.transform.position:F1}");
                        DumpFsm(f);
                    }
                Plugin.Log.LogInfo($"[AutoTest] 圣巢记录 {name} 导出了 {n} 个");
                var unload = USM.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
        }

        int v301Ok, v301All;

        void V301Check(string what, bool pass, string detail)
        {
            v301All++;
            if (pass) v301Ok++;
            Plugin.Log.LogInfo($"[AutoTest] 3.0.1 {what}：{detail} {(pass ? "通过" : "不通过")}");
        }

        IEnumerator V301Test(string only)
        {
            v301Ok = v301All = 0;
            if (only == null || only == "invlag") yield return InvLagTest();
            if (only == null || only == "nailhits") yield return NailHitsTest();
            if (only == null || only == "hkuse") yield return HkUseTest();
            if (only == null || only == "prefixkeep") yield return PrefixKeepTest();
            if (only == null || only == "craftrule") yield return CraftRuleTest();
            if (only == null || only == "tombstone") yield return TombstoneTest();
            if (only == null || only == "dashes") yield return DashesTest();
            if (only == null || only == "furniture2") yield return CarriedFurnitureTest();
            // Last: it leaves for the title screen.
            if (only == null || only == "spikymenu") yield return SpikyMenuTest();
            Plugin.Log.LogInfo($"[AutoTest] 3.0.1 合计 {v301Ok}/{v301All} {(v301Ok == v301All ? "通过" : "不通过")}");
        }

        // ------------------------------------------------------------------ the inventory open while walking

        IEnumerator InvLagTest()
        {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            // A bag of everyday materials, so the column has something to offer.
            inv.Slots[20] = ItemStack.Of(ItemType.Wood, 200);
            inv.Slots[21] = ItemStack.Of(ItemType.StoneBlock, 200);
            inv.Slots[22] = ItemStack.Of(ItemType.Gel, 60);
            inv.Slots[23] = ItemStack.Of(ItemType.IronBar, 30);
            inv.Slots[24] = ItemStack.Of(ItemType.CopperBar, 30);
            InventoryGui.Open = false;
            List<float> closed = null, open = null;
            yield return WalkMeasure(false, f => closed = f);
            InventoryGui.Open = true;
            yield return null;
            bool drawn = InventoryGui.DevCraftingDrawn;
            yield return WalkMeasure(true, f => open = f);
            drawn |= InventoryGui.DevCraftingDrawn;

            // The column's list made the way 3.0.0 made it (each recipe asking for itself) and the way it is made now.
            var hero = HeroController.instance;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int oldCount = 0;
            const int n = 20;
            for (int k = 0; k < n; k++)
            {
                oldCount = 0;
                bool here = Shop.Available(hero);
                int stage = Shop.UnlockedStage();
                var near = World.Chests.ForCrafting(hero);
                foreach (var r in Recipes.All)
                {
                    bool byHand = !r.IsBought && (string.IsNullOrEmpty(r.Station) || r.Station == "徒手");
                    bool can = (!r.IsBought || ProgressionBalance.Unlocked(r.Result)) && CraftEnvironments.Available(r.Environment)
                        && (r.IsBought ? here : byHand || World.Stations.Near(r.Station, hero));
                    if (can && Recipes.HasIngredients(r, inv, near)) oldCount++;
                }
            }
            double oldMs = sw.Elapsed.TotalMilliseconds / n;
            double newMs = InventoryGui.DevRefreshMs(n);
            int newCount = InventoryGui.DevOffered().Count(t => Recipes.For(t) != null && !Recipes.For(t).IsBought);
            sw.Restart();
            for (int k = 0; k < 200; k++) Shop.Available(hero);
            double benchMs = sw.Elapsed.TotalMilliseconds / 200;
            InventoryGui.Open = false;
            inv.Infinite = infinite;

            float Avg(List<float> f) => f == null || f.Count == 0 ? 0f : f.Average() * 1000f;
            float P95(List<float> f) { if (f == null || f.Count == 0) return 0f; var s = f.OrderBy(x => x).ToList(); return s[(int)(s.Count * 0.95f)] * 1000f; }
            Plugin.Log.LogInfo($"[AutoTest] 3.0.1 背包关着走：{FrameStats(closed)}");
            Plugin.Log.LogInfo($"[AutoTest] 3.0.1 背包开着走：{FrameStats(open)}");
            V301Check("制作栏每帧的列表", drawn && newMs < 1.0 && newMs * 3 < oldMs,
                $"3.0.0 的做法每次 {oldMs:F2} ms（可做 {oldCount} 条），现在每次 {newMs:F3} ms（可做 {newCount} 条），找长椅每次 {benchMs:F4} ms，制作栏画了 {drawn}");
            V301Check("背包开着走不掉帧", Avg(open) < Avg(closed) + 2f && P95(open) < P95(closed) + 4f,
                $"关着平均 {Avg(closed):F1} ms、95% {P95(closed):F1} ms；开着平均 {Avg(open):F1} ms、95% {P95(open):F1} ms");
        }

        IEnumerator WalkMeasure(bool open, System.Action<List<float>> done)
        {
            var frames = new List<float>();
            foreach (int dir in new[] { 1, -1, 1, -1 })
            {
                Player.TerraMovement.DevMove = dir;
                float until = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < until)
                {
                    InventoryGui.Open = open;
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime);
                }
            }
            Player.TerraMovement.DevMove = 0;
            done(frames);
        }

        // ------------------------------------------------------------------ one hit a swing

        IEnumerator NailHitsTest()
        {
            var pd = PlayerData.instance;
            if (GameManager.instance.sceneName != "Crossroads_01")
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                pd.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
            }
            pd.isInvincible = true;
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            var rb = hero.GetComponent<Rigidbody2D>();
            HealthManager target = null;
            float bestD = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != "Crossroads_01") continue;
                var info = EnemyStats.Of(hm);
                if (info == null || info.Boss) continue;
                if (!EnemyRespawn.Instance.DevBody(hm, out _, out _, out _, out bool walker) || !walker) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < bestD) { bestD = d; target = hm; }
            }
            if (target == null)
            {
                V301Check("一刀一下", false, "找不到怪");
                yield break;
            }
            target.hp = Mathf.Max(target.hp, 5000);
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != target && Vector2.Distance(hm.transform.position, target.transform.position) < 12f) hm.gameObject.SetActive(false);
            var log = new List<string>();
            NailItem.DevLog = line => log.Add(line);
            int cols = target.GetComponentsInChildren<Collider2D>().Length;
            var trb = target.GetComponent<Rigidbody2D>();
            if (trb != null) { trb.linearVelocity = Vector2.zero; trb.bodyType = RigidbodyType2D.Kinematic; }
            var tb = target.GetComponent<Collider2D>().bounds;
            var stand = target.transform.position;

            // How many times the enemy's health went down over a while (each a hit's number).
            int drops = 0;
            IEnumerator Watch(float seconds, System.Action each)
            {
                int hp = target.hp;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    each?.Invoke();
                    yield return null;
                    if (target == null) yield break;
                    target.transform.position = stand;
                    if (target.hp < hp) drops++;
                    hp = target.hp;
                }
            }

            // The Nail: one slash at a time, each held for one frame.
            inv.Slots[0] = ItemStack.Of(ItemType.KnightNail);
            inv.Selected = 0;
            inv.Override = -1;
            int swings0 = NailItem.DevSwings, hits0 = NailItem.DevHits, repeats0 = NailItem.DevRepeatsRefused, numbers0 = UI.CombatText.DevEnemyNumbers;
            drops = 0;
            for (int k = 0; k < 3; k++)
            {
                hero.transform.position = Clear(hero, new Vector2(tb.min.x - 0.9f, tb.center.y));
                hero.FaceRight();
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.2f);
                int s0 = NailItem.DevSwings;
                NailItem.DevHold = true;
                for (int w = 0; w < 20 && NailItem.DevSwings == s0; w++) yield return null;
                NailItem.DevHold = false;
                yield return Watch(0.6f, null);
            }
            NailItem.DevHold = null;
            if (target == null)
            {
                V301Check("骨钉一刀一下", false, "测试中怪没了（主角死了或换了场景）");
                yield break;
            }
            NailItem.DevLog = null;
            int swings = NailItem.DevSwings - swings0, hits = NailItem.DevHits - hits0, numbers = UI.CombatText.DevEnemyNumbers - numbers0;
            foreach (var line in log) Plugin.Log.LogInfo($"[AutoTest] 3.0.1 骨钉打中：{line}");
            V301Check("骨钉一刀一下", swings > 0 && hits == swings && numbers == swings,
                $"{target.name}（碰撞体 {cols} 个）：挥了 {swings} 刀，伤害数字 {numbers} 个，经过 mod 的伤害 {hits} 下，血掉了 {drops} 次，同一刀又碰到被拦下 {NailItem.DevRepeatsRefused - repeats0} 次");

            // Many of the game's enemies have more than one hitbox (a body and a head): the slash meets each.
            var main = target.GetComponent<Collider2D>();
            var extra = new GameObject("Second Hitbox");
            extra.layer = target.gameObject.layer;
            extra.transform.SetParent(target.transform, false);
            var eb = extra.AddComponent<BoxCollider2D>();
            eb.size = new Vector2(main.bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(target.transform.lossyScale.x)), main.bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(target.transform.lossyScale.y)));
            eb.isTrigger = main.isTrigger;
            yield return new WaitForFixedUpdate();
            log.Clear();
            NailItem.DevLog = line => log.Add(line);
            swings0 = NailItem.DevSwings; numbers0 = UI.CombatText.DevEnemyNumbers; repeats0 = NailItem.DevRepeatsRefused;
            for (int k = 0; k < 2; k++)
            {
                hero.transform.position = Clear(hero, new Vector2(tb.min.x - 0.9f, tb.center.y));
                hero.FaceRight();
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.2f);
                int s0 = NailItem.DevSwings;
                NailItem.DevHold = true;
                for (int w = 0; w < 20 && NailItem.DevSwings == s0; w++) yield return null;
                NailItem.DevHold = false;
                yield return Watch(0.6f, null);
            }
            NailItem.DevHold = null;
            NailItem.DevLog = null;
            int swings2 = NailItem.DevSwings - swings0, numbers2 = UI.CombatText.DevEnemyNumbers - numbers0;
            V301Check("两个受击框的怪，骨钉一刀一下", swings2 > 0 && numbers2 == swings2,
                $"挥了 {swings2} 刀，伤害数字 {numbers2} 个，打到受击框 {log.Count} 次，同一刀又碰到被拦下 {NailItem.DevRepeatsRefused - repeats0} 次");
            Object.Destroy(extra);

            // A Terraria sword: one swing.
            inv.Slots[1] = ItemStack.Of(ItemType.CopperBroadsword);
            inv.Selected = 1;
            yield return new WaitForSeconds(0.3f);
            drops = 0;
            int uses = 0, swordNumbers0 = UI.CombatText.DevEnemyNumbers;
            var covered = new List<string>();
            MeleeHits.DebugLog = line => covered.Add(line);
            for (int k = 0; k < 2; k++)
            {
                hero.transform.position = Clear(hero, new Vector2(tb.min.x - 0.7f, tb.center.y));
                hero.FaceRight();
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.2f);
                for (int w = 0; w < 30 && !(mod.DevReady && !mod.IsUsingItem); w++) yield return null;
                mod.DevUseSelected((Vector2)tb.center);
                uses++;
                yield return Watch(0.7f, null);
            }
            MeleeHits.DebugLog = null;
            int swordNumbers = UI.CombatText.DevEnemyNumbers - swordNumbers0;
            V301Check("剑一刀一下", uses > 0 && swordNumbers == uses,
                $"铜阔剑挥了 {uses} 次，伤害数字 {swordNumbers} 个，血掉了 {drops} 次；挥砍覆盖：{string.Join("；", covered.Take(6))}");
            inv.Slots[0] = default;
            inv.Slots[1] = default;
        }

        // ------------------------------------------------------------------ thrown things left lying, then the title

        IEnumerator SpikyMenuTest()
        {
            var pd = PlayerData.instance;
            if (GameManager.instance.sceneName != "Crossroads_01")
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                pd.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
            }
            pd.isInvincible = true;
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            inv.Slots[2] = ItemStack.Of(ItemType.SpikyBall, 99);
            inv.Selected = 2;
            inv.Override = -1;
            Vector2 at = hero.transform.position;
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(at + new Vector2(Random.Range(-4f, 4f), 2f));
                yield return null;
            }
            int lying = TerraProjectiles.Instance.Count;
            int clears0 = TerraProjectiles.DevMenuClears;
            var gm = GameManager.instance;
            float wait = Time.realtimeSinceStartup + 20f;
            while ((pd.disablePause || gm.gameState != GlobalEnums.GameState.PLAYING) && Time.realtimeSinceStartup < wait) yield return null;
            yield return Tap(UnityEngine.InputSystem.Key.Escape);
            wait = Time.realtimeSinceStartup + 5f;
            while (gm.gameState != GlobalEnums.GameState.PAUSED && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            UIManager.instance.UIReturnToMainMenu();
            InventoryGui.Open = false;
            wait = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < wait && ((gm = GameManager.UnsafeInstance) == null || gm.sceneName != "Menu_Title" || UIManager.instance == null || UIManager.instance.menuState != GlobalEnums.MainMenuState.MAIN_MENU))
                yield return null;
            yield return new WaitForSecondsRealtime(1f);
            var frames = new List<float>();
            for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime);
            }
            int left = TerraProjectiles.Instance != null ? TerraProjectiles.Instance.Count : -1;
            V301Check("扔下的尖球回主菜单后清掉", lying >= 3 && left == 0 && TerraProjectiles.DevMenuClears > clears0,
                $"扔下 {lying} 个，回到 {gm?.sceneName} 后还剩 {left} 个；菜单里 {FrameStats(frames)}");
        }

        // ------------------------------------------------------------------ a reforged accessory put on and off

        IEnumerator PrefixKeepTest()
        {
            yield return null;
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            int k = -1;
            for (int i = 0; i < Inventory.OpenAccessories && k < 0; i++) if (inv.Accessories[i].IsEmpty) k = i;
            if (k < 0) { k = Inventory.OpenAccessories - 1; inv.Accessories[k] = default; }
            const byte menacing = 72;
            var acc = ItemStack.Of(ItemType.AnkletoftheWind);
            acc.Prefix = menacing;
            acc.Forge = 2;
            inv.Mouse = acc;
            InventoryGui.Open = true;
            var steps = new System.Text.StringBuilder();
            bool ok = true;
            void Note(string what, ItemStack st)
            {
                steps.Append($"{what}：词条 {st.Prefix} 锻造 {st.Forge}；");
                if (st.IsEmpty || st.Prefix != menacing || st.Forge != 2) ok = false;
            }
            for (int round = 0; round < 3; round++)
            {
                // Onto the empty slot from the cursor.
                InventoryGui.DevLeftClickEquip(false, k);
                Note("放上", inv.Accessories[k]);
                // Shift: off into the bag.
                InventoryGui.DevShift = true;
                InventoryGui.DevLeftClickEquip(false, k);
                InventoryGui.DevShift = false;
                int at = System.Array.FindIndex(inv.Slots, x => !x.IsEmpty && x.Type == ItemType.AnkletoftheWind);
                Note("Shift 卸下", at >= 0 ? inv.Slots[at] : default);
                if (at < 0) break;
                // Right click in the bag: equipped (SwapEquip).
                InventoryGui.DevRightClick(at);
                int worn = System.Array.FindIndex(inv.Accessories, x => !x.IsEmpty && x.Type == ItemType.AnkletoftheWind);
                Note("右键装备", worn >= 0 ? inv.Accessories[worn] : default);
                if (worn < 0) break;
                // Picked up with a click, ready for the next round.
                InventoryGui.DevLeftClickEquip(false, worn);
                k = worn;
                yield return null;
            }
            // The trash and back with Shift.
            inv.Trash = inv.Mouse;
            inv.Mouse = default;
            InventoryGui.DevShift = true;
            InventoryGui.DevClickTrash();
            InventoryGui.DevShift = false;
            int back = System.Array.FindIndex(inv.Slots, x => !x.IsEmpty && x.Type == ItemType.AnkletoftheWind);
            Note("垃圾桶 Shift 取回", back >= 0 ? inv.Slots[back] : default);
            if (back >= 0) inv.Slots[back] = default;
            InventoryGui.Open = false;
            inv.Infinite = infinite;
            V301Check("反复装备饰品，重铸词条和锻造等级都在", ok, steps.ToString());
        }

        // ------------------------------------------------------------------ the crafting stations and where they come from

        /// <summary>
        /// Dev (suite "stationaudit"): every station the recipes ask for, the furniture that counts as it, and where that
        /// furniture comes from (a recipe and its own station, the townsfolk's shops, enemies' drops and from which stage).
        /// </summary>
        IEnumerator StationAudit()
        {
            yield return null;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            var specs = (System.Collections.IDictionary)typeof(Stations).GetField("specs", flags).GetValue(null);
            var furniture = new Dictionary<string, List<ItemType>>();
            foreach (System.Collections.DictionaryEntry kv in specs)
            {
                var spec = kv.Value;
                var item = (ItemType)spec.GetType().GetField("Item", flags).GetValue(spec);
                var counts = (string[])spec.GetType().GetField("Counts", flags).GetValue(spec);
                if (counts == null) continue;
                foreach (var c in counts)
                {
                    if (!furniture.TryGetValue(c, out var list)) furniture[c] = list = new List<ItemType>();
                    if (!list.Contains(item)) list.Add(item);
                }
            }
            var sold = new Dictionary<ItemType, string>();
            foreach (var name in new[] { "roster", "more" })
            {
                var npcs = (Town.NpcDef[])typeof(Town.TownNpcs).GetField(name, flags).GetValue(null);
                foreach (var npc in npcs)
                    if (npc.Items != null)
                        foreach (var st in npc.Items)
                            if (!sold.ContainsKey(st.Type)) sold[st.Type] = $"{npc.Zh}（阶段 {Mathf.Max(st.Stage, npc.Stage)}）";
            }
            var dropped = new Dictionary<ItemType, (int stage, string record, float chance)>();
            foreach (var record in EnemyDrops.DevRecords)
                for (int stage = 0; stage <= 5; stage++)
                    foreach (var d in EnemyDrops.At(record, stage, true, stage >= 4, stage >= 5))
                        if (!dropped.TryGetValue(d.Type, out var was) || stage < was.stage || (stage == was.stage && d.Chance > was.chance))
                            dropped[d.Type] = (stage, record, d.Chance);
            var needed = new Dictionary<string, int>();
            int minStage(string st) => needed.TryGetValue(st, out int v) ? v : 99;
            foreach (var r in Recipes.All)
            {
                if (r.IsBought || string.IsNullOrEmpty(r.Station) || r.Station == "徒手") continue;
                int st = Mathf.Max(r.StationStage, ItemDb.Get(r.Result).Stage);
                needed[r.Station] = Mathf.Min(minStage(r.Station), st);
            }
            int missing = 0;
            foreach (var kv in needed.OrderBy(x => x.Value))
            {
                var items = furniture.TryGetValue(kv.Key, out var l) ? l : new List<ItemType>();
                var parts = new List<string>();
                bool any = false;
                foreach (var t in items)
                {
                    var rec = Recipes.All.FirstOrDefault(x => x.Result == t && !x.IsBought);
                    var src = new List<string>();
                    if (rec != null) src.Add($"合成@{(string.IsNullOrEmpty(rec.Station) ? "徒手" : rec.Station)}(阶段 {rec.StationStage})");
                    if (Shop.Basics.Any(b => b.Type == t)) src.Add("长椅买");
                    if (sold.TryGetValue(t, out var who)) src.Add("商店 " + who);
                    if (dropped.TryGetValue(t, out var dr)) src.Add($"掉落 {dr.record} 阶段 {dr.stage} 概率 {dr.chance:P1}");
                    if (src.Count > 0) any = true;
                    parts.Add($"{ItemDb.Get(t).Name}[{t}]（物品阶段 {ItemDb.Get(t).Stage}）：{(src.Count == 0 ? "无来源" : string.Join("，", src))}");
                }
                if (!any) missing++;
                Plugin.Log.LogInfo($"[AutoTest] 制作站 {(any ? "有" : "缺")} {kv.Key}（配方最早阶段 {kv.Value}，{(items.Count == 0 ? "没有对应家具" : string.Join("；", parts))}）");
            }
            Plugin.Log.LogInfo($"[AutoTest] 制作站 合计 {needed.Count} 种，缺来源 {missing} 种");
        }

        // ------------------------------------------------------------------ made at its own station only

        IEnumerator CraftRuleTest()
        {
            yield return GoTo("Town", EntryGateOf("Town"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var inv = mod.Inventory;
            var hero = HeroController.instance;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            int stage = Shop.DevStage;
            Shop.DevStage = 0;
            for (int i = 10; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[20] = ItemStack.Of(ItemType.Wood, 99);
            inv.Slots[21] = ItemStack.Of(ItemType.IronBar, 20);
            inv.Slots[22] = ItemStack.Of(ItemType.StoneBlock, 50);
            inv.Slots[23] = ItemStack.Of(ItemType.Torch, 10);
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var feet = new Vector2(b.center.x, b.min.y);
            // Stations already standing here (a test before) would answer: none within reach to begin with.
            var near0 = Stations.NearNames(hero);
            var offered = InventoryGui.DevOffered();
            bool inTownNoStation = offered.Contains(ItemType.WorkBench) && !offered.Contains(ItemType.WoodHelmet) && !offered.Contains(ItemType.IronAnvil) && !offered.Contains(ItemType.Furnace);
            bool basics = Shop.Basics.Count == 0 || offered.Any(t => Shop.Basics.Any(d => d.Type == t));
            V301Check("德特茅斯不再算所有制作站", near0.Count > 0 || inTownNoStation,
                $"附近已有制作站 {string.Join("、", near0)}；能做工作台 {offered.Contains(ItemType.WorkBench)}，木头盔 {offered.Contains(ItemType.WoodHelmet)}，铁砧 {offered.Contains(ItemType.IronAnvil)}，熔炉 {offered.Contains(ItemType.Furnace)}；长椅/村庄买的基础物品还在 {basics}");
            bool placed = Stations.TryPlace(StationKind.WorkBench, hero, feet + new Vector2(1.2f, 0.3f)) || Stations.TryPlace(StationKind.WorkBench, hero, feet + new Vector2(-1.2f, 0.3f));
            yield return null;
            offered = InventoryGui.DevOffered();
            V301Check("放下工作台后能做工作台的配方", placed && offered.Contains(ItemType.WoodHelmet) && offered.Contains(ItemType.IronAnvil) && offered.Contains(ItemType.Furnace),
                $"放下 {placed}，附近 {string.Join("、", Stations.NearNames(hero))}；木头盔 {offered.Contains(ItemType.WoodHelmet)}，铁砧 {offered.Contains(ItemType.IronAnvil)}，熔炉 {offered.Contains(ItemType.Furnace)}");

            // The altars beside Dirtmouth's bench, reached from the start.
            for (float t = 0f; t < 3f && FixedStations.DevDemon == default; t += Time.deltaTime) yield return null;
            var benchFsm = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).FirstOrDefault(f => f.FsmName == "Bench Control" && f.gameObject.scene.name == "Town");
            Vector2 benchAt = benchFsm != null ? (Vector2)benchFsm.transform.position : Vector2.zero;
            var demon = FixedStations.DevDemon;
            bool nearAltar = false;
            if (demon != default)
            {
                hero.transform.position = Clear(hero, demon + new Vector2(-1.5f, 1f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                nearAltar = Stations.Near("恶魔祭坛 / 猩红祭坛", hero);
                yield return CloseUpAt("Town", "v301_altars", demon + new Vector2(0f, 1f), 520, 2);
            }
            V301Check("祭坛在德特茅斯长椅旁", demon != default && benchFsm != null && Vector2.Distance(demon, benchAt) < 25f && Vector2.Distance(FixedStations.DevCrimson, benchAt) < 25f && nearAltar,
                $"长椅 {benchAt:F1}，恶魔祭坛 {demon:F1}，猩红祭坛 {FixedStations.DevCrimson:F1}，站在旁边算祭坛 {nearAltar}");
            Shop.DevStage = stage;
            inv.Infinite = infinite;
        }

        /// <summary>
        /// Dev (suite "sourceaudit"): every item a recipe asks for, and every station's furniture: where it comes from (a
        /// recipe, the townsfolk's shops, a bench's basics, enemies' drops at some stage, a catch, a crate); those with none.
        /// </summary>
        IEnumerator SourceAudit()
        {
            yield return null;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            var src = new Dictionary<ItemType, string>();
            void Add(ItemType t, string how) { if (!src.ContainsKey(t)) src[t] = how; }
            foreach (var r in Recipes.All) if (!r.IsBought) Add(r.Result, "合成");
            foreach (var d in Shop.Basics) Add(d.Type, "长椅买");
            foreach (var name in new[] { "roster", "more" })
                foreach (var npc in (Town.NpcDef[])typeof(Town.TownNpcs).GetField(name, flags).GetValue(null))
                    if (npc.Items != null) foreach (var st in npc.Items) Add(st.Type, "商店 " + npc.Zh);
            foreach (var (t, _) in ((Items.ItemType Type, int Stage)[])typeof(Town.TownNpcs).GetField("travelPool", flags).GetValue(null)) Add(t, "旅商");
            foreach (var t in Town.Angler.DevRewardSources()) Add(t, "渔夫任务");
            foreach (var record in EnemyDrops.DevRecords)
                for (int stage = 0; stage <= 5; stage++)
                    foreach (bool first in new[] { true, false })
                        foreach (var d in EnemyDrops.At(record, stage, first, stage >= 4, stage >= 5)) Add(d.Type, $"掉落 {record}@{stage}");
            foreach (var rule in (Rule[])typeof(Fishing).GetField("rules", flags).GetValue(null))
                if (rule.Items != null) foreach (var t in rule.Items) Add(t, "钓鱼");
            foreach (System.Collections.DictionaryEntry kv in (System.Collections.IDictionary)typeof(Fishing).GetField("crates", flags).GetValue(null))
            {
                var c = (CrateDef)kv.Value;
                if (c.Special != ItemType.None) Add(c.Special, "宝匣 " + kv.Key);
                if (c.Pool != null) foreach (var t in c.Pool) Add(t, "宝匣 " + kv.Key);
            }
            var wanted = new Dictionary<ItemType, string>();
            foreach (var r in Recipes.All)
            {
                if (r.IsBought) continue;
                foreach (var (t, _) in r.Ingredients) if (!wanted.ContainsKey(t)) wanted[t] = ItemDb.Get(r.Result).Name;
            }
            var specs = (System.Collections.IDictionary)typeof(Stations).GetField("specs", flags).GetValue(null);
            foreach (System.Collections.DictionaryEntry kv in specs)
            {
                var item = (ItemType)kv.Value.GetType().GetField("Item", flags).GetValue(kv.Value);
                if (item != ItemType.None && !wanted.ContainsKey(item)) wanted[item] = "（制作站）";
            }
            int missing = 0;
            foreach (var kv in wanted.OrderBy(x => ItemDb.Get(x.Key).Stage))
            {
                if (src.ContainsKey(kv.Key)) continue;
                missing++;
                var def = ItemDb.Get(kv.Key);
                Plugin.Log.LogInfo($"[AutoTest] 来源 缺 {def.Name}[{kv.Key}]（物品阶段 {def.Stage}，来源标记 {def.Source}）：用于 {kv.Value}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 来源 合计 {wanted.Count} 种材料和制作站，缺来源 {missing} 种");
            // Where a few key things drop, and how likely (the share of their group's roll, times how many).
            foreach (var t in new[] { ItemType.BrokenHeroSword, ItemType.Hellforge, ItemType.AlchemyTable, ItemType.BoneWelder, ItemType.Solidifier, ItemType.LivingLoom, ItemType.CrystalBall, ItemType.Autohammer })
            {
                var lines = new List<string>();
                foreach (var record in EnemyDrops.DevRecords)
                    for (int stage = 0; stage <= 5; stage++)
                    {
                        var drops = EnemyDrops.At(record, stage, false, stage >= 4, stage >= 5);
                        foreach (var d in drops)
                            if (d.Type == t)
                            {
                                float each = EnemyDrops.MarginalChance(drops, d) * (d.MeanCount > 0 ? d.MeanCount : (d.Min + d.Max) * 0.5f);
                                lines.Add($"{record}@{stage} 每次 {each:P2}");
                                stage = 6;
                                break;
                            }
                    }
                Plugin.Log.LogInfo($"[AutoTest] 来源 {ItemDb.Get(t).Name}：{(lines.Count == 0 ? "没有掉落" : string.Join("，", lines.Take(12)))}{(lines.Count > 12 ? $" 等 {lines.Count} 处" : "")}");
            }
        }

        // ------------------------------------------------------------------ tombstones and the graveyard

        IEnumerator TombstoneTest()
        {
            var pd = PlayerData.instance;
            yield return GoTo("Town", EntryGateOf("Town"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            int thrown0 = Tombstones.DevThrown, stood0 = Tombstones.DevStood, items0 = Tombstones.DevItems;

            // 1. A death: one thrown off the body, tumbling till it stands.
            mod.Stats.FullHeal();
            pd.isInvincible = false;
            hero.cState.invulnerable = false;
            hero.TakeDamage(null, GlobalEnums.CollisionSide.right, 99, 1);
            float wait = Time.realtimeSinceStartup + 20f;
            while (!hero.cState.dead && Time.realtimeSinceStartup < wait) yield return null;
            Vector2 diedAt = hero.transform.position;
            yield return new WaitForSeconds(0.6f);
            Shot("Town", "tomb_1_tumbling");
            while (Tombstones.DevStood == stood0 && Tombstones.DevItems == items0 && Time.realtimeSinceStartup < wait) yield return null;
            V301Check("死亡时抛出墓碑并立在地上", hero.cState.dead && Tombstones.DevThrown > thrown0 && (Tombstones.DevStood > stood0 || Tombstones.DevItems > items0),
                $"死在 {diedAt:F1}，抛出 {Tombstones.DevThrown - thrown0}，{Tombstones.DevLast}");
            // Back in play.
            wait = Time.realtimeSinceStartup + 60f;
            while ((hero.cState.dead || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.realtimeSinceStartup < wait) yield return null;
            yield return new WaitForSeconds(1f);
            if (GameManager.instance.sceneName != "Town") yield return GoTo("Town", EntryGateOf("Town"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            hero = HeroController.instance;

            // 2. Seven about: a graveyard (dimmer, its recipes made).
            Vector2 c = hero.transform.position;
            for (int k = 0; k < 14 && Stations.Standing(Stations.IsGrave).Count < 8; k++)
            {
                int before = Tombstones.DevStood + Tombstones.DevItems;
                Tombstones.Throw(k % 11, c + new Vector2(-6f + k * 1.6f, 2f), Vector2.zero);
                float w = Time.realtimeSinceStartup + 12f;
                while (Tombstones.DevStood + Tombstones.DevItems == before && Time.realtimeSinceStartup < w) yield return null;
            }
            yield return new WaitForSeconds(2.5f);
            int graves = Tombstones.DevGraves;
            V301Check("七块墓碑形成墓地", Tombstones.InGraveyard && CraftEnvironments.Available(CraftEnvironment.Graveyard),
                $"附近墓碑 {graves} 块，墓地 {Tombstones.InGraveyard}，阴暗 {Tombstones.Gloom:F2}，墓地配方环境 {CraftEnvironments.Available(CraftEnvironment.Graveyard)}；{Stations.DevDescribe()}");
            Shot("Town", "tomb_2_graveyard");
            yield return null;

            // 3. Abigail's Flower by them, picked with a right click.
            bool grew = Tombstones.DevGrowNow();
            yield return null;
            var flower = Stations.Standing(k => k == StationKind.AbigailsFlower).Select(x => x.Box).FirstOrDefault();
            int had = Recipes.Count(inv, ItemType.AbigailsFlower), picked0 = Stations.DevFlowersPicked;
            if (grew && flower.width > 0f)
            {
                // Beside it on the scene's side (one by the left edge of Dirtmouth has no room to its left).
                hero.transform.position = Clear(hero, flower.center + new Vector2(flower.center.x < 3f ? 1f : -1f, 0.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                Shot("Town", "tomb_3_flower");
                yield return null;
                Stations.TryRightClick(flower.center);
                yield return null;
            }
            V301Check("墓碑旁长出阿比盖尔的花，右键摘下", grew && Stations.DevFlowersPicked > picked0 && Recipes.Count(inv, ItemType.AbigailsFlower) > had,
                $"长出 {grew} 在 {flower.center:F1}，摘下 {Stations.DevFlowersPicked - picked0}，背包里 {had}→{Recipes.Count(inv, ItemType.AbigailsFlower)}");

            // 4. Nothing to stand on: an item after 10 s.
            items0 = Tombstones.DevItems;
            Tombstones.Throw(0, c + new Vector2(-400f, 0f), Vector2.zero);
            wait = Time.realtimeSinceStartup + 14f;
            while (Tombstones.DevItems == items0 && Time.realtimeSinceStartup < wait) yield return null;
            V301Check("没地方立的墓碑变成掉落物", Tombstones.DevItems > items0, Tombstones.DevLast);
        }

        // ------------------------------------------------------------------ probes: the super dash and the shade gates

        IEnumerator AbilityProbe()
        {
            yield return null;
            var hero = HeroController.instance;
            foreach (var f in hero.GetComponentsInChildren<PlayMakerFSM>(true))
                if (f.FsmName.IndexOf("dash", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 能力 主角状态机 {PathOf(f.transform)} '{f.FsmName}'");
                    DumpFsm(f);
                }
            var ih = GameManager.instance.inputHandler;
            Plugin.Log.LogInfo($"[AutoTest] 能力 超冲按键 {ih.inputActions.superDash.Name}：{string.Join(",", ih.inputActions.superDash.Bindings.Select(b => b.Name))}");
            foreach (var scene in new[] { "Fungus3_44", "Deepnest_East_18" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
                foreach (var g in Object.FindObjectsByType<ShadowGateColliderControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var c = g.disableCollider;
                    Plugin.Log.LogInfo($"[AutoTest] 能力 暗影之门 {scene} {PathOf(g.transform)}：碰撞体 {(c == null ? "无" : $"{PathOf(c.transform)} 层 {c.gameObject.layer} 触发 {c.isTrigger} 启用 {c.enabled} 范围 {c.bounds}")}，网格实心 {(c != null && World.TerraWorld.IsSolidAt(c.bounds.center))}");
                    foreach (var comp in g.GetComponentsInChildren<Component>(true))
                        if (!(comp is Transform)) Plugin.Log.LogInfo($"[AutoTest] 能力   部件 {PathOf(comp.transform)} {comp.GetType().Name}{(comp is Collider2D cc ? $" 层 {cc.gameObject.layer} 触发 {cc.isTrigger} 启用 {cc.enabled} {cc.bounds}" : "")}{(comp is DamageHero dh ? $" 伤害 {dh.damageDealt} 类型 {dh.hazardType} 暗影冲刺免疫 {dh.shadowDashHazard}" : "")}");
                    DumpFsms(g.transform);
                }
            }
        }

        // ------------------------------------------------------------------ the Shade Cloak and the Crystal Heart

        IEnumerator DashesTest()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            yield return GoTo("Town", EntryGateOf("Town"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            // 1. Given into the bag with the abilities; reforged as accessories.
            pd.hasShadowDash = true;
            pd.hasSuperDash = true;
            yield return new WaitForSeconds(2.2f);
            bool gotCloak = inv.Owns(ItemType.ShadeCloak), gotHeart = inv.Owns(ItemType.CrystalHeart);
            var cloakTex = Terraria.TerrariaAssets.Texture(ItemDb.Get(ItemType.ShadeCloak).Texture);
            var shieldTex = Terraria.TerrariaAssets.Texture(ItemDb.Get(ItemType.HallownestShield).Texture);
            V301Check("拿到暗影披风、水晶之心的能力就给进背包，三件都能重铸", gotCloak && gotHeart && Prefixes.CanHave(ItemDb.Get(ItemType.ShadeCloak))
                    && Prefixes.CanHave(ItemDb.Get(ItemType.CrystalHeart)) && Prefixes.CanHave(ItemDb.Get(ItemType.HallownestShield)) && shieldTex != null && shieldTex.width == 36,
                $"暗影披风 {gotCloak}，水晶之心 {gotHeart}，图标 {cloakTex?.name}，圣巢之盾图标 {shieldTex?.name} {shieldTex?.width}x{shieldTex?.height}");
            for (int i = 0; i < inv.Slots.Length; i++)
                if (!inv.Slots[i].IsEmpty && (inv.Slots[i].Type == ItemType.ShadeCloak || inv.Slots[i].Type == ItemType.CrystalHeart)) inv.Slots[i] = default;
            void Wear(params ItemType[] items)
            {
                for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
                for (int k = 0; k < items.Length; k++) inv.Accessories[k] = ItemStack.Of(items[k]);
                inv.RefreshEffective();
            }
            var hero = HeroController.instance;
            IEnumerator InControl()
            {
                for (float t = 0f; t < 6f && (!GameHooks.PlayerHasControl() || hero.cState.superDashing || Player.TerraMovement.IsDashing); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);
            }

            // 2. The Hallownest Shield made at a Tinkerer's Workshop from the three.
            int stage = Shop.DevStage;
            Shop.DevStage = 5;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            var hb = hero.GetComponent<BoxCollider2D>().bounds;
            bool workshop = Stations.TryPlace(StationKind.TinkerersWorkshop, hero, new Vector2(hb.center.x + 1.5f, hb.min.y + 0.3f)) || Stations.TryPlace(StationKind.TinkerersWorkshop, hero, new Vector2(hb.center.x - 1.5f, hb.min.y + 0.3f));
            inv.Slots[30] = ItemStack.Of(ItemType.ShieldOfCthulhu);
            inv.Slots[31] = ItemStack.Of(ItemType.ShadeCloak);
            inv.Slots[32] = ItemStack.Of(ItemType.CrystalHeart);
            yield return null;
            bool offered = InventoryGui.DevOffered().Contains(ItemType.HallownestShield);
            inv.Mouse = default;
            bool made = InventoryGui.DevCraft(ItemType.HallownestShield);
            bool used = inv.Slots[30].IsEmpty && inv.Slots[31].IsEmpty && inv.Slots[32].IsEmpty;
            V301Check("工匠作坊旁用三件合成圣巢之盾", workshop && offered && made && used && inv.Mouse.Type == ItemType.HallownestShield,
                $"放下工匠作坊 {workshop}（{Stations.DevWhy}），列表里有 {offered}，做出 {made}，三件用掉 {used}，鼠标上 {inv.Mouse.Def?.Name}");
            inv.Mouse = default;
            Shop.DevStage = stage;
            inv.Infinite = infinite;

            // 3. The Crystal Heart (in the shield): a long press charges and goes, then 20 s; a short press dashes.
            Wear(ItemType.HallownestShield);
            AbilityAccessories.DevReset();
            yield return InControl();
            float x0 = hero.transform.position.x;
            int supers0 = AbilityAccessories.DevSuperDashes, dashes0 = Player.TerraMovement.DevDashes;
            AbilityAccessories.DevDashKey = true;
            yield return new WaitForSeconds(1.1f);
            yield return CloseUpAt("Town", "fx_crystal_charge", (Vector2)hero.transform.position, 260, 2);
            yield return new WaitForSeconds(0.2f);
            string chargeState = HeroFsm(hero, "Superdash")?.ActiveStateName;
            AbilityAccessories.DevDashKey = false;
            float maxSpeed = 0f;
            bool shotFly = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                maxSpeed = Mathf.Max(maxSpeed, Mathf.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.x));
                if (!shotFly && t > 0.25f)
                {
                    shotFly = true;
                    Shot("Town", "fx_super_dash");
                }
                yield return null;
            }
            var after = new System.Text.StringBuilder();
            for (float t = 0f; t < 5f; t += 0.2f)
            {
                var sf = HeroFsm(hero, "Superdash");
                after.Append($" [{t:F1} x{hero.transform.position.x:F1} {sf?.ActiveStateName} 超冲{(hero.cState.superDashing ? 1 : 0)} 输入{(hero.acceptingInput ? 1 : 0)} 放手{(hero.controlReqlinquished ? 1 : 0)} 速{hero.GetComponent<Rigidbody2D>().linearVelocity.x:F1} 动画 {hero.GetComponent<tk2dSpriteAnimator>()?.CurrentClip?.name}]");
                yield return new WaitForSeconds(0.2f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 3.0.1 超级冲刺之后：{after}");
            V301Check("长按冲刺键：水晶之心蓄力后超级冲刺", AbilityAccessories.DevSuperDashes > supers0 && maxSpeed > 15f && Player.TerraMovement.DevDashes == dashes0,
                $"松手时 {chargeState}，超级冲刺 {AbilityAccessories.DevSuperDashes - supers0} 次，最快 {maxSpeed:F1}，走了 {hero.transform.position.x - x0:F1}，普通冲刺 {Player.TerraMovement.DevDashes - dashes0} 次，冷却剩 {AbilityAccessories.SuperCooldownLeft:F1} 秒");
            yield return InControl();
            AbilityAccessories.DevReset();
            dashes0 = Player.TerraMovement.DevDashes;
            supers0 = AbilityAccessories.DevSuperDashes;
            AbilityAccessories.DevDashKey = true;
            yield return new WaitForSeconds(0.1f);
            AbilityAccessories.DevDashKey = false;
            yield return new WaitForSeconds(0.5f);
            V301Check("点按冲刺键：克苏鲁护盾冲刺", Player.TerraMovement.DevDashes > dashes0 && AbilityAccessories.DevSuperDashes == supers0,
                $"普通冲刺 {Player.TerraMovement.DevDashes - dashes0} 次，超级冲刺 {AbilityAccessories.DevSuperDashes - supers0} 次；{AbilityAccessories.DevPress}");
            AbilityAccessories.DevDashKey = null;

            // 4. The shadow dash through an enemy: by its key, then while charging by a double tap; 10 s between.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            hero = HeroController.instance;
            HealthManager enemy = null;
            for (float w = 0f; w < 6f && enemy == null; w += 0.25f)
            {
                // One walking on the ground first (a wall crawler's wall would stop the dash), else any.
                foreach (bool groundOnly in new[] { true, false })
                {
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (!hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == "Crossroads_01" && hm.GetComponent<Collider2D>() != null && EnemyStats.Of(hm) is EnemyStats.Info ei && !ei.Boss
                            && (!groundOnly || EnemyRespawn.Instance.DevBody(hm, out _, out _, out _, out bool walker) && walker && !hm.name.StartsWith("Climber"))) { enemy = hm; break; }
                    if (enemy != null) break;
                }
                if (enemy == null) yield return new WaitForSeconds(0.25f);
            }
            if (enemy == null) { V301Check("暗影冲刺穿过敌人", false, "找不到怪"); yield break; }
            enemy.hp = Mathf.Max(enemy.hp, 5000);
            var erb = enemy.GetComponent<Rigidbody2D>();
            if (erb != null) { erb.linearVelocity = Vector2.zero; erb.bodyType = RigidbodyType2D.Kinematic; }
            var eb = enemy.GetComponent<Collider2D>().bounds;
            Vector3 stand = enemy.transform.position;
            int lifeBefore = 0;
            IEnumerator Toward(bool doubleTap)
            {
                AbilityAccessories.DevReset();
                hero.transform.position = Clear(hero, new Vector2(eb.min.x - 3f, eb.center.y));
                hero.FaceRight();
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return InControl();
                mod.Stats.FullHeal();
                lifeBefore = mod.Stats.Life;
                pd.isInvincible = false;
                hero.cState.invulnerable = false;
                if (doubleTap)
                {
                    // Dash held (the heart charging), then right tapped twice.
                    AbilityAccessories.DevDashKey = true;
                    yield return new WaitForSeconds(0.5f);
                    AbilityAccessories.DevTap = 1;
                    yield return null;
                    yield return null;
                    AbilityAccessories.DevTap = 1;
                    yield return null;
                    AbilityAccessories.DevDashKey = false;
                }
                else AbilityAccessories.DevShadowKey = true;
                var tr = new System.Text.StringBuilder();
                bool shotShade = false;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    if (!shotShade && AbilityAccessories.ShadowDashing && t > 0.12f)
                    {
                        shotShade = true;
                        Shot("Crossroads_01", doubleTap ? "fx_shadow_doubletap" : "fx_shadow_key");
                    }
                    enemy.transform.position = stand;
                    if (t > 0.2f && Time.frameCount % 4 == 0) tr.Append($" [{t:F2} x{hero.transform.position.x - eb.min.x:F2} y{hero.transform.position.y - eb.min.y:F2} v{hero.GetComponent<Rigidbody2D>().linearVelocity.x:F1},{hero.GetComponent<Rigidbody2D>().linearVelocity.y:F1} 冲{(Player.TerraMovement.IsDashing ? 1 : 0)} 影{(AbilityAccessories.ShadowDashing ? 1 : 0)} 输入{(hero.acceptingInput ? 1 : 0)} 无敌{(hero.cState.invulnerable ? 1 : 0)} 后退{(hero.cState.recoiling ? 1 : 0)} 怪{enemy.transform.position.x - stand.x:F2}]");
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 3.0.1 冲刺轨迹（{(doubleTap ? "双击" : "按键")}，怪宽 {eb.size.x:F2} 高 {eb.size.y:F2}，主角高 {hero.GetComponent<BoxCollider2D>().bounds.size.y:F2}）：{tr}");
                pd.isInvincible = true;
            }
            Wear(ItemType.ShieldOfCthulhu, ItemType.ShadeCloak);
            int life0 = mod.Stats.Life, shadows0 = AbilityAccessories.DevShadowDashes;
            yield return Toward(false);
            float passed = hero.transform.position.x - eb.max.x;
            V301Check("暗影冲刺键：穿过敌人，不受伤、不被弹回，原版冷却 1.5 秒", AbilityAccessories.DevShadowDashes > shadows0 && passed > 0f && mod.Stats.Life == lifeBefore && AbilityAccessories.ShadowCooldownLeft > 0f && AbilityAccessories.ShadowCooldownLeft <= 1.5f,
                $"{enemy.name}：暗影冲刺 {AbilityAccessories.DevShadowDashes - shadows0} 次，冲过怪的右边 {passed:F2}，掉血 {lifeBefore - mod.Stats.Life}，冷却剩 {AbilityAccessories.ShadowCooldownLeft:F1} 秒");
            // Again at once: on cooldown, nothing.
            shadows0 = AbilityAccessories.DevShadowDashes;
            AbilityAccessories.DevShadowKey = true;
            yield return new WaitForSeconds(0.3f);
            AbilityAccessories.DevShadowKey = false;
            bool refused = AbilityAccessories.DevShadowDashes == shadows0;
            Wear(ItemType.HallownestShield);
            mod.Stats.FullHeal();
            life0 = mod.Stats.Life;
            shadows0 = AbilityAccessories.DevShadowDashes;
            yield return Toward(true);
            passed = hero.transform.position.x - eb.max.x;
            V301Check("圣巢之盾：按住冲刺键蓄力时双击方向键打出暗影冲刺；冷却中按键无效", refused && AbilityAccessories.DevShadowDashes > shadows0 && passed > 0f && mod.Stats.Life == lifeBefore,
                $"冷却中没冲 {refused}；双击后暗影冲刺 {AbilityAccessories.DevShadowDashes - shadows0} 次，冲过怪的右边 {passed:F2}，掉血 {lifeBefore - mod.Stats.Life}，{AbilityAccessories.DevPress}，超级冲刺状态机 {HeroFsm(hero, "Superdash")?.ActiveStateName}");
            AbilityAccessories.DevDashKey = null;

            // 5. The shade gate: a plain dash stops at it, a shadow dash goes through, and it shuts after.
            yield return GoTo("Fungus3_44", EntryGateOf("Fungus3_44"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            hero = HeroController.instance;
            var gate = Object.FindObjectsByType<ShadowGateColliderControl>(FindObjectsSortMode.None).FirstOrDefault();
            if (gate == null) { V301Check("暗影之门", false, "找不到门"); yield break; }
            var gb = gate.disableCollider.bounds;
            IEnumerator DashGate(bool shadow)
            {
                Wear(ItemType.HallownestShield);
                AbilityAccessories.DevReset();
                hero.transform.position = Clear(hero, new Vector2(gb.center.x - 2.5f, gb.min.y + 1f));
                hero.FaceRight();
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return InControl();
                if (shadow) AbilityAccessories.DevShadowKey = true;
                else Player.TerraMovement.RequestDash(1);
                yield return new WaitForSeconds(1f);
            }
            yield return DashGate(false);
            float blocked = hero.transform.position.x - gb.center.x;
            yield return DashGate(true);
            float through = hero.transform.position.x - gb.center.x;
            yield return null;
            bool shut = gate.disableCollider.enabled;
            V301Check("暗影之门：普通冲刺过不去，暗影冲刺冲过去，之后门又关上", blocked < 0f && through > 0f && shut,
                $"门在 x={gb.center.x:F1}；普通冲刺停在门 {(blocked < 0 ? "左" : "右")} {Mathf.Abs(blocked):F2}，暗影冲刺停在门 {(through < 0 ? "左" : "右")} {Mathf.Abs(through):F2}，门又启用 {shut}");
        }

        static PlayMakerFSM HeroFsm(HeroController hero, string name)
        {
            foreach (var f in hero.GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return f;
            return null;
        }

        // ------------------------------------------------------------------ buff furniture carried, the Water Candle

        IEnumerator CarriedFurnitureTest()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            yield return GoTo("Town", EntryGateOf("Town"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            for (int i = 10; i < Inventory.Size; i++) inv.Slots[i] = default;
            yield return new WaitForSeconds(0.5f);
            var before = Equipment.Current;
            var carried = new[] { ItemType.Campfire, ItemType.HeartLantern, ItemType.StarinaBottle, ItemType.CatBast, ItemType.Sunflower, ItemType.GardenGnome };
            for (int k = 0; k < carried.Length; k++) inv.Slots[40 + k] = ItemStack.Of(carried[k]);
            yield return new WaitForSeconds(0.6f);
            var after = Equipment.Current;
            int[] buffs = { Player.PlayerBuffs.CozyFire, Player.PlayerBuffs.HeartLamp, Player.PlayerBuffs.StarInBottle, Player.PlayerBuffs.BastDefense, Player.PlayerBuffs.Happy };
            bool allOn = buffs.All(Player.PlayerBuffs.Has);
            V301Check("增益家具带在身上就有效果", allOn && after.Defense - before.Defense == 5 && after.LifeRegen - before.LifeRegen == 3 && after.ManaRegenBonus - before.ManaRegenBonus == 10
                    && Mathf.Abs(after.MoveSpeed - before.MoveSpeed - 0.21f) < 0.01f && Mathf.Abs(after.Luck - before.Luck - 0.2f) < 0.01f,
                $"增益 {string.Join(" ", buffs.Select(b => b + (Player.PlayerBuffs.Has(b) ? "有" : "无")))}；防御 +{after.Defense - before.Defense}，生命再生 +{after.LifeRegen - before.LifeRegen}，魔力再生 +{after.ManaRegenBonus - before.ManaRegenBonus}，移速 +{after.MoveSpeed - before.MoveSpeed:F2}，运气 +{after.Luck - before.Luck:F2}");
            Shot("Town", "furniture_buffs");
            yield return null;
            for (int k = 0; k < carried.Length; k++) inv.Slots[40 + k] = default;
            yield return new WaitForSeconds(0.8f);
            V301Check("拿走后增益消失", !buffs.Any(Player.PlayerBuffs.Has) && Equipment.Current.Defense == before.Defense, $"还有 {string.Join(" ", buffs.Where(Player.PlayerBuffs.Has))}");

            // Made by hand: the Heart Lantern and the Star in a Bottle.
            inv.Slots[40] = ItemStack.Of(ItemType.LifeCrystal);
            inv.Slots[41] = ItemStack.Of(ItemType.Chain, 4);
            inv.Slots[42] = ItemStack.Of(ItemType.Bottle);
            inv.Slots[43] = ItemStack.Of(ItemType.FallenStar);
            int stage = Shop.DevStage;
            Shop.DevStage = 0;
            var offered = InventoryGui.DevOffered();
            V301Check("红心灯笼、星星瓶徒手合成", offered.Contains(ItemType.HeartLantern) && offered.Contains(ItemType.StarinaBottle),
                $"红心灯笼 {offered.Contains(ItemType.HeartLantern)}，星星瓶 {offered.Contains(ItemType.StarinaBottle)}");
            Shop.DevStage = stage;
            for (int k = 40; k < 44; k++) inv.Slots[k] = default;

            // The Water Candle: the Merchant's at 3000 geo, its setting, its buff in the hotbar.
            var merchant = ProgressionBalance.ShopStock("Merchant", new Town.Stock[0]).Select(x => x.Type).ToList();
            long price = Shop.PriceOf(ItemDb.Get(ItemType.WaterCandle));
            inv.Slots[5] = ItemStack.Of(ItemType.WaterCandle);
            inv.Selected = 5;
            inv.Override = -1;
            CarriedFurniture.CandleLevel = 1;
            yield return new WaitForSeconds(0.5f);
            var levels = new List<int>();
            for (int k = 0; k < 3; k++)
            {
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected((Vector2)HeroController.instance.transform.position + Vector2.right);
                yield return new WaitForSeconds(0.4f);
                levels.Add(CarriedFurniture.CandleLevel);
            }
            V301Check("水蜡烛：商人卖 3000 吉欧，放快捷栏有增益，点击在 1~3 倍间切换", merchant.Contains(ItemType.WaterCandle) && price == 300000 && Player.PlayerBuffs.Has(Player.PlayerBuffs.WaterCandle)
                    && levels.SequenceEqual(new[] { 2, 3, 1 }),
                $"商人有 {merchant.Contains(ItemType.WaterCandle)}，价格 {Shop.Money(price)}，增益 {Player.PlayerBuffs.Has(Player.PlayerBuffs.WaterCandle)}，三次点击后 {string.Join("→", levels)}");

            // The setting times the doubling, on coming into a scene.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            int one = EnemyRespawn.Instance.SlotCount;
            CarriedFurniture.CandleLevel = 3;
            yield return GoTo("Crossroads_02", EntryGateOf("Crossroads_02"));
            yield return new WaitForSeconds(1f);
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            int three = EnemyRespawn.Instance.SlotCount;
            V301Check("水蜡烛 ×3：进房间的敌人是平时（已翻倍）的 3 倍", three >= one * 2 + 1,
                $"×1 时刷新点 {one}，×3 时 {three}（翻倍设置 {EnemyRespawn.Multiplier}）");
            CarriedFurniture.CandleLevel = 1;

            // An enemy the game keeps dead (its PersistentBoolItem): back with the candle, not a boss.
            HealthManager kept = null;
            string keptScene = null, keptName = null;
            foreach (var scene in new[] { "Crossroads_01", "Crossroads_04", "Crossroads_07", "Crossroads_15", "Crossroads_19", "Crossroads_21", "Fungus1_01", "Fungus1_02" })
            {
                if (GameManager.instance.sceneName != scene)
                {
                    yield return GoTo(scene, EntryGateOf(scene));
                    pd.isInvincible = true;
                    yield return new WaitForSeconds(1.5f);
                }
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (!hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == scene && hm.GetComponent<PersistentBoolItem>() != null
                            && EnemyStats.Of(hm) is EnemyStats.Info ei2 && !ei2.Boss) { kept = hm; break; }
                if (kept != null) { keptScene = scene; keptName = PathOf(kept.transform); break; }
            }
            if (kept == null) { V301Check("水蜡烛刷新一次性/长椅才复活的敌人", false, "找不到带持久记录的敌人"); inv.Infinite = infinite; yield break; }
            bool semi = kept.GetComponent<PersistentBoolItem>().persistentBoolData.semiPersistent;
            inv.Slots[5] = default;
            kept.Die(null, AttackTypes.Generic, true);
            yield return new WaitForSeconds(1f);
            string other = keptScene == "Crossroads_01" ? "Crossroads_02" : "Crossroads_01";
            yield return GoTo(other, EntryGateOf(other));
            yield return new WaitForSeconds(1f);
            yield return GoTo(keptScene, EntryGateOf(keptScene));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            bool AliveNow()
            {
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (hm.gameObject.scene.name == keptScene && PathOf(hm.transform) == keptName) return hm.gameObject.activeInHierarchy && !hm.isDead;
                return false;
            }
            bool withoutCandle = AliveNow();
            inv.Slots[5] = ItemStack.Of(ItemType.WaterCandle);
            int revived0 = CarriedFurniture.DevRevived;
            yield return new WaitForSeconds(0.5f);
            yield return GoTo(other, EntryGateOf(other));
            yield return new WaitForSeconds(1f);
            yield return GoTo(keptScene, EntryGateOf(keptScene));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            bool withCandle = AliveNow();
            V301Check("水蜡烛在快捷栏：被记成已死的敌人进房间就回来", !withoutCandle && withCandle && CarriedFurniture.DevRevived > revived0,
                $"{keptScene} {keptName}（{(semi ? "长椅复活" : "一次性")}）：没水蜡烛回来 {withoutCandle}，有水蜡烛回来 {withCandle}，复活 {CarriedFurniture.DevRevived - revived0}");
            inv.Slots[5] = default;
            inv.Infinite = infinite;
        }

        // ------------------------------------------------------------------ taken in hand after the room came in

        IEnumerator HkUseTest()
        {
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            pd.hasKingsBrand = false;
            pd.abyssGateOpened = false;
            pd.gotCharm_36 = false;
            pd.royalCharmState = 3;
            pd.openedBlackEggPath = true;
            yield return null;
            yield return null;
            // Both bags full as the brand and the Kingsoul are got: neither carried on the way in (a weapon in hand).
            var bagFillers = new System.Collections.Generic.List<int>();
            for (int i = 0; i < HkItems.KeyBagSlots; i++) if (inv.HkBag[i].IsEmpty) { inv.HkBag[i] = ItemStack.Of(ItemType.Torch, 1); bagFillers.Add(i); }
            for (int i = 0; i < Inventory.HotbarSize; i++) inv.Slots[i] = ItemStack.Of(ItemType.CopperBroadsword);
            inv.Selected = 0;
            inv.Override = -1;
            pd.hasKingsBrand = true;
            pd.gotCharm_36 = true;
            yield return null;
            bool notCarried = !HkItems.Carried(inv, ItemType.HkKingsBrand) && !HkItems.Carried(inv, ItemType.HkKingsoul);
            yield return GoTo("Abyss_06_Core", EntryGateOf("Abyss_06_Core"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            pd.isInvincible = true;
            PlayMakerFSM door = null, floor = null, spawner = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != "Abyss_06_Core") continue;
                if (f.name == "abyss_door" && f.FsmName == "Control") door = f;
                else if (f.name == "floor_closed" && f.FsmName == "Disappear") floor = f;
                else if (f.name == "Shade Sibling Spawner" && f.FsmName == "Spawn") spawner = f;
            }
            if (door == null || floor == null)
            {
                V301Check("深渊大门和出生地地板", false, $"找不到：门 {door != null}，地板 {floor != null}");
                yield break;
            }
            string doorBefore = door.ActiveStateName, floorBefore = floor.ActiveStateName;
            bool floorWas = floor.gameObject.activeSelf;
            int asked0 = HkItems.DevAskedAgain;

            // A hotbar slot freed: the brand (first in the table) comes into it, carried, not in hand.
            inv.Slots[1] = default;
            yield return null;
            yield return null;
            string doorAfter = door.ActiveStateName;
            bool brandIn = !inv.Slots[1].IsEmpty && inv.Slots[1].Type == ItemType.HkKingsBrand;
            V301Check("背包满着进房间，腾出一格让王之印记进来，深渊大门准备打开", notCarried && brandIn && doorBefore == "Check" && doorAfter == "Ready",
                $"进门时没带上 {notCarried}，第 2 格 {inv.Slots[1].Def?.Name}，放进前 {doorBefore}，放进后 {doorAfter}，手里 {inv.Current.Def?.Name}（{HkItems.DevLastAskedAgain}）");
            // Walked up to it, it opens (Ready: the door's own trigger).
            var box = door.GetComponent<Collider2D>();
            var hero = HeroController.instance;
            if (box != null)
            {
                hero.transform.position = Clear(hero, box.bounds.center);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            }
            for (float t = 0f; t < 3f && !pd.abyssGateOpened; t += Time.deltaTime) yield return null;
            V301Check("带着王之印记走过去，大门打开", pd.abyssGateOpened, $"门的触发区 {(box != null ? box.bounds.ToString() : "无")}，门状态 {door.ActiveStateName}，abyssGateOpened {pd.abyssGateOpened}");

            string spawnerBefore = spawner != null ? spawner.ActiveStateName : "无";
            inv.Slots[2] = default;
            yield return null;
            yield return null;
            bool soulIn = !inv.Slots[2].IsEmpty && inv.Slots[2].Type == ItemType.HkKingsoul;
            V301Check("再腾出一格让国王之魂进来，出生地的地板打开", soulIn && floorBefore == "State 1" && floorWas && !floor.gameObject.activeSelf,
                $"第 3 格 {inv.Slots[2].Def?.Name}，地板状态 {floorBefore}→{floor.ActiveStateName}，显示 {floorWas}→{floor.gameObject.activeSelf}，重新询问 {HkItems.DevAskedAgain - asked0} 次；暗影刷新器 {spawnerBefore}→{(spawner != null ? spawner.ActiveStateName : "无")}");
            foreach (int i in bagFillers) inv.HkBag[i] = default;
            for (int i = 0; i < Inventory.HotbarSize; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.CopperBroadsword) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(ItemType.CopperBroadsword);

            // The King's Brand's stand: got already, it isn't there again (the brand carried, not in hand).
            inv.Selected = 0;
            yield return GoTo("Room_Wyrm", EntryGateOf("Room_Wyrm"));
            pd.isInvincible = true;
            yield return new WaitForSeconds(2f);
            PlayMakerFSM stand = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene.name == "Room_Wyrm" && f.name == "Shiny Item Stand" && f.FsmName == "Shiny Control") stand = f;
            bool standShown = stand != null && stand.gameObject.activeInHierarchy && stand.ActiveStateName != "Destroy";
            V301Check("王之印记带在身上没拿在手里，进它的房间拾取处不会再出现", !standShown,
                $"拾取处 {(stand == null ? "已销毁" : stand.ActiveStateName + "，显示 " + stand.gameObject.activeInHierarchy)}，手里 {inv.Current.Def?.Name}");
            inv.Infinite = infinite;
        }
    }
}
