using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// What the Collector's, the Crystal Guardians', Uumuu's, Nosk's, the Broken Vessel's and the Hive Knight's kits share:
    /// an action of a state found, a clip played, a body turned, the room's scripts found by name, the prefab a state of
    /// the game spawns, and a summoned enemy made quicker.
    /// </summary>
    internal static class Batch3
    {
        /// <summary>A state's first action of a kind, or null.</summary>
        internal static T Find<T>(FsmState s) where T : FsmStateAction
        {
            if (s == null) return null;
            foreach (var a in s.Actions) if (a is T t) return t;
            return null;
        }

        /// <summary>One of the body's own clips played (none if it has no clip by that name).</summary>
        internal static void Play(Component on, string clip)
        {
            var anim = on != null ? on.GetComponent<tk2dSpriteAnimator>() : null;
            if (anim != null && anim.GetClipByName(clip) != null) anim.Play(clip);
        }

        /// <summary>A body turned one way (its sprite drawn facing right at a positive x scale, or left).</summary>
        internal static void Face(Transform t, float dir, bool spriteFacesRight)
        {
            if (t == null || Mathf.Abs(dir) < 0.001f) return;
            var s = t.localScale;
            float want = (dir > 0f) == spriteFacesRight ? 1f : -1f;
            s.x = Mathf.Abs(s.x) * want;
            t.localScale = s;
        }

        /// <summary>Whether an object carries a script by that name.</summary>
        internal static PlayMakerFSM FsmOn(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (var f in go.GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return f;
            return null;
        }

        /// <summary>The scripts by a name of the scene's objects whose names start so (inactive ones too), in their names' order.</summary>
        internal static List<PlayMakerFSM> SceneFsms(UnityEngine.SceneManagement.Scene scene, string prefix, string fsm)
        {
            var list = new List<PlayMakerFSM>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f != null && f.gameObject.scene == scene && f.FsmName == fsm && f.gameObject.name.StartsWith(prefix)) list.Add(f);
            list.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            return list;
        }

        /// <summary>What a state of a script spawns from the global pool (its prefab), or null.</summary>
        internal static GameObject PoolPrefab(PlayMakerFSM f, string state)
        {
            var s = f != null ? f.Fsm.GetState(state) : null;
            var spawn = Find<SpawnObjectFromGlobalPool>(s);
            return spawn != null && spawn.gameObject != null ? spawn.gameObject.Value : null;
        }

        static readonly Dictionary<System.Type, System.Reflection.FieldInfo[]> speeds = new Dictionary<System.Type, System.Reflection.FieldInfo[]>();
        static readonly HashSet<int> hastened = new HashSet<int>();

        /// <summary>
        /// A boss's summoned enemy quicker: its scripts' speeds and accelerations (their variables and the actions' own
        /// numbers) times <paramref name="speed"/>, its waits at <paramref name="waits"/> of their length (with the kit). Once.
        /// </summary>
        internal static void Hasten(BossKit kit, GameObject go, float speed, float waits)
        {
            if (go == null || !hastened.Add(go.GetInstanceID())) return;
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                foreach (var v in f.FsmVariables.FloatVariables)
                    if (Quick(v.Name)) v.Value *= speed;
                foreach (var s in f.FsmStates)
                    foreach (var a in s.Actions)
                    {
                        BossAi.Tempo(a, kit, waits);
                        foreach (var field in SpeedFields(a.GetType()))
                            if (field.GetValue(a) is FsmFloat ff && !ff.UsesVariable && !ff.IsNone) ff.Value *= speed;
                    }
            }
        }

        static bool Quick(string name) =>
            name != null && (name.IndexOf("Speed", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Accel", System.StringComparison.OrdinalIgnoreCase) >= 0);

        static System.Reflection.FieldInfo[] SpeedFields(System.Type t)
        {
            if (speeds.TryGetValue(t, out var f)) return f;
            var list = new List<System.Reflection.FieldInfo>();
            foreach (var field in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                if (field.FieldType == typeof(FsmFloat) && Quick(field.Name)) list.Add(field);
            return speeds[t] = list.ToArray();
        }
    }
}
