using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's SmartInteractSystem (Player.SmartInteractLookup): with the smart cursor on and nothing held on the cursor,
    /// of what a right click works on within reach the one nearest the mouse is picked, and a right click anywhere goes to
    /// it. The providers in their order: the projectiles (Chester), the townsfolk (NPCSmartInteractCandidateProvider: the
    /// hitbox's distance from the mouse), the tiles (TileSmartInteractCandidateProvider: chests, the stations that do
    /// something or craft, lit pylons; their cell whose middle is nearest the mouse, then the mouse's distance from that
    /// cell). One the mouse is on ends the search; else the least distance wins, an earlier provider on a tie. None while
    /// the mouse is on a torch or a candle (BlockBecauseYouAreOverAnImportantTile, TileID.Sets.DisableSmartInteract).
    /// The tiles in reach are outlined grey, the picked one gold (TileDrawing's highlight).
    /// </summary>
    internal static class SmartInteract
    {
        public enum Kind : byte { None, Chester, Npc, Chest, Station, Pylon }

        /// <summary>What a right click goes to now.</summary>
        public static Kind Picked { get; private set; }

        /// <summary>The query ran this frame (the smart cursor on, not blocked): the tiles in reach show their outlines.</summary>
        public static bool Showing { get; private set; }

        public static int DevUsed;

        /// <summary>Dev: the mouse SmartInteract looks from (the autotest has no real one).</summary>
        public static Vector2? DevMouse;

        /// <summary>Each frame of play, before the right click is read.</summary>
        public static void Tick(HeroController hero, Vector2 mouse, bool smartCursor)
        {
            Picked = Kind.None;
            Showing = false;
            var mod = TerraMod.Instance;
            bool run = smartCursor && hero != null && mod != null && !InventoryGui.MouseOverUi && !UI.IngameOptions.MouseOver
                       && Net.WorldSync.CanInteract && mod.Inventory.Mouse.IsEmpty && !Blocked(mouse);
            float best = float.MaxValue;
            var kind = Kind.None;
            if (run)
            {
                Showing = true;
                if (Minions.SmartChester(mouse, out float dc)) { best = dc; kind = Kind.Chester; }
                if (best > 0f && Town.TownNpcs.SmartCandidate(hero, mouse, out float dn) && dn < best) { best = dn; kind = Kind.Npc; }
                if (best > 0f)
                {
                    // The tiles together: the cell nearest the mouse of all of them.
                    float centre = float.MaxValue, rect = float.MaxValue;
                    var tile = Kind.None;
                    if (Chests.SmartCandidate(hero, mouse, out float c1, out float r1) && c1 < centre) { centre = c1; rect = r1; tile = Kind.Chest; }
                    if (Stations.SmartCandidate(hero, mouse, out float c2, out float r2) && c2 < centre) { centre = c2; rect = r2; tile = Kind.Station; }
                    if (Pylons.SmartCandidate(hero, mouse, out float c3, out float r3) && c3 < centre) { centre = c3; rect = r3; tile = Kind.Pylon; }
                    if (tile != Kind.None && rect < best) { best = rect; kind = tile; }
                }
            }
            Picked = kind;
            Chests.SmartWin(kind == Kind.Chest);
            Stations.SmartWin(kind == Kind.Station);
            Pylons.SmartWin(kind == Kind.Pylon);
            Town.TownNpcs.SmartWin(kind == Kind.Npc);
        }

        /// <summary>A right click that nothing under the cursor took: the picked one's use. True when taken.</summary>
        public static bool TryUse()
        {
            bool used;
            switch (Picked)
            {
                case Kind.Chester: used = Minions.TrySmartChester(); break;
                case Kind.Npc: used = Town.TownNpcs.TrySmartTalk(); break;
                case Kind.Chest: used = Chests.TrySmartOpen(); break;
                case Kind.Station: used = Stations.TrySmartUse(); break;
                case Kind.Pylon: used = Pylons.TrySmartUse(); break;
                default: used = false; break;
            }
            if (used) DevUsed++;
            return used;
        }

        /// <summary>TileID.Sets.DisableSmartInteract under the mouse: a torch, a candle.</summary>
        static bool Blocked(Vector2 mouse)
        {
            var cell = CellGrid.WorldToCell(mouse);
            return TerraWorld.Get(cell.x, cell.y) == CellKind.Torch || Stations.DisablesSmartInteractAt(cell);
        }

        /// <summary>
        /// TileSmartInteractCandidateProvider's measure for a thing of whole tiles: of its cells the one whose middle is
        /// nearest the mouse (centre, how they are compared) and the mouse's distance from that cell (rect, 0 on it).
        /// </summary>
        public static void NearestCell(Rect box, Vector2 mouse, out float centre, out float rect)
        {
            const float c = CellGrid.CellSize;
            var lo = CellGrid.WorldToCell(box.min + Vector2.one * 0.01f);
            var hi = CellGrid.WorldToCell(box.max - Vector2.one * 0.01f);
            centre = float.MaxValue;
            rect = float.MaxValue;
            for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                {
                    Vector2 mid = CellGrid.CellCenter(x, y);
                    float d = Vector2.Distance(mid, mouse);
                    if (d > centre) continue;
                    centre = d;
                    var cell = new Rect(mid.x - c / 2f, mid.y - c / 2f, c, c);
                    float dx = Mathf.Max(cell.xMin - mouse.x, 0f, mouse.x - cell.xMax), dy = Mathf.Max(cell.yMin - mouse.y, 0f, mouse.y - cell.yMax);
                    rect = Mathf.Sqrt(dx * dx + dy * dy);
                }
        }

        /// <summary>A box's distance from the mouse (0 with the mouse in it): Rectangle.Distance.</summary>
        public static float BoxDistance(Rect box, Vector2 mouse)
        {
            float dx = Mathf.Max(box.xMin - mouse.x, 0f, mouse.x - box.xMax), dy = Mathf.Max(box.yMin - mouse.y, 0f, mouse.y - box.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Main.mouseTextColor's pulse (190..255 and back, a step a tick) as TileDrawing's highlight: gold on the pick, grey on the rest.</summary>
        public static Color OutlineColor(bool picked)
        {
            float pulse = 190f + 65f * Mathf.PingPong(Time.unscaledTime * 60f / 65f, 1f);
            float n = (pulse * 2f + 255f) / 3f / 255f;
            return picked ? new Color(n, n, n / 3f, n) : new Color(n / 2f, n / 2f, n / 2f, n);
        }
    }
}
