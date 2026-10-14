using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Main.DrawPVPIcons in a LAN game, left of the equipment: the crossed swords (UI/PVP_0, off or
    /// on, in the team's colour) to turn PvP on and off, and under it the six team shields
    /// (UI/PVP_1: none, red, green, blue, yellow, pink), with Terraria's hover lines
    /// (UI.PvpIcon_*, UI.TeamIcon*). A click goes after a short cooldown (Main.teamCooldown).
    /// </summary>
    internal static partial class InventoryGui
    {
        static float teamCooldownUntil;
        static readonly string[] teamNames = { "None", "Red", "Green", "Blue", "Yellow", "Pink" };

        static void DrawPvpIcons(float ax, float ay, float step, float ui, Vector2 mouse)
        {
            if (!Net.NetSession.Active) return;
            Texture2D swords, shields;
            try
            {
                swords = TerrariaAssets.Texture("UI/PVP_0");
                shields = TerrariaAssets.Texture("UI/PVP_1");
            }
            catch (System.Exception)
            {
                return;
            }
            bool cooling = Time.unscaledTime < teamCooldownUntil;
            var prev = GUI.color;
            // The swords: frame (on?, team) of a 2×7 sheet; the last row is the hover rim.
            float fw = swords.width / 2f, fh = swords.height / 7f;
            var r = new Rect(ax - 92f * ui, ay + step * 2.4f, fw * ui, fh * ui);
            Add(r, Kind.Pvp, 0);
            bool over = r.Contains(mouse);
            int col = Net.NetSession.Pvp ? 1 : 0, row = Net.NetSession.Team;
            GUI.color = new Color(1f, 1f, 1f, cooling ? 0.25f : 1f);
            GUI.DrawTextureWithTexCoords(r, swords, new Rect(col * fw / swords.width, 1f - (row + 1) * fh / swords.height, fw / swords.width, fh / swords.height));
            if (over && !cooling)
            {
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(r, swords, new Rect(col * fw / swords.width, 1f - 7f * fh / swords.height, fw / swords.width, fh / swords.height));
                DrawLines(mouse, new List<string> { TerrariaText.Get(Lang.Culture, Net.NetSession.Pvp ? "UI.PvpIcon_Active" : "UI.PvpIcon_Inactive") ?? "PvP" }, Color.white);
            }
            // The shields, two to a row.
            float sw = shields.width / 6f, sh = shields.height;
            for (int i = 0; i < 6; i++)
            {
                var s = new Rect(ax - 98f * ui + (i % 2) * 20f * ui, ay + step * 2.4f + 60f * ui + (i / 2) * 20f * ui, sw * ui, sh * ui);
                Add(s, Kind.Team, i);
                bool on = Net.NetSession.Team == i;
                GUI.color = new Color(1f, 1f, 1f, cooling ? 0.25f : on ? 1f : 0.6f);
                GUI.DrawTextureWithTexCoords(s, shields, new Rect(i * sw / shields.width, 0f, sw / shields.width, 1f));
                if (s.Contains(mouse) && !cooling)
                    DrawLines(mouse, new List<string> { TerrariaText.Get(Lang.Culture, "UI.TeamIcon" + teamNames[i] + (on ? "_Active" : "_Inactive")) ?? teamNames[i] }, Color.white);
            }
            GUI.color = prev;
        }

        static void ClickPvp(int index, bool team)
        {
            if (Time.unscaledTime < teamCooldownUntil) return;
            // Main.teamCooldownLen: 300 ticks.
            teamCooldownUntil = Time.unscaledTime + 5f;
            Sound("Menu_Tick");
            if (team) Net.NetSession.SetTeam((byte)index);
            else Net.NetSession.SetPvp(!Net.NetSession.Pvp);
        }
    }
}
