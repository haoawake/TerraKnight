using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bladearts", the user's 10-26 remade true melee and its corrections): every true melee weapon's kind and
    /// combo listed; no two combos alike; every stroke in front of the player (the blade never past 117 degrees from the
    /// aim); a club, a mace or a hand never thrusts; every weapon's flat blade made. At Dirtmouth's Target Dummy, for a
    /// range of weapons: four seconds of uses with the game's swing and four with the remade one (no crits or rolls), the
    /// damage a second and, for the swords that shoot, their shots' hits (the game's pace kept); a three-move combo taking
    /// its moves in turn on quick uses and starting over after a pause; a thrust following the cursor up and down; then
    /// close-ups through one use of a weapon of each kind (TK_BA_SHOTS=0: none; TK_BA_ONLY=ItemType,...: just those).
    /// Lines "[AutoTest] 新挥砍 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BladeArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 新挥砍 {what} {(pass ? "通过" : "不通过")}");
            }
            // The table.
            var weapons = ItemDb.All.Where(d => d != null && BladeArts.Is(d)).OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage).ToList();
            var seen = new Dictionary<string, ItemType>();
            int dupes = 0, backward = 0, poking = 0, flatless = 0;
            foreach (var d in weapons)
            {
                var art = BladeArts.Of(d);
                string combo = string.Join(" ", art.Combo.Select(m => m.Name));
                if (seen.TryGetValue(combo, out var other)) { dupes++; Plugin.Log.LogInfo($"[AutoTest] 新挥砍 重复连招 {d.Type} 与 {other}：{combo}"); }
                else seen[combo] = d.Type;
                float worst = 0f;
                foreach (var m in art.Combo)
                    foreach (var s in m.Strokes)
                        for (float su = 0f; su <= 1f; su += 0.05f)
                        {
                            float deg = BladeArts.Blade(s, su, 0f, out _, out _, out _);
                            worst = Mathf.Max(worst, Mathf.Abs(Mathf.DeltaAngle(0f, deg)));
                        }
                if (worst > 135f) { backward++; Plugin.Log.LogInfo($"[AutoTest] 新挥砍 往后挥 {d.Type} {combo} 最大 {worst:F0}°"); }
                // Only a knife stabs (the user's 10-26 "大剑和刀这种就不应该有戳的动画…短剑才会戳").
                if (art.Kind != BladeArts.Kind.Knife && art.Combo.Any(m => m.Strokes.Any(s => s.Shape == BladeArts.Shape.Thrust || s.Shape == BladeArts.Shape.Drag || s.Shape == BladeArts.Shape.Plunge)))
                { poking++; Plugin.Log.LogInfo($"[AutoTest] 新挥砍 不该戳的在戳 {d.Type} {combo}"); }
                float blade = SwordFx.BladeLength(d, 1f);
                if (!SwordFx.DevHasFlat(d)) flatless++;
                Plugin.Log.LogInfo($"[AutoTest] 新挥砍 表 {d.Type} {d.Name} {art.Kind} 阶段{d.Stage} 级{art.Level} 增伤{art.Boost:F2} 范围{art.Reach:F2} 剑身{blade:F0}px 色 #{ColorUtility.ToHtmlStringRGB(art.Dark)}/#{ColorUtility.ToHtmlStringRGB(art.Main)}/#{ColorUtility.ToHtmlStringRGB(art.Light)}{(art.Rainbow ? " 彩虹" : "")} 粒子{art.Mote}：{combo}（最大偏离 {worst:F0}°）");
            }
            Check(weapons.Count >= 90, $"真近战武器 {weapons.Count} 把");
            Check(dupes == 0, $"连招两两不同（重复 {dupes}）");
            Check(backward == 0, $"每一刀都在身前（往后挥的 {backward} 把）");
            Check(poking == 0, $"只有短刀会戳（别的 {poking} 把在戳）");
            Check(flatless == 0, $"每把都有平放的剑身（没有的 {flatless} 把）");

            // The dummy.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            var savedSlot = inv.Slots[0];
            int savedSel = inv.Selected;
            bool shots = System.Environment.GetEnvironmentVariable("TK_BA_SHOTS") != "0";
            var only = System.Environment.GetEnvironmentVariable("TK_BA_ONLY");
            var sample = new List<ItemType>
            {
                ItemType.WoodenSword, ItemType.CopperBroadsword, ItemType.SilverBroadsword, ItemType.Katana, ItemType.TentacleSpike, ItemType.Muramasa,
                ItemType.NightsEdge, ItemType.BladedGlove, ItemType.BreakerBlade, ItemType.Excalibur, ItemType.LucyTheAxe, ItemType.DeathSickle,
                ItemType.TerraBlade, ItemType.InfluxWaver, ItemType.PsychoKnife, ItemType.StarWrath, ItemType.Meowmere, ItemType.TrueNightsEdge,
                ItemType.TheHorsemansBlade, ItemType.FieryGreatsword, ItemType.HamBat, ItemType.Starfury, ItemType.Frostbrand,
            };
            if (!string.IsNullOrEmpty(only))
            {
                sample.Clear();
                foreach (var s in only.Split(','))
                    if (System.Enum.TryParse<ItemType>(s.Trim(), out var ty)) sample.Add(ty);
            }
            var aim = at.Value + new Vector2(0f, 0.8f);
            IEnumerator Stand()
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - 1.5f, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(0.4f);
            }
            int better = 0, measured = 0;
            // No crits or rolls: the two swings compared without the dice; four seconds each of a held button, aimed at the dummy.
            MeleeHits.DevSteady = true;
            mod.DevAim = aim;
            // Measured standing: the cuts that run or spring stay put here.
            BladeArts.DevNoBody = true;
            foreach (var type in sample)
            {
                var def = ItemDb.Get(type);
                if (def == null) continue;
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
                var result = new float[2];
                var hitsOf = new int[2];
                var shotHits = new int[2];
                for (int pass = 0; pass < 2; pass++)
                {
                    BladeArts.Set(pass == 1);
                    yield return Stand();
                    while (mod.IsUsingItem) yield return null;
                    TerraProjectiles.Instance?.DevClear();
                    yield return null;
                    int d0 = TargetDummy.DevDamage, h0 = TargetDummy.DevHits, p0 = TerraProjectiles.DevHit(def.Shoot);
                    float began = Time.time;
                    for (float end = Time.time + 4f; Time.time < end;)
                    {
                        if (mod.DevReady) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    while (mod.IsUsingItem) yield return null;
                    yield return new WaitForSeconds(1f);
                    float elapsed = Time.time - began;
                    result[pass] = (TargetDummy.DevDamage - d0) / elapsed;
                    hitsOf[pass] = TargetDummy.DevHits - h0;
                    shotHits[pass] = TerraProjectiles.DevHit(def.Shoot) - p0;
                }
                measured++;
                float ratio = result[0] > 0 ? result[1] / result[0] : 0f;
                var a2 = BladeArts.Of(def);
                bool shoots = def.Shoot != ProjKind.None || def.Fx == MeleeFx.TerraBlade || def.Fx == MeleeFx.TrueNightsEdge || def.Fx == MeleeFx.NightsEdge || def.Fx == MeleeFx.HorsemansBlade;
                bool thrown = TerraProjectiles.IsPhaseblade(def.Shoot);
                // The melee part gains the boost; the shots stay at the game's pace; the game's held swing loses every other
                // use when its animation outlasts its use time by a tick (the Copper Broadsword's 21/20).
                float high = def.UseAnimation > def.UseTime ? 2.2f : 1.45f;
                bool fine = thrown ? result[1] > 0 : result[1] > 0 && ratio > (shoots ? 0.9f : 0.9f * a2.Boost) && ratio < high;
                if (def.Shoot != ProjKind.None && shotHits[0] > 0 && shotHits[1] < shotHits[0] * 0.8f) fine = false;
                if (fine) better++;
                Plugin.Log.LogInfo($"[AutoTest] 新挥砍 假人 {type} {def.Name}：原版每秒 {result[0]:F0}（{hitsOf[0]} 下，弹幕 {shotHits[0]}），新版每秒 {result[1]:F0}（{hitsOf[1]} 下，弹幕 {shotHits[1]}），比 {ratio:F2}（增伤 {a2.Boost:F2}） {(fine ? "通过" : "不通过")}");
            }
            BladeArts.Set(true);
            BladeArts.DevNoBody = false;
            MeleeHits.DevSteady = false;
            Check(better == measured && measured > 0, $"假人上新版伤害约为原版×增伤、弹幕不少（{better}/{measured}）");

            // The combo: quick uses take its moves in turn, a pause starts it over.
            inv.Slots[0] = ItemStack.Of(ItemType.CobaltSword);
            inv.Selected = 0;
            yield return Stand();
            var movesSeen = new List<string>();
            for (int k = 0; k < 4; k++)
            {
                if (k == 3) yield return new WaitForSeconds(1f);
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(aim);
                yield return null;
                movesSeen.Add(SwordFx.DevLastMove);
            }
            var cobalt = BladeArts.Of(ItemDb.Get(ItemType.CobaltSword)).Combo.Select(m => m.Name).ToArray();
            Check(movesSeen.SequenceEqual(new[] { cobalt[0], cobalt[1], cobalt[2], cobalt[0] }), $"钴剑连招按顺序出、停一下从头：{string.Join(" → ", movesSeen)}");

            // The aim: the Iron Broadsword's thrust (its second move) at a point up and ahead, then down and ahead.
            inv.Slots[0] = ItemStack.Of(ItemType.IronBroadsword);
            var aims = new List<float>();
            foreach (var dy in new[] { 2.5f, -2.5f })
            {
                yield return Stand();
                var c = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
                mod.DevAim = c + new Vector2(2.5f, dy);
                while (!mod.DevReady || mod.IsUsingItem) yield return null;
                mod.DevUseSelected(mod.DevAim.Value);
                while (mod.IsUsingItem) yield return null;
                mod.DevUseSelected(mod.DevAim.Value);
                yield return null;
                aims.Add(mod.DevArtAim);
                Plugin.Log.LogInfo($"[AutoTest] 新挥砍 朝向 {SwordFx.DevLastMove} 瞄 {mod.DevArtAim:F0}°");
                while (mod.IsUsingItem) yield return null;
                yield return new WaitForSeconds(0.8f);
            }
            Check(aims.Count == 2 && aims[0] > 30f && aims[1] < -30f, $"突刺跟着鼠标：朝上 {(aims.Count > 0 ? aims[0] : 0f):F0}°，朝下 {(aims.Count > 1 ? aims[1] : 0f):F0}°");
            mod.DevAim = aim;

            // The left button moves nobody (the user's 10-26 "左键的攻击不要有强制位移"): a phase blade's cuts, a greatsword's,
            // and the katana's in the air with the cursor below leave the player where he is (no plunge).
            IEnumerator Away()
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - 9f, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.8f);
            }
            float worstMove = 0f;
            foreach (var type in new[] { ItemType.BluePhaseblade, ItemType.BreakerBlade, ItemType.Muramasa })
            {
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
                yield return Away();
                float x0 = hero.transform.position.x;
                mod.DevAim = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + new Vector2(-3f, 0f);
                for (int k = 0; k < 3; k++)
                {
                    while (!mod.DevReady || mod.IsUsingItem) yield return null;
                    mod.DevUseSelected(mod.DevAim.Value);
                    yield return null;
                }
                while (mod.IsUsingItem) yield return null;
                yield return new WaitForSeconds(0.3f);
                worstMove = Mathf.Max(worstMove, Mathf.Abs(x0 - hero.transform.position.x));
            }
            inv.Slots[0] = ItemStack.Of(ItemType.Katana);
            yield return new WaitForSeconds(0.6f);
            hero.transform.position = Clear(hero, new Vector2(at.Value.x, at.Value.y + 3.5f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.05f);
            mod.DevAim = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + new Vector2(0.4f, -4f);
            while (!mod.DevReady || mod.IsUsingItem) yield return null;
            mod.DevUseSelected(mod.DevAim.Value);
            yield return null;
            string air = SwordFx.DevLastMove;
            while (mod.IsUsingItem) yield return null;
            Check(worstMove < 0.3f && air != "Plunge", $"左键出招人不动（最多移动 {worstMove:F2}），空中朝下也不下冲（{air}）");
            mod.DevAim = aim;

            // Close-ups through a use of each kind.
            if (shots)
            {
                var looks = new[]
                {
                    ItemType.WoodenSword, ItemType.BorealWoodSword, ItemType.IronBroadsword, ItemType.TentacleSpike, ItemType.LucyTheAxe, ItemType.DeathSickle,
                    ItemType.PsychoKnife, ItemType.BladedGlove, ItemType.Muramasa, ItemType.RedPhasesaber, ItemType.BreakerBlade, ItemType.TerraBlade, ItemType.Meowmere,
                };
                foreach (var type in looks)
                {
                    var def = ItemDb.Get(type);
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    var art = BladeArts.Of(def);
                    for (int m = 0; m < art.Combo.Length && m < 3; m++)
                    {
                        yield return Stand();
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        float began = Time.time;
                        mod.DevUseSelected(aim);
                        float dur = def.UseAnimation * Equipment.UseScale(def);
                        foreach (float f in new[] { 0.25f, 0.5f, 0.75f })
                        {
                            while (Time.time - began < f * dur) yield return null;
                            yield return CloseUpAt("Town", $"bladearts_{type}_{m}{(int)(f * 100):D2}", hero.GetComponent<BoxCollider2D>().bounds.center, 110, 2);
                        }
                        while (mod.IsUsingItem) yield return null;
                    }
                    yield return new WaitForSeconds(0.6f);
                }
            }
            mod.DevAim = null;
            Check(SwordFx.DevArtCuts > 0 && MeleeHits.DevArcHits > 0, $"画出的挥砍 {SwordFx.DevArtCuts} 次，收招 {SwordFx.DevArtFinishers} 次，砸地 {SwordFx.DevArtImpacts} 次，命中闪光 {SwordFx.DevArtFlashes} 次");
            inv.Slots[0] = savedSlot;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 新挥砍 合计 {ok}/{all}");
        }
    }
}
