using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "magecast", Items.MageArts / SwordFxMage, the user's 10-26 "法师武器的挥舞动画，全改"): in Dirtmouth by its
    /// Target Dummy, every magic weapon used once at it (mana kept full, channelled ones held a second): its cast begins, its
    /// burst comes off its head, a wand is drawn swept round; shots early in the use and midway (Town_magecast_&lt;item&gt;_a / _b).
    /// TK_MAGE_ONLY narrows it to some items (comma separated). Lines "[AutoTest] 法师挥舞 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MageCastTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 法师挥舞 {what} {(pass ? "通过" : "不通过")}");
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
            var only = (System.Environment.GetEnvironmentVariable("TK_MAGE_ONLY") ?? "").Split(',').Where(x => x.Length > 0).ToList();
            var weapons = ItemDb.All.Where(d => d != null && d.IsWeapon && d.Class == DamageClass.Magic && (only.Count == 0 || only.Contains(d.Type.ToString())))
                .OrderBy(d => d.Stage).ThenBy(d => d.Rare).ToList();
            foreach (var def in weapons)
            {
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - 4f, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                mod.Stats.Mana = mod.Stats.ManaMax;
                yield return new WaitForSeconds(0.35f);
                for (float t = Time.time + 2f; Time.time < t && (mod.IsUsingItem || !mod.DevReady);) yield return null;
                var look = MageArts.Of(def);
                int casts0 = SwordFx.DevMageCasts, bursts0 = SwordFx.DevMageBursts, wands0 = SwordFx.DevWandDraws;
                float seconds = Mathf.Max(0.1f, def.UseAnimation * Equipment.UseScale(def));
                bool hold = def.Channel;
                if (hold) mod.DevHold = true;
                mod.DevAim = aim;
                mod.DevUse(def.Type, aim);
                yield return new WaitForSeconds(Mathf.Min(0.1f, seconds * 0.3f));
                Shot("Town", $"magecast_{def.Type}_a");
                yield return new WaitForSeconds(Mathf.Max(0.05f, seconds * 0.45f - 0.1f));
                Shot("Town", $"magecast_{def.Type}_b");
                yield return new WaitForSeconds(hold ? 0.8f : seconds * 0.6f + 0.1f);
                mod.DevHold = null;
                bool wand = look.Kind == MageArts.Kind.Wand;
                bool pass = SwordFx.DevMageCasts > casts0 && SwordFx.DevMageBursts > bursts0 && (!wand || SwordFx.DevWandDraws > wands0);
                Check(pass, $"{def.Name}（{look.Kind}）：施法 {SwordFx.DevMageCasts - casts0}，爆开 {SwordFx.DevMageBursts - bursts0}{(wand ? $"，魔杖画出 {SwordFx.DevWandDraws - wands0} 帧" : "")}");
                for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
            }
            mod.DevAim = null;
            Plugin.Log.LogInfo($"[AutoTest] 法师挥舞 合计 {ok}/{all}");
        }
    }
}
