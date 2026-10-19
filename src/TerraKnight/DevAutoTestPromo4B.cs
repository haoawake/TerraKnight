using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_b*_zh"): recorder B's clips for the 4.0 trailer: the four classes' weapons against crowds and a
    /// boss each, the movement (dashes, wings, hook, Gravity Globe, the nail at the mouse, the zoom) and the pets.
    /// The fights are played the way a person would: the aim eased onto the enemy being fought, the hero keeping a
    /// distance on one side of it and now and then flying over to the other, rising on wings.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // ------------------------------------------------------------------ the crowd

        /// <summary>Enemies' health multiplied as they come (so a crowd stands up to the weapon a few seconds); 1: as they are.</summary>
        float bToughK = 1f;
        readonly Dictionary<int, HealthManager> bTough = new Dictionary<int, HealthManager>();
        int bKills;

        /// <summary>The enemies that can be fought near a point: alive, shown, with a collider.</summary>
        static List<HealthManager> BFoes(Vector2 from, float range)
        {
            var list = new List<HealthManager>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy) continue;
                var c = hm.GetComponent<Collider2D>();
                if (c == null || !c.enabled) continue;
                if (Vector2.Distance(c.bounds.center, from) > range) continue;
                list.Add(hm);
            }
            return list;
        }

        static Vector2 BCentre(HealthManager hm)
        {
            var c = hm.GetComponent<Collider2D>();
            return c != null ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
        }

        /// <summary>The newcomers among the enemies toughened (once each); the toughened ones fallen since counted as kills.</summary>
        void BToughen(List<HealthManager> foes)
        {
            foreach (var hm in foes)
            {
                int id = hm.GetInstanceID();
                if (bTough.ContainsKey(id)) continue;
                bTough[id] = hm;
                if (bToughK > 1f && !EnemyStats.Of(hm).Boss) hm.hp = Mathf.CeilToInt(hm.hp * bToughK);
            }
            List<int> gone = null;
            foreach (var kv in bTough)
                if (kv.Value == null || kv.Value.isDead || kv.Value.hp <= 0) (gone = gone ?? new List<int>()).Add(kv.Key);
            if (gone != null)
                foreach (int id in gone)
                {
                    bKills++;
                    bTough.Remove(id);
                }
        }

        /// <summary>An area's title (shown on coming into an area) waited out.</summary>
        IEnumerator BWaitTitle()
        {
            // It can come some seconds after the room: watched for a while, then waited out if it came.
            bool seen = false;
            float t = 0f;
            for (; t < 10f; t += Time.deltaTime)
            {
                BKeep();
                bool up = AreaTitle.instance != null && AreaTitle.instance.gameObject.activeInHierarchy;
                var f = up ? FSMUtility.GetFSM(AreaTitle.instance.gameObject) : null;
                if (up) seen = true;
                if (!up && (seen || t > 7.5f)) break;
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 录制B 区域标题：{(seen ? "出现过" : "没有")}，等了 {t:F1}s");
        }

        // ------------------------------------------------------------------ the fight, played

        int bSide = 1, bMove;
        float bSwapAt, bLift = 1.5f, bLiftAt;

        /// <summary>The hero kept alive and full of mana (Terraria's damage too: the late enemies hit hard).</summary>
        void BKeep()
        {
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.Stats.Mana = mod.Stats.ManaMax;
            if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
        }

        /// <summary>
        /// One frame of moving like a player against a point: a steady distance on our side of it, a little above, held by
        /// the wings; every few seconds over to its other side (high over it, clear of its body); walls turn us round.
        /// </summary>
        void BSteer(HeroController hero, Vector2 at, float dist, bool roam = true)
        {
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            Vector2 c = col.bounds.center;
            if (roam && Time.time > bSwapAt)
            {
                bSide = -bSide;
                bSwapAt = Time.time + Random.Range(2.6f, 3.8f);
            }
            if (Time.time > bLiftAt)
            {
                bLift = Random.Range(0.6f, 3.2f);
                bLiftAt = Time.time + Random.Range(1.4f, 2.6f);
            }
            // A wall close behind the side we keep: the other side.
            var wall = Physics2D.Raycast(new Vector2(at.x, c.y), Vector2.right * bSide, dist + 1.2f, terrain);
            if (wall.collider != null)
            {
                bSide = -bSide;
                bSwapAt = Time.time + Random.Range(2.6f, 3.8f);
            }
            float wantX = at.x + bSide * dist;
            var down = Physics2D.Raycast(c, Vector2.down, 40f, terrain);
            var up = Physics2D.Raycast(c, Vector2.up, 40f, terrain);
            float ground = down.collider != null ? down.point.y : c.y - 6f;
            float ceiling = up.collider != null ? up.point.y : c.y + 30f;
            // Crossing over: high over it, so we never touch it.
            bool crossing = Mathf.Abs(c.x - at.x) < dist * 0.8f && Mathf.Sign(c.x - at.x) != bSide;
            float wantY = at.y + (crossing ? 4.5f : bLift);
            wantY = Mathf.Clamp(wantY, ground + 1.6f, Mathf.Max(ground + 1.6f, ceiling - 1.8f));
            float dx = wantX - c.x;
            if (Mathf.Abs(dx) > 1.1f) bMove = dx > 0 ? 1 : -1;
            else if (Mathf.Abs(dx) < 0.3f) bMove = 0;
            Player.TerraMovement.DevMove = bMove;
            float dy = wantY - c.y, vy = rb.linearVelocity.y;
            bool rise = dy > 0.35f || (dy > -0.7f && vy < -1.5f);
            if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
            Player.TerraMovement.DevJump = rise;
        }

        /// <summary>A row of enemies on a floor to fight over (its middle's x, the floor's y), and how far each way.</summary>
        Vector2? bRow;
        float bRowSpan = 7f, bPatrolY = 2f;
        int bPatrolDir = 1;

        /// <summary>
        /// One frame of strafing over the row: run and fly from one end to the other and back (a wall turns us early),
        /// each pass at its own height, low over the floor or high over the enemies' heads.
        /// </summary>
        void BPatrol(HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            Vector2 c = col.bounds.center, row = bRow.Value;
            bool turn = (bPatrolDir > 0 && c.x > row.x + bRowSpan) || (bPatrolDir < 0 && c.x < row.x - bRowSpan)
                || Physics2D.Raycast(c, Vector2.right * bPatrolDir, 1.1f, BTerrain).collider != null;
            if (turn)
            {
                bPatrolDir = -bPatrolDir;
                bPatrolY = bPatrolY < 3f ? Random.Range(3.8f, 5f) : Random.Range(1.6f, 2.6f);
            }
            Player.TerraMovement.DevMove = bPatrolDir;
            var up = Physics2D.Raycast(c, Vector2.up, 40f, BTerrain);
            float ceiling = up.collider != null ? up.point.y : c.y + 30f;
            float wantY = Mathf.Min(row.y + bPatrolY, ceiling - 1.8f);
            float dy = wantY - c.y, vy = rb.linearVelocity.y;
            bool rise = dy > 0.3f || (dy > -0.7f && vy < -1.5f);
            if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
            Player.TerraMovement.DevJump = rise;
        }

        void BStop()
        {
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            bMove = 0;
            mod.DevHold = null;
        }

        /// <summary>
        /// A weapon on the room's enemies for so many seconds of fighting (the time with none about isn't counted, up to
        /// a limit): the nearest one taken and kept till it falls, the aim eased onto it, the weapon held down; a summon
        /// staff used `uses` times first.
        /// </summary>
        IEnumerator BBrawl(HeroController hero, ItemType weapon, float seconds, float dist, int uses = 0, float maxWait = 12f, System.Action<float> marks = null)
        {
            var inv = mod.Inventory;
            var col = hero.GetComponent<BoxCollider2D>();
            // An item out (a yoyo, a flail) comes back first; the hero hovers meanwhile.
            mod.DevHold = null;
            for (float t = 0f; t < 1.5f && mod.IsUsingItem; t += Time.deltaTime)
            {
                BKeep();
                yield return null;
            }
            int slot = SlotOf(weapon);
            if (slot < 0)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制B 背包里没有 {weapon}");
                yield break;
            }
            inv.Selected = slot;
            var def = ItemDb.Get(weapon);
            float fought = 0f, waited = 0f;
            HealthManager target = null;
            int used = 0;
            while (fought < seconds && waited < maxWait)
            {
                BKeep();
                Vector2 c = col.bounds.center;
                var foes = BFoes(c, 26f);
                BToughen(foes);
                if (target == null || target.isDead || !foes.Contains(target) || (bRow.HasValue && Vector2.Distance(BCentre(target), c) > 9f))
                {
                    target = null;
                    float bd = float.MaxValue;
                    foreach (var hm in foes)
                    {
                        var p = BCentre(hm);
                        float d = Vector2.Distance(p, c);
                        // Over a row: its enemies first (the ones on the floor, ahead of us more than behind).
                        if (bRow.HasValue)
                        {
                            if (Mathf.Abs(p.y - bRow.Value.y) > 4f || Mathf.Abs(p.x - bRow.Value.x) > bRowSpan + 6f) d += 30f;
                            if (Mathf.Sign(p.x - c.x) != bPatrolDir) d += 2.5f;
                        }
                        if (d < bd) { bd = d; target = hm; }
                    }
                }
                if (target != null)
                {
                    fought += Time.deltaTime;
                    Vector2 at = BCentre(target);
                    SmoothAim(at, 9f);
                    mod.DevHold = true;
                    if (mod.DevReady && !mod.IsUsingItem && (!def.IsSummon || used < uses))
                    {
                        mod.DevUseSelected(aimNow);
                        used++;
                    }
                    if (bRow.HasValue && dist < 5f) BPatrol(hero);
                    else if (bRow.HasValue) BSteer(hero, new Vector2(bRow.Value.x, bRow.Value.y + 1.2f), dist);
                    else BSteer(hero, at, dist);
                }
                else if (bRow.HasValue)
                {
                    waited += Time.deltaTime;
                    mod.DevHold = false;
                    SmoothAim(c + new Vector2(bPatrolDir * 5f, -1f), 4f);
                    if (dist < 5f) BPatrol(hero); else BSteer(hero, new Vector2(bRow.Value.x, bRow.Value.y + 1.2f), dist);
                }
                else
                {
                    waited += Time.deltaTime;
                    mod.DevHold = false;
                    // Nothing about: hover where we are, looking ahead.
                    SmoothAim(c + new Vector2(-bSide * 5f, 0.8f), 4f);
                    BSteer(hero, c + new Vector2(-bSide * dist, -bLift), dist, false);
                }
                yield return null;
            }
            mod.DevHold = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制B {def.Name}：打了 {fought:F1}s，等了 {waited:F1}s，至今击杀 {bKills}");
        }

        // ------------------------------------------------------------------ the Colosseum

        /// <summary>
        /// A Colosseum trial begun: its flag set (paid for), its arena entered, the hero set down by the arena's middle
        /// (where the game's own detectors start it), until its waves start.
        /// </summary>
        IEnumerator BColosseum(string arena)
        {
            var pd = PlayerData.instance;
            pd.SetBool("seenColosseumTitle", true);
            pd.SetBool("colosseum" + arena + "Opened", true);
            string room = "Room_Colosseum_" + arena;
            yield return EnterRoom(room);
            var hero = HeroController.instance;
            PlayMakerFSM manager = null, battle = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.gameObject.scene.name == room && f.name == "Colosseum Manager")
                {
                    if (f.FsmName == "Manager") manager = f;
                    if (f.FsmName == "Battle Control") battle = f;
                }
            if (manager == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 录制B {room} 找不到 Colosseum Manager");
                yield break;
            }
            Vector2 mid = manager.transform.position;
            hero.transform.position = Clear(hero, new Vector2(mid.x - 14f, mid.y - 4f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.4f);
            float t0 = Time.time;
            while (Time.time - t0 < 25f && (battle == null || battle.ActiveStateName == "Idle" || battle.ActiveStateName == "Init"))
            {
                BKeep();
                var c = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
                Player.TerraMovement.DevMove = c.x < mid.x - 1f ? 1 : 0;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            Plugin.Log.LogInfo($"[AutoTest] 录制B {room}：{Time.time - t0:F1}s 后 管理 {manager.ActiveStateName}，战斗 {battle?.ActiveStateName}");
        }

        /// <summary>Test: the Bronze trial with two melee weapons, recorded as one take to look at.</summary>
        IEnumerator P4_bcoltest(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            yield return BColosseum("Bronze");
            hero = HeroController.instance;
            mod.TrySwitchLoadout(0);
            Player.TerraCamera.Zoom = 1.6f;
            bToughK = 10f;
            aimInit = false;
            // The first wave down from its cage first.
            float t0 = Time.time;
            while (Time.time - t0 < 10f && BFoes(hero.transform.position, 30f).Count == 0) { BKeep(); yield return null; }
            StartRec("bcoltest");
            yield return BBrawl(hero, ItemType.Zenith, 7f, 3.5f);
            yield return BBrawl(hero, ItemType.StarWrath, 7f, 4f);
            yield return BBrawl(hero, ItemType.Terrarian, 7f, 4.5f);
            BStop();
            yield return new WaitForSeconds(0.5f);
            StopRec();
            Player.TerraCamera.Zoom = 2f;
        }

        // ------------------------------------------------------------------ busy rooms

        /// <summary>Dev: rooms looked over for crowds (enemies four to an original): how many, how many in one view, of what.</summary>
        IEnumerator P4_bscan(HeroController hero)
        {
            yield return P4Kit();
            Player.TerraStats.DevImmortal = true;
            EnemyRespawn.Multiplier = 4;
            var rooms = new[]
            {
                "Crossroads_07", "Crossroads_19", "Crossroads_21", "Crossroads_13", "Crossroads_16", "Crossroads_05",
                "Fungus1_22", "Fungus1_31", "Fungus1_21", "Fungus1_19", "Fungus1_10",
                "Fungus2_12", "Fungus2_14", "Fungus2_18", "Fungus2_28", "Fungus2_03", "Fungus2_06",
                "Fungus3_10", "Fungus3_22", "Fungus3_48", "Fungus3_04", "Fungus3_05",
                "Ruins1_32", "Ruins1_28", "Ruins1_01", "Ruins1_03", "Ruins2_01", "Ruins2_04", "Ruins2_06",
                "Mines_02", "Mines_03", "Mines_05", "Mines_25", "Mines_20",
                "Deepnest_East_03", "Deepnest_East_04", "Deepnest_East_06", "Deepnest_East_07", "Deepnest_East_11", "Deepnest_East_14",
                "Hive_01", "Hive_02", "Hive_03", "Hive_04",
                "Waterways_01", "Waterways_02", "Waterways_04",
                "Abyss_04", "Abyss_17", "Deepnest_35", "Deepnest_26", "RestingGrounds_06",
            };
            foreach (var room in rooms)
            {
                yield return EnterRoom(room);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != room) { Plugin.Log.LogInfo($"[AutoTest] B扫描 {room}：没进去"); continue; }
                yield return new WaitForSeconds(1.5f);
                var foes = new List<HealthManager>();
                foreach (var hm in BFoes(hero.transform.position, 400f)) if (hm.gameObject.scene.name == room) foes.Add(hm);
                int best = 0;
                Vector2 bestAt = hero.transform.position;
                foreach (var a in foes)
                {
                    var pa = BCentre(a);
                    int n = 0;
                    foreach (var b in foes) { var pb = BCentre(b); if (Mathf.Abs(pb.x - pa.x) < 17f && Mathf.Abs(pb.y - pa.y) < 9f) n++; }
                    if (n > best) { best = n; bestAt = pa; }
                }
                var kinds = new Dictionary<string, int>();
                foreach (var hm in foes) { string k = hm.name.Split('(')[0].Trim(); kinds[k] = kinds.TryGetValue(k, out int v) ? v + 1 : 1; }
                var sb = new System.Text.StringBuilder();
                foreach (var kv in kinds) sb.Append($" {kv.Key}×{kv.Value}");
                Plugin.Log.LogInfo($"[AutoTest] B扫描 {room}：{foes.Count} 只，一屏最多 {best}（{bestAt:F0}），{sb}");
                hero.transform.position = Clear(hero, bestAt + Vector2.up * 1f);
                yield return new WaitForSeconds(0.5f);
                Shot(room, "bscan");
            }
        }

        // ------------------------------------------------------------------ the parades

        /// <summary>The middle of the room's thickest crowd (the most enemies in one view of it), and how many.</summary>
        static Vector2 BCluster(string room, out int most)
        {
            var foes = new List<HealthManager>();
            foreach (var hm in BFoes(Vector2.zero, 100000f)) if (hm.gameObject.scene.name == room) foes.Add(hm);
            most = 0;
            Vector2 best = HeroController.instance != null ? (Vector2)HeroController.instance.transform.position : Vector2.zero;
            foreach (var a in foes)
            {
                var pa = BCentre(a);
                int n = 0;
                Vector2 sum = Vector2.zero;
                foreach (var b in foes) { var pb = BCentre(b); if (Mathf.Abs(pb.x - pa.x) < 14f && Mathf.Abs(pb.y - pa.y) < 7f) { n++; sum += pb; } }
                if (n > most) { most = n; best = sum / n; }
            }
            return best;
        }

        /// <summary>A room of the parade: where, and the weapons shown in it (each its own clip).</summary>
        struct BStop2 { public string Room; public ItemType[] Weapons; public float Dist; public float Tough; }

        /// <summary>Added to the parade's clip names (a second take of a weapon elsewhere).</summary>
        string bClipSuffix = "";

        static BStop2 BAt(string room, float dist, float tough, params ItemType[] weapons) => new BStop2 { Room = room, Weapons = weapons, Dist = dist, Tough = tough };

        /// <summary>
        /// A class's weapons, each a clip "class_&lt;cls&gt;_&lt;weapon&gt;": in each room (its enemies four to one) the hero set down by
        /// its thickest crowd, then every weapon of it fought with a few seconds on wings that never tire (the Soaring
        /// Insignia worn), the view widened a little, centred on the hero.
        /// </summary>
        IEnumerator BParade(string cls, int loadout, float seconds, BStop2[] stops, ItemType[] extra = null)
        {
            bool camWas = Player.TerraCamera.Enabled;
            Player.TerraCamera.Enabled = false;
            // The fallen come back soon (their ghosts first), so a crowd stays a crowd.
            EnemyRespawn.DevFixedSeconds = 2.5f;
            foreach (var stop in stops)
            {
                EnemyRespawn.Multiplier = 6;
                Player.TerraCamera.Zoom = 2f;
                bool arena = stop.Room.StartsWith("Colosseum_");
                string scene = arena ? "Room_" + stop.Room : stop.Room;
                if (arena) yield return BColosseum(stop.Room.Substring(10));
                else yield return EnterRoom(stop.Room);
                var hero = HeroController.instance;
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 录制B {stop.Room} 没进去"); continue; }
                yield return BWaitTitle();
                mod.TrySwitchLoadout(loadout);
                yield return null;
                if (extra != null)
                    foreach (var t in extra)
                        if (SlotOf(t) < 0)
                            for (int i = 0; i < Inventory.Size; i++) if (mod.Inventory.Slots[i].IsEmpty) { mod.Inventory.Slots[i] = ItemStack.Of(t); break; }
                BWear(ItemType.EmpressFlightBooster);
                yield return new WaitForSeconds(0.8f);
                if (arena)
                    for (float t = 0f; t < 10f && BSceneFoes(scene).Count == 0; t += Time.deltaTime) { BKeep(); yield return null; }
                var at = BCluster(scene, out int most);
                bRow = null;
                if (!arena && BGroundRow(scene, 8f, out var rowMid, out float rowFloor, out int inRow) && inRow >= 3)
                {
                    // The row's floor, as far each way as it's clear (7 at most).
                    float Free(int d) { var h = Physics2D.Raycast(new Vector2(rowMid.x, rowFloor + 1.2f), Vector2.right * d, 12f, BTerrain); return h.collider != null ? h.distance - 1.2f : 12f; }
                    bRowSpan = Mathf.Clamp(Mathf.Min(Free(-1), Free(1)), 3f, 7f);
                    bRow = new Vector2(rowMid.x, rowFloor);
                    at = rowMid;
                    most = inRow;
                }
                bSide = Physics2D.Raycast(at, Vector2.left, stop.Dist + 2f, BTerrain).collider == null ? -1 : 1;
                bPatrolDir = -bSide;
                bPatrolY = 2f;
                hero.transform.position = Clear(hero, bRow.HasValue ? new Vector2(at.x + bSide * (stop.Dist < 5f ? bRowSpan : stop.Dist), bRow.Value.y + 0.2f) : at + new Vector2(bSide * stop.Dist, 1.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                bSwapAt = Time.time + 4f;
                bToughK = stop.Tough;
                BToughen(BFoes(at, 40f));
                Plugin.Log.LogInfo($"[AutoTest] 录制B {cls} {stop.Room}：人群 {most} 只在 {at:F1}，{(bRow.HasValue ? $"地上一排，宽 {bRowSpan * 2:F0}" : "不是一排")}");
                aimInit = false;
                // Up on the wings, the view eased out.
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    BKeep();
                    Player.TerraCamera.Zoom = Mathf.Lerp(2f, 1.6f, t / 1.2f);
                    if (bRow.HasValue && stop.Dist < 5f) BPatrol(hero); else BSteer(hero, at, stop.Dist, false);
                    SmoothAim(at, 6f);
                    yield return null;
                }
                foreach (var w in stop.Weapons)
                {
                    string name = w.ToString().ToLower();
                    // Into the fight first (the weapon out, aimed), then recorded.
                    float t0 = Time.time;
                    while (Time.time - t0 < 6f && BFoes(hero.GetComponent<BoxCollider2D>().bounds.center, 18f).Count < 2)
                    {
                        BKeep();
                        var c2 = BCluster(scene, out _);
                        if (bRow.HasValue && stop.Dist < 5f) BPatrol(hero); else BSteer(hero, bRow.HasValue ? new Vector2(bRow.Value.x, bRow.Value.y + 1.2f) : c2, stop.Dist);
                        SmoothAim(c2, 5f);
                        yield return null;
                    }
                    // A summon's clip shows its own minions only.
                    if (ItemDb.Get(w).IsSummon) Minions.Instance?.Clear();
                    for (float tt = 0f; tt < 8f && AreaTitle.instance != null && AreaTitle.instance.gameObject.activeInHierarchy; tt += Time.deltaTime) { BKeep(); yield return null; }
                    bool summon = ItemDb.Get(w).IsSummon;
                    yield return BBrawl(hero, w, summon ? 1.6f : 0.4f, stop.Dist, summon ? (w == ItemType.StardustDragonStaff ? 5 : 3) : 0);
                    int kills0 = bKills;
                    StartRec($"class_{cls}_{name}{bClipSuffix}");
                    yield return BBrawl(hero, w, seconds, stop.Dist, 1, 3f);
                    StopRec();
                    Plugin.Log.LogInfo($"[AutoTest] 录制B 片段 class_{cls}_{name}{bClipSuffix}：{stop.Room}，击杀 {bKills - kills0}");
                }
                BStop();
                bRow = null;
            }
            bToughK = 1f;
            Player.TerraCamera.Zoom = 2f;
            Player.TerraCamera.Enabled = camWas;
            EnemyRespawn.DevFixedSeconds = -1f;
        }

        IEnumerator P4_bmelee(HeroController hero)
        {
            yield return P4Kit();
            yield return BParade("melee", 0, 4.5f, new[]
            {
                BAt("Crossroads_05", 3.5f, 6f, ItemType.StarWrath, ItemType.DayBreak),
                BAt("Fungus1_10", 4.5f, 6f, ItemType.Terrarian, ItemType.InfluxWaver),
                BAt("Ruins1_28", 4f, 6f, ItemType.SolarEruption, ItemType.Zenith),
            });
        }

        IEnumerator P4_branged(HeroController hero)
        {
            yield return P4Kit();
            yield return BParade("ranged", 1, 4.5f, new[]
            {
                BAt("Ruins1_01", 6f, 3f, ItemType.SDMG, ItemType.Phantasm),
                BAt("Hive_03", 6f, 3f, ItemType.CelebrationMk2, ItemType.VortexBeater),
                BAt("Deepnest_East_11", 6f, 3f, ItemType.SnowmanCannon, ItemType.Xenopopper),
                BAt("Abyss_17", 6f, 3f, ItemType.Tsunami, ItemType.ChainGun),
            });
        }

        IEnumerator P4_bmagic(HeroController hero)
        {
            yield return P4Kit();
            yield return BParade("magic", 2, 4.5f, new[]
            {
                BAt("Fungus2_28", 6f, 3f, ItemType.LastPrism, ItemType.LunarFlareBook),
                BAt("Mines_05", 6f, 3f, ItemType.NebulaBlaze, ItemType.RazorbladeTyphoon),
                BAt("Crossroads_16", 6f, 3f, ItemType.LaserMachinegun, ItemType.RainbowGun),
                BAt("Fungus2_12", 6f, 3f, ItemType.NebulaArcanum, ItemType.ChargedBlasterCannon),
            }, new[] { ItemType.RainbowGun });
        }

        /// <summary>The ranged, magic and summon parades in one go, and the melee weapons that wanted another room.</summary>
        IEnumerator P4_bclasses(HeroController hero)
        {
            yield return P4_branged(hero);
            yield return P4_bmagic(hero);
            yield return P4_bsummon(hero);
            yield return BParade("melee", 0, 4.5f, new[] { BAt("Ruins2_04", 4.5f, 6f, ItemType.Terrarian, ItemType.InfluxWaver) });
        }

        /// <summary>The takes redone in better rooms (bright halls with a row of enemies on the floor).</summary>
        IEnumerator P4_bredo(HeroController hero)
        {
            yield return P4Kit();
            yield return BParade("ranged", 1, 4.5f, new[]
            {
                BAt("Ruins1_01", 7f, 3f, ItemType.SDMG, ItemType.Phantasm),
                BAt("Ruins2_04", 7f, 3f, ItemType.Tsunami, ItemType.ChainGun),
            });
            yield return BParade("magic", 2, 4.5f, new[]
            {
                BAt("Ruins1_28", 7.5f, 3f, ItemType.LastPrism, ItemType.LunarFlareBook),
                BAt("Crossroads_21", 7f, 3f, ItemType.NebulaBlaze, ItemType.RazorbladeTyphoon),
            });
            yield return BParade("summon", 3, 4.5f, new[]
            {
                BAt("Crossroads_05", 4f, 3f, ItemType.Kaleidoscope, ItemType.DesertTigerStaff),
            });
        }

        /// <summary>A take of each class in the Colosseum of Fools' trials (its waves coming down from their cages).</summary>
        IEnumerator P4_bcolo(HeroController hero)
        {
            yield return P4Kit();
            bClipSuffix = "_colo";
            yield return BParade("melee", 0, 5f, new[] { BAt("Colosseum_Bronze", 3.5f, 3f, ItemType.Zenith, ItemType.Meowmere) });
            yield return BParade("ranged", 1, 5f, new[] { BAt("Colosseum_Silver", 7f, 3f, ItemType.CelebrationMk2, ItemType.SDMG) });
            yield return BParade("magic", 2, 5f, new[] { BAt("Colosseum_Gold", 7f, 3f, ItemType.LastPrism, ItemType.NebulaBlaze) });
            yield return BParade("summon", 3, 5f, new[] { BAt("Colosseum_Bronze", 5f, 3f, ItemType.StardustDragonStaff, ItemType.Kaleidoscope) });
            bClipSuffix = "";
        }

        IEnumerator P4_bredo2(HeroController hero)
        {
            yield return P4Kit();
            yield return BParade("ranged", 1, 4.5f, new[] { BAt("Mines_03", 7f, 3f, ItemType.Tsunami, ItemType.ChainGun) });
        }

        IEnumerator P4_bsummon(HeroController hero)
        {
            yield return P4Kit();
            yield return BParade("summon", 3, 4.5f, new[]
            {
                BAt("Fungus2_06", 5f, 3f, ItemType.StardustDragonStaff, ItemType.Terraprisma),
                BAt("Crossroads_13", 5f, 3f, ItemType.LunarPortalStaff, ItemType.RainbowCrystalStaff),
                BAt("Ruins1_03", 3.5f, 3f, ItemType.Kaleidoscope, ItemType.DesertTigerStaff),
                BAt("Deepnest_East_04", 5f, 3f, ItemType.XenoStaff, ItemType.StardustCellStaff),
            });
        }
    }
}
