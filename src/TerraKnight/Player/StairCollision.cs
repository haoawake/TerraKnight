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

        static GameObject ledge;
        static bool stairFall, onStair;

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
            // Upside down (the Gravity Globe) the stairs are overhead: nothing to walk.
            if (!Active || __instance.cState.dead || __instance.cState.transitioning || ___rb2d == null || GravityGlobe.Flipped)
            {
                Leave();
                return;
            }
            var col = __instance.GetComponent<BoxCollider2D>();
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
            const float c = CellGrid.CellSize;
            // WalkDownSlope takes the highest top under him: a block or flat platform he still
            // stands on beats a stair below, so he only follows a stair down once off them.
            bool groundBelow = false;
            int under = Mathf.FloorToInt((feet - 0.02f) / c);
            for (int gx = Mathf.FloorToInt(b.min.x / c); gx <= Mathf.FloorToInt((b.max.x - 0.001f) / c) && !groundBelow; gx++)
                groundBelow = TerraWorld.IsSolid(gx, under) || (TerraWorld.IsPlatform(gx, under) && TerraWorld.GetSlope(gx, under) == 0);
            bool standing = onStair || (__instance.cState.onGround && !groundBelow);
            float best = float.NegativeInfinity;
            int x0 = Mathf.FloorToInt(L / c) - 1, x1 = Mathf.FloorToInt(R / c) + 1;
            int y0 = Mathf.FloorToInt((feet - c - 0.1f) / c), y1 = Mathf.FloorToInt((feet + 17f * Px + 0.01f) / c);
            for (int gx = x0; gx <= x1; gx++)
                for (int gy = y0; gy <= y1; gy++)
                {
                    if (!TerraWorld.IsPlatform(gx, gy)) continue;
                    int s = TerraWorld.GetSlope(gx, gy);
                    if (s == 0) continue;
                    float left = gx * c, right = left + c, bottom = gy * c, top = bottom + c;
                    if (R <= left || L >= right) continue;
                    // The feet within the stair piece, or a little under it (1 + |vx| px), or, walking down, just over it.
                    if (feet < bottom - (1f + vxPx) * Px) continue;
                    if (feet > top + (standing ? 17f * Px : 0f)) continue;
                    // On this piece's bottom line moving downhill (or standing still): the floor under its low end, not the stair.
                    if (((s == 1 && v.x >= 0f) || (s == 2 && v.x <= 0f)) && Mathf.Abs(feet - bottom) < 1f / 64f) continue;
                    float e = s == 1 ? L - left : right - R;
                    float surface;
                    if (e >= 0f) surface = top - e;
                    else
                    {
                        // Past its high end: stood on like a flat top, with a little give.
                        if (feet < top - (4f + vxPx) * Px) continue;
                        surface = top;
                    }
                    bool catches = feet <= surface + 0.0005f || (standing && feet - surface <= 17f * Px);
                    if (catches && surface > best) best = surface;
                }
            if (float.IsNegativeInfinity(best))
            {
                // Nothing would catch him: back to normal once Down is let go.
                if (!down) stairFall = false;
                Leave();
                return;
            }
            if (stairFall)
            {
                Leave();
                return;
            }
            DevSurface = best;
            var p = __instance.transform.position;
            __instance.transform.position = new Vector3(p.x, p.y + (best - feet), p.z);
            if (v.y < 0f) ___rb2d.linearVelocity = new Vector2(v.x, 0f);
            onStair = true;
            Lay(b.center.x, best, b.size.x);
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
