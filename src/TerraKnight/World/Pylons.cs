using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's teleportation pylons, one beside every bench. A bench's pylon appears the first
    /// time the player rests on that bench and never before (nor on the map). Right-clicking a
    /// pylon opens the fullscreen map, where every pylon found so far is marked; clicking one takes
    /// the player there at once. None of Terraria's conditions apply (no townsfolk needed, no
    /// danger check, from anywhere): the game brings the player there as it does to a bench, but
    /// standing, beside the pylon, out of a portal in the area's colours (Player.OpeningPortal).
    /// </summary>
    internal static partial class Pylons
    {
        internal sealed class Pylon
        {
            /// <summary>The bench's scene and its respawn marker (the bench object's name).</summary>
            public string Scene, Bench;
            /// <summary>Bottom-left of the pylon in the scene's own coordinates.</summary>
            public Vector2 Local;
            /// <summary>The scene's size, to place it on the map without the scene loaded.</summary>
            public Vector2 SceneSize;
            /// <summary>Terraria pylon style (0 forest ... 8 universal), by area.</summary>
            public int Style;
            /// <summary>Creative's: put by the bench's survey position, its spot not yet chosen (that waits for its scene).</summary>
            public bool Provisional;
            public string Name => StyleNames[Style];
        }

        /// <summary>
        /// Creative: every bench's pylon unlocked (PylonBenches, from the layout survey). Its spot is
        /// chosen as by sitting there when its scene first loads; till then it stands by the bench.
        /// </summary>
        public static void UnlockAllForCreative()
        {
            int added = 0;
            foreach (var b in Benches)
            {
                if (Find(b.Scene, b.Bench) != null) continue;
                Unlocked.Add(new Pylon { Scene = b.Scene, Bench = b.Bench, Local = new Vector2(b.X + 2f, b.Y - 0.5f), SceneSize = new Vector2(b.W, b.H), Style = StyleFor(b.Scene), Provisional = true });
                added++;
            }
            Plugin.Log.LogInfo($"[晶塔] 创造模式：解锁全部 {Unlocked.Count} 座晶塔（新加 {added} 座）");
        }

        public static readonly List<Pylon> Unlocked = new List<Pylon>();

        /// <summary>Terraria's pylon items, by style.</summary>
        public static readonly string[] StyleNames = { "森林晶塔", "丛林晶塔", "神圣晶塔", "洞穴晶塔", "海洋晶塔", "沙漠晶塔", "雪原晶塔", "蘑菇晶塔", "万能晶塔" };

        /// <summary>The pylon is 3 tiles wide and 4 tall.</summary>
        public const int WidthCells = 3, HeightCells = 4;
        /// <summary>How near (units) the player must be to use a pylon by hand.</summary>
        const float UseRange = 6f;

        static bool wasAtBench, restPending;
        static float nextRestTry;
        static int restTries;
        static readonly List<PylonObject> spawned = new List<PylonObject>();

        public static void Clear()
        {
            Unlocked.Clear();
            wasAtBench = false;
        }

        public static Pylon Find(string scene, string bench)
        {
            foreach (var p in Unlocked) if (p.Scene == scene && p.Bench == bench) return p;
            return null;
        }

        /// <summary>Which pylon an area gets, matching Terraria's biomes.</summary>
        public static int StyleFor(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return 0;
            if (scene.StartsWith("Fungus1") || scene.StartsWith("Room_nailmaster_02") || scene.StartsWith("Room_Slug_Shrine")) return 1; // jungle
            if (scene.StartsWith("Fungus2") || scene.StartsWith("Room_Fungus_Shaman")) return 7;                                         // mushroom
            if (scene.StartsWith("Fungus3")) return IsFogCanyon(scene) ? 4 : 1;                                                       // ocean / jungle
            if (scene.StartsWith("Mines")) return 2;                                                                                 // hallow (crystals)
            if (scene.StartsWith("Deepnest_East")) return 5;                                                                         // desert (ash)
            if (scene.StartsWith("Deepnest") || scene.StartsWith("Abyss") || scene.StartsWith("Hive") || scene.StartsWith("Room_spider")) return 3; // cavern
            if (scene.StartsWith("Waterways") || scene.StartsWith("Ruins")) return 4;                                                // ocean (rain, water)
            if (scene.StartsWith("RestingGrounds") || scene.StartsWith("Room_Mansion")) return 6;                                   // snow (pale)
            if (scene.StartsWith("White_Palace") || scene.StartsWith("GG_") || scene.StartsWith("Room_Final")) return 8;            // universal
            return 0;                                                                                                                // forest
        }

        static bool IsFogCanyon(string scene)
        {
            foreach (var s in new[] { "Fungus3_01", "Fungus3_02", "Fungus3_03", "Fungus3_24", "Fungus3_25", "Fungus3_26", "Fungus3_27", "Fungus3_28", "Fungus3_30", "Fungus3_35", "Fungus3_47" })
                if (scene.StartsWith(s)) return true;
            return false;
        }

        // --- Unlocking: resting on a bench. ---

        /// <summary>Every frame in play: the first rest on a bench unlocks its pylon.</summary>
        public static void Tick(HeroController hero)
        {
            var pd = PlayerData.instance;
            var gm = GameManager.UnsafeInstance;
            if (pd == null || gm == null || hero == null) return;
            bool atBench = pd.atBench;
            if (atBench && !wasAtBench)
            {
                restPending = true;
                restTries = 0;
            }
            if (!atBench) restPending = false;
            // The bench marks itself as the respawn point a moment after the player sits down (and
            // the first frame still names the last bench): keep trying while seated.
            if (restPending && Time.unscaledTime >= nextRestTry)
            {
                nextRestTry = Time.unscaledTime + 0.2f;
                if (OnRest(gm, pd, hero) || ++restTries > 50) restPending = false;
            }
            wasAtBench = atBench;
            HandleUse(hero);
        }

        /// <summary>Unlocks the pylon of the bench the player sits on. False while the bench hasn't made itself the respawn point yet.</summary>
        static bool OnRest(GameManager gm, PlayerData pd, HeroController hero)
        {
            string scene = gm.sceneName;
            if (pd.respawnScene != scene || string.IsNullOrEmpty(pd.respawnMarkerName)) return false;
            if (Find(scene, pd.respawnMarkerName) != null) return true;
            var live = TerraWorld.Live;
            var bench = BenchTransform(scene, pd.respawnMarkerName);
            if (live == null || bench == null) return false;
            // Still the old respawn point of this same scene (a death's marker, another bench):
            // the bench the player sits on is the one right beside him.
            if (Vector2.Distance(bench.position, hero.transform.position) > 5f) return false;
            var origin = (Vector2)live.CellOffset * CellGrid.CellSize;
            if (!PlaceBeside(live, bench.position, out var cell, out int looseness))
            {
                Plugin.Log.LogWarning($"[晶塔] {scene} 的 {pd.respawnMarkerName} 旁边找不到放晶塔的地方");
                return true;
            }
            var p = new Pylon
            {
                Scene = scene,
                Bench = pd.respawnMarkerName,
                Local = CellGrid.CellCenter(cell.x, cell.y) - Vector2.one * (CellGrid.CellSize / 2f) - origin,
                SceneSize = new Vector2(live.Grid.Width, live.Grid.Height) * CellGrid.CellSize,
                Style = StyleFor(scene),
            };
            Unlocked.Add(p);
            string loose = looseness > 0 ? $"（地方窄，放宽到第 {looseness} 档）" : "";
            Plugin.Log.LogInfo($"[晶塔] 在 {scene} 的 {p.Bench} 旁解锁了{p.Name}，位置（场景坐标）{p.Local}{loose}");
            var obj = Spawn(p, live);
            if (obj != null) obj.Appear();
            TerraMod.Instance?.PlaySound("Item_4", 0.8f);
            UI.CombatText.Show(hero.transform.position + Vector3.up * 1.2f, Lang.F("解锁了{0}", Lang.T(p.Name)), UI.CombatText.Pickup, true);
            return true;
        }

        /// <summary>The bench a respawn marker names, in a loaded scene.</summary>
        static Transform BenchTransform(string scene, string marker)
        {
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint"))
                if (go.name == marker && go.scene.name == scene) return go.transform;
            return null;
        }

        /// <summary>
        /// A spot for the pylon on the floor next to a bench: three empty columns four cells high
        /// over solid ground, a little way to the right of the bench, else to the left. Every
        /// bench gets one: where there's no such room the search loosens step by step (nearer
        /// and further, more height either way, platforms and torches in the way, then only
        /// two cells of headroom), and at worst it stands right beside the bench. Looseness 0
        /// is the plain rule.
        /// </summary>
        static bool PlaceBeside(TerrainRegion region, Vector2 bench, out Vector2Int cell, out int looseness)
        {
            var benchCell = CellGrid.WorldToCell(bench);
            // The bench stands on the floor: find it under the bench.
            int floor = benchCell.y;
            for (int k = 0; k < 12 && !TerraWorld.IsSolid(benchCell.x, floor - 1); k++) floor--;
            var steps = new (int dMin, int dMax, int dyMax, bool overItems, int head)[]
            {
                (6, 16, 6, false, HeightCells),
                (3, 24, 10, false, HeightCells),
                (3, 24, 10, true, HeightCells),
                (2, 30, 14, true, 2),
            };
            for (looseness = 0; looseness < steps.Length; looseness++)
            {
                var st = steps[looseness];
                foreach (int side in new[] { 1, -1 })
                    for (int d = st.dMin; d <= st.dMax; d++)
                    {
                        int x0 = side > 0 ? benchCell.x + d : benchCell.x - d - WidthCells + 1;
                        for (int dy = 0; dy <= st.dyMax; dy++)
                            foreach (int y in new[] { floor + dy, floor - dy })
                                if (Fits(region, x0, y, st.overItems, st.head))
                                {
                                    cell = new Vector2Int(x0, y);
                                    return true;
                                }
                    }
            }
            // Nowhere to stand: right beside the bench, on its floor.
            looseness = steps.Length;
            cell = new Vector2Int(benchCell.x + 3, floor);
            return region.Contains(cell.x, cell.y);
        }

        static bool Fits(TerrainRegion region, int x0, int y, bool overItems, int head)
        {
            for (int x = x0; x < x0 + WidthCells; x++)
            {
                if (!region.Contains(x, y - 1) || !TerraWorld.IsSolid(x, y - 1)) return false;
                for (int k = 0; k < head; k++)
                    if (!region.Contains(x, y + k) || (overItems ? TerraWorld.IsSolid(x, y + k) : TerraWorld.IsOccupied(x, y + k))) return false;
            }
            return true;
        }

        // --- Pylons in loaded scenes. ---

        /// <summary>A scene's grid came into the world (the one played or a neighbour): put its pylons up.</summary>
        public static void OnRegionReady(TerrainRegion region)
        {
            spawned.RemoveAll(o => o == null);
            foreach (var p in Unlocked)
            {
                if (p.Scene != region.SceneName) continue;
                if (p.Provisional)
                {
                    var bench = BenchTransform(p.Scene, p.Bench);
                    if (bench != null && PlaceBeside(region, bench.position, out var cell, out _))
                    {
                        var origin = (Vector2)region.CellOffset * CellGrid.CellSize;
                        p.Local = CellGrid.CellCenter(cell.x, cell.y) - Vector2.one * (CellGrid.CellSize / 2f) - origin;
                        p.Provisional = false;
                    }
                }
                Spawn(p, region);
            }
        }

        static PylonObject Spawn(Pylon p, TerrainRegion region)
        {
            foreach (var o in spawned)
                if (o != null && o.Pylon == p && o.transform.parent == region.Root) return o;
            var go = new GameObject("Terra Pylon " + p.Name);
            go.transform.SetParent(region.Root, false);
            go.transform.localPosition = new Vector3(p.Local.x, p.Local.y, 0f);
            var obj = go.AddComponent<PylonObject>();
            obj.Init(p);
            spawned.Add(obj);
            return obj;
        }

        // --- Using one. ---

        /// <summary>The pylon under the mouse within reach of the player, if any.</summary>
        static PylonObject Hovered(HeroController hero, Vector2 mouse)
        {
            Vector2 at = hero.transform.position;
            foreach (var o in spawned)
            {
                if (o == null || !o.isActiveAndEnabled) continue;
                if (!o.Bounds.Contains(mouse)) continue;
                if (Vector2.Distance(o.Bounds.center, at) > UseRange) continue;
                return o;
            }
            return null;
        }

        public static bool HoveringPylon { get; private set; }
        /// <summary>The hovered pylon's item texture (Terraria's cursor item icon).</summary>
        public static string HoverIcon { get; private set; } = "Item_4876";

        /// <summary>Terraria's pylon items, by style.</summary>
        static readonly int[] StyleItems = { 4876, 4875, 4916, 4917, 4918, 4919, 4920, 4921, 4951 };

        static void HandleUse(HeroController hero)
        {
            HoveringPylon = false;
            if (!GameHooks.PlayerHasControl() || Mouse.current == null || UI.FullMap.Open) return;
            if (!TerraMod.MouseWorld(out var mouse)) return;
            var o = Hovered(hero, mouse);
            if (o == null) return;
            HoveringPylon = true;
            HoverIcon = "Item_" + StyleItems[Mathf.Clamp(o.Pylon.Style, 0, StyleItems.Length - 1)];
            if (Mouse.current.rightButton.wasPressedThisFrame) UI.FullMap.OpenForPylons();
        }

        /// <summary>
        /// Off to a pylon's bench: the game's own way back to a bench (its respawn, with the bench
        /// as the respawn point, so the player arrives sitting on it).
        /// </summary>
        public static void Teleport(Pylon p)
        {
            var gm = GameManager.UnsafeInstance;
            var pd = PlayerData.instance;
            var hero = HeroController.SilentInstance;
            if (gm == null || pd == null || hero == null || gm.IsInSceneTransition || hero.cState.dead || hero.cState.transitioning) return;
            Plugin.Log.LogInfo($"[晶塔] 传送到 {p.Scene} 的 {p.Name}（{p.Bench}）");
            PylonObject.TeleportEffect(hero, p.Style);
            gm.StartCoroutine(Travel(gm, pd, hero, p));
        }

        /// <summary>
        /// As the game brings the player back to his bench after dying (GameManager.PlayerDead): the
        /// view fades out, the bench becomes the respawn point, and the respawn fades it back in. Kept
        /// to about a second from the click to moving again: a short fade, the scene loaded at full
        /// priority without the respawn's sweep of unused assets, no getting up off the ground
        /// (<see cref="SkipWakeUp"/>), and a quick portal (Player.OpeningPortal).
        /// </summary>
        static System.Collections.IEnumerator Travel(GameManager gm, PlayerData pd, HeroController hero, Pylon p)
        {
            // Taken away mid-talk: the box goes down with the player.
            UI.HudDialogue.Close();
            hero.RelinquishControl();
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            gm.cameraCtrl.FadeOut(GlobalEnums.CameraFadeType.HERO_DEATH);
            DevTravelStart = Time.realtimeSinceStartup;
            yield return new WaitForSeconds(0.15f);
            prevRespawnScene = pd.respawnScene;
            prevRespawnMarker = pd.respawnMarkerName;
            prevRespawnType = pd.respawnType;
            pd.respawnScene = p.Scene;
            pd.respawnMarkerName = p.Bench;
            // Taken from a bench while sitting: its FSM never stands the player up, and atBench would
            // stay set (drawn seated out of the portal).
            pd.atBench = false;
            if (TerraMod.Instance != null && TerraMod.Instance.Creative) UnlockTollBenches(pd);
            // Brought in standing, not sat on the bench (HeroController.Respawn's other road); the
            // bench is the respawn again once there (Arrive).
            pd.respawnType = 0;
            arrivingAt = p;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            // GameManager.ReadyForRespawn, less its AlwaysUnloadUnusedAssets.
            gm.RespawningHero = true;
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                PreventCameraFadeOut = true, WaitForSceneTransitionCameraFade = false, EntryGateName = "", SceneName = p.Scene,
                Visualization = GameManager.SceneLoadVisualizations.Default, AlwaysUnloadUnusedAssets = false,
            });
        }

        /// <summary>Dev: when the last teleport was clicked (realtime), and how long until the player could move.</summary>
        public static float DevTravelStart, DevTravelTook;

        static float arrivedAt = -10f;

        /// <summary>
        /// HeroController.Respawn (standing) plays "Wake Up Ground" and holds control for its length:
        /// a pylon's arrival steps out of a portal instead, so the clip takes no time then.
        /// </summary>
        [HarmonyLib.HarmonyPatch(typeof(HeroAnimationController), nameof(HeroAnimationController.GetClipDuration))]
        static class SkipWakeUp
        {
            static void Postfix(string clipName, ref float __result)
            {
                if (clipName == "Wake Up Ground" && Time.unscaledTime - arrivedAt < 1f) __result = 0f;
            }
        }

        static Pylon arrivingAt;
        public static int DevArrivals;

        /// <summary>A teleport on its way: the player isn't drawn until he steps out of the portal.</summary>
        public static bool Arriving => arrivingAt != null;

        /// <summary>
        /// The game has put the player in the pylon's scene (SendHeroInPosition): beside the pylon
        /// instead, on whichever side has floor and room, the bench made the respawn point as resting
        /// on it does, and the summoning portal plays there. False when no teleport is arriving.
        /// </summary>
        public static bool Arrive(HeroController hero)
        {
            var p = arrivingAt;
            var gm = GameManager.instance;
            if (p == null || hero == null || gm == null || gm.sceneName != p.Scene) return false;
            arrivingAt = null;
            arrivedAt = Time.unscaledTime;
            var pd = PlayerData.instance;
            if (pd != null) pd.atBench = false;
            // The bench is the respawn point now, as resting on it makes it; one not there to rest on
            // (inactive as Hive_01's) gives the old one back, or dying would bring the player to nothing.
            if (pd != null && BenchUsable(p.Bench)) pd.respawnType = 1;
            else if (pd != null)
            {
                pd.respawnScene = prevRespawnScene;
                pd.respawnMarkerName = prevRespawnMarker;
                pd.respawnType = prevRespawnType;
                Plugin.Log.LogWarning($"[晶塔] {p.Scene} 的长椅 {p.Bench} 现在用不了，复活点留在 {prevRespawnScene} 的 {prevRespawnMarker}");
            }
            DevArrivals++;
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 at = hero.transform.position;
            PylonObject obj = null;
            foreach (var o in spawned) if (o != null && o.Pylon == p) obj = o;
            var region = TerrainSystem.Find(p.Scene);
            Vector2 baseLeft = obj != null ? (Vector2)obj.transform.position : region != null ? (Vector2)region.Root.TransformPoint(p.Local) : at;
            float centre = baseLeft.x + WidthCells * CellGrid.CellSize * 0.5f;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            Vector2 feet = new Vector2(centre, baseLeft.y);
            int dir = 1;
            foreach (int side in new[] { 1, -1 })
            {
                float x = centre + side * (WidthCells * CellGrid.CellSize * 0.5f + 1.3f);
                var down = Physics2D.Raycast(new Vector2(x, baseLeft.y + 1.2f), Vector2.down, 3f, terrain);
                if (down.collider == null) continue;
                var box = new Vector2(col.bounds.size.x, col.bounds.size.y);
                if (Physics2D.OverlapBox(new Vector2(x, down.point.y + box.y * 0.5f + 0.08f), box * 0.95f, 0f, terrain) != null) continue;
                feet = new Vector2(x, down.point.y);
                // Out away from the pylon if there is room that way, else past it.
                var ahead = Physics2D.Raycast(new Vector2(x, down.point.y + box.y * 0.5f), Vector2.right * side, 2.8f, terrain);
                dir = ahead.collider == null ? side : -side;
                break;
            }
            float lift = hero.transform.position.y - col.bounds.min.y;
            hero.transform.position = new Vector3(feet.x, feet.y + lift + 0.02f, hero.transform.position.z);
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            gm.cameraCtrl?.PositionToHero(true);
            Player.OpeningPortal.Summon(hero, feet, Areas.AreaName(p.Scene), dir);
            return true;
        }

        /// <summary>
        /// The Recall Potion (Player.Spawn via TeleportationPotion style 0... Pl:52512): home, which
        /// in Hallownest is the bench last rested on, by the same respawn road as a pylon's.
        /// </summary>
        public static bool Recall()
        {
            var gm = GameManager.UnsafeInstance;
            var pd = PlayerData.instance;
            var hero = HeroController.SilentInstance;
            if (gm == null || pd == null || hero == null || gm.IsInSceneTransition || hero.cState.dead || hero.cState.transitioning) return false;
            if (string.IsNullOrEmpty(pd.respawnScene) || string.IsNullOrEmpty(pd.respawnMarkerName)) return false;
            Plugin.Log.LogInfo($"[回忆药水] 回到 {pd.respawnScene} 的 {pd.respawnMarkerName}");
            Player.PotionBuffs.Sparkle(hero.GetComponent<BoxCollider2D>().bounds.center);
            DevRecalls++;
            gm.StartCoroutine(RecallTravel(gm, pd, hero));
            return true;
        }

        public static int DevRecalls;

        static System.Collections.IEnumerator RecallTravel(GameManager gm, PlayerData pd, HeroController hero)
        {
            UI.HudDialogue.Close();
            hero.RelinquishControl();
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            gm.cameraCtrl.FadeOut(GlobalEnums.CameraFadeType.HERO_DEATH);
            yield return new WaitForSeconds(0.6f);
            gm.ReadyForRespawn(false);
        }

        /// <summary>The player has come into a scene: a teleport that didn't arrive there is dropped (its scene failed to load).</summary>
        public static void OnSceneReady()
        {
            var gm = GameManager.UnsafeInstance;
            if (arrivingAt != null && gm != null && gm.sceneName != arrivingAt.Scene && !gm.IsInSceneTransition) arrivingAt = null;
        }
    }
}
