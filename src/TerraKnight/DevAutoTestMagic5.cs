using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "magic5", sweep R5a): the gem staves with their gem effects, the Crimson Rod,
    /// Flower of Frost, Bee and Wasp Guns, Nettle Burst, Rainbow Gun, Staff of Earth and Razorpine,
    /// each used for a while at an enemy held a few tiles off: what each makes and lands, the
    /// damage the enemy takes and the debuffs it gets.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ProjKind[] magic5Kinds =
        {
            ProjKind.TopazBolt, ProjKind.SapphireBolt, ProjKind.EmeraldBolt, ProjKind.AmberBolt, ProjKind.CrimsonCloudMoving, ProjKind.CrimsonCloud, ProjKind.BloodRain,
            ProjKind.FrostBall, ProjKind.Bee, ProjKind.GiantBee, ProjKind.Wasp, ProjKind.NettleStem, ProjKind.NettleStem2, ProjKind.NettleTip, ProjKind.RainbowHead,
            ProjKind.RainbowPiece, ProjKind.EarthBoulder, ProjKind.PineNeedle, ProjKind.CrystalCharge, ProjKind.CrystalChargeShard, ProjKind.SoulDrainBox,
            ProjKind.ZapinatorLaser, ProjKind.BetsysWrathBolt, ProjKind.SharpTear, ProjKind.ShadowflameTentacle, ProjKind.BookStaffShot, ProjKind.BookStaffStorm,
            ProjKind.LaserMachinegunHeld, ProjKind.MachinegunLaser, ProjKind.StellarTuneStar, ProjKind.NightglowStar, ProjKind.WeatherPainCloud, ProjKind.FrostSpark,
            ProjKind.PrettyMirrorProj, ProjKind.DaybloomMote, ProjKind.GlacierFangShot, ProjKind.PetalstormPetal,
        };

        IEnumerator Magic5Test(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 魔法5：场景里没有敌人");
                yield break;
            }
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected, savedCrystals = mod.Stats.ManaCrystals;
            mod.Stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            var trb = target.GetComponent<Rigidbody2D>();
            int slot = Inventory.HotbarSize - 1;
            var info = EnemyStats.Of(target);
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(col.bounds.center, Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            var runs = new (ItemType Type, float Dist, float Seconds, float Picture)[]
            {
                (ItemType.TopazStaff, 5f, 1.5f, 0.3f), (ItemType.SapphireStaff, 5f, 1.5f, 0.25f), (ItemType.EmeraldStaff, 5f, 1.5f, 0.4f), (ItemType.AmberStaff, 5f, 1.5f, 0.3f),
                (ItemType.CrimsonRod, 4f, 3f, 1.5f), (ItemType.FlowerofFrost, 5f, 1.5f, 0.3f), (ItemType.BeeGun, 5f, 1.5f, 0.3f), (ItemType.WaspGun, 5f, 1.5f, 0.3f),
                (ItemType.NettleBurst, 4f, 1.5f, 0.12f), (ItemType.RainbowGun, 6f, 2f, 0.6f), (ItemType.StaffofEarth, 5f, 2.5f, 0.3f), (ItemType.Razorpine, 5f, 1.5f, 0.3f),
                (ItemType.CrystalSerpent, 5f, 1.5f, 0.3f), (ItemType.SoulDrain, 5f, 2f, 0.3f), (ItemType.ZapinatorGray, 5f, 1.5f, 0.1f), (ItemType.ZapinatorOrange, 5f, 1.5f, 0.1f),
                (ItemType.BetsysWrath, 5f, 1.5f, 0.2f), (ItemType.SharpTears, 4f, 1.5f, 0.3f),
                (ItemType.ShadowFlameHexDoll, 4f, 1.5f, 0.15f), (ItemType.BookStaff, 5f, 1.5f, 0.2f), (ItemType.BookStaff, 5f, 2.5f, 0.6f),
                (ItemType.LaserMachinegun, 5f, 3f, 2.5f), (ItemType.SparkleGuitar, 5f, 1.5f, 0.2f), (ItemType.Nightglow, 5f, 2f, 0.3f),
                (ItemType.WeatherPain, 5f, 3f, 1.2f), (ItemType.WandofFrosting, 4f, 1.5f, 0.2f), (ItemType.PrettyMirror, 4f, 1.5f, 0.2f),
                (ItemType.DaybloomStaff, 4f, 1.5f, 0.2f), (ItemType.GlacierFang, 5f, 3f, 0.8f), (ItemType.Petalstorm, 5f, 2f, 0.3f),
            };
            int bookRuns = 0;
            foreach (var run in runs)
            {
                if (target == null || target.isDead) break;
                var def = ItemDb.Get(run.Type);
                // The second Tome run is its right button (the twister).
                bool alt = run.Type == ItemType.BookStaff && bookRuns++ > 0;
                if (def.Channel) mod.DevHold = true;
                inv.Slots[slot] = ItemStack.Of(run.Type, def.Consumable ? 30 : 1);
                inv.Selected = slot;
                target.hp = Mathf.Max(target.hp, 99999);
                EnemyDebuffs.Clear();
                Vector2 hold = (Vector2)col.bounds.center + Vector2.right * side * run.Dist;
                TerraProjectiles.Instance.DevClear();
                Pin(target, trb, hold);
                yield return null;
                var made0 = new Dictionary<ProjKind, int>();
                var hits0 = new Dictionary<ProjKind, int>();
                foreach (var k in magic5Kinds)
                {
                    made0[k] = TerraProjectiles.DevMade(k);
                    hits0[k] = TerraProjectiles.DevHit(k);
                }
                int bursts0 = TerraProjectiles.DevBoulderBursts, pieces0 = TerraProjectiles.DevRainbowSegments;
                int life = EnemyStats.Life(target, info), taken = 0, uses = 0, maxAlive = 0;
                bool frostbite = false;
                int maxDrainers = 0, life0 = 0;
                if (run.Type == ItemType.SoulDrain)
                {
                    mod.Stats.Life = mod.Stats.LifeMax / 2;
                    life0 = mod.Stats.Life;
                }
                int glacier0 = TerraProjectiles.DevGlacierReleases;
                int tent0 = TerraProjectiles.DevTentacles, storms0 = TerraProjectiles.DevBookStorms, mgl0 = TerraProjectiles.DevMachinegunLasers, stars0 = TerraProjectiles.DevStellarStars;
                int shards0 = TerraProjectiles.DevCrystalShards, jumps0 = TerraProjectiles.DevZapJumps, tears0 = TerraProjectiles.DevSharpTears;
                // The Crimson Rod is aimed above the enemy, for its cloud to rain on it.
                var aim = run.Type == ItemType.CrimsonRod ? hold + Vector2.up * 3f : hold;
                bool wide = run.Type == ItemType.CrimsonRod || run.Type == ItemType.RainbowGun || run.Type == ItemType.StaffofEarth || run.Type == ItemType.NettleBurst || alt || run.Type == ItemType.Nightglow;
                mod.DevAim = aim;
                bool pictured = false;
                for (float t = 0f; t < run.Seconds; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Pin(target, trb, hold);
                    if (run.Type != ItemType.SoulDrain || mod.Stats.Mana < 20) mod.Stats.Mana = mod.Stats.ManaMax;
                    if (mod.DevReady)
                    {
                        mod.DevUseSelected(aim, alt);
                        uses++;
                    }
                    int now = EnemyStats.Life(target, info);
                    taken += Mathf.Max(0, life - now);
                    if (target.hp < 50000) target.hp = 99999;
                    life = EnemyStats.Life(target, info);
                    frostbite |= EnemyDebuffs.Has(target, EnemyDebuffs.Frostbite);
                    maxDrainers = Mathf.Max(maxDrainers, EnemyDebuffs.SoulDrainers);
                    int alive = 0;
                    foreach (var k in magic5Kinds) alive += TerraProjectiles.Instance.DevCount(k);
                    maxAlive = Mathf.Max(maxAlive, alive);
                    if (!pictured && t >= run.Picture)
                    {
                        pictured = true;
                        yield return CloseUp(scene, $"m5_{run.Type}{(alt ? "_alt" : "")}", hero, wide ? 330 : 200, wide ? 1 : 2);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                mod.DevHold = null;
                for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Pin(target, trb, hold);
                    int now = EnemyStats.Life(target, info);
                    taken += Mathf.Max(0, life - now);
                    if (target.hp < 50000) target.hp = 99999;
                    life = EnemyStats.Life(target, info);
                    frostbite |= EnemyDebuffs.Has(target, EnemyDebuffs.Frostbite);
                    yield return null;
                }
                var made = new System.Text.StringBuilder();
                foreach (var k in magic5Kinds)
                {
                    int n = TerraProjectiles.DevMade(k) - made0[k];
                    if (n > 0) made.Append($"{k}×{n}(命中{TerraProjectiles.DevHit(k) - hits0[k]}) ");
                }
                if (TerraProjectiles.DevBoulderBursts > bursts0) made.Append($"巨石爆裂{TerraProjectiles.DevBoulderBursts - bursts0} ");
                if (TerraProjectiles.DevRainbowSegments > pieces0) made.Append($"彩虹段{TerraProjectiles.DevRainbowSegments - pieces0} ");
                if (TerraProjectiles.DevCrystalShards > shards0) made.Append($"水晶碎片{TerraProjectiles.DevCrystalShards - shards0} ");
                if (TerraProjectiles.DevZapJumps > jumps0) made.Append($"冲击跳跃{TerraProjectiles.DevZapJumps - jumps0} ");
                if (TerraProjectiles.DevSharpTears > tears0) made.Append($"血荆棘{TerraProjectiles.DevSharpTears - tears0} ");
                if (TerraProjectiles.DevTentacles > tent0) made.Append($"触手{TerraProjectiles.DevTentacles - tent0} ");
                if (TerraProjectiles.DevBookStorms > storms0) made.Append($"龙卷风{TerraProjectiles.DevBookStorms - storms0} ");
                if (TerraProjectiles.DevMachinegunLasers > mgl0) made.Append($"激光{TerraProjectiles.DevMachinegunLasers - mgl0} ");
                if (TerraProjectiles.DevStellarStars > stars0) made.Append($"星星{TerraProjectiles.DevStellarStars - stars0} ");
                if (alt) made.Append("(右键) ");
                if (TerraProjectiles.DevGlacierReleases > glacier0) made.Append($"冰锥放出{TerraProjectiles.DevGlacierReleases - glacier0} ");
                if (run.Type == ItemType.SoulDrain) made.Append($"吸取敌人最多{maxDrainers} 回血{mod.Stats.Life - life0} 魔力{mod.Stats.Mana}/{mod.Stats.ManaMax} ");
                Plugin.Log.LogInfo($"[AutoTest] 魔法5 {def.Name}：用 {uses} 次，敌人共受泰拉伤害 {taken}（面板 {def.Damage}），霜噬 {frostbite}，弹幕 [{made.ToString().Trim()}]，同时最多 {maxAlive}");
                yield return new WaitForSeconds(0.3f);
            }
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSelected;
            mod.Stats.ManaCrystals = savedCrystals;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            mod.DevNoFallDamage = false;
        }
    }
}
