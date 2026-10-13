using System.Collections;
using System.Text;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "rockets", sweep R4b): each rocket launcher fires each of the eight rockets at an
    /// enemy held 5 tiles off (the snowmen aimed well above it, to home in), counting the shots,
    /// hits, blasts and cluster fragments; a close-up of a Mini Nuke's blast and of the snowman in flight.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RocketsTest(string scene, HeroController hero)
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
            var far = C() + Vector2.right * side * 5f;
            void Hold()
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 30000);
                Pin(target, trb, far);
            }
            var sb = new StringBuilder();
            var ammo = new[] { ItemType.RocketI, ItemType.RocketII, ItemType.RocketIII, ItemType.RocketIV, ItemType.ClusterRocketI, ItemType.ClusterRocketII, ItemType.MiniNukeI, ItemType.MiniNukeII };
            foreach (var launcher in new[] { ItemType.GrenadeLauncher, ItemType.RocketLauncher, ItemType.ProximityMineLauncher, ItemType.SnowmanCannon })
            {
                sb.Append(ItemDb.Get(launcher).Name).Append("：");
                foreach (var a in ammo)
                {
                    inv.Slots[slot] = ItemStack.Of(launcher);
                    inv.Selected = slot;
                    inv.Slots[Inventory.Size - 1] = ItemStack.Of(a, 50);
                    tp.DevClear();
                    while (!mod.DevReady) yield return null;
                    var kind = TerraProjectiles.LauncherShot(launcher, a);
                    int m0 = TerraProjectiles.DevMade(kind), h0 = TerraProjectiles.DevHit(kind), b0 = TerraProjectiles.DevRocketBlasts, f0 = TerraProjectiles.DevClusterFragments;
                    var aim = launcher == ItemType.SnowmanCannon ? far + Vector2.up * 2f : far;
                    mod.DevAim = aim;
                    mod.DevUseSelected(aim);
                    bool pictured = !(a == ItemType.MiniNukeI && launcher == ItemType.RocketLauncher) && !(a == ItemType.RocketI && launcher == ItemType.SnowmanCannon);
                    for (float t = 0f; t < 2.2f; t += Time.deltaTime)
                    {
                        Hold();
                        if (!pictured && launcher == ItemType.SnowmanCannon && t > 0.2f)
                        {
                            pictured = true;
                            yield return CloseUp(scene, "r_snowman", hero, 200, 2);
                        }
                        if (!pictured && launcher == ItemType.RocketLauncher && TerraProjectiles.DevRocketBlasts > b0)
                        {
                            pictured = true;
                            for (int f = 0; f < 6; f++) yield return null;
                            yield return CloseUpAt(scene, "r_nuke", far, 300, 1);
                        }
                        yield return null;
                    }
                    sb.Append($"{ItemDb.Get(a).Name}出{TerraProjectiles.DevMade(kind) - m0}中{TerraProjectiles.DevHit(kind) - h0}爆{TerraProjectiles.DevRocketBlasts - b0}");
                    if (TerraProjectiles.DevClusterFragments > f0) sb.Append($"(碎{TerraProjectiles.DevClusterFragments - f0})");
                    sb.Append(' ');
                }
                Plugin.Log.LogInfo($"[AutoTest] 火箭 {sb}");
                sb.Clear();
            }
            // The Celebration, the Electrosphere Launcher (aimed at the enemy), the Celebration Mk2 held 2.5 s (all seven kinds).
            inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.RocketI, 200);
            foreach (var (gun, kinds, hold, pic) in new[]
            {
                (ItemType.Celebration, new[] { ProjKind.Firework1, ProjKind.Firework2, ProjKind.Firework3, ProjKind.Firework4 }, false, "r_celebration"),
                (ItemType.ElectrosphereLauncher, new[] { ProjKind.ElectrosphereMissile, ProjKind.Electrosphere }, false, "r_electrosphere"),
                (ItemType.CelebrationMk2, new[] { ProjKind.Celeb2Rocket1 }, true, "r_mk2"),
            })
            {
                inv.Slots[slot] = ItemStack.Of(gun);
                inv.Selected = slot;
                tp.DevClear();
                while (!mod.DevReady) yield return null;
                var made0 = new int[kinds.Length];
                var hit0 = new int[kinds.Length];
                for (int k = 0; k < kinds.Length; k++)
                {
                    made0[k] = TerraProjectiles.DevMade(kinds[k]);
                    hit0[k] = TerraProjectiles.DevHit(kinds[k]);
                }
                int fw0 = TerraProjectiles.DevFireworks, es0 = TerraProjectiles.DevElectrospheres, c20 = TerraProjectiles.DevCeleb2Rockets, ammo0 = inv.Slots[Inventory.Size - 1].Count;
                mod.DevAim = far;
                mod.DevHold = hold;
                int shots = 0;
                bool pictured = false;
                for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                {
                    Hold();
                    if (!hold && shots < 2 && mod.DevReady && !mod.IsUsingItem)
                    {
                        mod.DevUseSelected(far);
                        shots++;
                    }
                    if (hold && shots == 0)
                    {
                        mod.DevUseSelected(far);
                        shots++;
                    }
                    if (!pictured && t > (gun == ItemType.Celebration ? 0.75f : gun == ItemType.ElectrosphereLauncher ? 0.9f : 1.6f))
                    {
                        pictured = true;
                        if (gun == ItemType.CelebrationMk2) yield return CloseUp(scene, pic, hero, 220, 1);
                        else yield return CloseUpAt(scene, pic, far, 220, 1);
                    }
                    yield return null;
                }
                mod.DevHold = false;
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    Hold();
                    yield return null;
                }
                sb.Append(ItemDb.Get(gun).Name);
                for (int k = 0; k < kinds.Length; k++) sb.Append($" {kinds[k]}出{TerraProjectiles.DevMade(kinds[k]) - made0[k]}中{TerraProjectiles.DevHit(kinds[k]) - hit0[k]}");
                sb.Append($"(烟花{TerraProjectiles.DevFireworks - fw0} 电圈{TerraProjectiles.DevElectrospheres - es0} Mk2火箭{TerraProjectiles.DevCeleb2Rockets - c20} 耗弹{ammo0 - inv.Slots[Inventory.Size - 1].Count})；");
            }
            mod.DevHold = null;
            Plugin.Log.LogInfo($"[AutoTest] 火箭 派对与电圈：{sb}");
            sb.Clear();
            mod.DevAim = null;
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.DevNoFallDamage = false;
        }
    }
}
