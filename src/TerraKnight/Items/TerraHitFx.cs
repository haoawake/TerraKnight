using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// What a Terraria weapon's hit looks like on an enemy: Terraria's, not the nail's. The game's
    /// white strike flash, the enemy's own hit effect (splatter, white flash, hit sound) and its
    /// death burst are left out for hits from <see cref="Source"/>; instead the enemy bleeds
    /// Terraria's blood dust (NPC.HitEffect: one dust per percent of its life taken, a burst of 50
    /// when it dies). A guarded hit loses the nail's clink, spark and camera shake, and the push is
    /// Terraria's (<see cref="TerraKnockback"/>). Everything else (damage, the recoil slide itself,
    /// corpse, geo, journal) stays the game's.
    /// </summary>
    [HarmonyPatch]
    internal static class TerraHitFx
    {
        /// <summary>The object Terraria weapon hits name as their source.</summary>
        public static GameObject Source;

        static readonly AccessTools.FieldRef<HealthManager, GameObject> strikeNailPrefab = AccessTools.FieldRefAccess<HealthManager, GameObject>("strikeNailPrefab");
        static readonly AccessTools.FieldRef<HealthManager, IHitEffectReciever> hitEffectReceiver = AccessTools.FieldRefAccess<HealthManager, IHitEffectReciever>("hitEffectReceiver");

        static bool terraHit;
        static GameObject suppressed;
        static GameObject nothing;

        struct Saved
        {
            public bool Active;
            public IHitEffectReciever Receiver;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void BeforeTakeDamage(HealthManager __instance, HitInstance hitInstance, out Saved __state)
        {
            __state = default;
            if (Source == null || hitInstance.Source != Source) return;
            __state = new Saved { Active = true, Receiver = hitEffectReceiver(__instance) };
            hitEffectReceiver(__instance) = null;
            suppressed = strikeNailPrefab(__instance);
            terraHit = true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void AfterTakeDamage(HealthManager __instance, Saved __state)
        {
            if (!__state.Active) return;
            hitEffectReceiver(__instance) = __state.Receiver;
            suppressed = null;
            terraHit = false;
        }

        static readonly AccessTools.FieldRef<HealthManager, int> directionOfLastAttack = AccessTools.FieldRefAccess<HealthManager, int>("directionOfLastAttack");
        static readonly AccessTools.FieldRef<HealthManager, bool> preventInvincibleEffect = AccessTools.FieldRefAccess<HealthManager, bool>("preventInvincibleEffect");
        static readonly AccessTools.FieldRef<HealthManager, BoxCollider2D> boxCollider = AccessTools.FieldRefAccess<HealthManager, BoxCollider2D>("boxCollider");
        static readonly AccessTools.FieldRef<HealthManager, float> evasionByHitRemaining = AccessTools.FieldRefAccess<HealthManager, float>("evasionByHitRemaining");

        /// <summary>
        /// A Terraria hit on a guarded side (a shell, a shield): the enemy still hears it and
        /// guards (its FSM events, as HealthManager.Invincible sends them), but none of the nail's
        /// clink — no spark, no camera shake, no clink sound. Terraria has no such thing.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "Invincible")]
        static bool BeforeInvincible(HealthManager __instance, HitInstance hitInstance)
        {
            if (Source == null || hitInstance.Source != Source) return true;
            var go = __instance.gameObject;
            int dir = DirectionUtils.GetCardinalDirection(hitInstance.GetActualDirection(__instance.transform));
            directionOfLastAttack(__instance) = dir;
            FSMUtility.SendEventToGameObject(go, "BLOCKED HIT");
            FSMUtility.SendEventToGameObject(hitInstance.Source, "HIT LANDED");
            if (go.GetComponent<DontClinkGates>() == null)
            {
                // Not to a body a weak point of it reports its hits to (the False Knight's head sends him HIT):
                // a blow on its shut armor isn't one on the head, and a Terraria weapon's stream of them held his
                // stun open for good, the head out and him never fighting again.
                if (!TakesForwardedHits(__instance)) FSMUtility.SendEventToGameObject(go, "HIT");
                if (!preventInvincibleEffect(__instance) && boxCollider(__instance) != null)
                {
                    string side = dir switch { 0 => "BLOCKED HIT R", 2 => "BLOCKED HIT L", 1 => "BLOCKED HIT U", 3 => "BLOCKED DOWN", _ => null };
                    if (side != null) FSMUtility.SendEventToGameObject(go, side);
                }
            }
            evasionByHitRemaining(__instance) = 0.15f;
            return false;
        }

        static readonly AccessTools.FieldRef<HealthManager, GameObject> sendHitTo = AccessTools.FieldRefAccess<HealthManager, GameObject>("sendHitTo");

        /// <summary>Whether a part of this enemy passes its hits on to it (HealthManager.sendHitTo: the False Knight's head to his body).</summary>
        static bool TakesForwardedHits(HealthManager hm)
        {
            foreach (var part in hm.GetComponentsInChildren<HealthManager>(true))
                if (part != hm && sendHitTo(part) == hm.gameObject) return true;
            return false;
        }

        /// <summary>
        /// A Terraria hit that pushes it nowhere (a boss, a hit with no knockback) doesn't start a
        /// recoil at all: a recoil of no speed would still stop many enemies in their tracks.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Recoil), nameof(Recoil.RecoilByDirection))]
        static bool BeforeRecoil(float attackMagnitude) => !(terraHit && attackMagnitude <= 0f);

        /// <summary>The strike flash spawned for a Terraria hit: hand back an idle stand-in instead.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ObjectPool), nameof(ObjectPool.Spawn), typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion))]
        static bool Spawn(GameObject prefab, ref GameObject __result)
        {
            if (!terraHit || prefab == null || prefab != suppressed) return true;
            if (nothing == null)
            {
                nothing = new GameObject("Terra No Strike Effect");
                nothing.SetActive(false);
                Object.DontDestroyOnLoad(nothing);
            }
            __result = nothing;
            return false;
        }

        /// <summary>The death burst (the corpse, geo and the rest still happen).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDeathEffects), "EmitEffects")]
        [HarmonyPatch(typeof(EnemyDeathEffectsUninfected), "EmitEffects")]
        [HarmonyPatch(typeof(EnemyDeathEffectsBubble), "EmitEffects")]
        [HarmonyPatch(typeof(EnemyDeathEffectsDung), "EmitEffects")]
        [HarmonyPatch(typeof(EnemyDeathEffectsBlackKnight), "EmitEffects")]
        static bool EmitEffects() => !terraHit;

        /// <summary>
        /// Terraria's blood on an enemy hit by a weapon swung towards hitDir (NPC.HitEffect): while
        /// it lives, one dust per percent of its Terraria life the hit took; when it dies, a burst of 50.
        /// </summary>
        public static void Bleed(HealthManager hm, Bounds area, int damage, float hitDir, int lifeMax)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var topLeft = TerraParticles.ToPx(new Vector2(area.min.x, area.max.y));
            int w = Mathf.Max(8, Mathf.RoundToInt(area.size.x / TerraParticles.Px));
            int h = Mathf.Max(8, Mathf.RoundToInt(area.size.y / TerraParticles.Px));
            if (hm.isDead)
            {
                for (int i = 0; i < 50; i++) p.NewDust(topLeft, w, h, 5, 2f * hitDir, -2f);
                return;
            }
            int count = Mathf.Clamp(Mathf.CeilToInt(100f * damage / Mathf.Max(1, lifeMax)), 1, 50);
            for (int i = 0; i < count; i++) p.NewDust(topLeft, w, h, 5, hitDir, -1f);
        }
    }
}
