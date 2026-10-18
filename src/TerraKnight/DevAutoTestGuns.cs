using System.Collections;
using System.Text;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "guns", sweep R4a): every new gun and bow shot twice at an enemy held 4 tiles
    /// off, counting what each makes and lands (the shotguns' pellets, the Onyx blast, the
    /// Xenopopper's bubbles and their rounds, Aerial Bane's shrapnel, the bee arrows' bees), the
    /// Fallen Stars used up by the Star Cannon, the Harpoon out and back, and the Revolver's crit
    /// bonus growing when its use is let go of.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GunsTest(string scene, HeroController hero)
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
            int savedSelected = inv.Selected;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            Vector2 C() => col.bounds.center;
            Wear();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            const int slot = Inventory.HotbarSize - 1;
            void Take(ItemType t)
            {
                inv.Slots[slot] = ItemStack.Of(t);
                inv.Selected = slot;
            }
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                float dd = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (dd < best) { best = dd; target = hm; }
            }
            var trb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            var tp = TerraProjectiles.Instance;
            void Hold(Vector2 at)
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 3000);
                Pin(target, trb, at);
            }
            // Ammo in the last three slots.
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 999);
            inv.Slots[Inventory.Size - 2] = ItemStack.Of(ItemType.WoodenArrow, 999);
            inv.Slots[Inventory.Size - 3] = ItemStack.Of(ItemType.FallenStar, 50);
            var far = C() + Vector2.right * side * 4f;
            var sb = new StringBuilder();
            foreach (var (item, kinds, pic) in new[]
            {
                (ItemType.Shotgun, new[] { ProjKind.Bullet }, (string)null), (ItemType.QuadBarrelShotgun, new[] { ProjKind.Bullet }, null),
                (ItemType.OnyxBlaster, new[] { ProjKind.Bullet, ProjKind.OnyxBlast }, "g_onyx"), (ItemType.RedRyder, new[] { ProjKind.Bullet }, null),
                (ItemType.Revolver, new[] { ProjKind.Bullet }, null), (ItemType.Xenopopper, new[] { ProjKind.XenoBubble, ProjKind.Bullet }, "g_xeno"),
                (ItemType.AerialBane, new[] { ProjKind.BetsyArrow }, "g_bane"), (ItemType.BeesKnees, new[] { ProjKind.BeeArrow, ProjKind.Bee }, null),
                (ItemType.HellwingBow, new[] { ProjKind.HellwingBat }, "g_hellwing"), (ItemType.Toxikarp, new[] { ProjKind.ToxicBubble }, "g_toxikarp"),
                (ItemType.PewMaticHorn, new[] { ProjKind.PewMaticNote }, "g_pewmatic"), (ItemType.StarCannon, new[] { ProjKind.StarCannonStar }, "g_star"),
                (ItemType.SuperStarCannon, new[] { ProjKind.SuperStar }, "g_superstar"), (ItemType.Harpoon, new[] { ProjKind.HarpoonProj }, "g_harpoon"),
            })
            {
                Take(item);
                tp.DevClear();
                mod.DevHold = item == ItemType.Harpoon;
                while (!mod.DevReady) yield return null;
                var made0 = new int[kinds.Length];
                var hit0 = new int[kinds.Length];
                for (int k = 0; k < kinds.Length; k++)
                {
                    made0[k] = TerraProjectiles.DevMade(kinds[k]);
                    hit0[k] = TerraProjectiles.DevHit(kinds[k]);
                }
                int xeno0 = TerraProjectiles.DevXenoBullets, shrap0 = TerraProjectiles.DevBetsyShrapnel, bees0 = TerraProjectiles.DevBeeArrowBees, onyx0 = TerraProjectiles.DevOnyxBlasts;
                int stars0 = inv.Slots[Inventory.Size - 3].Count, most = 0;
                mod.DevAim = far;
                int shots = 0;
                bool pictured = pic == null;
                for (float t = 0f; t < 2.6f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (shots < 2 && mod.DevReady && !mod.IsUsingItem)
                    {
                        mod.DevUseSelected(far);
                        shots++;
                    }
                    // The Harpoon: held out a second, then let go to come back.
                    if (item == ItemType.Harpoon && t > 1f) mod.DevHold = false;
                    most = Mathf.Max(most, tp.DevCount(kinds[0]));
                    if (!pictured && t > (item == ItemType.Harpoon ? 0.3f : item == ItemType.Xenopopper ? 0.15f : 0.12f))
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 130, 2);
                    }
                    yield return null;
                }
                sb.Append(ItemDb.Get(item).Name);
                for (int k = 0; k < kinds.Length; k++) sb.Append($" {kinds[k]}出{TerraProjectiles.DevMade(kinds[k]) - made0[k]}中{TerraProjectiles.DevHit(kinds[k]) - hit0[k]}");
                if (item == ItemType.Xenopopper) sb.Append($"(子弹{TerraProjectiles.DevXenoBullets - xeno0})");
                if (item == ItemType.AerialBane) sb.Append($"(弹片{TerraProjectiles.DevBetsyShrapnel - shrap0})");
                if (item == ItemType.BeesKnees) sb.Append($"(蜜蜂{TerraProjectiles.DevBeeArrowBees - bees0})");
                if (item == ItemType.OnyxBlaster) sb.Append($"(爆{TerraProjectiles.DevOnyxBlasts - onyx0})");
                if (item == ItemType.StarCannon || item == ItemType.SuperStarCannon) sb.Append($"(星{stars0}->{inv.Slots[Inventory.Size - 3].Count})");
                if (item == ItemType.Harpoon) sb.Append($"(最多{most}，剩{tp.DevCount(ProjKind.HarpoonProj)})");
                sb.Append("；");
            }
            mod.DevAim = null;
            mod.DevHold = null;
            Plugin.Log.LogInfo($"[AutoTest] 枪弓 R4a：{sb}");
            // The held bows and the Vortex Beater: two seconds held at the enemy, then let go.
            sb.Clear();
            foreach (var (item, held, kinds, pic) in new[]
            {
                (ItemType.VortexBeater, ProjKind.VortexBeaterHeld, new[] { ProjKind.Bullet, ProjKind.VortexRocket }, "g_vortex"),
                (ItemType.Phantasm, ProjKind.PhantasmHeld, new[] { ProjKind.WoodenArrow, ProjKind.PhantasmArrow }, "g_phantasm"),
                (ItemType.PhantomPhoenix, ProjKind.PhoenixBowHeld, new[] { ProjKind.FlamingArrow, ProjKind.Phoenix }, "g_phoenix"),
            })
            {
                Take(item);
                tp.DevClear();
                while (!mod.DevReady) yield return null;
                var made0 = new int[kinds.Length];
                var hit0 = new int[kinds.Length];
                for (int k = 0; k < kinds.Length; k++)
                {
                    made0[k] = TerraProjectiles.DevMade(kinds[k]);
                    hit0[k] = TerraProjectiles.DevHit(kinds[k]);
                }
                int ammo0 = inv.Slots[Inventory.Size - 1].Count + inv.Slots[Inventory.Size - 2].Count;
                mod.DevAim = far;
                mod.DevHold = true;
                mod.DevUseSelected(far);
                bool pictured = false, heldSeen = false;
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    Hold(far);
                    heldSeen |= tp.DevCount(held) > 0;
                    if (!pictured && t > 1.2f)
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 130, 2);
                    }
                    yield return null;
                }
                mod.DevHold = false;
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    Hold(far);
                    yield return null;
                }
                int ammo1 = inv.Slots[Inventory.Size - 1].Count + inv.Slots[Inventory.Size - 2].Count;
                sb.Append($"{ItemDb.Get(item).Name} 手持{heldSeen}松后{tp.DevCount(held)}");
                for (int k = 0; k < kinds.Length; k++) sb.Append($" {kinds[k]}出{TerraProjectiles.DevMade(kinds[k]) - made0[k]}中{TerraProjectiles.DevHit(kinds[k]) - hit0[k]}");
                sb.Append($" 弹药耗{ammo0 - ammo1}；");
            }
            mod.DevAim = null;
            mod.DevHold = null;
            Plugin.Log.LogInfo($"[AutoTest] 枪弓 手持：{sb}");
            // Eventide: two uses; the Piranha Gun: held 2 s at the enemy, then let go.
            Take(ItemType.Eventide);
            tp.DevClear();
            while (!mod.DevReady) yield return null;
            int ev0 = TerraProjectiles.DevMade(ProjKind.EventideArrow), evh0 = TerraProjectiles.DevHit(ProjKind.EventideArrow), wood0 = TerraProjectiles.DevMade(ProjKind.WoodenArrow);
            int arrows0 = inv.Slots[Inventory.Size - 2].Count;
            mod.DevAim = far;
            int uses = 0;
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                Hold(far);
                if (uses < 2 && mod.DevReady && !mod.IsUsingItem)
                {
                    mod.DevUseSelected(far);
                    uses++;
                }
                if (uses == 1 && t > 0.12f && t < 0.12f + Time.deltaTime * 1.5f) yield return CloseUp(scene, "g_eventide", hero, 130, 2);
                yield return null;
            }
            string evLine = $"日暮 彩虹箭出{TerraProjectiles.DevMade(ProjKind.EventideArrow) - ev0}中{TerraProjectiles.DevHit(ProjKind.EventideArrow) - evh0} 木箭出{TerraProjectiles.DevMade(ProjKind.WoodenArrow) - wood0} 箭耗{arrows0 - inv.Slots[Inventory.Size - 2].Count}";
            Take(ItemType.PiranhaGun);
            tp.DevClear();
            while (!mod.DevReady) yield return null;
            int pm0 = TerraProjectiles.DevMade(ProjKind.PiranhaProj), ph0 = TerraProjectiles.DevHit(ProjKind.PiranhaProj), pmost = 0;
            mod.DevHold = true;
            mod.DevUseSelected(far);
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                Hold(far);
                pmost = Mathf.Max(pmost, tp.DevCount(ProjKind.PiranhaProj));
                if (t > 1f && t < 1f + Time.deltaTime * 1.5f) yield return CloseUp(scene, "g_piranha", hero, 150, 2);
                yield return null;
            }
            mod.DevHold = false;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                Hold(far);
                yield return null;
            }
            mod.DevHold = null;
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 枪弓 日暮食人鱼：{evLine}；食人鱼枪 出{TerraProjectiles.DevMade(ProjKind.PiranhaProj) - pm0}中{TerraProjectiles.DevHit(ProjKind.PiranhaProj) - ph0} 最多{pmost} 松后{tp.DevCount(ProjKind.PiranhaProj)}");
            // The Revolver: held through a use, then let go mid-use.
            Take(ItemType.Revolver);
            mod.DevAim = far;
            int[] bonus = new int[2];
            for (int pass = 0; pass < 2; pass++)
            {
                TerraMod.RevolverCrit = 0;
                for (int n = 0; n < 4; n++)
                {
                    while (!mod.DevReady) yield return null;
                    mod.DevHold = true;
                    mod.DevUseSelected(far);
                    for (int f = 0; f < 3; f++) yield return null;
                    mod.DevHold = pass == 0;
                    while (mod.IsUsingItem) yield return null;
                }
                bonus[pass] = TerraMod.RevolverCrit;
            }
            mod.DevHold = null;
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 枪弓 左轮：按住暴击加成{bonus[0]}，松手暴击加成{bonus[1]}");
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.SetInfiniteItems(wasInfinite);
            mod.DevNoFallDamage = false;
        }
    }
}
