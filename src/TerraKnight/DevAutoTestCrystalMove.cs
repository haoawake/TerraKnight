using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "crystalmove"): a save from 2.4.1 with crystals in later stages' scenes, loaded: the
    /// ones not dug up are moved to scenes of the first two stages, a dug-up one stays where it was, and
    /// the save keeps 24. The save's own crystals are put back after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CrystalMoveTest()
        {
            yield return new WaitForSeconds(1f);
            int seed0 = LifeCrystals.Seed;
            var saved0 = LifeCrystals.Save();
            var old = new List<LifeCrystals.Saved>();
            string[] early = { "Crossroads_01", "Crossroads_03", "Fungus1_01", "Fungus2_01", "Cliffs_01", "Tutorial_01" };
            string[] later = { "Ruins1_01", "Ruins2_01", "Waterways_01", "Mines_01", "Deepnest_01", "Fungus3_10" };
            foreach (var s in early) old.Add(new LifeCrystals.Saved { Scene = s });
            foreach (var s in later) old.Add(new LifeCrystals.Saved { Scene = s, Taken = s == "Mines_01" });
            LifeCrystals.Load(12345, old);
            var scenes = LifeCrystals.DevScenes();
            int laterLeft = scenes.Count(s => Areas.LevelOf(s) > LifeCrystals.LastStage);
            bool takenKept = scenes.Contains("Mines_01");
            bool earlyKept = early.All(scenes.Contains);
            bool pass = scenes.Count == LifeCrystals.PerSave && laterLeft == 1 && takenKept && earlyKept && scenes.Distinct().Count() == scenes.Count;
            Plugin.Log.LogInfo($"[AutoTest] 生命水晶迁移 老存档 6 个在前两个阶段、6 个在后面（1 个已挖）：读档后 {scenes.Count} 个，后面阶段还剩 {laterLeft} 个（已挖的 Mines_01 留着 {takenKept}），原来的前期场景都在 {earlyKept}，场景不重复 {scenes.Distinct().Count() == scenes.Count}：{string.Join("、", scenes.Select(s => s + "/" + Areas.LevelOf(s)))} {(pass ? "通过" : "不通过")}");
            LifeCrystals.Load(seed0, saved0);
            Plugin.Log.LogInfo($"[AutoTest] 生命水晶迁移 放回本存档的 {LifeCrystals.DevCount} 个");
        }
    }
}
