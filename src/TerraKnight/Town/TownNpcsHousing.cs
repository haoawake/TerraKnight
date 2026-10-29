using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Town
{
    /// <summary>
    /// Terraria's housing for the townsfolk (World.Housing does the checking). In the housing panel a head chosen and
    /// then a click in a room the player built (Main.TryMovingNPC): WorldGen.MoveTownNPC's verdict, and when the room
    /// is a house the townsperson moves in at once (WorldGen.moveRoom, then put at its home as NPC.AI_007's teleport
    /// home does: on the best floor tile ScoreRoom found) and lives there, walking its room, in whatever scene it is
    /// (its shop and its words as ever); a townsperson with a house elsewhere isn't in Dirtmouth meanwhile. Its home is
    /// kept in the save (the scene and that scene's own cell of the floor tile, Terraria's homeTileX / homeTileY).
    /// Every 7200 ticks of play each one whose scene is loaded has its room looked at again (Main.UpdateTime_SpawnTownNPCs
    /// → WorldGen.QuickFindHome); one whose room is no house any more loses it and goes back to its usual place, with
    /// Terraria's words on why (LegacyInterface.55 and the room's own fault). The "?" of Terraria's list is the housing
    /// query (Main.PerformHousingCheck): a click on a room, the verdict. While the panel is up each home shows its banner
    /// under the ceiling (Main.DrawNPCHousesInWorld) and a right click on one turns its townsperson out (WorldGen.kickOut).
        /// The panel's chair and bench stay for clicks in Dirtmouth's open street, and for NPCs excluded from housing.
    /// </summary>
    internal static partial class TownNpcs
    {
        /// <summary>A home: the scene, its own cell of the floor tile the townsperson stands on (homeTileX, homeTileY), and the tile the housing menu gave it (TownRoomManager's room).</summary>
        public sealed class HouseSave
        {
            public string Scene;
            public int X, Y;
            public int RX, RY;
        }

        /// <summary>Who lives in a house, by internal name.</summary>
        public static readonly Dictionary<string, HouseSave> Houses = new Dictionary<string, HouseSave>();

        /// <summary>Each housed one's walk across its room, in tiles either side of its home tile (Housing.WalkSpan).</summary>
        static readonly Dictionary<string, Vector2Int> walkTiles = new Dictionary<string, Vector2Int>();
        /// <summary>The same in world x, for the loaded region it stands in now (Safe keeps it inside).</summary>
        static readonly Dictionary<string, Vector2> walkSpans = new Dictionary<string, Vector2>();
        /// <summary>The loaded region each housed one standing now was put in: another load of the scene puts it there again.</summary>
        static readonly Dictionary<string, TerrainRegion> housedIn = new Dictionary<string, TerrainRegion>();

        /// <summary>The housing query is on the cursor (Main.SetMouseNPC_ToHousingQuery).</summary>
        static bool querying;
        public static bool Querying => querying;

        /// <summary>Main.checkForSpawns: ticks of play since the homes were last looked at; 7200 / worldUpdateRate (1).</summary>
        static float houseCheckTicks;
        const float HouseCheckTicks = 7200f;

        /// <summary>Main.NewText's colour for the housing lines (255, 240, 20; ChatColors.ServerMessage the same).</summary>
        static readonly Color housingColor = new Color(1f, 240f / 255f, 20f / 255f);

        /// <summary>Dev: the housing lines said since the last check began.</summary>
        public static readonly List<string> DevSaid = new List<string>();
        public static int DevEvicted, DevHouseChecks;

        /// <summary>
        /// The Traveling Merchant and Old Man remain excluded. TerraKnight also allows the Skeleton Merchant
        /// to live in a normal house, using the same room, occupancy and progression checks as other residents.
        /// </summary>
        public static bool Housable(NpcDef def) => def != null && def.Id != 368 && def.Id != 37;

        /// <summary>NPC.housingCategory: the town pets and town slimes 1, the rest 0.</summary>
        static int Category(NpcDef def) => def.Kind == NpcKind.Pet ? 1 : 0;

        /// <summary>The area its house is in (Areas' name in the language now), null when it has none.</summary>
        public static string HouseArea(string internalName) =>
            Houses.TryGetValue(internalName, out var h) ? Areas.Display(Areas.AreaName(h.Scene)) : null;

        /// <summary>Those whose walk couldn't be read yet (the room not a room at that moment): read again.</summary>
        static readonly HashSet<string> walkPending = new HashSet<string>();

        /// <summary>The walk across its room, from a check of the room over its home tile; none (pending) when that fails.</summary>
        static bool ReadWalk(string name, HouseSave h, TerrainRegion region)
        {
            var tile = HomeTile(h, region);
            int left = tile.x, right = tile.x;
            bool room = Housing.StartRoomCheck(tile.x, tile.y - 1);
            if (room) Housing.WalkSpan(tile.x, tile.y, out left, out right);
            walkTiles[name] = new Vector2Int(left - tile.x, right - tile.x);
            if (room) walkPending.Remove(name);
            else walkPending.Add(name);
            return room;
        }

        static void ClearHouses()
        {
            walkPending.Clear();
            Houses.Clear();
            walkTiles.Clear();
            walkSpans.Clear();
            housedIn.Clear();
            querying = false;
            houseCheckTicks = 0f;
        }

        /// <summary>A townsperson leaves its house (put on a chair or the bench, sent away, turned out).</summary>
        static void ClearHouse(string internalName)
        {
            walkPending.Remove(internalName);
            Houses.Remove(internalName);
            walkTiles.Remove(internalName);
            walkSpans.Remove(internalName);
            housedIn.Remove(internalName);
        }

        public static Dictionary<string, HouseSave> SaveHouses() => new Dictionary<string, HouseSave>(Houses);

        public static void LoadHouses(Dictionary<string, HouseSave> saved)
        {
            ClearHouses();
            if (saved == null) return;
            foreach (var kv in saved)
                if (kv.Value != null && !string.IsNullOrEmpty(kv.Value.Scene) && DefOf(kv.Key) != null) Houses[kv.Key] = kv.Value;
        }

        // ------------------------------------------------------------------ where things are

        /// <summary>The home tile in Terraria's coordinates, for a loaded region.</summary>
        static Vector2Int HomeTile(HouseSave h, TerrainRegion region) =>
            new Vector2Int(region.CellOffset.x + h.X, -(region.CellOffset.y + h.Y) - 1);

        /// <summary>Every loaded home (Main.npc's townsfolk with homeTileX / homeTileY), for the occupancy rules.</summary>
        static List<Housing.Occupant> Occupants()
        {
            var list = new List<Housing.Occupant>();
            foreach (var kv in Houses)
            {
                var def = DefOf(kv.Key);
                var region = TerrainSystem.Find(kv.Value.Scene);
                if (def == null || region == null) continue;
                var t = HomeTile(kv.Value, region);
                list.Add(new Housing.Occupant { Who = kv.Key, X = t.x, Y = t.y, Category = Category(def) });
            }
            return list;
        }

        static string TypeName(NpcDef def) => Label(def);

        static void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Net.NetChat.Add(line, housingColor);
            DevSaid.Add(line);
            Plugin.Log.LogInfo($"[房屋] {line}");
        }

        /// <summary>
        /// Whether a click with a head chosen is for Terraria's housing rather than the panel's chair and bench: anywhere
        /// but Dirtmouth; there, on a block the player laid, in front of a wall they put up, or in an enclosed space
        /// (Housing.Enclosed). Dirtmouth's open street, its own ground and a chair out in the open are the panel's.
        /// </summary>
        static bool HousingClick(Vector2 world)
        {
            var cell = CellGrid.WorldToCell(world);
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            if (region == null) return false;
            if (region.SceneName != "Town") return true;
            var t = Housing.TileOf(world);
            if (Housing.SolidAt(t.x, t.y, out bool mine)) return mine;
            if (TerraWorld.GetWall(cell.x, cell.y) > 0) return true;
            return Housing.Enclosed(t.x, t.y);
        }

        // ------------------------------------------------------------------ moving in

        /// <summary>
        /// Main.TryMovingNPC: WorldGen.MoveTownNPC's verdict on the room under the click, in Terraria's words; a house,
        /// and WorldGen.moveRoom gives the townsperson the best floor tile ScoreRoom found there for its home, and it is
        /// in it straight away. A room that isn't one leaves the head on the cursor, as Terraria does.
        /// </summary>
        static bool TryHouse(NpcDef def, Vector2 world)
        {
            DevSaid.Clear();
            if (RefusePlacement(def)) return false;
            var t = Housing.TileOf(world);
            var said = new List<string>();
            bool ok = Housing.MoveTownNPC(t.x, t.y, def.Id, Category(def), TypeName(def), Occupants(), said);
            foreach (var line in said) Say(line);
            if (!ok)
            {
                Plugin.Log.LogInfo($"[房屋] {Label(def)} 没能住进 ({t.x},{t.y})：{Housing.Reason}");
                return false;
            }
            var home = Housing.CellOf(Housing.BestX, Housing.BestY);
            var region = TerraWorld.RegionAt(home.x, home.y);
            if (region == null) return false;
            Housing.WalkSpan(Housing.BestX, Housing.BestY, out int left, out int right);
            var h = new HouseSave { Scene = region.SceneName, X = home.x - region.CellOffset.x, Y = home.y - region.CellOffset.y };
            h.RX = h.X;
            h.RY = h.Y;
            bool fresh = !Lives(def.Internal);
            Houses[def.Internal] = h;
            walkTiles[def.Internal] = new Vector2Int(left - Housing.BestX, right - Housing.BestX);
            // Its home is the house now: not a chair, not the street, and it isn't sent away any more.
            Homes.Remove(def.Internal);
            HomeY.Remove(def.Internal);
            Dismissed.Remove(def.Internal);
            Arrived.Add(def.Internal);
            var n = npcs.Find(v => v.Def == def);
            if (n == null)
            {
                n = NewNpc(def);
                npcs.Add(n);
            }
            PutHome(n, region, h);
            Achievements.AchievementTracker.Housed();
            placing = null;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            Plugin.Log.LogInfo($"[房屋] {n.Name} 住进 {region.SceneName} 的房屋：家 ({Housing.BestX},{Housing.BestY})，得分 {Housing.HiScore}，房间 {Housing.NumRoomTiles} 格，走动 {left}..{right}");
            if (fresh && InTown(def, Shop.UnlockedStage()))
            {
                string line = Terraria.TerrariaText.Get(Lang.Culture, "Announcement.HasArrived");
                Net.NetChat.Add(line != null ? line.Replace("{0}", n.Name) : Lang.F("{0}已到达！", n.Name), new Color(50 / 255f, 125 / 255f, 255 / 255f));
            }
            return true;
        }

        static Npc NewNpc(NpcDef def)
        {
            if (!Names.TryGetValue(def.Internal, out var given))
            {
                var pool = GivenNames(def.Internal);
                given = pool[Random.Range(0, pool.Length)];
                Names[def.Internal] = given;
            }
            return new Npc { Def = def, Given = given, Dir = Random.Range(0, 2) == 0 ? -1 : 1, Ai1 = Random.Range(60, 300f) };
        }

        /// <summary>NPC.AI_007_TownEntities_TeleportToHome: on its home tile, standing, its walk the room's width.</summary>
        static void PutHome(Npc n, TerrainRegion region, HouseSave h)
        {
            const float c = CellGrid.CellSize;
            int gx = region.CellOffset.x + h.X, gy = region.CellOffset.y + h.Y;
            n.HomeX = (gx + 0.5f) * c;
            n.X = n.HomeX;
            n.FeetY = (gy + 1) * c;
            n.Vx = 0f;
            n.Ai0 = 0;
            n.LastX = n.X;
            SetWalk(n, region, h);
            housedIn[n.Def.Internal] = region;
        }

        static void SetWalk(Npc n, TerrainRegion region, HouseSave h)
        {
            const float c = CellGrid.CellSize;
            int gx = region.CellOffset.x + h.X;
            var span = walkTiles.TryGetValue(n.Def.Internal, out var w) ? w : Vector2Int.zero;
            // Its own half-width (9 px) inside the run of floor.
            float lo = (gx + span.x) * c + 0.3f, hi = (gx + span.y + 1) * c - 0.3f;
            if (hi < lo) lo = hi = (gx + 0.5f) * c;
            walkSpans[n.Def.Internal] = new Vector2(lo, hi);
            n.Range = Mathf.Max(Mathf.Abs(lo - n.HomeX), Mathf.Abs(hi - n.HomeX)) + 0.01f;
        }

        /// <summary>The run of its room's floor a housed townsperson walks (Safe's limits), when it has one.</summary>
        static bool HouseSpan(Npc n, out float lo, out float hi)
        {
            lo = hi = 0f;
            if (!Houses.ContainsKey(n.Def.Internal) || !walkSpans.TryGetValue(n.Def.Internal, out var s)) return false;
            lo = s.x;
            hi = s.y;
            return true;
        }

        // ------------------------------------------------------------------ every frame

        /// <summary>
        /// The housed townsfolk in the loaded scenes, in their rooms; those whose scene isn't loaded (or who aren't in
        /// town now: Santa outside December) nowhere. Main.UpdateTime_SpawnTownNPCs' look at the homes every 7200 ticks.
        /// A right click away from the interface puts back what is on the cursor.
        /// </summary>
        static void HousingTick(HeroController hero)
        {
            if (hero == null) return;
            if ((placing != null || querying) && !InventoryGui.MouseOverUi && !TerraMod.DevNoRealMouse && Player.TerraKeys.Pressed(Player.TerraKeys.Action.Interact))
            {
                placing = null;
                querying = false;
                TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            }
            if (Houses.Count == 0) return;
            int stage = Shop.UnlockedStage();
            foreach (var name in new List<string>(Houses.Keys))
            {
                var def = DefOf(name);
                var h = Houses[name];
                var region = def != null ? TerrainSystem.Find(h.Scene) : null;
                var n = def != null ? npcs.Find(v => v.Def == def) : null;
                if (region == null || !InTown(def, stage))
                {
                    if (n != null)
                    {
                        if (talk == n) Close(false);
                        npcs.Remove(n);
                    }
                    housedIn.Remove(name);
                    continue;
                }
                if (n != null && housedIn.TryGetValue(name, out var was) && was == region)
                {
                    // A walk read before the room's furniture had come in (just loaded): read again now and then.
                    if (walkPending.Contains(name) && Time.frameCount % 30 == 0 && ReadWalk(name, h, region)) SetWalk(n, region, h);
                    continue;
                }
                // Its scene came in: it is at home, where its room was found (its walk read when not yet).
                if (!walkTiles.ContainsKey(name)) ReadWalk(name, h, region);
                if (n == null)
                {
                    n = NewNpc(def);
                    npcs.Add(n);
                }
                PutHome(n, region, h);
            }
            houseCheckTicks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            if (houseCheckTicks >= HouseCheckTicks)
            {
                houseCheckTicks = 0f;
                RecheckHouses();
            }
        }

        /// <summary>
        /// WorldGen.QuickFindHome for each townsperson with a home in a loaded scene: still a house, and its home is the
        /// best tile there now (it may have moved within the room); none, and it loses it (homeless): back to its usual
        /// place, Terraria's words on why in the chat. Returns how many lost theirs.
        /// </summary>
        static int RecheckHouses()
        {
            int lost = 0;
            DevHouseChecks++;
            foreach (var name in new List<string>(Houses.Keys))
            {
                var def = DefOf(name);
                var h = Houses[name];
                var region = def != null ? TerrainSystem.Find(h.Scene) : null;
                if (region == null) continue;
                var tile = HomeTile(h, region);
                var occupants = Occupants();
                if (Housing.QuickFindHome(tile.x, tile.y, def.Id, Category(def), name, occupants))
                {
                    var best = Housing.CellOf(Housing.BestX, Housing.BestY);
                    var at = TerraWorld.RegionAt(best.x, best.y);
                    if (at == null) continue;
                    Housing.WalkSpan(Housing.BestX, Housing.BestY, out int left, out int right);
                    walkTiles[name] = new Vector2Int(left - Housing.BestX, right - Housing.BestX);
                    walkPending.Remove(name);
                    if (at != region || best.x - region.CellOffset.x != h.X || best.y - region.CellOffset.y != h.Y)
                    {
                        h.Scene = at.SceneName;
                        h.X = best.x - at.CellOffset.x;
                        h.Y = best.y - at.CellOffset.y;
                        Plugin.Log.LogInfo($"[房屋] {Label(def)} 的家挪到同一房间的 ({Housing.BestX},{Housing.BestY})");
                    }
                    var n = npcs.Find(v => v.Def == def);
                    if (n != null)
                    {
                        n.HomeX = (best.x + 0.5f) * CellGrid.CellSize;
                        SetWalk(n, at, h);
                    }
                    continue;
                }
                Evict(def, tile, occupants);
                lost++;
            }
            return lost;
        }

        /// <summary>
        /// Its room is no house now: homeless, back to its usual place (Dirtmouth's street, where Place puts it). Terraria
        /// says nothing then; here it says LegacyInterface.55 with its name ("this housing doesn't suit"), and what the
        /// housing menu would say of that room (WorldGen.MoveTownNPC, its own home aside).
        /// </summary>
        static void Evict(NpcDef def, Vector2Int home, List<Housing.Occupant> occupants)
        {
            DevSaid.Clear();
            var said = new List<string> { Housing.Text("LegacyInterface.55") + " " + TypeName(def) };
            Housing.MoveTownNPC(home.x, home.y - 1, def.Id, Category(def), TypeName(def), occupants, said, def.Internal);
            foreach (var line in said) Say(line);
            LeaveHouse(def);
            DevEvicted++;
            Plugin.Log.LogInfo($"[房屋] {Label(def)} 的房屋不再合适，回到原来的地方");
        }

        /// <summary>Out of its house, to its usual place (Place, the next time it looks at Dirtmouth).</summary>
        static void LeaveHouse(NpcDef def)
        {
            ClearHouse(def.Internal);
            var n = npcs.Find(v => v.Def == def);
            if (n != null)
            {
                if (talk == n) Close(false);
                npcs.Remove(n);
            }
            nextRecheck = 0f;
        }

        /// <summary>Main.PerformHousingCheck: the room under the click, Terraria's verdict (LegacyInterface.39 when it is a house).</summary>
        static void HousingQuery(Vector2 world)
        {
            DevSaid.Clear();
            var t = Housing.TileOf(world);
            var said = new List<string>();
            if (Housing.MoveTownNPC(t.x, t.y, -1, 0, null, Occupants(), said)) said.Add(Housing.Text("LegacyInterface.39"));
            foreach (var line in said) Say(line);
        }

        /// <summary>The "?" in the list: the housing query on the cursor, or off it again.</summary>
        public static void ClickQuery(bool left, bool right)
        {
            if (right)
            {
                querying = false;
                return;
            }
            if (!left) return;
            querying = !querying;
            placing = null;
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
        }

        // ------------------------------------------------------------------ banners

        /// <summary>A house's banner where Main.DrawNPCHousesInWorld hangs it: centre in Terraria px, its frame, the townsperson.</summary>
        struct Banner
        {
            public NpcDef Def;
            public Npc Npc;
            public Vector2 Px;
            public int Style, Category;
        }

        static readonly List<Banner> banners = new List<Banner>();

        /// <summary>
        /// The banners: from the tile over the home up to the ceiling's first solid tile, hung under it (a platform ceiling
        /// 8 px higher), those of the same home 26 px apart, the pets' first (NPCBannerSorter); style 1, "Reserved for",
        /// while its home is still the tile the housing menu gave it (TownRoomManager's room).
        /// </summary>
        static List<Banner> Banners()
        {
            banners.Clear();
            var list = new List<(NpcDef Def, HouseSave House, Vector2Int Home)>();
            foreach (var kv in Houses)
            {
                var def = DefOf(kv.Key);
                var region = def != null ? TerrainSystem.Find(kv.Value.Scene) : null;
                if (region == null) continue;
                list.Add((def, kv.Value, HomeTile(kv.Value, region)));
            }
            list.Sort((a, b) => -Category(a.Def).CompareTo(Category(b.Def)));
            if (list.Count > 0) Housing.Refresh();
            for (int j = 0; j < list.Count; j++)
            {
                var (def, house, home) = list[j];
                int num4 = 0;
                for (int k = list.Count - 1; k > j; k--)
                    if (list[k].Home == home) num4++;
                int row = home.y - 1;
                Housing.Tile ceiling = default;
                for (int guard = 0; guard < 200; guard++)
                {
                    ceiling = Housing.TileAt(home.x, row);
                    if (Housing.SolidTile(ceiling)) break;
                    row--;
                }
                int num9 = ceiling.Type == 19 ? 10 : 18;
                float num11 = (row + 1) * 16f + num4 * 26f;
                banners.Add(new Banner
                {
                    Def = def,
                    Npc = npcs.Find(v => v.Def == def),
                    Px = new Vector2(home.x * 16f + 8f, num11 + num9),
                    Style = house.X == house.RX && house.Y == house.RY ? 1 : 0,
                    Category = Category(def),
                });
            }
            return banners;
        }

        /// <summary>Main.DrawNPCHousesInWorld while the housing menu is up: House_Banner_1's frame, the head on it (24 px at the most).</summary>
        static void DrawHouseBanners(FxBatch fx)
        {
            hoveredBanner = null;
            if (!panelOpen || fx == null || Houses.Count == 0) return;
            var tex = Terraria.TerrariaAssets.Texture("House_Banner_1");
            if (tex == null) return;
            int fw = tex.width / 2, fh = tex.height / 2;
            Vector2? mouse = TerraMod.MouseWorld(out var m) ? m : (Vector2?)null;
            foreach (var b in Banners())
            {
                var color = new Color(L, L, L, 1f);
                fx.Draw(FxBatch.Frame("House_Banner_1", fw * b.Style, fh * b.Category, fw, fh, fw / 2f, fh / 2f), b.Px, color, 0f, 1f, town: true);
                int head = Terraria.TerrariaText.HeadIndex(b.Def.Internal);
                if (head > 0)
                {
                    string name = "NPC_Head_" + head;
                    var ht = Terraria.TerrariaAssets.Texture(name);
                    if (ht != null)
                    {
                        float big = Mathf.Max(ht.width, ht.height);
                        // On the banner, nearer than it (the townsfolk's plane sorts by distance, so a layer in front).
                        fx.Draw(FxBatch.Whole(name), b.Px + new Vector2(0f, 2f), color, 0f, big > 24f ? 24f / big : 1f, overPlayer: true);
                    }
                }
                if (mouse.HasValue && b.Npc != null && BannerRect(b, fw, fh).Contains(TerraParticles.ToPx(mouse.Value))) hoveredBanner = b.Npc;
            }
        }

        /// <summary>Main's banner hover box: the frame, its last 8 px left out.</summary>
        static Rect BannerRect(Banner b, int fw, int fh) => new Rect(b.Px.x - fw / 2f, b.Px.y - fh / 2f + 4f, fw, fh - 8f);

        static Npc hoveredBanner;

        /// <summary>Lang.GetNPCHouseBannerText for the banner under the cursor: its full name, or "Reserved for {0}".</summary>
        public static string BannerHoverText()
        {
            var n = hoveredBanner;
            if (n == null || !panelOpen) return null;
            if (Houses.TryGetValue(n.Def.Internal, out var h) && h.X == h.RX && h.Y == h.RY)
            {
                string reserved = Terraria.TerrariaText.Get(Lang.Culture, "Game.ReservedForNPC");
                if (reserved != null) return reserved.Replace("{0}", n.Name);
            }
            return n.Name;
        }

        /// <summary>A right click on a banner with the housing menu up: WorldGen.kickOut, that townsperson homeless (back to its usual place).</summary>
        static bool BannerRightClick(Vector2 mouseWorld)
        {
            if (!panelOpen || Houses.Count == 0) return false;
            var tex = Terraria.TerrariaAssets.Texture("House_Banner_1");
            if (tex == null) return false;
            var px = TerraParticles.ToPx(mouseWorld);
            foreach (var b in Banners())
            {
                if (!BannerRect(b, tex.width / 2, tex.height / 2).Contains(px)) continue;
                Plugin.Log.LogInfo($"[房屋] 右键旗帜：{Label(b.Def)} 被请出房屋");
                LeaveHouse(b.Def);
                TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ dev

        /// <summary>Dev: the head chosen and a click at that world point (the housing menu's two clicks); what was said.</summary>
        public static string DevHouse(string internalName, Vector2 at)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return "没有主角";
            DevSaid.Clear();
            placing = internalName;
            querying = false;
            PlaceClick(at, hero);
            bool chosen = placing != null;
            placing = null;
            return $"{(Houses.ContainsKey(internalName) ? "住进了房屋" : "没住进")}（头像还在光标上 {chosen}）：{string.Join(" / ", DevSaid)}";
        }

        /// <summary>Dev: the housing query at a world point; what was said.</summary>
        public static string DevQuery(Vector2 at)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return "没有主角";
            querying = true;
            placing = null;
            PlaceClick(at, hero);
            querying = false;
            return string.Join(" / ", DevSaid);
        }

        /// <summary>Dev: someone's home (scene, its cell, the world point it stands at), or null.</summary>
        public static string DevHouseOf(string internalName, out Vector2 feet)
        {
            feet = default;
            if (!Houses.TryGetValue(internalName, out var h)) return null;
            var region = TerrainSystem.Find(h.Scene);
            if (region != null) feet = new Vector2((region.CellOffset.x + h.X + 0.5f) * CellGrid.CellSize, (region.CellOffset.y + h.Y + 1) * CellGrid.CellSize);
            return $"{h.Scene} ({h.X},{h.Y})";
        }

        /// <summary>Dev: where a townsperson stands now (feet), when it is standing in a loaded scene.</summary>
        public static Vector2? DevFeet(string internalName)
        {
            foreach (var n in npcs) if (n.Def.Internal == internalName) return new Vector2(n.X, n.FeetY);
            return null;
        }

        /// <summary>Dev: its walk across its room (world x), when it has a house.</summary>
        public static Vector2? DevWalk(string internalName) => walkSpans.TryGetValue(internalName, out var s) ? s : (Vector2?)null;

        /// <summary>Dev: the look at the homes Main.UpdateTime_SpawnTownNPCs makes, now; how many lost theirs.</summary>
        public static int DevRecheckHouses()
        {
            DevSaid.Clear();
            return RecheckHouses();
        }

        /// <summary>Dev: ticks until the next look at the homes.</summary>
        public static float DevTicksToCheck => HouseCheckTicks - houseCheckTicks;

        /// <summary>Dev: the banners shown now (the panel up), and the banner text under a world point.</summary>
        public static int DevBanners(Vector2 probe, out string text)
        {
            text = null;
            var tex = Terraria.TerrariaAssets.Texture("House_Banner_1");
            var px = TerraParticles.ToPx(probe);
            var list = Banners();
            foreach (var b in list)
                if (tex != null && BannerRect(b, tex.width / 2, tex.height / 2).Contains(px)) text = b.Npc != null ? b.Npc.Name : Label(b.Def);
            return list.Count;
        }

        /// <summary>Dev: the world point of a banner of a townsperson's house (its centre), when shown.</summary>
        public static Vector2? DevBannerAt(string internalName)
        {
            foreach (var b in Banners()) if (b.Def.Internal == internalName) return TerraParticles.ToWorld(b.Px + new Vector2(0f, 4f));
            return null;
        }

        /// <summary>Dev: a right click at a world point with the housing menu up (banners).</summary>
        public static bool DevBannerRightClick(Vector2 at)
        {
            bool was = panelOpen;
            panelOpen = true;
            bool hit = BannerRightClick(at);
            panelOpen = was;
            return hit;
        }
    }
}
