using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The charm page: the equipment column's third page (its button beside housing's), Hollow Knight's charm menu laid out
    /// in Terraria's slots and tooltips, right-aligned with the equipment column. On top, the charms worn in the order they
    /// went on (and an open slot while a notch is free) and the notches, lit as they are filled and pink when overcharmed;
    /// under them the forty in the game's order, an empty slot for one not found, a worn one dimmed, each with its cost in
    /// notches in the corner. A click puts a charm on or takes it off, only while resting on a bench (Charms).
    /// </summary>
    internal static partial class InventoryGui
    {
        const int CharmPage = 3, CharmColumns = 10, CharmRows = 4;
        static readonly Color OverchargeColor = new Color(1f, 0.45f, 0.75f);
        /// <summary>The worn row's slots: the equipment slot's picture in a slate of their own (charms belong to no loadout).</summary>
        static readonly Color CharmWornBack = new Color(0.45f, 0.47f, 0.72f, 0.86f);

        /// <summary>The page's button: the notch's picture (the one Salubra sells), brighter while the page is open.</summary>
        static void CharmPageIcon(Vector2 at, float ui, Vector2 mouse, bool open)
        {
            var like = TerrariaAssets.Texture("UI/DisplaySlots_4");
            var r = new Rect(at.x, at.y, like.width * 0.9f * ui, like.height * 0.9f * ui);
            Add(r, Kind.EquipPage, 2);
            bool over = r.Contains(mouse);
            var icon = Charms.NotchIcon;
            if (icon != null)
            {
                float k = Mathf.Min(r.width / icon.width, r.height / icon.height) * (over ? 1.08f : 1f);
                float w = icon.width * k, h = icon.height * k;
                var prev = GUI.color;
                GUI.color = over ? new Color(1f, 0.9f, 0.55f) : open ? Color.white : new Color(0.8f, 0.8f, 0.8f);
                GUI.DrawTexture(new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h), icon);
                GUI.color = prev;
            }
            if (over) DrawLines(mouse, new List<string> { Lang.T(open ? "装备" : "护符") }, Color.white);
        }

        static void DrawCharmPage(float ax, float ay, float slot, float step, float ui, Vector2 mouse)
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            float right = ax + slot, gridW = (CharmColumns - 1) * step + slot, x0 = right - gridW;
            Outlined(new Rect(x0, ay - 24f * ui, 200f * ui, 20f * ui), Lang.T("护符"), labelStyle, Color.white);

            // The charms worn, and an open slot where the next goes while a notch is free.
            var worn = Charms.WornList;
            var shown = worn != null ? new List<int>(worn) : new List<int>();
            // The one being taken off keeps its place while it trembles and comes out (DrawRemoving).
            bool ghost = removing != null && Time.unscaledTime - removing.Start < RemoveShake + RemovePop && !shown.Contains(removing.Id);
            if (ghost) shown.Insert(Mathf.Clamp(removing.Index, 0, shown.Count), removing.Id);
            int n = shown.Count;
            int notches = Charms.Notches, filled = Charms.Filled;
            bool over = Charms.Overcharmed;
            int cells = Mathf.Max(1, n + (filled < notches ? 1 : 0));
            float y = ay;
            Rect? setInto = null, takeFrom = null, takeTo = null;
            wornRectOf.Clear();
            for (int i = 0; i < cells; i++)
            {
                var rect = new Rect(x0 + (i % CharmColumns) * step, y + (i / CharmColumns) * step, slot, slot);
                var prev = GUI.color;
                GUI.color = i < n ? CharmWornBack : new Color(CharmWornBack.r, CharmWornBack.g, CharmWornBack.b, 0.45f);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back13"));
                GUI.color = prev;
                if (i < n)
                {
                    int id = shown[i];
                    if (ghost && id == removing.Id)
                    {
                        takeFrom = rect;
                        panels.Add(rect);
                        continue;
                    }
                    Add(rect, Kind.CharmWorn, worn.IndexOf(id));
                    wornRectOf[id] = rect;
                    // The one being put on is drawn on its way in, over the page (DrawEquipping).
                    if (equipping != null && id == equipping.Id) setInto = rect;
                    else DrawCharm(rect, id, 1f, ui);
                    if (rect.Contains(mouse)) DrawCharmTip(mouse, id);
                }
                else
                {
                    panels.Add(rect);
                    if (rect.Contains(mouse)) DrawLines(mouse, new List<string> { Lang.F("还剩 {0} 个凹槽", notches - filled) }, Color.white);
                }
            }
            y += ((cells - 1) / CharmColumns + 1) * step + 2f * ui;

            // The notches (UI Charms' Notches: lit as filled; all pink when overcharmed), then their count; lit or put out
            // one at a time as a charm goes on or comes off (NotchesShown).
            float open = 18f * ui, gap = 22f * ui;
            int lit = NotchesShown(filled);
            bool overShown = lit > notches;
            for (int i = 0; i < notches; i++)
                DrawNotch(new Vector2(x0 + open / 2f + i * gap, y + open / 2f + 2f * ui), i < lit || overShown, overShown, open, NotchChange(i), ui);
            string count = (overShown ? Charms.OvercharmedWord : Charms.NotchesWord) + "  " + lit + "/" + notches;
            Outlined(new Rect(x0 + notches * gap + 8f * ui, y + 1f * ui, 220f * ui, 20f * ui), count, labelStyle, overShown ? OverchargeColor : Color.white);
            y += open + 12f * ui;

            // The forty, in the game's order.
            for (int id = 1; id <= Charms.Count; id++)
            {
                int k = id - 1;
                var rect = new Rect(x0 + (k % CharmColumns) * step, y + (k / CharmColumns) * step, slot, slot);
                Add(rect, Kind.CharmSlot, id);
                bool got = Charms.Got(id);
                var prev = GUI.color;
                if (!got) GUI.color = new Color(prev.r, prev.g, prev.b, 0.55f * prev.a);
                DrawSlot(rect, default, "Inventory_Back", false);
                GUI.color = prev;
                // One on its way back stays dimmed until it lands.
                bool back = removing != null && removing.Id == id;
                if (back) takeTo = rect;
                bool coming = back && Time.unscaledTime - removing.Start < RemoveShake + RemovePop + RemoveFly;
                if (got) DrawCharm(rect, id, Charms.Worn(id) || coming ? 0.35f : 1f, ui);
                if (rect.Contains(mouse))
                {
                    if (got) DrawCharmTip(mouse, id);
                    else DrawLines(mouse, new List<string> { Lang.T("未获得的护符") }, new Color(0.7f, 0.7f, 0.7f));
                }
            }
            y += CharmRows * step;
            Outlined(new Rect(x0, y + 2f * ui, gridW, 20f * ui), Lang.T(Charms.CanChange ? "点击护符装上或卸下" : "坐在长椅上才能更换护符"), labelStyle,
                Charms.CanChange ? new Color(0.85f, 0.85f, 0.95f, 0.85f) : new Color(1f, 0.75f, 0.55f));
            // The page as a whole is on the interface (no item use through its gaps).
            panels.Add(new Rect(x0 - 6f * ui, ay - 28f * ui, gridW + 12f * ui, y + 26f * ui - (ay - 28f * ui)));
            if (equipping != null) DrawEquipping(setInto, ui);
            if (removing != null) DrawRemoving(takeFrom, takeTo, ui);
        }

        /// <summary>A charm being put on: the slot it was clicked in, and when.</summary>
        sealed class EquipAnim
        {
            public int Id;
            public Rect From;
            public float Start;
            public bool Rang;
            /// <summary>Taken off: where it was in the worn row.</summary>
            public int Index;
        }

        static EquipAnim equipping, removing;
        /// <summary>The worn slots as last drawn (a charm taken off from the list trembles where it was worn).</summary>
        static readonly Dictionary<int, Rect> wornRectOf = new Dictionary<int, Rect>();
        /// <summary>Taken off: the tremble in its worn slot, the pull out of it, the flight back to the list, the landing.</summary>
        const float RemoveShake = 0.2f, RemovePop = 0.1f, RemoveFly = 0.3f, RemoveLand = 0.1f;
        /// <summary>The flight into the worn slot, the tremble there, the press as it sets in, and its white glow after.</summary>
        const float CharmFly = 0.3f, CharmShake = 0.22f, CharmSet = 0.12f, CharmGlow = 0.32f;
        static Texture2D charmGlow;
        static readonly Dictionary<Texture2D, Texture2D> charmWhite = new Dictionary<Texture2D, Texture2D>();

        /// <summary>
        /// The charm put on (under a second): it flies from its slot in the list into its worn slot, rising a little and
        /// swelling on the way; it trembles there, then presses in and sets with a soft white glow (its picture flashed
        /// white, a halo behind) and a light ring.
        /// </summary>
        static void DrawEquipping(Rect? target, float ui)
        {
            var anim = equipping;
            var icon = Charms.Icon(anim.Id);
            if (target == null || icon == null)
            {
                equipping = null;
                return;
            }
            var to = target.Value;
            float t = Time.unscaledTime - anim.Start, setAt = CharmFly + CharmShake;
            if (t >= setAt + CharmGlow)
            {
                equipping = null;
                DrawCharm(to, anim.Id, 1f, ui);
                return;
            }
            Vector2 c;
            float scale = 1f, angle = 0f;
            if (t < CharmFly)
            {
                float p = Mathf.Clamp01(t / CharmFly), e = 1f - (1f - p) * (1f - p) * (1f - p);
                c = Vector2.Lerp(anim.From.center, to.center, e) + new Vector2(0f, -Mathf.Sin(Mathf.PI * p) * 16f * ui);
                scale = 1f + 0.18f * Mathf.Sin(Mathf.PI * p);
            }
            else if (t < setAt)
            {
                float q = (t - CharmFly) / CharmShake, fade = 1f - q;
                c = to.center + new Vector2(Mathf.Sin(q * Mathf.PI * 9f) * 2.2f * ui * fade, Mathf.Cos(q * Mathf.PI * 7f) * 1.2f * ui * fade);
                angle = Mathf.Sin(q * Mathf.PI * 8f) * 6f * fade;
            }
            else
            {
                c = to.center;
                scale = 1f - 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - setAt) / CharmSet));
                if (!anim.Rang)
                {
                    anim.Rang = true;
                    TerraMod.Instance?.PlaySound("Item_37", 0.3f);
                }
            }
            float g = t < setAt ? 0f : (t - setAt) / CharmGlow;
            float glow = g <= 0f ? 0f : g < 0.25f ? g / 0.25f : Mathf.Clamp01((1f - g) / 0.75f);
            var prev = GUI.color;
            if (glow > 0f)
            {
                float hs = to.width * 1.9f;
                GUI.color = new Color(1f, 1f, 1f, 0.55f * glow);
                GUI.DrawTexture(new Rect(to.center.x - hs / 2f, to.center.y - hs / 2f, hs, hs), CharmGlowTexture());
                GUI.color = prev;
            }
            float fit = Mathf.Min(to.width * 0.8f / icon.width, to.height * 0.8f / icon.height) * scale;
            float w = icon.width * fit, h = icon.height * fit;
            var r = new Rect(c.x - w / 2f, c.y - h / 2f, w, h);
            var matrix = GUI.matrix;
            if (angle != 0f) GUIUtility.RotateAroundPivot(angle, c);
            GUI.DrawTexture(r, icon);
            if (glow > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.75f * glow);
                GUI.DrawTexture(r, WhiteCharm(icon));
                GUI.color = prev;
            }
            GUI.matrix = matrix;
        }

        /// <summary>
        /// The charm taken off (under a second): it trembles in its worn slot, which keeps its place meanwhile, is pulled
        /// out (rising a little, swelling), flies back along an arc into its slot in the list and lands with a small press.
        /// </summary>
        static void DrawRemoving(Rect? from, Rect? to, float ui)
        {
            var anim = removing;
            var icon = Charms.Icon(anim.Id);
            if (to == null || icon == null || Charms.Worn(anim.Id))
            {
                removing = null;
                return;
            }
            // Where its worn slot is now (the row as drawn); once out of it, where it was.
            if (from != null) anim.From = from.Value;
            float t = Time.unscaledTime - anim.Start;
            float flyAt = RemoveShake + RemovePop, landAt = flyAt + RemoveFly;
            if (t >= landAt + RemoveLand)
            {
                removing = null;
                return;
            }
            var lift = new Vector2(0f, -7f * ui);
            Vector2 c;
            float scale = 1f, angle = 0f;
            if (t < RemoveShake)
            {
                float q = t / RemoveShake, fade = 1f - 0.5f * q;
                c = anim.From.center + new Vector2(Mathf.Sin(q * Mathf.PI * 8f) * 2.2f * ui * fade, Mathf.Cos(q * Mathf.PI * 6f) * 1.2f * ui * fade);
                angle = Mathf.Sin(q * Mathf.PI * 7f) * 6f * fade;
            }
            else if (t < flyAt)
            {
                float p = (t - RemoveShake) / RemovePop, e = 1f - (1f - p) * (1f - p);
                c = anim.From.center + lift * e;
                scale = 1f + 0.15f * e;
            }
            else if (t < landAt)
            {
                float q = (t - flyAt) / RemoveFly;
                float e = q < 0.5f ? 4f * q * q * q : 1f - Mathf.Pow(-2f * q + 2f, 3f) / 2f;
                c = Vector2.Lerp(anim.From.center + lift, to.Value.center, e) + new Vector2(0f, -Mathf.Sin(Mathf.PI * q) * 14f * ui);
                scale = 1.15f - 0.15f * e;
            }
            else
            {
                c = to.Value.center;
                scale = 1f - 0.08f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - landAt) / RemoveLand));
            }
            float fit = Mathf.Min(to.Value.width * 0.8f / icon.width, to.Value.height * 0.8f / icon.height) * scale;
            float w = icon.width * fit, h = icon.height * fit;
            var matrix = GUI.matrix;
            if (angle != 0f) GUIUtility.RotateAroundPivot(angle, c);
            GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - h / 2f, w, h), icon);
            GUI.matrix = matrix;
        }

        /// <summary>A soft white halo: full in the middle, gone at the edge.</summary>
        static Texture2D CharmGlowTexture()
        {
            if (charmGlow != null) return charmGlow;
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f));
                    float a = (1f - d) * (1f - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            charmGlow = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "TerraKnight/charm_glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            charmGlow.SetPixels32(px);
            charmGlow.Apply(false, true);
            return charmGlow;
        }

        /// <summary>The charm's shape in white (its flash as it sets in).</summary>
        static Texture2D WhiteCharm(Texture2D icon)
        {
            if (charmWhite.TryGetValue(icon, out var white) && white != null) return white;
            Color32[] px;
            try { px = icon.GetPixels32(); }
            catch (UnityException) { return icon; }
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a);
            white = new Texture2D(icon.width, icon.height, TextureFormat.RGBA32, false) { name = icon.name + "#white", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            white.SetPixels32(px);
            white.Apply(false, true);
            charmWhite[icon] = white;
            return white;
        }

        /// <summary>The notches as lit on the page: from, to, and when the first of them changes.</summary>
        static int notchFrom = -1, notchTo = -1;
        static float notchAt;
        /// <summary>One notch after another, and how long each one's light swells (lit) or fades (put out).</summary>
        const float NotchStep = 0.07f, NotchFlash = 0.18f;

        /// <summary>How many notches show lit now: the count stepping one at a time toward the filled one after a change.</summary>
        static int NotchesShown(int filled)
        {
            // A change the page didn't make (loading, the game's own): as it is.
            if (notchTo != filled) notchFrom = notchTo = filled;
            int span = Mathf.Abs(notchTo - notchFrom);
            float t = Time.unscaledTime - notchAt;
            if (span == 0) return notchTo;
            if (t < 0f) return notchFrom;
            int k = Mathf.Min(span, Mathf.FloorToInt(t / NotchStep) + 1);
            return notchFrom + (notchTo > notchFrom ? k : -k);
        }

        /// <summary>A change to step through: from the count shown now to the filled one, the first notch after a delay.</summary>
        static void StepNotches(int from, int to, float delay)
        {
            notchFrom = from;
            notchTo = to;
            notchAt = Time.unscaledTime + delay;
        }

        /// <summary>How fresh a notch's change is (1 just lit or put out, 0 long since or untouched).</summary>
        static float NotchChange(int i)
        {
            int span = Mathf.Abs(notchTo - notchFrom);
            if (span == 0) return 0f;
            int j = notchTo > notchFrom ? i - notchFrom : notchFrom - 1 - i;
            if (j < 0 || j >= span) return 0f;
            float since = Time.unscaledTime - notchAt - j * NotchStep;
            return since < 0f || since >= NotchFlash ? 0f : 1f - since / NotchFlash;
        }

        /// <summary>
        /// A notch's picture centred on a point: the open one at the given size, the lit one (its glow larger) to scale. Just
        /// lit, it swells a little with a soft white glow; just put out, its light fades and shrinks over the open one.
        /// </summary>
        static void DrawNotch(Vector2 c, bool lit, bool over, float open, float change, float ui)
        {
            var litTex = Charms.NotchLit;
            var tex = lit ? litTex : Charms.NotchOpen;
            if (tex == null) return;
            // The lit picture is 46 px to the open one's 28, the same notch with its glow.
            float s = lit ? open * 46f / 28f : open;
            if (lit) s *= 1f + 0.35f * change;
            float w = s * tex.width / tex.height;
            var prev = GUI.color;
            if (lit && change > 0f)
            {
                float hs = open * 2.4f;
                GUI.color = new Color(1f, 1f, 1f, 0.6f * change);
                GUI.DrawTexture(new Rect(c.x - hs / 2f, c.y - hs / 2f, hs, hs), CharmGlowTexture());
                GUI.color = prev;
            }
            if (lit && over) GUI.color = OverchargeColor;
            GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - s / 2f, w, s), tex);
            GUI.color = prev;
            if (!lit && change > 0f && litTex != null)
            {
                float ls = open * 46f / 28f * (0.8f + 0.2f * change);
                float lw = ls * litTex.width / litTex.height;
                GUI.color = new Color(1f, 1f, 1f, 0.85f * change);
                GUI.DrawTexture(new Rect(c.x - lw / 2f, c.y - ls / 2f, lw, ls), litTex);
                GUI.color = prev;
            }
        }

        /// <summary>The charm's picture in a slot (faded when worn, as the game dims a worn one in the list), its cost in the corner.</summary>
        static void DrawCharm(Rect rect, int id, float alpha, float ui)
        {
            var icon = Charms.Icon(id);
            var prev = GUI.color;
            if (icon != null)
            {
                float fit = Mathf.Min(rect.width * 0.8f / icon.width, rect.height * 0.8f / icon.height);
                float w = icon.width * fit, h = icon.height * fit;
                GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * alpha);
                GUI.DrawTexture(new Rect(rect.center.x - w / 2f, rect.center.y - h / 2f, w, h), icon);
            }
            GUI.color = prev;
            int cost = Charms.Cost(id);
            var lit = Charms.NotchLit;
            if (cost <= 0 || lit == null) return;
            float s = 12f * ui, step = 7f * ui;
            for (int i = 0; i < cost; i++)
            {
                var c = new Vector2(rect.xMax - 6f * ui - i * step, rect.yMax - 6f * ui);
                GUI.color = new Color(1f, 1f, 1f, prev.a * Mathf.Max(alpha, 0.6f));
                GUI.DrawTexture(new Rect(c.x - s / 2f, c.y - s / 2f, s, s), lit);
            }
            GUI.color = prev;
        }

        static readonly Dictionary<string, List<string>> wrapped = new Dictionary<string, List<string>>();

        /// <summary>A charm's tooltip: its name, its cost, the game's description, whether it is worn, and how it is changed.</summary>
        static void DrawCharmTip(Vector2 mouse, int id)
        {
            var pd = PlayerData.instance;
            var lines = new List<string> { Charms.Name(id) };
            var colors = new Dictionary<int, Color>();
            int cost = Charms.Cost(id);
            lines.Add(cost > 0 ? Lang.F("消耗 {0} 个凹槽", cost) : Lang.T("不占凹槽"));
            colors[lines.Count - 1] = new Color(0.95f, 0.85f, 0.55f);
            lines.AddRange(Wrapped(Charms.Desc(id)));
            bool worn = Charms.Worn(id);
            string state = null;
            var stateColor = new Color(0.55f, 0.95f, 0.55f);
            if (Charms.Broken(id)) { state = Lang.T("已破碎：找食腿者修复"); stateColor = new Color(1f, 0.5f, 0.45f); }
            else if (id == Charms.Royal && pd != null && pd.royalCharmState < 3) { state = Lang.T("无法装备"); stateColor = new Color(0.7f, 0.7f, 0.7f); }
            else if (worn && id == Charms.Royal && pd != null && pd.royalCharmState >= 4) state = Lang.T("已装备（无法卸下）");
            else if (worn) state = Lang.T("已装备");
            if (state != null)
            {
                lines.Add(state);
                colors[lines.Count - 1] = stateColor;
            }
            lines.Add(Lang.T(!Charms.CanChange ? "坐在长椅上才能更换" : worn ? "点击卸下" : "点击装上"));
            colors[lines.Count - 1] = Charms.CanChange ? new Color(0.75f, 0.8f, 1f) : new Color(1f, 0.75f, 0.55f);
            DrawLines(mouse, lines, Color.white, colors);
        }

        /// <summary>The description broken into lines a tooltip can hold (a word at a time, or a character for Chinese and Japanese).</summary>
        static List<string> Wrapped(string text)
        {
            float max = 520f * GameView.Scale;
            string key = tipStyle.fontSize + "|" + text;
            if (wrapped.TryGetValue(key, out var done)) return done;
            var lines = new List<string>();
            foreach (var para in text.Split('\n'))
            {
                var line = new System.Text.StringBuilder();
                int i = 0;
                while (i < para.Length)
                {
                    // A token: one wide character, or a word with the spaces after it.
                    int j = i + 1;
                    if (para[i] < 0x2E80)
                    {
                        while (j < para.Length && para[j] != ' ' && para[j] < 0x2E80) j++;
                        while (j < para.Length && para[j] == ' ') j++;
                    }
                    string token = para.Substring(i, j - i);
                    if (line.Length > 0 && tipStyle.CalcSize(new GUIContent(line + token.TrimEnd())).x > max)
                    {
                        lines.Add(line.ToString().TrimEnd());
                        line.Clear();
                        token = token.TrimStart();
                    }
                    line.Append(token);
                    i = j;
                }
                if (line.Length > 0) lines.Add(line.ToString().TrimEnd());
            }
            if (wrapped.Count > 200) wrapped.Clear();
            wrapped[key] = lines;
            return lines;
        }

        /// <summary>A click on a charm (in the list or worn): put on (flying in from where it was clicked) or taken off.</summary>
        static void ClickCharm(int id, Rect from)
        {
            var worn = Charms.WornList;
            int index = worn != null ? worn.IndexOf(id) : -1;
            // Taken off from the list: it comes out of its worn slot, not the list's.
            var wornAt = index >= 0 && wornRectOf.TryGetValue(id, out var at) ? at : from;
            int litBefore = NotchesShown(Charms.Filled);
            var answer = Charms.Click(id);
            if (answer == Charms.Answer.On || answer == Charms.Answer.Overcharmed)
            {
                equipping = new EquipAnim { Id = id, From = from, Start = Time.unscaledTime };
                if (removing != null && removing.Id == id) removing = null;
                // Lit as the charm sets in.
                StepNotches(litBefore, Charms.Filled, CharmFly + CharmShake);
            }
            else if (answer == Charms.Answer.Off)
            {
                if (equipping != null && equipping.Id == id) equipping = null;
                removing = new EquipAnim { Id = id, From = wornAt, Index = index, Start = Time.unscaledTime };
                // Put out as it trembles loose.
                StepNotches(litBefore, Charms.Filled, 0f);
            }
            switch (answer)
            {
                case Charms.Answer.On:
                case Charms.Answer.Off:
                    Sound("Grab");
                    break;
                case Charms.Answer.Overcharmed:
                    NoticeCharm(Charms.OvercharmedWord + Lang.T("：受到的伤害翻倍"), "Shatter");
                    break;
                case Charms.Answer.Crack:
                    NoticeCharm(Lang.F("凹槽不够：再点 {0} 次强行装上", Charms.TriesLeft), "Tink_0");
                    break;
                case Charms.Answer.NoNotch:
                    NoticeHk("没有空余的凹槽");
                    break;
                case Charms.Answer.NotAtBench:
                    NoticeHk("坐在长椅上才能更换护符");
                    break;
                case Charms.Answer.CantWear:
                    NoticeHk("白色碎片无法装备");
                    break;
                case Charms.Answer.CantRemove:
                    NoticeHk("虚空之心无法卸下");
                    break;
            }
        }

        static void NoticeCharm(string text, string sound)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 2f;
            Sound(sound);
        }

        /// <summary>Dev: a click on a charm in the page's list.</summary>
        public static string DevClickCharm(int id)
        {
            var answer = Charms.Click(id);
            return answer.ToString();
        }

        /// <summary>Dev: the equipment column's page (0 equipment, 2 misc, 3 charms).</summary>
        public static int DevEquipPage
        {
            get => equipPage;
            set => equipPage = value;
        }
    }
}
