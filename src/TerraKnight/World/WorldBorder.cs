using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The edge of the world, as Terraria's invisible walls at the map's border: where no loaded scene
    /// carries on past the one the player is in (Dirtmouth's sky, a cliff's open side, a room's far
    /// side with nothing beyond), the player stops at its edge as at a wall instead of flying off into
    /// nothing. A gate there (a door into a scene not loaded, a transition the seamless world leaves
    /// to the game) still lets the player through to it. Held at the bottom a moment, he has fallen
    /// into a pit: the game's pit hazard takes him back to the last safe ground, as its own pit
    /// triggers do further down (a dream's platforms over the void, the Dream Nail's trial).
    /// </summary>
    [HarmonyPatch]
    internal static class WorldBorder
    {
        public static bool Enabled = true;
        public static int DevStops;
        public static string DevLast = "";

        const float Inset = 0.02f;
        /// <summary>How near a gate's trigger the edge is open (units).</summary>
        const float GateReach = 1.5f;

        static readonly List<Bounds> gates = new List<Bounds>();
        static int gatesForScenes = -1;
        static float gatesAt = -10f;
        static float bottomSince = -1f;
        public static int DevPits;
        /// <summary>The bottom of the scene the Knight was last in (world units), and since when he has been out under it.</summary>
        static float? lastBottom;
        static float outSince = -1f;
        public static int DevFellOut;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterFixedUpdate(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!Enabled || TerraMod.Instance == null || TerrainSystem.Current == null || ___rb2d == null) return;
            var gm = GameManager.UnsafeInstance;
            var cs = __instance.cState;
            if (gm == null || gm.IsInSceneTransition || cs.transitioning || cs.dead || cs.hazardDeath || cs.hazardRespawning) return;
            // Native cutscenes may intentionally stage the Knight beyond the playable map.
            // Their placement and motion must not be clamped back inside the world.
            if (!GameHooks.PlayerHasControl() || Player.CutsceneAttack.KnightHidden())
            {
                bottomSince = outSince = -1f;
                return;
            }
            var col = __instance.GetComponent<BoxCollider2D>();
            if (col == null) return;
            var b = col.bounds;
            var centre = CellGrid.WorldToCell(b.center);
            var home = TerraWorld.RegionAt(centre.x, centre.y);
            // Outside every scene already (a cutscene, a teleport on its way): not ours to hold. But under the scene he
            // was last in, falling with control a second on (out past a gate at the bottom, down a dug-out lift's shaft,
            // the user's: "泪城电梯被挖掉后玩家走进去会无限下坠"): a pit, as the bottom's.
            if (home == null)
            {
                if (lastBottom != null && b.max.y < lastBottom.Value - 1f && ___rb2d.linearVelocity.y < -0.5f && GameHooks.PlayerHasControl())
                {
                    if (outSince < 0f) outSince = Time.time;
                    else if (Time.time - outSince > 1f)
                    {
                        outSince = -1f;
                        DevPits++;
                        DevFellOut++;
                        Plugin.Log.LogInfo($"[边界] 掉出了所有场景（{(Vector2)b.center:F1}，上一个场景底部 y {lastBottom.Value:F1}）：当作掉进深坑，回到上一个安全落脚点");
                        SafeRespawn(__instance);
                        __instance.TakeDamage(null, GlobalEnums.CollisionSide.bottom, 1, 1 + (int)GlobalEnums.HazardType.PIT);
                    }
                }
                else outSince = -1f;
                return;
            }
            outSince = -1f;
            lastBottom = home.CellRect.yMin * CellGrid.CellSize;
            // The last ground stood on here: static terrain (a grid cell under the feet, not a lift or a platform), with control.
            if (cs.onGround && GameHooks.PlayerHasControl() && !cs.onConveyor && !cs.onConveyorV)
            {
                var under = CellGrid.WorldToCell(new Vector2(b.center.x, b.min.y - CellGrid.CellSize * 0.5f));
                if (TerraWorld.IsSolid(under.x, under.y)) safeGround = new Vector2(b.center.x, b.min.y + 0.1f);
            }
            RefreshGates();
            const float c = CellGrid.CellSize;
            var cr = home.CellRect;
            float left = cr.xMin * c, right = cr.xMax * c, bottom = cr.yMin * c, top = cr.yMax * c;
            float dt = Time.fixedDeltaTime;
            var v = ___rb2d.linearVelocity;
            Vector2 shift = Vector2.zero;
            string side = null;
            // Each side by its middle, where it will be after this step.
            if (Out(new Vector2(b.center.x, b.max.y + Mathf.Max(0f, v.y) * dt)) && b.max.y + Mathf.Max(0f, v.y) * dt > top - Inset)
            {
                shift.y = Mathf.Min(0f, top - Inset - b.max.y);
                if (v.y > 0f) v.y = 0f;
                side = "上";
            }
            else if (Out(new Vector2(b.center.x, b.min.y + Mathf.Min(0f, v.y) * dt)) && b.min.y + Mathf.Min(0f, v.y) * dt < bottom + Inset)
            {
                shift.y = Mathf.Max(0f, bottom + Inset - b.min.y);
                if (v.y < 0f) v.y = 0f;
                side = "下";
            }
            if (Out(new Vector2(b.max.x + Mathf.Max(0f, v.x) * dt, b.center.y)) && b.max.x + Mathf.Max(0f, v.x) * dt > right - Inset)
            {
                shift.x = Mathf.Min(0f, right - Inset - b.max.x);
                if (v.x > 0f) v.x = 0f;
                side = (side ?? "") + "右";
            }
            else if (Out(new Vector2(b.min.x + Mathf.Min(0f, v.x) * dt, b.center.y)) && b.min.x + Mathf.Min(0f, v.x) * dt < left + Inset)
            {
                shift.x = Mathf.Max(0f, left + Inset - b.min.x);
                if (v.x < 0f) v.x = 0f;
                side = (side ?? "") + "左";
            }
            if (side == null || !side.Contains("下")) bottomSince = -1f;
            else if (bottomSince < 0f) bottomSince = Time.time;
            else if (Time.time - bottomSince > 0.3f)
            {
                bottomSince = -1f;
                DevPits++;
                Plugin.Log.LogInfo($"[边界] {home.SceneName} 掉出了场景底部：当作掉进深坑，回到上一个安全落脚点");
                SafeRespawn(__instance);
                __instance.TakeDamage(null, GlobalEnums.CollisionSide.bottom, 1, 1 + (int)GlobalEnums.HazardType.PIT);
                return;
            }
            if (side == null) return;
            ___rb2d.linearVelocity = v;
            if (shift != Vector2.zero)
            {
                var p = __instance.transform.position + (Vector3)shift;
                __instance.transform.position = p;
                ___rb2d.position = p;
            }
            DevStops++;
            Achievements.AchievementTracker.WorldEdge();
            DevLast = $"{home.SceneName} {side}";
        }

        /// <summary>The last ground the Knight stood on in a scene (world units).</summary>
        static Vector2? safeGround;
        public static int DevRespawnMoved;
        public static Vector2? DevSafeGround => safeGround;
        public static string DevLastCheck = "";

        /// <summary>
        /// Before a pit: the game's hazard respawn outside every loaded scene (a room entered by its bottom gate, or by a
        /// lift, keeps the spot under it; a pit there fell for ever) set to the last ground stood on.
        /// </summary>
        static void SafeRespawn(HeroController hero)
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            Vector2 at = pd.hazardRespawnLocation;
            var cell = CellGrid.WorldToCell(at + Vector2.up * 0.5f);
            // The game sets the Knight down on the ground under its spot (FindGroundPoint); none under it (the spot over a
            // lift's open shaft, by the bottom gate) puts him at the world's origin, out of the room again.
            var ground = Physics2D.Raycast(at, Vector2.down, 30f, 1 << ChunkColliders.TerrainLayer);
            bool landed = ground.collider != null;
            if (landed)
            {
                var gc = CellGrid.WorldToCell(ground.point + Vector2.up * 0.25f);
                landed = TerraWorld.RegionAt(gc.x, gc.y) != null;
            }
            DevLastCheck = $"复活点 {at:F2} 在场景里 {TerraWorld.RegionAt(cell.x, cell.y) != null} 下面 {(ground.collider != null ? ground.collider.name + (ground.collider.isTrigger ? "(触发器)" : "") + " @" + ground.point.ToString("F2") : "没有")} 落地 {landed}";
            if (TerraWorld.RegionAt(cell.x, cell.y) != null && landed) return;
            DevLastCheck += $"；最近站过的地面 {safeGround}";
            if (safeGround == null) return;
            var g = CellGrid.WorldToCell(safeGround.Value);
            if (TerraWorld.RegionAt(g.x, g.y) == null) { DevLastCheck += " 不在场景里"; return; }
            hero.SetHazardRespawn(safeGround.Value + Vector2.up * 0.5f, hero.cState.facingRight);
            DevRespawnMoved++;
            Plugin.Log.LogInfo($"[边界] 原版的复活落脚点 {at:F1} 不在场景里或下面没有地面：改到最近站过的地面 {safeGround.Value:F1}");
        }

        /// <summary>A point in no loaded scene, and not by a gate.</summary>
        static bool Out(Vector2 p)
        {
            var cell = CellGrid.WorldToCell(p);
            if (TerraWorld.RegionAt(cell.x, cell.y) != null) return false;
            foreach (var g in gates)
            {
                var e = g;
                e.Expand(GateReach * 2f);
                if (e.Contains(new Vector3(p.x, p.y, e.center.z))) return false;
            }
            return true;
        }

        /// <summary>The loaded scenes' gate triggers, looked up again when scenes come and go (and now and then: some are switched on late).</summary>
        static void RefreshGates()
        {
            int loaded = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (loaded == gatesForScenes && Time.time - gatesAt < 2f) return;
            gatesForScenes = loaded;
            gatesAt = Time.time;
            gates.Clear();
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
            {
                var tc = tp.GetComponent<Collider2D>();
                gates.Add(tc != null ? tc.bounds : new Bounds(tp.transform.position, Vector3.one));
            }
        }
    }
}
