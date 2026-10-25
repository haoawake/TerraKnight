using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "p4_v5G_*_zh"): recorder G's clips of the ability shields and the nail arts for the 4.4 trailer, in gear of
    /// their stage: the shield's bash, the Crystal Heart's super dash, the Shade Cloak's shadow dash (its key and a direction
    /// tapped twice) through enemies, the dive and the Descending Dark's smash onto them; the Nail's Great Slash, Cyclone
    /// Slash and Dash Slash.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>A floor with enemies on it: entered, the hero set down at `back` from their middle on the side with room.</summary>
        IEnumerator GFloorRoom(string room, float span, float back, int stage, int cls, ItemType shield)
        {
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            EnemyRespawn.Multiplier = 6;
            yield return BEnter(room);
            var hero = HeroController.instance;
            for (int k = 0; k < 20 && !mod.TrySwitchLoadout(cls); k++) yield return null;
            yield return null;
            GStageGear(stage, cls);
            var inv = mod.Inventory;
            if (shield != ItemType.None)
            {
                // The shield in the last slot the stage's kit left free (it is the dash: no other dash worn with it).
                int at = -1;
                for (int i = 0; i < inv.Accessories.Length; i++) if (inv.Accessories[i].IsEmpty) { at = i; break; }
                if (at < 0) at = inv.Accessories.Length - 1;
                for (int i = 0; i < inv.Accessories.Length; i++)
                    if (!inv.Accessories[i].IsEmpty && (inv.Accessories[i].Type == ItemType.CobaltShield || inv.Accessories[i].Type == ItemType.FrozenShield)) { inv.Accessories[i] = default; if (at > i) at = i; }
                inv.Accessories[at] = ItemStack.Of(shield);
                inv.RefreshEffective();
                Equipment.Update(inv);
            }
            GStageBag(gStage, cls);
            gFloorOk = BGroundRow(room, span, out gRowMid, out gRowFloor, out gRowCount);
            if (!gFloorOk) { Plugin.Log.LogInfo($"[AutoTest] 录制G {room}：地上没有怪"); yield break; }
            float Room(int d) { var h = Physics2D.Raycast(new Vector2(gRowMid.x, gRowFloor + 0.8f), Vector2.right * d, 60f, BTerrain); return h.collider != null ? h.distance : 60f; }
            gRowDir = Room(-1) > Room(1) ? 1 : -1;
            float b = Mathf.Min(back, Room(-gRowDir) - 1.5f);
            hero.transform.position = Clear(hero, new Vector2(gRowMid.x - gRowDir * b, gRowFloor + 0.1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            if (gRowDir > 0) hero.FaceRight(); else hero.FaceLeft();
            Items.AbilityAccessories.DevReset();
            yield return new WaitForSeconds(0.8f);
            Plugin.Log.LogInfo($"[AutoTest] 录制G {room}：{gRowCount} 只在 {gRowMid:F1}，地面 {gRowFloor:F1}，往{(gRowDir > 0 ? "右" : "左")}，后退 {b:F1}");
        }

        bool gFloorOk;
        Vector2 gRowMid;
        float gRowFloor;
        int gRowCount, gRowDir = 1;

        /// <summary>A few frames held still (alive, mana full), the aim eased ahead.</summary>
        IEnumerator GHold(HeroController hero, float seconds, int dir)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                BKeep();
                BToughen(BFoes(col.bounds.center, 16f));
                SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 0.3f), 5f);
                yield return null;
            }
        }

        /// <summary>Walked a moment the way it goes (the aim ahead).</summary>
        IEnumerator GStep(HeroController hero, float seconds, int dir)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                BKeep();
                BToughen(BFoes(col.bounds.center, 16f));
                Player.TerraMovement.DevMove = dir;
                SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 0.3f), 5f);
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
        }

        /// <summary>
        /// The shadow dash through the row and back: by its key (`tap` false) or by a direction tapped twice (true), each after
        /// a step or two toward them, turned round once its cooldown is over.
        /// </summary>
        IEnumerator GShadowDash(HeroController hero, string clip, int dashes, bool tap)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            int dir = gRowDir, done = 0;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 0.3f), 99f);
            bToughK = 6f;
            StartRec(clip);
            for (int n = 0; n < dashes; n++)
            {
                yield return GStep(hero, 0.4f, dir);
                int before = Items.AbilityAccessories.DevShadowDashes;
                if (tap)
                {
                    // The direction let go and pressed again: the second press dashes.
                    Player.TerraMovement.DevMove = 0;
                    yield return GHold(hero, 0.06f, dir);
                    Items.AbilityAccessories.TapShadowDash(dir);
                    Player.TerraMovement.DevMove = dir;
                }
                else Items.AbilityAccessories.DevShadowKey = true;
                yield return GHold(hero, 0.5f, dir);
                Player.TerraMovement.DevMove = 0;
                if (Items.AbilityAccessories.DevShadowDashes > before) done++;
                dir = -dir;
                float t0 = Time.time;
                while (Time.time - t0 < 1.0f || Items.AbilityAccessories.ShadowCooldownLeft > 0f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 14f));
                    Player.TerraMovement.DevMove = Time.time - t0 < 0.12f ? dir : 0;
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 0.3f), 6f);
                    if (Time.time - t0 > 3f) break;
                    yield return null;
                }
            }
            Player.TerraMovement.DevMove = 0;
            yield return GHold(hero, 0.3f, dir);
            StopRec();
            bToughK = 1f;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 暗影冲刺 {clip}：{done}/{dashes} 次（双击 {tap}），击杀 {bKills}");
        }

        /// <summary>The shield's own dash (the Dash key tapped) through the row: it rams each it meets; turned round, again.</summary>
        IEnumerator GShieldBash(HeroController hero, string clip, int dashes)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            int dir = gRowDir;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 5f, 0.3f), 99f);
            bToughK = 6f;
            int dashes0 = Player.TerraMovement.DevDashes;
            StartRec(clip);
            for (int n = 0; n < dashes; n++)
            {
                yield return GStep(hero, 0.45f, dir);
                Player.TerraMovement.RequestDash(dir);
                Player.TerraMovement.DevMove = dir;
                yield return GHold(hero, 0.6f, dir);
                Player.TerraMovement.DevMove = 0;
                yield return GHold(hero, 0.25f, dir);
                // Past the row (or a wall ahead): back the other way.
                var c = (Vector2)col.bounds.center;
                bool more = false;
                foreach (var hm in BFoes(c, 14f)) if (Mathf.Sign(BCentre(hm).x - c.x) == dir && Mathf.Abs(BCentre(hm).y - c.y) < 2.5f) more = true;
                if (!more || Physics2D.Raycast(c, Vector2.right * dir, 3f, BTerrain).collider != null) dir = -dir;
                yield return GHold(hero, 0.2f, dir);
            }
            yield return GHold(hero, 0.3f, dir);
            StopRec();
            bToughK = 1f;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 盾冲 {clip}：冲了 {Player.TerraMovement.DevDashes - dashes0} 次，击杀 {bKills}");
        }

        /// <summary>The Crystal Heart's super dash along the floor: the Dash key held (crystals drawn in), let go, flown till a wall.</summary>
        IEnumerator GSuperDash(HeroController hero, string clip, int runs)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            int dir = gRowDir, done = 0;
            PlayerData.instance.hasSuperDash = true;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 7f, 0.4f), 99f);
            bToughK = 4f;
            StartRec(clip);
            for (int n = 0; n < runs; n++)
            {
                int before = Items.AbilityAccessories.DevSuperDashes;
                for (float t = Time.time + 2f; Time.time < t && !GameHooks.PlayerHasControl();) { BKeep(); yield return null; }
                if (dir > 0) hero.FaceRight(); else hero.FaceLeft();
                Items.AbilityAccessories.DevDashKey = false;
                yield return null;
                Items.AbilityAccessories.DevDashKey = true;
                float t0 = Time.time, full = -1f;
                while (Time.time - t0 < 3f && (full < 0f || Time.time - full < 0.2f))
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 18f));
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 7f, 0.4f), 5f);
                    string st = HeroFsm(hero, "Superdash")?.ActiveStateName ?? "";
                    if (full < 0f && st.EndsWith("Charged")) full = Time.time;
                    yield return null;
                }
                Items.AbilityAccessories.DevDashKey = false;
                t0 = Time.time;
                bool flew = false;
                float flewAt = 0f;
                while (Time.time - t0 < 3f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 18f));
                    SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 7f, 0.4f), 5f);
                    if (hero.cState.superDashing) { if (!flew) flewAt = Time.time; flew = true; }
                    else if (flew && Time.time - t0 > 0.3f) break;
                    if (flew && hero.cState.superDashing && Time.time - flewAt > 0.85f)
                    {
                        Items.AbilityAccessories.DevDashKey = true;
                        yield return null;
                        yield return null;
                        Items.AbilityAccessories.DevDashKey = false;
                    }
                    yield return null;
                }
                if (hero.cState.superDashing)
                {
                    Items.AbilityAccessories.DevDashKey = true;
                    yield return null;
                    yield return null;
                    Items.AbilityAccessories.DevDashKey = false;
                }
                if (Items.AbilityAccessories.DevSuperDashes > before) done++;
                Items.AbilityAccessories.DevDashKey = null;
                for (float t = Time.time + 2f; Time.time < t && (!GameHooks.PlayerHasControl() || hero.cState.superDashing);) { BKeep(); yield return null; }
                yield return GHold(hero, 0.3f, dir);
                dir = -dir;
                yield return GStep(hero, 0.12f, dir);
                yield return GHold(hero, 0.2f, dir);
            }
            StopRec();
            bToughK = 1f;
            Items.AbilityAccessories.DevDashKey = null;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 超级冲刺 {clip}：{done}/{runs} 次，击杀 {bKills}");
        }

        /// <summary>
        /// The smash: a jump (and the second) up over the row, Down held and jump pressed in the air: straight down onto them,
        /// the ground pounded; walked over to the next and again.
        /// </summary>
        IEnumerator GSmash(HeroController hero, string clip, int smashes)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            int dir = gRowDir;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 4f, -0.5f), 99f);
            bToughK = 5f;
            int pounds0 = Player.TerraMovement.DevPounds;
            StartRec(clip);
            for (int n = 0; n < smashes; n++)
            {
                // Over the nearest one ahead.
                var c0 = (Vector2)col.bounds.center;
                HealthManager over = null;
                float bd = float.MaxValue;
                foreach (var hm in BFoes(c0, 14f))
                {
                    var p = BCentre(hm);
                    if (Mathf.Abs(p.y - gRowFloor) > 3f) continue;
                    float d = Mathf.Abs(p.x - c0.x) + (Mathf.Sign(p.x - c0.x) != dir ? 6f : 0f);
                    if (d < bd) { bd = d; over = hm; }
                }
                float goal = over != null ? BCentre(over).x : c0.x + dir * 3f;
                if (goal < c0.x) dir = -1; else dir = 1;
                Player.TerraMovement.DevGroundJump(hero);
                Player.TerraMovement.DevJump = true;
                float t0 = Time.time;
                bool second = false;
                while (Time.time - t0 < 1.3f)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 16f));
                    var c = (Vector2)col.bounds.center;
                    float dx = goal - c.x;
                    Player.TerraMovement.DevMove = Mathf.Abs(dx) > 0.3f ? (dx > 0 ? 1 : -1) : 0;
                    SmoothAim(new Vector2(goal, gRowFloor + 0.5f), 6f);
                    var rb = hero.GetComponent<Rigidbody2D>();
                    if (!second && Time.time - t0 > 0.35f && rb.linearVelocity.y < 1f)
                    {
                        // The second jump (let go, pressed again).
                        Player.TerraMovement.DevJump = false;
                        yield return null;
                        Player.TerraMovement.DevJump = true;
                        second = true;
                    }
                    if (second && Time.time - t0 > 0.6f && Mathf.Abs(dx) < 0.6f && rb.linearVelocity.y < 2f) break;
                    yield return null;
                }
                Player.TerraMovement.DevJump = false;
                Player.TerraMovement.DevMove = 0;
                yield return null;
                Player.TerraMovement.DevDown = true;
                Player.TerraMovement.DevDownDash = true;
                for (float t = 0f; t < 1.2f && (Player.TerraMovement.DownDashing || t < 0.1f); t += Time.deltaTime)
                {
                    BKeep();
                    BToughen(BFoes(col.bounds.center, 16f));
                    yield return null;
                }
                Player.TerraMovement.DevDown = false;
                yield return GHold(hero, 0.55f, dir);
            }
            yield return GHold(hero, 0.3f, dir);
            StopRec();
            bToughK = 1f;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 下砸 {clip}：猛击 {Player.TerraMovement.DevPounds - pounds0} 次，击杀 {bKills}");
        }

        // ------------------------------------------------------------------ the nail arts

        /// <summary>
        /// The Nail's arts at the row, by the keys as a player presses them (Use Item put on K): held a second (the charge's
        /// flash), let go: the Great Slash; with Up held as it goes: the Cyclone Slash; let go inside a dash: the Dash Slash.
        /// </summary>
        IEnumerator GNailArts(HeroController hero, string clip, string art, int count)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var pd = PlayerData.instance;
            foreach (var f in new[] { "hasNailArt", "hasCyclone", "hasDashSlash", "hasUpwardSlash", "hasAllNailArts" }) pd.SetBool(f, true);
            var inv = mod.Inventory;
            for (int i = 0; i < Inventory.Size; i++) inv.Slots[i] = default;
            inv.Slots[0] = ItemStack.Of(ItemType.KnightNail);
            inv.Selected = 0;
            inv.Override = -1;
            var useWas = Player.TerraKeys.Get(Player.TerraKeys.Action.UseItem);
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, Key.K);
            int dir = gRowDir, arts0 = NailArts.DevArts;
            aimInit = false;
            SmoothAim((Vector2)col.bounds.center + new Vector2(dir * 3f, 0.2f), 99f);
            bToughK = 5f;
            StartRec(clip);
            try
            {
                for (int n = 0; n < count; n++)
                {
                    // Toward the nearest one ahead, charging on the way.
                    var c0 = (Vector2)col.bounds.center;
                    HealthManager to = null;
                    float bd = float.MaxValue;
                    foreach (var hm in BFoes(c0, 16f))
                    {
                        var p = BCentre(hm);
                        if (Mathf.Abs(p.y - c0.y) > 3f || p.y - gRowFloor > 2.2f) continue;
                        float d = Mathf.Abs(p.x - c0.x) + (Mathf.Sign(p.x - c0.x) != dir ? 4f : 0f);
                        if (d < bd) { bd = d; to = hm; }
                    }
                    float goal = to != null ? BCentre(to).x : c0.x + dir * 3f;
                    dir = goal < c0.x ? -1 : 1;
                    float stop = art == "dash" ? 4.5f : art == "cyclone" ? 0.6f : 1.6f;
                    float held = 0f;
                    for (float t = 0f; t < 2.6f; t += Time.deltaTime)
                    {
                        BKeep();
                        BToughen(BFoes(col.bounds.center, 16f));
                        var c = (Vector2)col.bounds.center;
                        if (to != null && !to.isDead) goal = BCentre(to).x;
                        float dx = goal - c.x;
                        Player.TerraMovement.DevMove = Mathf.Abs(dx) > stop ? (dx > 0 ? 1 : -1) : 0;
                        if (Player.TerraMovement.DevMove == 0) { if (dx > 0 && !hero.cState.facingRight) hero.FaceRight(); else if (dx < 0 && hero.cState.facingRight) hero.FaceLeft(); }
                        KeysDown(Key.K);
                        held += Time.deltaTime;
                        SmoothAim(new Vector2(goal, c.y + 0.2f), 6f);
                        if (held > 1.15f && Player.TerraMovement.DevMove == 0) break;
                        yield return null;
                    }
                    if (art == "cyclone")
                    {
                        KeysDown(Key.K, Key.W);
                        yield return null;
                        KeysDown(Key.W);
                        // Pressed on while it spins: it spins longer.
                        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                        {
                            BKeep();
                            BToughen(BFoes(col.bounds.center, 16f));
                            KeysDown(t % 0.2f < 0.1f ? new[] { Key.W, Key.K } : new[] { Key.W });
                            yield return null;
                        }
                    }
                    else if (art == "dash")
                    {
                        Player.TerraMovement.RequestDash(dir);
                        Player.TerraMovement.DevMove = dir;
                        yield return null;
                        yield return null;
                        yield return null;
                        KeysDown();
                    }
                    else KeysDown();
                    Player.TerraMovement.DevMove = 0;
                    KeysDown();
                    yield return GHold(hero, art == "dash" ? 0.9f : 0.8f, dir);
                }
                yield return GHold(hero, 0.3f, dir);
            }
            finally
            {
                KeysDown();
                Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem, useWas);
                Player.TerraMovement.DevMove = 0;
            }
            StopRec();
            bToughK = 1f;
            Plugin.Log.LogInfo($"[AutoTest] 录制G 骨钉技艺 {clip}：技艺命中 {NailArts.DevArts - arts0}，冲刺斩 {NailArts.DevDashSlashes}，击杀 {bKills}");
        }

        // ------------------------------------------------------------------ the suites

        IEnumerator P4_v5G_shields(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 1.7f;
            yield return GFloorRoom("Crossroads_39", 9f, 13f, 3, 0, ItemType.CrystalShield);
            if (gFloorOk) yield return GSuperDash(HeroController.instance, "v5G_shield_superdash", 2);
            yield return GFloorRoom("Crossroads_15", 7f, 6f, 3, 0, ItemType.CrystalShield);
            if (gFloorOk) yield return GShieldBash(HeroController.instance, "v5G_shield_bash", 5);
            yield return GFloorRoom("Fungus2_12", 8f, 7f, 5, 0, ItemType.ShadeShield);
            if (gFloorOk) yield return GShadowDash(HeroController.instance, "v5G_shield_shadowdash", 4, false);
            if (gFloorOk) yield return GShadowDash(HeroController.instance, "v5G_shield_doubletap", 4, true);
            Player.TerraCamera.Zoom = 1.9f;
            yield return GFloorRoom("Crossroads_05", 6f, 5f, 2, 0, ItemType.DiveShield);
            if (gFloorOk) yield return GSmash(HeroController.instance, "v5G_shield_dive", 3);
            Player.TerraCamera.Zoom = 1.9f;
            yield return GFloorRoom("Crossroads_15", 7f, 5f, 5, 0, ItemType.DarkShield);
            if (gFloorOk) yield return GSmash(HeroController.instance, "v5G_shield_darksmash", 3);
        }

        IEnumerator P4_v5G_nailarts(HeroController hero)
        {
            yield return P4Kit();
            GBegin();
            Player.TerraStats.DevImmortal = true;
            Player.TerraCamera.Zoom = 2f;
            yield return GFloorRoom("Crossroads_39", 8f, 7f, 3, 0, ItemType.CrystalShield);
            if (!gFloorOk) yield break;
            yield return GNailArts(HeroController.instance, "v5G_nail_great", "great", 3);
            yield return GNailArts(HeroController.instance, "v5G_nail_cyclone", "cyclone", 3);
            yield return GNailArts(HeroController.instance, "v5G_nail_dash", "dash", 3);
        }
    }
}
