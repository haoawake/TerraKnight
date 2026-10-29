using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "rightspam", the user's 10-27 "有些武器按住左键的时候狂点右键可以无限重置左键的攻击，比如腐化者之戟"): in Dirtmouth
    /// by its Target Dummy, weapons with right-click arts (the Scourge of the Corruptor's charged throw, the katana's draw, the
    /// Breaker Blade's slam, a phase blade's throw, the Tax Collector's pressed art): the left's uses in 3 s held alone; with the
    /// right clicked every 0.1 s while the left is held; with the two clicked in turn. Neither may get more of the left's
    /// uses than held alone (TerraMod.CutSwing keeps the use time). As before (TerraMod.DevOldCut, BladeSkills.DevNoLatch), to
    /// compare. Lines "[AutoTest] 右键连点 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RightSpamTest()
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
            const float Seconds = 3f;

            void Left(bool down) { mod.DevLeftHeld = down; mod.DevHold = down; }

            // The left's uses over the time: held (the right clicked every 0.1 s if spam), or the two clicked in turn.
            IEnumerator Run(string how, System.Action<int> done)
            {
                int uses0 = TerraMod.DevUseCount;
                float start = Time.time, nextRight = start + 0.1f;
                bool turn = false;
                if (how != "交替" && how != "只点左键") Left(true);
                while (Time.time - start < Seconds)
                {
                    if (how == "连点右键" && Time.time >= nextRight)
                    {
                        nextRight += 0.1f;
                        BladeSkills.DevPress = true;
                        BladeSkills.DevHeld = true;
                        yield return null;
                        BladeSkills.DevHeld = false;
                    }
                    else if ((how == "交替" || how == "只点左键") && Time.time >= nextRight)
                    {
                        nextRight += 0.1f;
                        turn = !turn;
                        if (turn) { Left(true); yield return null; Left(false); }
                        else if (how == "交替") { BladeSkills.DevPress = true; BladeSkills.DevHeld = true; yield return null; BladeSkills.DevHeld = false; }
                    }
                    yield return null;
                }
                Left(false);
                BladeSkills.DevHeld = null;
                done(TerraMod.DevUseCount - uses0);
                yield return new WaitForSeconds(1.2f);
            }

            var weapons = new[] { ItemType.ScourgeoftheCorruptor, ItemType.Katana, ItemType.BreakerBlade, ItemType.BluePhaseblade, ItemType.TaxCollectorsStickOfDoom };
            try
            {
                foreach (var type in weapons)
                {
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    hero.transform.position = Clear(hero, new Vector2(at.x - 4f, at.y - bottom + 0.05f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    hero.FaceRight();
                    mod.DevAim = at + new Vector2(0f, 0.8f);
                    yield return new WaitForSeconds(0.5f);
                    int alone = 0, clicks = 0;
                    yield return Run("只按左键", n => alone = n);
                    // The left clicked at the alternation's pace (every 0.2 s), no right: what clicking alone gets.
                    yield return Run("只点左键", n => clicks = n);
                    foreach (bool old in new[] { true, false })
                    {
                        TerraMod.DevOldCut = old;
                        BladeSkills.DevNoLatch = old;
                        int spam = 0, turns = 0;
                        yield return Run("连点右键", n => spam = n);
                        yield return Run("交替", n => turns = n);
                        bool pass = spam <= alone && turns <= Mathf.Max(alone, clicks);
                        if (!old) { all++; if (pass) ok++; }
                        Plugin.Log.LogInfo($"[AutoTest] 右键连点 {(old ? "以前" : "现在")} {ItemDb.Get(type).Name}：{Seconds} 秒内左键出手 只按左键 {alone} 次，只点左键 {clicks} 次，按住左键连点右键 {spam} 次，左右交替点 {turns} 次{(old ? "" : pass ? " 通过" : " 不通过")}");
                    }
                    mod.DevAim = null;
                }
            }
            finally
            {
                TerraMod.DevOldCut = false;
                BladeSkills.DevNoLatch = false;
                BladeSkills.DevHeld = null;
                Left(false);
                mod.DevLeftHeld = null;
                mod.DevHold = null;
                mod.DevAim = null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 右键连点 合计 {ok}/{all}");
        }
    }
}
