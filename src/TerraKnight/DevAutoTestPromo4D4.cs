using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.0, recorder D): the monster book. Opened from the inventory's button; a Chinese search typed into
    /// its box a character at a time (the grid narrowing as it goes), an entry chosen; the drop-stage picker clicked
    /// through, the drop list changing; a boss's page.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator P4_Dbook(HeroController hero)
        {
            yield return P4Kit();
            yield return EnterRoom("Town");
            hero = HeroController.instance;
            D4NoAreaTitle();
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2.5f);
            var col = hero.GetComponent<BoxCollider2D>();
            // The search to type: one that finds a handful (by an enemy's name, or by an item it drops).
            string query = null;
            var counts = new System.Text.StringBuilder();
            InventoryGui.Open = true;
            InventoryGui.DevBestiary(true);
            yield return null;
            foreach (var q in new[] { "天顶", "泰拉", "骑士", "灵魂", "蜂", "苔藓" })
            {
                InventoryGui.DevBookSearch = q;
                yield return null;
                yield return null;
                yield return null;
                int n = InventoryGui.DevBookRows().Count;
                counts.Append($" {q}:{n}");
                if (query == null && n >= 2 && n <= 14) query = q;
            }
            InventoryGui.DevBookSearch = "";
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 图鉴：搜索结果{counts}，用 {query}");
            if (query == null) query = "骑士";

            PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.2f, 1.4f)));
            yield return D4Rec("book_search");
            yield return new WaitForSeconds(0.3f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return new WaitForSeconds(0.5f);
            yield return ClickHit("BestiaryButton", 0, 0.7f);
            yield return new WaitForSeconds(0.9f);
            // Into the search box, typed a character at a time.
            yield return ClickHit("BestiaryEntry", -4, 0.6f);
            yield return new WaitForSeconds(0.5f);
            for (int k = 1; k <= query.Length; k++)
            {
                InventoryGui.DevBookSearch = query.Substring(0, k);
                mod.PlaySound("Menu_Tick", 0.5f);
                yield return new WaitForSeconds(k < query.Length ? 0.45f : 0.3f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 图鉴：输入“{InventoryGui.DevBookSearch}”，{InventoryGui.DevBookRows().Count} 个");
            }
            InventoryGui.DevBookType(false);
            yield return new WaitForSeconds(0.8f);
            // Over the results, one chosen.
            var vis = InventoryGui.DevBestiaryVisible();
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 图鉴：结果 {string.Join(",", vis)}");
            for (int k = 0; k < vis.Count && k < 3; k++)
            {
                var r = InventoryGui.DevBestiaryEntryRect(vis[k]);
                if (r != null) { yield return Glide(r.Value.center, 0.35f); yield return new WaitForSeconds(0.3f); }
            }
            if (vis.Count > 0)
            {
                var r = InventoryGui.DevBestiaryEntryRect(vis[Mathf.Min(1, vis.Count - 1)]);
                if (r != null) yield return ClickAt(r.Value, 0.35f);
            }
            yield return new WaitForSeconds(1.6f);
            StopRec();

            // --- The drop stages: the picker clicked through, the list changing.
            yield return D4Rec("book_stages");
            yield return new WaitForSeconds(0.2f);
            foreach (int st in new[] { 0, 2, 4, 5 })
            {
                yield return ClickHit("BestiaryEntry", -11 - st, 0.5f);
                yield return new WaitForSeconds(0.9f);
                Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 图鉴：掉落阶段 {InventoryGui.DevBestiaryDropStage}");
            }
            yield return ClickHit("BestiaryEntry", -10, 0.5f);
            yield return new WaitForSeconds(0.7f);
            StopRec();

            // --- A boss's page: the search cleared, the bosses' section, one opened.
            InventoryGui.DevBookSearch = "";
            InventoryGui.DevBookType(false);
            string boss = null;
            var rows = InventoryGui.DevBestiaryFiltered();
            foreach (var rec in new[] { "HiveKnight", "Hornet Boss 1", "Mawlek", "MegaMossCharger", "FalseKnight" })
                if (boss == null && rows.Contains(rec)) boss = rec;
            if (boss == null) foreach (var rec in rows) if (boss == null && ProgressionBalance.FormalBoss(rec)) boss = rec;
            Plugin.Log.LogInfo($"[AutoTest] 录制 {D4At} 图鉴：首领 {boss}");
            if (boss != null)
            {
                // The grid scrolled to its section, a neighbour on the page; then the pointer clicks the boss.
                InventoryGui.DevBestiaryShow(boss);
                yield return null;
                yield return null;
                rows = InventoryGui.DevBookRows();
                int at = rows.IndexOf(boss);
                if (at >= 0) InventoryGui.DevBestiarySelect(at > 0 ? at - 1 : at + 1);
                yield return null;
                yield return D4Rec("book_boss");
                yield return new WaitForSeconds(0.3f);
                var r = InventoryGui.DevBestiaryEntryRect(boss);
                if (r != null) yield return ClickAt(r.Value, 0.6f);
                yield return new WaitForSeconds(0.6f);
                yield return Glide(new Vector2(Screen.width * 0.66f, Screen.height * 0.42f), 0.9f);
                yield return new WaitForSeconds(0.8f);
                yield return Glide(new Vector2(Screen.width * 0.7f, Screen.height * 0.72f), 1.0f);
                yield return new WaitForSeconds(1.2f);
                StopRec();
            }
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            PointerOff();
        }
    }
}
