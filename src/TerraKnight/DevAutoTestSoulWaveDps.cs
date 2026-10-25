using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "soulwavedps"): what the Vengeful Spirit's weapons do a second against one target (Dirtmouth's training dummy,
    /// TargetDummy.DevDamage), beside what the False Knight gives (his four class rewards) and the first weapon of each class at
    /// stage 1, all at stage 0 with nothing worn: the sword; the rifle's lasers and its charge over and over; the spell cast and
    /// split; the staff's soul. Mana refilled (the raw rate; what mana allows is the item's cost over the regeneration).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SoulWaveDpsTest(string scene, HeroController hero, bool shadeOnly = false)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            Items.Shop.DevStage = 0;
            var inv = mod.Inventory;
            var sb = new StringBuilder();
            try
            {
                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
                if (World.TargetDummy.DevAt == null) { Plugin.Log.LogInfo("[AutoTest] 白波输出 没有训练假人 不通过"); yield break; }
                hero = HeroController.instance;
                var dummy = World.TargetDummy.DevAt.Value;
                var col = hero.GetComponent<BoxCollider2D>();
                mod.SetInfiniteItems(false);
                MinionEngage.DevAlways = true;
                const int slot = Inventory.HotbarSize - 1;
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 999);
                inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.WoodenArrow, 999);
                var aim = dummy + new Vector2(0f, 0.6f);

                IEnumerator Stand(float away)
                {
                    hero.transform.position = Clear(hero, dummy + Vector2.left * away + Vector2.up * 0.5f);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.4f);
                }

                // A weapon used as fast as it goes for seconds (or a routine of its own), its damage a second on the dummy.
                IEnumerator Measure(string label, ItemType item, float away, float seconds, System.Func<IEnumerator> routine = null)
                {
                    inv.Slots[slot] = ItemStack.Of(item);
                    inv.Selected = slot;
                    yield return Stand(away);
                    mod.DevAim = aim;
                    int d0 = World.TargetDummy.DevDamage, h0 = World.TargetDummy.DevHits, w0 = TerraProjectiles.DevWraiths, wh0 = TerraProjectiles.DevHit(ProjKind.SoulWraith);
                    float start = Time.time;
                    if (routine != null) yield return routine();
                    else
                        while (Time.time - start < seconds)
                        {
                            mod.Stats.Mana = mod.Stats.ManaMax;
                            if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                            yield return null;
                        }
                    yield return new WaitForSeconds(0.6f);
                    float took = Time.time - start - 0.6f;
                    int dmg = World.TargetDummy.DevDamage - d0, hits = World.TargetDummy.DevHits - h0;
                    var def = ItemDb.Get(item);
                    int wraiths = TerraProjectiles.DevWraiths - w0, wraithHits = TerraProjectiles.DevHit(ProjKind.SoulWraith) - wh0;
                    string line = $"{label}（{def.Name}，伤害 {def.Damage}，{def.UseAnimation * 60f:F0} 帧{(def.Mana > 0 ? $"，耗魔 {def.Mana}" : "")}）：{dmg / Mathf.Max(0.1f, took):F1}/秒，{hits} 下{(wraiths > 0 || wraithHits > 0 ? $"（怨灵 {wraiths} 只，命中 {wraithHits}）" : "")}";
                    sb.Append(line).Append("\n");
                    Plugin.Log.LogInfo("[AutoTest] 白波输出 " + line);
                }

                IEnumerator ChargeLoop()
                {
                    for (int k = 0; k < 3; k++)
                    {
                        mod.DevAltHold = true;
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        mod.DevUseSelected(aim, alt: true);
                        for (float t = 0f; t < 2.15f; t += Time.deltaTime) yield return null;
                        mod.DevAltHold = false;
                        for (float t = 0f; t < 0.2f; t += Time.deltaTime) yield return null;
                    }
                }

                IEnumerator SplitLoop()
                {
                    for (int k = 0; k < 4; k++)
                    {
                        mod.Stats.Mana = mod.Stats.ManaMax;
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        mod.DevUseSelected(aim);
                        for (float t = 0f; t < 0.12f; t += Time.deltaTime) yield return null;
                        mod.DevAltPress = true;
                        for (float t = 0f; t < 1.4f; t += Time.deltaTime) yield return null;
                    }
                }

                // The rift rifle: five rays, then the burst, over and over.
                IEnumerator RiftLoop()
                {
                    for (int k = 0; k < 3; k++)
                    {
                        for (int r = 0; r < 5; r++)
                        {
                            while (!mod.DevReady || mod.IsUsingItem) yield return null;
                            mod.DevUseSelected(aim);
                        }
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        mod.DevUseSelected(aim, alt: true);
                    }
                }

                // The shade wraith held on the dummy for six seconds, then let go and caught.
                IEnumerator GhostLoop()
                {
                    mod.DevHold = true;
                    while (!mod.DevReady || mod.IsUsingItem) yield return null;
                    mod.DevUseSelected(aim);
                    for (float t = 0f; t < 6f; t += Time.deltaTime)
                    {
                        mod.Stats.Mana = mod.Stats.ManaMax;
                        yield return null;
                    }
                    mod.DevHold = false;
                    for (float t = 0f; t < 3f && TerraProjectiles.Instance != null && TerraProjectiles.Instance.DevPos(ProjKind.ShadeGhost) != null; t += Time.deltaTime) yield return null;
                    mod.DevHold = null;
                }

                IEnumerator Summon(ItemType staff, int buff)
                {
                    while (!mod.DevReady || mod.IsUsingItem) yield return null;
                    mod.DevUseSelected(hero.GetComponent<BoxCollider2D>().bounds.center + Vector3.up);
                    for (float t = 0f; t < 8f; t += Time.deltaTime) yield return null;
                    Player.PlayerBuffs.Remove(buff);
                    for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;
                }

                if (!shadeOnly)
                {
                sb.Append("== 白波\n");
                yield return Measure("剑", ItemType.RevenantEdge, 1.6f, 6f);
                yield return Measure("枪 激光", ItemType.RevenantPulseRifle, 5f, 6f);
                yield return Measure("枪 蓄力循环", ItemType.RevenantPulseRifle, 5f, 0f, ChargeLoop);
                yield return Measure("法术 直放", ItemType.VengefulSpiritFocus, 5f, 6f);
                yield return Measure("法术 分裂", ItemType.VengefulSpiritFocus, 5f, 0f, SplitLoop);
                yield return Measure("召唤", ItemType.WanderingSoulStaff, 5f, 0f, () => Summon(ItemType.WanderingSoulStaff, Player.PlayerBuffs.WanderingSoul));
                sb.Append("== 白吼\n");
                mod.DevHold = true;
                yield return Measure("剑", ItemType.WailingDeathbell, 3f, 6f);
                mod.DevHold = null;
                yield return Measure("枪", ItemType.GhostPipeOrgan, 5f, 6f);
                yield return Measure("法术", ItemType.HowlingWraithsFocus, 4f, 6f);
                yield return Measure("召唤", ItemType.ElegyMask, 5f, 0f, () => Summon(ItemType.ElegyMask, Player.PlayerBuffs.ChoirSoul));
                }
                sb.Append("== 黑波\n");
                mod.DevHold = true;
                yield return Measure("剑 连斩", ItemType.ShadeEdge, 1.4f, 6f);
                mod.DevHold = null;
                yield return Measure("枪 裂光", ItemType.ShadeRiftRifle, 5f, 6f);
                yield return Measure("枪 五层引爆循环", ItemType.ShadeRiftRifle, 5f, 0f, RiftLoop);
                yield return Measure("法术", ItemType.ShadeSoulFocus, 5f, 0f, GhostLoop);
                yield return Measure("召唤", ItemType.ShadeSoulStaff, 5f, 0f, () => Summon(ItemType.ShadeSoulStaff, Player.PlayerBuffs.ShadeSoul));
                sb.Append("== 阶段 2 参考\n");
                foreach (var (label, cls) in new[] { ("战士", 0), ("射手", 1), ("法师", 2) })
                    yield return Measure(label, ProgressionBalance.Core[2][cls][0], cls == 0 ? 1.6f : 5f, 6f);
                yield return Measure("法师 2", ProgressionBalance.Core[2][2][1], 5f, 6f);
                yield return Measure("召唤", ProgressionBalance.Core[2][3][0], 5f, 0f, () => Summon(ProgressionBalance.Core[2][3][0], Player.PlayerBuffs.ImpMinion));
                sb.Append("== 阶段 3 参考\n");
                foreach (var (label, cls) in new[] { ("战士", 0), ("射手", 1), ("法师", 2) })
                    yield return Measure(label, ProgressionBalance.Core[3][cls][0], cls == 0 ? 1.6f : 5f, 6f);
                yield return Measure("召唤", ProgressionBalance.Core[3][3][0], 5f, 0f, () => Summon(ProgressionBalance.Core[3][3][0], Player.PlayerBuffs.SpiderMinion));
                if (shadeOnly) yield break;
                sb.Append("== 假骑士奖励\n");
                yield return Measure("战士", ItemType.EnchantedBoomerang, 4f, 6f);
                yield return Measure("射手", ItemType.IronBow, 5f, 6f);
                yield return Measure("法师", ItemType.Vilethorn, 3f, 6f);
                yield return Measure("召唤", ItemType.SlimeStaff, 5f, 0f, () => Summon(ItemType.SlimeStaff, Player.PlayerBuffs.BabySlime));
                sb.Append("== 阶段 1 参考\n");
                foreach (var (label, cls) in new[] { ("战士", 0), ("射手", 1), ("法师", 2) })
                    yield return Measure(label, ProgressionBalance.Core[1][cls][0], cls == 0 ? 1.6f : 5f, 6f);
            }
            finally
            {
                mod.DevAltHold = null;
                mod.DevAltPress = false;
                mod.DevAim = null;
                MinionEngage.DevAlways = false;
                Player.TerraStats.DevImmortal = false;
                Items.Shop.DevStage = -1;
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(DevCopy.SaveDir, "soulwavedps.txt"), sb.ToString());
            Plugin.Log.LogInfo("[AutoTest] 白波输出 完成");
        }
    }
}
