using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fix1031b", the user's 10-31 evening reports; TK_FIX_PARTS picks some of gate;saber;shadow;hook):
    /// (gate) in boss rooms with battle gates (TK_GATE_ROOMS), the Knight outside each open gate on the doorway's floor: the
    /// boss out of his reach (the old raised-gate judgement logged beside it), his blow lands on nothing and wakes nothing;
    /// beside the boss he is in. (saber) in Crossroads_01 the Blue Phaseblade thrown to hang in the air over an enemy: as
    /// before (any layer) and as now, the enemies cut while it hangs; then thrown onto an enemy, that one is cut. (shadow) a
    /// shadow dash each way on Dirtmouth's street: the Knight's burst behind him, turned as HeroDash turns it. (hook) in the
    /// Troupe Master's halls (TK_HOOK_ROOMS) the Hook of Dissonance thrown straight up from the arena: what is over him, and
    /// where the hook holds and he ends up, terrain layer only (as before) and as now.
    /// Lines "[AutoTest] 10-31晚 …：通过 / 不通过".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Fix1031bTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var parts = (Environment.GetEnvironmentVariable("TK_FIX_PARTS") ?? "gate;saber;shadow;hook").Split(';');
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 10-31晚 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            void Note(string line) => Plugin.Log.LogInfo($"[AutoTest] 10-31晚 {line}");
            try
            {
                if (parts.Contains("gate")) yield return Fix1031bGates(Check, Note);
                if (parts.Contains("lockout")) yield return Fix1031bLockout(Check, Note);
                if (parts.Contains("saber")) yield return Fix1031bSaber(Check, Note);
                if (parts.Contains("shadow")) yield return Fix1031bShadow(Check, Note);
                if (parts.Contains("hook")) yield return Fix1031bHook(Check, Note);
                if (parts.Contains("survey")) yield return Fix1031bGateSurvey(Note);
            }
            finally
            {
                ArenaGuard.DevRaisedGates = false;
                BladeSkills.DevAnyLayer = false;
                Player.GrapplingHook.DevTerrainOnly = false;
                Plugin.Log.LogInfo($"[AutoTest] 10-31晚 结果 {ok}/{all}");
            }
        }

        IEnumerator Fix1031bGates(Action<string, bool, string> check, Action<string> note)
        {
            string rooms = Environment.GetEnvironmentVariable("TK_GATE_ROOMS") ?? "Crossroads_04;Mines_18;Fungus1_20_v02;Fungus1_04;Crossroads_09";
            foreach (var room in rooms.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSecondsRealtime(2f);
                var hero = HeroController.instance;
                PlayerData.instance.isInvincible = true;
                var rb = hero.GetComponent<Rigidbody2D>();
                var hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h)?.Boss == true)
                    .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).FirstOrDefault();
                if (hm == null) { note($"场外 {room}：没有首领"); continue; }
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                string who = $"{room} {EnemyStats.Of(hm).Record}";
                Vector2 boss = col.bounds.center;
                ArenaGuard.DevRaisedGates = false;
                var gates = ArenaGuard.DevGates().Where(g => g.Scene == room).ToList();
                string States() => string.Join("、", Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                    .Where(f => f.FsmName == "BG Control" && f.gameObject.scene.name.StartsWith(room))
                    .Select(f => $"{f.name}@{f.transform.position.x:F1}:{f.ActiveStateName}"));
                note($"场外 {who}：首领在 {boss:F1}，战斗门 {gates.Count} 道（{string.Join("；", gates.Select(g => $"x {g.X:F1} 门洞 {g.Bottom:F1}~{g.Top:F1}"))}），门状态 {States()}");
                if (gates.Count == 0) continue;
                var ways = Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None)
                    .Select(t => t.GetComponent<Collider2D>()).Where(c => c != null).Select(c => c.bounds).ToList();
                float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
                float side = 1f;
                foreach (var g in gates)
                {
                    side = Mathf.Sign(boss.x - g.X);
                    var spot = new Vector2(g.X - side * 2.5f, g.Bottom - bottom + 0.1f);
                    if (ways.Any(b => { b.Expand(4f); return b.Contains(spot); })) { note($"场外 {who}：门 x {g.X:F1} 外面紧挨出口，跳过"); continue; }
                    hero.transform.position = Clear(hero, spot);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSecondsRealtime(0.8f);
                    Vector2 at = hero.GetComponent<BoxCollider2D>().bounds.center;
                    if (Mathf.Sign(at.x - g.X) != -side) { note($"场外 {who}：没站到门 x {g.X:F1} 外面（人在 {at:F1}）"); continue; }
                    ArenaGuard.DevRaisedGates = true;
                    ArenaGuard.DevGates();
                    bool oldOut = ArenaGuard.Outside(hm);
                    ArenaGuard.DevRaisedGates = false;
                    ArenaGuard.DevGates();
                    bool newOut = ArenaGuard.Outside(hm);
                    int hp0 = hm.hp, woke0 = BossWake.DevWoken;
                    bool landed = mod.Melee.HitWith(col, 40, 0f, 0f, side, at);
                    yield return new WaitForSecondsRealtime(0.6f);
                    check($"场外 {who} 站在门外（门 x {g.X:F1}，人 {at:F1}）",
                        newOut && !landed && hm.hp == hp0 && BossWake.DevWoken == woke0,
                        $"旧判断场外 {oldOut}，新判断场外 {newOut}；出手命中 {landed}，血 {hp0}→{hm.hp}，叫醒 {BossWake.DevWoken - woke0}；{ArenaGuard.DevWhy(hm)}；门 {States()}");
                }
                // Beside the boss, in the arena: still struck from there.
                bool inside = false;
                Vector2 near = default;
                foreach (float s in new[] { 1f, -1f })
                {
                    hero.transform.position = Clear(hero, boss + new Vector2(s * (col.bounds.extents.x + 2f), 0f));
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSecondsRealtime(0.8f);
                    near = hero.GetComponent<BoxCollider2D>().bounds.center;
                    if (inside = !ArenaGuard.Outside(hm)) break;
                }
                check($"场外 {who} 首领旁边", inside, $"人 {near:F1}，{ArenaGuard.DevWhy(hm)}");
            }
        }

        /// <summary>
        /// Under an open battle gate, on its outer half (where one stands to shoot in): as now the boss is out of reach and a
        /// blow wakes nothing; then as before (a gate within 0.3 of the Knight not between), the blow from there, and where he
        /// is once the gates have come down. TK_LOCK_ROOMS.
        /// </summary>
        IEnumerator Fix1031bLockout(Action<string, bool, string> check, Action<string> note)
        {
            string rooms = Environment.GetEnvironmentVariable("TK_LOCK_ROOMS") ?? "Crossroads_04;Mines_18;Crossroads_09";
            foreach (var room in rooms.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSecondsRealtime(2f);
                var hero = HeroController.instance;
                PlayerData.instance.isInvincible = true;
                var rb = hero.GetComponent<Rigidbody2D>();
                var box = hero.GetComponent<BoxCollider2D>();
                var hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h)?.Boss == true)
                    .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).FirstOrDefault();
                if (hm == null) { note($"门下 {room}：没有首领"); continue; }
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                Vector2 boss = col.bounds.center;
                string who = $"{room} {EnemyStats.Of(hm).Record}";
                var fsms = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Where(f => f.FsmName == "BG Control" && f.gameObject.scene.name.StartsWith(room)).ToList();
                bool Shut(float x) => fsms.Any(f => Mathf.Abs(f.transform.position.x - x) < 0.5f && f.GetComponent<Collider2D>() != null && f.GetComponent<Collider2D>().enabled);
                var gates = ArenaGuard.DevGates().Where(g => g.Scene == room && !Shut(g.X)).OrderBy(g => Mathf.Abs(g.X - hero.transform.position.x)).ToList();
                if (gates.Count == 0) { note($"门下 {who}：没有开着的战斗门"); continue; }
                var gate = gates[0];
                float side = Mathf.Sign(boss.x - gate.X);
                float bottom = box.bounds.min.y - hero.transform.position.y;
                float offset = box.bounds.center.x - hero.transform.position.x;
                IEnumerator Under()
                {
                    hero.transform.position = new Vector3(gate.X - side * 0.2f - offset, gate.Bottom - bottom + 0.05f, hero.transform.position.z);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSecondsRealtime(0.8f);
                }
                yield return Under();
                Vector2 at = box.bounds.center;
                ArenaGuard.DevOldDoorway = true;
                bool oldOut = ArenaGuard.Outside(hm);
                ArenaGuard.DevOldDoorway = false;
                bool newOut = ArenaGuard.Outside(hm);
                int woke0 = BossWake.DevWoken;
                bool landed = mod.Melee.HitWith(col, 40, 0f, 0f, side, at);
                yield return new WaitForSecondsRealtime(2f);
                check($"门下 {who}（门 x {gate.X:F1} 门洞 {gate.Bottom:F1}~{gate.Top:F1}，人 {at:F1}）", newOut && !landed && BossWake.DevWoken == woke0,
                    $"旧判断场外 {oldOut}，新判断场外 {newOut}；出手命中 {landed}，叫醒 {BossWake.DevWoken - woke0}；{ArenaGuard.DevWhy(hm)}");
                // As before: the blow from under the gate, and the gates coming down.
                ArenaGuard.DevOldDoorway = true;
                yield return Under();
                at = box.bounds.center;
                landed = mod.Melee.HitWith(col, 40, 0f, 0f, side, at);
                ArenaGuard.DevOldDoorway = false;
                yield return new WaitForSecondsRealtime(4f);
                Vector2 now = box.bounds.center;
                bool shut = Shut(gate.X), outside = Mathf.Sign(now.x - gate.X) == -side;
                note($"门下 {who} （旧写法）从门下出手：命中 {landed}，叫醒 {BossWake.DevWoken - woke0}，4 秒后门关了 {shut}，人在 {now:F1}（{(outside ? "门外" : "门里")}）{(shut && outside ? "  ← 被关在外面" : "")}");
            }
        }

        /// <summary>
        /// Every world arena's fight as a seal starts it, four seconds in: its battle gates (BG Control) and every other
        /// solid thing named like a gate or a wall that stands in the room (what shuts an arena that ArenaGuard doesn't know).
        /// </summary>
        IEnumerator Fix1031bGateSurvey(Action<string> note)
        {
            PlayerData.instance.isInvincible = true;
            var words = new[] { "gate", "door", "wall", "block", "shutter", "barrier", "bars", "cage", "lock" };
            foreach (var key in BossArena.DevKeys().ToArray())
            {
                if (!BossArena.DevWorldArena(key, out _, out _)) continue;
                string scene = BossArena.SceneOf(key);
                Bestiary.Kills[key] = Mathf.Max(1, Bestiary.KillsOf(key));
                int started = BossSummonStart.DevStarted;
                if (!BossArena.Fight(key, true)) { note($"门普查 {key} 进不去"); continue; }
                float until = Time.realtimeSinceStartup + 25f;
                while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                until = Time.realtimeSinceStartup + 15f;
                while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                bool fight = BossSummonStart.DevStarted > started;
                yield return new WaitForSecondsRealtime(4f);
                PlayerData.instance.isInvincible = true;
                var bg = new List<string>();
                var other = new List<string>();
                foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                {
                    if (col == null || !col.enabled || col.isTrigger || !col.gameObject.scene.name.StartsWith(scene)) continue;
                    if (col.GetComponentInParent<HealthManager>() != null || col.GetComponentInParent<World.TerraOwned>() != null) continue;
                    var go = col.gameObject;
                    var fsms = go.GetComponents<PlayMakerFSM>();
                    string at = $"{go.name}@{col.bounds.center.x:F1},{col.bounds.center.y:F1} {col.bounds.size.x:F1}x{col.bounds.size.y:F1} 层{go.layer}";
                    if (fsms.Any(f => f.FsmName == "BG Control")) { bg.Add(at + ":" + fsms.First(f => f.FsmName == "BG Control").ActiveStateName); continue; }
                    string n = go.name.ToLowerInvariant();
                    bool named = words.Any(w => n.Contains(w)) || fsms.Any(f => words.Any(w => f.FsmName.ToLowerInvariant().Contains(w)));
                    if (!named || go.GetComponent<TransitionPoint>() != null) continue;
                    other.Add(at + (fsms.Length > 0 ? " FSM " + string.Join("/", fsms.Select(f => $"{f.FsmName}:{f.ActiveStateName}")) : ""));
                }
                note($"门普查 {key} 场景 {GameManager.instance.sceneName} 开战 {fight}：战斗门 {bg.Count}（{string.Join("；", bg)}）；其他像门的 {other.Count}（{string.Join("；", other)}）");
            }
        }

        IEnumerator Fix1031bSaber(Action<string, bool, string> check, Action<string> note)
        {
            const string room = "Crossroads_01";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSecondsRealtime(2f);
            for (float t = 0f; t < 8f && !GameHooks.PlayerHasControl(); t += Time.deltaTime) yield return null;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            var rb = hero.GetComponent<Rigidbody2D>();
            var box = hero.GetComponent<BoxCollider2D>();
            var inv = mod.Inventory;
            inv.Slots[0] = ItemStack.Of(ItemType.BluePhaseblade);
            inv.Selected = 0;
            var def = ItemDb.Get(ItemType.BluePhaseblade);
            int mask = MeleeHits.HitMask;
            List<HealthManager> Enemies() => Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h => h != null && !h.isDead && h.hp > 0 && h.gameObject.activeInHierarchy && EnemyStats.Of(h)?.Boss != true && UI.EnemyHover.RecordOf(h) != null).ToList();
            Vector2 Centre(HealthManager h)
            {
                var c = h.GetComponent<Collider2D>();
                return c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)h.transform.position;
            }
            int Life(HealthManager h) => h == null || h.isDead ? -1 : EnemyStats.Life(h, EnemyStats.Of(h));
            // An enemy's wide triggers no swing strikes (its sight and alert ranges).
            List<Collider2D> Ranges(HealthManager h) => h.GetComponentsInChildren<Collider2D>().Where(c => c.enabled && c.isTrigger && (mask & (1 << c.gameObject.layer)) == 0).ToList();
            bool Touching(Vector2 p) => Physics2D.OverlapCircleAll(p, 1.6f, mask).Any(c => c.GetComponentInParent<HealthManager>() != null);
            var list = Enemies();
            note($"光剑 {room}：可操作 {GameHooks.PlayerHasControl()}，右键技 {BladeSkills.Of(def)}，活着的敌人 {list.Count} 个（{string.Join("、", list.Select(h => $"{h.name}@{Centre(h):F1} 范围 {Ranges(h).Count} 个"))}）");
            // The place in the air most enemies' ranges take in, touching none, with a floor 4 to its side to throw from.
            Vector2 hang = default, stand = default;
            int best = 0;
            var rangeOf = list.ToDictionary(h => h, Ranges);
            foreach (var h in list)
                foreach (var r in rangeOf[h])
                {
                    var b = r.bounds;
                    for (float x = b.min.x + 0.5f; x < b.max.x; x += 1f)
                        for (float y = b.min.y + 0.5f; y < b.max.y; y += 1f)
                        {
                            var p = new Vector2(x, y);
                            if (TerraWorld.IsSolidAt(p) || Touching(p)) continue;
                            int n = list.Count(e => rangeOf[e].Any(c => c.OverlapPoint(p)));
                            if (n <= best) continue;
                            foreach (float s in new[] { -4f, 4f })
                            {
                                var from = p + new Vector2(s, 0f);
                                if (TerraWorld.IsSolidAt(from)) continue;
                                float floor = float.NaN;
                                for (float fy = from.y; fy > from.y - 4f; fy -= 0.25f)
                                    if (TerraWorld.IsSolidAt(new Vector2(from.x, fy))) { floor = fy + 0.25f; break; }
                                if (float.IsNaN(floor) || Physics2D.Linecast(new Vector2(from.x, floor + 1.2f), p, 1 << ChunkColliders.TerrainLayer).collider != null) continue;
                                best = n;
                                hang = p;
                                stand = new Vector2(from.x, floor);
                                break;
                            }
                        }
                }
            if (best == 0) { check("光剑 找悬停的地方", false, "没有罩在敌人警戒范围里又不碰敌人的空处"); yield break; }
            var covering = list.Where(e => Ranges(e).Any(c => c.OverlapPoint(hang))).ToList();
            note($"光剑 悬停点 {hang:F1}（站在 {stand:F1}）在 {best} 个敌人的警戒范围里：{string.Join("、", covering.Select(e => $"{e.name} 离 {Vector2.Distance(Centre(e), hang):F1}"))}");
            float bottom = box.bounds.min.y - hero.transform.position.y;
            IEnumerator Recall()
            {
                if (BladeSkills.thrown == null) yield break;
                BladeSkills.DevPress = true;
                for (float t = Time.time + 3f; Time.time < t && BladeSkills.thrown != null;) yield return null;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                BladeSkills.DevAnyLayer = pass == 0;
                yield return Recall();
                hero.transform.position = new Vector3(stand.x, stand.y - bottom + 0.05f, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                if (hang.x > stand.x) hero.FaceRight(); else hero.FaceLeft();
                yield return new WaitForSecondsRealtime(0.6f);
                list = Enemies();
                var life = list.ToDictionary(h => h, Life);
                int hits0 = BladeSkills.DevSaberHits, throws0 = BladeSkills.DevThrows;
                mod.DevAim = hang;
                BladeSkills.DevPress = true;
                yield return new WaitForSecondsRealtime(3f);
                var pivot = BladeSkills.RemotePivot(def);
                if (pass == 1) Shot(room, "fix1031b_saber_hang");
                var cut = life.Keys.Where(h => Life(h) < life[h]).ToList();
                Vector2 at = pivot ?? hang;
                var far = cut.Where(h => h != null && Vector2.Distance(Centre(h), at) > 2.5f).ToList();
                string detail = $"扔出 {BladeSkills.DevThrows - throws0}，悬在 {(pivot.HasValue ? pivot.Value.ToString("F1") : "没悬住")}，砍中 {BladeSkills.DevSaberHits - hits0} 下，掉血的敌人 {cut.Count}/{life.Count}（离剑超过 2.5 的 {far.Count}）：" +
                                string.Join("、", cut.Select(h => h == null ? "?" : $"{h.name} 离剑 {Vector2.Distance(Centre(h), at):F1}"));
                if (pass == 0) note($"光剑 （旧写法）{detail}");
                else check("光剑 悬在敌人警戒范围里、不碰敌人", pivot.HasValue && far.Count == 0, detail);
                mod.DevAim = null;
            }
            BladeSkills.DevAnyLayer = false;
            yield return Recall();
            // Thrown onto an enemy: that one is cut.
            list = Enemies();
            var target = list.OrderBy(h => Vector2.Distance(Centre(h), box.bounds.center)).FirstOrDefault();
            if (target != null)
            {
                Vector2 tc = Centre(target);
                hero.transform.position = Clear(hero, new Vector2(tc.x - 3f, tc.y) - new Vector2(0f, bottom));
                rb.linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSecondsRealtime(0.7f);
                list = Enemies();
                var life = list.ToDictionary(h => h, Life);
                int hits0 = BladeSkills.DevSaberHits;
                mod.DevAim = Centre(target);
                BladeSkills.DevPress = true;
                yield return new WaitForSecondsRealtime(2f);
                var cut = life.Keys.Where(h => Life(h) < life[h]).ToList();
                check($"光剑 扔到 {target.name} 身上", cut.Contains(target) || target == null || target.isDead,
                    $"砍中 {BladeSkills.DevSaberHits - hits0} 下，掉血的敌人 {string.Join("、", cut.Select(h => h == null ? "?" : h.name))}");
                yield return Recall();
                mod.DevAim = null;
            }
        }

        IEnumerator Fix1031bShadow(Action<string, bool, string> check, Action<string> note)
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSecondsRealtime(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
            inv.Accessories[0] = ItemStack.Of(ItemType.ShieldOfCthulhu);
            inv.Accessories[1] = ItemStack.Of(ItemType.ShadeCloak);
            inv.RefreshEffective();
            AbilityAccessories.DevDashKey = false;
            IEnumerator Settle()
            {
                for (float t = 0f; t < 4f && (!GameHooks.PlayerHasControl() || Player.TerraMovement.IsDashing || AbilityAccessories.ShadowDashing); t += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
            hero.transform.position = Clear(hero, (Vector2)hero.transform.position + new Vector2(12f, 0f));
            foreach (int dir in new[] { 1, -1 })
            {
                AbilityAccessories.DevReset();
                yield return Settle();
                AbilityAccessories.DevBurst = null;
                Vector2 from = hero.transform.position;
                bool went = AbilityAccessories.TapShadowDash(dir);
                yield return null;
                var b = AbilityAccessories.DevBurst;
                yield return new WaitForSeconds(0.05f);
                Shot("Town", dir > 0 ? "fix1031b_shadow_right" : "fix1031b_shadow_left");
                float dx = b != null ? b.transform.position.x - from.x : 0f, sx = b != null ? b.transform.localScale.x : 0f;
                // HeroDash: 5.21 behind the Knight, x scale -1.92 for a dash to the right.
                check($"黑冲 往{(dir > 0 ? "右" : "左")}", went && b != null && dx * dir < -4f && sx * dir < 0f,
                    $"冲刺 {went}，爆发在人物 {dx:+0.00;-0.00} 处，x 缩放 {sx:F2}（原版：{-5.21f * dir:+0.00;-0.00}、{-1.92f * dir:F2}）");
                yield return Settle();
            }
        }

        IEnumerator Fix1031bHook(Action<string, bool, string> check, Action<string> note)
        {
            string rooms = Environment.GetEnvironmentVariable("TK_HOOK_ROOMS") ?? "GG_Grimm#door_dreamEnter;Grimm_Main_Tent#left1";
            var hook = Player.GrapplingHook.Instance;
            var hook0 = Player.GrapplingHook.DevHook;
            try
            {
                Player.GrapplingHook.DevHook = ItemType.QueenSlimeHook;
                foreach (var entry in rooms.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = entry.Split('#');
                    yield return GoTo(p[0], p.Length > 1 ? p[1] : EntryGateOf(p[0]));
                    yield return new WaitForSecondsRealtime(2.5f);
                    var hero = HeroController.instance;
                    PlayerData.instance.isInvincible = true;
                    var rb = hero.GetComponent<Rigidbody2D>();
                    var box = hero.GetComponent<BoxCollider2D>();
                    // The middle of the Troupe Master's arena (his fight's spot in Grimm_Main_Tent, the same cells in his hall).
                    var spot = CellGrid.CellCenter(170, 13);
                    int heroMask = Physics2D.GetLayerCollisionMask(hero.gameObject.layer) | (1 << ChunkColliders.TerrainLayer);
                    float roof = float.PositiveInfinity;
                    string over = "";
                    IEnumerator Place()
                    {
                        hook.ReleaseAll();
                        hero.transform.position = Clear(hero, spot);
                        rb.linearVelocity = Vector2.zero;
                        yield return new WaitForSecondsRealtime(1f);
                    }
                    yield return Place();
                    Vector2 c0 = box.bounds.center;
                    // What is over him: every solid collider straight up, and the grid's first solid cell.
                    foreach (var h in Physics2D.RaycastAll(c0, Vector2.up, 45f).OrderBy(h => h.distance))
                    {
                        if (h.collider == null || h.collider.isTrigger || h.collider.GetComponentInParent<HeroController>() != null) continue;
                        bool stops = (heroMask & (1 << h.collider.gameObject.layer)) != 0 && !h.collider.usedByEffector && h.collider.GetComponentInParent<HealthManager>() == null;
                        over += $"{h.collider.name}[{h.collider.GetType().Name} 层{h.collider.gameObject.layer}{(stops ? " 挡人" : "")}] y {h.point.y:F1}；";
                        if (stops) roof = Mathf.Min(roof, h.point.y);
                    }
                    float gridRoof = float.PositiveInfinity;
                    for (float y = c0.y; y < c0.y + 45f; y += 0.25f)
                        if (TerraWorld.IsSolidAt(new Vector2(c0.x, y))) { gridRoof = y; break; }
                    note($"勾爪 {p[0]}：人在 {c0:F1}，头顶 {(over.Length > 0 ? over : "没有碰撞体")}，格子顶 {gridRoof:F1}，挡人的最近 {roof:F1}");
                    // As now first, then as before (the first throw in a room is no different from the second); from the floor,
                    // then from 7 over it (a jump's or wings' height, the old hook's reach past the roof).
                    foreach (bool high in new[] { false, true })
                    for (int pass = 1; pass >= 0; pass--)
                    {
                        Player.GrapplingHook.DevTerrainOnly = pass == 0;
                        yield return Place();
                        for (float t = 0f; t < 5f && !GameHooks.PlayerHasControl(); t += Time.deltaTime) yield return null;
                        if (high)
                        {
                            hero.transform.position += new Vector3(0f, 7f, 0f);
                            rb.linearVelocity = Vector2.zero;
                        }
                        Vector2 before = box.bounds.center;
                        int tele0 = Player.GrapplingHook.DevTeleports, thrown0 = Player.GrapplingHook.DevThrown, latch0 = Player.GrapplingHook.DevLatches;
                        hook.Throw(hero, before + new Vector2(0f, 30f));
                        float top = float.NegativeInfinity;
                        var states = new List<string>();
                        string on = "-", state = "None";
                        Vector2 hookAt = default;
                        for (float t = 0f; t < 2.5f && Player.GrapplingHook.DevTeleports == tele0; t += Time.deltaTime)
                        {
                            if (hook.DevOut > 0)
                            {
                                top = Mathf.Max(top, hook.DevHookPosition.y);
                                on = hook.DevLatchedOn;
                                state = hook.DevState;
                                hookAt = hook.DevHookPosition;
                            }
                            if (states.Count == 0 || states[states.Count - 1] != hook.DevState) states.Add(hook.DevState);
                            yield return null;
                        }
                        if (hook.DevOut > 0) { on = hook.DevLatchedOn; state = hook.DevState; hookAt = hook.DevHookPosition; }
                        yield return new WaitForSecondsRealtime(0.3f);
                        Vector2 after = box.bounds.center;
                        if (pass == 1) Shot(p[0], "fix1031b_hook_" + p[0] + (high ? "_high" : ""));
                        string detail = $"{(high ? "半空" : "地面")}：扔出 {Player.GrapplingHook.DevThrown - thrown0}，钩住 {Player.GrapplingHook.DevLatches - latch0} 次 {on}（{state}，钩在 {hookAt:F1}，最高飞到 y {top:F1}，经过 {string.Join("→", states)}），传送 {Player.GrapplingHook.DevTeleports - tele0} 次，人 {before.y:F1}→{after.y:F1}（头顶 {box.bounds.max.y:F1}），挡人的顶 {roof:F1}";
                        if (pass == 0) note($"勾爪 {p[0]} （旧写法）{detail}");
                        else check($"勾爪 {p[0]} 往上钩", float.IsInfinity(roof) ? after.y < gridRoof : box.bounds.max.y <= roof + 0.1f, detail);
                        hook.ReleaseAll();
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                }
            }
            finally
            {
                Player.GrapplingHook.DevTerrainOnly = false;
                Player.GrapplingHook.DevHook = hook0;
                hook.ReleaseAll();
            }
        }
    }
}
