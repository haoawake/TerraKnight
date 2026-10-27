using UnityEngine;
using HarmonyLib;

namespace TerraKnight.Player
{
    /// <summary>The Abyss altar still owns its upgrade ceremony. Its cast trigger needs
    /// an interaction equivalent because the Terraria player has no native spell button.</summary>
    [HarmonyPatch]
    internal static class SpellAltars
    {
        static float nextHint;
        static PlayMakerFSM shadeShaman;

        [HarmonyPostfix, HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        static void Watch(PlayMakerFSM __instance)
        {
            if (__instance.FsmName == "Ruins Shaman" && __instance.gameObject.scene.name == "Ruins1_31b")
                shadeShaman = __instance;
        }

        internal static PlayMakerFSM ShriekAltar(HeroController hero)
        {
            var pd = PlayerData.instance;
            if (TerraMod.Instance == null || hero == null || pd == null || pd.screamLevel != 1 ||
                GameManager.UnsafeInstance == null || GameManager.instance.sceneName != "Abyss_12") return null;
            foreach (var fsm in hero.GetComponents<PlayMakerFSM>())
                if (fsm.FsmName == "Spell Control" && fsm.FsmVariables.FindFsmBool("Scream 2 Zone")?.Value == true)
                    return fsm;
            return null;
        }

        internal static bool TryShriek(HeroController hero)
        {
            var fsm = ShriekAltar(hero);
            if (fsm == null || !hero.CanInput() || !hero.cState.onGround || GameManager.instance.isPaused ||
                Items.InventoryGui.MouseOverUi || UI.IngameOptions.MouseOver || Net.NetChat.Typing ||
                fsm.ActiveStateName != "Inactive") return false;
            // This is the same branch selected by casting Howling Wraiths in the native zone.
            // It broadcasts SCREAM GET and waits for the scene's ceremony to finish.
            fsm.SetState("SG Antic");
            return true;
        }

        internal static void Tick(HeroController hero)
        {
            if (hero == null) return;
            // Keep the inspect/confirmation sequence and all of its native eligibility checks.
            // The Terraria interaction binding is an alternative to pressing Up at the statue.
            if (shadeShaman != null && shadeShaman.isActiveAndEnabled &&
                shadeShaman.gameObject.scene.name == GameManager.UnsafeInstance?.sceneName &&
                shadeShaman.ActiveStateName == "In Range" && PlayerData.instance.fireballLevel < 2 &&
                !Items.InventoryGui.MouseOverUi && !UI.IngameOptions.MouseOver && hero.CanInput() &&
                TerraKeys.Pressed(TerraKeys.Action.Interact))
                shadeShaman.SendEvent("UP PRESSED");
            if (ShriekAltar(hero) == null) { nextHint = 0; return; }
            if (TerraKeys.Pressed(TerraKeys.Action.Interact)) TryShriek(hero);
            if (!hero.CanInput() || Time.unscaledTime < nextHint) return;
            nextHint = Time.unscaledTime + 6f;
            UI.CenterNotice.Show(Lang.T("在祭坛上按交互键（默认右键），吸收深渊尖啸"));
        }
    }
}
