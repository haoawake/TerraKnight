using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "weapons"): the 17 newer weapons used on an enemy held in front of the player:
    /// the life each took, the debuffs left, what each made (stars, beams, bees, pellets, thorns,
    /// streams), how far the spears reached, whether the boomerangs came back and the missiles
    /// followed the aim, what was used up; with a picture of each in flight.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ProjKind[] weaponKinds =
        {
            ProjKind.StarfuryStar, ProjKind.EnchantedBeam, ProjKind.IceBolt, ProjKind.Bee, ProjKind.DarkLance, ProjKind.TridentSpear,
            ProjKind.WoodenBoomerang, ProjKind.Flamarang, ProjKind.ThornChakram, ProjKind.Bullet, ProjKind.Shuriken, ProjKind.ThrowingKnife,
            ProjKind.MagicMissile, ProjKind.Flamelash, ProjKind.VilethornBase, ProjKind.VilethornTip, ProjKind.WaterStream,
        };

        IEnumerator WeaponsTest(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 武器：场景里没有敌人");
                yield break;
            }
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected, savedCrystals = mod.Stats.ManaCrystals;
            mod.Stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            for (int i = Inventory.HotbarSize; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 500);
            var trb = target.GetComponent<Rigidbody2D>();
            int slot = Inventory.HotbarSize - 1;
            int[] debuffs = { EnemyDebuffs.OnFire, EnemyDebuffs.Poisoned, EnemyDebuffs.Shadowflame, EnemyDebuffs.Confused };
            string[] debuffNames = { "着火", "中毒", "暗影焰", "困惑" };

            // One weapon: distance to the enemy (units), how long to watch, whether the button stays held
            // (a use whenever the last is over), when to take the picture, and what to aim at instead.
            var runs = new (ItemType Type, float Dist, float Seconds, bool Held, float Picture)[]
            {
                (ItemType.Starfury, 3f, 1.6f, true, 0.28f),
                (ItemType.EnchantedSword, 3f, 2f, true, 0.15f),
                (ItemType.IceBlade, 3f, 2f, true, 0.12f),
                (ItemType.BeeKeeper, 1f, 2.5f, false, 0.6f),
                (ItemType.DarkLance, 5.5f, 0.6f, false, 0.12f),
                (ItemType.Trident, 2.5f, 0.7f, false, 0.33f),
                (ItemType.WoodenBoomerang, 4f, 3f, false, 0.3f),
                (ItemType.Flamarang, 5f, 3f, false, 0.25f),
                (ItemType.ThornChakram, 5f, 3f, false, 0.25f),
                (ItemType.Musket, 4f, 0.7f, false, 0.05f),
                (ItemType.Boomstick, 4f, 0.7f, false, 0.05f),
                (ItemType.Shuriken, 4f, 1.2f, true, 0.12f),
                (ItemType.ThrowingKnife, 4f, 1.2f, true, 0.12f),
                (ItemType.MagicMissile, 4f, 2f, false, 0.4f),
                (ItemType.Vilethorn, 3.5f, 1.8f, false, 0.7f),
                (ItemType.AquaScepter, 4f, 1f, true, 0.35f),
                (ItemType.Flamelash, 4f, 2.5f, false, 0.4f),
            };
            foreach (var run in runs)
            {
                if (target == null || target.isDead) break;
                var def = ItemDb.Get(run.Type);
                inv.Slots[slot] = ItemStack.Of(run.Type, def.MaxStack > 1 ? 50 : 1);
                inv.Selected = slot;
                target.hp = Mathf.Max(target.hp, 3000);
                EnemyDebuffs.Clear();
                mod.Stats.Mana = mod.Stats.ManaMax;
                Vector2 centre = col.bounds.center;
                Vector2 hold = centre + Vector2.right * run.Dist;
                Pin(target, trb, hold);
                yield return null;
                var made0 = new Dictionary<ProjKind, int>();
                foreach (var k in weaponKinds) made0[k] = TerraProjectiles.DevMade(k);
                int life0 = EnemyStats.Life(target, EnemyStats.Of(target)), mana0 = mod.Stats.Mana, ammo0 = inv.Slots[Inventory.Size - 1].Count;
                bool channel = def.Channel;
                // The missiles first follow an aim above the enemy, then the enemy itself; the Flamelash is steered onto it.
                var aim = run.Type == ItemType.MagicMissile ? hold + Vector2.up * 3f : hold;
                mod.DevHold = channel ? true : (bool?)null;
                mod.DevAim = aim;
                mod.DevUseSelected(aim);
                int manaAfterCast = mod.Stats.Mana;
                if (run.Type == ItemType.WoodenBoomerang || run.Type == ItemType.Flamarang)
                {
                    // A second throw while the first is out makes nothing.
                    yield return new WaitForSeconds(0.1f);
                    mod.DevUseSelected(aim);
                }
                var seen = new HashSet<string>();
                float reach = 0f, missileOff = -1f;
                int alive = 0, maxAlive = 0;
                bool pictured = false;
                for (float t = 0f; t < run.Seconds; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Pin(target, trb, hold);
                    if (run.Held && mod.DevReady) mod.DevUseSelected(aim);
                    var pr = TerraProjectiles.Instance;
                    centre = col.bounds.center;
                    foreach (var k in new[] { ProjKind.DarkLance, ProjKind.TridentSpear })
                    {
                        var at = pr.DevNewest(k);
                        if (at.HasValue) reach = Mathf.Max(reach, Vector2.Distance(at.Value, centre) / Effects.TerraParticles.Px);
                    }
                    alive = 0;
                    foreach (var k in weaponKinds) alive += pr.DevCount(k);
                    maxAlive = Mathf.Max(maxAlive, alive);
                    if (run.Type == ItemType.MagicMissile)
                    {
                        var m = pr.DevNewest(ProjKind.MagicMissile);
                        if (t >= 0.55f && t - Time.deltaTime < 0.55f && m.HasValue)
                        {
                            missileOff = Vector2.Distance(m.Value, aim) / Effects.TerraParticles.Px;
                            Shot(scene, "w_MagicMissile_steered");
                            aim = hold;
                            mod.DevAim = aim;
                        }
                        // Then let go: it homes on the enemy, trailing its ribbon.
                        if (t >= 0.55f) mod.DevHold = false;
                        if (t >= 0.62f && t - Time.deltaTime < 0.62f) Shot(scene, "w_MagicMissile_free");
                    }
                    for (int k = 0; k < debuffs.Length; k++) if (EnemyDebuffs.Has(target, debuffs[k])) seen.Add(debuffNames[k]);
                    if (!pictured && t >= run.Picture && run.Type != ItemType.MagicMissile)
                    {
                        pictured = true;
                        Shot(scene, "w_" + run.Type);
                    }
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                int life1 = target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0;
                var made = new System.Text.StringBuilder();
                foreach (var k in weaponKinds)
                {
                    int n = TerraProjectiles.DevMade(k) - made0[k];
                    if (n > 0) made.Append($"{k}×{n} ");
                }
                string extra = "";
                if (run.Type == ItemType.DarkLance || run.Type == ItemType.Trident) extra = $"，枪尖最远 {reach:F0}px";
                if (run.Type == ItemType.MagicMissile) extra = $"，跟随瞄准点偏差 {missileOff:F0}px";
                if (run.Type == ItemType.WoodenBoomerang || run.Type == ItemType.Flamarang || run.Type == ItemType.ThornChakram)
                    extra = $"，结束时仍在外 {TerraProjectiles.Instance.DevCount(def.Shoot)}";
                if (def.Consumable) extra += $"，剩余 {inv.Slots[slot].Count}/50";
                if (def.UseAmmo != AmmoKind.None) extra += $"，子弹 {ammo0} -> {inv.Slots[Inventory.Size - 1].Count}";
                if (def.Mana > 0) extra += $"，魔力 {mana0} -> 施放后 {manaAfterCast} -> {mod.Stats.Mana}";
                Plugin.Log.LogInfo($"[AutoTest] 武器 {def.Name}：敌人泰拉血量 {life0} -> {life1}，减益 [{string.Join("、", seen)}]，生成 [{made.ToString().Trim()}]，同时最多 {maxAlive}{extra}");
                yield return new WaitForSeconds(0.4f);
            }
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSelected;
            mod.Stats.ManaCrystals = savedCrystals;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            mod.DevNoFallDamage = false;
        }

        static void Pin(HealthManager target, Rigidbody2D trb, Vector2 at)
        {
            if (target == null) return;
            target.transform.position = new Vector3(at.x, at.y, target.transform.position.z);
            if (trb != null) trb.linearVelocity = Vector2.zero;
        }
    }
}
