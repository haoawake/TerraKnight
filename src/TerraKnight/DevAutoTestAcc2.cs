using System.Collections;
using System.IO;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "acc2"): the hardmode accessories (accarmor-hm sec-G2, sec-G3) against
    /// Terraria's numbers: the stat blocks (sky stones by day and night, the werewolf, the ice
    /// barrier, the chisel), what a hit sets off (each cloak's stars, the strong bees, the
    /// dodges), the timed projectiles (spores, gel, bones, shadow hands), the hit-time ones
    /// (snapping stones, barbs, the Lucky Coin, the Mana Cloak's star and its pickup, the Harpy
    /// Charm), the whip tags (stacking, duration, the Snake Band's echo), the sentry sling, ammo
    /// cycling and its toggle, the scope's pan, the Ram Rune's dash, the Stress Ball; and
    /// close-up pictures of the new draw layers.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// A spot in the scene to stand with a clear line <paramref name="span"/> units long to one
        /// side at chest height and floor under it, the nearest one to <paramref name="near"/> (or the
        /// first found bottom-up).
        /// </summary>
        static bool OpenRun(HeroController hero, float span, out Vector2 at, out float dir, Vector2? near = null)
        {
            bool found = false;
            float best = float.MaxValue;
            at = Vector2.zero;
            dir = 1f;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            var gm = GameManager.instance;
            var size = hero.GetComponent<BoxCollider2D>().bounds.size;
            for (float y = 4f; y < gm.sceneHeight - 2f; y += 1f)
                for (float x = 2f; x < gm.sceneWidth - 2f; x += 1f)
                {
                    var p = new Vector2(x, y);
                    if (World.TerraWorld.IsSolidAt(p)) continue;
                    if (Physics2D.OverlapBox(p, (Vector2)size + new Vector2(0.4f, 0.4f), 0f, terrain) != null) continue;
                    var floor = Physics2D.Raycast(p, Vector2.down, 3f, terrain);
                    if (floor.collider == null) continue;
                    foreach (float d in new[] { 1f, -1f })
                    {
                        var end = p + Vector2.right * d * span;
                        if (end.x < 1f || end.x > gm.sceneWidth - 1f) continue;
                        if (Physics2D.Linecast(p, end, terrain).collider != null || Physics2D.Linecast(p + Vector2.up * 1.5f, end + Vector2.up * 1.5f, terrain).collider != null) continue;
                        // Clear in the Terraria cells too (the arrows' and the charm's sight).
                        if (!World.TerraWorld.LineClearPx(Effects.TerraParticles.ToPx(p), Effects.TerraParticles.ToPx(end))
                            || !World.TerraWorld.LineClearPx(Effects.TerraParticles.ToPx(p + Vector2.up * 1.5f), Effects.TerraParticles.ToPx(end + Vector2.up * 1.5f))) continue;
                        var spot = new Vector2(x, floor.point.y + size.y * 0.5f + 0.05f);
                        float dist = near.HasValue ? Vector2.Distance(spot, near.Value) : 0f;
                        if (dist < best)
                        {
                            best = dist;
                            at = spot;
                            dir = d;
                            found = true;
                        }
                        if (!near.HasValue) return true;
                        break;
                    }
                }
            return found;
        }

        IEnumerator Acc2Test(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            var savedArmor = (ItemStack[])inv.Armor.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            // Ammo for the bows and guns.
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 999);
            inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.MusketBall, 999);

            // --- The items themselves.
            int n = 0, drawn = 0;
            foreach (var d in ItemDb.All)
            {
                if (!d.IsAccessory) continue;
                n++;
                if (d.Shoe + d.Waist + d.Balloon + d.Wing + d.Shield + d.Neck + d.Back + d.HandOn + d.HandOff + d.Face + d.Front > 0) drawn++;
            }
            Plugin.Log.LogInfo($"[AutoTest] 饰品2：饰品共 {n} 件（{drawn} 件画在身上）；例：{ItemDb.Get(ItemType.AnkhShield).Name} 防御 {ItemDb.Get(ItemType.AnkhShield).Defense}，" +
                               $"{ItemDb.Get(ItemType.CelestialShell).Name} 阶段 {ItemDb.Get(ItemType.CelestialShell).Stage} 价值 {ItemDb.Get(ItemType.CelestialShell).Value}，" +
                               $"{ItemDb.Get(ItemType.ChaosCylinderRandom).Name} 价值 {ItemDb.Get(ItemType.ChaosCylinderRandom).Value}");

            // --- Stat blocks.
            Wear(ItemType.SilverBracer, ItemType.MobiusStrip, ItemType.WickedArmlet);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 标记手环：标记上限 1+{Equipment.Current.MaxTagEffects}（原版 4），持续 ×{1f + Equipment.Current.TagDuration}（原版 3），鞭子范围 +{Equipment.Current.WhipRange * 100f:F0}%（原版 10）");
            Wear(ItemType.TwilightGrasp, ItemType.SilverBracer);
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 暮光之握+银护腕：标记上限 1+{Equipment.Current.MaxTagEffects}（原版 3，同旗不叠）");
            Wear(ItemType.CelestialStone, ItemType.AncientChisel);
            yield return null;
            var b = Equipment.Current;
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 天界石+远古凿子：近战伤害 +{b.MeleeDamage * 100f:F0}%，近战速度 +{b.MeleeSpeed * 100f:F0}%，暴击 +{b.MeleeCrit * 100f:F0}，防御 {b.Defense}，再生 {b.LifeRegen}，" +
                               $"仆从击退 +{b.MinionKB}，挖矿用时 ×{Equipment.PickTime:F2}（原版 10/10/2/4/2/0.5/0.60）");
            World.TerraTime.DevForceDay = true;
            Wear(ItemType.SunStone, ItemType.MoonCharm);
            yield return null;
            float sunDay = Equipment.Current.MeleeDamage;
            bool wolfDay = Equipment.WerewolfNow;
            World.TerraTime.DevForceDay = false;
            yield return null;
            yield return null;
            float sunNight = Equipment.Current.MeleeDamage;
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 太阳石+月光护身符：白天近战伤害 +{sunDay * 100f:F1}%、狼人={wolfDay}；夜里 +{sunNight * 100f:F1}%（狼人 5.1）、狼人={Equipment.WerewolfNow}，" +
                               $"防御 {Equipment.Current.Defense}（原版 3），狼人增益={PlayerBuffs.Has(PlayerBuffs.Werewolf)}；时钟 {World.TerraTime.Now:F0}s");
            yield return new WaitForSeconds(0.3f);
            yield return CloseUp(scene, "a2_werewolf", hero);
            World.TerraTime.DevForceDay = null;
            Wear(ItemType.FrozenTurtleShell, ItemType.WormScarf);
            mod.Stats.Life = Mathf.RoundToInt(mod.Stats.LifeMax * 0.4f);
            yield return null;
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 冰冻海龟壳：40% 生命时减伤 {Equipment.Current.Endurance:F2}（原版 0.17+0.25），冰障增益={PlayerBuffs.Has(PlayerBuffs.IceBarrier)}");
            yield return new WaitForSeconds(0.3f);
            yield return CloseUp(scene, "a2_ice_barrier", hero);
            mod.Stats.Life = mod.Stats.LifeMax;
            Wear(ItemType.StressBall);
            yield return null;
            bool still = Equipment.AutoReuses(ItemDb.Get(ItemType.Musket));
            Wear();
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 解压球：站着不动时火枪连发={still}，摘下={Equipment.AutoReuses(ItemDb.Get(ItemType.Musket))}");

            // --- What a hit sets off.
            PlayerData.instance.isInvincible = false;
            var hitter = new GameObject("Dev Hitter");
            IEnumerator Hit(int dmg = 20)
            {
                hero.cState.invulnerable = false;
                mod.Stats.Life = mod.Stats.LifeMax;
                hitter.transform.position = hero.transform.position + Vector3.left;
                PlayerHooks.HurtPlayer(hero, dmg, 1, hitter);
                yield return null;
            }
            foreach (var (acc, kind, label) in new[]
            {
                (ItemType.StarVeil, ProjKind.StarVeilStar, "星星面纱"), (ItemType.BeeCloak, ProjKind.BeeCloakStar, "蜜蜂斗篷"),
                (ItemType.ManaCloak, ProjKind.ManaCloakStar, "魔力斗篷"), (ItemType.DruidicSerpentCloak, ProjKind.SnakeCloakStar, "德鲁伊蛇纹斗篷"),
            })
            {
                Wear(acc, ItemType.HiveBackpack);
                yield return null;
                int s0 = TerraProjectiles.DevMade(kind), b0 = TerraProjectiles.DevMade(ProjKind.Bee), g0 = TerraProjectiles.DevMade(ProjKind.GiantBee);
                yield return Hit();
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 受伤 {label}+蜂巢背包：{kind}×{TerraProjectiles.DevMade(kind) - s0}（原版 3），小蜜蜂 {TerraProjectiles.DevMade(ProjKind.Bee) - b0}，大蜜蜂 {TerraProjectiles.DevMade(ProjKind.GiantBee) - g0}");
                yield return new WaitForSeconds(0.45f);
                Shot(scene, "a2_stars_" + acc);
                yield return new WaitForSeconds(0.9f);
            }
            foreach (var (acc, label, expect) in new[] { (ItemType.BrainOfConfusion, "混乱之脑", "约 1/6，增益在时不闪"), (ItemType.MysticArtsSash, "秘术腰带", "约 1/10") })
            {
                Wear(acc);
                yield return null;
                int d0 = TerraHurt.DevDodges, ill0 = TerraProjectiles.DevMade(ProjKind.BrainIllusion), tries = 60;
                for (int k = 0; k < tries; k++)
                {
                    if (acc == ItemType.BrainOfConfusion) PlayerBuffs.Remove(PlayerBuffs.BrainOfConfusion);
                    yield return Hit();
                    if (k == 0 || TerraHurt.BrainDodgeTicks > 290)
                    {
                        if (acc == ItemType.BrainOfConfusion && TerraHurt.BrainDodgeTicks > 280) yield return CloseUp(scene, "a2_brain_dodge", hero, 160, 2);
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 {label}：{tries} 次受击闪避 {TerraHurt.DevDodges - d0} 次（{expect}），幻象 {TerraProjectiles.DevMade(ProjKind.BrainIllusion) - ill0}，控脑术增益={PlayerBuffs.Has(PlayerBuffs.BrainOfConfusion)}");
                if (acc == ItemType.MysticArtsSash) Shot(scene, "a2_sash");
                yield return new WaitForSeconds(0.5f);
            }
            Object.Destroy(hitter);
            PlayerData.instance.isInvincible = true;
            hero.cState.invulnerable = false;
            PlayerBuffs.Remove(PlayerBuffs.BrainOfConfusion);

            // --- An enemy to work on.
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
                Plugin.Log.LogInfo("[AutoTest] 饰品2：场景里没有敌人，跳过命中部分");
            }
            else
            {
                var trb = target.GetComponent<Rigidbody2D>();
                void Hold(Vector2 at)
                {
                    target.hp = Mathf.Max(target.hp, 3000);
                    Pin(target, trb, at);
                }
                void Reset()
                {
                    Minions.Instance.Clear();
                    PlayerBuffs.Clear();
                    SummonTags.Clear();
                    EnemyDebuffs.Clear();
                    Whips.Instance.Clear();
                    TerraProjectiles.Instance.DevClear();
                    AccessoryEffects.SnapCooldown = 0;
                }
                IEnumerator Watch(float seconds, Vector2 at, System.Action each = null)
                {
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold(at);
                        each?.Invoke();
                        yield return null;
                    }
                }
                IEnumerator UseFor(ItemType item, float seconds, Vector2 at, System.Func<Vector2> aim)
                {
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold(at);
                        if (mod.DevReady) mod.DevUse(item, aim());
                        yield return null;
                    }
                }
                // The target stands 2 units off on the side with more room, its centre in the open
                // (the timed shots need a clear line from the player to it).
                const int terrain = 1 << World.ChunkColliders.TerrainLayer;
                float Room(float d) { var h = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return h.collider != null ? h.distance : 30f; }
                float side = Room(1f) >= Room(-1f) ? 1f : -1f;
                var near = C() + Vector2.right * side * 2f;

                // Timed projectiles.
                Reset();
                Wear(ItemType.SporeSac, ItemType.VolatileGelatin, ItemType.BoneHelm);
                yield return Watch(6f, near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 6 秒：孢子 {TerraProjectiles.DevMade(ProjKind.Spore)}（1/15 每 tick 起，至多 10），挥发明胶 {TerraProjectiles.DevMade(ProjKind.GelBall)}（41 tick 一个，约 8），暗影之手 {TerraProjectiles.DevMade(ProjKind.ShadowHand)}（20-100 tick 一个）");
                Shot(scene, "a2_spores_gel_hands");
                Reset();
                Wear(ItemType.BoneGlove);
                int bones0 = AccessoryEffects.DevBones;
                yield return UseFor(ItemType.CopperBroadsword, 3.1f, near, () => near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 骨头手套：挥剑 3 秒扔骨头 {AccessoryEffects.DevBones - bones0}（原版每 60 tick 一个，约 3），骨头命中 {TerraProjectiles.DevHit(ProjKind.BoneGloveBone)}");

                // The snapping stones.
                Reset();
                Wear(ItemType.SnappingStone);
                int snaps0 = AccessoryEffects.DevSnaps;
                yield return UseFor(ItemType.Minishark, 1.5f, near, () => near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 碎裂之石：迷你鲨射击 1.5 秒打出脉冲 {AccessoryEffects.DevSnaps - snaps0}（6 秒一次，原版 1），冷却 {AccessoryEffects.SnapCooldown}");
                Shot(scene, "a2_snapping_stone");
                Reset();
                Wear(ItemType.Pyroclast);
                snaps0 = AccessoryEffects.DevSnaps;
                bool hell = false;
                yield return UseFor(ItemType.CopperBroadsword, 1f, near, () => near);
                hell = target != null && EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 火山碎屑石：挥剑斩击 {AccessoryEffects.DevSnaps - snaps0}（原版 1），地狱火={hell}");
                Shot(scene, "a2_pyroclast");
                Reset();
                Wear(ItemType.ArmletOfRuin);
                snaps0 = AccessoryEffects.DevSnaps;
                mod.DevUse(ItemType.LeatherWhip, near);
                yield return Watch(0.5f, near);
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(ItemType.HornetStaff);
                inv.Selected = Inventory.HotbarSize - 1;
                mod.DevAim = near + Vector2.up;
                mod.DevUseSelected(near + Vector2.up);
                mod.DevAim = null;
                yield return Watch(4f, near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 毁灭臂环：带标记的仆从命中引发远古之力 {AccessoryEffects.DevSnaps - snaps0}（原版 1）");

                // Barbs, the Catalyst Band, the Lucky Coin.
                Reset();
                Wear(ItemType.CatalystBand, ItemType.LuckyCoin);
                bool poison = false, accel = false;
                int coins0 = AccessoryEffects.DevCoins;
                yield return UseFor(ItemType.WoodenBow, 1.2f, near, () => near);
                if (target != null) { poison = EnemyDebuffs.Has(target, EnemyDebuffs.Poisoned); accel = EnemyDebuffs.Has(target, EnemyDebuffs.AcceleratePoisons); }
                yield return UseFor(ItemType.CopperBroadsword, 3f, near, () => near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 催化手环+幸运币：箭中毒={poison}，加速负面效果={accel}，钱币 {AccessoryEffects.DevCoins - coins0} 个，钱 {Shop.Purse}");

                // The Mana Cloak's star off a magic hit, and picking it up.
                Reset();
                Wear(ItemType.ManaCloak);
                int stars0 = TerraProjectiles.DevMade(ProjKind.ManaCloakStar), got0 = TerraProjectiles.DevManaStars;
                mod.Stats.Mana = 0;
                yield return UseFor(ItemType.WandofSparking, 0.5f, near, () => near);
                yield return Watch(4f, near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 魔力斗篷：魔法命中招来星星 {TerraProjectiles.DevMade(ProjKind.ManaCloakStar) - stars0}（300 tick 一颗），拾到魔力星 {TerraProjectiles.DevManaStars - got0}，魔力 {mod.Stats.Mana}，魔力涌动={PlayerBuffs.Has(PlayerBuffs.ManaHeat)}");

                // The Harpy Charm: arrows over 400 px off turn at the enemy.
                // It wants an arrow over 400 px out that still sees the enemy: a long open run somewhere in the scene.
                Reset();
                Wear(ItemType.PhoenixQuiver);
                int harpy0 = TerraProjectiles.DevHarpy;
                var back = (Vector2)hero.transform.position;
                if (OpenRun(hero, 22f, out var runAt, out float runDir, back))
                {
                    hero.transform.position = Clear(hero, runAt);
                    rb.linearVelocity = Vector2.zero;
                    yield return Watch(0.4f, C() + Vector2.right * 30f);
                    var far = C() + Vector2.right * runDir * 18f;
                    yield return UseFor(ItemType.WoodenBow, 1.6f, far, () => far + Vector2.up * 1.5f);
                    Shot(scene, "a2_phoenix");
                    yield return UseFor(ItemType.WoodenBow, 1.4f, far, () => far + Vector2.up * 1.5f);
                    yield return Watch(1f, far);
                    Plugin.Log.LogInfo($"[AutoTest] 饰品2 凤凰箭袋：在 {runAt} 朝 {runDir} 射，箭被引向敌人 {TerraProjectiles.DevHarpy - harpy0} 次（90 tick 一次），地狱火={(target != null && EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire))}，木箭变火焰箭 {TerraProjectiles.DevMade(ProjKind.FlamingArrow)}");
                    hero.transform.position = back;
                    rb.linearVelocity = Vector2.zero;
                    yield return Watch(0.4f, near);
                }
                else Plugin.Log.LogInfo("[AutoTest] 饰品2 凤凰箭袋：场景里找不到 22 格长的开阔处，跳过");

                // Whip tags: three whips at once, three times as long; the Snake Band's echo.
                Reset();
                Wear(ItemType.SilverBracer, ItemType.MobiusStrip);
                foreach (var w in new[] { ItemType.LeatherWhip, ItemType.Snapthorn, ItemType.SpinalTap })
                {
                    while (!mod.DevReady) yield return null;
                    mod.DevUse(w, near);
                    yield return Watch(0.7f, near);
                }
                int liveWhips = SummonTags.LiveWhips;
                yield return Watch(4.5f, near);
                bool stillTagged = SummonTags.Tagged(target);
                Wear();
                yield return Watch(0.1f, near);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 银护腕+莫比乌斯带：三条鞭子的标记同时生效 {liveWhips}（原版 3），6 秒后仍有标记={stillTagged}（原版 720 tick），摘下后 {SummonTags.LiveWhips}（1）");
                Reset();
                Wear(ItemType.SnakeBand);
                int echo0 = Whips.DevEchoes;
                Whips.DevEchoLog = "";
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(ItemType.LeatherWhip);
                inv.Selected = Inventory.HotbarSize - 1;
                // The band in effect, and 120 ticks since the last swing, so the count starts over.
                yield return Watch(2.2f, near);
                for (int k = 0; k < 4; k++)
                {
                    while (!mod.DevReady) yield return null;
                    mod.DevAim = near;
                    mod.DevUseSelected(near);
                    yield return Watch(0.15f, near, null);
                    if (k == 0) yield return Watch(0.2f, near, () => { });
                    if (k == 0) Shot(scene, "a2_snake_band");
                    yield return Watch(0.6f, near);
                }
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 蛇形手环：挥鞭 4 次第二条鞭子 {Whips.DevEchoes - echo0} 次（原版第 1、4 次，2）；{Whips.DevEchoLog}");

                // The Ram Rune: up, then down and jump.
                Reset();
                Wear(ItemType.DeadCellsRamRune);
                int dash0 = TerraMovement.DevDownDashes, pound0 = TerraMovement.DevPounds;
                rb.linearVelocity = new Vector2(0f, 14f);
                yield return new WaitForSeconds(0.35f);
                TerraMovement.DevDown = true;
                TerraMovement.DevDownDash = true;
                bool pictured = false;
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    Hold(C() + Vector2.right * 1.5f);
                    if (!pictured && TerraMovement.DownDashing && t > 0.05f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, "a2_ram_dash", hero, 140, 2);
                    }
                    if (TerraMovement.DevPounds > pound0) break;
                    yield return null;
                }
                TerraMovement.DevDown = false;
                yield return Watch(0.2f, C() + Vector2.right * 1.5f);
                Plugin.Log.LogInfo($"[AutoTest] 饰品2 重锤符文：下冲 {TerraMovement.DevDownDashes - dash0}，砸地 {TerraMovement.DevPounds - pound0}，踩踏 {TerraMovement.DevStomps}");
                Shot(scene, "a2_ram_pound");
                Pin(target, trb, C() + Vector2.right * 30f);
            }

            // --- The sentry sling.
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.4f);
            Wear(ItemType.HeavySling);
            Minions.Instance.Clear();
            int slung0 = Minions.DevSlung;
            bool placed = Minions.Instance.Summon(ItemDb.Get(ItemType.BallistaCane), C() + Vector2.right * 0.3f);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 重型投石索：放下弩车={placed}，背上 {Minions.DevSlung - slung0} 座（原版碰到即背上），哨兵 {Minions.Instance.Count}");
            yield return CloseUp(scene, "a2_sling", hero);
            Wear();
            yield return new WaitForSeconds(0.2f);
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 摘下投石索：哨兵剩 {Minions.Instance.Count}（原版背着的消失）");

            // --- Ammo cycling and the toggle.
            Wear(ItemType.ChaosCylinder);
            for (int i = Inventory.Size - Inventory.Columns; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 50);
            inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.FlamingArrow, 50);
            yield return null;
            int wood0 = TerraProjectiles.DevMade(ProjKind.WoodenArrow), fire0 = TerraProjectiles.DevMade(ProjKind.FlamingArrow);
            for (int k = 0; k < 6; k++)
            {
                while (!mod.DevReady) yield return null;
                mod.DevUse(ItemType.WoodenBow, C() + Vector2.right * 4f);
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 混沌弹筒：6 箭 木箭 {TerraProjectiles.DevMade(ProjKind.WoodenArrow) - wood0}、火焰箭 {TerraProjectiles.DevMade(ProjKind.FlamingArrow) - fire0}（轮流，3/3）");
            inv.Slots[10] = ItemStack.Of(ItemType.ChaosCylinder);
            var seq = new System.Text.StringBuilder(ItemDb.Get(inv.Slots[10].Type).Name);
            for (int k = 0; k < 4; k++)
            {
                InventoryGui.DevRightClick(10);
                seq.Append(" -> ").Append(ItemDb.Get(inv.Slots[10].Type).Name);
            }
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 混沌弹筒右键切换：{seq}");

            // --- The scope's pan.
            Wear(ItemType.RifleScope);
            inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(ItemType.Minishark);
            inv.Selected = Inventory.HotbarSize - 1;
            TerraCamera.DevPan = new Vector2(4f, 1f);
            yield return new WaitForSeconds(0.6f);
            Plugin.Log.LogInfo($"[AutoTest] 饰品2 步枪瞄准镜：视野平移 {TerraCamera.Pan:F2}（目标 (4,1)，每帧 36 px）");
            Shot(scene, "a2_scope_pan");
            TerraCamera.DevPan = null;
            yield return new WaitForSeconds(0.4f);

            // --- Close-ups of the new draw layers.
            var looks = new (string Name, ItemType[] Armor, ItemType[] Acc)[]
            {
                ("faces", new ItemType[0], new[] { ItemType.Blindfold, ItemType.ObsidianRose, ItemType.SilverShield, ItemType.SilverBracer, ItemType.StarVeil }),
                ("diving", new[] { ItemType.MiningHelmet }, new[] { ItemType.ArcticDivingGear, ItemType.AnkhShield, ItemType.TwilightGrasp, ItemType.JellyfishNecklace, ItemType.TreasureMagnet }),
                ("bonehelm", new ItemType[0], new[] { ItemType.BoneHelm, ItemType.HeroShield, ItemType.CatalystBand, ItemType.SweetheartNecklace, ItemType.MysticArtsSash }),
                ("skull", new ItemType[0], new[] { ItemType.LavaSkull, ItemType.PaladinsShield, ItemType.BoneGlove, ItemType.CrossedHeartNecklace, ItemType.PhoenixQuiver }),
                ("manacloak", new ItemType[0], new[] { ItemType.ManaCloak, ItemType.ArcaneFlower, ItemType.SunStone, ItemType.FrozenShield, ItemType.SeraphNecklace }),
                ("druid", new ItemType[0], new[] { ItemType.DruidicSerpentCloak, ItemType.ReflectiveShades, ItemType.HandWarmer, ItemType.RestorationShield, ItemType.StingerNecklace }),
                ("backpack", new ItemType[0], new[] { ItemType.HiveBackpack, ItemType.AngelWings, ItemType.CharmofMyths, ItemType.SpectreGoggles, ItemType.BeeCloak }),
            };
            foreach (var look in looks)
            {
                Equip(look.Armor, look.Acc);
                yield return new WaitForSeconds(0.25f);
                yield return CloseUp(scene, "a2_look_" + look.Name, hero);
            }

            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = savedSlots[i];
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            for (int i = 0; i < Inventory.ArmorSlots; i++) inv.Armor[i] = savedArmor[i];
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }

        /// <summary>A close-up of the player: the screen round him cut out and blown up (each pixel 3×3).</summary>
        IEnumerator CloseUp(string scene, string name, HeroController hero, int halfPx = 90, int zoom = 3)
        {
            yield return new WaitForEndOfFrame();
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : Camera.main;
            if (cam == null || hero == null) yield break;
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var sp = cam.WorldToScreenPoint(hero.GetComponent<BoxCollider2D>().bounds.center);
            int cx = (int)sp.x, cy = (int)sp.y, w = halfPx * 2;
            var outTex = new Texture2D(w * zoom, w * zoom, TextureFormat.RGB24, false);
            var px = new Color[w * zoom * w * zoom];
            for (int y = 0; y < w * zoom; y++)
                for (int x = 0; x < w * zoom; x++)
                    px[y * w * zoom + x] = tex.GetPixel(Mathf.Clamp(cx - halfPx + x / zoom, 0, tex.width - 1), Mathf.Clamp(cy - halfPx + y / zoom, 0, tex.height - 1));
            outTex.SetPixels(px);
            outTex.Apply();
            string path = Path.Combine(outDir, $"{scene}_{name}.png");
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(outTex);
            Plugin.Log.LogInfo($"[AutoTest] 截图 {path}");
        }
    }
}
