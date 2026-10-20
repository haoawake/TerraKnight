using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>Enemy replicas must never make a distant scene's FSM move or capture the local hero.</summary>
    [HarmonyPatch]
    internal static class BattleIsolation
    {
        static readonly HashSet<string> reported = new HashSet<string>();
        public static int DevBlocked;
        static string Room(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        static bool BattleController(Fsm from)
        {
            var source = from?.GameObject;
            if (source == null) return false;
            var health = source.GetComponentInParent<HealthManager>();
            if (health != null && Items.EnemyStats.Of(health).Boss) return true;
            if (source.GetComponentInParent<BossSceneController>() != null) return true;
            string name = source.name + "/" + from.Name;
            foreach (string word in new[] { "Boss", "Battle", "Roar", "Dream Scene" })
                if (name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool Allows(Fsm from)
        {
            if (!NetSession.Active || from?.GameObject == null) return true;
            var source = from.GameObject;
            var hero = HeroController.SilentInstance;
            if (hero == null || source == hero.gameObject || source.transform.IsChildOf(hero.transform)) return true;
            string scene = source.scene.name;
            bool remote = Enemies.RemoteOwned(scene);
            // A dream or a Godhome room is a fight when it is a boss's room, or another player's: the Seer's dream
            // (Dream_Nailcollection), the warriors' graves, the shrines are the player's own scenes.
            bool bossScene = BossRooms.IsRoom(scene) || (remote && (scene.StartsWith("GG_") || scene.StartsWith("Dream_")));
            if (!bossScene && !remote) return true;
            // The scene the player is in and runs himself: its scripts are his own, also while it loads (the grid still
            // names the room left until the new one is in place, and its fade and transition must go through).
            if (!remote && (source.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene() || (GameManager.UnsafeInstance?.IsInSceneTransition ?? false))) return true;
            var cell = CellGrid.WorldToCell(hero.transform.position);
            string room = Room(scene);
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            string here = Room(region?.SceneName ?? NetScenes.Current);
            bool allowed = here == room;
            // A player can remain in the same seamless room, but outside the actual arena.
            var arenaRegion = TerrainSystem.Find(room);
            if (allowed && bossScene && BattleController(from) && arenaRegion?.OwnZone != null)
                allowed = arenaRegion.OwnZone.Value.Contains(hero.transform.position);
            if (allowed) return true;
            DevBlocked++;
            string key = scene + "/" + source.name + "/" + from.Name + ":" + from.ActiveStateName;
            if (reported.Add(key)) Plugin.Log.LogInfo("[联机] 隔离未参与战斗的本地主角，拦截 " + key);
            return false;
        }

        public static bool AllowsCurrent() => Allows(FsmExecutionStack.ExecutingFsm);
        public static bool Allows(SetPosition action)
        {
            var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            return AllowsTarget(action.Fsm, target);
        }

        public static bool AllowsTarget(Fsm from, GameObject target)
        {
            var hero = HeroController.SilentInstance;
            if (target == null || hero == null || target != hero.gameObject) return true;
            if (!Allows(from)) return false;
            var source = from?.GameObject;
            if (NetSession.Active && source != null && source != hero.gameObject && Enemies.RemoteOwned(source.scene.name)
                && (BossRooms.IsRoom(source.scene.name) || source.scene.name.StartsWith("GG_") || source.scene.name.StartsWith("Dream_"))
                && BattleController(from)
                && !(GameManager.UnsafeInstance?.IsInSceneTransition ?? false))
            {
                // Even a participant keeps their own position: a replica's scripted staging
                // must not snap the local player to the scene owner's entrance or boss.
                DevBlocked++;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControl))]
        static bool Relinquish() => AllowsCurrent();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControlNotVelocity))]
        static bool RelinquishWithoutVelocity() => AllowsCurrent();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Fsm), nameof(Fsm.ProcessEvent))]
        static bool Event(Fsm __instance, FsmEvent fsmEvent)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || __instance.GameObject != hero.gameObject || fsmEvent?.Name != "ROAR ENTER") return true;
            return AllowsCurrent();
        }
    }
}
