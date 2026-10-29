using System.Collections;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "heldarts", Items.TerraProjectilesHeldArts, the user's 10-26 "怎么没看见泰拉魔刃"): in Dirtmouth by its Target
    /// Dummy, each held blade held 2.5 s at it: the Arkhalis's and the Terragrim's dance goes through its cuts, reaching at
    /// least 30% further than the game's whirl (50 px), the Terragrim's gales fly, the dummy is struck; then its damage a
    /// second against the game's (the option off), within 0.85 to 1.6; then its right-click art (the gale run moving the
    /// player and loosing gales, the meteor flurry striking). Shots Town_heldarts_&lt;blade&gt;. Lines "[AutoTest] 握持剑 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HeldArtsTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 握持剑 {what} {(pass ? "通过" : "不通过")}");
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
            var inv = mod.Inventory;
            var saved = inv.Slots[0];
            int savedSel = inv.Selected;

            IEnumerator Hold(ItemType type, float seconds, string shot)
            {
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
                mod.DevAim = aim;
                mod.DevHold = true;
                mod.DevUse(type, aim);
                float start = Time.time;
                bool shotDone = false;
                while (Time.time - start < seconds)
                {
                    if (!shotDone && shot != null && Time.time - start > seconds * 0.5f) { Shot("Town", shot); shotDone = true; }
                    yield return null;
                }
                mod.DevHold = null;
                mod.DevAim = null;
                yield return new WaitForSeconds(0.5f);
            }

            void Stand(float dx)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
            }

            foreach (var type in new[] { ItemType.Arkhalis, ItemType.Terragrim, ItemType.PiercingStarlight })
            {
                var def = ItemDb.Get(type);
                Stand(1.8f);
                yield return new WaitForSeconds(0.8f);
                int hits0 = TargetDummy.DevHits, cuts0 = TerraProjectiles.DevDanceCuts, gales0 = TerraProjectiles.DevGales;
                TerraProjectiles.DevDanceFar = 0f;
                yield return Hold(type, 2.5f, $"heldarts_{type}");
                int hits = TargetDummy.DevHits - hits0, cuts = TerraProjectiles.DevDanceCuts - cuts0, gales = TerraProjectiles.DevGales - gales0;
                float far = TerraProjectiles.DevDanceFar;
                bool dance = type == ItemType.PiercingStarlight || cuts >= 5 && far >= 50f * 1.3f;
                bool gale = type != ItemType.Terragrim || gales >= 1;
                Check(hits >= 5 && dance && gale, $"{def.Name}：命中 {hits}，剑舞 {cuts} 招，最远 {far:F0} px，疾风 {gales}");

                float[] dps = new float[2];
                for (int mode = 0; mode < 2; mode++)
                {
                    BladeArts.Set(mode == 1);
                    Stand(1.8f);
                    yield return new WaitForSeconds(0.8f);
                    int dmg0 = TargetDummy.DevDamage;
                    float start = Time.time;
                    yield return Hold(type, 3f, null);
                    dps[mode] = (TargetDummy.DevDamage - dmg0) / (Time.time - start);
                }
                BladeArts.Set(true);
                float ratio = dps[0] > 0f ? dps[1] / dps[0] : 0f;
                Check(ratio >= 0.85f && ratio <= 1.6f, $"{def.Name} 每秒伤害：原版 {dps[0]:F0}，新版 {dps[1]:F0}（×{ratio:F2}）");

                // Its right-click art.
                inv.Slots[0] = ItemStack.Of(type);
                inv.Selected = 0;
                Stand(3.2f);
                mod.DevAim = aim;
                yield return new WaitForSeconds(1.2f);
                bool charged = type != ItemType.PiercingStarlight;
                int uses0 = BladeSkills.DevUses, dmgA = TargetDummy.DevDamage, galesA = TerraProjectiles.DevGales;
                float x0 = hero.transform.position.x;
                BladeSkills.DevHeld = true;
                BladeSkills.DevPress = true;
                yield return new WaitForSeconds(charged ? 1.15f : 0.1f);
                BladeSkills.DevHeld = false;
                yield return new WaitForSeconds(0.3f);
                Shot("Town", $"heldarts_art_{type}");
                yield return new WaitForSeconds(1.2f);
                BladeSkills.DevHeld = null;
                mod.DevAim = null;
                float moved = hero.transform.position.x - x0;
                int artDmg = TargetDummy.DevDamage - dmgA, artGales = TerraProjectiles.DevGales - galesA;
                bool went = BladeSkills.DevUses > uses0;
                bool shape = charged ? moved > 1.5f && artGales >= 2 : true;
                Check(went && artDmg > 0 && shape, $"右键 {def.Name}：{BladeSkills.DevLast}，用了 {(went ? "是" : "否")}，假人受到 {artDmg}，人移动 {moved:F1}，疾风 {artGales}");
                yield return new WaitForSeconds(3f);
            }
            inv.Slots[0] = saved;
            inv.Selected = savedSel;
            Plugin.Log.LogInfo($"[AutoTest] 握持剑 合计 {ok}/{all}");
        }
    }
}
