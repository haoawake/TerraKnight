using System.Collections;
using System.Text;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ammo2", sweep R4b3+): the new ammo families — each dart from the Dart Rifle and
    /// each dart gun with seeds, the Sandgun with each sand — two shots at an enemy held 4 tiles
    /// off, counting what's made and lands (cursed flames, ichor splits, crystal bounces, sand
    /// set down) and the ammo used.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Ammo2Test(string scene, HeroController hero)
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
            var far = C() + Vector2.right * side * 4f;
            void Hold()
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 30000);
                Pin(target, trb, far);
            }
            var sb = new StringBuilder();
            IEnumerator Fire(ItemType gun, ItemType ammo, ProjKind kind, string pic)
            {
                inv.Slots[slot] = ItemStack.Of(gun);
                inv.Selected = slot;
                inv.Slots[Inventory.Size - 1] = ItemStack.Of(ammo, 50);
                tp.DevClear();
                while (!mod.DevReady) yield return null;
                int m0 = TerraProjectiles.DevMade(kind), h0 = TerraProjectiles.DevHit(kind);
                int cf0 = TerraProjectiles.DevCursedFlames, is0 = TerraProjectiles.DevIchorSplits, cb0 = TerraProjectiles.DevCrystalBounces, sp0 = TerraProjectiles.DevSandPlaced;
                mod.DevAim = far;
                int shots = 0;
                bool pictured = pic == null;
                for (float t = 0f; t < 2f; t += Time.deltaTime)
                {
                    Hold();
                    if (shots < 2 && mod.DevReady && !mod.IsUsingItem)
                    {
                        mod.DevUseSelected(far);
                        shots++;
                    }
                    if (!pictured && t > (gun == ItemType.FlareGun ? 1.5f : gun == ItemType.NailGun ? 0.8f : 0.15f))
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 160, 2);
                    }
                    yield return null;
                }
                sb.Append($"{ItemDb.Get(gun).Name}+{ItemDb.Get(ammo).Name} 出{TerraProjectiles.DevMade(kind) - m0}中{TerraProjectiles.DevHit(kind) - h0} 耗{50 - inv.Slots[Inventory.Size - 1].Count}");
                if (TerraProjectiles.DevCursedFlames > cf0) sb.Append($"(诅咒焰{TerraProjectiles.DevCursedFlames - cf0})");
                if (TerraProjectiles.DevIchorSplits > is0) sb.Append($"(分裂{TerraProjectiles.DevIchorSplits - is0})");
                if (TerraProjectiles.DevCrystalBounces > cb0) sb.Append($"(弹跳{TerraProjectiles.DevCrystalBounces - cb0})");
                if (TerraProjectiles.DevSandPlaced > sp0) sb.Append($"(落沙{TerraProjectiles.DevSandPlaced - sp0})");
                if (kind == ProjKind.StyngerBoltProj) sb.Append($"(弹片{TerraProjectiles.DevStyngerShrapnel})");
                if (kind == ProjKind.NailProj) sb.Append($"(钉住{TerraProjectiles.DevNailsStuck})");
                sb.Append("；");
            }
            foreach (var (gun, ammo, kind, pic) in new[]
            {
                (ItemType.Blowpipe, ItemType.Seed, ProjKind.SeedProj, (string)null), (ItemType.Blowgun, ItemType.Seed, ProjKind.SeedProj, null),
                (ItemType.DartPistol, ItemType.Seed, ProjKind.SeedProj, null), (ItemType.DartRifle, ItemType.Seed, ProjKind.SeedProj, "a_dartrifle"),
                (ItemType.DartRifle, ItemType.PoisonDart, ProjKind.PoisonDartProj, null), (ItemType.DartRifle, ItemType.CrystalDart, ProjKind.CrystalDartProj, null),
                (ItemType.DartRifle, ItemType.CursedDart, ProjKind.CursedDartProj, null), (ItemType.DartRifle, ItemType.IchorDart, ProjKind.IchorDartProj, "a_ichor"),
                (ItemType.Sandgun, ItemType.SandBlock, ProjKind.SandBall, "a_sandgun"), (ItemType.Sandgun, ItemType.EbonsandBlock, ProjKind.EbonsandBall, null),
                (ItemType.Sandgun, ItemType.PearlsandBlock, ProjKind.PearlsandBall, null), (ItemType.Sandgun, ItemType.CrimsandBlock, ProjKind.CrimsandBall, null),
                (ItemType.Flamethrower, ItemType.Gel, ProjKind.FlamethrowerFlame, "a_flamethrower"), (ItemType.ElfMelter, ItemType.Gel, ProjKind.FlamethrowerFlame, "a_elfmelter"),
                (ItemType.FlareGun, ItemType.Flare, ProjKind.FlareProj, null), (ItemType.FlareGun, ItemType.BlueFlare, ProjKind.BlueFlareProj, null),
                (ItemType.FlareGun, ItemType.SpelunkerFlare, ProjKind.SpelunkerFlareProj, null), (ItemType.FlareGun, ItemType.CursedFlare, ProjKind.CursedFlareProj, "a_flares"),
                (ItemType.SnowballCannon, ItemType.Snowball, ProjKind.SnowballProj, null), (ItemType.Stynger, ItemType.StyngerBolt, ProjKind.StyngerBoltProj, null),
                (ItemType.CandyCornRifle, ItemType.CandyCorn, ProjKind.CandyCornProj, null), (ItemType.JackOLanternLauncher, ItemType.ExplosiveJackOLantern, ProjKind.JackOLanternProj, "a_jack"),
                (ItemType.StakeLauncher, ItemType.Stake, ProjKind.StakeProj, null), (ItemType.NailGun, ItemType.Nail, ProjKind.NailProj, "a_nail"),
                (ItemType.AleTosser, ItemType.Ale, ProjKind.AleMug, null), (ItemType.AcornSlingshot, ItemType.Acorn, ProjKind.AcornProj, null),
            })
                yield return Fire(gun, ammo, kind, pic);
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 弹药 {sb}");
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.SetInfiniteItems(wasInfinite);
            mod.DevNoFallDamage = false;
        }
    }
}
