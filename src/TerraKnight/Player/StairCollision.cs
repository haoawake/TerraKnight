using HarmonyLib;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Walking Terraria's platform stairs (Collision.SlopeCollision and WalkDownSlope). A stair
    /// piece has no collider of its own: the Knight would slide off a 45° one, and Hollow Knight
    /// knows it stands on ground only by touching a collider. So each physics step the hero's
    /// feet are put on the stair surface under him — under his left edge on a "\" stair, his
    /// right edge on a "/", the uphill corner of a 20 px wide box — and a small invisible ledge
    /// is laid right under them for the game to stand on.
    /// The surface catches feet up to a cell (plus 1 + |vx| px) below it, but never while rising,
    /// so a jump goes up through a stair and lands on it on the way down; walking downhill keeps
    /// him on it (up to 17 px below the feet). Holding Down falls through, and keeps falling as
    /// long as a stair would still catch him (the sticky stairFall), so he drops out cleanly.
    /// </summary>
    [HarmonyPatch]
    internal static class StairCollision
    {
        const float Px = 1f / 32f;
        const float HalfWidth = 10f * Px;
        const float Height = 42f * Px;
        // Before simulation the player's feet must clear the next tile's contact envelope,
        // including the two colliders' skin, rather than just its mathematical top line.
        static float Skin => Mathf.Max(Px, Physics2D.defaultContactOffset * 2f + 0.005f);

        static GameObject ledge;
        static bool stairFall, onStair;
        public static string DevState => $"fall={stairFall} on={onStair} surface={DevSurface}";
        static readonly RaycastHit2D[] ceilingHits = new RaycastHit2D[24];
        static readonly System.Collections.Generic.List<Collider2D> handoffPlatforms = new System.Collections.Generic.List<Collider2D>();
        static bool handingOff;

        /// <summary>The hero stands on a stair (set each physics step).</summary>
        public static bool OnStair => onStair;

        /// <summary>Dev: where the ledge is.</summary>
        public static string DevLedge => ledge == null ? "托板无" : ledge.activeSelf ? $"托板顶{ledge.transform.position.y + 0.125f:F2}" : "托板关";

        /// <summary>Dev: the stair height under the hero last step, if any.</summary>
        public static float? DevSurface { get; private set; }

        static bool Active => TerraMovement.Enabled && TerraMod.Instance != null && TerrainSystem.Current != null;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            var col = __instance.GetComponent<BoxCollider2D>();
            bool finishedHandoff = handingOff;
            handingOff = false;
            foreach (var platform in handoffPlatforms)
                if (platform != null && col != null) Physics2D.IgnoreCollision(col, platform, false);
            handoffPlatforms.Clear();
            // Upside down (the Gravity Globe) the stairs are overhead: nothing to walk.
            if (!Active || __instance.cState.dead || __instance.cState.transitioning || ___rb2d == null || GravityGlobe.Flipped)
            {
                Leave();
                return;
            }
            var b = col.bounds;
            var v = ___rb2d.linearVelocity;
            float vxPx = Mathf.Abs(v.x) / TerraMovement.PxT;
            bool down = TerraMovement.DownHeld;
            // Player.SlopingCollision: Down, a hook or a rope lets stairs go by.
            if (down || GrapplingHook.IsLatched || TerraMovement.OnRope) stairFall = true;
            if (v.y > 0.01f)
            {
                // Rising: stairs never stop him from below.
                Leave();
                return;
            }
            float feet = b.min.y, L = b.center.x - HalfWidth, R = b.center.x + HalfWidth;
            float lead = v.x * Time.fixedDeltaTime;
            const float c = CellGrid.CellSize;
            // WalkDownSlope takes the highest top under him: a block or flat platform he still
            // stands on beats a stair below, so he only follows a stair down once off them.
            bool groundBelow = false;
            int under = Mathf.FloorToInt((feet - 0.02f) / c);
            for (int gx = Mathf.FloorToInt(b.min.x / c); gx <= Mathf.FloorToInt((b.max.x - 0.001f) / c) && !groundBelow; gx++)
                groundBelow = TerraWorld.IsSolid(gx, under) || (TerraWorld.IsPlatform(gx, under) && TerraWorld.GetSlope(gx, under) == 0);
            bool standing = onStair || (__instance.cState.onGround && !groundBelow);
            float best = float.NegativeInfinity;
            int bestSlope = 0;
            bool bestPlatform = true, platformWouldCatch = false;
            int x0 = Mathf.FloorToInt((L + Mathf.Min(lead, 0f)) / c) - 1, x1 = Mathf.FloorToInt((R + Mathf.Max(lead, 0f)) / c) + 1;
            int y0 = Mathf.FloorToInt((feet - c - 0.1f) / c), y1 = Mathf.FloorToInt((feet + 17f * Px + 0.01f) / c);
            for (int gx = x0; gx <= x1; gx++)
                for (int gy = y0; gy <= y1; gy++)
                {
                    bool platform = TerraWorld.IsPlatform(gx, gy);
                    if (!platform && !TerraWorld.IsSolid(gx, gy)) continue;
                    int s = TerraWorld.GetSlope(gx, gy);
                    if (s != 1 && s != 2) continue;
                    float left = gx * c, right = left + c, bottom = gy * c, top = bottom + c;
                    float uphillL = L + (s == 1 ? Mathf.Min(lead, 0f) : 0f);
                    float uphillR = R + (s == 2 ? Mathf.Max(lead, 0f) : 0f);
                    if (uphillR <= left || uphillL >= right) continue;
                    // The feet within the stair piece, or a little under it (1 + |vx| px), or, walking down, just over it.
                    if (feet < bottom - (1f + vxPx) * Px) continue;
                    if (feet > top + (standing ? 17f * Px : 0f)) continue;
                    // On this piece's bottom line moving downhill (or standing still): the floor under its low end, not the stair.
                    if (platform && ((s == 1 && v.x >= 0f) || (s == 2 && v.x <= 0f)) && Mathf.Abs(feet - bottom) <= Mathf.Max(Px, Physics2D.defaultContactOffset * 2f)) continue;
                    // Unity has not moved yet. Sample the uphill corner at the end of this
                    // step so the feet enter the next flat platform from above, as in vanilla.
                    float e = s == 1 ? uphillL - left : right - uphillR;
                    float surface;
                    if (e >= 0f) surface = top - e;
                    else
                    {
                        // Past its high end: stood on like a flat top, with a little give.
                        if (platform && feet < top - (4f + vxPx) * Px) continue;
                        surface = top;
                    }
                    bool catches = feet <= surface + 0.0005f || (standing && feet - surface <= 17f * Px);
                    if (platform && catches) platformWouldCatch = true;
                    if (platform && stairFall) continue;
                    if (catches && surface > best) { best = surface; bestSlope = s; bestPlatform = platform; }
                }
            if (float.IsNegativeInfinity(best))
            {
                // One-way effectors retain a side-entry decision while the pair is touching.
                // Keep the ledge for one step and separate the player/platform contact pair;
                // restoring it next step rebuilds a top-entry contact at the now-correct feet.
                if (onStair && !finishedHandoff && !stairFall && TryFlatTop(b, out float flat))
                {
                    foreach (var platform in ChunkColliders.AllPlatforms)
                    {
                        if (platform == null || !platform.enabled || !platform.gameObject.activeInHierarchy) continue;
                        if (platform.bounds.max.x < b.min.x || platform.bounds.min.x > b.max.x
                            || platform.bounds.min.y > flat + Px || platform.bounds.max.y < flat - Px) continue;
                        Physics2D.IgnoreCollision(col, platform, true);
                        handoffPlatforms.Add(platform);
                    }
                    handingOff = true;
                    __instance.transform.position += Vector3.up * (flat - feet + Skin);
                    ___rb2d.linearVelocity = new Vector2(v.x, 0f);
                    Lay(b.center.x, flat, b.size.x);
                    DevSurface = flat;
                    return;
                }
                // Nothing would catch him: back to normal once Down is let go.
                if (!down && !platformWouldCatch) stairFall = false;
                Leave();
                return;
            }
            if (stairFall && bestPlatform)
            {
                Leave();
                return;
            }
            DevSurface = best;
            var p = __instance.transform.position;
            float lift = best - feet + Skin, allowed = lift;
            if (lift > 0f)
            {
                var filter = new ContactFilter2D();
                filter.SetLayerMask(1 << ChunkColliders.TerrainLayer);
                filter.useTriggers = false;
                // Sweep only the head. Sweeping the whole body also reports the solid stair's
                // high corner already touching the feet as a zero-distance "ceiling" hit.
                var head = new Vector2(b.center.x, b.max.y - Px / 4f);
                int count = Physics2D.BoxCast(head, new Vector2(b.size.x - 0.01f, Px / 2f), 0f,
                    Vector2.up, filter, ceilingHits, lift + 0.001f);
                for (int i = 0; i < count; i++)
                {
                    var hit = ceilingHits[i];
                    if (hit.collider == null || hit.collider.gameObject == ledge || hit.normal.y > -0.5f
                        || hit.collider.usedByEffector) continue;
                    allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - 0.001f));
                }
            }
            float blocked = lift - allowed;
            // Collision.SlopeCollision: a ceiling converts the refused rise into horizontal
            // pushback. In a three-tile passage this produces vanilla's one-way platform wall.
            float push = blocked > 0.001f ? (bestSlope == 1 ? blocked : -blocked) : 0f;
            __instance.transform.position = new Vector3(p.x + push, p.y + allowed, p.z);
            if (blocked > 0.001f) v.x = 0f;
            if (v.y < 0f) ___rb2d.linearVelocity = new Vector2(v.x, 0f);
            else if (blocked > 0.001f) ___rb2d.linearVelocity = v;
            onStair = true;
            DevSurface = feet + allowed - Skin;
            Lay(b.center.x + push, feet + allowed - Skin, b.size.x);
        }

        static bool TryFlatTop(Bounds b, out float top)
        {
            const float c = CellGrid.CellSize;
            top = float.NegativeInfinity;
            int row = Mathf.FloorToInt((b.min.y - 0.02f) / c);
            for (int x = Mathf.FloorToInt(b.min.x / c); x <= Mathf.FloorToInt((b.max.x - 0.001f) / c); x++)
            for (int y = row - 1; y <= row + 1; y++)
            {
                if (!TerraWorld.IsPlatform(x, y)) continue;
                int shape = TerraWorld.GetSlope(x, y);
                if (shape != 0 && shape != CellGrid.HalfBrick) continue;
                float surface = (y + (shape == CellGrid.HalfBrick ? 0.5f : 1f)) * c;
                if (Mathf.Abs(surface - b.min.y) < 4f * Px) top = Mathf.Max(top, surface);
            }
            return !float.IsNegativeInfinity(top);
        }

        /// <summary>The ledge the game stands the hero on: as wide as him, its top on the stair.</summary>
        static void Lay(float x, float top, float width)
        {
            if (ledge == null)
            {
                ledge = new GameObject("Terra Stair Ledge") { layer = ChunkColliders.TerrainLayer };
                Object.DontDestroyOnLoad(ledge);
                var rb = ledge.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
                var box = ledge.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, 0.25f);
                box.sharedMaterial = TerrainSystem.Current?.Region?.Material;
            }
            if (!ledge.activeSelf) ledge.SetActive(true);
            var collider = ledge.GetComponent<BoxCollider2D>();
            collider.size = new Vector2(width, 0.25f);
            ledge.transform.position = new Vector3(x, top - 0.125f, 0f);
        }

        static void Leave()
        {
            onStair = false;
            DevSurface = null;
            if (ledge != null && ledge.activeSelf) ledge.SetActive(false);
        }
    }
}
