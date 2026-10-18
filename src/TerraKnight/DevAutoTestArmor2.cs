using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "armor2", accarmor-hm sec-H2): the hardmode sets' bonuses at work on an enemy
    /// held in place: Rapid Healing, the petals, the shards, frostburn, Holy Protection, the leaf
    /// crystal, the thorns, the Spectre bolts, the Nebula boosters, the Beetle orbs, the Solar
    /// shields and dash, the stealth, the Stardust Guardian, the Forbidden storm, the Crystal
    /// Assassin's dash; and close-ups of how the sets look.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Armor2Test(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var savedSlots = (ItemStack[])inv.Slots.Clone();
            var savedAcc = (ItemStack[])inv.Accessories.Clone();
            var savedArmor = (ItemStack[])inv.Armor.Clone();
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.WoodenArrow, 999);
            Wear();
            void Dress(ItemType h, ItemType b, ItemType l)
            {
                inv.Armor[0] = ItemStack.Of(h);
                inv.Armor[1] = ItemStack.Of(b);
                inv.Armor[2] = ItemStack.Of(l);
            }
            void Reset()
            {
                TerraProjectiles.Instance.DevClear();
                PlayerBuffs.Clear();
                EnemyDebuffs.Clear();
                ArmorEffects.Clear();
            }

            // Looks first (no enemy needed).
            foreach (var (name, h, b, l) in new[]
            {
                ("cobalt", ItemType.CobaltHelmet, ItemType.CobaltBreastplate, ItemType.CobaltLeggings),
                ("hallowed", ItemType.HallowedMask, ItemType.HallowedPlateMail, ItemType.HallowedGreaves),
                ("spectre", ItemType.SpectreMask, ItemType.SpectreRobe, ItemType.SpectrePants),
                ("spooky", ItemType.SpookyHelmet, ItemType.SpookyBreastplate, ItemType.SpookyLeggings),
                ("vortex", ItemType.VortexHelmet, ItemType.VortexBreastplate, ItemType.VortexLeggings),
                ("nebula", ItemType.NebulaHelmet, ItemType.NebulaBreastplate, ItemType.NebulaLeggings),
                ("stardust", ItemType.StardustHelmet, ItemType.StardustBreastplate, ItemType.StardustLeggings),
            })
            {
                Reset();
                Dress(h, b, l);
                TerraMovement.DevMove = 1;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) yield return null;
                TerraMovement.DevMove = 0;
                yield return CloseUp(scene, "a3_look_" + name, hero, 90, 3);
                hero.transform.position = home;
                rb.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.3f);
            }

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
                Plugin.Log.LogInfo("[AutoTest] 盔甲2：场景里没有敌人，跳过");
            }
            else
            {
                var trb = target.GetComponent<Rigidbody2D>();
                const int terrain = 1 << World.ChunkColliders.TerrainLayer;
                float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
                float side = Room(1f) >= Room(-1f) ? 1f : -1f;
                var near = C() + Vector2.right * side * 2f;
                void Hold(Vector2 at)
                {
                    target.hp = Mathf.Max(target.hp, 3000);
                    Pin(target, trb, at);
                    mod.Stats.Mana = mod.Stats.ManaMax;
                }
                IEnumerator Watch(float seconds, Vector2 at)
                {
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold(at);
                        yield return null;
                    }
                }
                IEnumerator UseFor(ItemType item, float seconds, Vector2 at)
                {
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold(at);
                        if (mod.DevReady) mod.DevUse(item, at);
                        yield return null;
                    }
                }
                // The enemy's touch on the player (the thorns need the attacker).
                IEnumerator Touch()
                {
                    PlayerData.instance.isInvincible = false;
                    hero.cState.invulnerable = false;
                    mod.Stats.Life = mod.Stats.LifeMax;
                    PlayerHooks.HurtPlayer(hero, 1, 1, target.gameObject);
                    yield return null;
                    PlayerData.instance.isInvincible = true;
                }

                // Palladium: Rapid Healing on a hit.
                Reset();
                Dress(ItemType.PalladiumMask, ItemType.PalladiumBreastplate, ItemType.PalladiumLeggings);
                yield return UseFor(ItemType.CopperBroadsword, 1.2f, near);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 钯金：快速治疗={PlayerBuffs.Has(ArmorEffects.RapidHealing)}（{PlayerBuffs.TimeLeft(ArmorEffects.RapidHealing)} tick，原版 300），额外再生 {ArmorEffects.RegenBonus}（原版 4 即 2 HP/s）");

                // Orichalcum: a petal every 20 ticks of hitting.
                Reset();
                Dress(ItemType.OrichalcumMask, ItemType.OrichalcumBreastplate, ItemType.OrichalcumLeggings);
                int petals0 = ArmorEffects.DevPetals;
                yield return UseFor(ItemType.CopperBroadsword, 2.5f, near);
                Shot(scene, "a3_petals");
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 山铜：花瓣 {ArmorEffects.DevPetals - petals0}，命中 {TerraProjectiles.DevHit(ProjKind.OrichalcumPetal)}（20 tick 至多一片）");

                // Titanium: shards up to seven.
                Reset();
                Dress(ItemType.TitaniumMask, ItemType.TitaniumBreastplate, ItemType.TitaniumLeggings);
                int shards0 = ArmorEffects.DevShards;
                yield return UseFor(ItemType.CopperBroadsword, 3f, near);
                yield return CloseUp(scene, "a3_titanium", hero, 100, 3);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 钛金：碎片生成 {ArmorEffects.DevShards - shards0}，命中 {TerraProjectiles.DevHit(ProjKind.TitaniumShard)}，钛金屏障={PlayerBuffs.Has(ArmorEffects.TitaniumStorm)}");

                // Frost: frostburn from melee.
                Reset();
                Dress(ItemType.FrostHelmet, ItemType.FrostBreastplate, ItemType.FrostLeggings);
                yield return UseFor(ItemType.CopperBroadsword, 0.6f, near);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 寒霜：冻伤={EnemyDebuffs.Has(target, EnemyDebuffs.Frostbite)}");

                // Hallowed: Holy Protection after a hit, spent on the next blow, none again for 30 s.
                Reset();
                Dress(ItemType.HallowedMask, ItemType.HallowedPlateMail, ItemType.HallowedGreaves);
                yield return UseFor(ItemType.CopperBroadsword, 0.6f, near);
                bool holy = PlayerBuffs.Has(ArmorEffects.HolyProtection);
                yield return Watch(0.6f, near);
                yield return CloseUp(scene, "a3_holy", hero, 90, 3);
                int dodges0 = ArmorEffects.DevHolyDodges;
                yield return Touch();
                bool after = PlayerBuffs.Has(ArmorEffects.HolyProtection);
                yield return Watch(1.6f, near);
                yield return UseFor(ItemType.CopperBroadsword, 0.6f, near);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 神圣：命中后神圣防护={holy}，受击闪避 {ArmorEffects.DevHolyDodges - dodges0}（1），之后={after}，30 秒内再命中={PlayerBuffs.Has(ArmorEffects.HolyProtection)}（False）");

                // Chlorophyte: the crystal shoots every 40 ticks.
                Reset();
                Dress(ItemType.ChlorophyteMask, ItemType.ChlorophytePlateMail, ItemType.ChlorophyteGreaves);
                int leaf0 = TerraProjectiles.DevMade(ProjKind.CrystalLeafShot);
                yield return Watch(3f, near);
                yield return CloseUp(scene, "a3_chloro", hero, 110, 3);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 叶绿：水晶 {TerraProjectiles.DevMade(ProjKind.CrystalLeaf)}，叶弹 {TerraProjectiles.DevMade(ProjKind.CrystalLeafShot) - leaf0}（3 秒约 4），命中 {TerraProjectiles.DevHit(ProjKind.CrystalLeafShot)}，减伤 {Equipment.Current.Endurance:F2}（0.05）");

                // Turtle: the touch answered twice over.
                Reset();
                Dress(ItemType.TurtleHelmet, ItemType.TurtleScaleMail, ItemType.TurtleLeggings);
                yield return Watch(0.3f, near);
                int thorns0 = ArmorEffects.DevThorns;
                yield return Touch();
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 海龟：反伤 {ArmorEffects.DevThorns - thorns0}（1），受伤 {TerraHurt.LastDamage}，减伤 {Equipment.Current.Endurance:F2}（0.15）");
                yield return Watch(1.2f, near);

                // Spectre: the hood's healing spirits, the mask's wrath.
                Reset();
                Dress(ItemType.SpectreHood, ItemType.SpectreRobe, ItemType.SpectrePants);
                int heals0 = ArmorEffects.DevHeals;
                for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                {
                    mod.Stats.Life = mod.Stats.LifeMax / 2;
                    Hold(near);
                    if (mod.DevReady) mod.DevUse(ItemType.WandofSparking, near);
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 幽灵兜帽：治疗灵 {TerraProjectiles.DevMade(ProjKind.SpiritHeal)}，治疗到 {ArmorEffects.DevHeals - heals0}，魔法伤害 {Equipment.Current.MagicDamage * 100f:F0}%（-25）");
                Reset();
                Dress(ItemType.SpectreMask, ItemType.SpectreRobe, ItemType.SpectrePants);
                int wraths0 = ArmorEffects.DevWraths;
                yield return UseFor(ItemType.WandofSparking, 2.5f, near);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 幽灵面具：怒气 {ArmorEffects.DevWraths - wraths0}，命中 {TerraProjectiles.DevHit(ProjKind.SpectreWrath)}");

                // Nebula: boosters from magic hits, picked up for levels.
                Reset();
                Dress(ItemType.NebulaHelmet, ItemType.NebulaBreastplate, ItemType.NebulaLeggings);
                int boost0 = ArmorEffects.DevBoosters;
                yield return UseFor(ItemType.CrystalStorm, 4f, near);
                yield return Watch(1.5f, near);
                yield return CloseUp(scene, "a3_nebula", hero, 100, 3);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 星云：强化焰 {ArmorEffects.DevBoosters - boost0}（30 tick 冷却，1/3），等级 生命/魔力/伤害 {ArmorEffects.NebulaLifeLevel}/{ArmorEffects.NebulaManaLevel}/{ArmorEffects.NebulaDamageLevel}");

                // Beetle: Endurance's orbs every 3 s, one spent per blow; Might's from hitting.
                Reset();
                Dress(ItemType.BeetleHelmet, ItemType.BeetleShell, ItemType.BeetleLeggings);
                yield return Watch(9.6f, near);
                int orbs = ArmorEffects.BeetleOrbs;
                yield return CloseUp(scene, "a3_beetle", hero, 100, 3);
                yield return Touch();
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 甲虫壳：9.6 秒 {orbs} 个甲虫（3），受击后 {ArmorEffects.BeetleOrbs}（2），受伤 {TerraHurt.LastDamage}");
                Reset();
                Dress(ItemType.BeetleHelmet, ItemType.BeetleScaleMail, ItemType.BeetleLeggings);
                // Might needs real melee damage: over 400 a level against a drain of 3+ a tick.
                yield return UseFor(ItemType.TerraBlade, 5f, near);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 甲虫铠甲：挥 5 秒 {ArmorEffects.BeetleOrbs} 个甲虫，近战伤害 +{Equipment.Current.MeleeDamage * 100f:F0}%");

                // Solar: a shield every 3 s; a blow spends one for 20% off and a flare; the dash hits.
                Reset();
                Dress(ItemType.SolarFlareHelmet, ItemType.SolarFlareBreastplate, ItemType.SolarFlareLeggings);
                yield return Watch(9.6f, near);
                int shields = ArmorEffects.SolarShields;
                yield return CloseUp(scene, "a3_solar", hero, 90, 3);
                int flares0 = ArmorEffects.DevFlares;
                yield return Touch();
                int afterHit = ArmorEffects.SolarShields;
                yield return Watch(1.2f, C() + Vector2.right * side * 4f);
                TerraMovement.RequestDash((int)side);
                int kind = 0;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    kind = Mathf.Max(kind, TerraMovement.DevDashKind);
                    Hold(C() + Vector2.right * side * 3f);
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 日耀：9.6 秒护盾 {shields}（3），受击后 {afterHit}（2），耀斑 {ArmorEffects.DevFlares - flares0}，冲刺种类 {kind}（3），减伤 {Equipment.Current.Endurance:F2}（0.12）");
                hero.transform.position = home;
                yield return Watch(0.5f, near);

                // Shroomite: standing still hides; the ranged share.
                Reset();
                Dress(ItemType.ShroomiteMask, ItemType.ShroomiteBreastplate, ItemType.ShroomiteLeggings);
                yield return Watch(1.5f, near);
                yield return CloseUp(scene, "a3_shroomite", hero, 90, 3);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 蘑菇矿：静止 1.5 秒隐身 {ArmorEffects.Stealth:F2}（0），远程伤害 +{Equipment.Current.RangedDamage * 100f:F0}%（13+50）");

                // Vortex: the key toggles stealth, slowing the run.
                Reset();
                Dress(ItemType.VortexHelmet, ItemType.VortexBreastplate, ItemType.VortexLeggings);
                yield return Watch(0.2f, near);
                ArmorEffects.KeyDoubleTapDown();
                yield return Watch(1f, near);
                yield return CloseUp(scene, "a3_vortex_stealth", hero, 90, 3);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 星旋：隐身={ArmorEffects.VortexStealth}，{ArmorEffects.Stealth:F2}（0），远程伤害 +{Equipment.Current.RangedDamage * 100f:F0}%（36+80），跑速 ×{ArmorEffects.RunSpeedMult}（0.3）");
                ArmorEffects.KeyDoubleTapDown();

                // Stardust: the guardian follows and punches.
                Reset();
                Dress(ItemType.StardustHelmet, ItemType.StardustBreastplate, ItemType.StardustLeggings);
                int punches0 = ArmorEffects.DevGuardianHits;
                yield return Watch(1.5f, near);
                yield return CloseUp(scene, "a3_stardust", hero, 130, 2);
                yield return Watch(2.5f, near);
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 星尘：守卫 {TerraProjectiles.DevMade(ProjKind.StardustGuardian)}，出拳命中 {ArmorEffects.DevGuardianHits - punches0}，星尘守卫增益={PlayerBuffs.Has(ArmorEffects.StardustGuardian)}");

                // Forbidden: the storm at the cursor.
                Reset();
                Dress(ItemType.AncientBattleArmorHat, ItemType.AncientBattleArmorShirt, ItemType.AncientBattleArmorPants);
                int storms0 = ArmorEffects.DevStorms;
                mod.DevAim = near;
                yield return Watch(0.2f, near);
                ArmorEffects.KeyDoubleTapDown();
                yield return Watch(1.2f, near);
                Shot(scene, "a3_forbidden");
                yield return Watch(1.5f, near);
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 禁戒：沙暴 {ArmorEffects.DevStorms - storms0}（1），命中 {TerraProjectiles.DevHit(ProjKind.SandnadoFriendly)}");

                // Crystal Assassin: its own dash.
                Reset();
                Dress(ItemType.CrystalNinjaHelmet, ItemType.CrystalNinjaChestplate, ItemType.CrystalNinjaLeggings);
                yield return Watch(0.3f, C() + Vector2.right * side * 30f);
                TerraMovement.RequestDash((int)side);
                int ckind = 0;
                float top = 0f;
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    ckind = Mathf.Max(ckind, TerraMovement.DevDashKind);
                    top = Mathf.Max(top, Mathf.Abs(rb.linearVelocity.x) / TerraMovement.PxT);
                    if (t > 0.1f && t < 0.12f) Shot(scene, "a3_crystal_dash");
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 盔甲2 水晶刺客：冲刺种类 {ckind}（5），最快 {top:F1}（16.9）");
                hero.transform.position = home;
            }

            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = savedSlots[i];
            for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = savedArmor[i];
            for (int i = 0; i < Inventory.AccessorySlots; i++) inv.Accessories[i] = savedAcc[i];
            inv.Selected = savedSelected;
            PlayerData.instance.isInvincible = false;
            mod.DevNoFallDamage = false;
        }
    }
}
