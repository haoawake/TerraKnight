using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Corpses of the enemies that come back (<see cref="EnemyRespawn"/>) lie where they fell as Hollow Knight leaves them
    /// (Deepnest's are taken over and get up as Corpse Creepers), up to <see cref="MaxCorpses"/> at once: past that the
    /// oldest fades and goes, so doubled, respawning enemies don't fill a room with bodies. Bosses, scripted and one-time
    /// enemies (whose bodies can matter to the scene) keep theirs.
    /// </summary>
    [HarmonyPatch]
    internal static class CorpseCleanup
    {
        /// <summary>Seconds an extra corpse lies before fading, and the fade; how many lie at once.</summary>
        public const float Linger = 0f, Fade = 1f;
        public const int MaxCorpses = 24;
        static readonly List<GameObject> lying = new List<GameObject>();

        static readonly AccessTools.FieldRef<EnemyDeathEffects, GameObject> corpseOf = AccessTools.FieldRefAccess<EnemyDeathEffects, GameObject>("corpse");
        static EnemyDeathEffects emitting;
        static readonly Dictionary<GameObject, bool> fromPool = new Dictionary<GameObject, bool>();
        static GameObject pooled;

        /// <summary>Dev: corpses set to fade, and those gone.</summary>
        public static int DevScheduled, DevRemoved;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnemyDeathEffects), "EmitCorpse")]
        static void BeforeEmit(EnemyDeathEffects __instance)
        {
            emitting = __instance;
            pooled = null;
        }

        /// <summary>A recycled corpse is the first thing the pool hands out while the corpse is emitted.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectPool), nameof(ObjectPool.Spawn), new[] { typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion) })]
        static void AfterPoolSpawn(GameObject __result)
        {
            if (emitting != null && pooled == null) pooled = __result;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(EnemyDeathEffects), "EmitCorpse")]
        static void AfterEmit(EnemyDeathEffects __instance)
        {
            try
            {
                var respawn = EnemyRespawn.Instance;
                if (TerraMod.Instance == null || respawn == null || __instance == null || !respawn.Owns(__instance.gameObject)) return;
                var body = pooled != null ? pooled : corpseOf(__instance);
                if (body == null || !body.activeInHierarchy) return;
                lying.RemoveAll(g => g == null || !g.activeInHierarchy);
                if (fromPool.Count > 4 * MaxCorpses)
                {
                    var gone = new List<GameObject>();
                    foreach (var k in fromPool.Keys) if (k == null || !lying.Contains(k)) gone.Add(k);
                    foreach (var k in gone) fromPool.Remove(k);
                }
                if (!lying.Contains(body)) lying.Add(body);
                fromPool[body] = pooled != null;
                // Past the most that lie at once, the oldest goes.
                while (lying.Count > MaxCorpses)
                {
                    var old = lying[0];
                    lying.RemoveAt(0);
                    if (old == null) continue;
                    var fade = old.GetComponent<CorpseFade>() ?? old.AddComponent<CorpseFade>();
                    fade.Begin(fromPool.TryGetValue(old, out bool p) && p);
                    fromPool.Remove(old);
                    DevScheduled++;
                }
            }
            finally
            {
                emitting = null;
                pooled = null;
            }
        }
    }

    /// <summary>Lies still a moment, fades every sprite out, then goes (back to its pool, colours put back, if it came from one).</summary>
    internal sealed class CorpseFade : MonoBehaviour
    {
        bool recycle;
        float age;
        readonly List<(tk2dBaseSprite s, Color c)> tk = new List<(tk2dBaseSprite, Color)>();
        readonly List<(SpriteRenderer s, Color c)> sr = new List<(SpriteRenderer, Color)>();

        public void Begin(bool fromPool)
        {
            recycle = fromPool;
            age = 0f;
            tk.Clear();
            sr.Clear();
            foreach (var s in GetComponentsInChildren<tk2dBaseSprite>(true)) tk.Add((s, s.color));
            foreach (var s in GetComponentsInChildren<SpriteRenderer>(true)) sr.Add((s, s.color));
            enabled = true;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age < CorpseCleanup.Linger) return;
            float a = 1f - (age - CorpseCleanup.Linger) / CorpseCleanup.Fade;
            if (a <= 0f)
            {
                Finish();
                return;
            }
            foreach (var (s, c) in tk) if (s != null) s.color = new Color(c.r, c.g, c.b, c.a * a);
            foreach (var (s, c) in sr) if (s != null) s.color = new Color(c.r, c.g, c.b, c.a * a);
        }

        void Finish()
        {
            CorpseCleanup.DevRemoved++;
            if (!recycle)
            {
                Destroy(gameObject);
                return;
            }
            foreach (var (s, c) in tk) if (s != null) s.color = c;
            foreach (var (s, c) in sr) if (s != null) s.color = c;
            enabled = false;
            ObjectPool.Recycle(gameObject);
        }
    }
}
