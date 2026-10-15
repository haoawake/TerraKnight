using System.Collections;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dropscroll"): the monster book on a boss with more drops than fit (the False Knight's),
    /// beaten: its drops scroll by rows to the last one, and a photograph before and after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DropScrollTest()
        {
            yield return new WaitForSeconds(1f);
            // The entry with the most drops.
            string rec = "FalseKnight";
            foreach (var r in EnemyDrops.DevRecords) if (r.IndexOf('@') < 0 && EnemyDrops.Of(r).Count > EnemyDrops.Of(rec).Count) rec = r;
            int kills0 = Bestiary.KillsOf(rec);
            Bestiary.Kills[rec] = Mathf.Max(1, kills0);
            InventoryGui.Open = true;
            yield return null;
            InventoryGui.DevBestiaryShow(rec);
            yield return new WaitForSeconds(0.6f);
            int max = InventoryGui.DevDropMaxScroll, drops = EnemyDrops.Of(rec).Count;
            Shot(GameManager.instance.sceneName, "dropscroll_top");
            yield return null;
            InventoryGui.DevScrollDrops(99);
            yield return new WaitForSeconds(0.3f);
            int at = InventoryGui.DevDropScroll;
            Shot(GameManager.instance.sceneName, "dropscroll_bottom");
            yield return null;
            InventoryGui.DevScrollDrops(-99);
            yield return new WaitForSeconds(0.2f);
            int back = InventoryGui.DevDropScroll;
            bool pass = max > 0 && at == max && back == 0;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴掉落滚动 {rec} {drops} 种掉落：能往下翻 {max} 行，翻到底 {at}，翻回顶 {back} {(pass ? "通过" : "不通过")}");
            InventoryGui.Open = false;
            if (kills0 == 0) Bestiary.Kills.Remove(rec);
        }
    }
}
