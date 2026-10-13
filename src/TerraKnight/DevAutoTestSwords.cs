using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "swords"): the hardmode swords held on an enemy kept in front of the player: the
    /// life each took, the projectiles each made and how many hits they landed (the blade swings and
    /// slashes of SwordFx by vanilla type), the Influx Waver's strikes from the ring, the Meowmere's
    /// bounces, the Star Wrath's stars reaching the cursor's height; with pictures in flight.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ProjKind[] swordKinds =
        {
            ProjKind.FlamingJack, ProjKind.SeedlerSeed, ProjKind.SeedlerSeedlet, ProjKind.InfluxWaver, ProjKind.Meowmere, ProjKind.StarWrath,
            ProjKind.CobaltNaginata, ProjKind.PalladiumPike, ProjKind.MythrilHalberd, ProjKind.OrichalcumHalberd, ProjKind.AdamantiteGlaive, ProjKind.TitaniumTrident,
            ProjKind.Gungnir, ProjKind.MushroomSpear, ProjKind.ChlorophytePartisan, ProjKind.NorthPole,
            ProjKind.SpearMushroom, ProjKind.SporeCloud, ProjKind.NorthPoleSpear, ProjKind.NorthPoleFlake,
            ProjKind.LightDisc, ProjKind.Bananarang, ProjKind.PossessedHatchet, ProjKind.PaladinsHammer,
        };

        IEnumerator SwordsTest(string scene, HeroController hero)
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
                Plugin.Log.LogInfo("[AutoTest] 剑：场景里没有敌人");
                yield break;
            }
            Plugin.Log.LogInfo($"[AutoTest] 剑：靶子 {target.name}");
            var saved = (ItemStack[])inv.Slots.Clone();
            int savedSelected = inv.Selected;
            var trb = target.GetComponent<Rigidbody2D>();
            int slot = Inventory.HotbarSize - 1;
            var info = EnemyStats.Of(target);

            // One sword: distance to the enemy (units), how long to keep swinging, when to take the pictures.
            var runs = new (ItemType Type, float Dist, float Seconds, float[] Pictures)[]
            {
                (ItemType.TrueNightsEdge, 4f, 1.6f, new[] { 0.12f, 0.3f }),
                (ItemType.TrueExcalibur, 2.2f, 1.2f, new[] { 0.1f, 0.25f }),
                (ItemType.TheHorsemansBlade, 2f, 2.4f, new[] { 0.15f, 0.8f }),
                (ItemType.Seedler, 3f, 2.4f, new[] { 0.3f, 1.2f }),
                (ItemType.InfluxWaver, 4f, 2f, new[] { 0.15f, 0.45f }),
                (ItemType.Meowmere, 5f, 2.4f, new[] { 0.3f, 0.9f }),
                (ItemType.StarWrath, 3f, 2.4f, new[] { 0.35f, 0.7f }),
                (ItemType.CobaltSword, 1.8f, 0.8f, new float[0]),
                (ItemType.PalladiumSword, 1.8f, 0.8f, new float[0]),
                (ItemType.MythrilSword, 1.8f, 0.8f, new float[0]),
                (ItemType.OrichalcumSword, 1.8f, 0.8f, new float[0]),
                (ItemType.AdamantiteSword, 1.8f, 0.8f, new float[0]),
                (ItemType.TitaniumSword, 1.8f, 0.8f, new float[] { 0.1f }),
                (ItemType.CobaltNaginata, 3f, 1f, new float[0]),
                (ItemType.PalladiumPike, 3f, 1f, new float[0]),
                (ItemType.MythrilHalberd, 3f, 1f, new float[0]),
                (ItemType.OrichalcumHalberd, 3f, 1f, new float[0]),
                (ItemType.AdamantiteGlaive, 3f, 1f, new float[0]),
                (ItemType.TitaniumTrident, 3f, 1f, new float[] { 0.2f }),
                (ItemType.Gungnir, 4f, 1f, new float[] { 0.14f }),
                (ItemType.MushroomSpear, 3f, 1.4f, new float[] { 0.45f }),
                (ItemType.ChlorophytePartisan, 6.5f, 1.4f, new float[] { 0.5f }),
                (ItemType.NorthPole, 11f, 1.8f, new float[] { 0.45f, 0.9f }),
                (ItemType.LightDisc, 5f, 2.5f, new float[] { 0.4f, 1.2f }),
                (ItemType.Bananarang, 5f, 2.5f, new float[] { 0.5f }),
                (ItemType.PossessedHatchet, 6f, 2.5f, new float[] { 0.35f, 1f }),
                (ItemType.PaladinsHammer, 5f, 2.5f, new float[] { 0.08f, 0.4f, 0.75f }),
            };
            foreach (var run in runs)
            {
                if (target == null || target.isDead) break;
                var def = ItemDb.Get(run.Type);
                inv.Slots[slot] = ItemStack.Of(run.Type, 1);
                inv.Selected = slot;
                target.hp = Mathf.Max(target.hp, 99999);
                Vector2 centre = col.bounds.center;
                Vector2 hold = centre + Vector2.right * run.Dist;
                // The last weapon's shots would leave the enemy immune.
                TerraProjectiles.Instance.DevClear();
                Pin(target, trb, hold);
                yield return null;
                var made0 = new Dictionary<ProjKind, int>();
                var hits0 = new Dictionary<ProjKind, int>();
                foreach (var k in swordKinds)
                {
                    made0[k] = TerraProjectiles.DevMade(k);
                    hits0[k] = TerraProjectiles.DevHit(k);
                }
                var fxHits0 = new Dictionary<int, int>(SwordFx.DevHits);
                int rings0 = TerraProjectiles.DevInfluxRings, meows0 = TerraProjectiles.DevMeowBounces, stars0 = TerraProjectiles.DevStarsDown;
                int life = EnemyStats.Life(target, info), taken = 0, uses = 0, maxAlive = 0;
                float reach = 0f;
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
                    foreach (var k in swordKinds) alive += TerraProjectiles.Instance.DevCount(k);
                    // A spear's tip: how far out from the player's centre it gets (Terraria px).
                    var tip = def.Shoot != ProjKind.None ? TerraProjectiles.Instance.DevNewest(def.Shoot) : null;
                    if (tip.HasValue && def.HoldOut) reach = Mathf.Max(reach, Vector2.Distance(tip.Value, col.bounds.center) / Effects.TerraParticles.Px);
                    maxAlive = Mathf.Max(maxAlive, alive + SwordFx.Instance.DevLive);
                    if (pic < run.Pictures.Length && t >= run.Pictures[pic])
                    {
                        Shot(scene, $"s_{run.Type}_{pic}");
                        pic++;
                    }
                    yield return null;
                }
                mod.DevAim = null;
                // Let the last shots finish (they count, the swinging has stopped).
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
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
                foreach (var k in swordKinds)
                {
                    int n = TerraProjectiles.DevMade(k) - made0[k];
                    if (n > 0) made.Append($"{k}×{n}(命中{TerraProjectiles.DevHit(k) - hits0[k]}) ");
                }
                var fx = new System.Text.StringBuilder();
                foreach (var kv in SwordFx.DevHits)
                {
                    int n = kv.Value - (fxHits0.TryGetValue(kv.Key, out int o) ? o : 0);
                    if (n > 0) fx.Append($"{kv.Key}命中{n} ");
                }
                string extra = "";
                if (run.Type == ItemType.InfluxWaver) extra = $"，绕圈再刺 {TerraProjectiles.DevInfluxRings - rings0} 次";
                if (run.Type == ItemType.Meowmere) extra = $"，反弹 {TerraProjectiles.DevMeowBounces - meows0} 次";
                if (run.Type == ItemType.StarWrath) extra = $"，落到光标高度的星星 {TerraProjectiles.DevStarsDown - stars0} 颗";
                if (def.HoldOut) extra += $"，枪尖最远 {reach:F0}px";
                Plugin.Log.LogInfo($"[AutoTest] 剑 {def.Name}：挥 {uses} 次，敌人共受泰拉伤害 {taken}（武器面板 {def.Damage}），弹幕 [{made.ToString().Trim()}]，剑气 [{fx.ToString().Trim()}]，同时最多 {maxAlive}{extra}");
                yield return new WaitForSeconds(0.3f);
            }
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
