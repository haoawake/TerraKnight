using HarmonyLib;
using HutongGames.PlayMaker.Actions;

namespace TerraKnight.Player
{
    /// <summary>
    /// The Knight's own attack button is switched off (TerraControls: Terraria's use button attacks), but some of the
    /// game's scenes wait for it with the player held: the Beast's Den trap bench leaves him hanging in a web
    /// (Deepnest_Spider_Town, "RestBench Spider" FSM "Fade", state Stuck: four attack presses break it, then he falls
    /// and gets control back). While the game holds the player (no control), Terraria's use button (the left mouse
    /// button by default) is that attack too, for every FSM listening for it (ListenForAttack): the Radiance's last blow
    /// (Dream_Final_Boss and GG_Radiance, "Boss Control" FSM "Control": SLASH), the climb out of the Abyss dream
    /// (Dream_Abyss "Entry Cutscene"), getting up after Vengeful Spirit and after Hornet in Kingdom's Edge, item prompts.
    /// Not over the inventory or the settings, nor on a bench. It also holds while the game shows the Knight by a stand-in
    /// (his own renderer off), whoever gave control back (a guard's release must not leave the Radiance unfinished).
    /// </summary>
    [HarmonyPatch]
    internal static class CutsceneAttack
    {
        public static int DevPresses;

        /// <summary>The game has switched the Knight's own renderer off (SetMeshRenderer): a cutscene's stand-in shows him.</summary>
        public static bool KnightHidden()
        {
            var hero = HeroController.SilentInstance;
            var mr = hero != null ? hero.GetComponent<UnityEngine.MeshRenderer>() : null;
            return mr != null && !mr.enabled && !hero.cState.dead && !hero.cState.transitioning;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ListenForAttack), nameof(ListenForAttack.OnUpdate))]
        static void AlsoUseButton(ListenForAttack __instance)
        {
            var gm = GameManager.UnsafeInstance;
            if (TerraMod.Instance == null || gm == null || gm.isPaused) return;
            // Held by the game, or shown by a stand-in of its own (the Knight's own renderer switched off: the void-born
            // Knight before the Radiance, the Knight in the web) even if something gave control back meanwhile.
            if (GameHooks.PlayerHasControl() && !KnightHidden()) return;
            // A click on the inventory or the settings is theirs; resting on a bench, the game's own keys get up
            // (a click there would stand the player up while he sorts his things).
            if (Items.InventoryGui.Open || Items.InventoryGui.MouseOverUi || UI.IngameOptions.AnyOpen || (PlayerData.instance != null && PlayerData.instance.atBench)) return;
            // Only while the state that listens is still the one running (the game's own presses may have moved it on).
            if (__instance.Fsm == null || __instance.State != __instance.Fsm.ActiveState) return;
            if (__instance.wasPressed != null && TerraKeys.Pressed(TerraKeys.Action.UseItem))
            {
                DevPresses++;
                __instance.Fsm.Event(__instance.wasPressed);
                return;
            }
            if (__instance.isPressed != null && TerraKeys.Held(TerraKeys.Action.UseItem)) __instance.Fsm.Event(__instance.isPressed);
        }
    }
}
