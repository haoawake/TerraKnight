using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator EarlyMagicSingleHit()
        {
            var tp = TerraProjectiles.Instance;
            var at = (Vector2)TargetDummy.DevHm.GetComponent<Collider2D>().bounds.center;
            var records = new List<object>();
            int checks = 0, failures = 0;
            void Check(string name, bool pass) { checks++; if (!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] GEM_BALANCE {name} {(pass ? "通过" : "不通过")}"); }
            foreach (var feature in new[] { TerraProjectiles.GemFx.Bounce, TerraProjectiles.GemFx.Bigger, TerraProjectiles.GemFx.FastSlow }) {
                EarlyMagicEquip(ItemDb.Get(ItemType.AmethystStaff), "bare");
                var fx = TerraProjectiles.GemFx.Homing | feature;
                int hits = TerraProjectiles.DevHit(ProjKind.AmethystBolt);
                tp.ShootGem(ProjKind.AmethystBolt, at, Vector2.zero, 10, 0, 0, 0, fx);
                tp.DevGemIgnoreTerrainForAudit();
                yield return new WaitForSeconds(.8f);
                int first = TerraProjectiles.DevHit(ProjKind.AmethystBolt) - hits;
                Check(feature + "/single-projectile-hits-once", first == 1);
                var live = (System.Collections.IList)AccessTools.Field(typeof(TerraProjectiles), "live").GetValue(tp);
                Check(feature + "/piercing-shot-survives", live.Count == 1);
                if (live.Count == 1) {
                    var p = live[0];
                    var immune = (Dictionary<int, int>)AccessTools.Field(p.GetType(), "Immune").GetValue(p);
                    Check(feature + "/permanent-hit-history", immune.TryGetValue(TargetDummy.DevHm.GetInstanceID(), out int ticks) && ticks == int.MaxValue);
                    Check(feature + "/hit-target-not-reacquired", AccessTools.Method(typeof(TerraProjectiles), "Nearest").Invoke(null, new object[] { p, 1000f }) == null);
                    // A second enemy may still be pierced and selected: use the same live dummy at a new
                    // instance ID only in a separate encounter fixture; never reset this shot's hit history.
                }
                tp.ShootGem(ProjKind.AmethystBolt, at, Vector2.zero, 10, 0, 0, 0, fx);
                tp.DevGemIgnoreTerrainForAudit();
                yield return new WaitForSeconds(.8f);
                int both = TerraProjectiles.DevHit(ProjKind.AmethystBolt) - hits;
                Check(feature + "/new-shot-can-hit-same-target", both == 2);
                records.Add(new { Feature = feature.ToString(), FirstShotHits = first, TwoShotsHits = both });
            }
            EarlyMagicReset();
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "early-magic-single-hit.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Records = records }, Formatting.Indented));
        }

        IEnumerator EarlyMagicTracking()
        {
            var tp = TerraProjectiles.Instance;
            var dummy = TargetDummy.DevHm;
            var at = (Vector2)dummy.GetComponent<Collider2D>().bounds.center;
            var method = AccessTools.Method(typeof(TerraProjectiles), "NearestEnemy");
            Vector2? Seek(Vector2 from, float radius) => (Vector2?)method.Invoke(tp, new object[] { from, radius });
            int checks = 0, failures = 0;
            void Check(string name, bool pass) { checks++; if (!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] TRACKING {name} {(pass ? "通过" : "不通过")}"); }
            Check("unobstructed-target", Seek(at + Vector2.up * 2, 3).HasValue);
            Check("out-of-range-released", !Seek(at + Vector2.up * 2, 1).HasValue);
            dummy.isDead = true;
            Check("dead-target-released", !Seek(at + Vector2.up * 2, 3).HasValue);
            dummy.isDead = false;
            dummy.gameObject.SetActive(false);
            Check("inactive-target-released", !Seek(at + Vector2.up * 2, 3).HasValue);
            dummy.gameObject.SetActive(true);
            var wall = new GameObject("Tracking audit wall"); wall.layer = ChunkColliders.TerrainLayer;
            wall.transform.position = at + Vector2.up;
            wall.AddComponent<BoxCollider2D>().size = new Vector2(3, .3f);
            Physics2D.SyncTransforms();
            Check("wall-breaks-line-of-sight", !Seek(at + Vector2.up * 2, 3).HasValue);
            Object.Destroy(wall); yield return null; Physics2D.SyncTransforms();
            Check("visible-target-reacquired", Seek(at + Vector2.up * 2, 3).HasValue);

            EarlyMagicReset();
            var cases = new[] {
                ("amethyst", ProjKind.AmethystBolt, TerraProjectiles.GemFx.Homing, 300),
                ("amber-alone", ProjKind.AmberBolt, TerraProjectiles.GemFx.Bounce, 300),
                ("amber-amethyst-robe", ProjKind.AmberBolt, TerraProjectiles.GemFx.Bounce | TerraProjectiles.GemFx.Homing, 300),
                ("amethyst-amber-robe", ProjKind.AmethystBolt, TerraProjectiles.GemFx.Bounce | TerraProjectiles.GemFx.Homing, 300),
                ("emerald-amethyst-robe", ProjKind.EmeraldBolt, TerraProjectiles.GemFx.FastSlow | TerraProjectiles.GemFx.Homing, 600),
                ("amethyst-emerald-robe", ProjKind.AmethystBolt, TerraProjectiles.GemFx.FastSlow | TerraProjectiles.GemFx.Homing, 600),
            };
            var ids = new Dictionary<int, string>();
            var samples = new List<object>();
            var lastSeen = new Dictionary<int, float>();
            var maxAge = new Dictionary<int, int>();
            float started = Time.time;
            foreach (var entry in cases)
            {
                // Empty space isolates age expiry from terrain, hits and piercing depletion.
                // This uses the actual live StepGem/Step path, not a simulated countdown.
                var staff = ItemDb.All.First(d => d != null && d.Shoot == entry.Item2 && d.Type.ToString().EndsWith("Staff"));
                float speed = staff.ShootSpeed;
                if ((entry.Item3 & TerraProjectiles.GemFx.FastSlow) != 0) speed *= 5;
                if ((entry.Item3 & TerraProjectiles.GemFx.Bounce) != 0) speed *= 1.25f;
                tp.ShootGem(entry.Item2, new Vector2(10000, 10000 + ids.Count * 10), Vector2.right * speed, 1, 0, 0, 0, entry.Item3);
                var p = tp.DevGemTrace().Last();
                ids[p.Id] = entry.Item1;
                Check("initial-lifetime/" + entry.Item1, p.TimeLeft == entry.Item4);
            }
            tp.DevGemIgnoreTerrainForAudit();
            while (Time.time - started < 11f)
            {
                foreach (var p in tp.DevGemTrace()) {
                    if (!ids.ContainsKey(p.Id)) continue;
                    float t = Time.time - started;
                    lastSeen[p.Id] = t; maxAge[p.Id] = p.Age;
                    samples.Add(new { Case = ids[p.Id], Seconds = t, p.Age, p.TimeLeft, X = p.Position.x, Y = p.Position.y });
                }
                yield return new WaitForSeconds(.1f);
            }
            foreach (var pair in ids) {
                Check("single-shot-expires/" + pair.Value, !tp.DevGemTrace().Any(p => p.Id == pair.Key));
                bool fast = pair.Value.Contains("emerald");
                Check("aged-before-expiry/" + pair.Value, maxAge.TryGetValue(pair.Key, out int age) && age >= (fast ? 180 : 285));
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "early-magic-tracking.json"), JsonConvert.SerializeObject(new {
                Checks = checks, Failures = failures,
                Scope = "Real live projectile ageing in empty space with terrain collision disabled for these fixture shots only; real NearestEnemy against a live, dead, inactive, out-of-range and wall-occluded dummy. Not a claim about all homing projectile families.",
                Lifetimes = ids.Select(p => new { Case = p.Value, LastSeenSeconds = lastSeen.TryGetValue(p.Key, out float t) ? t : -1, LastAge = maxAge.TryGetValue(p.Key, out int a) ? a : -1 }), Samples = samples
            }, Formatting.Indented));
        }
    }
}
