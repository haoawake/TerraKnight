using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2", sweep S1.A): the plain swung items' own rules on an enemy held in
    /// place (the rapid bonus, the Breaker Blade, the bones, the ichor, the spikes and cuts with
    /// their bleeding, the Bat Bat, the debuffs, the Tax Collector's coins, the Ham Bat's meal),
    /// the Psycho Knife's stealth, the Brand of the Inferno's shield and parry, the umbrellas'
    /// fall; close-ups of the hold styles, the thrust and the raised shield.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Melee2Test(string scene, HeroController hero)
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
            int savedSelected = inv.Selected;
            Vector2 C() => col.bounds.center;
            Wear();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            void Take(ItemType t)
            {
                inv.Slots[Inventory.HotbarSize - 1] = ItemStack.Of(t);
                inv.Selected = Inventory.HotbarSize - 1;
            }
            IEnumerator Wait(float s) { for (float t = 0f; t < s; t += Time.deltaTime) yield return null; }

            // --- Looks: the hold styles, the thrust.
            foreach (var (item, name) in new[] { (ItemType.Umbrella, "m2_umbrella_hold"), (ItemType.ZombieArm, "m2_arm_hold"), (ItemType.BreathingReed, "m2_reed_hold") })
            {
                Take(item);
                yield return Wait(0.3f);
                yield return CloseUp(scene, name, hero, 90, 3);
            }
            Take(ItemType.TragicUmbrella);
            mod.DevUseSelected(C() + Vector2.right * side * 3f);
            yield return Wait(0.2f);
            yield return CloseUp(scene, "m2_umbrella_thrust", hero, 90, 3);
            yield return Wait(0.4f);

            // --- The umbrella's fall: no faster than 2 px a tick (vanilla), against 10 without.
            // As high as the room allows (up to 8 units), held there a moment, then let go.
            var upHit = Physics2D.Raycast(C(), Vector2.up, 12f, terrain);
            float lift = Mathf.Clamp((upHit.collider != null ? upHit.distance : 12f) - 1.5f, 1f, 8f);
            float fastest = 0f;
            IEnumerator Fall(ItemType item)
            {
                Take(item);
                fastest = 0f;
                for (float t = 0f; t < 0.15f; t += Time.deltaTime)
                {
                    hero.transform.position = (Vector2)home + Vector2.up * lift;
                    rb.linearVelocity = Vector2.zero;
                    yield return null;
                }
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    fastest = Mathf.Max(fastest, -rb.linearVelocity.y / TerraMovement.PxT);
                    if (t > 0.2f && hero.cState.onGround) break;
                    yield return null;
                }
            }
            yield return Fall(ItemType.Umbrella);
            float umbrellaFall = fastest;
            yield return Fall(ItemType.Katana);
            Plugin.Log.LogInfo($"[AutoTest] 近战2 伞：从 {lift / Effects.TerraParticles.Px:F0}px 高落下，最快 {umbrellaFall:F2} px/tick（原版 2），不拿伞 {fastest:F2}（原版 10）");
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return Wait(0.6f);

            // --- The Psycho Knife's stealth: standing still it fades 0.02 a tick after 15 ticks.
            Take(ItemType.PsychoKnife);
            yield return Wait(1.6f);
            yield return CloseUp(scene, "m2_psycho", hero, 70, 3);
            Plugin.Log.LogInfo($"[AutoTest] 近战2 变态人的刀：站立 1.6 秒后隐身度 {MeleeSpecials.Stealth:F2}（原版 0），近战伤害 +{Equipment.Current.MeleeDamage * 100f:F0}%（含 +300%），暴击 +{Equipment.Current.MeleeCrit * 100f:F0}");

            // --- The Brand of the Inferno's shield: only with a shield worn.
            Take(ItemType.DD2SquireDemonSword);
            MeleeSpecials.DevShield = true;
            yield return Wait(0.2f);
            bool upBare = MeleeSpecials.ShieldRaised;
            MeleeSpecials.DevShield = false;
            yield return Wait(0.4f);
            Wear(ItemType.CobaltShield);
            int def0 = Equipment.Current.Defense;
            MeleeSpecials.DevShield = true;
            yield return Wait(0.1f);
            yield return CloseUp(scene, "m2_shield", hero, 70, 3);
            Plugin.Log.LogInfo($"[AutoTest] 近战2 地狱之剑：不戴盾举盾={upBare}（False），戴钴护盾举盾={MeleeSpecials.ShieldRaised}，防御 {def0}→{Equipment.Current.Defense}（+20），格挡窗 {MeleeSpecials.ParryLeft} tick");

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
                MeleeSpecials.DevShield = false;
                Plugin.Log.LogInfo("[AutoTest] 近战2：场景里没有敌人，跳过敌人部分");
            }
            else
            {
                var trb = target.GetComponent<Rigidbody2D>();
                var near = C() + Vector2.right * side * 1.6f;
                var info = EnemyStats.Of(target);
                void Hold() { target.hp = Mathf.Max(target.hp, 3000); Pin(target, trb, near); }
                IEnumerator Watch(float seconds) { for (float t = 0f; t < seconds; t += Time.deltaTime) { if (target == null || target.isDead) yield break; Hold(); yield return null; } }
                IEnumerator SwingFor(ItemType item, float seconds, int swings = 99)
                {
                    Take(item);
                    int n = 0;
                    for (float t = 0f; t < seconds; t += Time.deltaTime)
                    {
                        if (target == null || target.isDead) yield break;
                        Hold();
                        if (mod.DevReady && n < swings)
                        {
                            mod.DevUseSelected(near);
                            n++;
                        }
                        yield return null;
                    }
                }
                void Reset()
                {
                    TerraProjectiles.Instance.DevClear();
                    PlayerBuffs.Clear();
                    EnemyDebuffs.Clear();
                    MeleeSpecials.DevHits.Clear();
                }
                int Hits(ItemType t) => MeleeSpecials.DevHits.TryGetValue(t, out int n) ? n : 0;

                // The parry: the enemy's touch in the first 20 ticks of the shield coming up.
                MeleeSpecials.DevShield = false;
                yield return Watch(0.4f);
                hero.cState.facingRight = side > 0f;
                MeleeSpecials.DevShield = true;
                // The shield goes up on the next 60 Hz tick.
                for (float t = 0f; t < 0.3f && !MeleeSpecials.ShieldRaised; t += Time.deltaTime) yield return null;
                hero.cState.facingRight = side > 0f;
                PlayerData.instance.isInvincible = false;
                hero.cState.invulnerable = false;
                mod.Stats.Life = mod.Stats.LifeMax;
                int life0 = mod.Stats.Life, parries0 = MeleeSpecials.DevParries;
                PlayerHooks.HurtPlayer(hero, 1, side > 0f ? -1 : 1, target.gameObject);
                yield return null;
                PlayerData.instance.isInvincible = true;
                bool striking = PlayerBuffs.Has(MeleeSpecials.StrikingMoment);
                MeleeSpecials.DevShield = false;
                yield return Watch(0.5f);
                yield return SwingFor(ItemType.DD2SquireDemonSword, 0.4f, 1);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 格挡：格挡 {MeleeSpecials.DevParries - parries0}（1），掉血 {life0 - mod.Stats.Life}（0），惊人一刻={striking}，" +
                                   $"下一击 {MeleeSpecials.DevLastBase}→{MeleeSpecials.DevLastModified}（×5），用掉后={PlayerBuffs.Has(MeleeSpecials.StrikingMoment)}（False）");
                Wear();

                // The rapid attack bonus: +0.12 a hit to 0.62, at most +50% counted.
                Reset();
                yield return SwingFor(ItemType.Cutlass, 2f);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 短弯刀：命中 {Hits(ItemType.Cutlass)}，快攻加成 {MeleeSpecials.DevRapid:F2}（上限 0.62），最后一击 {MeleeSpecials.DevLastBase}→{MeleeSpecials.DevLastModified}（×1.5）");

                // The Breaker Blade: ×2.5 at 90% life or more.
                Reset();
                Take(ItemType.BreakerBlade);
                target.hp = Mathf.CeilToInt(info.MaxHp);
                Pin(target, trb, near);
                mod.DevUseSelected(near);
                for (float t = 0f; t < 0.7f && Hits(ItemType.BreakerBlade) == 0; t += Time.deltaTime) { Pin(target, trb, near); yield return null; }
                int fullBase = MeleeSpecials.DevLastBase, fullMod = MeleeSpecials.DevLastModified;
                yield return Wait(0.3f);
                target.hp = Mathf.Max(2, Mathf.CeilToInt(info.MaxHp * 0.5f));
                int h0 = Hits(ItemType.BreakerBlade);
                while (!mod.DevReady) yield return null;
                mod.DevUseSelected(near);
                for (float t = 0f; t < 0.7f && Hits(ItemType.BreakerBlade) == h0; t += Time.deltaTime) { Pin(target, trb, near); yield return null; }
                Plugin.Log.LogInfo($"[AutoTest] 近战2 毁灭刃：满血 {fullBase}→{fullMod}（×2.5），半血 {MeleeSpecials.DevLastBase}→{MeleeSpecials.DevLastModified}（×1）");
                yield return Watch(0.3f);

                // The bones, the ichor stream and the debuffs.
                Reset();
                yield return SwingFor(ItemType.BoneSword, 1.6f);
                yield return Watch(0.6f);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 骨剑：命中 {Hits(ItemType.BoneSword)}，骨头 {TerraProjectiles.DevMade(ProjKind.BoneSwordBone)}（每击一根），骨头命中 {TerraProjectiles.DevHit(ProjKind.BoneSwordBone)}");
                Reset();
                yield return SwingFor(ItemType.Bladetongue, 1.2f);
                yield return CloseUp(scene, "m2_ichor", hero, 110, 2);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 舌锋剑：命中 {Hits(ItemType.Bladetongue)}，灵液流 {TerraProjectiles.DevMade(ProjKind.IchorSplash)}，命中 {TerraProjectiles.DevHit(ProjKind.IchorSplash)}，灵液={EnemyDebuffs.Has(target, EnemyDebuffs.Ichor)}");
                Reset();
                bool confused = false;
                Take(ItemType.PurpleClubberfish);
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    Hold();
                    if (mod.DevReady) mod.DevUseSelected(near);
                    confused |= EnemyDebuffs.Has(target, EnemyDebuffs.Confused);
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 近战2 紫挥棒鱼：命中 {Hits(ItemType.PurpleClubberfish)}，期间困惑过={confused}（每击 1/2）");
                Reset();
                yield return SwingFor(ItemType.DD2SquireDemonSword, 2f);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 地狱之剑：命中 {Hits(ItemType.DD2SquireDemonSword)}，狱火={EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire)}（每击 1/4）");

                // The spikes and cuts: one a swing, five at most, bleeding by how many.
                foreach (var (item, kind, debuff, label, pic) in new[]
                {
                    (ItemType.TentacleSpike, ProjKind.TentacleSpikeStuck, EnemyDebuffs.TentacleSpiked, "触手钉锤 尖刺", "m2_spikes"),
                    (ItemType.BloodButcherer, ProjKind.BloodButchererStuck, EnemyDebuffs.BloodButchered, "血腥屠刀 血腥屠宰", (string)null),
                })
                {
                    Reset();
                    yield return SwingFor(item, 3.2f);
                    int stuck = TerraProjectiles.Instance.Stuck(target, kind);
                    if (pic != null) yield return CloseUp(scene, pic, hero, 90, 3);
                    // The bleeding alone over 2 s: the enemy left at a set life, not swung at.
                    target.hp = 2000;
                    int hp0 = target.hp;
                    for (float t = 0f; t < 2f; t += Time.deltaTime) { Pin(target, trb, near); yield return null; }
                    int lost = Mathf.CeilToInt((hp0 - target.hp) * info.Scale);
                    Plugin.Log.LogInfo($"[AutoTest] 近战2 {label}：命中 {Hits(item)}，插着 {stuck}（上限 5），减益={EnemyDebuffs.Has(target, debuff)}，2 秒流血约 {lost}（{stuck} 个原版 {stuck * (kind == ProjKind.TentacleSpikeStuck ? 3 : 4) * 2}）");
                    yield return Watch(0.2f);
                }

                // The Bat Bat: a life point once a swing.
                Reset();
                mod.Stats.Life = mod.Stats.LifeMax - 30;
                int bat0 = MeleeSpecials.DevBatHeals;
                yield return SwingFor(ItemType.BatBat, 2.5f);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 蝙蝠棍：命中 {Hits(ItemType.BatBat)}，回血 {MeleeSpecials.DevBatHeals - bat0} 次（每次挥动最多一次）");
                mod.Stats.Life = mod.Stats.LifeMax;

                // The Tax Collector's cane: a copper a hit.
                Reset();
                long purse0 = Shop.Purse;
                int coins0 = MeleeSpecials.DevCoins;
                yield return SwingFor(ItemType.TaxCollectorsStickOfDoom, 1.2f);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 精致手杖：命中 {Hits(ItemType.TaxCollectorsStickOfDoom)}，铜币 {MeleeSpecials.DevCoins - coins0}，钱包 +{Shop.Purse - purse0} 铜");

                // Plain swings: they hit.
                Reset();
                foreach (var item in new[] { ItemType.WoodenSword, ItemType.PearlwoodSword, ItemType.TinBroadsword, ItemType.CactusSword, ItemType.Katana, ItemType.FalconBlade,
                    ItemType.SlapHand, ItemType.ZombieArm, ItemType.BladedGlove, ItemType.LucyTheAxe, ItemType.HamBat, ItemType.PsychoKnife, ItemType.Umbrella, ItemType.CandyCaneSword })
                    yield return SwingFor(item, 0.5f, 1);
                var sb = new System.Text.StringBuilder();
                foreach (var kv in MeleeSpecials.DevHits) sb.Append($"{ItemDb.Get(kv.Key).Name}={kv.Value} ");
                Plugin.Log.LogInfo($"[AutoTest] 近战2 普通挥砍命中：{sb}");
                Take(ItemType.Katana);
                mod.DevUseSelected(near);
                yield return Wait(0.12f);
                yield return CloseUp(scene, "m2_katana_swing", hero, 80, 3);

                // The Psycho Knife's hit gives the stealth away.
                Take(ItemType.PsychoKnife);
                yield return Watch(1.6f);
                float hidden = MeleeSpecials.Stealth;
                yield return SwingFor(ItemType.PsychoKnife, 0.3f, 1);
                Plugin.Log.LogInfo($"[AutoTest] 近战2 变态人的刀：出手前隐身度 {hidden:F2}，命中后 {MeleeSpecials.Stealth:F2}（1）");

                // The Ham Bat's kill: Hearty Meal for 7 s.
                Reset();
                Take(ItemType.HamBat);
                target.hp = 1;
                Pin(target, trb, near);
                mod.DevUseSelected(near);
                for (float t = 0f; t < 0.8f && !target.isDead; t += Time.deltaTime) { if (!target.isDead) Pin(target, trb, near); yield return null; }
                yield return null;
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2 火腿棍：击杀={target.isDead}，丰盛大餐={PlayerBuffs.Has(MeleeSpecials.HeartyMeal)}（{PlayerBuffs.TimeLeft(MeleeSpecials.HeartyMeal)} tick，原版 420），再生 +{Equipment.Current.LifeRegen}");
            }

            MeleeSpecials.DevShield = false;
            MeleeSpecials.Clear();
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            System.Array.Copy(savedAcc, inv.Accessories, savedAcc.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
