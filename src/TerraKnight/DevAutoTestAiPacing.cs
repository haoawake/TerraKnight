using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.EnemyAi;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator AiPacingTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("isolated saves required");
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            World.GameDifficulty.Current = World.DifficultyLevel.Master;
            _ = Profiles.Count;
            var profiles = (Dictionary<string, Profile>)AccessTools.Field(typeof(Profiles), "all").GetValue(null);
            var inventory = profiles.SelectMany(p => p.Value.Moves.Where(m => m.Kind == Kind.Shot)
                .Select(m => new { Key = p.Key, Before = m, After = m.ForExecution() })).ToArray();
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "ai-pacing.json"), JsonConvert.SerializeObject(inventory, Formatting.Indented));
            FxCheck("All ranged moves have one warned volley", inventory.All(p => p.After.Interval == 0 && p.After.Repeat == 0
                && p.After.Count <= (p.After.Aim == Aim.Radial ? 6 : 3) && p.After.Antic * EnemyAi.EnemyAi.AnticScale >= .65f
                && p.After.Recover >= .8f), "ranged=" + inventory.Length);
            foreach (string key in new[] { "Spitter", "SuperSpitter", "MawlekTurret" })
            {
                yield return GoTo("Crossroads_21", "left1");
                var hero = HeroController.instance;
                yield return new WaitForSeconds(1f);
                BookSpawner.Spawn(key);
                Brain brain = null;
                for (float until = Time.time + 16f; Time.time < until && brain == null;)
                {
                    brain = Object.FindObjectsByType<Brain>(FindObjectsSortMode.None).FirstOrDefault(b => b.Record == key && b.name.Contains("图鉴"));
                    yield return null;
                }
                FxCheck(key + " spawned", brain != null, BookSpawner.DevLast);
                if (brain == null) continue;
                var hm = brain.GetComponent<HealthManager>();
                World.EnemyAggro.Struck(hm, EnemyStats.Of(hm));
                yield return new WaitForSeconds(2f);
                AiCall(brain, "End", false);
                brain.enabled = false;
                int index = profiles[key].Moves.FindLastIndex(m => m.Kind == Kind.Shot);
                AiCall(brain, "Begin", index, Time.time);
                var move = AiGet<Move>(brain, "cur");
                Vector2 locked = AiGet<Vector2>(brain, "aimPoint");
                float began = Time.time;
                int before = Brain.DevOwnShots;
                FxCheck(key + " native attack paused during wind-up", AiGet<bool>(brain, "seized"), brain.DevState);
                Player.TerraCamera.DevLook = brain.Position;
                yield return new WaitForEndOfFrame();
                Shot("Crossroads_21", key + "_windup");
                hero.transform.position += Vector3.left * 2f;
                for (float until = Time.time + 2.5f; Time.time < until && brain != null;)
                {
                    AiCall(brain, "Tick", Time.time);
                    yield return null;
                }
                Player.TerraCamera.DevLook = null;
                FxCheck(key + " one projectile volley", Brain.DevOwnShots - before == move.Count,
                    "expected=" + move.Count + " fired=" + (Brain.DevOwnShots - before) + " time=" + (Time.time - began));
                FxCheck(key + " aim remains locked", AiGet<Vector2>(brain, "aimPoint") == locked, "locked=" + locked);
                FxCheck(key + " native attack restored", !AiGet<bool>(brain, "seized"), brain.DevState);
                Object.Destroy(brain.gameObject);
                yield return null;
            }
        }
    }
}
