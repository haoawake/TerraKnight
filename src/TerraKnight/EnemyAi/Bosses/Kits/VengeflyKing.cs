using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// The Vengefly King (Big Buzzer; 空洞骑士首领新版AI设计.md §2.1): his swoop's lowest point is where the player will be
    /// (led as its wind-up starts, drawn as a line from him to it), its sideways speed made to reach it there; from the second
    /// rank a swoop is now and then followed by another, aimed afresh; no more than three Vengeflies of his up at once; pace
    /// ×0.9; a player kept up in the air gets a swoop at his height; in the fight he goes for the player wherever he is in
    /// the room (up under the ceiling, on a platform over his alert range), never off to lurk while the player is there;
    /// against a player well up (under the ceiling, where his U-shaped swoop can't come up to) he rams straight at him
    /// (his own body and swoop pictures). With the room's rock between them he flies round it by the room's grid, and his
    /// swoop's tween stops short of the room's ground.
    /// </summary>
    [Kit("BigBuzzer")]
    internal sealed class VengeflyKingKit : BossKit
    {
        protected override string ControlName => "Big Buzzer";
        protected override float Pace => 0.9f;
        protected override Look Theme => Look.Rock;
        protected override bool Roams => true;

        /// <summary>The swoop's low point, and the swoop's time down to it (the game's iTween, 0.8 s).</summary>
        Vector2 aim;
        bool aimed, again, chained;
        const float SwoopTime = 0.8f;
        /// <summary>His alert range's own "In Alert Range" (read every frame by his Idle and Lurk).</summary>
        FsmBool inRange;

        protected override void Setup()
        {
            Tempo("Idle");
            OnEnter("Swoop Antic", AimSwoop, first: false);
            // The low point behind rock from where he is: during the wind-up he flies round to a clear line first.
            OnEnter("Swoop Antic", () =>
            {
                if (!aimed || Grace || !ClearSpot(aim, 10f, Level, out var spot)) return;
                Run(FlyTo(spot, 0.5f, 22f));
            }, first: false);
            At("Swoop Direction", 0, () =>
            {
                var h = Float("Swoop Height");
                if (aimed && h != null) h.Value = aim.y;
                // Facing the low point from where he is now (he may have flown round it).
                var face = Bool("Face Right");
                if (aimed && face != null && Mathf.Abs(aim.x - Me.x) > 0.5f) face.Value = aim.x > Me.x;
            });
            OnEnter("Swoop L", SwoopSpeed, first: false);
            // His idle's flight after the player (the game's 6 a second) a half faster: across his long room in time.
            var fly = Batch1Helpers.First<HutongGames.PlayMaker.Actions.DistanceFly>(State("Idle"));
            if (fly != null && fly.speedMax != null) wayspeed = fly.speedMax.Value *= 1.5f;
            OnEnter("Swoop R", SwoopSpeed, first: false);
            OnEnter("Swoop Rise", () =>
            {
                again = !chained && Rank >= 1 && Random.value < 0.4f;
                chained = false;
            });
            Branch("Idle", "Swoop Antic", () =>
            {
                if (!again) return false;
                again = false;
                chained = true;
                return true;
            });
            if (AddState("TK Ram", Ram, "Idle") != null)
                Branch("Choose Attack", "TK Ram", () => !Grace && HasTarget && (TargetHeight > 4f || TargetCenter.y - Me.y > 3f) && Random.value < 0.6f);
            Branch("Choose Attack", "Swoop Antic", WantAntiAir);
            // The room's rock between him and the player: no move into it while his way round it goes on (taken first).
            Branch("Choose Attack", "Idle", () => HasTarget && way.Count > 1 && !ClearLine());
            // Lurking (the player out of his alert range): straight back after him while he is in the room, his alert range's
            // bool set as he turns: Idle reads it on entering, and false there sent him to Lurk and back a thousand times in
            // a frame, and the game stopped his script for good (frozen by a wall, the user's 10-29 "反击蝇之王会卡墙").
            Branch("Lurk", "Idle", () =>
            {
                if (inRange == null || !InRoom()) return false;
                inRange.Value = true;
                return true;
            });
            var alert = Go("Alert Range")?.Value;
            if (alert != null)
                foreach (var f in alert.GetComponents<PlayMakerFSM>())
                    if ((inRange = f.FsmVariables.FindFsmBool("In Alert Range")) != null) break;
            // Not when the swoop would send him straight back here (too many swoops in a row): no loop.
            Branch("Summon Antic", "Swoop Antic", () => (Int("Swoops in A Row")?.Value ?? 0) <= 3 && Vengeflies() >= 3);
        }

        /// <summary>The fight on and the player in the room (over its floor up to well above its top).</summary>
        bool InRoom() => Engaged && HasTarget && new Rect(Arena.xMin - 4f, Arena.yMin - 2f, Arena.width + 8f, Arena.height + 14f).Contains(TargetCenter);

        /// <summary>
        /// In the room, the player counts as in his alert range (his Idle sends him off to lurk otherwise); his swoop kept
        /// out of the room's ground, his idle's flight taken round its rock.
        /// </summary>
        protected override void Tick()
        {
            if (inRange != null && !inRange.Value && InRoom()) inRange.Value = true;
            SwoopClear();
            Detour();
        }

        /// <summary>No room's rock on the line from him to the player.</summary>
        bool ClearLine()
        {
            var to = TargetCenter - Me;
            return to.sqrMagnitude < 1f || Hostiles.NativeRay(Me, to.normalized, to.magnitude).collider == null;
        }

        // ------------------------------------------------------------------ round the room's rock

        /// <summary>His way round the room's rock to the player (cell middles, his first), his idle's speed on it.</summary>
        readonly System.Collections.Generic.List<Vector2> way = new System.Collections.Generic.List<Vector2>();
        float wayspeed = 9f, nextWay;
        /// <summary>The room's grid as his body sees it (rock within his half-size of a cell), a search's marks: kept, no garbage.</summary>
        bool[] rock, wide, shut;
        int[] seen, back, sums;
        int stamp;
        readonly System.Collections.Generic.Queue<int> open = new System.Collections.Generic.Queue<int>();

        /// <summary>
        /// His idle flies him straight for the player (the game's DistanceFly): with the room's rock between them (Greenpath's
        /// room is 180 wide, a pillar hangs in its middle and rock stands at its right end) he pressed on it till his next
        /// move and came back to it after (the user's 10-29 "反击蝇之王会卡墙"). Rock on the line: he flies the way round it
        /// his body fits through by the room's grid, the farthest point of it he can fly to straight, at his idle's speed.
        /// </summary>
        void Detour()
        {
            if (StateNow != "Idle" || rb == null || body == null || !body.enabled || !HasTarget || ClearLine())
            {
                way.Clear();
                if (DevWatch) DevWay = "";
                return;
            }
            if (Time.time >= nextWay)
            {
                nextWay = Time.time + 0.5f;
                FindWay();
            }
            if (way.Count == 0) return;
            // Where he is on it: the nearest of its next points (those before it passed).
            int at = 0;
            float near = float.MaxValue;
            for (int i = 0; i < way.Count && i < 24; i++)
            {
                float d = (way[i] - Me).sqrMagnitude;
                if (d < near) { near = d; at = i; }
            }
            if (at > 0) way.RemoveRange(0, at);
            // The farthest of the next points he flies to straight, by his whole body (a skin short of it: a narrower look
            // found ways his body caught on the rock's corners).
            var size = (Vector2)body.bounds.size - new Vector2(0.1f, 0.1f);
            int reach = 0;
            for (int i = 1; i < way.Count && i < 16; i++)
            {
                var d = way[i] - Me;
                if (Hostiles.NativeBox(Me, size, d.normalized, d.magnitude).collider != null) break;
                reach = i;
            }
            var go = Vector2.zero;
            if (reach == 0 && way.Count > 1)
            {
                // None clear: pressed on the room's ground, where its colliders stand a little past the grid's cells (a
                // floating platform's top over its cells). A cell or two on along the way, turned off what he meets.
                reach = Mathf.Min(2, way.Count - 1);
                var d = way[reach] - Me;
                var hit = Hostiles.NativeBox(Me, size, d.normalized, d.magnitude);
                go = d.normalized;
                if (hit.collider != null) go = (go + hit.normal).normalized;
                if (DevWatch) DevBlock = hit.collider != null ? $"{hit.collider.name}/{hit.collider.GetType().Name} 距 {hit.distance:F2} 点 {hit.point:F2} 法线 {hit.normal:F2}" : "";
            }
            else if ((way[reach] - Me).sqrMagnitude > 0.01f) go = (way[reach] - Me).normalized;
            if (DevWatch) DevWay = $"路 {way.Count} 点，飞向第 {reach} 点 {way[reach]:F1}，终点 {way[way.Count - 1]:F1}";
            rb.linearVelocity = go * wayspeed;
        }

        /// <summary>Dev: his way round the rock as last flown.</summary>
        internal static string DevWay = "", DevBlock = "";
        /// <summary>Dev: <see cref="DevWay"/> kept (a line a frame: garbage, so only for a test).</summary>
        internal static bool DevWatch;

        /// <summary>
        /// The shortest way through the room's grid from him to near the player: cells clear of its rock by his half-size
        /// (a little short of it), none cut across a corner; the cells next to him free to leave (he may be pressed on rock).
        /// </summary>
        void FindWay()
        {
            way.Clear();
            var region = World.TerrainSystem.Find(Scene);
            if (region == null) return;
            var grid = region.Grid;
            int w = grid.Width, h = grid.Height, n = w * h;
            if (rock == null || rock.Length != n)
            {
                rock = new bool[n]; wide = new bool[n]; shut = new bool[n];
                seen = new int[n]; back = new int[n]; sums = new int[Mathf.Max(w, h) + 1];
            }
            const float cell = World.CellGrid.CellSize;
            var ext = body.bounds.extents;
            int rx = Mathf.CeilToInt(ext.x * 0.9f / cell), ry = Mathf.CeilToInt(ext.y * 0.9f / cell);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    rock[y * w + x] = grid.Get(x, y) == World.CellKind.Original;
            // Rock within rx across, then within ry up or down of that (past the grid's edge counts as rock).
            for (int y = 0; y < h; y++)
            {
                sums[0] = 0;
                for (int x = 0; x < w; x++) sums[x + 1] = sums[x] + (rock[y * w + x] ? 1 : 0);
                for (int x = 0; x < w; x++)
                    wide[y * w + x] = x - rx < 0 || x + rx >= w || sums[x + rx + 1] - sums[x - rx] > 0;
            }
            for (int x = 0; x < w; x++)
            {
                sums[0] = 0;
                for (int y = 0; y < h; y++) sums[y + 1] = sums[y] + (wide[y * w + x] ? 1 : 0);
                for (int y = 0; y < h; y++)
                    shut[y * w + x] = y - ry < 0 || y + ry >= h || sums[y + ry + 1] - sums[y - ry] > 0;
            }
            var off = region.CellOffset;
            var me = World.CellGrid.WorldToCell(Me) - off;
            if (me.x < 0 || me.y < 0 || me.x >= w || me.y >= h) return;
            stamp++;
            open.Clear();
            int start = me.y * w + me.x, goal = -1, best = -1;
            float bestFar = float.MaxValue;
            seen[start] = stamp;
            back[start] = -1;
            open.Enqueue(start);
            while (open.Count > 0)
            {
                int c = open.Dequeue(), cx = c % w, cy = c / w;
                float far = (World.CellGrid.CellCenter(cx + off.x, cy + off.y) - TargetCenter).sqrMagnitude;
                if (!shut[c] && far < 9f) { goal = c; break; }
                if (!shut[c] && far < bestFar) { bestFar = far; best = c; }
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int k = ny * w + nx;
                        if (seen[k] == stamp) continue;
                        bool near = Mathf.Abs(nx - me.x) <= 3 && Mathf.Abs(ny - me.y) <= 3;
                        if (rock[k] || !near && (shut[k] || dx != 0 && dy != 0 && (shut[cy * w + nx] || shut[ny * w + cx]))) continue;
                        seen[k] = stamp;
                        back[k] = c;
                        open.Enqueue(k);
                    }
            }
            // The player where his body can't come (a narrow way): as near to him as it can.
            if (goal < 0) goal = best;
            if (goal < 0)
            {
                if (DevWatch) DevWay = $"找不到路（从格 {me}）";
                return;
            }
            for (int c = goal; c >= 0; c = back[c]) way.Add(World.CellGrid.CellCenter(c % w + off.x, c / w + off.y));
            way.Reverse();
        }

        /// <summary>Where across his body the swoop looks down or up for the room's ground (shares of his half-width).</summary>
        static readonly float[] Across = { -0.8f, 0f, 0.8f };

        /// <summary>
        /// The game's swoop moves him by iTween, down to its low point and then 3.5 up, through whatever is in the way (a
        /// platform passed over on the way down, a low ceiling over the rise): the tween stopped as his body comes up to the
        /// room's ground, his sideways flight going on. The rise ends only with its tween (no wait of its own): stopped, it
        /// is ended as the tween would.
        /// </summary>
        void SwoopClear()
        {
            string s = StateNow;
            bool down = s == "Swoop L" || s == "Swoop R";
            if (!down && s != "Swoop Rise" || body == null || !body.enabled || GetComponent<iTween>() == null) return;
            var b = body.bounds;
            var dir = down ? Vector2.down : Vector2.up;
            foreach (float dx in Across)
                if (Hostiles.NativeRay(new Vector2(b.center.x + dx * b.extents.x, b.center.y), dir, b.extents.y + 0.4f).collider != null)
                {
                    iTween.Stop(gameObject);
                    if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    if (!down) Control.Fsm.Event(FsmEvent.Finished);
                    return;
                }
        }

        /// <summary>
        /// Where the swoop bottoms out: where the player will be once it gets there, kept off the floor and ceiling. The
        /// game's swoop moves his transform there by iTween, through whatever is in the way: his body's bottom and top (from
        /// his transform) are kept clear of them, not his middle (Godhome's hall: he sank into the floor at each swoop). The
        /// floor is looked for from no lower than the player is now: a falling player's lead goes on down into the floor,
        /// and from in there no floor is found under it.
        /// </summary>
        void AimSwoop()
        {
            var p = Lead(0.45f + SwoopTime);
            float under = body != null ? transform.position.y - body.bounds.min.y : 1.2f, over = body != null ? body.bounds.max.y - transform.position.y : 1.5f;
            var look = new Vector2(p.x, Mathf.Max(p.y, TargetCenter.y) + 0.5f);
            if (RoomRockAt(look)) look.x = TargetCenter.x;
            float floor = GroundBelow(look) + Mathf.Max(1.2f, under + 0.3f), roof = CeilingAbove(p) - Mathf.Max(1.5f, over + 0.3f);
            p.y = Mathf.Clamp(p.y, floor, Mathf.Max(floor, roof));
            aim = p;
            aimed = true;
            MarkLine(Me, aim, Warn(0.5f));
        }

        /// <summary>The swoop's sideways speed (the game's ±20): enough to be over the low point as it gets down to it.</summary>
        void SwoopSpeed()
        {
            if (!aimed || rb == null) return;
            aimed = false;
            float face = Bool("Face Right")?.Value == true ? 1f : -1f;
            float speed = Mathf.Clamp(Mathf.Abs(aim.x - Me.x) / SwoopTime, 10f, 28f);
            if (Mathf.Sign(aim.x - Me.x) != face) speed = 10f;
            rb.linearVelocity = new Vector2(face * speed, rb.linearVelocity.y);
        }

        /// <summary>The ways round the low point a swoop can come in from: level with it or a little over or under.</summary>
        static readonly float[] Level = { 0f, 20f, -20f, 180f, 160f, -160f, 40f, 140f };

        /// <summary>
        /// The player behind the room's rock from here: a free spot with a clear line to him (round him, the nearest to him
        /// first), to fly to before he swoops.
        /// </summary>
        bool ClearSpot(Vector2 target, float reach, float[] angles, out Vector2 spot)
        {
            spot = Me;
            var to = target - Me;
            if (Hostiles.NativeRay(Me, to.normalized, to.magnitude).collider == null) return false;
            float best = float.MaxValue;
            bool found = false;
            foreach (float deg in angles)
            {
                var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                var hit = Hostiles.NativeRay(target, dir, reach);
                float len = hit.collider != null ? hit.distance - 2f : reach;
                if (len < 4f) continue;
                var p = target + dir * len;
                if (Hostiles.NativeSolidAt(p)) continue;
                float d = (p - Me).sqrMagnitude;
                if (d < best) { best = d; spot = p; found = true; }
            }
            return found;
        }

        /// <summary>Flown to a spot (his own flight) through his wind-up, for a while at most.</summary>
        System.Collections.IEnumerator FlyTo(Vector2 spot, float seconds, float speed)
        {
            // Only through the wind-up it started in (the move's own speed is set as the next state starts).
            string state = StateNow;
            for (float until = Time.time + seconds; rb != null && Time.time < until && Active && StateNow == state;)
            {
                var d = spot - Me;
                if (d.sqrMagnitude < 0.25f) break;
                rb.linearVelocity = d.normalized * Mathf.Min(speed, d.magnitude / Mathf.Max(Time.deltaTime, 0.01f));
                yield return null;
            }
            if (rb != null && StateNow == state) rb.linearVelocity = Vector2.zero;
        }

        /// <summary>A short wind-up turned to the player, then straight at where he will be, until the room's rock stops him.</summary>
        System.Collections.IEnumerator Ram()
        {
            if (rb == null) yield break;
            Batch2.Play(gameObject, "Swoop Antic");
            float warn = Warn(0.45f);
            Batch5.Face(transform, TargetCenter.x);
            for (float t = 0f; t < warn && Active; t += Time.deltaTime)
            {
                rb.linearVelocity = Vector2.zero;
                yield return null;
            }
            Vector2 dir = Lead(0.3f) - Me;
            if (dir.sqrMagnitude < 0.01f) yield break;
            dir.Normalize();
            Batch5.Face(transform, Me.x + dir.x);
            Batch2.Play(gameObject, "Swoop");
            BossAi.DevMoves++;
            // His body hurts all the way (his own swoop's pictures show it: the hitbox unseen); on until the room's rock.
            var escort = Batch2.Escort(this, transform, Me - (Vector2)transform.position, 2.2f, 1.45f, Look.Rock);
            try
            {
                for (float until = Time.time + 1.3f; Time.time < until && Active;)
                {
                    rb.linearVelocity = dir * 26f;
                    if (Hostiles.NativeRay(Me, dir, 0.7f).collider != null) break;
                    yield return null;
                }
            }
            finally
            {
                if (escort != null) Destroy(escort.gameObject);
            }
            rb.linearVelocity = Vector2.zero;
        }

        /// <summary>His Vengeflies up in the room now.</summary>
        int Vengeflies()
        {
            int n = 0;
            foreach (var info in Items.EnemyStats.All)
                if (info.Hm != null && !info.Hm.isDead && info.Record == "Buzzer" && info.Hm.gameObject.activeInHierarchy && info.Hm.gameObject.scene == gameObject.scene) n++;
            return n;
        }
    }
}
