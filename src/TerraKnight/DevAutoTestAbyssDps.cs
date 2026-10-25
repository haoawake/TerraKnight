using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "abyssdps"): what the Abyss Shriek's weapons do a second against Dirtmouth's training dummy
    /// (TargetDummy.DevDamage), beside stage 4's and stage 5's first weapon of each class, at stage 0 with nothing worn: the
    /// yoyo over its whole 12 seconds, the sniper plain and aimed, the staff cast over and over, the mask's soul. Mana refilled.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AbyssDpsTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            Items.Shop.DevStage = 0;
            var inv = mod.Inventory;
            try
            {
                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
                if (World.TargetDummy.DevAt == null) { Plugin.Log.LogInfo("[AutoTest] 黑吼输出 没有训练假人 不通过"); yield break; }
                hero = HeroController.instance;
                var dummy = World.TargetDummy.DevAt.Value;
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

                IEnumerator Measure(string label, ItemType item, float away, float seconds, System.Func<IEnumerator> routine = null)
                {
                    inv.Slots[slot] = ItemStack.Of(item);
                    inv.Selected = slot;
                    yield return Stand(away);
                    mod.DevAim = aim;
                    int d0 = World.TargetDummy.DevDamage, h0 = World.TargetDummy.DevHits;
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
                    Plugin.Log.LogInfo($"[AutoTest] 黑吼输出 {label}（{def.Name}，伤害 {def.Damage}，{def.UseAnimation * 60f:F0} 帧{(def.Mana > 0 ? $"，耗魔 {def.Mana}" : "")}）：{dmg / Mathf.Max(0.1f, took):F1}/秒，{hits} 下");
                }

                IEnumerator YoyoLife()
                {
                    mod.DevHold = true;
                    while (!mod.DevReady || mod.IsUsingItem) yield return null;
                    mod.DevUseSelected(aim);
                    for (float t = 0f; t < 12.5f; t += Time.deltaTime) yield return null;
                    mod.DevHold = null;
                }

                IEnumerator Aimed()
                {
                    mod.DevAltHold = true;
                    for (int k = 0; k < 3; k++) yield return null;
                    for (float t = 0f; t < 6f; t += Time.deltaTime)
                    {
                        if (mod.DevReady) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    mod.DevAltHold = null;
                }

                IEnumerator Summon(int buff)
                {
                    while (!mod.DevReady || mod.IsUsingItem) yield return null;
                    mod.DevUseSelected(hero.GetComponent<BoxCollider2D>().bounds.center + Vector3.up);
                    for (float t = 0f; t < 8f; t += Time.deltaTime) yield return null;
                    Player.PlayerBuffs.Remove(buff);
                    for (float t = 0f; t < 0.3f; t += Time.deltaTime) yield return null;
                }

                yield return Measure("剑 悠悠球 12 秒", ItemType.AbyssYoyo, 3f, 0f, YoyoLife);
                yield return Measure("枪 普通", ItemType.VoidSniper, 6f, 6f);
                yield return Measure("枪 瞄准", ItemType.VoidSniper, 6f, 0f, Aimed);
                yield return Measure("法术", ItemType.AbyssStaff, 5f, 6f);
                yield return Measure("召唤", ItemType.DirgeMask, 6f, 0f, () => Summon(Player.PlayerBuffs.DirgeSoul));
                foreach (int stage in new[] { 4, 5 })
                {
                    foreach (var (label, cls) in new[] { ("战士", 0), ("射手", 1), ("法师", 2) })
                        yield return Measure($"阶段 {stage} {label}", ProgressionBalance.Core[stage][cls][0], cls == 0 ? 1.6f : 5f, 6f);
                    yield return Measure($"阶段 {stage} 召唤", ProgressionBalance.Core[stage][3][0], 5f, 0f, () => Summon(stage == 4 ? Player.PlayerBuffs.TwinEyesMinion : Player.PlayerBuffs.Ravens));
                }
            }
            finally
            {
                mod.DevAltHold = null;
                mod.DevHold = null;
                mod.DevAim = null;
                MinionEngage.DevAlways = false;
                Player.TerraStats.DevImmortal = false;
                Items.Shop.DevStage = -1;
            }
            Plugin.Log.LogInfo("[AutoTest] 黑吼输出 完成");
        }
    }
}
