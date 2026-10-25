using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "r8"): the 10-21 batch (the Arc Surge, the Blood Rain Bow, the Blood Hamaxe, The Undertaker, the Storm
    /// Spear, the two jousting lances, the Resonance Scepter, the Toolbelt and the Toolbox): their numbers, where they come
    /// from (the drops, the shops, the crates), their reach bonuses, and each weapon used on Dirtmouth's Target Dummy at its
    /// stage's defense, Master: its shots, its hits, its damage a second; the lances at rest and charging, knocked away by a
    /// hit; pictures of the bolts, the lance, the ring and the blood.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Batch1021Test()
        {
            charmPass = charmAll = 0;
            charmTag = "r8";
            PlayerData.instance.isInvincible = true;
            TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            int stageWas = Shop.DevStage;
            var difficultyWas = GameDifficulty.Current;
            try
            {
                // ---- Numbers.
                (ItemType Type, int Stage, ItemSource Source)[] all =
                {
                    (ItemType.ArcSurge, 5, ItemSource.Drop), (ItemType.BloodRainBow, 1, ItemSource.Drop), (ItemType.BloodHamaxe, 4, ItemSource.Drop),
                    (ItemType.TheUndertaker, 1, ItemSource.Drop), (ItemType.ThunderSpear, 1, ItemSource.Drop), (ItemType.JoustingLance, 3, ItemSource.Shop),
                    (ItemType.ShadowJoustingLance, 5, ItemSource.Drop), (ItemType.PrincessWeapon, 5, ItemSource.Shop), (ItemType.Toolbelt, 0, ItemSource.Shop),
                    (ItemType.Toolbox, 1, ItemSource.Shop),
                };
                foreach (var (t, stage, source) in all)
                {
                    var d = ItemDb.Get(t);
                    bool art = d != null && !d.Placeholder && Terraria.TerrariaAssets.Texture(d.Texture) != null;
                    CharmCheck($"物品 {d?.Name}", art && d.Stage == stage && d.Source == source && d.Price > 0,
                        $"贴图 {d?.Texture} 阶段 {d?.Stage} 来源 {d?.Source} 价格 {d?.Price} 伤害 {d?.Damage} 稀有度 {d?.Rare}");
                }
                var hamaxe = ItemDb.Get(ItemType.BloodHamaxe);
                CharmCheck("血锤斧 锤力 80 斧力 30", hamaxe.HammerPower == 80 && hamaxe.AxePower == 30, $"{hamaxe.HammerPower}/{hamaxe.AxePower}");
                CharmCheck("骑枪 按住", ItemDb.Get(ItemType.JoustingLance).Channel && ItemDb.Get(ItemType.ShadowJoustingLance).Channel);
                var arcDef = ItemDb.Get(ItemType.ArcSurge);
                CharmCheck("电弧涌动 一次三发", Mathf.Abs(arcDef.UseTime * 60f - 6f) < 0.1f && Mathf.Abs(arcDef.UseAnimation * 60f - 18f) < 0.1f && arcDef.Mana == 18,
                    $"使用 {arcDef.UseTime * 60f:F0}/{arcDef.UseAnimation * 60f:F0} 帧，耗魔 {arcDef.Mana}");

                // ---- Sources.
                TownNpcs.DevStockAt("GoblinTinkerer", 0, ItemType.Toolbelt, out bool belt);
                TownNpcs.DevStockAt("GoblinTinkerer", 1, ItemType.Toolbox, out bool box);
                TownNpcs.DevStockAt("BestiaryGirl", 3, ItemType.JoustingLance, out bool lance);
                TownNpcs.DevStockAt("BestiaryGirl", 2, ItemType.JoustingLance, out bool lanceEarly);
                TownNpcs.DevStockAt("Princess", 5, ItemType.PrincessWeapon, out bool scepter);
                CharmCheck("商店 哥布林 工具腰带/工具箱", belt && box);
                CharmCheck("商店 动物学家 骑枪（困难模式）", lance && !lanceEarly);
                CharmCheck("商店 公主 共鸣权杖", scepter);
                foreach (var (record, stage, t) in new[]
                {
                    ("GrassHopper", 1, ItemType.BloodRainBow), ("SnapperTrap", 1, ItemType.BloodRainBow), ("MossmanRunner", 1, ItemType.TheUndertaker),
                    ("SuperSpitter", 4, ItemType.BloodHamaxe), ("PalaceFly", 5, ItemType.ShadowJoustingLance), ("NailBros", 5, ItemType.ArcSurge),
                })
                {
                    var row = EnemyDrops.At(record, stage, false, false, false).FirstOrDefault(x => x.Type == t);
                    CharmCheck($"掉落 {record} {ItemDb.Get(t).Name}", row.Type == t, row.Type == t ? $"{row.Num}/{row.Den}" : "没有");
                }
                var crates = typeof(Fishing);
                var oasis = crates.GetField("oasis", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) as ItemType[];
                var crimson = crates.GetField("crimson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null) as ItemType[];
                CharmCheck("匣子 风暴长矛（白灰匣）/夺命枪（猩红匣）", oasis != null && oasis.Contains(ItemType.ThunderSpear) && crimson != null && crimson.Contains(ItemType.TheUndertaker));

                // ---- Reach.
                var keepAcc = (ItemStack[])inv.Accessories.Clone();
                for (int i = 0; i < inv.Accessories.Length; i++) inv.Accessories[i] = default;
                inv.Accessories[0] = ItemStack.Of(ItemType.Toolbox);
                inv.Accessories[1] = ItemStack.Of(ItemType.Toolbelt);
                inv.Accessories[2] = ItemStack.Of(ItemType.ExtendoGrip);
                Equipment.Update(inv);
                var b = Equipment.Current;
                CharmCheck("范围 工具箱 +1/+1、工具腰带 放置 +1、加长握爪 +3/+2", b.TileRangeX == 1 && b.TileRangeY == 1 && b.BlockRange >= 1 && b.TileRangeAcc,
                    $"工具范围 {b.TileRangeX}/{b.TileRangeY} 放置 {b.BlockRange} 握爪 {b.TileRangeAcc}");
                for (int i = 0; i < inv.Accessories.Length; i++) inv.Accessories[i] = keepAcc[i];
                Equipment.Update(inv);

                // ---- Use, on the dummy.
                GameDifficulty.Current = DifficultyLevel.Master;
                yield return GoTo("Town", "left1");
                for (float w = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < w;) yield return null;
                if (TargetDummy.DevAt == null)
                {
                    CharmCheck("训练假人", false);
                    yield break;
                }
                var hero = HeroController.instance;
                var heroBody = hero.GetComponent<Rigidbody2D>();
                var dummy = TargetDummy.DevAt.Value;
                var aim = DummyCentre();
                // The scene's fade and its title out of the way of the pictures.
                yield return new WaitForSeconds(2.5f);
                inv.Infinite = true;
                InventoryGui.Open = false;
                const int slot = Inventory.HotbarSize - 1;
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 999);
                inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.WoodenArrow, 999);
                mod.Stats.ManaCrystals = TerraStats.MaxManaCrystals;
                mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);

                IEnumerator Use(ItemType t, float away, float seconds, string shotName, System.Func<float, Vector2> velocity, System.Action<float, int> done)
                {
                    var d = ItemDb.Get(t);
                    Shop.DevStage = d.Stage;
                    EnemyStats.DevDummyDefense = Areas.Defense(d.Stage);
                    TerraProjectiles.Instance.DevClear();
                    inv.Slots[slot] = ItemStack.Of(t);
                    inv.Selected = slot;
                    var stand = Clear(hero, dummy + Vector2.left * away + Vector2.up * 0.5f);
                    hero.transform.position = stand;
                    heroBody.linearVelocity = Vector2.zero;
                    hero.FaceRight();
                    yield return new WaitForSeconds(0.4f);
                    mod.DevAim = aim;
                    mod.DevHold = d.Channel ? true : (bool?)null;
                    int d0 = TargetDummy.DevDamage, h0 = TargetDummy.DevHits;
                    float start = Time.time;
                    bool shot = false;
                    while (Time.time - start < seconds)
                    {
                        hero.transform.position = stand;
                        heroBody.linearVelocity = velocity != null ? velocity(Time.time - start) : Vector2.zero;
                        mod.Stats.Mana = mod.Stats.ManaMax;
                        mod.DevAim = aim;
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                        if (!shot && shotName != null && Time.time - start > 0.45f)
                        {
                            shot = true;
                            Shot("Town", "r8_" + shotName);
                        }
                        yield return null;
                    }
                    mod.DevHold = null;
                    heroBody.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                    float took = Time.time - start - 0.6f;
                    int dmg = TargetDummy.DevDamage - d0, hits = TargetDummy.DevHits - h0;
                    mod.DevAim = null;
                    Plugin.Log.LogInfo($"[AutoTest] 护符r8 输出 {d.Name}（阶段 {d.Stage}，伤害 {d.Damage}，防 {EnemyStats.DevDummyDefense}）：{dmg / Mathf.Max(0.1f, took):F0}/秒，{hits} 下");
                    done(dmg / Mathf.Max(0.1f, took), hits);
                }

                float dps = 0f;
                int hitCount = 0;
                void Got(float x, int h) { dps = x; hitCount = h; }

                yield return Use(ItemType.ArcSurge, 6f, 4f, "arcsurge", null, Got);
                var dummyHm = TargetDummy.DevHm;
                CharmCheck("电弧涌动 闪电并命中", TerraProjectiles.DevMade(ProjKind.ArcSurgeBolt) >= 6 && hitCount > 0,
                    $"{TerraProjectiles.DevMade(ProjKind.ArcSurgeBolt)} 道闪电，{hitCount} 下，{dps:F0}/秒");
                // One target: no faster than one hit in ten ticks for all of them (usesIDStaticNPCImmunity).
                CharmCheck("电弧涌动 同类每10帧一下", hitCount <= 4f * 6.5f, $"{hitCount} 下 / 4 秒");
                CharmCheck("电弧涌动 红色闪电减益", dummyHm != null && EnemyDebuffs.Has(dummyHm, EnemyDebuffs.RedLightning));

                yield return Use(ItemType.BloodRainBow, 5f, 4f, "bloodrain", null, Got);
                CharmCheck("血雨弓 天降血箭并命中", TerraProjectiles.DevMade(ProjKind.BloodRainArrow) >= 5 && hitCount > 0, $"{TerraProjectiles.DevMade(ProjKind.BloodRainArrow)} 支，{hitCount} 下");

                yield return Use(ItemType.ThunderSpear, 1.6f, 4f, "stormspear", null, Got);
                CharmCheck("风暴长矛 刺出电光并命中", TerraProjectiles.DevMade(ProjKind.ThunderSpearShot) >= 3 && hitCount > 0, $"{TerraProjectiles.DevMade(ProjKind.ThunderSpearShot)} 道，{hitCount} 下");

                yield return Use(ItemType.TheUndertaker, 5f, 4f, null, null, Got);
                CharmCheck("夺命枪 命中", hitCount >= 6, $"{hitCount} 下，{dps:F0}/秒");

                yield return Use(ItemType.BloodHamaxe, 1.4f, 3f, null, null, Got);
                CharmCheck("血锤斧 挥砍命中", hitCount > 0, $"{hitCount} 下");

                yield return Use(ItemType.PrincessWeapon, 5f, 4f, "resonance", null, Got);
                CharmCheck("共鸣权杖 光环并多次命中", TerraProjectiles.DevMade(ProjKind.PrincessRing) >= 3 && hitCount >= TerraProjectiles.DevMade(ProjKind.PrincessRing) * 2,
                    $"{TerraProjectiles.DevMade(ProjKind.PrincessRing)} 个，{hitCount} 下");

                // The lances: at rest a tenth of their damage, charging (20 a second, 10.7 px a tick) half again.
                foreach (var (t, kind) in new[] { (ItemType.JoustingLance, ProjKind.JoustingLanceThrust), (ItemType.ShadowJoustingLance, ProjKind.ShadowJoustingLanceThrust),
                    (ItemType.HallowJoustingLance, ProjKind.HallowLance) })
                {
                    var name = ItemDb.Get(t).Name;
                    yield return Use(t, 1.2f, 3f, t == ItemType.JoustingLance ? "lance" : null, null, Got);
                    float still = dps;
                    yield return Use(t, 1.2f, 3f, t == ItemType.ShadowJoustingLance ? "lance_charge" : null, _ => Vector2.right * 20f, Got);
                    float charging = dps;
                    CharmCheck($"{name} 冲刺伤害远高于站立", charging > still * 5f && charging > 0f, $"站立 {still:F0}/秒，冲刺 {charging:F0}/秒");
                }
                inv.Slots[slot] = ItemStack.Of(ItemType.JoustingLance);
                inv.Selected = slot;
                mod.DevHold = true;
                mod.DevAim = aim;
                mod.DevUseSelected(aim);
                yield return new WaitForSeconds(0.5f);
                int held = TerraProjectiles.Instance.DevCount(ProjKind.JoustingLanceThrust);
                TerraHurt.LastHurtAt = Time.time;
                yield return new WaitForSeconds(0.1f);
                int afterHit = TerraProjectiles.Instance.DevCount(ProjKind.JoustingLanceThrust);
                mod.DevHold = null;
                mod.DevAim = null;
                CharmCheck("骑枪 按住一支、受击脱手", held == 1 && afterHit == 0, $"按住 {held}，受击后 {afterHit}");
            }
            finally
            {
                EnemyStats.DevDummyDefense = 0;
                mod.DevHold = null;
                mod.DevAim = null;
                inv.Slots[Inventory.HotbarSize - 1] = default;
                Shop.DevStage = stageWas;
                GameDifficulty.Current = difficultyWas;
                TerraStats.DevImmortal = false;
                Plugin.Log.LogInfo($"[AutoTest] 护符{charmTag} 结果 {charmPass}/{charmAll}");
            }
        }
    }
}
