using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "rangedarts", Items.RangedArts / RangedFire / BladeSkillsRanged, the user's 10-27 "现在升级所有射手武器…创意+
    /// 正常强度"): in Dirtmouth by its Target Dummy, each ranged weapon (the four Hallownest spell guns aside) with its cheapest
    /// ammo (endless items), the ground round the dummy put back before each: its damage a second on the dummy over 8 to 16 s
    /// with the left button, the game's way against the new
    /// (0.85 to 1.4), the new way's shots, power shots, burst shots and muzzle looks counted ("dps"); its right-click art at
    /// the dummy, used and striking it (an overdrive with 2 s of fire after it) ("arts"). TK_RANGED_ONLY: item names, and
    /// any of dps/arts. Shots Town_ranged_&lt;weapon&gt;(_art). Lines "[AutoTest] 射手 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RangedArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 射手 {what} {(pass ? "通过" : "不通过")}");
            }
            var only = (System.Environment.GetEnvironmentVariable("TK_RANGED_ONLY") ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            // The arts whose words name what they fire (the user's 10-27 "幽灵凤凰…火雨…蜂膝弓…蜜蜂…地狱之翼…蝙蝠…血雨弓…血雨").
            var says = new System.Collections.Generic.Dictionary<ItemType, ProjKind[]>
            {
                [ItemType.PhantomPhoenix] = new[] { ProjKind.FlamingArrow, ProjKind.Phoenix },
                [ItemType.BeesKnees] = new[] { ProjKind.BeeArrow, ProjKind.Bee },
                [ItemType.HellwingBow] = new[] { ProjKind.HellwingBat },
                [ItemType.BloodRainBow] = new[] { ProjKind.BloodRainArrow },
                [ItemType.AshWoodBow] = new[] { ProjKind.FlamingArrow },
                [ItemType.MoltenFury] = new[] { ProjKind.HellfireArrow },
                [ItemType.ElectrosphereLauncher] = new[] { ProjKind.Electrosphere },
                [ItemType.Stynger] = new[] { ProjKind.StyngerShrapnel },
            };
            bool Part(string name) => !only.Any(o => o == "dps" || o == "arts") || only.Contains(name);
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
            bool savedInfinite = inv.Infinite;
            inv.Infinite = true;
            var tp = TerraProjectiles.Instance;

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }

            // The ground round the dummy as it is (a Sandgun's sand, a bomb's crater put back before each weapon).
            var groundFrom = CellGrid.WorldToCell(at.Value + new Vector2(-7f, -1.5f));
            var groundTo = CellGrid.WorldToCell(at.Value + new Vector2(3f, 7f));
            var ground = new Dictionary<Vector2Int, CellKind>();
            for (int x = groundFrom.x; x <= groundTo.x; x++)
                for (int y = groundFrom.y; y <= groundTo.y; y++) ground[new Vector2Int(x, y)] = TerraWorld.Get(x, y);
            int restored = 0;
            IEnumerator Restore()
            {
                tp.Clear();
                for (float end = Time.time + 3f; Time.time < end && FallingSand.Instance != null && FallingSand.Instance.Falling > 0;) yield return null;
                foreach (var kv in ground)
                    if (TerraWorld.Get(kv.Key.x, kv.Key.y) != kv.Value) { TerraWorld.Set(kv.Key.x, kv.Key.y, kv.Value); restored++; }
            }

            var weapons = ItemDb.All.Where(d => d != null && ItemDb.Get(d.Type) == d && d.IsWeapon && d.Class == DamageClass.Ranged)
                .Where(d => d.Type != ItemType.RevenantPulseRifle && d.Type != ItemType.GhostPipeOrgan && d.Type != ItemType.ShadeRiftRifle && d.Type != ItemType.VoidSniper)
                .OrderBy(d => d.Stage).ThenBy(d => d.Rare).ThenBy(d => d.Damage).ToList();
            if (only.Any(o => o != "dps" && o != "arts")) weapons = weapons.Where(d => only.Contains(d.Type.ToString())).ToList();
            Plugin.Log.LogInfo($"[AutoTest] 射手 武器 {weapons.Count} 件");

            // The weapon in the first slot, its cheapest ammo in the second (the ammo slots emptied), aimed at the dummy.
            void Arm(ItemDef def)
            {
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                inv.Slots[0] = ItemStack.Of(def.Type, def.MaxStack > 1 ? def.MaxStack : 1);
                if (def.UseAmmo != AmmoKind.None)
                {
                    var ammo = ItemDb.All.Where(a => a != null && ItemDb.Get(a.Type) == a && a.Ammo == def.UseAmmo).OrderBy(a => a.Damage).ThenBy(a => (int)a.Type).FirstOrDefault();
                    if (ammo != null) inv.Slots[1] = ItemStack.Of(ammo.Type, ammo.MaxStack);
                }
                inv.Selected = 0;
            }

            // Fired at the dummy for a while: a click whenever a use may start, held for the held ones.
            IEnumerator Fire(ItemDef def, float seconds, System.Action each)
            {
                mod.DevAim = aim;
                mod.DevHold = true;
                for (float end = Time.time + seconds; Time.time < end;)
                {
                    if (mod.DevReady && !mod.IsUsingItem && !BladeSkills.HoldsLeft) mod.DevUseSelected(aim);
                    each?.Invoke();
                    yield return null;
                }
                mod.DevHold = null;
            }

            float Distance(ItemDef def)
            {
                var k = RangedArts.Of(def).Kind;
                return k == RangedArts.Kind.Shotgun || k == RangedArts.Kind.Sprayer ? 3f : k == RangedArts.Kind.Thrown || k == RangedArts.Kind.Lob ? 4f : 5f;
            }

            foreach (var def in weapons)
            {
                var type = def.Type;
                var look = RangedArts.Of(def);
                if (Part("dps"))
                {
                    float[] dps = new float[2];
                    int[] uses = new int[2];
                    int shots0 = RangedFire.DevShots, power0 = RangedFire.DevPower, burst0 = RangedFire.DevBurstShots, fx0 = SwordFx.DevRangedFx;
                    int bounce0 = TerraProjectiles.DevRangedBounces, shard0 = TerraProjectiles.DevRangedShards, blast0 = TerraProjectiles.DevRangedBursts;
                    // Long enough for about 20 uses (8 to 16 s): a slow gun's few crits otherwise swing it.
                    float window = Mathf.Clamp(def.UseAnimation * 22f, 8f, 16f);
                    // The bows whose arrows fall from the sky at random spots round the cursor: a much longer look.
                    if (type == ItemType.BloodRainBow || type == ItemType.DaedalusStormbow) window = 20f;
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        yield return Restore();
                        Arm(def);
                        Stand(Distance(def));
                        yield return new WaitForSeconds(0.8f);
                        int dmg0 = TargetDummy.DevDamage, use0 = TerraMod.DevUseCount;
                        float start = Time.time;
                        bool shotDone = mode == 0;
                        yield return Fire(def, window, () =>
                        {
                            if (!shotDone && Time.time - start > 1.2f) { Shot("Town", $"ranged_{type}"); shotDone = true; }
                        });
                        dps[mode] = (TargetDummy.DevDamage - dmg0) / Mathf.Max(1f, Time.time - start);
                        uses[mode] = TerraMod.DevUseCount - use0;
                        for (float end = Time.time + 1f; Time.time < end && mod.IsUsingItem;) yield return null;
                    }
                    BladeArts.Set(true);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    bool held = look.Kind == RangedArts.Kind.Held;
                    bool pass = dps[0] <= 0f ? dps[1] > 0f : ratio >= 0.85f && ratio <= 1.4f;
                    Check(pass && SwordFx.DevRangedFx > fx0, $"{def.Name}（{look.Kind}/{look.Left}{(look.N > 0 ? look.N.ToString() : "")}）每秒伤害：原版 {dps[0]:F0}，新版 {dps[1]:F0}（×{ratio:F2}），使用 {uses[0]}/{uses[1]}，"
                        + $"新射法 {RangedFire.DevShots - shots0}，强化 {RangedFire.DevPower - power0}，连发 {RangedFire.DevBurstShots - burst0}，枪口 {SwordFx.DevRangedFx - fx0}，"
                        + $"弹射 {TerraProjectiles.DevRangedBounces - bounce0}，分裂 {TerraProjectiles.DevRangedShards - shard0}，爆开 {TerraProjectiles.DevRangedBursts - blast0}{(held ? "（引导）" : "")}");
                }

                if (Part("arts"))
                {
                    var art = BladeSkills.KindArtOf(def);
                    if (art == null)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 射手 右键 {def.Name}：无");
                        continue;
                    }
                    BladeArts.Set(true);
                    yield return Restore();
                    Arm(def);
                    Stand(Distance(def));
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(1.2f);
                    int uses0 = BladeSkills.DevUses, dmg0 = TargetDummy.DevDamage, shots0 = BladeSkills.DevRangedArtShots, od0 = BladeSkills.DevOverdrives;
                    var made0 = new System.Collections.Generic.Dictionary<ProjKind, int>(TerraProjectiles.DevSpawned);
                    int Was(ProjKind k) => made0.TryGetValue(k, out int n) ? n : 0;
                    // What the art made, the most first; and whether it made what its words say.
                    string Made()
                    {
                        var kinds = TerraProjectiles.DevSpawned.Keys.Where(k => TerraProjectiles.DevMade(k) > Was(k))
                            .OrderByDescending(k => TerraProjectiles.DevMade(k) - Was(k)).Take(5).Select(k => $"{k}×{TerraProjectiles.DevMade(k) - Was(k)}").ToList();
                        return string.Join(" ", kinds);
                    }
                    bool Says()
                    {
                        if (!says.TryGetValue(type, out var want)) return true;
                        return want.All(k => TerraProjectiles.DevMade(k) > Was(k));
                    }
                    // A gun: its right strengthens the left (an aim held while the left fires, rounds loaded, a stance).
                    var gun = BladeSkills.GunArtOf(def);
                    if (gun != null)
                    {
                        int gun0 = BladeSkills.DevGunShots;
                        switch (gun.Mode)
                        {
                            case BladeSkills.GunMode.Aim:
                                BladeSkills.DevHeld = true;
                                yield return new WaitForSeconds(gun.Full + 0.2f);
                                Shot("Town", $"ranged_{type}_aim");
                                for (float end = Time.time + 1.5f; Time.time < end && BladeSkills.DevGunShots == gun0;)
                                {
                                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                                    yield return null;
                                }
                                yield return new WaitForSeconds(0.25f);
                                Shot("Town", $"ranged_{type}_art");
                                BladeSkills.DevHeld = false;
                                break;
                            case BladeSkills.GunMode.Hold:
                                BladeSkills.DevHeld = true;
                                yield return new WaitForSeconds(0.3f);
                                yield return Fire(def, 1.6f, null);
                                Shot("Town", $"ranged_{type}_art");
                                BladeSkills.DevHeld = false;
                                break;
                            default:
                                BladeSkills.DevPress = true;
                                yield return new WaitForSeconds(0.35f);
                                Shot("Town", $"ranged_{type}_load");
                                yield return Fire(def, gun.Mode == BladeSkills.GunMode.Load ? Mathf.Max(1.2f, gun.Rounds * def.UseAnimation * 1.3f + 0.4f) : 2.2f, null);
                                Shot("Town", $"ranged_{type}_art");
                                break;
                        }
                        int spheres = TerraProjectiles.Instance != null ? TerraProjectiles.Instance.DevCount(ProjKind.Electrosphere) : 0;
                        yield return new WaitForSeconds(2f);
                        BladeSkills.DevHeld = null;
                        mod.DevAim = null;
                        int gunDmg = TargetDummy.DevDamage - dmg0, strong = BladeSkills.DevGunShots - gun0, pulses = BladeSkills.DevRangedArtShots - shots0;
                        Check((strong > 0 || pulses > 0) && gunDmg > 0 && Says() && (type != ItemType.ElectrosphereLauncher || spheres >= 3),
                            $"右键 {def.Name}：{gun.Name}（{gun.Mode}），加强的左键 {strong} 发，另射出 {pulses}，假人受到 {gunDmg}，做出 {Made()}" +
                            (type == ItemType.ElectrosphereLauncher ? $"，留下电球 {spheres} 颗" : "") +
                            (gun.Mode == BladeSkills.GunMode.Aim ? $"，稳住时 ×{BladeSkills.AimMost(def, gun):0.##}（用时 {def.UseAnimation:0.##} 秒，暴击 {def.Crit}）" : ""));
                        yield return new WaitForSeconds(0.3f);
                        continue;
                    }
                    if (art.Charged)
                    {
                        BladeSkills.DevHeld = true;
                        BladeSkills.DevPress = true;
                        yield return new WaitForSeconds(art.Full * 0.7f);
                        Shot("Town", $"ranged_{type}_charge");
                        yield return new WaitForSeconds(art.Full * 0.3f + 0.25f);
                        BladeSkills.DevHeld = false;
                    }
                    else BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(0.4f);
                    Shot("Town", $"ranged_{type}_art");
                    bool overdrive = BladeSkills.DevOverdrives > od0;
                    if (overdrive) yield return Fire(def, 2f, null);
                    yield return new WaitForSeconds(2.6f);
                    BladeSkills.DevHeld = null;
                    mod.DevAim = null;
                    int artDmg = TargetDummy.DevDamage - dmg0, shots = BladeSkills.DevRangedArtShots - shots0;
                    bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLastArt == art.Name;
                    Check(went && (shots > 0 || overdrive) && artDmg > 0 && Says(), $"右键 {def.Name}：{art.Name}，用了 {(went ? "是" : "否")}，射出 {shots}，过载 {(overdrive ? "是" : "否")}，假人受到 {artDmg}，做出 {Made()}");
                    yield return new WaitForSeconds(0.3f);
                }
            }
            yield return Restore();
            Plugin.Log.LogInfo($"[AutoTest] 射手 假人周围恢复的格子 {restored}");
            BladeArts.Set(true);
            for (int i = 0; i < inv.Slots.Length && i < savedSlots.Length; i++) inv.Slots[i] = savedSlots[i];
            inv.Selected = savedSel;
            inv.Infinite = savedInfinite;
            Plugin.Log.LogInfo($"[AutoTest] 射手 新射法 {RangedFire.DevShots}，强化 {RangedFire.DevPower}，连发 {RangedFire.DevBurstShots}，枪口 {SwordFx.DevRangedFx}，强化环 {SwordFx.DevRangedRings}，"
                + $"手中投掷物 {SwordFx.DevThrownDraws}，抛壳 {SwordFx.DevShells}，追踪转向 {TerraProjectiles.DevRangedHomed}");
            Plugin.Log.LogInfo($"[AutoTest] 射手 合计 {ok}/{all}");
        }
    }
}
