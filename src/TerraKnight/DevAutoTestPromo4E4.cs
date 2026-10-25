using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (trailer 4.4, recorder E, suite "p4_v5E_book_zh"): the monster book by area. Opened from the inventory's button;
    /// the areas down its left clicked one after another (the grid showing each area's enemies); an enemy that lives in
    /// several areas opened, its "掉落地区" buttons clicked (the drops change with the area); a boss's page: its world
    /// fight's drops and geo, then its "神居" button (the Godhome fight's drops at the player's stage, and Godhome's note).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>An enemy (not a boss) that lives in the most areas, preferring the early ones, from the book's full list.</summary>
        static string E5WideEnemy()
        {
            string best = null;
            int most = 0;
            foreach (var rec in InventoryGui.DevBestiaryFiltered())
            {
                if (EnemyDrops.IsBoss(rec)) continue;
                int n = BestiaryHabitats.Of(rec).Count;
                if (n > most && n <= 7) { most = n; best = rec; }
            }
            return best;
        }

        /// <summary>A boss with a world fight and a Godhome one.</summary>
        static string E5GodhomeBoss()
        {
            foreach (var rec in new[] { "Hornet Boss 1", "Hornet", "MantisLord", "Mantis Lord", "MegaMossCharger", "MageLord", "DungDefender", "FalseKnight" })
            {
                var homes = BestiaryHabitats.Of(rec);
                if (homes.Count > 1 && EnemyDrops.IsBoss(rec))
                    foreach (var h in homes) if (World.Areas.GodhomeRoom(h.Scene)) return rec;
            }
            foreach (var rec in InventoryGui.DevBestiaryFiltered())
            {
                if (!EnemyDrops.IsBoss(rec)) continue;
                var homes = BestiaryHabitats.Of(rec);
                if (homes.Count > 1) foreach (var h in homes) if (World.Areas.GodhomeRoom(h.Scene)) return rec;
            }
            return null;
        }

        /// <summary>The book's area row for a record clicked (its area shown), then its slot in the grid clicked.</summary>
        IEnumerator E5OpenEntry(string rec, float glide)
        {
            int area = BestiaryRoute.Of(rec).Area;
            yield return E5Hit("BestiaryEntry", -100 - area, glide, 0.2f);
            yield return E5Wait(0.7f);
            var r = InventoryGui.DevBestiaryEntryRect(rec);
            if (r == null)
            {
                // Below the grid's first page: scrolled to it as the wheel would.
                for (int k = 0; k < 8 && (r = InventoryGui.DevBestiaryEntryRect(rec)) == null; k++)
                {
                    InventoryGui.DevScrollBestiary(1);
                    yield return E5Wait(0.18f);
                }
            }
            if (r != null) yield return E5GlideClick(r.Value.center, 0.5f, 0.25f);
            else E5Log($"图鉴：网格里找不到 {rec}");
        }

        IEnumerator P4_v5E_book(HeroController hero)
        {
            yield return P4Kit();
            E5NoInfinite();
            yield return E5ToTown(7f);
            hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            // What to show, found before the clip.
            InventoryGui.Open = true;
            InventoryGui.DevBestiary(true);
            yield return null;
            yield return null;
            string wide = E5WideEnemy(), boss = E5GodhomeBoss();
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            E5Log($"图鉴：多地区的怪 {wide}（{(wide != null ? string.Join("、", System.Linq.Enumerable.Select(BestiaryHabitats.Of(wide), h => h.Area)) : "")}），首领 {boss}（{(boss != null ? string.Join("、", System.Linq.Enumerable.Select(BestiaryHabitats.Of(boss), h => h.Area + ":" + h.Scene)) : "")}）");
            yield return new WaitForSeconds(0.5f);

            // ---- The areas.
            E5ClearChat();
            E5PointerOn(GuiOf((Vector2)col.bounds.center + new Vector2(1.5f, 1.8f)));
            yield return E5Rec("book_areas");
            yield return E5Wait(0.4f);
            InventoryGui.Open = true;
            mod.PlaySound("Menu_Open", 0.7f);
            yield return E5Wait(0.5f);
            yield return E5Hit("BestiaryButton", 0, 0.7f, 0.25f);
            yield return E5Wait(1.0f);
            foreach (int f in new[] { 1, 2, 5, 14, 15 })
            {
                yield return E5Hit("BestiaryEntry", -100 - f, f == 1 ? 0.6f : 0.4f, 0.15f);
                yield return E5Wait(1.0f);
                // A couple of the area's enemies hovered.
                var vis = InventoryGui.DevBestiaryVisible();
                for (int k = 0; k < 2 && k < vis.Count; k++)
                {
                    var r = InventoryGui.DevBestiaryEntryRect(vis[Mathf.Min(vis.Count - 1, 2 + k * 3)]);
                    if (r != null) { yield return E5Glide(r.Value.center, 0.35f); yield return E5Wait(0.3f); }
                }
            }
            StopRec();

            // ---- One enemy, area by area.
            if (wide != null)
            {
                yield return E5Rec("book_drops");
                yield return E5Wait(0.2f);
                yield return E5OpenEntry(wide, 0.5f);
                yield return E5Wait(1.2f);
                var homes = BestiaryHabitats.Of(wide);
                E5Log($"图鉴：{Bestiary.NameOf(wide)} 掉落地区 {homes.Count} 个，按钮 {string.Join(",", InventoryGui.DevHitIndexes("BestiaryEntry").FindAll(i => i <= -30 && i > -60))}");
                for (int k = 1; k < homes.Count && k < 5; k++)
                {
                    yield return E5Hit("BestiaryEntry", -30 - k, 0.45f, 0.2f);
                    yield return E5Wait(1.3f);
                }
                yield return E5Hit("BestiaryEntry", -30, 0.5f, 0.2f);
                yield return E5Wait(1.0f);
                StopRec();
            }

            // ---- A boss: its world fight, then Godhome.
            if (boss != null)
            {
                yield return E5Rec("book_boss");
                yield return E5Wait(0.2f);
                yield return E5OpenEntry(boss, 0.5f);
                yield return E5Wait(1.4f);
                // Over its geo and its drops.
                var panel = InventoryGui.DevHitRect("BestiaryPanel", 0);
                if (panel != null)
                {
                    yield return E5Glide(new Vector2(panel.Value.x + panel.Value.width * 0.72f, panel.Value.y + panel.Value.height * 0.62f), 0.7f);
                    yield return E5Wait(0.8f);
                }
                var homes = BestiaryHabitats.Of(boss);
                int god = homes.Count - 1;
                for (int i = 0; i < homes.Count; i++) if (World.Areas.GodhomeRoom(homes[i].Scene)) god = i;
                yield return E5Hit("BestiaryEntry", -30 - god, 0.6f, 0.25f);
                yield return E5Wait(1.6f);
                if (panel != null)
                {
                    yield return E5Glide(new Vector2(panel.Value.x + panel.Value.width * 0.74f, panel.Value.y + panel.Value.height * 0.7f), 0.6f);
                    yield return E5Wait(0.5f);
                    for (int k = 0; k < 3; k++)
                    {
                        InventoryGui.DevScrollDrops(1);
                        yield return E5Wait(0.35f);
                    }
                    yield return E5Wait(0.6f);
                }
                yield return E5Hit("BestiaryEntry", -30, 0.6f, 0.25f);
                yield return E5Wait(1.2f);
                StopRec();
            }
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            E5PointerOff();
        }
    }
}
