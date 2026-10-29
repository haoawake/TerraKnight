using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dmgcompare", the user's 10-28 "为什么明明只是微光嬗变，真铜短剑的伤害比天顶剑高那么多"): the Zenith's and the
    /// True Copper Shortsword's damage as balanced and as shown, their output models, and what each deals the Target Dummy in
    /// 8 s, Terraria's weapons and the remade. Lines "[AutoTest] 伤害对比 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DmgCompareTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            foreach (var t in new[] { ItemType.Zenith, ItemType.TrueCopperShortsword })
            {
                var d = ItemDb.Get(t);
                var o = ProgressionBalance.OutputOf(d);
                Plugin.Log.LogInfo($"[AutoTest] 伤害对比 {d.Name}：面板 {d.Damage}，实际 {Equipment.Damage(d, null)}，模型 每秒 {o.Rate:0.##} 次 × {o.Volley} × {o.Repeat:0.##}");
            }
            for (float w = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < w;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Plugin.Log.LogInfo("[AutoTest] 伤害对比 不通过：没有训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at.Value + new Vector2(0f, 0.8f);
            var inv = mod.Inventory;
            var saved = inv.Slots.ToArray();
            int savedSel = inv.Selected;
            var dealt = new int[2, 2];
            for (int mode = 0; mode < 2; mode++)
                for (int w = 0; w < 2; w++)
                {
                    BladeArts.Set(mode == 1);
                    for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                    inv.Slots[0] = ItemStack.Of(w == 0 ? ItemType.Zenith : ItemType.TrueCopperShortsword);
                    inv.Selected = 0;
                    hero.transform.position = Clear(hero, new Vector2(at.Value.x - 5f, at.Value.y - bottom + 0.05f));
                    hero.FaceRight();
                    yield return new WaitForSeconds(1f);
                    int d0 = TargetDummy.DevDamage;
                    mod.DevAim = aim;
                    mod.DevHold = true;
                    for (float end = Time.time + 8f; Time.time < end;)
                    {
                        if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(aim);
                        yield return null;
                    }
                    mod.DevHold = null;
                    mod.DevAim = null;
                    yield return new WaitForSeconds(1.5f);
                    dealt[mode, w] = TargetDummy.DevDamage - d0;
                }
            BladeArts.Set(true);
            for (int i = 0; i < inv.Slots.Length && i < saved.Length; i++) inv.Slots[i] = saved[i];
            inv.Selected = savedSel;
            bool close = true;
            for (int mode = 0; mode < 2; mode++)
            {
                float ratio = dealt[mode, 0] > 0 ? dealt[mode, 1] / (float)dealt[mode, 0] : 0f;
                close &= ratio > 0.85f && ratio < 1.15f;
                Plugin.Log.LogInfo($"[AutoTest] 伤害对比 {(mode == 0 ? "原版武器" : "新版武器")} 8 秒：天顶剑 {dealt[mode, 0]}，真铜短剑 {dealt[mode, 1]}（×{ratio:0.00}）");
            }
            Plugin.Log.LogInfo($"[AutoTest] 伤害对比 {(close ? "通过" : "不通过")}");
        }
    }
}
