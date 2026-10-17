using System.Collections;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "lifeblood"): the game's blue masks given (a lifeblood cocoon's): two silver hearts of shield and two mana
    /// stars; hits take the shield first, a broken shield lets the blue masks go; lifeblood again fills the shield, not more
    /// stars; death takes both. The life column photographed with the shield up.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LifebloodTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 生命血 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", EntryGateOf("Town"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            var st = mod.Stats;
            st.FullHeal();
            int mana0 = st.ManaMax;
            pd.healthBlue = 2;
            yield return new WaitForSeconds(0.3f);
            Check("得到生命血", st.Shield == Lifeblood.ShieldLife && st.TempMana == Lifeblood.ManaStars && st.ManaMax == mana0 + Lifeblood.ManaStars,
                $"护盾 {st.Shield}，临时魔力 {st.TempMana}，魔力上限 {mana0} → {st.ManaMax}");
            st.RestoreMana(999);
            yield return new WaitForSeconds(0.4f);
            Shot("Town", "lifeblood_shield");
            yield return null;
            int life0 = st.Life;
            st.TakeDirect(30 + st.Defense / 2);
            yield return new WaitForSeconds(0.2f);
            Check("护盾先挨打", st.Life == life0 && st.Shield == 10, $"生命 {life0} → {st.Life}，护盾 {st.Shield}");
            st.TakeDirect(30 + st.Defense / 2);
            yield return new WaitForSeconds(0.3f);
            Check("护盾打破后挨生命", st.Shield == 0 && st.Life == life0 - 20 && pd.healthBlue == 0, $"生命 {st.Life}，护盾 {st.Shield}，蓝面具 {pd.healthBlue}");
            pd.healthBlue = 3;
            yield return new WaitForSeconds(0.3f);
            Check("再打一次生命血", st.Shield == Lifeblood.ShieldLife && st.ManaMax == mana0 + Lifeblood.ManaStars, $"护盾 {st.Shield}，魔力上限 {st.ManaMax}");
            Lifeblood.OnDeath(st);
            pd.healthBlue = 0;
            yield return new WaitForSeconds(0.3f);
            Check("死亡清空", st.Shield == 0 && st.TempMana == 0 && st.ManaMax == mana0, $"护盾 {st.Shield}，魔力上限 {st.ManaMax}");
            // The Water Candle's tooltip: its use's multiplier said, in English too.
            var tip = Items.ItemDb.Get(Items.ItemType.WaterCandle).Tooltip ?? "";
            var lines = tip.Split('\n');
            string en = lines.Length > 1 ? Lang.Line(lines[1], "en-US") : null;
            Check("水蜡烛说明写了倍率", lines.Length >= 2 && lines[1].Contains("倍率") && en != null && en.Contains("multiplier"), $"{tip.Replace('\n', '|')} / {en}");
            st.FullHeal();
            Plugin.Log.LogInfo($"[AutoTest] 生命血 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
