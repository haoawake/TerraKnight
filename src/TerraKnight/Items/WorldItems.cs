using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Items lying in the world (Terraria 1.4.5's WorldItem): thrown from the inventory, spilled
    /// when the cursor's stack doesn't fit back, dropped by mined blocks. Each sits in a 16×16 px
    /// box, falls at 0.1 px/tick² up to 7, loses 5% of its sideways speed a tick, lands on blocks
    /// and on platforms (from above) without bouncing, and merges with the same item nearby; in a boss room's own ground
    /// (the game's colliders kept) it lands on those instead, and one let out inside the ground rises out of it. The
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
        /// <summary>
        /// How far round the player's hitbox items come to them (Terraria's itemGrabRange, 42 px), in Terraria pixels:
        /// almost twice Terraria's here, the Knight floating on Hallownest's pools while what falls in sinks to their floor.
        /// The magnets' reach is added to it (items, hearts, stars; geo too, AccessoryEffects).
        /// </summary>
        internal const float BaseGrabPx = 80f;
        const float GrabRange = BaseGrabPx * Px;
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
            /// <summary>The game's collider it lies on that the grid doesn't hold (a lift, a conveyor...), where that was, and its belt's speed (units a second).</summary>
            public Collider2D On;
            public Vector2 OnAt;
            public float Belt;
            /// <summary>WorldItem.shimmerTime (0-1: how long it has lain in the shimmer) and shimmered (it came out of the shimmer: it floats).</summary>
            public float ShimmerTime;
            public float ShimmerRequestAt = -10f;
            public bool Shimmered;
            /// <summary>Ticks left of the pop it gives as the shimmer changes it.</summary>
            public int Pop;
            /// <summary>Where in its bob a floating item is (each its own).</summary>
            public float Bob = Random.Range(0f, 6.283f);
            /// <summary>In water (or the shimmer) at the last tick: drawn behind it.</summary>
            public bool Wet;
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
            public byte Prefix, Forge;
        }

        public static WorldItems Instance { get; private set; }
        static readonly List<Item> items = new List<Item>();

        /// <summary>Dev: the renderers of the items lying now.</summary>
        public static IEnumerable<SpriteRenderer> DevRenderers
        {
            get { foreach (var it in items) if (it.Renderer != null) yield return it.Renderer; }
        }

        /// <summary>Dev: the items lying now: their network key (null: this game's own), stack, place, and whether drawn.</summary>
        public static IEnumerable<(string NetId, ItemStack Stack, Vector2 At, bool Drawn)> DevItems
        {
            get
            {
                foreach (var it in items)
                    if (!it.Stack.IsEmpty)
                        yield return (it.NetId, it.Stack, it.World, it.Renderer != null && it.Renderer.enabled && it.Renderer.gameObject.activeInHierarchy);
            }
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

        /// <summary>Dev: nothing picked up (to see where drops come to rest).</summary>
        public static bool DevNoGrab;

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
            if (stack.IsEmpty || hero == null || stack.Def.CannotDiscard) return;
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

        /// <summary>Whether an item dropped there would lie somewhere (a scene to lie in).</summary>
        public static bool CanDrop(Vector2 centre) => (RegionAt(centre) ?? TerrainSystem.Current?.Region) != null;

        /// <summary>An item into a scene by its own coordinates, loaded or not: it lies there once the scene comes in.</summary>
        public static void DropIn(ItemStack stack, string scene, Vector2 local)
        {
            if (stack.IsEmpty || string.IsNullOrEmpty(scene)) return;
            stack.Favorite = false;
            items.Add(new Item { Stack = stack, Scene = scene, Local = local, Vel = Vector2.zero });
        }

        /// <summary>The item made (null: none, or a LAN game's, which the host makes).</summary>
        static Item Spawn(ItemStack stack, Vector2 centre, Vector2 vel, int grabDelay, bool fromSky = false, bool personal = false)
        {
            var region = RegionAt(centre) ?? TerrainSystem.Current?.Region;
            if (region == null) return null;
            personal |= !stack.IsEmpty && HallownestRecovery.IsUnique(stack.Type);
            if (Net.NetSession.Active && !personal && (Net.NetSession.Hosting || Net.WorldSync.CanInteract))
            {
                var local = centre - (Vector2)region.Root.position;
                var action = new Net.WorldSync.Action { Op = "drop", Scene = region.SceneName, X = local.x, Y = local.y,
                    Vx = vel.x, Vy = vel.y, Power = grabDelay, Sky = fromSky, Kind = (int)stack.Type, Count = stack.Count, Prefix = stack.Prefix, Forge = stack.Forge };
                if (Net.NetSession.Hosting) NetSpawn(action, Net.NetSession.MySlot);
                else Net.WorldSync.Submit(action);
                return null;
            }
            stack.Favorite = false;
            if (items.Count >= MaxItems)
            {
                // The oldest makes way (Item.PickAnItemSlotToSpawnItemOn); never a spell's weapon (SpellRewards).
                int oldest = -1;
                for (int i = 0; i < items.Count; i++)
                    if (items[i].NetId == null && !SpellRewards.Is(items[i].Stack.Type) && (oldest < 0 || items[i].Age > items[oldest].Age)) oldest = i;
                if (oldest >= 0) Remove(oldest);
            }
            var made = new Item
            {
                Stack = stack, Region = region, Scene = region.SceneName,
                Local = centre - (Vector2)region.Root.position, Vel = vel, GrabDelay = grabDelay, FromSky = fromSky,
            };
            items.Add(made);
            return made;
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
            if (!hero.cState.dead && !DevNoGrab) GrabItems(hero, inv);
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
                    // MoveInWorld: gravity (none for what came out of the shimmer), sideways friction, then the blocks. In water
                    // (any: Hallownest's pools, acid, the hot springs, poured liquid, the shimmer) it floats instead, buoyed up to
                    // the surface and riding there with its lower part in the water.
                    float? surface = it.Shimmered ? null : Fishing.SurfaceAt(it.World - new Vector2(0f, Half));
                    if (!ShimmerTick(it))
                    {
                        if (surface != null) Float(it, surface.Value);
                        else it.Vel.y = Mathf.Max(it.Vel.y - 0.1f, -7f);
                    }
                    it.Vel.x *= surface != null ? 0.9f : 0.95f;
                    if (Mathf.Abs(it.Vel.x) < 0.1f) it.Vel.x = 0f;
                    Move(it);
                    it.Wet = surface != null || Shimmer.In(it.World);
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
        /// <summary>
        /// Player.PickupItem for an item touching the player: a heart or a star taken at once; a LAN guest's shared item claimed
        /// from the host (WorldSync); else sold by the Auto-Sell Bag, or into the Banner Case or the Potion Box, the bag, the
        /// Void Vault, as far as there is room (the rest stays lying), with its popup and the Grab sound.
        /// </summary>
        static void TakeTouching(Item it, Inventory inv, Vector3 at)
        {
            bool heart = it.Stack.Type == ItemType.Heart;
            bool star = it.Stack.Type == ItemType.Star;
            // A LAN game's shared heart or star is the host's to give: a guest claims it (WorldSync), then takes it as here.
            if ((heart || star) && !(it.NetId != null && Net.WorldSync.Guest))
            {
                TakeAtOnce(it.Stack, at);
                it.Stack = default;
                return;
            }
            if (it.NetId != null && Net.WorldSync.Guest)
            {
                if (!it.ClaimPending && (heart || star || Containers.Sells(inv, it.Stack.Type) || inv.Room(it.Stack.Type) > 0))
                {
                    it.ClaimPending = true;
                    Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "claim", Key = it.NetId, Count = ClaimCount(inv, it.Stack) });
                }
                return;
            }
            int count = it.Stack.Count;
            // The Auto-Sell Bag carried: an item on its list is sold as it is picked up (Containers).
            if (Containers.AutoSell(inv, it.Stack, at))
            {
                it.Stack = default;
                return;
            }
            // The Banner Case or the Potion Box carried: its banners or potions go into it first (Containers).
            int left = Containers.Store(inv, it.Stack, count);
            if (left > 0) left = it.Stack.Prefix > 0 || it.Stack.Forge > 0 ? inv.Pickup(it.Stack) : inv.Pickup(it.Stack.Type, left);
            // The Void Bag carried: what the bag had no room for goes into the Void Vault (Player.GetItem's void bag).
            if (left > 0 && World.Chests.VoidBagCarried(inv)) left = World.Chests.StoreInVoid(inv, it.Stack, left);
            if (left == count) { Achievements.AchievementTracker.BagFull(it); return; }
            if (it.Stack.Type == ItemType.FallenStar) Achievements.AchievementTracker.StarsPicked(count - left);
            UI.PopupText.Show(it.Stack.Def, count - left, at);
            TerraMod.Instance.PlaySound("Grab", 0.7f);
            it.Stack.Count = left;
            if (left <= 0) it.Stack = default;
        }

        /// <summary>
        /// The Gathering Swarm's ripple (CharmSkills): every item lying in this scene within the radius round the centre is
        /// picked up as if it touched the player (TakeTouching), what the player may not pick up yet (one just thrown) aside.
        /// How many it took in.
        /// </summary>
        internal static int SweepPickup(Vector2 centre, float radius, string scene)
        {
            var hero = HeroController.SilentInstance;
            var mod = TerraMod.Instance;
            if (hero == null || mod == null || hero.cState.dead || !Net.WorldSync.CanInteract) return 0;
            var at = hero.GetComponent<BoxCollider2D>().bounds.center;
            float r2 = radius * radius;
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Region == null || it.Stack.IsEmpty || it.Scene != scene) continue;
                if (it.GrabDelay > 0 && (it.NetId == null || it.Dropper == Net.NetSession.MySlot)) continue;
                if ((it.World - centre).sqrMagnitude > r2) continue;
                TakeTouching(it, mod.Inventory, at);
                if (it.Stack.IsEmpty) n++;
            }
            return n;
        }

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
            // Stars come 300 px further with a mana magnet (manaMagnet, Item.manaGrabRange); without one they come only
            // while the mana is short of full or a magic weapon is held (Player.GrabItems).
            var stats = TerraMod.Instance.Stats;
            float starRange = range + (Equipment.Current.ManaMagnet ? 300f * Px : 0f);
            var starReach = new Bounds(body.center, body.size + Vector3.one * (starRange * 2f));
            bool starsCome = Equipment.Current.ManaMagnet || stats.Mana < stats.ManaMax || inv.Current.Def?.Class == DamageClass.Magic;
            Vector2 heroVel = hero.GetComponent<Rigidbody2D>().linearVelocity / Player.TerraMovement.PxT;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.Region == null || (it.GrabDelay > 0 && (it.NetId == null || it.Dropper == Net.NetSession.MySlot)) || it.Stack.IsEmpty) continue;
                var c = it.World;
                var box = new Bounds(new Vector3(c.x, c.y, body.center.z), new Vector3(Half * 2f, Half * 2f, 1f));
                bool heart = it.Stack.Type == ItemType.Heart;
                bool star = it.Stack.Type == ItemType.Star;
                if (box.Intersects(body))
                {
                    TakeTouching(it, inv, body.center);
                    continue;
                }
                if (star && !starsCome) continue;
                if (!box.Intersects(heart ? heartReach : star ? starReach : reach) || (!heart && !star && inv.Room(it.Stack.Type) <= 0
                    && !Containers.Sells(inv, it.Stack.Type) && !(World.Chests.VoidBagCarried(inv) && World.Chests.VoidHasRoom(inv, it.Stack.Type)) && !Containers.HasRoom(inv, it.Stack.Type))) continue;
                if (it.NetId != null && Net.WorldSync.Guest)
                {
                    // The authority may have no local hero near this item. Request the same
                    // magnet-range pickup, rather than simulating a second authoritative item.
                    if (!it.ClaimPending)
                    {
                        it.ClaimPending = true;
                        Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "claim", Key = it.NetId, Count = ClaimCount(inv, it.Stack) });
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
        /// Player.PickupItem's heart and star: 20 life or 100 mana each, and gone (taken at full life or mana too), never
        /// carried. True for those two; also for the ones a LAN game's host hands a guest (WorldSync's claim).
        /// </summary>
        internal static bool TakeAtOnce(ItemStack stack, Vector3 at)
        {
            var mod = TerraMod.Instance;
            if (mod == null || stack.IsEmpty) return false;
            if (stack.Type == ItemType.Star)
            {
                ManaStars.Give(stack.Count);
                return true;
            }
            if (stack.Type != ItemType.Heart) return false;
            int n = stack.Count;
            mod.Stats.Heal(20 * n);
            UI.CombatText.Show(at + Vector3.up * 0.9f, (20 * n).ToString(), UI.CombatText.Heal);
            mod.PlaySound("Grab", 0.7f);
            return true;
        }

        /// <summary>How much of a shared item a guest asks for: all of a heart, a star or what the Auto-Sell Bag sells, else what fits.</summary>
        static int ClaimCount(Inventory inv, ItemStack s) =>
            s.Type == ItemType.Heart || s.Type == ItemType.Star || Containers.Sells(inv, s.Type) ? s.Count : Mathf.Min(s.Count, inv.Room(s.Type));

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
                if (o.Region == null || o.Grabbed || o.Stack.IsEmpty || o.Stack.Type != it.Stack.Type || o.FromSky != it.FromSky || o.Shimmered != it.Shimmered
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
        /// It stops flush against what it meets, with no bounce. The game's own colliders that the grid leaves to the
        /// game (lifts, moving platforms, conveyors, one-way platforms) stop it too, and it rides the ones that move.
        /// </summary>
        static void Move(Item it)
        {
            var c = it.World;
            var was = it.On;
            Ride(it, ref c);
            // A boss room's own ground (World.BossRooms): the game's colliders are the floor there, not the grid.
            if (TerraWorld.OwnGroundAt(c))
            {
                MoveOnOwnGround(it, c, was);
                return;
            }
            it.On = null;
            float belt = it.Belt / 60f;
            // Sunk in a block (let out inside the ground, or the ground laid over it): up to the first open cell, six
            // units at most, as an item never rests inside a tile.
            if (TerraWorld.IsSolidAt(c))
                for (int up = 1; up <= 12; up++)
                {
                    var cell = CellGrid.WorldToCell(c) + new Vector2Int(0, up);
                    if (TerraWorld.IsSolid(cell.x, cell.y)) continue;
                    c.y = cell.y * CellGrid.CellSize + Half;
                    it.Vel.y = 0f;
                    DevUnstuck++;
                    break;
                }
            float dx = it.Vel.x * Px + belt;
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
                // The game's own walls the grid doesn't hold (a breakable wall, a lift's side), if one comes first.
                float g = GameCast(c, dx > 0f ? Vector2.right : Vector2.left, Mathf.Abs(dx), out var wall);
                if (wall != null && g < Mathf.Abs(nx - c.x))
                {
                    nx = c.x + Mathf.Sign(dx) * g;
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
                    // The game's own ground the grid doesn't hold (a lift, a moving platform, a conveyor, a one-way
                    // platform, a station's top), if it comes first: it lands there and rides it.
                    float g = GameCast(c, Vector2.down, -dy, out var under);
                    if (under != null && c.y - g > ny)
                    {
                        ny = c.y - g;
                        it.Vel.y = 0f;
                        Land(it, under, was);
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
            // A belt's run only while it lies on the belt.
            if (it.On == null) it.Belt = 0f;
            it.Local = c - (Vector2)it.Region.Root.position;
        }

        public static int DevUnstuck;

        /// <summary>
        /// Floating: pushed up while under the waterline (a fall into the water dips it under, then it comes back up), held
        /// with a little under half of it below the surface, bobbing gently; the water's drag settles it.
        /// </summary>
        static void Float(Item it, float surface)
        {
            it.Bob += 0.04f;
            float target = surface - Half * 0.35f + Mathf.Sin(it.Bob) * Half * 0.12f;
            float off = (target - it.World.y) / Px;
            it.Vel.y = it.Vel.y * 0.86f + Mathf.Clamp(off * 0.035f, -0.25f, 0.35f);
        }

        static readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        static readonly Collider2D[] groundOverlap = new Collider2D[4];
        static ContactFilter2D groundFilter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << ChunkColliders.TerrainLayer };
        static readonly Vector2 boxSize = new Vector2(2f * Half - 0.02f, 2f * Half - 0.02f);
        /// <summary>The box's core: only this inside the ground is sunk. The whole box resting on a collider touches it within the physics' skin, and lifting that lifted it off its floor each time it settled.</summary>
        static readonly Vector2 coreSize = boxSize * 0.5f;
        /// <summary>The gap a cast leaves before what it meets: past the colliders' skin (Physics2D.defaultContactOffset 0.01).</summary>
        const float CastGap = 0.015f;

        /// <summary>
        /// The same box's moves on the game's own ground (a boss room's): cast against its colliders, stopping flush; one
        /// born inside them (a boss's loot let out in the floor) first rises to the first free spot over it and is set down
        /// on what it rose out of.
        /// </summary>
        static void MoveOnOwnGround(Item it, Vector2 c, Collider2D was)
        {
            it.On = null;
            float belt = it.Belt / 60f;
            if (Physics2D.OverlapBox(c, coreSize, 0f, groundFilter, groundOverlap) > 0)
                for (float up = 0.1f; up <= 6f; up += 0.1f)
                {
                    if (Physics2D.OverlapBox(c + Vector2.up * up, boxSize, 0f, groundFilter, groundOverlap) > 0) continue;
                    c.y += up;
                    c.y -= GroundCast(c, Vector2.down, up, false);
                    it.Vel.y = 0f;
                    DevUnstuck++;
                    break;
                }
            float dx = it.Vel.x * Px + belt;
            if (dx != 0f)
            {
                float d = GroundCast(c, dx > 0f ? Vector2.right : Vector2.left, Mathf.Abs(dx), false, out _);
                if (d < Mathf.Abs(dx)) it.Vel.x = 0f;
                c.x += Mathf.Sign(dx) * d;
            }
            float dy = it.Vel.y * Px;
            if (dy != 0f)
            {
                float d = GroundCast(c, dy > 0f ? Vector2.up : Vector2.down, Mathf.Abs(dy), dy > 0f, out var under);
                if (d < Mathf.Abs(dy))
                {
                    it.Vel.y = 0f;
                    if (dy < 0f && under != null) Land(it, under, was);
                }
                c.y += Mathf.Sign(dy) * d;
            }
            // A belt's run only while it lies on the belt.
            if (it.On == null) it.Belt = 0f;
            it.Local = c - (Vector2)it.Region.Root.position;
        }

        /// <summary>How far the box goes along a direction before the ground stops it (one-way platforms not on the way up), and what stops it.</summary>
        static float GroundCast(Vector2 c, Vector2 dir, float dist, bool up, out Collider2D hit)
        {
            hit = null;
            int n = Physics2D.BoxCast(c, boxSize, 0f, dir, groundFilter, groundHits, dist);
            float free = dist;
            for (int i = 0; i < n; i++)
            {
                var h = groundHits[i];
                if (h.collider == null || h.distance <= 0f) continue;
                if (up && h.collider.GetComponent<PlatformEffector2D>() != null) continue;
                float d = Mathf.Max(0f, h.distance - CastGap);
                if (d < free)
                {
                    free = d;
                    hit = h.collider;
                }
            }
            return free;
        }

        static float GroundCast(Vector2 c, Vector2 dir, float dist, bool up) => GroundCast(c, dir, dist, up, out _);

        /// <summary>
        /// How far the box goes along a direction before one of the game's own colliders that the grid doesn't hold
        /// stops it (World.TerrainSystem leaves lifts, moving platforms, conveyors, one-way platforms and breakable
        /// walls to the game; a station's top is the mod's), and which. Sideways a one-way platform lets it by, and
        /// nothing it is already in stops it (it passes up through a one-way platform and falls back on to it).
        /// </summary>
        static float GameCast(Vector2 c, Vector2 dir, float dist, out Collider2D hit)
        {
            hit = null;
            int n = Physics2D.BoxCast(c, boxSize, 0f, dir, groundFilter, groundHits, dist);
            float free = dist;
            for (int i = 0; i < n; i++)
            {
                var h = groundHits[i];
                if (h.collider == null || h.distance <= 0f || ChunkColliders.Mirrors.Contains(h.collider)) continue;
                if (dir.y >= 0f && h.collider.usedByEffector) continue;
                float d = Mathf.Max(0f, h.distance - CastGap);
                if (d < free)
                {
                    free = d;
                    hit = h.collider;
                }
            }
            return free;
        }

        /// <summary>Lying on one of the game's colliders: it is carried along by it, and along a conveyor's run (ConveyorBelt.speed; not a wall's).</summary>
        static void Land(Item it, Collider2D on, Collider2D was)
        {
            it.On = on;
            it.OnAt = on.transform.position;
            if (on == was) return;
            var belt = on.GetComponentInParent<ConveyorBelt>();
            it.Belt = belt != null && !belt.vertical ? belt.speed : 0f;
        }

        /// <summary>Carried by what it lay on last tick: a lift's or a moving platform's move since then (a jump of a unit or more is not a ride).</summary>
        static void Ride(Item it, ref Vector2 c)
        {
            var on = it.On;
            if (on == null) return;
            if (!on.isActiveAndEnabled)
            {
                it.On = null;
                it.Belt = 0f;
                return;
            }
            Vector2 p = on.transform.position;
            var d = p - it.OnAt;
            it.OnAt = p;
            if (d.sqrMagnitude < 1f) c += d;
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
                // Beside the terrain, not under it: a boss room keeping the game's own ground switches the terrain root off.
                var holder = it.Region.Props ?? it.Region.Root;
                if (it.Renderer == null)
                {
                    var go = new GameObject("Terra World Item " + it.Stack.Type);
                    go.transform.SetParent(holder, false);
                    it.Renderer = go.AddComponent<SpriteRenderer>();
                }
                else if (it.Renderer.transform.parent != holder) it.Renderer.transform.SetParent(holder, false);
                // Over the scenery (its foreground and the black of its ground draw nearer than z 0) and under the light map;
                // in water, among the scenery, behind the pool's surface and its water (drawn nearer than z 0), poured water too.
                it.Renderer.sortingOrder = it.Wet && !it.Shimmered ? 0 : World.LightMap.Order - 5;
                // An animated sheet's frame of the moment (the Fallen Star twinkles, souls pulse).
                it.Renderer.sprite = SpriteOf(it.Stack.Type, ItemFrames.FrameNow(ItemDb.Get(it.Stack.Type).Texture));
                it.Renderer.enabled = true;
                GlowOver(it.Renderer, ItemDb.Get(it.Stack.Type));
                var s = it.Renderer.sprite;
                float fit = Fit(it.Stack.Type, s);
                float h = s != null ? s.rect.height * Px * fit : 0f;
                var t = it.Renderer.transform;
                t.localPosition = new Vector3(it.Local.x, it.Local.y - Half + h / 2f, Z - it.Region.Root.position.z);
                t.localRotation = Quaternion.Euler(0f, 0f, -it.Vel.x * 0.2f * Mathf.Rad2Deg);
                float pop = it.Pop / (float)PopTicks;
                t.localScale = Vector3.one * (fit * (1f + 0.7f * pop * pop));
            }
        }

        static readonly Dictionary<ItemType, Sprite> glowSprites = new Dictionary<ItemType, Sprite>();

        /// <summary>Main.DrawItem's glowmask over an item lying on the ground (the rainbow blades' are their blades), in its colour.</summary>
        static void GlowOver(SpriteRenderer item, ItemDef def)
        {
            var t = item.transform.Find("Glow");
            if (def.Glow == null) { if (t != null) t.gameObject.SetActive(false); return; }
            if (!glowSprites.TryGetValue(def.Type, out var s))
            {
                try
                {
                    var tex = Terraria.TerrariaAssets.Texture(def.Glow);
                    s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                }
                catch (System.Exception) { s = null; }
                glowSprites[def.Type] = s;
            }
            if (s == null) return;
            SpriteRenderer g;
            if (t == null)
            {
                g = new GameObject("Glow").AddComponent<SpriteRenderer>();
                g.transform.SetParent(item.transform, false);
                g.transform.localPosition = new Vector3(0f, 0f, -0.0001f);
            }
            else g = t.GetComponent<SpriteRenderer>();
            g.gameObject.SetActive(true);
            g.sprite = s;
            g.sortingLayerID = item.sortingLayerID;
            g.sortingOrder = item.sortingOrder + 1;
            g.color = ItemGlows.Tint(def);
        }

        static readonly Dictionary<ItemType, float> fits = new Dictionary<ItemType, float>();

        /// <summary>
        /// The size an item lies on the ground at. Terraria's own item pictures (Item_N) at theirs, as Main.DrawItem draws
        /// them; any other picture an item has (a prefab house's whole house, a tile or armor sheet, a townsperson's, Hallownest's
        /// key items, relics, charms, the bosses' seals, the areas' chests and furniture) is made for something else: fitted
        /// to the size Terraria's own items lie at, at most 40 px across, and no less than 16 (one a few pixels wide would be lost).
        /// </summary>
        static float Fit(ItemType type, Sprite s)
        {
            if (s == null) return 1f;
            if (fits.TryGetValue(type, out float f)) return f;
            string name = ItemDb.Get(type)?.Texture ?? "";
            float big = Mathf.Max(s.rect.width, s.rect.height);
            f = 1f;
            if (!name.StartsWith("Item_", System.StringComparison.Ordinal) && big > 0f)
                f = big > 40f ? 40f / big : big < 10f ? 16f / big : 1f;
            fits[type] = f;
            return f;
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
                float fit = Fit(it.Stack.Type, s);
                float w = s.rect.width * Px * fit, h = s.rect.height * Px * fit;
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

        /// <summary>A recoverable personal drop, including an unloaded scene. Shared network drops remain their owners'.</summary>
        public static ItemStack PersonalStack(ItemType type)
        {
            foreach (var it in items)
                if (!it.Stack.IsEmpty && it.Stack.Type == type && (it.NetId == null || !Net.NetSession.Active)) return it.Stack;
            return default;
        }

        public static List<Saved> Save()
        {
            var list = new List<Saved>(items.Count);
            foreach (var it in items)
                if (!it.Stack.IsEmpty) list.Add(new Saved { Scene = it.Scene, Type = it.Stack.Type, Count = it.Stack.Count, Local = it.Local, Prefix = it.Stack.Prefix, Forge = it.Stack.Forge });
            return list;
        }

        public static void Load(IEnumerable<Saved> saved)
        {
            Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (s.Type != ItemType.None && s.Count > 0)
                    items.Add(new Item { Stack = new ItemStack { Type = s.Type, Count = s.Count, Prefix = s.Prefix, Forge = s.Forge }, Scene = s.Scene, Local = s.Local });
        }
    }
}
