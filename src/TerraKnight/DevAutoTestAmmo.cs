using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ammo"): every arrow and bullet fired once at an enemy held in front of the
    /// player (an Iron Bow or a Handgun): the life it took, the debuffs it left, what it spawned
    /// (stars, shards, the Luminite afterimage), with a picture of the ones that show something.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AmmoTest(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 弹药：场景里没有敌人");
                yield break;
            }
            var ammo = new[]
            {
                ItemType.WoodenArrow, ItemType.FlamingArrow, ItemType.UnholyArrow, ItemType.JestersArrow, ItemType.HellfireArrow, ItemType.HolyArrow,
                ItemType.CursedArrow, ItemType.FrostburnArrow, ItemType.ChlorophyteArrow, ItemType.IchorArrow, ItemType.VenomArrow, ItemType.BoneArrow,
                ItemType.EndlessQuiver, ItemType.LuminiteArrow, ItemType.ShimmerArrow,
                ItemType.MusketBall, ItemType.MeteorShot, ItemType.SilverBullet, ItemType.TungstenBullet, ItemType.CrystalBullet, ItemType.CursedBullet,
                ItemType.IchorBullet, ItemType.ChlorophyteBullet, ItemType.HighVelocityBullet, ItemType.VenomBullet, ItemType.PartyBullet, ItemType.NanoBullet,
                ItemType.ExplodingBullet, ItemType.GoldenBullet, ItemType.EndlessMusketPouch, ItemType.LuminiteBullet,
            };
            var pictures = new HashSet<ItemType>
            {
                ItemType.HellfireArrow, ItemType.HolyArrow, ItemType.CursedArrow, ItemType.LuminiteArrow, ItemType.ShimmerArrow, ItemType.CrystalBullet,
                ItemType.ChlorophyteBullet, ItemType.HighVelocityBullet, ItemType.VenomBullet, ItemType.PartyBullet, ItemType.ExplodingBullet, ItemType.LuminiteBullet,
            };
            var saved = (ItemStack[])inv.Slots.Clone();
            var trb = target.GetComponent<Rigidbody2D>();
            int[] debuffs = { EnemyDebuffs.OnFire, EnemyDebuffs.CursedInferno, EnemyDebuffs.Frostburn, EnemyDebuffs.Ichor, EnemyDebuffs.Venom,
                              EnemyDebuffs.PotentAcid, EnemyDebuffs.Midas, EnemyDebuffs.Confused };
            string[] debuffNames = { "着火", "诅咒狱火", "霜冻", "灵液", "酸性毒液", "烈性酸液", "迈达斯", "困惑" };
            foreach (var a in ammo)
            {
                if (target == null || target.isDead) break;
                var def = ItemDb.Get(a);
                // Only this ammo in the inventory.
                for (int i = 0; i < Inventory.Size; i++) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Def.Ammo != AmmoKind.None) inv.Slots[i] = default;
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(a, def.MaxStack > 1 ? 50 : 1);
                var weapon = def.Ammo == AmmoKind.Arrow ? ItemType.IronBow : ItemType.Handgun;
                target.hp = Mathf.Max(target.hp, 400);
                EnemyDebuffs.Clear();
                Vector2 hold = (Vector2)col.bounds.center + Vector2.right * 4f;
                target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                if (trb != null) trb.linearVelocity = Vector2.zero;
                yield return null;
                var info = EnemyStats.Of(target);
                int life0 = EnemyStats.Life(target, info);
                int left0 = inv.Slots[Inventory.Size - 1].Count;
                mod.DevUse(weapon, hold);
                var shot = def.Shoot;
                int spawned = 0, stars = 0, shards = 0, trails = 0;
                bool shotTaken = false;
                var seen = new HashSet<string>();
                for (float t = 0f; t < 2.2f; t += Time.deltaTime)
                {
                    if (target == null || target.isDead) break;
                    target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                    if (trb != null) trb.linearVelocity = Vector2.zero;
                    var pr = TerraProjectiles.Instance;
                    spawned = Mathf.Max(spawned, pr.DevCount(shot));
                    stars = Mathf.Max(stars, pr.DevCount(ProjKind.HallowStar));
                    shards = Mathf.Max(shards, pr.DevCount(ProjKind.CrystalShard));
                    trails = Mathf.Max(trails, pr.DevCount(ProjKind.LuminiteTrail));
                    for (int k = 0; k < debuffs.Length; k++) if (EnemyDebuffs.Has(target, debuffs[k])) seen.Add(debuffNames[k]);
                    if (!shotTaken && pictures.Contains(a) && t >= (a == ItemType.HolyArrow ? 0.7f : a == ItemType.LuminiteArrow ? 0.35f : 0.12f))
                    {
                        shotTaken = true;
                        Shot(scene, "a_" + a);
                    }
                    yield return null;
                }
                int life1 = target != null && !target.isDead ? EnemyStats.Life(target, EnemyStats.Of(target)) : 0;
                int left1 = inv.Slots[Inventory.Size - 1].Count;
                Plugin.Log.LogInfo($"[AutoTest] 弹药 {def.Name}（{weapon}）：敌人泰拉血量 {life0} -> {life1}，减益 [{string.Join("、", seen)}]，" +
                                   $"弹幕最多 {spawned}，圣星 {stars}，碎片 {shards}，夜明拖尾 {trails}，弹药 {left0} -> {left1}");
                yield return new WaitForSeconds(0.3f);
            }
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            mod.DevNoFallDamage = false;
        }
    }
}
