using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Items lying in the world (Terraria 1.4.5's WorldItem): thrown from the inventory, spilled
    /// when the cursor's stack doesn't fit back, dropped by mined blocks. Each sits in a 16×16 px
    /// box, falls at 0.1 px/tick² up to 7, loses 5% of its sideways speed a tick, lands on blocks
    /// and on platforms (from above) without bouncing, and merges with the same item nearby. The
    /// player pulls items in from 42 px around his hitbox (0.45 px/tick² on each axis, at most
    /// 4 px/tick, straight through walls) and picks up what touches him; a thrown item can't be
    /// taken back for 100 ticks.
    /// An item belongs to the scene it lies in, in that scene's own coordinates: it sleeps while
    /// the scene isn't loaded and is saved with the mod's save.
    /// </summary>
    internal sealed partial class WorldItems : MonoBehaviour
    {
        const float Px = Effects.TerraParticles.Px;
        /// <summary>Half the 16 px box, world units.</summary>
        const float Half = 8f * Px;
        const int MaxItems = 400;
        const int ThrowGrabDelay = 100;
        const float GrabRange = 42f * Px;
        const float Z = 0.0032f;

        sealed class Item
        {
            public string NetId;
            public int Dropper = -1;
            public int PhysicsOwner = -1;
            public bool ClaimPending;
            public ItemStack Stack;
            public string Scene;
            /// <summary>Box centre, in the scene's own coordinates.</summary>
            public Vector2 Local;
            /// <summary>px per tick, y up.</summary>
            public Vector2 Vel;
            public int GrabDelay, Age, OutTicks;
            public bool Grabbed;
            /// <summary>A Fallen Star that fell from the night sky: gone at daybreak unless picked up.</summary>
            public bool FromSky;
            public SpriteRenderer Renderer;
            TerrainRegion region;
            /// <summary>The item's scene while it is laid out; null from the moment that scene goes (not only from the next tick).</summary>
            public TerrainRegion Region
            {
                get => region != null && region.Root != null ? region : null;
                set => region = value;
            }
            public Vector2 World => Region != null ? (Vector2)Region.Root.position + Local : Local;
        }

        /// <summary>What the save keeps of an item.</summary>
        public struct Saved
        {
            public string Scene;
            public ItemType Type;
            public int Count;
            public Vector2 Local;
        }

        public static WorldItems Instance { get; private set; }
        static readonly List<Item> items = new List<Item>();

        /// <summary>Dev: the renderers of the items lying now.</summary>
        public static IEnumerable<SpriteRenderer> DevRenderers
        {
            get { foreach (var it in items) if (it.Renderer != null) yield return it.Renderer; }
        }

        /// <summary>Dev: every lying item gone.</summary>
        public static void DevClear() => Clear();

        /// <summary>Dev: how many of an item lie in the world.</summary>
        public static int DevCount(ItemType t)
        {
            int n = 0;
            foreach (var it in items) if (!it.Stack.IsEmpty && it.Stack.Type == t) n += it.Stack.Count;
            return n;
        }
        static readonly Dictionary<(ItemType, int), Sprite> sprites = new Dictionary<(ItemType, int), Sprite>();
        float ticks;

        public static WorldItems Create(Transform parent)
        {
            var go = new GameObject("Terra World Items");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<WorldItems>();
            return Instance;
        }

        public static int Count => items.Count;

        /// <summary>Where each item lying in a loaded scene is (the Spelunker potion's glint).</summary>
        public static IEnumerable<Vector2> Positions()
        {
            foreach (var it in items)
                if (it.Region != null && !it.Stack.IsEmpty) yield return it.World;
        }

        /// <summary>Dev: where the first item lies (box centre), if any.</summary>
        public static Vector2? DevFirst => items.Count > 0 ? items[0].World : (Vector2?)null;

        /// <summary>Dev: each item, its place and state.</summary>
        public static string DevDescribe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var it in items)
                sb.Append($"{it.Stack.Type}×{it.Stack.Count}@{it.World:F2} 速度{it.Vel:F2} 延迟{it.GrabDelay} {(it.Region == null ? "休眠" : it.Scene)}；");
            return sb.ToString();
        }

        // --- Into the world. ---

        /// <summary>Player.DropSelectedItem: from the middle of the player, forward and up, out of his reach for 100 ticks.</summary>
        public static void Throw(ItemStack stack, HeroController hero)
        {
            if (stack.IsEmpty || hero == null) return;
            var body = hero.GetComponent<BoxCollider2D>().bounds;
            float dir = hero.cState.facingRight ? 1f : -1f;
            float heroVx = hero.GetComponent<Rigidbody2D>().linearVelocity.x / Player.TerraMovement.PxT;
            Spawn(stack, body.center, new Vector2(4f * dir + heroVx, 2f), ThrowGrabDelay);
        }

        /// <summary>Item.NewItem with no velocity given: a random hop, up to 3 px/tick sideways and 1.6-4 up.</summary>
        public static void Drop(ItemStack stack, Vector2 centre, bool fromSky = false, bool personal = false)
        {
            var vel = new Vector2(Random.Range(-30, 31) * 0.1f, Random.Range(16, 41) * 0.1f);
            Spawn(stack, centre, vel, 0, fromSky, personal);
        }

        static void Spawn(ItemStack stack, Vector2 centre, Vector2 vel, int grabDelay, bool fromSky = false, bool personal = false)
        {
            var region = RegionAt(centre) ?? TerrainSystem.Current?.Region;
            if (region == null) return;
            if (Net.NetSession.Active && !personal && (Net.NetSession.Hosting || Net.WorldSync.CanInteract))
            {
                var local = centre - (Vector2)region.Root.position;
                var action = new Net.WorldSync.Action { Op = "drop", Scene = region.SceneName, X = local.x, Y = local.y,
                    Vx = vel.x, Vy = vel.y, Power = grabDelay, Sky = fromSky, Kind = (int)stack.Type, Count = stack.Count };
                if (Net.NetSession.Hosting) NetSpawn(action, Net.NetSession.MySlot);
                else Net.WorldSync.Submit(action);
                return;
            }
            stack.Favorite = false;
            if (items.Count >= MaxItems)
            {
                // The oldest makes way (Item.PickAnItemSlotToSpawnItemOn).
                int oldest = -1;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].NetId == null && (oldest < 0 || items[i].Age > items[oldest].Age)) oldest = i;
                if (oldest >= 0) Remove(oldest);
            }
            items.Add(new Item
            {
                Stack = stack, Region = region, Scene = region.SceneName,
                Local = centre - (Vector2)region.Root.position, Vel = vel, GrabDelay = grabDelay, FromSky = fromSky,
            });
        }

        // --- Per tick. ---

        void Update()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || TerrainSystem.Current == null) return;
            ticks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (ticks >= 1f)
            {
                ticks -= 1f;
                Tick(hero);
                UI.PopupText.Tick();
            }
            Draw();
        }

        static void Tick(HeroController hero)
        {
            for (int i = 0; i < items.Count; i++) items[i].Region = RegionNamed(items[i].Scene);
            var inv = TerraMod.Instance.Inventory;
            if (!hero.cState.dead) GrabItems(hero, inv);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Region == null) continue;
                if (!NetSimulates(it)) continue;
                if (it.Grabbed)
                {
                    it.Local += it.Vel * Px;
                    it.Grabbed = false;
                }
                else
                {
                    if (it.NetId == null || !Net.WorldSync.Guest) Combine(i);
                    // MoveInWorld: gravity, sideways friction, then the blocks.
                    it.Vel.y = Mathf.Max(it.Vel.y - 0.1f, -7f);
                    it.Vel.x *= 0.95f;
                    if (Mathf.Abs(it.Vel.x) < 0.1f) it.Vel.x = 0f;
                    Move(it);
                }
                Adopt(it);
                if (it.GrabDelay > 0) it.GrabDelay--;
                it.Age++;
                if (it.FromSky && !it.Grabbed && World.TerraTime.IsDay) Fade(it);
            }
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].Stack.IsEmpty || items[i].OutTicks > 120)
                {
                    if (Net.WorldSync.Guest && items[i].NetId != null && NetSimulates(items[i]))
                        Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "motion", Motion = new[] { new Net.WorldSync.Record { Key = items[i].NetId, Taken = true } } });
                    Remove(i);
                }
        }

        /// <summary>Player.GrabItems: pick up what touches the player, pull in what's within reach and fits.</summary>
        static void GrabItems(HeroController hero, Inventory inv)
        {
            if (!Net.WorldSync.CanInteract) return;
            var body = hero.GetComponent<BoxCollider2D>().bounds;
            // The Treasure Magnet reaches 150 px further for everything (treasureGrabRange, Pl:35428-35431).
            float range = GrabRange + (Equipment.Current.TreasureMagnet ? 150f * Px : 0f);
            var reach = new Bounds(body.center, body.size + Vector3.one * (range * 2f));
            // The hearts reach 250 px further with the Heartreach potion (lifeMagnet, Item.lifeGrabRange).
            float heartRange = range + (Player.PlayerBuffs.Has(Player.PotionBuffs.Heartreach) ? 250f * Px : 0f);
            var heartReach = new Bounds(body.center, body.size + Vector3.one * (heartRange * 2f));
            Vector2 heroVel = hero.GetComponent<Rigidbody2D>().linearVelocity / Player.TerraMovement.PxT;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Region == null || (it.GrabDelay > 0 && (it.NetId == null || it.Dropper == Net.NetSession.MySlot)) || it.Stack.IsEmpty) continue;
                var c = it.World;
                var box = new Bounds(new Vector3(c.x, c.y, body.center.z), new Vector3(Half * 2f, Half * 2f, 1f));
                bool heart = it.Stack.Type == ItemType.Heart;
                if (box.Intersects(body))
                {
                    if (heart && it.NetId == null)
                    {
                        // Player.PickupItem: a heart heals 20 and is gone (taken at full life too), never carried.
                        int n = it.Stack.Count;
                        TerraMod.Instance.Stats.Heal(20 * n);
                        UI.CombatText.Show(body.center + Vector3.up * 0.9f, (20 * n).ToString(), UI.CombatText.Heal);
                        TerraMod.Instance.PlaySound("Grab", 0.7f);
                        it.Stack = default;
                        continue;
                    }
                    if (it.NetId != null && Net.WorldSync.Guest)
                    {
                        if (!it.ClaimPending && inv.Room(it.Stack.Type) > 0)
                        {
                            it.ClaimPending = true;
                            Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "claim", Key = it.NetId, Count = Mathf.Min(it.Stack.Count, inv.Room(it.Stack.Type)) });
                        }
                        continue;
                    }
                    int count = it.Stack.Count;
                    int left = inv.Pickup(it.Stack.Type, count);
                    if (left == count) continue;
                    UI.PopupText.Show(it.Stack.Def, count - left, body.center);
                    TerraMod.Instance.PlaySound("Grab", 0.7f);
                    it.Stack.Count = left;
                    if (left <= 0) it.Stack = default;
                    continue;
                }
                if (!box.Intersects(heart ? heartReach : reach) || (!heart && inv.Room(it.Stack.Type) <= 0)) continue;
                if (it.NetId != null && Net.WorldSync.Guest)
                {
                    // The authority may have no local hero near this item. Request the same
                    // magnet-range pickup, rather than simulating a second authoritative item.
                    if (!it.ClaimPending)
                    {
                        it.ClaimPending = true;
                        Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "claim", Key = it.NetId, Count = Mathf.Min(it.Stack.Count, inv.Room(it.Stack.Type)) });
                    }
                    continue;
                }
                // PullItem_Common: towards the player's middle on each axis on its own.
                if (body.center.x > c.x)
                {
                    if (it.Vel.x < 4f + heroVel.x) it.Vel.x += 0.45f;
                    if (it.Vel.x < 0f) it.Vel.x += 0.3375f;
                }
                else
                {
                    if (it.Vel.x > -4f + heroVel.x) it.Vel.x -= 0.45f;
                    if (it.Vel.x > 0f) it.Vel.x -= 0.3375f;
                }
                if (body.center.y < c.y)
                {
                    if (it.Vel.y > -4f) it.Vel.y -= 0.45f;
                    if (it.Vel.y > 0f) it.Vel.y -= 0.3375f;
                }
                else
                {
                    if (it.Vel.y < 4f) it.Vel.y += 0.45f;
                    if (it.Vel.y < 0f) it.Vel.y += 0.3375f;
                }
                it.Grabbed = true;
            }
        }

        /// <summary>
        /// WorldItem.DespawnIfMeetingConditions: a Fallen Star lying in the daylight goes in ten sparkles (15) and
        /// three star gores. Only the sky's: the ones enemies drop and the player throws stay.
        /// </summary>
        static void Fade(Item it)
        {
            var tl = Effects.TerraParticles.ToPx(it.World) - new Vector2(8f, 8f);
            var v = new Vector2(it.Vel.x, -it.Vel.y);
            var pp = Effects.TerraParticles.Instance;
            if (pp != null)
            {
                for (int i = 0; i < 10; i++) pp.NewDust(tl, 16, 16, 15, v.x, v.y, 150, 1.2f);
                for (int i = 0; i < 3; i++) pp.NewGore(tl, v, Random.Range(16, 18));
            }
            it.Stack = default;
            DevFaded++;
        }

        /// <summary>Dev: how many of the sky's Fallen Stars went at daybreak.</summary>
        public static int DevFaded;

        /// <summary>How many of the sky's Fallen Stars lie in the world.</summary>
        public static int SkyStarsLying()
        {
            int n = 0;
            foreach (var it in items) if (it.FromSky && !it.Stack.IsEmpty) n += it.Stack.Count;
            return n;
        }

        /// <summary>TryCombiningIntoNearbyItems: the same stackable item within 30 px (summed along x and y) joins this one.</summary>
        static void Combine(int i)
        {
            var it = items[i];
            int max = it.Stack.Def.MaxStack;
            if (max <= 1 || it.Stack.Count >= max) return;
            var c = it.World;
            for (int j = i + 1; j < items.Count && it.Stack.Count < max; j++)
            {
                var o = items[j];
                if (o.Region == null || o.Grabbed || o.Stack.IsEmpty || o.Stack.Type != it.Stack.Type || o.FromSky != it.FromSky
                    || (o.NetId == null) != (it.NetId == null) || o.Dropper != it.Dropper || o.GrabDelay > 0 || it.GrabDelay > 0) continue;
                var d = o.World - c;
                if ((Mathf.Abs(d.x) + Mathf.Abs(d.y)) / Px > 30f) continue;
                int n = Mathf.Min(o.Stack.Count, max - it.Stack.Count);
                o.Stack.Count -= n;
                it.Stack.Count += n;
                if (o.Stack.Count <= 0) o.Stack = default;
            }
        }

        /// <summary>
        /// Collision.TileCollision for a 16×16 box with fallThrough off: blocks stop it on every
        /// side, platforms only from above (it rests on them and passes up and sideways through).
        /// It stops flush against what it meets, with no bounce.
        /// </summary>
        static void Move(Item it)
        {
            var c = it.World;
            float dx = it.Vel.x * Px;
            if (dx != 0f)
            {
                float nx = c.x + dx;
                float edge = dx > 0f ? nx + Half : nx - Half;
                int gx = CellGrid.WorldToCell(new Vector2(edge, 0f)).x;
                if (SolidColumn(gx, c.y - Half + 0.001f, c.y + Half - 0.001f))
                {
                    nx = dx > 0f ? gx * CellGrid.CellSize - Half : (gx + 1) * CellGrid.CellSize + Half;
                    it.Vel.x = 0f;
                }
                c.x = nx;
            }
            float dy = it.Vel.y * Px;
            if (dy != 0f)
            {
                float ny = c.y + dy;
                if (dy < 0f)
                {
                    float bottom = ny - Half;
                    int gy = CellGrid.WorldToCell(new Vector2(0f, bottom)).y;
                    float top = (gy + 1) * CellGrid.CellSize;
                    bool wasAbove = c.y - Half >= top - 0.001f;
                    if (SolidRow(gy, c.x - Half + 0.001f, c.x + Half - 0.001f, wasAbove))
                    {
                        ny = top + Half;
                        it.Vel.y = 0f;
                    }
                }
                else
                {
                    int gy = CellGrid.WorldToCell(new Vector2(0f, ny + Half)).y;
                    if (SolidRow(gy, c.x - Half + 0.001f, c.x + Half - 0.001f, false))
                    {
                        ny = gy * CellGrid.CellSize - Half;
                        it.Vel.y = 0f;
                    }
                }
                c.y = ny;
            }
            it.Local = c - (Vector2)it.Region.Root.position;
        }

        static bool SolidColumn(int gx, float y0, float y1)
        {
            int a = CellGrid.WorldToCell(new Vector2(0f, y0)).y, b = CellGrid.WorldToCell(new Vector2(0f, y1)).y;
            for (int gy = a; gy <= b; gy++) if (TerraWorld.IsSolid(gx, gy)) return true;
            return false;
        }

        static bool SolidRow(int gy, float x0, float x1, bool platforms)
        {
            int a = CellGrid.WorldToCell(new Vector2(x0, 0f)).x, b = CellGrid.WorldToCell(new Vector2(x1, 0f)).x;
            for (int gx = a; gx <= b; gx++)
                if (TerraWorld.IsSolid(gx, gy) || (platforms && TerraWorld.IsPlatform(gx, gy))) return true;
            return false;
        }

        /// <summary>An item that crossed into a neighbouring scene now belongs to it; one that left every scene goes after two seconds.</summary>
        static void Adopt(Item it)
        {
            var w = it.World;
            var here = RegionAt(w);
            if (here == null)
            {
                it.OutTicks++;
                return;
            }
            it.OutTicks = 0;
            if (here == it.Region) return;
            it.Region = here;
            it.Scene = here.SceneName;
            it.Local = w - (Vector2)here.Root.position;
            if (it.Renderer != null) Object.Destroy(it.Renderer.gameObject);
            it.Renderer = null;
        }

        // --- Drawing (Main.DrawItem). ---

        /// <summary>The sprite's bottom on the box's bottom, centred; tilted by its sideways speed (0.2 rad per px/tick).</summary>
        static void Draw()
        {
            foreach (var it in items)
            {
                if (it.Region == null || it.Stack.IsEmpty)
                {
                    if (it.Renderer != null) it.Renderer.enabled = false;
                    continue;
                }
                if (it.Renderer == null)
                {
                    var go = new GameObject("Terra World Item " + it.Stack.Type);
                    go.transform.SetParent(it.Region.Root, false);
                    it.Renderer = go.AddComponent<SpriteRenderer>();
                    // Over the scenery (its foreground and the black of its ground draw nearer than z 0) and under the light map.
                    it.Renderer.sortingOrder = World.LightMap.Order - 5;
                }
                // An animated sheet's frame of the moment (the Fallen Star twinkles, souls pulse).
                it.Renderer.sprite = SpriteOf(it.Stack.Type, ItemFrames.FrameNow(ItemDb.Get(it.Stack.Type).Texture));
                it.Renderer.enabled = true;
                var s = it.Renderer.sprite;
                float h = s != null ? s.rect.height * Px : 0f;
                var t = it.Renderer.transform;
                t.localPosition = new Vector3(it.Local.x, it.Local.y - Half + h / 2f, Z - it.Region.Root.position.z);
                t.localRotation = Quaternion.Euler(0f, 0f, -it.Vel.x * 0.2f * Mathf.Rad2Deg);
            }
        }

        static Sprite SpriteOf(ItemType type, int frame = 0)
        {
            if (sprites.TryGetValue((type, frame), out var s)) return s;
            string name = ItemDb.Get(type).Texture;
            var tex = Terraria.TerrariaAssets.Texture(name);
            s = Sprite.Create(tex, ItemFrames.PixelsOf(tex, name, frame), new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
            sprites[(type, frame)] = s;
            return s;
        }

        /// <summary>Main.DrawMouseOver: the item whose picture is under a world point.</summary>
        public static bool ItemAt(Vector2 world, out ItemStack stack)
        {
            stack = default;
            foreach (var it in items)
            {
                if (it.Region == null || it.Stack.IsEmpty || it.Renderer == null || !it.Renderer.enabled) continue;
                var s = it.Renderer.sprite;
                if (s == null) continue;
                var c = it.World;
                float w = s.rect.width * Px, h = s.rect.height * Px;
                if (world.x < c.x - w / 2f || world.x > c.x + w / 2f || world.y < c.y - Half || world.y > c.y - Half + h) continue;
                stack = it.Stack;
                return true;
            }
            return false;
        }

        // --- Scenes and the save. ---

        static TerrainRegion RegionNamed(string scene)
        {
            foreach (var t in TerrainSystem.All)
                if (t != null && t.Region != null && t.Region.SceneName == scene) return t.Region;
            return null;
        }

        static TerrainRegion RegionAt(Vector2 world)
        {
            var c = CellGrid.WorldToCell(world);
            return TerraWorld.RegionAt(c.x, c.y);
        }

        static void Remove(int i)
        {
            var r = items[i].Renderer;
            if (r != null) Object.Destroy(r.gameObject);
            items.RemoveAt(i);
        }

        public static void Clear()
        {
            for (int i = items.Count - 1; i >= 0; i--) Remove(i);
            UI.PopupText.Clear();
        }

        /// <summary>
        /// One of a kind (UniqueItems): the lying ones of a type beyond one go, all of them when one is had
        /// elsewhere already. Whether one is left lying.
        /// </summary>
        public static bool KeepOne(ItemType type, bool haveOne)
        {
            bool kept = haveOne;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Stack.IsEmpty || it.Stack.Type != type) continue;
                if (it.NetId != null && Net.NetSession.Active)
                {
                    // Personal uniqueness never deletes somebody else's shared property.
                    kept = true;
                    continue;
                }
                if (!kept)
                {
                    kept = true;
                    it.Stack.Count = 1;
                    continue;
                }
                it.Stack = default;
            }
            return kept && !haveOne;
        }

        /// <summary>Whether an item of this kind lies in the world.</summary>
        public static bool Lying(ItemType type)
        {
            foreach (var it in items) if (!it.Stack.IsEmpty && it.Stack.Type == type) return true;
            return false;
        }

        public static List<Saved> Save()
        {
            var list = new List<Saved>(items.Count);
            foreach (var it in items)
                if (!it.Stack.IsEmpty) list.Add(new Saved { Scene = it.Scene, Type = it.Stack.Type, Count = it.Stack.Count, Local = it.Local });
            return list;
        }

        public static void Load(IEnumerable<Saved> saved)
        {
            Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (s.Type != ItemType.None && s.Count > 0)
                    items.Add(new Item { Stack = ItemStack.Of(s.Type, s.Count), Scene = s.Scene, Local = s.Local });
        }
    }
}
