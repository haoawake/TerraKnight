using GlobalEnums;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>Harmony patches that tell the mod where it is in the game's lifecycle.</summary>
    [HarmonyPatch]
    internal static class GameHooks
    {
        /// <summary>
        /// Runs after the hero has finished entering a scene. Fires more than once per scene
        /// (respawns, hazard respawns), so listeners must be idempotent.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FinishedEnteringScene))]
        static void AfterFinishedEnteringScene(GameManager __instance)
        {
            if (!IsGameplay(__instance)) return;
            TerraMod.Instance?.OnSceneReady(__instance);
        }

        /// <summary>
        /// The game has just found the new scene's tilemap. This runs before the hero is placed,
        /// enemies Start() or anything raycasts for ground, so the terrain swap happens here.
        /// It is called several times per scene; TerrainSystem.AttachTo is idempotent.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.RefreshTilemapInfo))]
        static void AfterRefreshTilemapInfo(GameManager __instance, string targetScene)
        {
            var map = __instance.tilemap;
            if (map == null || !IsGameplaySceneObject(map.gameObject)) return;
            try
            {
                World.TerrainSystem.AttachTo(map, targetScene);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[{targetScene}] 地形格子化失败，保持原版地形：{e}");
            }
        }

        /// <summary>Every game save (benches, quitting to the menu, death) also writes the mod's save.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "SaveGame", new[] { typeof(int), typeof(System.Action<bool>) })]
        static void AfterSaveGame(int saveSlot)
        {
            if (TerraMod.Instance != null) Save.ModSave.Write(saveSlot, TerraMod.Instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new[] { typeof(SaveGameData), typeof(int) })]
        static void AfterLoadGame(int saveSlot)
        {
            World.LiveWorld.Instance?.ClearAll();
            if (TerraMod.Instance != null) Save.ModSave.Read(saveSlot, TerraMod.Instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartNewGame))]
        static void BeforeNewGame()
        {
            World.LiveWorld.Instance?.ClearAll();
            World.WorldEdits.Clear();
            TerraMod.Instance?.ResetPlayer();
            // Creative if the creative mode button asked for this slot, else an ordinary game; at
            // the difficulty chosen for it (Master when none was, as for the autotests' games).
            var gm = GameManager.instance;
            TerraMod.Instance?.ApplyCreative(gm != null && UI.CreativeMenu.TakePending(gm.profileID));
            World.GameDifficulty.Current = UI.CreativeMenu.TakeDifficulty();
            // A creative save starts with the showcase kit: every class's best, worn and in the bag.
            if (TerraMod.Instance != null && TerraMod.Instance.Creative) Items.ShowcaseKit.Give(TerraMod.Instance);
            // A new game's map starts empty (the last save's mustn't stay); a creative one shows everything.
            UI.WorldMap.Reset();
            TerraMod.Instance?.ApplyCreativeWorld();
        }

        /// <summary>Gameplay scenes carry the game's per-scene SceneManager with sceneType GAMEPLAY.</summary>
        static bool IsGameplaySceneObject(GameObject go)
        {
            foreach (var root in go.scene.GetRootGameObjects())
            {
                var sm = root.GetComponentInChildren<SceneManager>(true);
                if (sm != null) return sm.sceneType == SceneType.GAMEPLAY;
            }
            return false;
        }

        /// <summary>
        /// The game hides and locks the cursor every OnGUI during gameplay. Terraria aims with
        /// the mouse, so keep it visible while the mod wants it.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InputHandler), "SetCursorEnabled")]
        static void KeepCursor(ref bool isEnabled)
        {
            if (TerraMod.Instance != null && TerraMod.Instance.WantsCursor) isEnabled = true;
        }

        public static bool IsGameplay(GameManager gm) =>
            gm != null
            && gm.IsGameplayScene()
            && gm.gameState != GameState.MAIN_MENU
            && gm.sceneName != "Quit_To_Menu"
            && HeroController.SilentInstance != null;

        /// <summary>True while the player is in control: not paused, in a menu, transition or cutscene.</summary>
        public static bool PlayerHasControl()
        {
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            if (gm == null || hero == null) return false;
            if (gm.isPaused || gm.gameState != GameState.PLAYING || gm.IsInSceneTransition) return false;
            if (Time.timeScale == 0f) return false; // inventory and map pause time without setting isPaused
            return hero.acceptingInput && !hero.controlReqlinquished && !hero.cState.dead && !hero.cState.transitioning;
        }
    }
}
