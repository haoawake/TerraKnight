using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Town
{
    /// <summary>
    /// The townsfolk panel: Terraria's housing menu read in Hallownest. Every townsperson's head out
    /// of Terraria's own Images/NPC_Head_n (NPCHeadID), dim while it isn't in town and lit while it is.
    /// A left click chooses one to put down, and the next left click in Dirtmouth puts it there: on the
    /// chair the click landed on — a Wooden Chair the player set down, where it then stays, bound to that
    /// chair — or, when the click was anywhere else (the street, the air, a wall, the bench itself), at
    /// Dirtmouth's own bench, the town's default resting place (moving it when it already lives there —
    /// one of each). A right click sends it away. Only the townsfolk with
    /// something to do move in by themselves now (see InTown): the shopkeepers, the Guide's crafting,
    /// the Nurse's healing, the Angler's quests. The rest wait in the panel for the player, and nothing
    /// else about them changes: their shops, chat and wanderings are the usual ones.
    /// </summary>
    internal static partial class TownNpcs
    {
        /// <summary>Where the player put a townsperson down (internal name -> street x); saved with the town.</summary>
        public static readonly Dictionary<string, float> Homes = new Dictionary<string, float>();
        /// <summary>The same spot's height (a roof, an upper floor, the street); saved with the town.</summary>
        public static readonly Dictionary<string, float> HomeY = new Dictionary<string, float>();

        /// <summary>The one chosen to be put down, null when none is (the panel's left click).</summary>
        static string placing;

        /// <summary>A townsperson is waiting for the next click on the street.</summary>
        public static bool Placing => placing != null;
        /// <summary>What is being placed (for the panel's hint line).</summary>
        public static string PlacingName => placing == null ? null : Label(DefOf(placing));

        /// <summary>The panel is up (the inventory's townsfolk button).</summary>
        static bool panelOpen;
        public static bool PanelOpen => panelOpen;

        static NpcDef DefOf(string internalName) => System.Array.Find(All, d => d.Internal == internalName);

        /// <summary>Every townsperson, for the panel's grid.</summary>
        public static NpcDef[] AllDefs => All;

        /// <summary>Its name in the language now, for the panel.</summary>
        public static string DisplayName(NpcDef d) => Label(d);

        /// <summary>The sprite the townsperson is drawn with (for the panel's fallback head).</summary>
        public static string BodyTexture(NpcDef d) => d == null ? null : TextureOf(d);
        /// <summary>Its sheet's frames (the panel crops the head out of the first one).</summary>
        public static int BodyFrames(NpcDef d) => d == null ? 1 : FramesOf(d);

        /// <summary>Its name in the language now (Terraria's own for it, else the mod's).</summary>
        static string Label(NpcDef d) => d == null ? "" : Terraria.TerrariaText.NpcName(Lang.Culture, d.Id) ?? d.Zh;

        /// <summary>Whether one of that kind lives in Dirtmouth now (or has moved in before).</summary>
        public static bool Lives(string internalName) =>
            Arrived.Contains(internalName) || npcs.Exists(n => n.Def.Internal == internalName);

        /// <summary>Whether it is in town at this moment (it may have moved in and be away: Santa, the traveling merchant).</summary>
        public static bool Present(string internalName) => npcs.Exists(n => n.Def.Internal == internalName);

        /// <summary>Whether it would move in on its own: the shops and the few with something to do.</summary>
        public static bool AutoArrives(NpcDef def) =>
            (def.Items != null && def.Items.Length > 0) || def.Kind == NpcKind.Visitor   // the traveling merchant's stock is picked on arrival
            || def.Internal == "Guide" || def.Internal == "Nurse" || def.Internal == "Angler";

        /// <summary>
        /// A left click on the street while one is chosen: puts it there, or moves it there when it is
        /// already in town. True when this click was the panel's (so it isn't also a use of the item).
        /// </summary>
        public static bool PlaceClick(Vector2 world, HeroController hero)
        {
            // The housing query on the cursor (Main.PerformHousingCheck): Terraria's verdict on the room clicked.
            if (querying)
            {
                HousingQuery(world);
                return true;
            }
            if (placing == null) return false;
            var def = DefOf(placing);
            // Terraria's housing (Main.TryMovingNPC, TownNpcsHousing): a click in a room the player built (anywhere
            // outside Dirtmouth) moves the chosen one in at once, or says what the room lacks; Dirtmouth's open street
            // keeps the chair and the bench below.
            if (Housable(def) && HousingClick(world))
            {
                TryHouse(def, world);
                return true;
            }
            var region = TerrainSystem.Find("Town");
            if (def == null || region == null || hero == null)
            {
                // Somewhere that isn't Dirtmouth: the click is taken, with a word about it.
                Net.NetChat.Add(Lang.T("只能在德特茅斯的街道上安置居民"), new Color(50 / 255f, 125 / 255f, 255 / 255f));
                return true;
            }
            // The town's street must have been read first (bench, ground and its flat run).
            if (streetMax - streetMin < 1f) Place(region, Shop.UnlockedStage(), hero);
            // Whether there is anywhere to put them is the bench's business, not the street run's: a
            // player who has built across the street leaves the run empty, and that must not refuse
            // every click (it only means the run round the bench is what the wander falls back to).
            if (!townFound)
            {
                // No bench to stand them on here: somewhere that isn't Dirtmouth's street after all.
                Plugin.Log.LogInfo($"[城镇] 这个场景没有找到长椅（{region.SceneName}），安置被拒");
                Net.NetChat.Add(Lang.T("只能在德特茅斯的街道上安置居民"), new Color(50 / 255f, 125 / 255f, 255 / 255f));
                return true;
            }
            // TerraKnight's townsfolk live around Dirtmouth's own bench, not in houses: a click on a chair
            // puts the townsperson on that chair — the Wooden Chair the player set down (World.Stations.
            // ChairNear: Terraria's own furniture for exactly this) — and it stays bound to it, put back
            // there on loading. Every other click — the street, the air, a wall, the bench itself — stands
            // it at the bench, the town's own default resting place. No refusals.
            float x, y;
            string where;
            bool onChair = World.Stations.ChairNear(world, out x, out y);
            if (onChair)
            {
                FitBeside(ref x, ref y);
                where = "椅子";
            }
            else
            {
                x = benchX;
                // Read from just above the bench, not from three tiles up: a floor the player built over
                // the bench (with a chair on it) used to be found first, and the townsperson put up there.
                y = GridGround(x, benchY + 0.3f, out float ground) ? ground : benchY;
                where = "德特茅斯的长椅（默认位置）";
            }
            Plugin.Log.LogInfo($"[城镇] 玩家把 {Label(def)} 放在{where} ({x:F1},{y:F1})");
            Homes[def.Internal] = x;
            HomeY[def.Internal] = y;
            // On a chair or at the bench it has no house any more.
            ClearHouse(def.Internal);
            Arrived.Add(def.Internal);
            Dismissed.Remove(def.Internal);      // put down again: it may live here from now on
            var n = npcs.Find(v => v.Def == def);
            bool fresh = n == null;
            if (fresh)
            {
                if (!Names.TryGetValue(def.Internal, out var given))
                {
                    var pool = GivenNames(def.Internal);
                    given = pool[Random.Range(0, pool.Length)];
                    Names[def.Internal] = given;
                }
                n = new Npc { Def = def, Given = given, Dir = Random.Range(0, 2) == 0 ? -1 : 1, Ai1 = Random.Range(60, 300f) };
                npcs.Add(n);
            }
            n.HomeX = x;
            n.X = x;
            n.Vx = 0f;
            n.FeetY = y;
            n.LastX = x;
            // Bound to a chair it keeps close to it; the town's own bench is one spot everybody shares,
            // so those are given room to spread over the street.
            n.Range = onChair ? ChairRange : RoamRange;
            // Main.NewText(Language.GetTextValue("Announcement.HasArrived", FullName)): only the first time.
            if (fresh && InTown(def, lastStage))
            {
                string line = Terraria.TerrariaText.Get(Lang.Culture, "Announcement.HasArrived");
                Net.NetChat.Add(line != null ? line.Replace("{0}", n.Name) : Lang.F("{0}已到达！", n.Name), new Color(50 / 255f, 125 / 255f, 255 / 255f));
                Plugin.Log.LogInfo($"[城镇] 玩家安置 {n.Name} 于 ({x:F1},{y:F1})");
            }
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            placing = null;
            return true;
        }

        /// <summary>
        /// A townsperson stands on the chair's seat. When a wall is in the way there (a chair jammed
        /// against one), it steps down to the floor at the chair's base and is nudged half a tile or so
        /// to a side that has the same floor, so it never ends up standing inside a block.
        /// </summary>
        static void FitBeside(ref float x, ref float y)
        {
            if (Standable(x, y)) return;
            float baseY = y - CellGrid.CellSize;
            for (float d = 0.5f; d <= 1.5f; d += 0.5f)
                foreach (float side in new[] { -1f, 1f })
                {
                    float nx = x + side * d;
                    if (!GridGround(nx, baseY + 0.5f, out float ground) || Mathf.Abs(ground - baseY) > 0.3f || !Standable(nx, ground)) continue;
                    x = nx;
                    y = ground;
                    return;
                }
            // Nowhere beside it is clear: stand at the chair's base rather than in the wall.
            y = baseY;
        }

        /// <summary>
        /// Whether that cell blocks. A scene's own terrain only counts inside a region: outside them
        /// nothing is loaded and reading it as solid would make the whole sky out of bounds.
        /// </summary>
        static bool GridBlocks(int gx, int gy) => TerraWorld.RegionAt(gx, gy) != null && TerraWorld.IsSolid(gx, gy);
        static bool GridPlatform(int gx, int gy) => TerraWorld.RegionAt(gx, gy) != null && TerraWorld.IsPlatform(gx, gy);

        /// <summary>
        /// The floor under a point, read from the cell grid — the same grid the player walks on, and the
        /// one a player's own blocks go into at once. Their physics colliders are rebuilt per chunk
        /// later, so a raycast looks straight through a house that was just built and finds the street
        /// inside it instead of the roof above.
        /// </summary>
        static bool GridGround(float x, float fromY, out float feet)
        {
            feet = 0f;
            var cell = CellGrid.WorldToCell(new Vector2(x, fromY));
            for (int i = 0; i < 400; i++)
            {
                int cy = cell.y - i;
                if (!GridBlocks(cell.x, cy) && !GridPlatform(cell.x, cy)) continue;
                feet = CellGrid.CellCenter(cell.x, cy).y + CellGrid.CellSize / 2f;
                return true;
            }
            return false;
        }

        /// <summary>Whether a townsperson fits standing there: its own box, read from the cell grid.</summary>
        static bool Standable(float x, float feetY)
        {
            var lo = CellGrid.WorldToCell(new Vector2(x - 0.6f, feetY + 0.1f));
            var hi = CellGrid.WorldToCell(new Vector2(x + 0.6f, feetY + 1.55f));
            for (int cx = lo.x; cx <= hi.x; cx++)
                for (int cy = lo.y; cy <= hi.y; cy++)
                    if (GridBlocks(cx, cy)) return false;
            return true;
        }

        /// <summary>
        /// The floor a townsperson stands on at x, read from just above its feet: its own chair's seat
        /// when it is on it (the chair it was put on, at its home), else the cell grid's floor (a roof,
        /// an upper storey, the street); Ground(x) alone only knows the street, under a chair set above
        /// it. Any other chair is furniture it walks past on the floor, as Terraria's townsfolk walk
        /// through chairs.
        /// </summary>
        static float? StandingFloor(float x, float feetY, Npc n = null)
        {
            if (World.Stations.ChairNear(new Vector2(x, feetY), out float chairX, out float seat) && (n == null || Mathf.Abs(chairX - n.HomeX) < 0.3f) && Standable(x, seat)) return seat;
            if (GridGround(x, feetY + 0.5f, out float floor)) return floor;
            return Ground(x);
        }

        /// <summary>
        /// Right click on a head in the panel: the townsperson leaves and stays away — Dismissed holds it
        /// out of InTown, so even one that would move in on its own doesn't come back — until the player
        /// chooses it in the panel and sets it down again.
        /// </summary>
        public static bool Remove(string internalName)
        {
            var def = DefOf(internalName);
            if (def == null || !Lives(internalName)) return false;
            Arrived.Remove(def.Internal);
            Dismissed.Add(def.Internal);
            Homes.Remove(def.Internal);
            HomeY.Remove(def.Internal);
            ClearHouse(def.Internal);
            var n = npcs.Find(v => v.Def == def);
            string who = n != null ? n.Name : Label(def);
            if (n != null)
            {
                if (talk == n) Close(false);
                npcs.Remove(n);
            }
            string left = Terraria.TerrariaText.Get(Lang.Culture, "LegacyMisc.35");
            Net.NetChat.Add(left != null ? left.Replace("{0}", who) : Lang.F("{0}已离去！", who), new Color(50 / 255f, 125 / 255f, 255 / 255f));
            Plugin.Log.LogInfo($"[城镇] 玩家移除 {Label(def)}");
            return true;
        }

        /// <summary>The panel's own state: opened or closed by its button, closed with the inventory.</summary>
        public static void TogglePanel()
        {
            panelOpen = !panelOpen;
            if (!panelOpen)
            {
                placing = null;
                querying = false;
            }
            else TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
            if (!panelOpen) TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>The panel's clicks: a head chosen for placing, or sent away with a right click.</summary>
        public static void ClickHead(NpcDef def, bool left, bool right)
        {
            if (def == null) return;
            if (right)
            {
                if (Lives(def.Internal)) Remove(def.Internal);
                else if (placing == def.Internal) placing = null;
                return;
            }
            if (!left) return;
            // The same head again gives the click back; choosing another replaces the choice (and the query).
            querying = false;
            placing = placing == def.Internal ? null : def.Internal;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        /// <summary>Called when the inventory closes: nothing stays chosen.</summary>
        public static void ClosePanel()
        {
            panelOpen = false;
            placing = null;
            querying = false;
        }

        /// <summary>Dev: opens the panel and pictures it (the townsfolk grid, as the player sees it).</summary>
        public static void DevPanelShot(string name)
        {
            panelOpen = true;
            string dir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "TerraKnight", "dev");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Plugin.Log.LogInfo($"[AutoTest] 截图 {path}");
        }

        /// <summary>Dev: puts one down two tiles right of the player, as the panel's two clicks would.</summary>
        public static string DevPlace(string internalName)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return "没有主角";
            placing = internalName;
            var at = new Vector2(hero.transform.position.x + 2f, hero.transform.position.y);
            bool ok = PlaceClick(at, hero);
            return $"{(ok ? "放在" : "没放上")} {internalName}（在街上 {Present(internalName)}，已入住 {Lives(internalName)}，家 x {(Homes.TryGetValue(internalName, out var h) ? h.ToString("F1") : "-")}）";
        }

        /// <summary>Dev: as a click at that world point: where it ended up, and whether it stands on the ground.</summary>
        public static string DevPlaceAt(string internalName, Vector2 at)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return "没有主角";
            placing = internalName;
            bool taken = PlaceClick(at, hero);
            var n = npcs.Find(v => v.Def.Internal == internalName);
            if (n == null) return $"点 ({at.x:F1},{at.y:F1}) -> {(taken ? "受理" : "没受理")}，不在街上";
            bool has = GridGround(n.X, n.FeetY + 0.5f, out float g);
            return $"点 ({at.x:F1},{at.y:F1}) -> 落在 ({n.X:F1},{n.FeetY:F1})，脚下地面 {(has ? g.ToString("F1") : "无")}，"
                + $"站着 {(has && Mathf.Abs(g - n.FeetY) < 0.3f ? "是" : "否")}";
        }
    }
}
