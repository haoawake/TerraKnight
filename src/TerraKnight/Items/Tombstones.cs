using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's tombstones. A death, any (a spike's too), throws one off the body (Player.DropTombstone: one of six,
    /// a gold one with over 10 gold in the purse) that tumbles as Projectile AI_017 does (0.2 px/tick² down, 2% of its
    /// roll lost a tick on the ground, three quarters back off a wall, seven tenths up off the floor from a fall over
    /// 1.5) and stands where it comes to rest on even ground with room for it (TileObject.CanPlace, tile 85). One that
    /// finds no such ground in 10 s lies there as its item instead. Its whole tumble is worked out as it is thrown and
    /// played out; a scene left before it ends (the respawn) gets the outcome at once, in its own coordinates.
    /// Seven of them about (28 tiles of 85 in the scan around the player, SceneMetrics) make a graveyard: the light
    /// dims, the graveyard's recipes can be made, and as in WorldGen.TryGrowingAbigailsFlower each tombstone now and
    /// then grows an Abigail's Flower within 10 tiles, none within 120 of another; a right click picks it.
    /// </summary>
    internal static class Tombstones
    {
        const float Px = TerraParticles.Px;
        const int Lifetime = 600;
        const float Z = 0.0032f;

        static readonly StationKind[] kinds =
        {
            StationKind.Tombstone, StationKind.GraveMarker, StationKind.CrossGraveMarker, StationKind.Headstone, StationKind.Gravestone, StationKind.Obelisk,
            StationKind.RichGravestone1, StationKind.RichGravestone2, StationKind.RichGravestone3, StationKind.RichGravestone4, StationKind.RichGravestone5,
        };
        static readonly ItemType[] items =
        {
            ItemType.Tombstone, ItemType.GraveMarker, ItemType.CrossGraveMarker, ItemType.Headstone, ItemType.Gravestone, ItemType.Obelisk,
            ItemType.RichGravestone1, ItemType.RichGravestone2, ItemType.RichGravestone3, ItemType.RichGravestone4, ItemType.RichGravestone5,
        };
        /// <summary>The thrown ones' projectiles: 43, 201-205, 527-531.</summary>
        static readonly int[] projectiles = { 43, 201, 202, 203, 204, 205, 527, 528, 529, 530, 531 };

        sealed class Flight
        {
            public int Style;
            public string Scene;
            public Vector2 Root, Start;
            public readonly List<Vector3> Path = new List<Vector3>();
            /// <summary>Where it stands at the end (its bottom middle, world), or none: an item there.</summary>
            public bool Lands;
            public Vector2 Bottom;
            public float Age;
            public SpriteRenderer Sprite;
        }

        static readonly List<Flight> flights = new List<Flight>();
        static Runner runner;

        public static int DevThrown, DevStood, DevItems, DevGrown;
        public static string DevLast = "";

        /// <summary>Player.DropTombstone, when the player dies: one thrown off the body, away from the blow.</summary>
        public static void OnDeath(HeroController hero, int hitDirection)
        {
            if (hero == null || TerraMod.Instance == null || Net.WorldSync.Guest && !Net.WorldSync.CanInteract) return;
            var body = hero.GetComponent<BoxCollider2D>().bounds;
            if (hitDirection == 0) hitDirection = Random.value < 0.5f ? -1 : 1;
            float extra;
            for (extra = Random.Range(-35, 36) * 0.1f; extra < 2f && extra > -2f; extra += Random.Range(-30, 31) * 0.1f) { }
            int style = Random.Range(0, 6);
            if (Shop.Purse > 100000) style = 6 + Random.Range(0, 5);
            var v = new Vector2(Random.Range(10, 30) * 0.1f * hitDirection + extra, Random.Range(-40, -20) * 0.1f);
            Throw(style, body.center, v);
        }

        /// <summary>A tombstone thrown from a point (world) at a speed (px a tick, Terraria's: y down).</summary>
        public static void Throw(int style, Vector2 from, Vector2 velocity)
        {
            var cell = CellGrid.WorldToCell(from);
            var region = TerraWorld.RegionAt(cell.x, cell.y) ?? TerrainSystem.Current?.Region;
            if (region == null) return;
            var f = new Flight { Style = Mathf.Clamp(style, 0, kinds.Length - 1), Scene = region.SceneName, Root = region.Root.position, Start = from };
            Simulate(f, from, velocity);
            flights.Add(f);
            DevThrown++;
            EnsureRunner();
        }

        /// <summary>AI_017 and its tile collision, tick by tick, until it can stand or its time is up.</summary>
        static void Simulate(Flight f, Vector2 pos, Vector2 v)
        {
            const int mask = 1 << ChunkColliders.TerrainLayer;
            var size = new Vector2(24f, 24f) * Px * 0.98f;
            const float skin = 0.01f;
            float rot = 0f;
            var kind = kinds[f.Style];
            for (int t = 0; t < Lifetime; t++)
            {
                bool onGround = v.y == 0f;
                if (onGround) v.x *= 0.98f;
                rot += v.x * 0.1f;
                v.y = Mathf.Min(v.y + 0.2f, 16f);
                float dx = v.x * Px;
                if (Mathf.Abs(dx) > 1e-5f)
                {
                    var hit = Physics2D.BoxCast(pos, size, 0f, new Vector2(Mathf.Sign(dx), 0f), Mathf.Abs(dx), mask);
                    if (hit.collider != null && !OneWay(hit.collider))
                    {
                        pos.x += Mathf.Sign(dx) * Mathf.Max(0f, hit.distance - skin);
                        v.x *= -0.75f;
                    }
                    else pos.x += dx;
                }
                float dy = -v.y * Px;
                if (Mathf.Abs(dy) > 1e-5f)
                {
                    var hit = Physics2D.BoxCast(pos, size, 0f, new Vector2(0f, Mathf.Sign(dy)), Mathf.Abs(dy), mask);
                    // One-way platforms hold it only from above.
                    if (hit.collider != null && !(dy > 0f && OneWay(hit.collider)))
                    {
                        pos.y += Mathf.Sign(dy) * Mathf.Max(0f, hit.distance - skin);
                        v.y = v.y > 1.5f ? v.y * -0.7f : 0f;
                    }
                    else pos.y += dy;
                }
                // Out of every scene's grid (off a room's edge) it goes on falling, unseen past its last place inside.
                var bottom = new Vector2(pos.x, pos.y - 12f * Px);
                var cell = CellGrid.WorldToCell(bottom + new Vector2(0f, 0.05f));
                if (TerraWorld.RegionAt(cell.x, cell.y) == null) continue;
                f.Path.Add(new Vector3(pos.x, pos.y, rot));
                // Its bottom on ground that takes it (the cell its bottom is in, as TileObject.CanPlace there).
                if (Stations.FindSpot(kind, null, bottom + new Vector2(0f, 0.05f), out var spot) && Mathf.Abs(spot.y - bottom.y) < 0.3f)
                {
                    f.Lands = true;
                    f.Bottom = spot;
                    return;
                }
            }
            // Time up: an item where it was last seen (where it was thrown, if it left the grid at once).
            if (f.Path.Count == 0) f.Path.Add(new Vector3(f.Start.x, f.Start.y, 0f));
        }

        static bool OneWay(Collider2D c) => c.usedByEffector && c.GetComponent<PlatformEffector2D>() != null;

        /// <summary>Its tumble played out at Terraria's 60 a second; then it stands, or lies there as its item.</summary>
        static void Play(float dt)
        {
            var gm = GameManager.UnsafeInstance;
            bool leaving = gm == null || gm.IsInSceneTransition;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                bool loaded = TerrainSystem.Find(f.Scene) != null;
                f.Age += dt * 60f;
                int at = Mathf.Min((int)f.Age, f.Path.Count - 1);
                if (leaving || !loaded || at >= f.Path.Count - 1)
                {
                    Finish(f, loaded && !leaving);
                    flights.RemoveAt(i);
                    continue;
                }
                if (f.Sprite == null)
                {
                    var go = new GameObject("Terra Tombstone");
                    Object.DontDestroyOnLoad(go);
                    f.Sprite = go.AddComponent<SpriteRenderer>();
                    f.Sprite.sprite = Terraria.TerrariaAssets.Sprite("Projectile_" + projectiles[f.Style], 1f / Px);
                    f.Sprite.sortingOrder = LightMap.Order - 5;
                }
                var p = f.Path[at];
                f.Sprite.transform.position = new Vector3(p.x, p.y, Z);
                f.Sprite.transform.rotation = Quaternion.Euler(0f, 0f, -p.z * Mathf.Rad2Deg);
            }
        }

        static void Finish(Flight f, bool here)
        {
            if (f.Sprite != null) Object.Destroy(f.Sprite.gameObject);
            var kind = kinds[f.Style];
            var end = f.Path.Count > 0 ? (Vector2)f.Path[f.Path.Count - 1] : f.Root;
            if (f.Lands)
            {
                // Set down now (it is checked again: something may have come there meanwhile), or into its scene as it was worked out.
                if (here)
                {
                    if (Stations.TryPlaceNatural(kind, f.Bottom + new Vector2(0f, 0.05f), out _)) TerraMod.Instance?.PlaySound("Dig_0");
                    else WorldItems.Drop(ItemStack.Of(items[f.Style]), end);
                }
                else Stations.PlaceIn(kind, f.Scene, f.Bottom - f.Root);
                DevStood++;
                DevLast = $"{kind} 立在 {f.Scene} {f.Bottom:F1}（滚了 {f.Path.Count} 帧）";
            }
            else
            {
                if (here) WorldItems.Drop(ItemStack.Of(items[f.Style]), end);
                else WorldItems.DropIn(ItemStack.Of(items[f.Style]), f.Scene, end - f.Root);
                DevItems++;
                DevLast = $"{items[f.Style]} 没处可立，成了物品，在 {f.Scene} {end:F1}";
            }
            Plugin.Log.LogInfo($"[墓碑] {DevLast}");
        }

        // ------------------------------------------------------------------ the graveyard

        /// <summary>Seven tombstones or more about the player (SceneMetrics.EnoughTilesForGraveyard: 28 tiles of 85).</summary>
        public static bool InGraveyard { get; private set; }

        /// <summary>How far the graveyard's gloom has come in (0 to 1), as Main.GraveyardVisualIntensity eases.</summary>
        public static float Gloom { get; private set; }

        /// <summary>The light of the room's open air, times this: a graveyard is darker.</summary>
        public static float LightFactor => 1f - 0.35f * Gloom;

        public static int DevGraves;
        static float nextScan, growTicks;

        static void Scan()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || TerrainSystem.Current == null)
            {
                InGraveyard = false;
                DevGraves = 0;
                return;
            }
            // SceneMetrics.ZoneScanSize: the screen Terraria assumes (1920x1200) and 25 tiles round it.
            Vector2 c = hero.transform.position;
            float hw = (1920f / 16f / 2f + 25f) * CellGrid.CellSize, hh = (1200f / 16f / 2f + 25f) * CellGrid.CellSize;
            int n = 0;
            foreach (var (_, box) in Stations.Standing(Stations.IsGrave))
                if (Mathf.Abs(box.center.x - c.x) <= hw && Mathf.Abs(box.center.y - c.y) <= hh) n++;
            DevGraves = n;
            InGraveyard = n * 4 >= 28;
        }

        /// <summary>WorldGen.TryGrowingAbigailsFlower for each tombstone standing, at about Terraria's pace for a tile's random update.</summary>
        static void Grow()
        {
            var graves = Stations.Standing(Stations.IsGrave);
            if (graves.Count == 0) return;
            var flowers = Stations.Standing(k => k == StationKind.AbigailsFlower);
            foreach (var (_, box) in graves)
                if (Random.Range(0, 30) == 0 && TryGrowNear(box.center, flowers)) return;
        }

        static bool TryGrowNear(Vector2 grave, List<(StationKind Kind, Rect Box)> flowers)
        {
            const float cell = CellGrid.CellSize;
            var at = grave + new Vector2(Random.Range(-10, 11), Random.Range(-10, 11)) * cell;
            foreach (var (_, box) in flowers)
                if (Mathf.Abs(box.center.x - at.x) <= 120f * cell && Mathf.Abs(box.center.y - at.y) <= 120f * cell) return false;
            if (!Stations.TryPlaceNatural(StationKind.AbigailsFlower, at, out var bottom)) return false;
            DevGrown++;
            Plugin.Log.LogInfo($"[墓碑] 墓碑旁长出了阿比盖尔的花：{bottom:F1}");
            return true;
        }

        /// <summary>Dev: one tombstone standing tries its growth now, whatever the dice.</summary>
        public static bool DevGrowNow()
        {
            var flowers = Stations.Standing(k => k == StationKind.AbigailsFlower);
            foreach (var (_, box) in Stations.Standing(Stations.IsGrave))
                for (int k = 0; k < 60; k++)
                    if (TryGrowNear(box.center, flowers)) return true;
            return false;
        }

        // ------------------------------------------------------------------ each frame

        static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("Terra Tombstones");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        /// <summary>Started with the mod: the graveyard's watch runs from the first frame.</summary>
        public static void Start() => EnsureRunner();

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                var gm = GameManager.UnsafeInstance;
                bool menu = gm == null || gm.gameState == GlobalEnums.GameState.MAIN_MENU || gm.sceneName == "Menu_Title" || gm.sceneName == "Quit_To_Menu";
                if (menu)
                {
                    for (int i = flights.Count - 1; i >= 0; i--) if (flights[i].Sprite != null) Destroy(flights[i].Sprite.gameObject);
                    flights.Clear();
                    InGraveyard = false;
                    Gloom = 0f;
                    return;
                }
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                if (flights.Count > 0) Play(dt);
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 0.25f;
                    Scan();
                }
                Gloom = Mathf.MoveTowards(Gloom, InGraveyard ? 1f : 0f, dt / 2f);
                if (gm.isPaused) return;
                // A tile's random update comes round about every ten seconds or so for the few tiles of a tombstone.
                growTicks += dt * 60f;
                if (growTicks >= 600f)
                {
                    growTicks = 0f;
                    Grow();
                }
            }
        }
    }
}
