using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// A boss's own thrown things for the kits' moves (the user's, 2026-10-23: the bosses' own looks wherever they can): the
    /// prefab its script spawns or flings, spawned from the game's pool as its script does it (dynamic, sent FIRE when its
    /// script does), then sent the kit's way. Its picture, its splash or burst, its harm are the game's.
    /// </summary>
    internal static class OwnShots
    {
        /// <summary>The thing a state of a boss's script spawns or flings (the first such action), or null.</summary>
        public static GameObject Of(FsmState s)
        {
            if (s == null) return null;
            foreach (var a in s.Actions)
            {
                GameObject go = a switch
                {
                    SpawnObjectFromGlobalPool sp => sp.gameObject?.Value,
                    FlingObjectsFromGlobalPool f => f.gameObject?.Value,
                    FlingObjectsFromGlobalPoolVel v => v.gameObject?.Value,
                    FlingObjectsFromGlobalPoolTime t => t.gameObject?.Value,
                    _ => null,
                };
                if (go != null) return go;
            }
            return null;
        }

        /// <summary>
        /// One of them spawned at a point and sent off at a velocity (world units a second); <paramref name="fire"/>: made
        /// dynamic and sent FIRE first, as its script does for the ones that wait for it (the soul orbs).
        /// </summary>
        public static GameObject Throw(GameObject prefab, Vector2 at, Vector2 velocity, bool fire = false)
        {
            if (prefab == null) return null;
            var go = ObjectPool.Spawn(prefab, new Vector3(at.x, at.y, prefab.transform.position.z), Quaternion.identity);
            if (go == null) return null;
            var rb = go.GetComponent<Rigidbody2D>();
            if (fire)
            {
                if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
                FSMUtility.SendEventToGameObject(go, "FIRE", false);
            }
            if (rb != null) rb.linearVelocity = velocity;
            BossAi.DevMoves++;
            return go;
        }

        /// <summary>One of them lobbed to land on a point after some seconds, under its own gravity.</summary>
        public static GameObject Lob(GameObject prefab, Vector2 from, Vector2 to, float seconds)
        {
            var rb = prefab != null ? prefab.GetComponent<Rigidbody2D>() : null;
            float g = Mathf.Abs(Physics2D.gravity.y) * (rb != null ? rb.gravityScale : 1f);
            seconds = Mathf.Max(0.25f, seconds);
            var d = to - from;
            return Throw(prefab, from, new Vector2(d.x / seconds, (d.y + 0.5f * g * seconds * seconds) / seconds));
        }
    }
}
