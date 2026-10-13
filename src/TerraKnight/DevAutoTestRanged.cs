using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ranged"): the hardmode guns and bows fired at an enemy kept in front of the
    /// player: the life it lost, the shots each made (by kind) and how many hit, the ammo used for
    /// the uses made (the burst rifle pays once a burst, the sharks save some), with pictures.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ProjKind[] rangedKinds =
        {
            ProjKind.Bullet, ProjKind.HighVelocityBullet, ProjKind.WoodenArrow, ProjKind.MarrowArrow, ProjKind.BoneShard, ProjKind.FrostArrow,
            ProjKind.PulseBolt, ProjKind.ShadowflameArrow,
        };

        IEnumerator RangedTest(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 远程：场景里没有敌人");
                yield break;
            }
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++) inv.Slots[i] = default;
            int ammoSlot = Inventory.Size - 1;
            var trb = target.GetComponent<Rigidbody2D>();
            int slot = Inventory.HotbarSize - 1;
            var info = EnemyStats.Of(target);
            var runs = new (ItemType Type, float Dist, float Seconds, float[] Pictures)[]
            {
                (ItemType.Megashark, 5f, 1.5f, new[] { 0.4f }),
                (ItemType.ClockworkAssaultRifle, 5f, 1.6f, new[] { 0.1f }),
                (ItemType.Uzi, 5f, 1.2f, new float[0]),
                (ItemType.VenusMagnum, 5f, 1.2f, new float[0]),
                (ItemType.SniperRifle, 6f, 1.3f, new[] { 0.05f }),
                (ItemType.TacticalShotgun, 5f, 1.3f, new[] { 0.08f }),
                (ItemType.Gatligator, 5f, 1.2f, new[] { 0.5f }),
                (ItemType.ChainGun, 5f, 1.2f, new[] { 0.5f }),
                (ItemType.SDMG, 5f, 1.2f, new[] { 0.5f }),
                (ItemType.CobaltRepeater, 5f, 1.2f, new float[0]),
                (ItemType.HallowedRepeater, 5f, 1.2f, new[] { 0.15f }),
                (ItemType.DaedalusStormbow, 5f, 2f, new[] { 0.45f }),
                (ItemType.ChlorophyteShotbow, 5f, 1.3f, new[] { 0.12f }),
                (ItemType.Tsunami, 6f, 1.3f, new[] { 0.1f }),
                (ItemType.Marrow, 6f, 1.3f, new[] { 0.12f }),
                (ItemType.IceBow, 5f, 1.2f, new[] { 0.12f }),
                (ItemType.PulseBow, 5f, 1.5f, new[] { 0.15f }),
                (ItemType.ShadowFlameBow, 5f, 1.3f, new[] { 0.12f }),
            };
            foreach (var run in runs)
            {
                if (target == null || target.isDead) break;
                var def = ItemDb.Get(run.Type);
                inv.Slots[slot] = ItemStack.Of(run.Type, 1);
                inv.Selected = slot;
                inv.Slots[ammoSlot] = def.UseAmmo == AmmoKind.Arrow ? ItemStack.Of(ItemType.WoodenArrow, 500) : ItemStack.Of(ItemType.MusketBall, 500);
                target.hp = Mathf.Max(target.hp, 99999);
                Vector2 hold = (Vector2)col.bounds.center + Vector2.right * run.Dist;
                // The last weapon's shots (bouncing bolts, shards) would leave the enemy immune.
                TerraProjectiles.Instance.DevClear();
                Pin(target, trb, hold);
                yield return null;
                var made0 = new Dictionary<ProjKind, int>();
                var hits0 = new Dictionary<ProjKind, int>();
                foreach (var k in rangedKinds)
                {
                    made0[k] = TerraProjectiles.DevMade(k);
                    hits0[k] = TerraProjectiles.DevHit(k);
                }
                int life = EnemyStats.Life(target, info), taken = 0, uses = 0, maxAlive = 0, ammo0 = inv.Slots[ammoSlot].Count;
                mod.DevAim = hold;
                int pic = 0;
                for (float t = 0f; t < run.Seconds; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Pin(target, trb, hold);
                    if (mod.DevReady)
                    {
                        mod.DevUseSelected(hold);
                        uses++;
                    }
                    int now = EnemyStats.Life(target, info);
                    taken += Mathf.Max(0, life - now);
                    if (target.hp < 50000) target.hp = 99999;
                    life = EnemyStats.Life(target, info);
                    int alive = 0;
                    foreach (var k in rangedKinds) alive += TerraProjectiles.Instance.DevCount(k);
                    maxAlive = Mathf.Max(maxAlive, alive);
                    if (pic < run.Pictures.Length && t >= run.Pictures[pic])
                    {
                        Shot(scene, $"r_{run.Type}_{pic}");
                        pic++;
                    }
                    yield return null;
                }
                mod.DevAim = null;
                int ammo1 = inv.Slots[ammoSlot].Count;
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Pin(target, trb, hold);
                    int now = EnemyStats.Life(target, info);
                    taken += Mathf.Max(0, life - now);
                    if (target.hp < 50000) target.hp = 99999;
                    life = EnemyStats.Life(target, info);
                    yield return null;
                }
                var made = new System.Text.StringBuilder();
                foreach (var k in rangedKinds)
                {
                    int n = TerraProjectiles.DevMade(k) - made0[k];
                    if (n > 0) made.Append($"{k}×{n}(命中{TerraProjectiles.DevHit(k) - hits0[k]}) ");
                }
                Plugin.Log.LogInfo($"[AutoTest] 远程 {def.Name}：用 {uses} 次，耗弹 {ammo0 - ammo1}，敌人共受泰拉伤害 {taken}（面板 {def.Damage}），弹幕 [{made.ToString().Trim()}]，同时最多 {maxAlive}");
                yield return new WaitForSeconds(0.3f);
            }
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
