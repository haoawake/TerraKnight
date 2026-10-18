using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "magic"): the hardmode spells cast at an enemy kept in front of the player (mana
    /// topped up): the life it lost, the shots each made by kind and how many hit, the mana a use
    /// cost, the debuffs left; with pictures in flight.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ProjKind[] magicKinds =
        {
            ProjKind.CrystalStormShard, ProjKind.CursedFlame, ProjKind.GoldenShowerStream, ProjKind.FrostBolt, ProjKind.PoisonFang, ProjKind.VenomFang,
            ProjKind.UnholyTrident, ProjKind.Meteor, ProjKind.SpectreWisp, ProjKind.InfernoBolt, ProjKind.InfernoBlast,
            ProjKind.Shadowbeam, ProjKind.HeatRay, ProjKind.Bubble, ProjKind.RazorbladeTyphoon, ProjKind.Icicle, ProjKind.SkyFractureSword, ProjKind.Leaf,
            ProjKind.PurpleLaser, ProjKind.Bat, ProjKind.CrystalVileShard, ProjKind.CrystalVileTip, ProjKind.MagnetSphere, ProjKind.MagnetBolt, ProjKind.RainbowRod,
            ProjKind.NimbusCloudMoving, ProjKind.NimbusCloud, ProjKind.NimbusRain,
            ProjKind.HarpNote1, ProjKind.HarpNote2, ProjKind.HarpNote3, ProjKind.ClingerWall, ProjKind.ToxicFlask, ProjKind.ToxicCloud, ProjKind.Skull, ProjKind.SpiritFlame,
            ProjKind.NebulaBlaze, ProjKind.NebulaBlazeBig, ProjKind.LunarFlare, ProjKind.NebulaArcanum, ProjKind.NebulaArcanumSub, ProjKind.NebulaArcanumShard,
            ProjKind.LastPrism, ProjKind.LastPrismBeam, ProjKind.ChargedBlaster, ProjKind.ChargedBlasterOrb, ProjKind.ChargedBlasterBeam, ProjKind.MedusaHead, ProjKind.MedusaRay,
            ProjKind.IceRodBolt,
        };

        IEnumerator MagicTest(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 魔法：场景里没有敌人");
                yield break;
            }
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected, savedCrystals = mod.Stats.ManaCrystals;
            mod.Stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            mod.Stats.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            var trb = target.GetComponent<Rigidbody2D>();
            int slot = Inventory.HotbarSize - 1;
            var info = EnemyStats.Of(target);
            int[] debuffs = { EnemyDebuffs.CursedInferno, EnemyDebuffs.Ichor, EnemyDebuffs.Frostbite, EnemyDebuffs.Poisoned, EnemyDebuffs.Venom, EnemyDebuffs.Shadowflame, EnemyDebuffs.Hellfire };
            string[] debuffNames = { "诅咒狱火", "灵液", "霜噬", "中毒", "酸性毒液", "暗影焰", "地狱火" };
            var runs = new (ItemType Type, float Dist, float Seconds, float[] Pictures)[]
            {
                (ItemType.CrystalStorm, 5f, 1.2f, new[] { 0.4f }),
                (ItemType.CursedFlames, 5f, 1.5f, new[] { 0.3f }),
                (ItemType.GoldenShower, 4f, 1.2f, new[] { 0.4f }),
                (ItemType.FrostStaff, 5f, 1.2f, new[] { 0.15f }),
                (ItemType.PoisonStaff, 4f, 1.4f, new[] { 0.15f }),
                (ItemType.VenomStaff, 4f, 1.4f, new[] { 0.15f }),
                (ItemType.UnholyTrident, 6f, 1.5f, new[] { 0.2f }),
                (ItemType.MeteorStaff, 5f, 2f, new[] { 0.5f, 0.9f }),
                (ItemType.SpectreStaff, 6f, 1.6f, new[] { 0.3f }),
                (ItemType.InfernoFork, 6f, 2.5f, new[] { 0.6f, 1.2f }),
                (ItemType.ShadowbeamStaff, 6f, 1.2f, new[] { 0.05f }),
                (ItemType.HeatRay, 6f, 1.2f, new[] { 0.05f }),
                (ItemType.BubbleGun, 4f, 1.2f, new[] { 0.2f }),
                (ItemType.RazorbladeTyphoon, 7f, 2.5f, new[] { 0.4f }),
                (ItemType.BlizzardStaff, 5f, 2f, new[] { 0.5f }),
                (ItemType.SkyFracture, 6f, 1.5f, new[] { 0.12f }),
                (ItemType.LeafBlower, 5f, 1.5f, new[] { 0.3f }),
                (ItemType.LaserRifle, 6f, 1.2f, new[] { 0.1f }),
                (ItemType.BatScepter, 5f, 1.6f, new[] { 0.4f }),
                (ItemType.CrystalVileShard, 5f, 1.2f, new[] { 0.15f }),
                (ItemType.MagnetSphere, 3f, 3f, new[] { 1f, 2f }),
                (ItemType.RainbowRod, 6f, 2f, new[] { 0.6f, 1.2f }),
                (ItemType.NimbusRod, 4f, 3f, new[] { 1.5f }),
                (ItemType.MagicalHarp, 5f, 1.5f, new[] { 0.5f }),
                (ItemType.ClingerStaff, 4f, 1.5f, new[] { 0.5f }),
                (ItemType.ToxicFlask, 4f, 3f, new[] { 0.9f }),
                (ItemType.BookofSkulls, 6f, 2f, new[] { 0.5f }),
                (ItemType.SpiritFlame, 5f, 2.5f, new[] { 0.6f }),
                (ItemType.NebulaBlaze, 6f, 1.5f, new[] { 0.3f }),
                (ItemType.LunarFlareBook, 5f, 1.5f, new[] { 0.45f, 0.8f }),
                (ItemType.NebulaArcanum, 6f, 3f, new[] { 0.6f, 1.6f }),
                // Held: the button stays down for the whole run (player.channel).
                (ItemType.LastPrism, 5f, 4f, new[] { 0.6f, 2f, 3.6f }),
                (ItemType.ChargedBlasterCannon, 6f, 5f, new[] { 0.6f, 3.3f, 4.3f }),
                (ItemType.MedusaHead, 4f, 2f, new[] { 0.3f, 1.1f }),
                // Aimed above the enemy, for its ice to form there; last, so the ice stands in no one's way.
                (ItemType.IceRod, 4f, 1.5f, new[] { 0.25f, 1.2f }),
            };
            foreach (var run in runs)
            {
                if (target == null || target.isDead) break;
                var def = ItemDb.Get(run.Type);
                inv.Slots[slot] = ItemStack.Of(run.Type, 1);
                inv.Selected = slot;
                target.hp = Mathf.Max(target.hp, 99999);
                EnemyDebuffs.Clear();
                Vector2 hold = (Vector2)col.bounds.center + Vector2.right * run.Dist;
                TerraProjectiles.Instance.DevClear();
                Pin(target, trb, hold);
                yield return null;
                var made0 = new Dictionary<ProjKind, int>();
                var hits0 = new Dictionary<ProjKind, int>();
                foreach (var k in magicKinds)
                {
                    made0[k] = TerraProjectiles.DevMade(k);
                    hits0[k] = TerraProjectiles.DevHit(k);
                }
                int life = EnemyStats.Life(target, info), taken = 0, uses = 0, maxAlive = 0, manaUsed = 0;
                var seen = new HashSet<string>();
                // The Nimbus Rod is aimed above the enemy, for its cloud to rain on it; the Ice Rod for its ice.
                var aim = run.Type == ItemType.NimbusRod ? hold + Vector2.up * 3f : run.Type == ItemType.IceRod ? hold + Vector2.up * 2.5f : hold;
                mod.DevAim = aim;
                if (def.Channel) mod.DevHold = true;
                int pic = 0;
                for (float t = 0f; t < run.Seconds; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Pin(target, trb, hold);
                    // What was paid since the last frame (held items pay as they go), then topped up.
                    manaUsed += mod.Stats.ManaMax - mod.Stats.Mana;
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    if (mod.DevReady)
                    {
                        mod.DevUseSelected(aim);
                        uses++;
                    }
                    int now = EnemyStats.Life(target, info);
                    taken += Mathf.Max(0, life - now);
                    if (target.hp < 50000) target.hp = 99999;
                    life = EnemyStats.Life(target, info);
                    for (int k = 0; k < debuffs.Length; k++) if (EnemyDebuffs.Has(target, debuffs[k])) seen.Add(debuffNames[k]);
                    int alive = 0;
                    foreach (var k in magicKinds) alive += TerraProjectiles.Instance.DevCount(k);
                    maxAlive = Mathf.Max(maxAlive, alive);
                    if (pic < run.Pictures.Length && t >= run.Pictures[pic])
                    {
                        Shot(scene, $"m_{run.Type}_{pic}");
                        pic++;
                    }
                    yield return null;
                }
                manaUsed += mod.Stats.ManaMax - mod.Stats.Mana;
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
                    for (int k = 0; k < debuffs.Length; k++) if (EnemyDebuffs.Has(target, debuffs[k])) seen.Add(debuffNames[k]);
                    yield return null;
                }
                var made = new System.Text.StringBuilder();
                foreach (var k in magicKinds)
                {
                    int n = TerraProjectiles.DevMade(k) - made0[k];
                    if (n > 0) made.Append($"{k}×{n}(命中{TerraProjectiles.DevHit(k) - hits0[k]}) ");
                }
                Plugin.Log.LogInfo($"[AutoTest] 魔法 {def.Name}：用 {uses} 次，共耗魔 {manaUsed}（每次 {def.Mana}），敌人共受泰拉伤害 {taken}（面板 {def.Damage}），减益 [{string.Join("、", seen)}]，弹幕 [{made.ToString().Trim()}]，同时最多 {maxAlive}");
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
