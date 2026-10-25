using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bejeweled"): the Bejeweled Staff (1.4.5's unobtainable 6171, user 10-25): crafted from the seven gem staves
    /// and ten of each of the six souls at a Mythril Anvil; a use shoots three bolts of one random gem with every gem's
    /// features (homing, bouncing, the Topaz's burst, the Emerald's slowing, the Diamond's size), through three, 4 armour
    /// penetration, a second and a half; they find the enemy nearby.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BejeweledTest(string scene, HeroController hero)
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 宝石镶嵌法杖 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var def = ItemDb.Get(ItemType.BejeweledStaff);
            Check("物品", def != null && def.Name == "宝石镶嵌法杖" && def.Texture == "Item_6171" && def.Mana == 15 && def.Shoot == ProjKind.DiamondBolt,
                $"{def?.Name} {def?.Texture} 伤害 {def?.Damage} 耗魔 {def?.Mana} 阶段 {def?.Stage} 售价 {Shop.SellValue(ItemStack.Of(ItemType.BejeweledStaff))}");
            var recipe = Recipes.All.FirstOrDefault(r => r.Result == ItemType.BejeweledStaff);
            var want = new[] { ItemType.AmethystStaff, ItemType.TopazStaff, ItemType.SapphireStaff, ItemType.EmeraldStaff, ItemType.RubyStaff, ItemType.AmberStaff, ItemType.DiamondStaff };
            var souls = new[] { ItemType.SoulofLight, ItemType.SoulofNight, ItemType.SoulofFlight, ItemType.SoulofMight, ItemType.SoulofSight, ItemType.SoulofFright };
            bool recipeOk = recipe != null && recipe.Station == "秘银砧" && recipe.Ingredients.Length == 13
                && want.All(w => recipe.Ingredients.Any(i => i.Item1 == w && i.Item2 == 1)) && souls.All(w => recipe.Ingredients.Any(i => i.Item1 == w && i.Item2 == 10));
            Check("配方：七把宝石法杖 + 六种魂各 10", recipeOk, recipe == null ? "没有配方" : $"{recipe.Station}，{string.Join("、", recipe.Ingredients.Select(i => $"{ItemDb.Get(i.Item1).Name}×{i.Item2}"))}");

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
            Vector2 at = Clear(hero, (Vector2)hero.transform.position + new Vector2(-4f, 2.5f));
            var inv = mod.Inventory;
            int slot = Inventory.HotbarSize - 1;
            inv.Slots[slot] = ItemStack.Of(ItemType.BejeweledStaff);
            inv.Selected = slot;
            TerraProjectiles.Instance.DevClear();
            mod.Stats.Mana = mod.Stats.ManaMax;
            while (!mod.DevReady) yield return null;
            int life0 = target != null ? target.hp : 0;
            Vector2 aim = target != null ? (Vector2)target.transform.position : at;
            if (target != null) { target.hp = Mathf.Max(target.hp, 2000); life0 = target.hp; }
            mod.DevAim = aim;
            mod.DevUseSelected(aim);
            mod.DevAim = null;
            yield return null;
            yield return null;
            var bolts = TerraProjectiles.Instance.DevGemBolts();
            var gemKinds = bolts.Select(b => b.Kind).Distinct().ToList();
            var all5 = TerraProjectiles.GemFx.AllGems | TerraProjectiles.GemFx.Homing | TerraProjectiles.GemFx.Bounce | TerraProjectiles.GemFx.Aoe | TerraProjectiles.GemFx.FastSlow | TerraProjectiles.GemFx.Bigger;
            Check("一次三发，同一种随机宝石，兼具所有宝石的特性", bolts.Count == 3 && gemKinds.Count == 1 && bolts.All(b => (b.Gem & all5) == all5),
                $"{bolts.Count} 发：{string.Join("；", bolts.Select(b => $"{b.Kind} [{b.Gem}]"))}");
            Check("穿透 3、盔甲穿透 4、1.5 秒", bolts.Count > 0 && bolts.All(b => b.Penetrate == 3 && b.ArmorPen == 4 && b.TimeLeft <= 90 && b.TimeLeft > 60),
                string.Join("；", bolts.Select(b => $"穿透 {b.Penetrate} 盔甲穿透 {b.ArmorPen} 剩 {b.TimeLeft} 帧")));
            Shot(scene, "bejeweled_shot");
            // A few more uses: the gem changes; the bolts find the enemy and are gone within a second and a half.
            var seen = new System.Collections.Generic.HashSet<ProjKind>(gemKinds);
            for (int k = 0; k < 6; k++)
            {
                while (!mod.DevReady) yield return null;
                mod.Stats.Mana = mod.Stats.ManaMax;
                if (target != null && !target.isDead) aim = target.transform.position;
                mod.DevAim = aim;
                mod.DevUseSelected(aim);
                mod.DevAim = null;
                yield return null;
                yield return null;
                foreach (var b in TerraProjectiles.Instance.DevGemBolts()) seen.Add(b.Kind);
            }
            Check("每次随机换宝石外观", seen.Count >= 2, $"见到 {seen.Count} 种：{string.Join("、", seen)}");
            yield return new WaitForSeconds(2f);
            Check("1.5 秒后全部消失", TerraProjectiles.Instance.DevGemBolts().Count == 0, $"还剩 {TerraProjectiles.Instance.DevGemBolts().Count} 发");
            // On a husk up and about (the start room's Vengefly sleeps out of reach): shot at from a little off, the bolts find it.
            Player.TerraStats.DevImmortal = true;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            hero = HeroController.instance;
            var husk = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h => !h.isDead && h.gameObject.activeInHierarchy && h.name.StartsWith("Zombie"))
                .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).FirstOrDefault();
            if (husk != null)
            {
                var hc = husk.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled && !c.isTrigger);
                // On its own floor a few units off, wherever there is room (a spot in the rock is cleared upwards, off its floor).
                float floorY = hc != null ? hc.bounds.min.y + 1.2f : husk.transform.position.y;
                Vector2 stand = Clear(hero, new Vector2(husk.transform.position.x - 4f, floorY));
                foreach (float dx in new[] { -4f, 4f, -3f, 3f, -5f, 5f, -2.5f, 2.5f })
                {
                    var meant = new Vector2(husk.transform.position.x + dx, floorY);
                    var spot = Clear(hero, meant);
                    if (Vector2.Distance(spot, meant) < 0.6f) { stand = spot; break; }
                }
                hero.transform.position = new Vector3(stand.x, stand.y, hero.transform.position.z);
                yield return new WaitForSeconds(0.4f);
                husk.hp = Mathf.Max(husk.hp, 3000);
                int huskLife = husk.hp;
                inv.Slots[slot] = ItemStack.Of(ItemType.BejeweledStaff);
                inv.Selected = slot;
                for (int k = 0; k < 4 && husk != null && !husk.isDead; k++)
                {
                    while (!mod.DevReady) yield return null;
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    // Aimed a little above it: the homing has to turn them onto it.
                    aim = (Vector2)husk.transform.position + Vector2.up * 2f;
                    mod.DevAim = aim;
                    mod.DevUseSelected(aim);
                    mod.DevAim = null;
                    yield return null;
                }
                yield return new WaitForSeconds(1.6f);
                Check("追踪打中躯壳", husk == null || husk.isDead || husk.hp < huskLife, $"{husk?.name} 生命 {huskLife}→{(husk == null || husk.isDead ? 0 : husk.hp)}");
                Shot("Crossroads_01", "bejeweled_husk");
            }
            else Check("追踪打中躯壳", false, "房间里没有躯壳");
            inv.Slots[slot] = default;
            PlayerData.instance.isInvincible = false;
            Plugin.Log.LogInfo($"[AutoTest] 宝石镶嵌法杖 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
