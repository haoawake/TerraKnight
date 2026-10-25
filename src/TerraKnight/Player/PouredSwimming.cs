using HarmonyLib;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Poured liquid is editable and can cover doors or have a solid ceiling. Native
    /// Surface Water takes control and positions the hero at a fixed surface; use
    /// collision-respecting buoyancy for poured cells, without owning gravity/control.
    /// </summary>
    [HarmonyPatch]
    internal static class PouredSwimming
    {
        static HeroController owner;
        public static bool Active { get; private set; }
        public static bool Wet { get; private set; }
        public static bool InAcid { get; private set; }
        public static bool For(HeroController hero) => Active && owner == hero;

        internal static bool Surface(HeroController hero, out float top, out LiquidKind kind)
        {
            top = 0f; kind = LiquidKind.Water;
            var box = hero != null ? hero.GetComponent<BoxCollider2D>() : null;
            if (box == null) return false;
            var b = box.bounds;
            // Sampling the feet retains contact at the surface, not with water
            // somewhere overhead. A full block always separates the two volumes.
            var cell = CellGrid.WorldToCell(new Vector2(b.center.x, b.min.y + 0.08f));
            for (int attempt = 0; attempt < 2; attempt++, cell.y--)
            {
                var r = TerraWorld.RegionAt(cell.x, cell.y);
                if (r == null) continue;
                int x = cell.x-r.CellOffset.x, y = cell.y-r.CellOffset.y;
                var g = r.Grid;
                if (Cells.Blocks(g.Get(x,y))) return false;
                if (g.GetLiquid(x,y)<24) continue;
                kind = g.GetLiquidKind(x,y);
                var liquidKind=kind;
                bool Wet(int row) => !Cells.Blocks(g.Get(x,row)) && g.GetLiquid(x,row)>=24 && g.GetLiquidKind(x,row)==liquidKind;
                int upper=y, lower=y;
                while (Wet(upper+1)) upper++;
                while (Wet(lower-1)) lower--;
                top = r.Root.position.y+(upper+g.GetLiquid(x,upper)/255f)*CellGrid.CellSize;
                float bottom=r.Root.position.y+lower*CellGrid.CellSize;
                if (Mathf.CeilToInt((top-bottom)/CellGrid.CellSize-0.01f)<3 || top-b.min.y < -0.18f) continue;
                return true;
            }
            return false;
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void Buoyancy(HeroController __instance, Rigidbody2D ___rb2d)
        {
            owner=__instance; Active=false; Wet=false; InAcid=false;
            var gm=GameManager.UnsafeInstance;
            if (___rb2d==null || !GameHooks.IsGameplay(gm) || gm.isPaused || gm.IsInSceneTransition
                || __instance.cState.transitioning || __instance.cState.dead || __instance.cState.hazardDeath
                || __instance.cState.hazardRespawning || __instance.cState.recoiling) return;
            if (!Surface(__instance,out float top,out var kind)) return;
            Wet=true;
            InAcid=kind==LiquidKind.Acid;
            if (!GameHooks.PlayerHasControl() || InAcid && !AcidLiquid.DevSwimOn) return;
            Active=true;
            if (Mounts.OwnMovement || GrapplingHook.IsLatched || TerraMovement.OnRope || TerraMovement.Flying) return;
            var v=___rb2d.linearVelocity;
            if (v.y>6f || __instance.cState.jumping && v.y>0f) return;
            float feet=__instance.GetComponent<BoxCollider2D>().bounds.min.y;
            bool walk=Items.Equipment.Current.WaterWalk>0 && !TerraMovement.DownHeld;
            float depth=walk ? -0.04f : 0.55f;
            float speed=TerraMovement.DownHeld ? -3f : Mathf.Clamp((top-feet-depth)*5f,-3f,4f);
            // Physics applies gravity after FixedUpdate. Cancel only that step;
            // never leave gravityScale=0 or relocate through terrain/doorways.
            v.y=speed-Physics2D.gravity.y*___rb2d.gravityScale*Time.fixedDeltaTime;
            ___rb2d.linearVelocity=v;
            TerraMod.Instance?.ResetFall();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "CanJump")]
        static void WaterJump(HeroController __instance, ref bool __result)
        {
            if (For(__instance) && GameHooks.PlayerHasControl() && !__instance.cState.recoiling
                && !__instance.cState.dead && !__instance.cState.transitioning) __result=true;
        }

        internal static bool ClearRise(HeroController hero, float targetFeet)
        {
            var b=hero.GetComponent<BoxCollider2D>().bounds;
            float distance=Mathf.Max(0f,targetFeet-b.min.y);
            foreach (var hit in Physics2D.BoxCastAll(b.center,(Vector2)b.size*0.9f,0f,Vector2.up,distance,1<<ChunkColliders.TerrainLayer))
                if (hit.collider!=null && !hit.collider.isTrigger && !hit.collider.usedByEffector) return false;
            return true;
        }
    }
}
