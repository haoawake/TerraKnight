using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "magearts", MageLefts / MageFire / BladeSkillsMagic / SwordFxMageCircles, the user's 10-28 "现在开始做新版本的
    /// 法师武器吧"): each magic weapon (but the Hallownest spells') at the Target Dummy in Dirtmouth — its damage a second the
    /// game's way and the new (mana kept full, so no slow casts), its new casts, echoes, great shots, pulses and circles
    /// ("dps"); its right-click art used: mana paid, projectiles made (by kind), the dummy struck ("arts"). TK_MAGE_ONLY:
    /// item names, and any of dps/arts. Shots Town_mage_&lt;weapon&gt;(_art). Lines "[AutoTest] 法师 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MageArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 法师 {what} {(pass ? "通过" : "不通过")}");
            }
            var only = (System.Environment.GetEnvironmentVariable("TK_MAGE_ONLY") ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            bool Part(string name) => !only.Any(o => o == "dps" || o == "arts" || o == "effects") || only.Contains(name);
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
            var savedSlots = inv.Slots.ToArray();
            int savedSel = inv.Selected;
            var tp = TerraProjectiles.Instance;
            var st = mod.Stats;
            int savedCrystals = st.ManaCrystals;
            st.ManaCrystals = Player.TerraStats.MaxManaCrystals;
            st.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }

            var ground = new Dictionary<Vector2Int, CellKind>();
            var groundFrom = CellGrid.WorldToCell(at.Value + new Vector2(-7f, -1.5f));
            var groundTo = CellGrid.WorldToCell(at.Value + new Vector2(3f, 7f));
            for (int x = groundFrom.x; x <= groundTo.x; x++)
                for (int y = groundFrom.y; y <= groundTo.y; y++) ground[new Vector2Int(x, y)] = TerraWorld.Get(x, y);
            IEnumerator Restore()
            {
                tp.Clear();
                yield return null;
                foreach (var kv in ground)
                    if (TerraWorld.Get(kv.Key.x, kv.Key.y) != kv.Value) TerraWorld.Set(kv.Key.x, kv.Key.y, kv.Value);
            }

            var spells = new HashSet<ItemType> { ItemType.VengefulSpiritFocus, ItemType.HowlingWraithsFocus, ItemType.ShadeSoulFocus, ItemType.AbyssStaff };
            var weapons = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && d.Class == DamageClass.Magic && !spells.Contains(d.Type))
                .OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage).ToList();
            if (only.Any(o => o != "dps" && o != "arts" && o != "effects")) weapons = weapons.Where(d => only.Contains(d.Type.ToString())).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 法师 武器 {weapons.Count} 件");

            void Arm(ItemDef def)
            {
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[0] = ItemStack.Of(def.Type);
                inv.Selected = 0;
            }

            IEnumerator Fire(float seconds, System.Action each)
            {
                mod.DevAim = aim;
                mod.DevHold = true;
                for (float end = Time.time + seconds; Time.time < end;)
                {
                    st.Mana = st.ManaMax;
                    if (mod.DevReady && !mod.IsUsingItem && !BladeSkills.HoldsLeft) mod.DevUseSelected(aim);
                    each?.Invoke();
                    yield return null;
                }
                mod.DevHold = null;
            }

            var dummyAim = aim;
            foreach (var def in weapons)
            {
                var type = def.Type;
                var spec = MageLefts.Of(def);
                // The rods' clouds rain from above: aimed a way over the dummy.
                aim = type == ItemType.CrimsonRod || type == ItemType.NimbusRod ? dummyAim + new Vector2(0f, 4f) : dummyAim;
                if (Part("dps"))
                {
                    float[] dps = new float[2];
                    int casts0 = MageFire.DevCasts, echo0 = MageFire.DevEchoes, great0 = MageFire.DevGreat, pulse0 = MageFire.DevPulses, extra0 = MageFire.DevExtras;
                    int circle0 = SwordFx.DevCircles, launch0 = TerraProjectiles.DevMageLaunched;
                    float window = Mathf.Clamp(def.UseAnimation * 22f, 8f, 14f);
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        yield return Restore();
                        Arm(def);
                        Stand(5f);
                        yield return new WaitForSeconds(0.8f);
                        int dmg0 = TargetDummy.DevDamage;
                        float start = Time.time;
                        bool shotDone = mode == 0;
                        yield return Fire(window, () =>
                        {
                            if (!shotDone && Time.time - start > 1.2f) { Shot("Town", $"mage_{type}"); shotDone = true; }
                        });
                        yield return new WaitForSeconds(1f);
                        dps[mode] = (TargetDummy.DevDamage - dmg0) / window;
                    }
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    string what = $"{def.Name}（{spec?.Left.ToString() ?? "无"}）：原版 {dps[0]:0} → 新版 {dps[1]:0}（×{ratio:0.00}），新施法 {MageFire.DevCasts - casts0}，回响 {MageFire.DevEchoes - echo0}，" +
                        $"强化 {MageFire.DevGreat - great0}，迸发 {MageFire.DevPulses - pulse0}，追加 {MageFire.DevExtras - extra0}，绕身放出 {TerraProjectiles.DevMageLaunched - launch0}，法阵 {SwordFx.DevCircles - circle0}";
                    Check(spec == null || dps[1] > 0f && (dps[0] <= 0f || ratio > 0.8f && ratio < 1.5f), "伤害 " + what);
                }
                // "effects": what its left says it does, seen happening (the user's 10-28 "有很多法师武器都没有生效…魔晶碎块左键无法着火").
                if (Part("effects") && spec != null)
                {
                    BladeArts.Set(true);
                    yield return Restore();
                    Arm(def);
                    Stand(5f);
                    yield return new WaitForSeconds(0.8f);
                    var want = new TerraProjectiles.RangedShot();
                    spec.Sign?.Invoke(want);
                    int deb0 = TerraProjectiles.DevRangedDebuffs, burst0 = TerraProjectiles.DevRangedBursts, shard0 = TerraProjectiles.DevRangedShards, arc0 = TerraProjectiles.DevRangedArcs;
                    int spawn0 = TerraProjectiles.DevRangedSpawned, bounce0 = TerraProjectiles.DevRangedBounces, home0 = TerraProjectiles.DevRangedHomed;
                    int weave0 = TerraProjectiles.DevWeaveTicks, point0 = TerraProjectiles.DevPointReached, launch0 = TerraProjectiles.DevMageLaunched;
                    int echo0 = MageFire.DevEchoes, great0 = MageFire.DevGreat, extra0 = MageFire.DevExtras, nova0 = MageFire.DevNovas, pulse0 = MageFire.DevPulses, casts0 = MageFire.DevCasts;
                    var keepAim = aim;
                    // Gathering on the cursor: aimed off the dummy, so the motes have to turn to get there.
                    if (spec.Left == MageLefts.Left.Gather) aim = dummyAim + new Vector2(-3f, 3f);
                    // The user's 10-28 reports: "磁球说是最多三颗，结果第二颗后第一颗会消失" (three out at once);
                    // "充能爆破炮…激光扫射的时候怎么一卡一卡的" (its beam after a moving cursor turns a little each update, not in
                    // jumps); "雨云法杖里面也没有雷电" (the clouds' rain marks what it strikes: wet, then lightning; blood).
                    bool blaster = type == ItemType.ChargedBlasterCannon, cloud = type == ItemType.NimbusRod || type == ItemType.CrimsonRod;
                    int spheres = 0, allSpheres = 0, turns = 0, lastTicks = -1, marks0 = MageMech.DevMarks, arcs0 = SwordFx.DevArcs;
                    float sweepAt = -1f, worstTurn = 0f, farthest = 0f;
                    Vector2 lastBeam = Vector2.zero;
                    yield return Fire(blaster ? 6.5f : cloud ? 7f : 4f, () =>
                    {
                        if (type == ItemType.MagnetSphere)
                        {
                            // Zapping ones (the oldest fade out over their last 30 updates: two at once at its pace of a cast in 20).
                            spheres = Mathf.Max(spheres, tp.DevCount(ProjKind.MagnetSphere, 30));
                            allSpheres = Mathf.Max(allSpheres, tp.DevCount(ProjKind.MagnetSphere));
                        }
                        // "恶魔之镰初始速度太慢了但依然被判定走了很多格，导致根本没飞出去就已经回到我手里了": out to the dummy 10 blocks off.
                        if (type == ItemType.DemonScythe && tp.DevNewest(ProjKind.DemonScythe) is Vector2 scythe)
                            farthest = Mathf.Max(farthest, Vector2.Distance(scythe, hero.GetComponent<BoxCollider2D>().bounds.center) / MageMech.Block);
                        var beam = blaster ? tp.DevNewestMotion(ProjKind.ChargedBlasterBeam) : null;
                        if (beam == null) return;
                        if (sweepAt < 0f) sweepAt = Time.time;
                        aim = dummyAim + new Vector2(0f, 3f * Mathf.Sin((Time.time - sweepAt) * Mathf.PI));
                        mod.DevAim = aim;
                        int ticks = beam.Value.Ticks - lastTicks;
                        if (lastTicks >= 0 && ticks > 0)
                        {
                            float turn = Vector2.Angle(lastBeam, beam.Value.Vel) / ticks;
                            worstTurn = Mathf.Max(worstTurn, turn);
                            if (turn > 0.01f) turns++;
                        }
                        if (ticks > 0) { lastBeam = beam.Value.Vel; lastTicks = beam.Value.Ticks; }
                    });
                    yield return new WaitForSeconds(1f);
                    aim = keepAim;
                    if (type == ItemType.MagnetSphere) Check(spheres == 3 && allSpheres <= 5, $"效果 磁球：同时放电的最多 {spheres} 颗（应为 3），连同淡出的最多 {allSpheres} 颗");
                    if (type == ItemType.DemonScythe) Check(farthest >= 8.5f, $"效果 恶魔之镰：飞出最远 {farthest:0.0} 格（假人在 10 格外）");
                    if (blaster) Check(turns >= 30 && worstTurn < 4f, $"效果 充能爆破炮：激光跟着上下移动的鼠标转了 {turns} 次，每次更新最多转 {worstTurn:0.0}°（应小于 4°）");
                    if (cloud) Check(MageMech.DevMarks > marks0 && (type != ItemType.NimbusRod || SwordFx.DevArcs > arcs0),
                        $"效果 {def.Name}：雨打上的印记 {MageMech.DevMarks - marks0}，落雷 {SwordFx.DevArcs - arcs0}");
                    var got = new List<string>();
                    var missing = new List<string>();
                    void Need(bool wanted, int n, string name)
                    {
                        if (!wanted) return;
                        got.Add($"{name} {n}");
                        if (n <= 0) missing.Add(name);
                    }
                    Need(true, MageFire.DevCasts - casts0, "新施法");
                    Need(want.Debuff != 0, TerraProjectiles.DevRangedDebuffs - deb0, "减益");
                    Need(want.Burst > 0f, TerraProjectiles.DevRangedBursts - burst0, "炸开");
                    Need(want.Shards > 0, TerraProjectiles.DevRangedShards - shard0, "碎片");
                    Need(want.Chain > 0, TerraProjectiles.DevRangedArcs - arc0, "电弧");
                    Need(want.Bounces > 0, TerraProjectiles.DevRangedBounces - bounce0, "弹向下一个");
                    Need(want.Home > 0f, TerraProjectiles.DevRangedHomed - home0, "追踪");
                    Need(want.OnHit != null || want.OnDeath != null, TerraProjectiles.DevRangedSpawned - spawn0, "附带");
                    Need(want.Weave > 0f, TerraProjectiles.DevWeaveTicks - weave0, "蛇行");
                    Need(spec.Left == MageLefts.Left.Gather, TerraProjectiles.DevPointReached - point0, "汇聚到鼠标");
                    Need(spec.Left == MageLefts.Left.Echo, MageFire.DevEchoes - echo0, "回响");
                    Need(spec.Left == MageLefts.Left.Resonance, MageFire.DevGreat - great0, "强化");
                    Need(spec.Left == MageLefts.Left.Extra || spec.Left == MageLefts.Left.Rain, MageFire.DevExtras - extra0, "追加");
                    Need(spec.Left == MageLefts.Left.Nova, MageFire.DevNovas - nova0, "新星");
                    Need(spec.Left == MageLefts.Left.Pulse, MageFire.DevPulses - pulse0, "迸发");
                    Need(spec.Left == MageLefts.Left.Orbit || spec.Left == MageLefts.Left.Hover, TerraProjectiles.DevMageLaunched - launch0, "绕身/悬停放出");
                    Check(missing.Count == 0, $"效果 {def.Name}（{spec.Left}{(want.Weave > 0f ? $"，蛇行 {want.Weave:0} px" : "")}）：{string.Join("，", got)}{(missing.Count > 0 ? "；没发生：" + string.Join("、", missing) : "")}");
                }
                if (Part("arts"))
                {
                    var art = BladeSkills.KindArtOf(def);
                    if (art == null) { Plugin.Log.LogInfo($"[AutoTest] 法师 右键 {def.Name}：无"); continue; }
                    BladeArts.Set(true);
                    yield return Restore();
                    Arm(def);
                    Stand(5f);
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(1.2f);
                    st.Mana = st.ManaMax;
                    int uses0 = BladeSkills.DevUses, dmg0 = TargetDummy.DevDamage, shots0 = BladeSkills.DevMageArtShots, arts0 = BladeSkills.DevMageArts;
                    var made0 = new Dictionary<ProjKind, int>(TerraProjectiles.DevSpawned);
                    int manaBefore = st.Mana;
                    if (art.Charged)
                    {
                        BladeSkills.DevHeld = true;
                        BladeSkills.DevPress = true;
                        yield return new WaitForSeconds(art.Full * 0.7f);
                        Shot("Town", $"mage_{type}_charge");
                        yield return new WaitForSeconds(art.Full * 0.3f + 0.25f);
                        BladeSkills.DevHeld = false;
                    }
                    else BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(0.35f);
                    int paid = manaBefore - st.Mana;
                    Shot("Town", $"mage_{type}_art");
                    yield return new WaitForSeconds(2.6f);
                    BladeSkills.DevHeld = null;
                    mod.DevAim = null;
                    int artDmg = TargetDummy.DevDamage - dmg0, shots = BladeSkills.DevMageArtShots - shots0;
                    bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLastArt == art.Name;
                    string made = string.Join(" ", TerraProjectiles.DevSpawned.Keys.Where(k => TerraProjectiles.DevMade(k) > (made0.TryGetValue(k, out int n) ? n : 0))
                        .OrderByDescending(k => TerraProjectiles.DevMade(k)).Take(4).Select(k => $"{k}×{TerraProjectiles.DevMade(k) - (made0.TryGetValue(k, out int n) ? n : 0)}"));
                    Check(went && shots > 0 && artDmg > 0 && paid > 0, $"右键 {def.Name}：{art.Name}，用了 {(went ? "是" : "否")}，耗魔 {paid}，射出 {shots}，假人受到 {artDmg}，做出 {made}");
                    yield return new WaitForSeconds(0.3f);
                }
            }
            yield return Restore();
            BladeArts.Set(true);
            for (int i = 0; i < inv.Slots.Length && i < savedSlots.Length; i++) inv.Slots[i] = savedSlots[i];
            inv.Selected = savedSel;
            st.ManaCrystals = savedCrystals;
            st.UpdateMaximums(PlayerData.instance, Equipment.Current.ManaMax);
            Plugin.Log.LogInfo($"[AutoTest] 法师 合计 {ok}/{all}");
        }
    }
}
