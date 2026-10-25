using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hollow Knight's props show the nail's strike when the nail hits them (a white flash, a slash across, a spell's
    /// splash), and a Terraria hit on one is passed to it as the nail's (PropHits' stand-in slash, MeleeHits' HitTaker hit,
    /// the geo deposit's TAKE DAMAGE). A Terraria weapon has no nail: on its hit the prop shows only its own breaking (its
    /// debris, dust, flung pieces and sound), not the nail's strike.
    /// <list type="bullet">
    /// <item>The props whose own code spawns the strike (pots and jars, poles, grass, vines, lifeblood cocoons, stalactites,
    /// levers, statue levers): while one takes a Terraria hit, the effect prefabs it keeps for the nail and spells are
    /// recycled as they are spawned (only those: <see cref="fxFields"/>).</item>
    /// <item>Fog Canyon's eggs: the strike flash they switch on is switched off again.</item>
    /// <item>The props run by a script (gate and lift levers, breakable walls and floors, chests, windows, geo deposits):
    /// while a Terraria hit is landing (<see cref="Active"/>), a strike their script creates or spawns isn't (by its prefab's
    /// name: one of the props' strikes above, or a "strike", "nail" or "slash" one), nor a geo deposit's hit flash.</item>
    /// </list>
    /// An enemy's own hit effects are left as they are (its script is under a HealthManager).
    /// </summary>
    internal static class PropHitFx
    {
        public static int DevDropped;
        static readonly HashSet<string> logged = new HashSet<string>();

        // ------------------------------------------------------------------ when

        static float until = -1f;

        /// <summary>A Terraria hit is landing on a prop (its stand-in slash, a HitProp, a geo deposit's strike): for half a second.</summary>
        public static void Mark()
        {
            Install();
            until = Time.time + 0.5f;
        }

        static bool Active => Time.time <= until || NailAttackProxy.OutNow;

        // ------------------------------------------------------------------ the components' own strikes

        /// <summary>Each prop's fields holding its nail's and spell's strike prefabs (a GameObject, a Transform or an array of them).</summary>
        static readonly (string Type, string Hit, string[] Fields)[] fxFields =
        {
            ("Breakable", "Hit", new[] { "strikeEffectPrefab", "nailHitEffectPrefab", "spellHitEffectPrefab" }),
            ("BreakablePole", "Hit", new[] { "slashImpactPrefab" }),
            ("BreakableObject", "OnTriggerEnter2D", new[] { "objectNailEffectPrefab", "midpointNailEffectPrefab", "spellHitEffectPrefab" }),
            ("BreakablePoleSimple", "OnTriggerEnter2D", new[] { "slashEffectPrefab" }),
            ("GrassCut", "OnTriggerEnter2D", new[] { "cutEffectPrefab" }),
            ("VinePlatformCut", "OnTriggerEnter2D", new[] { "cutEffectPrefab" }),
            ("HealthCocoon", "OnTriggerEnter2D", new[] { "slashEffects" }),
            ("StalactiteControl", "OnTriggerEnter2D", new[] { "nailStrikePrefab" }),
            ("BridgeLever", "OnTriggerEnter2D", new[] { "strikeNailPrefab" }),
            ("BossStatueLever", "OnTriggerEnter2D", new[] { "strikeNailPrefab" }),
        };

        static readonly Dictionary<System.Type, FieldInfo[]> fieldsOf = new Dictionary<System.Type, FieldInfo[]>();
        /// <summary>The prefabs being spawned now that are a prop's strike for a Terraria hit (counted: a prop's handler within another's).</summary>
        static readonly Dictionary<GameObject, int> blocked = new Dictionary<GameObject, int>();
        /// <summary>The names of the props' strike prefabs seen so far: a script's strike of the same name is one too.</summary>
        static readonly HashSet<string> strikeNames = new HashSet<string>();
        static readonly List<GameObject> found = new List<GameObject>();

        static void Collect(object component)
        {
            found.Clear();
            if (component == null || !fieldsOf.TryGetValue(component.GetType(), out var fields)) return;
            foreach (var f in fields)
            {
                var v = f.GetValue(component);
                if (v is GameObject go) Add(go);
                else if (v is Component c && c != null) Add(c.gameObject);
                else if (v is GameObject[] gos) foreach (var g in gos) Add(g);
                else if (v is Transform[] ts) foreach (var t in ts) if (t != null) Add(t.gameObject);
            }
        }

        static void Add(GameObject go)
        {
            if (go == null) return;
            found.Add(go);
            strikeNames.Add(go.name);
        }

        /// <summary>A prop's handler starts on a Terraria hit: its strikes blocked until it ends.</summary>
        static void Block(object component)
        {
            Collect(component);
            foreach (var go in found) blocked[go] = (blocked.TryGetValue(go, out int n) ? n : 0) + 1;
        }

        static void Unblock(object component)
        {
            Collect(component);
            foreach (var go in found)
            {
                if (!blocked.TryGetValue(go, out int n)) continue;
                if (n <= 1) blocked.Remove(go);
                else blocked[go] = n - 1;
            }
        }

        /// <summary>Struck by one of the player's stand-in slashes (a Terraria hit), not the Knight's nail.</summary>
        static bool Terraria(object[] args)
        {
            if (args == null || args.Length == 0) return false;
            if (args[0] is Collider2D col) return col != null && NailAttackProxy.IsAnyProxy(col.gameObject);
            if (args[0] is HitInstance hit) return hit.Source != null && hit.Source.name == MeleeHits.SourceName;
            return false;
        }

        static void Before(object __instance, object[] __args, out bool __state)
        {
            __state = Terraria(__args);
            if (__state) Block(__instance);
        }

        static void After(object __instance, bool __state)
        {
            if (__state) Unblock(__instance);
        }

        static void Spawned(GameObject prefab, GameObject __result)
        {
            if (blocked.Count == 0 || prefab == null || __result == null || !blocked.ContainsKey(prefab)) return;
            Drop(__result, prefab.name);
        }

        static void Drop(GameObject spawned, string name)
        {
            ObjectPool.Recycle(spawned);
            DevDropped++;
            if (logged.Count < 64 && logged.Add(name)) Plugin.Log.LogInfo($"[打击特效] 泰拉瑞亚武器打中场景物件：去掉骨钉打击特效 {name}");
        }

        /// <summary>Fog Canyon's egg bursting on a Terraria hit: its strike flash off again (it keeps its own pop).</summary>
        static void EggBurst(JellyEgg __instance, object[] __args)
        {
            if (!Terraria(__args)) return;
            var flash = eggStrike?.GetValue(__instance) as GameObject;
            if (flash != null && flash.activeSelf)
            {
                flash.SetActive(false);
                DevDropped++;
            }
        }

        static FieldInfo eggStrike;

        // ------------------------------------------------------------------ the scripts' strikes

        static bool Strike(GameObject prefab)
        {
            if (prefab == null) return false;
            string n = prefab.name;
            return strikeNames.Contains(n) || n.IndexOf("strike", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("nail", System.StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("slash", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>A prop's script (none of an enemy's), while a Terraria hit is landing.</summary>
        static bool PropScript(FsmStateAction a)
        {
            var owner = a?.Fsm?.GameObject;
            return owner != null && owner.GetComponentInParent<HealthManager>() == null;
        }

        /// <summary>A geo deposit's hit flash: its Geo Rock script's Hit state's spawn at itself (the geo it flings stays).</summary>
        static bool GeoFlash(FsmStateAction a) => a.Fsm != null && a.Fsm.Name == "Geo Rock" && a.State != null && a.State.Name == "Hit";

        static bool SkipCreate(CreateObject __instance)
        {
            if (!Active || !PropScript(__instance)) return true;
            var prefab = __instance.gameObject?.Value;
            if (!Strike(prefab)) return true;
            Skipped(__instance, prefab.name);
            return false;
        }

        static bool SkipSpawn(SpawnObjectFromGlobalPool __instance)
        {
            if (!Active || !PropScript(__instance)) return true;
            var prefab = __instance.gameObject?.Value;
            if (prefab == null || !(Strike(prefab) || GeoFlash(__instance))) return true;
            Skipped(__instance, prefab.name);
            return false;
        }

        static void Skipped(FsmStateAction a, string name)
        {
            a.Finish();
            DevDropped++;
            if (logged.Count < 64 && logged.Add(name)) Plugin.Log.LogInfo($"[打击特效] 泰拉瑞亚武器打中 {a.Fsm?.GameObject?.name}：去掉骨钉打击特效 {name}");
        }

        // ------------------------------------------------------------------ the patches

        static bool installed;

        /// <summary>The patches, once, as the player's hits are set up (each on its own: one that won't take is left out).</summary>
        internal static void Install()
        {
            if (installed) return;
            installed = true;
            var h = new Harmony(Plugin.Guid + ".prophitfx");
            int ok = 0;
            void Try(string what, System.Action patch)
            {
                try { patch(); ok++; }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[打击特效] {what} 没装上：{e.Message}"); }
            }
            var before = new HarmonyMethod(AccessTools.Method(typeof(PropHitFx), nameof(Before)));
            var after = new HarmonyMethod(AccessTools.Method(typeof(PropHitFx), nameof(After)));
            foreach (var (typeName, hit, names) in fxFields)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) { Plugin.Log.LogWarning($"[打击特效] 没有 {typeName}"); continue; }
                var fields = new List<FieldInfo>();
                foreach (var n in names)
                {
                    var f = AccessTools.Field(type, n);
                    if (f != null) fields.Add(f);
                }
                fieldsOf[type] = fields.ToArray();
                var m = AccessTools.Method(type, hit);
                if (m == null) { Plugin.Log.LogWarning($"[打击特效] {typeName}.{hit} 不在"); continue; }
                Try(typeName + "." + hit, () => h.Patch(m, prefix: before, finalizer: after));
            }
            Try("ObjectPool.Spawn", () => h.Patch(AccessTools.Method(typeof(ObjectPool), nameof(ObjectPool.Spawn), new[] { typeof(GameObject), typeof(Transform), typeof(Vector3), typeof(Quaternion) }),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PropHitFx), nameof(Spawned)))));
            eggStrike = AccessTools.Field(typeof(JellyEgg), "strikeEffect");
            Try("JellyEgg", () => h.Patch(AccessTools.Method(typeof(JellyEgg), "OnTriggerEnter2D"), postfix: new HarmonyMethod(AccessTools.Method(typeof(PropHitFx), nameof(EggBurst)))));
            Try("CreateObject", () => h.Patch(AccessTools.Method(typeof(CreateObject), nameof(CreateObject.OnEnter)), prefix: new HarmonyMethod(AccessTools.Method(typeof(PropHitFx), nameof(SkipCreate)))));
            Try("SpawnObjectFromGlobalPool", () => h.Patch(AccessTools.Method(typeof(SpawnObjectFromGlobalPool), nameof(SpawnObjectFromGlobalPool.OnEnter)), prefix: new HarmonyMethod(AccessTools.Method(typeof(PropHitFx), nameof(SkipSpawn)))));
            Plugin.Log.LogInfo($"[打击特效] 去掉场景物件骨钉打击特效的补丁装上 {ok}/{fxFields.Length + 4}");
        }
    }
}
