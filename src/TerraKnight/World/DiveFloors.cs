using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The floors the Knight breaks with Desolate Dive (the quake_floor FSM: it turns to a trigger when
    /// a dive starts and breaks when the diving Knight passes into it; a Detect Quake FSM beside some
    /// others), and the floors broken by striking them (break_floor). There is no dive here: the Ram
    /// Rune's slam breaks the one it lands on (it comes with Desolate Dive), a bomb's blast breaks
    /// them, and a pickaxe swung at one in reach chips at it, pick power until <see cref="Toughness"/>
    /// breaks it.
    /// </summary>
    internal static class DiveFloors
    {
        /// <summary>The pick power a dive floor takes before it breaks: a Copper Pickaxe's six swings.</summary>
        const int Toughness = 200;
        const float Cell = CellGrid.CellSize;
        static readonly Dictionary<int, int> damage = new Dictionary<int, int>();

        public static int DevMined, DevBroken;

        static bool IsDive(PlayMakerFSM fsm) => fsm.FsmName == "quake_floor" || fsm.FsmName == "Detect Quake";

        /// <summary>Breaks one: what the dive would do to it. True when it was a floor that breaks.</summary>
        public static bool Break(PlayMakerFSM fsm)
        {
            if (fsm == null) return false;
            if (fsm.FsmName == "quake_floor")
            {
                fsm.SendEvent("QUAKE FALL START");
                fsm.SendEvent("DESTROY");
            }
            else if (fsm.FsmName == "break_floor" && fsm.Fsm.GetState("Break") != null && fsm.ActiveStateName != "Break") fsm.SetState("Break");
            else if (fsm.FsmName == "Detect Quake")
            {
                // Crystal Peak's loose floors: on contact it asks the Knight whether he is diving (Check Quake,
                // HeroController.GetState spellQuake), which is never so here; past that, to its hit, which breaks
                // the floor its own way. The Knight is who the hit sounds from.
                var hero = HeroController.SilentInstance;
                foreach (var name in new[] { "Hero", "Hero Obj" })
                {
                    var v = fsm.FsmVariables.FindFsmGameObject(name);
                    if (v != null && hero != null) v.Value = hero.gameObject;
                }
                if (fsm.Fsm.GetState("Hit Pause") != null)
                {
                    if (fsm.ActiveStateName != "Detect" && fsm.ActiveStateName != "Init" && fsm.ActiveStateName != "Check Quake" && fsm.ActiveStateName != "Pause") return false;
                    fsm.SetState("Hit Pause");
                }
                else fsm.SendEvent("QUAKE");
            }
            else return false;
            DevBroken++;
            return true;
        }

        /// <summary>Every floor of these in a blast: how many broke.</summary>
        public static int BreakAround(Vector2 center, float radius)
        {
            int n = 0;
            var seen = new HashSet<PlayMakerFSM>();
            foreach (var col in Physics2D.OverlapCircleAll(center, radius))
                foreach (var fsm in col.GetComponentsInParent<PlayMakerFSM>())
                    if (seen.Add(fsm) && Break(fsm)) n++;
            return n;
        }

        /// <summary>
        /// The Ram Rune's slam landing (the player's hitbox): a dive floor right under its feet breaks,
        /// as the Knight's dive breaks it, and the player falls on through. How many broke.
        /// </summary>
        public static int Pound(Bounds hero)
        {
            int n = 0;
            var seen = new HashSet<PlayMakerFSM>();
            var at = new Vector2(hero.center.x, hero.min.y - 0.25f);
            foreach (var col in Physics2D.OverlapBoxAll(at, new Vector2(hero.size.x + 0.2f, 0.6f), 0f))
                foreach (var fsm in col.GetComponentsInParent<PlayMakerFSM>())
                    if (IsDive(fsm) && seen.Add(fsm) && Break(fsm)) n++;
            if (n > 0)
            {
                DevPounded += n;
                Plugin.Log.LogInfo($"[下砸地板] 重锤符文的猛击砸开了脚下 {n} 块下砸地板");
            }
            return n;
        }

        public static int DevPounded;

        /// <summary>TileReachCheckSettings.Simple: the player's tiles, 5 more across and 4 more up and down.</summary>
        static Rect Reach(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            return Rect.MinMaxRect(b.min.x - 5f * Cell, b.min.y - 4f * Cell, b.max.x + 5f * Cell, b.max.y + 4f * Cell);
        }

        /// <summary>A pickaxe swung at a dive floor in reach: true when the swing was at one.</summary>
        public static bool TryMine(ItemDef pick, Vector2 mouse, HeroController hero)
        {
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            var reach = Reach(hero);
            foreach (var col in Physics2D.OverlapPointAll(mouse))
            {
                if (col == null || !col.enabled) continue;
                var b = col.bounds;
                if (!reach.Overlaps(new Rect(b.min.x, b.min.y, b.size.x, b.size.y))) continue;
                foreach (var fsm in col.GetComponentsInParent<PlayMakerFSM>())
                {
                    if (!IsDive(fsm)) continue;
                    int id = fsm.GetInstanceID();
                    damage.TryGetValue(id, out int d);
                    d += pick.PickPower;
                    TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                    Dust(mouse, d >= Toughness ? 10 : 3);
                    DevMined++;
                    if (d < Toughness)
                    {
                        damage[id] = d;
                        return true;
                    }
                    damage.Remove(id);
                    Break(fsm);
                    Plugin.Log.LogInfo($"[下砸地板] {fsm.gameObject.scene.name} 的 {fsm.gameObject.name} 被镐子挖开");
                    return true;
                }
            }
            return false;
        }

        static void Dust(Vector2 at, int n)
        {
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            var px = Effects.TerraParticles.ToPx(at);
            // KillTile_MakeTileDust: stone's dust is 1.
            for (int k = 0; k < n; k++) p.NewDust(px - new Vector2(16f, 16f), 32, 32, 1);
        }
    }
}
