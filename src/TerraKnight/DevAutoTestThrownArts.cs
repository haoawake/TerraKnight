using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "thrownarts", Items.TerraProjectilesThrownArts / BladeSkillsThrown, the user's 10-26 "继续升级剩下的这些…
    /// 特效，动画，粒子效果，新颖，多样"): in Dirtmouth by its Target Dummy, each melee thrown weapon: thrown up into the open
    /// sky three times with the remade throws (their paths in turn, how far they went) against once with the game's
    /// ("reach": at least 20% further where its flight is remade); its damage a second on the dummy, remade against the
    /// game's (the option off), within 0.85 to 1.45, over 40 of the game's hits (12-30 s) and as long remade ("dps"); its right-click art at the dummy, used, throwing, striking
    /// ("arts"). TK_THROWN_ONLY: item names, and any of reach/dps/arts. Shots Town_thrown_&lt;weapon&gt;(_art).
    /// Lines "[AutoTest] 投掷 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly ItemType[] thrownWeapons =
        {
            ItemType.WoodenBoomerang, ItemType.EnchantedBoomerang, ItemType.IceBoomerang, ItemType.FruitcakeChakram, ItemType.Shroomerang,
            ItemType.Trimarang, ItemType.Axearang, ItemType.BloodyMachete, ItemType.CombatWrench, ItemType.ThornChakram, ItemType.Flamarang,
            ItemType.BouncingShield, ItemType.LightDisc, ItemType.Bananarang, ItemType.PossessedHatchet, ItemType.PaladinsHammer,
            ItemType.VampireKnives, ItemType.ShadowFlameKnife, ItemType.FlyingKnife, ItemType.DayBreak, ItemType.ScourgeoftheCorruptor,
        };

        /// <summary>Their flight is their own (homing, wound up, dropping, leaping, steered): no reach to compare.</summary>
        static readonly HashSet<ItemType> ownFlight = new HashSet<ItemType>
        {
            ItemType.BloodyMachete, ItemType.BouncingShield, ItemType.PossessedHatchet, ItemType.PaladinsHammer, ItemType.FlyingKnife,
            // It flies on (no fall, no turn) until it strikes something.
            ItemType.ScourgeoftheCorruptor,
        };

        IEnumerator ThrownArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 投掷 {what} {(pass ? "通过" : "不通过")}");
            }
            var only = (System.Environment.GetEnvironmentVariable("TK_THROWN_ONLY") ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            bool Part(string name) => only.Count == 0 || only.Contains(name) || !only.Any(o => o == "reach" || o == "dps" || o == "arts");
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at.Value + new Vector2(0f, 0.8f);
            var inv = mod.Inventory;
            var saved = inv.Slots[0];
            int savedSel = inv.Selected;
            var tp = TerraProjectiles.Instance;

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }
            Vector2 Middle() => hero.GetComponent<BoxCollider2D>().bounds.center;

            // Throws for a while at a point, clicking whenever a use may start (and holding, for the held and repeating ones).
            IEnumerator Throws(ItemType type, Vector2 target, float seconds, int most, System.Action each, System.Func<bool> longer = null)
            {
                inv.Slots[0] = ItemStack.Of(type, 1);
                inv.Selected = 0;
                mod.DevAim = target;
                mod.DevHold = true;
                int thrown = 0;
                for (float end = Time.time + seconds; Time.time < end || longer != null && longer();)
                {
                    if (thrown < most && !mod.IsUsingItem && mod.DevReady)
                    {
                        int before = TerraProjectiles.DevMade(ItemDb.Get(type).Shoot);
                        mod.DevUse(type, target);
                        if (TerraProjectiles.DevMade(ItemDb.Get(type).Shoot) > before) thrown++;
                    }
                    each?.Invoke();
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                for (float end = Time.time + 3f; Time.time < end && tp.DevCount(ItemDb.Get(type).Shoot) > 0;) yield return null;
            }

            foreach (var type in thrownWeapons)
            {
                var def = ItemDb.Get(type);
                if (only.Count > 0 && only.Any(o => o != "reach" && o != "dps" && o != "arts") && !only.Contains(type.ToString())) continue;
                var kind = def.Shoot;

                // How far it goes: up into the open sky, the game's once, the remade three times (their paths).
                if (Part("reach") && !ownFlight.Contains(type))
                {
                    float[] far = new float[2];
                    var paths = new List<string>();
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        Stand(7f);
                        yield return new WaitForSeconds(0.6f);
                        var from = Middle();
                        var up = from + new Vector2(-16f, 18f);
                        string last = TerraProjectiles.DevLastPath;
                        bool shotDone = false;
                        yield return Throws(type, up, mode == 1 ? 5.5f : 2.5f, mode == 1 ? 3 : 1, () =>
                        {
                            foreach (var q in tp.DevAll(kind)) far[mode] = Mathf.Max(far[mode], Vector2.Distance(q, from) / Effects.TerraParticles.Px);
                            if (mode == 1 && TerraProjectiles.DevLastPath != last)
                            {
                                last = TerraProjectiles.DevLastPath;
                                paths.Add(last);
                            }
                            if (mode == 1 && !shotDone && far[1] > 100f) { Shot("Town", $"thrown_{type}"); shotDone = true; }
                        });
                    }
                    BladeArts.Set(true);
                    float ratio = far[0] > 0f ? far[1] / far[0] : 0f;
                    bool distinct = paths.Distinct().Count() >= Mathf.Min(2, paths.Count) && paths.Count >= 2;
                    Check(ratio >= 1.2f && distinct, $"{def.Name} 射程：原版 {far[0]:F0} px，新版 {far[1]:F0} px（×{ratio:F2}），路线 {string.Join(" → ", paths)}");
                }

                // Its damage a second on the dummy, remade against the game's.
                if (Part("dps"))
                {
                    float[] dps = new float[2];
                    int[] hitsBy = new int[2], throwsBy = new int[2];
                    // Long enough for the game's to strike 40 times (12 to 30 s; a slow boomerang's few crits otherwise swing it
                    // a fifth either way), the remade one the same time.
                    float window = 12f;
                    var blows = new[] { new List<int>(), new List<int>() };
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        Stand(type == ItemType.FlyingKnife ? 3f : 4.5f);
                        yield return new WaitForSeconds(1.2f);
                        int dmg0 = TargetDummy.DevDamage, hits0 = TargetDummy.DevHits, made0 = TerraProjectiles.DevMade(kind);
                        TerraProjectiles.DevFlightLog.Clear();
                        TerraProjectiles.DevTrackVanilla = true;
                        float start = Time.time;
                        bool shotDone = ownFlight.Contains(type) && mode == 1 ? false : true;
                        float lastThrow = start;
                        int seen = TargetDummy.DevDamage, m = mode;
                        yield return Throws(type, aim, window, 999, () =>
                        {
                            lastThrow = Time.time;
                            // Each frame's damage, for the blows' sizes (a crit doubles one).
                            if (TargetDummy.DevDamage > seen) { blows[m].Add(TargetDummy.DevDamage - seen); seen = TargetDummy.DevDamage; }
                            if (!shotDone && Time.time - start > 1f) { Shot("Town", $"thrown_{type}"); shotDone = true; }
                        }, mode == 0 ? () => TargetDummy.DevHits - hits0 < 40 && Time.time - start < 30f : (System.Func<bool>)null);
                        if (mode == 0) window = Mathf.Max(12f, lastThrow - start);
                        dps[mode] = (TargetDummy.DevDamage - dmg0) / Mathf.Max(1f, Time.time - start);
                        hitsBy[mode] = TargetDummy.DevHits - hits0;
                        TerraProjectiles.DevTrackVanilla = false;
                        throwsBy[mode] = TerraProjectiles.DevMade(kind) - made0;
                        if (TerraProjectiles.DevFlightLog.Count > 0) Plugin.Log.LogInfo($"[AutoTest] 投掷 {def.Name} 各次：{string.Join("；", TerraProjectiles.DevFlightLog)}");
                    }
                    BladeArts.Set(true);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    string Blows(List<int> b)
                    {
                        if (b.Count == 0) return "无";
                        var sorted = b.OrderBy(x => x).ToList();
                        int median = sorted[sorted.Count / 2];
                        return $"中位 {median}，平均 {b.Average():F1}，暴击约 {b.Count(x => x > median * 1.6f)}";
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 投掷 {def.Name} 每击：原版 {Blows(blows[0])}；新版 {Blows(blows[1])}");
                    Check(ratio >= 0.85f && ratio <= 1.45f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新版 {dps[1]:F0}（×{ratio:F2}），{window:F0} 秒，掷出 {throwsBy[0]}/{throwsBy[1]}，命中 {hitsBy[0]}/{hitsBy[1]}");
                }

                // Its right-click art at the dummy.
                if (Part("arts") && type != ItemType.BouncingShield)
                {
                    inv.Slots[0] = ItemStack.Of(type, 1);
                    inv.Selected = 0;
                    Stand(type == ItemType.ThornChakram ? 1.1f : 4.5f);
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(1.5f);
                    var art = BladeSkills.KindArtOf(def);
                    int uses0 = BladeSkills.DevUses, dmg0 = TargetDummy.DevDamage, arts0 = TerraProjectiles.DevArtThrows;
                    if (art != null && art.Charged)
                    {
                        BladeSkills.DevHeld = true;
                        BladeSkills.DevPress = true;
                        yield return new WaitForSeconds(art.Full + 0.25f);
                        BladeSkills.DevHeld = false;
                    }
                    else BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(0.45f);
                    Shot("Town", $"thrown_{type}_art");
                    yield return new WaitForSeconds(2.6f);
                    BladeSkills.DevHeld = null;
                    mod.DevAim = null;
                    int artDmg = TargetDummy.DevDamage - dmg0, arts = TerraProjectiles.DevArtThrows - arts0;
                    bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLastArt == art?.Name;
                    Check(art != null && went && arts > 0 && artDmg > 0, $"右键 {def.Name}：{art?.Name}，用了 {(went ? "是" : "否")}，掷出 {arts} 个，假人受到 {artDmg}");
                    for (float end = Time.time + 4f; Time.time < end && tp.DevCount(kind) > 0;) yield return null;
                    yield return new WaitForSeconds(0.5f);
                }
            }
            BladeArts.Set(true);
            inv.Slots[0] = saved;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 投掷 路线 {TerraProjectiles.DevThrowPaths}，折射火花 {TerraProjectiles.DevSparks}，接住 {TerraProjectiles.DevCatches}，右键掷出 {TerraProjectiles.DevArtThrows}");
            Plugin.Log.LogInfo($"[AutoTest] 投掷 合计 {ok}/{all}");
        }
    }
}
