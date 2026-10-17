using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A seal's fight begun the moment its arena is reached (the player brought in by the arena's own way in), as the
    /// arena's own FSMs start it when the player walks up, with nothing left for the player to do:
    /// the player is set down beside the boss on the side they came in by (inside the arena, its gates behind them);
    /// the room's battle FSM waiting for the player's touch (the arena's "Battle Scene"/"Battle Control", Grimm's
    /// "Initial Scene", the Black Egg's gate) is sent the event that touch sends; a boss waiting for the player to come
    /// into one of its own ranges (Wake/Alert/Start/Refight/Rewake Range) has the player in it; the Mantis Lords'
    /// challenge is taken up (in range, then up pressed); a warrior's dream appears and is dream-nailed; a boss asleep
    /// is woken (BossWake.Rouse). Done when the room's battle FSM has gone on, or a boss of the seal has come up or left
    /// the waiting state it was in. If not within a second and a half, what it waits on sent to it (startEvents); a second try after that.
    /// </summary>
    internal static class BossSummonStart
    {
        public static int DevStarted, DevFired, DevPlaced, DevPrompts, DevDreamed, DevRoused;
        public static string DevLast = "";

        /// <summary>The room's own battle FSMs (not a boss's): by their object's or FSM's name.</summary>
        static readonly Regex battleFsm = new Regex(@"Battle Scene|Battle Control|Battle Start|Grimm Scene|Initial Scene");

        /// <summary>The room's own battle FSMs (not a boss's): by name; the Black Egg's gate ("Gate" / "Control").</summary>
        static bool IsBattle(PlayMakerFSM f) =>
            battleFsm.IsMatch(f.gameObject.name) || battleFsm.IsMatch(f.FsmName) || f.gameObject.name == "Gate" && f.FsmName == "Control";
        /// <summary>
        /// What a waiting boss or battle FSM goes on by when no touch of the player's is what it waits on (Hornet's refight
        /// and her Kingdom's Edge battle, the Hollow Knight's chains), in the order to try: sent when the rest hasn't started it.
        /// </summary>
        static readonly string[] startEvents = { "REFIGHT", "WAKE", "BATTLE START", "CHAIN BREAK" };
        /// <summary>A boss's own range it waits on the player to come into, in the order to put the player in one.</summary>
        static readonly string[] ranges = { "Start Range", "Refight Range", "Rewake Range", "Wake Range", "Alert Range", "Battle Range", "Hero Range" };
        /// <summary>States a boss waits in for its fight.</summary>
        static readonly Regex waitingState = new Regex("Sleep|Dormant|Inert|Rest|Ready|Away|Waiting|Init|Detect|Invincible|Stopped");

        public static IEnumerator Begin(string key)
        {
            string record = key.Split('@')[0];
            string room = BossArena.SceneOf(key);
            // Godhome starts its own fights; a dream's waits like a world arena's (the Failed Champion's battle scene, the
            // Radiance's challenge).
            if (room == null || room.StartsWith("GG_")) yield break;
            // Out of the scene's entry: control back (the gate's walk in), a frame for the arena's FSMs to settle.
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until)
            {
                var gm = GameManager.UnsafeInstance;
                if (gm != null && !gm.IsInSceneTransition && HeroController.SilentInstance != null && GameHooks.PlayerHasControl()) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            var done = new List<string>();
            var before = States(room, record);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!BossArena.Visiting) yield break;
                var hero = HeroController.SilentInstance;
                if (hero == null) yield break;
                if (attempt != 1) yield return Act(room, record, hero, done, attempt > 0);
                if (attempt > 0) yield return Nudge(room, record, done);
                float wait = Time.realtimeSinceStartup + (attempt == 0 ? 1.5f : 3f);
                while (Time.realtimeSinceStartup < wait)
                {
                    if (!BossArena.Visiting) yield break;
                    string why = Started(room, record, before);
                    if (why != null)
                    {
                        DevStarted++;
                        DevLast = $"{key} 开战（{why}）：{string.Join("，", done)}";
                        Plugin.Log.LogInfo("[首领召唤物] " + DevLast);
                        TerraMod.Instance?.StartCoroutine(Free(room, record));
                        yield return SkipTalk(room, 12f, done);
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            DevLast = $"{key} 未确认开战：{string.Join("，", done)}";
            Plugin.Log.LogWarning("[首领召唤物] " + DevLast);
        }

        /// <summary>What the player's walking up would do, done.</summary>
        static IEnumerator Act(string room, string record, HeroController hero, List<string> done, bool again)
        {
            var parts = Parts(room, record);
            // A fight with the game's own short way in once faced (the Radiance's: the challenge's long build-up, or a
            // pause and the boss): the short way.
            var fork = Fsm(room, f => Waits(f, "CHALLENGE") && Waits(f, "REFIGHT") && f.GetComponentInParent<HealthManager>() == null);
            if (fork != null)
            {
                fork.SendEvent("REFIGHT");
                PlayerData.instance.SetBool("disablePause", false);
                DevRefights++;
                done.Add($"{fork.gameObject.name}/{fork.FsmName} ← REFIGHT（跳过开场）");
                yield break;
            }
            // The Mantis Lords' challenge: at the prompt, in range, then up.
            var prompt = Fsm(room, f => f.FsmName == "Challenge Start");
            if (prompt != null)
            {
                yield return Challenge(room, hero, prompt, done);
                yield break;
            }
            // A warrior's dream: near it, it appears, one blow of the dream nail.
            var dream = Fsm(room, f => f.FsmName == "ghost_npc_dreamnail" && Waits(f, "HIT"));
            if (dream != null)
            {
                var appear = dream.transform.parent != null ? dream.transform.parent.Find("Appear Range") : null;
                var range = appear != null ? appear.GetComponent<Collider2D>() : null;
                Put(hero, range != null ? (Vector2)range.bounds.center : (Vector2)dream.transform.position + new Vector2(-3f, 0f));
                var npc = dream.transform.parent != null ? dream.transform.parent.GetComponent<PlayMakerFSM>() : null;
                for (float t = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < t; )
                {
                    var shown = AppearOf(dream);
                    if (shown == null || shown.ActiveStateName != "Away") break;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                // A blow before the warrior is fully there goes unanswered: again each second, three at most.
                for (int k = 0; k < 3 && dream != null && Waits(dream, "HIT"); k++)
                {
                    dream.SendEvent("HIT");
                    DevDreamed++;
                    done.Add("梦之钉");
                    for (float t = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < t; ) yield return null;
                    if (Parts(room, record).Exists(hm => hm.gameObject.activeInHierarchy)) break;
                }
                yield break;
            }
            // Beside the boss, on the side the player came in by.
            if (!again && parts.Count > 0)
            {
                Vector2 at = Beside(hero, parts);
                Put(hero, at);
                DevPlaced++;
                done.Add($"站到首领旁 {at:F1}");
            }
            // Into the boss's own range it waits on.
            foreach (var name in ranges)
            {
                Collider2D range = null;
                foreach (var hm in parts)
                {
                    var t = Find(hm.transform, name);
                    var c = t != null ? t.GetComponent<Collider2D>() : null;
                    if (c != null && c.enabled && c.gameObject.activeInHierarchy) { range = c; break; }
                }
                if (range == null) continue;
                var feet = (Vector2)hero.transform.position;
                if (!range.bounds.Contains(new Vector3(feet.x, feet.y + 0.5f, range.bounds.center.z)))
                {
                    var b = range.bounds;
                    Put(hero, new Vector2(Mathf.Clamp(feet.x, b.min.x + Mathf.Min(1f, b.extents.x), b.max.x - Mathf.Min(1f, b.extents.x)), b.center.y));
                }
                done.Add("进入 " + name);
                break;
            }
            yield return new WaitForSecondsRealtime(0.1f);
            // The Troupe Master's tent: past his entrance, words and hand-out, straight to the refight's lights (the lights up,
            // the camera's lock, the gates shut; a second on, the battle).
            var tent = Fsm(room, f => f.FsmName == "Initial Scene" && f.ActiveStateName == "Battle Ready");
            if (tent != null && HasState(tent, "Lights Up 2"))
            {
                tent.SetState("Lights Up 2");
                done.Add("格林直接开灯开战");
            }
            // The room's battle FSM waiting on the player's touch: the touch's event.
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if (sn != room && sn != room + "_boss") continue;
                if (!IsBattle(f)) continue;
                if (f.GetComponentInParent<HealthManager>() != null) continue;
                string ev = TouchEvent(f);
                if (ev == null) continue;
                f.SendEvent(ev);
                DevFired++;
                done.Add($"{f.gameObject.name}/{f.FsmName} ← {ev}");
            }
            // A boss asleep: woken.
            foreach (var hm in parts)
                if (BossWake.Rouse(hm)) { DevRoused++; done.Add("叫醒 " + hm.name); }
        }

        /// <summary>A boss's challenge taken up as the player does it: at its prompt, on the ground, in range, then up.</summary>
        static IEnumerator Challenge(string room, HeroController hero, PlayMakerFSM prompt, List<string> done)
        {
            Put(hero, prompt.transform.position);
            for (float t = Time.realtimeSinceStartup + 2f; !hero.cState.onGround && Time.realtimeSinceStartup < t;) yield return null;
            Plugin.Log.LogInfo($"[首领召唤物] 挑战提示 {prompt.gameObject.scene.name}/{prompt.gameObject.name}#{prompt.GetInstanceID()} 状态 {prompt.ActiveStateName}，主角落地 {hero.cState.onGround} 可操作 {GameHooks.PlayerHasControl()}");
            if (prompt != null && Waits(prompt, "IN RANGE")) prompt.SendEvent("IN RANGE");
            // Up, again until the prompt takes it (its "Can Talk?" turns it away while the player isn't yet free to act).
            for (float until = Time.realtimeSinceStartup + 1.5f; prompt != null && Time.realtimeSinceStartup < until;)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                if (prompt == null || !Waits(prompt, "UP PRESSED")) break;
                prompt.SendEvent("UP PRESSED");
                DevPrompts++;
            }
            // Still turned away (the Radiance's asks HeroController.CanTalk, which the Terraria movement's states don't
            // meet): its challenge sent as the prompt's last step does, the player kept free.
            yield return new WaitForSecondsRealtime(0.2f);
            string at = prompt != null ? prompt.ActiveStateName : null;
            // Turned away still: taken up as the prompt does it (its marker down, the player held for the draw).
            if (at != null && (at.StartsWith("In Range") || at == "Cancel Frame" || at == "Can Talk?") && HasState(prompt, "Take Control"))
            {
                prompt.SetState("Take Control");
                done.Add("代为接受挑战");
                yield return new WaitForSecondsRealtime(0.5f);
                at = prompt != null ? prompt.ActiveStateName : null;
            }
            // Waiting on the Knight's own challenge clip (its tk2d event never comes under the Terraria player): the
            // challenge sent on.
            if (at != null && (at.StartsWith("Challenge") || at == "Set Facing" || at.StartsWith("On ") || at == "Take Control")
                && HasState(prompt, "Send Challenge"))
            {
                prompt.SetState("Send Challenge");
                done.Add("直接发出挑战");
            }
            // And to what waits on the challenge in the room (the Radiance's "Boss Control"), if it still does.
            yield return new WaitForSecondsRealtime(0.9f);
            var receiver = Fsm(room, f => f != prompt && Waits(f, "CHALLENGE"));
            if (receiver != null)
            {
                receiver.SendEvent("CHALLENGE");
                done.Add($"{receiver.gameObject.name} ← CHALLENGE");
            }
            done.Add($"接受挑战（{(prompt != null ? prompt.ActiveStateName : "无")}）");
        }

        public static int DevTalksSkipped, DevFreed;

        /// <summary>
        /// Alongside the fight's start: the player given control back once held four seconds on end with no words up (a
        /// step skipped on the way, the challenge's own draw, was what would have handed it back; the game's own roar or
        /// title hands it back sooner). Twenty seconds at most, and only until the fight is under way.
        /// </summary>
        static IEnumerator Free(string room, string record)
        {
            float heldSince = -1f, freeSince = -1f;
            var hp = new Dictionary<int, int>();
            for (float until = Time.realtimeSinceStartup + 20f; Time.realtimeSinceStartup < until;)
            {
                if (!BossArena.Visiting) yield break;
                var hero = HeroController.SilentInstance;
                if (hero == null || hero.cState.dead) yield break;
                // Only the start's: once the fight is under way (the player has had a second and a half in hand, or the
                // boss is hurt or down) what holds the player is the fight's own (a death, the Radiance's last blow).
                foreach (var hm in Parts(room, record))
                {
                    int id = hm.GetInstanceID();
                    if (hm.isDead || hp.TryGetValue(id, out int was) && hm.hp < was) yield break;
                    if (!hp.ContainsKey(id)) hp[id] = hm.hp;
                }
                bool free = GameHooks.PlayerHasControl();
                if (!free) freeSince = -1f;
                else if (freeSince < 0f) freeSince = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - freeSince > 1.5f) yield break;
                if (free || UI.HudDialogue.DevOpen) heldSince = -1f;
                else if (heldSince < 0f) heldSince = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - heldSince > 4f)
                {
                    hero.RegainControl();
                    hero.StartAnimationControl();
                    PlayerData.instance.SetBool("disablePause", false);
                    DevFreed++;
                    Plugin.Log.LogInfo("[首领召唤物] 开战后把操作还给玩家（" + room + "）");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        /// <summary>
        /// A boss's words before its fight (the Troupe Master's) skipped: while they show, the box put down and the FSM in
        /// the room that waits for the talk's end (CONVO_FINISH) sent it, as the last page turned would.
        /// </summary>
        static IEnumerator SkipTalk(string room, float seconds, List<string> done)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
            {
                if (!BossArena.Visiting) yield break;
                if (UI.HudDialogue.DevOpen)
                {
                    var talker = Fsm(room, f => Waits(f, "CONVO_FINISH"));
                    if (talker != null)
                    {
                        UI.HudDialogue.Close();
                        talker.SendEvent("CONVO_FINISH");
                        DevTalksSkipped++;
                        done.Add($"跳过对话 {talker.gameObject.name}");
                        Plugin.Log.LogInfo($"[首领召唤物] 跳过开战前的对话：{talker.gameObject.name}/{talker.FsmName}");
                    }
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        /// <summary>
        /// The boss's and the room's battle FSMs still waiting, sent the first start event each waits on; the Hollow Knight's
        /// chains one break at a time until they are all broken (its "Battle Start" counts them down).
        /// </summary>
        static IEnumerator Nudge(string room, string record, List<string> done)
        {
            var fsms = new List<PlayMakerFSM>();
            foreach (var hm in Parts(room, record))
                if (hm.gameObject.activeInHierarchy) fsms.AddRange(hm.GetComponents<PlayMakerFSM>());
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if ((sn == room || sn == room + "_boss") && IsBattle(f)
                    && f.GetComponentInParent<HealthManager>() == null) fsms.Add(f);
            }
            foreach (var f in fsms)
            {
                var st = f.Fsm?.ActiveState;
                if (st == null || !waitingState.IsMatch(st.Name) && st.Name != "Idle") continue;
                foreach (var ev in startEvents)
                    if (Waits(f, ev))
                    {
                        int times = 0;
                        for (int k = 0; k < (ev == "CHAIN BREAK" ? 8 : 1) && f != null && Waits(f, ev); k++)
                        {
                            f.SendEvent(ev);
                            times++;
                            yield return null;
                            yield return null;
                        }
                        DevNudged++;
                        done.Add($"{f?.gameObject.name}/{f?.FsmName} ← {ev}" + (times > 1 ? " ×" + times : ""));
                        // The Hollow Knight free of its chains: past its pause and its struggle, straight to the chains' break.
                        if (ev == "CHAIN BREAK" && f != null && HasState(f, "Break")
                            && (f.ActiveStateName == "Free Pause" || f.ActiveStateName == "Struggle" || f.ActiveStateName == "Break Antic"))
                        {
                            f.SetState("Break");
                            done.Add("锁链直接断开");
                        }
                        break;
                    }
            }
        }

        public static int DevNudged, DevRefights;

        /// <summary>
        /// Started: the room's battle FSM gone on from where it stood, a boss of the seal come up (it was waiting out of
        /// play), or one of its FSMs out of the waiting state it was in. Null while not.
        /// </summary>
        static string Started(string room, string record, Dictionary<int, string> before)
        {
            // A LAN game where another player's game runs the arena: the boss here is its puppet (Net.Enemies), its scripts
            // still; the fight is that game's to start.
            if (Net.Enemies.RemoteOwned(room) || Net.Enemies.RemoteOwned(room + "_boss")) return "别人的游戏负责这场战斗";
            foreach (var kv in States(room, record))
            {
                if (!before.TryGetValue(kv.Key, out var was)) return "首领出场 " + kv.Value;
                if (was == kv.Value) continue;
                string now = kv.Value.Substring(kv.Value.IndexOf(':') + 1);
                if (!waitingState.IsMatch(now)) return $"{was} → {now}";
            }
            return null;
        }

        /// <summary>
        /// The FSMs that tell a fight on: those of the seal's boss in play (on its HealthManager's object) and the room's
        /// battle FSMs, each "path:state".
        /// </summary>
        static Dictionary<int, string> States(string room, string record)
        {
            var map = new Dictionary<int, string>();
            foreach (var hm in Parts(room, record))
            {
                if (!hm.gameObject.activeInHierarchy) continue;
                foreach (var f in hm.GetComponents<PlayMakerFSM>())
                    if (f.Fsm?.ActiveState != null) map[f.GetInstanceID()] = $"{hm.name}/{f.FsmName}:{f.ActiveStateName}";
            }
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if ((sn != room && sn != room + "_boss") || f.Fsm?.ActiveState == null) continue;
                if (!battleFsm.IsMatch(f.gameObject.name) && !battleFsm.IsMatch(f.FsmName)) continue;
                if (f.GetComponentInParent<HealthManager>() != null) continue;
                map[f.GetInstanceID()] = $"{f.gameObject.name}/{f.FsmName}:{f.ActiveStateName}";
            }
            return map;
        }

        /// <summary>The event the FSM's state sends on the player's touch (Trigger2dEvent on entering or staying, for the player), if it waits on one it goes on by.</summary>
        static string TouchEvent(PlayMakerFSM f)
        {
            var st = f.Fsm?.ActiveState;
            if (st == null) return null;
            foreach (var act in st.Actions)
                if (act is HutongGames.PlayMaker.Actions.Trigger2dEvent te && te.trigger != PlayMakerUnity2d.Trigger2DType.OnTriggerExit2D
                    && (te.collideTag == null || string.IsNullOrEmpty(te.collideTag.Value) || te.collideTag.Value == "Player" || te.collideTag.Value == "HeroBox")
                    && te.sendEvent != null && Waits(f, te.sendEvent.Name))
                    return te.sendEvent.Name;
            return null;
        }

        static bool HasState(PlayMakerFSM f, string name)
        {
            foreach (var st in f.FsmStates) if (st.Name == name) return true;
            return false;
        }

        static bool Waits(PlayMakerFSM f, string ev)
        {
            var st = f.Fsm?.ActiveState;
            if (st == null) return false;
            foreach (var t in st.Transitions) if (t.EventName == ev) return true;
            foreach (var t in f.FsmGlobalTransitions) if (t.EventName == ev) return true;
            return false;
        }

        static PlayMakerFSM Fsm(string room, System.Predicate<PlayMakerFSM> which)
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if ((sn == room || sn == room + "_boss") && f.Fsm?.ActiveState != null && which(f)) return f;
            }
            return null;
        }

        /// <summary>The warrior's own "Appear" FSM (on the NPC beside its dream-nail target).</summary>
        static PlayMakerFSM AppearOf(PlayMakerFSM dream)
        {
            var npc = dream.transform.parent;
            if (npc == null) return null;
            foreach (var f in npc.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Appear") return f;
            return null;
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var t = Find(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>The seal's boss in the room (and its "_boss" scene), up or still waiting.</summary>
        static List<HealthManager> Parts(string room, string record)
        {
            var list = new List<HealthManager>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead) continue;
                string sn = hm.gameObject.scene.name;
                if (sn != room && sn != room + "_boss") continue;
                if (UI.EnemyHover.RecordOf(hm) == record) list.Add(hm);
            }
            return list;
        }

        /// <summary>
        /// A spot on the ground some steps from the boss on the side the player stands (the side they came in by): the
        /// first of 7, 5, 9, 3 and 11 units out with floor under it, from up level with the boss down — but from no higher
        /// than a little over where the player came in: a boss asleep on a ledge up in its room (the Hive Knight) or hanging
        /// from its ceiling would set the player up out of the arena's view, the fight below.
        /// </summary>
        static Vector2 Beside(HeroController hero, List<HealthManager> parts)
        {
            Vector2 boss = Vector2.zero;
            foreach (var hm in parts) boss += (Vector2)hm.transform.position;
            boss /= parts.Count;
            float side = hero.transform.position.x < boss.x ? -1f : 1f;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            foreach (float dx in new[] { 7f, 5f, 9f, 3f, 11f })
                foreach (float s in new[] { side, -side })
                {
                    var from = new Vector2(boss.x + s * dx, Mathf.Min(boss.y + 4f, hero.transform.position.y + 6f));
                    var down = Physics2D.Raycast(from, Vector2.down, 30f, terrain);
                    if (down.collider != null && down.distance > 0.1f) return new Vector2(from.x, down.point.y + 0.1f);
                }
            return boss;
        }

        /// <summary>The player to a spot: its feet there, or the first place straight above it the player fits.</summary>
        static void Put(HeroController hero, Vector2 feet)
        {
            var box = hero.GetComponent<BoxCollider2D>().bounds;
            Vector2 offset = (Vector2)box.center - (Vector2)hero.transform.position;
            float footToCentre = box.extents.y;
            var pos = Clear(hero, feet + new Vector2(0f, footToCentre) - offset);
            hero.transform.position = new Vector3(pos.x, pos.y, hero.transform.position.z);
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
        }

        static Vector2 Clear(HeroController hero, Vector2 pos)
        {
            const float c = CellGrid.CellSize;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            Vector2 offset = (Vector2)b.center - (Vector2)hero.transform.position;
            for (int k = 0; k < 40; k++)
            {
                var center = pos + offset + Vector2.up * (k * c);
                var lo = CellGrid.WorldToCell(center - (Vector2)b.extents);
                var hi = CellGrid.WorldToCell(center + (Vector2)b.extents);
                bool solid = false;
                for (int y = lo.y; y <= hi.y && !solid; y++)
                    for (int x = lo.x; x <= hi.x && !solid; x++)
                        solid = TerraWorld.IsSolid(x, y);
                if (!solid) return pos + Vector2.up * (k * c);
            }
            return pos;
        }
    }
}
