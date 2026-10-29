using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "arcsurge", the user's 10-27 "电弧涌动是不是不符合原版？耗蓝和伤害以及机制什么的似乎都不对"): in Dirtmouth, the Arc
    /// Surge held on the Target Dummy for 0.9 s (three uses, three bolts a use with up to two more at enemies): mana paid once a
    /// use (ItemCheck_TryStartUse, 1.4.5), not for every bolt; its numbers (damage, mana, use and shot times) logged against the
    /// game's (180, 18, 18/6). Lines "[AutoTest] 电弧涌动 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArcSurgeTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt ?? (Vector2)hero.transform.position + Vector2.right * 4f;
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var inv = mod.Inventory;
            var def = ItemDb.Get(ItemType.ArcSurge);
            inv.Slots[0] = ItemStack.Of(ItemType.ArcSurge);
            inv.Selected = 0;
            hero.transform.position = Clear(hero, new Vector2(at.x - 5f, at.y - bottom + 0.05f));
            hero.FaceRight();
            mod.DevAim = at + new Vector2(0f, 0.8f);
            yield return new WaitForSeconds(0.6f);
            int cost = Equipment.ManaCost(def);
            mod.Stats.Mana = mod.Stats.ManaMax;
            int mana0 = mod.Stats.Mana, bolts0 = TerraProjectiles.DevMade(ProjKind.ArcSurgeBolt), uses0 = TerraMod.DevUseCount, dmg0 = TargetDummy.DevDamage, pays0 = TerraMod.DevManaChecks;
            float start = Time.time;
            TerraMod.DevShotLog = new System.Collections.Generic.List<string>();
            mod.DevLeftHeld = true;
            mod.DevHold = true;
            // The real mouse isn't over the dummy: the Update's own loop aims with the held button, so click each shot at it.
            // Mana kept up (a pool too small for three uses runs slow and spoils the count); its slow casts after, apart.
            for (float end = Time.time + 0.9f; Time.time < end;)
            {
                if (mod.Stats.Mana < 60) mod.Stats.Mana = Mathf.Max(mod.Stats.ManaMax, 60);
                if (mod.DevReady) mod.DevUseSelected(at + new Vector2(0f, 0.8f));
                yield return null;
            }
            mod.DevLeftHeld = null;
            mod.DevHold = null;
            int spent = mana0 - mod.Stats.Mana, bolts = TerraProjectiles.DevMade(ProjKind.ArcSurgeBolt) - bolts0, starts = Mathf.Max(1, Mathf.CeilToInt((Time.time - start) / (def.UseAnimation * Equipment.UseScale(def))));
            // The mana paid (each a CheckMana: the start of a use) against the shots made (TerraMod.DevUseCount): one in three.
            int pays = TerraMod.DevManaChecks - pays0, shots = TerraMod.DevUseCount - uses0;
            yield return new WaitForSeconds(1f);
            int dmg = TargetDummy.DevDamage - dmg0;
            // Cast 3 bolts a use (more with enemies about): mana about a use's cost per three bolts, never one per bolt.
            float perBolt = bolts > 0 ? spent / (float)bolts : 0f;
            bool pass = bolts >= 6 && shots >= 6 && pays >= 2 && pays <= Mathf.CeilToInt(shots / 3f) + 1;
            Plugin.Log.LogInfo($"[AutoTest] 电弧涌动 数值：伤害 {def.Damage}（原版 180），耗魔 {def.Mana}（原版 18，实付 {cost}），用时 {def.UseAnimation * 60f:F0}/{def.UseTime * 60f:F0}（原版 18/6）");
            Plugin.Log.LogInfo($"[AutoTest] 电弧涌动 0.9 秒：射击 {shots} 次，闪电 {bolts} 道，付魔 {pays} 次（每次 {cost}，约 {starts} 次使用），魔力少了 {spent}（含回魔），假人受到 {dmg} {(pass ? "通过" : "不通过")}");
            Plugin.Log.LogInfo($"[AutoTest] 电弧涌动 各次射击：{string.Join("  ", TerraMod.DevShotLog)}");
            // Out of mana: a slow use, its shots 2.5 times apart too (no more of them for the mana it doesn't have).
            yield return new WaitForSeconds(1f);
            mod.Stats.Mana = 0;
            TerraMod.DevShotLog.Clear();
            int slowShots0 = TerraMod.DevUseCount;
            mod.DevLeftHeld = true;
            mod.DevHold = true;
            for (float end = Time.time + 0.9f; Time.time < end;)
            {
                mod.Stats.Mana = 0;
                if (mod.DevReady) mod.DevUseSelected(at + new Vector2(0f, 0.8f));
                yield return null;
            }
            mod.DevLeftHeld = null;
            mod.DevHold = null;
            int slowShots = TerraMod.DevUseCount - slowShots0;
            Plugin.Log.LogInfo($"[AutoTest] 电弧涌动 没魔力 0.9 秒：射击 {slowShots} 次（正常 {shots} 次）{(slowShots <= shots / 2 + 1 ? "通过" : "不通过")}：{string.Join("  ", TerraMod.DevShotLog)}");
            TerraMod.DevShotLog = null;
            mod.DevAim = null;
        }
    }
}
