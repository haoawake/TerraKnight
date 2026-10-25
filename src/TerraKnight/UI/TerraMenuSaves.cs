using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class TerraMenu
    {
        /// <summary>A save on the world list: its slot, when its game save was written, the game's stats of it (read in turn) and its mod save's header.</summary>
        sealed class Row
        {
            public int Slot;
            public DateTime Stamp;
            public SaveStats Stats;
            public bool StatsAsked, StatsFailed;
            public Save.ModSave.Header Head;
            public bool Favorite;
            /// <summary>A Steel Soul save whose knight died: the game only lets it be cleared.</summary>
            public bool Dead => Stats != null && Stats.permadeathMode == 2;
        }

        static readonly List<Row> rows = new List<Row>();
        static readonly Dictionary<int, (DateTime Stamp, SaveStats Stats)> statsCache = new Dictionary<int, (DateTime, SaveStats)>();
        static bool statsBusy;

        static string GameSave(int slot) => Path.Combine(TerraMod.SaveDir, $"user{slot}.dat");

        /// <summary>The first slot with no game save (0 when all fifty have one).</summary>
        static int FirstFreeSlot()
        {
            for (int s = 1; s <= SaveSlots.Max; s++)
                if (!File.Exists(GameSave(s))) return s;
            return 0;
        }

        /// <summary>The list read again: every slot with a game save, favourites first, then the latest played (UIWorldSelect's order).</summary>
        static void Refresh()
        {
            rows.Clear();
            LoadMenuFile();
            for (int s = 1; s <= SaveSlots.Max; s++)
            {
                string path = GameSave(s);
                if (!File.Exists(path)) continue;
                var row = new Row { Slot = s, Stamp = File.GetLastWriteTimeUtc(path), Head = Save.ModSave.HeaderOf(s), Favorite = menuFile.Favorites.Contains(s) };
                if (statsCache.TryGetValue(s, out var c) && c.Stamp == row.Stamp)
                {
                    row.Stats = c.Stats;
                    row.StatsAsked = true;
                }
                rows.Add(row);
            }
            Sort();
        }

        static void Sort() => rows.Sort((a, b) => a.Favorite != b.Favorite ? (a.Favorite ? -1 : 1) : a.Stamp != b.Stamp ? b.Stamp.CompareTo(a.Stamp) : a.Slot.CompareTo(b.Slot));

        /// <summary>The game's stats of one save at a time (GameManager.GetSaveStatsForSlot decrypts the whole file), the rest after.</summary>
        static void PumpStats()
        {
            var gm = GameManager.UnsafeInstance;
            if (statsBusy || gm == null) return;
            foreach (var row in rows)
            {
                if (row.StatsAsked) continue;
                row.StatsAsked = true;
                statsBusy = true;
                var r = row;
                try
                {
                    gm.GetSaveStatsForSlot(r.Slot, st =>
                    {
                        statsBusy = false;
                        r.Stats = st;
                        r.StatsFailed = st == null;
                        if (st != null) statsCache[r.Slot] = (r.Stamp, st);
                    });
                }
                catch (Exception e)
                {
                    statsBusy = false;
                    r.StatsFailed = true;
                    Plugin.Log.LogWarning($"[开始菜单] 读不了存档 {r.Slot} 的统计：{e.Message}");
                }
                return;
            }
        }

        /// <summary>The world's name: its own, the one given on the list to a save the mod has none of, else "存档 N".</summary>
        static string RowName(Row row)
        {
            if (!string.IsNullOrEmpty(row?.Head?.WorldName)) return row.Head.WorldName;
            if (row != null && menuFile.Names.TryGetValue(row.Slot, out var n) && !string.IsNullOrEmpty(n)) return n;
            return Lang.F("存档 {0}", row?.Slot ?? 0);
        }

        // ------------------------------------------------------------------ favourites and names kept beside the saves

        /// <summary>
        /// The list's own file beside the saves (as Terraria keeps favorites.json): the favourite slots, and the names given
        /// to saves the mod has no save of (a save from the game alone).
        /// </summary>
        sealed class MenuFile
        {
            public List<int> Favorites = new List<int>();
            public Dictionary<int, string> Names = new Dictionary<int, string>();
        }

        static MenuFile menuFile = new MenuFile();

        static string MenuPath => Path.Combine(TerraMod.SaveDir, "TerraKnight_menu.json");

        static void LoadMenuFile()
        {
            try { menuFile = File.Exists(MenuPath) ? JsonConvert.DeserializeObject<MenuFile>(File.ReadAllText(MenuPath)) : null; }
            catch (Exception e) { Plugin.Log.LogWarning($"[开始菜单] 读不了 {MenuPath}：{e.Message}"); menuFile = null; }
            if (menuFile == null) menuFile = new MenuFile();
            if (menuFile.Favorites == null) menuFile.Favorites = new List<int>();
            if (menuFile.Names == null) menuFile.Names = new Dictionary<int, string>();
        }

        static void SaveMenuFile()
        {
            try { File.WriteAllText(MenuPath, JsonConvert.SerializeObject(menuFile)); }
            catch (Exception e) { Plugin.Log.LogWarning($"[开始菜单] 写不了 {MenuPath}：{e.Message}"); }
        }

        static void ToggleFavorite(Row row)
        {
            row.Favorite = !row.Favorite;
            menuFile.Favorites.Remove(row.Slot);
            if (row.Favorite) menuFile.Favorites.Add(row.Slot);
            SaveMenuFile();
            Sort();
        }

        /// <summary>Delete's 是: the game's save cleared, then the mod's save, the map and the list's marks of it.</summary>
        static void Delete(Row row)
        {
            var gm = GameManager.instance;
            if (gm == null || row == null) return;
            int slot = row.Slot;
            busy = true;
            gm.ClearSaveFile(slot, ok =>
            {
                busy = false;
                if (ok)
                {
                    Save.ModSave.Delete(slot);
                    menuFile.Favorites.Remove(slot);
                    menuFile.Names.Remove(slot);
                    SaveMenuFile();
                    statsCache.Remove(slot);
                    Plugin.Log.LogInfo($"[开始菜单] 删除了存档 {slot}「{RowName(row)}」");
                }
                else Notice(Lang.T("删除失败：存档没能清除。"));
                Refresh();
            });
        }

        // ------------------------------------------------------------------ renaming a world on the list

        static int renaming;
        static string renameDraft = "";
        static readonly ImeTextEntry renameInput = new ImeTextEntry(RenameTyped);
        /// <summary>The frame typing ended: its Enter or Esc does nothing else.</summary>
        static int typingEnded = -10;

        /// <summary>A name is being typed (the list's rename or the world creation's name): keys are the text's.</summary>
        static bool Typing => renaming != 0 || nameTyping || Time.frameCount - typingEnded <= 1;

        const int MaxNameLength = 27;

        static void BeginRename(Row row)
        {
            EndRename(true);
            renaming = row.Slot;
            renameDraft = RowName(row);
            renameInput.Begin();
        }

        static void RenameTyped(char c)
        {
            if (renaming == 0 || char.IsControl(c)) return;
            if (renameDraft.Length < MaxNameLength) renameDraft += c;
        }

        /// <summary>The rename ended: kept (Enter, a click elsewhere) or dropped (Esc).</summary>
        static void EndRename(bool keep)
        {
            if (renaming == 0) return;
            int slot = renaming;
            renaming = 0;
            renameInput.End();
            typingEnded = Time.frameCount;
            string name = (renameDraft ?? "").Trim();
            if (!keep || name.Length == 0) return;
            var row = rows.Find(r => r.Slot == slot);
            if (row == null || name == RowName(row)) return;
            if (Save.ModSave.Rename(slot, name)) row.Head = Save.ModSave.HeaderOf(slot);
            else
            {
                menuFile.Names[slot] = name;
                SaveMenuFile();
            }
        }

        /// <summary>Each frame: Backspace, Enter and Esc of whichever name is being typed.</summary>
        static void TickTyping()
        {
            if (renaming == 0 && !nameTyping) return;
            var input = renaming != 0 ? renameInput : nameInput;
            input.Tick();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || input.EditingComposition) return;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame, esc = kb.escapeKey.wasPressedThisFrame;
            if (renaming != 0)
            {
                if (kb.backspaceKey.wasPressedThisFrame && renameDraft.Length > 0) renameDraft = ImeTextEntry.Backspace(renameDraft);
                if (enter) EndRename(true);
                else if (esc) EndRename(false);
            }
            else
            {
                if (kb.backspaceKey.wasPressedThisFrame && worldName.Length > 0) worldName = ImeTextEntry.Backspace(worldName);
                if (enter || esc) EndNameTyping();
            }
        }
    }
}
