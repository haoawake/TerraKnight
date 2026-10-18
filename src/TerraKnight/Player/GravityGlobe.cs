using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// The Gravity Globe (Player.gravControl2, Pl:26913-26931): a fresh press of Up turns gravity
    /// over (Item8), and back; taking it off, dying, a hazard respawn or a scene change sets it
    /// right again. Hollow Knight's hero only knows gravity pulling down, so while it's turned the
    /// hero's controller (and the Terraria movement patched onto it) runs in a mirrored space: on
    /// the way into each of its methods the rigidbody's vertical speed and gravity are turned over,
    /// on the way out turned back, so its jumps, falls, wall slides and landings all work the other
    /// way up; its looks for ground cast upward and a ceiling it lands on counts as the floor.
    /// With an interaction prompt showing (a bench, a door, someone to talk to), Up keeps its
    /// Hallownest meaning and leaves gravity alone.
    /// </summary>
    [HarmonyPatch]
    internal static class GravityGlobe
    {
        /// <summary>Player.gravDir == -1.</summary>
        public static bool Flipped { get; private set; }

        public static int DevToggles;

        /// <summary>Dev: an Up press for the autotest, which has no keyboard.</summary>
        public static bool DevUpPress;

        static int depth;

        // The controller's methods that read or set the hero's vertical speed or gravity (coroutines left out: they run later).
        static readonly string[] Mirrored =
        {
            "Update", "FixedUpdate", "Move", "Jump", "DoubleJump", "Dash", "CancelHeroJump", "CancelJump", "TakeDamage", "Bounce", "BounceHigh", "ShroomBounce",
            "RecoilLeft", "RecoilRight", "RecoilRightLong", "RecoilLeftLong", "RecoilDown", "FallCheck", "FailSafeChecks", "ResetMotion", "JumpReleased",
            "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D", "AffectedByGravity", "HeroJump", "HeroJumpNoEffect", "DoWallJump", "DoDoubleJump",
            "LookForInput", "LookForQueueInput", "HeroDash", "BackOnGround", "UpdateRunEffect", "DoHardLanding",
        };

        static IEnumerable<MethodBase> TargetMethods()
        {
            var names = new HashSet<string>(Mirrored);
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(HeroController)))
                if (names.Contains(m.Name) && m.ReturnType != typeof(System.Collections.IEnumerator) && !m.IsAbstract) yield return m;
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        static void Enter(HeroController __instance) => Begin(__instance);

        [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
        static void Leave(HeroController __instance) => End(__instance);

        /// <summary>Into the mirrored space (nested calls count once): the Terraria movement's own entry points use it too.</summary>
        public static void Begin(HeroController hero)
        {
            if (depth++ == 0 && Flipped) Mirror(hero, true);
        }

        public static void End(HeroController hero)
        {
            if (depth > 0 && --depth == 0 && Flipped) Mirror(hero, false);
        }

        /// <summary>Speed up turned over; gravity positive inside, negative outside, whatever set it in between.</summary>
        static void Mirror(HeroController hero, bool inside)
        {
            var rb = hero != null ? hero.GetComponent<Rigidbody2D>() : null;
            if (rb == null) return;
            var v = rb.linearVelocity;
            rb.linearVelocity = new Vector2(v.x, -v.y);
            float g = Mathf.Abs(rb.gravityScale);
            rb.gravityScale = inside ? g : -g;
        }

        static void Set(HeroController hero, bool flip)
        {
            if (Flipped == flip) return;
            // Pl:26915-26929: turned at once, the fall measured from here, the jump cut.
            Flipped = flip;
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null) rb.gravityScale = flip ? -Mathf.Abs(rb.gravityScale) : Mathf.Abs(rb.gravityScale);
            TerraMod.Instance?.ResetFall();
            DevToggles++;
        }

        /// <summary>Each frame (outside the controller's methods): Up turns it over; off without the Globe or once the hero isn't his own.</summary>
        public static void Tick(HeroController hero)
        {
            if (hero == null || depth != 0) return;
            bool press = DevUpPress || (GameHooks.Input != null && GameHooks.Input.inputActions.up.WasPressed);
            DevUpPress = false;
            var cs = hero.cState;
            if (!Equipment.Current.GravityControl || cs.dead || cs.hazardDeath || cs.hazardRespawning || cs.transitioning)
            {
                if (Flipped)
                {
                    Set(hero, false);
                    TerraMod.Instance?.PlaySound("Item_8");
                }
                return;
            }
            if (!GameHooks.PlayerHasControl()) return;
            // Gone up out of the room (nothing overhead to stand on): back down.
            var gm = GameManager.UnsafeInstance;
            if (Flipped && gm != null && gm.sceneHeight > 0f && hero.transform.position.y > gm.sceneHeight + 8f)
            {
                Set(hero, false);
                TerraMod.Instance?.PlaySound("Item_8");
                return;
            }
            if (!press || !hero.CanInput() || PromptShowing()) return;
            Set(hero, !Flipped);
            TerraMod.Instance?.PlaySound("Item_8");
        }

        static readonly FieldInfo promptVisible = AccessTools.Field(typeof(PromptMarker), "isVisible");

        /// <summary>A bench's, door's or speaker's prompt over the hero: Up is theirs.</summary>
        static bool PromptShowing()
        {
            if (promptVisible == null) return false;
            foreach (var m in Object.FindObjectsByType<PromptMarker>(FindObjectsSortMode.None))
                if (m.gameObject.activeInHierarchy && (bool)promptVisible.GetValue(m)) return true;
            return false;
        }

        public static void Clear()
        {
            Flipped = false;
            depth = 0;
        }
    }

    /// <summary>The controller's looks for ground, turned over while the Gravity Globe has gravity turned.</summary>
    [HarmonyPatch]
    internal static class GravityGlobeChecks
    {
        static bool Flipped => GravityGlobe.Flipped;


        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "CheckTouchingGround")]
        static bool GroundAbove(HeroController __instance, ref bool __result)
        {
            if (!Flipped) return true;
            var b = __instance.GetComponent<BoxCollider2D>().bounds;
            float distance = b.extents.y + 0.16f;
            __result = Physics2D.Raycast(new Vector2(b.min.x, b.center.y), Vector2.up, distance, 256).collider != null
                || Physics2D.Raycast(b.center, Vector2.up, distance, 256).collider != null
                || Physics2D.Raycast(new Vector2(b.max.x, b.center.y), Vector2.up, distance, 256).collider != null;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "CheckNearRoof")]
        static bool RoofBelow(HeroController __instance, ref bool __result)
        {
            if (!Flipped) return true;
            var b = __instance.GetComponent<BoxCollider2D>().bounds;
            float q = b.size.x / 4f;
            __result = Physics2D.Raycast(new Vector2(b.min.x, b.min.y), new Vector2(-0.5f, -1f), 2f, 256).collider != null
                || Physics2D.Raycast(new Vector2(b.max.x, b.min.y), new Vector2(0.5f, -1f), 2f, 256).collider != null
                || Physics2D.Raycast(new Vector2(b.center.x + q, b.min.y), Vector2.down, 1f, 256).collider != null
                || Physics2D.Raycast(new Vector2(b.center.x - q, b.min.y), Vector2.down, 1f, 256).collider != null;
            return false;
        }

        /// <summary>The little step up onto a ledge is for feet on a floor; none upside down.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "CheckForBump")]
        static bool NoBump(ref bool __result)
        {
            if (!Flipped) return true;
            __result = false;
            return false;
        }

        /// <summary>A ceiling met is the floor, the floor the ceiling.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FindCollisionDirection")]
        static void TurnSides(ref GlobalEnums.CollisionSide __result)
        {
            if (!Flipped) return;
            if (__result == GlobalEnums.CollisionSide.top) __result = GlobalEnums.CollisionSide.bottom;
            else if (__result == GlobalEnums.CollisionSide.bottom) __result = GlobalEnums.CollisionSide.top;
        }
    }
}
