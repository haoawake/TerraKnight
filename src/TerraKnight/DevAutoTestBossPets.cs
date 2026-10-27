using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bosspets", the user's 10-26 boss pets): the 48 pet items, their sheets and icons, buffs; each boss's own
    /// pet in its drops at 1 in 100 (a variant's where its fight is, and not its record's there), none in chests, crates or
    /// the kill eggs; then each called in Dirtmouth: a walker's idle and walk frames and its flight after a far player, a
    /// flyer's loop, a shoulder pet's float and gesture; a screenshot each (TK_BOSSPETS_SHOTS=0: none). Lines
    /// "[AutoTest] 首领宠物 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossPetsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                if (!pass || detail.Length < 240) Plugin.Log.LogInfo($"[AutoTest] 首领宠物 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var rows = BossPets.Table.Rows;
            var bad = new List<string>();
            foreach (var r in rows)
            {
                var d = ItemDb.Get(r.Type);
                var sheet = Terraria.TerrariaAssets.Texture("BossPet_" + r.Name);
                var icon = Terraria.TerrariaAssets.Texture("BossPetItem_" + r.Name);
                var buffIcon = Terraria.TerrariaAssets.Texture("Buff_" + d.PetBuff);
                if (d == null || d.Placeholder || d.PetBuff <= 0 || !Pets.Is(r.Type) || sheet == null || sheet.width != r.CellW || sheet.height != r.CellH * r.Frames
                    || icon == null || buffIcon == null || d.LightPet != r.Light || d.Rare != -13)
                    bad.Add($"{r.Name}（贴图 {sheet?.width}x{sheet?.height}，图标 {icon != null}，增益图标 {buffIcon != null}）");
            }
            Check($"{rows.Length} 只首领宠物的物品、贴图、图标和增益都在", rows.Length == 48 && bad.Count == 0, string.Join(" ", bad.Take(10)));

            var missing = new List<string>();
            foreach (var r in rows)
            {
                string scene = r.Scenes != null ? r.Scenes[0] : null;
                var drops = scene != null ? EnemyDrops.At(r.Record, 5, false, true, true, scene) : EnemyDrops.At(r.Record, 5, false, true, true);
                var row = drops.FirstOrDefault(x => x.Type == r.Type);
                if (row.Type != r.Type || row.Num != 1 || row.Den != BossPets.OneIn) missing.Add($"{r.Record}{(scene != null ? "#" + scene : "")}→{r.Name}");
                // The record's own pet not where its variant is fought.
                if (r.Scenes != null && drops.Any(x => x.Type != r.Type && BossPets.Is(x.Type))) missing.Add($"{r.Record}#{scene} 还掉 {string.Join("/", drops.Where(x => x.Type != r.Type && BossPets.Is(x.Type)).Select(x => x.Type))}");
            }
            Check("每个首领掉落自己的宠物（1/100，变体按场地）", missing.Count == 0, string.Join(" ", missing.Take(12)));
            var leaked = rows.Where(r => !BossDropPlan.BossOnly(r.Type) || ProgressionLoot.Droppable(r.Type) || Pets.Eggs.Contains(r.Type)).Select(r => r.Name).ToList();
            Check("首领宠物不进宝箱、钓鱼箱和击杀彩蛋", leaked.Count == 0, string.Join(" ", leaked.Take(12)));

            // Called, one by one.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            // Where he stands after coming in (the well a few steps on is a long way down).
            for (float until = Time.time + 5f; Time.time < until && !hero.cState.onGround;) yield return null;
            var start = (Vector2)hero.transform.position;
            bool shots = Environment.GetEnvironmentVariable("TK_BOSSPETS_SHOTS") != "0";
            var keptPet = inv.Misc[Pets.PetSlot];
            var keptLight = inv.Misc[Pets.LightSlot];
            var moving = new List<string>();
            string only = Environment.GetEnvironmentVariable("TK_BOSSPETS_ONLY");
            foreach (var r in rows)
            {
                if (!string.IsNullOrEmpty(only) && !only.Split(';').Contains(r.Name)) continue;
                inv.Misc[Pets.PetSlot] = default;
                inv.Misc[Pets.LightSlot] = default;
                yield return null;
                inv.Misc[r.Light ? Pets.LightSlot : Pets.PetSlot] = ItemStack.Of(r.Type);
                hero.transform.position = Clear(hero, start);
                var frames = new HashSet<int>();
                bool flew = false;
                for (float t0 = Time.time; Time.time - t0 < 1.6f;)
                {
                    yield return null;
                    var p = Minions.DevPet(r.Light);
                    // The pet called now (a frame of the one before may still be read as it goes).
                    if (p.HasValue && ((r.Kind == BossPets.Kind.Ground) != (p.Value.id >= 20000) || p.Value.frame >= r.Frames)) p = null;
                    if (p.HasValue) frames.Add(p.Value.frame);
                    if (!string.IsNullOrEmpty(only))
                    {
                        var mo = Minions.DevPetMotion(r.Light);
                        var hb = Effects.TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
                        if (mo.HasValue) Plugin.Log.LogInfo($"[AutoTest] 首领宠物 轨迹 {r.Name} t={Time.time - t0:F2} 帧 {p?.frame} 在 {mo.Value.at:F0} 速 {mo.Value.vel:F2} 飞 {mo.Value.ai0} 主角 {hb:F0}");
                    }
                }
                if (shots)
                {
                    var p = Minions.DevPet(r.Light);
                    var cam = Camera.main;
                    if (p.HasValue && cam != null)
                    {
                        var world = Effects.TerraParticles.ToWorld(p.Value.at);
                        var sp = cam.WorldToScreenPoint(world);
                        Plugin.Log.LogInfo($"[AutoTest] 首领宠物 截图位置 {r.Name} {sp.x / Screen.width:F3},{1f - sp.y / Screen.height:F3}");
                    }
                    Shot("Town", "bosspet_" + r.Name);
                }
                // Across the room (a walker walks), then far off and up (it flies after him); a shoulder pet's gesture meanwhile.
                hero.transform.position = Clear(hero, start + Vector2.right * 5f);
                for (float t0 = Time.time; Time.time - t0 < (r.Kind == BossPets.Kind.Anchor ? 6f : 2f);)
                {
                    yield return null;
                    var p = Minions.DevPet(r.Light);
                    if (p.HasValue) frames.Add(p.Value.frame);
                }
                if (r.Kind == BossPets.Kind.Ground)
                {
                    hero.transform.position = Clear(hero, start + new Vector2(-30f, 6f));
                    for (float t0 = Time.time; Time.time - t0 < 2.5f;)
                    {
                        yield return null;
                        var p = Minions.DevPet(r.Light);
                        if (p.HasValue) { frames.Add(p.Value.frame); flew |= p.Value.flying; }
                    }
                }
                bool pass = r.Kind == BossPets.Kind.Ground ? frames.Any(f => f <= 3) && frames.Any(f => f >= 4 && f <= 11) && flew && frames.Any(f => f >= 14)
                    : r.Kind == BossPets.Kind.Flyer ? frames.Count >= 3 && frames.All(f => f < 8)
                    : frames.Any(f => f < 8) && frames.Any(f => f >= 8 && f < 12);
                if (!pass) moving.Add($"{r.Name}（{r.Kind}：帧 {string.Join(",", frames.OrderBy(f => f))}，飞过 {flew}）");
                else Plugin.Log.LogInfo($"[AutoTest] 首领宠物 {r.Name}（{r.Kind}）帧 {string.Join(",", frames.OrderBy(f => f))}，飞过 {flew}");
            }
            Check("每只宠物召唤出来动作齐全（地面：待机、走、飞着追；飞行：循环；肩旁：漂浮和小动作）", moving.Count == 0, string.Join(" ", moving.Take(12)));
            inv.Misc[Pets.PetSlot] = keptPet;
            inv.Misc[Pets.LightSlot] = keptLight;
            Plugin.Log.LogInfo($"[AutoTest] 首领宠物 合计 {ok}/{all}");
        }
    }
}
