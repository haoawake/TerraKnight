using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "stag"): three things players ran into. The camera stays on the player whatever he
    /// does; the enemy blockers at gates joined to the next scene are off, and a body on the enemies'
    /// layer pushed at such a doorway goes through it (and doesn't with the blocker back on); and a
    /// stag station used the way a player uses it, with key presses fed to the game's keyboard.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int EnemiesLayer = 11;

        IEnumerator StagTest(HeroController hero)
        {
            var gm = GameManager.instance;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            yield return CameraCentred("山道");

            int tested = 0;
            foreach (var scene in new[] { "Town", "Crossroads_01" })
            {
                if (gm.sceneName != scene) yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                float wait = Time.time + 12f;
                while (LiveWorld.Instance != null && LiveWorld.Instance.IsLoading && Time.time < wait) yield return null;
                yield return new WaitForSeconds(1f);
                Plugin.Log.LogInfo($"[AutoTest] 挡怪 {scene}：已载入 {string.Join("、", LiveWorld.Instance.LoadedNames)}；门边的挡怪墙{EnemyBlockers.DevGuards(null)}；停住的敌人 {EnemyBlockers.DevHeld}");
                if (scene == "Town") yield return CameraCentred("德特茅斯");
                foreach (var (blocker, gate) in EnemyBlockers.DevOpen(null))
                {
                    if (tested >= 1 || (!gate.name.StartsWith("left") && !gate.name.StartsWith("right"))) continue;
                    yield return DoorwayBody(gate.gameObject.scene.name, blocker, gate);
                    tested++;
                }
            }
            if (tested == 0) Plugin.Log.LogInfo("[AutoTest] 挡怪 没找到可以试的门（左右门、挡怪墙已关）");

            yield return StagRealRide();
            KeysDown();
            yield return FalseKnightGates();
            mod.DevNoFallDamage = false;
        }

        /// <summary>Walks and jumps both ways for a few seconds; how far the camera ever was from the player's centre (after LateUpdate).</summary>
        IEnumerator CameraCentred(string label)
        {
            float worst = 0f;
            Vector2 worstAt = default;
            int frames = 0;
            var eof = new WaitForEndOfFrame();
            for (int leg = 0; leg < 4; leg++)
            {
                Player.TerraMovement.DevMove = leg % 2 == 0 ? 1 : -1;
                float end = Time.time + 1.2f;
                while (Time.time < end)
                {
                    Player.TerraMovement.DevJump = Time.time % 0.8f < 0.4f;
                    yield return eof;
                    var off = Player.TerraCamera.DevOffset;
                    frames++;
                    if (off.magnitude > worst) { worst = off.magnitude; worstAt = off; }
                }
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            Plugin.Log.LogInfo($"[AutoTest] 镜头 {label}：{frames} 帧里镜头离主角中心最远 {worst:F3}（{worstAt:F3}）{(worst < 0.05f ? " 通过" : " 不通过")}");
        }

        /// <summary>
        /// A body on the enemies' layer, set down three units inside a gate joined to the next scene
        /// and pushed at it for two and a half seconds: through with the gate's blocker off (as now),
        /// stopped with it on. Where it stops, what is in front of it.
        /// </summary>
        IEnumerator DoorwayBody(string scene, Collider2D blocker, TransitionPoint gate)
        {
            var gb = gate.GetComponent<Collider2D>().bounds;
            int dir = gate.name.StartsWith("left") ? -1 : 1;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var down = Physics2D.Raycast(new Vector2(gb.center.x - dir * 3f, gb.center.y + 1f), Vector2.down, 10f, terrain);
            if (down.collider == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 挡怪 {scene} 门 {gate.name}：门里面 3 格没有地面，跳过");
                yield break;
            }
            Plugin.Log.LogInfo($"[AutoTest] 挡怪 {scene} 门 {gate.name}（{gb.center:F1}）的挡怪墙 {blocker.name} 范围 {EnemyBlockers.BoundsOf(blocker).min:F1}..{EnemyBlockers.BoundsOf(blocker).max:F1}，" +
                               $"敌人层和挡怪层{(Physics2D.GetIgnoreLayerCollision(EnemiesLayer, EnemyBlockers.Layer) ? "不碰撞" : "会碰撞")}，地面 y {down.point.y:F2}");
            var go = new GameObject("TK test enemy body") { layer = EnemiesLayer };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 0.8f);
            var start = new Vector2(gb.center.x - dir * 3f, down.point.y + 0.45f);
            for (int pass = 0; pass < 2; pass++)
            {
                bool blockerOn = pass == 1;
                blocker.enabled = blockerOn;
                rb.position = start;
                go.transform.position = start;
                rb.linearVelocity = Vector2.zero;
                float end = Time.time + 2.5f;
                var fixedStep = new WaitForFixedUpdate();
                while (Time.time < end)
                {
                    rb.linearVelocity = new Vector2(dir * 8f, rb.linearVelocity.y);
                    yield return fixedStep;
                }
                float past = (rb.position.x - gb.center.x) * dir;
                var cell = CellGrid.WorldToCell(rb.position);
                var region = TerraWorld.RegionAt(cell.x, cell.y);
                var ahead = new StringBuilder();
                foreach (var c in Physics2D.OverlapBoxAll(rb.position + new Vector2(dir * 0.5f, 0.05f), new Vector2(0.4f, 0.6f), 0f))
                    if (c != box && !c.isTrigger && !Physics2D.GetIgnoreLayerCollision(EnemiesLayer, c.gameObject.layer))
                        ahead.Append($" {c.gameObject.scene.name}/{PathOf(c.transform)}[{c.GetType().Name} 层{c.gameObject.layer} 启用 {c.enabled}]");
                Plugin.Log.LogInfo($"[AutoTest] 挡怪 {scene} 门 {gate.name} 挡怪墙{(blockerOn ? "开着" : "关着")}：身体停在 {rb.position:F1}，过了门线 {past:F1}，" +
                                   $"所在场景 {(region != null ? region.SceneName : "无")}{(blockerOn ? (past < 0.5f ? " 被挡住（对）" : " 没挡住") : (past > 1.5f ? " 过去了（通过）" : " 没过去（不通过）"))}；前面挡着的：{(ahead.Length == 0 ? " 无" : ahead.ToString())}");
            }
            Object.Destroy(go);
            EnemyBlockers.Refresh();
            Plugin.Log.LogInfo($"[AutoTest] 挡怪 {scene} 门 {gate.name} 试完，挡怪墙放回原样：{(blocker.enabled ? "开" : "关")}");
        }

        // --- Keys pressed for the game, as the keyboard would. ---

        /// <summary>The game's keyboard now has exactly these keys down (none: all up).</summary>
        static void KeysDown(params Key[] keys)
        {
            var kb = Keyboard.current;
            if (kb != null) InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        }

        /// <summary>The keys taps hold down now: taps running at once (a jump while a key is pressed) keep each other's keys.</summary>
        static readonly System.Collections.Generic.List<Key> tapHeld = new System.Collections.Generic.List<Key>();

        /// <summary>A key pressed for a moment and let go (any other tap's key still held stays held).</summary>
        static IEnumerator Tap(Key key, float hold = 0.15f)
        {
            tapHeld.Add(key);
            KeysDown(tapHeld.ToArray());
            yield return new WaitForSecondsRealtime(hold);
            tapHeld.Remove(key);
            KeysDown(tapHeld.ToArray());
            yield return new WaitForSecondsRealtime(0.2f);
        }

        /// <summary>
        /// A ride the way a player takes it: into the Forgotten Crossroads station over the gate
        /// line from Crossroads_03 (so it is a moved scene), up at the bell to inspect it and Space
        /// on "yes" to pay, swing at the bell, up at the stag when it has come, S once in its menu
        /// (Greenpath, one down from here) and Space; then where the player comes out.
        /// </summary>
        IEnumerator StagRealRide()
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            const string station = "Crossroads_47", next = "Crossroads_03", to = "Fungus1_16_alt";
            // The arrival door's state machine, entered the game's way first, then over the gate line.
            yield return GoTo(station, EntryGateOf(station));
            yield return new WaitForSeconds(1f);
            DumpDoor(station, "原版切换进站");
            yield return GoTo(next, EntryGateOf(next));
            float wait = Time.time + 20f;
            while (!LiveWorld.Instance.IsNeighbour(station) && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1f);
            PlayMakerFSM bellFsm = null, stag = null;
            Collider2D travelRange = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != station) continue;
                if (f.FsmName == "Stag Bell") bellFsm = f;
                if (f.FsmName == "Stag Control") stag = f;
            }
            if (bellFsm == null || stag == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 {station} 没接上（隔壁：{LiveWorld.Instance.IsNeighbour(station)}），铃 {bellFsm != null}，鹿角虫 {stag != null}，不通过");
                yield break;
            }
            pd.geo = 300;
            pd.metStag = true;
            pd.openedGreenpath = true;
            var hero = HeroController.instance;
            var bellAt = bellFsm.transform.position;
            Put(hero, new Vector2(bellAt.x - 1.2f, bellAt.y + 1.5f));
            wait = Time.time + 6f;
            while (LiveWorld.Instance.CurrentName != station && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1.5f);
            DumpDoor(station, "无缝走进站");
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 走进站后复活点 {pd.respawnScene}/{pd.respawnMarkerName}（类型 {pd.respawnType}）");
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 走进 {LiveWorld.Instance.CurrentName}（场景偏移 {SceneFrames.Of(bellFsm.gameObject):F1}），铃 {bellAt:F1}，主角 {(Vector2)hero.transform.position:F1}，铃的状态 {bellFsm.ActiveStateName}，鹿角虫 {stag.ActiveStateName}");

            // The bell: up to inspect it, Space on "yes".
            var trail = new List<string>();
            yield return Until(bellFsm, s => s == "In Range", 5f, trail);
            yield return Tap(Key.W);
            bool upWorks = bellFsm.ActiveStateName != "In Range";
            yield return Until(bellFsm, s => s == "Send Text", 12f, trail);
            yield return new WaitForSeconds(1f);
            Shot(station, "stag3_toll");
            int geo = pd.geo;
            yield return Tap(Key.Space);
            yield return Until(bellFsm, s => s == "Opened", 20f, trail);
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 付钱（按 W {(upWorks ? "有反应" : "没反应")}，再按空格选“是”）：铃 {string.Join(" > ", trail)}；吉欧 {geo} → {pd.geo}，openedCrossroads {pd.openedCrossroads}，鹿角虫 {stag.ActiveStateName}");
            Shot(station, "stag3_paid");

            // Ring it: the weapons of a new game, one by one, as the player would swing them.
            var bellObj = bellFsm.transform.Find("Bell");
            Vector2 ring = bellObj != null ? (Vector2)bellObj.position : (Vector2)bellAt;
            bool rang = false;
            var tried = new StringBuilder();
            for (int slot = 0; slot < 3 && !rang; slot++)
            {
                mod.Inventory.Selected = slot;
                Put(hero, new Vector2(ring.x - 1.2f, hero.transform.position.y + 0.3f));
                yield return new WaitForSeconds(0.5f);
                float end = Time.time + 1.5f;
                while (Time.time < end && stag.ActiveStateName == "Set Can Talk")
                {
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(ring);
                    yield return null;
                }
                rang = stag.ActiveStateName != "Set Can Talk";
                tried.Append($" {mod.Inventory.Current.Def?.Name}={(rang ? "响了" : "没响")}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 敲铃（{ring:F1}）：{tried}");
            mod.Inventory.Selected = 0;
            if (!rang) stag.SendEvent("STAG BELL RUNG");
            trail.Clear();
            yield return Until(stag, s => s == "Idle" || s == "In Range", 30f, trail);
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 鹿角虫到站：{string.Join(" > ", trail)}；stagPosition {pd.stagPosition}，鹿角虫在 {stag.transform.position:F1}");

            // Up at the stag.
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (c.name == "Travel Range" && c.gameObject.scene.name == station) travelRange = c;
            var spot = travelRange != null ? new Vector2(travelRange.bounds.center.x, travelRange.bounds.min.y + 1.5f) : (Vector2)stag.transform.position + new Vector2(3f, 1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 对话范围 {(travelRange != null ? travelRange.bounds.min.ToString("F1") + ".." + travelRange.bounds.max.ToString("F1") : "没找到")}，站到 {spot:F1}");
            Put(hero, spot);
            trail.Clear();
            yield return Until(stag, s => s == "In Range", 6f, trail);
            yield return new WaitForSeconds(0.5f);
            yield return Tap(Key.W);
            float talkEnd = Time.time + 20f;
            string last = null;
            while (Time.time < talkEnd)
            {
                string s = stag.ActiveStateName;
                if (s != last) { trail.Add(s); last = s; }
                if (s == "Open map" || s == "Check Result") break;
                // A line of talk on the way: Space moves it on.
                if (s.Contains("Convo") || s.Contains("Remember") || s.Contains("Hope") || s == "Old" || s == "Last" || s.Contains("Box")) yield return Tap(Key.Space);
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 对话：{string.Join(" > ", trail)}；主角 {(Vector2)hero.transform.position:F1}");
            yield return new WaitForSeconds(1.5f);

            // The menu: S once, then Space.
            PlayMakerFSM list = null;
            var items = new List<PlayMakerFSM>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!PathOf(f.transform).Contains("Stag Map(Clone)/UI List Stag")) continue;
                if (f.FsmName == "ui_list" && f.name == "UI List Stag") list = f;
                if (f.FsmName == "ui_list_item") items.Add(f);
            }
            if (list == null || stag.ActiveStateName != "Open map")
            {
                Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 地图没打开（鹿角虫 {stag.ActiveStateName}，列表 {list != null}），不通过");
                Shot(station, "stag3_nomenu");
                yield break;
            }
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 地图打开：{MenuState(list, items)}");
            Shot(station, "stag3_menu");
            yield return Tap(Key.S);
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 按一下 S：{MenuState(list, items)}");
            Shot(station, "stag3_menu_down");
            yield return new WaitForSeconds(0.3f);
            yield return Tap(Key.Space);

            // The ride.
            float t0 = Time.time, nextLog = 0f;
            var seen = new List<string>();
            while (Time.time - t0 < 60f)
            {
                var h = HeroController.SilentInstance;
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 0.5f;
                    seen.Add($"{Time.time - t0:F1}s {gm.sceneName} {(h != null ? ((Vector2)h.transform.position).ToString("F1") : "-")} next={pd.nextScene}{(pd.travelling ? " 途中" : "")}{(GameHooks.PlayerHasControl() ? " 可操作" : "")}{(stag != null ? " 鹿角虫 " + stag.ActiveStateName : "")}");
                }
                if (gm.sceneName != station && gm.sceneName != "Cinematic_Stag_travel" && GameHooks.PlayerHasControl() && !pd.travelling) break;
                yield return null;
            }
            foreach (var chunk in Chunks(seen, 8)) Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 一路：{chunk}");
            yield return new WaitForSeconds(2f);
            hero = HeroController.instance;
            Vector2? exit = null, stagThere = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.name == "door_stagExit" && tp.gameObject.scene.name == gm.sceneName) exit = tp.transform.position;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Stag Control" && f.gameObject.scene.name == gm.sceneName) stagThere = f.transform.position;
            float dist = exit.HasValue ? Vector2.Distance(exit.Value, hero.transform.position) : -1f;
            Plugin.Log.LogInfo($"[AutoTest] 鹿角站2 到了 {gm.sceneName}（要去 {to}）：主角 {(Vector2)hero.transform.position:F1}，door_stagExit {(exit.HasValue ? exit.Value.ToString("F1") : "没找到")}，鹿角虫 {(stagThere.HasValue ? stagThere.Value.ToString("F1") : "没找到")}，" +
                               $"相距 {dist:F1}，无缝当前 {LiveWorld.Instance.CurrentName}{(gm.sceneName == to && dist >= 0f && dist < 15f ? " 通过" : " 不通过")}");
            Shot(gm.sceneName, "stag3_arrived");
            yield return CameraCentred("鹿角站到站");
        }

        /// <summary>The station's arrival door: its gate, and its Door Control state machine with its state and variables.</summary>
        static void DumpDoor(string scene, string how)
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != scene || f.name != "door_stagExit") continue;
                var vars = new StringBuilder();
                foreach (var v in f.FsmVariables.StringVariables) vars.Append($" {v.Name}='{v.Value}'");
                foreach (var v in f.FsmVariables.BoolVariables) vars.Append($" {v.Name}={v.Value}");
                var tp = f.GetComponent<TransitionPoint>();
                var col = f.GetComponent<Collider2D>();
                Plugin.Log.LogInfo($"[AutoTest] 鹿角站门 {how}：'{f.FsmName}' 启用 {f.enabled}/{f.gameObject.activeInHierarchy} 当前状态 {f.ActiveStateName}，变量:{vars}；门 {(tp != null ? tp.targetScene + "/" + tp.entryPoint + " 启用 " + tp.enabled : "无")}，触发区 {(col != null ? col.bounds.min.ToString("F1") + ".." + col.bounds.max.ToString("F1") + " 启用 " + col.enabled : "无")}");
                if (f.FsmName == "Door Control") DumpFsm(f);
            }
        }

        /// <summary>
        /// The False Knight's arena, entered by its left door as walking in does: frame by frame,
        /// where the player is while the game walks him in, and when each battle gate and the battle
        /// itself change state (and where the player is then). Then a death there, and where the
        /// player comes back.
        /// </summary>
        IEnumerator FalseKnightGates()
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            const string arena = "Crossroads_10";
            PlayerData.instance.isInvincible = true;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 出发前复活点 {pd.respawnScene}/{pd.respawnMarkerName}（类型 {pd.respawnType}）");
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = arena, EntryGateName = "left1", HeroLeaveDirection = GlobalEnums.GatePosition.right,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            float wait = Time.time + 30f;
            while (gm.sceneName != arena && Time.time < wait) yield return null;
            // The boss scene comes in additively a moment later.
            wait = Time.time + 8f;
            while (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(arena + "_boss").isLoaded && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1.5f);
            // The room from above: shots over its whole span.
            foreach (var at in new[] { new Vector2(12f, 8f), new Vector2(38f, 8f), new Vector2(64f, 8f), new Vector2(12f, 24f), new Vector2(38f, 24f), new Vector2(64f, 24f), new Vector2(25f, 40f), new Vector2(50f, 40f) })
            {
                Player.TerraCamera.DevLook = at;
                yield return new WaitForSeconds(0.25f);
                Shot(arena, $"fk_room_{at.x:F0}_{at.y:F0}");
                yield return null;
            }
            Player.TerraCamera.DevLook = null;
            // The room: its gates, and every trigger and state machine of its battle and its boss scene.
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.gameObject.scene.name == arena)
                {
                    var c = tp.GetComponent<Collider2D>();
                    Plugin.Log.LogInfo($"[AutoTest] 假骑士 门 {tp.name} → {tp.targetScene}/{tp.entryPoint}，位置 {(Vector2)tp.transform.position:F1}{(c != null ? $" 范围 {c.bounds.min:F1}..{c.bounds.max:F1}" : "")}");
                }
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if (sn != arena && sn != arena + "_boss") continue;
                string low = (PathOf(f.transform) + " " + f.FsmName).ToLowerInvariant();
                if (sn == arena && !low.Contains("battle") && !low.Contains("false knight") && !low.Contains("fk")) continue;
                if (low.Contains("ghost false knight") || low.Contains("piece")) continue;
                var trig = new StringBuilder();
                foreach (var c in f.GetComponents<Collider2D>())
                    trig.Append($" [{(c.isTrigger ? "触发" : "实心")} {c.bounds.min:F1}..{c.bounds.max:F1} 启用 {c.enabled}]");
                Plugin.Log.LogInfo($"[AutoTest] 假骑士 {sn}/{PathOf(f.transform)} '{f.FsmName}' 启用 {f.enabled}/{f.gameObject.activeInHierarchy} 状态 {f.ActiveStateName} 位置 {(Vector2)f.transform.position:F1}{trig}");
                if (f.name == "Battle Scene" || (sn == arena + "_boss" && f.FsmName.Contains("Battle")) || f.name == "Escape Checker" || f.name == "FK Saver" || (f.name == "Fk Break Wall" && f.FsmName == "Control") || f.name == "Battle Gate Prayer") DumpFsm(f);
            }
            var states = new Dictionary<PlayMakerFSM, string>();
            var events = new List<string>();
            var path = new List<string>();
            float t0 = Time.time, nextPath = 0f, shotAt = -1f;
            bool listed = false;
            while (Time.time - t0 < 9f)
            {
                var hero = HeroController.SilentInstance;
                float hx = hero != null ? hero.GetComponent<BoxCollider2D>().bounds.center.x : 0f;
                if (!listed)
                {
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if ((f.gameObject.scene.name == arena || f.gameObject.scene.name == arena + "_boss") && (f.name.Contains("Battle Gate") || f.name == "Battle Scene" || f.FsmName == "BG Control"))
                            states[f] = null;
                    if (states.Count > 0)
                    {
                        listed = true;
                        var sb = new StringBuilder();
                        foreach (var f in states.Keys)
                        {
                            var c = f.GetComponent<Collider2D>();
                            sb.Append($" {f.gameObject.scene.name}/{f.name} '{f.FsmName}' x {f.transform.position.x:F1}{(c != null ? $" 碰撞 {c.bounds.min.x:F1}..{c.bounds.max.x:F1} 实心 {!c.isTrigger}" : "")}");
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 假骑士 门和战斗：{sb}");
                    }
                }
                foreach (var f in new List<PlayMakerFSM>(states.Keys))
                {
                    if (f == null) continue;
                    string s = f.ActiveStateName;
                    if (s == states[f]) continue;
                    states[f] = s;
                    var c = f.GetComponent<Collider2D>();
                    events.Add($"{Time.time - t0:F2}s {f.name}/{f.FsmName}→{s}（主角中心 x {hx:F2}{(c != null ? $"，门碰撞开 {c.enabled && c.gameObject.activeInHierarchy}" : "")}）");
                    if (f.name.Contains("Battle Gate") && (s.Contains("Close") || s.Contains("Shut") || s.Contains("Down")) && shotAt < 0f) shotAt = Time.time + 0.4f;
                }
                if (shotAt > 0f && Time.time >= shotAt)
                {
                    Shot(arena, "fk_gate_closed");
                    shotAt = 0f;
                }
                if (Time.time >= nextPath)
                {
                    nextPath = Time.time + 0.1f;
                    if (hero != null)
                        path.Add($"{Time.time - t0:F1}s x {hx:F2} vx {hero.GetComponent<Rigidbody2D>().linearVelocity.x:F1}{(hero.cState.transitioning ? " 进场中" : "")}{(GameHooks.PlayerHasControl() ? " 可操作" : "")}");
                }
                yield return null;
            }
            foreach (var chunk in Chunks(events, 6)) Plugin.Log.LogInfo($"[AutoTest] 假骑士 状态：{chunk}");
            foreach (var chunk in Chunks(path, 15)) Plugin.Log.LogInfo($"[AutoTest] 假骑士 主角：{chunk}");
            foreach (var f in states.Keys)
                if (f != null && (f.name == "Battle Scene" || f.name == "Battle Gate 1" || f.name == "Battle Gate 2" || f.name == "Battle Gate Prayer"))
                {
                    var trig = new StringBuilder();
                    foreach (var c in f.GetComponentsInChildren<Collider2D>(true))
                        trig.Append($" {c.name}[{(c.isTrigger ? "触发" : "实心")} {c.bounds.min:F1}..{c.bounds.max:F1} 启用 {c.enabled && c.gameObject.activeInHierarchy}]");
                    Plugin.Log.LogInfo($"[AutoTest] 假骑士 {f.name} 的碰撞:{trig}");
                    DumpFsm(f);
                }

            // The way in a player takes: from the ledge left of the arena, up onto its floor, then right.
            PlayMakerFSM battle = null;
            Collider2D trigger = null;
            foreach (var f in states.Keys)
                if (f != null && f.name == "Battle Scene") { battle = f; trigger = f.GetComponent<Collider2D>(); }
            if (trigger != null)
            {
                var hits = new StringBuilder();
                for (int layer = 0; layer < 32; layer++)
                    if (!Physics2D.GetIgnoreLayerCollision(trigger.gameObject.layer, layer)) hits.Append(' ').Append(layer);
                string tag = "";
                foreach (var st in battle.FsmStates)
                    foreach (var a in st.Actions)
                        if (a is HutongGames.PlayMaker.Actions.Trigger2dEvent te) tag = te.collideTag?.Value;
                Plugin.Log.LogInfo($"[AutoTest] 假骑士 战斗触发区在层 {trigger.gameObject.layer}，和这些层碰撞:{hits}；只认标签 '{tag}'");
            }
            var hb = HeroController.instance.GetComponent<BoxCollider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 主角碰撞盒 {hb.size:F2}（偏移 {hb.offset:F2}），主角层 {hb.gameObject.layer}，标签 {hb.tag}");
            mod.DevNoFallDamage = true;
            // The arena floor over the shaft below it, where the trigger is: its underside and top.
            const int terrainMask = 1 << ChunkColliders.TerrainLayer;
            foreach (float x in new[] { 13.6f, 14f, 14.4f, 16f })
            {
                var up = Physics2D.Raycast(new Vector2(x, 15f), Vector2.up, 20f, terrainMask);
                var down = Physics2D.Raycast(new Vector2(x, 35f), Vector2.down, 20f, terrainMask);
                Plugin.Log.LogInfo($"[AutoTest] 假骑士 x {x:F1}：竖井顶（石板底面）y {(up.collider != null ? up.point.y.ToString("F2") : "无")}，竞技场地面 y {(down.collider != null ? down.point.y.ToString("F2") : "无")}；触发区下沿 {(trigger != null ? trigger.bounds.min.y.ToString("F2") : "?")}");
            }
            // Flying up the shaft under the floor (wings, rockets, a cloud jump), head first into the floor.
            Put(HeroController.instance, new Vector2(14f, 20f));
            float flyEnd = Time.time + 1.5f;
            var fixedStep = new WaitForFixedUpdate();
            string flyState = battle != null ? battle.ActiveStateName : "?";
            float topReached = 0f;
            while (Time.time < flyEnd)
            {
                var hrb = HeroController.instance.GetComponent<Rigidbody2D>();
                hrb.linearVelocity = new Vector2(0f, 10f);
                topReached = Mathf.Max(topReached, HeroController.instance.GetComponent<BoxCollider2D>().bounds.max.y);
                if (battle != null && battle.ActiveStateName != flyState) break;
                yield return fixedStep;
            }
            var flyAt = (Vector2)HeroController.instance.GetComponent<BoxCollider2D>().bounds.center;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 从竖井往上飞：头顶最高 {topReached:F2}，主角 {flyAt:F1}，战斗 {flyState} → {(battle != null ? battle.ActiveStateName : "?")}{(battle != null && battle.ActiveStateName != "Detect" ? "（在竞技场外面就开打了，不通过）" : "（没开打，通过）")}");
            Shot(arena, "fk_fly_up");
            yield return new WaitForSeconds(0.5f);
            Put(HeroController.instance, new Vector2(5f, 22f));
            yield return new WaitForSeconds(1f);
            events.Clear();
            path.Clear();
            t0 = Time.time;
            nextPath = 0f;
            bool started = false;
            Player.TerraMovement.DevMove = 1;
            while (Time.time - t0 < 10f)
            {
                var hero = HeroController.SilentInstance;
                var b = hero.GetComponent<BoxCollider2D>().bounds;
                // Jump while short of the arena floor's height.
                Player.TerraMovement.DevJump = b.min.y < 25.5f && Time.time % 0.9f < 0.5f;
                foreach (var f in new List<PlayMakerFSM>(states.Keys))
                {
                    if (f == null) continue;
                    string s = f.ActiveStateName;
                    if (s == states[f]) continue;
                    states[f] = s;
                    events.Add($"{Time.time - t0:F2}s {f.name}/{f.FsmName}→{s}（主角 {b.min.x:F2}..{b.max.x:F2}, {b.min.y:F1}）");
                    if (f == battle && s == "Start" && !started && trigger != null)
                    {
                        started = true;
                        var inside = new StringBuilder();
                        foreach (var c in Physics2D.OverlapBoxAll(trigger.bounds.center, trigger.bounds.size, 0f))
                            if (c != trigger) inside.Append($" {c.gameObject.scene.name}/{PathOf(c.transform)}[层{c.gameObject.layer} 标签 {c.tag}{(c.isTrigger ? " 触发" : "")}]");
                        Plugin.Log.LogInfo($"[AutoTest] 假骑士 战斗开始时压在触发区上的:{inside}");
                        if (shotAt <= 0f) shotAt = Time.time + 0.2f;
                    }
                }
                if (shotAt > 0f && Time.time >= shotAt)
                {
                    Shot(arena, "fk_walk_gate");
                    shotAt = -2f;
                }
                if (Time.time >= nextPath)
                {
                    nextPath = Time.time + 0.1f;
                    path.Add($"{Time.time - t0:F1}s {b.min.x:F2}..{b.max.x:F2},{b.min.y:F1} vx {hero.GetComponent<Rigidbody2D>().linearVelocity.x:F1}");
                }
                if (started && Time.time - t0 > 3f && b.center.x > 16f) break;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            foreach (var chunk in Chunks(events, 6)) Plugin.Log.LogInfo($"[AutoTest] 假骑士 走进去 状态：{chunk}");
            foreach (var chunk in Chunks(path, 15)) Plugin.Log.LogInfo($"[AutoTest] 假骑士 走进去 主角：{chunk}");
            var hNow = HeroController.instance.GetComponent<BoxCollider2D>().bounds.center;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 战斗{(started ? "开始了" : "没开始")}，主角在 {(Vector2)hNow:F1}{(started && hNow.x > 11.2f ? "（在门里面，通过）" : started ? "（在门外面，不通过）" : "")}");

            // A death in the arena.
            var h = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 死前复活点 {pd.respawnScene}/{pd.respawnMarkerName}（类型 {pd.respawnType}）");
            pd.isInvincible = false;
            Player.PlayerHooks.HurtPlayer(h, 9999, 1, null);
            wait = Time.time + 6f;
            while (!h.cState.dead && Time.time < wait) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 死了：{h.cState.dead}");
            wait = Time.time + 40f;
            while ((h.cState.dead || gm.sceneName == arena || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            yield return new WaitForSeconds(2f);
            pd.isInvincible = true;
            h = HeroController.instance;
            var near = new StringBuilder();
            foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint"))
                if (go.scene.name == gm.sceneName) near.Append($" {go.name}@{(Vector2)go.transform.position:F1}");
            Plugin.Log.LogInfo($"[AutoTest] 假骑士 复活在 {gm.sceneName}：主角 {(Vector2)h.transform.position:F1}，复活点 {pd.respawnScene}/{pd.respawnMarkerName}，这里的复活点:{near}，" +
                               $"这里的晶塔 {World.Pylons.Unlocked.FindAll(p => p.Scene == gm.sceneName).Count} 座{(gm.sceneName == pd.respawnScene ? " 通过" : " 不通过")}");
            Shot(gm.sceneName, "fk_respawn");
        }

        /// <summary>
        /// Dev (suite "bell"): the Forgotten Crossroads station's bell, opened, struck with each of a
        /// new game's first three items in turn: whether it rings, and where the stab's stand-in
        /// slash was against the bell's own box.
        /// </summary>
        IEnumerator BellTest()
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            pd.openedCrossroads = true;
            yield return GoTo("Crossroads_47", EntryGateOf("Crossroads_47"));
            yield return new WaitForSeconds(1f);
            PlayMakerFSM bell = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Bell Control" && f.gameObject.scene.name == "Crossroads_47") bell = f;
            if (bell == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 敲铃 找不到铃（Bell Control），不通过");
                yield break;
            }
            var bc = bell.GetComponent<Collider2D>();
            Plugin.Log.LogInfo($"[AutoTest] 敲铃 铃 {(Vector2)bell.transform.position:F2}，碰撞 {(bc != null ? $"{bc.bounds.min:F2}..{bc.bounds.max:F2} 触发 {bc.isTrigger} 层 {bc.gameObject.layer}，和攻击层(17){(Physics2D.GetIgnoreLayerCollision(17, bc.gameObject.layer) ? "不碰撞" : "碰撞")}" : "无")}，状态 {bell.ActiveStateName}");
            var hero = HeroController.instance;
            Vector2 ring = bc != null ? (Vector2)bc.bounds.center : (Vector2)bell.transform.position;
            for (int slot = 0; slot < 3; slot++)
            {
                mod.Inventory.Selected = slot;
                // Close enough for a short blade; the bow from further off.
                Put(hero, new Vector2(ring.x - (slot == 2 ? 3f : 0.7f), ring.y + 0.5f));
                yield return new WaitForSeconds(1f);
                int arms = Items.PropHits.DevPulses;
                var trail = new List<string>();
                string last = bell.ActiveStateName;
                float end = Time.time + 1.5f;
                while (Time.time < end)
                {
                    if (mod.DevReady && !mod.IsUsingItem) mod.DevUseSelected(ring);
                    if (bell.ActiveStateName != last) { last = bell.ActiveStateName; trail.Add(last); }
                    yield return null;
                }
                bool rang = trail.Exists(s => s.StartsWith("Ring"));
                Plugin.Log.LogInfo($"[AutoTest] 敲铃 用 {mod.Inventory.Current.Def?.Name}（主角 {(Vector2)hero.transform.position:F2}）：{(rang ? "响了（通过）" : "没响（不通过）")}，铃的状态 {string.Join(" > ", trail)}；机关命中替身出了 {Items.PropHits.DevPulses - arms} 次，最后在 {Items.PropHits.DevLast}");
            }
            mod.Inventory.Selected = 0;
        }

        /// <summary>
        /// Dev (suite "plat", after the bell): a row of platforms over Dirtmouth's open ground, the
        /// player dropped onto it from ever higher up (does he stay on it?), down held on it (does he
        /// drop through?), and the grappling hook thrown up at it from the ground (does it hold?).
        /// </summary>
        IEnumerator PlatformDropTest()
        {
            var gm = GameManager.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            var hero = HeroController.instance;
            var col = hero.GetComponent<BoxCollider2D>();
            Put(hero, new Vector2(60f, 14f));
            yield return new WaitForSeconds(1.5f);
            var feet = CellGrid.WorldToCell(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.1f));
            int fx = feet.x - 3, floor = feet.y;
            bool open = true;
            for (int x = fx; x <= fx + 6 && open; x++)
                for (int y = floor; y <= floor + 50; y++) if (TerraWorld.Get(x, y) != CellKind.Empty) { open = false; break; }
            if (!open)
            {
                Plugin.Log.LogInfo($"[AutoTest] 平台2 ({fx},{floor}) 上面不空，跳过");
                yield break;
            }
            int rowY = floor + 6;
            var placed = new List<Vector2Int>();
            for (int x = fx; x <= fx + 6; x++) { TerraWorld.Set(x, rowY, CellKind.Platform); placed.Add(new Vector2Int(x, rowY)); }
            yield return new WaitForSeconds(0.3f);
            float top = (rowY + 1) * CellGrid.CellSize, ground = floor * CellGrid.CellSize;
            float midX = (fx + 3.5f) * CellGrid.CellSize;
            var results = new StringBuilder();
            int stood = 0, drops = 0;
            foreach (float h in new[] { 1f, 3f, 6f, 12f, 20f })
            {
                drops++;
                Put(hero, new Vector2(midX, top + h + 0.8f));
                float wait = Time.time + 4f;
                yield return new WaitForSeconds(0.2f);
                while (!hero.cState.onGround && Time.time < wait) yield return null;
                yield return new WaitForSeconds(0.3f);
                float bottom = col.bounds.min.y;
                bool on = Mathf.Abs(bottom - top) < 0.3f;
                if (on) stood++;
                results.Append($" {h:F0}={(on ? "站住" : bottom < top - 0.3f ? "掉穿" : "?")}({bottom:F2})");
            }
            Plugin.Log.LogInfo($"[AutoTest] 平台2 平台顶 {top:F2}，地面 {ground:F2}，从高处掉下来：{results}{(stood == drops ? " 通过" : " 不通过")}");

            // Down held on it.
            Put(hero, new Vector2(midX, top + 1f));
            yield return new WaitForSeconds(0.8f);
            Player.TerraMovement.DevDown = true;
            yield return new WaitForSeconds(0.6f);
            Player.TerraMovement.DevDown = false;
            yield return new WaitForSeconds(1f);
            float after = col.bounds.min.y;
            Plugin.Log.LogInfo($"[AutoTest] 平台2 按住下：脚底 {after:F2}{(after < top - 0.5f ? "，穿下去了（通过）" : "，还在上面（不通过）")}");

            // The hook from the ground up at the platform.
            Put(hero, new Vector2(midX, ground + 1f));
            yield return new WaitForSeconds(0.8f);
            int latches = Player.GrapplingHook.DevLatches;
            Player.GrapplingHook.Instance?.Throw(hero, new Vector2(midX + 0.3f, top - 0.2f));
            yield return new WaitForSeconds(1.2f);
            bool held = Player.GrapplingHook.DevLatches > latches;
            Plugin.Log.LogInfo($"[AutoTest] 平台2 抓钩勾平台：{(held ? "勾住了（通过）" : "没勾住（不通过）")}，主角 {(Vector2)col.bounds.center:F2}");
            Shot("Town", "plat_hook");
            yield return new WaitForSeconds(1.5f);
            foreach (var p in placed) TerraWorld.Set(p.x, p.y, CellKind.Empty);
        }

        /// <summary>
        /// Dev (suite "guide"): the Guide's crafting menu: talk to him, his crafting button, wood
        /// put in his slot (the recipes it goes into, nothing made from them), the talk ended (the
        /// wood back in the bag).
        /// </summary>
        IEnumerator GuideTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var at = Town.TownNpcs.DevPos("Guide");
            if (!at.HasValue)
            {
                Plugin.Log.LogInfo($"[AutoTest] 向导 不在德特茅斯（{Town.TownNpcs.DevDescribe()}），不通过");
                yield break;
            }
            var hero = HeroController.instance;
            bool talked = false;
            for (int tries = 0; tries < 5 && !talked; tries++)
            {
                at = Town.TownNpcs.DevPos("Guide");
                float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
                hero.transform.position = Clear(hero, new Vector2(at.Value.x - 1.5f, at.Value.y - 20f / 32f - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.6f);
                talked = Town.TownNpcs.TryRightClick(Town.TownNpcs.DevPos("Guide").Value);
            }
            Plugin.Log.LogInfo($"[AutoTest] 向导 对话：{talked}，是向导 {Town.TownNpcs.TalkingIsGuide}，对话框 {Town.TownNpcs.ChatOpen}");
            if (!talked) yield break;
            yield return new WaitForSeconds(0.5f);
            Shot("Town", "guide_chat");
            yield return null;
            Town.TownNpcs.OpenGuideCraft();
            yield return new WaitForSeconds(0.3f);
            int woodBefore = Items.Recipes.Count(mod.Inventory, Items.ItemType.Wood);
            mod.Inventory.Mouse = Items.ItemStack.Of(Items.ItemType.Wood, 10);
            Items.InventoryGui.DevGuideSlot();
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 向导 制作菜单 {Town.TownNpcs.GuideCraftOpen}，槽里 {Town.TownNpcs.GuideItem.Def?.Name}×{Town.TownNpcs.GuideItem.Count}，光标 {(mod.Inventory.Mouse.IsEmpty ? "空" : mod.Inventory.Mouse.Def.Name)}；用到木材的配方：{Items.InventoryGui.DevRecipeNames(12)}");
            Shot("Town", "guide_craft");
            yield return null;
            // A save while the wood is in his slot: it goes back into the bag first, and nothing is lost.
            Save.ModSave.Write(2, mod);
            int woodSaved = Items.Recipes.Count(mod.Inventory, Items.ItemType.Wood);
            Plugin.Log.LogInfo($"[AutoTest] 向导 槽里有木材时存档：槽 {(Town.TownNpcs.GuideItem.IsEmpty ? "空了" : "还有")}，背包木材 {woodBefore} → {woodSaved}{(woodSaved == woodBefore + 10 && Town.TownNpcs.GuideItem.IsEmpty ? " 通过" : " 不通过")}");
            mod.Inventory.Mouse = default;
            int taken = Items.Recipes.Count(mod.Inventory, Items.ItemType.Wood);
            // Back into the slot for the rest.
            for (int i = 0; i < mod.Inventory.Slots.Length; i++)
                if (mod.Inventory.Slots[i].Type == Items.ItemType.Wood) { mod.Inventory.Mouse = mod.Inventory.Slots[i]; mod.Inventory.Slots[i] = default; break; }
            Items.InventoryGui.DevGuideSlot();
            bool made = Items.InventoryGui.DevCraft(Items.ItemType.WorkBench);
            Plugin.Log.LogInfo($"[AutoTest] 向导 在向导的列表里点工作台：{(made ? "做出来了（不通过）" : "没做（对，只能看）")}");
            Town.TownNpcs.Close(true);
            yield return new WaitForSeconds(0.3f);
            int woodAfter = Items.Recipes.Count(mod.Inventory, Items.ItemType.Wood);
            Plugin.Log.LogInfo($"[AutoTest] 向导 关掉对话：槽 {(Town.TownNpcs.GuideItem.IsEmpty ? "空了" : "还有东西")}，背包木材 {woodBefore} → {woodAfter}{(woodAfter == woodBefore + 10 ? " 通过" : " 不通过")}");
            // A stone that goes into nothing isn't taken.
            Town.TownNpcs.TryRightClick(Town.TownNpcs.DevPos("Guide").Value);
            Town.TownNpcs.OpenGuideCraft();
            yield return new WaitForSeconds(0.3f);
            mod.Inventory.Mouse = Items.ItemStack.Of(Items.ItemType.CopperShortsword, 1);
            Items.InventoryGui.DevGuideSlot();
            Plugin.Log.LogInfo($"[AutoTest] 向导 放铜短剑（不是材料）：槽 {(Town.TownNpcs.GuideItem.IsEmpty ? "没收（通过）" : "收了")}");
            mod.Inventory.Mouse = default;
            Town.TownNpcs.Close(true);
        }

        static void Put(HeroController hero, Vector2 at)
        {
            var rb = hero.GetComponent<Rigidbody2D>();
            hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
            rb.position = at;
            rb.linearVelocity = Vector2.zero;
        }

        /// <summary>Waits until an FSM's state passes a test, noting each state it goes through.</summary>
        static IEnumerator Until(PlayMakerFSM f, System.Func<string, bool> done, float seconds, List<string> trail)
        {
            float end = Time.time + seconds;
            string last = null;
            while (f != null && Time.time < end)
            {
                string s = f.ActiveStateName;
                if (s != last) { trail.Add(s); last = s; }
                if (done(s)) yield break;
                yield return null;
            }
            trail.Add("（超时）");
        }

        static IEnumerable<string> Chunks(List<string> lines, int n)
        {
            for (int i = 0; i < lines.Count; i += n) yield return string.Join(" | ", lines.GetRange(i, Mathf.Min(n, lines.Count - i)));
        }

        /// <summary>The stag menu: the item chosen, each item's number, name and shown text, and where the map marker is.</summary>
        static string MenuState(PlayMakerFSM list, List<PlayMakerFSM> items)
        {
            var sb = new StringBuilder();
            sb.Append($"当前第 {list.FsmVariables.GetFsmInt("Current Item").Value} 项 '{list.FsmVariables.GetFsmString("Current Selection").Value}'，共 {list.FsmVariables.GetFsmInt("Items").Value} 项；");
            foreach (var it in items)
            {
                if (!it.gameObject.activeInHierarchy) continue;
                string text = "";
                foreach (var c in it.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name.Contains("TextMeshPro"))
                    {
                        text = c.GetType().GetProperty("text")?.GetValue(c) as string ?? "";
                        break;
                    }
                sb.Append($" {it.FsmVariables.GetFsmInt("Item Number").Value}={it.FsmVariables.GetFsmString("Selection Name").Value}「{text}」{(it.FsmVariables.GetFsmBool("Selected").Value ? "★" : "")}");
            }
            var marker = GameObject.FindWithTag("StagMapMarker");
            sb.Append($"；地图标记 {(marker != null ? marker.transform.localPosition.ToString("F2") : "无")}");
            return sb.ToString();
        }
    }
}
