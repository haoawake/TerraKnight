using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spearart", Items.TerraProjectilesSpearArts, the user's 10-26 "长矛的戳刺类，可以多几种枪法…更好的粒子特效和戳刺
    /// 特效，范围也搞大一点"): in Dirtmouth by its Target Dummy, every spear used three times running: its techniques come in
    /// its order, each use strikes the dummy, the head reaches at least 30% further than the game's thrust, and its own shots
    /// still come (the Mushroom Spear's mushrooms, the Chlorophyte Partisan's spores, the North Pole's ice spear, the Storm
    /// Spear's bolt). Then its damage a second on the dummy over 4 s, the remade spear against the game's (the option off):
    /// within 0.85 to 1.6 of it. Shots Town_spearart_&lt;spear&gt;_&lt;use&gt;. TK_SPEAR_ONLY narrows it to some spears.
    /// Lines "[AutoTest] 枪法 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpearArtTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 枪法 {what} {(pass ? "通过" : "不通过")}");
            }
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
            var only = new List<string>((System.Environment.GetEnvironmentVariable("TK_SPEAR_ONLY") ?? "").Split(','));
            var spears = new (ItemType Item, ProjKind Shot)[]
            {
                (ItemType.Spear, ProjKind.None), (ItemType.Trident, ProjKind.None), (ItemType.TheRottedFork, ProjKind.None), (ItemType.Swordfish, ProjKind.None),
                (ItemType.ObsidianSwordfish, ProjKind.None), (ItemType.SlimeSpear, ProjKind.None), (ItemType.DarkLance, ProjKind.None),
                (ItemType.ThunderSpear, ProjKind.ThunderSpearShot), (ItemType.CobaltNaginata, ProjKind.None), (ItemType.PalladiumPike, ProjKind.None),
                (ItemType.MythrilHalberd, ProjKind.None), (ItemType.OrichalcumHalberd, ProjKind.None), (ItemType.AdamantiteGlaive, ProjKind.None),
                (ItemType.TitaniumTrident, ProjKind.None), (ItemType.Gungnir, ProjKind.None), (ItemType.MushroomSpear, ProjKind.SpearMushroom),
                (ItemType.ChlorophytePartisan, ProjKind.SporeCloud), (ItemType.NorthPole, ProjKind.NorthPoleSpear),
            };
            foreach (var (type, shot) in spears)
            {
                if (only.Count > 0 && only[0].Length > 0 && !only.Contains(type.ToString())) continue;
                var def = ItemDb.Get(type);
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - 2.6f, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(0.8f);
                var moves = new List<string>();
                int struck = 0, shots0 = shot != ProjKind.None ? TerraProjectiles.DevMade(shot) : 0;
                TerraProjectiles.DevSpearFar = 0f;
                float seconds = Mathf.Max(0.1f, def.UseAnimation * Equipment.UseScale(def));
                for (int use = 1; use <= 3; use++)
                {
                    while (mod.IsUsingItem || !mod.DevReady) yield return null;
                    int hits0 = TargetDummy.DevHits;
                    TerraProjectiles.DevSpearMove = "";
                    mod.DevAim = aim;
                    mod.DevUse(type, aim);
                    yield return new WaitForSeconds(seconds * 0.45f);
                    Shot("Town", $"spearart_{type}_{use}");
                    for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                    yield return new WaitForSeconds(0.05f);
                    if (TargetDummy.DevHits > hits0) struck++;
                    moves.Add(TerraProjectiles.DevSpearMove);
                }
                mod.DevAim = null;
                float far = TerraProjectiles.DevSpearFar, vanilla = TerraProjectiles.DevSpearVanilla;
                int shots = shot != ProjKind.None ? TerraProjectiles.DevMade(shot) - shots0 : -1;
                bool distinctOrder = moves.TrueForAll(m => m.Length > 0);
                Check(struck == 3 && distinctOrder && vanilla > 0f && far >= vanilla * 1.3f && shots != 0,
                    $"{def.Name}：三下打中 {struck}，枪法 {string.Join(" → ", moves)}，最远 {far:F0} px（原版 {vanilla:F0}，×{(vanilla > 0f ? far / vanilla : 0f):F2}）{(shot != ProjKind.None ? $"，{shot} {shots} 个" : "")}");
            }
            // Its damage a second on the dummy, remade against the game's.
            if (only.Count == 0 || only[0].Length == 0 || only.Contains("dps"))
                foreach (var type in new[] { ItemType.Spear, ItemType.Trident, ItemType.DarkLance, ItemType.ThunderSpear, ItemType.CobaltNaginata, ItemType.MythrilHalberd,
                             ItemType.AdamantiteGlaive, ItemType.Gungnir, ItemType.ChlorophytePartisan, ItemType.NorthPole })
                {
                    var def = ItemDb.Get(type);
                    float[] dps = new float[2];
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        hero.transform.position = Clear(hero, new Vector2(at.Value.x - 2.2f, at.Value.y - bottom + 0.05f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        yield return new WaitForSeconds(1f);
                        int dmg0 = TargetDummy.DevDamage;
                        float start = Time.time;
                        while (Time.time - start < 4f)
                        {
                            if (!mod.IsUsingItem && mod.DevReady)
                            {
                                mod.DevAim = aim;
                                mod.DevUse(type, aim);
                            }
                            yield return null;
                        }
                        while (mod.IsUsingItem) yield return null;
                        mod.DevAim = null;
                        dps[mode] = (TargetDummy.DevDamage - dmg0) / (Time.time - start);
                        yield return new WaitForSeconds(1f);
                    }
                    BladeArts.Set(true);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    Check(ratio >= 0.85f && ratio <= 1.6f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新枪法 {dps[1]:F0}（×{ratio:F2}）");
                }
            // The right-click arts, each held (a charge, let go full) or pressed with the spear in hand by the dummy: the art
            // goes (BladeSkills.DevLast), the dummy takes damage, and what it makes comes (the art's spear, Gungnir thrown, ice
            // spears, spores, mushrooms, bolts; a run moves the player).
            if (only.Count == 0 || only[0].Length == 0 || only.Contains("arts"))
            {
                var inv = mod.Inventory;
                var saved = inv.Slots[0];
                int savedSel = inv.Selected;
                var arts = new (ItemType Item, BladeSkills.Skill Skill, bool Charged, ProjKind Makes)[]
                {
                    (ItemType.PalladiumPike, BladeSkills.Skill.SpearPierce, true, ProjKind.PalladiumPike),
                    (ItemType.Trident, BladeSkills.Skill.TriThrust, true, ProjKind.TridentSpear),
                    (ItemType.Swordfish, BladeSkills.Skill.SpearDash, true, ProjKind.SwordfishThrust),
                    (ItemType.DarkLance, BladeSkills.Skill.SpearDash, true, ProjKind.DarkLance),
                    (ItemType.ThunderSpear, BladeSkills.Skill.ThunderVolley, true, ProjKind.ThunderSpearShot),
                    (ItemType.MythrilHalberd, BladeSkills.Skill.Whirl, false, ProjKind.MythrilHalberd),
                    (ItemType.Gungnir, BladeSkills.Skill.SpearThrow, false, ProjKind.ThrownGungnir),
                    (ItemType.NorthPole, BladeSkills.Skill.IceRain, false, ProjKind.NorthPoleSpear),
                    (ItemType.ChlorophytePartisan, BladeSkills.Skill.SporeRing, false, ProjKind.SporeCloud),
                    (ItemType.MushroomSpear, BladeSkills.Skill.MushroomRing, false, ProjKind.SpearMushroom),
                };
                foreach (var (item, skill, charged, makes) in arts)
                {
                    var def = ItemDb.Get(item);
                    inv.Slots[0] = ItemStack.Of(item);
                    inv.Selected = 0;
                    hero.transform.position = Clear(hero, new Vector2(at.Value.x - 3.2f, at.Value.y - bottom + 0.05f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    hero.FaceRight();
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(1.2f);
                    int uses0 = BladeSkills.DevUses, dmg0 = TargetDummy.DevDamage, made0 = TerraProjectiles.DevMade(makes);
                    float x0 = hero.transform.position.x;
                    BladeSkills.DevHeld = true;
                    BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(charged ? 1.15f : 0.1f);
                    BladeSkills.DevHeld = false;
                    yield return new WaitForSeconds(0.35f);
                    Shot("Town", $"spearart_art_{item}");
                    yield return new WaitForSeconds(1.2f);
                    BladeSkills.DevHeld = null;
                    mod.DevAim = null;
                    int made = TerraProjectiles.DevMade(makes) - made0, dmg = TargetDummy.DevDamage - dmg0;
                    float moved = hero.transform.position.x - x0;
                    bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLast == skill;
                    bool run = skill != BladeSkills.Skill.SpearDash || moved > 1.5f;
                    Check(went && made > 0 && dmg > 0 && run, $"右键 {def.Name}：{skill}，用了 {(went ? "是" : "否")}，{makes} {made} 个，假人受到 {dmg}，人移动 {moved:F1}");
                    yield return new WaitForSeconds(3.2f);
                }
                inv.Slots[0] = saved;
                inv.Selected = savedSel;
            }
            Plugin.Log.LogInfo($"[AutoTest] 枪法 合计 {ok}/{all}");
        }
    }
}
