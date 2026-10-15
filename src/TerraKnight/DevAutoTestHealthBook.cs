using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator HealthBookTest()
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 百科血量：需要隔离存档，不通过"); yield break; }
            var math = new Dictionary<string, object>();
            foreach (string key in EnemyDrops.DevRecords)
            {
                int at = key.IndexOf('@');
                string record = at >= 0 ? key.Substring(0, at) : key;
                string sceneName = EnemyDrops.SceneOf(key);
                int variant = at >= 0 ? int.Parse(key.Substring(at + 1)) : EnemyDrops.VariantLevel(sceneName);
                string kind = EnemyDrops.KindOf(key);
                int raw = EnemyDrops.HpOf(key);
                bool boss = kind != "miniboss" && (EnemyDrops.IsBoss(key) || raw >= 300);
                var reference = boss ? BossStats.Of(record, variant) : null;
                string ordinary = sceneName.StartsWith("GG_") && kind != "godhome_boss" ? "ordinary-world" : sceneName;
                var info = new EnemyStats.Info { Record = record, Scene = ordinary, MaxHp = raw, Boss = boss, Ref = reference, Level = Areas.LevelOf(sceneName) };
                var levels = new List<object>();
                var saved = GameDifficulty.Current;
                try
                {
                    foreach (DifficultyLevel d in System.Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        GameDifficulty.Current = d;
                        var tune = BossBalance.Of(record, variant);
                        levels.Add(new { difficulty = d.ToString(), life = EnemyStats.LifeWhole(info), pool_life = EnemyStats.LifeMax(info), scale = info.Scale,
                            raw_budget = reference != null ? BossStats.LifeOf(reference, ordinary, d) * tune.Life : EnemyStats.LifeWhole(info),
                            godhome = reference != null ? Mathf.CeilToInt(BossStats.LifeOf(reference, "GG_", d) * tune.Life) : 0 });
                    }
                }
                finally { GameDifficulty.Current = saved; }
                math[key] = new { stage = reference?.Stage ?? Areas.LevelOf(sceneName), boss_reference = reference?.Boss.Name, difficulties = levels };
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "encyclopedia-enemy-health.json"), JsonConvert.SerializeObject(math, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 百科血量 游戏内公式导出 {math.Count} 条、四难度");
            if (DevCopy.Suite == "healthmath") yield break;
            var output = new List<object>();
            foreach (string name in new[] { "Crossroads_10_boss", "Dream_01_False_Knight", "Ruins1_24_boss", "Dream_02_Mage_Lord", "Fungus2_15_boss", "Ruins2_03_boss", "GG_Oblobbles", "GG_Nailmasters", "GG_God_Tamer", "Dream_Final_Boss", "Room_Final_Boss_Core", "Room_Colosseum_Silver", "Room_Colosseum_Gold" })
            {
                var scene = USM.GetSceneByName(name);
                bool loadedHere = !scene.isLoaded;
                if (loadedHere)
                {
                    var op = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    while (op != null && !op.isDone) yield return null;
                    scene = USM.GetSceneByName(name);
                }
                yield return null;
                PlayerData.instance.isInvincible = true;
                var entries = new List<object>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                    {
                        string record = UI.EnemyHover.RecordOf(hm);
                        var info = EnemyStats.Of(hm);
                        var levels = new List<object>();
                        var saved = GameDifficulty.Current;
                        try
                        {
                            foreach (DifficultyLevel d in System.Enum.GetValues(typeof(DifficultyLevel)))
                            {
                                GameDifficulty.Current = d;
                                levels.Add(new { difficulty = d.ToString(), pool = EnemyStats.LifeMax(info), whole = EnemyStats.LifeWhole(info), scale = info.Scale });
                            }
                        }
                        finally { GameDifficulty.Current = saved; }
                        var actions = new List<object>();
                        var hpScale = new Dictionary<string, int>();
                        var scaleObject = HarmonyLib.AccessTools.Field(typeof(HealthManager), "hpScale")?.GetValue(hm);
                        if (scaleObject != null)
                            foreach (var field in scaleObject.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                                if (field.FieldType == typeof(int)) hpScale[field.Name] = (int)field.GetValue(scaleObject);
                        var variables = new List<object>();
                        foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true))
                        {
                            foreach (var v in f.FsmVariables.IntVariables)
                                variables.Add(new { path = PathOf(f.transform), fsm = f.FsmName, name = v.Name, value = v.Value });
                            foreach (var state in f.Fsm.States)
                                foreach (var a in state.Actions)
                                {
                                    string type = a.GetType().Name;
                                    if (!type.Contains("HP") && !type.Contains("Health") && !type.StartsWith("Int") && type != "SetIntValue") continue;
                                    var values = new Dictionary<string, string>();
                                    foreach (var field in a.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                                        if (field.DeclaringType != typeof(HutongGames.PlayMaker.FsmStateAction))
                                        {
                                            try { values[field.Name] = Val(field.GetValue(a)); }
                                            catch (AmbiguousMatchException) { values[field.Name] = field.GetValue(a)?.ToString() ?? "null"; }
                                        }
                                    if (type.Contains("HP") || type.Contains("Health") || (type.StartsWith("Int") || type == "SetIntValue") && string.Join(" ", values.Values).ToLowerInvariant().Contains("hp"))
                                        actions.Add(new { path = PathOf(f.transform), fsm = f.FsmName, state = state.Name, type, values });
                                }
                        }
                        entries.Add(new { path = PathOf(hm.transform), record, active = hm.gameObject.activeInHierarchy, raw_hp = hm.hp, max_hp = info.MaxHp, info.Boss, info.Elite, info.GodhomeTier, hpScale, levels, actions, variables });
                    }
                output.Add(new { scene = name, entries });
                Plugin.Log.LogInfo($"[AutoTest] 百科血量 {name}：{entries.Count} 个部位");
                if (loadedHere)
                {
                    foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
                    var op = USM.UnloadSceneAsync(scene);
                    while (op != null && !op.isDone) yield return null;
                }
            }
            string path = Path.Combine(DevCopy.SaveDir, "encyclopedia-health-parts.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(output, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 百科血量 导出 {output.Count} 个场景到 {path}");
        }
    }
}
