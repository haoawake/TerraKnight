using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's housing menu, as Hallownest has it. Terraria gives it the equipment's own page
    /// (Main.EquipPage == 1 draws Main.DrawNPCHousesInUI in the armour and accessories' place, at their x,
    /// in Inventory_Back11 frames, the wheel's PvP icons hidden), so the button here sits with the
    /// inventory's other buttons and takes the equipment column over the same way: the same x, the same
    /// slot size, the same spacing, one townsperson's head to a slot, the list walking down the column and
    /// then starting another column 48 px to the left (up to four, as Terraria's does), and the armour,
    /// defense, loadouts, accessories and their page icon nowhere to be seen while it is up. A left click
    /// on a head chooses it to be put down — on a bench, or at its own place in the street — and a right
    /// click sends that townsperson away. Only the shopkeepers and the few with something to do move in
    /// on their own.
    /// </summary>
    internal static partial class InventoryGui
    {
        static Rect NpcButtonRect(Rect view, float ui) =>
            new Rect(view.x + RightOfAmmo(ui), HkButtonRect(view, ui).yMax + 28f * ui, 90f * ui, 24f * ui);

        static void DrawNpcButton(Rect view, float ui, Vector2 mouse)
        {
            var r = NpcButtonRect(view, ui);
            bool over = r.Contains(mouse);
            Outlined(r, Lang.T("房屋"), labelStyle, over || TownNpcs.PanelOpen ? new Color(1f, 0.85f, 0.3f) : Color.white);
            Add(r, Kind.NpcButton, 0);
        }

        static void ToggleNpcPanel()
        {
            if (!TownNpcs.PanelOpen)
            {
                bestiaryOpen = false;
                hkOpen = false;
            }
            TownNpcs.TogglePanel();
        }

        /// <summary>
        /// The housing list, in the place the equipment column stands: one head a slot of the equipment's
        /// own size, down the column to the bottom of the screen and then a fresh column 48 px to the left
        /// (Terraria's own does this while num3 walks left by 48: DrawNPCHousesInUI), four of them at the
        /// most, the wheel moving the window along the roster when even those don't hold it.
        /// </summary>
        static void DrawNpcColumn(float ax, float ay, float slot, float step, Rect view, float ui, Vector2 mouse)
        {
            var all = TownNpcs.AllDefs;
            if (all == null || all.Length == 0) return;
            Outlined(new Rect(ax - 40f * ui, ay - 24f * ui, slot + 80f * ui, 20f * ui), Lang.T("房屋"), labelStyle, Color.white);
            // As far down as a slot may go before the next column starts: Terraria wraps at
            // screenHeight - 80, which here is also clear of the bottom-right settings buttons.
            int rows = Mathf.Max(1, (int)((view.yMax - 100f * ui - ay) / step));
            const int maxColumns = 4;
            int capacity = rows * maxColumns;
            npcScroll = Mathf.Clamp(npcScroll, 0, Mathf.Max(0, all.Length - capacity));
            int shown = Mathf.Min(all.Length - npcScroll, capacity);
            for (int i = 0; i < shown; i++)
            {
                int index = npcScroll + i;
                var rect = new Rect(ax - i / rows * 48f * ui, ay + i % rows * step, slot, slot);
                bool over = rect.Contains(mouse);
                bool alive = TownNpcs.Lives(all[index].Internal);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back11"));
                if (TownNpcs.PlacingName != null && TownNpcs.PlacingName == TownNpcs.DisplayName(all[index]))
                    Rim(rect, new Color(1f, 0.85f, 0.3f), 2f * ui);
                DrawNpcHead(rect, all[index], alive ? 1f : 0.42f);
                if (over)
                {
                    Fill(new Rect(rect.x, rect.y, rect.width, rect.height), new Color(1f, 1f, 1f, 0.12f));
                    var lines = new List<string> { TerrariaText.NpcName(Lang.Culture, all[index].Id) ?? all[index].Zh };
                    if (TownNpcs.Dismissed.Contains(all[index].Internal)) lines.Add(Lang.T("已经请走了，不会自己回来：左键选中再放下就回来"));
                    else if (TownNpcs.Present(all[index].Internal)) lines.Add(Lang.T("在德特茅斯：右键请它离开"));
                    else if (alive) lines.Add(Lang.T("已入住，现在不在"));
                    else lines.Add(Lang.T("还没来：左键选中，再点一把木椅；点别处就站在长椅上"));
                    if (TownNpcs.AutoArrives(all[index])) lines.Add(Lang.T("会自己入住"));
                    DrawLines(mouse, lines, Color.white);
                }
                Add(rect, Kind.NpcHead, index);
            }
            if (all.Length > capacity)
            {
                var track = new Rect(ax + slot + 6f * ui, ay, 5f * ui, rows * step);
                Fill(track, new Color(0f, 0f, 0f, 0.35f));
                float span = capacity / (float)all.Length;
                Fill(new Rect(track.x, track.y + (track.height * (1f - span)) * (npcScroll / (float)Mathf.Max(1, all.Length - capacity)), track.width, track.height * span), new Color(0.75f, 0.85f, 1f, 0.85f));
            }
        }

        /// <summary>The housing list's first head (the wheel moves it, when the roster outgrows four columns).</summary>
        static int npcScroll;

        /// <summary>
        /// A townsperson's head: Terraria's own housing-menu sprite for it (NPCHeadID); for the one or two
        /// it hasn't got (the Skeleton Merchant lives in caves and can't be housed in Terraria, so it has
        /// no head there) a stand-in of its own (HeadStandIns), and failing that the head end of the first
        /// standing frame of its own sheet, cropped — the top of the frame, which is where a standing
        /// sprite keeps its head (a sprite's bottom is the feet, and Terraria draws textures bottom-up).
        /// </summary>
        static void DrawNpcHead(Rect r, NpcDef def, float alpha)
        {
            var tex = NpcHeadTexture(def.Internal);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha) * prev.a;
            // Terraria draws the head centred in the slot, 36 px at the most (DrawNPCHousesInUI:
            // scale = 36 / max(w, h), drawn at the slot's centre). Its inventory is a fixed pixel size
            // and the head sprite keeps its own pixels there; this mod's UI is scaled to the screen, so
            // the head is scaled with it too — otherwise a 24 px head sits tiny in a 88 px slot at 4K.
            // 36 is Terraria's cap against its 52 px slot: against this 44 px one that is 30.
            float ui = UI.GameView.Scale;
            if (tex != null)
            {
                float k = ui * Mathf.Min(1f, 30f / Mathf.Max(tex.width, tex.height));
                float w = tex.width * k, h = tex.height * k;
                GUI.DrawTexture(new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h), tex);
            }
            else
            {
                var inner = new Rect(r.x + 6f * ui, r.y + 6f * ui, r.width - 12f * ui, r.height - 12f * ui);
                var body = TerrariaAssets.Texture(TownNpcs.BodyTexture(def));
                if (body != null)
                {
                    float fh = body.height / (float)Mathf.Max(1, TownNpcs.BodyFrames(def));
                    float part = Mathf.Min(fh, fh * 0.5f) / body.height;
                    GUI.DrawTextureWithTexCoords(inner, body, new Rect(0.05f, 1f - part, 0.9f, part));
                }
                else Plain(inner, def.Internal.Length > 0 ? def.Internal.Substring(0, 1) : "?", chipStyle, Color.white);
            }
            GUI.color = prev;
        }

        /// <summary>The head sprite, cached: Terraria's Images/NPC_Head_&lt;NPCHeadID&gt;.xnb.</summary>
        static readonly Dictionary<string, Texture2D> npcHeads = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The few townsfolk with no head of their own in Terraria's housing menu (NPCHeadID), given a
        /// picture of Terraria's instead: the Skeleton Merchant is not a townsperson there at all (it
        /// lives in caves), so it takes the game's own Skull item (ItemID 1274) for a head.
        /// </summary>
        static readonly Dictionary<string, string> headStandIns = new Dictionary<string, string>
        {
            ["SkeletonMerchant"] = "Item_1274",
        };

        static Texture2D NpcHeadTexture(string internalName)
        {
            if (npcHeads.TryGetValue(internalName, out var cached)) return cached;
            Texture2D tex = null;
            int index = TerrariaText.HeadIndex(internalName);
            if (headStandIns.TryGetValue(internalName, out var standIn))
            {
                try { tex = TerrariaAssets.Texture(standIn); }
                catch (System.Exception) { tex = null; }
            }
            else if (index >= 0)
            {
                try { tex = TerrariaAssets.Texture("NPC_Head_" + index); }
                catch (System.Exception) { tex = null; }
            }
            npcHeads[internalName] = tex;
            return tex;
        }
    }
}
