using System.Collections;
using System.Text;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "thrown3", sweep R3): every thrown consumable thrown three times at an enemy held
    /// in place (the Bone Javelins stuck in it, the Beenade's bees, the Molotov's flames, the Magic
    /// Dagger's mana), and the three new grenades onto the floor (the Sticky one held there, the
    /// Bouncy one up again, the Happy one's confetti), each used up as it goes.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Thrown3Test(string scene, HeroController hero)
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
            // Used up for real here: the dev build's endless items off for the suite.
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            float Room(float d) { var hit = Physics2D.Raycast(C(), Vector2.right * d, 30f, terrain); return hit.collider != null ? hit.distance : 30f; }
            float side = Room(1f) >= Room(-1f) ? 1f : -1f;
            const int slot = Inventory.HotbarSize - 1;
            void Take(ItemType t, int n)
            {
                inv.Slots[slot] = ItemStack.Of(t, n);
                inv.Selected = slot;
            }
            int Left() => inv.Slots[slot].IsEmpty ? 0 : inv.Slots[slot].Count;
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
            var stats = mod.Stats;
            void Hold(Vector2 at)
            {
                if (target == null) return;
                target.hp = Mathf.Max(target.hp, 3000);
                Pin(target, trb, at);
            }
            var far = C() + Vector2.right * side * 3f;
            var sb = new StringBuilder();
            // The throws: three each at the enemy 3 tiles off, then 2 s for them to land.
            foreach (var (item, kind, pic) in new[]
            {
                (ItemType.PoisonedKnife, ProjKind.PoisonedKnifeProj, (string)null), (ItemType.HolyWater, ProjKind.HolyWaterProj, "t3_holy"),
                (ItemType.UnholyWater, ProjKind.UnholyWaterProj, null), (ItemType.BloodWater, ProjKind.BloodWaterProj, null), (ItemType.RottenEgg, ProjKind.RottenEggProj, null),
                (ItemType.StarAnise, ProjKind.StarAniseProj, null), (ItemType.BoneDagger, ProjKind.BoneDaggerProj, null), (ItemType.FrostDaggerfish, ProjKind.FrostDaggerfishProj, "t3_daggerfish"),
                (ItemType.MagicDagger, ProjKind.MagicDaggerProj, null), (ItemType.Javelin, ProjKind.JavelinProj, null), (ItemType.BoneJavelin, ProjKind.BoneJavelinProj, "t3_bonejavelin"),
                (ItemType.SpikyBall, ProjKind.SpikyBallProj, "t3_spiky"), (ItemType.Beenade, ProjKind.BeenadeProj, null), (ItemType.MudBall, ProjKind.MudBallProj, null),
                (ItemType.MolotovCocktail, ProjKind.MolotovProj, "t3_molotov"),
            })
            {
                Take(item, 20);
                tp.DevClear();
                stats.Mana = stats.ManaMax;
                while (!mod.DevReady) yield return null;
                int h0 = TerraProjectiles.DevHit(kind), m0 = TerraProjectiles.DevMade(kind), bees0 = TerraProjectiles.DevBeenadeBees, fires0 = TerraProjectiles.DevMolotovs;
                int mana0 = stats.Mana, stuck = 0;
                mod.DevAim = far;
                int thrown = 0;
                bool pictured = pic == null;
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    Hold(far);
                    if (thrown < 3 && mod.DevReady && !mod.IsUsingItem)
                    {
                        mod.DevUseSelected(far);
                        thrown++;
                    }
                    if (target != null && item == ItemType.BoneJavelin) stuck = Mathf.Max(stuck, tp.StuckBoneJavelins(target));
                    if (!pictured && t > (item == ItemType.MolotovCocktail ? 1.2f : item == ItemType.BoneJavelin ? 1.6f : 0.25f))
                    {
                        pictured = true;
                        yield return CloseUp(scene, pic, hero, 110, 3);
                    }
                    yield return null;
                }
                string extra = item == ItemType.BoneJavelin ? $"(插着{stuck})" : item == ItemType.Beenade ? $"(蜜蜂{TerraProjectiles.DevBeenadeBees - bees0})"
                    : item == ItemType.MolotovCocktail ? $"(火{TerraProjectiles.DevMolotovs - fires0})" : item == ItemType.MagicDagger ? $"(魔力{mana0}->{stats.Mana})" : "";
                sb.Append($"{ItemDb.Get(item).Name} 出{TerraProjectiles.DevMade(kind) - m0}中{TerraProjectiles.DevHit(kind) - h0}剩{Left()}{extra} ");
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 投掷 命中：{sb}");
            // The grenades, lobbed onto the floor 2 tiles off with nobody near.
            if (target != null) Pin(target, trb, C() + Vector2.right * -side * 12f);
            sb.Clear();
            var floor = C() + new Vector2(side * 2f, -1.2f);
            foreach (var (item, kind) in new[]
            {
                (ItemType.StickyGrenade, ProjKind.StickyGrenadeProj), (ItemType.BouncyGrenade, ProjKind.BouncyGrenadeProj), (ItemType.PartyGirlGrenade, ProjKind.PartyGrenadeProj),
            })
            {
                Take(item, 5);
                tp.DevClear();
                while (!mod.DevReady) yield return null;
                mod.DevAim = floor;
                mod.DevUseSelected(floor);
                float still = 0f, stillMost = 0f, low = float.MaxValue, rise = 0f, life = 0f;
                Vector2? last = null;
                bool pictured = item != ItemType.PartyGirlGrenade;
                for (float t = 0f; t < 4f; t += Time.deltaTime)
                {
                    if (target != null) Pin(target, trb, C() + Vector2.right * -side * 12f);
                    var at = tp.DevNewest(kind);
                    if (at.HasValue)
                    {
                        life = t;
                        if (last.HasValue && Vector2.Distance(at.Value, last.Value) < 0.001f) still += Time.deltaTime; else still = 0f;
                        stillMost = Mathf.Max(stillMost, still);
                        low = Mathf.Min(low, at.Value.y);
                        rise = Mathf.Max(rise, at.Value.y - low);
                        last = at;
                    }
                    else if (!pictured && last.HasValue)
                    {
                        pictured = true;
                        Plugin.Log.LogInfo($"[AutoTest] 投掷 快乐手榴弹炸在 {last.Value}，玩家 {C()}");
                        yield return CloseUp(scene, "t3_party", hero, 150, 2);
                    }
                    yield return null;
                }
                sb.Append($"{ItemDb.Get(item).Name} 存活{life:0.0}s 静止{stillMost:0.0}s 回弹{rise:0.00} 剩{Left()} ");
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 投掷 手榴弹：{sb}");
            System.Array.Copy(savedSlots, inv.Slots, savedSlots.Length);
            inv.Selected = savedSelected;
            mod.SetInfiniteItems(wasInfinite);
            mod.DevNoFallDamage = false;
        }
    }
}
