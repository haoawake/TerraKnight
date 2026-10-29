using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "leftlatch", the user's 10-27 "很多武器右键蓄力的时候左键还按着就会触发双倍动画双倍伤害"): in Dirtmouth by its
    /// Target Dummy, weapons of each kind of right-click art (charged: katana, greatsword, Terra Blade, Starfury, spear, yoyo,
    /// flail, boomerang, shortsword, Zenith; pressed: the Ice Blade's frost, the Cutlass's parry, the Light's Bane's step):
    /// the left button held and swinging, the right pressed (held 1 s for a charged art) and let go, the left kept down: no use
    /// of the left's within 0.25 s of the art (its recovery, BladeSkills.HoldsLeft: no double hit), and a weapon that goes on
    /// while held goes on again by itself within 1.5 s (the user's 10-27 "如果左键还按着不应该失效而是回到左键的设计流程"); the
    /// left let go and pressed again uses again. With no recovery at all (BladeSkills.DevNoLatch) for each, to compare.
    /// Lines "[AutoTest] 左键 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LeftLatchTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt ?? (Vector2)hero.transform.position + Vector2.right * 4f;
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            var weapons = new[]
            {
                ItemType.Katana, ItemType.BreakerBlade, ItemType.TerraBlade, ItemType.Starfury, ItemType.Trident, ItemType.Rally,
                ItemType.BallOHurt, ItemType.EnchantedBoomerang, ItemType.CopperShortsword, ItemType.Zenith,
                ItemType.IceBlade, ItemType.Cutlass, ItemType.LightsBane,
            };
            var bad = new List<string>();
            try
            {
                foreach (var type in weapons)
                    foreach (bool old in new[] { true, false })
                    {
                        BladeSkills.DevNoLatch = old;
                        var def = ItemDb.Get(type);
                        var art = BladeSkills.Of(def);
                        bool charged = art == BladeSkills.Skill.Kind ? BladeSkills.KindArtOf(def)?.Charged == true
                            : art == BladeSkills.Skill.Draw || art == BladeSkills.Skill.Slam || art == BladeSkills.Skill.TerraDash || art == BladeSkills.Skill.Starfall || art.ToString().StartsWith("Spear") || art == BladeSkills.Skill.TriThrust;
                        hero.transform.position = Clear(hero, new Vector2(at.x - 4f, at.y - bottom + 0.05f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        inv.Slots[0] = ItemStack.Of(type);
                        inv.Selected = 0;
                        mod.DevAim = at + new Vector2(0f, 0.8f);
                        yield return new WaitForSeconds(0.6f);
                        // The left held, swinging (going on by itself if the weapon does).
                        int held0 = TerraMod.DevUseCount;
                        mod.DevLeftHeld = true; mod.DevHold = true;
                        yield return new WaitForSeconds(1.2f);
                        bool repeats = TerraMod.DevUseCount - held0 >= 2;
                        int uses0 = BladeSkills.DevUses + BladeSkills.DevThrows, latches0 = BladeSkills.DevLeftLatches;
                        // The right: pressed, held a second for a charged art, let go. A pressed art goes off as it is pressed: its
                        // recovery counted from then.
                        int leftAtPress = TerraMod.DevUseCount;
                        float pressedAt = Time.time;
                        BladeSkills.DevHeld = true;
                        BladeSkills.DevPress = true;
                        yield return new WaitForSeconds(charged ? 1.0f : 0.15f);
                        BladeSkills.DevHeld = false;
                        yield return null;
                        int left0 = charged ? TerraMod.DevUseCount : leftAtPress;
                        float artAt = charged ? Time.time : pressedAt;
                        bool fired = BladeSkills.DevUses + BladeSkills.DevThrows > uses0 || BladeSkills.Guarding;
                        // The left kept down after the art: none at once, then on again by itself.
                        yield return new WaitForSeconds(Mathf.Max(0f, artAt + 0.25f - Time.time));
                        int early = TerraMod.DevUseCount - left0;
                        for (float t = 0f; t < 1.5f && TerraMod.DevUseCount - left0 == early; t += Time.deltaTime) yield return null;
                        int after = TerraMod.DevUseCount - left0;
                        // Let go and pressed again: the left's own use.
                        mod.DevLeftHeld = false; mod.DevHold = false;
                        yield return new WaitForSeconds(0.25f);
                        int left1 = TerraMod.DevUseCount;
                        mod.DevLeftHeld = true; mod.DevHold = true;
                        // Pressed again every half second, up to four times: a press while a heavy art (the greatsword's slam) still
                        // keeps the weapon is lost, as a click in a use's cooldown is.
                        for (int press = 0; press < 4 && TerraMod.DevUseCount == left1; press++)
                        {
                            if (press > 0)
                            {
                                mod.DevLeftHeld = false; mod.DevHold = false;
                                yield return new WaitForSeconds(0.15f);
                                mod.DevLeftHeld = true; mod.DevHold = true;
                            }
                            for (float t = 0f; t < 0.35f && TerraMod.DevUseCount == left1; t += Time.deltaTime) yield return null;
                        }
                        int again = TerraMod.DevUseCount - left1;
                        mod.DevLeftHeld = false; mod.DevHold = false;
                        BladeSkills.DevHeld = null;
                        mod.DevAim = null;
                        bool pass = fired && early == 0 && (!repeats || after > 0) && again > 0;
                        if (!old) { all++; if (pass) ok++; else bad.Add(type.ToString()); }
                        Plugin.Log.LogInfo($"[AutoTest] 左键 {(old ? "不等" : "现在")} {def.Name}（{(art == BladeSkills.Skill.Kind ? BladeSkills.DevLastArt : art.ToString())}，{(charged ? "蓄力" : "按下")}，按住{(repeats ? "连续" : "不连续")}）：右键技艺{(fired ? "放出" : "没放出")}，之后左键仍按着 0.25 秒内用了 {early} 次、1.75 秒内 {after} 次，松开再按用了 {again} 次{(old ? "" : pass ? " 通过" : " 不通过")}");
                        yield return new WaitForSeconds(1.5f);
                    }
            }
            finally
            {
                BladeSkills.DevNoLatch = false;
                BladeSkills.DevHeld = null;
                mod.DevLeftHeld = null; mod.DevHold = null;
                mod.DevAim = null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 左键 合计 {ok}/{all}{(bad.Count > 0 ? "，不通过：" + string.Join("、", bad) : "")}");
        }
    }
}
