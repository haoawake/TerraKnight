using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "maskhearts"): the Knight's Mask Shards as life of their own (half a heart each, cyan hearts after the
    /// rest), Life Crystals no longer gated by masks, Life Fruits drawn golden; the life column photographed full, with the
    /// masks' hearts half gone, and into the red hearts.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MaskHeartsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 面具生命 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", EntryGateOf("Town"));
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            var st = mod.Stats;
            int masks0 = pd.maxHealthBase, pieces0 = pd.heartPieces, crystals0 = st.LifeCrystals, fruits0 = st.LifeFruits;
            try
            {
                // Seven masks and two shards of the eighth: (2 * 4 + 2) shards, 100 life; fifteen crystals and four fruits.
                pd.maxHealthBase = 7;
                pd.heartPieces = 2;
                st.LifeCrystals = 15;
                st.LifeFruits = 4;
                yield return new WaitForSeconds(0.3f);
                Check("上限分开算", st.MaskLife == 100 && st.LifeMax == 520, $"面具 {st.MaskLife}，上限 {st.LifeMax}（100 + 15×20 + 4×5 + 100）");
                Check("生命水晶额度不看面具", World.LifeCrystals.Allowance() <= Player.TerraStats.MaxLifeCrystals, $"额度 {World.LifeCrystals.Allowance()}");
                st.Life = st.LifeMax;
                yield return new WaitForSeconds(0.4f);
                Shot("Town", "maskhearts_full");
                yield return null;
                st.Life = st.LifeMax - 50;
                yield return new WaitForSeconds(0.4f);
                Shot("Town", "maskhearts_masks_hit");
                yield return null;
                st.Life = 300;
                yield return new WaitForSeconds(0.4f);
                Shot("Town", "maskhearts_red");
                yield return null;
                // No masks beyond five, no fruits: Terraria's own hearts only.
                pd.maxHealthBase = 5;
                pd.heartPieces = 0;
                st.LifeFruits = 0;
                st.LifeCrystals = 5;
                yield return new WaitForSeconds(0.3f);
                Check("没有额外面具", st.MaskLife == 0 && st.LifeMax == 200, $"面具 {st.MaskLife}，上限 {st.LifeMax}");
                // Twenty fruits: twenty golden hearts of 25.
                st.LifeCrystals = 15;
                st.LifeFruits = 20;
                pd.maxHealthBase = 9;
                pd.heartPieces = 0;
                yield return new WaitForSeconds(0.3f);
                st.Life = st.LifeMax;
                yield return new WaitForSeconds(0.4f);
                Check("满生命果加满面具", st.MaskLife == 160 && st.LifeMax == 660, $"面具 {st.MaskLife}，上限 {st.LifeMax}");
                Shot("Town", "maskhearts_max");
                yield return null;
            }
            finally
            {
                pd.maxHealthBase = masks0;
                pd.heartPieces = pieces0;
                st.LifeCrystals = crystals0;
                st.LifeFruits = fruits0;
            }
            yield return new WaitForSeconds(0.3f);
            st.FullHeal();
            Plugin.Log.LogInfo($"[AutoTest] 面具生命 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
