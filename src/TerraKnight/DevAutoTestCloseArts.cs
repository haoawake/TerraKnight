using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "closearts", Items.SwordFxClose / TerraProjectilesClose / MeleeSpecials' umbrellas / BladeSkillsClose, the
    /// user's 10-26 "继续升级剩下的这些…特效，动画，粒子效果，新颖，多样"): in Dirtmouth by its Target Dummy. Each shortsword used four
    /// times running: its techniques come in its round, the dummy struck, the point out at least 25% further than the game's
    /// stab. Their damage a second against the game's (the option off), 0.85 to 1.4. The jousting lances held at the dummy
    /// (the stationary jab, the damage against the game's), run with along the town (the wind lines), and their charge (the
    /// player carried, the dummy struck). The umbrellas' three techniques and their damage, then every right-click art: it
    /// goes, the dummy takes damage. Shots Town_closearts_&lt;item&gt;_&lt;n&gt;. TK_CLOSE_ONLY: stab, dps, lance, umbrella, arts.
    /// Lines "[AutoTest] 近身 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CloseArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 近身 {what} {(pass ? "通过" : "不通过")}");
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
            var only = new List<string>((System.Environment.GetEnvironmentVariable("TK_CLOSE_ONLY") ?? "").Split(','));
            bool Wants(string part) => only.Count == 0 || only[0].Length == 0 || only.Contains(part);
            var inv = mod.Inventory;
            var saved = inv.Slots[0];
            int savedSel = inv.Selected;

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }

            IEnumerator Dps(ItemType type, float dx, float seconds, float[] dps, bool hold)
            {
                for (int mode = 0; mode < 2; mode++)
                {
                    BladeArts.Set(mode == 1);
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    Stand(dx);
                    yield return new WaitForSeconds(1f);
                    int dmg0 = TargetDummy.DevDamage;
                    float start = Time.time;
                    mod.DevAim = aim;
                    if (hold) mod.DevHold = true;
                    while (Time.time - start < seconds)
                    {
                        if (!mod.IsUsingItem && mod.DevReady) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    mod.DevHold = null;
                    for (float t = Time.time + 2f; mod.IsUsingItem && Time.time < t;) yield return null;
                    mod.DevAim = null;
                    dps[mode] = (TargetDummy.DevDamage - dmg0) / (Time.time - start);
                    yield return new WaitForSeconds(0.8f);
                }
                BladeArts.Set(true);
            }

            // The shortswords: four uses each, their techniques in turn.
            var swords = new[]
            {
                ItemType.CopperShortsword, ItemType.TinShortsword, ItemType.IronShortsword, ItemType.LeadShortsword, ItemType.SilverShortsword, ItemType.TungstenShortsword,
                ItemType.GoldShortsword, ItemType.PlatinumShortsword, ItemType.Gladius, ItemType.Ruler, ItemType.ChlorophyteSaber,
            };
            if (Wants("stab"))
                foreach (var type in swords)
                {
                    var def = ItemDb.Get(type);
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    Stand(1.3f);
                    yield return new WaitForSeconds(0.6f);
                    var moves = new List<string>();
                    int struck = 0;
                    SwordFx.DevCloseFar = 0f;
                    float seconds = Mathf.Max(0.1f, def.UseAnimation * Equipment.UseScale(def));
                    for (int use = 1; use <= 4; use++)
                    {
                        while (mod.IsUsingItem || !mod.DevReady) yield return null;
                        int hits0 = TargetDummy.DevHits;
                        SwordFx.DevCloseMove = "";
                        mod.DevAim = aim;
                        mod.DevUseSelected(aim);
                        yield return new WaitForSeconds(seconds * 0.5f);
                        if (use <= 4 && (type == ItemType.CopperShortsword || type == ItemType.SilverShortsword || type == ItemType.GoldShortsword || use == 1)) Shot("Town", $"closearts_{type}_{use}");
                        for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                        yield return new WaitForSeconds(0.12f);
                        if (TargetDummy.DevHits > hits0) struck++;
                        moves.Add(SwordFx.DevCloseMove);
                    }
                    mod.DevAim = null;
                    float far = SwordFx.DevCloseFar, vanilla = type == ItemType.Gladius || type == ItemType.Ruler || type == ItemType.ChlorophyteSaber ? 2.4f * 21f : 2.1f * 21f;
                    bool named = moves.TrueForAll(m => m.Length > 0);
                    bool varied = new HashSet<string>(moves).Count >= (type == ItemType.Ruler || type == ItemType.ChlorophyteSaber ? 1 : 2);
                    Check(struck >= 3 && named && varied && far >= vanilla * 1.25f,
                        $"{def.Name}：四下打中 {struck}，招式 {string.Join(" → ", moves)}，最远 {far:F0} px（原版约 {vanilla:F0}，×{far / vanilla:F2}）");
                    yield return new WaitForSeconds(0.6f);
                }
            if (Wants("dps"))
                foreach (var type in new[] { ItemType.CopperShortsword, ItemType.IronShortsword, ItemType.SilverShortsword, ItemType.GoldShortsword, ItemType.PlatinumShortsword,
                             ItemType.Gladius, ItemType.Ruler, ItemType.ChlorophyteSaber })
                {
                    var def = ItemDb.Get(type);
                    float[] dps = new float[2];
                    yield return Dps(type, 1.2f, 4f, dps, type == ItemType.Ruler || type == ItemType.ChlorophyteSaber);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    Check(ratio >= 0.85f && ratio <= 1.4f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新招式 {dps[1]:F0}（×{ratio:F2}）");
                }
            // The lances: held at the dummy (jabs, damage), run with (wind), the charge.
            var lances = new[] { ItemType.JoustingLance, ItemType.ShadowJoustingLance, ItemType.HallowJoustingLance };
            if (Wants("lance"))
                foreach (var type in lances)
                {
                    var def = ItemDb.Get(type);
                    float[] dps = new float[2];
                    int jabs0 = TerraProjectiles.DevLanceJabs;
                    yield return Dps(type, 2.6f, 6f, dps, true);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    int jabs = TerraProjectiles.DevLanceJabs - jabs0;
                    Check(ratio >= 0.85f && ratio <= 1.4f && jabs > 0, $"{def.Name} 站着：每秒伤害 原版 {dps[0]:F0}，新 {dps[1]:F0}（×{ratio:F2}），抖枪 {jabs} 次");
                    // Running with it held, away from the dummy: the wind lines.
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    Stand(12f);
                    hero.FaceLeft();
                    yield return new WaitForSeconds(0.5f);
                    int winds0 = TerraProjectiles.DevLanceWinds;
                    var left = (Vector2)hero.transform.position + new Vector2(-6f, 0.5f);
                    mod.DevAim = left;
                    mod.DevHold = true;
                    mod.DevUseSelected(left);
                    Player.TerraMovement.DevMove = -1;
                    yield return new WaitForSeconds(0.9f);
                    if (type == ItemType.JoustingLance) Shot("Town", $"closearts_{type}_run");
                    yield return new WaitForSeconds(0.6f);
                    Player.TerraMovement.DevMove = 0;
                    mod.DevHold = null;
                    mod.DevAim = null;
                    for (float t = Time.time + 2f; mod.IsUsingItem && Time.time < t;) yield return null;
                    int winds = TerraProjectiles.DevLanceWinds - winds0;
                    Check(winds > 3, $"{def.Name} 跑动：风线 {winds} 道");
                    yield return new WaitForSeconds(0.5f);
                }
            // The umbrellas: three uses, their techniques; the damage against the game's.
            if (Wants("umbrella"))
                foreach (var type in new[] { ItemType.Umbrella, ItemType.TragicUmbrella })
                {
                    var def = ItemDb.Get(type);
                    inv.Slots[0] = ItemStack.Of(type);
                    inv.Selected = 0;
                    Stand(1.3f);
                    yield return new WaitForSeconds(0.6f);
                    var moves = new List<string>();
                    int struck = 0;
                    float seconds = Mathf.Max(0.1f, def.UseAnimation * Equipment.UseScale(def));
                    for (int use = 1; use <= 3; use++)
                    {
                        while (mod.IsUsingItem || !mod.DevReady) yield return null;
                        int hits0 = TargetDummy.DevHits;
                        mod.DevAim = aim;
                        mod.DevUseSelected(aim);
                        yield return new WaitForSeconds(seconds * 0.45f);
                        Shot("Town", $"closearts_{type}_{use}");
                        for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                        yield return new WaitForSeconds(0.1f);
                        if (TargetDummy.DevHits > hits0) struck++;
                        moves.Add(MeleeSpecials.DevUmbrellaMove);
                    }
                    mod.DevAim = null;
                    Check(struck == 3 && new HashSet<string>(moves).Count == 3, $"{def.Name}：三下打中 {struck}，招式 {string.Join(" → ", moves)}");
                    float[] dps = new float[2];
                    yield return Dps(type, 1.2f, 4f, dps, false);
                    float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                    Check(ratio >= 0.85f && ratio <= 1.4f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新 {dps[1]:F0}（×{ratio:F2}）");
                }
            // Every right-click art.
            if (Wants("arts"))
            {
                var arts = new (ItemType Item, string Name, bool Charged, float Dx)[]
                {
                    (ItemType.CopperShortsword, "飞刃", false, 4f), (ItemType.LeadShortsword, "飞刃", false, 4f),
                    (ItemType.SilverShortsword, "穿心刺", true, 2.8f), (ItemType.PlatinumShortsword, "穿心刺", true, 2.8f),
                    (ItemType.Gladius, "罗马连刺", false, 1.6f), (ItemType.Ruler, "量天尺", false, 5f), (ItemType.ChlorophyteSaber, "孢子穿刺", true, 2.8f),
                    (ItemType.JoustingLance, "冲锋", true, 7f), (ItemType.ShadowJoustingLance, "暗影冲锋", true, 7f), (ItemType.HallowJoustingLance, "圣光冲锋", true, 7f),
                    (ItemType.Umbrella, "回旋伞", false, 4f), (ItemType.TragicUmbrella, "悲雨", false, 3f),
                };
                foreach (var (item, name, charged, dx) in arts)
                {
                    var def = ItemDb.Get(item);
                    inv.Slots[0] = ItemStack.Of(item);
                    inv.Selected = 0;
                    Stand(dx);
                    mod.DevAim = aim;
                    yield return new WaitForSeconds(1.2f);
                    int uses0 = BladeSkills.DevUses, dmg0 = TargetDummy.DevDamage;
                    BladeSkills.DevLastArt = "";
                    float x0 = hero.transform.position.x;
                    BladeSkills.DevHeld = true;
                    BladeSkills.DevPress = true;
                    yield return new WaitForSeconds(charged ? 1.15f : 0.1f);
                    BladeSkills.DevHeld = false;
                    if (item == ItemType.Umbrella || item == ItemType.CopperShortsword)
                        for (int k = 0; k < 3; k++)
                        {
                            yield return new WaitForSeconds(0.05f);
                            Shot("Town", $"closearts_art_{item}_{k}");
                            Plugin.Log.LogInfo($"[AutoTest] 近身 画出的投掷物 {SwordFx.DevCloseSprite}（主角 {Effects.TerraParticles.ToPx(hero.transform.position):F0}）");
                        }
                    yield return new WaitForSeconds(item == ItemType.TragicUmbrella ? 1.2f : charged ? 0.07f : 0.15f);
                    Shot("Town", $"closearts_art_{item}");
                    yield return new WaitForSeconds(item == ItemType.TragicUmbrella ? 2.4f : 1.4f);
                    BladeSkills.DevHeld = null;
                    mod.DevAim = null;
                    int dmg = TargetDummy.DevDamage - dmg0;
                    float moved = hero.transform.position.x - x0;
                    bool went = BladeSkills.DevUses > uses0 && BladeSkills.DevLastArt == name;
                    bool run = !(item == ItemType.JoustingLance || item == ItemType.ShadowJoustingLance || item == ItemType.HallowJoustingLance) || moved > 3f;
                    Check(went && dmg > 0 && run, $"右键 {def.Name}：{BladeSkills.DevLastArt}，用了 {(went ? "是" : "否")}，假人受到 {dmg}，人移动 {moved:F1}");
                    yield return new WaitForSeconds(item == ItemType.TragicUmbrella ? 4.2f : 2.4f);
                }
            }
            inv.Slots[0] = saved;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 近身 合计 {ok}/{all}");
        }
    }
}
