using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A tiktik (the game's Climber) feels for its surface by rays on the terrain layer, and the mod's one-way tops live
    /// there too: a station's top (a workbench, a table, set down by the player) and the platforms built in the grid. A
    /// tiktik that met one took it for ground, went up and over it and, the top standing off the floor, round and round
    /// it for good. Its rays pass through one-way colliders: it walks the ground and the solid blocks, as before.
    /// </summary>
    [HarmonyPatch]
    internal static class ClimberSurfaces
    {
        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        public static int DevSkipped;

        /// <summary>Climber.FireRayLocal: the same ray (from its box's middle, turned with it), the first solid surface it meets.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Climber), "FireRayLocal")]
        static bool FireRayLocal(Climber __instance, Vector2 direction, float length, BoxCollider2D ___col, ref RaycastHit2D __result)
        {
            if (TerraMod.Instance == null || ___col == null) return true;
            Vector2 from = __instance.transform.TransformPoint(___col.offset);
            Vector2 way = __instance.transform.TransformDirection(direction);
            __result = Surface(from, way, length);
            return false;
        }

        // ------------------------------------------------------------------ one going round a tiny loop

        const float SampleEvery = 0.5f, Window = 4f, LoopBox = 1.5f, SearchReach = 8f;
        const int MinRun = 4;

        sealed class Track
        {
            public readonly Vector2[] At = new Vector2[(int)(Window / SampleEvery)];
            public readonly float[] Angle = new float[(int)(Window / SampleEvery)];
            public int Count, Next;
        }

        static readonly System.Collections.Generic.Dictionary<int, Track> tracks = new System.Collections.Generic.Dictionary<int, Track>();
        static float nextSample;
        static readonly System.Reflection.MethodInfo start = AccessTools.Method(typeof(Climber), "Start");
        public static int DevReset;

        /// <summary>
        /// Twice a second each tiktik's place and turn: one that has stayed in a <see cref="LoopBox"/> square for
        /// <see cref="Window"/> seconds while turning through three sides (round a single cell, a nub of the grid, the
        /// empty air) lets go and hops (or drops) onto the nearest run of ground at least <see cref="MinRun"/> cells long in
        /// its room, righting itself on the way, and walks on from there as it would from its start.
        /// </summary>
        public static void Tick()
        {
            if (Time.time < nextSample) return;
            nextSample = Time.time + SampleEvery;
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                var climber = hm.GetComponent<Climber>();
                if (climber == null || !climber.enabled) continue;
                int id = hm.GetInstanceID();
                if (!tracks.TryGetValue(id, out var t))
                {
                    if (tracks.Count > 256) tracks.Clear();
                    tracks[id] = t = new Track();
                }
                t.At[t.Next] = hm.transform.position;
                t.Angle[t.Next] = hm.transform.eulerAngles.z;
                t.Next = (t.Next + 1) % t.At.Length;
                t.Count = Mathf.Min(t.Count + 1, t.At.Length);
                if (t.Count < t.At.Length || !Looping(t)) continue;
                t.Count = 0;
                Reset(climber, hm);
            }
        }

        /// <summary>All the samples within the loop square, the body turned to at least three of its four sides.</summary>
        static bool Looping(Track t)
        {
            Vector2 lo = t.At[0], hi = t.At[0];
            int sides = 0;
            foreach (var p in t.At) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
            if (hi.x - lo.x > LoopBox || hi.y - lo.y > LoopBox) return false;
            foreach (var a in t.Angle) sides |= 1 << (Mathf.RoundToInt(Mathf.Repeat(a, 360f) / 90f) % 4);
            int n = 0;
            for (int k = 0; k < 4; k++) if ((sides & (1 << k)) != 0) n++;
            return n >= 3;
        }

        static void Reset(Climber climber, HealthManager hm)
        {
            Vector2 from = hm.transform.position;
            string scene = hm.gameObject.scene.name;
            if (!NearestRun(from, scene, out var top))
            {
                Plugin.Log.LogWarning($"[提克提克] {hm.name}（{scene}）在 {from:F1} 绕着小圈打转，{SearchReach} 格内没有够长的地面");
                return;
            }
            climber.StopAllCoroutines();
            climber.StartCoroutine(Hop(climber, hm, top));
            DevReset++;
            Plugin.Log.LogInfo($"[提克提克] {hm.name}（{scene}）在 {from:F1} 绕着小圈打转，跳到 {top:F1} 的地面上重新爬");
        }

        /// <summary>
        /// The way there, seen all along: an arc from where it clung to where it lands (higher for a climb up, little more
        /// than a fall for a drop), turning upright in its first third, landing just where Climber.Start's own stick to
        /// the ground would put it, so it walks on without a jump.
        /// </summary>
        static System.Collections.IEnumerator Hop(Climber climber, HealthManager hm, Vector2 top)
        {
            var t = hm.transform;
            var rb = hm.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            Vector3 from = t.position;
            float turn = t.eulerAngles.z;
            var land = Surface(top + Vector2.up * 0.5f, Vector2.down, 2f);
            Vector3 to = new Vector3(top.x, land.collider != null ? land.point.y : top.y, from.z);
            float rise = to.y - from.y;
            float arc = 0.5f + Mathf.Max(0f, rise) * 0.5f;
            float time = Mathf.Clamp(0.3f + Vector2.Distance(from, to) * 0.06f, 0.35f, 0.9f);
            for (float e = 0f; e < time; e += Time.deltaTime)
            {
                if (hm == null || hm.isDead) yield break;
                float k = e / time;
                var p = Vector3.Lerp(from, to, k);
                p.y += arc * 4f * k * (1f - k);
                t.position = p;
                t.SetRotation2D(Mathf.LerpAngle(turn, 0f, Mathf.Clamp01(k * 3f)));
                if (rb != null) rb.linearVelocity = Vector2.zero;
                yield return null;
            }
            if (hm == null || hm.isDead) yield break;
            t.SetRotation2D(0f);
            t.position = to;
            // Climber.Start: onto the ground (where it already is), its way round from its facing, walking.
            start?.Invoke(climber, null);
        }

        /// <summary>The top of the nearest run of solid cells with open cells over them, at least <see cref="MinRun"/> long, in the room.</summary>
        static bool NearestRun(Vector2 from, string scene, out Vector2 top)
        {
            var c0 = CellGrid.WorldToCell(from);
            int r = Mathf.CeilToInt(SearchReach / CellGrid.CellSize);
            float best = float.MaxValue;
            top = default;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = c0.x + dx, y = c0.y + dy;
                    if (!Walkable(x, y)) continue;
                    // The middle of a run: walkable cells either side, MinRun in all.
                    int left = 0, right = 0;
                    while (left < MinRun && Walkable(x - left - 1, y)) left++;
                    while (right < MinRun && Walkable(x + right + 1, y)) right++;
                    if (left + right + 1 < MinRun) continue;
                    var at = new Vector2((x + 0.5f) * CellGrid.CellSize, (y + 1) * CellGrid.CellSize);
                    float d = (at - from).sqrMagnitude;
                    if (d >= best) continue;
                    var region = TerraWorld.RegionAt(x, y);
                    if (region == null || region.SceneName != scene) continue;
                    best = d;
                    top = at;
                }
            return best < float.MaxValue;
        }

        /// <summary>A solid cell with two open cells over it.</summary>
        static bool Walkable(int x, int y) => TerraWorld.IsSolid(x, y) && !TerraWorld.IsSolid(x, y + 1) && !TerraWorld.IsSolid(x, y + 2);

        /// <summary>The nearest terrain-layer hit along a ray that isn't a one-way top (none: an empty hit).</summary>
        internal static RaycastHit2D Surface(Vector2 from, Vector2 way, float length)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = 1 << ChunkColliders.TerrainLayer, useTriggers = Physics2D.queriesHitTriggers };
            int n = Physics2D.Raycast(from, way, filter, hits, length);
            RaycastHit2D best = default;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null) continue;
                if (c.usedByEffector) { DevSkipped++; continue; }
                if (hits[i].distance < bestD) { bestD = hits[i].distance; best = hits[i]; }
            }
            return best;
        }
    }
}
