using System.Collections.Generic;
using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_all", or one part: "fx_map", "fx_pause", "fx_trap", "fx_guard", "fx_stars"): the checks for the
    /// 2026-10-16 batch, each line "[AutoTest] 修复1016 结果 …：通过 / 不通过".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        int fxOk, fxAll;

        void FxCheck(string what, bool pass, string detail)
        {
            fxAll++;
            if (pass) fxOk++;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 结果 {what}：{detail} {(pass ? "通过" : "不通过")}");
        }

        IEnumerator Fix1016Checks(string which)
        {
            bool all = which == "all";
            if (all || which == "map") yield return FxMap();
            if (all || which == "pause") yield return FxPause();
            if (all || which == "trap") yield return FxTrap();
            if (all || which == "guard") yield return FxGuard();
            if (all || which == "stars") yield return FxStars();
            if (all || which == "knock") yield return FxKnock();
            if (all || which == "reflect") yield return FxReflect();
            if (all || which == "time") yield return FxTime();
            if (all || which == "candle") yield return FxCandle();
            if (all || which == "meteoronce") yield return FxMeteorOnce();
            if (all || which == "maskreturn") yield return FxMaskReturn();
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}");
        }

        // ------------------------------------------------------------------ 6, 7: the map

        IEnumerator FxMap()
        {
            // A new game: nothing charted, King's Pass and Dirtmouth included (mapDirtmouth is already set).
            yield return new WaitForSecondsRealtime(2.5f);
            var pd = PlayerData.instance;
            int kp = UI.WorldMap.DevChartCount("国王山道"), town = UI.WorldMap.DevChartCount("德特茅斯");
            FxCheck("开局地图不点亮", kp == 0 && town == 0 && pd.GetBool("mapDirtmouth") && !pd.GetBool("hasMap"),
                $"国王山道已画 {kp}，德特茅斯已画 {town}，mapDirtmouth {pd.GetBool("mapDirtmouth")} hasMap {pd.GetBool("hasMap")}");
            // A map bought: Dirtmouth's own map charts it, as in the game.
            pd.SetBool("hasMap", true);
            yield return new WaitForSecondsRealtime(2.5f);
            town = UI.WorldMap.DevChartCount("德特茅斯");
            pd.SetBool("hasMap", false);
            FxCheck("买了地图后德特茅斯画出", town > 0, $"德特茅斯已画 {town}");
            yield return new WaitForSecondsRealtime(1.5f);

            // The reach round the player (MapExploration): five cells more up and down than to the sides, on an open grid.
            const int w = 96, h = 96, rx = UI.MapExploration.Radius, ry = UI.MapExploration.RadiusY;
            var seen = new bool[w * h];
            UI.MapExploration.Reveal(w, h, 48, 48, (x, y) => false, (x, y) => { if (seen[y * w + x]) return false; seen[y * w + x] = true; return true; });
            bool upIn = seen[(48 + ry) * w + 48], upOut = seen[(48 + ry + 1) * w + 48], downIn = seen[(48 - ry) * w + 48];
            bool sideIn = seen[48 * w + 48 + rx], sideOut = seen[48 * w + 48 + rx + 1];
            FxCheck("点亮范围上下多五格", ry == rx + 5 && upIn && downIn && !upOut && sideIn && !sideOut, $"上{ry} {upIn} 下{ry} {downIn} 上{ry + 1} {upOut} 横{rx} {sideIn} 横{rx + 1} {sideOut}");
        }

        // ------------------------------------------------------------------ 9: Autopause

        IEnumerator FxPause()
        {
            bool was = UI.AutoPause.On;
            var hero = HeroController.instance;
            UI.AutoPause.Set(true);
            InventoryGui.Open = true;
            for (int i = 0; i < 5; i++) yield return null;
            float scaleOpen = Time.timeScale;
            bool control = GameHooks.PlayerHasControl();
            InventoryGui.Open = false;
            for (int i = 0; i < 5; i++) yield return null;
            float scaleClosed = Time.timeScale;
            FxCheck("自动暂停开：打开背包暂停", scaleOpen == 0f && !control && scaleClosed == 1f, $"背包开时时速 {scaleOpen} 可操作 {control}，关后 {scaleClosed}");

            UI.AutoPause.Set(false);
            InventoryGui.Open = true;
            for (int i = 0; i < 5; i++) yield return null;
            float scaleOff = Time.timeScale;
            InventoryGui.Open = false;
            for (int i = 0; i < 5; i++) yield return null;
            FxCheck("自动暂停关：背包不暂停", scaleOff == 1f, $"背包开时时速 {scaleOff}");

            UI.IngameOptions.Show();
            for (int i = 0; i < 5; i++) yield return null;
            float scaleOptions = Time.timeScale;
            UI.IngameOptions.Hide();
            for (int i = 0; i < 5; i++) yield return null;
            FxCheck("泰拉瑞亚设置窗口总会暂停", scaleOptions == 0f && Time.timeScale == 1f, $"设置开时时速 {scaleOptions}，关后 {Time.timeScale}");
            UI.AutoPause.Set(was);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ------------------------------------------------------------------ 8: falling traps

        IEnumerator FxTrap()
        {
            var hero = HeroController.instance;
            var stats = mod.Stats;
            // A stand-in stalactite beside the player (the game's own sit in the Crossroads' and the Edge's ceilings).
            var go = new GameObject("Test Stalactite");
            go.AddComponent<Rigidbody2D>().isKinematic = true;
            go.AddComponent<StalactiteControl>();
            go.transform.position = hero.transform.position + new Vector3(0.5f, 1f, 0f);
            yield return null;
            // Enough life for the hit on any difficulty (Master's boulder is 420).
            int crystals = stats.LifeCrystals;
            stats.LifeCrystals = TerraKnight.Player.TerraStats.MaxLifeCrystals;
            var difficulty = World.GameDifficulty.Current;
            World.GameDifficulty.Current = World.DifficultyLevel.Normal;
            yield return new WaitForSecondsRealtime(0.5f);
            stats.FullHeal();
            PlayerData.instance.isInvincible = false;
            yield return new WaitForSecondsRealtime(1.5f);
            int before = stats.Life, hits0 = World.Stalactites.DevHits;
            int raw = World.Stalactites.Damage();
            int want = Mathf.Max(1, raw - stats.Defense / 2);
            hero.TakeDamage(go, GlobalEnums.CollisionSide.left, 1, 1);
            yield return null;
            int lost = before - stats.Life;
            int boulder = Mathf.RoundToInt(70f * (World.GameDifficulty.Current == World.DifficultyLevel.Normal ? 1f : World.GameDifficulty.Current == World.DifficultyLevel.Hard ? 1.5f : World.GameDifficulty.Current == World.DifficultyLevel.Expert ? 2f : 3f)) * 2;
            FxCheck("下落陷阱=原版巨石伤害", World.Stalactites.DevHits == hits0 + 1 && raw == boulder && lost >= want - 2 && lost <= want,
                $"难度 {World.GameDifficulty.Current}，巨石 {raw}（应 {boulder}），防御 {stats.Defense}，掉血 {lost}（应约 {want}）");
            Object.Destroy(go);
            World.GameDifficulty.Current = difficulty;
            stats.LifeCrystals = crystals;
            stats.FullHeal();
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
        }

        // ------------------------------------------------------------------ 11, 12: shields, the Elder Baldur, minions

        static bool StateStarts(HealthManager hm, string fsmName, string prefix)
        {
            foreach (var f in hm.GetComponents<PlayMakerFSM>())
                if (f.FsmName == fsmName && f.ActiveStateName != null && f.ActiveStateName.StartsWith(prefix)) return true;
            return false;
        }

        IEnumerator UseOn(ItemType type, Vector2 aim)
        {
            var inv = mod.Inventory;
            inv.Slots[0] = ItemStack.Of(type);
            inv.Selected = 0;
            inv.Override = -1;
            mod.Stats.Mana = mod.Stats.ManaMax;
            float until = Time.time + 2f;
            while (!mod.DevReady && Time.time < until) yield return null;
            mod.DevUseSelected(aim);
        }

        IEnumerator FxGuard()
        {
            HitTrace.On = true;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            int selected = inv.Selected;
            var ammo = inv.Slots[Inventory.Size - 1];
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 200);

            // Husk Warriors: once its shield is up towards the player, its FSM and body are held still; then swords and
            // arrows from the shield's side are stopped, magic goes through, and a sword from behind lands.
            yield return GoTo("Crossroads_15", EntryGateOf("Crossroads_15"));
            PlayerData.instance.isInvincible = true;
            var husks = EnemiesNamed("Zombie Shield");
            if (husks.Count == 0) FxCheck("盾牌怪", false, "Crossroads_15 没有躯壳战士");
            else
            {
                var hm = husks[0];
                hm.hp = 400;
                PlayMakerFSM control = null;
                foreach (var f in hm.GetComponents<PlayMakerFSM>()) if (f.FsmName == "ZombieShieldControl") control = f;
                var c = (Vector2)hm.GetComponent<Collider2D>().bounds.center;
                bool raised = false;
                for (int k = 0; k < 6 && !raised; k++)
                {
                    c = hm.GetComponent<Collider2D>().bounds.center;
                    hero.transform.position = new Vector3(c.x - 2.2f, c.y + 0.2f, hero.transform.position.z);
                    hero.FaceRight();
                    float wait = Time.time + 2.5f;
                    while (Time.time < wait && !(hm.IsInvincible && hm.InvincibleFromDirection != 0 && StateStarts(hm, "ZombieShieldControl", "Shield Left"))) { PlayerData.instance.isInvincible = true; yield return null; }
                    raised = hm.IsInvincible && StateStarts(hm, "ZombieShieldControl", "Shield Left");
                }
                if (!raised || control == null) FxCheck("盾牌怪举盾", false, Guard(hm));
                else
                {
                    control.enabled = false;
                    var body = hm.GetComponent<Rigidbody2D>();
                    if (body != null) { body.linearVelocity = Vector2.zero; body.isKinematic = true; }
                    var hold = (Vector2)hm.transform.position;
                    c = hm.GetComponent<Collider2D>().bounds.center;
                    Plugin.Log.LogInfo($"[AutoTest] 修复1016 盾举起并定住：{Guard(hm)}");
                    var tries = new (string label, ItemType item, float dx, bool expectBlock)[]
                    {
                        ("剑砍盾面", ItemType.BreakerBlade, -1.5f, true), ("剑砍盾面", ItemType.BreakerBlade, -1.5f, true),
                        ("箭射盾面", ItemType.WoodenBow, -4f, true), ("箭射盾面", ItemType.WoodenBow, -4f, true),
                        ("魔法打盾面", ItemType.AmethystStaff, -3f, false), ("魔法打盾面", ItemType.AmethystStaff, -3f, false),
                        ("剑砍背后", ItemType.BreakerBlade, 1.5f, false),
                    };
                    foreach (var t in tries)
                    {
                        hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                        hero.transform.position = new Vector3(c.x + t.dx, c.y + 0.2f, hero.transform.position.z);
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        if (t.dx < 0) hero.FaceRight(); else hero.FaceLeft();
                        yield return new WaitForSeconds(0.2f);
                        int before = hm.hp, blocked0 = MeleeHits.DevBlocked, bolts0 = TerraProjectiles.DevHit(ProjKind.AmethystBolt);
                        string guard = Guard(hm);
                        yield return UseOn(t.item, c);
                        float wait = Time.time + 1f;
                        while (Time.time < wait && hm.hp == before && MeleeHits.DevBlocked == blocked0 && TerraProjectiles.DevHit(ProjKind.AmethystBolt) == bolts0)
                        {
                            hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                            PlayerData.instance.isInvincible = true;
                            yield return null;
                        }
                        bool wasBlocked = hm.hp == before && MeleeHits.DevBlocked > blocked0;
                        // A small hit may go only towards the enemy's next point of health: not stopped is what counts.
                        yield return new WaitForSeconds(0.1f);
                        bool landed = hm.hp < before || (t.item == ItemType.AmethystStaff && TerraProjectiles.DevHit(ProjKind.AmethystBolt) > bolts0 && MeleeHits.DevBlocked == blocked0);
                        FxCheck($"盾牌怪 {t.label}", t.expectBlock ? wasBlocked : landed, $"挥前 {guard}，掉血 {before - hm.hp}，挡 {MeleeHits.DevBlocked - blocked0}，紫晶弹 造出 {TerraProjectiles.DevMade(ProjKind.AmethystBolt)} 命中 {TerraProjectiles.DevHit(ProjKind.AmethystBolt)}，魔力 {mod.Stats.Mana}/{mod.Stats.ManaMax}，主角 {(Vector2)hero.transform.position:F1} 目标 {c:F1}");
                        yield return new WaitForSeconds(0.3f);
                    }
                    control.enabled = true;
                    if (body != null) body.isKinematic = false;
                }
            }

            // The Elder Baldur: shut, nothing gets in; open (the player some way off on its open side), the finch goes for it.
            yield return GoTo("Crossroads_11_alt", EntryGateOf("Crossroads_11_alt"));
            PlayerData.instance.isInvincible = true;
            var baldurs = EnemiesNamed("Blocker");
            if (baldurs.Count == 0) FxCheck("巴德尔长者", false, "Crossroads_11_alt 没有巴德尔长者");
            else
            {
                var hm = baldurs[0];
                hm.hp = 400;
                int tries = 0, kept = 0;
                for (int k = 0; k < 10 && tries < 4; k++)
                {
                    var c = (Vector2)hm.GetComponent<Collider2D>().bounds.center;
                    hero.transform.position = new Vector3(c.x + 1.6f, c.y + 0.3f, hero.transform.position.z);
                    hero.FaceLeft();
                    float wait = Time.time + 3f;
                    while (Time.time < wait && !hm.IsInvincible) { PlayerData.instance.isInvincible = true; yield return null; }
                    if (!hm.IsInvincible) continue;
                    int before = hm.hp;
                    yield return UseOn(k % 2 == 0 ? ItemType.BreakerBlade : ItemType.AmethystStaff, c);
                    yield return new WaitForSeconds(0.6f);
                    tries++;
                    if (hm.hp == before) kept++;
                }
                FxCheck("巴德尔缩壳时打不动（剑和魔法）", tries > 0 && kept == tries, $"缩壳时打 {tries} 次，没掉血 {kept} 次");
                Shot("Crossroads_11_alt", "fx_baldur_shell");

                // The finch, with the player far enough off on its open side for it to open up and spit.
                var c2 = (Vector2)hm.GetComponent<Collider2D>().bounds.center;
                float stand = 5f;
                for (int k = 0; k < 6; k++)
                {
                    hero.transform.position = new Vector3(c2.x + stand, c2.y + 0.5f, hero.transform.position.z);
                    float wait = Time.time + 3f;
                    while (Time.time < wait && hm.IsInvincible) { PlayerData.instance.isInvincible = true; yield return null; }
                    Plugin.Log.LogInfo($"[AutoTest] 修复1016 雀 站 {stand:F1} 格外：{Guard(hm)}");
                    if (!hm.IsInvincible) break;
                    stand += 0.5f;
                }
                inv.Slots[1] = ItemStack.Of(ItemType.FinchStaff);
                inv.Selected = 1;
                float until = Time.time + 2f;
                while (!mod.DevReady && Time.time < until) yield return null;
                mod.DevUseSelected((Vector2)hero.transform.position + Vector2.up);
                int hp0 = hm.hp, hits0 = Minions.DevHits, blockedF = MeleeHits.DevBlocked;
                float end = Time.time + 15f;
                float nextLog = Time.time;
                while (Time.time < end && hm.hp == hp0 && !hm.isDead && Minions.DevHits < hits0 + 5)
                {
                    PlayerData.instance.isInvincible = true;
                    if (Time.time >= nextLog)
                    {
                        nextLog = Time.time + 1f;
                        Plugin.Log.LogInfo($"[AutoTest] 修复1016 雀 {Minions.Instance?.DevDescribe()}，巴德尔 {Guard(hm)}；{Minions.Instance?.DevWhyNot(hm, Minions.Kind.Finch)}");
                    }
                    yield return null;
                }
                FxCheck("雀杖打张嘴的巴德尔长者", hm.hp < hp0 || hm.isDead || Minions.DevHits >= hits0 + 5, $"血 {hp0} -> {hm.hp}，召唤物命中 {Minions.DevHits - hits0} 次，被挡 {MeleeHits.DevBlocked - blockedF} 次；{Minions.Instance?.DevWhyNot(hm, Minions.Kind.Finch)}");
                Shot("Crossroads_11_alt", "fx_finch_baldur");
                Minions.Instance?.Clear();
                Player.PlayerBuffs.Clear();
            }
            HitTrace.On = false;
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Slots[Inventory.Size - 1] = ammo;
            inv.Selected = selected;
        }

        // ------------------------------------------------------------------ the Water Candle: one step a click

        /// <summary>A left click held as a player holds one (the Input System's own state), in the middle of the screen.</summary>
        static IEnumerator HoldLeft(float seconds)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) yield break;
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = mid }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
            yield return new WaitForSecondsRealtime(seconds);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = mid });
            yield return new WaitForSecondsRealtime(0.4f);
        }

        IEnumerator FxCandle()
        {
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            int selected = inv.Selected, level0 = CarriedFurniture.CandleLevel;
            inv.Slots[0] = ItemStack.Of(ItemType.WaterCandle);
            inv.Selected = 0;
            inv.Override = -1;
            CarriedFurniture.CandleLevel = 1;
            yield return new WaitForSeconds(0.5f);
            var levels = new List<int>();
            TerraMod.DevNoRealMouse = false;
            try
            {
                for (int k = 0; k < 3; k++)
                {
                    yield return HoldLeft(0.3f);
                    levels.Add(CarriedFurniture.CandleLevel);
                }
            }
            finally
            {
                TerraMod.DevNoRealMouse = true;
            }
            // The old setting for comparison (auto reuse on, no use time): one hold of the same length.
            var def = ItemDb.Get(ItemType.WaterCandle);
            bool auto0 = def.AutoReuse;
            float use0 = def.UseTime, anim0 = def.UseAnimation;
            def.AutoReuse = true;
            def.UseTime = def.UseAnimation = 0f;
            int steps = 0;
            CarriedFurniture.CandleLevel = 1;
            TerraMod.DevNoRealMouse = false;
            int last = CarriedFurniture.CandleLevel;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = mid }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
            for (float t = Time.realtimeSinceStartup + 0.3f; Time.realtimeSinceStartup < t;) { if (CarriedFurniture.CandleLevel != last) { steps++; last = CarriedFurniture.CandleLevel; } yield return null; }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = mid });
            yield return new WaitForSecondsRealtime(0.4f);
            TerraMod.DevNoRealMouse = true;
            def.AutoReuse = auto0;
            def.UseTime = use0;
            def.UseAnimation = anim0;
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 水蜡烛 旧设置（自动连用、间隔 0）按住 0.3 秒切了 {steps} 档");
            FxCheck("水蜡烛点一下只切一档", levels.Count == 3 && levels[0] == 2 && levels[1] == 3 && levels[2] == 1, $"三次按住 0.3 秒：1 -> {string.Join(" -> ", levels)}");
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Selected = selected;
            CarriedFurniture.CandleLevel = level0;
        }

        // ------------------------------------------------------------------ an old save's mask crystals taken back

        IEnumerator FxMaskReturn()
        {
            var inv = mod.Inventory;
            var stats = mod.Stats;
            var saved = (ItemStack[])inv.Slots.Clone();
            var granted0 = new HashSet<string>(mod.GrantedAbilities);
            int crystals0 = stats.LifeCrystals;
            // As a 3.1.1 save: three masks made beyond the first five (each gave a crystal), one of those still in the bag.
            mod.GrantedAbilities.Remove("MaskCrystalsReturned");
            foreach (var k in new[] { "Mask6", "Mask7", "Mask8" }) mod.GrantedAbilities.Add(k);
            for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.LifeCrystal) inv.Slots[i] = default;
            inv.Slots[Inventory.HotbarSize + 2] = ItemStack.Of(ItemType.LifeCrystal, 1);
            stats.LifeCrystals = 10;
            int back0 = MaskCrystalReturn.DevReturned;
            MaskCrystalReturn.Run(mod);
            int inBag = Recipes.Count(inv, ItemType.LifeCrystal), eaten = stats.LifeCrystals, back = MaskCrystalReturn.DevReturned - back0;
            MaskCrystalReturn.Run(mod);
            bool once = MaskCrystalReturn.DevReturned - back0 == back && stats.LifeCrystals == eaten;
            FxCheck("老存档收回面具送的生命水晶", back == 3 && inBag == 0 && eaten == 8 && once && mod.GrantedAbilities.Contains("MaskCrystalsReturned"),
                $"送过 3 颗：收回 {back} 颗，背包剩 {inBag} 颗（原 1），已吃 10 -> {eaten}，再跑一次不再收 {once}");
            yield return new WaitForSeconds(0.3f);
            System.Array.Copy(saved, inv.Slots, saved.Length);
            mod.GrantedAbilities.Clear();
            mod.GrantedAbilities.UnionWith(granted0);
            stats.LifeCrystals = crystals0;
        }

        // ------------------------------------------------------------------ one meteorite for the whole save

        IEnumerator FxMeteorOnce()
        {
            bool pending0 = World.Meteorites.Pending;
            int landed0 = World.Meteorites.Landed;
            World.Meteorites.Pending = false;
            World.Meteorites.Landed = 0;
            World.Meteorites.OnBossDown(true);
            bool first = World.Meteorites.Pending;
            World.Meteorites.Pending = false;
            World.Meteorites.Landed = 1;
            World.Meteorites.OnBossDown(true);
            bool againFirst = World.Meteorites.Pending;
            World.Meteorites.OnBossDown(false);
            bool againRepeat = World.Meteorites.Pending;
            FxCheck("整个存档只落一次陨石", first && !againFirst && !againRepeat, $"没落过时击败首领排上 {first}；落过一颗后再击败新首领 {againFirst}、重打首领 {againRepeat}");
            World.Meteorites.Pending = pending0;
            World.Meteorites.Landed = landed0;
            yield return null;
        }

        // ------------------------------------------------------------------ Terraria's clock runs between saves

        IEnumerator FxTime()
        {
            float saved0 = PlayerData.instance.playTime, now0 = World.TerraTime.Now, real0 = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(3f);
            float went = World.TerraTime.Now - now0, real = Time.realtimeSinceStartup - real0;
            FxCheck("泰拉瑞亚时间在走（不用存档）", went > real * 0.8f && went < real * 1.2f,
                $"3 秒里时钟走了 {went:F2} 秒（实际 {real:F2}），存档里的游玩时间 {saved0:F1} -> {PlayerData.instance.playTime:F1}");
        }

        // ------------------------------------------------------------------ shields send shots back

        IEnumerator FxReflect()
        {
            TerraProjectiles.DevExportReflectionCatalog(System.IO.Path.Combine(DevCopy.SaveDir,"reflection-catalog.json"));
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var stats = mod.Stats;
            var saved = (ItemStack[])inv.Slots.Clone();
            int selected = inv.Selected;
            yield return GoTo("Crossroads_15", EntryGateOf("Crossroads_15"));
            PlayerData.instance.isInvincible = true;
            var husks = EnemiesNamed("Zombie Shield");
            if (husks.Count == 0) { FxCheck("盾反弹", false, "Crossroads_15 没有躯壳战士"); yield break; }
            var hm = husks[0];
            hm.hp = 400;
            // The other enemies of the room out of the way meanwhile (an earlier check may have pushed one into the line).
            var others = new System.Collections.Generic.List<GameObject>();
            foreach (var o in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (o != null && o != hm && !o.isDead && o.gameObject.activeInHierarchy) { others.Add(o.gameObject); o.gameObject.SetActive(false); }
            PlayMakerFSM control = null;
            foreach (var f in hm.GetComponents<PlayMakerFSM>()) if (f.FsmName == "ZombieShieldControl") control = f;
            bool raised = false;
            Vector2 c = hm.GetComponent<Collider2D>().bounds.center;
            for (int k = 0; k < 6 && !raised; k++)
            {
                c = hm.GetComponent<Collider2D>().bounds.center;
                hero.transform.position = new Vector3(c.x - 2.2f, c.y + 0.2f, hero.transform.position.z);
                hero.FaceRight();
                float wait = Time.time + 2.5f;
                while (Time.time < wait && !(hm.IsInvincible && StateStarts(hm, "ZombieShieldControl", "Shield Left"))) { PlayerData.instance.isInvincible = true; yield return null; }
                raised = hm.IsInvincible && StateStarts(hm, "ZombieShieldControl", "Shield Left");
            }
            if (!raised || control == null) { FxCheck("盾反弹 举盾", false, Guard(hm)); yield break; }
            control.enabled = false;
            var body = hm.GetComponent<Rigidbody2D>();
            if (body != null) { body.linearVelocity = Vector2.zero; body.isKinematic = true; }
            var hold = (Vector2)hm.transform.position;
            c = hm.GetComponent<Collider2D>().bounds.center;
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 200);
            inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.MusketBall, 200);
            int crystals = stats.LifeCrystals;
            stats.LifeCrystals = TerraKnight.Player.TerraStats.MaxLifeCrystals;
            FxCheck("原版反弹分类：无贴图小鬼火球及黄蜂毒刺", TerraProjectiles.ReflectableKind(ProjKind.ImpFireball) && TerraProjectiles.ReflectableKind(ProjKind.HornetStinger), "原版376/374都允许反弹");
            FxCheck("原版反弹分类：共用贴图不能共用规则", TerraProjectiles.ReflectableKind(ProjKind.ThunderBolt) && !TerraProjectiles.ReflectableKind(ProjKind.LightningStrikeBolt), "原版731可反弹，1117不可反弹");
            FxCheck("原版反弹分类：回旋镖与反弹光球不再反弹", !TerraProjectiles.ReflectableKind(ProjKind.WoodenBoomerang) && !TerraProjectiles.ReflectableKind(ProjKind.ReflectedOrb), "无反弹循环");
            FxCheck("原版反弹分类：飞镖及魔法竖琴音符", TerraProjectiles.ReflectableKind(ProjKind.PoisonDartProj) && TerraProjectiles.ReflectableKind(ProjKind.CrystalDartProj) && TerraProjectiles.ReflectableKind(ProjKind.HarpNote1) && TerraProjectiles.ReflectableKind(ProjKind.HarpNote3), "原版267/477/76/78");
            var shots = new (string label, ItemType item, ProjKind direct, bool reflects)[]
            {
                ("木弓箭", ItemType.WoodenBow, ProjKind.None, true),
                ("燧发枪子弹", ItemType.FlintlockPistol, ProjKind.None, true),
                ("木制回旋镖（原版不反弹）", ItemType.WoodenBoomerang, ProjKind.None, false),
                ("小鬼火球", ItemType.None, ProjKind.ImpFireball, true),
                ("黄蜂毒刺", ItemType.None, ProjKind.HornetStinger, true),
                ("霹雳法杖弹幕", ItemType.None, ProjKind.ThunderBolt, true),
                ("闪电之击弹幕（原版不反弹）", ItemType.None, ProjKind.LightningStrikeBolt, false),
            };
            foreach (var t in shots)
            {
                TerraProjectiles.Instance.DevClear();
                hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                // An arrow falls on its way back at half speed: from nearer.
                hero.transform.position = new Vector3(c.x - (t.item == ItemType.WoodenBow ? 2.6f : 5f), c.y + 0.2f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(1.2f);
                stats.FullHeal();
                PlayerData.instance.isInvincible = false;
                int life0 = stats.Life, refl0 = TerraProjectiles.DevReflected, hits0 = TerraProjectiles.DevReflectHits, blocked0 = MeleeHits.DevBlocked, hp0 = hm.hp;
                if(t.direct==ProjKind.None) yield return UseOn(t.item, c);
                else TerraProjectiles.Instance.Shoot(t.direct,c-Vector2.right*.7f,Vector2.right*8f,160,0f,0f);
                float until = Time.time + 4f;
                while (Time.time < until && TerraProjectiles.DevReflectHits == hits0 && !(!t.reflects && MeleeHits.DevBlocked > blocked0 && Time.time > until - 2.5f))
                {
                    hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                    yield return null;
                }
                yield return new WaitForSeconds(0.2f);
                PlayerData.instance.isInvincible = true;
                int lost = life0 - stats.Life;
                bool reflected = TerraProjectiles.DevReflected > refl0;
                if (t.reflects)
                    FxCheck($"盾反弹 {t.label}（变成慢速光球）", reflected && Mathf.Abs(TerraProjectiles.DevReflectSpeedOut - TerraProjectiles.ReflectSpeed) < 0.05f
                        && TerraProjectiles.DevReflectHits > hits0 && lost > 0 && hm.hp == hp0,
                        $"反弹 {reflected}（{TerraProjectiles.DevReflectKind}），速度 {TerraProjectiles.DevReflectSpeedIn:F1}->{TerraProjectiles.DevReflectSpeedOut:F1}，反弹伤害 {TerraProjectiles.DevReflectDamage}×2={TerraProjectiles.DevReflectHitDamage}，打中主角 {TerraProjectiles.DevReflectHits - hits0} 次 掉血 {lost}（防御 {stats.Defense}），怪掉血 {hp0 - hm.hp}");
                else
                    FxCheck($"盾反弹 {t.label}", !reflected && MeleeHits.DevBlocked > blocked0 && lost == 0 && hm.hp == hp0,
                        $"反弹 {reflected}，挡住 {MeleeHits.DevBlocked - blocked0} 次，主角掉血 {lost}，怪掉血 {hp0 - hm.hp}");
                Shot("Crossroads_15", $"fx_reflect_{(int)t.item}_{t.direct}");
                yield return new WaitForSeconds(1.5f);
            }
            // Invincible all round (an enemy hiding, curled up): the shot is stopped, not sent back.
            {
                hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                hero.transform.position = new Vector3(c.x - 5f, c.y + 0.2f, hero.transform.position.z);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(1.2f);
                int dir0 = hm.InvincibleFromDirection;
                hm.IsInvincible = true;
                hm.InvincibleFromDirection = 0;
                int refl0 = TerraProjectiles.DevReflected, blocked0 = MeleeHits.DevBlocked, hp0 = hm.hp;
                yield return UseOn(ItemType.FlintlockPistol, c);
                for (float until = Time.time + 1.5f; Time.time < until && MeleeHits.DevBlocked == blocked0; )
                {
                    hm.transform.position = new Vector3(hold.x, hold.y, hm.transform.position.z);
                    hm.IsInvincible = true;
                    hm.InvincibleFromDirection = 0;
                    yield return null;
                }
                bool reflected = TerraProjectiles.DevReflected > refl0;
                FxCheck("全方位无敌（躲着的怪）挡住子弹但不反弹", !reflected && MeleeHits.DevBlocked > blocked0 && hm.hp == hp0,
                    $"反弹 {reflected}，挡住 {MeleeHits.DevBlocked - blocked0} 次，怪掉血 {hp0 - hm.hp}");
                hm.InvincibleFromDirection = dir0;
            }
            control.enabled = true;
            if (body != null) body.isKinematic = false;
            foreach (var o in others) if (o != null) o.SetActive(true);
            stats.LifeCrystals = crystals;
            stats.FullHeal();
            System.Array.Copy(saved, inv.Slots, saved.Length);
            inv.Selected = selected;
        }


        // ------------------------------------------------------------------ 13: steady after five knockbacks

        IEnumerator FxKnock()
        {
            yield return GoTo("Crossroads_15", EntryGateOf("Crossroads_15"));
            PlayerData.instance.isInvincible = true;
            HealthManager target = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && !TerraKnockback.Immune(hm) && EnemyStats.Of(hm) != null) { target = hm; break; }
            if (target == null) { FxCheck("连续击退后免疫", false, "这里没有能击退的敌人"); yield break; }
            target.hp = Mathf.Max(target.hp, 400);
            // A push while it still flies from the last moves it no further but counts towards steadying it.
            int Applied() => TerraKnockback.DevLaunches + TerraKnockback.DevOwnPushes + TerraKnockback.DevHeldInFlight;
            int a0 = Applied(), held0 = TerraKnockback.DevHeldBySteady;
            var moved = new System.Text.StringBuilder();
            for (int i = 0; i < 7; i++)
            {
                int before = Applied();
                TerraKnockback.Push(target, 6f, i % 2 == 0 ? 1f : -1f);
                moved.Append(Applied() > before ? "推" : "稳");
                yield return new WaitForSeconds(0.3f);
            }
            bool steadyNow = TerraKnockback.Steady(target);
            FxCheck("3秒内击退5次后免疫", moved.ToString() == "推推推推推稳稳" && steadyNow, $"{target.name}：{moved}，稳住 {steadyNow}，挡下 {TerraKnockback.DevHeldBySteady - held0}");
            yield return new WaitForSeconds(TerraKnockback.SteadyFor + 0.2f);
            int b0 = Applied();
            TerraKnockback.Push(target, 6f, 1f);
            FxCheck($"免疫{TerraKnockback.SteadyFor:F0}秒后又能击退", Applied() == b0 + 1 && !TerraKnockback.Steady(target), $"推了 {Applied() - b0}");
            // Spread out (one every 0.8 s, five over more than 3 s): never steady.
            yield return new WaitForSeconds(0.8f);
            int c0 = Applied();
            for (int i = 0; i < 6; i++)
            {
                TerraKnockback.Push(target, 6f, i % 2 == 0 ? 1f : -1f);
                yield return new WaitForSeconds(0.8f);
            }
            FxCheck("慢慢击退不会免疫", Applied() - c0 == 6, $"6 次推了 {Applied() - c0} 次");
        }

        // ------------------------------------------------------------------ 10: mana stars

        IEnumerator FxStars()
        {
            var stats = mod.Stats;
            var hero = HeroController.instance;
            stats.Mana = 0;
            int stars = ManaStars.DevRoll(4000);
            float mean = stars / 4000f;
            // 1/6 x 1/2 + 1/2 a kill, while the mana is short of full (luck aside).
            FxCheck("怪物掉星概率同原版", mean > 0.54f && mean < 0.63f, $"4000 次击杀平均 {mean:F3} 颗（原版约 0.583）");
            stats.Mana = stats.ManaMax;
            int full = ManaStars.DevRoll(1000);
            FxCheck("魔力满时不掉星", full == 0, $"1000 次 {full} 颗");

            stats.Mana = 0;
            int given0 = ManaStars.DevGiven;
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 1.2f;
            WorldItems.Drop(ItemStack.Of(ItemType.Star, 1), at, personal: true);
            float until = Time.time + 4f;
            while (Time.time < until && ManaStars.DevGiven == given0) yield return null;
            FxCheck("捡星回100魔力", ManaStars.DevGiven == given0 + 1 && stats.Mana == Mathf.Min(100, stats.ManaMax), $"拾取 {ManaStars.DevGiven - given0}，魔力 {stats.Mana}/{stats.ManaMax}");

            // Real kills with the mana empty: stars on the ground (or none, a roll each).
            stats.Mana = 0;
            int dropped0 = ManaStars.DevDropped, killed = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || killed >= 6) continue;
                if (EnemyStats.Of(hm) == null) continue;
                hm.Die(0f, AttackTypes.Generic, true);
                killed++;
                stats.Mana = 0;
            }
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 回蓝星 杀 {killed} 个掉了 {ManaStars.DevDropped - dropped0} 颗");
            stats.Mana = stats.ManaMax;
        }
    }
}
