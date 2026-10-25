using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        static readonly HashSet<string> fkDown = new HashSet<string>
        {
            "Stun Start", "Check Direction", "Stun Turn L", "Stun Turn R", "Stun In Air", "Stun Land", "Roll End",
            "Pause Short", "Pause Long", "Check If GG", "Open Uuup", "Head Reset", "Opened", "Hit",
        };

        /// <summary>
        /// The False Knight's rounds with the new AI (the user's 10-23: no barrels, slid along after his fall, his head never
        /// out): his shell struck until he falls, his head struck until he gets up, then his rage. Each round whether his head
        /// came out, how far he moved lying still, whether he got up and raged, how many barrels fell, whether his script
        /// started over and whether the watchdog moved him. One [FK_FLOW] line a round.
        /// </summary>
        IEnumerator FalseKnightFlow()
        {
            if (!DevCopy.Verified) throw new System.InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            PlayerData.instance.isInvincible = false;
            BossArena.Fight("FalseKnight", true);
            BossKit kit = null;
            float until = Time.realtimeSinceStartup + 70f;
            while (Time.realtimeSinceStartup < until && (kit == null || GameManager.instance.IsInSceneTransition))
            {
                kit = KitOf("FalseKnight");
                yield return null;
            }
            if (kit == null) { Plugin.Log.LogInfo("[FK_FLOW] no kit"); yield break; }
            var control = (PlayMakerFSM)AccessTools.Field(typeof(BossKit), "Control").GetValue(kit);
            var hero = HeroController.instance;
            var hbody = hero.GetComponent<Rigidbody2D>();
            void Beside()
            {
                var at = kit.ClampToArena(kit.Me, 3f);
                float side = at.x - 6f > kit.Arena.xMin + 1f ? -6f : 6f;
                hero.transform.position = new Vector3(at.x + side, kit.GroundBelow(at) + 1.2f, hero.transform.position.z);
                if (hbody != null) hbody.linearVelocity = Vector2.zero;
            }
            // The fight under way.
            until = Time.time + 20f;
            while (Time.time < until && kit.StateNow != "Idle") yield return null;
            Plugin.Log.LogInfo($"[FK_FLOW] fight on: {kit.StateNow} scene={GameManager.instance.sceneName}");
            PlayMakerFSM summon = null;
            var summoner = kit.Go("Barrel Summoner")?.Value;
            if (summoner != null) summon = summoner.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "summon");
            var head = kit.Go("Head")?.Value;
            var headHm = head != null ? head.GetComponent<HealthManager>() : null;
            Plugin.Log.LogInfo($"[FK_FLOW] summoner={(summon != null)} head={(headHm != null)}");
            // A block set down in his room is the room's area platform (World.BossRooms.Placed).
            {
                var cell = World.CellGrid.WorldToCell((Vector2)hero.transform.position + Vector2.up * 2f);
                var kind = World.BossRooms.Placed(cell.x, cell.y, World.CellKind.Stone);
                Plugin.Log.LogInfo($"[FK_FLOW] stone placed in his room becomes {kind} (platform {World.Cells.IsPlatform(kind)})");
            }
            // His slams' waves both ways: the player on his left a while, then on his right.
            foreach (float sideSign in new[] { -1f, 1f })
                for (float t = 0f; t < 30f; t += Time.deltaTime)
                {
                    var at = kit.ClampToArena(kit.Me, 3f);
                    float x = Mathf.Clamp(at.x + sideSign * 6f, kit.Arena.xMin + 2f, kit.Arena.xMax - 2f);
                    hero.transform.position = new Vector3(x, kit.GroundBelow(new Vector2(x, at.y)) + 1.2f, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    yield return null;
                }
            for (int round = 1; round <= 2; round++)
            {
                int unstuck0 = BossAi.DevUnstuck;
                bool stunned = false, headOut = false, recovered = false, rage = false, reset = false;
                int barrels = 0;
                float slide = 0f;
                var seen = new List<string>();
                string last = null, barrelName = null;
                var up = new HashSet<int>();
                float off = 0f;
                void Note()
                {
                    string s = kit.StateNow;
                    if (s != last) { last = s; if (seen.Count < 60) seen.Add(s); }
                    if (s == "Dormant" || s == "Init" || s == "Start Fall") reset = true;
                    if (s == "Rage" || s == "Turn") rage = true;
                    // Barrels: each one coming up (the pool's reused, so by its coming, not its object), and how far from
                    // the player it falls.
                    var now = new HashSet<int>();
                    foreach (var b in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                    {
                        string n = b.gameObject.name;
                        if (!n.Contains("Barrel") || n.Contains("Summon")) continue;
                        int id = b.GetInstanceID();
                        now.Add(id);
                        if (up.Contains(id)) continue;
                        barrels++;
                        off += Mathf.Abs(b.position.x - hero.transform.position.x);
                        if (barrelName == null) barrelName = n;
                    }
                    up = now;
                }
                // His shell struck until he falls.
                Beside();
                until = Time.time + 30f;
                float nextHit = 0f;
                while (Time.time < until && !fkDown.Contains(kit.StateNow ?? ""))
                {
                    Note();
                    if (Time.time >= nextHit)
                    {
                        nextHit = Time.time + 0.15f;
                        kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 40, Direction = kit.Me.x > hero.transform.position.x ? 0f : 180f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                    }
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    yield return null;
                }
                stunned = fkDown.Contains(kit.StateNow ?? "");
                // Down: where he lies once his roll is over, and his head out.
                float lyingX = float.NaN;
                until = Time.time + 10f;
                while (stunned && Time.time < until && kit.StateNow != "Opened")
                {
                    Note();
                    if (kit.StateNow == "Roll End" || kit.StateNow == "Pause Short" || kit.StateNow == "Pause Long" || kit.StateNow == "Open Uuup")
                    {
                        if (float.IsNaN(lyingX)) lyingX = kit.transform.position.x;
                        slide = Mathf.Max(slide, Mathf.Abs(kit.transform.position.x - lyingX));
                    }
                    yield return null;
                }
                headOut = kit.StateNow == "Opened";
                // His head struck until he gets up.
                until = Time.time + 12f;
                while (headOut && Time.time < until && (kit.StateNow == "Opened" || kit.StateNow == "Hit"))
                {
                    Note();
                    if (!float.IsNaN(lyingX)) slide = Mathf.Max(slide, Mathf.Abs(kit.transform.position.x - lyingX));
                    if (Time.time >= nextHit && headHm != null)
                    {
                        nextHit = Time.time + 0.15f;
                        headHm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Nail, DamageDealt = 40, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                    }
                    yield return null;
                }
                recovered = kit.StateNow == "Recover" || seen.Contains("Recover");
                // His rage, and its barrels.
                until = Time.time + 14f;
                while (Time.time < until && !(rage && kit.StateNow == "Idle"))
                {
                    Note();
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    yield return null;
                }
                // And the barrels after it (the summoner's last drops).
                for (float t = 0f; t < 2f; t += Time.deltaTime) { Note(); yield return null; }
                Plugin.Log.LogInfo($"[FK_FLOW] round={round} stunned={stunned} headOut={headOut} slide={slide:F2} recovered={recovered} rage={rage} barrels={barrels} ({barrelName}, {(barrels > 0 ? off / barrels : 0f):F1} from him) reset={reset} unstuck={BossAi.DevUnstuck - unstuck0} hp={kit.Hm.hp} head={(headHm != null ? headHm.hp : -1)} states={string.Join(",", seen)}");
            }
            var w=EnemyAi.Bosses.FalseKnightKit.DevWaveEnds;
            Plugin.Log.LogInfo($"[FK_FLOW] waves left: floor {w[0,0]} wall {w[0,1]} ceiling {w[0,2]} far wall {w[0,3]}; right: floor {w[1,0]} wall {w[1,1]} ceiling {w[1,2]} far wall {w[1,3]}");
            Plugin.Log.LogInfo("[NEWAI_DONE]");
        }
    }
}
