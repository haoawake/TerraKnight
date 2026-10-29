using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "flailarts", Items.TerraProjectilesFlailArts / ChainArts, the user's 10-26 "继续升级剩下的这些…特效，动画，粒子效果，
    /// 新颖，多样"): in Dirtmouth by its Target Dummy. Every flail used three times running (held 0.9 s, let go): its styles come
    /// in its order, the dummy is struck, the spin reaches at least 25% further than the game's. Every chained weapon used
    /// three times: its paths in order, the dummy struck, the head's reach. Then damage a second over 6 s (flails) or 4 s
    /// (chains), remade against the game's (the option off): within 0.85 to 1.4. Then each right-click art (held 1.1 s and
    /// let go, or pressed): used, the dummy hurt. Shots Town_flailarts_&lt;item&gt;_&lt;use&gt;[_spin|_throw], _art_&lt;item&gt;[_late].
    /// TK_FLAIL_ONLY narrows it (item names, "flails", "chains", "dps", "arts"). Lines "[AutoTest] 链锤 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FlailArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 链锤 {what} {(pass ? "通过" : "不通过")}");
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
            var only = new List<string>((System.Environment.GetEnvironmentVariable("TK_FLAIL_ONLY") ?? "").Split(','));
            bool Want(string what) => only.Count == 0 || only[0].Length == 0 || only.Contains(what);
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
            void Hold(ItemType type)
            {
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
            }
            bool Out(ItemType type)
            {
                var d = ItemDb.Get(type);
                return d != null && d.Shoot != ProjKind.None && tp.Holds(d.Shoot);
            }

            // One use of a flail: held (spun) a while, let go, home again.
            IEnumerator FlailUse(ItemType type, float spin, string shot)
            {
                while (mod.IsUsingItem || !mod.DevReady || Out(type)) yield return null;
                mod.DevAim = aim;
                mod.DevHold = true;
                mod.DevUseSelected(aim);
                for (float t = 0f; t < spin; t += Time.deltaTime)
                {
                    if (shot != null && t < spin * 0.6f && t + Time.deltaTime >= spin * 0.6f) Shot("Town", shot + "_spin");
                    yield return null;
                }
                mod.DevHold = false;
                float let = Time.time;
                bool shotThrow = false;
                while (Time.time - let < 3.5f && (Out(type) || mod.IsUsingItem))
                {
                    if (shot != null && !shotThrow && Time.time - let > 0.22f) { Shot("Town", shot + "_throw"); shotThrow = true; }
                    yield return null;
                }
                mod.DevHold = null;
            }

            var flails = new[]
            {
                ItemType.Mace, ItemType.FlamingMace, ItemType.BallOHurt, ItemType.TheMeatball, ItemType.BlueMoon, ItemType.Sunfury,
                ItemType.DaoofPow, ItemType.DripplerFlail, ItemType.FlowerPow, ItemType.FlaironFlail,
            };
            if (Want("flails") || only.Exists(o => System.Array.Exists(flails, f => f.ToString() == o)))
                foreach (var type in flails)
                {
                    if (!Want("flails") && !only.Contains(type.ToString())) continue;
                    var def = ItemDb.Get(type);
                    Hold(type);
                    Stand(2f);
                    yield return new WaitForSeconds(0.8f);
                    var styles = new List<string>();
                    int struck = 0;
                    TerraProjectiles.DevFlailSpinFar = TerraProjectiles.DevFlailThrowFar = TerraProjectiles.DevFlailVanillaSpin = TerraProjectiles.DevFlailVanillaThrow = 0f;
                    int shocks0 = TerraProjectiles.DevFlailShocks, skips0 = TerraProjectiles.DevFlailSkips;
                    for (int use = 1; use <= 3; use++)
                    {
                        int hits0 = TargetDummy.DevHits;
                        TerraProjectiles.DevFlailStyle = "";
                        yield return FlailUse(type, 0.9f, $"flailarts_{type}_{use}");
                        if (TargetDummy.DevHits > hits0) struck++;
                        styles.Add(TerraProjectiles.DevFlailStyle);
                    }
                    float spin = TerraProjectiles.DevFlailSpinFar, vSpin = TerraProjectiles.DevFlailVanillaSpin;
                    bool named = styles.TrueForAll(s => s.Length > 0) && styles[0] != styles[1];
                    Check(named && struck >= 2 && vSpin > 0f && spin >= vSpin * 1.25f,
                        $"{def.Name}：打中 {struck}/3，招式 {string.Join(" → ", styles)}，转动最远 {spin:F0} px（原版 {vSpin:F0}，×{(vSpin > 0f ? spin / vSpin : 0f):F2}），甩出最远 {TerraProjectiles.DevFlailThrowFar:F0} px（原版直抛 {TerraProjectiles.DevFlailVanillaThrow:F0}），震地 {TerraProjectiles.DevFlailShocks - shocks0}，弹跳 {TerraProjectiles.DevFlailSkips - skips0}");
                    yield return new WaitForSeconds(0.6f);
                }

            var chains = new[] { ItemType.GolemFist, ItemType.KOCannon, ItemType.ChainKnife, ItemType.ChainGuillotines, ItemType.Anchor, ItemType.SolarEruption };
            if (Want("chains") || only.Exists(o => System.Array.Exists(chains, f => f.ToString() == o)))
                foreach (var type in chains)
                {
                    if (!Want("chains") && !only.Contains(type.ToString())) continue;
                    var def = ItemDb.Get(type);
                    Hold(type);
                    Stand(3f);
                    yield return new WaitForSeconds(0.8f);
                    var styles = new List<string>();
                    int struck = 0;
                    TerraProjectiles.DevChainFar = 0f;
                    for (int use = 1; use <= 3; use++)
                    {
                        while (mod.IsUsingItem || !mod.DevReady || Out(type)) yield return null;
                        int hits0 = TargetDummy.DevHits;
                        TerraProjectiles.DevChainStyle = "";
                        mod.DevAim = aim;
                        mod.DevHold = def.Channel;
                        mod.DevUseSelected(aim);
                        yield return new WaitForSeconds(0.2f);
                        Shot("Town", $"flailarts_{type}_{use}");
                        mod.DevHold = null;
                        for (float t = Time.time + 3f; Time.time < t && (Out(type) || mod.IsUsingItem);) yield return null;
                        yield return new WaitForSeconds(0.1f);
                        if (TargetDummy.DevHits > hits0) struck++;
                        styles.Add(TerraProjectiles.DevChainStyle);
                    }
                    float far = TerraProjectiles.DevChainFar, vanilla = TerraProjectiles.DevChainVanilla;
                    bool named = styles.TrueForAll(s => s.Length > 0);
                    Check(named && struck >= 2, $"{def.Name}：打中 {struck}/3，招式 {string.Join(" → ", styles)}，最远 {far:F0} px（原版能到 {vanilla:F0}）");
                    yield return new WaitForSeconds(0.6f);
                }

            // One flail's styles, each alone (TK_FLAIL_ONLY "styles,<item>"): its damage a second.
            if (only.Contains("styles"))
                foreach (var type in flails)
                {
                    if (!only.Contains(type.ToString())) continue;
                    var def = ItemDb.Get(type);
                    for (int style = -1; style < 4; style++)
                    {
                        BladeArts.Set(style >= 0);
                        TerraProjectiles.DevFlailForce = style;
                        Hold(type);
                        Stand(2f);
                        yield return new WaitForSeconds(1f);
                        int dmg0 = TargetDummy.DevDamage, hits0 = TargetDummy.DevHits;
                        float start = Time.time;
                        int uses = 0;
                        while (Time.time - start < 6f) { yield return FlailUse(type, 0.5f, null); uses++; }
                        Plugin.Log.LogInfo($"[AutoTest] 链锤 招式单测 {def.Name} {(style < 0 ? "原版" : TerraProjectiles.DevFlailStyle)}：每秒 {(TargetDummy.DevDamage - dmg0) / (Time.time - start):F0}，{uses} 次，命中 {TargetDummy.DevHits - hits0}");
                    }
                    TerraProjectiles.DevFlailForce = -1;
                    BladeArts.Set(true);
                }

            // Damage a second on the dummy, remade against the game's; the dummy's burning and Daybroken from what came before
            // let run out first (5 s), and again before each measure.
            if (Want("dps"))
            {
                yield return new WaitForSeconds(5f);
                var dpsFlails = new[] { ItemType.Mace, ItemType.BallOHurt, ItemType.BlueMoon, ItemType.Sunfury, ItemType.DaoofPow, ItemType.DripplerFlail, ItemType.FlowerPow, ItemType.FlaironFlail };
                // Narrowed to some weapons ("dps,<item>,..."): only those.
                bool named = only.Exists(o => System.Array.Exists(dpsFlails, f => f.ToString() == o) || System.Array.Exists(chains, f => f.ToString() == o));
                foreach (var type in dpsFlails)
                {
                    if (named && !only.Contains(type.ToString())) continue;
                    var def = ItemDb.Get(type);
                    float[] dps = new float[2];
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        Hold(type);
                        Stand(2f);
                        yield return new WaitForSeconds(5f);
                        int dmg0 = TargetDummy.DevDamage;
                        float start = Time.time;
                        while (Time.time - start < 6f) yield return FlailUse(type, 0.5f, null);
                        dps[mode] = (TargetDummy.DevDamage - dmg0) / (Time.time - start);
                        yield return new WaitForSeconds(1f);
                    }
                    BladeArts.Set(true);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    Check(ratio >= 0.85f && ratio <= 1.4f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新招式 {dps[1]:F0}（×{ratio:F2}）");
                }
                foreach (var type in chains)
                {
                    if (named && !only.Contains(type.ToString())) continue;
                    var def = ItemDb.Get(type);
                    float[] dps = new float[2];
                    for (int mode = 0; mode < 2; mode++)
                    {
                        BladeArts.Set(mode == 1);
                        Hold(type);
                        Stand(3f);
                        yield return new WaitForSeconds(5f);
                        int dmg0 = TargetDummy.DevDamage;
                        float start = Time.time;
                        mod.DevAim = aim;
                        if (def.Channel) mod.DevHold = true;
                        while (Time.time - start < 4f)
                        {
                            if (!mod.IsUsingItem && mod.DevReady && !Out(type)) mod.DevUseSelected(aim);
                            yield return null;
                        }
                        mod.DevHold = null;
                        for (float t = Time.time + 3f; Time.time < t && (Out(type) || mod.IsUsingItem);) yield return null;
                        dps[mode] = (TargetDummy.DevDamage - dmg0) / (Time.time - start);
                        yield return new WaitForSeconds(1f);
                    }
                    BladeArts.Set(true);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    Check(ratio >= 0.85f && ratio <= 1.4f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新招式 {dps[1]:F0}（×{ratio:F2}）");
                }
            }

            // The right-click arts: held 1.1 s and let go (the charged ones) or pressed, in hand by the dummy.
            if (Want("arts"))
            {
                var arts = new (ItemType Item, string Name, bool Charged)[]
                {
                    (ItemType.Mace, "重锤砸地", true), (ItemType.FlamingMace, "烈焰砸地", true), (ItemType.BallOHurt, "腐化风暴", false),
                    (ItemType.TheMeatball, "血肉重砸", true), (ItemType.BlueMoon, "月轮", false), (ItemType.Sunfury, "陨日坠击", true),
                    (ItemType.DaoofPow, "太极螺旋", false), (ItemType.DripplerFlail, "血泉三连", false), (ItemType.FlowerPow, "花瓣风暴", false),
                    (ItemType.FlaironFlail, "泡泡漩涡", false), (ItemType.Flairon, "泡泡漩涡", false),
                    (ItemType.GolemFist, "火箭重拳", true), (ItemType.KOCannon, "致胜重拳", true), (ItemType.ChainKnife, "链刃旋风", false),
                    (ItemType.ChainGuillotines, "血滴子收割", false), (ItemType.Anchor, "落锚", true), (ItemType.SolarEruption, "日轮", false),
                };
                foreach (var (item, name, charged) in arts)
                {
                    if (!Want("arts") && !only.Contains(item.ToString())) continue;
                    var def = ItemDb.Get(item);
                    Hold(item);
                    Stand(2.6f);
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(1f);
                    int uses0 = BladeSkills.DevUses, dmg0 = TargetDummy.DevDamage;
                    BladeSkills.DevLastArt = "";
                    BladeSkills.DevHeld = true;
                    BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(charged ? 1.1f : 0.1f);
                    BladeSkills.DevHeld = false;
                    yield return new WaitForSeconds(0.45f);
                    Shot("Town", $"flailarts_art_{item}");
                    yield return new WaitForSeconds(0.45f);
                    Shot("Town", $"flailarts_art_{item}_late");
                    yield return new WaitForSeconds(1.2f);
                    BladeSkills.DevHeld = null;
                    mod.DevAim = null;
                    int dmg = TargetDummy.DevDamage - dmg0;
                    bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLastArt == name;
                    Check(went && dmg > 0, $"右键 {def.Name}：{name}，用了 {(went ? "是" : "否")}（{BladeSkills.DevLastArt}），假人受到 {dmg}");
                    yield return new WaitForSeconds(2.5f);
                }
            }
            mod.DevAim = null;
            mod.DevHold = null;
            inv.Slots[0] = saved;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 链锤 合计 {ok}/{all}");
        }
    }
}
