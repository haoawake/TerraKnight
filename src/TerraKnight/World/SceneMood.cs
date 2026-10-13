using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;

namespace TerraKnight.World
{
    /// <summary>
    /// What a scene sets up for the player when it is entered: colour grading, ambient and hero
    /// light, footstep environment, atmosphere and music, darkness. The game does this once in its
    /// SceneManager's Start; walking back into a neighbour that is still loaded needs it again.
    /// </summary>
    internal static class SceneMood
    {
        /// <summary>
        /// Hollow Knight's darkness (config Gameplay.HollowKnightDarkness): the scene's darkness
        /// level and the darkness regions inside scenes close the vignette round the player to a
        /// small circle where the Lumafly Lantern is wanted (Howling Cliffs' dark edges, Deepnest,
        /// Crystal Peak's mines). Off by default: a level above the ordinary 0 is put back to 0 as
        /// soon as anything raises it. Lighter levels (below 0) are left alone.
        /// </summary>
        public static bool KeepDarkness;
        static PlayMakerFSM darknessFsm;
        public static int DevDarknessLowered;

        /// <summary>Call every frame in gameplay.</summary>
        public static void KeepLight()
        {
            if (KeepDarkness) return;
            if (darknessFsm == null)
            {
                var vignette = GameObject.FindGameObjectWithTag("Vignette");
                darknessFsm = vignette != null ? FSMUtility.LocateFSM(vignette, "Darkness Control") : null;
                if (darknessFsm == null) return;
            }
            var level = darknessFsm.FsmVariables.GetFsmInt("Darkness Level");
            if (level == null || level.Value <= 0) return;
            level.Value = 0;
            darknessFsm.SendEvent("RESET");
            DevDarknessLowered++;
        }

        static readonly AccessTools.FieldRef<SceneManager, AtmosCue> atmosCue = AccessTools.FieldRefAccess<SceneManager, AtmosCue>("atmosCue");
        static readonly AccessTools.FieldRef<SceneManager, MusicCue> musicCue = AccessTools.FieldRefAccess<SceneManager, MusicCue>("musicCue");
        static readonly AccessTools.FieldRef<SceneManager, MusicCue> infectedMusicCue = AccessTools.FieldRefAccess<SceneManager, MusicCue>("infectedMusicCue");
        static readonly AccessTools.FieldRef<SceneManager, AudioMixerSnapshot> musicSnapshot = AccessTools.FieldRefAccess<SceneManager, AudioMixerSnapshot>("musicSnapshot");
        static readonly AccessTools.FieldRef<SceneManager, float> musicDelayTime = AccessTools.FieldRefAccess<SceneManager, float>("musicDelayTime");
        static readonly AccessTools.FieldRef<SceneManager, float> musicTransitionTime = AccessTools.FieldRefAccess<SceneManager, float>("musicTransitionTime");

        public static void Apply(SceneManager sm)
        {
            var gm = GameManager.instance;
            var gc = GameCameras.instance;
            var pd = PlayerData.instance;
            var hero = HeroController.instance;

            float saturation = sm.AdjustSaturation(sm.saturation);
            gc.colorCorrectionCurves.saturation = saturation;
            gc.colorCorrectionCurves.redChannel = sm.redChannel;
            gc.colorCorrectionCurves.greenChannel = sm.greenChannel;
            gc.colorCorrectionCurves.blueChannel = sm.blueChannel;
            gc.colorCorrectionCurves.UpdateParameters();
            gc.sceneColorManager.SaturationA = saturation;
            gc.sceneColorManager.RedA = sm.redChannel;
            gc.sceneColorManager.GreenA = sm.greenChannel;
            gc.sceneColorManager.BlueA = sm.blueChannel;
            SceneManager.SetLighting(sm.defaultColor, sm.defaultIntensity);
            gc.sceneColorManager.AmbientColorA = sm.defaultColor;
            gc.sceneColorManager.AmbientIntensityA = sm.defaultIntensity;
            if (hero != null) hero.heroLight.color = sm.heroLightColor;
            gc.sceneColorManager.HeroLightColorA = sm.heroLightColor;
            gc.sceneColorManager.UpdateScript(forceUpdate: true);
            pd.environmentType = sm.environmentType;
            pd.environmentTypeDefault = sm.environmentType;

            if (atmosCue(sm) != null) gm.AudioManager.ApplyAtmosCue(atmosCue(sm), sm.transitionTime);
            var music = pd.crossroadsInfected && infectedMusicCue(sm) != null ? infectedMusicCue(sm) : musicCue(sm);
            if (music != null) gm.AudioManager.ApplyMusicCue(music, musicDelayTime(sm), musicTransitionTime(sm), applySnapshot: false);
            if (musicSnapshot(sm) != null) gm.AudioManager.ApplyMusicSnapshot(musicSnapshot(sm), musicDelayTime(sm), musicTransitionTime(sm));
            if (sm.enviroSnapshot != null) sm.enviroSnapshot.TransitionTo(sm.transitionTime);
            if (sm.actorSnapshot != null) sm.actorSnapshot.TransitionTo(sm.transitionTime);
            if (sm.shadeSnapshot != null) sm.shadeSnapshot.TransitionTo(sm.transitionTime);

            var vignette = GameObject.FindGameObjectWithTag("Vignette");
            var darkness = vignette != null ? FSMUtility.LocateFSM(vignette, "Darkness Control") : null;
            if (darkness != null)
            {
                FSMUtility.SetInt(darkness, "Darkness Level", sm.darknessLevel);
                darkness.SendEvent(sm.noLantern ? "SCENE RESET NO LANTERN" : "RESET");
            }
            if (hero != null)
            {
                hero.SetDarkness(sm.darknessLevel);
                hero.checkEnvironment();
            }
        }
    }
}
