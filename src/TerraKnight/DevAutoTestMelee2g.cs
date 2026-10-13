using System.Collections;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "melee2g", sweep S1.G): the chained heads shot at an enemy held in place and
    /// pulled back (the Golem Fist cracking the ground from afar), the Anchor thrown, and the
    /// Solar Eruption lashed three times with its solar blasts.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Melee2gTest(string scene, HeroController hero)
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
            foreach (var (item, kind, dist, uses, pic) in new[]
            {
                (ItemType.GolemFist, ProjKind.GolemFistProj, 2.5f, 1, "m2g_golemfist"), (ItemType.GolemFist, ProjKind.GolemFistProj, 7f, 1, "m2g_fistcrack"),
                (ItemType.KOCannon, ProjKind.BoxingGlove, 2.5f, 1, "m2g_kocannon"), (ItemType.ChainKnife, ProjKind.ChainKnifeProj, 2.5f, 3, "m2g_chainknife"),
                (ItemType.ChainGuillotines, ProjKind.ChainGuillotine, 4f, 3, "m2g_guillotine"), (ItemType.Anchor, ProjKind.AnchorProj, 3f, 1, "m2g_anchor"),
            })
            {
                Take(item);
                tp.DevClear();
                mod.DevHold = false;
                while (!mod.DevReady) yield return null;
                var at = C() + Vector2.right * side * dist;
                int h0 = TerraProjectiles.DevHit(kind), m0 = TerraProjectiles.DevMade(kind), c0 = TerraProjectiles.DevFistCracks, a0 = TerraProjectiles.DevAnchorSmashes;
                mod.DevAim = at;
                int used = 0, most = 0;
                bool pictured = false;
                float t = 0f;
                for (; t < 3f; t += Time.deltaTime)
                {
                    Hold(at);
                    if (used < uses && mod.DevReady)
                    {
                        mod.DevUseSelected(at);
                        used++;
                    }
                    most = Mathf.Max(most, tp.DevCount(kind));
                    if (!pictured && t > 0.12f && tp.DevCount(kind) > 0)
                    {
                        pictured = true;
                        yield return CloseUpAt(scene, pic, (C() + at) / 2f, 150, 2);
                    }
                    if (used >= uses && t > 0.3f && tp.DevCount(kind) == 0) break;
                    yield return null;
                }
                mod.DevAim = null;
                Plugin.Log.LogInfo($"[AutoTest] 近战2g {ItemDb.Get(item).Name}（敌人 {dist * 32f:F0} 像素外）：用 {used} 次 生成 {TerraProjectiles.DevMade(kind) - m0}，同时最多 {most}，命中 {TerraProjectiles.DevHit(kind) - h0}，" +
                    $"{t:F2} 秒后全部收回={tp.DevCount(kind) == 0}，碎地 {TerraProjectiles.DevFistCracks - c0}，锚砸地 {TerraProjectiles.DevAnchorSmashes - a0}");
            }
            // The Solar Eruption: three lashes at an enemy 5 tiles off.
            Take(ItemType.SolarEruption);
            tp.DevClear();
            while (!mod.DevReady) yield return null;
            var whip = C() + Vector2.right * side * 5f;
            int e0 = TerraProjectiles.DevHit(ProjKind.SolarEruptionProj), b0 = TerraProjectiles.DevHit(ProjKind.SolarEruptionBlast), eb0 = TerraProjectiles.DevEruptionBlasts;
            mod.DevAim = whip;
            mod.DevHold = true;
            int lashes = 0;
            bool lashPic = false, blastPic = false;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                Hold(whip);
                if (lashes < 3 && mod.DevReady && tp.DevCount(ProjKind.SolarEruptionProj) == 0)
                {
                    mod.DevUseSelected(whip);
                    lashes++;
                }
                if (!lashPic && t > 0.15f && tp.DevCount(ProjKind.SolarEruptionProj) > 0)
                {
                    lashPic = true;
                    yield return CloseUpAt(scene, "m2g_eruption", (C() + whip) / 2f, 170, 2);
                }
                if (!blastPic && tp.DevCount(ProjKind.SolarEruptionBlast) > 0)
                {
                    blastPic = true;
                    yield return CloseUpAt(scene, "m2g_eruptionblast", whip, 120, 2);
                }
                yield return null;
            }
            mod.DevHold = false;
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 近战2g 日耀喷发剑：甩 {lashes} 次，命中 {TerraProjectiles.DevHit(ProjKind.SolarEruptionProj) - e0}，日耀爆炸 {TerraProjectiles.DevEruptionBlasts - eb0} 次 命中 {TerraProjectiles.DevHit(ProjKind.SolarEruptionBlast) - b0}" +
                (target != null ? $"，破晓燃烧={EnemyDebuffs.Has(target, EnemyDebuffs.Daybroken)}" : ""));
            mod.DevHold = null;
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
