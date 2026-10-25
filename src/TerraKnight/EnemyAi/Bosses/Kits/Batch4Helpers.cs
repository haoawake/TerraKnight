using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// What the Traitor Lord's, the Oblobbles', the God Tamer's and the warrior dreams' kits share: a boss script's actions
    /// found by their type's name (and their fields read without a compile-time type: HK has its own copies of PlayMaker's),
    /// a stand-in for the hero that a thrown thing's own script aims at (put where the player was locked), the throw that
    /// lands a falling shot on a point, and one fixed-update wait for every routine.
    /// </summary>
    internal static class Batch4
    {
        /// <summary>A routine's wait for the next physics step (one for all: no garbage).</summary>
        internal static readonly WaitForFixedUpdate Fixed = new WaitForFixedUpdate();

        /// <summary>The index of a state's nth action of a type (by its class name), or -1.</summary>
        internal static int IndexOf(FsmState s, string typeName, int nth = 0)
        {
            if (s == null) return -1;
            var actions = s.Actions;
            for (int i = 0; i < actions.Length; i++)
                if (actions[i] != null && actions[i].GetType().Name == typeName && nth-- == 0) return i;
            return -1;
        }

        /// <summary>A state's nth action of a type, or null.</summary>
        internal static FsmStateAction Find(FsmState s, string typeName, int nth = 0)
        {
            int i = IndexOf(s, typeName, nth);
            return i >= 0 ? s.Actions[i] : null;
        }

        static readonly Dictionary<(System.Type, string), System.Reflection.FieldInfo> fields = new Dictionary<(System.Type, string), System.Reflection.FieldInfo>();

        /// <summary>An action's FsmFloat field by name (SetVelocity2d's x or y...), or null.</summary>
        internal static FsmFloat FloatField(FsmStateAction a, string field)
        {
            if (a == null) return null;
            var key = (a.GetType(), field);
            if (!fields.TryGetValue(key, out var fi))
                fields[key] = fi = a.GetType().GetField(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fi != null ? fi.GetValue(a) as FsmFloat : null;
        }

        /// <summary>An action's field set by name (a SpawnObjectFromGlobalPool's storeObject...); false if it has none.</summary>
        internal static bool SetField(FsmStateAction a, string field, object value)
        {
            if (a == null) return false;
            var key = (a.GetType(), field);
            if (!fields.TryGetValue(key, out var fi))
                fields[key] = fi = a.GetType().GetField(field, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi == null || !fi.FieldType.IsInstanceOfType(value)) return false;
            fi.SetValue(a, value);
            return true;
        }

        static readonly Dictionary<System.Type, System.Reflection.FieldInfo[]> objectFields = new Dictionary<System.Type, System.Reflection.FieldInfo[]>();

        /// <summary>
        /// A script's aim at the hero turned to a stand-in: in the states named (all, with none), every action's game object
        /// that is the hero's (the global Hero, or a variable a GetHero of the script fills) reads the stand-in instead.
        /// The number of them turned.
        /// </summary>
        internal static int Retarget(PlayMakerFSM f, GameObject to, params string[] states)
        {
            if (f == null || to == null) return 0;
            var heroVars = new HashSet<string> { "Hero" };
            foreach (var s in f.FsmStates)
                foreach (var a in s.Actions)
                    if (a != null && a.GetType().Name == "GetHero")
                    {
                        var store = FieldsOf(a.GetType());
                        foreach (var fi in store)
                            if (fi.GetValue(a) is FsmGameObject g && g.UseVariable && !string.IsNullOrEmpty(g.Name)) heroVars.Add(g.Name);
                    }
            int n = 0;
            var aim = new FsmGameObject { Value = to };
            foreach (var s in f.FsmStates)
            {
                if (states.Length > 0 && System.Array.IndexOf(states, s.Name) < 0) continue;
                foreach (var a in s.Actions)
                {
                    if (a == null || a.GetType().Name == "GetHero") continue;
                    foreach (var fi in FieldsOf(a.GetType()))
                        if (fi.GetValue(a) is FsmGameObject g && g.UseVariable && heroVars.Contains(g.Name))
                        {
                            to.GetComponent<KitAimProxy>()?.Remember(a, fi, g, aim);
                            fi.SetValue(a, aim);
                            n++;
                        }
                }
            }
            return n;
        }

        static System.Reflection.FieldInfo[] FieldsOf(System.Type t)
        {
            if (objectFields.TryGetValue(t, out var list)) return list;
            var found = new List<System.Reflection.FieldInfo>();
            foreach (var fi in t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                if (fi.FieldType == typeof(FsmGameObject)) found.Add(fi);
            return objectFields[t] = found.ToArray();
        }

        /// <summary>
        /// A stand-in for the hero in a boss's scene (one per name), for a thrown thing's own script to aim at: put where the
        /// player was locked, it turns and fires there, not after him (its warning drawn by the kit).
        /// </summary>
        internal static GameObject Proxy(BossKit kit, string name)
        {
            var scene = kit.gameObject.scene;
            string key = kit.GetInstanceID() + ":" + name;
            if (proxies.TryGetValue(key, out var go) && go != null && go.scene == scene) return go;
            go = new GameObject("TK Aim " + name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var tracker = go.AddComponent<KitAimProxy>();
            tracker.Owner = kit; tracker.Key = key;
            proxies[key] = go;
            return go;
        }

        internal static void ForgetProxy(string key, GameObject go)
        {
            if (key != null && proxies.TryGetValue(key, out var stored) && ReferenceEquals(stored, go)) proxies.Remove(key);
        }

        static readonly Dictionary<string, GameObject> proxies = new Dictionary<string, GameObject>();

        /// <summary>
        /// The velocity that lands a falling shot (gravity <paramref name="g"/> units/s², launched up at <paramref name="vy"/>)
        /// on a point: its time to come down there, its x speed from it.
        /// </summary>
        internal static Vector2 Lob(Vector2 from, Vector2 to, float vy, float g)
        {
            float drop = from.y - to.y;
            float disc = vy * vy + 2f * g * drop;
            float t = disc > 0f ? (vy + Mathf.Sqrt(disc)) / g : Mathf.Max(0.3f, 2f * vy / g);
            return new Vector2((to.x - from.x) / Mathf.Max(0.2f, t), vy);
        }

        /// <summary>The angle (degrees) from one point to another.</summary>
        internal static float AngleTo(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
    }

    internal sealed class KitAimProxy : MonoBehaviour
    {
        internal BossKit Owner;
        internal string Key;
        readonly List<(object Action, System.Reflection.FieldInfo Field, FsmGameObject Original, FsmGameObject Replacement)> bindings
            = new List<(object, System.Reflection.FieldInfo, FsmGameObject, FsmGameObject)>();
        internal bool HasBindings => bindings.Count > 0;

        internal void Remember(object action, System.Reflection.FieldInfo field, FsmGameObject original, FsmGameObject replacement)
            => bindings.Add((action, field, original, replacement));

        internal void Restore()
        {
            foreach (var b in bindings)
                if (ReferenceEquals(b.Field.GetValue(b.Action), b.Replacement)) b.Field.SetValue(b.Action, b.Original);
            bindings.Clear();
        }

        void Update()
        {
            if (Owner != null && Owner.Active) return;
            Restore();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            Restore();
            Batch4.ForgetProxy(Key, gameObject);
        }
    }
}
