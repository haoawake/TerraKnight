using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "vanity", the user's 10-25 Terraria vanity): every vanity item's pictures there (its icon, the sheets it
    /// is drawn from), each one obtainable (a costume's row, a set it comes with, a developer set) and each row in its
    /// enemy's drops at stage 5; then every costume worn in the social slots in Dirtmouth, the player's layers checked for
    /// a missing picture, a screenshot of each (TK_VANITY_SHOTS=0: none). Lines "[AutoTest] 时装 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator VanityTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                if (!pass || detail.Length < 200) Plugin.Log.LogInfo($"[AutoTest] 时装 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var vanity = ItemDb.All.Where(d => d.Vanity).ToList();
            // Pictures.
            var missing = new List<string>();
            bool Has(string tex) => TerrariaAssets.Texture(tex) != null;
            foreach (var d in vanity)
            {
                if (!Has(d.Texture)) missing.Add(d.Type + ":" + d.Texture);
                if (d.IsArmor && d.ArmorSheet > 0)
                {
                    string sheet = d.Armor == ArmorSlot.Head ? "Armor_Head_" + d.ArmorSheet : d.Armor == ArmorSlot.Body ? "Armor/Armor_" + d.ArmorSheet : "Armor_Legs_" + d.ArmorSheet;
                    if (!Has(sheet)) missing.Add(d.Type + ":" + sheet);
                }
                foreach (var (n, prefix) in new[] { (d.Shoe, "Acc_Shoes_"), (d.Waist, "Acc_Waist_"), (d.Balloon, "Acc_Balloon_"), (d.Shield, "Acc_Shield_"),
                    (d.Neck, "Acc_Neck_"), (d.Back, "Acc_Back_"), (d.HandOn, "Accessories/Acc_HandsOn_"), (d.HandOff, "Accessories/Acc_HandsOff_"),
                    (d.Face, "Acc_Face_"), (d.Front, "Acc_Front_"), (d.Beard, "Acc_Beard_") })
                    if (n > 0 && !Has(prefix + n)) missing.Add(d.Type + ":" + prefix + n);
            }
            Check($"{vanity.Count} 件时装的图标和穿戴贴图都在", missing.Count == 0, string.Join(" ", missing.Take(30)));
            // Sources.
            var rows = VanityDrops.All.ToList();
            var got = new HashSet<ItemType>();
            foreach (var r in rows) { got.Add(r.Lead); foreach (var m in VanityDrops.Mates(r.Lead)) got.Add(m); }
            foreach (var rep in VanityDrops.DeveloperReps) { got.Add(rep); foreach (var m in VanityDrops.Mates(rep)) got.Add(m); }
            var orphans = vanity.Where(d => d.Region == 0 && !got.Contains(d.Type) && d.Type != ItemType.Robe && d.Type != ItemType.WhoopieCushion && d.Type != ItemType.FlameWakerBoots).Select(d => d.Type.ToString()).ToList();
            Check("每件时装都有来源（怪物掉落、随套掉落或开发者套装）", orphans.Count == 0, string.Join(" ", orphans.Take(30)));
            // The areas' costumes: sewn at a Loom from their area's block, never dropped, each sheet in the area's colours.
            var regional = vanity.Where(d => d.Region > 0).ToList();
            var unsewn = regional.Where(d => !Recipes.All.Any(r => r.Result == d.Type && r.Station == "织布机" && r.Ingredients.Length == 1
                && r.Ingredients[0].Type == World.AreaBlocks.BlockItem(d.Region - 1))).Select(d => d.Type.ToString()).ToList();
            Check($"{regional.Count} 件地区时装都在织布机用本地区方块制作", regional.Count == 72 && unsewn.Count == 0, string.Join(" ", unsewn.Take(20)));
            var dropped = regional.Where(d => rows.Any(r => r.Lead == d.Type) || ProgressionLoot.Droppable(d.Type)).Select(d => d.Type.ToString()).ToList();
            Check("地区时装不掉落", dropped.Count == 0, string.Join(" ", dropped.Take(20)));
            var plain = regional.Where(d =>
            {
                string sheet = d.Armor == ArmorSlot.Head ? "Armor_Head_" + d.ArmorSheet : d.Armor == ArmorSlot.Body ? "Armor/Armor_" + d.ArmorSheet : "Armor_Legs_" + d.ArmorSheet;
                string mine = Player.RegionVanity.Sheet(d.Region, sheet);
                return mine == sheet || !Has(mine);
            }).Select(d => d.Type.ToString()).ToList();
            Check("地区时装都有本地区配色的穿戴贴图", plain.Count == 0, string.Join(" ", plain.Take(20)));
            var unreached = new List<string>();
            // As in a world with every boss beaten: a Godhome-only fight pays its own once the Cultist is down.
            var keptClears = VanillaBossProgression.DevClears;
            VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Keys);
            foreach (var r in rows)
            {
                var drops = EnemyDrops.At(r.Record, 5, false, true, true);
                if (!drops.Any(x => x.Type == r.Lead)) unreached.Add($"{r.Record}→{r.Lead}");
            }
            VanillaBossProgression.DevClears = keptClears;
            Check($"{rows.Count} 条怪物时装掉落都在怪物掉落表里（阶段 5）", unreached.Count == 0, string.Join(" ", unreached.Take(30)));
            // In no equipment pool: crates and chests take none.
            var pooled = vanity.Where(d => ProgressionLoot.Droppable(d.Type)).Select(d => d.Type.ToString()).ToList();
            Check("时装不进通用装备池（宝箱、钓鱼箱）", pooled.Count == 0, string.Join(" ", pooled.Take(20)));

            // Worn.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            Player.TerraStats.DevImmortal = true;
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 26f);
            var rb = hero.GetComponent<Rigidbody2D>();
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            var keptArmor = (ItemStack[])inv.VanityArmor.Clone();
            var keptAcc = (ItemStack[])inv.VanityAccessories.Clone();
            bool shots = Environment.GetEnvironmentVariable("TK_VANITY_SHOTS") != "0";
            var outfits = new List<(string Name, List<ItemType> Pieces)>();
            foreach (var r in rows.GroupBy(x => x.Lead).Select(g => g.First()))
                outfits.Add((r.Lead.ToString(), new List<ItemType> { r.Lead }.Concat(VanityDrops.Mates(r.Lead)).ToList()));
            foreach (var rep in VanityDrops.DeveloperReps)
                outfits.Add((rep.ToString(), new List<ItemType> { rep }.Concat(VanityDrops.Mates(rep)).Where(t => ItemDb.Get(t).Vanity).ToList()));
            foreach (var g in vanity.Where(d => d.Region > 0).GroupBy(d => d.Region))
                outfits.Add(("Region_" + World.AreaBlocks.All[g.Key - 1].Key, g.Select(d => d.Type).ToList()));
            // TK_VANITY_ONLY: the outfits whose name starts so (Region_: the areas' costumes alone).
            string only = Environment.GetEnvironmentVariable("TK_VANITY_ONLY");
            if (!string.IsNullOrEmpty(only)) outfits.RemoveAll(o => !o.Name.StartsWith(only));
            int broken = 0;
            foreach (var (name, pieces) in outfits)
            {
                for (int i = 0; i < inv.VanityArmor.Length; i++) inv.VanityArmor[i] = default;
                for (int i = 0; i < inv.VanityAccessories.Length; i++) inv.VanityAccessories[i] = default;
                int acc = 0;
                foreach (var t in pieces)
                {
                    var d = ItemDb.Get(t);
                    if (d.IsArmor) inv.VanityArmor[(int)d.Armor - 1] = ItemStack.Of(t);
                    else if (d.IsAccessory && acc < Items.Inventory.OpenAccessories) inv.VanityAccessories[acc++] = ItemStack.Of(t);
                }
                yield return null;
                yield return null;
                string miss = mod.PlayerLook != null ? mod.PlayerLook.DevMissing() : "-";
                if (miss.Length > 0)
                {
                    broken++;
                    string sizes = string.Join(" ", miss.Split(',').Distinct().Select(n => { var tx = TerrariaAssets.Texture(n); return tx != null ? $"{n}={tx.width}x{tx.height}" : n + "=无"; }));
                    Check($"穿上 {name}（{pieces.Count} 件）各层都有图", false, $"{miss}（{sizes}）");
                }
                else { ok++; all++; }
                if (shots)
                {
                    var sp = Camera.main.WorldToScreenPoint(hero.GetComponent<BoxCollider2D>().bounds.center);
                    Plugin.Log.LogInfo($"[AutoTest] 时装 截图位置 {name} {sp.x / Screen.width:F3},{1f - sp.y / Screen.height:F3}");
                    Shot("Town", "vanity_" + name);
                    yield return null;
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 时装 穿戴 {outfits.Count} 套，缺图 {broken} 套");
            Array.Copy(keptArmor, inv.VanityArmor, keptArmor.Length);
            Array.Copy(keptAcc, inv.VanityAccessories, keptAcc.Length);
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            Plugin.Log.LogInfo($"[AutoTest] 时装 合计 {ok}/{all}");
        }
    }
}
