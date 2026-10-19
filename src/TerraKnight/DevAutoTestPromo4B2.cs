using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_bmove*_zh", "p4_bpets*_zh"): recorder B's movement and pet clips for the 4.0 trailer: the Shade
    /// Cloak's shadow dash and the Crystal Heart's super dash through enemies, wings across a big room, the hook along a
    /// ceiling, the Gravity Globe, the nail swung at the mouse, the view zoom; Terraria's pets following, Chester's bank.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int BTerrain = 1 << ChunkColliders.TerrainLayer;

        /// <summary>A room entered (no fall damage on the way in) and its area title, if one comes, waited out.</summary>
        IEnumerator BEnter(string room)
        {
            mod.DevNoFallDamage = true;
            yield return EnterRoom(room);
            yield return BWaitTitle();
            BNoTitles(room);
        }

        /// <summary>
        /// A room's own area-title triggers switched off (some rooms show their area's name when the hero walks into a
        /// part of them, seconds after coming in: it would lie over a take).
        /// </summary>
        static void BNoTitles(string room)
        {
            var sc = UnityEngine.SceneManagement.SceneManager.GetSceneByName(room);
            if (!sc.IsValid() || !sc.isLoaded) return;
            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<AreaTitleController>(true))
                {
                    t.gameObject.SetActive(false);
                    n++;
                }
            Plugin.Log.LogInfo($"[AutoTest] 录制B {room}：关掉区域标题触发 {n} 个");
        }

        /// <summary>Accessories put on in the loadout worn now: into its empty slots, else over its last ones.</summary>
        void BWear(params ItemType[] items)
        {
            var inv = mod.Inventory;
            int k = 0;
            for (int i = 0; i < inv.Accessories.Length && k < items.Length; i++)
                if (inv.Accessories[i].IsEmpty) inv.Accessories[i] = ItemStack.Of(items[k++]);
            for (int i = inv.Accessories.Length - 1; i >= 0 && k < items.Length; i--) inv.Accessories[i] = ItemStack.Of(items[k++]);
            inv.RefreshEffective();
        }

        /// <summary>The enemies of one scene now (alive, shown).</summary>
        static List<HealthManager> BSceneFoes(string scene)
        {
            var list = new List<HealthManager>();
            foreach (var hm in BFoes(Vector2.zero, 100000f)) if (hm.gameObject.scene.name == scene) list.Add(hm);
            return list;
        }

        /// <summary>The floor under a point (its y), or NaN.</summary>
        static float BFloor(Vector2 p, float depth = 20f)
        {
            var hit = Physics2D.Raycast(p, Vector2.down, depth, BTerrain);
            return hit.collider != null ? hit.point.y : float.NaN;
        }

        /// <summary>
        /// The densest row of enemies standing on one floor: for each, how many others are within `w` across and 2 up or
        /// down, on ground (a floor within 3 below). Its middle and its floor.
        /// </summary>
        static bool BGroundRow(string scene, float w, out Vector2 mid, out float floor, out int count)
        {
            mid = Vector2.zero;
            floor = 0f;
            count = 0;
            var foes = BSceneFoes(scene);
            foreach (var a in foes)
            {
                var pa = BCentre(a);
                float fa = BFloor(pa, 4f);
                if (float.IsNaN(fa)) continue;
                int n = 0;
                float sx = 0f;
                foreach (var b in foes)
                {
                    var pb = BCentre(b);
                    if (Mathf.Abs(pb.x - pa.x) < w && Mathf.Abs(pb.y - pa.y) < 2f) { n++; sx += pb.x; }
                }
                if (n > count) { count = n; mid = new Vector2(sx / n, pa.y); floor = fa; }
            }
            return count > 0;
        }

        // ------------------------------------------------------------------ the shadow dash

        /// <summary>
        /// The shadow dash (the Hallownest Shield worn: Shade Cloak, Crystal Heart and the Shield of Cthulhu in one) through
        /// a row of enemies on a floor, back and forth: walked up to, dashed through in a cloud of black (each one rammed,
        /// its number up), turned round once the cooldown is over.
        /// </summary>
        IEnumerator BShadowDash(string room, string clip, int dashes = 4)
        {
            EnemyRespawn.Multiplier = 6;
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            yield return null;
            BWear(ItemType.HallownestShield);
            yield return new WaitForSeconds(2f);
            if (!BGroundRow(room, 6f, out var mid, out float floor, out int count))
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制B 暗影冲刺 {room}：地上没有怪");
                yield break;
            }
            // Start on the side with room.
            int dir = Physics2D.Raycast(new Vector2(mid.x, floor + 1f), Vector2.left, 9f, BTerrain).collider == null ? 1 : -1;
            hero.transform.position = Clear(hero, new Vector2(mid.x - dir * 7f, floor + 0.1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            bToughK = 6f;
            BToughen(BFoes(mid, 12f));
            aimInit = false;
            var col = hero.GetComponent<BoxCollider2D>();
            SmoothAim(mid, 99f);
            Items.AbilityAccessories.DevReset();
            yield return new WaitForSeconds(0.8f);
            Plugin.Log.LogInfo($"[AutoTest] 录制B 暗影冲刺 {room}：{count} 只在 {mid:F1}，地面 {floor:F1}，往{(dir > 0 ? "右" : "左")}");
            StartRec(clip);
            int done = 0;
            for (int n = 0; n < dashes; n++)
            {
                // A step or two toward them, then the dash.
                float t0 = Time.time;
                while (Time.time - t0 < 0.35f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 14f));
                    Player.TerraMovement.DevMove = dir;
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 6f, 0.3f), 6f);
                    yield return null;
                }
                int before = Items.AbilityAccessories.DevShadowDashes;
                Items.AbilityAccessories.DevShadowKey = true;
                t0 = Time.time;
                while (Time.time - t0 < 0.5f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 14f));
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 6f, 0.3f), 6f);
                    yield return null;
                }
                if (Items.AbilityAccessories.DevShadowDashes > before) done++;
                Player.TerraMovement.DevMove = 0;
                // Turned round, waiting the cooldown out.
                dir = -dir;
                t0 = Time.time;
                while (Time.time - t0 < 1.3f || Items.AbilityAccessories.ShadowCooldownLeft > 0f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 14f));
                    Player.TerraMovement.DevMove = Time.time - t0 < 0.12f ? dir : 0;
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 6f, 0.3f), 6f);
                    if (Time.time - t0 > 3f) break;
                    yield return null;
                }
            }
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(0.3f);
            StopRec();
            bToughK = 1f;
            Plugin.Log.LogInfo($"[AutoTest] 录制B 暗影冲刺：{done}/{dashes} 次冲出，击杀 {bKills}");
        }

        // ------------------------------------------------------------------ the super dash

        /// <summary>
        /// The Crystal Heart's super dash along a floor with enemies on it: the Dash key held (the crystals drawn in, the
        /// body white), let go, flown till the wall stops it; turned round and again.
        /// </summary>
        IEnumerator BSuperDash(string room, string clip, int runs = 2)
        {
            EnemyRespawn.Multiplier = 6;
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            yield return null;
            BWear(ItemType.HallownestShield);
            PlayerData.instance.hasSuperDash = true;
            yield return new WaitForSeconds(2f);
            if (!BGroundRow(room, 9f, out var mid, out float floor, out int count))
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制B 超级冲刺 {room}：地上没有怪");
                yield break;
            }
            float Room(int d) { var h = Physics2D.Raycast(new Vector2(mid.x, floor + 0.8f), Vector2.right * d, 60f, BTerrain); return h.collider != null ? h.distance : 60f; }
            int dir = Room(-1) > Room(1) ? 1 : -1;
            float back = Mathf.Min(14f, Room(-dir) - 1.5f);
            hero.transform.position = Clear(hero, new Vector2(mid.x - dir * back, floor + 0.1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            if (dir > 0) hero.FaceRight(); else hero.FaceLeft();
            bToughK = 4f;
            BToughen(BFoes(mid, 16f));
            aimInit = false;
            var col = hero.GetComponent<BoxCollider2D>();
            SmoothAim(mid, 99f);
            yield return new WaitForSeconds(0.8f);
            Plugin.Log.LogInfo($"[AutoTest] 录制B 超级冲刺 {room}：{count} 只在 {mid:F1}，地面 {floor:F1}，往{(dir > 0 ? "右" : "左")}，后面空 {back:F1}");
            StartRec(clip);
            int done = 0;
            Items.AbilityAccessories.DevReset();
            for (int n = 0; n < runs; n++)
            {
                int before = Items.AbilityAccessories.DevSuperDashes;
                for (float t = Time.time + 2f; Time.time < t && !GameHooks.PlayerHasControl();) { BKeep(); yield return null; }
                Items.AbilityAccessories.DevDashKey = false;
                yield return null;
                Items.AbilityAccessories.DevDashKey = true;
                float t0 = Time.time, full = -1f;
                while (Time.time - t0 < 3f && (full < 0f || Time.time - full < 0.15f))
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 18f));
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 7f, 0.4f), 5f);
                    string st = HeroFsm(hero, "Superdash")?.ActiveStateName ?? "";
                    if (full < 0f && st.EndsWith("Charged")) full = Time.time;
                    yield return null;
                }
                string charged = HeroFsm(hero, "Superdash")?.ActiveStateName;
                Items.AbilityAccessories.DevDashKey = false;
                t0 = Time.time;
                bool flew = false;
                while (Time.time - t0 < 3f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 18f));
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 7f, 0.4f), 5f);
                    if (hero.cState.superDashing) flew = true;
                    else if (flew && Time.time - t0 > 0.3f) break;
                    yield return null;
                }
                // Still flying (no wall): a Dash press stops it.
                if (hero.cState.superDashing)
                {
                    Items.AbilityAccessories.DevDashKey = true;
                    yield return null;
                    yield return null;
                    Items.AbilityAccessories.DevDashKey = false;
                }
                if (Items.AbilityAccessories.DevSuperDashes > before) done++;
                Plugin.Log.LogInfo($"[AutoTest] 录制B 超级冲刺 第{n + 1}次：松手时 {charged}，飞了 {flew}；{Items.AbilityAccessories.DevPress}，戴着 {Items.AbilityAccessories.CrystalWorn}，冷却 {Items.AbilityAccessories.SuperCooldownLeft:F1}");
                Items.AbilityAccessories.DevDashKey = null;
                for (float t = Time.time + 2f; Time.time < t && (!GameHooks.PlayerHasControl() || hero.cState.superDashing);) { BKeep(); yield return null; }
                yield return new WaitForSeconds(0.25f);
                dir = -dir;
                t0 = Time.time;
                while (Time.time - t0 < 0.3f)
                {
                    BKeep();
                    Player.TerraMovement.DevMove = Time.time - t0 < 0.12f ? dir : 0;
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 7f, 0.4f), 6f);
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
            }
            yield return new WaitForSeconds(0.3f);
            StopRec();
            bToughK = 1f;
            Items.AbilityAccessories.DevDashKey = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制B 超级冲刺：{done}/{runs} 次，击杀 {bKills}");
        }

        // ------------------------------------------------------------------ a route through a room

        /// <summary>
        /// A way through a room's free space (the hero's box and a margin clear of the terrain) from a point to the farthest
        /// one it can reach (or the given goal), straightened and made a smooth curve, at most `maxLen` long.
        /// </summary>
        List<Vector2> BRoute(Vector2 from, Vector2? goal, Vector2 box, float maxLen)
        {
            var region = TerraWorld.Live;
            if (region == null) return null;
            Vector2 size = box + new Vector2(0.9f, 0.9f);
            bool Blocked(Vector2 p) => Physics2D.OverlapBox(p, size, 0f, BTerrain) != null;
            const float step = 0.5f;
            var cr = region.CellRect;
            Vector2 origin = new Vector2(cr.x, cr.y) * CellGrid.CellSize;
            int w = Mathf.CeilToInt(cr.width * CellGrid.CellSize / step), h = Mathf.CeilToInt(cr.height * CellGrid.CellSize / step);
            var free = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) free[y * w + x] = !Blocked(origin + new Vector2((x + 0.5f) * step, (y + 0.5f) * step));
            Vector2 P(int i) => origin + new Vector2((i % w + 0.5f) * step, (i / w + 0.5f) * step);
            int Nearest(Vector2 p)
            {
                int best = -1;
                float bd = float.MaxValue;
                int cx = Mathf.Clamp((int)((p.x - origin.x) / step), 0, w - 1), cy = Mathf.Clamp((int)((p.y - origin.y) / step), 0, h - 1);
                for (int y = Mathf.Max(0, cy - 10); y <= Mathf.Min(h - 1, cy + 10); y++)
                    for (int x = Mathf.Max(0, cx - 10); x <= Mathf.Min(w - 1, cx + 10); x++)
                    {
                        int i = y * w + x;
                        if (!free[i]) continue;
                        float d = (P(i) - p).sqrMagnitude;
                        if (d < bd) { bd = d; best = i; }
                    }
                return best;
            }
            int start = Nearest(from);
            if (start < 0) return null;
            var parent = new int[w * h];
            var depth = new int[w * h];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            var queue = new Queue<int>();
            parent[start] = -1;
            queue.Enqueue(start);
            int[] dxs = { 1, -1, 0, 0, 1, 1, -1, -1 }, dys = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int far = start;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                if (depth[i] > depth[far]) far = i;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + dxs[k], ny = y + dys[k];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int j = ny * w + nx;
                    if (!free[j] || parent[j] != -2) continue;
                    if (k >= 4 && (!free[y * w + nx] || !free[ny * w + x])) continue;
                    parent[j] = i;
                    depth[j] = depth[i] + 1;
                    queue.Enqueue(j);
                }
            }
            int end = far;
            if (goal.HasValue)
            {
                int g = Nearest(goal.Value);
                if (g >= 0 && parent[g] != -2) end = g;
            }
            var raw = new List<Vector2>();
            for (int i = end; i >= 0; i = parent[i]) raw.Add(P(i));
            raw.Reverse();
            bool Clear2(Vector2 a, Vector2 b)
            {
                int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.25f);
                for (int s = 1; s < n; s++) if (Blocked(Vector2.Lerp(a, b, s / (float)n))) return false;
                return true;
            }
            var pts = new List<Vector2> { raw[0] };
            for (int i = 0; i < raw.Count - 1;)
            {
                int j = raw.Count - 1;
                while (j > i + 1 && !Clear2(raw[i], raw[j])) j--;
                pts.Add(raw[j]);
                i = j;
            }
            var curve = new List<Vector2>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / 0.1f));
                for (int s = 0; s < n; s++)
                {
                    float t = s / (float)n, t2 = t * t, t3 = t2 * t;
                    curve.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            curve.Add(pts[pts.Count - 1]);
            // Cut to its first maxLen.
            var cut = new List<Vector2> { curve[0] };
            float len = 0f;
            for (int i = 1; i < curve.Count && len < maxLen; i++)
            {
                len += Vector2.Distance(curve[i - 1], curve[i]);
                cut.Add(curve[i]);
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制B 路线：网格 {w}x{h}，原路 {raw.Count} 点，拉直 {pts.Count} 点，取 {len:F1}");
            return cut;
        }

        /// <summary>
        /// Flown along a route on wings (the Soaring Insignia's endless flight): the hero carried along it at a steady
        /// speed eased in and out, the jump held so the wings beat, the aim a little ahead.
        /// </summary>
        IEnumerator BFlyRoute(HeroController hero, List<Vector2> curve, float speed)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var dist = new float[curve.Count];
            for (int i = 1; i < curve.Count; i++) dist[i] = dist[i - 1] + Vector2.Distance(curve[i - 1], curve[i]);
            float total = dist[curve.Count - 1];
            Vector2 At(float s)
            {
                int lo = 0, hi = curve.Count - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) / 2;
                    if (dist[mid] < s) lo = mid; else hi = mid;
                }
                float seg = dist[hi] - dist[lo];
                return seg > 0f ? Vector2.Lerp(curve[lo], curve[hi], (s - dist[lo]) / seg) : curve[lo];
            }
            Vector2 offset = (Vector2)col.bounds.center - (Vector2)hero.transform.position;
            float sNow = 0f, t0 = Time.time;
            Vector2 last = At(0f);
            Player.TerraMovement.DevJump = true;
            if (hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
            while (sNow < total && Time.time - t0 < 30f)
            {
                BKeep();
                float v = speed * Mathf.Clamp01((Time.time - t0) / 0.7f) * Mathf.Clamp01((total - sNow) / 2.5f + 0.2f);
                sNow = Mathf.Min(total, sNow + v * Time.deltaTime);
                var p = At(sNow);
                var vel = Time.deltaTime > 0f ? (p - last) / Time.deltaTime : Vector2.zero;
                hero.transform.position = new Vector3(p.x - offset.x, p.y - offset.y, hero.transform.position.z);
                rb.linearVelocity = vel;
                Player.TerraMovement.DevMove = vel.x > 0.6f ? 1 : vel.x < -0.6f ? -1 : 0;
                Player.TerraMovement.DevJump = true;
                SmoothAim(At(Mathf.Min(total, sNow + 4f)) + new Vector2(0f, 0.5f), 5f);
                last = p;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
        }

        /// <summary>Wings across a big room: in from its gate, flown to its far side (or the goal) and a moment after.</summary>
        IEnumerator BWings(string room, string clip, int loadout, float maxLen, Vector2? startOff = null, float speed = 10f)
        {
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            yield return null;
            BWear(ItemType.EmpressFlightBooster);
            var col = hero.GetComponent<BoxCollider2D>();
            if (startOff.HasValue)
            {
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + startOff.Value);
                yield return new WaitForSeconds(0.6f);
            }
            var curve = BRoute(col.bounds.center, null, col.bounds.size, maxLen);
            if (curve == null || curve.Count < 2) { Plugin.Log.LogInfo($"[AutoTest] 录制B 翅膀 {room}：没有路线"); yield break; }
            aimInit = false;
            yield return new WaitForSeconds(0.4f);
            StartRec(clip);
            yield return BFlyRoute(hero, curve, speed);
            yield return new WaitForSeconds(0.6f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制B 翅膀 {room}：到了 {(Vector2)col.bounds.center:F1}");
        }

        // ------------------------------------------------------------------ the hook

        /// <summary>The spot in a room (near a start) with a ceiling 5 to 11 above and the longest run of it ahead.</summary>
        Vector3 BUnderCeiling(HeroController hero, Vector2 home, float lo, float hi, out int dir, out float run)
        {
            Vector3 under = home;
            dir = 1;
            run = 0f;
            for (int k = 0; k <= 240; k++)
            {
                float dx = (k % 2 == 0 ? 1f : -1f) * (k / 2) * 0.5f;
                var spot = Clear(hero, home + new Vector2(dx, 0f));
                var head = Physics2D.Raycast((Vector2)spot + Vector2.up * 0.7f, Vector2.up, 60f, BTerrain);
                var feet = Physics2D.Raycast((Vector2)spot + Vector2.up * 0.2f, Vector2.down, 3f, BTerrain);
                float r = head.collider != null ? head.distance : 99f;
                if (feet.collider == null || r < lo || r > hi) continue;
                foreach (int d in new[] { 1, -1 })
                {
                    float len = 0f;
                    for (float x = 0.5f; x < 40f; x += 0.5f)
                    {
                        var up2 = Physics2D.Raycast((Vector2)spot + new Vector2(d * x, 0.7f), Vector2.up, 60f, BTerrain);
                        var wall = Physics2D.Raycast((Vector2)spot + new Vector2(d * (x - 0.5f), 1.2f), Vector2.right * d, 0.5f, BTerrain);
                        if (up2.collider == null || up2.distance > hi + 2f || up2.distance < 2.5f || wall.collider != null) break;
                        len = x;
                    }
                    if (len > run) { run = len; under = spot; dir = d; }
                }
            }
            return under;
        }

        /// <summary>The Lunar Hook along a ceiling: thrown up ahead, pulled up to it, let go with a jump, thrown again.</summary>
        IEnumerator BHook(string room, string clip, int loadout, int throws = 6)
        {
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            yield return new WaitForSeconds(0.5f);
            var hook = Player.GrapplingHook.Instance;
            var col = hero.GetComponent<BoxCollider2D>();
            var under = BUnderCeiling(hero, hero.transform.position, 5f, 11f, out int dir, out float run);
            Plugin.Log.LogInfo($"[AutoTest] 录制B 抓钩 {room}：起点 {under:F1}，往{(dir > 0 ? "右" : "左")}，天花板长 {run:F1}");
            hero.transform.position = under;
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 4f, 3f), 99f);
            StartRec(clip);
            yield return new WaitForSeconds(0.3f);
            int latches = 0;
            float tStart = Time.time;
            int misses = 0;
            for (int n = 0; n < throws && Time.time - tStart < 14f; n++)
            {
                Vector2 c = col.bounds.center;
                Vector2? anchor = null;
                foreach (float deg in new[] { 50f, 40f, 60f, 30f, 70f, 20f, 80f })
                {
                    var d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad) * dir, Mathf.Sin(deg * Mathf.Deg2Rad));
                    var hit = Physics2D.Raycast(c, d, 16f, BTerrain);
                    if (hit.collider != null && hit.distance > 2f) { anchor = hit.point - d * 0.1f; break; }
                }
                if (anchor == null)
                {
                    // Nothing ahead to catch: fall and drift on a moment (turned round at the end of the hall).
                    if (++misses > 6) break;
                    if (misses % 3 == 0) dir = -dir;
                    for (float t = 0f; t < 0.25f; t += Time.deltaTime) { BKeep(); Player.TerraMovement.DevMove = dir; yield return null; }
                    n--;
                    continue;
                }
                for (float t = 0f; t < 0.18f; t += Time.deltaTime)
                {
                    BKeep();
                    SmoothAim(anchor.Value, 22f);
                    Player.TerraMovement.DevMove = dir;
                    yield return null;
                }
                hook.Throw(hero, ItemType.LunarHook, anchor.Value);
                float t1 = Time.time;
                while (hook.DevState != "Latched" && Time.time - t1 < 0.8f) { BKeep(); yield return null; }
                if (hook.DevState == "Latched") latches++;
                t1 = Time.time;
                while (Time.time - t1 < 0.9f && Vector2.Distance(col.bounds.center, hook.DevHookPosition) > 3.2f)
                {
                    BKeep();
                    SmoothAim(hook.DevHookPosition, 8f);
                    yield return null;
                }
                hook.DevJumpRelease(hero);
                Player.TerraMovement.DevMove = dir;
                Player.TerraMovement.DevJump = true;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    BKeep();
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 3f), 8f);
                    yield return null;
                }
                Player.TerraMovement.DevJump = false;
                // The wall ahead: turn round.
                if (Physics2D.Raycast(col.bounds.center, Vector2.right * dir, 5f, BTerrain).collider != null) dir = -dir;
            }
            hook.DevRelease(hero);
            Player.TerraMovement.DevMove = 0;
            for (float t = 0f; t < 1f; t += Time.deltaTime) { BKeep(); yield return null; }
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制B 抓钩：钩住 {latches} 次，用时 {Time.time - tStart:F1}s");
        }

        // ------------------------------------------------------------------ the Gravity Globe

        /// <summary>The Gravity Globe worn: Up turns the world over, the hero falls to the ceiling and runs along it, then back.</summary>
        IEnumerator BGravity(string room, string clip, int loadout)
        {
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            yield return new WaitForSeconds(0.5f);
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var under = BUnderCeiling(hero, hero.transform.position, 3f, 8f, out int dir, out float run);
            Plugin.Log.LogInfo($"[AutoTest] 录制B 重力球 {room}：起点 {under:F1}，往{(dir > 0 ? "右" : "左")}，天花板长 {run:F1}");
            BWear(ItemType.GravityGlobe);
            hero.transform.position = under;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 3f, 2f), 99f);
            StartRec(clip);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { BKeep(); Player.TerraMovement.DevMove = dir; yield return null; }
            Player.GravityGlobe.DevUpPress = true;
            float t0 = Time.time;
            while (Time.time - t0 < 1.6f && !(Player.GravityGlobe.Flipped && hero.cState.onGround && Time.time - t0 > 0.2f))
            {
                BKeep();
                Player.TerraMovement.DevMove = dir;
                SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 3f, 1f), 6f);
                yield return null;
            }
            bool flipped = Player.GravityGlobe.Flipped, onCeiling = hero.cState.onGround;
            float x0 = col.bounds.center.x;
            float walk = Mathf.Clamp(run / 9f, 1.4f, 2.8f);
            for (float t = 0f; t < walk; t += Time.deltaTime)
            {
                BKeep();
                Player.TerraMovement.DevMove = dir;
                SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 4f, -1f), 6f);
                if (Physics2D.Raycast(col.bounds.center, Vector2.right * dir, 1.2f, BTerrain).collider != null) break;
                yield return null;
            }
            float walked = col.bounds.center.x - x0;
            Player.GravityGlobe.DevUpPress = true;
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                BKeep();
                Player.TerraMovement.DevMove = t < 0.6f ? dir : 0;
                SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 3f, 0.5f), 6f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            StopRec();
            if (Player.GravityGlobe.Flipped) Player.GravityGlobe.DevUpPress = true;
            Plugin.Log.LogInfo($"[AutoTest] 录制B 重力球：翻转 {flipped}，站上天花板 {onCeiling}，倒着走了 {walked:F1}");
        }

        // ------------------------------------------------------------------ the nail at the mouse

        /// <summary>
        /// The nail swung at the cursor, any way round ("骨钉朝鼠标劈砍" on): among a room's flyers, each swing at the
        /// nearest one in reach, else round the circle; hops between, so some go down from the air.
        /// </summary>
        IEnumerator BNailAim(string room, string clip, float seconds)
        {
            EnemyRespawn.Multiplier = 6;
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            yield return null;
            var inv = mod.Inventory;
            int nail = SlotOf(ItemType.KnightNail);
            if (nail < 0) { inv.Pickup(ItemType.KnightNail, 1); nail = SlotOf(ItemType.KnightNail); }
            if (nail < 0) { Plugin.Log.LogInfo("[AutoTest] 录制B 骨钉：没有骨钉"); yield break; }
            inv.Selected = nail;
            inv.Override = -1;
            Player.TerraCamera.Zoom = 2f;
            bool was = NailAim.On;
            NailAim.Set(true);
            var col = hero.GetComponent<BoxCollider2D>();
            // The densest group, stood under.
            var foes = BSceneFoes(room);
            Vector2 best = col.bounds.center;
            int most = 0;
            foreach (var a in foes)
            {
                int n = 0;
                foreach (var b in foes) if (Vector2.Distance(BCentre(a), BCentre(b)) < 6f) n++;
                if (n > most) { most = n; best = BCentre(a); }
            }
            float fl = BFloor(best, 12f);
            hero.transform.position = Clear(hero, new Vector2(best.x - 2.5f, float.IsNaN(fl) ? best.y : fl + 0.1f));
            bToughK = 3f;
            BToughen(BFoes(best, 12f));
            yield return new WaitForSeconds(0.6f);
            aimInit = false;
            float sweep = 30f;
            int swings0 = NailItem.DevSwings;
            StartRec(clip);
            float t0 = Time.time, hopAt = Time.time + 1.2f;
            Vector2 aimPt = (Vector2)col.bounds.center + new Vector2(3f, 1.5f);
            while (Time.time - t0 < seconds)
            {
                BKeep();
                Vector2 c = col.bounds.center;
                var near = BFoes(c, 4.2f);
                BToughen(BFoes(c, 14f));
                HealthManager target = null;
                float bd = float.MaxValue;
                foreach (var hm in near) { float d = Vector2.Distance(BCentre(hm), c); if (d < bd) { bd = d; target = hm; } }
                int before = NailItem.DevSwings;
                if (target != null) aimPt = BCentre(target);
                else aimPt = c + new Vector2(Mathf.Cos(sweep * Mathf.Deg2Rad), Mathf.Sin(sweep * Mathf.Deg2Rad)) * 3f;
                SmoothAim(aimPt, 14f);
                NailAim.DevAim = aimNow;
                NailItem.DevHold = true;
                yield return null;
                if (NailItem.DevSwings > before) sweep += 67f;
                // Now and then a hop (a down slash from the air when the cursor is below).
                if (Time.time > hopAt && hero.cState.onGround)
                {
                    hopAt = Time.time + Random.Range(1.2f, 2f);
                    Player.TerraMovement.DevGroundJump(hero);
                    Player.TerraMovement.DevJump = true;
                }
                else if (Time.time > hopAt - 0.9f) Player.TerraMovement.DevJump = false;
                // Drift with the target.
                float dx = aimPt.x - c.x;
                Player.TerraMovement.DevMove = Mathf.Abs(dx) > 3f ? (int)Mathf.Sign(dx) : 0;
            }
            NailItem.DevHold = null;
            NailAim.DevAim = null;
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.3f);
            StopRec();
            NailAim.Set(was);
            bToughK = 1f;
            Plugin.Log.LogInfo($"[AutoTest] 录制B 骨钉朝鼠标：挥了 {NailItem.DevSwings - swings0} 下，击杀 {bKills}");
        }

        // ------------------------------------------------------------------ the zoom

        /// <summary>The view zoomed out (Terraria's zoom from 2, the game's own view, toward 1, twice as far) in a big room, and a little back in.</summary>
        IEnumerator BZoom(string room, string clip, int loadout, Vector2 startOff, float to = 1f)
        {
            Player.TerraCamera.Zoom = 2f;
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + startOff);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            var col = hero.GetComponent<BoxCollider2D>();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 1.5f), 99f);
            StartRec(clip);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { BKeep(); yield return null; }
            float dur = 2.6f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                BKeep();
                Player.TerraCamera.Zoom = Mathf.Lerp(2f, to, Mathf.SmoothStep(0f, 1f, t / dur));
                SmoothAim((Vector2)col.bounds.center + new Vector2(3f + t, 1.5f), 3f);
                yield return null;
            }
            Player.TerraCamera.Zoom = to;
            for (float t = 0f; t < 1.8f; t += Time.deltaTime) { BKeep(); yield return null; }
            StopRec();
            Player.TerraCamera.Zoom = 2f;
        }

        /// <summary>The view zoomed out from the middle of the Colosseum's arena (its trial begun, its first fools coming down).</summary>
        IEnumerator BZoomArena(string arena, string clip, int loadout, float to)
        {
            Player.TerraCamera.Zoom = 2f;
            mod.DevNoFallDamage = true;
            yield return BColosseum(arena);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            yield return null;
            BWear(ItemType.EmpressFlightBooster);
            int sword = SlotOf(ItemType.Zenith);
            if (sword >= 0) mod.Inventory.Selected = sword;
            // The arena's own title (shown as the trial opens) waited out.
            yield return BWaitTitle();
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            float floor = BFloor(col.bounds.center, 6f);
            if (float.IsNaN(floor)) floor = col.bounds.min.y;
            bool camWas = Player.TerraCamera.Enabled;
            // The view on the hero, who rises to the arena's middle height as it widens.
            Player.TerraCamera.Enabled = true;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 1.5f), 99f);
            void Hover(float height)
            {
                float dy = floor + height - col.bounds.center.y, vy = rb.linearVelocity.y;
                bool rise = dy > 0.3f || (dy > -0.6f && vy < -1.2f);
                if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = rise;
            }
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { BKeep(); Hover(1.2f); yield return null; }
            StartRec(clip);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { BKeep(); Hover(1.2f); yield return null; }
            float dur = 3f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                BKeep();
                float u = Mathf.SmoothStep(0f, 1f, t / dur);
                Player.TerraCamera.Zoom = Mathf.Lerp(2f, to, u);
                Hover(Mathf.Lerp(1.2f, 7f, u));
                SmoothAim((Vector2)col.bounds.center + new Vector2(3f + t * 2f, 1.5f - t), 3f);
                yield return null;
            }
            Player.TerraCamera.Zoom = to;
            for (float t = 0f; t < 2f; t += Time.deltaTime) { BKeep(); Hover(7f); yield return null; }
            StopRec();
            Player.TerraMovement.DevJump = false;
            Player.TerraCamera.Zoom = 2f;
            Player.TerraCamera.Enabled = camWas;
        }

        IEnumerator P4_bzoom(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            yield return BZoomArena("Bronze", "move_zoom", 0, 1.05f);
        }

        IEnumerator P4_bfinal(HeroController hero)
        {
            yield return P4_bzoom(hero);
            yield return P4_bcolo(hero);
        }

        /// <summary>The movement takes redone: bright rooms, the area titles waited out.</summary>
        IEnumerator P4_bmove2(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BShadowDash("Crossroads_05", "move_shadowdash", 4);
            yield return BSuperDash("Ruins1_28", "move_superdash", 2);
            Player.TerraCamera.Zoom = 1.6f;
            yield return BWings("Crossroads_07", "move_wings", 0, 75f);
            Player.TerraCamera.Zoom = 1.7f;
            yield return BHook("Ruins1_01", "move_hook", 1, 7);
            yield return BHook("Crossroads_21", "move_hook2", 2, 7);
            yield return BGravity("Mines_02", "move_gravity", 3);
            yield return BNailAim("Fungus2_03", "move_nailaim", 6f);
            yield return BZoomArena("Bronze", "move_zoom", 0, 1.1f);
            Player.TerraCamera.Zoom = 2f;
        }

        // ------------------------------------------------------------------ pets

        /// <summary>
        /// A pet (and a light pet) out, following the hero on a walk across a room: walked, hopped over what's in the
        /// way, a stop now and then so the pet catches up and idles.
        /// </summary>
        IEnumerator BPets(string room, string clip, ItemType pet, ItemType light, float seconds, Vector2 startOff, int dir)
        {
            EnemyRespawn.Multiplier = 0;
            yield return BEnter(room);
            var hero = HeroController.instance;
            Minions.Instance?.Clear();
            mod.TrySwitchLoadout(1);
            yield return null;
            var inv = mod.Inventory;
            foreach (var hm in BSceneFoes(room)) hm.gameObject.SetActive(false);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + startOff);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            BPetsAway();
            yield return null;
            inv.Misc[Pets.PetSlot] = pet != ItemType.None ? ItemStack.Of(pet) : default;
            inv.Misc[Pets.LightSlot] = light != ItemType.None ? ItemStack.Of(light) : default;
            yield return new WaitForSeconds(2f);
            var col = hero.GetComponent<BoxCollider2D>();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 3f, 1f), 99f);
            StartRec(clip);
            float t0 = Time.time, pauseAt = Time.time + 2.2f, pauseEnd = 0f;
            while (Time.time - t0 < seconds)
            {
                BKeep();
                Vector2 c = col.bounds.center;
                float phase = (Time.time - t0) % 1.5f;
                bool paused = phase > 0.75f;
                // The way ahead blocked: turn round if it's a wall, hop if it's low.
                var wall = Physics2D.Raycast(c, Vector2.right * dir, 1.4f, BTerrain);
                var high = Physics2D.Raycast(c + Vector2.up * 2.5f, Vector2.right * dir, 1.6f, BTerrain);
                if (wall.collider != null && high.collider != null) dir = -dir;
                else if (wall.collider != null && hero.cState.onGround) { Player.TerraMovement.DevGroundJump(hero); Player.TerraMovement.DevJump = true; }
                if (hero.cState.onGround && wall.collider == null) Player.TerraMovement.DevJump = false;
                Player.TerraMovement.DevMove = paused ? 0 : dir;
                SmoothAim(c + new Vector2(dir * 3f, 1f), 4f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            yield return new WaitForSeconds(0.4f);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制B 宠物 {clip}：{pet} {light}，宠物 {Minions.DevPet(false)?.id}，光 {Minions.DevPet(true)?.id}");
        }

        /// <summary>Every pet sent away: both pet slots emptied, every pet's buff taken off.</summary>
        void BPetsAway()
        {
            var inv = mod.Inventory;
            inv.Misc[Pets.PetSlot] = default;
            inv.Misc[Pets.LightSlot] = default;
            foreach (var t in Pets.All)
            {
                var d = Pets.Of(t);
                if (d != null && Player.PlayerBuffs.Has(d.Buff)) Player.PlayerBuffs.Remove(d.Buff);
            }
        }

        IEnumerator P4_bdash(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BShadowDash("Ruins1_28", "move_shadowdash", 4);
            yield return BSuperDash("Ruins2_04", "move_superdash", 2);
            Player.TerraCamera.Zoom = 2f;
        }

        IEnumerator P4_bdash2(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BShadowDash("Ruins2_04", "move_shadowdash", 4);
            Player.TerraCamera.Zoom = 2f;
        }

        IEnumerator P4_bpets4(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BShadowDash("Ruins2_04", "move_shadowdash", 4);
            Player.TerraCamera.Zoom = 1.7f;
            yield return BGravity("Ruins1_01", "move_gravity2", 0);
            Player.TerraCamera.Zoom = 2f;
            yield return BPets("Fungus1_10", "pets_1", ItemType.DogWhistle, ItemType.FairyBell, 7f, Vector2.zero, 1);
            yield return BPets("Crossroads_05", "pets_2", ItemType.MoonLordPetItem, ItemType.SuspiciousLookingTentacle, 7f, Vector2.zero, 1);
            yield return BPets("Fungus2_03", "pets_3", ItemType.EyeOfCthulhuPetItem, ItemType.WispinaBottle, 7f, Vector2.zero, 1);
            yield return BPets("Town", "pets_4", ItemType.ResplendentDessert, ItemType.FairyQueenPetItem, 7f, new Vector2(40f, 0f), 1);
            yield return BPets("Crossroads_21", "pets_5", ItemType.SpiderEgg, ItemType.CrimsonHeart, 7f, Vector2.zero, 1);
            BPetsAway();
        }

        IEnumerator P4_bpets3(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BShadowDash("Mines_03", "move_shadowdash", 4);
            Player.TerraCamera.Zoom = 2f;
            yield return BPets("Town", "pets_1", ItemType.DogWhistle, ItemType.FairyBell, 6f, new Vector2(12f, 0f), 1);
            yield return BPets("Fungus1_10", "pets_2", ItemType.MoonLordPetItem, ItemType.SuspiciousLookingTentacle, 6f, Vector2.zero, 1);
            yield return BPets("Ruins1_28", "pets_3", ItemType.EyeOfCthulhuPetItem, ItemType.WispinaBottle, 6f, Vector2.zero, 1);
            yield return BPets("Town", "pets_4", ItemType.ResplendentDessert, ItemType.FairyQueenPetItem, 6f, new Vector2(40f, 0f), 1);
            yield return BPets("Crossroads_21", "pets_5", ItemType.SpiderEgg, ItemType.CrimsonHeart, 6f, Vector2.zero, 1);
            BPetsAway();
            yield return BChester("Town", "pets_chester", new Vector2(24f, 0f));
        }

        IEnumerator P4_bpets2(HeroController hero)
        {
            yield return P4_bdash(hero);
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 2f;
            yield return BPets("Town", "pets_1", ItemType.DogWhistle, ItemType.FairyBell, 6f, new Vector2(12f, 0f), 1);
            yield return BPets("Fungus1_10", "pets_2", ItemType.MoonLordPetItem, ItemType.SuspiciousLookingTentacle, 6f, Vector2.zero, 1);
            yield return BPets("Ruins1_28", "pets_3", ItemType.EyeOfCthulhuPetItem, ItemType.WispinaBottle, 6f, Vector2.zero, 1);
            yield return BPets("Town", "pets_4", ItemType.ResplendentDessert, ItemType.FairyQueenPetItem, 6f, new Vector2(40f, 0f), 1);
            yield return BPets("Crossroads_21", "pets_5", ItemType.SpiderEgg, ItemType.CrimsonHeart, 6f, Vector2.zero, 1);
            BPetsAway();
            yield return BChester("Town", "pets_chester", new Vector2(24f, 0f));
        }

        /// <summary>Chester out: walked with, then the cursor to him, a right click: the Piggy Bank opens on him; shut again.</summary>
        IEnumerator BChester(string room, string clip, Vector2 startOff)
        {
            EnemyRespawn.Multiplier = 0;
            yield return BEnter(room);
            var hero = HeroController.instance;
            Minions.Instance?.Clear();
            mod.TrySwitchLoadout(1);
            yield return null;
            var inv = mod.Inventory;
            foreach (var hm in BSceneFoes(room)) hm.gameObject.SetActive(false);
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + startOff);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            BPetsAway();
            yield return null;
            inv.Misc[Pets.PetSlot] = ItemStack.Of(ItemType.ChesterPetItem);
            // Some money and things in the bank to show.
            yield return new WaitForSeconds(2f);
            var col = hero.GetComponent<BoxCollider2D>();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 1f), 99f);
            StartRec(clip);
            for (float t = 0f; t < 1.6f; t += Time.deltaTime) { BKeep(); Player.TerraMovement.DevMove = 1; SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 1f), 4f); yield return null; }
            Player.TerraMovement.DevMove = 0;
            for (float t = 0f; t < 1.0f; t += Time.deltaTime) { BKeep(); yield return null; }
            var ch = Minions.DevChester();
            if (ch.HasValue)
            {
                PointerOn(UI.TerraCursor.DevAt ?? new Vector2(Screen.width * 0.6f, Screen.height * 0.5f));
                yield return Glide(GuiOf(Effects.TerraParticles.ToWorld(Minions.DevChester().Value.at)), 0.6f);
                yield return new WaitForSeconds(0.25f);
                bool opened = Minions.Instance != null && Minions.Instance.DevClickChester();
                Plugin.Log.LogInfo($"[AutoTest] 录制B 切斯特：打开 {opened}，存储 {World.Chests.OpenBankKind}");
                for (float t = 0f; t < 2.6f; t += Time.deltaTime) { yield return null; }
                if (World.Chests.OpenWith == ItemType.ChesterPetItem)
                {
                    yield return Glide(GuiOf(Effects.TerraParticles.ToWorld(Minions.DevChester().Value.at)), 0.4f);
                    yield return new WaitForSeconds(0.2f);
                    Minions.Instance.DevClickChester();
                }
                InventoryGui.Open = false;
                yield return new WaitForSeconds(0.8f);
                PointerOff();
            }
            StopRec();
        }

        IEnumerator P4_bmove(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            yield return BShadowDash("Crossroads_21", "move_shadowdash", 4);
            yield return BSuperDash("Ruins1_28", "move_superdash", 2);
            Player.TerraCamera.Zoom = 1.6f;
            yield return BWings("Crossroads_07", "move_wings", 0, 75f);
            Player.TerraCamera.Zoom = 1.7f;
            yield return BHook("Crossroads_21", "move_hook", 1, 6);
            yield return BHook("Ruins1_01", "move_hook2", 2, 6);
            yield return BGravity("Crossroads_16", "move_gravity", 3);
            yield return BGravity("Mines_02", "move_gravity2", 0);
            yield return BNailAim("Crossroads_13", "move_nailaim", 6f);
            yield return BZoom("Ruins1_27", "move_zoom", 0, Vector2.zero, 1f);
            Player.TerraCamera.Zoom = 2f;
        }

        IEnumerator P4_bpets(HeroController hero)
        {
            yield return P4Kit();
            yield return P4_bredo2(hero);
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BPets("Fungus1_10", "pets_1", ItemType.DogWhistle, ItemType.FairyBell, 6f, Vector2.zero, 1);
            yield return BPets("Crossroads_05", "pets_2", ItemType.DD2PetDragon, ItemType.SuspiciousLookingTentacle, 6f, Vector2.zero, 1);
            yield return BPets("Ruins1_28", "pets_3", ItemType.ZephyrFish, ItemType.MagicLantern, 6f, Vector2.zero, 1);
            yield return BPets("Mines_03", "pets_4", ItemType.ResplendentDessert, ItemType.CompanionCube, 6f, Vector2.zero, 1);
            yield return BChester("Crossroads_21", "pets_chester", Vector2.zero);
            Player.TerraCamera.Zoom = 2f;
        }
    }
}
