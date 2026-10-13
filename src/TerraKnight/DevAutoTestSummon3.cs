using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "summon3", sweep R5e): the 1.4.5 summons, each staff used twice beside an
    /// enemy held a few tiles off (the Mushroom Boi's prey held high, for it to fling itself),
    /// then the Foxsparks taken up to breathe fire: minions, hits and their shots.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Summon3Test(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 召唤3：场景里没有敌人");
                yield break;
            }
            var trb = target.GetComponent<Rigidbody2D>();
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            var minions = Minions.Instance;
            int slot = Inventory.HotbarSize - 1;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(col.bounds.center, Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            Vector2 Centre() => col.bounds.center;
            void Hold(Vector2 at)
            {
                target.hp = Mathf.Max(target.hp, 30000);
                Pin(target, trb, at);
            }
            void Reset()
            {
                minions.Clear();
                PlayerBuffs.Clear();
                SummonTags.Clear();
                EnemyDebuffs.Clear();
                TerraProjectiles.Instance.DevClear();
            }
            IEnumerator Use(ItemType type, Vector2 aim, int times)
            {
                inv.Slots[slot] = ItemStack.Of(type);
                inv.Selected = slot;
                for (int n = 0; n < times; n++)
                {
                    while (!mod.DevReady) yield return null;
                    mod.DevAim = aim;
                    mod.DevUseSelected(aim);
                    yield return null;
                }
            }
            var staves = new (ItemType Staff, Minions.Kind Kind, float High)[]
            {
                (ItemType.MushroomBoiStaff, Minions.Kind.MushroomBoi, 6f), (ItemType.CattivaStaff, Minions.Kind.Cattiva, 0f), (ItemType.TrustyCattivaStaff, Minions.Kind.TrustyCattiva, 0f),
                (ItemType.FoxsparksStaff, Minions.Kind.Foxsparks, 0f), (ItemType.TrustyFoxsparksStaff, Minions.Kind.TrustyFoxsparks, 0f), (ItemType.ClayPotStaff, Minions.Kind.ClayPot, 0f),
                (ItemType.ForbiddenStaff, Minions.Kind.ForbiddenRelic, 2f),
            };
            foreach (var run in staves)
            {
                if (target == null || target.isDead) break;
                Reset();
                var hold = Centre() + new Vector2(side * 2.5f, run.High);
                Hold(hold);
                var def = ItemDb.Get(run.Staff);
                yield return Use(run.Staff, Centre() + new Vector2(side * 2f, 0.5f), IsFoxStaff(run.Staff) ? 1 : 2);
                mod.DevAim = hold;
                int hits0 = Minions.DevHits, dmg0 = Minions.DevHitDamage;
                int launches0 = Minions.DevMushroomLaunches, rides0 = Minions.DevCattivaRides;
                int blasts0 = TerraProjectiles.DevMushroomBlasts, fire0 = TerraProjectiles.DevFoxFireballs, soul0 = TerraProjectiles.DevSoulFireBursts, gems0 = TerraProjectiles.DevForbiddenGems;
                bool pictured = false;
                var debuffs = new System.Collections.Generic.HashSet<string>();
                for (float t = 0f; t < 6f; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Hold(hold);
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.OnFire)) debuffs.Add("着火");
                    if (EnemyDebuffs.Has(target, EnemyDebuffs.Hellfire)) debuffs.Add("地狱火");
                    if (!pictured && t >= 2.5f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, "s3_" + run.Staff, hero, 260, 1);
                    }
                    yield return null;
                }
                mod.DevAim = null;
                int hits = Minions.DevHits - hits0, dmg = Minions.DevHitDamage - dmg0;
                var extra = new System.Text.StringBuilder();
                if (Minions.DevMushroomLaunches > launches0) extra.Append($"飞扑{Minions.DevMushroomLaunches - launches0} ");
                if (TerraProjectiles.DevMushroomBlasts > blasts0) extra.Append($"爆炸{TerraProjectiles.DevMushroomBlasts - blasts0}(命中{TerraProjectiles.DevHit(ProjKind.MushroomBlast)}) ");
                if (TerraProjectiles.DevFoxFireballs > fire0) extra.Append($"火球{TerraProjectiles.DevFoxFireballs - fire0}(命中{TerraProjectiles.DevHit(ProjKind.FoxFireball)}) ");
                if (TerraProjectiles.DevSoulFireBursts > soul0) extra.Append($"灵魂之火爆开{TerraProjectiles.DevSoulFireBursts - soul0} ");
                if (TerraProjectiles.DevForbiddenGems > gems0) extra.Append($"宝石{TerraProjectiles.DevForbiddenGems - gems0}(命中{TerraProjectiles.DevHit(ProjKind.ForbiddenGem)}) ");
                if (Minions.DevCattivaRides > rides0) extra.Append($"骑头{Minions.DevCattivaRides - rides0}帧 ");
                Plugin.Log.LogInfo($"[AutoTest] 召唤3 {def.Name}（面板 {Equipment.Damage(def)}）：{run.Kind}×{minions.DevCount(run.Kind)}，占 {minions.SlotsUsed} 栏，增益 {PlayerBuffs.Name(def.MinionBuff)} x{minions.BuffCount(def.MinionBuff)}，" +
                                   $"6 秒近身命中 {hits} 次 {dmg} 伤害，减益 [{string.Join("、", debuffs)}]，{extra.ToString().Trim()}");
            }
            // The Foxsparks' breath: one out, then the staff used again and held for 2 s.
            if (target != null && !target.isDead)
            {
                Reset();
                var hold = Centre() + new Vector2(side * 4f, 0f);
                Hold(hold);
                yield return Use(ItemType.FoxsparksStaff, Centre() + new Vector2(side * 2f, 0.5f), 1);
                yield return new WaitForSeconds(0.8f);
                int flames0 = TerraProjectiles.DevFoxFlames, flameHits0 = TerraProjectiles.DevHit(ProjKind.FoxFlame);
                mod.DevHold = true;
                mod.DevAim = hold;
                yield return Use(ItemType.FoxsparksStaff, hold, 1);
                bool pictured = false;
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    Hold(hold);
                    if (!pictured && t >= 1f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, "s3_FoxBreath", hero, 260, 1);
                    }
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 召唤3 火绒狐“拥抱烈焰”：火绒狐 {minions.DevCount(Minions.Kind.Foxsparks)} 只（原版 不再多召唤），2 秒喷火 {TerraProjectiles.DevFoxFlames - flames0} 团（原版 每 5 tick 一团，约 24），命中 {TerraProjectiles.DevHit(ProjKind.FoxFlame) - flameHits0}");
            }
            Reset();
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }

        static bool IsFoxStaff(ItemType t) => t == ItemType.FoxsparksStaff || t == ItemType.TrustyFoxsparksStaff;
    }
}
