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
            // Each step on its own: one that fails is logged and the rest go on, and the game's new
            // game is never stopped by it (a failure here used to leave the player on the menu).
            // Everything the last world had built or changed goes, its walls too (World.WorldReset).
            Safely("清空世界", World.WorldReset.All);
            Safely("重置角色", () => TerraMod.Instance?.ResetPlayer());
            // Creative if the creative mode button asked for this slot, else an ordinary game; at
            // the difficulty chosen for it (Master when none was, as for the autotests' games).
            var gm = GameManager.instance;
            if (gm != null) Save.ModSave.BeganNewGame(gm.profileID);
            // A new game's geo and essence are the items from the start.
            Items.GeoWallet.Reset(true);
            Items.EssenceWallet.Reset(true);
            Safely("模式和难度", () =>
            {
                TerraMod.Instance?.ApplyCreative(gm != null && UI.CreativeMenu.TakePending(gm.profileID));
                World.GameDifficulty.Current = UI.CreativeMenu.TakeDifficulty();
                Items.BossGuide.NewGame(UI.CreativeMenu.TakeGuide());
            });
            // The enemy AI, the character's look and the names chosen on the start menu's creation screens.
            Safely("世界与人物", UI.TerraMenu.ApplyNewGame);
            // A creative save starts with the showcase kit: every class's best, worn and in the bag.
            if (TerraMod.Instance != null && TerraMod.Instance.Creative) Safely("创造模式开局装备", () => Items.ShowcaseKit.Give(TerraMod.Instance));
            // A new game's map starts empty (the last save's mustn't stay); a creative one shows everything.
            Safely("地图", () => UI.WorldMap.Reset());
            // Its own Life Crystals in the early areas.
            Safely("生命水晶", () => World.LifeCrystals.NewWorld());
            // Its own chests in every area (World.RegionalChests).
            Safely("地区储物箱", () => World.RegionalChests.NewWorld());
            Safely("创造模式的世界", () => TerraMod.Instance?.ApplyCreativeWorld());
        }

        static void Safely(string what, System.Action step)
        {
            try { step(); }
            catch (System.Exception e) { Plugin.Log.LogError($"[新存档] 开局准备“{what}”出错（游戏照常开始）：{e}"); }
        }

        /// <summary>
        /// The game's live input handler: the GameManager's own. InputHandler.Instance is whichever one
        /// woke last, and back at the menu that is a duplicate GameManager's, destroyed straight away:
        /// keys bound and the pause switched off on it reach nothing (the game's pause came back with
        /// Escape, rebound keys didn't move the Knight).
        /// </summary>
        public static InputHandler Input
        {
            get
            {
                var gm = GameManager.UnsafeInstance;
                var ih = gm != null ? gm.inputHandler : null;
                return ih != null ? ih : InputHandler.Instance;
            }
        }

        /// <summary>GameManager.IsGameplayScene, once a frame: it reads the active scene's name, a new string each time.</summary>
        internal static bool GameplayScene(GameManager gm)
        {
            if (Time.frameCount == gameplayFrame && ReferenceEquals(gm, gameplayOf)) return gameplayScene;
            gameplayFrame = Time.frameCount;
            gameplayOf = gm;
            return gameplayScene = gm.IsGameplayScene();
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

        static int gameplayFrame = -1;
        static GameManager gameplayOf;
        static bool gameplayScene;

        public static bool IsGameplay(GameManager gm) =>
            gm != null
            && GameplayScene(gm)
            && gm.gameState != GameState.MAIN_MENU
            && gm.sceneName != "Quit_To_Menu"
            && HeroController.SilentInstance != null;

        /// <summary>True while the player is in control: not paused, in a menu, transition or cutscene.</summary>
        /// <summary>
        /// A sequence of the game's holds the Knight (an item got and its message, a cutscene, a boss's roar): the map, a
        /// pylon or a teammate's seal mustn't take him away mid-way (a mask shard's award comes at its message's end).
        /// Sitting on a bench isn't one.
        /// </summary>
        public static bool HeldByTheGame()
        {
            var hero = HeroController.SilentInstance;
            var pd = PlayerData.instance;
            if (hero == null) return false;
            if (pd != null && pd.atBench) return false;
            return hero.controlReqlinquished || !hero.acceptingInput || hero.cState.transitioning;
        }

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
