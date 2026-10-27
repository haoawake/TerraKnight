using System.Collections;
using System.Linq;
using HarmonyLib;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "savereset", World.WorldReset, the user's 10-26 "为什么其他存档建造的方块会留到新的存档里面？每个存档的建筑都应该是
    /// 新的"): in Dirtmouth, blocks, walls and a liquid put in, and the player's own world set aside as a guest's would be
    /// (Net.WorldSync's personal snapshot) holding them; then (1) a new game's start (GameHooks.BeforeNewGame), (2) a save
    /// with nothing in it read (ModSave.Read of an empty slot): after each, no block, wall or liquid of the last world is
    /// left, and what a save would write (WorldSync.SaveEdits/SaveWalls) is empty. Lines "[AutoTest] 存档世界 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SaveResetTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 存档世界 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var at = CellGrid.WorldToCell((Vector2)hero.transform.position + new Vector2(3f, 2f));

            void Build()
            {
                for (int y = 0; y < 3; y++) TerraWorld.Set(at.x, at.y + y, CellKind.Stone);
                for (int y = 0; y < 3; y++) TerraWorld.SetWall(at.x + 1, at.y + y, 4);
                WorldLiquids.For("Town")[0] = 200;
                // As a guest would have it: his own world (with these in it) set aside.
                var capture = AccessTools.Method(typeof(Net.WorldSync), "Capture").Invoke(null, null);
                AccessTools.Field(typeof(Net.WorldSync), "personal").SetValue(null, capture);
            }

            string Left()
            {
                int edits = WorldEdits.All.Sum(kv => kv.Value.Count), walls = WorldWalls.All.Sum(kv => kv.Value.Count);
                int savedEdits = Net.WorldSync.SaveEdits().Sum(kv => kv.Value.Count), savedWalls = Net.WorldSync.SaveWalls().Sum(kv => kv.Value.Count);
                return $"方块 {edits}，墙 {walls}，存档会写的方块 {savedEdits}、墙 {savedWalls}";
            }

            bool Empty() => WorldEdits.All.All(kv => kv.Value.Count == 0) && WorldWalls.All.All(kv => kv.Value.Count == 0)
                && Net.WorldSync.SaveEdits().All(kv => kv.Value.Count == 0) && Net.WorldSync.SaveWalls().All(kv => kv.Value.Count == 0);

            Build();
            Plugin.Log.LogInfo($"[AutoTest] 存档世界 放好之后：{Left()}");
            Check(!Empty(), "放进去的方块和墙在（测试本身有效）");
            AccessTools.Method(typeof(GameHooks), "BeforeNewGame").Invoke(null, null);
            Check(Empty(), $"开新档之后什么都不剩：{Left()}");

            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            Build();
            Save.ModSave.Read(29, mod);
            Check(Empty(), $"读一个空存档之后什么都不剩：{Left()}");
            Plugin.Log.LogInfo($"[AutoTest] 存档世界 合计 {ok}/{all}");
        }
    }
}
