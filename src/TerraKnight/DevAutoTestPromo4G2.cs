using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_v5G_*_zh"): recorder G's mount clips for the 4.4 trailer: the rider sat on the saddled mounts (horse,
    /// unicorn, UFO, Santank, reindeer, golf cart), a horse run up to its charge ramming enemies in its dust, a Chillet's own
    /// dash, the transformations (wolf, bat, pixie); and a scan of rooms for long floors with enemies on them.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        // ------------------------------------------------------------------ floors with enemies

        /// <summary>
        /// The flat floor a point stands over: how far it runs each way (the floor within a third of a unit of its height, no
        /// wall across it a unit up), its ends' x and its height; false with no floor under the point.
        /// </summary>
        static bool GFlat(Vector2 p, out float x0, out float x1, out float y)
        {
            x0 = x1 = p.x;
            y = BFloor(p, 5f);
            if (float.IsNaN(y)) return false;
            float fy = y;
            foreach (int d in new[] { -1, 1 })
            {
                float x = p.x;
                for (int k = 0; k < 160; k++)
                {
                    float nx = x + d * 0.5f;
                    float f = BFloor(new Vector2(nx, fy + 1.2f), 2.5f);
                    if (float.IsNaN(f) || Mathf.Abs(f - fy) > 0.35f) break;
                    if (Physics2D.Raycast(new Vector2(x, fy + 0.9f), Vector2.right * d, 0.5f, BTerrain).collider != null) break;
                    x = nx;
                }
                if (d < 0) x0 = x; else x1 = x;
            }
            return true;
        }

        // ------------------------------------------------------------------ riding

        /// <summary>
        /// One frame of riding along: on the way it goes, a low step jumped, a wall turning it round; a flyer kept `high` over
        /// the floor (hover or flight held while under it). The aim eased ahead.
        /// </summary>
        void GRideStep(HeroController hero, ref int dir, bool fly, float high, ref float hopUntil)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            Vector2 c = col.bounds.center;
            float half = col.bounds.extents.y;
            var feet = new Vector2(c.x, col.bounds.min.y + 0.3f);
            var low = Physics2D.Raycast(feet, Vector2.right * dir, 1.3f, BTerrain);
            var mid = Physics2D.Raycast(c + Vector2.up * 0.4f, Vector2.right * dir, 1.6f, BTerrain);
            var top = Physics2D.Raycast(c + Vector2.up * (half + 2.2f), Vector2.right * dir, 1.8f, BTerrain);
            if (!fly)
            {
                if ((low.collider != null || mid.collider != null) && top.collider == null && hero.cState.onGround && Time.time > hopUntil)
                {
                    Player.TerraMovement.DevGroundJump(hero);
                    hopUntil = Time.time + 0.45f;
                }
                else if (mid.collider != null && top.collider != null) dir = -dir;
                Player.TerraMovement.DevJump = Time.time < hopUntil;
            }
            else
            {
                float want = gFlyY;
                var up = Physics2D.Raycast(c, Vector2.up, 30f, BTerrain);
                if (up.collider != null) want = Mathf.Min(want, up.point.y - 1.4f);
                if (mid.collider != null && Time.time > gTurnAt) { dir = -dir; gTurnAt = Time.time + 1.5f; }
                bool rise = want - c.y > 0.25f || (want - c.y > -0.5f && rb.linearVelocity.y < -2f);
                if (rise && hero.cState.onGround) Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = rise;
            }
            Player.TerraMovement.DevMove = dir;
            SmoothAim(c + new Vector2(dir * 4f, 0.8f), 4f);
        }

        float gFlyY, gTurnAt;

        /// <summary>Off the mount, every held key let go.</summary>
        void GRideStop()
        {
            Player.TerraMovement.DevMove = 0;
            Player.TerraMovement.DevJump = false;
            Player.TerraMovement.DevUp = false;
            Player.TerraMovement.DevDown = false;
        }

        /// <summary>A mount got on (the room's entry or a floor's start), ridden along for the take, got off.</summary>
        IEnumerator GMountRide(string clip, string room, int mount, float seconds, Vector2? start, int dir, bool fly, float high = 3f, int loadout = 3, bool foes = false, int stage = -1)
        {
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            EnemyRespawn.Multiplier = foes ? 6 : 0;
            Mounts.Dismount();
            yield return BEnter(room);
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(loadout);
            yield return null;
            var def0 = Mounts.Get(mount);
            GStageGear(stage >= 0 ? stage : Mathf.Clamp(def0 != null ? def0.Stage : 5, 0, 5), loadout);
            {
                var inv = mod.Inventory;
                for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
                if (def0 != null) { inv.Slots[0] = ItemStack.Of(def0.Item); inv.Misc[Mounts.Slot] = ItemStack.Of(def0.Item); }
                inv.Selected = 0;
            }
            bToughK = foes ? 4f : 1f;
            if (!foes) foreach (var hm in BSceneFoes(room)) hm.gameObject.SetActive(false);
            if (start.HasValue)
            {
                hero.transform.position = Clear(hero, start.Value);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            }
            if (dir > 0) hero.FaceRight(); else hero.FaceLeft();
            yield return new WaitForSeconds(0.6f);
            var def = Mounts.Get(mount);
            bool on = Mounts.TryMount(def, hero);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 坐骑 {clip}：{def?.Name} 骑上 {on}，在 {room} {(Vector2)hero.transform.position:F1}");
            if (!on) yield break;
            var col = hero.GetComponent<BoxCollider2D>();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 4f, 0.8f), 99f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime) { BKeep(); yield return null; }
            gFlyY = col.bounds.center.y + high;
            gTurnAt = 0f;
            StartRec(clip);
            float hop = 0f, x0 = col.bounds.center.x;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                BKeep();
                if (foes) BToughen(BFoes(col.bounds.center, 20f));
                GRideStep(hero, ref dir, fly, high, ref hop);
                yield return null;
            }
            GRideStop();
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { BKeep(); SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 4f, 0.8f), 4f); yield return null; }
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制G 坐骑 {clip}：走了 {col.bounds.center.x - x0:F1}，撞 {Mounts.DevHits}，射 {Mounts.DevShots}，还骑着 {Mounts.Riding}");
            Mounts.Dismount();
        }

        /// <summary>Rooms looked over: their enemies, and each floor with enemies on it (its span, height, how many).</summary>
        IEnumerator P4_v5G_roomscan(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            Player.TerraStats.DevImmortal = true;
            var rooms = new[]
            {
                "Crossroads_01", "Crossroads_03", "Crossroads_04", "Crossroads_05", "Crossroads_07", "Crossroads_10", "Crossroads_13",
                "Crossroads_15", "Crossroads_16", "Crossroads_19", "Crossroads_21", "Crossroads_35", "Crossroads_39", "Crossroads_40",
                "Crossroads_42", "Crossroads_45", "Ruins1_01", "Ruins1_23", "Ruins1_28", "Ruins1_32", "Ruins2_01", "Ruins2_04",
                "Fungus2_03", "Fungus2_04", "Fungus2_12", "Fungus2_18", "Fungus1_10", "Fungus1_21", "Fungus1_22", "Mines_02", "Mines_03",
                "Mines_05", "Mines_19", "Deepnest_East_02", "Deepnest_East_03", "Deepnest_East_04", "Deepnest_East_07", "Abyss_01", "Fungus3_02",
            };
            foreach (var room in rooms)
            {
                yield return EnterRoom(room);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != room) { Plugin.Log.LogInfo($"[AutoTest] 录制G 房间 {room}：没进去"); continue; }
                yield return new WaitForSeconds(1f);
                var foes = BSceneFoes(room);
                var seen = new List<(float, float, float, int, string)>();
                foreach (var hm in foes)
                {
                    var c = BCentre(hm);
                    if (!GFlat(c, out float x0, out float x1, out float y)) continue;
                    if (c.y - y > 3f) continue;
                    bool dup = false;
                    foreach (var s in seen) if (Mathf.Abs(s.Item1 - x0) < 0.6f && Mathf.Abs(s.Item3 - y) < 0.4f) dup = true;
                    if (dup) continue;
                    int n = 0;
                    var names = new System.Text.StringBuilder();
                    foreach (var o in foes)
                    {
                        var oc = BCentre(o);
                        if (oc.x >= x0 - 0.5f && oc.x <= x1 + 0.5f && oc.y > y - 0.5f && oc.y < y + 3f) { n++; names.Append(UI.EnemyHover.RecordOf(o)).Append(','); }
                    }
                    seen.Add((x0, x1, y, n, names.ToString()));
                }
                var sb = new System.Text.StringBuilder();
                foreach (var s in seen) sb.Append($" [{s.Item1:F0}..{s.Item2:F0} 长{s.Item2 - s.Item1:F0} y{s.Item3:F1} 敌{s.Item4} {s.Item5}]");
                var names2 = new System.Text.StringBuilder();
                foreach (var hm in foes) names2.Append(UI.EnemyHover.RecordOf(hm)).Append(' ');
                Plugin.Log.LogInfo($"[AutoTest] 录制G 房间 {room}：敌人 {foes.Count}（{names2}），入口 {(Vector2)hero.transform.position:F0}；地面{sb}");
            }
        }

        /// <summary>
        /// A Chillet's own dash (a direction tapped twice) along a floor of enemies: walked up to them, dashed through in its
        /// frost, turned round, again.
        /// </summary>
        IEnumerator GChilletClip(string clip, string room, int mount, int dashes)
        {
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            EnemyRespawn.Multiplier = 6;
            Mounts.Dismount();
            yield return GFloorRoom(room, 8f, 9f, Mathf.Clamp(Mounts.Get(mount).Stage, 0, 5), 3, ItemType.None);
            var hero = HeroController.instance;
            if (!gFloorOk) yield break;
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(Mounts.Get(mount).Item);
            inv.Misc[Mounts.Slot] = ItemStack.Of(Mounts.Get(mount).Item);
            inv.Selected = 0;
            bool on = Mounts.TryMount(Mounts.Get(mount), hero);
            Plugin.Log.LogInfo($"[AutoTest] 录制G 疾旋鼬 {clip}：骑上 {on}");
            if (!on) yield break;
            var col = hero.GetComponent<BoxCollider2D>();
            int dir = gRowDir, dashes0 = Mounts.DevDashes, hits0 = Mounts.DevHits;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 4f, 0.5f), 99f);
            bToughK = 5f;
            yield return GHold(hero, 0.6f, dir);
            StartRec(clip);
            for (int n = 0; n < dashes; n++)
            {
                // Up to the nearest ahead, a few tiles short of it.
                for (float t = 0f; t < 2.5f; t += Time.deltaTime)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 18f));
                    var c = (Vector2)col.bounds.center;
                    HealthManager ahead = null;
                    float bd = float.MaxValue;
                    foreach (var hm in BFoes(c, 18f))
                    {
                        var p = BCentre(hm);
                        if (Mathf.Abs(p.y - c.y) > 2.5f || Mathf.Sign(p.x - c.x) != dir) continue;
                        float d = Mathf.Abs(p.x - c.x);
                        if (d < bd) { bd = d; ahead = hm; }
                    }
                    Player.TerraMovement.DevMove = dir;
                    SmoothAim(c + new Vector2(dir * 4f, 0.5f), 5f);
                    if (ahead == null && t > 0.6f) break;
                    if (ahead != null && bd < 5f) break;
                    if (Physics2D.Raycast(c, Vector2.right * dir, 2f, BTerrain).collider != null) break;
                    yield return null;
                }
                // The tap, let go and pressed again.
                Player.TerraMovement.DevMove = 0;
                yield return GHold(hero, 0.05f, dir);
                Player.TerraMovement.DevMove = dir;
                int asked0 = Mounts.DevDashes;
                Plugin.Log.LogInfo($"[AutoTest] 录制G 疾旋鼬 冲刺前：骑着 {Mounts.Riding} 疾旋鼬 {Mounts.Chillet} 后退 {hero.cState.recoiling} 能动 {hero.acceptingInput} 地上 {hero.cState.onGround}");
                for (float t = 0f; t < 1.2f && (t < 0.25f || Mounts.Dashing); t += Time.deltaTime)
                {
                    if (Mounts.DevDashes == asked0 && t < 0.25f) Mounts.RequestDash(dir);
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 18f));
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 0.5f), 6f);
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return GHold(hero, 0.35f, dir);
                // Past them or at a wall: back the other way.
                var cc = (Vector2)col.bounds.center;
                bool more = false;
                foreach (var hm in BFoes(cc, 18f)) if (Mathf.Sign(BCentre(hm).x - cc.x) == dir && Mathf.Abs(BCentre(hm).y - cc.y) < 2.5f) more = true;
                if (!more || Physics2D.Raycast(cc, Vector2.right * dir, 3f, BTerrain).collider != null) dir = -dir;
            }
            yield return GHold(hero, 0.4f, dir);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制G 疾旋鼬 {clip}：冲刺 {Mounts.DevDashes - dashes0}，撞到 {Mounts.DevHits - hits0}，击杀 {bKills}");
            Mounts.Dismount();
            bToughK = 1f;
        }

        // ------------------------------------------------------------------ the suites

        IEnumerator P4_v5G_mountride(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            var town = TerrainSystem.Find("Town");
            var o = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            yield return GMountRide("v5G_mount_horse", "Town", 41, 6f, o + new Vector2(18f, 9f), 1, false);
            yield return GMountRide("v5G_mount_unicorn", "Fungus2_12", 10, 5f, new Vector2(53f, 11f), 1, false);
            yield return GMountRide("v5G_mount_ufo", "Town", 7, 5f, o + new Vector2(40f, 9f), 1, true, 3.5f);
            yield return GMountRide("v5G_mount_santank", "Crossroads_39", 46, 4.5f, new Vector2(3f, 6f), 1, false, 3f, 3, true);
            yield return GMountRide("v5G_mount_reindeer", "Mines_02", 0, 5f, new Vector2(14f, 3f), 1, false);
            yield return GMountRide("v5G_mount_golfcart", "Town", 17, 4.5f, o + new Vector2(30f, 9f), 1, false);
        }

        IEnumerator P4_v5G_mountform(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            var town = TerrainSystem.Find("Town");
            var o = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            yield return GMountRide("v5G_mount_wolf", "Crossroads_39", 52, 4.5f, new Vector2(3f, 6f), 1, false);
            yield return GMountRide("v5G_mount_bat", "Town", 56, 5f, o + new Vector2(90f, 9f), 1, true, 4f);
            yield return GMountRide("v5G_mount_pixie", "Fungus3_02", 61, 5f, new Vector2(9.5f, 44.5f), 1, true, 3f);
        }

        IEnumerator P4_v5G_mountfight(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.6f;
            yield return GMountRide("v5G_mount_charge", "Crossroads_39", 40, 4.8f, new Vector2(3f, 6f), 1, false, 3f, 3, true);
            yield return GChilletClip("v5G_mount_chillet", "Crossroads_39", 64, 4);
        }

        /// <summary>Rooms photographed by their thickest crowd on a floor (to pick bright ones for the fights).</summary>
        IEnumerator P4_v5G_roomphotos(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            var rooms = new[]
            {
                "Crossroads_04", "Crossroads_05", "Crossroads_15", "Crossroads_16", "Crossroads_21", "Crossroads_39", "Crossroads_40",
                "Ruins1_01", "Ruins1_28", "Ruins2_04", "Ruins2_01", "Fungus2_03", "Fungus2_12", "Fungus2_18", "Fungus1_22", "Fungus1_10",
                "Mines_02", "Mines_03", "Deepnest_East_02", "Deepnest_East_07", "Fungus3_02", "Crossroads_13",
            };
            foreach (var room in rooms)
            {
                yield return EnterRoom(room);
                hero = HeroController.instance;
                if (GameManager.instance.sceneName != room) continue;
                if (BGroundRow(room, 10f, out var mid, out float floor, out int count))
                {
                    hero.transform.position = Clear(hero, new Vector2(mid.x - 5f, floor + 0.1f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                }
                yield return new WaitForSeconds(1.2f);
                GClearChat();
                yield return null;
                Shot(room, "photo");
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>The mount takes redone alone: the tank and the golf cart, the pixie, the Chillet's dash.</summary>
        IEnumerator P4_v5G_mountfix(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            var town = TerrainSystem.Find("Town");
            var o = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            yield return GChilletClip("v5G_mount_chillet", "Crossroads_39", 64, 4);
            yield return GMountRide("v5G_mount_santank", "Crossroads_39", 46, 4.5f, new Vector2(3f, 6f), 1, false, 3f, 3, true);
            yield return GMountRide("v5G_mount_golfcart", "Town", 17, 4.5f, o + new Vector2(30f, 9f), 1, false);
            yield return GMountRide("v5G_mount_pixie", "Fungus3_02", 61, 5f, new Vector2(9.5f, 44.5f), 1, true, 3f);
        }

        /// <summary>A Chillet's dash along Dirtmouth's lit street: walked a few steps, a direction tapped twice, its frost trail; again.</summary>
        IEnumerator GChilletStreet(string clip, int mount, Vector2 start, int dashes)
        {
            Mounts.Dismount();
            yield return BEnter("Town");
            var hero = HeroController.instance;
            mod.TrySwitchLoadout(3);
            yield return null;
            var def = Mounts.Get(mount);
            GStageGear(Mathf.Clamp(def.Stage, 0, 5), 3);
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(def.Item);
            inv.Misc[Mounts.Slot] = ItemStack.Of(def.Item);
            inv.Selected = 0;
            hero.transform.position = Clear(hero, start);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            hero.FaceRight();
            yield return new WaitForSeconds(0.6f);
            bool on = Mounts.TryMount(def, hero);
            if (!on) yield break;
            var col = hero.GetComponent<BoxCollider2D>();
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(4f, 0.5f), 99f);
            yield return GHold(hero, 0.6f, 1);
            int dashes0 = Mounts.DevDashes;
            StartRec(clip);
            for (int n = 0; n < dashes; n++)
            {
                yield return GStep(hero, 0.55f, 1);
                Player.TerraMovement.DevMove = 0;
                yield return GHold(hero, 0.05f, 1);
                Player.TerraMovement.DevMove = 1;
                int asked0 = Mounts.DevDashes;
                for (float t = 0f; t < 1.2f && (t < 0.25f || Mounts.Dashing); t += Time.deltaTime)
                {
                    if (Mounts.DevDashes == asked0 && t < 0.25f) Mounts.RequestDash(1);
                    BKeep();
                    SmoothAim((Vector2)col.bounds.center + new Vector2(5f, 0.5f), 6f);
                    yield return null;
                }
                Player.TerraMovement.DevMove = 0;
                yield return GHold(hero, 0.3f, 1);
            }
            yield return GHold(hero, 0.4f, 1);
            StopRec();
            Plugin.Log.LogInfo($"[AutoTest] 录制G 疾旋鼬街道 {clip}：冲刺 {Mounts.DevDashes - dashes0}，还骑着 {Mounts.Riding}，在 {(Vector2)hero.transform.position:F1}");
            Mounts.Dismount();
        }

        IEnumerator P4_v5G_mountfix2(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.8f;
            var town = TerrainSystem.Find("Town");
            var o = town != null ? (Vector2)town.CellOffset * CellGrid.CellSize : Vector2.zero;
            yield return GChilletStreet("v5G_mount_chillet_street", 64, o + new Vector2(22f, 9f), 4);
            Player.TerraCamera.Zoom = 1.7f;
            yield return GMountRide("v5G_mount_pixie", "Mines_02", 61, 5f, new Vector2(14f, 3f), 1, true, 3f);
        }
    }
}
