using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "summon"): the summoner. With the Spider set and four summoner accessories in
    /// the fourth loadout (seven slots): each staff called twice beside an enemy held in front of
    /// the player, what came, its buff and count, the hits and life they took, with a picture;
    /// the slot cap (an eighth slime frees the oldest, taking the armor off kills down to the
    /// cap); the right-click target; every whip on the enemy (strikes, tags, player buffs, the
    /// snowflake); a tag's damage on a minion's shots, Firecracker's ×2.75 proc; a buff right-
    /// clicked away; and a picture of the buff bar.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SummonTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);

            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 召唤：场景里没有敌人");
                yield break;
            }
            var trb = target.GetComponent<Rigidbody2D>();
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            var minions = Minions.Instance;
            int slot = Inventory.HotbarSize - 1;

            // The summoner's loadout: the Spider set and four accessories (1 + 3 + 3 = 7 slots).
            mod.TrySwitchLoadout(3);
            Equip(new[] { ItemType.SpiderMask, ItemType.SpiderBreastplate, ItemType.SpiderGreaves },
                new[] { ItemType.PygmyNecklace, ItemType.NecromanticScroll, ItemType.PapyrusScarab, ItemType.SummonerEmblem });
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 召唤配装4：仆从上限 {Equipment.MaxMinions}（原版 7），召唤伤害 +{Equipment.Current.MinionDamage * 100f:F0}%（原版 5+5+6+12套装+10+15+15 = 68），仆从击退 +{Equipment.Current.MinionKB}，套装 {Equipment.FullSet}；制作栏 {InventoryGui.DevRecipeKits()}");
            yield return InventoryShot(scene, "s_loadout4");

            Vector2 Centre() => col.bounds.center;
            void Hold(Vector2 at)
            {
                target.hp = Mathf.Max(target.hp, 3000);
                Pin(target, trb, at);
            }
            void Reset()
            {
                minions.Clear();
                PlayerBuffs.Clear();
                SummonTags.Clear();
                EnemyDebuffs.Clear();
                Whips.Instance.Clear();
                TerraProjectiles.Instance.DevClear();
            }
            IEnumerator Use(ItemType type, Vector2 aim, int times)
            {
                inv.Slots[slot] = ItemStack.Of(type);
                inv.Selected = slot;
                for (int n = 0; n < times; n++)
                {
                    while (!mod.DevReady) yield return null;
                    mod.DevAim = aim;
                    mod.DevUseSelected(aim);
                    mod.DevAim = null;
                    yield return null;
                }
            }

            // --- Each staff, twice, beside the enemy.
            var staves = new (ItemType Staff, Minions.Kind Kind, float Seconds)[]
            {
                (ItemType.SlimeStaff, Minions.Kind.BabySlime, 4f),
                (ItemType.FlinxStaff, Minions.Kind.Flinx, 4f),
                (ItemType.VampireFrogStaff, Minions.Kind.VampireFrog, 4f),
                (ItemType.SpiderStaff, Minions.Kind.VenomSpider, 4f),
                (ItemType.PirateStaff, Minions.Kind.OneEyedPirate, 4f),
                (ItemType.FinchStaff, Minions.Kind.Finch, 4f),
                (ItemType.HornetStaff, Minions.Kind.Hornet, 4f),
                (ItemType.ImpStaff, Minions.Kind.Imp, 4f),
                (ItemType.AbigailsFlower, Minions.Kind.Abigail, 4f),
                (ItemType.OpticStaff, Minions.Kind.Retanimini, 4f),
                (ItemType.SanguineStaff, Minions.Kind.Bat, 5f),
                (ItemType.StardustDragonStaff, Minions.Kind.DragonHead, 4f),
                (ItemType.DeadlySphereStaff, Minions.Kind.DeadlySphere, 6f),
                (ItemType.TempestStaff, Minions.Kind.Sharknado, 5f),
                (ItemType.XenoStaff, Minions.Kind.UFO, 5f),
                (ItemType.StardustCellStaff, Minions.Kind.StardustCell, 5f),
                (ItemType.RavenStaff, Minions.Kind.Raven, 5f),
                (ItemType.Terraprisma, Minions.Kind.Terraprisma, 5f),
                (ItemType.BladeStaff, Minions.Kind.BladeStaff, 5f),
                (ItemType.DesertTigerStaff, Minions.Kind.Tiger1, 6f),
            };
            foreach (var run in staves)
            {
                if (target == null || target.isDead) break;
                Reset();
                var hold = Centre() + Vector2.right * 4f;
                Hold(hold);
                var def = ItemDb.Get(run.Staff);
                yield return Use(run.Staff, Centre() + Vector2.right * 2f + Vector2.up * 0.5f, 2);
                int hits0 = Minions.DevHits, dmg0 = Minions.DevHitDamage;
                int life0 = EnemyStats.Life(target, EnemyStats.Of(target));
                bool pictured = false;
                var debuffs = new System.Collections.Generic.HashSet<string>();
                for (float t = 0f; t < run.Seconds; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Hold(hold);
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.Venom)) debuffs.Add("毒液");
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.Poisoned)) debuffs.Add("中毒");
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)) debuffs.Add("着火");
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.Celled)) debuffs.Add("细胞附着×" + TerraProjectiles.Instance.StuckCells(target));
                    if (!pictured && t >= 1.6f)
                    {
                        pictured = true;
                        Shot(scene, "s_" + run.Staff);
                    }
                    yield return null;
                }
                int life1 = target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0;
                int hits = Minions.DevHits - hits0, dmg = Minions.DevHitDamage - dmg0;
                int buffSlot = System.Array.IndexOf(PlayerBuffs.Type, def.MinionBuff);
                Plugin.Log.LogInfo($"[AutoTest] 召唤 {def.Name}（基础伤害 {def.Damage}，面板 {Equipment.Damage(def)}）：{run.Kind}×{minions.DevCount(run.Kind)}，占用 {minions.SlotsUsed} 栏，" +
                                   $"增益 {(buffSlot >= 0 ? $"{PlayerBuffs.Name(def.MinionBuff)} x{minions.BuffCount(def.MinionBuff)} 剩 {PlayerBuffs.Time[buffSlot]}" : "无")}，" +
                                   $"命中 {hits} 次（平均 {(hits > 0 ? dmg / hits : 0)}），敌人泰拉血量 {life0} -> {life1}，减益 [{string.Join("、", debuffs)}]\n  {minions.DevDescribe()}");
            }

            // --- The sentries: each on the ground under the cursor, one at a time (maxTurrets 1).
            var sentryRuns = new (ItemType Staff, float Seconds)[]
            {
                (ItemType.QueenSpiderStaff, 6f), (ItemType.StaffoftheFrostHydra, 5f), (ItemType.HoundiusShootius, 5f),
                (ItemType.LunarPortalStaff, 4f), (ItemType.RainbowCrystalStaff, 4f), (ItemType.BarnacleStaff, 5f),
                (ItemType.FlameburstCane, 5f), (ItemType.FlameburstRod, 5f), (ItemType.FlameburstStaff, 5f),
                (ItemType.BallistaCane, 7f), (ItemType.BallistaRod, 7f), (ItemType.BallistaStaff, 7f),
                (ItemType.ExplosiveTrapCane, 4f), (ItemType.ExplosiveTrapRod, 4f), (ItemType.ExplosiveTrapStaff, 4f),
                (ItemType.LightningAuraCane, 4f), (ItemType.LightningAuraRod, 4f), (ItemType.LightningAuraStaff, 4f),
            };
            foreach (var run in sentryRuns)
            {
                if (target == null || target.isDead) break;
                Reset();
                var sentryAt = Centre() + Vector2.right * 4f;
                Hold(sentryAt);
                var def = ItemDb.Get(run.Staff);
                // The portal and the crystal float where they are put, the Barnacle rises from there: above the player.
                bool floats = run.Staff == ItemType.LunarPortalStaff || run.Staff == ItemType.RainbowCrystalStaff || run.Staff == ItemType.BarnacleStaff;
                yield return Use(run.Staff, Centre() + Vector2.right * 2f + (floats ? Vector2.up * 3f : Vector2.zero), 1);
                int placed = minions.DevSentries();
                int hits0 = Minions.DevHits, dmg0 = Minions.DevHitDamage;
                int life0 = EnemyStats.Life(target, EnemyStats.Of(target));
                var debuffs = new System.Collections.Generic.HashSet<string>();
                bool pictured = false;
                for (float t = 0f; t < run.Seconds; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Hold(sentryAt);
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)) debuffs.Add("着火");
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire)) debuffs.Add("狱火");
                    if (!pictured && t >= Mathf.Min(2.9f, run.Seconds - 0.5f))
                    {
                        pictured = true;
                        Shot(scene, "s_" + run.Staff);
                    }
                    yield return null;
                }
                int hits = Minions.DevHits - hits0, dmg = Minions.DevHitDamage - dmg0;
                Plugin.Log.LogInfo($"[AutoTest] 哨兵 {def.Name}（基础伤害 {def.Damage}，面板 {Equipment.Damage(def)}）：放下 {placed} 个（上限 {Equipment.MaxTurrets}），{run.Seconds:F0} 秒命中 {hits} 次（平均 {(hits > 0 ? dmg / hits : 0)}），" +
                                   $"敌人泰拉血量 {life0} -> {(target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0)}，减益 [{string.Join("、", debuffs)}]\n  {minions.DevDescribe()}");
            }
            Reset();
            yield return Use(ItemType.BallistaCane, Centre() + Vector2.right * 2f, 1);
            yield return Use(ItemType.FlameburstCane, Centre() + Vector2.left * 2f, 1);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 哨兵上限：先放弩车再放爆炸烈焰塔，剩 {minions.DevSentries()} 个（原版 1，旧的消失）：{minions.DevDescribe()}");
            Reset();

            // --- Sight (Collision.CanHitLine): a wall of blocks between the player and the enemy; a
            // Hornet doesn't go for what it can't see. Then the wall comes down.
            Reset();
            var sightAt = Centre() + Vector2.right * 4f;
            Hold(sightAt);
            var wall = new System.Collections.Generic.List<(int X, int Y)>();
            int wallX = Mathf.FloorToInt((Centre().x + 2f) * 2f), wallY = Mathf.FloorToInt((Centre().y - 1.5f) * 2f);
            for (int y = wallY; y < wallY + 16; y++)
            {
                if (TerraKnight.World.TerraWorld.Get(wallX, y) != TerraKnight.World.CellKind.Empty) continue;
                TerraKnight.World.TerraWorld.Set(wallX, y, TerraKnight.World.CellKind.Stone);
                wall.Add((wallX, y));
            }
            yield return Use(ItemType.HornetStaff, Centre() + Vector2.up * 1.5f, 1);
            int sting0 = TerraProjectiles.DevMade(ProjKind.HornetStinger);
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                Hold(sightAt);
                if (t > 1.5f && t < 1.55f) Shot(scene, "s_sight_wall");
                yield return null;
            }
            int walled = TerraProjectiles.DevMade(ProjKind.HornetStinger) - sting0;
            foreach (var c in wall) TerraKnight.World.TerraWorld.Set(c.X, c.Y, TerraKnight.World.CellKind.Empty);
            sting0 = TerraProjectiles.DevMade(ProjKind.HornetStinger);
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                Hold(sightAt);
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 视线：{wall.Count} 格石墙挡在中间时黄蜂射了 {walled} 根毒刺（原版 0，看不见就不追），拆墙后 3 秒 {TerraProjectiles.DevMade(ProjKind.HornetStinger) - sting0} 根");

            // --- The pygmies throw spears (every 30 ticks, poisoning with Venom) from where they stand.
            Reset();
            var pygmyAt = Centre() + Vector2.right * 4f;
            Hold(pygmyAt);
            yield return Use(ItemType.PygmyStaff, Centre() + Vector2.right * 2f + Vector2.up * 0.5f, 2);
            int spears0 = TerraProjectiles.DevMade(ProjKind.PygmySpear), spearHits0 = TerraProjectiles.DevHit(ProjKind.PygmySpear);
            int pygmyLife0 = target != null ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0;
            bool venom = false;
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                if (target == null || target.isDead) break;
                Hold(pygmyAt);
                venom |= EnemyDebuffs.Has(target, EnemyDebuffs.Venom);
                if (t > 1.6f && t < 1.65f) Shot(scene, "s_PygmyStaff");
                yield return null;
            }
            int pygmies = minions.DevCount(Minions.Kind.Pygmy1) + minions.DevCount(Minions.Kind.Pygmy2) + minions.DevCount(Minions.Kind.Pygmy3) + minions.DevCount(Minions.Kind.Pygmy4);
            Plugin.Log.LogInfo($"[AutoTest] 召唤 矮人法杖：矮人 {pygmies} 个，占 {minions.SlotsUsed} 栏，增益 {PlayerBuffs.Name(PlayerBuffs.Pygmies)} x{minions.BuffCount(PlayerBuffs.Pygmies)}；6 秒掷矛 {TerraProjectiles.DevMade(ProjKind.PygmySpear) - spears0} 支（原版 每个 30 tick 一支，约 24），命中 {TerraProjectiles.DevHit(ProjKind.PygmySpear) - spearHits0}，" +
                               $"敌人泰拉血量 {pygmyLife0} -> {(target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0)}，毒液={venom}\n  {minions.DevDescribe()}");

            // --- The Desert Tiger grows with its gems (1-3, 4-6, 7+) and pounces from enemy to enemy.
            Reset();
            var tigerAt = Centre() + Vector2.right * 4f;
            Hold(tigerAt);
            yield return Use(ItemType.DesertTigerStaff, Centre() + Vector2.up * 2f, 4);
            yield return new WaitForSeconds(0.3f);
            string Tigers() => $"小 {minions.DevCount(Minions.Kind.Tiger1)} 中 {minions.DevCount(Minions.Kind.Tiger2)} 大 {minions.DevCount(Minions.Kind.Tiger3)}";
            string four = Tigers();
            yield return Use(ItemType.DesertTigerStaff, Centre() + Vector2.up * 2f, 3);
            int pounce0 = TerraProjectiles.DevMade(ProjKind.TigerPounce), pounceHit0 = TerraProjectiles.DevHit(ProjKind.TigerPounce);
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                Hold(tigerAt);
                if (t > 1.2f && t < 1.25f) Shot(scene, "s_tiger7");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 沙漠虎：4 颗宝石时 [{four}]（原版 中虎），7 颗时 [{Tigers()}]（原版 大虎），宝石 {minions.DevCount(Minions.Kind.TigerGem)} 占 {minions.SlotsUsed} 栏；扑击 {TerraProjectiles.DevMade(ProjKind.TigerPounce) - pounce0} 次（命中 {TerraProjectiles.DevHit(ProjKind.TigerPounce) - pounceHit0}）");

            // --- Abigail's flowers stack; the dragon grows.
            Reset();
            yield return Use(ItemType.AbigailsFlower, Centre() + Vector2.up * 2f, 3);
            yield return new WaitForSeconds(0.2f);
            Plugin.Log.LogInfo($"[AutoTest] 阿比盖尔的花×3：计数器 {minions.DevCount(Minions.Kind.AbigailCounter)}（占 {minions.SlotsUsed} 栏），阿比盖尔 {minions.DevCount(Minions.Kind.Abigail)}（原版 只有一个，不占栏），伤害 {minions.DevDamage(Minions.Kind.Abigail)}");
            Reset();
            yield return Use(ItemType.StardustDragonStaff, Centre() + Vector2.up * 2f, 3);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 星尘之龙×3：头 {minions.DevCount(Minions.Kind.DragonHead)} 身 {minions.DevCount(Minions.Kind.DragonBody1)}+{minions.DevCount(Minions.Kind.DragonBody2)} 尾 {minions.DevCount(Minions.Kind.DragonTail)}，占 {minions.SlotsUsed} 栏（原版 3）");
            Shot(scene, "s_dragon3");

            // --- The slot cap: an eighth slime frees the oldest; without the armor down to the cap.
            Reset();
            yield return Use(ItemType.SlimeStaff, Centre() + Vector2.right * 1.5f, 8);
            yield return new WaitForSeconds(0.2f);
            int eight = minions.DevCount(Minions.Kind.BabySlime);
            for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Armor[i] = default;
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 仆从栏：召唤 8 次史莱姆宝宝后 {eight} 只（上限 7），脱下蜘蛛套后上限 {Equipment.MaxMinions}，剩 {minions.DevCount(Minions.Kind.BabySlime)} 只（原版 多出的当场消失）");
            Equip(new[] { ItemType.SpiderMask, ItemType.SpiderBreastplate, ItemType.SpiderGreaves },
                new[] { ItemType.PygmyNecklace, ItemType.NecromanticScroll, ItemType.PapyrusScarab, ItemType.SummonerEmblem });

            // --- Right-click target.
            Reset();
            var aimAt = (Vector2)target.GetComponent<Collider2D>().bounds.center;
            mod.DevAimMinions(ItemType.HornetStaff, aimAt);
            bool picked = minions.Target == target;
            yield return new WaitForSeconds(0.7f);
            mod.DevAimMinions(ItemType.HornetStaff, aimAt);
            Plugin.Log.LogInfo($"[AutoTest] 右键指定目标：第一次 {(picked ? "选中该敌人" : "未选中")}，再点一次 {(minions.Target == null ? "取消" : "仍选中")}（原版 再点同一个取消）");

            // --- The buff bar and taking a buff away.
            Reset();
            yield return Use(ItemType.SlimeStaff, Centre() + Vector2.right, 2);
            yield return Use(ItemType.HornetStaff, Centre() + Vector2.up * 2f, 1);
            yield return Use(ItemType.OpticStaff, Centre() + Vector2.up * 2f, 1);
            yield return Use(ItemType.ImpStaff, Centre() + Vector2.up * 2f, 1);
            mod.PanicTicks = 480f;
            inv.Slots[slot] = ItemStack.Of(ItemType.HornetStaff);
            yield return new WaitForSeconds(0.4f);
            var bar = new System.Text.StringBuilder();
            for (int i = 0; i < PlayerBuffs.MaxBuffs && PlayerBuffs.Type[i] != 0; i++)
                bar.Append($"{PlayerBuffs.Name(PlayerBuffs.Type[i])}({PlayerBuffs.Type[i]}){(PlayerBuffs.IsMinionBuff(PlayerBuffs.Type[i]) ? " x" + minions.BuffCount(PlayerBuffs.Type[i]) : " " + PlayerBuffs.Duration(PlayerBuffs.Time[i]))} ");
            Plugin.Log.LogInfo($"[AutoTest] 增益栏：{bar}");
            Shot(scene, "s_buffbar");
            yield return null;
            int slimeBuff = System.Array.IndexOf(PlayerBuffs.Type, PlayerBuffs.BabySlime);
            bool removed = slimeBuff >= 0 && PlayerBuffs.TryRemoveAt(slimeBuff);
            yield return new WaitForSeconds(0.1f);
            Plugin.Log.LogInfo($"[AutoTest] 右键移除史莱姆宝宝增益：{removed}，0.1 秒后史莱姆宝宝 {minions.DevCount(Minions.Kind.BabySlime)} 只（原版 2 tick 后消失），黄蜂仍有 {minions.DevCount(Minions.Kind.Hornet)}");
            mod.PanicTicks = 0f;

            // --- A tag's damage on the Hornet's stingers; Firecracker's proc.
            Reset();
            var near = Centre() + Vector2.right * 3f;
            Hold(near);
            yield return Use(ItemType.HornetStaff, Centre() + Vector2.up * 2f, 1);
            yield return new WaitForSeconds(1.2f);
            int h0 = Minions.DevHits, d0 = Minions.DevHitDamage;
            for (float t = 0f; t < 3f; t += Time.deltaTime) { Hold(near); yield return null; }
            int hPlain = Minions.DevHits - h0, dPlain = Minions.DevHitDamage - d0;
            yield return Use(ItemType.LeatherWhip, near, 1);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { Hold(near); yield return null; }
            bool tagged = SummonTags.Tagged(target);
            h0 = Minions.DevHits;
            d0 = Minions.DevHitDamage;
            for (float t = 0f; t < 3f; t += Time.deltaTime) { Hold(near); yield return null; }
            int hTag = Minions.DevHits - h0, dTag = Minions.DevHitDamage - d0;
            Plugin.Log.LogInfo($"[AutoTest] 标记：黄蜂毒刺 无标记 {hPlain} 次 平均 {(hPlain > 0 ? dPlain / (float)hPlain : 0):F1}，皮鞭标记后（标记={tagged}）{hTag} 次 平均 {(hTag > 0 ? dTag / (float)hTag : 0):F1}（原版 +4），目标 {(minions.Target == target ? "已设为该敌人" : "未设")}");
            yield return Use(ItemType.Firecracker, near, 1);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { Hold(near); yield return null; }
            int max0 = Minions.DevMaxHit;
            Minions.DevMaxHit = 0;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime) { Hold(near); yield return null; }
            Plugin.Log.LogInfo($"[AutoTest] 鞭炮：皮鞭标记被换掉={!SummonTags.Tagged(target) || SummonTags.Active == WhipKind.Firecracker}，之后最大一击 {Minions.DevMaxHit}（无标记时最大 {max0}，原版 ×2.75），爆炸 {Minions.DevBlasts} 次");

            // --- Every whip on the enemy.
            var whips = new[] { ItemType.LeatherWhip, ItemType.Snapthorn, ItemType.SpinalTap, ItemType.Firecracker, ItemType.CoolWhip, ItemType.Durendal, ItemType.MorningStar,
                ItemType.SlimeWhip, ItemType.CobWhip, ItemType.DarkHarvest, ItemType.Kaleidoscope,
                ItemType.CorruptWhip, ItemType.CrimsonWhip, ItemType.MeteorWhip, ItemType.FlowerWhip, ItemType.EelWhip, ItemType.ConstellationWhip, ItemType.MoonLordWhip };
            foreach (var w in whips)
            {
                if (target == null || target.isDead) break;
                Reset();
                var def = ItemDb.Get(w);
                var at = Centre() + Vector2.right * 3f;
                Hold(at);
                int s0 = Whips.DevStrikes;
                int life0 = EnemyStats.Life(target, EnemyStats.Of(target));
                int anim = Mathf.Max(1, (int)(Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()));
                yield return Use(w, at, 1);
                bool pictured = false;
                for (float t = 0f; t < anim / 60f + 0.3f; t += Time.deltaTime)
                {
                    Hold(at);
                    if (!pictured && t >= anim / 60f * 0.62f)
                    {
                        pictured = true;
                        Shot(scene, "s_" + w);
                    }
                    yield return null;
                }
                int life1 = target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0;
                var buffs = new System.Text.StringBuilder();
                foreach (int b in new[] { PlayerBuffs.SwordWhip, PlayerBuffs.ThornWhip, PlayerBuffs.CoolWhip, PlayerBuffs.ScytheWhip, PlayerBuffs.CobWhip })
                    if (PlayerBuffs.Has(b)) buffs.Append(PlayerBuffs.Name(b)).Append(' ');
                var debuffs = new System.Text.StringBuilder();
                foreach (var (b, n) in new[] { (EnemyDebuffs.Hellfire, "狱火"), (EnemyDebuffs.Frostbite, "寒霜"), (EnemyDebuffs.Poisoned, "中毒"), (EnemyDebuffs.Slimed, "黏液"),
                             (EnemyDebuffs.OnFire, "着火"), (EnemyDebuffs.ScytheMark, "收割印记"), (EnemyDebuffs.EelMark, "电鳗印记") })
                    if (EnemyDebuffs.Has(target, b)) debuffs.Append(n).Append(' ');
                Plugin.Log.LogInfo($"[AutoTest] 鞭子 {def.Name}（面板 {Equipment.Damage(def)}，挥动 {anim} tick）：命中 {Whips.DevStrikes - s0} 次，敌人泰拉血量 {life0} -> {life1}，标记={SummonTags.Tagged(target)}，减益 [{debuffs.ToString().Trim()}]，玩家增益 [{buffs.ToString().Trim()}]，雪花 {minions.DevCount(Minions.Kind.Snowflake)}，小蜘蛛 {minions.DevCount(Minions.Kind.CobSpider)}，召唤物目标={(minions.Target == target)}");
            }

            // --- The tags that act on minion hits: Dark Harvest's lightning, Kaleidoscope's stars; the Cob Whip's spider biting.
            foreach (var w in new[] { ItemType.DarkHarvest, ItemType.Kaleidoscope, ItemType.CobWhip, ItemType.MeteorWhip, ItemType.FlowerWhip, ItemType.EelWhip,
                         ItemType.ConstellationWhip, ItemType.MoonLordWhip })
            {
                if (target == null || target.isDead) break;
                Reset();
                var at = Centre() + Vector2.right * 3f;
                Hold(at);
                yield return Use(ItemType.HornetStaff, Centre() + Vector2.up * 2f, 1);
                yield return new WaitForSeconds(1f);
                int bolts0 = TerraProjectiles.DevMade(ProjKind.ScytheLightning), boltHits0 = TerraProjectiles.DevHit(ProjKind.ScytheLightning);
                var kinds = new[] { ProjKind.WhipMeteor, ProjKind.FlowerPetal, ProjKind.ConstellationStar, ProjKind.PhantomSlash };
                var made0 = System.Array.ConvertAll(kinds, k => TerraProjectiles.DevMade(k));
                var hit0 = System.Array.ConvertAll(kinds, k => TerraProjectiles.DevHit(k));
                int mh0 = Minions.DevHits, strikes0 = Whips.DevStrikes;
                Minions.DevMaxHit = 0;
                yield return Use(w, at, 1);
                bool pic = false, again = false;
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    Hold(at);
                    // The Constellation: the minions grow the energy, a second crack releases the stars.
                    if (w == ItemType.ConstellationWhip && !again && t > 1.5f)
                    {
                        again = true;
                        yield return Use(w, at, 1);
                    }
                    if (!pic && t > (w == ItemType.ConstellationWhip ? 1.85f : w == ItemType.MoonLordWhip ? 0.3f : 1.2f))
                    {
                        pic = true;
                        Shot(scene, "s_tag_" + w);
                    }
                    yield return null;
                }
                var extra = new System.Text.StringBuilder();
                for (int k = 0; k < kinds.Length; k++)
                {
                    int made = TerraProjectiles.DevMade(kinds[k]) - made0[k];
                    if (made > 0) extra.Append($"，{kinds[k]} {made} 个（命中 {TerraProjectiles.DevHit(kinds[k]) - hit0[k]}）");
                }
                Plugin.Log.LogInfo($"[AutoTest] 鞭子标记效果 {ItemDb.Get(w).Name}：鞭击 {Whips.DevStrikes - strikes0} 次，黄蜂命中 {Minions.DevHits - mh0} 次（最大一击 {Minions.DevMaxHit}），黑暗闪电 {TerraProjectiles.DevMade(ProjKind.ScytheLightning) - bolts0} 道（命中 {TerraProjectiles.DevHit(ProjKind.ScytheLightning) - boltHits0}），小蜘蛛 {minions.DevCount(Minions.Kind.CobSpider)}，标记={SummonTags.Tagged(target)}，电鳗印记={EnemyDebuffs.Has(target, EnemyDebuffs.EelMark)}{extra}，鞭速 {Mathf.Max(1, (int)(Mathf.Round(ItemDb.Get(w).UseAnimation * 60f) * Equipment.WhipScale()))} tick");
            }
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 鞭速：荆鞭增益下 {Mathf.Max(1, (int)(30 * Equipment.WhipScale()))} tick（若有丛林之怒增益原版 26）；黑曜石套装加 15% 另算");

            Reset();
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSelected;
            for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Armor[i] = default;
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = default;
            mod.TrySwitchLoadout(0);
            mod.DevNoFallDamage = false;
        }
    }
}
