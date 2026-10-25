using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_v5G_*_zh"): recorder G's clips for the 4.4 trailer: the casino (its house built in Dirtmouth, the slot
    /// pulled by hand, the wheel flung, the pinball's marbles), the mounts, the four spells' class weapons, the nail arts and
    /// the ability shields. The casino's machines run on real time (Time.realtimeSinceStartup), so its clips are recorded
    /// with a recorder of their own that keeps real time with the video: each frame held to a fiftieth of a second, its
    /// picture encoded on worker threads (ImageConversion.EncodeArrayToJPG) so the frame itself stays short.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // ------------------------------------------------------------------ the paced recorder

        bool gRec;
        int gRecRun, gRecFrame, gLate, gVsync, gTargetFps;
        float gWorst;
        string gRecDir, gRecErr;
        int gPending;
        System.Collections.Concurrent.BlockingCollection<(byte[] Data, UnityEngine.Experimental.Rendering.GraphicsFormat Format, int W, int H, string Path)> gQueue;
        Thread[] gWorkers;

        /// <summary>Recording that keeps real time: 50 frames a second of game time, each frame also a fiftieth of a second of real time.</summary>
        void StartRecG(string clip)
        {
            if (zh) clip = "zh_" + clip;
            gRecDir = Path.Combine(outDir, "rec", clip);
            if (Directory.Exists(gRecDir)) Directory.Delete(gRecDir, true);
            Directory.CreateDirectory(gRecDir);
            gRecFrame = 0;
            gLate = 0;
            gWorst = 0f;
            gRecErr = null;
            if (gQueue == null)
            {
                gQueue = new System.Collections.Concurrent.BlockingCollection<(byte[], UnityEngine.Experimental.Rendering.GraphicsFormat, int, int, string)>();
                gWorkers = new Thread[3];
                for (int i = 0; i < gWorkers.Length; i++)
                {
                    gWorkers[i] = new Thread(GEncodeWorker) { IsBackground = true, Name = "TK promo G encoder " + i };
                    gWorkers[i].Start();
                }
            }
            gVsync = QualitySettings.vSyncCount;
            gTargetFps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            gRec = true;
            Time.captureFramerate = 50;
            mod.StartCoroutine(GRecLoop(++gRecRun));
            GClearChat();
            mod.StartCoroutine(GQuiet());
            Plugin.Log.LogInfo($"[AutoTest] 录制 开始 {clip}（按真实时间）");
        }

        void GEncodeWorker()
        {
            foreach (var job in gQueue.GetConsumingEnumerable())
            {
                try
                {
                    var jpg = ImageConversion.EncodeArrayToJPG(job.Data, job.Format, (uint)job.W, (uint)job.H, 0, 88);
                    File.WriteAllBytes(job.Path, jpg);
                }
                catch (System.Exception e) { gRecErr = e.Message; }
                finally { Interlocked.Decrement(ref gPending); }
            }
        }

        IEnumerator GRecLoop(int run)
        {
            float next = -1f, last = -1f;
            while (gRec && run == gRecRun)
            {
                yield return new WaitForEndOfFrame();
                if (!gRec || run != gRecRun) break;
                float now = Time.realtimeSinceStartup;
                if (last > 0f)
                {
                    float took = now - last;
                    if (took > 0.026f) gLate++;
                    if (took > gWorst) gWorst = took;
                }
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var data = tex.GetRawTextureData();
                var fmt = tex.graphicsFormat;
                int w = tex.width, h = tex.height;
                Object.Destroy(tex);
                Interlocked.Increment(ref gPending);
                gQueue.Add((data, fmt, w, h, Path.Combine(gRecDir, gRecFrame.ToString("D5") + ".jpg")));
                gRecFrame++;
                // The encoders behind: wait for them (memory), a frame late rather than lost.
                while (Volatile.Read(ref gPending) > 48) Thread.Sleep(1);
                // A fiftieth of a second a frame, real time as game time (far behind: start the count again).
                now = Time.realtimeSinceStartup;
                if (next < 0f || now - next > 0.2f) next = now;
                next += 0.02f;
                while (Time.realtimeSinceStartup < next) { }
                last = Time.realtimeSinceStartup;
            }
        }

        /// <summary>The paced recording stopped, its last frames written before going on.</summary>
        IEnumerator StopRecG()
        {
            gRec = false;
            Time.captureFramerate = 0;
            QualitySettings.vSyncCount = gVsync;
            Application.targetFrameRate = gTargetFps;
            UI.TerraCursor.DevAt = null;
            for (float t = 0f; t < 30f && Volatile.Read(ref gPending) > 0; t += Time.unscaledDeltaTime) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 录制 结束 {Path.GetFileName(gRecDir)}：{gRecFrame} 帧，慢帧 {gLate}，最慢 {gWorst * 1000f:F0} ms，未写完 {gPending}{(gRecErr != null ? "，出错 " + gRecErr : "")}");
        }

        /// <summary>The money carried set to so many geo (spent down or earned up), for amounts that look like a player's.</summary>
        static void GSetPurse(long geo)
        {
            long want = geo * 100L, have = Shop.Purse;
            if (have > want) Shop.Spend(have - want);
            else if (have < want) Shop.Earn(want - have);
        }

        /// <summary>The chat's lines cleared (the game's own reminders turned into chat lines would lie over a take).</summary>
        static void GClearChat()
        {
            var f = typeof(Net.NetChat).GetField("lines", BindingFlags.NonPublic | BindingFlags.Static);
            if (f?.GetValue(null) is System.Collections.IList list) list.Clear();
        }

        /// <summary>While a take is recorded (either recorder): the chat kept clear, each frame before it is drawn.</summary>
        IEnumerator GQuiet()
        {
            while (gRec || recording || gQuietAll)
            {
                GClearChat();
                yield return null;
            }
        }

        bool gQuietAll;

        /// <summary>A suite of recorder G's begun: the chat kept clear from here to its end (townsfolk leaving, reminders).</summary>
        void GBegin()
        {
            if (gQuietAll) return;
            gQuietAll = true;
            mod.StartCoroutine(GQuiet());
        }

        // ------------------------------------------------------------------ the casino's screens

        /// <summary>A point of a machine's art (its pixels from its top left) on the screen (GUI), as CasinoScenes.Frame lays it.</summary>
        static Vector2 GArt(float width, float x, float y)
        {
            float p = Mathf.Max(1f, Mathf.Floor(Mathf.Min(Screen.height / 270f, (Screen.width - 24f) / width)));
            var o = new Vector2(Mathf.Round((Screen.width - width * p) / 2f), Mathf.Round((Screen.height - 270f * p) / 2f));
            return o + new Vector2(x, y) * p;
        }

        /// <summary>A button of a machine's art clicked (its middle).</summary>
        IEnumerator GPush(float width, float x, float y, float w, float h, float glide = 0.4f)
        {
            yield return Glide(GArt(width, x + w / 2f, y + h / 2f), glide);
            yield return new WaitForSeconds(0.12f);
            UI.TerraCursor.DevClick();
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.15f);
        }

        // The casino's own random numbers (Casino.NextUInt / Next), to choose a round's outcome before it is played.
        static uint GCasinoNext(ref ulong x)
        {
            unchecked
            {
                if (x == 0) x = 0x9E3779B97F4A7C15UL;
                x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
                return (uint)((x * 2685821657736338717UL) >> 32);
            }
        }

        static int GCasinoNext(ref ulong x, int count)
        {
            uint bound = (uint)count, threshold = unchecked(0u - bound) % bound, r;
            do { r = GCasinoNext(ref x); } while (r < threshold);
            return (int)(r % bound);
        }

        static Casino.Saved GCasinoState => (Casino.Saved)typeof(Casino).GetField("state", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        /// <summary>The casino's random state set so the next slot spin stops on (a, b, c).</summary>
        static bool GStageSlots(int a, int b, int c)
        {
            for (ulong s = 7919; s < 7919 + 400000; s++)
            {
                ulong x = s;
                if (GCasinoNext(ref x, 5) != a || GCasinoNext(ref x, 5) != b || GCasinoNext(ref x, 5) != c) continue;
                GCasinoState.RandomState = s;
                return true;
            }
            return false;
        }

        /// <summary>The casino's random state set so the next turn of the wheel lands on a number of this colour.</summary>
        static bool GStageWheel(Casino.Colour colour)
        {
            for (ulong s = 104729; s < 104729 + 400000; s++)
            {
                ulong x = s;
                if (Casino.WheelColour(GCasinoNext(ref x, Casino.WheelSectors)) != colour) continue;
                GCasinoState.RandomState = s;
                return true;
            }
            return false;
        }

        /// <summary>A slot stop with these winnings (quarters of the stake times 20 back, i.e. SlotPayout of 20 copper): the first such.</summary>
        static (int, int, int) GSlotsWith(System.Func<long, int, bool> want)
        {
            for (int a = 0; a < 5; a++)
                for (int b = 0; b < 5; b++)
                    for (int c = 0; c < 5; c++)
                    {
                        long pay = Casino.SlotPayout(10000, a, b, c);
                        int lines = 0;
                        for (int l = 0; l < Casino.Lines.Length; l++) if (Casino.LineTriple(l, a, b, c)) lines++;
                        if (want(pay, lines)) return (a, b, c);
                    }
            return (0, 1, 2);
        }

        // ------------------------------------------------------------------ the casino built in Dirtmouth

        Vector2Int gCasinoOrigin;
        bool gCasinoBuilt;

        /// <summary>
        /// A flat stretch of Dirtmouth's street for the casino (26 by 12 cells): every column's ground within a fifth of a unit
        /// under its floor, nothing of the scene's in the way, closest to the hero's right.
        /// </summary>
        bool GFindCasinoSite(HeroController hero, out Vector2Int site)
        {
            const float c = CellGrid.CellSize;
            int w = PrefabHouses.Width(Casino.HouseArea);
            var col = hero.GetComponent<BoxCollider2D>();
            Vector2 h = col.bounds.center;
            site = default;
            float best = float.MaxValue;
            gSites.Clear();
            for (float x0 = h.x - 40f; x0 < h.x + 60f; x0 += c)
            {
                int cx = Mathf.FloorToInt(x0 / c);
                float top = float.MinValue, low = float.MaxValue;
                bool ok = true;
                for (int k = 0; k < w && ok; k++)
                {
                    var hit = Physics2D.Raycast(new Vector2((cx + k + 0.5f) * c, h.y + 8f), Vector2.down, 24f, 1 << ChunkColliders.TerrainLayer);
                    if (hit.collider == null || hit.distance < 0.5f) { ok = false; gWhy[0]++; break; }
                    top = Mathf.Max(top, hit.point.y);
                    low = Mathf.Min(low, hit.point.y);
                }
                if (!ok) continue;
                if (top - low > 0.25f) { gWhy[1]++; continue; }
                int cy = Mathf.CeilToInt(top / c - 0.02f);
                if (cy * c - low > 0.3f) { gWhy[2]++; continue; }
                var o = new Vector2Int(cx, cy);
                if (!PrefabHouses.CanBuild(Casino.HouseArea, o, hero, out var why)) { gWhy[3]++; gWhyText = why; continue; }
                // Nothing of the player's there (a house, a wall, a station): the street bare.
                for (int x = cx - 1; x <= cx + w && ok; x++)
                    for (int y = cy; y < cy + PrefabHouses.HouseHeight(Casino.HouseArea) && ok; y++)
                        if (TerraWorld.Get(x, y) != CellKind.Empty) { ok = false; gWhyCell = TerraWorld.Get(x, y).ToString(); }
                var box = new Rect((cx - 1) * c, cy * c, (w + 2) * c, PrefabHouses.HouseHeight(Casino.HouseArea) * c);
                foreach (var s in Stations.Standing(k => true)) if (s.Box.Overlaps(box)) { ok = false; gWhyCell = s.Kind.ToString(); }
                if (!ok) { gWhy[4]++; continue; }
                gSites.Append(cx).Append(' ');
                // Its door a few steps ahead of the hero, to the right.
                float d =Mathf.Abs(cx * c - (h.x + 7f)) + (cx * c < h.x + 3f ? 20f : 0f);
                if (d < best) { best = d; site = o; }
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制G 赌场选址：从 {h:F1} 找，没地面 {gWhy[0]}，不平 {gWhy[1]}，悬空 {gWhy[2]}，不能盖 {gWhy[3]}（{gWhyText}），有东西 {gWhy[4]}（{gWhyCell}），可以盖的格 {gSites}");
            return best < float.MaxValue;
        }
        readonly int[] gWhy = new int[5];
        string gWhyText, gWhyCell;
        readonly System.Text.StringBuilder gSites = new System.Text.StringBuilder();

        /// <summary>Into Dirtmouth with the kit, money for the tables, the casino's site found; the hero set down left of it.</summary>
        IEnumerator GCasinoPrepare(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            yield return BEnter("Town");
            hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            yield return null;
            var inv = mod.Inventory;
            // Out of creative for the casino: a creative save's geo is held at 99,999,999; the player's own money instead.
            Achievements.Achievements.DevSetAll(true);
            mod.ApplyCreative(false);
            inv.Slots[9] = ItemStack.Of(ItemType.CasinoKit, 1);
            GSetPurse(68000);
            Casino.Clear();
            gCasinoBuilt = false;
            // Dirtmouth's street, west of the well.
            var town = TerrainSystem.Find("Town");
            var townOrigin = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            hero.transform.position = Clear(hero, townOrigin + new Vector2(120f, 9f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1f);
            if (!GFindCasinoSite(hero, out gCasinoOrigin))
            {
                // Walk on along the street and look again.
                Player.TerraMovement.DevMove = 1;
                yield return new WaitForSeconds(3f);
                Player.TerraMovement.DevMove = 0;
                yield return new WaitForSeconds(0.5f);
                if (!GFindCasinoSite(hero, out gCasinoOrigin))
                {
                    Plugin.Log.LogInfo("[AutoTest] 录制G 赌场：街上找不到地方");
                    yield break;
                }
            }
            const float c = CellGrid.CellSize;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 赌场：地点 {gCasinoOrigin}（世界 {gCasinoOrigin.x * c:F1}, {gCasinoOrigin.y * c:F1}），英雄在 {(Vector2)hero.transform.position:F1}");
        }

        void GCasinoBuildNow(HeroController hero)
        {
            const float c = CellGrid.CellSize;
            var aim = new Vector2((gCasinoOrigin.x + 13) * c + 0.25f, gCasinoOrigin.y * c + 0.25f);
            gCasinoBuilt = PrefabHouses.Build(Casino.HouseArea, gCasinoOrigin, hero);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 赌场：盖好 {gCasinoBuilt}（瞄准 {aim:F1}）");
        }

        /// <summary>The hero walked (on the casino's floor) to stand left of a machine's middle, facing it.</summary>
        IEnumerator GWalkTo(HeroController hero, float x, float within = 0.25f, float most = 6f)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < most; t += Time.deltaTime)
            {
                BKeep();
                float dx = x - col.bounds.center.x;
                if (Mathf.Abs(dx) < within) break;
                Player.TerraMovement.DevMove = dx > 0 ? 1 : -1;
                SmoothAim((Vector2)col.bounds.center + new Vector2(Mathf.Sign(dx) * 3f, 0.6f), 5f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 走到 {x:F1}：现在 {col.bounds.center.x:F1}");
        }

        /// <summary>The cursor brought over a machine in the world and a right click on it: its screen opens.</summary>
        IEnumerator GOpenMachine(HeroController hero, StationKind kind)
        {
            var box = Stations.DevBoxOf(kind);
            if (!box.HasValue) { Plugin.Log.LogInfo($"[AutoTest] 录制G 赌场：没有 {kind}"); yield break; }
            var at = box.Value.center + new Vector2(0f, 0.3f);
            for (float t = 0f; t < 0.7f; t += Time.deltaTime) { BKeep(); SmoothAim(at, 7f); yield return null; }
            for (float t = 0f; t < 0.25f; t += Time.deltaTime) { BKeep(); SmoothAim(at, 12f); yield return null; }
            bool open = Stations.TryRightClick(box.Value.center);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 赌场：打开 {kind} {open}，现在 {Casino.IsOpen}");
            PointerOn(UI.TerraCursor.DevAt ?? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            mod.DevAim = null;
        }

        IEnumerator GLeaveMachine()
        {
            yield return Glide(new Vector2(72f, 30f), 0.5f);
            yield return new WaitForSeconds(0.15f);
            UI.TerraCursor.DevClick();
            yield return null;
            yield return null;
            if (Casino.IsOpen) Casino.Close();
            PointerOff();
        }

        // ------------------------------------------------------------------ the clips

        /// <summary>The casino kit held on Dirtmouth's street: aimed (its green outline following the cursor), set down, walked into.</summary>
        IEnumerator GCasinoBuildClip(HeroController hero)
        {
            const float c = CellGrid.CellSize;
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            float door = gCasinoOrigin.x * c;
            // Set down a little left of where it will stand.
            hero.transform.position = Clear(hero, new Vector2(door - 4f, gCasinoOrigin.y * c + 0.1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            hero.FaceRight();
            inv.Selected = 0;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 0.5f), 99f);
            yield return new WaitForSeconds(1.2f);
            var aim = new Vector2((gCasinoOrigin.x + 13) * c + 0.25f, gCasinoOrigin.y * c + 0.25f);
            StartRecG("v5G_casino_build");
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { BKeep(); Player.TerraMovement.DevMove = 1; SmoothAim((Vector2)col.bounds.center + new Vector2(3f, 0.5f), 5f); yield return null; }
            Player.TerraMovement.DevMove = 0;
            inv.Selected = 9;
            // The outline brought over the street, held a moment, set down.
            for (float t = 0f; t < 1.1f; t += Time.deltaTime) { BKeep(); SmoothAim(aim + new Vector2(0f, 3f), 4f); yield return null; }
            for (float t = 0f; t < 0.7f; t += Time.deltaTime) { BKeep(); SmoothAim(aim, 6f); yield return null; }
            while (!mod.DevReady) yield return null;
            mod.DevUseSelected(aim);
            yield return null;
            gCasinoBuilt = Stations.DevBoxOf(StationKind.CasinoSlots).HasValue;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 赌场：用套件盖 {gCasinoBuilt}，剩 {inv.Slots[9].Count}");
            if (!gCasinoBuilt) GCasinoBuildNow(hero);
            inv.Selected = 0;
            for (float t = 0f; t < 1.3f; t += Time.deltaTime) { BKeep(); SmoothAim(aim + new Vector2(-2f, 2.5f), 3f); yield return null; }
            // In at its door, to the slot machine.
            var slots = Stations.DevBoxOf(StationKind.CasinoSlots);
            float to = slots.HasValue ? slots.Value.center.x - 1.1f : door + 3f;
            yield return GWalkTo(hero, to, 0.25f, 5f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { BKeep(); SmoothAim((Vector2)col.bounds.center + new Vector2(2.5f, 0.8f), 4f); yield return null; }
            yield return StopRecG();
        }

        /// <summary>
        /// The slot machine: the cursor on it in the world, a right click, the machine fills the screen. The top bet, the lever
        /// pulled down by hand: nothing; again: a win, its lines lit, the payout counting up; all in, pulled: the whole screen in
        /// rainbow. Left by its button.
        /// </summary>
        IEnumerator GCasinoSlotsClip(HeroController hero)
        {
            const float W = 236f;
            var lose = GSlotsWith((pay, lines) => pay == 0);
            var win = GSlotsWith((pay, lines) => lines == 2 && pay > 20000);
            if (win == (0, 1, 2)) win = GSlotsWith((pay, lines) => lines >= 1 && pay > 20000 && pay < 93500);
            var top = GSlotsWith((pay, lines) => pay == 93500);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 老虎机：输 {lose}，赢 {win}（{Casino.SlotPayout(10000, win.Item1, win.Item2, win.Item3)}），全额 {top}");
            GSetPurse(68000);
            StartRecG("v5G_casino_slots");
            yield return new WaitForSeconds(0.4f);
            yield return GOpenMachine(hero, StationKind.CasinoSlots);
            yield return new WaitForSeconds(1.1f);
            // The bet up to the most.
            yield return GPush(W, 38, 225, 20, 12, 0.6f);
            yield return GPush(W, 38, 225, 20, 12, 0.25f);
            yield return GPush(W, 60, 225, 26, 12, 0.3f);
            yield return new WaitForSeconds(0.3f);
            foreach (var (a, b, c, wait) in new[] { (lose.Item1, lose.Item2, lose.Item3, 3.9f), (win.Item1, win.Item2, win.Item3, 4.6f) })
            {
                GStageSlots(a, b, c);
                yield return GPullLever(W);
                yield return new WaitForSeconds(wait);
            }
            // Everything in.
            yield return GPush(W, 88, 225, 28, 12, 0.5f);
            yield return new WaitForSeconds(0.5f);
            GStageSlots(top.Item1, top.Item2, top.Item3);
            long before = Casino.Available;
            yield return GPullLever(W);
            yield return new WaitForSeconds(6.5f);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 老虎机：全额 {before / 100} 吉欧 → 现在 {Casino.Available / 100}，{Casino.Last.LastMessage}");
            yield return GLeaveMachine();
            yield return new WaitForSeconds(1.2f);
            yield return StopRecG();
        }

        /// <summary>The lever's red knob taken by the cursor, pulled to the bottom and let go.</summary>
        IEnumerator GPullLever(float W)
        {
            yield return Glide(GArt(W, 222, 97), 0.45f);
            yield return new WaitForSeconds(0.1f);
            UI.TerraCursor.DevClick();
            UI.TerraCursor.DevHeld = true;
            yield return null;
            yield return null;
            var from = GArt(W, 222, 97);
            var to = GArt(W, 223, 162);
            for (float t = 0f; t < 0.32f; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / 0.32f);
                UI.TerraCursor.DevAt = Vector2.Lerp(from, to, u);
                yield return null;
            }
            UI.TerraCursor.DevAt = to;
            yield return new WaitForSeconds(0.08f);
            UI.TerraCursor.DevHeld = false;
            yield return null;
            // The hand drifts off the lever as it springs back.
            yield return Glide(GArt(W, 196, 150), 0.5f);
        }

        /// <summary>
        /// The wheel: chips laid from the rack onto the table, red chosen, the wheel dragged round and flung: black; then all in
        /// on green, spun: green, sixty-three times, the screen in rainbow and fireworks.
        /// </summary>
        IEnumerator GCasinoWheelClip(HeroController hero)
        {
            const float W = 300f;
            GSetPurse(150000);
            StartRecG("v5G_casino_wheel");
            yield return new WaitForSeconds(0.3f);
            yield return GOpenMachine(hero, StationKind.CasinoRoulette);
            yield return new WaitForSeconds(1.1f);
            // Chips: 10000, 10000, 5000, 1000 (rack rows 4, 4, 3, 2).
            foreach (int k in new[] { 4, 4, 3, 2, 2 })
                yield return GPush(W, 245, 36 + k * 18 - 1, 50, 17, 0.3f);
            // Red.
            yield return GPush(W, 16, 194, 97, 43, 0.45f);
            yield return new WaitForSeconds(0.2f);
            GStageWheel(Casino.Colour.Black);
            yield return GFlingWheel(W);
            yield return new WaitForSeconds(4.8f);
            // All in on green.
            yield return GPush(W, 4, 166, 52, 11, 0.55f);
            yield return GPush(W, 122, 194, 57, 43, 0.45f);
            yield return new WaitForSeconds(0.3f);
            GStageWheel(Casino.Colour.Green);
            long before = Casino.Available;
            yield return GPush(W, 244, 153, 52, 24, 0.5f);
            yield return Glide(GArt(W, 230, 120), 0.6f);
            yield return new WaitForSeconds(7.5f);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 转盘：全额 {before / 100} 吉欧 → 现在 {Casino.Available / 100}，{Casino.Last.LastMessage}");
            yield return GLeaveMachine();
            yield return new WaitForSeconds(1f);
            yield return StopRecG();
        }

        /// <summary>The wheel taken at its rim and dragged a quarter round, let go: it spins.</summary>
        IEnumerator GFlingWheel(float W)
        {
            var c = new Vector2(150f, 92f);
            yield return Glide(GArt(W, c.x + 50f, c.y - 20f), 0.5f);
            yield return new WaitForSeconds(0.1f);
            UI.TerraCursor.DevClick();
            UI.TerraCursor.DevHeld = true;
            yield return null;
            yield return null;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(-22f, 55f, Mathf.SmoothStep(0f, 1f, t / 0.3f)) * Mathf.Deg2Rad;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 54f;
                UI.TerraCursor.DevAt = GArt(W, p.x, p.y);
                yield return null;
            }
            UI.TerraCursor.DevHeld = false;
            yield return null;
            yield return Glide(GArt(W, 236, 118), 0.6f);
        }

        /// <summary>
        /// The pinball: marbles bought and loaded from the tray, the handle drawn and let go (a marble over the hood, down the
        /// funnel onto the windmills, through the pins into a channel); auto-fire on for a stream (the cup gold and rainbow);
        /// left with marbles still falling, the little machine in the world going on.
        /// </summary>
        IEnumerator GCasinoPinballClip(HeroController hero)
        {
            const float W = 410f;
            GSetPurse(200000);
            StartRecG("v5G_casino_pinball");
            yield return new WaitForSeconds(0.3f);
            yield return GOpenMachine(hero, StationKind.CasinoPinball);
            yield return new WaitForSeconds(1.1f);
            // Buy ten of 500 and ten of 5000.
            yield return GPush(W, 34, 34 + 1 * 24 + 11, 26, 10, 0.6f);
            yield return GPush(W, 34, 34 + 3 * 24 + 11, 26, 10, 0.3f);
            yield return GPush(W, 34, 34 + 4 * 24 + 11, 26, 10, 0.3f);
            // Loaded from the tray: a handful.
            foreach (int k in new[] { 1, 1, 3, 1, 4, 3 })
                yield return GPush(W, 318, 34 + k * 30, 86, 28, 0.28f);
            // Two by hand: the handle held, let go.
            foreach (float hold in new[] { 0.75f, 0.5f })
            {
                yield return GPush(W, 272, 236, 30, 26, 0.45f, true, hold);
                yield return new WaitForSeconds(2.2f);
            }
            // More marbles in, auto-fire on.
            foreach (int k in new[] { 1, 1, 3, 1, 4, 1, 3, 1, 1, 4, 3, 1 })
                yield return GPush(W, 318, 34 + k * 30, 86, 28, 0.2f);
            yield return GPush(W, 6, 212, 86, 14, 0.6f);
            yield return Glide(GArt(W, 250, 140), 0.8f);
            yield return new WaitForSeconds(9f);
            yield return GPush(W, 6, 212, 86, 14, 0.6f);
            yield return new WaitForSeconds(0.5f);
            yield return GLeaveMachine();
            yield return new WaitForSeconds(3.5f);
            yield return StopRecG();
        }

        /// <summary>A press held on a part of the art for a while, then let go (the pinball's handle).</summary>
        IEnumerator GPush(float width, float x, float y, float w, float h, float glide, bool held, float hold)
        {
            yield return Glide(GArt(width, x + w / 2f, y + h / 2f), glide);
            yield return new WaitForSeconds(0.1f);
            UI.TerraCursor.DevClick();
            UI.TerraCursor.DevHeld = true;
            var at = UI.TerraCursor.DevAt.Value;
            for (float t = 0f; t < hold; t += Time.deltaTime)
            {
                // The hand drawing the handle down a little.
                UI.TerraCursor.DevAt = at + new Vector2(0f, Mathf.Min(1f, t / hold) * 30f);
                yield return null;
            }
            UI.TerraCursor.DevHeld = false;
            yield return null;
            yield return Glide(at + new Vector2(-40f, -10f), 0.3f);
        }

        // ------------------------------------------------------------------ the suites

        IEnumerator P4_v5G_casino(HeroController hero)
        {
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            yield return GCasinoPrepare(hero);
            hero = HeroController.instance;
            if (gCasinoOrigin == default) yield break;
            yield return GCasinoBuildClip(hero);
            if (!Stations.DevBoxOf(StationKind.CasinoSlots).HasValue) yield break;
            yield return new WaitForSeconds(0.5f);
            yield return GCasinoSlotsClip(hero);
            var wheel = Stations.DevBoxOf(StationKind.CasinoRoulette).Value;
            yield return GWalkTo(hero, wheel.center.x - 1.1f, 0.25f, 5f);
            yield return new WaitForSeconds(0.5f);
            yield return GCasinoWheelClip(hero);
            var pin = Stations.DevBoxOf(StationKind.CasinoPinball).Value;
            yield return GWalkTo(hero, pin.center.x - 1.1f, 0.25f, 5f);
            yield return new WaitForSeconds(0.5f);
            yield return GCasinoPinballClip(hero);
            Plugin.Log.LogInfo("[AutoTest] 录制G 赌场 完成");
        }

        /// <summary>Where on Dirtmouth's street the casino could stand (logged), from several places along it.</summary>
        IEnumerator P4_v5G_sitescan(HeroController hero)
        {
            yield return P4Kit();
            yield return BEnter("Town");
            hero = HeroController.instance;
            var town = TerrainSystem.Find("Town");
            var townOrigin = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            foreach (float x in new[] { 40f, 90f, 140f, 190f, 240f, 290f })
            {
                hero.transform.position = Clear(hero, townOrigin + new Vector2(x, 9f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(1f);
                for (int i = 0; i < gWhy.Length; i++) gWhy[i] = 0;
                GFindCasinoSite(hero, out var site);
                Plugin.Log.LogInfo($"[AutoTest] 录制G 选址 {x}: 选 {site}");
            }
            foreach (var s in Stations.Standing(k => true)) Plugin.Log.LogInfo($"[AutoTest] 录制G 家具 {s.Kind} {s.Box}");
        }

        /// <summary>The recorder checked: the casino built off camera, three seconds of the slot's screen.</summary>
        IEnumerator P4_v5G_rectest(HeroController hero)
        {
            yield return GCasinoPrepare(hero);
            hero = HeroController.instance;
            if (gCasinoOrigin == default) yield break;
            GCasinoBuildNow(hero);
            var slots = Stations.DevBoxOf(StationKind.CasinoSlots);
            if (!slots.HasValue) yield break;
            hero.transform.position = Clear(hero, new Vector2(slots.Value.center.x - 1.1f, slots.Value.y + 0.1f));
            yield return new WaitForSeconds(1f);
            yield return GCasinoSlotsClip(hero);
        }
    }
}
